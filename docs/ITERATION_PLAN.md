# План итераций и оптимизация промптов

## 📚 1. Изучение документации Bannerlord

### Что нужно изучить

#### 1.1. Dialogue System API
**Ключевые классы для изучения:**
- `ConversationManager` - управление диалогами
- `ConversationSentence` - предложения в диалоге
- `ConversationAction` - действия в диалоге
- `DialogueFlow` - поток диалога
- `CampaignConversationManager` - менеджер диалогов в кампании

**Что нужно узнать:**
- Как перехватить открытие диалога с NPC
- Как получить текущего собеседника (Hero)
- Как получить текст сообщения игрока
- Как вставить ответ NPC в диалог
- Как получить доступные действия для NPC

#### 1.2. Hero/Character API
**Ключевые классы:**
- `Hero` - герой/NPC
- `CharacterObject` - объект персонажа
- `CharacterTraits` - черты характера
- `CharacterAttributes` - атрибуты
- `Hero.CharacterObject` - связь Hero → CharacterObject

**Что нужно узнать:**
- Как получить все traits NPC
- Как получить отношения с игроком (`Hero.GetRelationWithPlayer()`)
- Как получить текущее местоположение NPC
- Как получить роль NPC (lord, notable, companion, etc.)
- Как получить информацию о поселении/королевстве

#### 1.3. Campaign System API
**Ключевые классы:**
- `Campaign` - основная система кампании
- `Settlement` - поселение
- `Kingdom` - королевство
- `Clan` - клан
- `QuestBase` - квесты

**Что нужно узнать:**
- Как получить текущее поселение игрока
- Как получить информацию о королевствах (только релевантные)
- Как получить активные квесты NPC
- Как получить историю взаимодействий

#### 1.4. Harmony Patching
**Что нужно изучить:**
- Как патчить методы диалоговой системы
- Prefix vs Postfix vs Transpiler
- Как передавать данные между патчами
- Как избежать конфликтов с другими модами

### Ресурсы для изучения

1. **Официальная документация:**
   - `docs/Documentations-master/` - локальная копия
   - https://moddocs.bannerlord.com/ - онлайн версия

