import sqlite3

conn = sqlite3.connect('Database/bannerlord_lore.db')
cursor = conn.cursor()

# Проверяем фракции
cursor.execute('SELECT COUNT(*) FROM factions_lore')
total = cursor.fetchone()[0]
print(f'Total factions: {total}')

cursor.execute('SELECT COUNT(DISTINCT encyclopedia_id) FROM factions_lore WHERE encyclopedia_id IS NOT NULL AND encyclopedia_id != ""')
unique = cursor.fetchone()[0]
print(f'Unique encyclopedia_id: {unique}')

cursor.execute('SELECT encyclopedia_id, COUNT(*) as cnt FROM factions_lore GROUP BY encyclopedia_id HAVING cnt > 1')
duplicates = cursor.fetchall()
print(f'Duplicates: {len(duplicates)}')
for dup in duplicates[:10]:
    print(f'  {dup[0]}: {dup[1]} copies')

# Проверяем пустые ID
cursor.execute('SELECT COUNT(*) FROM factions_lore WHERE encyclopedia_id IS NULL OR encyclopedia_id = ""')
empty = cursor.fetchone()[0]
print(f'Empty encyclopedia_id: {empty}')

# Проверяем детали для одной фракции
print('\n--- Details for "aserai" ---')
cursor.execute('SELECT encyclopedia_id, name_en, name_ru, name_tr, source FROM factions_lore WHERE encyclopedia_id = ?', ('aserai',))
rows = cursor.fetchall()
print(f'Records: {len(rows)}')
for i, r in enumerate(rows, 1):
    print(f'  {i}. ID: {r[0]}, EN: {r[1][:30] if r[1] else "None"}, RU: {r[2][:30] if r[2] else "None"}, TR: {r[3][:30] if r[3] else "None"}, Source: {r[4]}')

conn.close()

