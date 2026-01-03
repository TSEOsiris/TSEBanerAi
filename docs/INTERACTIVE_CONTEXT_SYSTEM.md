# Интерактивная система контекста (Interactive Context System)

## 🎯 Концепция

**Двухэтапный процесс:**
1. Мод отправляет минимальный промпт
2. Модель анализирует и запрашивает недостающую информацию
3. Мод предоставляет запрошенные данные
4. Модель генерирует финальный ответ

## 📋 Пример сценария

### Шаг 1: Игрок говорит
```
Игрок: "Какие сейчас дела в Оморе?"
```

### Шаг 2: Мод отправляет минимальный промпт
```json
{
  "task": "dialogue",
  "participants": {
    "player": "Player",
    "npc": "Raganvad"
  },
  "dialogue_history": [
    {"role": "player", "text": "Привет"},
    {"role": "npc", "text": "Здравствуй, путник"}
  ],
  "current_message": "Какие сейчас дела в Оморе?",
  "available_tools": [
    "get_settlement_info",
    "get_npc_info",
    "get_kingdom_info",
    "get_quest_info"
  ]
}
```

### Шаг 3: Модель запрашивает информацию
```json
{
  "thinking": "Игрок спрашивает про Омор. Мне нужна информация о текущем состоянии этого поселения.",
  "tool_calls": [
    {
      "tool": "get_settlement_info",
      "parameters": {
        "settlement_name": "Омор"
      }
    },
    {
      "tool": "get_npc_info",
      "parameters": {
        "npc_name": "Raganvad"
      }
    }
  ]
}
```

### Шаг 4: Мод предоставляет данные
```json
{
  "settlement_info": {
    "name": "Омор",
    "type": "town",
    "owner": "Clan.Clan_Sturgia_1",
    "current_owner_name": "Raganvad",
    "culture": "Culture.sturgia",
    "prosperity": 4500,
    "garrison": 120,
    "bound_villages": ["Dnin", "Karbur"]
  },
  "npc_info": {
    "name": "Raganvad",
    "role": "Ruler",
    "kingdom": "Sturgia",
    "relation_with_player": 15
  }
}
```

### Шаг 5: Модель генерирует финальный ответ
```json
{
  "response": "Омор находится под моим управлением. Город процветает, население растёт. Гарнизон готов к обороне. Деревни Dnin и Karbur обеспечивают нас ресурсами.",
  "emotion": "proud",
  "actions": []
}
```

## 🏗️ Архитектура

### Компоненты системы:

1. **ContextAnalyzer** - анализирует промпт и определяет недостающую информацию
2. **ToolCallParser** - парсит запросы модели на инструменты
3. **DataProvider** - предоставляет запрошенные данные из игры
4. **ResponseGenerator** - генерирует финальный ответ

### Поток данных:

```
[Игрок] 
  ↓
[Диалог Handler]
  ↓
[ContextBuilder] → Минимальный промпт (~500-1000 токенов)
  ↓
[LLM Provider] → Первый запрос
  ↓
[ToolCallParser] → Извлекает tool_calls
  ↓
[DataProvider] → Собирает данные из игры
  ↓
[ContextBuilder] → Добавляет данные в промпт
  ↓
[LLM Provider] → Второй запрос (с полным контекстом)
  ↓
[ResponseParser] → Парсит финальный ответ
  ↓
[Игрок видит ответ]
```

## 💻 Реализация

### 1. Минимальный промпт (Tier 1)

```csharp
public class MinimalPromptBuilder
{
    public string BuildMinimalPrompt(Hero npc, string playerMessage, List<DialogueTurn> history)
    {
        return $@"You are {npc.Name}, a {npc.Occupation} in Mount & Blade II: Bannerlord.

Current dialogue:
{FormatHistory(history)}
Player: {playerMessage}

If you need additional information to answer, use the available tools.
Available tools:
- get_settlement_info(name): Get current state of a settlement
- get_npc_info(name): Get information about an NPC
- get_kingdom_info(name): Get information about a kingdom
- get_quest_info(id): Get information about a quest

Respond in JSON format:
{{
  ""thinking"": ""Your analysis of what information you need"",
  ""tool_calls"": [{{""tool"": ""get_settlement_info"", ""parameters"": {{""name"": ""Omor""}}}}],
  ""response"": ""Your response (if you have enough info)""
}}";
    }
}
```

### 2. Tool Call Parser

```csharp
public class ToolCallParser
{
    public class ToolCall
    {
        public string Tool;
        public Dictionary<string, object> Parameters;
    }
    
    public List<ToolCall> ParseToolCalls(string llmResponse)
    {
        // Парсим JSON ответ модели
        var json = JsonConvert.DeserializeObject<dynamic>(llmResponse);
        
        var toolCalls = new List<ToolCall>();
        
        if (json.tool_calls != null)
        {
            foreach (var call in json.tool_calls)
            {
                toolCalls.Add(new ToolCall
                {
                    Tool = call.tool,
                    Parameters = call.parameters
                });
            }
        }
        
        return toolCalls;
    }
}
```