2. **Исходный код AIInfluence:**
   - `E:\AIinfluence\` - примеры Harmony патчей
   - Изучить как они перехватывают диалоги

3. **Bannerlord API Reference:**
   - Использовать IntelliSense в Visual Studio
   - Изучить доступные методы через Reflection

### План изучения

**Итерация 0.1: Изучение API (1-2 дня)**
- [ ] Найти все классы, связанные с диалогами
- [ ] Создать тестовый Harmony patch для перехвата диалога
- [ ] Изучить структуру Hero и CharacterObject
- [ ] Найти способы получения traits и отношений
- [ ] Документировать найденные API в `docs/API_REFERENCE.md`

---

## 🔄 2. План итераций разработки

### Итерация 0: Подготовка (ТЕКУЩАЯ)
**Цель:** Изучить документацию, спроектировать архитектуру

**Задачи:**
- [x] Изучить документацию Bannerlord
- [ ] Спроектировать tier-based context system
- [ ] Определить интерфейсы для модулей
- [ ] Создать план оптимизации промптов

**Выход:** Документация и архитектурный план

---

### Итерация 1: Базовый перехват диалога (POC)
**Цель:** Перехватить диалог и получить базовые данные NPC

**Задачи:**
- [ ] Создать `Dialogue/DialoguePatches.cs` с Harmony patch
- [ ] Перехватить открытие диалога с NPC
- [ ] Получить Hero объект текущего собеседника
- [ ] Получить имя, роль, базовые данные NPC
- [ ] Логировать данные в консоль для проверки

**Критерии успеха:**
- ✅ При открытии диалога с NPC видим в логах его данные
- ✅ Можем получить Hero объект
- ✅ Можем получить имя и роль NPC

**Оценка:** 1-2 дня

---

### Итерация 2: Context Builder - Tier 1
**Цель:** Собрать минимальный контекст (Tier 1, ~500 tokens)

**Задачи:**
- [ ] Создать `Context/ContextBuilder.cs`
- [ ] Реализовать сбор Tier 1 данных:
  - NPC name, role, current location
  - Player message (если есть)
  - Critical JSON instructions
- [ ] Создать `Context/ContextTier.cs` enum
- [ ] Тестировать сбор данных

**Критерии успеха:**
- ✅ Можем собрать Tier 1 контекст
- ✅ Размер контекста ~400-600 tokens
- ✅ Все критичные данные включены

**Оценка:** 1 день

---

### Итерация 3: Prompt Builder - Базовый
**Цель:** Сгенерировать базовый промпт из Tier 1 контекста

**Задачи:**
- [ ] Создать `Prompt/PromptBuilder.cs`
- [ ] Создать `Prompt/PromptTemplate.cs` с шаблоном
- [ ] Реализовать генерацию промпта из Tier 1
- [ ] Добавить few-shot examples (1-2 примера)
- [ ] Тестировать генерацию промпта

**Критерии успеха:**
- ✅ Промпт генерируется корректно
- ✅ Размер промпта ~500-700 tokens
- ✅ JSON формат четко описан

**Оценка:** 1 день

---

### Итерация 4: LLM Provider - Ollama
**Цель:** Подключиться к Ollama и получить ответ

**Задачи:**
- [ ] Создать `LLM/ILLMProvider.cs` интерфейс
- [ ] Создать `LLM/OllamaProvider.cs` реализацию
- [ ] Реализовать HTTP запрос к Ollama API
- [ ] Парсить JSON ответ
- [ ] Обработать ошибки и таймауты

**Критерии успеха:**
- ✅ Можем отправить промпт в Ollama
- ✅ Получаем ответ за < 60 секунд
- ✅ JSON парсится корректно в 80%+ случаев

**Оценка:** 2 дня

---

### Итерация 5: Response Parser и отображение
**Цель:** Парсить ответ LLM и показывать в игре

**Задачи:**
- [ ] Создать `Dialogue/ResponseParser.cs`
- [ ] Реализовать парсинг JSON ответа
- [ ] Валидация структуры ответа
- [ ] Очистка ответа (удаление "thinking", markdown)
- [ ] Интегрировать ответ в диалог (или показать через InformationManager)

**Критерии успеха:**
- ✅ JSON парсится в 90%+ случаев
- ✅ Ответ показывается в игре
- ✅ Ошибки обрабатываются gracefully

**Оценка:** 1 день

---

### Итерация 6: Context Builder - Tier 2
**Цель:** Добавить релевантный контекст (Tier 2, ~1500 tokens)

**Задачи:**
- [ ] Расширить `ContextBuilder` для Tier 2
- [ ] Реализовать фильтрацию traits (топ-5 по relevance)
- [ ] Получить отношения с игроком
- [ ] Получить последние 3 диалога (если есть)
- [ ] Получить активные квесты NPC
- [ ] Реализовать relevance scoring для traits

**Критерии успеха:**
- ✅ Tier 2 контекст собирается корректно
- ✅ Размер контекста ~1400-1600 tokens
- ✅ Traits фильтруются по релевантности

**Оценка:** 2-3 дня

---

### Итерация 7: Prompt Optimization
**Цель:** Оптимизировать промпт до 5-7K tokens

**Задачи:**
- [ ] Создать `Prompt/PromptOptimizer.cs`
- [ ] Реализовать сжатие контекста
- [ ] Добавить Tier 3 (условный контекст)
- [ ] Реализовать progressive loading
- [ ] Тестировать размер промпта

**Критерии успеха:**
- ✅ Промпт < 7000 tokens
- ✅ Качество ответов не ухудшилось
- ✅ Время ответа < 40 секунд (Qwen2.5:14B)

**Оценка:** 2-3 дня

---

### Итерация 8: History Management
**Цель:** Управление историей диалогов

**Задачи:**
- [ ] Создать систему хранения истории диалогов
- [ ] Реализовать summarization старых сообщений
- [ ] Хранить только последние 3-5 диалогов полностью
- [ ] Старые диалоги → краткое описание

**Критерии успеха:**
- ✅ История сохраняется между сессиями
- ✅ Старые диалоги суммируются
- ✅ Размер истории не растет бесконечно

**Оценка:** 2 дня

---

### Итерация 9: Multiple LLM Providers
**Цель:** Поддержка нескольких провайдеров

**Задачи:**
- [ ] Создать `LMStudioProvider.cs`
- [ ] Создать `GroqProvider.cs`
- [ ] Реализовать переключение провайдеров
- [ ] Добавить настройки в конфиг

**Оценка:** 2-3 дня

---

### Итерация 10: MCM Integration
**Цель:** Настройки в игре через MCM

**Задачи:**
- [ ] Создать MCM settings page
- [ ] Настройки: LLM provider, URL, модель, таймауты
- [ ] Настройки: размер промпта, tier thresholds
- [ ] Сохранение настроек

**Оценка:** 2 дня

---

## 🚀 3. Способы оптимизации промпта

### 3.1. Tier-based Context Filtering

**Принцип:** Разделить контекст на 3 уровня приоритета

```
Tier 1 (Always, ~500 tokens):
  - NPC: name, role, location
  - Player message
  - JSON format instructions
  - Critical system prompts

