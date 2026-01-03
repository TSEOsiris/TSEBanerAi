"""
Extract data from Mount & Blade II Bannerlord Digital Companion
Extracts localization data, catalog information, and other accessible data
"""

import json
import os
import sqlite3
from pathlib import Path
from typing import Dict, List, Any, Optional
import re

class DigitalCompanionExtractor:
    def __init__(self, companion_path: str, output_dir: str = "Database/Digital Companion"):
        """
        companion_path: Path to DigitalCompanion folder
        output_dir: Directory to save extracted data
        """
        self.companion_path = Path(companion_path)
        self.output_dir = Path(output_dir)
        self.data_path = self.companion_path / "Mount & Blade II Bannerlord - Digital Companion_Data"
        self.streaming_assets = self.data_path / "StreamingAssets" / "aa"
        
        # Create output directory
        self.output_dir.mkdir(parents=True, exist_ok=True)
        
        # Storage for extracted data
        self.data = {
            'catalog': {},
            'localization': {},
            'settings': {},
            'metadata': {}
        }
    
    def extract_catalog(self):
        """Extract catalog.json information"""
        print("Extracting catalog data...")
        catalog_file = self.streaming_assets / "catalog.json"
        
        if not catalog_file.exists():
            print(f"Warning: {catalog_file} not found")
            return
        
        try:
            # Try to load as JSON (might be too large)
            with open(catalog_file, 'r', encoding='utf-8') as f:
                # Read in chunks to handle large files
                content = f.read()
                
            # Try to parse JSON
            try:
                catalog_data = json.loads(content)
                self.data['catalog'] = catalog_data
                
                # Extract key information
                if 'm_InternalIds' in catalog_data:
                    print(f"Found {len(catalog_data['m_InternalIds'])} internal IDs")
                
                # Save full catalog
                output_file = self.output_dir / "catalog.json"
                with open(output_file, 'w', encoding='utf-8') as f:
                    json.dump(catalog_data, f, ensure_ascii=False, indent=2)
                print(f"Saved catalog to {output_file}")
                
            except json.JSONDecodeError as e:
                print(f"Warning: Could not parse catalog.json as JSON: {e}")
                # Save as text for manual inspection
                output_file = self.output_dir / "catalog.txt"
                with open(output_file, 'w', encoding='utf-8') as f:
                    f.write(content[:100000])  # First 100k chars
                print(f"Saved catalog preview to {output_file}")
                
        except Exception as e:
            print(f"Error extracting catalog: {e}")
    
    def extract_settings(self):
        """Extract settings.json"""
        print("Extracting settings...")
        settings_file = self.streaming_assets / "settings.json"
        
        if not settings_file.exists():
            print(f"Warning: {settings_file} not found")
            return
        
        try:
            with open(settings_file, 'r', encoding='utf-8') as f:
                settings_data = json.load(f)
            
            self.data['settings'] = settings_data
            
            output_file = self.output_dir / "settings.json"
            with open(output_file, 'w', encoding='utf-8') as f:
                json.dump(settings_data, f, ensure_ascii=False, indent=2)
            print(f"Saved settings to {output_file}")
            
        except Exception as e:
            print(f"Error extracting settings: {e}")
    
    def extract_metadata(self):
        """Extract metadata from JSON files"""
        print("Extracting metadata...")
        
        metadata_files = {
            'scripting_assemblies': self.data_path / "ScriptingAssemblies.json",
            'runtime_initialize': self.data_path / "RuntimeInitializeOnLoads.json"
        }
        
        for key, file_path in metadata_files.items():
            if file_path.exists():
                try:
                    with open(file_path, 'r', encoding='utf-8') as f:
                        data = json.load(f)
                    self.data['metadata'][key] = data
                    
                    output_file = self.output_dir / f"{key}.json"
                    with open(output_file, 'w', encoding='utf-8') as f:
                        json.dump(data, f, ensure_ascii=False, indent=2)
                    print(f"Saved {key} to {output_file}")
                except Exception as e:
                    print(f"Error extracting {key}: {e}")
    
    def analyze_bundles(self):
        """Analyze Unity Asset Bundle files and extract information"""
        print("Analyzing Unity Asset Bundles...")
        
        bundles_dir = self.streaming_assets / "StandaloneWindows64"
        if not bundles_dir.exists():
            print(f"Warning: {bundles_dir} not found")
            return
        
        bundle_info = {
            'localization_bundles': [],
            'asset_bundles': [],
            'total_size': 0
        }
        
        # Find localization bundles
        for bundle_file in bundles_dir.glob("*.bundle"):
            size = bundle_file.stat().st_size
            bundle_info['total_size'] += size
            
            if 'localization' in bundle_file.name.lower():
                bundle_info['localization_bundles'].append({
                    'name': bundle_file.name,
                    'size': size,
                    'size_mb': round(size / (1024 * 1024), 2)
                })
            else:
                bundle_info['asset_bundles'].append({
                    'name': bundle_file.name,
                    'size': size,
                    'size_mb': round(size / (1024 * 1024), 2)
                })
        
        bundle_info['total_size_mb'] = round(bundle_info['total_size'] / (1024 * 1024), 2)
        bundle_info['total_bundles'] = len(bundle_info['localization_bundles']) + len(bundle_info['asset_bundles'])
        
        # Save bundle information
        output_file = self.output_dir / "bundle_info.json"
        with open(output_file, 'w', encoding='utf-8') as f:
            json.dump(bundle_info, f, ensure_ascii=False, indent=2)
        
        print(f"Found {bundle_info['total_bundles']} bundles ({bundle_info['total_size_mb']} MB total)")
        print(f"  - {len(bundle_info['localization_bundles'])} localization bundles")
        print(f"  - {len(bundle_info['asset_bundles'])} asset bundles")
        print(f"Saved bundle info to {output_file}")
        
        self.data['bundle_info'] = bundle_info
    
    def try_extract_localization_from_bundles(self):
        """Try to extract text data from localization bundles"""
        print("Attempting to extract localization from bundles...")
        
        bundles_dir = self.streaming_assets / "StandaloneWindows64"
        if not bundles_dir.exists():
            return
        
        localization_data = {}
        
        # Find localization bundles
        loc_bundles = [
            'localization-string-tables-english(en)_assets_all.bundle',
            'localization-string-tables-russian(ru)_assets_all.bundle',
            'localization-string-tables-turkish(tr)_assets_all.bundle'
        ]
        
        for bundle_name in loc_bundles:
            bundle_file = bundles_dir / bundle_name
            if not bundle_file.exists():
                continue
            
            print(f"  Processing {bundle_name}...")
            
            # Try to extract text strings from bundle
            # Unity bundles are binary, but may contain readable strings
            try:
                with open(bundle_file, 'rb') as f:
                    content = f.read()
                
                # Try to find JSON-like structures or text strings
                # Look for common patterns
                text_content = content.decode('utf-8', errors='ignore')
                
                # Try to find JSON structures
                json_patterns = [
                    r'\{[^{}]*"text"[^{}]*\}',
                    r'\{[^{}]*"value"[^{}]*\}',
                    r'\{[^{}]*"name"[^{}]*\}'
                ]
                
                found_strings = []
                for pattern in json_patterns:
                    matches = re.findall(pattern, text_content, re.DOTALL)
                    found_strings.extend(matches[:100])  # Limit to first 100
                
                if found_strings:
                    lang_code = bundle_name.split('(')[1].split(')')[0] if '(' in bundle_name else 'unknown'
                    localization_data[lang_code] = {
                        'bundle_name': bundle_name,
                        'size': bundle_file.stat().st_size,
                        'extracted_strings_count': len(found_strings),
                        'sample_strings': found_strings[:50]  # First 50 as samples
                    }
                    print(f"    Found {len(found_strings)} potential strings")
                
            except Exception as e:
                print(f"    Warning: Could not process {bundle_name}: {e}")
        
        if localization_data:
            output_file = self.output_dir / "localization_extracted.json"
            with open(output_file, 'w', encoding='utf-8') as f:
                json.dump(localization_data, f, ensure_ascii=False, indent=2)
            print(f"Saved extracted localization to {output_file}")
            self.data['localization'] = localization_data
    
    def extract_catalog_summary(self):
        """Extract summary information from catalog.json"""
        print("Extracting catalog summary...")
        
        catalog_file = self.streaming_assets / "catalog.json"
        if not catalog_file.exists():
            return
        
        try:
            with open(catalog_file, 'r', encoding='utf-8') as f:
                content = f.read()
            
            # Try to extract key information using regex
            summary = {
                'file_size': catalog_file.stat().st_size,
                'file_size_mb': round(catalog_file.stat().st_size / (1024 * 1024), 2),
                'estimated_entries': 0,
                'contains_localization': 'localization' in content.lower(),
                'contains_assets': 'asset' in content.lower() or 'bundle' in content.lower(),
                'sample_keys': []
            }
            
            # Try to find keys/IDs in catalog
            key_patterns = [
                r'"m_InternalId"\s*:\s*"([^"]+)"',
                r'"m_Keys"\s*:\s*\[([^\]]+)\]',
                r'"([a-f0-9]{32})\.bundle"'
            ]
            
            all_keys = []
            for pattern in key_patterns:
                matches = re.findall(pattern, content)
                all_keys.extend(matches[:100])  # Limit
            
            summary['estimated_entries'] = len(set(all_keys))
            summary['sample_keys'] = list(set(all_keys))[:20]
            
            output_file = self.output_dir / "catalog_summary.json"
            with open(output_file, 'w', encoding='utf-8') as f:
                json.dump(summary, f, ensure_ascii=False, indent=2)
            print(f"Saved catalog summary to {output_file}")
            self.data['catalog_summary'] = summary
            
        except Exception as e:
            print(f"Error extracting catalog summary: {e}")
    
    def save_to_sqlite(self):
        """Save extracted data to SQLite database"""
        print("Saving to SQLite database...")
        
        db_file = self.output_dir / "digital_companion.db"
        conn = sqlite3.connect(db_file)
        cursor = conn.cursor()
        
        # Create tables
        cursor.execute("""
            CREATE TABLE IF NOT EXISTS bundle_info (
                name TEXT PRIMARY KEY,
                type TEXT,
                size INTEGER,
                size_mb REAL
            )
        """)
        
        cursor.execute("""
            CREATE TABLE IF NOT EXISTS localization_data (
                language TEXT,
                bundle_name TEXT,
                extracted_count INTEGER,
                PRIMARY KEY (language, bundle_name)
            )
        """)
        
        # Insert bundle info
        if 'bundle_info' in self.data:
            for bundle in self.data['bundle_info'].get('localization_bundles', []):
                cursor.execute("""
                    INSERT OR REPLACE INTO bundle_info VALUES (?, ?, ?, ?)
                """, (bundle['name'], 'localization', bundle['size'], bundle['size_mb']))
            
            for bundle in self.data['bundle_info'].get('asset_bundles', []):
                cursor.execute("""
                    INSERT OR REPLACE INTO bundle_info VALUES (?, ?, ?, ?)
                """, (bundle['name'], 'asset', bundle['size'], bundle['size_mb']))
        
        # Insert localization data
        if 'localization' in self.data:
            for lang, data in self.data['localization'].items():
                cursor.execute("""
                    INSERT OR REPLACE INTO localization_data VALUES (?, ?, ?)
                """, (lang, data['bundle_name'], data['extracted_strings_count']))
        
        conn.commit()
        conn.close()
        print(f"Saved to SQLite database: {db_file}")
    
    def extract_all(self):
        """Extract all available data from Digital Companion"""
        print("Starting Digital Companion data extraction...")
        print(f"Companion path: {self.companion_path}")
        print(f"Output directory: {self.output_dir}")
        
        if not self.data_path.exists():
            print(f"Error: Digital Companion data path not found: {self.data_path}")
            return
        
        # Extract available data
        self.extract_settings()
        self.extract_metadata()
        self.analyze_bundles()
        self.extract_catalog_summary()
        self.try_extract_localization_from_bundles()
        
        # Try to extract full catalog (might be too large)
        try:
            self.extract_catalog()
        except Exception as e:
            print(f"Warning: Could not extract full catalog: {e}")
        
        # Save results
        self.save_to_sqlite()
        
        # Save summary
        summary = {
            'extracted_data_types': list(self.data.keys()),
            'total_bundles': self.data.get('bundle_info', {}).get('total_bundles', 0),
            'localization_languages': list(self.data.get('localization', {}).keys())
        }
        
        summary_file = self.output_dir / "extraction_summary.json"
        with open(summary_file, 'w', encoding='utf-8') as f:
            json.dump(summary, f, ensure_ascii=False, indent=2)
        
        print("\nExtraction complete!")
        print(f"Summary saved to {summary_file}")


def main():
    import sys
    
    # Default paths
    default_companion_path = r"C:\TSEBanerAi\Mount & Blade II Bannerlord\DigitalCompanion"
    default_output = r"C:\TSEBanerAi\TSEBanerAi\Database\Digital Companion"
    
    # Get paths from command line or use defaults
    companion_path = sys.argv[1] if len(sys.argv) > 1 else default_companion_path
    output_dir = sys.argv[2] if len(sys.argv) > 2 else default_output
    
    if not os.path.exists(companion_path):
        print(f"Error: Digital Companion path not found: {companion_path}")
        print("Usage: python extract_digital_companion_data.py [companion_path] [output_dir]")
        return
    
    extractor = DigitalCompanionExtractor(companion_path, output_dir)
    extractor.extract_all()


if __name__ == "__main__":
    main()








