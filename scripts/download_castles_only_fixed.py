#!/usr/bin/env python3
"""
Download only castles from Castles_(Bannerlord) page
Sort by faction: Geography/fiefs/castles/{faction}/{castle}.html
"""
import time
import os
from pathlib import Path
import re

try:
    from selenium import webdriver
    from selenium.webdriver.chrome.service import Service
    from selenium.webdriver.chrome.options import Options
    from selenium.webdriver.common.by import By
    from selenium.webdriver.support.ui import WebDriverWait
    from selenium.webdriver.support import expected_conditions as EC
    
    try:
        from webdriver_manager.chrome import ChromeDriverManager
        USE_WEBDRIVER_MANAGER = True
    except ImportError:
        USE_WEBDRIVER_MANAGER = False
except ImportError:
    print("ERROR: selenium not installed!")
    exit(1)

from bs4 import BeautifulSoup

class CastlesDownloader:
    def __init__(self, wiki_url, output_base_dir):
        self.wiki_url = wiki_url.rstrip('/')
        self.output_base_dir = Path(output_base_dir)
        
        self.faction_map = {
            'Aserai': 'Aserai Sultanate',
            'Battania': 'High Kingdom of Battania',
            'Khuzait': 'Khuzait Khanate',
            'Sturgia': 'Principality of Sturgia',
            'Vlandia': 'Kingdom of Vlandia',
            'Northern_Empire': 'Northern Empire',
            'Southern_Empire': 'Southern Empire',
            'Western_Empire': 'Western Empire',
            'Nords_(Bannerlord)': 'Kingdom of Nordvyg',
            'Nordvyg': 'Kingdom of Nordvyg'
        }
        
        chrome_options = Options()
        chrome_options.add_argument('--headless')
        chrome_options.add_argument('--no-sandbox')
        chrome_options.add_argument('--disable-dev-shm-usage')
        chrome_options.add_argument('--disable-blink-features=AutomationControlled')
        chrome_options.add_experimental_option("excludeSwitches", ["enable-automation"])
        chrome_options.add_experimental_option('useAutomationExtension', False)
        chrome_options.add_argument('user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36')
        
        try:
            if USE_WEBDRIVER_MANAGER:
                service = Service(ChromeDriverManager().install())
                self.driver = webdriver.Chrome(service=service, options=chrome_options)
            else:
                self.driver = webdriver.Chrome(options=chrome_options)
        except Exception as e:
            print(f"ERROR: Could not start Chrome: {e}")
            exit(1)
    
    def parse_castles_table(self, html_content):
        """Parse castles lists and extract castles grouped by faction"""
        soup = BeautifulSoup(html_content, 'html.parser')
        castles_by_faction = {}
        
        # Find Bannerlord section
        bannerlord_section = None
        for h2 in soup.find_all('h2'):
            text = h2.get_text().strip()
            if 'Bannerlord' in text and '[]' in text:
                bannerlord_section = h2
                print(f"  Found Bannerlord section: {text}")
                break
        
        if not bannerlord_section:
            print("ERROR: Could not find Bannerlord section")
            return castles_by_faction
        
        # Find end of Bannerlord section
        bannerlord_end = bannerlord_section.find_next_sibling('h2')
        
        # Find all h3 headings (faction names) in Bannerlord section
        current = bannerlord_section.find_next_sibling()
        while current and current != bannerlord_end:
            if current.name == 'h3':
                text = current.get_text().strip()
                # Check if this is a faction
                faction_key = None
                faction_folder = None
                for key, value in self.faction_map.items():
                    if key == text or value == text:
                        faction_key = key
                        faction_folder = value
                        break
                
                if faction_key:
                    print(f"  Found faction: {faction_folder}")
                    # Find next <ul> list
                    next_elem = current.find_next_sibling()
                    while next_elem and next_elem != bannerlord_end:
                        if next_elem.name == 'h3':  # Next faction
                            break
                        if next_elem.name == 'ul':
                            castles = []
                            for li in next_elem.find_all('li'):
                                link = li.find('a', href=True)
                                if not link:
                                    continue
                                if li.find('span', class_='new'):
                                    continue
                                
                                href = link.get('href', '')
                                if f'/wiki/{faction_key}' in href:
                                    continue
                                if any(skip in href.lower() for skip in ['category:', 'file:', 'template:', 'special:']):
                                    continue
                                
                                match = re.search(r'/wiki/([^"?#]+)', href)
                                if match:
                                    castle_name = match.group(1)
                                    castle_name = castle_name.replace('%20', ' ').replace('%26', '&')
                                    castle_name = castle_name.replace('%28', '(').replace('%29', ')')
                                    castles.append(castle_name)
                            
                            if castles:
                                castles_by_faction[faction_folder] = castles
                                print(f"    {faction_folder}: {len(castles)} castles")
                            break
                        next_elem = next_elem.find_next_sibling()
            
            current = current.find_next_sibling()
        
        return castles_by_faction
    
    def download_castle(self, castle_name, faction_folder):
        """Download a single castle page"""
        url = f"{self.wiki_url}/wiki/{castle_name}"
        
        output_dir = self.output_base_dir / 'Geography' / 'fiefs' / 'castles' / faction_folder
        output_dir.mkdir(parents=True, exist_ok=True)
        
        filename = f"{castle_name.replace('_', ' ')} _ Mount & Blade Wiki _ Fandom.html"
        filepath = output_dir / filename
        
        if filepath.exists():
            file_size = filepath.stat().st_size
            if file_size > 10000:
                return True
        
        try:
            self.driver.set_page_load_timeout(30)  # 30 second timeout
            self.driver.get(url)
            time.sleep(2)
            
            html_content = self.driver.page_source
            
            if 'Client Challenge' in html_content:
                return False
            
            with open(filepath, 'w', encoding='utf-8') as f:
                f.write(html_content)
            
            return True
            
        except Exception as e:
            print(f"    ERROR: {e}")
            return False
    
    def download_all_castles(self):
        """Main download process"""
        print("=" * 60)
        print("CASTLES DOWNLOADER")
        print("=" * 60)
        print(f"Wiki: {self.wiki_url}")
        print(f"Output: {self.output_base_dir}/Geography/fiefs/castles/\n")
        
        print("[STEP 1] Loading List_of_castles page...")
        url = f"{self.wiki_url}/wiki/List_of_castles"
        
        try:
            self.driver.get(url)
            time.sleep(3)
            
            if 'Client Challenge' in self.driver.page_source:
                print("ERROR: Blocked by Cloudflare")
                self.driver.quit()
                return
            
            print("\n[STEP 2] Parsing castles lists...")
            castles_by_faction = self.parse_castles_table(self.driver.page_source)
            
            if not castles_by_faction:
                print("ERROR: No castles found in Bannerlord section")
                self.driver.quit()
                return
            
            total_castles = sum(len(castles) for castles in castles_by_faction.values())
            print(f"\n[STEP 2 COMPLETE] Found {total_castles} castles in {len(castles_by_faction)} factions")
            
            print(f"\n[STEP 3] Downloading castles...\n")
            
            success = 0
            failed = 0
            
            for faction_folder, castles in castles_by_faction.items():
                print(f"\n[{faction_folder}]")
                
                for i, castle_name in enumerate(castles, 1):
                    print(f"  [{i}/{len(castles)}] {castle_name:<30} ", end='', flush=True)
                    
                    if self.download_castle(castle_name, faction_folder):
                        success += 1
                        print("-> OK")
                    else:
                        failed += 1
                        print("-> FAIL")
                    
                    time.sleep(2)
            
            self.driver.quit()
            
            print("\n" + "=" * 60)
            print("DOWNLOAD STATISTICS")
            print("=" * 60)
            print(f"Total castles: {total_castles}")
            print(f"Downloaded:    {success}")
            print(f"Failed:        {failed}")
            print("=" * 60)
            print(f"\nFiles saved to: {self.output_base_dir}/Geography/fiefs/castles/")
            
        except Exception as e:
            print(f"ERROR: {e}")
            self.driver.quit()


if __name__ == '__main__':
    print("=" * 60)
    print("CASTLES DOWNLOADER")
    print("=" * 60)
    print("\nThis script will:")
    print("1. Load Castles_(Bannerlord) page")
    print("2. Parse castles table (grouped by faction)")
    print("3. Download all castles")
    print("4. Sort by: Geography/fiefs/castles/{faction}/{castle}.html\n")
    
    script_dir = Path(__file__).parent
    project_root = script_dir.parent
    output_dir = project_root / 'Database' / 'Wiki_pages' / 'mountandblade.fandom.com'
    
    downloader = CastlesDownloader(
        wiki_url='https://mountandblade.fandom.com',
        output_base_dir=str(output_dir)
    )
    downloader.download_all_castles()
    
    print("\n" + "=" * 60)
    print("DONE!")
    print("=" * 60)

