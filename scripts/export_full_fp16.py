#!/usr/bin/env python3
"""Export fine-tuned model with full dequantization to FP16"""

import torch
import os
from unsloth import FastLanguageModel

def main():
    print("=" * 80)
    print("EXPORTING MODEL WITH FULL DEQUANTIZATION")
    print("=" * 80)
    
    # Load fine-tuned model  
    model_path = 'outputs_full'
    print(f"\nLoading fine-tuned model from {model_path}...")
    model, tokenizer = FastLanguageModel.from_pretrained(model_path)
    print("Model loaded!")
    
    # Try to get the merged model
    output_dir = "models/bannerlord-lore-fp16-full"
    os.makedirs(output_dir, exist_ok=True)
    
    print(f"\nAttempting full FP16 export to {output_dir}...")
    print("Method: lora_model_for_inference merge")
    
    try:
        # Method 1: Use Unsloth's built-in GGUF export (just save locally first)
        model.save_pretrained_merged(
            output_dir,
            tokenizer,
            save_method="lora",  # Save just LoRA weights first
        )
        print("LoRA weights saved!")
        
    except Exception as e:
        print(f"Method 1 failed: {e}")
        
    # Method 2: Try forced_merged_16bit
    print("\nTrying forced_merged_16bit method...")
    try:
        model.save_pretrained_merged(
            output_dir,
            tokenizer,
            save_method="forced_merged_16bit",
        )
        print("Forced 16bit merge completed!")
    except Exception as e:
        print(f"Method 2 failed: {e}")
    
    print("\n" + "=" * 80)
    print("EXPORT ATTEMPT COMPLETED")
    print("=" * 80)

if __name__ == "__main__":
    main()