Tier 2 (Relevant, ~1500 tokens):
  - Top 5 personality traits (by relevance score)
  - Relationship with player
  - Last 3 dialogue turns
  - Current quest/task

Tier 3 (Conditional, ~1000 tokens):
  - Nearby NPCs (only if mentioned)
  - Settlement info (if at settlement)
  - Kingdom relations (if relevant)
  - Available actions (filtered by NPC type)
```

**Реализация:**
```csharp
public class ContextTier
{
    public const int Tier1MaxTokens = 500;
    public const int Tier2MaxTokens = 1500;
    public const int Tier3MaxTokens = 1000;
    public const int TotalMaxTokens = 7000;
}

public class ContextBuilder
{
    public string BuildContext(Hero npc, string playerMessage, int maxTokens)
    {
        var tier1 = BuildTier1(npc, playerMessage);
        var tier2 = BuildTier2(npc, playerMessage);
        var tier3 = BuildTier3(npc, playerMessage, maxTokens - tier1.TokenCount - tier2.TokenCount);
        
        return CombineTiers(tier1, tier2, tier3);
    }
}
```

---

### 3.2. Relevance Scoring для Traits

**Проблема:** AIInfluence включает ВСЕ 20 traits → ~2000 tokens

**Решение:** Только топ-5 по relevance score

**Алгоритм:**
```csharp
public class TraitRelevanceScorer
{
    public List<TraitScore> ScoreTraits(Hero npc, string playerMessage, List<TraitObject> allTraits)
    {
        var scores = new List<TraitScore>();
        
        foreach (var trait in allTraits)
        {
            float score = 0f;
            
            // 1. Base importance (some traits are always important)
            score += GetBaseImportance(trait);
            
            // 2. Context relevance (if player message mentions related topics)
            score += GetContextRelevance(trait, playerMessage);
            
            // 3. NPC role relevance (lords care about honor, merchants about generosity)
            score += GetRoleRelevance(trait, npc);
            
            // 4. Relationship relevance (if player is friend/enemy, certain traits matter more)
            score += GetRelationshipRelevance(trait, npc);
            
            scores.Add(new TraitScore(trait, score));
        }
        
        return scores.OrderByDescending(s => s.Score).Take(5).ToList();
    }
}
```

**Экономия:** 20 traits → 5 traits = **75% reduction** (~1500 tokens)

---

### 3.3. Smart History Summarization

**Проблема:** AIInfluence включает полный текст всех диалогов → ~2000 tokens

**Решение:** 
- Последние 3 диалога → полный текст
- Старые диалоги → краткое описание (1-2 предложения)

**Реализация:**
```csharp
public class DialogueHistory
{
    public class DialogueTurn
    {
        public string PlayerMessage;
        public string NPCResponse;
        public DateTime Timestamp;
        public bool IsSummarized;
    }
    
    public string GetHistoryContext(List<DialogueTurn> history, int maxTokens)
    {
        var recent = history.TakeLast(3).ToList();
        var old = history.SkipLast(3).ToList();
        
        var recentText = string.Join("\n", recent.Select(h => 
            $"Player: {h.PlayerMessage}\nNPC: {h.NPCResponse}"));
        
        var oldSummary = old.Any() 
            ? $"Previous conversations: {Summarize(old)}"
            : "";
        
        return $"{oldSummary}\n\nRecent conversation:\n{recentText}";
    }
    
