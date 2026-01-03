#!/usr/bin/env python3
"""Test parsing villages page"""
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

driver.get('https://mountandblade.fandom.com/wiki/List_of_villages')
time.sleep(5)

html = driver.page_source
soup = BeautifulSoup(html, 'html.parser')

# Try different selectors
print("=" * 60)
print("Testing different selectors:")
print("=" * 60)

# 1. mw-collapsible
collapsibles = soup.find_all('div', class_='mw-collapsible')
print(f"1. mw-collapsible: {len(collapsibles)}")

# 2. mw-made-collapsible
made_collapsible = soup.find_all('div', class_='mw-made-collapsible')
print(f"2. mw-made-collapsible: {len(made_collapsible)}")

# 3. Both classes
both = soup.find_all('div', class_=lambda x: x and 'mw-collapsible' in x and 'mw-made-collapsible' in x)
print(f"3. Both classes: {len(both)}")

# 4. Any class containing collapsible
any_collapsible = soup.find_all('div', class_=lambda x: x and 'collapsible' in ' '.join(x) if isinstance(x, list) else 'collapsible' in str(x))
print(f"4. Any collapsible: {len(any_collapsible)}")

# 5. dl tags
dl_tags = soup.find_all('dl')
print(f"5. dl tags: {len(dl_tags)}")

# 6. Check if Abba exists
print(f"6. Contains 'Abba': {'Abba' in html}")
print(f"7. Contains 'Aserai': {'Aserai' in html}")

# 7. Find all links to villages
village_links = soup.find_all('a', href=re.compile(r'/wiki/(Abba|Abghan|Abu_Khih)'))
print(f"8. Village links found: {len(village_links)}")

# 8. Save HTML snippet around "Abba"
if 'Abba' in html:
    idx = html.find('Abba')
    snippet = html[max(0, idx-500):idx+500]
    print("\n" + "=" * 60)
    print("HTML snippet around 'Abba':")
    print("=" * 60)
    print(snippet)

driver.quit()

