#!/usr/bin/env python3
"""
Парсинг wiki страниц фракций и извлечение разделов:
- Overview
- History
- Troops
- Tactics
- Economy

Сохраняет в отдельные JSON файлы
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
    print("Please run: pip install requests")
    sys.exit(1)

try:
    from selenium import webdriver
    from selenium.webdriver.chrome.service import Service
    from selenium.webdriver.chrome.options import Options
    from selenium.webdriver.common.by import By
    from selenium.webdriver.support.ui import WebDriverWait
    from selenium.webdriver.support import expected_conditions as EC
    USE_SELENIUM = True
    try:
        from webdriver_manager.chrome import ChromeDriverManager
        USE_WEBDRIVER_MANAGER = True
    except ImportError:
        USE_WEBDRIVER_MANAGER = False
except ImportError:
    USE_SELENIUM = False
    print("⚠️  Selenium not available, will use requests only")


class FactionWikiParser:
    """Парсер wiki страниц фракций"""
    
    def __init__(self, output_dir: Path, use_selenium=True):
        self.output_dir = output_dir
        self.output_dir.mkdir(parents=True, exist_ok=True)
        self.use_selenium = use_selenium and USE_SELENIUM
        
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
            'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36'
        })
        
        # Настройка Selenium если доступен
        self.driver = None
        if self.use_selenium:
            try:
                chrome_options = Options()
                chrome_options.add_argument('--headless')
                chrome_options.add_argument('--no-sandbox')
                chrome_options.add_argument('--disable-dev-shm-usage')
                chrome_options.add_argument('--disable-blink-features=AutomationControlled')
                chrome_options.add_experimental_option("excludeSwitches", ["enable-automation"])
                chrome_options.add_experimental_option('useAutomationExtension', False)
                chrome_options.add_argument('user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36')
                
                if USE_WEBDRIVER_MANAGER:
                    service = Service(ChromeDriverManager().install())
                    self.driver = webdriver.Chrome(service=service, options=chrome_options)
                else:
                    self.driver = webdriver.Chrome(options=chrome_options)
                print("✅ Selenium initialized")
            except Exception as e:
                print(f"⚠️  Could not initialize Selenium: {e}")
                self.use_selenium = False
                self.driver = None
    
    def get_faction_wiki_url(self, faction_id: str, faction_name: str) -> Optional[str]:
        """Получить wiki URL для фракции"""
        # Стандартный формат URL для Bannerlord wiki
        base_url = "https://bannerlord.fandom.com/wiki"
        
        # Маппинг ID фракций на правильные названия страниц
        faction_url_map = {
            'empire': 'Northern_Empire',
            'empire_w': 'Western_Empire',
            'empire_s': 'Southern_Empire',
            'khuzait': 'Khuzait_Khanate',
            'battania': 'Battania',
            'aserai': 'Aserai_Sultanate',
            'sturgia': 'Sturgia',
            'vlandia': 'Vlandia',
            'nord': 'Nord'
        }
        
        # Используем маппинг если есть
        if faction_id in faction_url_map:
            page_name = faction_url_map[faction_id]
        else:
            # Преобразуем ID/название в формат URL
            page_name = faction_name.replace(' ', '_')
            if not page_name:
                page_name = faction_id.replace('_', ' ').title().replace(' ', '_')
        
        # Проверяем разные варианты
        possible_names = [
            page_name,
            faction_name.replace(' ', '_'),
            faction_id.replace('_', ' ').title().replace(' ', '_'),
            f"{page_name}_(Bannerlord)",
            f"{faction_name.replace(' ', '_')}_(Bannerlord)"
        ]
        
        for name in possible_names:
            url = f"{base_url}/{name}"
            # Проверяем, существует ли страница (делаем HEAD запрос)
            try:
                response = self.session.head(url, timeout=5, allow_redirects=True)
                if response.status_code == 200:
                    # Проверяем, что это не страница ошибки
                    if 'not a valid community' not in response.text.lower():
                        return url
            except:
                continue
        
        # Если не нашли, возвращаем наиболее вероятный URL
        return f"{base_url}/{page_name}"
    
    def download_page(self, url: str) -> Optional[str]:
        """Скачать HTML страницу"""
        # Используем Selenium если доступен (для JavaScript)
        if self.use_selenium and self.driver:
            try:
                self.driver.get(url)
                # Ждем загрузки контента
                WebDriverWait(self.driver, 10).until(
                    EC.presence_of_element_located((By.TAG_NAME, "body"))
                )
                time.sleep(2)  # Дополнительное время для загрузки JS
                return self.driver.page_source
            except Exception as e:
                print(f"   ⚠️  Error downloading with Selenium: {e}")
                # Fallback to requests
                pass
        
        # Fallback: обычный HTTP запрос
        try:
            response = self.session.get(url, timeout=10)
            response.raise_for_status()
            return response.text
        except Exception as e:
            print(f"   ⚠️  Error downloading {url}: {e}")
            return None
    
    def extract_section(self, soup: BeautifulSoup, section_title: str) -> Optional[str]:
        """Извлечь текст из раздела по заголовку"""
        # Ищем заголовок (h1, h2, h3, h4, span с классом mw-headline)
        headers = soup.find_all(['h1', 'h2', 'h3', 'h4'])
        
        # Также ищем span с классом mw-headline (используется в Fandom)
        headlines = soup.find_all('span', class_='mw-headline')
        for headline in headlines:
            # Создаем псевдо-заголовок для обработки
            parent = headline.parent
            if parent and parent.name in ['h1', 'h2', 'h3', 'h4']:
                if parent not in headers:
                    headers.append(parent)
        
        # Также ищем через id (иногда разделы имеют id="Overview" и т.д.)
        section_id = soup.find(id=section_title.lower())
        if section_id:
            parent_header = section_id.find_parent(['h1', 'h2', 'h3', 'h4'])
            if parent_header and parent_header not in headers:
                headers.append(parent_header)
        
        for header in headers:
            # Проверяем, содержит ли заголовок нужный текст
            header_text = header.get_text(strip=True).lower()
            headline_text = header.find('span', class_='mw-headline')
            if headline_text:
                header_text = headline_text.get_text(strip=True).lower()
            
            # Также проверяем id заголовка
            header_id = header.get('id', '').lower()
            if section_title.lower() in header_text or section_title.lower() in header_id:
                # Собираем весь текст до следующего заголовка того же или более высокого уровня
                section_content = []
                current = header.next_sibling
                
                header_level = int(header.name[1]) if header.name.startswith('h') else 3
                
                while current:
                    # Если встретили заголовок того же или более высокого уровня - стоп
                    if current.name and current.name.startswith('h'):
                        current_level = int(current.name[1])
                        if current_level <= header_level:
                            break
                    
                    # Пропускаем навигационные элементы и рекламу
                    if isinstance(current, Tag):
                        # Пропускаем таблицы навигации, инфобоксы и т.д.
                        if current.get('class'):
                            classes = ' '.join(current.get('class', []))
                            if any(skip in classes.lower() for skip in ['navbox', 'infobox', 'mw-editsection', 'toc']):
                                current = current.next_sibling
                                continue
                        
                        text = current.get_text(separator=' ', strip=True)
                        # Фильтруем слишком короткие или служебные тексты
                        if text and len(text) > 20 and 'please enable javascript' not in text.lower():
                            section_content.append(text)
                    elif isinstance(current, NavigableString):
                        text = str(current).strip()
                        if text and len(text) > 20:
                            section_content.append(text)
                    
                    current = current.next_sibling
                
                if section_content:
                    result = ' '.join(section_content).strip()
                    # Очищаем от лишних пробелов
                    result = re.sub(r'\s+', ' ', result)
                    return result
        
        return None
    
    def parse_faction_page(self, html_content: str) -> Dict[str, Any]:
        """Парсить страницу фракции и извлечь все разделы"""
        soup = BeautifulSoup(html_content, 'html.parser')
        
        # Проверяем, не получили ли мы страницу с ошибкой
        html_lower = html_content.lower()
        if ('not a valid community' in html_lower or 
            'page not found' in html_lower or 
            'this page does not exist' in html_lower or
            'does not have an article' in html_lower):
            print(f"   ⚠️  Page not found or invalid")
            return {
                'overview': None,
                'history': None,
                'troops': None,
                'tactics': None,
                'economy': None
            }
        
        # Найти основную область контента - ищем более точно
        content_area = None
        
        # Пробуем разные варианты поиска контента
        content_selectors = [
            ('div', {'class': 'mw-parser-output'}),
            ('div', {'id': 'content'}),
            ('main', {}),
            ('article', {}),
            ('div', {'class': 'page-content'}),
            ('div', {'class': 'WikiaPage'}),
        ]
        
        for tag, attrs in content_selectors:
            content_area = soup.find(tag, attrs)
            if content_area:
                break
        
        # Если не нашли, ищем по структуре - контент обычно между header и footer
        if not content_area:
            # Ищем элемент с классом, содержащим "content" или "main"
            for div in soup.find_all('div'):
                classes = ' '.join(div.get('class', []))
                if 'content' in classes.lower() or 'main' in classes.lower():
                    # Проверяем, что это не футер или навигация
                    if 'footer' not in classes.lower() and 'nav' not in classes.lower():
                        content_area = div
                        break
        
        if not content_area:
            content_area = soup
        
        result = {
            'overview': None,
            'history': None,
            'troops': None,
            'tactics': None,
            'economy': None
        }
        
        # Отладка: выводим все заголовки для понимания структуры
        all_headers = content_area.find_all(['h1', 'h2', 'h3', 'h4'])
        header_texts = [h.get_text(strip=True) for h in all_headers[:15]]
        if header_texts:
            print(f"   📋 Found headers: {', '.join(header_texts[:8])}")
        
        # Также ищем span с классом mw-headline (используется в Fandom)
        headlines = content_area.find_all('span', class_='mw-headline')
        if headlines:
            headline_texts = [h.get_text(strip=True) for h in headlines[:10]]
            print(f"   📋 Found headlines: {', '.join(headline_texts[:8])}")
        
        # Извлекаем каждый раздел
        result['overview'] = self.extract_section(content_area, 'Overview')
        result['history'] = self.extract_section(content_area, 'History')
        result['troops'] = self.extract_section(content_area, 'Troops')
        result['tactics'] = self.extract_section(content_area, 'Tactics')
        result['economy'] = self.extract_section(content_area, 'Economy')
        
        # Если не нашли через заголовки, попробуем найти через инфобоксы и таблицы
        if not result['overview']:
            # Ищем первый параграф после заголовка страницы (но не в инфобоксе)
            paragraphs = content_area.find_all('p')
            for p in paragraphs:
                # Пропускаем параграфы в инфобоксах, навигации и футерах
                parent = p.parent
                skip = False
                while parent:
                    if parent.get('class'):
                        classes = ' '.join(parent.get('class', []))
                        if any(skip_class in classes.lower() for skip_class in ['infobox', 'navbox', 'footer', 'navigation', 'sidebar']):
                            skip = True
                            break
                    if parent.name in ['footer', 'nav', 'header']:
                        skip = True
                        break
                    parent = parent.parent
                
                if skip:
                    continue
                
                text = p.get_text(separator=' ', strip=True)
                # Фильтруем служебные тексты, но менее строго
                if (text and len(text) > 30 and 
                    'please enable javascript' not in text.lower() and
                    not text.lower().startswith('what is fandom') and
                    'terms of use' not in text.lower() and
                    'privacy policy' not in text.lower() and
                    'digital services act' not in text.lower()):
                    result['overview'] = text
                    break
        
        return result
    
    def parse_faction(self, faction_id: str, faction_name: str, wiki_url: Optional[str] = None) -> Dict[str, Any]:
        """Парсить фракцию"""
        print(f"\n📖 Parsing faction: {faction_name} ({faction_id})")
        
        # Получаем URL если не передан
        if not wiki_url:
            wiki_url = self.get_faction_wiki_url(faction_id, faction_name)
            print(f"   🔗 Wiki URL: {wiki_url}")
        
        # Скачиваем страницу
        html_content = self.download_page(wiki_url)
        if not html_content:
            print(f"   ❌ Failed to download page")
            return None
        
        # Проверяем, не получили ли мы страницу с требованием JavaScript
        if 'please enable javascript' in html_content.lower():
            print(f"   ⚠️  Page requires JavaScript, but Selenium not available or failed")
        
        # Парсим страницу
        sections = self.parse_faction_page(html_content)
        
        # Формируем результат
        result = {
            'id': faction_id,
            'name': faction_name,
            'wiki_url': wiki_url,
            'sections': sections
        }
        
        # Подсчитываем найденные разделы (исключаем служебные сообщения)
        found_sections = sum(1 for v in sections.values() 
                           if v and 'please enable javascript' not in v.lower() and len(v) > 20)
        print(f"   ✅ Found {found_sections}/5 sections")
        
        return result
    
    def close(self):
        """Закрыть браузер если используется Selenium"""
        if self.driver:
            self.driver.quit()
    
    def save_faction(self, faction_data: Dict[str, Any]):
        """Сохранить данные фракции в JSON"""
        if not faction_data:
            return
        
        filename = f"{faction_data['id']}.json"
        output_file = self.output_dir / filename
        
        with open(output_file, 'w', encoding='utf-8') as f:
            json.dump(faction_data, f, ensure_ascii=False, indent=2)
        
        print(f"   💾 Saved to {filename}")


def get_factions_from_db():
    """Получить фракции из базы данных с wiki_url"""
    import sqlite3
    project_root = Path(__file__).parent.parent
    db_path = project_root / 'Database' / 'bannerlord_lore.db'
    
    if not db_path.exists():
        return None
    
    conn = sqlite3.connect(str(db_path))
    conn.row_factory = sqlite3.Row
    cursor = conn.cursor()
    
    # Получаем из kingdoms таблицы
    cursor.execute('''
        SELECT id, name, wiki_url
        FROM kingdoms
        WHERE wiki_url IS NOT NULL AND wiki_url != ''
    ''')
    
    factions = []
    for row in cursor.fetchall():
        factions.append({
            'id': row['id'],
            'name': row['name'],
            'wiki_url': row['wiki_url']
        })
    
    conn.close()
    return factions


def main():
    """Main entry point"""
    project_root = Path(__file__).parent.parent
    
    # Пробуем получить фракции из базы данных
    factions = get_factions_from_db()
    
    # Если не получилось, загружаем из JSON
    if not factions:
        factions_file = project_root / 'finetuning_data' / 'factions.json'
        if not factions_file.exists():
            print(f"❌ Factions file not found: {factions_file}")
            print("   Please run export_finetuning_data.py first")
            return
        
        with open(factions_file, 'r', encoding='utf-8') as f:
            factions = json.load(f)
    
    # Создаем парсер
    output_dir = project_root / 'wiki_data' / 'factions'
    parser = FactionWikiParser(output_dir)
    
    print("=" * 60)
    print("PARSING FACTION WIKI PAGES")
    print("=" * 60)
    print(f"\n📊 Found {len(factions)} factions to parse")
    
    parsed_count = 0
    failed_count = 0
    
    try:
        for faction in factions:
            faction_id = faction.get('id', '')
            faction_name = faction.get('name_en', '') or faction.get('name', '')
            wiki_url = faction.get('wiki_url', '')
            
            if not faction_id or not faction_name:
                continue
            
            try:
                # Используем wiki_url из базы данных, если есть
                result = parser.parse_faction(faction_id, faction_name, wiki_url if wiki_url else None)
                if result:
                    parser.save_faction(result)
                    parsed_count += 1
                else:
                    failed_count += 1
            except Exception as e:
                print(f"   ❌ Error parsing {faction_name}: {e}")
                failed_count += 1
            
            # Небольшая задержка между запросами
            time.sleep(1)
    finally:
        # Закрываем браузер если использовали Selenium
        parser.close()
    
    print("\n" + "=" * 60)
    print("✅ PARSING COMPLETED!")
    print("=" * 60)
    print(f"\n📊 Statistics:")
    print(f"   Parsed: {parsed_count}")
    print(f"   Failed: {failed_count}")
    print(f"   Total: {len(factions)}")
    print(f"\n📁 Output directory: {output_dir}")


if __name__ == '__main__':
    main()

