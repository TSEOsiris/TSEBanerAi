#!/usr/bin/env python3
"""
Подсчет итоговой статистики по всем данным для fine-tuning
"""

import json
import sys
from pathlib import Path
from typing import Dict, List, Any

# Настройка кодировки для Windows
if sys.platform == 'win32':
    import io
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8')


class FineTuningStats:
    """Подсчет статистики по данным для fine-tuning"""
    
    def __init__(self, data_dir: Path):
        self.data_dir = data_dir
        self.stats = {}
        
    def count_file(self, file_path: Path) -> Dict[str, Any]:
        """Подсчет статистики для одного файла"""
        if not file_path.exists():
            return {'exists': False}
        
        with open(file_path, 'r', encoding='utf-8') as f:
            data = json.load(f)
        
        file_stats = {
            'exists': True,
            'file_size_kb': round(file_path.stat().st_size / 1024, 2)
        }
        
        if isinstance(data, list):
            file_stats['count'] = len(data)
            # Подсчет текстовых полей
            total_text_length = 0
            multilingual_count = 0
            
            for item in data:
                if isinstance(item, dict):
                    # Ищем текстовые поля
                    for key, value in item.items():
                        if isinstance(value, str) and value:
                            total_text_length += len(value)
                            if key.endswith('_ru') or key.endswith('_tr'):
                                multilingual_count += 1
            
            file_stats['total_text_length'] = total_text_length
            file_stats['total_text_length_kb'] = round(total_text_length / 1024, 2)
            file_stats['multilingual_fields'] = multilingual_count
            
        elif isinstance(data, dict):
            # Для словарей считаем по-другому
            if 'chapters' in data:
                file_stats['chapters'] = len(data.get('chapters', []))
                total_text = sum(len(ch.get('text', '')) for ch in data.get('chapters', []))
                file_stats['total_text_length'] = total_text
                file_stats['total_text_length_kb'] = round(total_text / 1024, 2)
            else:
                # Считаем все значения
                total_text_length = 0
                for key, value in data.items():
                    if isinstance(value, list):
                        file_stats[f'{key}_count'] = len(value)
                        for item in value:
                            if isinstance(item, dict):
                                for k, v in item.items():
                                    if isinstance(v, str):
                                        total_text_length += len(v)
                    elif isinstance(value, str):
                        total_text_length += len(value)
                
                file_stats['total_text_length'] = total_text_length
                file_stats['total_text_length_kb'] = round(total_text_length / 1024, 2)
        
        return file_stats
    
    def calculate_all_stats(self):
        """Подсчет статистики по всем файлам"""
        print("=" * 80)
        print("FINE-TUNING DATASET STATISTICS")
        print("=" * 80)
        
        files_to_check = {
            'clans': self.data_dir / 'clans.json',
            'settlements': self.data_dir / 'settlements.json',
            'lords': self.data_dir / 'lords.json',
            'factions': self.data_dir / 'factions.json',
            'english_only': self.data_dir / 'english_only.json',
            'travels_calradia': self.data_dir / 'travels_calradia_finetuning.json',  # Старый файл (русский)
            'travels_calradia_ru': self.data_dir / 'travels_calradia_finetuning_ru.json',
            'travels_calradia_en': self.data_dir / 'travels_calradia_finetuning_en.json',
            'travels_calradia_tr': self.data_dir / 'travels_calradia_finetuning_tr.json',
            'faction_descriptions': self.data_dir / 'faction_descriptions_ru.json',
            'emperor_neretzes': self.data_dir / 'emperor_neretzes.json',
            'organizations': self.data_dir / 'organizations_and_companies.json',
            'encyclopedia_all': self.data_dir / 'encyclopedia_all.json',
            'wiki_factions': Path(self.data_dir.parent) / 'wiki_data' / 'factions',
            'wiki_devblogs': Path(self.data_dir.parent) / 'wiki_data' / 'devblogs',
            'wiki_pages': Path(self.data_dir.parent) / 'wiki_data' / 'wiki_pages'
        }
        
        total_stats = {
            'total_records': 0,
            'total_text_length': 0,
            'total_file_size_kb': 0,
            'multilingual_records': 0,
            'files': {}
        }
        
        # Основные файлы
        print("\n📊 MAIN DATA FILES:")
        print("-" * 80)
        
        for name, file_path in files_to_check.items():
            if name.startswith('wiki_'):
                continue  # Обработаем отдельно
            
            stats = self.count_file(file_path)
            self.stats[name] = stats
            
            if stats.get('exists'):
                count = stats.get('count', stats.get('chapters', 0))
                text_kb = stats.get('total_text_length_kb', 0)
                size_kb = stats.get('file_size_kb', 0)
                
                print(f"✅ {name:20s}: {count:6d} records, {text_kb:8.2f} KB text, {size_kb:8.2f} KB file")
                
                total_stats['total_records'] += count
                total_stats['total_text_length'] += stats.get('total_text_length', 0)
                total_stats['total_file_size_kb'] += size_kb
                
                if stats.get('multilingual_fields', 0) > 0:
                    total_stats['multilingual_records'] += count
            else:
                print(f"❌ {name:20s}: File not found")
        
        # Wiki данные
        print("\n📚 WIKI DATA:")
        print("-" * 80)
        
        # Factions
        wiki_factions_dir = files_to_check['wiki_factions']
        if wiki_factions_dir.exists():
            faction_files = list(wiki_factions_dir.glob('*.json'))
            total_faction_text = 0
            for f in faction_files:
                with open(f, 'r', encoding='utf-8') as file:
                    data = json.load(file)
                    # Проверяем структуру с sections
                    if 'sections' in data:
                        for section in ['overview', 'history', 'troops', 'tactics', 'economy']:
                            if section in data['sections'] and data['sections'][section]:
                                total_faction_text += len(data['sections'][section])
                    else:
                        # Старая структура без sections
                        for section in ['overview', 'history', 'troops', 'tactics', 'economy']:
                            if section in data and data[section]:
                                total_faction_text += len(data[section])
            
            print(f"✅ wiki_factions: {len(faction_files)} files, {round(total_faction_text/1024, 2)} KB text")
            total_stats['total_text_length'] += total_faction_text
        
        # Devblogs
        wiki_devblogs_dir = files_to_check['wiki_devblogs']
        if wiki_devblogs_dir.exists():
            devblog_files = list(wiki_devblogs_dir.glob('*.json'))
            total_devblog_text = 0
            for f in devblog_files:
                with open(f, 'r', encoding='utf-8') as file:
                    data = json.load(file)
                    if 'content' in data and data['content']:
                        total_devblog_text += len(data['content'])
            
            print(f"✅ wiki_devblogs: {len(devblog_files)} files, {round(total_devblog_text/1024, 2)} KB text")
            total_stats['total_text_length'] += total_devblog_text
        
        # Wiki pages
        wiki_pages_dir = files_to_check['wiki_pages']
        if wiki_pages_dir.exists():
            wiki_page_files = list(wiki_pages_dir.rglob('*.json'))
            total_wiki_text = 0
            for f in wiki_page_files:
                with open(f, 'r', encoding='utf-8') as file:
                    data = json.load(file)
                    if 'content' in data and data['content']:
                        total_wiki_text += len(data['content'])
            
            print(f"✅ wiki_pages: {len(wiki_page_files)} files, {round(total_wiki_text/1024, 2)} KB text")
            total_stats['total_text_length'] += total_wiki_text
        
        # Travels in Calradia (мультиязычные версии)
        travels_files = [
            self.data_dir / 'travels_calradia_finetuning_ru.json',
            self.data_dir / 'travels_calradia_finetuning_en.json',
            self.data_dir / 'travels_calradia_finetuning_tr.json'
        ]
        
        total_travels_text = 0
        travels_count = 0
        for f in travels_files:
            if f.exists():
                with open(f, 'r', encoding='utf-8') as file:
                    data = json.load(file)
                    if isinstance(data, list):
                        travels_count += len(data)
                        for item in data:
                            if isinstance(item, dict) and 'text' in item:
                                total_travels_text += len(item['text'])
        
        if travels_count > 0:
            print(f"✅ travels_calradia (multilang): {travels_count} entries, {round(total_travels_text/1024, 2)} KB text")
            total_stats['total_text_length'] += total_travels_text
        
        # Описания фракций
        factions_desc_file = self.data_dir / 'faction_descriptions_ru.json'
        if factions_desc_file.exists():
            with open(factions_desc_file, 'r', encoding='utf-8') as f:
                factions_data = json.load(f)
                total_factions_text = sum(len(v) for v in factions_data.values() if isinstance(v, str))
                print(f"✅ faction_descriptions: {len(factions_data)} descriptions, {round(total_factions_text/1024, 2)} KB text")
                total_stats['total_text_length'] += total_factions_text
        
        # Итоговая статистика
        print("\n" + "=" * 80)
        print("📈 TOTAL STATISTICS")
        print("=" * 80)
        
        total_text_mb = total_stats['total_text_length'] / (1024 * 1024)
        total_file_mb = total_stats['total_file_size_kb'] / 1024
        
        print(f"\n📊 Dataset Overview:")
        print(f"   Total records:        {total_stats['total_records']:,}")
        print(f"   Multilingual records: {total_stats['multilingual_records']:,}")
        print(f"   Total text:           {total_text_mb:.2f} MB ({total_stats['total_text_length']:,} characters)")
        print(f"   Total file size:      {total_file_mb:.2f} MB")
        
        # Оценка качества
        print(f"\n📊 Quality Assessment:")
        
        quality_score = 0
        quality_notes = []
        
        if total_stats['total_records'] > 1000:
            quality_score += 2
            quality_notes.append("✅ Large dataset (>1000 records)")
        elif total_stats['total_records'] > 500:
            quality_score += 1
            quality_notes.append("⚠️  Medium dataset (500-1000 records)")
        else:
            quality_notes.append("❌ Small dataset (<500 records)")
        
        if total_text_mb > 10:
            quality_score += 2
            quality_notes.append("✅ Large text volume (>10 MB)")
        elif total_text_mb > 5:
            quality_score += 1
            quality_notes.append("⚠️  Medium text volume (5-10 MB)")
        else:
            quality_notes.append("❌ Small text volume (<5 MB)")
        
        if total_stats['multilingual_records'] > 0:
            quality_score += 2
            quality_notes.append(f"✅ Multilingual support ({total_stats['multilingual_records']} records)")
        else:
            quality_notes.append("❌ No multilingual data")
        
        # Проверка наличия разных типов данных
        data_types = []
        if self.stats.get('clans', {}).get('exists'):
            data_types.append('clans')
        if self.stats.get('settlements', {}).get('exists'):
            data_types.append('settlements')
        if self.stats.get('lords', {}).get('exists'):
            data_types.append('lords')
        if self.stats.get('factions', {}).get('exists'):
            data_types.append('factions')
        if self.stats.get('travels_calradia', {}).get('exists'):
            data_types.append('novella')
        if wiki_factions_dir.exists():
            data_types.append('wiki_factions')
        if wiki_devblogs_dir.exists():
            data_types.append('wiki_devblogs')
        
        if len(data_types) >= 5:
            quality_score += 2
            quality_notes.append(f"✅ Diverse data types ({len(data_types)} types)")
        elif len(data_types) >= 3:
            quality_score += 1
            quality_notes.append(f"⚠️  Limited data types ({len(data_types)} types)")
        else:
            quality_notes.append(f"❌ Few data types ({len(data_types)} types)")
        
        for note in quality_notes:
            print(f"   {note}")
        
        print(f"\n📊 Overall Quality Score: {quality_score}/8")
        
        if quality_score >= 7:
            print("   🎉 EXCELLENT - Ready for fine-tuning!")
        elif quality_score >= 5:
            print("   ✅ GOOD - Suitable for fine-tuning")
        elif quality_score >= 3:
            print("   ⚠️  FAIR - Consider adding more data")
        else:
            print("   ❌ POOR - Needs significant improvement")
        
        # Сохраняем статистику
        stats_file = self.data_dir / 'finetuning_stats.json'
        with open(stats_file, 'w', encoding='utf-8') as f:
            json.dump({
                'total_stats': total_stats,
                'file_stats': self.stats,
                'quality_score': quality_score,
                'quality_notes': quality_notes
            }, f, ensure_ascii=False, indent=2)
        
        print(f"\n💾 Statistics saved to: {stats_file.name}")
        
        return total_stats


def main():
    """Main entry point"""
    project_root = Path(__file__).parent.parent
    data_dir = project_root / 'finetuning_data'
    
    if not data_dir.exists():
        print(f"❌ Data directory not found: {data_dir}")
        return
    
    stats = FineTuningStats(data_dir)
    stats.calculate_all_stats()


if __name__ == '__main__':
    main()

