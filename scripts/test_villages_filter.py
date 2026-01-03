#!/usr/bin/env python3
"""Test that we only get Bannerlord villages"""
import sys
from pathlib import Path
import time

# Add parent directory to path
script_dir = Path(__file__).parent
sys.path.insert(0, str(script_dir.parent))

from scripts.download_villages_only import VillagesDownloader

project_root = script_dir.parent
output_dir = project_root / 'Database' / 'Wiki_pages' / 'mountandblade.fandom.com'

d = VillagesDownloader('https://mountandblade.fandom.com', str(output_dir))
d.driver.get('https://mountandblade.fandom.com/wiki/List_of_villages')
time.sleep(3)

villages = d.parse_villages_collapsible(d.driver.page_source)

print('\n' + '=' * 60)
print('SUMMARY - Only Bannerlord villages should be found')
print('=' * 60)
print(f'Total factions: {len(villages)}')
print(f'Total villages: {sum(len(v) for v in villages.values())}')
print('\nFactions found:')
for f, v in villages.items():
    print(f'  {f}: {len(v)} villages')
    # Show first 3 villages as sample
    print(f'    Sample: {v[:3]}')

# Check that we don't have villages from other games
expected_factions = ['Aserai Sultanate', 'High Kingdom of Battania', 'Khuzait Khanate', 
                     'Principality of Sturgia', 'Kingdom of Vlandia', 'Northern Empire', 
                     'Southern Empire', 'Western Empire']

print('\n' + '=' * 60)
print('VALIDATION:')
print('=' * 60)
if len(villages) == 8:
    print('[OK] Found exactly 8 factions (correct for Bannerlord)')
else:
    print(f'[ERROR] Found {len(villages)} factions (expected 8)')

all_expected = all(f in villages for f in expected_factions)
if all_expected:
    print('[OK] All expected Bannerlord factions found')
else:
    print('[ERROR] Missing some expected factions')
    for f in expected_factions:
        if f not in villages:
            print(f'  Missing: {f}')

d.driver.quit()

