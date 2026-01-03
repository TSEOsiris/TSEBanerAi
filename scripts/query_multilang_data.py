#!/usr/bin/env python3
"""
Примеры запросов к мультиязычной базе данных
Показывает как использовать русские и английские данные
"""

import sqlite3
import sys
from pathlib import Path
import re

# Настройка кодировки для Windows
if sys.platform == 'win32':
    import io
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8')


def is_cyrillic(text):
    """Проверка, содержит ли текст кириллицу"""
    if not text:
        return False
    return bool(re.search(r'[А-Яа-яЁё]', text))


def is_turkish(text):
    """Проверка, содержит ли текст турецкие символы"""
    if not text:
        return False
    # Турецкие специфичные символы: ç, ğ, ı, ö, ş, ü
    turkish_chars = re.search(r'[çÇğĞıİöÖşŞüÜ]', text)
    turkish_words = re.search(r'\b(ve|bir|bu|şu|o|ile|için|gibi|kadar|daha|en|çok|az|var|yok|de|da|ki|mi|mı|mu|mü)\b', text, re.IGNORECASE)
    return bool(turkish_chars or turkish_words)


def example_queries(db_path: Path):
    """Примеры полезных запросов с мультиязычными данными"""
    conn = sqlite3.connect(str(db_path))
    conn.row_factory = sqlite3.Row
    cursor = conn.cursor()
    
    print("=" * 60)
    print("MULTILINGUAL DATA QUERIES")
    print("=" * 60)
    
    # 1. Получить поселение со всеми языками
    print("\n1. Settlement with EN, RU, and TR names:")
    print("-" * 60)
    cursor.execute('''
        SELECT 
            id,
            name AS name_en,
            name_ru,
            name_tr,
            settlement_type,
            owner_name AS owner_name_en,
            owner_name_ru,
            owner_name_tr
        FROM campaign_settlements
        WHERE name_ru IS NOT NULL OR name_tr IS NOT NULL
        LIMIT 5
    ''')
    for row in cursor.fetchall():
        print(f"   {row['id']}:")
        print(f"      EN: {row['name_en']} (Owner: {row['owner_name_en']})")
        if row['name_ru']:
            print(f"      RU: {row['name_ru']} (Owner: {row['owner_name_ru']})")
        if row['name_tr']:
            print(f"      TR: {row['name_tr']} (Owner: {row['owner_name_tr']})")
    
    # 2. Поиск по названию на разных языках
    print("\n2. Search by name in different languages:")
    print("-" * 60)
    
    # Поиск по русскому
    print("   Russian (Диатма):")
    cursor.execute('''
        SELECT 
            id,
            name AS name_en,
            name_ru,
            name_tr,
            settlement_type
        FROM campaign_settlements
        WHERE name_ru LIKE '%Диатма%' OR name LIKE '%Diathma%'
        LIMIT 3
    ''')
    for row in cursor.fetchall():
        name = row['name_ru'] or row['name_en'] or row['name_tr']
        print(f"      Found: {name} ({row['id']}) - {row['settlement_type']}")
    
    # Поиск по турецкому (если есть)
    print("   Turkish:")
    cursor.execute('''
        SELECT 
            id,
            name AS name_en,
            name_ru,
            name_tr,
            settlement_type
        FROM campaign_settlements
        WHERE name_tr IS NOT NULL
        LIMIT 3
    ''')
    for row in cursor.fetchall():
        if row['name_tr']:
            print(f"      Found: {row['name_tr']} ({row['id']}) - {row['settlement_type']}")
    
    # 3. Кланы с описаниями на разных языках
    print("\n3. Clans with descriptions in different languages:")
    print("-" * 60)
    
    # Русские описания
    print("   Russian descriptions:")
    cursor.execute('''
        SELECT 
            id,
            name AS name_en,
            name_ru,
            description_ru
        FROM campaign_clans
        WHERE description_ru IS NOT NULL AND description_ru != ''
        LIMIT 2
    ''')
    for row in cursor.fetchall():
        desc = row['description_ru'][:80] + '...' if len(row['description_ru']) > 80 else row['description_ru']
        print(f"      {row['name_ru']} ({row['id']}): {desc}")
    
    # Турецкие описания
    print("   Turkish descriptions:")
    cursor.execute('''
        SELECT 
            id,
            name AS name_en,
            name_tr,
            description_tr
        FROM campaign_clans
        WHERE description_tr IS NOT NULL AND description_tr != ''
        LIMIT 2
    ''')
    for row in cursor.fetchall():
        desc = row['description_tr'][:80] + '...' if len(row['description_tr']) > 80 else row['description_tr']
        print(f"      {row['name_tr']} ({row['id']}): {desc}")
    
    # 4. Герои с именами на разных языках
    print("\n4. Heroes with names in different languages:")
    print("-" * 60)
    cursor.execute('''
        SELECT 
            ch.id,
            ch.name AS name_en,
            ch.name_ru,
            ch.name_tr,
            cc.name AS clan_name_en,
            cc.name_ru AS clan_name_ru,
            cc.name_tr AS clan_name_tr
        FROM campaign_heroes ch
        LEFT JOIN campaign_clans cc ON ch.clan_id = cc.id
        WHERE ch.name_ru IS NOT NULL OR ch.name_tr IS NOT NULL
        LIMIT 5
    ''')
    for row in cursor.fetchall():
        name = row['name_ru'] or row['name_tr'] or row['name_en']
        clan = row['clan_name_ru'] or row['clan_name_tr'] or row['clan_name_en'] or 'Unknown'
        print(f"   {name} ({row['id']}) - Clan: {clan}")
    
    # 5. Универсальная функция поиска (EN или RU)
    print("\n5. Universal search function (works with EN or RU):")
    print("-" * 60)
    
    def search_settlement(query):
        """Поиск поселения на любом языке (EN, RU, TR)"""
        is_ru = is_cyrillic(query)
        is_tr = is_turkish(query)
        
        if is_ru:
            cursor.execute('''
                SELECT id, name_ru, name AS name_en, name_tr, settlement_type
                FROM campaign_settlements
                WHERE name_ru LIKE ? OR name LIKE ? OR name_tr LIKE ?
            ''', (f'%{query}%', f'%{query}%', f'%{query}%'))
        elif is_tr:
            cursor.execute('''
                SELECT id, name_tr, name AS name_en, name_ru, settlement_type
                FROM campaign_settlements
                WHERE name_tr LIKE ? OR name LIKE ? OR name_ru LIKE ?
            ''', (f'%{query}%', f'%{query}%', f'%{query}%'))
        else:
            cursor.execute('''
                SELECT id, name AS name_en, name_ru, name_tr, settlement_type
                FROM campaign_settlements
                WHERE name LIKE ? OR name_ru LIKE ? OR name_tr LIKE ?
            ''', (f'%{query}%', f'%{query}%', f'%{query}%'))
        
        return cursor.fetchall()
    
    # Тест поиска на русском
    results = search_settlement('Диатма')
    print(f"   Search 'Диатма': Found {len(results)} results")
    for row in results:
        print(f"      {row['name_ru'] or row['name_en']} ({row['id']})")
    
    # Тест поиска на английском
    results = search_settlement('Diathma')
    print(f"   Search 'Diathma': Found {len(results)} results")
    for row in results:
        print(f"      {row['name_en'] or row['name_ru']} ({row['id']})")
    
    # 6. Статистика локализации
    print("\n6. Localization statistics:")
    print("-" * 60)
    
    # Clans
    cursor.execute('SELECT COUNT(*) FROM campaign_clans')
    clans_total = cursor.fetchone()[0]
    cursor.execute('SELECT COUNT(*) FROM campaign_clans WHERE name_ru IS NOT NULL')
    clans_ru = cursor.fetchone()[0]
    cursor.execute('SELECT COUNT(*) FROM campaign_clans WHERE name_tr IS NOT NULL')
    clans_tr = cursor.fetchone()[0]
    
    # Heroes
    cursor.execute('SELECT COUNT(*) FROM campaign_heroes')
    heroes_total = cursor.fetchone()[0]
    cursor.execute('SELECT COUNT(*) FROM campaign_heroes WHERE name_ru IS NOT NULL')
    heroes_ru = cursor.fetchone()[0]
    cursor.execute('SELECT COUNT(*) FROM campaign_heroes WHERE name_tr IS NOT NULL')
    heroes_tr = cursor.fetchone()[0]
    
    # Settlements
    cursor.execute('SELECT COUNT(*) FROM campaign_settlements')
    settlements_total = cursor.fetchone()[0]
    cursor.execute('SELECT COUNT(*) FROM campaign_settlements WHERE name_ru IS NOT NULL')
    settlements_ru = cursor.fetchone()[0]
    cursor.execute('SELECT COUNT(*) FROM campaign_settlements WHERE name_tr IS NOT NULL')
    settlements_tr = cursor.fetchone()[0]
    
    print(f"   Clans:")
    print(f"      RU: {clans_ru}/{clans_total} ({clans_ru*100//clans_total if clans_total > 0 else 0}%)")
    print(f"      TR: {clans_tr}/{clans_total} ({clans_tr*100//clans_total if clans_total > 0 else 0}%)")
    print(f"   Heroes:")
    print(f"      RU: {heroes_ru}/{heroes_total} ({heroes_ru*100//heroes_total if heroes_total > 0 else 0}%)")
    print(f"      TR: {heroes_tr}/{heroes_total} ({heroes_tr*100//heroes_total if heroes_total > 0 else 0}%)")
    print(f"   Settlements:")
    print(f"      RU: {settlements_ru}/{settlements_total} ({settlements_ru*100//settlements_total if settlements_total > 0 else 0}%)")
    print(f"      TR: {settlements_tr}/{settlements_total} ({settlements_tr*100//settlements_total if settlements_total > 0 else 0}%)")
    
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

