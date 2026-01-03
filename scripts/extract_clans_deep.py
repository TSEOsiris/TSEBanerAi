#!/usr/bin/env python3
"""
Глубокое извлечение данных о кланах из игровых файлов
Попытка найти реальные имена кланов в текстах
"""

import json
import sqlite3
import re
from pathlib import Path
from collections import defaultdict
import sys

# Настройка кодировки для Windows
if sys.platform == 'win32':
    import io
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8')


def extract_clan_names_from_texts():
    """Извлечь реальные имена кланов из текстов heroes.json"""
    heroes_path = Path('Database/Ingame Encyclopedia/EN/heroes.json')
    
    with open(heroes_path, 'r', encoding='utf-8') as f:
        heroes = json.load(f)
    
    # Паттерны для поиска имён кланов
    clan_patterns = [
        r'of the (\w+(?:\s+\w+)?)',  # "of the Kuloving", "of the Banu Sarran"
        r'(\w+(?:\s+\w+)?)\s+clan',  # "Khergit clan", "Arkit clan"
        r'clan\s+(\w+(?:\s+\w+)?)',  # "clan of the Banu Qild"
        r'(\w+(?:\s+\w+)?)\s+noble\s+houses?',  # "Sturgian noble houses"
        r'Banu\s+(\w+)',  # "Banu Sarran", "Banu Qild"
        r'fen\s+(\w+)',  # Battanian clans
    ]
    
    clan_mentions = defaultdict(list)
    
    for hero in heroes:
        faction = hero.get('faction', '')
        text = hero.get('text', '')
        
        if not text or not faction or 'clan' not in faction:
            continue
        
        # Извлекаем ID клана из faction
        clan_match = re.search(r'clan_([^_]+(?:_[^_]+)*)', faction)
        if not clan_match:
            continue
        
        clan_id = clan_match.group(1)
        
        # Ищем упоминания кланов в тексте
        for pattern in clan_patterns:
            matches = re.findall(pattern, text, re.IGNORECASE)
            for match in matches:
                if isinstance(match, tuple):
                    match = match[0] if match else ''
                if match and len(match) > 2:
                    clan_mentions[clan_id].append(match.strip())
    
    return clan_mentions


def extract_clan_info_from_db(db_path: Path):
    """Извлечь дополнительную информацию о кланах из БД"""
    conn = sqlite3.connect(str(db_path))
    conn.row_factory = sqlite3.Row
    cursor = conn.cursor()
    
    clans_info = {}
    
    # Получаем всех героев с их кланами
    cursor.execute('''
        SELECT h.id, h.faction, h.text, nc.name, nc.culture
        FROM heroes h
        JOIN npc_characters nc ON h.id = nc.id
        WHERE h.faction LIKE '%clan%'
        ORDER BY h.faction
    ''')
    
    heroes = cursor.fetchall()
    
    for hero in heroes:
        faction = hero['faction']
        clan_match = re.search(r'clan_([^_]+(?:_[^_]+)*)', faction)
        if not clan_match:
            continue
        
        clan_id = clan_match.group(1)
        
        if clan_id not in clans_info:
            clans_info[clan_id] = {
                'id': clan_id,
                'heroes': [],
                'cultures': set(),
                'texts': []
            }
        
        clans_info[clan_id]['heroes'].append({
            'id': hero['id'],
            'name': hero['name'],
            'culture': hero['culture']
        })
        
        if hero['culture']:
            clans_info[clan_id]['cultures'].add(hero['culture'])
        
        if hero['text']:
            clans_info[clan_id]['texts'].append(hero['text'])
    
    # Определяем лидера клана (первый герой или тот, у кого есть текст)
    for clan_id, info in clans_info.items():
        # Ищем героя с текстом (обычно это лидер)
        leader = None
        for hero in info['heroes']:
            for text in info['texts']:
                if hero['name'].split(hero['name'])[0] in text:  # Упрощённая проверка
                    leader = hero
                    break
            if leader:
                break
        
        if not leader and info['heroes']:
            leader = info['heroes'][0]
        
        info['leader'] = leader
        info['culture'] = list(info['cultures'])[0] if info['cultures'] else None
    
    conn.close()
    return clans_info


def extract_settlement_info_for_clans(db_path: Path):
    """Извлечь информацию о поселениях кланов"""
    conn = sqlite3.connect(str(db_path))
    conn.row_factory = sqlite3.Row
    cursor = conn.cursor()
    
    clan_settlements = defaultdict(list)
    
    cursor.execute('''
        SELECT owner, name, type, culture
        FROM settlements
        WHERE owner LIKE '%clan%'
    ''')
    
    settlements = cursor.fetchall()
    
    for settlement in settlements:
        owner = settlement['owner']
        clan_match = re.search(r'clan_([^_]+(?:_[^_]+)*)', owner)
        if not clan_match:
            continue
        
        clan_id = clan_match.group(1)
        clan_settlements[clan_id].append({
            'name': settlement['name'],
            'type': settlement['type'],
            'culture': settlement['culture']
        })
    
    conn.close()
    return clan_settlements


