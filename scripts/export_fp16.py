#!/usr/bin/env python3
"""Export fine-tuned model to FP16 format for GGUF conversion"""

import torch
from unsloth import FastLanguageModel

def main():
    print("=" * 80)
    print("EXPORTING MODEL TO FP16 FORMAT")
    print("=" * 80)
    
    # Load fine-tuned model
    model_path = 'outputs_full'
    print(f"\nLoading fine-tuned model from {model_path}...")
    model, tokenizer = FastLanguageModel.from_pretrained(model_path)
    print("Model loaded!")
    
    # Save as merged 16bit model
    output_dir = "models/bannerlord-lore-fp16"
    print(f"\nSaving merged FP16 model to {output_dir}...")
    print("This may take a few minutes...")
    
    model.save_pretrained_merged(
        output_dir,
        tokenizer,
        save_method="merged_16bit",
    )
    
    print("\n" + "=" * 80)
    print("EXPORT COMPLETED!")
    print("=" * 80)
    print(f"\nModel saved to: {output_dir}")
    print("\nNext: Upload this to HuggingFace and convert to GGUF")
    print("=" * 80)

if __name__ == "__main__":
    main()

