# TSEBanerAi - Development Context

## 🎯 Цель проекта

Создать AI-powered мод для Mount & Blade II: Bannerlord с **оптимизированными промптами** для локальных LLM моделей.

**Главная проблема AIInfluence:** промпт 14K токенов → слишком медленно для локальных моделей.
**Наша цель:** 5-7K токенов с умной фильтрацией контекста.

## ✅ Что уже сделано

### 1. Окружение разработки
- ✅ Отдельная копия игры: `C:\TSEBanerAi\Mount & Blade II Bannerlord`
- ✅ Visual Studio 2022 настроен с проектом C#
- ✅ Git репозиторий инициализирован
- ✅ Отладка работает (F5 или Attach to Process)
- ✅ Auto-deploy при сборке в папку игры

### 2. Структура проекта
```
C:\TSEBanerAi\TSEBanerAi/
├── TSEBanerAi.sln                    # Visual Studio Solution
├── .git/                             # Git repository
├── .gitignore                        # Игнорируем bin/, obj/, etc.
├── README.md                         # Краткое описание
├── CONTEXT.md                        # Этот файл
├── src/TSEBanerAi/
│   ├── TSEBanerAi.csproj            # C# проект (x64, .NET 4.7.2)
│   ├── SubModule.xml                # Bannerlord module definition
│   ├── SubModule.cs                 # Точка входа мода (✅ работает)
│   └── Properties/
│       └── launchSettings.json      # Настройки отладки
├── docs/
│   ├── DEVELOPMENT.md               # Инструкции по разработке
│   └── bannerlord-docs/             # Официальная документация
└── tools/                           # (будущее) скрипты сборки
```

### 3. Базовый мод
**Статус:** ✅ Работает

- Собирается без ошибок
- Деплоится автоматически в `C:\TSEBanerAi\Mount & Blade II Bannerlord\Modules\TSEBanerAi\`
- Загружается в игре
- Показывает сообщения через InformationManager
- Harmony патчи работают (тестовый патч `Campaign.OnGameLoaded`)

### 4. Технические детали
**Зависимости (NuGet):**
- `Bannerlord.ReferenceAssemblies.Core` 1.2.9.*
- `Bannerlord.MCM` 2.10.1
- `Bannerlord.Harmony` 2.2.2
- `Newtonsoft.Json` 13.0.3

**Решенные проблемы:**
- ✅ Platform target x64 (было MSIL → ошибки архитектуры)
- ✅ Добавлены using directives (Campaign, InformationManager, etc.)
- ✅ Wildcard версия для ReferenceAssemblies (1.2.9.*)
- ✅ launchSettings.json для запуска из VS

## 📋 Следующие шаги

### Phase 1: Архитектура (СЕЙЧАС)

**Нужно спроектировать:**

1. **Модульная структура**
```
src/TSEBanerAi/
├── Core/
│   ├── SubModule.cs                # Entry point
│   └── Config.cs                   # Settings
├── LLM/
│   ├── ILLMProvider.cs             # Interface
│   ├── OllamaProvider.cs           # Ollama implementation
│   ├── LMStudioProvider.cs         # LM Studio implementation
│   └── GroqProvider.cs             # Groq API implementation
├── Context/
│   ├── ContextBuilder.cs           # Собирает данные из игры
│   ├── ContextFilter.cs            # Фильтрует по важности
│   └── ContextTier.cs              # Tier 1/2/3 system
├── Prompt/
│   ├── PromptBuilder.cs            # Генерирует промпт
│   ├── PromptTemplate.cs           # Шаблоны промптов
│   └── PromptOptimizer.cs          # Сжатие/оптимизация
├── Dialogue/
│   ├── DialoguePatches.cs          # Harmony patches
│   ├── DialogueHandler.cs          # Обработка диалогов
│   └── ResponseParser.cs           # Парсинг JSON ответа
└── Utils/
    ├── Logger.cs                   # Логирование
    └── JsonHelper.cs               # JSON utilities
```

2. **Tier-based context system**
```
Tier 1 (Always): ~500 tokens
  - NPC: name, role, current location
  - Player message
  - Critical JSON instructions
  
