#!/usr/bin/env python3
"""
Создание SQLite БД из Ingame Encyclopedia JSON файлов

Сначала импортируем структурированные данные из игры,
потом можно дополнять данными из вики.
"""

import json
import sqlite3
from pathlib import Path
from typing import Dict, List, Any, Optional
import sys

# Настройка кодировки для Windows
if sys.platform == 'win32':
    import io
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8')


class EncyclopediaImporter:
    """Импорт данных из Ingame Encyclopedia в SQLite"""
    
    def __init__(self, db_path: Path, encyclopedia_dir: Path):
        self.db_path = db_path
        self.encyclopedia_dir = encyclopedia_dir
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
    
    def create_tables(self):
        """Создать таблицы в БД"""
        cursor = self.conn.cursor()
        
        # Удаляем старые таблицы если они есть (для пересоздания)
        cursor.execute('DROP TABLE IF EXISTS settlements_fts')
        cursor.execute('DROP TABLE IF EXISTS characters_fts')
        cursor.execute('DROP TABLE IF EXISTS factions_fts')
        cursor.execute('DROP TABLE IF EXISTS world_lore_fts')
        cursor.execute('DROP TABLE IF EXISTS settlements_lore')
        cursor.execute('DROP TABLE IF EXISTS characters_lore')
        cursor.execute('DROP TABLE IF EXISTS factions_lore')
        cursor.execute('DROP TABLE IF EXISTS clans_lore')
        cursor.execute('DROP TABLE IF EXISTS world_lore')
        cursor.execute('DROP TABLE IF EXISTS concepts')
        self.conn.commit()
        
        # Таблица: settlements_lore (поселения)
        cursor.execute('''
            CREATE TABLE IF NOT EXISTS settlements_lore (
                encyclopedia_id TEXT PRIMARY KEY,  -- ID из игры (первичный ключ)
                name TEXT NOT NULL,
                name_en TEXT,
                name_ru TEXT,
                name_tr TEXT,
                type TEXT,  -- 'town', 'castle', 'village'
                faction TEXT,
                culture TEXT,
                description_en TEXT,
                description_ru TEXT,
                description_tr TEXT,
                wiki_url TEXT,
                source TEXT DEFAULT 'encyclopedia',  -- 'encyclopedia' или 'wiki'
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            )
        ''')
        
        # Таблица: characters_lore (персонажи)
        cursor.execute('''
            CREATE TABLE IF NOT EXISTS characters_lore (
                encyclopedia_id TEXT PRIMARY KEY,  -- ID из игры (первичный ключ)
                name TEXT NOT NULL,
                name_en TEXT,
                name_ru TEXT,
                name_tr TEXT,
                clan TEXT,
                faction TEXT,
                culture TEXT,
                role TEXT,  -- 'lord', 'notable', 'companion', etc.
                description_en TEXT,
                description_ru TEXT,
                description_tr TEXT,
                biography_en TEXT,
                biography_ru TEXT,
                biography_tr TEXT,
                wiki_url TEXT,
                source TEXT DEFAULT 'encyclopedia',
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            )
        ''')
        
        # Таблица: factions_lore (фракции/королевства)
        cursor.execute('''
            CREATE TABLE IF NOT EXISTS factions_lore (
                encyclopedia_id TEXT PRIMARY KEY,  -- ID из игры (первичный ключ)
                name TEXT NOT NULL,
                name_en TEXT,
                name_ru TEXT,
                name_tr TEXT,
                short_name TEXT,
                title TEXT,
                ruler_title TEXT,
                culture TEXT,
                description_en TEXT,
                description_ru TEXT,
                description_tr TEXT,
                wiki_url TEXT,
                source TEXT DEFAULT 'encyclopedia',
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            )
        ''')
        
        # Таблица: clans_lore (кланы)
        cursor.execute('''
            CREATE TABLE IF NOT EXISTS clans_lore (
                encyclopedia_id TEXT PRIMARY KEY,  -- ID из игры (первичный ключ)
                name TEXT NOT NULL,
                name_en TEXT,
                name_ru TEXT,
                name_tr TEXT,
                faction TEXT,
                culture TEXT,
                description_en TEXT,
                description_ru TEXT,
                description_tr TEXT,
                wiki_url TEXT,
                source TEXT DEFAULT 'encyclopedia',
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            )
        ''')
        
        # Таблица: world_lore (политическая философия, концепции)
        cursor.execute('''
            CREATE TABLE IF NOT EXISTS world_lore (
                encyclopedia_id TEXT PRIMARY KEY,  -- ID из игры (первичный ключ)
                title TEXT NOT NULL,
                title_en TEXT,
                title_ru TEXT,
                title_tr TEXT,
                category TEXT,  -- 'governance', 'philosophy', 'history', 'concept'
                content_en TEXT,
                content_ru TEXT,
                content_tr TEXT,
                related_faction TEXT,
                related_concept TEXT,
                wiki_url TEXT,
                source TEXT DEFAULT 'encyclopedia',
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            )
        ''')
        
        # Таблица: concepts (игровые концепции)
        cursor.execute('''
            CREATE TABLE IF NOT EXISTS concepts (
                encyclopedia_id TEXT PRIMARY KEY,  -- ID из игры (первичный ключ)
                name TEXT NOT NULL,
                name_en TEXT,
                name_ru TEXT,
                name_tr TEXT,
                category TEXT,
                description_en TEXT,
                description_ru TEXT,
                description_tr TEXT,
                wiki_url TEXT,
                source TEXT DEFAULT 'encyclopedia',
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            )
        ''')
        
        self.conn.commit()
        print("✅ Tables created")
    
    def create_indexes(self):
        """Создать индексы для быстрого поиска"""
        cursor = self.conn.cursor()
        
        # Индексы для settlements_lore
        cursor.execute('CREATE INDEX IF NOT EXISTS idx_settlements_name ON settlements_lore(name)')
        cursor.execute('CREATE INDEX IF NOT EXISTS idx_settlements_faction ON settlements_lore(faction)')
        cursor.execute('CREATE INDEX IF NOT EXISTS idx_settlements_type ON settlements_lore(type)')
        
        # Индексы для characters_lore
        cursor.execute('CREATE INDEX IF NOT EXISTS idx_characters_name ON characters_lore(name)')
        cursor.execute('CREATE INDEX IF NOT EXISTS idx_characters_clan ON characters_lore(clan)')
        cursor.execute('CREATE INDEX IF NOT EXISTS idx_characters_faction ON characters_lore(faction)')
        
        # Индексы для factions_lore
        cursor.execute('CREATE INDEX IF NOT EXISTS idx_factions_name ON factions_lore(name)')
        
        # Индексы для clans_lore
        cursor.execute('CREATE INDEX IF NOT EXISTS idx_clans_name ON clans_lore(name)')
        cursor.execute('CREATE INDEX IF NOT EXISTS idx_clans_faction ON clans_lore(faction)')
        
        # Индексы для world_lore
        cursor.execute('CREATE INDEX IF NOT EXISTS idx_world_lore_category ON world_lore(category)')
        cursor.execute('CREATE INDEX IF NOT EXISTS idx_world_lore_faction ON world_lore(related_faction)')
        
        # Индексы для concepts
        cursor.execute('CREATE INDEX IF NOT EXISTS idx_concepts_name ON concepts(name)')
        cursor.execute('CREATE INDEX IF NOT EXISTS idx_concepts_category ON concepts(category)')
        
        self.conn.commit()
        print("✅ Indexes created")
    
    def create_fts_tables(self):
        """Создать FTS5 таблицы для полнотекстового поиска"""
        cursor = self.conn.cursor()
        
        # FTS для settlements (только EN поля)
        cursor.execute('''
            CREATE VIRTUAL TABLE IF NOT EXISTS settlements_fts USING fts5(
                name, name_en, description_en,
                content='settlements_lore',
                content_rowid='rowid'
            )
        ''')
        
        # FTS для characters (только EN поля)
        cursor.execute('''
            CREATE VIRTUAL TABLE IF NOT EXISTS characters_fts USING fts5(
                name, name_en, description_en, biography_en,
                content='characters_lore',
                content_rowid='rowid'
            )
        ''')
        
        # FTS для factions (только EN поля)
        cursor.execute('''
            CREATE VIRTUAL TABLE IF NOT EXISTS factions_fts USING fts5(
                name, name_en, description_en,
                content='factions_lore',
                content_rowid='rowid'
            )
        ''')
        
        # FTS для world_lore (только EN поля)
        cursor.execute('''
            CREATE VIRTUAL TABLE IF NOT EXISTS world_lore_fts USING fts5(
                title, title_en, content_en,
                content='world_lore',
                content_rowid='rowid'
            )
        ''')
        
        self.conn.commit()
        print("✅ FTS tables created")
    
    def import_settlements(self, language: str = 'EN'):
        """Импортировать поселения из settlements.json"""
        json_file = self.encyclopedia_dir / language / 'settlements.json'
        
        if not json_file.exists():
            print(f"⚠️  File not found: {json_file}")
            return 0
        
        with open(json_file, 'r', encoding='utf-8') as f:
            data = json.load(f)
        
        cursor = self.conn.cursor()
        count = 0
        
        for item in data:
            # Определяем тип поселения
            settlement_type = None
            if 'town' in item.get('id', '').lower():
                settlement_type = 'town'
            elif 'castle' in item.get('id', '').lower():
                settlement_type = 'castle'
            elif 'village' in item.get('id', '').lower():
                settlement_type = 'village'
            
            # Извлекаем название
            name = item.get('name', '')
            if name:
                # Убираем дублирование (например "OmorOmor" -> "Omor")
                if len(name) > 0 and name[:len(name)//2] == name[len(name)//2:]:
                    name = name[:len(name)//2]
            
            # Извлекаем текст описания
            text = item.get('text', '')
            if text:
                # Убираем дублирование
                if len(text) > 0 and text[:len(text)//2] == text[len(text)//2:]:
                    text = text[:len(text)//2]
            
            encyclopedia_id = item.get('id', '')
            
            if language == 'EN':
                # Для EN используем INSERT OR REPLACE
                cursor.execute('''
                    INSERT OR REPLACE INTO settlements_lore 
                    (encyclopedia_id, name, name_en, type, description_en, source, updated_at)
                    VALUES (?, ?, ?, ?, ?, 'encyclopedia', CURRENT_TIMESTAMP)
                ''', (encyclopedia_id, name, name, settlement_type, text))
            elif language == 'RU':
                cursor.execute('''
                    UPDATE settlements_lore 
                    SET name_ru = ?, description_ru = ?, updated_at = CURRENT_TIMESTAMP
                    WHERE encyclopedia_id = ?
                ''', (name, text, encyclopedia_id))
            elif language == 'TR':
                cursor.execute('''
                    UPDATE settlements_lore 
                    SET name_tr = ?, description_tr = ?, updated_at = CURRENT_TIMESTAMP
                    WHERE encyclopedia_id = ?
                ''', (name, text, encyclopedia_id))
            
            count += 1
        
        self.conn.commit()
        print(f"✅ Imported {count} settlements from {language}")
        return count
    
    def import_factions(self, language: str = 'EN'):
        """Импортировать фракции из kingdoms.json"""
        json_file = self.encyclopedia_dir / language / 'kingdoms.json'
        
        if not json_file.exists():
            print(f"⚠️  File not found: {json_file}")
            return 0
        
        with open(json_file, 'r', encoding='utf-8') as f:
            data = json.load(f)
        
        cursor = self.conn.cursor()
        count = 0
        
        for item in data:
            name = item.get('name', '')
            if name:
                # Убираем дублирование
                if len(name) > 0 and name[:len(name)//2] == name[len(name)//2:]:
                    name = name[:len(name)//2]
            
            text = item.get('text', '')
            if text:
                # Убираем дублирование
                if len(text) > 0 and text[:len(text)//2] == text[len(text)//2:]:
                    text = text[:len(text)//2]
            
            encyclopedia_id = item.get('id', '')
            
            # Используем INSERT OR REPLACE для простоты (только EN)
            cursor.execute('''
                INSERT OR REPLACE INTO factions_lore 
                (encyclopedia_id, name, name_en, short_name, title, ruler_title, culture, 
                 description_en, source, updated_at)
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, 'encyclopedia', CURRENT_TIMESTAMP)
            ''', (encyclopedia_id, name, name, item.get('short_name', ''), item.get('title', ''),
                  item.get('ruler_title', ''), item.get('culture', ''), text))
            
            count += 1
        
        self.conn.commit()
        print(f"✅ Imported {count} factions from {language}")
        return count
    
    def import_world_lore(self, language: str = 'EN'):
        """Импортировать world_lore из world_lore.json"""
        json_file = self.encyclopedia_dir / language / 'world_lore.json'
        
        if not json_file.exists():
            print(f"⚠️  File not found: {json_file}")
            return 0
        
        with open(json_file, 'r', encoding='utf-8') as f:
            data = json.load(f)
        
        cursor = self.conn.cursor()
        count = 0
        
        for item in data:
            title = item.get('title', '')
            if title:
                # Убираем дублирование
                if len(title) > 0 and title[:len(title)//2] == title[len(title)//2:]:
                    title = title[:len(title)//2]
            
            text = item.get('text', '')
            if text:
                # Убираем дублирование
                if len(text) > 0 and text[:len(text)//2] == text[len(text)//2:]:
                    text = text[:len(text)//2]
            
            # Определяем категорию и связанную фракцию
            category = 'philosophy'  # По умолчанию
            related_faction = None
            
            # Пытаемся определить фракцию из ID или текста
            lore_id = item.get('id', '')
            if 'northern' in lore_id.lower() or 'northern' in text.lower():
                related_faction = 'Northern Empire'
            elif 'southern' in lore_id.lower() or 'southern' in text.lower():
                related_faction = 'Southern Empire'
            elif 'western' in lore_id.lower() or 'western' in text.lower():
                related_faction = 'Western Empire'
            elif 'aserai' in lore_id.lower() or 'aserai' in text.lower():
                related_faction = 'Aserai'
            elif 'battania' in lore_id.lower() or 'battania' in text.lower():
                related_faction = 'Battania'
            elif 'khuzait' in lore_id.lower() or 'khuzait' in text.lower():
                related_faction = 'Khuzait'
            elif 'sturgia' in lore_id.lower() or 'sturgia' in text.lower():
                related_faction = 'Sturgia'
            elif 'vlandia' in lore_id.lower() or 'vlandia' in text.lower():
                related_faction = 'Vlandia'
            
            encyclopedia_id = item.get('id', '')
            
            # Используем INSERT OR REPLACE для простоты (только EN)
            cursor.execute('''
                INSERT OR REPLACE INTO world_lore 
                (encyclopedia_id, title, title_en, category, content_en, related_faction, 
                 source, updated_at)
                VALUES (?, ?, ?, ?, ?, ?, 'encyclopedia', CURRENT_TIMESTAMP)
            ''', (encyclopedia_id, title, title, category, text, related_faction))
            
            count += 1
        
        self.conn.commit()
        print(f"✅ Imported {count} world_lore entries from {language}")
        return count
    
    def import_concepts(self, language: str = 'EN'):
        """Импортировать концепции из concepts.json"""
        json_file = self.encyclopedia_dir / language / 'concepts.json'
        
        if not json_file.exists():
            print(f"⚠️  File not found: {json_file}")
            return 0
        
        with open(json_file, 'r', encoding='utf-8') as f:
            data = json.load(f)
        
        cursor = self.conn.cursor()
        count = 0
        
        for item in data:
            name = item.get('name', '')
            if name:
                # Убираем дублирование
                if len(name) > 0 and name[:len(name)//2] == name[len(name)//2:]:
                    name = name[:len(name)//2]
            
            text = item.get('text', '')
            if text:
                # Убираем дублирование
                if len(text) > 0 and text[:len(text)//2] == text[len(text)//2:]:
                    text = text[:len(text)//2]
            
            encyclopedia_id = item.get('id', '')
            
            # Используем INSERT OR REPLACE для простоты (только EN)
            cursor.execute('''
                INSERT OR REPLACE INTO concepts 
                (encyclopedia_id, name, name_en, category, description_en, source, updated_at)
                VALUES (?, ?, ?, ?, ?, 'encyclopedia', CURRENT_TIMESTAMP)
            ''', (encyclopedia_id, name, name, item.get('category', ''), text))
            
            count += 1
        
        self.conn.commit()
        print(f"✅ Imported {count} concepts from {language}")
        return count
    
    def import_characters(self, language: str = 'EN'):
        """Импортировать персонажей из npc_characters.json или heroes.json"""
        # Пробуем npc_characters.json
        json_file = self.encyclopedia_dir / language / 'npc_characters.json'
        if not json_file.exists():
            json_file = self.encyclopedia_dir / language / 'heroes.json'
        
        if not json_file.exists():
            print(f"⚠️  File not found: {json_file}")
            return 0
        
        with open(json_file, 'r', encoding='utf-8') as f:
            data = json.load(f)
        
        cursor = self.conn.cursor()
        count = 0
        
        for item in data:
            name = item.get('name', '')
            if name:
                # Убираем дублирование
                if len(name) > 0 and name[:len(name)//2] == name[len(name)//2:]:
                    name = name[:len(name)//2]
            
            text = item.get('text', '')
            if text:
                # Убираем дублирование
                if len(text) > 0 and text[:len(text)//2] == text[len(text)//2:]:
                    text = text[:len(text)//2]
            
            encyclopedia_id = item.get('id', '')
            
            # Используем INSERT OR REPLACE для простоты (только EN)
            cursor.execute('''
                INSERT OR REPLACE INTO characters_lore 
                (encyclopedia_id, name, name_en, description_en, source, updated_at)
                VALUES (?, ?, ?, ?, 'encyclopedia', CURRENT_TIMESTAMP)
            ''', (encyclopedia_id, name, name, text))
            
            count += 1
        
        self.conn.commit()
        print(f"✅ Imported {count} characters from {language}")
        return count
    
    def populate_fts(self):
        """Заполнить FTS таблицы"""
        cursor = self.conn.cursor()
        
        # Заполняем FTS для settlements (только EN поля)
        cursor.execute('''
            INSERT INTO settlements_fts(rowid, name, name_en, description_en)
            SELECT rowid, name, name_en, description_en
            FROM settlements_lore
        ''')
        
        # Заполняем FTS для characters (только EN поля)
        cursor.execute('''
            INSERT INTO characters_fts(rowid, name, name_en, description_en, biography_en)
            SELECT rowid, name, name_en, description_en, biography_en
            FROM characters_lore
        ''')
        
        # Заполняем FTS для factions (только EN поля)
        cursor.execute('''
            INSERT INTO factions_fts(rowid, name, name_en, description_en)
            SELECT rowid, name, name_en, description_en
            FROM factions_lore
        ''')
        
        # Заполняем FTS для world_lore (только EN поля)
        cursor.execute('''
            INSERT INTO world_lore_fts(rowid, title, title_en, content_en)
            SELECT rowid, title, title_en, content_en
            FROM world_lore
        ''')
        
        self.conn.commit()
        print("✅ FTS tables populated")
    
    def import_all(self, languages: List[str] = ['EN']):
        """Импортировать все данные (только английский язык)"""
        print("=" * 60)
        print("Creating database from Ingame Encyclopedia (EN only)")
        print("=" * 60)
        
        self.connect()
        self.create_tables()
        self.create_indexes()
        self.create_fts_tables()
        
        print("\n--- Importing data (EN only) ---")
        
        # Импортируем только EN
        language = 'EN'
        print(f"\n[{language}]")
        self.import_settlements(language)
        self.conn.commit()
        self.import_factions(language)
        self.conn.commit()
        self.import_world_lore(language)
        self.conn.commit()
        self.import_concepts(language)
        self.conn.commit()
        self.import_characters(language)
        self.conn.commit()
        
        print("\n--- Populating FTS tables ---")
        self.populate_fts()
        
        # Статистика
        cursor = self.conn.cursor()
        cursor.execute('SELECT COUNT(*) FROM settlements_lore')
        settlements_count = cursor.fetchone()[0]
        cursor.execute('SELECT COUNT(*) FROM factions_lore')
        factions_count = cursor.fetchone()[0]
        cursor.execute('SELECT COUNT(*) FROM characters_lore')
        characters_count = cursor.fetchone()[0]
        cursor.execute('SELECT COUNT(*) FROM world_lore')
        world_lore_count = cursor.fetchone()[0]
        cursor.execute('SELECT COUNT(*) FROM concepts')
        concepts_count = cursor.fetchone()[0]
        
        print("\n" + "=" * 60)
        print("✅ Database created successfully!")
        print("=" * 60)
        print(f"Settlements: {settlements_count}")
        print(f"Factions: {factions_count}")
        print(f"Characters: {characters_count}")
        print(f"World Lore: {world_lore_count}")
        print(f"Concepts: {concepts_count}")
        print(f"\nDatabase saved to: {self.db_path}")
        
        self.close()


if __name__ == '__main__':
    import argparse
    
    parser = argparse.ArgumentParser(
        description='Create SQLite database from Ingame Encyclopedia JSON files'
    )
    parser.add_argument(
        '--encyclopedia-dir',
        type=str,
        default='Database/Ingame Encyclopedia',
        help='Path to Ingame Encyclopedia directory'
    )
    parser.add_argument(
        '--db-path',
        type=str,
        default='Database/bannerlord_lore.db',
        help='Path to output SQLite database'
    )
    parser.add_argument(
        '--languages',
        nargs='+',
        default=['EN'],
        help='Languages to import (default: EN only)'
    )
    
    args = parser.parse_args()
    
    encyclopedia_dir = Path(args.encyclopedia_dir)
    db_path = Path(args.db_path)
    
    if not encyclopedia_dir.exists():
        print(f"ERROR: Encyclopedia directory does not exist: {encyclopedia_dir}")
        exit(1)
    
    # Создаём директорию для БД если нужно
    db_path.parent.mkdir(parents=True, exist_ok=True)
    
    importer = EncyclopediaImporter(db_path, encyclopedia_dir)
    importer.import_all(args.languages)

