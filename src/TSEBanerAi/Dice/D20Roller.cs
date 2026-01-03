using System;
using TaleWorlds.CampaignSystem;
using TSEBanerAi.Storage;
using TSEBanerAi.Storage.Models;
using TSEBanerAi.Utils;

namespace TSEBanerAi.Dice
{
    /// <summary>
    /// D20 dice rolling system for skill checks
    /// </summary>
    public class D20Roller
    {
        private static D20Roller _instance;
        private static readonly object _lock = new object();
        private readonly Random _random;

        /// <summary>
        /// Event fired when dice is rolled
        /// </summary>
        public event Action<DiceRollResult> OnDiceRolled;

        public static D20Roller Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new D20Roller();
                        }
                    }
                }
                return _instance;
            }
        }

        private D20Roller()
        {
            _random = new Random();
        }

        /// <summary>
        /// Roll D20 with modifiers
        /// </summary>
        public DiceRollResult Roll(Hero player, Hero npc, string skill, int dc)
        {
            // Roll base d20 (1-20)
            int baseRoll = _random.Next(1, 21);

            // Calculate modifier
            int modifier = ModifierCalculator.CalculateModifier(player, npc, skill);

            // Calculate total
            int total = baseRoll + modifier;

            // Determine success
            bool isSuccess = total >= dc;
            bool isCriticalSuccess = baseRoll == 20;
            bool isCriticalFailure = baseRoll == 1;

            // Override success for criticals
            if (isCriticalSuccess) isSuccess = true;
            if (isCriticalFailure) isSuccess = false;

            var result = new DiceRollResult
            {
                BaseRoll = baseRoll,
                Modifier = modifier,
                Total = total,
                DC = dc,
                Skill = skill,
                IsSuccess = isSuccess,
                IsCriticalSuccess = isCriticalSuccess,
                IsCriticalFailure = isCriticalFailure,
                PlayerName = player?.Name?.ToString(),
                NpcName = npc?.Name?.ToString(),
                Timestamp = DateTime.UtcNow
            };

            ModLogger.LogDebug($"D20 Roll: {baseRoll} + {modifier} = {total} vs DC {dc} -> {(isSuccess ? "SUCCESS" : "FAILURE")}");

            // Fire event for UI
            OnDiceRolled?.Invoke(result);

            // Log to database
            LogDiceRoll(npc, result);

            return result;
        }

        /// <summary>
        /// Roll D20 from DiceRequest
        /// </summary>
        public DiceRollResult RollFromRequest(Hero player, Hero npc, Dialogue.DiceRequest request)
        {
            if (request == null)
            {
                return new DiceRollResult { IsSuccess = false };
            }

            return Roll(player, npc, request.Skill, request.DC);
        }

        /// <summary>
        /// Log dice roll to database
        /// </summary>
        private void LogDiceRoll(Hero npc, DiceRollResult result)
        {
            try
            {
                var storage = StorageManager.Instance;
                if (!storage.IsInitialized) return;

                // Save as NPC memory
                var memory = new NpcMemory
                {
                    CampaignId = storage.CurrentCampaignId,
                    NpcId = npc?.StringId ?? "unknown",
                    MemoryType = result.IsSuccess ? MemoryTypes.DiceSuccess : MemoryTypes.DiceFailure,
                    Description = result.IsSuccess 
                        ? $"Player succeeded {result.Skill} check ({result.Total} vs DC {result.DC})"
                        : $"Player failed {result.Skill} check ({result.Total} vs DC {result.DC})",
                    Sentiment = result.IsSuccess ? 5 : -5,
                    GameDay = storage.GetCurrentGameDay(),
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true,
                    ExpiresOnDay = storage.GetCurrentGameDay() + 30 // Memory lasts 30 days
                };

                storage.Npcs.SaveMemory(memory);

                // Save as game event
                var gameEvent = new GameEvent
                {
                    CampaignId = storage.CurrentCampaignId,
                    EventType = EventTypes.DiceRoll,
                    PrimaryEntityId = npc?.StringId,
                    Description = $"{result.Skill} check: {result.BaseRoll}+{result.Modifier}={result.Total} vs DC {result.DC} - {(result.IsSuccess ? "Success" : "Failure")}",
                    DataJson = Newtonsoft.Json.JsonConvert.SerializeObject(result),
                    GameDay = storage.GetCurrentGameDay(),
                    Timestamp = DateTime.UtcNow,
                    IsLlmGenerated = false,
                    WasDisplayed = true
                };

                storage.Events.SaveEvent(gameEvent);
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to log dice roll", ex);
            }
        }
    }

    /// <summary>
    /// Result of a D20 roll
    /// </summary>
    public class DiceRollResult
    {
        public int BaseRoll { get; set; }
        public int Modifier { get; set; }
        public int Total { get; set; }
        public int DC { get; set; }
        public string Skill { get; set; }
        public bool IsSuccess { get; set; }
        public bool IsCriticalSuccess { get; set; }
        public bool IsCriticalFailure { get; set; }
        public string PlayerName { get; set; }
        public string NpcName { get; set; }
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Get result description for display
        /// </summary>
        public string GetResultText()
        {
            if (IsCriticalSuccess) return "CRITICAL SUCCESS!";
            if (IsCriticalFailure) return "CRITICAL FAILURE!";
            return IsSuccess ? "Success" : "Failure";
        }

        /// <summary>
        /// Get formatted roll string
        /// </summary>
        public string GetRollText()
        {
            string sign = Modifier >= 0 ? "+" : "";
            return $"[{BaseRoll}]{sign}{Modifier} = {Total} vs DC {DC}";
        }
    }
}

