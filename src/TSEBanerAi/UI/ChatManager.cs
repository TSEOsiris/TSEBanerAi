using System;
using TaleWorlds.CampaignSystem;
using TSEBanerAi.UI.Overlay;
using TSEBanerAi.Utils;

namespace TSEBanerAi.UI
{
    /// <summary>
    /// Manages chat system and overlay window
    /// </summary>
    public class ChatManager
    {
        private static ChatManager? _instance;
        private OverlayChatWindow? _chatWindow;
        private Hero? _currentNPC;

        public static ChatManager? Instance => _instance;

        private ChatManager()
        {
        }

        /// <summary>
        /// Initialize chat system
        /// </summary>
        public static void Initialize(OverlayChatWindow chatWindow)
        {
            try
            {
                ModLogger.LogDebug("=== ChatManager.Initialize START ===");
                ModLogger.LogDebug($"chatWindow parameter: {chatWindow != null}");
                ModLogger.LogDebug($"chatWindow type: {chatWindow?.GetType().FullName}");

                if (_instance == null)
                {
                    _instance = new ChatManager();
                }

                _instance._chatWindow = chatWindow;
                ModLogger.LogDebug($"_chatWindow field set: {_instance._chatWindow != null}");
                ModLogger.LogDebug($"_chatWindow type: {_instance._chatWindow?.GetType().FullName}");

                if (_instance._chatWindow == null || !_instance._chatWindow.Initialize())
                {
                    ModLogger.LogError("Failed to initialize chat window");
                    return;
                }

                ModLogger.LogDebug("=== ChatManager.Initialize END ===");
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to initialize ChatManager", ex);
            }
        }

        /// <summary>
        /// Open chat with NPC
        /// </summary>
        public void OpenChat(Hero npc)
        {
            try
            {
                ModLogger.LogDebug("=== ChatManager.OpenChat START ===");
                ModLogger.LogDebug($"npc parameter: {npc != null}");
                ModLogger.LogDebug($"npc type: {npc?.GetType().FullName}");
                ModLogger.LogDebug($"NPC name: {npc?.Name}");

                if (_chatWindow == null)
                {
                    ModLogger.LogError("Chat window not initialized");
                    return;
                }

                ModLogger.LogDebug($"_chatWindow field: {_chatWindow != null}");
                ModLogger.LogDebug($"_chatWindow type: {_chatWindow?.GetType().FullName}");

                _currentNPC = npc;
                ModLogger.LogDebug($"Setting _currentNPC to: {npc?.Name}");

                // Load chat history (simplified)
                ModLogger.LogDebug("Loading chat history...");
                // TODO: Load from storage

                ModLogger.LogDebug("Calling _chatWindow.Show()...");
                _chatWindow!.Show(npc?.Name?.ToString() ?? "NPC");
                ModLogger.LogDebug("_chatWindow.Show() completed");
                ModLogger.LogDebug("=== ChatManager.OpenChat END ===");
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to open chat", ex);
            }
        }

        /// <summary>
        /// Close chat
        /// </summary>
        public void CloseChat()
        {
            try
            {
                _chatWindow?.Hide();
                _currentNPC = null;
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to close chat", ex);
            }
        }

        /// <summary>
        /// Add message to chat
        /// </summary>
        public void AddMessage(string text, bool isPlayer)
        {
            try
            {
                _chatWindow?.AddMessage(text, isPlayer);
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to add message", ex);
            }
        }

        /// <summary>
        /// Clear messages
        /// </summary>
        public void ClearMessages()
        {
            try
            {
                _chatWindow?.ClearMessages();
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to clear messages", ex);
            }
        }
    }
}


