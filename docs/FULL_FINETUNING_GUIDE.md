# Полное руководство по Fine-tuning LLM для Bannerlord

## Эпопея за 3 дня: Уроки и решения

---

## 📋 Содержание

1. [Обзор проекта](#обзор-проекта)
2. [Подготовка датасета](#подготовка-датасета)
3. [Обучение модели](#обучение-модели)
4. [Конвертация в GGUF](#конвертация-в-gguf)
5. [Проблемы и решения](#проблемы-и-решения)
6. [Обучение в Colab](#обучение-в-colab)

---

## 📊 Обзор проекта

### Что мы сделали:
- Fine-tuned **Qwen3-8B** на лоре Mount & Blade II: Bannerlord
- Создали датасет из 7,209 записей на 3 языках (EN, RU, TR)
- Обучили модель на 2000 шагов
- Сконвертировали в GGUF для LM Studio/Ollama

### Результаты на HuggingFace:
- **Датасет:** https://huggingface.co/datasets/TSEOsiris/bannerlord-lore-dataset
- **LoRA v1 (4-bit base):** https://huggingface.co/TSEOsiris/bannerlord-lore-lora
- **LoRA v2 (clean base):** https://huggingface.co/TSEOsiris/bannerlord-lore-lora-v2
- **FP16 модель:** https://huggingface.co/TSEOsiris/bannerlord-lore-fp16
- **GGUF:** https://huggingface.co/TSEOsiris/bannerlord-lore-fp16-Q4_K_M-GGUF

---

## 📚 Подготовка датасета

### Источники данных:
1. In-game Encyclopedia (герои, королевства, поселения)
2. "Travels in Calradia" новеллы
3. Описания фракций и организаций
4. Исторические фигуры (Emperor Neretzes)

### Формат датасета (Alpaca):
```json
{
  "instruction": "Tell me about the Battanian faction",
  "input": "",
  "output": "The Battanians are the forest people..."
}
```

### Скрипты подготовки:
- `scripts/parse_ingame_encyclopedia.py` — парсинг энциклопедии
- `scripts/prepare_unsloth_dataset.py` — конвертация в формат обучения
- `scripts/publish_dataset.py` — публикация на HuggingFace

---

## 🎯 Обучение модели

### Железо:
- **Локально:** RTX 4070 Super (12GB) — достаточно для обучения
- **Colab:** A100 (40GB) — нужен для конвертации в GGUF

### Параметры обучения:
```python
model_name = "unsloth/Qwen3-8B"  # ВАЖНО: чистая модель!
max_steps = 2000
batch_size = 1  # для 12GB GPU
gradient_accumulation_steps = 8
learning_rate = 2e-4
lora_r = 16
lora_alpha = 16
```

### Команда запуска:
```powershell
cd C:\TSEBanerAi\TSEBanerAi
.\venv_py312\Scripts\Activate.ps1
python scripts/train_unsloth_v2.py --max_steps 2000 --batch_size 1
```

### Время обучения:
- 2000 шагов × ~5 сек = **~3 часа** на RTX 4070 Super

---

## 🔄 Конвертация в GGUF

### ⚠️ КРИТИЧЕСКИ ВАЖНО:

**Нельзя конвертировать в GGUF модель, обученную на `unsloth/Qwen3-8B-unsloth-bnb-4bit`!**

Причина: bitsandbytes квантизация создаёт специальные тензоры (`.absmax`, `.quant_state`), которые llama.cpp не понимает.

### ✅ Правильный путь:

1. **Обучение:** Использовать `unsloth/Qwen3-8B` (без `-bnb-4bit`)
2. **Конвертация в Colab с A100:**

```python
# Загрузить ПОЛНУЮ FP16 модель (не 4-bit!)
from transformers import AutoModelForCausalLM, AutoTokenizer
import torch

model = AutoModelForCausalLM.from_pretrained(
    "Qwen/Qwen3-8B",
    torch_dtype=torch.bfloat16,
    device_map="auto",
)
tokenizer = AutoTokenizer.from_pretrained("Qwen/Qwen3-8B")

# Применить LoRA
from peft import PeftModel
model = PeftModel.from_pretrained(model, "путь/к/lora")
model = model.merge_and_unload()

# Сохранить чистую FP16
model.save_pretrained("output")
tokenizer.save_pretrained("output")

# Загрузить на HuggingFace
# Использовать https://huggingface.co/spaces/ggml-org/gguf-my-repo
```

---

## 🔧 Проблемы и решения

### Проблема 1: Python 3.14 не поддерживает CUDA
**Решение:** Использовать Python 3.12

### Проблема 2: Обучение зависает
**Решение:** Уменьшить batch_size до 1, увеличить gradient_accumulation

### Проблема 3: "bitsandbytes not supported" при GGUF конвертации
**Решение:** 
1. Использовать чистую базовую модель (без `-bnb-4bit`)
2. Загружать FP16 модель для merge
3. Удалять `quantization_config` из config.json

### Проблема 4: T4/RTX 4070 не хватает памяти для конвертации
**Решение:** Использовать A100 в Colab Pro ($10/месяц)

### Проблема 5: "config.json not found" при GGUF
**Решение:** Сначала `model.save_pretrained()`, потом конвертация

### Проблема 6: Unsloth пытается запустить apt-get на Windows
**Решение:** GGUF конвертация только в Linux (Colab/Kaggle)

---

## ☁️ Обучение в Colab

### Можно ли обучать в Colab?

**ДА!** Colab отлично подходит для обучения.

### Плюсы:
- Бесплатный T4 (15GB) — достаточно для 8B модели
- Colab Pro даёт A100 (40GB) — комфортно для всего
- Не нагружает локальный ПК
- Можно оставить на ночь

### Минусы:
- Сессия отключается через ~12 часов
- Бесплатный Colab может отключить при высокой нагрузке
- Нужен интернет

### Рекомендация:
1. **Обучение:** Можно в Colab (T4 достаточно) или локально
2. **Конвертация в GGUF:** Только Colab с A100 (для 8B модели)

### Код для обучения в Colab:

```python
!pip install unsloth -q

from unsloth import FastLanguageModel

# Загрузка модели
model, tokenizer = FastLanguageModel.from_pretrained(
    model_name="unsloth/Qwen3-8B",
    max_seq_length=2048,
    load_in_4bit=True,
)

# Настройка LoRA
model = FastLanguageModel.get_peft_model(
    model,
    r=16,
    target_modules=["q_proj", "k_proj", "v_proj", "o_proj",
                    "gate_proj", "up_proj", "down_proj"],
    lora_alpha=16,
)

# Загрузка датасета
from datasets import load_dataset
dataset = load_dataset("TSEOsiris/bannerlord-lore-dataset")

# Обучение...
```

---

## 📁 Структура проекта

```
TSEBanerAi/
├── finetuning_data/           # Датасеты
│   ├── unsloth_training_dataset.json
│   ├── encyclopedia_*.json
│   └── travels_calradia/
├── scripts/
│   ├── train_unsloth_v2.py    # Основной скрипт обучения
│   ├── test_finetuned_model.py
│   ├── publish_dataset.py
│   └── Convert_to_GGUF_Colab.ipynb
├── outputs_v2/                # LoRA адаптер
├── models/                    # Локальные модели
└── docs/
    └── FULL_FINETUNING_GUIDE.md
```

---

## 🚀 Быстрый старт (для будущего)

### Если нужно переобучить:

1. Подготовь данные в `finetuning_data/`
2. Запусти локально:
   ```powershell
   python scripts/train_unsloth_v2.py --max_steps 2000
   ```
3. Загрузи LoRA на HuggingFace
4. В Colab (A100) примени LoRA к FP16 модели и сохрани
5. Используй Space для GGUF конвертации

### Если нужна только GGUF конвертация:

Используй готовый код из раздела "Конвертация в GGUF".

---

## 📝 Чеклист

- [ ] Python 3.12 (не 3.14!)
- [ ] CUDA 12.x
- [ ] Базовая модель: `unsloth/Qwen3-8B` (не `-bnb-4bit`)
- [ ] batch_size=1 для 12GB GPU
- [ ] Конвертация в GGUF только на A100 в Colab
- [ ] FP16 модель для merge (не 4-bit!)

---

## 🎮 Использование GGUF

### LM Studio:
1. Скачай GGUF с HuggingFace
2. Положи в `~/.cache/lm-studio/models/`
3. Выбери модель в LM Studio

### Ollama:
```bash
# Создай Modelfile
FROM ./bannerlord-lore-fp16-Q4_K_M.gguf

# Создай модель
ollama create bannerlord -f Modelfile

# Используй
ollama run bannerlord "Tell me about Battania"
```

---

---

## 🔮 Планы на будущее

### Мультиязычные модели:

| Язык | База для fine-tuning | Статус |
|------|---------------------|--------|
| EN | Qwen3-8B | ✅ Готово |
| RU | Saiga-Llama3-8B или Vikhr-Nemo-12B | 📋 Планируется |
| TR | Турецкая модель (TBD) | 📋 Планируется |

### Сбор данных из прототипа:

1. Логировать все диалоги Game Director
2. Отбирать лучшие примеры (качественный RP)
3. Добавлять в датасет для следующего fine-tuning
4. Итеративно улучшать модель

### Формат логов для сбора:
```json
{
  "context": "Location: Marunath...",
  "player_input": "I seek employment",
  "model_response": "Caladog speaks...",
  "quality_rating": 5,
  "commands_generated": ["offer_contract"],
  "language": "en"
}
```

---

*Документ создан: 3 января 2026*
*После 3 дней эпичной борьбы с fine-tuning и GGUF конвертацией* 😅

