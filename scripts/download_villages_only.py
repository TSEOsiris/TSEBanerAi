#!/usr/bin/env python3
"""
Download only villages from Villages_(Bannerlord) page
Sort by faction: Geography/fiefs/villages/{faction}/{village}.html
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

class VillagesDownloader:
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
            'Nords_(Bannerlord)': 'Kingdom of Nordvyg'
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
    
    def parse_villages_collapsible(self, html_content):
        """Parse collapsible blocks and extract villages grouped by faction"""
        soup = BeautifulSoup(html_content, 'html.parser')
        villages_by_faction = {}
        
        # Find all collapsible blocks (look for divs with both classes)
        collapsible_blocks = soup.find_all('div', class_=lambda x: x and 'mw-collapsible' in x and 'mw-made-collapsible' in x)
        
        # Filter only Bannerlord sections (skip Mount&Blade, Warband, etc.)
        bannerlord_section = None
        next_section = None
        
        for h2 in soup.find_all('h2'):
            text = h2.get_text().strip()
            # Look for "Bannerlord" heading - it should be exactly "Bannerlord[]" or contain "Bannerlord"
            if text == 'Bannerlord[]' or (text.startswith('Bannerlord') and '[]' in text):
                bannerlord_section = h2
                print(f"  Found Bannerlord section: {text}")
                # Find next h2 to know where to stop
                next_section = h2.find_next_sibling('h2')
                if next_section:
                    print(f"  Next section: {next_section.get_text().strip()}")
                break
        
        if not bannerlord_section:
            print("ERROR: Could not find Bannerlord section")
            return villages_by_faction
        
        # Get all collapsible blocks after Bannerlord heading, but before next h2
        bannerlord_blocks = []
        current = bannerlord_section.find_next_sibling()
        while current:
            # Stop at next h2 (next game section like "Maps[]" or another game)
            if current.name == 'h2':
                print(f"  Stopped at section: {current.get_text().strip()}")
                break
            # Look for collapsible divs
            if current.name == 'div' and current.get('class'):
                classes = current.get('class')
                if isinstance(classes, list):
                    classes_str = ' '.join(classes)
                else:
                    classes_str = str(classes)
                if 'mw-collapsible' in classes_str and 'mw-made-collapsible' in classes_str:
                    # Double-check: make sure this block is for Bannerlord factions
                    # by checking if faction name is in our map
                    faction_dl = current.find('dl')
                    if faction_dl:
                        faction_a = faction_dl.find('a', href=True)
                        if faction_a:
                            faction_href = faction_a.get('href', '')
                            match = re.search(r'/wiki/([^"?#]+)', faction_href)
                            if match:
                                faction_name = match.group(1)
                                if faction_name in self.faction_map:
                                    bannerlord_blocks.append(current)
            current = current.find_next_sibling()
        
        print(f"  Found {len(bannerlord_blocks)} Bannerlord faction blocks")
        
        for block in bannerlord_blocks:
            # Find faction name in <dl><dd><b><a>
            faction_dl = block.find('dl')
            if not faction_dl:
                continue
            
            faction_a = faction_dl.find('a', href=True)
            if not faction_a:
                continue
            
            faction_href = faction_a.get('href', '')
            # Extract faction name from /wiki/FactionName
            match = re.search(r'/wiki/([^"?#]+)', faction_href)
            if not match:
                continue
            
            faction_name = match.group(1)
            
            # Map to folder name
            folder_name = self.faction_map.get(faction_name, faction_name)
            
            # Find all village links in <ul><li><a>
            # Look in mw-collapsible-content div
            content_div = block.find('div', class_='mw-collapsible-content')
            if not content_div:
                continue
            
            village_list = content_div.find('ul')
            if not village_list:
                continue
            
            villages = []
            for li in village_list.find_all('li'):
                # Check if it's a "new" page (doesn't exist)
                if li.find('span', class_='new'):
                    continue
                
                link = li.find('a', href=True)
                if not link:
                    continue
                
                href = link.get('href', '')
                
                # Extract village name
                match = re.search(r'/wiki/([^"?#]+)', href)
                if match:
                    village_name = match.group(1)
                    # Decode URL encoding
                    village_name = village_name.replace('%20', ' ').replace('%26', '&')
                    village_name = village_name.replace('%28', '(').replace('%29', ')')
                    
                    villages.append(village_name)
            
            if villages:
                villages_by_faction[folder_name] = villages
                print(f"  {folder_name}: {len(villages)} villages")
        
        return villages_by_faction
    
    def download_village(self, village_name, faction_folder):
        """Download a single village page"""
        url = f"{self.wiki_url}/wiki/{village_name}"
        
        # Create output path: Geography/fiefs/villages/{faction}/{village}.html
        output_dir = self.output_base_dir / 'Geography' / 'fiefs' / 'villages' / faction_folder
        output_dir.mkdir(parents=True, exist_ok=True)
        
        # Filename: {village_name} _ Mount & Blade Wiki _ Fandom.html
        filename = f"{village_name.replace('_', ' ')} _ Mount & Blade Wiki _ Fandom.html"
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
    
    def download_all_villages(self):
        """Main download process"""
        print("=" * 60)
        print("VILLAGES DOWNLOADER")
        print("=" * 60)
        print(f"Wiki: {self.wiki_url}")
        print(f"Output: {self.output_base_dir}/Geography/fiefs/villages/\n")
        
        # Step 1: Load List_of_villages page
        print("[STEP 1] Loading List_of_villages page...")
        url = f"{self.wiki_url}/wiki/List_of_villages"
        
        try:
            self.driver.get(url)
            time.sleep(3)
            
            if 'Client Challenge' in self.driver.page_source:
                print("ERROR: Blocked by Cloudflare")
                self.driver.quit()
                return
            
            # Step 2: Parse collapsible blocks
            print("\n[STEP 2] Parsing villages collapsible blocks...")
            villages_by_faction = self.parse_villages_collapsible(self.driver.page_source)
            
            if not villages_by_faction:
                print("ERROR: No villages found in collapsible blocks")
                self.driver.quit()
                return
            
            total_villages = sum(len(villages) for villages in villages_by_faction.values())
            print(f"\n[STEP 2 COMPLETE] Found {total_villages} villages in {len(villages_by_faction)} factions")
            
            # Step 3: Download all villages
            print(f"\n[STEP 3] Downloading villages...\n")
            
            success = 0
            failed = 0
            skipped = 0
            
            for faction_folder, villages in villages_by_faction.items():
                print(f"\n[{faction_folder}]")
                
                for i, village_name in enumerate(villages, 1):
                    print(f"  [{i}/{len(villages)}] {village_name:<30} ", end='', flush=True)
                    
                    if self.download_village(village_name, faction_folder):
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
            print(f"Total villages: {total_villages}")
            print(f"Downloaded:     {success}")
            print(f"Failed:         {failed}")
            print(f"Skipped:        {skipped}")
            print("=" * 60)
            print(f"\nFiles saved to: {self.output_base_dir}/Geography/fiefs/villages/")
            
        except Exception as e:
            print(f"ERROR: {e}")
            self.driver.quit()


if __name__ == '__main__':
    print("=" * 60)
    print("VILLAGES DOWNLOADER")
    print("=" * 60)
    print("\nThis script will:")
    print("1. Load List_of_villages page")
    print("2. Parse collapsible blocks (grouped by faction)")
    print("3. Download all villages")
    print("4. Sort by: Geography/fiefs/villages/{faction}/{village}.html\n")
    
    # English only for now
    # Get absolute path to project root
    script_dir = Path(__file__).parent
    project_root = script_dir.parent
    output_dir = project_root / 'Database' / 'Wiki_pages' / 'mountandblade.fandom.com'
    
    downloader = VillagesDownloader(
        wiki_url='https://mountandblade.fandom.com',
        output_base_dir=str(output_dir)
    )
    downloader.download_all_villages()
    
    print("\n" + "=" * 60)
    print("DONE!")
    print("=" * 60)
    print("\nCheck the output folder and manually copy to your database if successful.")

