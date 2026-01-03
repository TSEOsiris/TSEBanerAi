using System;
using System.Threading;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TSEBanerAi.Context;
using TSEBanerAi.LLM;
using TSEBanerAi.Storage;
using TSEBanerAi.Storage.Models;
using TSEBanerAi.Utils;

namespace TSEBanerAi.Dialogue
{
    /// <summary>
    /// Handles dialogue between player and NPCs through LLM
    /// </summary>
    public class DialogueHandler
    {
        private static DialogueHandler _instance;
        private static readonly object _lock = new object();

        private Hero _currentNpc;
        private NpcSnapshot _currentNpcSnapshot;
        private CancellationTokenSource _currentRequestCts;
        private bool _isProcessing;

        /// <summary>
        /// Event fired when LLM response is received
        /// </summary>
        public event Action<DialogueResponse> OnResponseReceived;

        /// <summary>
        /// Event fired when LLM starts processing
        /// </summary>
        public event Action OnProcessingStarted;

        /// <summary>
        /// Event fired when processing ends (success or failure)
        /// </summary>
        public event Action OnProcessingEnded;

        /// <summary>
        /// Singleton instance
        /// </summary>
        public static DialogueHandler Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new DialogueHandler();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Current NPC in conversation
        /// </summary>
        public Hero CurrentNpc => _currentNpc;

        /// <summary>
        /// Whether currently processing a request
        /// </summary>
        public bool IsProcessing => _isProcessing;

        private DialogueHandler() { }

        /// <summary>
        /// Start conversation with NPC
        /// </summary>
        public void StartConversation(Hero npc)
        {
            if (npc == null) return;

            _currentNpc = npc;
            _currentNpcSnapshot = GameContextBuilder.Instance.GetNpcSnapshot(npc, true);

            // Debug: log NPC context
            if (_currentNpcSnapshot != null)
            {
                ModLogger.LogDebug($"Started conversation with: {npc.Name}");
                ModLogger.LogDebug($"[DialogueHandler] NPC Snapshot: Name={_currentNpcSnapshot.Name}, " +
                    $"Culture={_currentNpcSnapshot.Culture}, Clan={_currentNpcSnapshot.ClanName}, " +
                    $"Kingdom={_currentNpcSnapshot.KingdomName}, Relation={_currentNpcSnapshot.RelationWithPlayer}");
            }
            else
            {
                ModLogger.LogWarning($"Started conversation with {npc.Name} but NPC snapshot is NULL!");
            }
        }

        /// <summary>
        /// End current conversation
        /// </summary>
        public void EndConversation()
        {
            CancelCurrentRequest();
            _currentNpc = null;
            _currentNpcSnapshot = null;

            ModLogger.LogDebug("Ended conversation");
        }

        /// <summary>
        /// Send player message and get LLM response
        /// </summary>
        public async Task<DialogueResponse> SendMessageAsync(string playerMessage)
        {
            if (_currentNpc == null)
            {
                return new DialogueResponse { Success = false, Error = "No active conversation" };
            }

            if (string.IsNullOrWhiteSpace(playerMessage))
            {
                return new DialogueResponse { Success = false, Error = "Empty message" };
            }

            if (_isProcessing)
            {
                return new DialogueResponse { Success = false, Error = "Already processing" };
            }

            try
            {
                _isProcessing = true;
                OnProcessingStarted?.Invoke();

                // Cancel any previous request
                CancelCurrentRequest();
                _currentRequestCts = new CancellationTokenSource();
                var cancellationToken = _currentRequestCts.Token;

                // Save player message to DB
                SavePlayerMessage(playerMessage);

                // Build prompt
                var prompt = BuildPrompt(playerMessage);

                // Send to LLM
                var llmResponse = await LLMManager.Instance.GenerateAsync(prompt, cancellationToken);

                // Process response
                var response = ProcessResponse(llmResponse, playerMessage);

                // Save NPC response to DB
                SaveNpcResponse(response);

                OnResponseReceived?.Invoke(response);
                return response;
            }
            catch (OperationCanceledException)
            {
                return new DialogueResponse { Success = false, Error = "Request cancelled" };
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to send message", ex);
                return new DialogueResponse { Success = false, Error = ex.Message };
            }
            finally
            {
                _isProcessing = false;
                OnProcessingEnded?.Invoke();
            }
        }

        /// <summary>
        /// Cancel current LLM request
        /// </summary>
        public void CancelCurrentRequest()
        {
            if (_currentRequestCts != null && !_currentRequestCts.IsCancellationRequested)
            {
                _currentRequestCts.Cancel();
                _currentRequestCts.Dispose();
                _currentRequestCts = null;
            }
        }

