#!/usr/bin/env python3
"""
Экспорт данных из SQL базы для fine-tuning
Извлекает описания кланов, городов, замков, деревень, лордов, фракций
и данные только из английского экземпляра
"""

import sqlite3
import json
import sys
from pathlib import Path
from typing import Dict, List, Any

# Настройка кодировки для Windows
if sys.platform == 'win32':
    import io
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8')


class FineTuningDataExporter:
    """Экспорт данных для fine-tuning"""
    
    def __init__(self, db_path: Path, output_dir: Path):
        self.db_path = db_path
        self.output_dir = output_dir
        self.output_dir.mkdir(parents=True, exist_ok=True)
        self.conn = None
        
    def connect(self):
        """Подключиться к БД"""
        self.conn = sqlite3.connect(str(self.db_path))
        self.conn.row_factory = sqlite3.Row
        return self.conn
    
    def close(self):
        """Закрыть соединение"""
        if self.conn:
            self.conn.close()
    
    def get_all_tables(self):
        """Получить список всех таблиц"""
        cursor = self.conn.cursor()
        cursor.execute("SELECT name FROM sqlite_master WHERE type='table' ORDER BY name")
        return [row[0] for row in cursor.fetchall()]
    
    def export_clans(self):
        """Экспорт описаний кланов"""
        cursor = self.conn.cursor()
        
        # Из campaign_clans (мультиязычные данные)
        cursor.execute('''
            SELECT 
                id,
                name AS name_en,
                name_ru,
                name_tr,
                description AS description_en,
                description_ru,
                description_tr,
                culture,
                kingdom
            FROM campaign_clans
            WHERE description IS NOT NULL 
               OR description_ru IS NOT NULL 
               OR description_tr IS NOT NULL
        ''')
        
        clans = []
        for row in cursor.fetchall():
            clan_data = {
                'id': row['id'],
                'name_en': row['name_en'],
                'name_ru': row['name_ru'] if 'name_ru' in row.keys() and row['name_ru'] else None,
                'name_tr': row['name_tr'] if 'name_tr' in row.keys() and row['name_tr'] else None,
                'description_en': row['description_en'] if row['description_en'] else None,
                'description_ru': row['description_ru'] if 'description_ru' in row.keys() and row['description_ru'] else None,
                'description_tr': row['description_tr'] if 'description_tr' in row.keys() and row['description_tr'] else None,
                'culture': row['culture'] if row['culture'] else None,
                'kingdom': row['kingdom'] if row['kingdom'] else None
            }
            clans.append(clan_data)
        
        # Также из таблицы clans (если есть)
        try:
            cursor.execute('''
                SELECT 
                    id,
                    name,
                    text,
                    wiki_description
                FROM clans
                WHERE text IS NOT NULL OR wiki_description IS NOT NULL
            ''')
            
            for row in cursor.fetchall():
                # Проверяем, нет ли уже этого клана
                if not any(c['id'] == row['id'] for c in clans):
                    clans.append({
                        'id': row['id'],
                        'name_en': row['name'] if row['name'] else None,
                        'description_en': row['text'] if row['text'] else (row['wiki_description'] if 'wiki_description' in row.keys() and row['wiki_description'] else None),
                        'source': 'encyclopedia'
                    })
        except sqlite3.OperationalError:
            pass  # Таблица clans может не существовать
        
        output_file = self.output_dir / 'clans.json'
        with open(output_file, 'w', encoding='utf-8') as f:
            json.dump(clans, f, ensure_ascii=False, indent=2)
        
        print(f"✅ Exported {len(clans)} clans to {output_file.name}")
        return len(clans)
    
    def export_settlements(self):
        """Экспорт описаний городов, замков, деревень"""
        cursor = self.conn.cursor()
        
        settlements = []
        
        # Из campaign_settlements
        cursor.execute('''
            SELECT 
                id,
                name AS name_en,
                name_ru,
                name_tr,
                settlement_type,
                culture,
                owner_name AS owner_name_en,
                owner_name_ru,
                owner_name_tr
            FROM campaign_settlements
        ''')
        
        for row in cursor.fetchall():
            settlement_data = {
                'id': row['id'],
                'name_en': row['name_en'],
                'name_ru': row['name_ru'] if 'name_ru' in row.keys() and row['name_ru'] else None,
                'name_tr': row['name_tr'] if 'name_tr' in row.keys() and row['name_tr'] else None,
                'type': row['settlement_type'] if row['settlement_type'] else None,
                'culture': row['culture'] if row['culture'] else None,
                'owner_name_en': row['owner_name_en'] if row['owner_name_en'] else None,
                'owner_name_ru': row['owner_name_ru'] if 'owner_name_ru' in row.keys() and row['owner_name_ru'] else None,
                'owner_name_tr': row['owner_name_tr'] if 'owner_name_tr' in row.keys() and row['owner_name_tr'] else None
            }
            settlements.append(settlement_data)
        
        # Из settlements (энциклопедия) - только английский
        try:
            cursor.execute('''
                SELECT 
                    id,
                    name,
                    text,
                    type,
                    culture,
                    wiki_description
                FROM settlements
                WHERE text IS NOT NULL OR wiki_description IS NOT NULL
            ''')
            
            for row in cursor.fetchall():
                # Объединяем с существующими или добавляем новые
                existing = next((s for s in settlements if s['id'] == row['id']), None)
                if existing:
                    existing['description_en'] = row['text'] if row['text'] else (row['wiki_description'] if 'wiki_description' in row.keys() and row['wiki_description'] else None)
                else:
                    settlements.append({
                        'id': row['id'],
                        'name_en': row['name'] if row['name'] else None,
                        'description_en': row['text'] if row['text'] else (row['wiki_description'] if 'wiki_description' in row.keys() and row['wiki_description'] else None),
                        'type': row['type'] if row['type'] else None,
                        'culture': row['culture'] if row['culture'] else None,
                        'source': 'encyclopedia'
                    })
        except sqlite3.OperationalError:
            pass
        
        # Из settlements_fts (только английский)
        try:
            cursor.execute('''
                SELECT 
                    name,
                    text
                FROM settlements_fts
                WHERE text IS NOT NULL AND text != ''
            ''')
            
            for row in cursor.fetchall():
                # Ищем по имени и добавляем описание
                for settlement in settlements:
                    if settlement.get('name_en') == row['name']:
                        if 'description_en' not in settlement or not settlement['description_en']:
                            settlement['description_en'] = row['text'] if row['text'] else None
                        break
        except sqlite3.OperationalError:
            pass
        
        output_file = self.output_dir / 'settlements.json'
        with open(output_file, 'w', encoding='utf-8') as f:
            json.dump(settlements, f, ensure_ascii=False, indent=2)
        
        print(f"✅ Exported {len(settlements)} settlements to {output_file.name}")
        return len(settlements)
    
    def export_lords(self):
        """Экспорт описаний лордов (героев)"""
        cursor = self.conn.cursor()
        
        lords = []
        
        # Из campaign_heroes
        cursor.execute('''
            SELECT 
                id,
                name AS name_en,
                name_ru,
                name_tr,
                culture,
                clan_id,
                age,
                is_female
            FROM campaign_heroes
        ''')
        
        for row in cursor.fetchall():
            lord_data = {
                'id': row['id'],
                'name_en': row['name_en'],
                'name_ru': row['name_ru'] if 'name_ru' in row.keys() and row['name_ru'] else None,
                'name_tr': row['name_tr'] if 'name_tr' in row.keys() and row['name_tr'] else None,
                'culture': row['culture'] if row['culture'] else None,
                'clan_id': row['clan_id'] if row['clan_id'] else None,
                'age': row['age'] if row['age'] else None,
                'is_female': bool(row['is_female']) if row['is_female'] is not None else False
            }
            lords.append(lord_data)
        
        # Из heroes (энциклопедия) - только английский
        try:
            cursor.execute('''
                SELECT 
                    id,
                    text,
                    wiki_biography
                FROM heroes
                WHERE text IS NOT NULL OR wiki_biography IS NOT NULL
            ''')
            
            for row in cursor.fetchall():
                existing = next((l for l in lords if l['id'] == row['id']), None)
                if existing:
                    existing['description_en'] = row['text'] if row['text'] else (row['wiki_biography'] if 'wiki_biography' in row.keys() and row['wiki_biography'] else None)
                else:
                    lords.append({
                        'id': row['id'],
                        'description_en': row['text'] if row['text'] else (row['wiki_biography'] if 'wiki_biography' in row.keys() and row['wiki_biography'] else None),
                        'source': 'encyclopedia'
                    })
        except sqlite3.OperationalError:
            pass
        
        output_file = self.output_dir / 'lords.json'
        with open(output_file, 'w', encoding='utf-8') as f:
            json.dump(lords, f, ensure_ascii=False, indent=2)
        
        print(f"✅ Exported {len(lords)} lords to {output_file.name}")
        return len(lords)
    
    def export_factions(self):
        """Экспорт описаний фракций (королевств)"""
        cursor = self.conn.cursor()
        
        factions = []
        
        # Из campaign_kingdoms
        cursor.execute('''
            SELECT 
                id,
                name AS name_en,
                name_ru,
                name_tr,
                culture,
                ruler_name AS ruler_name_en,
                ruler_name_ru,
                ruler_name_tr
            FROM campaign_kingdoms
        ''')
        
        for row in cursor.fetchall():
            faction_data = {
                'id': row['id'],
                'name_en': row['name_en'],
                'name_ru': row['name_ru'] if 'name_ru' in row.keys() and row['name_ru'] else None,
                'name_tr': row['name_tr'] if 'name_tr' in row.keys() and row['name_tr'] else None,
                'culture': row['culture'] if row['culture'] else None,
                'ruler_name_en': row['ruler_name_en'] if row['ruler_name_en'] else None,
                'ruler_name_ru': row['ruler_name_ru'] if 'ruler_name_ru' in row.keys() and row['ruler_name_ru'] else None,
                'ruler_name_tr': row['ruler_name_tr'] if 'ruler_name_tr' in row.keys() and row['ruler_name_tr'] else None
            }
            factions.append(faction_data)
        
        # Из kingdoms (энциклопедия) - только английский
        try:
            cursor.execute('''
                SELECT 
                    id,
                    name,
                    text,
                    wiki_description
                FROM kingdoms
                WHERE text IS NOT NULL OR wiki_description IS NOT NULL
            ''')
            
            for row in cursor.fetchall():
                existing = next((f for f in factions if f['id'] == row['id']), None)
                if existing:
                    existing['description_en'] = row['text'] if row['text'] else (row['wiki_description'] if 'wiki_description' in row.keys() and row['wiki_description'] else None)
                else:
                    factions.append({
                        'id': row['id'],
                        'name_en': row['name'] if row['name'] else None,
                        'description_en': row['text'] if row['text'] else (row['wiki_description'] if 'wiki_description' in row.keys() and row['wiki_description'] else None),
                        'source': 'encyclopedia'
                    })
        except sqlite3.OperationalError:
            pass
        
        output_file = self.output_dir / 'factions.json'
        with open(output_file, 'w', encoding='utf-8') as f:
            json.dump(factions, f, ensure_ascii=False, indent=2)
        
        print(f"✅ Exported {len(factions)} factions to {output_file.name}")
        return len(factions)
    
    def export_english_only_data(self):
        """Экспорт данных только из английского экземпляра"""
        cursor = self.conn.cursor()
        english_data = {}
        
        # kingdoms - text
        try:
            cursor.execute('SELECT id, name, text FROM kingdoms WHERE text IS NOT NULL')
            english_data['kingdoms'] = [
                {'id': row['id'], 'name': row['name'], 'text': row['text']}
                for row in cursor.fetchall()
            ]
            print(f"   ✅ Exported {len(english_data['kingdoms'])} kingdoms texts")
        except sqlite3.OperationalError as e:
            print(f"   ⚠️  Error exporting kingdoms: {e}")
            english_data['kingdoms'] = []
        
        # settlements_fts - text
        try:
            cursor.execute('SELECT name, text FROM settlements_fts WHERE text IS NOT NULL AND text != ""')
            english_data['settlements_fts'] = [
                {'name': row['name'], 'text': row['text']}
                for row in cursor.fetchall()
            ]
            print(f"   ✅ Exported {len(english_data['settlements_fts'])} settlements_fts texts")
        except sqlite3.OperationalError as e:
            print(f"   ⚠️  Error exporting settlements_fts: {e}")
            english_data['settlements_fts'] = []
        
        # cultures - text
        try:
            cursor.execute('SELECT id, name, text FROM cultures WHERE text IS NOT NULL')
            english_data['cultures'] = [
                {'id': row['id'], 'name': row['name'], 'text': row['text']}
                for row in cursor.fetchall()
            ]
            print(f"   ✅ Exported {len(english_data['cultures'])} cultures texts")
        except sqlite3.OperationalError as e:
            print(f"   ⚠️  Error exporting cultures: {e}")
            english_data['cultures'] = []
        
        # concepts - text (используем title вместо name)
        try:
            cursor.execute('SELECT id, title, text, group_name FROM concepts WHERE text IS NOT NULL')
            english_data['concepts'] = [
                {
                    'id': row['id'], 
                    'title': row['title'] if row['title'] else None,
                    'text': row['text'],
                    'group_name': row['group_name'] if 'group_name' in row.keys() and row['group_name'] else None
                }
                for row in cursor.fetchall()
            ]
            print(f"   ✅ Exported {len(english_data['concepts'])} concepts texts")
        except sqlite3.OperationalError as e:
            print(f"   ⚠️  Error exporting concepts: {e}")
            english_data['concepts'] = []
        
        # world_lore - text (нет колонки name, используем wiki_title)
        try:
            cursor.execute('SELECT id, text, wiki_title FROM world_lore WHERE text IS NOT NULL')
            english_data['world_lore'] = [
                {
                    'id': row['id'], 
                    'text': row['text'],
                    'wiki_title': row['wiki_title'] if 'wiki_title' in row.keys() and row['wiki_title'] else None
                }
                for row in cursor.fetchall()
            ]
            print(f"   ✅ Exported {len(english_data['world_lore'])} world_lore texts")
        except sqlite3.OperationalError as e:
            print(f"   ⚠️  Error exporting world_lore: {e}")
            english_data['world_lore'] = []
        
        # world_lore_fts - text
        try:
            cursor.execute('SELECT text FROM world_lore_fts WHERE text IS NOT NULL AND text != ""')
            english_data['world_lore_fts'] = [
                {'text': row['text']}
                for row in cursor.fetchall()
            ]
            print(f"   ✅ Exported {len(english_data['world_lore_fts'])} world_lore_fts texts")
        except sqlite3.OperationalError as e:
            print(f"   ⚠️  Error exporting world_lore_fts: {e}")
            english_data['world_lore_fts'] = []
        
        # items - name
        try:
            cursor.execute('SELECT id, name FROM items WHERE name IS NOT NULL')
            english_data['items'] = [
                {'id': row['id'], 'name': row['name']}
                for row in cursor.fetchall()
            ]
            print(f"   ✅ Exported {len(english_data['items'])} items names")
        except sqlite3.OperationalError as e:
            print(f"   ⚠️  Error exporting items: {e}")
            english_data['items'] = []
        
        output_file = self.output_dir / 'english_only.json'
        with open(output_file, 'w', encoding='utf-8') as f:
            json.dump(english_data, f, ensure_ascii=False, indent=2)
        
        total = sum(len(v) for v in english_data.values())
        print(f"✅ Exported {total} English-only records to {output_file.name}")
        return total
    
    def export_all(self):
        """Экспортировать все данные"""
        print("=" * 60)
        print("EXPORTING DATA FOR FINE-TUNING")
        print("=" * 60)
        
        self.connect()
        
        # Проверяем доступные таблицы
        tables = self.get_all_tables()
        print(f"\n📊 Available tables: {len(tables)}")
        print(f"   {', '.join(tables[:10])}{'...' if len(tables) > 10 else ''}")
        
        print("\n" + "=" * 60)
        print("EXPORTING MULTILINGUAL DATA")
        print("=" * 60)
        
        clans_count = self.export_clans()
        settlements_count = self.export_settlements()
        lords_count = self.export_lords()
        factions_count = self.export_factions()
        
        print("\n" + "=" * 60)
        print("EXPORTING ENGLISH-ONLY DATA")
        print("=" * 60)
        
        english_count = self.export_english_only_data()
        
        print("\n" + "=" * 60)
        print("✅ EXPORT COMPLETED!")
        print("=" * 60)
        print(f"\n📊 Export statistics:")
        print(f"   Clans: {clans_count}")
        print(f"   Settlements: {settlements_count}")
        print(f"   Lords: {lords_count}")
        print(f"   Factions: {factions_count}")
        print(f"   English-only records: {english_count}")
        print(f"   Total records: {clans_count + settlements_count + lords_count + factions_count + english_count}")
        print(f"\n📁 Output directory: {self.output_dir}")
        
        self.close()


def main():
    """Main entry point"""
    project_root = Path(__file__).parent.parent
    db_path = project_root / 'Database' / 'bannerlord_lore.db'
    output_dir = project_root / 'finetuning_data'
    
    if not db_path.exists():
        print(f"❌ Database not found: {db_path}")
        return
    
    exporter = FineTuningDataExporter(db_path, output_dir)
    exporter.export_all()


if __name__ == '__main__':
    main()

