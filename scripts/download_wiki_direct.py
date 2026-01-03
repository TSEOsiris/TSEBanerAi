import time
import os
from pathlib import Path
import re

try:
    import cloudscraper
except ImportError:
    print("ERROR: cloudscraper not installed!")
    print("Please run: pip install cloudscraper")
    exit(1)

class DirectWikiDownloader:
    def __init__(self, wiki_url, output_dir, language='en'):
        """
        Download specific wiki pages by direct URLs
        """
        self.wiki_url = wiki_url.rstrip('/')
        self.output_dir = Path(output_dir)
        self.language = language
        
        # Use cloudscraper to bypass Cloudflare protection
        self.scraper = cloudscraper.create_scraper(
            browser={
                'browser': 'chrome',
                'platform': 'windows',
                'mobile': False
            }
        )
        
        os.makedirs(output_dir, exist_ok=True)
        
        # Page lists to download
        self.pages_to_download = self.get_page_list()
    
    def get_page_list(self):
        """Get list of important Bannerlord pages"""
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
            'Marunath', 'Pen_Cannoc', 'Dunglanys', 'Seonon', 'Car_Banseth',  # Battania
            'Epicrotea', 'Onira', 'Lycaron', 'Myzea', 'Amitatys', 'Rhotae', 'Zeonica',  # Empire
            'Quyaz', 'Makeb', 'Razih', 'Hubyar', 'Iyakis',  # Aserai
            'Akkalat', 'Chaikand', 'Baltakhand', 'Myzea', 'Odrysa'  # Khuzait
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
        
        # Major clans (examples)
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
        """Download a single page"""
        url = f"{self.wiki_url}/wiki/{page_name}"
        filename = self.sanitize_filename(page_name)
        filepath = self.output_dir / filename
        
        # Skip if already exists
        if filepath.exists():
            print(f"[SKIP] {page_name:<40} (already exists)")
            return True
        
        try:
            print(f"[GET]  {page_name:<40} ", end='', flush=True)
            response = self.scraper.get(url, timeout=30)
            
            if response.status_code == 200:
                # Check if it's a real page (not Cloudflare challenge)
                if 'Client Challenge' in response.text or 'Just a moment' in response.text:
                    print(f"-> BLOCKED (Cloudflare)")
                    return False
                
                # Save HTML
                with open(filepath, 'w', encoding='utf-8') as f:
                    f.write(response.text)
                print(f"-> OK ({len(response.text)} bytes)")
                return True
            elif response.status_code == 404:
                print(f"-> NOT FOUND")
                return False
            else:
                print(f"-> ERROR {response.status_code}")
                return False
                
        except Exception as e:
            print(f"-> ERROR: {e}")
            return False
    
    def download_all(self):
        """Download all pages"""
        print(f"\nDownloading {len(self.pages_to_download)} pages...")
        print(f"Output: {self.output_dir}\n")
        
        success = 0
        failed = 0
        skipped = 0
        
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
            time.sleep(2)  # 2 seconds delay
        
        print("\n" + "=" * 60)
        print(f"Downloaded: {success}")
        print(f"Failed:     {failed}")
        print(f"Skipped:    {skipped}")
        print(f"Total:      {len(self.pages_to_download)}")
        print("=" * 60)


if __name__ == '__main__':
    print("=" * 60)
    print("DIRECT WIKI DOWNLOADER")
    print("=" * 60)
    
    # English
    print("\n[1/3] English Wiki...")
    downloader_en = DirectWikiDownloader(
        wiki_url='https://mountandblade.fandom.com',
        output_dir='../Database/raw/en',
        language='en'
    )
    downloader_en.download_all()
    
    # Russian
    print("\n[2/3] Russian Wiki...")
    downloader_ru = DirectWikiDownloader(
        wiki_url='https://mountandblade.fandom.com/ru',
        output_dir='../Database/raw/ru',
        language='ru'
    )
    downloader_ru.download_all()
    
    # Turkish
    print("\n[3/3] Turkish Wiki...")
    downloader_tr = DirectWikiDownloader(
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

