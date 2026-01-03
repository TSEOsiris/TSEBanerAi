#!/usr/bin/env python3
"""
Парсинг локальных HTML файлов wiki страниц фракций
Извлекает разделы: Overview, History, Troops, Tactics, Economy
"""

import json
import sys
import re
from pathlib import Path
from typing import Dict, List, Optional, Any
from bs4 import BeautifulSoup, Tag, NavigableString

# Настройка кодировки для Windows
if sys.platform == 'win32':
    import io
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8')


class LocalFactionWikiParser:
    """Парсер локальных HTML файлов wiki страниц фракций"""
    
    def __init__(self, html_dir: Path, output_dir: Path):
        self.html_dir = html_dir
        self.output_dir = output_dir
        self.output_dir.mkdir(parents=True, exist_ok=True)
        
        # Маппинг ID фракций на имена файлов
        self.faction_file_map = {
            'empire': 'Northern Empire _ Mount & Blade Wiki _ Fandom.html',
            'empire_w': 'Western Empire _ Mount & Blade Wiki _ Fandom.html',
            'empire_s': 'Southern Empire _ Mount & Blade Wiki _ Fandom.html',
            'khuzait': 'Khuzait _ Mount & Blade Wiki _ Fandom.html',
            'battania': 'Battania _ Mount & Blade Wiki _ Fandom.html',
            'aserai': 'Aserai _ Mount & Blade Wiki _ Fandom.html',
            'sturgia': 'Sturgia _ Mount & Blade Wiki _ Fandom.html',
            'vlandia': 'Vlandia _ Mount & Blade Wiki _ Fandom.html',
            'nord': 'Nords (Bannerlord) _ Mount & Blade Wiki _ Fandom.html'
        }
    
    def find_html_file(self, faction_id: str) -> Optional[Path]:
        """Найти HTML файл для фракции"""
        # Пробуем по маппингу
        if faction_id in self.faction_file_map:
            file_path = self.html_dir / self.faction_file_map[faction_id]
            if file_path.exists():
                return file_path
        
        # Пробуем найти по части имени
        faction_name_map = {
            'empire': 'Northern Empire',
            'empire_w': 'Western Empire',
            'empire_s': 'Southern Empire',
            'khuzait': 'Khuzait',
            'battania': 'Battania',
            'aserai': 'Aserai',
            'sturgia': 'Sturgia',
            'vlandia': 'Vlandia',
            'nord': 'Nords'
        }
        
        if faction_id in faction_name_map:
            search_name = faction_name_map[faction_id]
            for html_file in self.html_dir.glob('*.html'):
                if search_name in html_file.name:
                    return html_file
        
        return None
    
    def extract_section_text(self, header, section_type: str = 'general') -> str:
        """Извлечь текст раздела начиная с заголовка"""
        content = []
        current = header.next_sibling
        header_level = int(header.name[1]) if header.name.startswith('h') else 3
        
        while current:
            # Если встретили заголовок того же или более высокого уровня - стоп
            if isinstance(current, Tag) and current.name and current.name.startswith('h'):
                current_level = int(current.name[1])
                if current_level <= header_level:
                    break
            
            # Пропускаем служебные элементы
            if isinstance(current, Tag):
                classes = ' '.join(current.get('class', []))
                if any(skip in classes.lower() for skip in ['navbox', 'infobox', 'mw-editsection', 'toc', 'reference', 'gallery']):
                    current = current.next_sibling
                    continue
                
                # Для раздела Troops - извлекаем текст из таблиц
                if section_type == 'troops' and current.name == 'table':
                    # Извлекаем названия юнитов из таблицы
                    table_text = current.get_text(separator=' | ', strip=True)
                    if table_text and len(table_text) > 50:
                        # Очищаем от лишних символов
                        table_text = re.sub(r'\s+', ' ', table_text)
                        content.append(table_text)
                    current = current.next_sibling
                    continue
                
                # Пропускаем таблицы навигации и инфобоксы (кроме Troops)
                if current.name in ['table']:
                    classes = ' '.join(current.get('class', []))
                    if 'infobox' in classes.lower() or 'navbox' in classes.lower():
                        current = current.next_sibling
                        continue
                
                # Извлекаем текст из параграфов, списков и div
                if current.name in ['p', 'li', 'div', 'ul', 'ol', 'dl', 'dt', 'dd']:
                    text = current.get_text(separator=' ', strip=True)
                    if text and len(text) > 20:
                        # Фильтруем служебные тексты
                        if not any(skip in text.lower() for skip in [
                            'please enable javascript',
                            'terms of use',
                            'privacy policy',
                            'cookie policy',
                            'fandom',
                            'this page does not exist',
                            'main article:'
                        ]):
                            content.append(text)
            
            current = current.next_sibling
        
        return ' '.join(content).strip()
    
    def parse_faction_page(self, html_file: Path) -> Dict[str, Any]:
        """Парсить HTML файл фракции"""
        try:
            with open(html_file, 'r', encoding='utf-8') as f:
                html_content = f.read()
        except Exception as e:
            print(f"   ⚠️  Error reading file: {e}")
            return {
                'overview': None,
                'history': None,
                'troops': None,
                'tactics': None,
                'economy': None
            }
        
        soup = BeautifulSoup(html_content, 'html.parser')
        
        # Найти основную область контента
        content_area = soup.find('div', class_='mw-parser-output')
        if not content_area:
            content_area = soup.find('div', id='content')
        if not content_area:
            content_area = soup
        
        result = {
            'overview': None,
            'history': None,
            'troops': None,
            'tactics': None,
            'economy': None
        }
        
        # Ищем все заголовки
        headers = content_area.find_all(['h2', 'h3'])
        
        # Также ищем через span.mw-headline
        headlines = content_area.find_all('span', class_='mw-headline')
        
        # Объединяем заголовки
        all_headers = []
        for h in headers:
            all_headers.append(h)
        
        for headline in headlines:
            parent = headline.parent
            if parent and parent.name in ['h2', 'h3'] and parent not in all_headers:
                all_headers.append(parent)
        
        # Ищем разделы (расширенный список ключевых слов)
        section_keywords = {
            'overview': ['overview', 'description', 'about', 'introduction', 'general', 'summary'],
            'history': ['history', 'background', 'origin', 'origins', 'past', 'historical'],
            'troops': ['troops', 'units', 'army', 'military', 'soldiers', 'forces', 'recruitment'],
            'tactics': ['tactics', 'strategy', 'combat', 'battle', 'warfare', 'fighting', 'tactical'],
            'economy': ['economy', 'trade', 'resources', 'commerce', 'wealth', 'economic', 'economy and trade']
        }
        
        for header in all_headers:
            header_text = header.get_text(strip=True).lower()
            headline = header.find('span', class_='mw-headline')
            if headline:
                header_text = headline.get_text(strip=True).lower()
            
            # Также проверяем id заголовка
            header_id = header.get('id', '').lower()
            combined_text = f"{header_text} {header_id}"
            
            # Проверяем каждый раздел
            for section_key, keywords in section_keywords.items():
                if not result[section_key]:  # Берем первый найденный
                    if any(keyword in combined_text for keyword in keywords):
                        text = self.extract_section_text(header, section_type=section_key)
                        if text and len(text) > 50:
                            # Очищаем от лишних пробелов и ссылок
                            text = re.sub(r'\[.*?\]', '', text)  # Убираем ссылки [1], [2] и т.д.
                            text = re.sub(r'\s+', ' ', text)  # Убираем множественные пробелы
                            result[section_key] = text.strip()
                            print(f"      ✅ Found {section_key}")
                        
                        # Специальная обработка для Tactics - ищем Economy внутри
                        if section_key == 'tactics' and result['tactics']:
                            # Ищем Economy внутри раздела Tactics
                            tactics_section = header
                            tactics_header_level = int(header.name[1]) if header.name.startswith('h') else 3
                            current = tactics_section.next_sibling
                            while current:
                                if isinstance(current, Tag) and current.name == 'dl':
                                    dt = current.find('dt')
                                    if dt and 'economy' in dt.get_text(strip=True).lower():
                                        # Извлекаем текст Economy
                                        dd = current.find('dd')
                                        if dd:
                                            economy_text = dd.get_text(separator=' ', strip=True)
                                        else:
                                            # Ищем следующий элемент после dt
                                            next_elem = current.find_next(['ul', 'ol', 'p'])
                                            if next_elem:
                                                economy_text = next_elem.get_text(separator=' ', strip=True)
                                            else:
                                                economy_text = None
                                        
                                        if economy_text and len(economy_text) > 20:
                                            economy_text = re.sub(r'\[.*?\]', '', economy_text)
                                            economy_text = re.sub(r'\s+', ' ', economy_text)
                                            result['economy'] = economy_text.strip()
                                            print(f"      ✅ Found economy (inside tactics)")
                                            break
                                
                                # Если встретили следующий заголовок - стоп
                                if isinstance(current, Tag) and current.name and current.name.startswith('h'):
                                    current_level = int(current.name[1])
                                    if current_level <= tactics_header_level:
                                        break
                                
                                current = current.next_sibling
        
        return result
    
    def parse_faction(self, faction_id: str, faction_name: str) -> Dict[str, Any]:
        """Парсить фракцию"""
        print(f"\n📖 Parsing: {faction_name} ({faction_id})")
        
        html_file = self.find_html_file(faction_id)
        if not html_file:
            print(f"   ⚠️  HTML file not found")
            return None
        
        print(f"   📄 File: {html_file.name}")
        
        sections = self.parse_faction_page(html_file)
        
        found_count = sum(1 for v in sections.values() if v)
        print(f"   ✅ Found {found_count}/5 sections")
        
        return {
            'id': faction_id,
            'name': faction_name,
            'wiki_url': f"https://mountandblade.fandom.com/wiki/{faction_name.replace(' ', '_')}",
            'sections': sections
        }
    
    def save_faction(self, faction_data: Dict[str, Any]):
        """Сохранить данные фракции"""
        if not faction_data:
            return
        
        filename = f"{faction_data['id']}.json"
        output_file = self.output_dir / filename
        
        with open(output_file, 'w', encoding='utf-8') as f:
            json.dump(faction_data, f, ensure_ascii=False, indent=2)
        
        print(f"   💾 Saved to {filename}")


