#!/usr/bin/env python3
"""
Download only towns from Towns_(Bannerlord) page
Sort by faction: Geography/fiefs/towns/{faction}/{town}.html
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

class TownsDownloader:
    def __init__(self, wiki_url, output_base_dir):
        """
        wiki_url: Base URL (e.g., 'https://mountandblade.fandom.com')
        output_base_dir: Base directory (e.g., '../Database/Wiki_pages/mountandblade.fandom.com')
        """
        self.wiki_url = wiki_url.rstrip('/')
        self.output_base_dir = Path(output_base_dir)
        
        # Faction name mapping (from table header to folder name)
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
    
    def parse_towns_table(self, html_content):
        """Parse towns table and extract towns grouped by faction"""
        soup = BeautifulSoup(html_content, 'html.parser')
        towns_by_faction = {}
        
        # Find the table with towns
        # Look for <tbody> with <tr> containing <th> elements with faction names
        tbody = soup.find('tbody')
        if not tbody:
            print("ERROR: Could not find towns table")
            return towns_by_faction
        
        # Find all <th> elements (each column is a faction)
        th_elements = tbody.find_all('th')
        
        for th in th_elements:
            # Get faction name from first link in <th>
            faction_link = th.find('a', href=True)
            if not faction_link:
                continue
            
            faction_href = faction_link.get('href', '')
            # Extract faction name from /wiki/FactionName
            match = re.search(r'/wiki/([^"]+)', faction_href)
            if not match:
                continue
            
            faction_name = match.group(1)
            
            # Map to folder name
            folder_name = self.faction_map.get(faction_name, faction_name)
            
            # Find all town links in this column
            town_links = th.find_all('a', href=True)
            towns = []
            
            for link in town_links:
                href = link.get('href', '')
                # Skip faction link itself
                if href == f'/wiki/{faction_name}':
                    continue
                
                # Extract town name
                match = re.search(r'/wiki/([^"?#]+)', href)
                if match:
                    town_name = match.group(1)
                    # Decode URL encoding
                    town_name = town_name.replace('%20', ' ').replace('%26', '&')
                    town_name = town_name.replace('%28', '(').replace('%29', ')')
                    
                    # Skip if it's a "new" page (doesn't exist)
                    if link.find_parent('span', class_='new'):
                        print(f"  [SKIP] {town_name} (page does not exist)")
                        continue
                    
                    towns.append(town_name)
            
            if towns:
                towns_by_faction[folder_name] = towns
                print(f"  {folder_name}: {len(towns)} towns")
        
        return towns_by_faction
    
    def download_town(self, town_name, faction_folder):
        """Download a single town page"""
        url = f"{self.wiki_url}/wiki/{town_name}"
        
        # Create output path: Geography/fiefs/towns/{faction}/{town}.html
        output_dir = self.output_base_dir / 'Geography' / 'fiefs' / 'towns' / faction_folder
        output_dir.mkdir(parents=True, exist_ok=True)
        
        # Filename: {town_name} _ Mount & Blade Wiki _ Fandom.html
        filename = f"{town_name.replace('_', ' ')} _ Mount & Blade Wiki _ Fandom.html"
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
    
    def download_all_towns(self):
        """Main download process"""
        print("=" * 60)
        print("TOWNS DOWNLOADER")
        print("=" * 60)
        print(f"Wiki: {self.wiki_url}")
        print(f"Output: {self.output_base_dir}/Geography/fiefs/towns/\n")
        
        # Step 1: Load Towns_(Bannerlord) page
        print("[STEP 1] Loading Towns_(Bannerlord) page...")
        url = f"{self.wiki_url}/wiki/Towns_(Bannerlord)"
        
        try:
            self.driver.get(url)
            time.sleep(3)
            
            if 'Client Challenge' in self.driver.page_source:
                print("ERROR: Blocked by Cloudflare")
                self.driver.quit()
                return
            
            # Step 2: Parse table
            print("\n[STEP 2] Parsing towns table...")
            towns_by_faction = self.parse_towns_table(self.driver.page_source)
            
            if not towns_by_faction:
                print("ERROR: No towns found in table")
                self.driver.quit()
                return
            
            total_towns = sum(len(towns) for towns in towns_by_faction.values())
            print(f"\n[STEP 2 COMPLETE] Found {total_towns} towns in {len(towns_by_faction)} factions")
            
            # Step 3: Download all towns
            print(f"\n[STEP 3] Downloading towns...\n")
            
            success = 0
            failed = 0
            skipped = 0
            
            for faction_folder, towns in towns_by_faction.items():
                print(f"\n[{faction_folder}]")
                
                for i, town_name in enumerate(towns, 1):
                    print(f"  [{i}/{len(towns)}] {town_name:<30} ", end='', flush=True)
                    
                    if self.download_town(town_name, faction_folder):
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
            print(f"Total towns:    {total_towns}")
            print(f"Downloaded:     {success}")
            print(f"Failed:         {failed}")
            print(f"Skipped:        {skipped}")
            print("=" * 60)
            print(f"\nFiles saved to: {self.output_base_dir}/Geography/fiefs/towns/")
            
        except Exception as e:
            print(f"ERROR: {e}")
            self.driver.quit()


if __name__ == '__main__':
    print("=" * 60)
    print("TOWNS DOWNLOADER")
    print("=" * 60)
    print("\nThis script will:")
    print("1. Load Towns_(Bannerlord) page")
    print("2. Parse towns table (grouped by faction)")
    print("3. Download all towns")
    print("4. Sort by: Geography/fiefs/towns/{faction}/{town}.html\n")
    
    # English only for now
    # Get absolute path to project root
    script_dir = Path(__file__).parent
    project_root = script_dir.parent
    output_dir = project_root / 'Database' / 'Wiki_pages' / 'mountandblade.fandom.com'
    
    downloader = TownsDownloader(
        wiki_url='https://mountandblade.fandom.com',
        output_base_dir=str(output_dir)
    )
    downloader.download_all_towns()
    
    print("\n" + "=" * 60)
    print("DONE!")
    print("=" * 60)
    print("\nCheck the output folder and manually copy to your database if successful.")