### 3. Data Provider

```csharp
public class GameDataProvider
{
    public SettlementInfo GetSettlementInfo(string settlementName)
    {
        // Найти поселение в игре
        var settlement = Campaign.Current.Settlements
            .FirstOrDefault(s => s.Name.ToString() == settlementName);
        
        if (settlement == null) return null;
        
        return new SettlementInfo
        {
            Name = settlement.Name.ToString(),
            Type = settlement.IsTown ? "town" : settlement.IsCastle ? "castle" : "village",
            Owner = settlement.OwnerClan?.Name?.ToString(),
            Culture = settlement.Culture?.Name?.ToString(),
            Prosperity = settlement.Town?.Prosperity ?? 0,
            Garrison = settlement.Town?.GarrisonParty?.Party?.NumberOfAllMembers ?? 0,
            BoundVillages = settlement.BoundVillages?.Select(v => v.Name.ToString()).ToList()
        };
    }
    
    public NPCInfo GetNPCInfo(string npcName)
    {
        var hero = Campaign.Current.Heroes
            .FirstOrDefault(h => h.Name.ToString() == npcName);
        
        if (hero == null) return null;
        
        return new NPCInfo
        {
            Name = hero.Name.ToString(),
            Role = hero.IsLord ? "Lord" : hero.IsWanderer ? "Wanderer" : "NPC",
            Kingdom = hero.Clan?.Kingdom?.Name?.ToString(),
            RelationWithPlayer = hero.GetRelationWithPlayer()
        };
    }
}
```

### 4. Двухэтапный процесс

```csharp
public class InteractiveDialogueHandler
{
    private readonly ILLMProvider _llmProvider;
    private readonly GameDataProvider _dataProvider;
    private readonly ToolCallParser _toolCallParser;
    
    public async Task<string> HandleDialogue(Hero npc, string playerMessage, List<DialogueTurn> history)
    {
        // Шаг 1: Минимальный промпт
        var minimalPrompt = BuildMinimalPrompt(npc, playerMessage, history);
        
        // Шаг 2: Первый запрос к модели
        var firstResponse = await _llmProvider.GenerateAsync(minimalPrompt);
        
        // Шаг 3: Парсим tool calls
        var toolCalls = _toolCallParser.ParseToolCalls(firstResponse);
        
        // Шаг 4: Если есть tool calls - собираем данные
        if (toolCalls.Any())
        {
            var additionalContext = new StringBuilder();
            
            foreach (var call in toolCalls)
            {
                switch (call.Tool)
                {
                    case "get_settlement_info":
                        var settlementName = call.Parameters["name"].ToString();
                        var settlementInfo = _dataProvider.GetSettlementInfo(settlementName);
                        additionalContext.AppendLine($"Settlement {settlementName}: {JsonConvert.SerializeObject(settlementInfo)}");
                        break;
                    
                    case "get_npc_info":
                        var npcName = call.Parameters["name"].ToString();
                        var npcInfo = _dataProvider.GetNPCInfo(npcName);
                        additionalContext.AppendLine($"NPC {npcName}: {JsonConvert.SerializeObject(npcInfo)}");
                        break;
                }
            }
            
            // Шаг 5: Второй запрос с полным контекстом
            var fullPrompt = $"{minimalPrompt}\n\nAdditional information:\n{additionalContext}\n\nNow provide your final response.";
            var finalResponse = await _llmProvider.GenerateAsync(fullPrompt);
            
            return ExtractResponse(finalResponse);
        }
        else
        {
            // Если tool calls нет - возвращаем ответ сразу
            return ExtractResponse(firstResponse);
        }
    }
}
```

## ✅ Преимущества

1. **Минимальный промпт** (~500-1000 токенов вместо 5-7K)
2. **Только релевантная информация** - модель запрашивает только то, что нужно
3. **Актуальные данные** - информация берётся из игры в реальном времени
4. **Масштабируемость** - легко добавлять новые инструменты

## ⚠️ Недостатки и вызовы

1. **Два запроса к модели** → удваивает время ответа
   - Решение: использовать быструю модель для первого запроса (7B), большую для финального (14B)

2. **Сложность парсинга tool calls**
   - Модель может неправильно сформировать запрос
   - Решение: Чёткие инструкции + валидация

3. **Латентность**
   - Первый запрос: ~5-10 сек
   - Сбор данных: ~0.1 сек
   - Второй запрос: ~15-25 сек
   - **Итого: ~20-35 сек** (vs 30-40 сек с полным промптом)

