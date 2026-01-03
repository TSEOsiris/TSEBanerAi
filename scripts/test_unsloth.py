"""Test script to verify Unsloth installation and CUDA support"""
import sys

try:
    import unsloth
    print("[OK] Unsloth imported successfully")
    print(f"Unsloth version: {unsloth.__version__ if hasattr(unsloth, '__version__') else 'unknown'}")
except ImportError as e:
    print(f"[ERROR] Failed to import unsloth: {e}")
    sys.exit(1)

try:
    from unsloth import FastLanguageModel
    print("[OK] FastLanguageModel imported successfully")
except ImportError as e:
    print(f"[ERROR] Failed to import FastLanguageModel: {e}")
    sys.exit(1)

try:
    import torch
    print(f"\nPyTorch version: {torch.__version__}")
    print(f"CUDA available: {torch.cuda.is_available()}")
    
    if torch.cuda.is_available():
        print("[OK] CUDA is working!")
        print(f"GPU: {torch.cuda.get_device_name(0)}")
    else:
        print("[ERROR] CUDA is NOT available - Unsloth will not work!")
        sys.exit(1)
except Exception as e:
    print(f"[ERROR] Error checking CUDA: {e}")
    sys.exit(1)

print("\n[OK] All checks passed! Unsloth is ready to use.")

