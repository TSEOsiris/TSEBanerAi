#!/usr/bin/env python3
"""Prepare and publish Bannerlord Lore dataset to HuggingFace"""

import json
import os
from pathlib import Path
from huggingface_hub import HfApi, login, create_repo

# Configuration
DATASET_NAME = "bannerlord-lore-dataset"
HF_TOKEN = os.environ.get("HF_TOKEN") or input("Enter HuggingFace token: ")

def load_json(path):
    """Load JSON file with UTF-8 encoding"""
    try:
        with open(path, 'r', encoding='utf-8') as f:
            content = f.read().strip()
            if not content:
                return []
            return json.loads(content)
    except (json.JSONDecodeError, Exception) as e:
        print(f"    Warning: Could not load {path}: {e}")
        return []

def collect_all_data():
    """Collect all data from finetuning_data folder"""
    data_dir = Path("finetuning_data")
    
    dataset = {
        "metadata": {
            "name": "Bannerlord Lore Dataset",
            "version": "1.0.0",
            "description": "Comprehensive lore dataset for Mount & Blade II: Bannerlord",
            "languages": ["en", "ru", "tr"],
            "source": "In-game encyclopedia, wiki, novellas",
            "license": "CC-BY-4.0",
            "game": "Mount & Blade II: Bannerlord",
            "publisher": "TaleWorlds Entertainment"
        },
        "categories": {}
    }
    
    # 1. Encyclopedia data
    print("Collecting encyclopedia data...")
    encyclopedia_files = [
        "encyclopedia_heroes.json",
        "encyclopedia_kingdoms.json", 
        "encyclopedia_settlements.json",
        "encyclopedia_cultures.json",
        "encyclopedia_concepts.json",
        "encyclopedia_world_lore.json",
        "encyclopedia_traits.json",
        "encyclopedia_items.json"
    ]
    
    for filename in encyclopedia_files:
        filepath = data_dir / filename
        if filepath.exists():
            category = filename.replace("encyclopedia_", "").replace(".json", "")
            data = load_json(filepath)
            dataset["categories"][category] = data
            print(f"  - {category}: {len(data)} entries")
    
    # 2. Factions and clans
    print("Collecting factions and clans...")
    for filename in ["factions.json", "clans.json", "lords.json", "settlements.json"]:
        filepath = data_dir / filename
        if filepath.exists():
            category = filename.replace(".json", "")
            data = load_json(filepath)
            dataset["categories"][category] = data
            print(f"  - {category}: {len(data)} entries")
    
    # 3. Organizations
    print("Collecting organizations...")
    org_path = data_dir / "organizations_and_companies.json"
    if org_path.exists():
        dataset["categories"]["organizations"] = load_json(org_path)
        print(f"  - organizations: {len(dataset['categories']['organizations'])} entries")
    
    # 4. Emperor Neretzes
    print("Collecting historical figures...")
    neretzes_path = data_dir / "emperor_neretzes.json"
    if neretzes_path.exists():
        dataset["categories"]["historical_figures"] = [load_json(neretzes_path)]
        print(f"  - historical_figures: 1 entry")
    
    # 5. Travels in Calradia (novellas)
    print("Collecting novellas...")
    novellas = {"en": [], "ru": [], "tr": []}
    
    travels_dir = data_dir / "travels_calradia"
    for lang in ["en", "ru", "tr"]:
        lang_dir = travels_dir / lang
        if lang_dir.exists():
            for chapter_file in sorted(lang_dir.glob("chapter_*.json")):
                chapter_data = load_json(chapter_file)
                novellas[lang].append(chapter_data)
    
    dataset["categories"]["novellas"] = novellas
    for lang, chapters in novellas.items():
        print(f"  - novellas_{lang}: {len(chapters)} chapters")
    
    # 6. Faction descriptions (Russian)
    print("Collecting faction descriptions...")
    faction_desc_path = data_dir / "faction_descriptions_ru.json"
    if faction_desc_path.exists():
        dataset["categories"]["faction_descriptions_ru"] = load_json(faction_desc_path)
    
    return dataset

