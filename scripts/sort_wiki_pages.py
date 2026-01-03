import os
import re
import json
import shutil
from pathlib import Path

class WikiPageSorter:
    def __init__(self, raw_dir):
        """
        raw_dir: Directory with unsorted HTML files (e.g., 'Database/raw/en')
        """
        self.raw_dir = Path(raw_dir)
        self.sorted_dir = self.raw_dir / 'sorted'
        
        # Category mappings (order matters - more specific first!)
        self.category_map = {
            # Settlements first (most specific)
            'Settlements/Towns': [
                'Bannerlord Towns',
                'Towns'
            ],
            'Settlements/Castles': [
                'Bannerlord Castles',
                'Castles'
            ],
            'Settlements/Villages': [
                'Bannerlord Villages',
                'Villages'
            ],
            # Then characters and specific entities
            'Persons': [
                'Bannerlord Characters',
                'Leaders of Bannerlord',
                'Characters'
            ],
            'Clans': [
                'Clans of Bannerlord',
                'Clans'
            ],
            'Mechanics': [
                'Game mechanics',
                'Gameplay'
            ],
            'History': [
                'History',
                'Historical events',
                'Battles'
            ],
            # Factions last (least specific, includes kingdom names)
            'Factions': [
                'Kingdoms of Bannerlord',
                'Vlandia', 'Sturgia', 'Battania', 
                'Northern Empire', 'Southern Empire', 'Western Empire',
                'Aserai', 'Khuzait'
            ]
        }
        
        # Statistics
        self.stats = {
            'total': 0,
            'sorted': 0,
            'unsorted': 0,
            'skipped': 0,
            'by_category': {}
        }
        
        # Create sorted directories
        self._create_directories()
    
    def _create_directories(self):
        """Create sorted directory structure"""
        categories = [
            'Factions',
            'Clans',
            'Persons',
            'Settlements/Towns',
            'Settlements/Castles',
            'Settlements/Villages',
            'Mechanics',
            'History',
            'Other',
            'Unsorted'
        ]
        
        for cat in categories:
            (self.sorted_dir / cat).mkdir(parents=True, exist_ok=True)
            self.stats['by_category'][cat] = 0
    
    def extract_categories(self, html_content):
        """Extract wgCategories from HTML"""
        # Find wgCategories in JavaScript
        match = re.search(r'"wgCategories":\s*\[(.*?)\]', html_content)
        if not match:
            return []
        
        # Extract category list
        categories_str = match.group(1)
        categories = re.findall(r'"([^"]+)"', categories_str)
        return categories
    
    def extract_page_title(self, html_content):
        """Extract page title from HTML"""
        # Try wgTitle first
        match = re.search(r'"wgTitle":"([^"]+)"', html_content)
        if match:
            return match.group(1).replace('\\u0026', '&')
        
        # Fallback to <title> tag
        match = re.search(r'<title>([^<]+)</title>', html_content)
        if match:
            title = match.group(1)
            # Remove " | Mount & Blade Wiki | Fandom"
            title = re.sub(r'\s*\|\s*Mount.*', '', title)
            return title.replace('&amp;', '&')
        
        return None
    
    def detect_category(self, categories, page_title):
        """Detect which category this page belongs to"""
        
        # Check against known category patterns
        for target_cat, patterns in self.category_map.items():
            for pattern in patterns:
                for cat in categories:
                    if pattern.lower() in cat.lower():
                        return target_cat
        
        # If no categories (ru/tr wikis), use title-based detection
        if not categories and page_title:
            title_lower = page_title.lower()
            
            # Known faction names
            factions = ['vlandia', 'sturgia', 'battania', 'northern empire', 'southern empire', 
                       'western empire', 'aserai', 'khuzait', 'empire']
            if any(faction in title_lower for faction in factions):
                return 'Factions'
            
            # Known leader names
            leaders = ['derthert', 'raganvad', 'caladog', 'lucon', 'rhagaea', 'garios', 
                      'monchug', 'unqid']
            if any(leader in title_lower for leader in leaders):
                return 'Persons'
            
            # Castle detection
            if 'castle' in title_lower:
                return 'Settlements/Castles'
            
            # Known town names
            towns = ['pravend', 'jaculan', 'galend', 'sargot', 'marunath', 'charas',
                    'revyl', 'varcheg', 'tyal', 'ustokol', 'pen cannoc', 'dunglanys',
                    'seonon', 'car banseth', 'epicrotea', 'onira', 'lycaron', 'myzea',
                    'amitatys', 'rhotae', 'zeonica', 'quyaz', 'makeb', 'razih', 'hubyar',
                    'iyakis', 'akkalat', 'chaikand', 'baltakhand', 'odrysa']
            if any(town in title_lower for town in towns):
                return 'Settlements/Towns'
            
            # Clan detection
            clan_prefixes = ['banu ', 'de', 'fen ', 'kuloving', 'vagiring', 'isyanak',
                           'ormidlung', 'khergit', 'karakhergit', 'yanseris', 'arkits',
                           'impestores', 'comnos', 'dionicos', 'maneolis', 'coros',
                           'fenada', 'sitra', 'wilunding', 'joulains']
            if any(title_lower.startswith(prefix) or prefix in title_lower for prefix in clan_prefixes):
                return 'Clans'
            
            # Game mechanics
            mechanics = ['marriage', 'vassalage', 'trading', 'companions', 'workshops',
                        'caravans', 'skills', 'perks', 'clans', 'armies']
            if any(mech in title_lower for mech in mechanics):
                return 'Mechanics'
        
        # Special cases based on title (fallback)
        if page_title:
            title_lower = page_title.lower()
            
            # Castle detection (has "Castle" in title)
            if 'castle' in title_lower:
                return 'Settlements/Castles'
            
            # Clan detection (starts with "Banu", "Clan", or common clan prefixes)
            clan_prefixes = ['banu ', 'clan ', 'house of']
            if any(title_lower.startswith(prefix) for prefix in clan_prefixes):
                return 'Clans'
        
        # If still no match, check for partial matches
        if categories:
            # Check if it's a settlement-related page
            for cat in categories:
                cat_lower = cat.lower()
                if 'settlement' in cat_lower or 'location' in cat_lower:
                    return 'Other'
                if 'character' in cat_lower:
                    return 'Persons'
                if 'kingdom' in cat_lower or 'faction' in cat_lower:
                    return 'Factions'
        
        return None
    
    def sort_file(self, html_file):
        """Sort a single HTML file"""
        try:
            # Read HTML content (try utf-8 first, fallback to latin-1, then with errors='ignore')
            try:
                with open(html_file, 'r', encoding='utf-8', errors='ignore') as f:
                    content = f.read()
            except Exception:
                # Fallback for files with special characters
                try:
                    with open(html_file, 'r', encoding='latin-1', errors='ignore') as f:
                        content = f.read()
                except Exception:
                    # Last resort - read as binary and decode
                    with open(html_file, 'rb') as f:
                        content = f.read().decode('utf-8', errors='ignore')
            
            # Extract categories and title
            categories = self.extract_categories(content)
            page_title = self.extract_page_title(content)
            
            # Detect target category
            target_cat = self.detect_category(categories, page_title)
            
            if target_cat:
                # Copy to sorted directory
                dest_dir = self.sorted_dir / target_cat
                dest_file = dest_dir / html_file.name
                
                shutil.copy2(html_file, dest_file)
                
                self.stats['sorted'] += 1
                self.stats['by_category'][target_cat] += 1
                
                print(f"[OK] {html_file.name:<50} -> {target_cat}")
                return True
            else:
                # Move to Unsorted
                dest_dir = self.sorted_dir / 'Unsorted'
                dest_file = dest_dir / html_file.name
                
                shutil.copy2(html_file, dest_file)
                
                self.stats['unsorted'] += 1
                self.stats['by_category']['Unsorted'] += 1
                
                print(f"[??] {html_file.name:<50} -> Unsorted (cats: {', '.join(categories[:3])})")
                return False
                
        except Exception as e:
            # Try to get error message without special characters
            error_msg = str(e).encode('ascii', errors='ignore').decode('ascii')
            print(f"[ERR] Error processing {html_file.name}: {error_msg[:50]}")
            self.stats['skipped'] += 1
            return False
    
    def sort_all(self):
        """Sort all HTML files in raw directory"""
        # Find all HTML files
        html_files = list(self.raw_dir.glob('*.html'))
        
        if not html_files:
            print(f"WARNING: No HTML files found in {self.raw_dir}")
            return
        
        print(f"Found {len(html_files)} HTML files\n")
        print(f"Starting sorting...\n")
        
        self.stats['total'] = len(html_files)
        
        for html_file in html_files:
            # Skip files that start with underscore (logs, etc.)
            if html_file.name.startswith('_'):
                self.stats['skipped'] += 1
                continue
            
            self.sort_file(html_file)
        
        # Print statistics
        self._print_stats()
    
    def _print_stats(self):
        """Print sorting statistics"""
        print("\n" + "=" * 60)
        print("SORTING STATISTICS")
        print("=" * 60)
        print(f"Total files:     {self.stats['total']}")
        print(f"Sorted:          {self.stats['sorted']}")
        print(f"Unsorted:        {self.stats['unsorted']}")
        print(f"Skipped:         {self.stats['skipped']}")
        print("\nBy Category:")
        for cat, count in sorted(self.stats['by_category'].items()):
            if count > 0:
                print(f"  {cat:<30} {count:>5} files")
        print("=" * 60)
        print(f"\nSorted files saved to: {self.sorted_dir}")


# ==================== USAGE ====================

if __name__ == '__main__':
    import sys
    
    if len(sys.argv) > 1:
        raw_dir = sys.argv[1]
    else:
        raw_dir = 'Database/raw/en'
    
    print("=" * 60)
    print("WIKI PAGE SORTER")
    print("=" * 60)
    print(f"Source: {raw_dir}")
    print(f"Target: {raw_dir}/sorted/\n")
    
    sorter = WikiPageSorter(raw_dir)
    sorter.sort_all()
    
    print("\nDone! Check the 'sorted' folder.")
    print("Pages that couldn't be categorized are in 'Unsorted' folder.")

