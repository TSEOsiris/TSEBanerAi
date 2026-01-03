# Wiki Scraping Scripts

## Installation

```bash
pip install -r requirements.txt
```

## Usage

### Download all wiki pages

```bash
cd scripts
python download_wiki.py
```

This will:
1. Download all English wiki pages to `Database/raw/en/`
2. Pause and wait for confirmation
3. Download all Russian wiki pages to `Database/raw/ru/`
4. Pause and wait for confirmation
5. Download all Turkish wiki pages to `Database/raw/tr/`

### Features

- ✅ Uses official Fandom MediaWiki API (fast and reliable)
- ✅ Filters Bannerlord-specific pages (excludes Warband, etc.)
- ✅ Saves progress (can resume if interrupted)
- ✅ Polite delays between requests (1 second)
- ✅ Safe filenames (removes invalid characters)
- ✅ Detailed logging

### Output Structure

```
Database/
└── raw/
    ├── en/
    │   ├── Vlandia.html
    │   ├── Derthert.html
    │   ├── Banu_Arbas.html
    │   ├── ...
    │   └── _download_log.json  (progress tracking)
    ├── ru/
    │   └── ...
    └── tr/
        └── ...
```

## Sorting Pages

After downloading, sort pages into categories:

```bash
python sort_wiki_pages.py Database/raw/en
```

This will:
- Analyze each HTML page's categories
- Sort into folders: Factions, Clans, Persons, Settlements (Towns/Castles/Villages), Mechanics, History
- Move unsorted pages to `Unsorted/` folder for manual review
- Create a sorted structure in `Database/raw/en/sorted/`

### Auto-detection Rules

- **Towns**: Contains "Bannerlord Towns" category
- **Castles**: Contains "Bannerlord Castles" or "Castle" in title
- **Villages**: Contains "Bannerlord Villages"
- **Factions**: Contains "Kingdoms of Bannerlord" or kingdom names
- **Persons**: Contains "Bannerlord Characters" or "Leaders of Bannerlord"
- **Clans**: Starts with "Banu", "Clan", or "House of"
- **Mechanics**: Contains "Game mechanics"
- **History**: Contains "History" or "Battles"

## Next Steps

After sorting:
1. Review `Unsorted/` folder and manually categorize if needed
2. Run parser scripts to extract structured data
3. Generate training examples for fine-tuning

## Extracting Encyclopedia Data from Game Files

Extract all encyclopedia data directly from Mount & Blade II Bannerlord game files:

```bash
cd scripts
extract_encyclopedia.bat
```

Or manually:

```bash
python extract_encyclopedia_data.py [game_path] [output_dir] [language]
```

### Parameters

- `game_path`: Path to Mount & Blade II Bannerlord installation folder (default: `C:\TSEBanerAi\Mount & Blade II Bannerlord`)
- `output_dir`: Directory to save extracted data (default: `Database/encyclopedia`)
- `language`: Language code for localization (default: `RU`)

### What It Extracts

The script extracts:

- **Heroes**: All characters with their relationships, factions, and descriptions
- **Settlements**: Towns, castles, and villages with locations and descriptions
- **Kingdoms**: All kingdoms with relationships, policies, and lore
- **Clans**: Clan information and members
- **Cultures**: Cultural information and descriptions
- **Concepts**: Game concepts and mechanics explanations
- **World Lore**: Historical events, character relationships, and world background

### Output Format

The script generates:

1. **JSON files**: Separate JSON file for each data type (`heroes.json`, `settlements.json`, etc.)
2. **SQLite database**: `encyclopedia.db` with normalized tables and relationships

### Database Schema

The SQLite database includes:

- `heroes` - Character information
- `settlements` - Settlement data (towns, castles, villages)
- `kingdoms` - Kingdom information
- `kingdom_relationships` - Relationships between kingdoms
- `kingdom_policies` - Policies of each kingdom
- `clans` - Clan data
- `clan_members` - Clan membership
- `cultures` - Cultural information
- `concepts` - Game concepts
- `world_lore` - World lore entries
- `world_lore_tags` - Tags for lore entries
- `world_lore_chars` - Characters mentioned in lore

### Usage Example

```python
import sqlite3

# Connect to database
conn = sqlite3.connect('Database/encyclopedia/encyclopedia.db')
cursor = conn.cursor()

# Query all heroes
cursor.execute("SELECT * FROM heroes WHERE alive = 1")
heroes = cursor.fetchall()

# Query settlements by type
cursor.execute("SELECT * FROM settlements WHERE type = 'town'")
towns = cursor.fetchall()

# Query kingdom relationships
cursor.execute("""
    SELECT k1.name, k2.name, kr.value, kr.isAtWar
    FROM kingdom_relationships kr
    JOIN kingdoms k1 ON kr.kingdom_id = k1.id
    JOIN kingdoms k2 ON kr.related_kingdom = k2.id
""")
relationships = cursor.fetchall()
```

## Extracting Data from Digital Companion

Extract metadata and information from Mount & Blade II Bannerlord Digital Companion application:

```bash
cd scripts
extract_digital_companion.bat
```

Or manually:

```bash
python extract_digital_companion_data.py [companion_path] [output_dir]
```

### What It Extracts

The script extracts:

- **Catalog**: Unity Addressables catalog with resource references (527 entries found)
- **Settings**: Unity Addressables settings and configuration
- **Bundle Information**: Analysis of 163 Unity Asset Bundles (941.88 MB total)
  - 15 localization bundles (for multiple languages)
  - 148 asset bundles (game assets, UI, etc.)
- **Metadata**: Scripting assemblies and runtime initialization data

### Output Format

The script generates:

1. **JSON files**: 
   - `catalog.json` - Full Unity Addressables catalog
   - `catalog_summary.json` - Summary of catalog contents
   - `bundle_info.json` - Detailed information about all bundles
   - `settings.json` - Unity Addressables settings
   - `scripting_assemblies.json` - List of .NET assemblies
   - `runtime_initialize.json` - Runtime initialization data
   - `extraction_summary.json` - Summary of extraction

2. **SQLite database**: `digital_companion.db` with bundle and localization information

### Limitations

- **Unity Asset Bundles** are binary files that require special tools (like UnityPy) for full extraction
- **Localization bundles** contain text data but are in Unity's binary format
- The script extracts metadata and catalog information, but not the actual asset content from bundles

### Note

For deeper extraction of Unity Asset Bundle contents, you may need to use specialized tools like:
- UnityPy (Python library for Unity asset extraction)
- AssetStudio (GUI tool for Unity asset extraction)
- uTinyRipper (command-line Unity asset extractor)