def create_readme():
    """Create dataset README"""
    readme = """---
language:
- en
- ru  
- tr
license: cc-by-4.0
task_categories:
- text-generation
- question-answering
tags:
- bannerlord
- mount-and-blade
- game-lore
- fantasy
- medieval
pretty_name: Bannerlord Lore Dataset
size_categories:
- 1K<n<10K
---

# Bannerlord Lore Dataset

Comprehensive lore dataset for **Mount & Blade II: Bannerlord** - a medieval action RPG by TaleWorlds Entertainment.

## Dataset Description

This dataset contains structured lore information extracted from the game, including:

### Categories

| Category | Description | Languages |
|----------|-------------|-----------|
| **Heroes** | NPCs, lords, companions | EN, RU, TR |
| **Kingdoms** | Major factions (Empire, Battania, etc.) | EN, RU, TR |
| **Settlements** | Cities, castles, villages | EN, RU, TR |
| **Cultures** | Cultural backgrounds | EN, RU, TR |
| **Concepts** | Game mechanics and lore concepts | EN, RU, TR |
| **World Lore** | Historical events and background | EN, RU, TR |
| **Organizations** | Minor factions (Wolfskins, Lake Rats, etc.) | EN, RU |
| **Historical Figures** | Emperor Neretzes and others | EN, RU, TR |
| **Novellas** | "Travels in Calradia" story chapters | EN, RU, TR |

### Languages

- **English (en)** - Primary language
- **Russian (ru)** - Full translation
- **Turkish (tr)** - Full translation

## Usage

### Load with Datasets library

```python
from datasets import load_dataset

dataset = load_dataset("TSEOsiris/bannerlord-lore-dataset")
```

### Direct JSON access

```python
import json
from huggingface_hub import hf_hub_download

path = hf_hub_download(
    repo_id="TSEOsiris/bannerlord-lore-dataset",
    filename="bannerlord_lore.json",
    repo_type="dataset"
)

with open(path, 'r', encoding='utf-8') as f:
    data = json.load(f)
```

## Use Cases

- **Fine-tuning LLMs** for Bannerlord-related Q&A
- **RAG systems** for game wikis and assistants  
- **NPC dialogue generation**
- **Lore research and analysis**
- **Fan fiction and modding**

## Fine-tuned Models

A fine-tuned model based on this dataset is available:
- [TSEOsiris/bannerlord-lore-lora](https://huggingface.co/TSEOsiris/bannerlord-lore-lora) - LoRA adapter for Qwen3-8B

## Sources

- In-game encyclopedia
- Official game files
- TaleWorlds wiki
- "Travels in Calradia" in-game novella

## License

This dataset is released under **CC-BY-4.0**.

Mount & Blade II: Bannerlord is a trademark of TaleWorlds Entertainment.
This is a fan-made dataset for educational and research purposes.

## Contributing

Found missing lore? Want to add more data?
- Open an issue or PR on the repository
- Contact: [TSEOsiris on HuggingFace](https://huggingface.co/TSEOsiris)

## Version History

- **v1.0.0** (2026-01-02): Initial release
  - Encyclopedia data (heroes, kingdoms, settlements, cultures)
  - Organizations and minor factions
  - "Travels in Calradia" novellas
  - Historical figures (Emperor Neretzes)
"""
    return readme

def main():
    print("=" * 60)
    print("PUBLISHING BANNERLORD LORE DATASET")
    print("=" * 60)
    
    # Login to HuggingFace
    print("\nLogging in to HuggingFace...")
    login(token=HF_TOKEN)
    api = HfApi()
    user_info = api.whoami()
    username = user_info["name"]
    print(f"Logged in as: {username}")
    
    repo_id = f"{username}/{DATASET_NAME}"
    
    # Collect data
    print("\nCollecting dataset...")
    dataset = collect_all_data()
    
    # Count total entries
    total = 0
    for cat, data in dataset["categories"].items():
        if isinstance(data, list):
            total += len(data)
        elif isinstance(data, dict):
            for lang_data in data.values():
                if isinstance(lang_data, list):
                    total += len(lang_data)
    
    print(f"\nTotal entries: {total}")
    
    # Save dataset
    output_dir = Path("dataset_export")
    output_dir.mkdir(exist_ok=True)
    
    dataset_path = output_dir / "bannerlord_lore.json"
    print(f"\nSaving dataset to {dataset_path}...")
    with open(dataset_path, 'w', encoding='utf-8') as f:
        json.dump(dataset, f, ensure_ascii=False, indent=2)
    
    # Save README
    readme_path = output_dir / "README.md"
    print(f"Saving README to {readme_path}...")
    with open(readme_path, 'w', encoding='utf-8') as f:
        f.write(create_readme())
    
    # Also save training format
    training_path = Path("finetuning_data/unsloth_training_dataset.json")
    if training_path.exists():
        import shutil
        shutil.copy(training_path, output_dir / "training_format.json")
        print("Copied training format dataset")
    
    # Create repo and upload
    print(f"\nCreating repository: {repo_id}...")
    try:
        create_repo(repo_id=repo_id, repo_type="dataset", exist_ok=True, token=HF_TOKEN)
    except Exception as e:
        print(f"Repo exists or error: {e}")
    
    print(f"\nUploading to HuggingFace...")
    api.upload_folder(
        folder_path=str(output_dir),
        repo_id=repo_id,
        repo_type="dataset",
        commit_message="Initial release: Bannerlord Lore Dataset v1.0.0"
    )
    
    print("\n" + "=" * 60)
    print("DATASET PUBLISHED!")
    print("=" * 60)
    print(f"\nDataset URL: https://huggingface.co/datasets/{repo_id}")
    print("\nYou can update it anytime by running this script again!")
    print("=" * 60)

if __name__ == "__main__":
    main()

