# Использование модели Bannerlord Lore

## Модель готова!

Обученная модель (4-bit merged) находится в папке:
```
C:\TSEBanerAi\TSEBanerAi\models\bannerlord-lore-4bit
```

Размер: ~7.1 GB (2 файла safetensors)

---

## Вариант 1: Загрузить в LM Studio напрямую

LM Studio поддерживает загрузку HuggingFace моделей:

1. Открой **LM Studio**
2. Перейди в **"My Models"** → **"Import a folder"**
3. Выбери папку: `C:\TSEBanerAi\TSEBanerAi\models\bannerlord-lore-4bit`
4. LM Studio автоматически распознает и загрузит модель

---

## Вариант 2: Конвертация в GGUF для Ollama

### Шаг 1: Загрузить на HuggingFace

```bash
# Установить huggingface-cli
pip install huggingface_hub

# Залогиниться
huggingface-cli login

# Создать репозиторий и загрузить
huggingface-cli upload YOUR_USERNAME/bannerlord-lore models/bannerlord-lore-4bit
```

### Шаг 2: Конвертировать в GGUF

Используй онлайн-конвертер:
https://huggingface.co/spaces/ggml-org/gguf-my-repo

1. Введи путь к модели: `YOUR_USERNAME/bannerlord-lore`
2. Выбери квантизацию: `Q4_K_M` (хороший баланс размер/качество)
3. Нажми "Submit"
4. Скачай GGUF файл

### Шаг 3: Использовать в Ollama

```bash
# Создай Modelfile
cat > Modelfile << EOF
FROM ./bannerlord-lore-Q4_K_M.gguf

SYSTEM "You are an expert on Mount & Blade II: Bannerlord lore."

PARAMETER temperature 0.7
EOF

# Создай модель в Ollama
ollama create bannerlord-lore -f Modelfile

# Запусти
ollama run bannerlord-lore
```

---

## Вариант 3: Использовать через Python API

Модель можно загрузить напрямую через Unsloth:

```python
from unsloth import FastLanguageModel

model, tokenizer = FastLanguageModel.from_pretrained(
    "models/bannerlord-lore-4bit",
    max_seq_length=2048,
    dtype=None,
    load_in_4bit=True,
)
FastLanguageModel.for_inference(model)

# Генерация
prompt = "Tell me about the Battanian faction"
inputs = tokenizer(f"### Instruction:\n{prompt}\n\n### Response:\n", return_tensors="pt").to("cuda")
outputs = model.generate(**inputs, max_new_tokens=300, temperature=0.7)
print(tokenizer.decode(outputs[0], skip_special_tokens=True))
```

---

## Характеристики модели

| Параметр | Значение |
|----------|----------|
| Базовая модель | Qwen3-8B |
| Fine-tuning | 2000 шагов, 5.76 эпох |
| Начальный Loss | 3.44 |
| Финальный Loss | 0.21 |
| Датасет | 2773 записи |
| Языки | EN, RU, TR |
| Размер | ~7.1 GB (4-bit) |

---

## Тестовые вопросы

Модель хорошо отвечает на вопросы о:
- Фракциях Calradia (Battania, Khuzait, Vlandia, etc.)
- Кланах и лордах
- Истории мира
- Поселениях и культурах