def update_clans_in_db(clans_info, clan_mentions, clan_settlements, db_path: Path):
    """Обновить данные о кланах в БД"""
    conn = sqlite3.connect(str(db_path))
    cursor = conn.cursor()
    
    # Добавляем колонки, если их нет
    try:
        cursor.execute('ALTER TABLE clans ADD COLUMN leader_id TEXT')
    except sqlite3.OperationalError:
        pass
    
    try:
        cursor.execute('ALTER TABLE clans ADD COLUMN leader_name TEXT')
    except sqlite3.OperationalError:
        pass
    
    updated = 0
    
    for clan_id, info in clans_info.items():
        # Определяем имя клана
        clan_name = None
        
        # Пробуем найти реальное имя из текстов
        if clan_id in clan_mentions:
            mentions = clan_mentions[clan_id]
            # Берём самое частое упоминание
            from collections import Counter
            most_common = Counter(mentions).most_common(1)
            if most_common:
                clan_name = most_common[0][0]
        
        # Если не нашли, используем имя лидера или ID
        if not clan_name:
            if info.get('leader'):
                # Пробуем извлечь фамилию или имя клана из имени лидера
                leader_name = info['leader']['name']
                # Убираем дубликаты имён (например, "LuconLucon" -> "Lucon")
                if len(leader_name) > 0 and leader_name[:len(leader_name)//2] == leader_name[len(leader_name)//2:]:
                    leader_name = leader_name[:len(leader_name)//2]
                clan_name = f"{leader_name}'s Clan"
            else:
                clan_name = clan_id.replace('_', ' ').title()
        
        # Формируем описание
        description_parts = []
        
        if info.get('leader'):
            description_parts.append(f"Led by {info['leader']['name']}")
        
        if clan_id in clan_settlements:
            settlements = clan_settlements[clan_id]
            settlement_count = len(settlements)
            description_parts.append(f"Controls {settlement_count} settlement(s)")
        
        hero_count = len(info.get('heroes', []))
        if hero_count > 0:
            description_parts.append(f"{hero_count} members")
        
        description = ". ".join(description_parts) if description_parts else None
        
        # Обновляем БД
        leader_id = info.get('leader', {}).get('id') if info.get('leader') else None
        leader_name_db = info.get('leader', {}).get('name') if info.get('leader') else None
        
        cursor.execute('''
            UPDATE clans
            SET name = ?,
                culture = ?,
                text = ?,
                leader_id = ?,
                leader_name = ?
            WHERE id = ?
        ''', (
            clan_name,
            info.get('culture'),
            description,
            leader_id,
            leader_name_db,
            clan_id
        ))
        
        updated += 1
    
    conn.commit()
    print(f"✅ Updated {updated} clans")
    
    conn.close()


if __name__ == '__main__':
    import argparse
    
    parser = argparse.ArgumentParser(
        description='Deep extraction of clan data from game files'
    )
    parser.add_argument(
        '--db-path',
        type=str,
        default='Database/bannerlord_lore.db',
        help='Path to database'
    )
    
    args = parser.parse_args()
    
    db_path = Path(args.db_path)
    
    if not db_path.exists():
        print(f"ERROR: Database not found: {db_path}")
        exit(1)
    
    print("=" * 60)
    print("Deep extraction of clan data")
    print("=" * 60)
    
    print("\n1. Extracting clan names from hero texts...")
    clan_mentions = extract_clan_names_from_texts()
    print(f"   Found mentions for {len(clan_mentions)} clans")
    
    # Показываем примеры
    print("\n   Examples of clan name mentions:")
    for clan_id, mentions in list(clan_mentions.items())[:5]:
        from collections import Counter
        top_mentions = Counter(mentions).most_common(3)
        print(f"   {clan_id}: {', '.join([f'{m[0]} ({m[1]})' for m in top_mentions])}")
    
    print("\n2. Extracting clan info from database...")
    clans_info = extract_clan_info_from_db(db_path)
    print(f"   Found info for {len(clans_info)} clans")
    
    print("\n3. Extracting settlement info...")
    clan_settlements = extract_settlement_info_for_clans(db_path)
    print(f"   Found settlements for {len(clan_settlements)} clans")
    
    print("\n4. Updating database...")
    update_clans_in_db(clans_info, clan_mentions, clan_settlements, db_path)
    
    print("\n✅ Done!")
    
    # Показываем примеры обновлённых кланов
    print("\n" + "=" * 60)
    print("Sample updated clans:")
    print("=" * 60)
    
    conn = sqlite3.connect(str(db_path))
    conn.row_factory = sqlite3.Row
    cursor = conn.cursor()
    
    cursor.execute('SELECT * FROM clans LIMIT 5')
    clans = cursor.fetchall()
    
    cursor.execute('PRAGMA table_info(clans)')
    columns = [col[1] for col in cursor.fetchall()]
    
    for clan in clans:
        print(f"\n{clan['id']}:")
        print(f"  Name: {clan['name']}")
        print(f"  Culture: {clan['culture']}")
        print(f"  Faction: {clan['faction']}")
        if 'leader_name' in columns:
            leader_name = clan['leader_name'] if clan['leader_name'] else 'N/A'
            print(f"  Leader: {leader_name}")
        if clan['text']:
            print(f"  Description: {clan['text'][:100]}...")
    
    conn.close()

