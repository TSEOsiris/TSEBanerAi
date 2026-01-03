using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TSEBanerAi.Storage.Models;
using TSEBanerAi.Utils;

namespace TSEBanerAi.LLM
{
    /// <summary>
    /// Builds prompts for LLM requests with context management
    /// </summary>
    public class PromptBuilder
    {
        private readonly StringBuilder _systemPrompt;
        private readonly List<LLMMessage> _messages;
        private NpcSnapshot _npcContext;
        private string _playerName;
        private int _currentGameDay;
        private List<string> _enemies;
        private List<string> _allies;

        /// <summary>
        /// Estimated token count (rough approximation)
        /// </summary>
        public int EstimatedTokens => (_systemPrompt.Length + _messages.Sum(m => m.Content.Length)) / 4;

        public PromptBuilder()
        {
            _systemPrompt = new StringBuilder();
            _messages = new List<LLMMessage>();
        }

        /// <summary>
        /// Set player name for context
        /// </summary>
        public PromptBuilder WithPlayerName(string name)
        {
            _playerName = name;
            return this;
        }

        /// <summary>
        /// Set current game day
        /// </summary>
        public PromptBuilder WithGameDay(int day)
        {
            _currentGameDay = day;
            return this;
        }

        /// <summary>
        /// Set NPC context
        /// </summary>
        public PromptBuilder WithNpcContext(NpcSnapshot npc)
        {
            _npcContext = npc;
            return this;
        }

        /// <summary>
        /// Set current enemies (kingdoms at war with NPC's faction)
        /// </summary>
        public PromptBuilder WithEnemies(List<string> enemies)
        {
            _enemies = enemies;
            return this;
        }

        /// <summary>
        /// Set current allies
        /// </summary>
        public PromptBuilder WithAllies(List<string> allies)
        {
            _allies = allies;
            return this;
        }

