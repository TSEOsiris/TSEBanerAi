#!/usr/bin/env python3
"""
Примеры запросов к объединенной базе данных
Показывает как использовать данные кампании вместе с данными энциклопедии
"""

import sqlite3
import sys
from pathlib import Path

# Настройка кодировки для Windows
if sys.platform == 'win32':
    import io
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8')


def example_queries(db_path: Path):
    """Примеры полезных запросов"""
    conn = sqlite3.connect(str(db_path))
    conn.row_factory = sqlite3.Row
    cursor = conn.cursor()
    
    print("=" * 60)
    print("EXAMPLE QUERIES")
    print("=" * 60)
    
    # 1. Получить героя с данными из кампании и энциклопедии
    print("\n1. Hero with campaign and encyclopedia data:")
    print("-" * 60)
    cursor.execute('''
        SELECT 
            ch.id,
            ch.name AS campaign_name,
            ch.age,
            ch.clan_id,
            cc.name AS clan_name,
            ch.skills_json,
            h.text AS encyclopedia_text,
            h.wiki_biography
        FROM campaign_heroes ch
        LEFT JOIN campaign_clans cc ON ch.clan_id = cc.id
        LEFT JOIN heroes h ON ch.encyclopedia_id = h.id
        WHERE ch.id = 'main_hero'
        LIMIT 1
    ''')
    row = cursor.fetchone()
    if row:
        print(f"   ID: {row['id']}")
        print(f"   Campaign Name: {row['campaign_name']}")
        print(f"   Encyclopedia Text: {row['encyclopedia_text'][:50] if row['encyclopedia_text'] else 'N/A'}...")
        print(f"   Age: {row['age']}")
        print(f"   Clan: {row['clan_name']}")
    
    # 2. Поселения с владельцами
    print("\n2. Settlements with owners:")
    print("-" * 60)
    cursor.execute('''
        SELECT 
            cs.id,
            cs.name,
            cs.settlement_type,
            cs.owner_name,
            cc.name AS clan_name,
            cs.prosperity,
            cs.loyalty
        FROM campaign_settlements cs
        LEFT JOIN campaign_clans cc ON cs.owner_clan_id = cc.id
        WHERE cs.settlement_type = 'town'
        ORDER BY cs.name
        LIMIT 5
    ''')
    for row in cursor.fetchall():
        print(f"   {row['name']} ({row['settlement_type']}) - Owner: {row['owner_name']} ({row['clan_name']})")
    
    # 3. Кланы с количеством членов
    print("\n3. Clans with member count:")
    print("-" * 60)
    cursor.execute('''
        SELECT 
            cc.id,
            cc.name,
            cc.kingdom,
            COUNT(ch.id) AS member_count
        FROM campaign_clans cc
        LEFT JOIN campaign_heroes ch ON ch.clan_id = cc.id
        WHERE cc.is_noble = 1
        GROUP BY cc.id
        ORDER BY member_count DESC
        LIMIT 5
    ''')
    for row in cursor.fetchall():
        print(f"   {row['name']} ({row['kingdom']}): {row['member_count']} members")
    
    # 4. Королевства с политиками
    print("\n4. Kingdoms with policies:")
    print("-" * 60)
    cursor.execute('''
        SELECT 
            ck.id,
            ck.name,
            ck.policies_json
        FROM campaign_kingdoms ck
        LIMIT 3
    ''')
    for row in cursor.fetchall():
        import json
        policies = json.loads(row['policies_json'] or '[]')
        print(f"   {row['name']}: {len(policies)} policies")
        if policies:
            print(f"      {', '.join(policies[:3])}...")
    
    conn.close()


def main():
    """Main entry point"""
    project_root = Path(__file__).parent.parent
    db_path = project_root / 'Database' / 'bannerlord_lore.db'
    
    if not db_path.exists():
        print(f"❌ Database not found: {db_path}")
        return
    
    example_queries(db_path)


if __name__ == '__main__':
    main()