        /// <summary>
        /// Build LLM prompt for current conversation
        /// </summary>
        private LLMRequest BuildPrompt(string playerMessage)
        {
            var builder = LLMManager.Instance.CreatePromptBuilder();
            var storage = StorageManager.Instance;
            var contextBuilder = GameContextBuilder.Instance;

            // Set basic context
            builder.WithPlayerName(contextBuilder.GetPlayerName())
                   .WithGameDay(contextBuilder.GetCurrentGameDay())
                   .WithNpcContext(_currentNpcSnapshot);

            // Add diplomacy context (enemies/allies)
            if (_currentNpc != null)
            {
                var enemies = contextBuilder.GetEnemyKingdoms(_currentNpc);
                var allies = contextBuilder.GetAlliedKingdoms(_currentNpc);
                builder.WithEnemies(enemies).WithAllies(allies);
                
                ModLogger.LogDebug($"[DialogueHandler] Diplomacy - Enemies: [{string.Join(", ", enemies)}], Allies: [{string.Join(", ", allies)}]");
            }

            // Build system prompt with command capabilities
            builder.BuildCommandSystemPrompt();

            // Add chat history if available
            if (storage.IsInitialized && _currentNpc != null)
            {
                var history = storage.Chat.GetChatHistory(
                    storage.CurrentCampaignId, 
                    _currentNpc.StringId,
                    LLMManager.Instance.Settings.MaxHistoryMessages
                );
                builder.WithChatHistory(history);

                // Add NPC memories
                var memories = storage.Npcs.GetActiveMemories(
                    storage.CurrentCampaignId,
                    _currentNpc.StringId,
                    storage.GetCurrentGameDay()
                );
                builder.WithMemories(memories);
            }

            // Add current message
            builder.WithPlayerMessage(playerMessage);

            return builder.Build();
        }

        /// <summary>
        /// Process LLM response and extract commands/dice requests
        /// </summary>
        private DialogueResponse ProcessResponse(LLMResponse llmResponse, string playerMessage)
        {
            var response = new DialogueResponse
            {
                Success = llmResponse.Success,
                Content = llmResponse.Content,
                Error = llmResponse.Error,
                TokensUsed = llmResponse.TotalTokens,
                ResponseTimeMs = llmResponse.ResponseTimeMs,
                IsFallback = llmResponse.IsFallback,
                Provider = llmResponse.Provider
            };

            if (!llmResponse.Success)
            {
                return response;
            }

            // Try to parse command from response
            response.Command = ResponseParser.ParseCommand(llmResponse.Content);
            
            // Try to parse dice request
            response.DiceRequest = ResponseParser.ParseDiceRequest(llmResponse.Content);

            // Clean the display content (remove JSON blocks)
            response.DisplayContent = ResponseParser.CleanContent(llmResponse.Content);

            return response;
        }

        /// <summary>
        /// Save player message to database
        /// </summary>
        private void SavePlayerMessage(string message)
        {
            if (_currentNpc == null) return;

            try
            {
                // Check storage availability safely (SQLite may not be loaded)
                var storage = StorageManager.Instance;
                if (!storage.IsInitialized) return;
                
                var chatMessage = new ChatMessage
                {
                    CampaignId = storage.CurrentCampaignId,
                    NpcId = _currentNpc.StringId,
                    NpcName = _currentNpc.Name?.ToString() ?? "Unknown",
                    IsPlayerMessage = true,
                    Content = message,
                    GameDay = storage.GetCurrentGameDay(),
                    Timestamp = DateTime.UtcNow,
                    LocationId = _currentNpc.CurrentSettlement?.StringId
                };

                storage.Chat.SaveMessage(chatMessage);
            }
            catch (Exception ex)
            {
                // SQLite not available - silently skip saving (LLM will still work)
                ModLogger.LogDebug($"Storage not available, skipping message save: {ex.Message}");
            }
        }

        /// <summary>
        /// Save NPC response to database
        /// </summary>
        private void SaveNpcResponse(DialogueResponse response)
        {
            if (_currentNpc == null) return;

            try
            {
                // Check storage availability safely (SQLite may not be loaded)
                var storage = StorageManager.Instance;
                if (!storage.IsInitialized) return;
                
                var chatMessage = new ChatMessage
                {
                    CampaignId = storage.CurrentCampaignId,
                    NpcId = _currentNpc.StringId,
                    NpcName = _currentNpc.Name?.ToString() ?? "Unknown",
                    IsPlayerMessage = false,
                    Content = response.DisplayContent ?? response.Content,
                    GameDay = storage.GetCurrentGameDay(),
                    Timestamp = DateTime.UtcNow,
                    LocationId = _currentNpc.CurrentSettlement?.StringId,
                    TokensUsed = response.TokensUsed,
                    ResponseTimeMs = response.ResponseTimeMs
                };

                // Add command JSON if present
                if (response.Command != null)
                {
                    chatMessage.CommandJson = Newtonsoft.Json.JsonConvert.SerializeObject(response.Command);
                }

                // Add dice info if present
                if (response.DiceRequest != null)
                {
                    chatMessage.DiceDC = response.DiceRequest.DC;
                }

                storage.Chat.SaveMessage(chatMessage);
            }
            catch (Exception ex)
            {
                // SQLite not available - silently skip saving
                ModLogger.LogDebug($"Storage not available, skipping response save: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Response from dialogue handler
    /// </summary>
    public class DialogueResponse
    {
        public bool Success { get; set; }
        public string Content { get; set; }
        public string DisplayContent { get; set; }
        public string Error { get; set; }
        public int TokensUsed { get; set; }
        public int ResponseTimeMs { get; set; }
        public bool IsFallback { get; set; }
        public string Provider { get; set; }
        public GameCommand Command { get; set; }
        public DiceRequest DiceRequest { get; set; }
    }

    /// <summary>
    /// Game command extracted from LLM response
    /// </summary>
    public class GameCommand
    {
        public string CommandType { get; set; }
        public string Target { get; set; }
        public int? Amount { get; set; }
        public string RawJson { get; set; }
    }

    /// <summary>
    /// Dice roll request from LLM
    /// </summary>
    public class DiceRequest
    {
        public string Skill { get; set; }
        public int DC { get; set; }
        public string Reason { get; set; }
    }
}