        /// <summary>
        /// Build system prompt for roleplay dialogue
        /// </summary>
        public PromptBuilder BuildRoleplaySystemPrompt()
        {
            _systemPrompt.Clear();

            string npcName = _npcContext?.Name ?? "Unknown";
            string clanName = _npcContext?.ClanName ?? "no clan";
            string kingdomName = _npcContext?.KingdomName ?? "no kingdom";
            string culture = _npcContext?.Culture ?? "unknown";
            string location = _npcContext?.CurrentLocationName ?? "traveling";

            // Very explicit instruction about character identity to prevent hallucination
            _systemPrompt.AppendLine("=== CRITICAL IDENTITY INFORMATION ===");
            _systemPrompt.AppendLine($"You are roleplaying as: {npcName}");
            _systemPrompt.AppendLine($"Your clan: {clanName}");
            _systemPrompt.AppendLine($"Your kingdom: {kingdomName}");
            _systemPrompt.AppendLine($"Your culture: {culture}");
            _systemPrompt.AppendLine();
            _systemPrompt.AppendLine("RULES:");
            _systemPrompt.AppendLine($"- You ARE {npcName}. No other name.");
            _systemPrompt.AppendLine($"- Your clan IS {clanName}. Do not invent other clan names.");
            _systemPrompt.AppendLine($"- Your kingdom IS {kingdomName}. Do not invent other kingdoms.");
            _systemPrompt.AppendLine("- Do NOT use placeholders like {{NAME}} or similar.");
            _systemPrompt.AppendLine("- Only reference locations, clans, and characters that exist in Bannerlord.");
            _systemPrompt.AppendLine();

            if (_npcContext != null)
            {
                _systemPrompt.AppendLine($"=== CHARACTER SHEET: {npcName} ===");
                _systemPrompt.AppendLine($"Full Name: {npcName}");
                _systemPrompt.AppendLine($"Age: {_npcContext.Age}");
                _systemPrompt.AppendLine($"Gender: {_npcContext.Gender}");
                _systemPrompt.AppendLine($"Culture: {culture}");
                
                if (!string.IsNullOrEmpty(_npcContext.Occupation))
                    _systemPrompt.AppendLine($"Role: {_npcContext.Occupation}");
                
                _systemPrompt.AppendLine($"Clan: {clanName}");
                _systemPrompt.AppendLine($"Kingdom/Faction: {kingdomName}");
                _systemPrompt.AppendLine($"Current Location: {location}");
                
                // Add owned fiefs if available
                if (!string.IsNullOrEmpty(_npcContext.OwnedFiefsJson) && _npcContext.OwnedFiefsJson != "[]")
                {
                    _systemPrompt.AppendLine($"Owned Settlements: {_npcContext.OwnedFiefsJson}");
                }
                
                if (_npcContext.PartySize > 0)
                {
                    _systemPrompt.AppendLine($"Party Size: {_npcContext.PartySize} troops");
                }

                _systemPrompt.AppendLine();
                _systemPrompt.AppendLine("=== PERSONALITY TRAITS ===");
                _systemPrompt.AppendLine(GetTraitDescription(_npcContext));

                _systemPrompt.AppendLine();
                _systemPrompt.AppendLine($"=== RELATIONSHIP WITH PLAYER ===");
                _systemPrompt.AppendLine($"Player name: {_playerName ?? "the player"}");
                _systemPrompt.AppendLine($"Your relation with player: {GetRelationDescription(_npcContext.RelationWithPlayer)}");
            }
            else
            {
                _systemPrompt.AppendLine("=== WARNING: No character context provided ===");
            }

            // Add diplomacy information
            _systemPrompt.AppendLine();
            _systemPrompt.AppendLine("=== CURRENT DIPLOMACY ===");
            if (_enemies != null && _enemies.Count > 0)
            {
                _systemPrompt.AppendLine($"Your kingdom is AT WAR with: {string.Join(", ", _enemies)}");
                _systemPrompt.AppendLine("You may ONLY speak negatively about these factions as enemies.");
            }
            else
            {
                _systemPrompt.AppendLine("Your kingdom is currently at peace (no active wars).");
            }
            
            if (_allies != null && _allies.Count > 0)
            {
                _systemPrompt.AppendLine($"Allied factions: {string.Join(", ", _allies)}");
            }
            
            _systemPrompt.AppendLine("IMPORTANT: Do NOT invent wars or enemies. Only mention conflicts listed above.");

            _systemPrompt.AppendLine();
            _systemPrompt.AppendLine("=== LANGUAGE & STYLE ===");
            _systemPrompt.AppendLine("You are in a medieval setting. Your speech must be appropriate:");
            _systemPrompt.AppendLine("- Use formal, medieval-appropriate language");
            _systemPrompt.AppendLine("- Avoid modern slang (no 'boss', 'cool', 'okay', 'guys', etc.)");
            _systemPrompt.AppendLine("- Use terms like: 'my lord', 'indeed', 'aye', 'nay', 'mayhaps', 'tis', 'twas'");
            _systemPrompt.AppendLine("- Reference medieval concepts: honor, duty, fealty, liege, vassal");
            _systemPrompt.AppendLine("- Be concise (2-4 sentences) unless asked for details");

            _systemPrompt.AppendLine();
            _systemPrompt.AppendLine("=== RESPONSE RULES ===");
            _systemPrompt.AppendLine("- Respond in character as the NPC described above");
            _systemPrompt.AppendLine("- Express emotions using *actions* in asterisks");
            _systemPrompt.AppendLine("- Reference ONLY the clan, kingdom, enemies from your character sheet");
            _systemPrompt.AppendLine("- Do NOT invent battles, raids, or events that are not happening");
            _systemPrompt.AppendLine("- React based on your relationship with the player");

            return this;
        }

