#!/usr/bin/env python3
"""Test the fine-tuned Bannerlord lore model"""

import os
os.environ["TOKENIZERS_PARALLELISM"] = "false"

def main():
    print("=" * 80)
    print("TESTING FINE-TUNED BANNERLORD LORE MODEL")
    print("=" * 80)
    
    from unsloth import FastLanguageModel
    
    # Load the fine-tuned model
    print("\nLoading model from outputs_full/...")
    model, tokenizer = FastLanguageModel.from_pretrained(
        "outputs_full",
        max_seq_length=2048,
        dtype=None,
        load_in_4bit=True,
    )
    FastLanguageModel.for_inference(model)
    print("Model loaded!")
    
    # Test questions about Bannerlord lore
    questions = [
        "Tell me about the Battanian faction and their culture",
        "Who was Emperor Neretzes and what happened to him?",
        "Describe the Khuzait Khanate and their way of life",
    ]
    
    print("\n" + "=" * 80)
    print("TESTING WITH 3 LORE QUESTIONS")
    print("=" * 80)
    
    for i, question in enumerate(questions, 1):
        print(f"\n{'='*80}")
        print(f"QUESTION {i}: {question}")
        print("=" * 80)
        
        # Format the prompt
        prompt = f"### Instruction:\n{question}\n\n### Response:\n"
        
        inputs = tokenizer(prompt, return_tensors="pt").to("cuda")
        
        # Generate response
        outputs = model.generate(
            **inputs,
            max_new_tokens=300,
            temperature=0.7,
            top_p=0.9,
            do_sample=True,
            pad_token_id=tokenizer.eos_token_id,
        )
        
        response = tokenizer.decode(outputs[0], skip_special_tokens=True)
        
        # Extract only the response part
        if "### Response:" in response:
            response = response.split("### Response:")[-1].strip()
        
        print(f"\nRESPONSE:\n{response}")
        print("-" * 80)
    
    print("\n" + "=" * 80)
    print("TESTING COMPLETE!")
    print("=" * 80)

if __name__ == "__main__":
    main()

