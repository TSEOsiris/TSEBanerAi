#!/usr/bin/env python3
"""
Подготовка датасета для fine-tuning в формате unsloth (Alpaca format)
Формат: instruction, input, output
"""

import json
from pathlib import Path
from typing import List, Dict, Any, Optional

def create_alpaca_format(entry: Dict[str, Any]) -> Optional[Dict[str, Any]]:
    """Создание формата Alpaca для unsloth"""
    
    # Определяем тип данных и создаем промпт
    data_type = entry.get('type', 'unknown')
    language = entry.get('language', 'en')
    text = entry.get('text', '').strip()
    
    # Пропускаем пустые или слишком короткие тексты
    if not text or len(text) < 50:
        return None
    
    # Определяем язык для инструкции
    lang_map = {
        'en': 'English',
        'ru': 'Russian', 
        'tr': 'Turkish'
    }
    lang_name = lang_map.get(language, 'English')
    
    # Создаем инструкцию в зависимости от типа
    if data_type in ['heroes', 'lords', 'npc_characters']:
        if language == 'ru':
            instruction = "Ты знающий ученый Кальрадии. Опиши этого исторического персонажа из Mount & Blade II: Bannerlord."
            input_text = f"Персонаж: {entry.get('id', 'Unknown')}"
        elif language == 'tr':
            instruction = "Calradia'dan bilgili bir bilginsin. Mount & Blade II: Bannerlord'dan bu tarihi karakteri açıkla."
            input_text = f"Karakter: {entry.get('id', 'Unknown')}"
        else:
            instruction = "You are a knowledgeable scholar of Calradia. Describe this historical figure from Mount & Blade II: Bannerlord."
            input_text = f"Character: {entry.get('id', 'Unknown')}"
    
    elif data_type == 'settlements':
        if language == 'ru':
            instruction = "Ты знающий ученый Кальрадии. Опиши это поселение из Mount & Blade II: Bannerlord."
            input_text = f"Поселение: {entry.get('id', 'Unknown')}"
        elif language == 'tr':
            instruction = "Calradia'dan bilgili bir bilginsin. Mount & Blade II: Bannerlord'dan bu yerleşimi açıkla."
            input_text = f"Yerleşim: {entry.get('id', 'Unknown')}"
        else:
            instruction = "You are a knowledgeable scholar of Calradia. Describe this settlement from Mount & Blade II: Bannerlord."
            input_text = f"Settlement: {entry.get('id', 'Unknown')}"
    
    elif data_type in ['world_lore', 'concepts']:
        if language == 'ru':
            instruction = "Ты знающий ученый Кальрадии. Объясни эту концепцию из мира Mount & Blade II: Bannerlord."
            input_text = f"Тема: {entry.get('id', 'Unknown')}"
        elif language == 'tr':
            instruction = "Calradia'dan bilgili bir bilginsin. Mount & Blade II: Bannerlord dünyasından bu kavramı açıkla."
            input_text = f"Konu: {entry.get('id', 'Unknown')}"
        else:
            instruction = "You are a knowledgeable scholar of Calradia. Explain this concept from the world of Mount & Blade II: Bannerlord."
            input_text = f"Topic: {entry.get('id', 'Unknown')}"
    
    elif data_type == 'kingdoms':
        if language == 'ru':
            instruction = "Ты знающий ученый Кальрадии. Опиши это королевство из Mount & Blade II: Bannerlord."
            input_text = f"Королевство: {entry.get('id', 'Unknown')}"
        elif language == 'tr':
            instruction = "Calradia'dan bilgili bir bilginsin. Mount & Blade II: Bannerlord'dan bu krallığı açıkla."
            input_text = f"Krallık: {entry.get('id', 'Unknown')}"
        else:
            instruction = "You are a knowledgeable scholar of Calradia. Describe this kingdom from Mount & Blade II: Bannerlord."
            input_text = f"Kingdom: {entry.get('id', 'Unknown')}"
    
    elif data_type == 'novella':
        if language == 'ru':
            instruction = "Ты читаешь путевые заметки из Mount & Blade II: Bannerlord. Продолжи или резюмируй эту главу."
            input_text = f"Глава: {entry.get('title', entry.get('id', 'Unknown'))}"
        elif language == 'tr':
            instruction = "Mount & Blade II: Bannerlord'dan bir seyahatname okuyorsun. Bu bölümü devam ettir veya özetle."
            input_text = f"Bölüm: {entry.get('title', entry.get('id', 'Unknown'))}"
        else:
            instruction = "You are reading a travelogue from Mount & Blade II: Bannerlord. Continue or summarize this chapter."
            input_text = f"Chapter: {entry.get('title', entry.get('id', 'Unknown'))}"
    
    elif data_type == 'items':
        if language == 'ru':
            instruction = "Ты знающий ученый Кальрадии. Опиши этот предмет из Mount & Blade II: Bannerlord."
            input_text = f"Предмет: {entry.get('id', 'Unknown')}"
        elif language == 'tr':
            instruction = "Calradia'dan bilgili bir bilginsin. Mount & Blade II: Bannerlord'dan bu eşyayı açıkla."
            input_text = f"Eşya: {entry.get('id', 'Unknown')}"
        else:
            instruction = "You are a knowledgeable scholar of Calradia. Describe this item from Mount & Blade II: Bannerlord."
            input_text = f"Item: {entry.get('id', 'Unknown')}"
    
    elif data_type in ['cultures', 'traits']:
        if language == 'ru':
            instruction = "Ты знающий ученый Кальрадии. Опиши эту особенность из мира Mount & Blade II: Bannerlord."
            input_text = f"Особенность: {entry.get('id', 'Unknown')}"
        elif language == 'tr':
            instruction = "Calradia'dan bilgili bir bilginsin. Mount & Blade II: Bannerlord dünyasından bu özelliği açıkla."
            input_text = f"Özellik: {entry.get('id', 'Unknown')}"
        else:
            instruction = "You are a knowledgeable scholar of Calradia. Describe this feature from the world of Mount & Blade II: Bannerlord."
            input_text = f"Feature: {entry.get('id', 'Unknown')}"
    
    else:
        # Общий формат
        if language == 'ru':
            instruction = "Ты знающий ученый Кальрадии. Предоставь информацию об этой теме из Mount & Blade II: Bannerlord."
            input_text = f"Тема: {entry.get('id', 'Unknown')}"
        elif language == 'tr':
            instruction = "Calradia'dan bilgili bir bilginsin. Mount & Blade II: Bannerlord'dan bu konu hakkında bilgi ver."
            input_text = f"Konu: {entry.get('id', 'Unknown')}"
        else:
            instruction = "You are a knowledgeable scholar of Calradia. Provide information about this topic from Mount & Blade II: Bannerlord."
            input_text = f"Topic: {entry.get('id', 'Unknown')}"
    
    return {
        "instruction": instruction,
        "input": input_text,
        "output": text
    }