    private string Summarize(List<DialogueTurn> turns)
    {
        // Use LLM to summarize, or simple rule-based
        // "Discussed trade, politics, and quests. Player helped NPC with a task."
        return "Brief summary of past interactions...";
    }
}
```

**Экономия:** 10 диалогов → 3 полных + summary = **60-70% reduction** (~600-800 tokens)

---

### 3.4. Conditional World Data Loading

**Проблема:** AIInfluence включает ВСЕ королевства и поселения → ~3000 tokens

**Решение:** Только релевантные данные

**Правила фильтрации:**
```csharp
public class WorldDataFilter
{
    public WorldContext GetRelevantWorldData(Hero npc, string playerMessage, int maxTokens)
    {
        var context = new WorldContext();
        
        // Always include: NPC's kingdom and current settlement
        context.Kingdoms.Add(npc.Clan?.Kingdom);
        context.Settlements.Add(npc.CurrentSettlement);
        
        // Include if mentioned in player message
        if (MentionsKingdom(playerMessage))
        {
            context.Kingdoms.AddRange(GetMentionedKingdoms(playerMessage));
        }
        
        // Include if NPC is lord (they care about politics)
        if (npc.IsLord)
        {
            context.Kingdoms.AddRange(GetAlliedKingdoms(npc.Clan?.Kingdom));
        }
        
        // Include if at settlement (settlement info matters)
        if (npc.CurrentSettlement != null)
        {
            context.SettlementDetails = GetSettlementInfo(npc.CurrentSettlement);
        }
        
        return context;
    }
}
```

**Экономия:** Все королевства → 1-3 релевантных = **80-90% reduction** (~300-500 tokens)

---

### 3.5. Action Filtering

**Проблема:** AIInfluence включает ВСЕ возможные действия → ~1500 tokens

**Решение:** Только действия, доступные для этого NPC

**Реализация:**
```csharp
public class ActionFilter
{
    public List<DialogueAction> GetAvailableActions(Hero npc)
    {
        var actions = new List<DialogueAction>();
        
        // Base actions (always available)
        actions.Add(new DialogueAction("greet", "Greet the NPC"));
        actions.Add(new DialogueAction("farewell", "Say goodbye"));
        
        // Role-specific actions
        if (npc.IsLord)
        {
            actions.Add(new DialogueAction("recruit", "Recruit troops"));
            actions.Add(new DialogueAction("join_kingdom", "Join their kingdom"));
        }
        
        if (npc.IsNotable)
        {
            actions.Add(new DialogueAction("quest", "Ask about quests"));
            actions.Add(new DialogueAction("trade", "Trade"));
        }
        
        if (npc.IsCompanion)
        {
            actions.Add(new DialogueAction("dismiss", "Dismiss companion"));
        }
        
        // Relationship-specific actions
        if (npc.GetRelationWithPlayer() > 20)
        {
            actions.Add(new DialogueAction("gift", "Give a gift"));
        }
        
        return actions;
    }
}
```

**Экономия:** 50 действий → 5-10 релевантных = **80% reduction** (~200-300 tokens)

---

### 3.6. Prompt Compression Techniques

#### 3.6.1. Abbreviation Dictionary
```csharp
// Instead of: "The NPC is a lord of the Kingdom of Sturgia"
// Use: "NPC: lord, Sturgia"

private static readonly Dictionary<string, string> Abbreviations = new()
{
    { "Kingdom of ", "K:" },
    { "personality trait", "trait" },
    { "relationship with player", "rel:" },
    // ...
};
```

#### 3.6.2. Structured Format вместо Natural Language
```csharp
// Instead of: "The NPC has a brave personality trait with value 75"
// Use: "traits: {brave:75, honor:60, mercy:40}"

