#!/usr/bin/env python3
"""
Fine-tuning script using Unsloth
Based on official guide: https://unsloth.ai/docs/get-started/fine-tuning-llms-guide
"""

# Fix for Windows - must be at the very top before any imports
import os
os.environ["TOKENIZERS_PARALLELISM"] = "false"

import json
import argparse
from pathlib import Path

# Fix for Unsloth psutil bug
import psutil
import builtins
builtins.psutil = psutil

def main():
    parser = argparse.ArgumentParser(description='Fine-tune model with Unsloth')
    parser.add_argument('--model_name', type=str, 
                       default='models/Qwen3-8B-unsloth-bnb-4bit',
                       help='Model name from HuggingFace or local path')
    parser.add_argument('--dataset_path', type=str,
                       default='finetuning_data/unsloth_training_dataset.json',
                       help='Path to training dataset JSON file')
    parser.add_argument('--output_dir', type=str, default='outputs',
                       help='Output directory for trained model')
    parser.add_argument('--max_seq_length', type=int, default=2048,
                       help='Maximum sequence length')
    parser.add_argument('--batch_size', type=int, default=2,
                       help='Training batch size per device')
    parser.add_argument('--gradient_accumulation_steps', type=int, default=4,
                       help='Gradient accumulation steps')
    parser.add_argument('--max_steps', type=int, default=500,
                       help='Maximum training steps')
    parser.add_argument('--learning_rate', type=float, default=2e-4,
                       help='Learning rate')
    parser.add_argument('--warmup_steps', type=int, default=50,
                       help='Warmup steps')
    parser.add_argument('--save_steps', type=int, default=100,
                       help='Save checkpoint every N steps')
    parser.add_argument('--lora_r', type=int, default=16,
                       help='LoRA rank')
    parser.add_argument('--lora_alpha', type=int, default=16,
                       help='LoRA alpha')
    
    args = parser.parse_args()
    
    print("=" * 80)
    print("UNSLOTH FINE-TUNING SCRIPT")
    print("Based on: https://unsloth.ai/docs/get-started/fine-tuning-llms-guide")
    print("=" * 80)
    print(f"Model: {args.model_name}")
    print(f"Dataset: {args.dataset_path}")
    print(f"Output: {args.output_dir}")
    print("=" * 80)
    
    # Import unsloth
    from unsloth import FastLanguageModel
    from transformers import TrainingArguments, Trainer
    from datasets import Dataset
    import torch
    
    # Load dataset
    dataset_path = Path(args.dataset_path)
    if not dataset_path.exists():
        print(f"ERROR: Dataset not found: {dataset_path}")
        return
    
    print(f"\nLoading dataset from {dataset_path}...")
    with open(dataset_path, 'r', encoding='utf-8') as f:
        data = json.load(f)
    
    print(f"Loaded {len(data)} entries")
    
    # Load model
    print(f"\nLoading model {args.model_name}...")
    model, tokenizer = FastLanguageModel.from_pretrained(
        model_name=args.model_name,
        max_seq_length=args.max_seq_length,
        dtype=None,
        load_in_4bit=True,
    )
    
    # Configure LoRA
    print(f"\nConfiguring LoRA (r={args.lora_r}, alpha={args.lora_alpha})...")
    model = FastLanguageModel.get_peft_model(
        model,
        r=args.lora_r,
        target_modules=[
            "q_proj", "k_proj", "v_proj", "o_proj",
            "gate_proj", "up_proj", "down_proj",
        ],
        lora_alpha=args.lora_alpha,
        lora_dropout=0,
        bias="none",
        use_gradient_checkpointing="unsloth",
        random_state=3407,
    )
    
    # Format and tokenize data manually (avoiding multiprocessing issues)
    print("\nFormatting and tokenizing dataset (this may take a few minutes)...")
    
    tokenized_data = []
    EOS_TOKEN = tokenizer.eos_token
    
    for i, item in enumerate(data):
        if i % 500 == 0:
            print(f"  Processing {i}/{len(data)}...")
        
        instruction = item.get("instruction", "")
        input_text = item.get("input", "")
        output = item.get("output", "")
        
        if input_text:
            text = f"### Instruction:\n{instruction}\n\n### Input:\n{input_text}\n\n### Response:\n{output}"
        else:
            text = f"### Instruction:\n{instruction}\n\n### Response:\n{output}"
        
        text = text + EOS_TOKEN
        
        # Tokenize
        tokenized = tokenizer(
            text,
            truncation=True,
            max_length=args.max_seq_length,
            padding=False,
            return_tensors=None,
        )
        tokenized["labels"] = tokenized["input_ids"].copy()
        tokenized_data.append(tokenized)
    
    dataset = Dataset.from_list(tokenized_data)
    print(f"Dataset tokenized: {len(dataset)} examples")
    
    # Data collator
    from transformers import DataCollatorForSeq2Seq
    data_collator = DataCollatorForSeq2Seq(
        tokenizer=tokenizer,
        padding=True,
        return_tensors="pt",
    )
    
    # Create trainer using basic Trainer (avoiding SFTTrainer multiprocessing issues)
    print(f"\nStarting training...")
    trainer = Trainer(
        model=model,
        tokenizer=tokenizer,
        train_dataset=dataset,
        data_collator=data_collator,
        args=TrainingArguments(
            per_device_train_batch_size=args.batch_size,
            gradient_accumulation_steps=args.gradient_accumulation_steps,
            warmup_steps=args.warmup_steps,
            max_steps=args.max_steps,
            learning_rate=args.learning_rate,
            fp16=not torch.cuda.is_bf16_supported(),
            bf16=torch.cuda.is_bf16_supported(),
            logging_steps=10,
            optim="adamw_8bit",
            weight_decay=0.01,
            lr_scheduler_type="linear",
            seed=3407,
            output_dir=args.output_dir,
            save_strategy="steps",
            save_steps=args.save_steps,
            report_to="none",
            remove_unused_columns=False,
        ),
    )
    
    # Train
    print("\n" + "=" * 80)
    print("TRAINING STARTED")
    print("=" * 80)
    trainer.train()
    
    # Save model
    print(f"\nSaving model to {args.output_dir}...")
    model.save_pretrained(args.output_dir)
    tokenizer.save_pretrained(args.output_dir)
    
    print("\n" + "=" * 80)
    print("TRAINING COMPLETED!")
    print("=" * 80)
    print(f"Model saved to: {args.output_dir}")
    
    print("\nTo use the model:")
    print(f"from unsloth import FastLanguageModel")
    print(f"model, tokenizer = FastLanguageModel.from_pretrained('{args.output_dir}')")
    print(f"FastLanguageModel.for_inference(model)")

if __name__ == '__main__':
    main()
