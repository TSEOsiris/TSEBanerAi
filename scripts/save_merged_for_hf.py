#!/usr/bin/env python3
"""Save merged model for HuggingFace GGUF conversion"""

import os
os.environ["TOKENIZERS_PARALLELISM"] = "false"

import gc
import torch

def main():
    print("=" * 60)
    print("SAVING MERGED MODEL FOR HUGGINGFACE")
    print("=" * 60)
    
    from unsloth import FastLanguageModel
    
    # Load base model
    print("\nLoading base model...")
    model, tokenizer = FastLanguageModel.from_pretrained(
        model_name="unsloth/Qwen3-8B",
        max_seq_length=2048,
        load_in_4bit=True,
    )
    print("Base model loaded!")
    
    # Load LoRA
    print("\nApplying LoRA adapter...")
    from peft import PeftModel
    model = PeftModel.from_pretrained(model, "outputs_v2")
    print("LoRA applied!")
    
    # Save merged model
    output_dir = "models/bannerlord-merged-v2"
    print(f"\nSaving merged model to {output_dir}...")
    print("This may take a few minutes...")
    
    model.save_pretrained_merged(
        output_dir,
        tokenizer,
        save_method="merged_16bit",
    )
    
    print("\n" + "=" * 60)
    print("DONE!")
    print("=" * 60)
    print(f"Model saved to: {output_dir}")
    print("\nNext: Upload to HuggingFace and use GGUF converter")

if __name__ == "__main__":
    main()

