#!/usr/bin/env python3
"""Export fine-tuned model - merge LoRA and save in HuggingFace format"""

import os
os.environ["TOKENIZERS_PARALLELISM"] = "false"

def main():
    print("=" * 80)
    print("EXPORTING MERGED MODEL")
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
    
    output_dir = "models/bannerlord-lore-merged"
    
    print(f"\nMerging LoRA weights and saving to {output_dir}...")
    print("This may take several minutes...")
    
    # Save merged model in 16-bit format
    model.save_pretrained_merged(
        output_dir,
        tokenizer,
        save_method="merged_16bit",
    )
    
    print("\n" + "=" * 80)
    print("MERGE COMPLETED!")
    print("=" * 80)
    print(f"\nMerged model saved to: {output_dir}")
    
    print("\n" + "=" * 80)
    print("NEXT STEPS FOR OLLAMA:")
    print("=" * 80)
    print("\nOption 1: Use llama.cpp to convert to GGUF")
    print("  1. Clone llama.cpp: git clone https://github.com/ggerganov/llama.cpp")
    print("  2. Run: python llama.cpp/convert_hf_to_gguf.py models/bannerlord-lore-merged")
    print("  3. Quantize: llama.cpp/llama-quantize model.gguf model-q4_k_m.gguf q4_k_m")
    print("\nOption 2: Upload to HuggingFace and use Ollama directly")
    print("  ollama run hf.co/your-username/bannerlord-lore")
    print("=" * 80)

if __name__ == "__main__":
    main()