Tier 2 (Relevant): ~1500 tokens
  - Top 3-5 personality traits (самые важные)
  - Relationship with player
  - Last 3 dialogue turns (summarized)
  - Current quest/task if any
  
Tier 3 (Conditional): ~1000 tokens
  - Nearby NPCs (only if mentioned)
  - Settlement info (if at settlement)
  - Kingdom relations (if relevant to topic)
  - Available actions for this NPC

TOTAL: ~3000 tokens (vs 14000 у AIInfluence)
```

3. **Smart filtering rules**
- Personality traits: только топ-5 по relevance score
- История: последние 3 сообщения, старые → summarize
- World data: только если NPC может на это влиять
- Actions: только доступные для этого NPC

### Phase 2: Proof of Concept

**Минимальный рабочий прототип:**

1. ✅ Harmony patch перехватывает открытие диалога
2. ✅ ContextBuilder собирает базовые данные (NPC name, traits)
3. ✅ PromptBuilder генерирует промпт (~3K tokens)
4. ✅ OllamaProvider отправляет в Ollama
5. ✅ ResponseParser парсит JSON ответ
6. ✅ Ответ показывается в игре

**Критерии успеха:**
- Промпт < 5000 токенов
- Ответ за < 10 секунд (Qwen2.5:14B на RTX 4070 Super)
- Валидный JSON в 90%+ случаев

### Phase 3: Расширения

- History summarization (старые сообщения → краткое описание)
- Caching (повторяющиеся данные)
- Multiple LLM providers (Ollama, LM Studio, Groq, Claude)
- MCM integration (настройки в игре)
- Event generation (как AIInfluence)

## 🎯 Ключевые технические решения

### Проблема AIInfluence

**Анализ промпта AIInfluence (14K tokens):**
```
~500 tokens  - Critical instructions
~2000 tokens - NPC full personality (ALL traits)
~3000 tokens - World data (all kingdoms, all settlements)
~2000 tokens - Conversation history (full text)
~1500 tokens - Available actions (ALL actions)
~1000 tokens - Examples
~4000 tokens - Context padding

ИТОГО: ~14,000 tokens
```

**Что происходит:**
- Qwen2.5:14B @ RTX 4070 Super: **85+ секунд** на ответ
- Groq API: **Rate limits** (6K TPM на free tier)
- Ollama 7B: **слишком глупая** для таких промптов

### Наше решение

**1. Умная фильтрация:**
```python
# Вместо всех 20 traits
traits_all = ["brave", "mercy", "valor", ...]  # 20 traits

# Берем только топ-5
traits_relevant = calculate_relevance(traits_all, context)[:5]

# Экономия: ~75% данных
```

**2. Контекстные зоны:**
```
Zone A (Core): ВСЕГДА включаем
  - Who is NPC
  - What player said
  - Expected output format

Zone B (Dynamic): Включаем по необходимости
  - Personality (если нужно для ответа)
  - History (если есть)
  - Relations (если спрашивают)

Zone C (Extended): Только если осталось место
  - World state
  - Available actions
```

**3. Progressive loading:**
```
1. Start with Tier 1 (500 tokens)
2. Add Tier 2 if token budget allows
3. Add Tier 3 if still under limit
4. If over limit → remove least important from Tier 3
```

**Ожидаемые результаты:**
- 5-7K tokens (вместо 14K) = **50-60% reduction**
- 30-40 секунд (вместо 85) на RTX 4070 Super
- Groq API: fit into 6K TPM limit

## 🔗 Референсы

### Код для изучения

**AIInfluence (плохие части - НЕ копировать):**
- `E:\AIinfluence\` - огромные промпты, но хорошие Harmony patches

**Наш прокси (хорошие части - использовать):**
- `E:\AIinfluence\llm_proxy\proxy_server.py`
- Few-shot examples
- JSON cleaning & validation
- Multiple backend support

### Документация

- Bannerlord Modding: https://moddocs.bannerlord.com/
- Harmony: https://harmony.pardeike.net/
- Локальная копия: `docs/bannerlord-docs/`

## 💡 Важные моменты

### Visual Studio
- **Platform Target: x64** обязательно (не Any CPU!)
- F5 для запуска с отладкой
- Или Debug → Attach to Process → Bannerlord.exe

### Harmony Patches
```csharp
[HarmonyPatch(typeof(TargetClass), "MethodName")]
public class MyPatch
{
    static void Prefix(/* params */) { }  // До вызова
    static void Postfix(/* params */) { } // После вызова
}
```

### Auto-deploy
Post-build event в `.csproj`:
```xml
<Target Name="PostBuild" AfterTargets="PostBuildEvent">
  <Exec Command="xcopy ... /Y" />
