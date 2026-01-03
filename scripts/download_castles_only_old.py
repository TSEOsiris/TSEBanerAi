#!/usr/bin/env python3
"""
Download only castles from List_of_castles page
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
        """
        wiki_url: Base URL (e.g., 'https://mountandblade.fandom.com')
        output_base_dir: Base directory (e.g., '../Database/Wiki_pages/mountandblade.fandom.com')
        """
        self.wiki_url = wiki_url.rstrip('/')
        self.output_base_dir = Path(output_base_dir)
        
        # Faction name mapping (from wiki link to folder name)
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
        
        # Setup Chrome
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
        """Parse castles section and extract castles grouped by faction"""
        soup = BeautifulSoup(html_content, 'html.parser')
        castles_by_faction = {}
        
        # Find Bannerlord section
        bannerlord_start = None
        for h2 in soup.find_all('h2'):
            text = h2.get_text().strip()
            if 'Bannerlord' in text and '[]' in text:
                bannerlord_start = h2
                print(f"  Found Bannerlord section: {text}")
                break
        
        if not bannerlord_start:
            print("ERROR: Could not find Bannerlord section")
            return castles_by_faction
        
        # Find end of Bannerlord section
        bannerlord_end = bannerlord_start.find_next_sibling('h2')
        
        # Strategy: Find all elements containing faction names, then find nearest <ul> after them
        for faction_key, faction_folder in self.faction_map.items():
            # Find all elements that might contain this faction name
            # Look for links to faction pages
            faction_links = soup.find_all('a', href=re.compile(rf'/wiki/{re.escape(faction_key)}'))
            
            for faction_link in faction_links:
                # Check if this link is in Bannerlord section
                parent = faction_link.find_parent()
                if not parent:
                    continue
                
                # Check if we're in Bannerlord section
                in_bannerlord = False
                check = parent
                while check:
                    if check == bannerlord_start:
                        in_bannerlord = True
                        break
                    if check == bannerlord_end:
                        break
                    check = check.find_parent()
                
                if not in_bannerlord:
                    continue
                
                # Find the next <ul> after this faction link
                current = parent.find_next_sibling()
                while current:
                    if current == bannerlord_end:
                        break
                    if current.name == 'h3' or (current.name == 'p' and (current.find('b') or current.find('strong'))):
                        # Hit next faction, stop
                        break
                    if current.name == 'ul':
                        # Found list! Parse it
                        castles = []
                        for li in current.find_all('li'):
                            link = li.find('a', href=True)
                            if not link:
                                continue
                            
                            if li.find('span', class_='new'):
                                continue
                            
                            href = link.get('href', '')
                            # Skip faction links
                            if f'/wiki/{faction_key}' in href:
                                continue
                            if any(skip in href.lower() for skip in ['category:', 'file:', 'template:', 'special:']):
                                continue
                            
                            match = re.search(r'/wiki/([^"?#]+)', href)
                            if match:
                                castle_name = match.group(1)
                                castle_name = castle_name.replace('%20', ' ').replace('%26', '&')
                                castle_name = castle_name.replace('%28', '(').replace('%29', ')')
                                
                                link_text = link.get_text().strip().lower()
                                if any(skip in link_text for skip in ['edit', 'view source', 'history', 'purge', 'talk']):
                                    continue
                                
                                castles.append(castle_name)
                        
                        if castles:
                            if faction_folder not in castles_by_faction:
                                castles_by_faction[faction_folder] = []
                            castles_by_faction[faction_folder].extend(castles)
                            print(f"  {faction_folder}: found {len(castles)} castles")
                        break
                    current = current.find_next_sibling()
        
        # Also check tables for Empire factions
        tables = soup.find_all('table')
        for table in tables:
            # Check if table is in Bannerlord section
            check = table
            in_bannerlord = False
            while check:
                if check == bannerlord_start:
                    in_bannerlord = True
                    break
                if check == bannerlord_end:
                    break
                check = check.find_parent()
            
            if not in_bannerlord:
                continue
            
            # Check if table has Empire faction headers
            headers = table.find_all('th')
            for header in headers:
                links = header.find_all('a', href=True)
                for link in links:
                    href = link.get('href', '')
                    if '/wiki/Northern_Empire' in href:
                        # Parse this column
                        col_idx = headers.index(header)
                        castles = []
                        for row in table.find_all('tr')[1:]:  # Skip header
                            cells = row.find_all('td')
                            if col_idx < len(cells):
                                cell = cells[col_idx]
                                for cell_link in cell.find_all('a', href=True):
                                    cell_href = cell_link.get('href', '')
                                    if '/wiki/Northern_Empire' in cell_href:
                                        continue
                                    match = re.search(r'/wiki/([^"?#]+)', cell_href)
                                    if match:
                                        castle_name = match.group(1)
                                        castle_name = castle_name.replace('%20', ' ').replace('%26', '&')
                                        castle_name = castle_name.replace('%28', '(').replace('%29', ')')
                                        castles.append(castle_name)
                        if castles:
                            if 'Northern Empire' not in castles_by_faction:
                                castles_by_faction['Northern Empire'] = []
                            castles_by_faction['Northern Empire'].extend(castles)
                            print(f"  Northern Empire: found {len(castles)} castles in table")
                    elif '/wiki/Southern_Empire' in href:
                        col_idx = headers.index(header)
                        castles = []
                        for row in table.find_all('tr')[1:]:
                            cells = row.find_all('td')
                            if col_idx < len(cells):
                                cell = cells[col_idx]
                                for cell_link in cell.find_all('a', href=True):
                                    cell_href = cell_link.get('href', '')
                                    if '/wiki/Southern_Empire' in cell_href:
                                        continue
                                    match = re.search(r'/wiki/([^"?#]+)', cell_href)
                                    if match:
                                        castle_name = match.group(1)
                                        castle_name = castle_name.replace('%20', ' ').replace('%26', '&')
                                        castle_name = castle_name.replace('%28', '(').replace('%29', ')')
                                        castles.append(castle_name)
                        if castles:
                            if 'Southern Empire' not in castles_by_faction:
                                castles_by_faction['Southern Empire'] = []
                            castles_by_faction['Southern Empire'].extend(castles)
                            print(f"  Southern Empire: found {len(castles)} castles in table")
                    elif '/wiki/Western_Empire' in href:
                        col_idx = headers.index(header)
                        castles = []
                        for row in table.find_all('tr')[1:]:
                            cells = row.find_all('td')
                            if col_idx < len(cells):
                                cell = cells[col_idx]
                                for cell_link in cell.find_all('a', href=True):
                                    cell_href = cell_link.get('href', '')
                                    if '/wiki/Western_Empire' in cell_href:
                                        continue
                                    match = re.search(r'/wiki/([^"?#]+)', cell_href)
                                    if match:
                                        castle_name = match.group(1)
                                        castle_name = castle_name.replace('%20', ' ').replace('%26', '&')
                                        castle_name = castle_name.replace('%28', '(').replace('%29', ')')
                                        castles.append(castle_name)
                        if castles:
                            if 'Western Empire' not in castles_by_faction:
                                castles_by_faction['Western Empire'] = []
                            castles_by_faction['Western Empire'].extend(castles)
                            print(f"  Western Empire: found {len(castles)} castles in table")
        
        # Remove duplicates
        for faction, castles in castles_by_faction.items():
            castles = list(dict.fromkeys(castles))
            castles_by_faction[faction] = castles
            print(f"  {faction}: {len(castles)} castles (final)")
        
        return castles_by_faction
    
    def download_castle(self, castle_name, faction_folder):
        """Download a single castle page"""
        url = f"{self.wiki_url}/wiki/{castle_name}"
        
        # Create output path: Geography/fiefs/castles/{faction}/{castle}.html
        output_dir = self.output_base_dir / 'Geography' / 'fiefs' / 'castles' / faction_folder
        output_dir.mkdir(parents=True, exist_ok=True)
        
        # Filename: {castle_name} _ Mount & Blade Wiki _ Fandom.html
        filename = f"{castle_name.replace('_', ' ')} _ Mount & Blade Wiki _ Fandom.html"
        filepath = output_dir / filename
        
        # Skip if already exists and valid
        if filepath.exists():
            file_size = filepath.stat().st_size
            if file_size > 10000:
                return True
        
        try:
            self.driver.get(url)
            time.sleep(2)
            
            html_content = self.driver.page_source
            
            # Check for Cloudflare
            if 'Client Challenge' in html_content:
                return False
            
            # Save
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
        
        # Step 1: Load Castles_(Bannerlord) page (similar to Towns_(Bannerlord))
        print("[STEP 1] Loading Castles_(Bannerlord) page...")
        url = f"{self.wiki_url}/wiki/Castles_(Bannerlord)"
        
        try:
            self.driver.get(url)
            time.sleep(3)
            
            if 'Client Challenge' in self.driver.page_source:
                print("ERROR: Blocked by Cloudflare")
                self.driver.quit()
                return
            
            # Step 2: Parse castles table
            print("\n[STEP 2] Parsing castles table...")
            castles_by_faction = self.parse_castles_table(self.driver.page_source)
            
            if not castles_by_faction:
                print("ERROR: No castles found in Bannerlord section")
                self.driver.quit()
                return
            
            total_castles = sum(len(castles) for castles in castles_by_faction.values())
            print(f"\n[STEP 2 COMPLETE] Found {total_castles} castles in {len(castles_by_faction)} factions")
            
            # Step 3: Download all castles
            print(f"\n[STEP 3] Downloading castles...\n")
            
            success = 0
            failed = 0
            skipped = 0
            
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
                    
                    time.sleep(2)  # Be polite
            
            # Close browser
            self.driver.quit()
            
            # Statistics
            print("\n" + "=" * 60)
            print("DOWNLOAD STATISTICS")
            print("=" * 60)
            print(f"Total castles: {total_castles}")
            print(f"Downloaded:    {success}")
            print(f"Failed:        {failed}")
            print(f"Skipped:       {skipped}")
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
    
    # English only for now
    # Get absolute path to project root
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
    print("\nCheck the output folder and manually copy to your database if successful.")

