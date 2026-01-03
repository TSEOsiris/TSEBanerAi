#!/usr/bin/env python3
"""
Упрощенный парсер wiki страниц фракций
Использует правильные URL и улучшенный парсинг разделов
"""

import json
import sys
import re
import time
from pathlib import Path
from typing import Dict, List, Optional, Any
from bs4 import BeautifulSoup, Tag, NavigableString

# Настройка кодировки для Windows
if sys.platform == 'win32':
    import io
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8')

try:
    import requests
    from requests.adapters import HTTPAdapter
    from urllib3.util.retry import Retry
except ImportError:
    print("ERROR: requests not installed!")
    print("Please run: pip install requests beautifulsoup4")
    sys.exit(1)


class SimpleFactionWikiParser:
    """Упрощенный парсер wiki страниц фракций"""
    
    def __init__(self, output_dir: Path):
        self.output_dir = output_dir
        self.output_dir.mkdir(parents=True, exist_ok=True)
        
        # Настройка HTTP сессии с retry
        self.session = requests.Session()
        retry_strategy = Retry(
            total=3,
            backoff_factor=1,
            status_forcelist=[429, 500, 502, 503, 504],
        )
        adapter = HTTPAdapter(max_retries=retry_strategy)
        self.session.mount("http://", adapter)
        self.session.mount("https://", adapter)
        self.session.headers.update({
            'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36'
        })
    
    def get_faction_url(self, faction_id: str) -> str:
        """Получить правильный URL для фракции"""
        # Правильные URL для фракций на Fandom
        url_map = {
            'empire': 'https://mountandblade.fandom.com/wiki/Northern_Empire',
            'empire_w': 'https://mountandblade.fandom.com/wiki/Western_Empire',
            'empire_s': 'https://mountandblade.fandom.com/wiki/Southern_Empire',
            'khuzait': 'https://mountandblade.fandom.com/wiki/Khuzait_Khanate',
            'battania': 'https://mountandblade.fandom.com/wiki/Battania',
            'aserai': 'https://mountandblade.fandom.com/wiki/Aserai_Sultanate',
            'sturgia': 'https://mountandblade.fandom.com/wiki/Sturgia',
            'vlandia': 'https://mountandblade.fandom.com/wiki/Vlandia',
            'nord': 'https://mountandblade.fandom.com/wiki/Nord'
        }
        
        return url_map.get(faction_id, f'https://mountandblade.fandom.com/wiki/{faction_id}')
    
    def download_page(self, url: str) -> Optional[str]:
        """Скачать HTML страницу"""
        try:
            response = self.session.get(url, timeout=15)
            response.raise_for_status()
            return response.text
        except Exception as e:
            print(f"   ⚠️  Error downloading {url}: {e}")
            return None
    
    def extract_section_text(self, header, max_level: int = 3) -> str:
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
                if any(skip in classes.lower() for skip in ['navbox', 'infobox', 'mw-editsection', 'toc', 'reference']):
                    current = current.next_sibling
                    continue
                
                # Извлекаем текст из параграфов и списков
                if current.name in ['p', 'li', 'div']:
                    text = current.get_text(separator=' ', strip=True)
                    if text and len(text) > 20:
                        content.append(text)
            
            current = current.next_sibling
        
        return ' '.join(content).strip()
    
    def parse_faction_page(self, html_content: str) -> Dict[str, Any]:
        """Парсить страницу фракции"""
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
            'overview': ['overview', 'description', 'about', 'introduction', 'general'],
            'history': ['history', 'background', 'origin', 'origins', 'past'],
            'troops': ['troops', 'units', 'army', 'military', 'soldiers', 'forces'],
            'tactics': ['tactics', 'strategy', 'combat', 'battle', 'warfare', 'fighting'],
            'economy': ['economy', 'trade', 'resources', 'commerce', 'wealth', 'economic']
        }
        
        for header in all_headers:
            header_text = header.get_text(strip=True).lower()
            headline = header.find('span', class_='mw-headline')
            if headline:
                header_text = headline.get_text(strip=True).lower()
            
            # Проверяем каждый раздел
            for section_key, keywords in section_keywords.items():
                if any(keyword in header_text for keyword in keywords):
                    if not result[section_key]:  # Берем первый найденный
                        text = self.extract_section_text(header)
                        if text and len(text) > 50:
                            result[section_key] = re.sub(r'\s+', ' ', text)
                            print(f"      ✅ Found {section_key}")
        
        return result
    
    def parse_faction(self, faction_id: str, faction_name: str) -> Dict[str, Any]:
        """Парсить фракцию"""
        print(f"\n📖 Parsing: {faction_name} ({faction_id})")
        
        url = self.get_faction_url(faction_id)
        print(f"   🔗 URL: {url}")
        
        html_content = self.download_page(url)
        if not html_content:
            return None
        
        # Проверяем на ошибки
        if 'not found' in html_content.lower() or 'does not exist' in html_content.lower():
            print(f"   ⚠️  Page not found")
            return None
        
        sections = self.parse_faction_page(html_content)
        
        found_count = sum(1 for v in sections.values() if v)
        print(f"   ✅ Found {found_count}/5 sections")
        
        return {
            'id': faction_id,
            'name': faction_name,
            'wiki_url': url,
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
    
    # Загружаем фракции из JSON
    factions_file = project_root / 'finetuning_data' / 'factions.json'
    if not factions_file.exists():
        print(f"❌ Factions file not found: {factions_file}")
        return
    
    with open(factions_file, 'r', encoding='utf-8') as f:
        factions = json.load(f)
    
    # Создаем парсер
    output_dir = project_root / 'wiki_data' / 'factions'
    parser = SimpleFactionWikiParser(output_dir)
    
    print("=" * 60)
    print("PARSING FACTION WIKI PAGES")
    print("=" * 60)
    print(f"\n📊 Found {len(factions)} factions")
    
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
        
        time.sleep(2)  # Задержка между запросами
    
    print("\n" + "=" * 60)
    print("✅ COMPLETED!")
    print("=" * 60)
    print(f"   Parsed: {parsed}")
    print(f"   Failed: {failed}")
    print(f"   Output: {output_dir}")


if __name__ == '__main__':
    main()

