#!/usr/bin/env python3
"""
Парсинг Travels in Calradia на разных языках и описаний фракций
Обрабатывает файлы в разных форматах (XML, string tags)
"""

import re
import json
import sys
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Dict, List, Any, Optional
from collections import defaultdict

# Настройка кодировки для Windows
if sys.platform == 'win32':
    import io
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8')


class MultilangTravelsParser:
    """Парсер для Travels in Calradia на разных языках"""
    
    def __init__(self, input_dir: Path, output_dir: Path):
        self.input_dir = input_dir
        self.output_dir = output_dir
        self.output_dir.mkdir(parents=True, exist_ok=True)
        
    def parse_russian_file(self, file_path: Path) -> Dict[int, Dict[str, Any]]:
        """Парсинг русского файла (1.txt) - формат string tags"""
        print(f"📖 Parsing Russian file: {file_path.name}")
        
        with open(file_path, 'r', encoding='utf-8', errors='ignore') as f:
            lines = f.readlines()
        
        chapters: Dict[int, Dict[str, Any]] = defaultdict(lambda: {
            'chapter': 0,
            'title': None,
            'pages': {}
        })
        
        start_pattern = r'<string\s+id="travels_in_calradia_chapter_(\d+)_(?:page_(\d+)|title)"\s+text="'
        end_pattern = r'"\s*/>'
        
        i = 0
        found_count = 0
        
        while i < len(lines):
            line = lines[i]
            start_match = re.search(start_pattern, line, re.IGNORECASE)
            if start_match:
                chapter_num = int(start_match.group(1))
                page_num = start_match.group(2)
                
                chapters[chapter_num]['chapter'] = chapter_num
                text_start = start_match.end()
                
                if '" />' in line[text_start:]:
                    text_end = line.find('" />', text_start)
                    text = line[text_start:text_end]
                else:
                    text_parts = [line[text_start:].rstrip()]
                    i += 1
                    while i < len(lines):
                        next_line = lines[i]
                        if '" />' in next_line:
                            text_end = next_line.find('" />')
                            text_parts.append(next_line[:text_end])
                            break
                        else:
                            text_parts.append(next_line.rstrip())
                        i += 1
                    text = ''.join(text_parts)
                
                text = text.replace('\\"', '"').replace('\\n', '\n').replace('\\t', '\t')
                text = text.replace('{newline}', '\n')
                
                found_count += 1
                
                if page_num is None:
                    chapters[chapter_num]['title'] = text
                else:
                    page_num_int = int(page_num)
                    chapters[chapter_num]['pages'][page_num_int] = text
            
            i += 1
        
        print(f"   ✅ Found {found_count} matches for {len(chapters)} chapters")
        return dict(sorted(chapters.items()))
    
    def parse_english_xml(self, file_path: Path) -> Dict[int, Dict[str, Any]]:
        """Парсинг английского файла (3.txt) - формат XML"""
        print(f"📖 Parsing English XML file: {file_path.name}")
        
        with open(file_path, 'r', encoding='utf-8', errors='ignore') as f:
            content = f.read()
        
        # Используем regex для парсинга, так как XML может быть невалидным
        return self.parse_english_regex(content)
    
    def parse_english_regex(self, content: str) -> Dict[int, Dict[str, Any]]:
        """Парсинг английского файла через regex"""
        chapters: Dict[int, Dict[str, Any]] = defaultdict(lambda: {
            'chapter': 0,
            'title': None,
            'pages': {}
        })
        
        # Ищем Chapter теги (многострочный режим)
        chapter_pattern = r'<Chapter\s+Index="(\d+)"\s+Title="([^"]*)"[^>]*>'
        
        # Находим все главы
        chapter_matches = list(re.finditer(chapter_pattern, content, re.MULTILINE))
        found_count = 0
        
        for i, chapter_match in enumerate(chapter_matches):
            chapter_num = int(chapter_match.group(1))
            title = chapter_match.group(2)
            
            chapters[chapter_num]['chapter'] = chapter_num
            chapters[chapter_num]['title'] = title if title else None
            
            # Определяем границы главы
            chapter_start = chapter_match.end()
            if i + 1 < len(chapter_matches):
                chapter_end = chapter_matches[i + 1].start()
            else:
                chapter_end = content.find('</Book>', chapter_start)
                if chapter_end == -1:
                    chapter_end = len(content)
            
            chapter_content = content[chapter_start:chapter_end]
            
            # Ищем страницы в этой главе (многострочный паттерн)
            # Формат: <Page Index="0" ... Text="...текст..." />
            page_pattern = r'<Page\s+Index="(\d+)"[^>]*Text="([^"]*)"\s*/>'
            
            for page_match in re.finditer(page_pattern, chapter_content, re.DOTALL):
                page_num = int(page_match.group(1))
                text = page_match.group(2).strip()
                # Очищаем от экранированных символов
                text = text.replace('&quot;', '"').replace('&lt;', '<').replace('&gt;', '>')
                chapters[chapter_num]['pages'][page_num] = text
                found_count += 1
        
        print(f"   ✅ Found {found_count} pages for {len(chapters)} chapters")
        return dict(sorted(chapters.items()))
    
    def parse_turkish_xml(self, file_path: Path) -> Dict[int, Dict[str, Any]]:
        """Парсинг турецкого файла (2.txt) - формат XML"""
        print(f"📖 Parsing Turkish XML file: {file_path.name}")
        
        with open(file_path, 'r', encoding='utf-8', errors='ignore') as f:
            content = f.read()
        
        chapters: Dict[int, Dict[str, Any]] = defaultdict(lambda: {
            'chapter': 0,
            'title': None,
            'pages': {}
        })
        
        # Парсим string теги
        pattern = r'<string\s+id="travels_in_calradia_chapter_(\d+)_(?:page_(\d+)|title)"\s+text="([^"]*)"\s*/>'
        
        matches = re.finditer(pattern, content, re.IGNORECASE)
        found_count = 0
        
        for match in matches:
            found_count += 1
            chapter_num = int(match.group(1))
            page_num = match.group(2)
            text = match.group(3)
            
            chapters[chapter_num]['chapter'] = chapter_num
            
            # Декодируем HTML entities
            text = text.replace('&quot;', '"').replace('&lt;', '<').replace('&gt;', '>')
            text = text.replace('{newline}', '\n')
            
            if page_num is None:
                chapters[chapter_num]['title'] = text
            else:
                page_num_int = int(page_num)
                chapters[chapter_num]['pages'][page_num_int] = text
        
        print(f"   ✅ Found {found_count} matches for {len(chapters)} chapters")
        return dict(sorted(chapters.items()))
    
    def parse_faction_descriptions(self, file_path: Path) -> Dict[str, str]:
        """Парсинг описаний фракций из 4.txt"""
        print(f"📖 Parsing faction descriptions: {file_path.name}")
        
        with open(file_path, 'r', encoding='utf-8', errors='ignore') as f:
            content = f.read()
        
        descriptions = {}
        
        # Паттерн для описаний фракций
        pattern = r'<string\s+id="calradia_map_description_(\w+)"\s+text="([^"]*)"\s*/>'
        
        matches = re.finditer(pattern, content, re.IGNORECASE)
        found_count = 0
        
        for match in matches:
            faction_id = match.group(1)
            text = match.group(2)
            
            # Декодируем
            text = text.replace('{newline}', '\n')
            descriptions[faction_id] = text
            found_count += 1
        
        # Также ищем concept_arts_description
        pattern2 = r'<string\s+id="concept_arts_description_(\w+)"\s+text="([^"]*)"\s*/>'
        matches2 = re.finditer(pattern2, content, re.IGNORECASE)
        
        for match in matches2:
            faction_id = match.group(1)
            text = match.group(2)
            text = text.replace('{newline}', '\n')
            key = f"concept_arts_{faction_id}"
            descriptions[key] = text
            found_count += 1
        
        print(f"   ✅ Found {found_count} descriptions")
        return descriptions
    
    def normalize_chapters(self, chapters: Dict[int, Dict[str, Any]]) -> Dict[int, Dict[str, Any]]:
        """Нормализация глав: преобразование словарей страниц в списки"""
        for chapter_num in chapters:
            if chapters[chapter_num]['pages']:
                max_page = max(chapters[chapter_num]['pages'].keys())
                pages_list = []
                for p in range(max_page + 1):
                    if p in chapters[chapter_num]['pages']:
                        pages_list.append(chapters[chapter_num]['pages'][p])
                chapters[chapter_num]['pages'] = pages_list
            else:
                chapters[chapter_num]['pages'] = []
        return chapters
    
    def create_finetuning_format(self, chapters: Dict[int, Dict[str, Any]], language: str) -> List[Dict[str, Any]]:
        """Создание формата для fine-tuning"""
        finetuning_data = []
        
        for chapter_num in sorted(chapters.keys()):
            chapter = chapters[chapter_num]
            
            full_text_parts = []
            if chapter.get('title'):
                full_text_parts.append(f"Chapter {chapter_num}: {chapter['title']}")
            
            for page_text in chapter.get('pages', []):
                if page_text and page_text.strip():
                    full_text_parts.append(page_text.strip())
            
            full_text = '\n\n'.join(full_text_parts)
            
            if full_text.strip():
                finetuning_data.append({
                    'id': f'travels_calradia_chapter_{chapter_num}_{language}',
                    'type': 'novella',
                    'source': 'digital_companion',
                    'language': language,
                    'chapter': chapter_num,
                    'title': chapter.get('title'),
                    'text': full_text,
                    'page_count': len(chapter.get('pages', []))
                })
        
        return finetuning_data
    
    def parse_all(self):
        """Парсинг всех файлов"""
        print("=" * 80)
        print("PARSING MULTILINGUAL TRAVELS IN CALRADIA")
        print("=" * 80)
        
        all_data = {
            'ru': {},
            'en': {},
            'tr': {},
            'faction_descriptions': {}
        }
        
        # Русский (1.txt)
        ru_file = self.input_dir / '1.txt'
        if ru_file.exists():
            all_data['ru'] = self.parse_russian_file(ru_file)
            all_data['ru'] = self.normalize_chapters(all_data['ru'])
        
        # Английский (3.txt)
        en_file = self.input_dir / '3.txt'
        if en_file.exists():
            all_data['en'] = self.parse_english_xml(en_file)
            all_data['en'] = self.normalize_chapters(all_data['en'])
        
        # Турецкий (2.txt)
        tr_file = self.input_dir / '2.txt'
        if tr_file.exists():
            all_data['tr'] = self.parse_turkish_xml(tr_file)
            all_data['tr'] = self.normalize_chapters(all_data['tr'])
        
        # Описания фракций (4.txt)
        factions_file = self.input_dir / '4.txt'
        if factions_file.exists():
            all_data['faction_descriptions'] = self.parse_faction_descriptions(factions_file)
        
        # Сохраняем результаты
        print("\n" + "=" * 80)
        print("SAVING RESULTS")
        print("=" * 80)
        
        # Сохраняем по языкам
        for lang in ['ru', 'en', 'tr']:
            if all_data[lang]:
                chapters_dir = self.output_dir / 'travels_calradia' / lang
                chapters_dir.mkdir(parents=True, exist_ok=True)
                
                for chapter_num, chapter_data in all_data[lang].items():
                    chapter_file = chapters_dir / f'chapter_{chapter_num:02d}.json'
                    with open(chapter_file, 'w', encoding='utf-8') as f:
                        json.dump(chapter_data, f, ensure_ascii=False, indent=2)
                
                # Полный файл
                full_file = self.output_dir / f'travels_calradia_{lang}.json'
                chapters_list = [all_data[lang][i] for i in sorted(all_data[lang].keys())]
                with open(full_file, 'w', encoding='utf-8') as f:
                    json.dump({
                        'title': 'Travels in Calradia',
                        'language': lang,
                        'total_chapters': len(all_data[lang]),
                        'chapters': chapters_list
                    }, f, ensure_ascii=False, indent=2)
                
                # Fine-tuning формат
                finetuning_data = self.create_finetuning_format(all_data[lang], lang)
                finetuning_file = self.output_dir / f'travels_calradia_finetuning_{lang}.json'
                with open(finetuning_file, 'w', encoding='utf-8') as f:
                    json.dump(finetuning_data, f, ensure_ascii=False, indent=2)
                
                print(f"✅ Saved {len(all_data[lang])} {lang.upper()} chapters")
        
        # Сохраняем описания фракций
        if all_data['faction_descriptions']:
            factions_file = self.output_dir / 'faction_descriptions_ru.json'
            with open(factions_file, 'w', encoding='utf-8') as f:
                json.dump(all_data['faction_descriptions'], f, ensure_ascii=False, indent=2)
            print(f"✅ Saved {len(all_data['faction_descriptions'])} faction descriptions")
        
        # Статистика
        print("\n" + "=" * 80)
        print("✅ PARSING COMPLETED!")
        print("=" * 80)
        
        total_chapters = sum(len(all_data[lang]) for lang in ['ru', 'en', 'tr'])
        total_text_length = 0
        
        for lang in ['ru', 'en', 'tr']:
            if all_data[lang]:
                lang_text = sum(
                    len(' '.join(ch.get('pages', []))) 
                    for ch in all_data[lang].values()
                )
                total_text_length += lang_text
                print(f"\n📊 {lang.upper()}:")
                print(f"   Chapters: {len(all_data[lang])}")
                print(f"   Text length: {lang_text:,} characters")
        
        print(f"\n📊 TOTAL:")
        print(f"   Chapters: {total_chapters}")
        print(f"   Total text: {total_text_length:,} characters")
        print(f"   Faction descriptions: {len(all_data['faction_descriptions'])}")
        
        return all_data


def main():
    """Main entry point"""
    project_root = Path(__file__).parent.parent
    input_dir = project_root / 'wiki_data' / 'travels_calradia'
    output_dir = project_root / 'finetuning_data'
    
    if not input_dir.exists():
        print(f"❌ Directory not found: {input_dir}")
        return
    
    parser = MultilangTravelsParser(input_dir, output_dir)
    parser.parse_all()


if __name__ == '__main__':
    main()