</Target>
```

### Логирование
```csharp
// In-game message
InformationManager.DisplayMessage(
    new InformationMessage("[TSEBanerAi] Text", Colors.Green)
);

// Console log
Debug.Print("[TSEBanerAi] Debug info");
```

## 🚀 Быстрые команды

### Сборка
```powershell
cd C:\TSEBanerAi\TSEBanerAi\src\TSEBanerAi
dotnet build -c Debug
```

### Очистка
```powershell
dotnet clean
```

### Restore пакетов
```powershell
dotnet restore
```

### Проверка в игре
1. Запустить игру (F5 или manually)
2. Main Menu → Singleplayer
3. Load save или New Campaign
4. Искать сообщение: `[TSEBanerAi] Module loaded successfully!` (зеленое)

## 📚 Контекст предыдущей работы

### Путь к этому проекту

**Начало:** Пытались заставить AIInfluence работать с локальными моделями

**Проблемы:**
1. Qwen2.5:7B - слишком глупая для сложных промптов
2. Qwen2.5:14B - слишком медленная (85 сек)
3. Groq API - rate limits на большие промпты
4. Ollama Cloud - unclear limits, "thinking" issues

**Решение:** Создать свой мод с оптимизированными промптами

### Опыт с прокси

Создали `llm_proxy` для AIInfluence:
- ✅ Поддержка Ollama, LM Studio, Groq
- ✅ Few-shot examples для формата
- ✅ Cleaning & validation JSON ответов
- ✅ Dynamic backend switching

**Этот опыт используем в новом моде напрямую в C#!**

### Технические уроки

1. **Few-shot works:** 1-2 примера JSON → 90%+ success rate
2. **Cleaning important:** models add `thinking`, need to strip
3. **Timeout tuning:** 60s для 14B, 30s для 7B
4. **Context matters:** shorter prompts = better quality

## 🎮 Hardware Setup

**Текущая система пользователя:**
- GPU: RTX 4070 Super 12GB VRAM
- Bannerlord + LLM одновременно: ~4-5 GB VRAM для LLM
- Optimal: Qwen2.5:14B Q4_K_M (~8GB)

**Бенчмарки (на основе экспериментов):**
- Qwen2.5:7B: быстро (~5 сек), но "глупая"
- Qwen2.5:14B Q4_K_M: ~85 сек на 14K prompt
- Qwen2.5:14B Q4_K_M: ~30-40 сек на 5K prompt (ожидаемое)

## 📝 Naming Conventions

**Префиксы:**
- Classes: `TSEBanerAi.*` или просто в namespace
- Harmony patches: `[TSEBanerAi] Patch description`
- In-game messages: `[TSEBanerAi] ...`
- Log messages: `[TSEBanerAi] ...`

**Не использовать `OVT_` как в AIInfluence - у нас свой стиль!**

---

## 📞 Для нового агента

Привет! Ты подключаешься к проекту разработки мода для Bannerlord.

**Текущий статус:**
- ✅ Dev environment готов
- ✅ Базовый мод работает
- 🔄 **СЛЕДУЮЩИЙ ШАГ: Спроектировать архитектуру оптимизации промптов**

**Что нужно:**
1. Детально спроектировать tier-based context system
2. Определить интерфейсы для модулей (LLM, Context, Prompt)
3. Создать план реализации POC (proof of concept)

**Важно:**
- Пользователь хочет **максимальную оптимизацию** для локальных LLM
- Фокус на **умную фильтрацию** контекста
- Цель: 5-7K tokens (vs 14K у AIInfluence)

**Вопросы?** Читай этот файл + `docs/DEVELOPMENT.md`

---

*Создано: 2025-12-29*  
*Последнее обновление: 2025-12-29 00:24 UTC+3*  
*Статус: ✅ Development Setup Complete → 🔄 Architecture Design Phase*