        /// <summary>
        /// Build system prompt with command capabilities
        /// </summary>
        public PromptBuilder BuildCommandSystemPrompt()
        {
            BuildRoleplaySystemPrompt();

            _systemPrompt.AppendLine();
            _systemPrompt.AppendLine("=== COMMAND OUTPUT ===");
            _systemPrompt.AppendLine("When you agree to an action, append a JSON command block:");
            _systemPrompt.AppendLine("```json");
            _systemPrompt.AppendLine("{\"command\": \"COMMAND_TYPE\", \"params\": {...}}");
            _systemPrompt.AppendLine("```");
            _systemPrompt.AppendLine();
            _systemPrompt.AppendLine("Available commands:");
            _systemPrompt.AppendLine("- {\"command\": \"follow\"} - Follow the player");
            _systemPrompt.AppendLine("- {\"command\": \"unfollow\"} - Stop following");
            _systemPrompt.AppendLine("- {\"command\": \"patrol\", \"params\": {\"target\": \"settlement_name\"}}");
            _systemPrompt.AppendLine("- {\"command\": \"attack\", \"params\": {\"target\": \"enemy_name\"}}");
            _systemPrompt.AppendLine("- {\"command\": \"siege\", \"params\": {\"target\": \"settlement_name\"}}");
            _systemPrompt.AppendLine("- {\"command\": \"change_relation\", \"params\": {\"amount\": N}} - Change relation by N");
            _systemPrompt.AppendLine();
            _systemPrompt.AppendLine("=== DICE ROLLS ===");
            _systemPrompt.AppendLine("If the request requires persuasion, intimidation, or deception, request a dice roll:");
            _systemPrompt.AppendLine("```json");
            _systemPrompt.AppendLine("{\"dice_request\": true, \"skill\": \"charm|roguery|leadership\", \"dc\": 10-20}");
            _systemPrompt.AppendLine("```");
            _systemPrompt.AppendLine("DC should be based on:");
            _systemPrompt.AppendLine("- 10: Easy (good relation, aligned with personality)");
            _systemPrompt.AppendLine("- 15: Medium (neutral relation or request)");
            _systemPrompt.AppendLine("- 20: Hard (bad relation, against personality)");

            return this;
        }

        /// <summary>
        /// Add chat history messages
        /// </summary>
        public PromptBuilder WithChatHistory(List<ChatMessage> history, int maxMessages = 10)
        {
            if (history == null) return this;

            // Take last N messages (TakeLast not available in .NET 4.7.2)
            var recent = history.Count > maxMessages 
                ? history.Skip(history.Count - maxMessages) 
                : history;
            
            foreach (var msg in recent)
            {
                if (msg.IsPlayerMessage)
                {
                    _messages.Add(LLMMessage.User(msg.Content));
                }
                else
                {
                    _messages.Add(LLMMessage.Assistant(msg.Content));
                }
            }

            return this;
        }

        /// <summary>
        /// Add NPC memories to context
        /// </summary>
        public PromptBuilder WithMemories(List<NpcMemory> memories)
        {
            if (memories == null || memories.Count == 0) return this;

            _systemPrompt.AppendLine();
            _systemPrompt.AppendLine("=== MEMORIES OF PAST INTERACTIONS ===");
            
            foreach (var memory in memories.Take(5))
            {
                string sentiment = memory.Sentiment > 0 ? "(positive)" : memory.Sentiment < 0 ? "(negative)" : "";
                _systemPrompt.AppendLine($"- Day {memory.GameDay}: {memory.Description} {sentiment}");
            }

            return this;
        }

        /// <summary>
        /// Add current player message
        /// </summary>
        public PromptBuilder WithPlayerMessage(string message)
        {
            _messages.Add(LLMMessage.User(message));
            return this;
        }

        /// <summary>
        /// Add dice roll result context
        /// </summary>
        public PromptBuilder WithDiceResult(int roll, int modifier, int dc, bool success, string skill)
        {
            string resultText = success ? "SUCCEEDED" : "FAILED";
            int total = roll + modifier;

            _systemPrompt.AppendLine();
            _systemPrompt.AppendLine($"=== DICE ROLL RESULT ===");
            _systemPrompt.AppendLine($"The player rolled: {roll} + {modifier} ({skill}) = {total} vs DC {dc}");
            _systemPrompt.AppendLine($"Result: {resultText}");
            _systemPrompt.AppendLine();

            if (success)
            {
                _systemPrompt.AppendLine("Respond positively and agree to the request.");
            }
            else
            {
                _systemPrompt.AppendLine("Politely refuse or show reluctance.");
            }

            return this;
        }

