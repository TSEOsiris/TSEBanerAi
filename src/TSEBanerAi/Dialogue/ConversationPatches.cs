using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TSEBanerAi.UI;
using TSEBanerAi.Utils;

namespace TSEBanerAi.Dialogue
{
    /// <summary>
    /// Harmony patches for conversation system
    /// </summary>
    [HarmonyPatch]
    public static class ConversationPatches
    {
        /// <summary>
        /// Patch SetupAndStartMapConversation to open chat overlay
        /// </summary>
        [HarmonyPatch(typeof(ConversationManager), "SetupAndStartMapConversation")]
        [HarmonyPostfix]
        public static void SetupAndStartMapConversation_Postfix(ConversationManager __instance)
        {
            try
            {
                ModLogger.LogDebug("=== ConversationPatches.SetupAndStartMapConversation START ===");

                // Try to get current conversation partner using reflection
                Hero? npc = null;
                try
                {
                    // Try to get conversation partner from ConversationManager instance
                    var conversationPartnerField = typeof(ConversationManager).GetField("_conversationPartner", 
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    if (conversationPartnerField != null)
                    {
                        var conversationPartner = conversationPartnerField.GetValue(__instance);
                        if (conversationPartner != null)
                        {
                            // Try to get Hero property
                            var heroProperty = conversationPartner.GetType().GetProperty("Hero");
                            if (heroProperty != null)
                            {
                                npc = heroProperty.GetValue(conversationPartner) as Hero;
                            }
                        }
                    }

                    // Alternative: try to get from OneToOneConversationCharacter
                    if (npc == null)
                    {
                        var oneToOneField = typeof(ConversationManager).GetField("_oneToOneConversationCharacter",
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        if (oneToOneField != null)
                        {
                            var oneToOne = oneToOneField.GetValue(__instance);
                            if (oneToOne != null)
                            {
                                var heroProperty = oneToOne.GetType().GetProperty("Hero");
                                if (heroProperty != null)
                                {
                                    npc = heroProperty.GetValue(oneToOne) as Hero;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ModLogger.LogException("Failed to get Hero from conversation partner", ex);
                }

                if (npc != null)
                {
                    ModLogger.LogDebug($"Attempting to open chat with {npc.Name} (from SetupAndStartMapConversation)");
                    ChatManager.Instance?.OpenChat(npc);
                }
                else
                {
                    ModLogger.LogDebug("Could not get Hero from conversation partner - trying alternative method");
                    // Fallback: try to get from Campaign.CurrentConversationManager
                    try
                    {
                        var campaignType = typeof(Campaign);
                        var currentConversationManagerProperty = campaignType.GetProperty("CurrentConversationManager",
                            BindingFlags.Public | BindingFlags.Static);
                        if (currentConversationManagerProperty != null)
                        {
                            var conversationManager = currentConversationManagerProperty.GetValue(null) as ConversationManager;
                            if (conversationManager != null)
                            {
                                // Try same reflection approach
                                var conversationPartnerField = typeof(ConversationManager).GetField("_conversationPartner",
                                    BindingFlags.NonPublic | BindingFlags.Instance);
                                if (conversationPartnerField != null)
                                {
                                    var conversationPartner = conversationPartnerField.GetValue(conversationManager);
                                    if (conversationPartner != null)
                                    {
                                        var heroProperty = conversationPartner.GetType().GetProperty("Hero");
                                        if (heroProperty != null)
                                        {
                                            npc = heroProperty.GetValue(conversationPartner) as Hero;
                                            if (npc != null)
                                            {
                                                ModLogger.LogDebug($"Found NPC via fallback: {npc.Name}");
                                                ChatManager.Instance?.OpenChat(npc);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        ModLogger.LogException("Failed fallback method", ex);
                    }
                }

                ModLogger.LogDebug("=== ConversationPatches.SetupAndStartMapConversation END ===");
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Error in SetupAndStartMapConversation patch", ex);
            }
        }
    }
}