def main():
    """Main entry point"""
    project_root = Path(__file__).parent.parent
    
    # Путь к HTML файлам
    html_dir = project_root / 'Database' / 'Wiki_pages' / 'mountandblade.fandom.com' / 'Factions'
    if not html_dir.exists():
        print(f"❌ HTML directory not found: {html_dir}")
        return
    
    # Загружаем фракции из JSON
    factions_file = project_root / 'finetuning_data' / 'factions.json'
    if not factions_file.exists():
        print(f"❌ Factions file not found: {factions_file}")
        return
    
    with open(factions_file, 'r', encoding='utf-8') as f:
        factions = json.load(f)
    
    # Создаем парсер
    output_dir = project_root / 'wiki_data' / 'factions'
    parser = LocalFactionWikiParser(html_dir, output_dir)
    
    print("=" * 60)
    print("PARSING LOCAL FACTION WIKI PAGES")
    print("=" * 60)
    print(f"\n📊 Found {len(factions)} factions")
    print(f"📁 HTML directory: {html_dir}")
    
    parsed = 0
    failed = 0
    
    for faction in factions:
        faction_id = faction.get('id', '')
        faction_name = faction.get('name_en', '') or faction.get('name', '')
        
        if not faction_id:
            continue
        
        try:
            result = parser.parse_faction(faction_id, faction_name)
            if result:
                parser.save_faction(result)
                parsed += 1
            else:
                failed += 1
        except Exception as e:
            print(f"   ❌ Error: {e}")
            failed += 1
    
    print("\n" + "=" * 60)
    print("✅ COMPLETED!")
    print("=" * 60)
    print(f"   Parsed: {parsed}")
    print(f"   Failed: {failed}")
    print(f"   Output: {output_dir}")


if __name__ == '__main__':
    main()

