#!/usr/bin/env python3
"""
Связывание данных кампании с данными энциклопедии
Создает связи между campaign_* таблицами и основными таблицами БД
"""

import sqlite3
import sys
from pathlib import Path

# Настройка кодировки для Windows
if sys.platform == 'win32':
    import io
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8')


def link_data(db_path: Path):
    """Связать данные кампании с энциклопедией"""
    conn = sqlite3.connect(str(db_path))
    conn.row_factory = sqlite3.Row
    cursor = conn.cursor()
    
    print("=" * 60)
    print("LINKING CAMPAIGN DATA TO ENCYCLOPEDIA")
    print("=" * 60)
    
    # 0. Добавить колонку encyclopedia_id если её нет (СНАЧАЛА!)
    print("\n0. Adding encyclopedia_id columns...")
    try:
        cursor.execute('ALTER TABLE campaign_heroes ADD COLUMN encyclopedia_id TEXT')
        print("   ✅ Added encyclopedia_id to campaign_heroes")
    except sqlite3.OperationalError:
        print("   ℹ️  encyclopedia_id already exists in campaign_heroes")
    
    try:
        cursor.execute('ALTER TABLE campaign_settlements ADD COLUMN encyclopedia_id TEXT')
        print("   ✅ Added encyclopedia_id to campaign_settlements")
    except sqlite3.OperationalError:
        print("   ℹ️  encyclopedia_id already exists in campaign_settlements")
    
    try:
        cursor.execute('ALTER TABLE campaign_kingdoms ADD COLUMN encyclopedia_id TEXT')
        print("   ✅ Added encyclopedia_id to campaign_kingdoms")
    except sqlite3.OperationalError:
        print("   ℹ️  encyclopedia_id already exists in campaign_kingdoms")
    
    conn.commit()
    
    # 1. Связать героев кампании с героями энциклопедии
    print("\n1. Linking campaign heroes to encyclopedia heroes...")
    cursor.execute('''
        UPDATE campaign_heroes
        SET encyclopedia_id = (
            SELECT id FROM heroes 
            WHERE heroes.id = campaign_heroes.id
            LIMIT 1
        )
        WHERE EXISTS (
            SELECT 1 FROM heroes 
            WHERE heroes.id = campaign_heroes.id
        )
    ''')
    linked_heroes = cursor.rowcount
    print(f"   ✅ Linked {linked_heroes} heroes")
    
    # 2. Связать поселения кампании с поселениями энциклопедии
    print("\n2. Linking campaign settlements to encyclopedia settlements...")
    cursor.execute('''
        UPDATE campaign_settlements
        SET encyclopedia_id = (
            SELECT id FROM settlements 
            WHERE settlements.id = campaign_settlements.id
            LIMIT 1
        )
        WHERE EXISTS (
            SELECT 1 FROM settlements 
            WHERE settlements.id = campaign_settlements.id
        )
    ''')
    linked_settlements = cursor.rowcount
    print(f"   ✅ Linked {linked_settlements} settlements")
    
    # 3. Связать королевства кампании с королевствами энциклопедии
    print("\n3. Linking campaign kingdoms to encyclopedia kingdoms...")
    cursor.execute('''
        UPDATE campaign_kingdoms
        SET encyclopedia_id = (
            SELECT id FROM kingdoms 
            WHERE kingdoms.id = campaign_kingdoms.id
            LIMIT 1
        )
        WHERE EXISTS (
            SELECT 1 FROM kingdoms 
            WHERE kingdoms.id = campaign_kingdoms.id
        )
    ''')
    linked_kingdoms = cursor.rowcount
    print(f"   ✅ Linked {linked_kingdoms} kingdoms")
    
    # 4. Создать индексы
    cursor.execute('CREATE INDEX IF NOT EXISTS idx_campaign_heroes_encyclopedia ON campaign_heroes(encyclopedia_id)')
    cursor.execute('CREATE INDEX IF NOT EXISTS idx_campaign_settlements_encyclopedia ON campaign_settlements(encyclopedia_id)')
    cursor.execute('CREATE INDEX IF NOT EXISTS idx_campaign_kingdoms_encyclopedia ON campaign_kingdoms(encyclopedia_id)')
    
    conn.commit()
    
    # Статистика связей
    cursor.execute('SELECT COUNT(*) FROM campaign_heroes WHERE encyclopedia_id IS NOT NULL')
    heroes_with_link = cursor.fetchone()[0]
    cursor.execute('SELECT COUNT(*) FROM campaign_settlements WHERE encyclopedia_id IS NOT NULL')
    settlements_with_link = cursor.fetchone()[0]
    cursor.execute('SELECT COUNT(*) FROM campaign_kingdoms WHERE encyclopedia_id IS NOT NULL')
    kingdoms_with_link = cursor.fetchone()[0]
    
    print("\n" + "=" * 60)
    print("✅ Linking completed!")
    print("=" * 60)
    print(f"\n📊 Link statistics:")
    print(f"   Heroes with encyclopedia link: {heroes_with_link}/{linked_heroes}")
    print(f"   Settlements with encyclopedia link: {settlements_with_link}/{linked_settlements}")
    print(f"   Kingdoms with encyclopedia link: {kingdoms_with_link}/{linked_kingdoms}")
    
    conn.close()


def main():
    """Main entry point"""
    project_root = Path(__file__).parent.parent
    db_path = project_root / 'Database' / 'bannerlord_lore.db'
    
    if not db_path.exists():
        print(f"❌ Database not found: {db_path}")
        return
    
    link_data(db_path)


if __name__ == '__main__':
    main()

