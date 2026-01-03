#!/usr/bin/env python3
"""Export fine-tuned model for Ollama - forced 4bit merge"""

import os
os.environ["TOKENIZERS_PARALLELISM"] = "false"

def main():
    print("=" * 80)
    print("EXPORTING MODEL FOR OLLAMA (4-bit merged)")
    print("=" * 80)
    
    from unsloth import FastLanguageModel
    
    # Load the fine-tuned model
    print("\nLoading fine-tuned model from outputs_full/...")
    model, tokenizer = FastLanguageModel.from_pretrained(
        "outputs_full",
        max_seq_length=2048,
        dtype=None,
        load_in_4bit=True,
    )
    print("Model loaded!")
    
    output_dir = "models/bannerlord-lore-4bit"
    
    print(f"\nSaving merged 4-bit model to {output_dir}...")
    print("This may take several minutes...")
    
    # Save with forced_merged_4bit method (works with 4-bit base)
    model.save_pretrained_merged(
        output_dir,
        tokenizer,
        save_method="merged_4bit_forced",
    )
    
    print("\n" + "=" * 80)
    print("EXPORT COMPLETED!")
    print("=" * 80)
    print(f"\nModel saved to: {output_dir}")
    
    # Check what files were created
    import os
    print("\nFiles created:")
    for f in os.listdir(output_dir):
        size = os.path.getsize(os.path.join(output_dir, f))
        print(f"  {f}: {size / 1024 / 1024:.2f} MB")

if __name__ == "__main__":
    main()