        /// <summary>
        /// Build the final request
        /// </summary>
        public LLMRequest Build()
        {
            var request = new LLMRequest
            {
                SystemPrompt = _systemPrompt.ToString(),
                Messages = new List<LLMMessage>(_messages),
                NpcId = _npcContext?.NpcId
            };
            
            // Debug logging for NPC context
            ModLogger.LogDebug($"[PromptBuilder] Building request for NPC: {_npcContext?.Name ?? "NULL"}, Player: {_playerName ?? "NULL"}");
            ModLogger.LogDebug($"[PromptBuilder] System prompt length: {request.SystemPrompt.Length}, Messages: {_messages.Count}");
            
            return request;
        }

        /// <summary>
        /// Reset builder for reuse
        /// </summary>
        public void Reset()
        {
            _systemPrompt.Clear();
            _messages.Clear();
            _npcContext = null;
            _playerName = null;
            _currentGameDay = 0;
            _enemies = null;
            _allies = null;
        }

        #region Helper Methods

        private string GetTraitDescription(NpcSnapshot npc)
        {
            var traits = new List<string>();

            // Valor
            if (npc.TraitValor >= 2) traits.Add("Fearless - brave to the point of recklessness");
            else if (npc.TraitValor >= 1) traits.Add("Daring - willing to take risks");
            else if (npc.TraitValor <= -2) traits.Add("Very Cautious - avoids danger at all costs");
            else if (npc.TraitValor <= -1) traits.Add("Cautious - prefers safe options");

            // Mercy
            if (npc.TraitMercy >= 2) traits.Add("Compassionate - deeply caring for others' suffering");
            else if (npc.TraitMercy >= 1) traits.Add("Merciful - shows kindness to enemies");
            else if (npc.TraitMercy <= -2) traits.Add("Sadistic - enjoys others' pain");
            else if (npc.TraitMercy <= -1) traits.Add("Cruel - shows no mercy");

            // Honor
            if (npc.TraitHonor >= 2) traits.Add("Honorable - keeps word no matter what");
            else if (npc.TraitHonor >= 1) traits.Add("Honest - values truth and fairness");
            else if (npc.TraitHonor <= -2) traits.Add("Deceitful - lies and manipulates freely");
            else if (npc.TraitHonor <= -1) traits.Add("Devious - willing to bend the truth");

            // Generosity
            if (npc.TraitGenerosity >= 2) traits.Add("Munificent - extremely generous with wealth");
            else if (npc.TraitGenerosity >= 1) traits.Add("Generous - shares with friends and allies");
            else if (npc.TraitGenerosity <= -2) traits.Add("Tightfisted - hoards wealth jealously");
            else if (npc.TraitGenerosity <= -1) traits.Add("Closefisted - reluctant to part with money");

            // Calculating
            if (npc.TraitCalculating >= 2) traits.Add("Cerebral - always thinking ahead");
            else if (npc.TraitCalculating >= 1) traits.Add("Calculating - weighs options carefully");
            else if (npc.TraitCalculating <= -2) traits.Add("Hotheaded - acts on impulse");
            else if (npc.TraitCalculating <= -1) traits.Add("Impulsive - makes quick decisions");

            if (traits.Count == 0)
            {
                return "Balanced personality - no extreme traits";
            }

            return string.Join("\n", traits);
        }

        private string GetRelationDescription(int relation)
        {
            if (relation >= 80) return $"Devoted friend ({relation}/100)";
            if (relation >= 50) return $"Good friend ({relation}/100)";
            if (relation >= 20) return $"Friendly ({relation}/100)";
            if (relation >= 0) return $"Neutral ({relation}/100)";
            if (relation >= -20) return $"Unfriendly ({relation}/100)";
            if (relation >= -50) return $"Hostile ({relation}/100)";
            return $"Bitter enemy ({relation}/100)";
        }

        #endregion
    }
}