public string FormatTraits(List<TraitScore> traits)
{
    return $"traits: {{{string.Join(", ", traits.Select(t => $"{t.Trait.Name}:{t.Value}"))}}}";
}
```

#### 3.6.3. Remove Redundancy
```csharp
// Instead of repeating "The NPC" in every sentence
// Use bullet points:
// "• Name: John
//  • Role: Lord
//  • Location: Sturgia"
```

**Экономия:** 10-15% reduction через compression

---

### 3.7. Progressive Loading Strategy

**Принцип:** Начинать с минимального контекста, добавлять по необходимости

```csharp
public class ProgressiveContextBuilder
{
    public string BuildContext(Hero npc, string playerMessage, int targetTokens)
    {
        var context = new StringBuilder();
        int currentTokens = 0;
        
        // Step 1: Always add Tier 1
        var tier1 = BuildTier1(npc, playerMessage);
        context.Append(tier1);
        currentTokens += tier1.TokenCount;
        
        // Step 2: Add Tier 2 if budget allows
        if (currentTokens + Tier2MaxTokens <= targetTokens)
        {
            var tier2 = BuildTier2(npc, playerMessage);
            context.Append(tier2);
            currentTokens += tier2.TokenCount;
        }
        else
        {
            // Add partial Tier 2 (most important parts)
            var tier2Partial = BuildTier2Partial(npc, playerMessage, targetTokens - currentTokens);
            context.Append(tier2Partial);
            currentTokens += tier2Partial.TokenCount;
        }
        
        // Step 3: Add Tier 3 if still have budget
        if (currentTokens + Tier3MaxTokens <= targetTokens)
        {
            var tier3 = BuildTier3(npc, playerMessage);
            context.Append(tier3);
        }
        
        return context.ToString();
    }
}
```

---

### 3.8. Caching Strategy

**Принцип:** Кэшировать статичные данные, которые не меняются

```csharp
public class ContextCache
{
    private static Dictionary<Hero, CachedNPCData> _cache = new();
    
    public CachedNPCData GetCachedData(Hero npc)
    {
        if (!_cache.ContainsKey(npc) || _cache[npc].IsStale())
        {
            _cache[npc] = BuildNPCData(npc);
        }
        return _cache[npc];
    }
    
    // Cache:
    // - NPC name, role (static)
    // - Personality traits (static)
    // - Kingdom info (changes rarely)
    // Don't cache:
    // - Current location (changes often)
    // - Relationship (changes)
    // - Dialogue history (changes)
}
```

**Экономия:** Не нужно пересчитывать статичные данные каждый раз

---

## 📊 Ожидаемые результаты оптимизации

### Текущее состояние (AIInfluence):
```
Total: ~14,000 tokens
- Instructions: ~500 tokens
- Traits (all 20): ~2,000 tokens
- World data (all): ~3,000 tokens
- History (full): ~2,000 tokens
- Actions (all): ~1,500 tokens
- Examples: ~1,000 tokens
- Padding: ~4,000 tokens
```

### После оптимизации (TSEBanerAi):
```
Total: ~5,000-7,000 tokens
- Instructions: ~500 tokens (same)
- Traits (top 5): ~500 tokens (-75%)
- World data (relevant): ~500 tokens (-83%)
- History (3 recent + summary): ~800 tokens (-60%)
- Actions (filtered): ~300 tokens (-80%)
- Examples: ~500 tokens (-50%, fewer examples)
- Tier 3 (conditional): ~1,000 tokens (new, smart)
- Padding: ~900 tokens (-77%)
```

### Производительность:
- **Токены:** 14K → 6K = **57% reduction**
- **Время ответа (Qwen2.5:14B):** 85 сек → 30-40 сек = **53-65% faster**
- **Groq API:** Fit into 6K TPM limit ✅

---

## 🎯 Приоритеты реализации

**Phase 1 (POC):**
1. Tier 1 context (Iteration 2)
2. Basic prompt (Iteration 3)
3. Ollama integration (Iteration 4)
4. Response display (Iteration 5)

**Phase 2 (Optimization):**
1. Tier 2 context with trait filtering (Iteration 6)
2. Prompt optimization (Iteration 7)
3. History summarization (Iteration 8)

**Phase 3 (Polish):**
1. Multiple providers (Iteration 9)
2. MCM integration (Iteration 10)

---

*Создано: 2025-01-XX*  
*Статус: План готов к реализации*

