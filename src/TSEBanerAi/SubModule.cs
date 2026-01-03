using System;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TSEBanerAi.Context;
using TSEBanerAi.Dialogue;
using TSEBanerAi.LLM;
using TSEBanerAi.Storage;
using TSEBanerAi.UI;
using TSEBanerAi.UI.Overlay;
using TSEBanerAi.Utils;

namespace TSEBanerAi
{
    /// <summary>
    /// Main entry point for TSEBanerAi mod
    /// </summary>
    public class SubModule : MBSubModuleBase
    {
        public const string ModuleName = "TSEBanerAi";
        public const string ModuleVersion = "0.1.0";

        private OverlayChatWindow _chatWindow;
        private bool _chatInitialized;
        private bool _wasInConversation = false;
        private string _currentConversationNpcName = string.Empty;
        private Hero _currentConversationHero = null;
        private bool _systemsInitialized = false;

        /// <summary>
        /// Called when the module is loaded for the first time.
        /// </summary>
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            try
            {
                ModLogger.LogDebug("=== TSEBanerAi OnSubModuleLoad START ===");
                InformationManager.DisplayMessage(new InformationMessage("[TSEBanerAi] Module loading...", Colors.Cyan));
                ModLogger.LogDebug("=== TSEBanerAi OnSubModuleLoad END ===");
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed in OnSubModuleLoad", ex);
                InformationManager.DisplayMessage(new InformationMessage($"[TSEBanerAi ERROR] Load failed: {ex.Message}", Colors.Red));
            }
        }

