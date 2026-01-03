#!/usr/bin/env python3
"""
Comprehensive wiki downloader - finds and downloads ALL pages from index pages
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
    print("Please run: pip install selenium webdriver-manager")
    exit(1)

from bs4 import BeautifulSoup

class ComprehensiveWikiDownloader:
    def __init__(self, wiki_url, output_dir, language='en'):
        """
        Download ALL wiki pages by finding links from index pages
        """
        self.wiki_url = wiki_url.rstrip('/')
        self.output_dir = Path(output_dir)
        self.language = language
        
        os.makedirs(output_dir, exist_ok=True)
        
        # Index pages to crawl for links
        self.index_pages = [
            'Towns_(Bannerlord)',
            'Castles_(Bannerlord)',
            'Villages_(Bannerlord)',
            'Factions_(Bannerlord)',
            'Clans_(Bannerlord)',
            'Companions_(Bannerlord)',
            'Quests_(Bannerlord)',
            'Characters_(Bannerlord)',
            'Items_(Bannerlord)',
            'Skills_(Bannerlord)',
            'Perks_(Bannerlord)',
        ]
        
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
        
        # Track what we've found and downloaded
        self.found_pages = set()
        self.downloaded_pages = set()
        
    def sanitize_filename(self, page_name):
        """Convert page name to safe filename"""
        filename = page_name.replace('_', ' ')
        filename = re.sub(r'[<>:"/\\|?*]', '_', filename)
        if len(filename) > 200:
            filename = filename[:200]
        return filename + '.html'
    
    def extract_wiki_links(self, html_content, context=''):
        """Extract all wiki page links from HTML"""
        soup = BeautifulSoup(html_content, 'html.parser')
        links = set()
        
        # Find main content area (usually in <div class="mw-parser-output"> or <main>)
        content_area = soup.find('div', class_='mw-parser-output')
        if not content_area:
            content_area = soup.find('main')
        if not content_area:
            content_area = soup  # Fallback to whole page
        
        # Find all links to wiki pages in content area
        for link in content_area.find_all('a', href=True):
            href = link.get('href', '')
            
            # Match wiki links: /wiki/PageName or /ru/wiki/PageName
            match = re.search(r'/wiki/([^?#]+)', href)
            if match:
                page_name = match.group(1)
                
                # Skip special pages
                if any(skip in page_name.lower() for skip in [
                    'special:', 'category:', 'file:', 'template:', 'help:', 
                    'user:', 'talk:', 'mediawiki:', 'wikipedia:', 'warband',
                    'viking', 'napoleonic', 'with fire'
                ]):
                    continue
                
                # Skip pages from other games (not Bannerlord)
                if any(other_game in page_name.lower() for other_game in [
                    '(warband)', '(viking', '(napoleonic', '(with fire'
                ]):
                    continue
                
                # Decode URL encoding
                page_name = page_name.replace('%20', ' ').replace('%26', '&')
                page_name = page_name.replace('%28', '(').replace('%29', ')')
                
                # Remove fragments
                if '#' in page_name:
                    page_name = page_name.split('#')[0]
                
                # Skip if it's the index page itself
                if page_name in self.index_pages:
                    continue
                
                # Only add if link text looks like a page name (not "edit", "view source", etc.)
                link_text = link.get_text().strip().lower()
                skip_texts = ['edit', 'view source', 'history', 'purge', 'talk', 'discussion']
                if any(skip in link_text for skip in skip_texts):
                    continue
                
                links.add(page_name)
        
        return links
    
    def crawl_index_page(self, index_page_name):
        """Crawl an index page and extract all linked pages"""
        url = f"{self.wiki_url}/wiki/{index_page_name}"
        
        print(f"\n[CRAWL] {index_page_name}")
        print(f"  URL: {url}")
        
        try:
            self.driver.get(url)
            time.sleep(3)  # Wait for page to load
            
            # Check for Cloudflare
            if 'Client Challenge' in self.driver.page_source:
                print(f"  -> BLOCKED by Cloudflare")
                return set()
            
            # Extract links
            links = self.extract_wiki_links(self.driver.page_source)
            print(f"  -> Found {len(links)} linked pages")
            
            return links
            
        except Exception as e:
            print(f"  -> ERROR: {e}")
            return set()
    
    def download_page(self, page_name):
        """Download a single page"""
        url = f"{self.wiki_url}/wiki/{page_name}"
        filename = self.sanitize_filename(page_name)
        filepath = self.output_dir / filename
        
        # Skip if already exists and valid
        if filepath.exists():
            file_size = filepath.stat().st_size
            if file_size > 10000:  # Valid file
                return True
        
        try:
            self.driver.get(url)
            time.sleep(2)
            
            # Check for Cloudflare
            html_content = self.driver.page_source
            if 'Client Challenge' in html_content:
                return False
            
            # Save
            with open(filepath, 'w', encoding='utf-8') as f:
                f.write(html_content)
            
            self.downloaded_pages.add(page_name)
            return True
            
        except Exception as e:
            return False
    
    def download_all(self):
        """Main download process"""
        print("=" * 60)
        print("COMPREHENSIVE WIKI DOWNLOADER")
        print("=" * 60)
        print(f"Wiki: {self.wiki_url}")
        print(f"Output: {self.output_dir}\n")
        
        # Step 1: Crawl index pages to find all linked pages
        print("\n[STEP 1] Crawling index pages...")
        all_links = set()
        
        for index_page in self.index_pages:
            links = self.crawl_index_page(index_page)
            all_links.update(links)
            time.sleep(2)  # Be polite
        
        print(f"\n[STEP 1 COMPLETE] Found {len(all_links)} unique pages to download")
        
        # Step 2: Download all found pages
        print(f"\n[STEP 2] Downloading {len(all_links)} pages...\n")
        
        success = 0
        failed = 0
        skipped = 0
        
        for i, page_name in enumerate(sorted(all_links), 1):
            print(f"[{i}/{len(all_links)}] ", end='', flush=True)
            
            filepath = self.output_dir / self.sanitize_filename(page_name)
            if filepath.exists() and filepath.stat().st_size > 10000:
                skipped += 1
                print(f"[SKIP] {page_name[:40]:<40}")
                continue
            
            if self.download_page(page_name):
                success += 1
                print(f"[OK]   {page_name[:40]:<40}")
            else:
                failed += 1
                print(f"[FAIL] {page_name[:40]:<40}")
            
            time.sleep(2)  # Be polite
        
        # Close browser
        self.driver.quit()
        
        # Statistics
        print("\n" + "=" * 60)
        print("DOWNLOAD STATISTICS")
        print("=" * 60)
        print(f"Pages found:    {len(all_links)}")
        print(f"Downloaded:     {success}")
        print(f"Failed:         {failed}")
        print(f"Skipped:        {skipped}")
        print("=" * 60)
        
        return len(all_links), success, failed, skipped


if __name__ == '__main__':
    print("=" * 60)
    print("COMPREHENSIVE WIKI DOWNLOADER")
    print("=" * 60)
    print("\nThis script will:")
    print("1. Crawl index pages (Towns, Castles, Factions, etc.)")
    print("2. Extract ALL linked pages")
    print("3. Download everything found\n")
    
    # English
    print("[1/3] English Wiki...")
    downloader_en = ComprehensiveWikiDownloader(
        wiki_url='https://mountandblade.fandom.com',
        output_dir='../Database/raw/en',
        language='en'
    )
    downloader_en.download_all()
    
    # Russian
    print("\n[2/3] Russian Wiki...")
    downloader_ru = ComprehensiveWikiDownloader(
        wiki_url='https://mountandblade.fandom.com/ru',
        output_dir='../Database/raw/ru',
        language='ru'
    )
    downloader_ru.download_all()
    
    # Turkish
    print("\n[3/3] Turkish Wiki...")
    downloader_tr = ComprehensiveWikiDownloader(
        wiki_url='https://mountandblade.fandom.com/tr',
        output_dir='../Database/raw/tr',
        language='tr'
    )
    downloader_tr.download_all()
    
    print("\n" + "=" * 60)
    print("ALL DONE!")
    print("=" * 60)

