#!/usr/bin/env python3
"""Анализ готовой БД encyclopedia.db из игры"""
import sqlite3
from pathlib import Path

db_path = Path('Database/Ingame Encyclopedia/EN/encyclopedia.db')

conn = sqlite3.connect(str(db_path))
cursor = conn.cursor()

# Список всех таблиц
print("=" * 60)
print("TABLES IN ENCYCLOPEDIA.DB")
print("=" * 60)
cursor.execute("SELECT name FROM sqlite_master WHERE type='table' ORDER BY name")
tables = cursor.fetchall()
for table in tables:
    print(f"\nTable: {table[0]}")
    
    # Структура таблицы
    cursor.execute(f"PRAGMA table_info({table[0]})")
    columns = cursor.fetchall()
    print("  Columns:")
    for col in columns:
        pk = " (PK)" if col[5] else ""
        print(f"    - {col[1]} ({col[2]}){pk}")
    
    # Количество записей
    cursor.execute(f"SELECT COUNT(*) FROM {table[0]}")
    count = cursor.fetchone()[0]
    print(f"  Records: {count}")
    
    # Пример записи
    if count > 0:
        cursor.execute(f"SELECT * FROM {table[0]} LIMIT 1")
        row = cursor.fetchone()
        print("  Sample row:")
        for i, col in enumerate(columns):
            value = row[i] if row[i] else "NULL"
            if isinstance(value, str) and len(value) > 50:
                value = value[:50] + "..."
            print(f"    {col[1]}: {value}")

conn.close()

