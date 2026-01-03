#!/usr/bin/env python3
"""
Использование готовой БД encyclopedia.db из игры
и дополнение её данными из вики
"""

import sqlite3
import sys
from pathlib import Path

# Настройка кодировки для Windows
if sys.platform == 'win32':
    import io
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8')


class EncyclopediaDBManager:
    """Работа с готовой БД encyclopedia.db"""
    
    def __init__(self, db_path: Path):
        self.db_path = db_path
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
    
    def add_wiki_columns(self):
        """Добавить колонки для данных из вики (если их нет)"""
        cursor = self.conn.cursor()
        
        # Для settlements
        try:
            cursor.execute('ALTER TABLE settlements ADD COLUMN wiki_url TEXT')
        except sqlite3.OperationalError:
            pass  # Колонка уже существует
        
        try:
            cursor.execute('ALTER TABLE settlements ADD COLUMN wiki_description TEXT')
        except sqlite3.OperationalError:
            pass
        
        # Для kingdoms
        try:
            cursor.execute('ALTER TABLE kingdoms ADD COLUMN wiki_url TEXT')
        except sqlite3.OperationalError:
            pass
        
        try:
            cursor.execute('ALTER TABLE kingdoms ADD COLUMN wiki_description TEXT')
        except sqlite3.OperationalError:
            pass
        
        # Для heroes
        try:
            cursor.execute('ALTER TABLE heroes ADD COLUMN wiki_url TEXT')
        except sqlite3.OperationalError:
            pass
        
        try:
            cursor.execute('ALTER TABLE heroes ADD COLUMN wiki_biography TEXT')
        except sqlite3.OperationalError:
            pass
        
        # Для npc_characters
        try:
            cursor.execute('ALTER TABLE npc_characters ADD COLUMN wiki_url TEXT')
        except sqlite3.OperationalError:
            pass
        
        try:
            cursor.execute('ALTER TABLE npc_characters ADD COLUMN wiki_biography TEXT')
        except sqlite3.OperationalError:
            pass
        
        # Для world_lore
        try:
            cursor.execute('ALTER TABLE world_lore ADD COLUMN wiki_url TEXT')
        except sqlite3.OperationalError:
            pass
        
        try:
            cursor.execute('ALTER TABLE world_lore ADD COLUMN wiki_title TEXT')
        except sqlite3.OperationalError:
            pass
        
        self.conn.commit()
        print("✅ Wiki columns added (if needed)")
    
    def create_indexes(self):
        """Создать индексы для быстрого поиска"""
        cursor = self.conn.cursor()
        
        # Индексы для settlements
        cursor.execute('CREATE INDEX IF NOT EXISTS idx_settlements_name ON settlements(name)')
        cursor.execute('CREATE INDEX IF NOT EXISTS idx_settlements_type ON settlements(type)')
        
        # Индексы для kingdoms
        cursor.execute('CREATE INDEX IF NOT EXISTS idx_kingdoms_name ON kingdoms(name)')
        
        # Индексы для heroes
        cursor.execute('CREATE INDEX IF NOT EXISTS idx_heroes_faction ON heroes(faction)')
        
        # Индексы для npc_characters
        cursor.execute('CREATE INDEX IF NOT EXISTS idx_npc_name ON npc_characters(name)')
        cursor.execute('CREATE INDEX IF NOT EXISTS idx_npc_culture ON npc_characters(culture)')
        
        self.conn.commit()
        print("✅ Indexes created")
    
    def create_fts_tables(self):
        """Создать FTS5 таблицы для полнотекстового поиска"""
        cursor = self.conn.cursor()
        
        # FTS для settlements
        cursor.execute('''
            CREATE VIRTUAL TABLE IF NOT EXISTS settlements_fts USING fts5(
                name, text, wiki_description,
                content='settlements',
                content_rowid='rowid'
            )
        ''')
        
        # FTS для kingdoms
        cursor.execute('''
            CREATE VIRTUAL TABLE IF NOT EXISTS kingdoms_fts USING fts5(
                name, text, wiki_description,
                content='kingdoms',
                content_rowid='rowid'
            )
        ''')
        
        # FTS для heroes
        cursor.execute('''
            CREATE VIRTUAL TABLE IF NOT EXISTS heroes_fts USING fts5(
                id, wiki_biography,
                content='heroes',
                content_rowid='rowid'
            )
        ''')
        
        # FTS для npc_characters
        cursor.execute('''
            CREATE VIRTUAL TABLE IF NOT EXISTS npc_characters_fts USING fts5(
                name, wiki_biography,
                content='npc_characters',
                content_rowid='rowid'
            )
        ''')
        
        # FTS для world_lore
        cursor.execute('''
            CREATE VIRTUAL TABLE IF NOT EXISTS world_lore_fts USING fts5(
                text, wiki_title,
                content='world_lore',
                content_rowid='rowid'
            )
        ''')
        
        # FTS для clans
        cursor.execute('''
            CREATE VIRTUAL TABLE IF NOT EXISTS clans_fts USING fts5(
                name, text, wiki_description,
                content='clans',
                content_rowid='rowid'
            )
        ''')
        
        self.conn.commit()
        print("✅ FTS tables created")
    
    def populate_fts(self):
        """Заполнить FTS таблицы"""
        cursor = self.conn.cursor()
        
        # Удаляем и пересоздаём FTS таблицы
        cursor.execute('DROP TABLE IF EXISTS settlements_fts')
        cursor.execute('DROP TABLE IF EXISTS kingdoms_fts')
        cursor.execute('DROP TABLE IF EXISTS heroes_fts')
        cursor.execute('DROP TABLE IF EXISTS npc_characters_fts')
        cursor.execute('DROP TABLE IF EXISTS world_lore_fts')
        cursor.execute('DROP TABLE IF EXISTS clans_fts')
        
        # Пересоздаём FTS таблицы
        self.create_fts_tables()
        
        # Заполняем FTS для settlements
        try:
            cursor.execute('''
                INSERT INTO settlements_fts(rowid, name, text, wiki_description)
                SELECT rowid, name, text, COALESCE(wiki_description, '')
                FROM settlements
            ''')
        except Exception as e:
            print(f"⚠️  Warning: Could not populate settlements_fts: {e}")
        
        # Заполняем FTS для kingdoms
        try:
            cursor.execute('''
                INSERT INTO kingdoms_fts(rowid, name, text, wiki_description)
                SELECT rowid, name, text, COALESCE(wiki_description, '')
                FROM kingdoms
            ''')
        except Exception as e:
            print(f"⚠️  Warning: Could not populate kingdoms_fts: {e}")
        
        # Заполняем FTS для heroes
        try:
            cursor.execute('''
                INSERT INTO heroes_fts(rowid, id, wiki_biography)
                SELECT rowid, id, COALESCE(wiki_biography, '')
                FROM heroes
            ''')
        except Exception as e:
            print(f"⚠️  Warning: Could not populate heroes_fts: {e}")
        
        # Заполняем FTS для npc_characters
        try:
            cursor.execute('''
                INSERT INTO npc_characters_fts(rowid, name, wiki_biography)
                SELECT rowid, name, COALESCE(wiki_biography, '')
                FROM npc_characters
            ''')
        except Exception as e:
            print(f"⚠️  Warning: Could not populate npc_characters_fts: {e}")
        
        # Заполняем FTS для world_lore
        try:
            cursor.execute('''
                INSERT INTO world_lore_fts(rowid, text, wiki_title)
                SELECT rowid, text, COALESCE(wiki_title, '')
                FROM world_lore
            ''')
        except Exception as e:
            print(f"⚠️  Warning: Could not populate world_lore_fts: {e}")
        
        # Заполняем FTS для clans
        try:
            cursor.execute('''
                INSERT INTO clans_fts(rowid, name, text, wiki_description)
                SELECT rowid, name, text, COALESCE(wiki_description, '')
                FROM clans
            ''')
        except Exception as e:
            print(f"⚠️  Warning: Could not populate clans_fts: {e}")
        
        self.conn.commit()
        print("✅ FTS tables populated")
    
    def get_statistics(self):
        """Получить статистику БД"""
        cursor = self.conn.cursor()
        
        stats = {}
        
        cursor.execute('SELECT COUNT(*) FROM settlements')
        stats['settlements'] = cursor.fetchone()[0]
        
        cursor.execute('SELECT COUNT(*) FROM kingdoms')
        stats['kingdoms'] = cursor.fetchone()[0]
        
        cursor.execute('SELECT COUNT(*) FROM heroes')
        stats['heroes'] = cursor.fetchone()[0]
        
        cursor.execute('SELECT COUNT(*) FROM npc_characters')
        stats['npc_characters'] = cursor.fetchone()[0]
        
        cursor.execute('SELECT COUNT(*) FROM world_lore')
        stats['world_lore'] = cursor.fetchone()[0]
        
        cursor.execute('SELECT COUNT(*) FROM concepts')
        stats['concepts'] = cursor.fetchone()[0]
        
        cursor.execute('SELECT COUNT(*) FROM clans')
        stats['clans'] = cursor.fetchone()[0]
        
        # Проверяем, сколько записей имеют данные из вики
        cursor.execute('SELECT COUNT(*) FROM settlements WHERE wiki_url IS NOT NULL')
        stats['settlements_with_wiki'] = cursor.fetchone()[0]
        
        cursor.execute('SELECT COUNT(*) FROM kingdoms WHERE wiki_url IS NOT NULL')
        stats['kingdoms_with_wiki'] = cursor.fetchone()[0]
        
        cursor.execute('SELECT COUNT(*) FROM heroes WHERE wiki_url IS NOT NULL')
        stats['heroes_with_wiki'] = cursor.fetchone()[0]
        
        cursor.execute('SELECT COUNT(*) FROM clans WHERE wiki_url IS NOT NULL')
        stats['clans_with_wiki'] = cursor.fetchone()[0]
        
        return stats
    
    def setup(self):
        """Настроить БД (добавить колонки, индексы, FTS)"""
        print("=" * 60)
        print("Setting up encyclopedia.db for wiki data")
        print("=" * 60)
        
        self.connect()
        self.add_wiki_columns()
        self.create_indexes()
        self.create_fts_tables()
        self.populate_fts()
        
        stats = self.get_statistics()
        print("\n" + "=" * 60)
        print("Database Statistics:")
        print("=" * 60)
        print(f"Settlements: {stats['settlements']} (with wiki: {stats['settlements_with_wiki']})")
        print(f"Kingdoms: {stats['kingdoms']} (with wiki: {stats['kingdoms_with_wiki']})")
        print(f"Heroes: {stats['heroes']} (with wiki: {stats['heroes_with_wiki']})")
        print(f"NPC Characters: {stats['npc_characters']}")
        print(f"Clans: {stats['clans']} (with wiki: {stats['clans_with_wiki']})")
        print(f"World Lore: {stats['world_lore']}")
        print(f"Concepts: {stats['concepts']}")
        print(f"\nDatabase: {self.db_path}")
        
        self.close()


if __name__ == '__main__':
    import argparse
    
    parser = argparse.ArgumentParser(
        description='Setup encyclopedia.db for wiki data integration'
    )
    parser.add_argument(
        '--db-path',
        type=str,
        default='Database/bannerlord_lore.db',
        help='Path to database (default: copy of encyclopedia.db)'
    )
    
    args = parser.parse_args()
    
    db_path = Path(args.db_path)
    
    if not db_path.exists():
        print(f"ERROR: Database not found: {db_path}")
        exit(1)
    
    manager = EncyclopediaDBManager(db_path)
    manager.setup()

