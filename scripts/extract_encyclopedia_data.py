"""
Extract encyclopedia data from Mount & Blade II Bannerlord game files
Extracts heroes, settlements, kingdoms, concepts, clans, cultures, and other game data
"""

import xml.etree.ElementTree as ET
import os
import json
import sqlite3
import re
from pathlib import Path
from typing import Dict, List, Optional, Any
from collections import defaultdict

class EncyclopediaExtractor:
    def __init__(self, game_path: str, output_dir: str = "Database/encyclopedia", language: str = "RU"):
        """
        game_path: Path to Mount & Blade II Bannerlord installation folder
        output_dir: Directory to save extracted data
        language: Language code for localization (RU, EN, etc.)
        """
        self.game_path = Path(game_path)
        self.output_dir = Path(output_dir)
        self.language = language
        self.modules_path = self.game_path / "Modules"
        
        # Create output directory
        self.output_dir.mkdir(parents=True, exist_ok=True)
        
        # Storage for localization strings
        self.localization_strings: Dict[str, str] = {}
        
        # Storage for extracted data
        self.data = {
            'heroes': [],
            'npc_characters': [],
            'settlements': [],
            'kingdoms': [],
            'clans': [],
            'cultures': [],
            'concepts': [],
            'world_lore': [],
            'items': [],
            'traits': [],
            'factions': []
        }
        
    def load_localization(self):
        """Load localization strings from language files"""
        print("Loading localization strings...")
        
        # Search for language files in all modules
        lang_files = []
        for module_dir in self.modules_path.iterdir():
            if not module_dir.is_dir():
                continue
                
            lang_dir = module_dir / "ModuleData" / "Languages"
            if not lang_dir.exists():
                continue
                
            # For English (EN), use root Languages directory files (std_*.xml)
            # For other languages, use language-specific subdirectory
            if self.language == "EN" or self.language == "":
                # English uses files in root without language suffix
                for xml_file in lang_dir.glob("std_*.xml"):
                    lang_files.append(xml_file)
            else:
                # Other languages use subdirectories
                lang_subdir = lang_dir / self.language
                if lang_subdir.exists():
                    for xml_file in lang_subdir.glob("*.xml"):
                        lang_files.append(xml_file)
        
        # Also check SandBox module for specific language files
        sandbox_lang = self.modules_path / "SandBox" / "ModuleData" / "Languages"
        if sandbox_lang.exists():
            if self.language == "EN" or self.language == "":
                # English uses root files
                for xml_file in sandbox_lang.glob("std_*.xml"):
                    lang_files.append(xml_file)
            else:
                # Other languages use subdirectories
                lang_subdir = sandbox_lang / self.language
                if lang_subdir.exists():
                    for xml_file in lang_subdir.glob("*.xml"):
                        lang_files.append(xml_file)
        
        # Load strings from all found files
        for lang_file in lang_files:
            try:
                tree = ET.parse(lang_file)
                root = tree.getroot()
                
                # Find all string elements
                for string_elem in root.findall(".//string"):
                    string_id = string_elem.get("id", "")
                    text = string_elem.get("text", "")
                    if string_id and text:
                        self.localization_strings[string_id] = text
            except Exception as e:
                print(f"Warning: Could not parse {lang_file}: {e}")
        
        print(f"Loaded {len(self.localization_strings)} localization strings")
    
    def resolve_text(self, text: str) -> str:
        """Resolve localization keys in text (e.g., {=abc123} -> actual text)"""
        if not text:
            return ""
        
        # Pattern: {=string_id} or {=string_id}text
        pattern = r'\{=([^}]+)\}'
        
        def replace_key(match):
            key = match.group(1)
            # Try to find in localization
            if key in self.localization_strings:
                return self.localization_strings[key]
            # If not found, return empty or keep original
            return match.group(0)
        
        resolved = re.sub(pattern, replace_key, text)
        
        # Also handle other patterns like {.MA} tags (formatting)
        resolved = re.sub(r'\{\.\w+\}', '', resolved)
        
        return resolved.strip()
    
    def extract_heroes(self):
        """Extract heroes data from heroes.xml"""
        print("Extracting heroes...")
        heroes_file = self.modules_path / "SandBox" / "ModuleData" / "heroes.xml"
        
        if not heroes_file.exists():
            print(f"Warning: {heroes_file} not found")
            return
        
        try:
            tree = ET.parse(heroes_file)
            root = tree.getroot()
            
            for hero_elem in root.findall("Hero"):
                hero_data = {
                    'id': hero_elem.get("id", ""),
                    'faction': hero_elem.get("faction", ""),
                    'alive': hero_elem.get("alive", "true") != "false",
                    'spouse': hero_elem.get("spouse", ""),
                    'father': hero_elem.get("father", ""),
                    'mother': hero_elem.get("mother", ""),
                    'text': self.resolve_text(hero_elem.get("text", ""))
                }
                
                if hero_data['id']:
                    self.data['heroes'].append(hero_data)
            
            print(f"Extracted {len(self.data['heroes'])} heroes")
        except Exception as e:
            print(f"Error extracting heroes: {e}")
    
    def extract_settlements(self):
        """Extract settlements data from settlements.xml"""
        print("Extracting settlements...")
        settlements_file = self.modules_path / "SandBox" / "ModuleData" / "settlements.xml"
        
        if not settlements_file.exists():
            print(f"Warning: {settlements_file} not found")
            return
        
        try:
            tree = ET.parse(settlements_file)
            root = tree.getroot()
            
            for settlement_elem in root.findall("Settlement"):
                settlement_data = {
                    'id': settlement_elem.get("id", ""),
                    'name': self.resolve_text(settlement_elem.get("name", "")),
                    'owner': settlement_elem.get("owner", ""),
                    'culture': settlement_elem.get("culture", ""),
                    'posX': settlement_elem.get("posX", ""),
                    'posY': settlement_elem.get("posY", ""),
                    'text': self.resolve_text(settlement_elem.get("text", "")),
                    'type': 'unknown'
                }
                
                # Determine settlement type
                town_elem = settlement_elem.find(".//Town")
                village_elem = settlement_elem.find(".//Village")
                
                if town_elem is not None:
                    if town_elem.get("is_castle", "false") == "true":
                        settlement_data['type'] = 'castle'
                    else:
                        settlement_data['type'] = 'town'
                elif village_elem is not None:
                    settlement_data['type'] = 'village'
                    settlement_data['village_type'] = village_elem.get("village_type", "")
                    settlement_data['bound'] = village_elem.get("bound", "")
                
                if settlement_data['id']:
                    self.data['settlements'].append(settlement_data)
            
            print(f"Extracted {len(self.data['settlements'])} settlements")
        except Exception as e:
            print(f"Error extracting settlements: {e}")
    
    def extract_kingdoms(self):
        """Extract kingdoms data from spkingdoms.xml"""
        print("Extracting kingdoms...")
        kingdoms_file = self.modules_path / "SandBox" / "ModuleData" / "spkingdoms.xml"
        
        if not kingdoms_file.exists():
            print(f"Warning: {kingdoms_file} not found")
            return
        
        try:
            tree = ET.parse(kingdoms_file)
            root = tree.getroot()
            
            for kingdom_elem in root.findall("Kingdom"):
                kingdom_data = {
                    'id': kingdom_elem.get("id", ""),
                    'name': self.resolve_text(kingdom_elem.get("name", "")),
                    'short_name': self.resolve_text(kingdom_elem.get("short_name", "")),
                    'title': self.resolve_text(kingdom_elem.get("title", "")),
                    'ruler_title': self.resolve_text(kingdom_elem.get("ruler_title", "")),
                    'culture': kingdom_elem.get("culture", ""),
                    'owner': kingdom_elem.get("owner", ""),
                    'initial_home_settlement': kingdom_elem.get("initial_home_settlement", ""),
                    'text': self.resolve_text(kingdom_elem.get("text", "")),
                    'relationships': [],
                    'policies': []
                }
                
                # Extract relationships
                for rel_elem in kingdom_elem.findall(".//relationship"):
                    kingdom_data['relationships'].append({
                        'kingdom': rel_elem.get("kingdom", ""),
                        'value': rel_elem.get("value", ""),
                        'isAtWar': rel_elem.get("isAtWar", "false") == "true"
                    })
                
                # Extract policies
                for policy_elem in kingdom_elem.findall(".//policy"):
                    kingdom_data['policies'].append(policy_elem.get("id", ""))
                
                if kingdom_data['id']:
                    self.data['kingdoms'].append(kingdom_data)
            
            print(f"Extracted {len(self.data['kingdoms'])} kingdoms")
        except Exception as e:
            print(f"Error extracting kingdoms: {e}")
    
    def extract_clans(self):
        """Extract clans data from spclans.xml"""
        print("Extracting clans...")
        clans_file = self.modules_path / "SandBox" / "ModuleData" / "spclans.xml"
        
        if not clans_file.exists():
            print(f"Warning: {clans_file} not found")
            return
        
        try:
            tree = ET.parse(clans_file)
            root = tree.getroot()
            
            for clan_elem in root.findall("Clan"):
                clan_data = {
                    'id': clan_elem.get("id", ""),
                    'name': self.resolve_text(clan_elem.get("name", "")),
                    'culture': clan_elem.get("culture", ""),
                    'faction': clan_elem.get("faction", ""),
                    'text': self.resolve_text(clan_elem.get("text", "")),
                    'members': []
                }
                
                # Extract members (hero IDs)
                for member_elem in clan_elem.findall(".//member"):
                    clan_data['members'].append(member_elem.get("id", ""))
                
                if clan_data['id']:
                    self.data['clans'].append(clan_data)
            
            print(f"Extracted {len(self.data['clans'])} clans")
        except Exception as e:
            print(f"Error extracting clans: {e}")
    
    def extract_cultures(self):
        """Extract cultures data from spcultures.xml"""
        print("Extracting cultures...")
        cultures_file = self.modules_path / "SandBoxCore" / "ModuleData" / "spcultures.xml"
        
        if not cultures_file.exists():
            print(f"Warning: {cultures_file} not found")
            return
        
        try:
            tree = ET.parse(cultures_file)
            root = tree.getroot()
            
            for culture_elem in root.findall("Culture"):
                culture_data = {
                    'id': culture_elem.get("id", ""),
                    'name': self.resolve_text(culture_elem.get("name", "")),
                    'text': self.resolve_text(culture_elem.get("text", ""))
                }
                
                if culture_data['id']:
                    self.data['cultures'].append(culture_data)
            
            print(f"Extracted {len(self.data['cultures'])} cultures")
        except Exception as e:
            print(f"Error extracting cultures: {e}")
    
    def extract_concepts(self):
        """Extract concepts data from concept_strings.xml"""
        print("Extracting concepts...")
        concepts_file = self.modules_path / "SandBox" / "ModuleData" / "concept_strings.xml"
        
        if not concepts_file.exists():
            print(f"Warning: {concepts_file} not found")
            return
        
        try:
            tree = ET.parse(concepts_file)
            root = tree.getroot()
            
            for concept_elem in root.findall("Concept"):
                concept_data = {
                    'id': concept_elem.get("id", ""),
                    'title': self.resolve_text(concept_elem.get("title", "")),
                    'text': self.resolve_text(concept_elem.get("text", "")),
                    'group': concept_elem.get("group", ""),
                    'link_id': concept_elem.get("link_id", "")
                }
                
                if concept_data['id']:
                    self.data['concepts'].append(concept_data)
            
            print(f"Extracted {len(self.data['concepts'])} concepts")
        except Exception as e:
            print(f"Error extracting concepts: {e}")
    
    def extract_world_lore(self):
        """Extract world lore strings from world_lore_strings.xml"""
        print("Extracting world lore...")
        lore_file = self.modules_path / "SandBox" / "ModuleData" / "world_lore_strings.xml"
        
        if not lore_file.exists():
            print(f"Warning: {lore_file} not found")
            return
        
        try:
            tree = ET.parse(lore_file)
            root = tree.getroot()
            
            for string_elem in root.findall(".//string"):
                lore_data = {
                    'id': string_elem.get("id", ""),
                    'text': self.resolve_text(string_elem.get("text", "")),
                    'tags': [],
                    'chars': []
                }
                
                # Extract tags
                for tag_elem in string_elem.findall(".//tag"):
                    lore_data['tags'].append(tag_elem.get("tag_name", ""))
                
                # Extract characters
                for char_elem in string_elem.findall(".//char"):
                    lore_data['chars'].append(char_elem.get("char_name", ""))
                
                if lore_data['id'] and lore_data['text']:
                    self.data['world_lore'].append(lore_data)
            
            print(f"Extracted {len(self.data['world_lore'])} world lore entries")
        except Exception as e:
            print(f"Error extracting world lore: {e}")
    
    def extract_npc_characters(self):
        """Extract NPC characters data from lords.xml"""
        print("Extracting NPC characters...")
        lords_file = self.modules_path / "SandBox" / "ModuleData" / "lords.xml"
        
        if not lords_file.exists():
            print(f"Warning: {lords_file} not found")
            return
        
        try:
            tree = ET.parse(lords_file)
            root = tree.getroot()
            
            for npc_elem in root.findall("NPCCharacter"):
                npc_data = {
                    'id': npc_elem.get("id", ""),
                    'name': self.resolve_text(npc_elem.get("name", "")),
                    'culture': npc_elem.get("culture", ""),
                    'is_hero': npc_elem.get("is_hero", "false") == "true",
                    'is_female': npc_elem.get("is_female", "false") == "true",
                    'age': npc_elem.get("age", ""),
                    'occupation': npc_elem.get("occupation", ""),
                    'voice': npc_elem.get("voice", ""),
                    'default_group': npc_elem.get("default_group", ""),
                    'skills': {}
                }
                
                # Extract skills
                for skill_elem in npc_elem.findall(".//skill"):
                    skill_id = skill_elem.get("id", "")
                    skill_value = skill_elem.get("value", "0")
                    if skill_id:
                        npc_data['skills'][skill_id] = skill_value
                
                if npc_data['id']:
                    self.data['npc_characters'].append(npc_data)
            
            print(f"Extracted {len(self.data['npc_characters'])} NPC characters")
        except Exception as e:
            print(f"Error extracting NPC characters: {e}")
    
    def extract_items(self):
        """Extract items data from items XML files"""
        print("Extracting items...")
        items_dir = self.modules_path / "SandBoxCore" / "ModuleData" / "items"
        
        if not items_dir.exists():
            print(f"Warning: {items_dir} not found")
            return
        
        item_files = list(items_dir.glob("*.xml"))
        
        for item_file in item_files:
            try:
                tree = ET.parse(item_file)
                root = tree.getroot()
                
                # Process Item elements
                for item_elem in root.findall("Item"):
                    item_data = {
                        'id': item_elem.get("id", ""),
                        'name': self.resolve_text(item_elem.get("name", "")),
                        'type': item_elem.get("Type", ""),
                        'culture': item_elem.get("culture", ""),
                        'value': item_elem.get("value", ""),
                        'weight': item_elem.get("weight", ""),
                        'is_merchandise': item_elem.get("is_merchandise", "true") == "true",
                        'source_file': item_file.name
                    }
                    
                    # Extract weapon data if present
                    weapon_elem = item_elem.find(".//Weapon")
                    if weapon_elem is not None:
                        item_data['weapon_class'] = weapon_elem.get("weapon_class", "")
                        item_data['weapon_length'] = weapon_elem.get("weapon_length", "")
                        item_data['swing_damage'] = weapon_elem.get("swing_damage", "")
                        item_data['thrust_damage'] = weapon_elem.get("thrust_damage", "")
                    
                    if item_data['id']:
                        self.data['items'].append(item_data)
                
                # Process CraftedItem elements
                for crafted_elem in root.findall("CraftedItem"):
                    item_data = {
                        'id': crafted_elem.get("id", ""),
                        'name': self.resolve_text(crafted_elem.get("name", "")),
                        'type': 'CraftedItem',
                        'culture': crafted_elem.get("culture", ""),
                        'crafting_template': crafted_elem.get("crafting_template", ""),
                        'is_merchandise': crafted_elem.get("is_merchandise", "true") == "true",
                        'source_file': item_file.name
                    }
                    
                    if item_data['id']:
                        self.data['items'].append(item_data)
                        
            except Exception as e:
                print(f"Warning: Could not parse {item_file}: {e}")
        
        print(f"Extracted {len(self.data['items'])} items")
    
    def extract_traits(self):
        """Extract personality traits from trait_strings.xml"""
        print("Extracting traits...")
        traits_file = self.modules_path / "SandBox" / "ModuleData" / "trait_strings.xml"
        
        if not traits_file.exists():
            print(f"Warning: {traits_file} not found")
            return
        
        try:
            tree = ET.parse(traits_file)
            root = tree.getroot()
            
            for string_elem in root.findall(".//string"):
                trait_data = {
                    'id': string_elem.get("id", ""),
                    'text': self.resolve_text(string_elem.get("text", ""))
                }
                
                if trait_data['id'] and trait_data['text']:
                    self.data['traits'].append(trait_data)
            
            print(f"Extracted {len(self.data['traits'])} traits")
        except Exception as e:
            print(f"Error extracting traits: {e}")
    
    def save_to_json(self):
        """Save extracted data to JSON files"""
        print("Saving to JSON...")
        
        for data_type, items in self.data.items():
            if items:
                output_file = self.output_dir / f"{data_type}.json"
                with open(output_file, 'w', encoding='utf-8') as f:
                    json.dump(items, f, ensure_ascii=False, indent=2)
                print(f"Saved {len(items)} {data_type} to {output_file}")
    
    def save_to_sqlite(self):
        """Save extracted data to SQLite database"""
        print("Saving to SQLite database...")
        
        db_file = self.output_dir / "encyclopedia.db"
        conn = sqlite3.connect(db_file)
        cursor = conn.cursor()
        
        # Create tables
        cursor.execute("""
            CREATE TABLE IF NOT EXISTS heroes (
                id TEXT PRIMARY KEY,
                faction TEXT,
                alive INTEGER,
                spouse TEXT,
                father TEXT,
                mother TEXT,
                text TEXT
            )
        """)
        
        cursor.execute("""
            CREATE TABLE IF NOT EXISTS settlements (
                id TEXT PRIMARY KEY,
                name TEXT,
                owner TEXT,
                culture TEXT,
                posX TEXT,
                posY TEXT,
                text TEXT,
                type TEXT,
                village_type TEXT,
                bound TEXT
            )
        """)
        
        cursor.execute("""
            CREATE TABLE IF NOT EXISTS kingdoms (
                id TEXT PRIMARY KEY,
                name TEXT,
                short_name TEXT,
                title TEXT,
                ruler_title TEXT,
                culture TEXT,
                owner TEXT,
                initial_home_settlement TEXT,
                text TEXT
            )
        """)
        
        cursor.execute("""
            CREATE TABLE IF NOT EXISTS kingdom_relationships (
                kingdom_id TEXT,
                related_kingdom TEXT,
                value TEXT,
                isAtWar INTEGER,
                FOREIGN KEY (kingdom_id) REFERENCES kingdoms(id)
            )
        """)
        
        cursor.execute("""
            CREATE TABLE IF NOT EXISTS kingdom_policies (
                kingdom_id TEXT,
                policy_id TEXT,
                FOREIGN KEY (kingdom_id) REFERENCES kingdoms(id)
            )
        """)
        
        cursor.execute("""
            CREATE TABLE IF NOT EXISTS clans (
                id TEXT PRIMARY KEY,
                name TEXT,
                culture TEXT,
                faction TEXT,
                text TEXT
            )
        """)
        
        cursor.execute("""
            CREATE TABLE IF NOT EXISTS clan_members (
                clan_id TEXT,
                member_id TEXT,
                FOREIGN KEY (clan_id) REFERENCES clans(id)
            )
        """)
        
        cursor.execute("""
            CREATE TABLE IF NOT EXISTS cultures (
                id TEXT PRIMARY KEY,
                name TEXT,
                text TEXT
            )
        """)
        
        cursor.execute("""
            CREATE TABLE IF NOT EXISTS concepts (
                id TEXT PRIMARY KEY,
                title TEXT,
                text TEXT,
                group_name TEXT,
                link_id TEXT
            )
        """)
        
        cursor.execute("""
            CREATE TABLE IF NOT EXISTS world_lore (
                id TEXT PRIMARY KEY,
                text TEXT
            )
        """)
        
        cursor.execute("""
            CREATE TABLE IF NOT EXISTS world_lore_tags (
                lore_id TEXT,
                tag TEXT,
                FOREIGN KEY (lore_id) REFERENCES world_lore(id)
            )
        """)
        
        cursor.execute("""
            CREATE TABLE IF NOT EXISTS world_lore_chars (
                lore_id TEXT,
                char_id TEXT,
                FOREIGN KEY (lore_id) REFERENCES world_lore(id)
            )
        """)
        
        cursor.execute("""
            CREATE TABLE IF NOT EXISTS npc_characters (
                id TEXT PRIMARY KEY,
                name TEXT,
                culture TEXT,
                is_hero INTEGER,
                is_female INTEGER,
                age TEXT,
                occupation TEXT,
                voice TEXT,
                default_group TEXT
            )
        """)
        
        cursor.execute("""
            CREATE TABLE IF NOT EXISTS npc_skills (
                npc_id TEXT,
                skill_id TEXT,
                skill_value TEXT,
                FOREIGN KEY (npc_id) REFERENCES npc_characters(id)
            )
        """)
        
        cursor.execute("""
            CREATE TABLE IF NOT EXISTS items (
                id TEXT PRIMARY KEY,
                name TEXT,
                type TEXT,
                culture TEXT,
                value TEXT,
                weight TEXT,
                is_merchandise INTEGER,
                weapon_class TEXT,
                weapon_length TEXT,
                swing_damage TEXT,
                thrust_damage TEXT,
                crafting_template TEXT,
                source_file TEXT
            )
        """)
        
        cursor.execute("""
            CREATE TABLE IF NOT EXISTS traits (
                id TEXT PRIMARY KEY,
                text TEXT
            )
        """)
        
        # Insert data
        for hero in self.data['heroes']:
            cursor.execute("""
                INSERT OR REPLACE INTO heroes VALUES (?, ?, ?, ?, ?, ?, ?)
            """, (hero['id'], hero['faction'], 1 if hero['alive'] else 0,
                  hero['spouse'], hero['father'], hero['mother'], hero['text']))
        
        for settlement in self.data['settlements']:
            cursor.execute("""
                INSERT OR REPLACE INTO settlements VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """, (settlement['id'], settlement['name'], settlement['owner'],
                  settlement['culture'], settlement['posX'], settlement['posY'],
                  settlement['text'], settlement['type'],
                  settlement.get('village_type', ''), settlement.get('bound', '')))
        
        for kingdom in self.data['kingdoms']:
            cursor.execute("""
                INSERT OR REPLACE INTO kingdoms VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
            """, (kingdom['id'], kingdom['name'], kingdom['short_name'],
                  kingdom['title'], kingdom['ruler_title'], kingdom['culture'],
                  kingdom['owner'], kingdom['initial_home_settlement'], kingdom['text']))
            
            for rel in kingdom['relationships']:
                cursor.execute("""
                    INSERT INTO kingdom_relationships VALUES (?, ?, ?, ?)
                """, (kingdom['id'], rel['kingdom'], rel['value'], 1 if rel['isAtWar'] else 0))
            
            for policy in kingdom['policies']:
                cursor.execute("""
                    INSERT INTO kingdom_policies VALUES (?, ?)
                """, (kingdom['id'], policy))
        
        for clan in self.data['clans']:
            cursor.execute("""
                INSERT OR REPLACE INTO clans VALUES (?, ?, ?, ?, ?)
            """, (clan['id'], clan['name'], clan['culture'], clan['faction'], clan['text']))
            
            for member in clan['members']:
                cursor.execute("""
                    INSERT INTO clan_members VALUES (?, ?)
                """, (clan['id'], member))
        
        for culture in self.data['cultures']:
            cursor.execute("""
                INSERT OR REPLACE INTO cultures VALUES (?, ?, ?)
            """, (culture['id'], culture['name'], culture['text']))
        
        for concept in self.data['concepts']:
            cursor.execute("""
                INSERT OR REPLACE INTO concepts VALUES (?, ?, ?, ?, ?)
            """, (concept['id'], concept['title'], concept['text'],
                  concept['group'], concept['link_id']))
        
        for lore in self.data['world_lore']:
            cursor.execute("""
                INSERT OR REPLACE INTO world_lore VALUES (?, ?)
            """, (lore['id'], lore['text']))
            
            for tag in lore['tags']:
                cursor.execute("""
                    INSERT INTO world_lore_tags VALUES (?, ?)
                """, (lore['id'], tag))
            
            for char in lore['chars']:
                cursor.execute("""
                    INSERT INTO world_lore_chars VALUES (?, ?)
                """, (lore['id'], char))
        
        for npc in self.data['npc_characters']:
            cursor.execute("""
                INSERT OR REPLACE INTO npc_characters VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
            """, (npc['id'], npc['name'], npc['culture'], 1 if npc['is_hero'] else 0,
                  1 if npc['is_female'] else 0, npc['age'], npc['occupation'],
                  npc['voice'], npc['default_group']))
            
            for skill_id, skill_value in npc['skills'].items():
                cursor.execute("""
                    INSERT INTO npc_skills VALUES (?, ?, ?)
                """, (npc['id'], skill_id, skill_value))
        
        for item in self.data['items']:
            cursor.execute("""
                INSERT OR REPLACE INTO items VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """, (item['id'], item['name'], item['type'], item['culture'],
                  item.get('value', ''), item.get('weight', ''),
                  1 if item.get('is_merchandise', True) else 0,
                  item.get('weapon_class', ''), item.get('weapon_length', ''),
                  item.get('swing_damage', ''), item.get('thrust_damage', ''),
                  item.get('crafting_template', ''), item.get('source_file', '')))
        
        for trait in self.data['traits']:
            cursor.execute("""
                INSERT OR REPLACE INTO traits VALUES (?, ?)
            """, (trait['id'], trait['text']))
        
        conn.commit()
        conn.close()
        print(f"Saved to SQLite database: {db_file}")
    
    def extract_all(self):
        """Extract all encyclopedia data"""
        print("Starting encyclopedia data extraction...")
        print(f"Game path: {self.game_path}")
        print(f"Output directory: {self.output_dir}")
        print(f"Language: {self.language}")
        
        # Load localization first
        self.load_localization()
        
        # Extract all data types
        self.extract_heroes()
        self.extract_npc_characters()
        self.extract_settlements()
        self.extract_kingdoms()
        self.extract_clans()
        self.extract_cultures()
        self.extract_concepts()
        self.extract_world_lore()
        self.extract_items()
        self.extract_traits()
        
        # Save results
        self.save_to_json()
        self.save_to_sqlite()
        
        print("\nExtraction complete!")
        print(f"Total extracted:")
        for data_type, items in self.data.items():
            print(f"  {data_type}: {len(items)}")


def main():
    import sys
    
    # Default paths
    default_game_path = r"C:\TSEBanerAi\Mount & Blade II Bannerlord"
    default_output = "Database/encyclopedia"
    
    # Get paths from command line or use defaults
    game_path = sys.argv[1] if len(sys.argv) > 1 else default_game_path
    output_dir = sys.argv[2] if len(sys.argv) > 2 else default_output
    language = sys.argv[3] if len(sys.argv) > 3 else "RU"
    
    if not os.path.exists(game_path):
        print(f"Error: Game path not found: {game_path}")
        print("Usage: python extract_encyclopedia_data.py [game_path] [output_dir] [language]")
        return
    
    extractor = EncyclopediaExtractor(game_path, output_dir, language)
    extractor.extract_all()


if __name__ == "__main__":
    main()