        /// <summary>
        /// Called when starting a new game.
        /// </summary>
        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);

            try
            {
                ModLogger.LogDebug("=== TSEBanerAi OnGameStart START ===");
                InformationManager.DisplayMessage(new InformationMessage("[TSEBanerAi] Game starting...", Colors.Cyan));

                // Initialize chat system after game starts
                InitializeChatSystem();

                ModLogger.LogDebug("=== TSEBanerAi OnGameStart END ===");
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed in OnGameStart", ex);
                InformationManager.DisplayMessage(new InformationMessage($"[TSEBanerAi ERROR] Game start failed: {ex.Message}", Colors.Red));
            }
        }

        /// <summary>
        /// Initialize chat system
        /// </summary>
        private void InitializeChatSystem()
        {
            try
            {
                ModLogger.LogDebug("=== InitializeChatSystem START ===");

                if (_chatInitialized)
                {
                    ModLogger.LogDebug("Chat already initialized, skipping");
                    return;
                }

                // Create overlay chat window
                ModLogger.LogDebug("Creating new OverlayChatWindow instance...");
                _chatWindow = new OverlayChatWindow();
                ModLogger.LogDebug($"OverlayChatWindow created: {_chatWindow != null}");

                // Initialize chat window
                ModLogger.LogDebug("Initializing OverlayChatWindow...");
                if (!_chatWindow.Initialize())
                {
                    ModLogger.LogError("Failed to initialize OverlayChatWindow");
                    InformationManager.DisplayMessage(new InformationMessage("[TSEBanerAi] Chat overlay failed to initialize", Colors.Red));
                    return;
                }

                // Initialize ChatManager
                ModLogger.LogDebug("Initializing ChatManager...");
                ChatManager.Initialize(_chatWindow);
                ModLogger.LogDebug("ChatManager.Initialize() called");

                _chatInitialized = true;
                InformationManager.DisplayMessage(new InformationMessage("[TSEBanerAi] Chat overlay ready! Press F10 to open", Colors.Green));
                ModLogger.LogDebug("=== InitializeChatSystem END ===");
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to initialize chat system", ex);
                InformationManager.DisplayMessage(new InformationMessage($"[TSEBanerAi ERROR] Chat init failed: {ex.Message}", Colors.Red));
            }
        }

        /// <summary>
        /// Initialize core systems (Storage, LLM, Context) - called when campaign is ready
        /// </summary>
        private async void InitializeCoreSystemsAsync()
        {
            if (_systemsInitialized) return;

            try
            {
                ModLogger.LogDebug("=== InitializeCoreSystemsAsync START ===");

                // Initialize Storage
                ModLogger.LogDebug("Initializing StorageManager...");
                if (!StorageManager.Instance.Initialize())
                {
                    ModLogger.LogError("Failed to initialize StorageManager");
                    InformationManager.DisplayMessage(new InformationMessage("[TSEBanerAi] Database init failed!", Colors.Red));
                }
                else
                {
                    ModLogger.LogDebug($"StorageManager initialized. Campaign ID: {StorageManager.Instance.CurrentCampaignId}");
                }

                // Initialize LLM
                ModLogger.LogDebug("Initializing LLMManager...");
                if (!await LLMManager.Instance.InitializeAsync())
                {
                    ModLogger.LogWarning("LLM initialization failed - will use fallback responses");
                    InformationManager.DisplayMessage(new InformationMessage("[TSEBanerAi] LLM not available - using fallback", Colors.Yellow));
                }
                else
                {
                    ModLogger.LogDebug($"LLMManager initialized. Provider: {LLMManager.Instance.CurrentProviderName}");
                    InformationManager.DisplayMessage(new InformationMessage($"[TSEBanerAi] LLM ready: {LLMManager.Instance.CurrentProviderName}", Colors.Green));
                }

                // Initialize Entity Index
                ModLogger.LogDebug("Refreshing EntityIndex...");
                EntityIndex.Instance.Refresh(true);

                _systemsInitialized = true;
                ModLogger.LogDebug("=== InitializeCoreSystemsAsync END ===");
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to initialize core systems", ex);
            }
        }

        /// <summary>
        /// Called every frame.
        /// </summary>
        protected override void OnApplicationTick(float dt)
        {
            base.OnApplicationTick(dt);

            try
            {
                if (!_chatInitialized || _chatWindow == null)
                    return;

                // Check for conversation state changes
                HandleConversationState();

                // Update chat window
                _chatWindow.Update(dt);

                // Render if visible
                if (_chatWindow.IsVisible)
                {
                    _chatWindow.Render();
                }

                // Check for F10 key to toggle chat (for manual testing)
                if (TaleWorlds.InputSystem.Input.IsKeyPressed(TaleWorlds.InputSystem.InputKey.F10))
                {
                    if (_chatWindow.IsVisible)
                    {
                        _chatWindow.Hide();
                        InformationManager.DisplayMessage(new InformationMessage("[TSEBanerAi] Chat hidden", Colors.Gray));
                    }
                    else
                    {
                        _chatWindow.Show("Test NPC");
                        InformationManager.DisplayMessage(new InformationMessage("[TSEBanerAi] Chat shown", Colors.Green));
                    }
                }
            }
            catch (Exception ex)
            {
                // Only log occasionally to avoid spam
                ModLogger.LogException("Error in OnApplicationTick", ex);
            }
        }

        /// <summary>
        /// Handle conversation start/end to auto-open/close chat
        /// </summary>
        private void HandleConversationState()
        {
            try
            {
                // Check if we're in a campaign
                if (Campaign.Current == null)
                    return;

                // Initialize core systems once campaign is ready
                if (!_systemsInitialized && Campaign.Current.MainParty != null)
                {
                    InitializeCoreSystemsAsync();
                }

                // Check if conversation is in progress
                bool isInConversation = Campaign.Current.ConversationManager != null &&
                                        Campaign.Current.ConversationManager.IsConversationInProgress;

                // Conversation just started
                if (isInConversation && !_wasInConversation)
                {
                    // Get the NPC we're talking to
                    var conversationCharacter = CharacterObject.OneToOneConversationCharacter;
                    if (conversationCharacter != null)
                    {
                        _currentConversationNpcName = conversationCharacter.Name?.ToString() ?? "Unknown";
                        _currentConversationHero = conversationCharacter.HeroObject;
                        
                        // Start dialogue with NPC
                        if (_currentConversationHero != null)
                        {
                            DialogueHandler.Instance.StartConversation(_currentConversationHero);
                        }
                        
                        // Open chat with NPC name
                        if (!_chatWindow.IsVisible)
                        {
                            _chatWindow.Show(_currentConversationNpcName);
                            ModLogger.LogDebug($"Auto-opened chat for conversation with: {_currentConversationNpcName}");
                        }
                    }
                }
                // Conversation just ended
                else if (!isInConversation && _wasInConversation)
                {
                    // End dialogue
                    DialogueHandler.Instance.EndConversation();
                    
                    // Close chat when conversation ends
                    if (_chatWindow.IsVisible)
                    {
                        _chatWindow.Hide();
                        ModLogger.LogDebug($"Auto-closed chat after conversation with: {_currentConversationNpcName}");
                    }
                    _currentConversationNpcName = string.Empty;
                    _currentConversationHero = null;
                }

                _wasInConversation = isInConversation;
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Error in HandleConversationState", ex);
            }
        }

        /// <summary>
        /// Called when module is unloaded
        /// </summary>
        protected override void OnSubModuleUnloaded()
        {
            base.OnSubModuleUnloaded();

            try
            {
                ModLogger.LogDebug("=== TSEBanerAi OnSubModuleUnloaded START ===");
                
                // Shutdown storage
                StorageManager.Instance.Shutdown();
                
                if (_chatWindow != null)
                {
                    _chatWindow.Dispose();
                    _chatWindow = null;
                }
                _chatInitialized = false;
                _systemsInitialized = false;

                ModLogger.LogDebug("=== TSEBanerAi OnSubModuleUnloaded END ===");
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed in OnSubModuleUnloaded", ex);
            }
        }
    }
}
