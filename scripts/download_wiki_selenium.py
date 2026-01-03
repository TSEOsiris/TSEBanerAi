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
    
    # Try to use webdriver-manager for auto ChromeDriver download
    try:
        from webdriver_manager.chrome import ChromeDriverManager
        USE_WEBDRIVER_MANAGER = True
    except ImportError:
        USE_WEBDRIVER_MANAGER = False
        print("INFO: webdriver-manager not installed. Will try system ChromeDriver.")
except ImportError:
    print("ERROR: selenium not installed!")
    print("Please run: pip install selenium webdriver-manager")
    exit(1)

class SeleniumWikiDownloader:
    def __init__(self, wiki_url, output_dir, language='en'):
        """
        Download wiki pages using Selenium (real browser)
        """
        self.wiki_url = wiki_url.rstrip('/')
        self.output_dir = Path(output_dir)
        self.language = language
        
        os.makedirs(output_dir, exist_ok=True)
        
        # Setup Chrome options
        chrome_options = Options()
        chrome_options.add_argument('--headless')  # Run without GUI (faster)
        chrome_options.add_argument('--no-sandbox')
        chrome_options.add_argument('--disable-dev-shm-usage')
        chrome_options.add_argument('--disable-blink-features=AutomationControlled')
        chrome_options.add_experimental_option("excludeSwitches", ["enable-automation"])
        chrome_options.add_experimental_option('useAutomationExtension', False)
        
        # User agent to look like real browser
        chrome_options.add_argument('user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36')
        
        # Initialize driver
        try:
            if USE_WEBDRIVER_MANAGER:
                # Auto-download ChromeDriver
                service = Service(ChromeDriverManager().install())
                self.driver = webdriver.Chrome(service=service, options=chrome_options)
            else:
                # Use system ChromeDriver
                self.driver = webdriver.Chrome(options=chrome_options)
        except Exception as e:
            print(f"\nERROR: Could not start Chrome driver: {e}")
            print("\nPlease install:")
            print("1. Chrome browser: https://www.google.com/chrome/")
            print("2. Python packages: pip install selenium webdriver-manager")
            print("\nOr download ChromeDriver manually:")
            print("   https://googlechromelabs.github.io/chrome-for-testing/")
            exit(1)
        
        # Page list to download
        self.pages_to_download = self.get_page_list()
    
    def get_page_list(self):
        """Get comprehensive list of Bannerlord pages"""
        pages = []
        
        # Factions/Kingdoms
        factions = [
            'Vlandia', 'Sturgia', 'Battania',
            'Northern_Empire', 'Southern_Empire', 'Western_Empire',
            'Aserai', 'Khuzait_Khanate'
        ]
        pages.extend(factions)
        
        # Major characters/leaders
        leaders = [
            'Derthert', 'Raganvad', 'Caladog',
            'Lucon', 'Rhagaea', 'Garios',
            'Monchug', 'Unqid'
        ]
        pages.extend(leaders)
        
        # Major towns
        towns = [
            'Pravend', 'Jaculan', 'Galend', 'Sargot', 'Marunath', 'Charas',  # Vlandia
            'Revyl', 'Varcheg', 'Tyal', 'Ov_Castle', 'Ustokol',  # Sturgia
            'Pen_Cannoc', 'Dunglanys', 'Seonon', 'Car_Banseth',  # Battania
            'Epicrotea', 'Onira', 'Lycaron', 'Myzea', 'Amitatys', 'Rhotae', 'Zeonica',  # Empire
            'Quyaz', 'Makeb', 'Razih', 'Hubyar', 'Iyakis',  # Aserai
            'Akkalat', 'Chaikand', 'Baltakhand', 'Odrysa'  # Khuzait
        ]
        pages.extend(towns)
        
        # Game mechanics
        mechanics = [
            'Marriage_(Bannerlord)', 'Vassalage', 'Trading', 
            'Companions_(Bannerlord)', 'Workshops', 'Caravans',
            'Skills_(Bannerlord)', 'Perks_(Bannerlord)',
            'Clans_(Bannerlord)', 'Armies_(Bannerlord)'
        ]
        pages.extend(mechanics)
        
        # Major clans
        clans = [
            'Banu_Arbas', 'Banu_Atij', 'Banu_Hulyan', 'Banu_Sarran',  # Aserai
            'deTihr', 'deMeroc', 'deMolarn', 'Joulains',  # Vlandia
            'Coros', 'Fenada', 'Sitra', 'Wilunding',  # Battania
            'Kuloving', 'Vagiring', 'Isyanak', 'Ormidlung',  # Sturgia
            'Khergit', 'Karakhergit', 'Yanseris', 'Arkits',  # Khuzait
            'Impestores', 'Comnos', 'Dionicos', 'Maneolis'  # Empire
        ]
        pages.extend(clans)
        
        return pages
    
    def sanitize_filename(self, page_name):
        """Convert page name to safe filename"""
        filename = page_name.replace('_', ' ')
        filename = re.sub(r'[<>:"/\\|?*]', '_', filename)
        return filename + '.html'
    
    def download_page(self, page_name):
        """Download a single page using Selenium"""
        url = f"{self.wiki_url}/wiki/{page_name}"
        filename = self.sanitize_filename(page_name)
        filepath = self.output_dir / filename
        
        # Skip if already exists (but check file size to ensure it's valid)
        if filepath.exists():
            file_size = filepath.stat().st_size
            if file_size > 10000:  # File exists and has reasonable size (>10KB)
                print(f"[SKIP] {page_name:<40} (already exists, {file_size} bytes)")
                return True
            else:
                # File exists but too small - probably corrupted, re-download
                print(f"[REDO] {page_name:<40} (file too small, re-downloading)")
                filepath.unlink()
        
        try:
            print(f"[GET]  {page_name:<40} ", end='', flush=True)
            
            # Navigate to page
            self.driver.get(url)
            
            # Wait for page to load (wait for main content)
            try:
                WebDriverWait(self.driver, 10).until(
                    EC.presence_of_element_located((By.TAG_NAME, "body"))
                )
            except:
                pass  # Continue anyway
            
            # Small delay to let JavaScript execute
            time.sleep(2)
            
            # Get page source (full HTML after JavaScript execution)
            html_content = self.driver.page_source
            
            # Check if it's a real page (not Cloudflare challenge)
            if 'Client Challenge' in html_content or 'Just a moment' in html_content:
                print(f"-> BLOCKED")
                return False
            
            # Save HTML
            with open(filepath, 'w', encoding='utf-8') as f:
                f.write(html_content)
            
            print(f"-> OK ({len(html_content)} bytes)")
            return True
                
        except Exception as e:
            print(f"-> ERROR: {str(e)[:50]}")
            return False
    
    def download_all(self):
        """Download all pages"""
        print(f"\nDownloading {len(self.pages_to_download)} pages using Selenium...")
        print(f"Output: {self.output_dir}\n")
        
        success = 0
        failed = 0
        skipped = 0
        
        try:
            for i, page_name in enumerate(self.pages_to_download, 1):
                print(f"[{i}/{len(self.pages_to_download)}] ", end='')
                
                filepath = self.output_dir / self.sanitize_filename(page_name)
                if filepath.exists():
                    skipped += 1
                    print(f"[SKIP] {page_name:<40} (already exists)")
                    continue
                
                if self.download_page(page_name):
                    success += 1
                else:
                    failed += 1
                
                # Be polite - wait between requests
                time.sleep(3)  # 3 seconds delay
                
        finally:
            # Always close browser
            self.driver.quit()
        
        print("\n" + "=" * 60)
        print(f"Downloaded: {success}")
        print(f"Failed:     {failed}")
        print(f"Skipped:    {skipped}")
        print(f"Total:      {len(self.pages_to_download)}")
        print("=" * 60)