## 🎯 Оптимизация: Гибридный подход

### Вариант A: Предсказательный (Predictive)

**Концепция:** Мод сам определяет, какая информация нужна, без запроса к модели

```csharp
public class PredictiveContextBuilder
{
    public ContextInfo BuildContext(Hero npc, string playerMessage)
    {
        var context = new ContextInfo();
        
        // Анализируем сообщение игрока
        var mentionedSettlements = ExtractSettlements(playerMessage);
        var mentionedNPCs = ExtractNPCs(playerMessage);
        var mentionedKingdoms = ExtractKingdoms(playerMessage);
        
        // Автоматически добавляем релевантную информацию
        foreach (var settlement in mentionedSettlements)
        {
            context.AddSettlementInfo(GetSettlementInfo(settlement));
        }
        
        // Добавляем только если упомянуто
        if (MentionsPolitics(playerMessage))
        {
            context.AddKingdomInfo(npc.Clan?.Kingdom);
        }
        
        return context;
    }
}
```

**Преимущества:**
- ✅ Один запрос к модели (быстрее)
- ✅ Мод сам определяет релевантность
- ✅ Меньше сложности

**Недостатки:**
- ⚠️ Может добавить лишнюю информацию
- ⚠️ Может пропустить нужную информацию

### Вариант B: Интерактивный (Interactive) - ваш вариант

**Преимущества:**
- ✅ Только нужная информация
- ✅ Модель сама решает, что нужно
- ✅ Более точный контекст

**Недостатки:**
- ⚠️ Два запроса (медленнее)
- ⚠️ Сложнее реализация

### Вариант C: Гибридный (Hybrid) ⭐ РЕКОМЕНДУЮ

**Стратегия:**
1. Мод предсказывает базовую релевантную информацию (1 запрос)
2. Если модель явно запрашивает что-то ещё - добавляем (опционально)

```csharp
public class HybridContextBuilder
{
    public async Task<string> HandleDialogue(Hero npc, string playerMessage, List<DialogueTurn> history)
    {
        // Шаг 1: Предсказываем релевантную информацию
        var predictedContext = PredictRelevantContext(playerMessage, npc);
        
        // Шаг 2: Строим промпт с предсказанным контекстом
        var prompt = BuildPrompt(npc, playerMessage, history, predictedContext);
        
        // Шаг 3: Запрос к модели
        var response = await _llmProvider.GenerateAsync(prompt);
        
        // Шаг 4: Проверяем, запрашивает ли модель дополнительную информацию
        if (HasToolCalls(response))
        {
            var toolCalls = ParseToolCalls(response);
            var additionalData = FetchAdditionalData(toolCalls);
            
            // Шаг 5: Второй запрос с дополнительными данными
            prompt += $"\n\nAdditional data: {additionalData}";
            response = await _llmProvider.GenerateAsync(prompt);
        }
        
        return ExtractResponse(response);
    }
}
```

## 📊 Сравнение подходов

| Подход | Размер промпта | Время ответа | Точность | Сложность |
|--------|---------------|--------------|----------|-----------|
| **Полный контекст** | 5-7K | 30-40 сек | Высокая | Низкая |
| **Предсказательный** | 1.5-3K | 15-25 сек | Средняя | Средняя |
| **Интерактивный** | 0.5-1K → 2-3K | 20-35 сек | Высокая | Высокая |
| **Гибридный** | 1.5-2K → 2.5-3K | 18-30 сек | Высокая | Средняя |

## 🎯 Рекомендация

**Начать с Гибридного подхода:**

1. **Фаза 1:** Предсказательный контекст (быстро, просто)
2. **Фаза 2:** Добавить интерактивные запросы (если нужно)

**Пример реализации:**

```csharp
// Простой анализ сообщения игрока
var needsSettlementInfo = playerMessage.Contains("Омор") || 
                          playerMessage.Contains("город") ||
                          playerMessage.Contains("поселение");

if (needsSettlementInfo)
{
    var mentionedSettlement = ExtractSettlementName(playerMessage);
    if (mentionedSettlement != null)
    {
        context.AddSettlementInfo(GetSettlementInfo(mentionedSettlement));
    }
}
```

Это даст **80% преимуществ** интерактивного подхода с **20% сложности**!

## 💡 Вывод

**Ваша идея отличная!** Интерактивная система позволяет:
- ✅ Минимальный промпт (~500-1000 токенов)
- ✅ Только релевантная информация
- ✅ Актуальные данные из игры

**Но рекомендую начать с гибридного подхода:**
- Предсказывать базовую информацию
- Добавлять интерактивные запросы только если нужно

Это даст лучшее соотношение скорость/качество/сложность!

