#!/usr/bin/env python3
"""Upload model to HuggingFace Hub"""

from huggingface_hub import HfApi, login
import os

def main():
    print("=" * 80)
    print("UPLOAD MODEL TO HUGGINGFACE")
    print("=" * 80)
    
    # Get HuggingFace token
    print("\n1. Go to: https://huggingface.co/settings/tokens")
    print("2. Create a new token with WRITE access")
    print("3. Paste it below:\n")
    
    token = input("Enter your HuggingFace token: ").strip()
    if not token:
        print("No token provided. Exiting.")
        return
    
    # Login
    login(token=token)
    print("Logged in successfully!")
    
    # Get username
    api = HfApi()
    user_info = api.whoami()
    username = user_info["name"]
    print(f"Username: {username}")
    
    # Repository name
    repo_name = "bannerlord-lore-qwen3-8b"
    repo_id = f"{username}/{repo_name}"
    
    print(f"\nUploading to: {repo_id}")
    print("This may take several minutes (model is ~7 GB)...")
    
    # Create repo if not exists
    try:
        api.create_repo(repo_id=repo_id, exist_ok=True, private=False)
        print(f"Repository created: https://huggingface.co/{repo_id}")
    except Exception as e:
        print(f"Note: {e}")
    
    # Upload the model
    model_path = "models/bannerlord-lore-4bit"
    
    api.upload_folder(
        folder_path=model_path,
        repo_id=repo_id,
        commit_message="Upload Bannerlord Lore fine-tuned Qwen3-8B model",
    )
    
    print("\n" + "=" * 80)
    print("UPLOAD COMPLETE!")
    print("=" * 80)
    print(f"\nModel URL: https://huggingface.co/{repo_id}")
    print(f"\nNext steps:")
    print(f"1. Go to: https://huggingface.co/spaces/ggml-org/gguf-my-repo")
    print(f"2. Enter model: {repo_id}")
    print(f"3. Select quantization: Q4_K_M")
    print(f"4. Click Submit and wait for conversion")
    print(f"5. Download the GGUF file")
    print("=" * 80)

if __name__ == "__main__":
    main()

