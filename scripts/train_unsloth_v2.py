#!/usr/bin/env python3
"""
Fine-tuning script using Unsloth v2
Uses clean base model for proper GGUF export
"""

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
    parser = argparse.ArgumentParser(description='Fine-tune model with Unsloth v2')
    parser.add_argument('--model_name', type=str, 
                       default='unsloth/Qwen3-8B',  # Clean model, not pre-quantized!
                       help='Model name from HuggingFace')
    parser.add_argument('--dataset_path', type=str,
                       default='finetuning_data/unsloth_training_dataset.json',
                       help='Path to training dataset JSON file')
    parser.add_argument('--output_dir', type=str, default='outputs_v2',
                       help='Output directory for trained model')
    parser.add_argument('--max_seq_length', type=int, default=2048,
                       help='Maximum sequence length')
    parser.add_argument('--batch_size', type=int, default=2,
                       help='Training batch size per device')
    parser.add_argument('--gradient_accumulation_steps', type=int, default=4,
                       help='Gradient accumulation steps')
    parser.add_argument('--max_steps', type=int, default=2000,
                       help='Maximum training steps')
    parser.add_argument('--learning_rate', type=float, default=2e-4,
                       help='Learning rate')
    parser.add_argument('--warmup_steps', type=int, default=50,
                       help='Warmup steps')
    parser.add_argument('--save_steps', type=int, default=200,
                       help='Save checkpoint every N steps')
    parser.add_argument('--lora_r', type=int, default=16,
                       help='LoRA rank')
    parser.add_argument('--lora_alpha', type=int, default=16,
                       help='LoRA alpha')
    parser.add_argument('--export_gguf', action='store_true',
                       help='Export to GGUF after training')
    
    args = parser.parse_args()
    
    print("=" * 80)
    print("UNSLOTH FINE-TUNING SCRIPT v2")
    print("With proper GGUF export support!")
    print("=" * 80)
    print(f"Model: {args.model_name}")
    print(f"Dataset: {args.dataset_path}")
    print(f"Output: {args.output_dir}")
    print(f"Steps: {args.max_steps}")
    print(f"Export GGUF: {args.export_gguf}")
    print("=" * 80)
    
    # Import unsloth
    from unsloth import FastLanguageModel
    from transformers import TrainingArguments
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
    
    # Load model - CLEAN model with on-the-fly quantization
    print(f"\nLoading model {args.model_name}...")
    print("This downloads the clean model and quantizes it properly...")
    
    model, tokenizer = FastLanguageModel.from_pretrained(
        model_name=args.model_name,
        max_seq_length=args.max_seq_length,
        dtype=None,  # Auto-detect
        load_in_4bit=True,  # Quantize on-the-fly (not bitsandbytes format!)
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
    
    # Format and tokenize data
    print("\nFormatting and tokenizing dataset...")
    
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
        
        tokenized = tokenizer(
            text,
            truncation=True,
            max_length=args.max_seq_length,
            padding=False,
            return_tensors=None,
        )
        tokenized["labels"] = tokenized["input_ids"].copy()
        tokenized_data.append(tokenized)
    
    print(f"Tokenized {len(tokenized_data)} entries")
    
    # Create dataset
    dataset = Dataset.from_list(tokenized_data)
    
    # Training arguments
    print("\nSetting up training...")
    from unsloth import UnslothTrainer
    
    training_args = TrainingArguments(
        output_dir=args.output_dir,
        per_device_train_batch_size=args.batch_size,
        gradient_accumulation_steps=args.gradient_accumulation_steps,
        warmup_steps=args.warmup_steps,
        max_steps=args.max_steps,
        learning_rate=args.learning_rate,
        logging_steps=10,
        save_steps=args.save_steps,
        save_total_limit=5,
        optim="adamw_8bit",
        weight_decay=0.01,
        lr_scheduler_type="linear",
        seed=3407,
        bf16=torch.cuda.is_bf16_supported(),
        fp16=not torch.cuda.is_bf16_supported(),
        report_to="none",
    )
    
    # Create trainer
    trainer = UnslothTrainer(
        model=model,
        tokenizer=tokenizer,
        train_dataset=dataset,
        args=training_args,
    )
    
    # Start training
    print("\n" + "=" * 80)
    print("STARTING TRAINING")
    print("=" * 80)
    
    trainer.train()
    
    print("\n" + "=" * 80)
    print("TRAINING COMPLETED!")
    print("=" * 80)
    
    # Save final model
    print(f"\nSaving model to {args.output_dir}...")
    model.save_pretrained(args.output_dir)
    tokenizer.save_pretrained(args.output_dir)
    
    # Export to GGUF if requested
    if args.export_gguf:
        print("\n" + "=" * 80)
        print("EXPORTING TO GGUF")
        print("=" * 80)
        
        gguf_dir = args.output_dir + "_gguf"
        print(f"Saving GGUF to {gguf_dir}...")
        
        model.save_pretrained_gguf(
            gguf_dir,
            tokenizer,
            quantization_method="q4_k_m"
        )
        
        print(f"\nGGUF exported to: {gguf_dir}")
        
        # List GGUF files
        import glob
        gguf_files = glob.glob(f"{gguf_dir}/*.gguf")
        if gguf_files:
            print(f"GGUF file: {gguf_files[0]}")
            size_gb = os.path.getsize(gguf_files[0]) / (1024**3)
            print(f"Size: {size_gb:.2f} GB")
    
    print("\n" + "=" * 80)
    print("ALL DONE!")
    print("=" * 80)
    print(f"Model saved to: {args.output_dir}")
    if args.export_gguf:
        print(f"GGUF saved to: {args.output_dir}_gguf")
    print("=" * 80)

if __name__ == "__main__":
    main()