def prepare_dataset(input_dir: Path, output_file: Path, languages: List[str] = None, max_entries: int = None):
    """Подготовка датасета из всех JSON файлов"""
    
    if languages is None:
        languages = ['en', 'ru', 'tr']
    
    all_data = []
    
    # Файлы для обработки
    files_to_process = [
        'encyclopedia_all.json',
        'travels_calradia_finetuning_ru.json',
        'travels_calradia_finetuning_en.json',
        'travels_calradia_finetuning_tr.json',
        'clans.json',
        'settlements.json',
        'lords.json',
        'factions.json',
        'emperor_neretzes.json',
        'organizations_and_companies.json',
    ]
    
    print("=" * 80)
    print("PREPARING DATASET FOR UNSLOTH FINE-TUNING")
    print("=" * 80)
    
    for file_name in files_to_process:
        file_path = input_dir / file_name
        if not file_path.exists():
            print(f"WARNING: Skipping {file_name} (not found)")
            continue
        
        print(f"\nProcessing {file_name}...")
        
        try:
            with open(file_path, 'r', encoding='utf-8') as f:
                data = json.load(f)
        except Exception as e:
            print(f"   ERROR: Error loading {file_name}: {e}")
            continue
        
        if isinstance(data, list):
            entries = data
        elif isinstance(data, dict):
            # Для словарей (например, faction_descriptions_ru.json)
            entries = [{"id": k, "text": v, "type": "faction_description", "language": "ru"} 
                        for k, v in data.items()]
        else:
            continue
        
        count = 0
        skipped = 0
        for entry in entries:
            # Определяем язык записи
            entry_lang = entry.get('language', None)
            
            # Если язык не указан, проверяем мультиязычные поля
            if entry_lang is None:
                # Проверяем наличие мультиязычных полей (description_en, description_ru, etc.)
                has_multilang = False
                
                # Обрабатываем английский вариант
                if 'en' in languages:
                    entry_en = entry.copy()
                    entry_en['language'] = 'en'
                    # Используем английские поля
                    if 'description_en' in entry and entry['description_en']:
                        entry_en['text'] = entry['description_en']
                        if 'name_en' in entry:
                            entry_en['name'] = entry['name_en']
                    elif 'text' in entry and entry['text']:
                        entry_en['text'] = entry['text']
                    
                    if entry_en.get('text'):
                        formatted = create_alpaca_format(entry_en)
                        if formatted:
                            all_data.append(formatted)
                            count += 1
                            has_multilang = True
                
                # Обрабатываем русский вариант
                if 'ru' in languages:
                    entry_ru = entry.copy()
                    entry_ru['language'] = 'ru'
                    # Используем русские поля
                    if 'description_ru' in entry and entry['description_ru']:
                        entry_ru['text'] = entry['description_ru']
                        if 'name_ru' in entry:
                            entry_ru['name'] = entry['name_ru']
                    
                    if entry_ru.get('text'):
                        formatted = create_alpaca_format(entry_ru)
                        if formatted:
                            all_data.append(formatted)
                            count += 1
                            has_multilang = True
                
                # Обрабатываем турецкий вариант
                if 'tr' in languages:
                    entry_tr = entry.copy()
                    entry_tr['language'] = 'tr'
                    # Используем турецкие поля
                    if 'description_tr' in entry and entry['description_tr']:
                        entry_tr['text'] = entry['description_tr']
                        if 'name_tr' in entry:
                            entry_tr['name'] = entry['name_tr']
                    
                    if entry_tr.get('text'):
                        formatted = create_alpaca_format(entry_tr)
                        if formatted:
                            all_data.append(formatted)
                            count += 1
                            has_multilang = True
                
                if has_multilang:
                    continue
                else:
                    # Нет текстовых полей - пропускаем
                    skipped += 1
                    continue
            
            # Если язык указан, фильтруем
            if entry_lang not in languages:
                skipped += 1
                continue
            
            formatted = create_alpaca_format(entry)
            if formatted:
                all_data.append(formatted)
                count += 1
            
            # Ограничение на количество записей для тестирования
            if max_entries and len(all_data) >= max_entries:
                print(f"   WARNING: Reached max_entries limit ({max_entries})")
                break
        
        print(f"   OK: Added {count} entries, skipped {skipped}")
        
        if max_entries and len(all_data) >= max_entries:
            print(f"   WARNING: Reached max_entries limit ({max_entries})")
            break
    
    # Сохраняем датасет
    print(f"\nSaving dataset...")
    with open(output_file, 'w', encoding='utf-8') as f:
        json.dump(all_data, f, ensure_ascii=False, indent=2)
    
    print(f"\nSUCCESS: Dataset prepared: {len(all_data)} entries")
    print(f"Saved to: {output_file}")
    
    # Статистика по языкам
    lang_stats = {}
    for entry in all_data:
        # Определяем язык по инструкции или тексту
        if 'You are' in entry['instruction']:
            lang = 'en'
        elif 'Ты знающий' in entry['instruction'] or 'Ты читаешь' in entry['instruction']:
            lang = 'ru'
        elif 'bilgili bir bilginsin' in entry['instruction'] or 'seyahatname' in entry['instruction']:
            lang = 'tr'
        else:
            lang = 'unknown'
        
        lang_stats[lang] = lang_stats.get(lang, 0) + 1
    
    print(f"\nLanguage distribution:")
    for lang, count in sorted(lang_stats.items()):
        print(f"   {lang}: {count} entries")
    
    return all_data

if __name__ == '__main__':
    import sys
    
    project_root = Path(__file__).parent.parent
    input_dir = project_root / 'finetuning_data'
    output_file = project_root / 'finetuning_data' / 'unsloth_training_dataset.json'
    
    # Можно выбрать языки для обучения
    # Для тестирования можно ограничить количество записей
    languages = None  # Все языки
    max_entries = None  # Без ограничений
    
    if len(sys.argv) > 1:
        if '--en-only' in sys.argv:
            languages = ['en']
        elif '--ru-only' in sys.argv:
            languages = ['ru']
        elif '--test' in sys.argv:
            max_entries = 1000  # Для быстрого тестирования
    
    prepare_dataset(input_dir, output_file, languages=languages, max_entries=max_entries)

