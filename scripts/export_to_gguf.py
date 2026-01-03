#!/usr/bin/env python3
"""Export fine-tuned model to GGUF format for Ollama"""

import os
os.environ["TOKENIZERS_PARALLELISM"] = "false"

def main():
    print("=" * 80)
    print("EXPORTING MODEL TO GGUF FORMAT FOR OLLAMA")
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
    
    # Export to GGUF
    output_dir = "models/bannerlord-lore-gguf"
    
    print(f"\nExporting to GGUF format...")
    print(f"Output directory: {output_dir}")
    print("This may take several minutes...")
    
    # Save as GGUF with different quantization options
    # Q4_K_M is a good balance between size and quality
    model.save_pretrained_gguf(
        output_dir,
        tokenizer,
        quantization_method="q4_k_m",  # Good balance of size/quality
    )
    
    print("\n" + "=" * 80)
    print("EXPORT COMPLETED!")
    print("=" * 80)
    print(f"\nGGUF file saved to: {output_dir}")
    
    # Create Modelfile for Ollama
    modelfile_content = '''FROM ./unsloth.Q4_K_M.gguf

TEMPLATE """{{ if .System }}<|im_start|>system
{{ .System }}<|im_end|>
{{ end }}{{ if .Prompt }}<|im_start|>user
{{ .Prompt }}<|im_end|>
{{ end }}<|im_start|>assistant
{{ .Response }}<|im_end|>
"""

PARAMETER temperature 0.7
PARAMETER top_p 0.9
PARAMETER stop "<|im_end|>"

SYSTEM """You are an expert on Mount & Blade II: Bannerlord lore. You have deep knowledge of the game's factions, characters, history, and world. Answer questions about Calradia, its kingdoms, clans, and historical events accurately and in detail."""
'''
    
    modelfile_path = os.path.join(output_dir, "Modelfile")
    with open(modelfile_path, "w", encoding="utf-8") as f:
        f.write(modelfile_content)
    
    print(f"Modelfile created: {modelfile_path}")
    
    print("\n" + "=" * 80)
    print("TO USE IN OLLAMA:")
    print("=" * 80)
    print(f"\n1. Navigate to the output directory:")
    print(f"   cd {os.path.abspath(output_dir)}")
    print(f"\n2. Create the Ollama model:")
    print(f"   ollama create bannerlord-lore -f Modelfile")
    print(f"\n3. Run the model:")
    print(f"   ollama run bannerlord-lore")
    print("=" * 80)

if __name__ == "__main__":
    main()

