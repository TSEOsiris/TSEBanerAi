#!/usr/bin/env python3
"""
Парсинг Travels in Calradia из extracted text для fine-tuning
Извлекает все главы и преобразует в формат для тюнинга
"""

import re
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


class TravelsParser:
    """Парсер для Travels in Calradia"""
    
    def __init__(self, input_file: Path, output_dir: Path):
        self.input_file = input_file
        self.output_dir = output_dir
        self.output_dir.mkdir(parents=True, exist_ok=True)
        
    def parse_file(self) -> Dict[int, Dict[str, Any]]:
        """Парсинг файла и извлечение глав"""
        print(f"📖 Reading file: {self.input_file}")
        
        with open(self.input_file, 'r', encoding='utf-8', errors='ignore') as f:
            lines = f.readlines()
        
        chapters: Dict[int, Dict[str, Any]] = defaultdict(lambda: {
            'chapter': 0,
            'title': None,
            'pages': {}
        })
        
        # Паттерны для поиска
        start_pattern = r'<string\s+id="travels_in_calradia_chapter_(\d+)_(?:page_(\d+)|title)"\s+text="'
        end_pattern = r'"\s*/>'
        
        i = 0
        found_count = 0
        
        while i < len(lines):
            line = lines[i]
            
            # Ищем начало строки
            start_match = re.search(start_pattern, line, re.IGNORECASE)
            if start_match:
                chapter_num = int(start_match.group(1))
                page_num = start_match.group(2)
                
                chapters[chapter_num]['chapter'] = chapter_num
                
                # Извлекаем текст после text="
                text_start = start_match.end()
                
                # Проверяем, есть ли закрывающая кавычка в этой же строке
                if '" />' in line[text_start:]:
                    # Текст в одной строке
                    text_end = line.find('" />', text_start)
                    text = line[text_start:text_end]
                else:
                    # Текст разбит на несколько строк - собираем до закрывающей кавычки
                    text_parts = [line[text_start:].rstrip()]
                    i += 1
                    
                    while i < len(lines):
                        next_line = lines[i]
                        if '" />' in next_line:
                            # Нашли конец
                            text_end = next_line.find('" />')
                            text_parts.append(next_line[:text_end])
                            break
                        else:
                            text_parts.append(next_line.rstrip())
                        i += 1
                    
                    text = ''.join(text_parts)
                
                # Очищаем текст от экранированных символов и заменяем {newline} на переносы строк
                text = text.replace('\\"', '"').replace('\\n', '\n').replace('\\t', '\t')
                text = text.replace('{newline}', '\n')
                
                found_count += 1
                
                if page_num is None:
                    # Это заголовок главы
                    chapters[chapter_num]['title'] = text
                else:
                    # Это страница
                    page_num_int = int(page_num)
                    chapters[chapter_num]['pages'][page_num_int] = text
            
            i += 1
        
        print(f"✅ Found {found_count} matches for {len(chapters)} chapters")
        
        # Преобразуем словари страниц в списки
        for chapter_num in chapters:
            max_page = max(chapters[chapter_num]['pages'].keys()) if chapters[chapter_num]['pages'] else -1
            pages_list = []
            for p in range(max_page + 1):
                if p in chapters[chapter_num]['pages']:
                    pages_list.append(chapters[chapter_num]['pages'][p])
            chapters[chapter_num]['pages'] = pages_list
        
        return dict(sorted(chapters.items()))
    
    def save_chapters(self, chapters: Dict[int, Dict[str, Any]]):
        """Сохранение глав в JSON"""
        # Сохраняем каждую главу отдельно
        chapters_dir = self.output_dir / 'travels_calradia'
        chapters_dir.mkdir(parents=True, exist_ok=True)
        
        for chapter_num, chapter_data in chapters.items():
            chapter_file = chapters_dir / f'chapter_{chapter_num:02d}.json'
            with open(chapter_file, 'w', encoding='utf-8') as f:
                json.dump(chapter_data, f, ensure_ascii=False, indent=2)
        
        # Сохраняем полный файл
        full_file = self.output_dir / 'travels_calradia.json'
        chapters_list = [chapters[i] for i in sorted(chapters.keys())]
        with open(full_file, 'w', encoding='utf-8') as f:
            json.dump({
                'title': 'Travels in Calradia',
                'total_chapters': len(chapters),
                'chapters': chapters_list
            }, f, ensure_ascii=False, indent=2)
        
        print(f"✅ Saved {len(chapters)} chapters to {chapters_dir}")
        print(f"✅ Saved full file to {full_file.name}")
        
        return len(chapters)
    
    def create_finetuning_format(self, chapters: Dict[int, Dict[str, Any]]) -> List[Dict[str, Any]]:
        """Создание формата для fine-tuning"""
        finetuning_data = []
        
        for chapter_num in sorted(chapters.keys()):
            chapter = chapters[chapter_num]
            
            # Собираем весь текст главы
            full_text_parts = []
            if chapter.get('title'):
                full_text_parts.append(f"Chapter {chapter_num}: {chapter['title']}")
            
            for page_num, page_text in enumerate(chapter.get('pages', [])):
                if page_text and page_text.strip():
                    full_text_parts.append(page_text.strip())
            
            full_text = '\n\n'.join(full_text_parts)
            
            if full_text.strip():
                finetuning_data.append({
                    'id': f'travels_calradia_chapter_{chapter_num}',
                    'type': 'novella',
                    'source': 'digital_companion',
                    'chapter': chapter_num,
                    'title': chapter.get('title'),
                    'text': full_text,
                    'page_count': len(chapter.get('pages', []))
                })
        
        return finetuning_data
    
    def save_finetuning_format(self, finetuning_data: List[Dict[str, Any]]):
        """Сохранение в формате для fine-tuning"""
        output_file = self.output_dir / 'travels_calradia_finetuning.json'
        with open(output_file, 'w', encoding='utf-8') as f:
            json.dump(finetuning_data, f, ensure_ascii=False, indent=2)
        
        print(f"✅ Saved fine-tuning format to {output_file.name}")
        return len(finetuning_data)
    
    def parse(self):
        """Основной метод парсинга"""
        print("=" * 60)
        print("PARSING TRAVELS IN CALRADIA")
        print("=" * 60)
        
        chapters = self.parse_file()
        
        if not chapters:
            print("❌ No chapters found!")
            return
        
        # Сохраняем главы
        chapters_count = self.save_chapters(chapters)
        
        # Создаем формат для fine-tuning
        finetuning_data = self.create_finetuning_format(chapters)
        finetuning_count = self.save_finetuning_format(finetuning_data)
        
        # Статистика
        total_pages = sum(len(ch.get('pages', [])) for ch in chapters.values())
        total_text_length = sum(len(ch.get('text', '')) for ch in finetuning_data)
        
        print("\n" + "=" * 60)
        print("✅ PARSING COMPLETED!")
        print("=" * 60)
        print(f"\n📊 Statistics:")
        print(f"   Chapters: {chapters_count}")
        print(f"   Total pages: {total_pages}")
        print(f"   Fine-tuning entries: {finetuning_count}")
        print(f"   Total text length: {total_text_length:,} characters")
        
        return {
            'chapters': chapters_count,
            'pages': total_pages,
            'entries': finetuning_count,
            'text_length': total_text_length
        }


def main():
    """Main entry point"""
    project_root = Path(__file__).parent.parent
    
    # Пробуем найти файл с полным текстом
    possible_files = [
        project_root / 'wiki_data' / 'travels_calradia' / '1.txt',
        project_root / 'wiki_data' / 'travels_calradia' / 'full_extracted_text.txt'
    ]
    
    input_file = None
    for f in possible_files:
        if f.exists():
            input_file = f
            break
    
    if not input_file:
        print(f"❌ File not found. Tried:")
        for f in possible_files:
            print(f"   {f}")
        return
    
    output_dir = project_root / 'finetuning_data'
    parser = TravelsParser(input_file, output_dir)
    parser.parse()


if __name__ == '__main__':
    main()

