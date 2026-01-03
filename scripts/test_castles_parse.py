#!/usr/bin/env python3
"""Test parsing castles page"""
from selenium import webdriver
from selenium.webdriver.chrome.service import Service
from selenium.webdriver.chrome.options import Options
from webdriver_manager.chrome import ChromeDriverManager
from bs4 import BeautifulSoup
import time
import re

opts = Options()
opts.add_argument('--headless')
driver = webdriver.Chrome(service=Service(ChromeDriverManager().install()), options=opts)

driver.get('https://mountandblade.fandom.com/wiki/List_of_castles')
time.sleep(8)  # Wait longer for dynamic content

# Check if content loaded
html = driver.page_source
print(f"HTML length: {len(html)}")
print(f"Contains 'Aserai': {'Aserai' in html}")
print(f"Contains 'Ain_Baliq_Castle': {'Ain_Baliq_Castle' in html}")
print(f"Contains 'Bannerlord': {'Bannerlord[]' in html}")

html = driver.page_source
soup = BeautifulSoup(html, 'html.parser')

# Find Bannerlord section
bannerlord_section = None
for h2 in soup.find_all('h2'):
    text = h2.get_text().strip()
    if text == 'Bannerlord[]' or (text.startswith('Bannerlord') and '[]' in text):
        bannerlord_section = h2
        print(f"Found Bannerlord section: {text}")
        break

if bannerlord_section:
    # Get content after Bannerlord
    current = bannerlord_section.find_next_sibling()
    print("\n" + "=" * 60)
    print("Structure after Bannerlord:")
    print("=" * 60)
    
    count = 0
    while current and count < 30:
        if current.name == 'h2':
            print(f"\n[STOP] Next section: {current.get_text().strip()}")
            break
        
        tag_info = f"[{current.name}]"
        if current.name == 'h3':
            print(f"\n{tag_info} {current.get_text().strip()}")
        elif current.name == 'ul':
            links = current.find_all('a', href=True)
            print(f"{tag_info} {len(links)} links")
            for link in links[:3]:  # First 3
                href = link.get('href', '')
                text = link.get_text().strip()
                print(f"    - {text} -> {href}")
        elif current.name == 'p':
            bold = current.find('b') or current.find('strong')
            if bold:
                print(f"{tag_info} BOLD: {bold.get_text().strip()}")
        elif current.name == 'table':
            print(f"{tag_info} TABLE found")
            links = current.find_all('a', href=True)
            print(f"  {len(links)} links in table")
            for link in links[:5]:
                href = link.get('href', '')
                text = link.get_text().strip()
                print(f"    - {text} -> {href}")
        
        current = current.find_next_sibling()
        count += 1

driver.quit()

