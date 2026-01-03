"""Скачать модель для fine-tuning"""
import os
from pathlib import Path
from huggingface_hub import snapshot_download

def main():
    model_id = "unsloth/Qwen3-8B-unsloth-bnb-4bit"
    local_dir = Path("models/Qwen3-8B-unsloth-bnb-4bit")
    
    print(f"Downloading model: {model_id}")
    print(f"Destination: {local_dir.absolute()}")
    print("This may take a while...")
    print()
    
    local_dir.mkdir(parents=True, exist_ok=True)
    
    try:
        snapshot_download(
            repo_id=model_id,
            local_dir=str(local_dir),
            local_dir_use_symlinks=False,
            resume_download=True,
        )
        print(f"\nSUCCESS: Model downloaded to {local_dir.absolute()}")
    except Exception as e:
        print(f"\nERROR: Failed to download model: {e}")
        return 1
    
    return 0

if __name__ == '__main__':
    exit(main())