if __name__ == '__main__':
    print("=" * 60)
    print("SELENIUM WIKI DOWNLOADER")
    print("=" * 60)
    print("\nThis script uses real Chrome browser to bypass Cloudflare.")
    print("Make sure Chrome is installed on your system.\n")
    
    # English
    print("[1/3] English Wiki...")
    downloader_en = SeleniumWikiDownloader(
        wiki_url='https://mountandblade.fandom.com',
        output_dir='../Database/raw/en',
        language='en'
    )
    downloader_en.download_all()
    
    # Russian
    print("\n[2/3] Russian Wiki...")
    downloader_ru = SeleniumWikiDownloader(
        wiki_url='https://mountandblade.fandom.com/ru',
        output_dir='../Database/raw/ru',
        language='ru'
    )
    downloader_ru.download_all()
    
    # Turkish
    print("\n[3/3] Turkish Wiki...")
    downloader_tr = SeleniumWikiDownloader(
        wiki_url='https://mountandblade.fandom.com/tr',
        output_dir='../Database/raw/tr',
        language='tr'
    )
    downloader_tr.download_all()
    
    print("\n" + "=" * 60)
    print("ALL DONE!")
    print("=" * 60)
    print("\nNext step: Run sorting script")
    print("  py sort_wiki_pages.py ../Database/raw/en")

