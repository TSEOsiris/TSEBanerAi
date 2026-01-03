#!/usr/bin/env python3
"""
Парсинг внутриигровой энциклопедии Bannerlord
Извлекает данные из JSON файлов на трех языках (EN, RU, TR)
"""

import json
import sys
from pathlib import Path
from typing import Dict, List, Any, Optional
from collections import defaultdict

# Настройка кодировки для Windows
if sys.platform == 'win32':
    import io
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8')


class IngameEncyclopediaParser:
    """Парсер для внутриигровой энциклопедии"""
    
    def __init__(self, input_dir: Path, output_dir: Path):
        self.input_dir = input_dir
        self.output_dir = output_dir
        self.output_dir.mkdir(parents=True, exist_ok=True)
        
        # Файлы для обработки
        self.files_to_process = [
            'heroes.json',
            'world_lore.json',
            'concepts.json',
            'kingdoms.json',
            'settlements.json',
            'items.json',
            'cultures.json',
            'npc_characters.json',
            'traits.json'
        ]
        
    def clean_text(self, text: str) -> str:
        """Очистка текста от тегов локализации и дублирования"""
        if not text:
            return ''
        
        # Удаляем теги локализации типа {=bsOLRZyS}
        import re
        text = re.sub(r'\{=[^}]+\}', '', text)
        
        # Удаляем дублирование текста (если текст повторяется дважды)
        # Разделяем пополам и проверяем, не одинаковые ли части
        text_len = len(text)
        if text_len > 20:
            mid = text_len // 2
            first_half = text[:mid].strip()
            second_half = text[mid:].strip()
            
            # Если части очень похожи (80% совпадения), берем только первую
            if first_half and second_half:
                similarity = sum(a == b for a, b in zip(first_half[:min(100, len(first_half))], 
                                                         second_half[:min(100, len(second_half))])) / min(100, len(first_half), len(second_half))
                if similarity > 0.8:
                    return first_half
        
        return text.strip()
    
    def extract_text_from_entry(self, entry: Dict[str, Any], file_type: str) -> Dict[str, Any]:
        """Извлечение текста из записи в зависимости от типа файла"""
        result = {
            'id': entry.get('id', ''),
            'text_en': '',
            'text_ru': '',
            'text_tr': '',
            'title_en': '',
            'title_ru': '',
            'title_tr': '',
            'name_en': '',
            'name_ru': '',
            'name_tr': ''
        }
        
        # Извлекаем текст в зависимости от типа
        if file_type in ['heroes', 'npc_characters']:
            result['text_en'] = self.clean_text(entry.get('text', ''))
        elif file_type in ['world_lore', 'concepts']:
            result['title_en'] = self.clean_text(entry.get('title', ''))
            result['text_en'] = self.clean_text(entry.get('text', ''))
        elif file_type == 'kingdoms':
            result['name_en'] = self.clean_text(entry.get('name', ''))
            result['text_en'] = self.clean_text(entry.get('text', ''))
        elif file_type == 'settlements':
            result['name_en'] = self.clean_text(entry.get('name', ''))
            result['text_en'] = self.clean_text(entry.get('text', ''))
        elif file_type == 'items':
            result['name_en'] = self.clean_text(entry.get('name', ''))
            result['text_en'] = self.clean_text(entry.get('text', ''))
        elif file_type == 'cultures':
            result['name_en'] = self.clean_text(entry.get('name', ''))
            result['text_en'] = self.clean_text(entry.get('text', ''))
        elif file_type == 'traits':
            result['name_en'] = self.clean_text(entry.get('name', ''))
            result['text_en'] = self.clean_text(entry.get('text', ''))
        
        return result
    
    def parse_duplicate_text(self, text: str) -> tuple:
        """Парсинг дублированного текста (RU + EN или TR + EN)"""
        if not text:
            return '', ''
        
        # В русской версии часто: "Русский текст.English text"
        # Или: "Русский текст\nEnglish text"
        # Или просто дублирование: "Русский текст.Russian text"
        
        # Пробуем найти точку как разделитель
        # Ищем последнюю точку перед английским текстом
        # Английский текст обычно начинается с заглавной буквы
        
        # Разделяем по точкам
        parts = text.split('.')
        if len(parts) >= 2:
            # Берем последнюю часть как потенциальный английский текст
            last_part = parts[-1].strip()
            first_parts = '.'.join(parts[:-1]).strip()
            
            # Проверяем, начинается ли последняя часть с заглавной (английский)
            if last_part and len(last_part) > 10 and last_part[0].isupper():
                # Проверяем, что это действительно английский (содержит латинские буквы)
                if any(c.isalpha() and ord(c) < 128 for c in last_part[:50]):
                    return first_parts, last_part
        
        # Пробуем разделить по переводу строки
        parts = text.split('\n', 1)
        if len(parts) == 2:
            second_part = parts[1].strip()
            if second_part and len(second_part) > 10 and second_part[0].isupper():
                # Проверяем на латинские буквы
                if any(c.isalpha() and ord(c) < 128 for c in second_part[:50]):
                    return parts[0].strip(), second_part
        
        # Если не нашли разделитель, возвращаем весь текст как первый язык
        return text, ''
    
    def load_file(self, file_path: Path) -> List[Dict[str, Any]]:
        """Загрузка JSON файла"""
        if not file_path.exists():
            return []
        
        try:
            with open(file_path, 'r', encoding='utf-8', errors='ignore') as f:
                data = json.load(f)
                if isinstance(data, list):
                    return data
                elif isinstance(data, dict):
                    return [data]
                return []
        except Exception as e:
            print(f"   ⚠️  Error loading {file_path.name}: {e}")
            return []
    
    def merge_multilang_data(self, en_data: List[Dict], ru_data: List[Dict], tr_data: List[Dict], file_type: str) -> List[Dict]:
        """Объединение данных на трех языках"""
        # Создаем индекс по ID
        merged = {}
        
        # Обрабатываем английские данные
        for entry in en_data:
            entry_id = entry.get('id', '')
            if not entry_id:
                continue
            
            extracted = self.extract_text_from_entry(entry, file_type)
            merged[entry_id] = extracted
        
        # Обрабатываем русские данные
        for entry in ru_data:
            entry_id = entry.get('id', '')
            if not entry_id or entry_id not in merged:
                continue
            
            extracted = self.extract_text_from_entry(entry, file_type)
            
            # Парсим дублированный текст если есть
            text_ru = extracted.get('text_ru', '') or extracted.get('text_en', '')
            if text_ru:
                text_ru = self.clean_text(text_ru)
                ru_text, en_text = self.parse_duplicate_text(text_ru)
                merged[entry_id]['text_ru'] = self.clean_text(ru_text if ru_text else text_ru)
                if en_text and not merged[entry_id]['text_en']:
                    merged[entry_id]['text_en'] = self.clean_text(en_text)
            
            title_ru = extracted.get('title_ru', '') or extracted.get('title_en', '')
            if title_ru:
                ru_title, en_title = self.parse_duplicate_text(title_ru)
                merged[entry_id]['title_ru'] = ru_title if ru_title else title_ru
                if en_title and not merged[entry_id]['title_en']:
                    merged[entry_id]['title_en'] = en_title
            
            name_ru = extracted.get('name_ru', '') or extracted.get('name_en', '')
            if name_ru:
                ru_name, en_name = self.parse_duplicate_text(name_ru)
                merged[entry_id]['name_ru'] = ru_name if ru_name else name_ru
                if en_name and not merged[entry_id]['name_en']:
                    merged[entry_id]['name_en'] = en_name
        
        # Обрабатываем турецкие данные
        for entry in tr_data:
            entry_id = entry.get('id', '')
            if not entry_id or entry_id not in merged:
                continue
            
            extracted = self.extract_text_from_entry(entry, file_type)
            
            # Парсим дублированный текст если есть
            text_tr = extracted.get('text_tr', '') or extracted.get('text_en', '')
            if text_tr:
                text_tr = self.clean_text(text_tr)
                tr_text, en_text = self.parse_duplicate_text(text_tr)
                merged[entry_id]['text_tr'] = self.clean_text(tr_text if tr_text else text_tr)
                if en_text and not merged[entry_id]['text_en']:
                    merged[entry_id]['text_en'] = self.clean_text(en_text)
            
            title_tr = extracted.get('title_tr', '') or extracted.get('title_en', '')
            if title_tr:
                tr_title, en_title = self.parse_duplicate_text(title_tr)
                merged[entry_id]['title_tr'] = tr_title if tr_title else title_tr
                if en_title and not merged[entry_id]['title_en']:
                    merged[entry_id]['title_en'] = en_title
            
            name_tr = extracted.get('name_tr', '') or extracted.get('name_en', '')
            if name_tr:
                tr_name, en_name = self.parse_duplicate_text(name_tr)
                merged[entry_id]['name_tr'] = tr_name if tr_name else name_tr
                if en_name and not merged[entry_id]['name_en']:
                    merged[entry_id]['name_en'] = en_name
        
        return list(merged.values())
    
    def create_finetuning_format(self, data: List[Dict], file_type: str) -> List[Dict]:
        """Создание формата для fine-tuning"""
        finetuning_data = []
        
        for entry in data:
            # Формируем полный текст
            text_parts = []
            
            # Добавляем название/заголовок
            if entry.get('name_en') or entry.get('title_en'):
                name = entry.get('name_en') or entry.get('title_en', '')
                if name:
                    text_parts.append(f"Name: {name}")
            
            # Добавляем основной текст
            if entry.get('text_en'):
                text_parts.append(entry['text_en'])
            
            full_text_en = '\n\n'.join(text_parts) if text_parts else ''
            
            # Русская версия
            text_parts_ru = []
            if entry.get('name_ru') or entry.get('title_ru'):
                name_ru = entry.get('name_ru') or entry.get('title_ru', '')
                if name_ru:
                    text_parts_ru.append(f"Название: {name_ru}")
            if entry.get('text_ru'):
                text_parts_ru.append(entry['text_ru'])
            full_text_ru = '\n\n'.join(text_parts_ru) if text_parts_ru else ''
            
            # Турецкая версия
            text_parts_tr = []
            if entry.get('name_tr') or entry.get('title_tr'):
                name_tr = entry.get('name_tr') or entry.get('title_tr', '')
                if name_tr:
                    text_parts_tr.append(f"İsim: {name_tr}")
            if entry.get('text_tr'):
                text_parts_tr.append(entry['text_tr'])
            full_text_tr = '\n\n'.join(text_parts_tr) if text_parts_tr else ''
            
            # Создаем записи для каждого языка, если есть текст
            if full_text_en:
                finetuning_data.append({
                    'id': f"{entry['id']}_en",
                    'type': file_type,
                    'source': 'ingame_encyclopedia',
                    'language': 'en',
                    'text': full_text_en
                })
            
            if full_text_ru:
                finetuning_data.append({
                    'id': f"{entry['id']}_ru",
                    'type': file_type,
                    'source': 'ingame_encyclopedia',
                    'language': 'ru',
                    'text': full_text_ru
                })
            
            if full_text_tr:
                finetuning_data.append({
                    'id': f"{entry['id']}_tr",
                    'type': file_type,
                    'source': 'ingame_encyclopedia',
                    'language': 'tr',
                    'text': full_text_tr
                })
        
        return finetuning_data
    
    def parse_all(self):
        """Парсинг всех файлов"""
        print("=" * 80)
        print("PARSING INGAME ENCYCLOPEDIA")
        print("=" * 80)
        
        all_finetuning_data = []
        stats = defaultdict(lambda: {'en': 0, 'ru': 0, 'tr': 0, 'total_text': 0})
        
        for file_name in self.files_to_process:
            file_type = file_name.replace('.json', '')
            print(f"\n📖 Processing {file_type}...")
            
            # Загружаем файлы на всех языках
            en_file = self.input_dir / 'EN' / file_name
            ru_file = self.input_dir / 'RU' / file_name
            tr_file = self.input_dir / 'TR' / file_name
            
            en_data = self.load_file(en_file)
            ru_data = self.load_file(ru_file)
            tr_data = self.load_file(tr_file)
            
            print(f"   EN: {len(en_data)} entries")
            print(f"   RU: {len(ru_data)} entries")
            print(f"   TR: {len(tr_data)} entries")
            
            # Объединяем данные
            merged_data = self.merge_multilang_data(en_data, ru_data, tr_data, file_type)
            
            # Создаем формат для fine-tuning
            finetuning_data = self.create_finetuning_format(merged_data, file_type)
            
            # Сохраняем отдельный файл для этого типа
            if finetuning_data:
                output_file = self.output_dir / f'encyclopedia_{file_type}.json'
                with open(output_file, 'w', encoding='utf-8') as f:
                    json.dump(finetuning_data, f, ensure_ascii=False, indent=2)
                
                # Статистика
                en_count = sum(1 for e in finetuning_data if e['language'] == 'en')
                ru_count = sum(1 for e in finetuning_data if e['language'] == 'ru')
                tr_count = sum(1 for e in finetuning_data if e['language'] == 'tr')
                total_text = sum(len(e['text']) for e in finetuning_data)
                
                stats[file_type] = {
                    'en': en_count,
                    'ru': ru_count,
                    'tr': tr_count,
                    'total_text': total_text
                }
                
                print(f"   ✅ Saved {len(finetuning_data)} entries ({en_count} EN, {ru_count} RU, {tr_count} TR)")
                print(f"   📊 Text: {round(total_text/1024, 2)} KB")
            
            all_finetuning_data.extend(finetuning_data)
        
        # Сохраняем объединенный файл
        if all_finetuning_data:
            combined_file = self.output_dir / 'encyclopedia_all.json'
            with open(combined_file, 'w', encoding='utf-8') as f:
                json.dump(all_finetuning_data, f, ensure_ascii=False, indent=2)
            
            print("\n" + "=" * 80)
            print("✅ PARSING COMPLETED!")
            print("=" * 80)
            
            total_en = sum(s['en'] for s in stats.values())
            total_ru = sum(s['ru'] for s in stats.values())
            total_tr = sum(s['tr'] for s in stats.values())
            total_text = sum(s['total_text'] for s in stats.values())
            
            print(f"\n📊 TOTAL STATISTICS:")
            print(f"   EN entries: {total_en}")
            print(f"   RU entries: {total_ru}")
            print(f"   TR entries: {total_tr}")
            print(f"   Total entries: {total_en + total_ru + total_tr}")
            print(f"   Total text: {round(total_text/1024, 2)} KB ({total_text:,} characters)")
            
            print(f"\n📁 Files saved:")
            print(f"   - encyclopedia_all.json (combined)")
            for file_type in stats.keys():
                print(f"   - encyclopedia_{file_type}.json")
        
        return all_finetuning_data


def main():
    """Main entry point"""
    project_root = Path(__file__).parent.parent
    input_dir = project_root / 'wiki_data' / 'Ingame Encyclopedia'
    output_dir = project_root / 'finetuning_data'
    
    if not input_dir.exists():
        print(f"❌ Directory not found: {input_dir}")
        return
    
    parser = IngameEncyclopediaParser(input_dir, output_dir)
    parser.parse_all()


if __name__ == '__main__':
    main()

