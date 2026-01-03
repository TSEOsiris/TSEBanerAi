#!/usr/bin/env python3
"""Test fine-tuned model with 25 lore questions"""

import torch
from unsloth import FastLanguageModel

def main():
    print("=" * 80)
    print("BANNERLORD LORE MODEL - 25 QUESTIONS TEST")
    print("=" * 80)
    
    # Load model
    model_path = 'outputs_full'
    print(f"\nLoading model from {model_path}...")
    model, tokenizer = FastLanguageModel.from_pretrained(model_path)
    FastLanguageModel.for_inference(model)
    print("Model loaded!\n")
    
    # 25 questions about Bannerlord lore
    questions = [
        # Factions
        "What is the Battanian faction known for?",
        "Describe the Khuzait Khanate and their fighting style.",
        "Who are the Vlandians and what is their culture based on?",
        "Tell me about the Aserai and their homeland.",
        "What is the Southern Empire and its history?",
        "Describe the Sturgian people and their origins.",
        
        # Historical figures
        "Who was Emperor Neretzes?",
        "What happened at the Battle of Pendraic?",
        "Who is Rhagaea and what is her role?",
        "Tell me about Caladog, the Battanian king.",
        
        # Minor factions
        "Who are the Wolfskins?",
        "What is the Company of the Golden Boar?",
        "Describe the Brotherhood of the Woods.",
        "Who are the Lake Rats and where do they live?",
        "What is the Legion of the Betrayed?",
        "Tell me about the Jawwal.",
        "Who are the Skolderbroda (Shield Brothers)?",
        "What are the Eleftheroi?",
        "Describe the Hidden Hand organization.",
        "Who are the Ghilman mercenaries?",
        
        # Geography and culture
        "What is Calradia?",
        "Describe the geography of the Nahasa desert.",
        "What happened to the Calradic Empire?",
        "Tell me about the forest people of Battania.",
        "What is the significance of the Dragon Banner?",
    ]
    
    print("Generating responses...\n")
    
    results = []
    for i, question in enumerate(questions, 1):
        print(f"[{i}/25] {question}")
        
        prompt = f"### Instruction:\n{question}\n\n### Response:\n"
        inputs = tokenizer(prompt, return_tensors="pt").to("cuda")
        
        with torch.no_grad():
            outputs = model.generate(
                **inputs,
                max_new_tokens=200,
                use_cache=True,
                temperature=0.7,
                do_sample=True,
                pad_token_id=tokenizer.eos_token_id
            )
        
        response = tokenizer.decode(outputs[0], skip_special_tokens=True)
        answer = response.split("### Response:")[-1].strip()
        
        # Clean up - remove any follow-up prompts
        if "### Instruction:" in answer:
            answer = answer.split("### Instruction:")[0].strip()
        
        results.append((question, answer))
        print(f"   -> {answer[:100]}..." if len(answer) > 100 else f"   -> {answer}")
        print()
    
    # Save results
    print("\n" + "=" * 80)
    print("SAVING FULL RESULTS...")
    print("=" * 80)
    
    with open("test_results_25_questions.txt", "w", encoding="utf-8") as f:
        for i, (q, a) in enumerate(results, 1):
            separator = "=" * 80
            output = f"\n{separator}\nQUESTION {i}: {q}\n{separator}\n\n{a}\n"
            f.write(output)
    
    print("\nResults saved to: test_results_25_questions.txt")
    print("=" * 80)

if __name__ == "__main__":
    main()

