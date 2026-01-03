import os
import shutil
from pathlib import Path

def collect_existing_pages(source_dir, target_dir):
    """
    Collect all HTML pages from existing Wiki_pages structure
    and copy them to raw directory for sorting
    """
    source = Path(source_dir)
    target = Path(target_dir)
    
    # Create target directory
    target.mkdir(parents=True, exist_ok=True)
    
    # Find all HTML files recursively
    html_files = list(source.rglob('*.html'))
    htm_files = list(source.rglob('*.htm'))
    
    all_files = html_files + htm_files
    
    print(f"Found {len(all_files)} HTML/HTM files in {source}")
    print(f"Copying to {target}...\n")
    
    copied = 0
    skipped = 0
    
    for html_file in all_files:
        try:
            # Skip files in _files directories (assets)
            if '_files' in str(html_file):
                skipped += 1
                continue
            
            # Copy to target directory
            dest_file = target / html_file.name
            
            # If file already exists, add number suffix
            if dest_file.exists():
                base_name = html_file.stem
                extension = html_file.suffix
                counter = 1
                while dest_file.exists():
                    dest_file = target / f"{base_name}_{counter}{extension}"
                    counter += 1
            
            shutil.copy2(html_file, dest_file)
            copied += 1
            
            if copied % 50 == 0:
                print(f"  Copied {copied} files...")
                
        except Exception as e:
            print(f"[ERR] Error copying {html_file.name}: {e}")
            skipped += 1
    
    print(f"\n{'='*60}")
    print(f"COLLECTION COMPLETE")
    print(f"{'='*60}")
    print(f"Total found:  {len(all_files)}")
    print(f"Copied:       {copied}")
    print(f"Skipped:      {skipped}")
    print(f"{'='*60}")
    print(f"\nFiles copied to: {target}")
    print(f"Next step: Run sort_wiki_pages.py on this directory")


if __name__ == '__main__':
    import sys
    
    if len(sys.argv) > 2:
        source_dir = sys.argv[1]
        target_dir = sys.argv[2]
    else:
        source_dir = 'Database/Wiki_pages/mountandblade.fandom.com'
        target_dir = 'Database/raw/en'
    
    print("="*60)
    print("COLLECT EXISTING WIKI PAGES")
    print("="*60)
    print(f"Source: {source_dir}")
    print(f"Target: {target_dir}\n")
    
    collect_existing_pages(source_dir, target_dir)

