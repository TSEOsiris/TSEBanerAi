using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TSEBanerAi.Utils;

namespace TSEBanerAi.Commands
{
    /// <summary>
    /// Command to change relation between NPC and player
    /// </summary>
    public class ChangeRelationCommand : IGameCommand
    {
        public string CommandType => "change_relation";
        public string Description => "Changes relation between NPC and player";

        public bool CanExecute(Hero npc, CommandContext context)
        {
            if (npc == null) return false;
            if (!npc.IsAlive) return false;
            if (!context.Amount.HasValue) return false;

            // Dice roll usually required for relation changes
            int amount = context.Amount.Value;
            if (Math.Abs(amount) > 5 && !context.WasDiceSuccess) return false;

            return true;
        }

        public CommandResult Execute(Hero npc, CommandContext context)
        {
            try
            {
                if (!CanExecute(npc, context))
                {
                    return CommandResult.Fail(GetCannotExecuteReason(npc, context));
                }

                int amount = context.Amount ?? 0;
                int currentRelation = (int)npc.GetRelationWithPlayer();
                
                // Use game's relation change action
                ChangeRelationAction.ApplyPlayerRelation(npc, amount);

                int newRelation = (int)npc.GetRelationWithPlayer();
                string direction = amount > 0 ? "improved" : "worsened";

                ModLogger.LogDebug($"Relation with {npc.Name} changed: {currentRelation} -> {newRelation} ({amount:+#;-#;0})");

                // Create memory
                CreateMemory(npc, amount, currentRelation, newRelation);

                return CommandResult.Ok($"Relation with {npc.Name} {direction} by {Math.Abs(amount)} (now {newRelation}).");
            }
            catch (Exception ex)
            {
                ModLogger.LogException($"Failed to execute change_relation command", ex);
                return CommandResult.Fail($"Command failed: {ex.Message}");
            }
        }

        public string GetCannotExecuteReason(Hero npc, CommandContext context)
        {
            if (npc == null) return "No NPC specified";
            if (!npc.IsAlive) return $"{npc.Name} is dead";
            if (!context.Amount.HasValue) return "No amount specified";

            int amount = context.Amount.Value;
            if (Math.Abs(amount) > 5) return "Large relation changes require a successful skill check";

            return "Unknown reason";
        }

        private void CreateMemory(Hero npc, int amount, int oldRelation, int newRelation)
        {
            try
            {
                var storage = Storage.StorageManager.Instance;
                if (!storage.IsInitialized) return;

                string description;
                int sentiment;

                if (amount > 10)
                {
                    description = "Had a very positive interaction";
                    sentiment = 20;
                }
                else if (amount > 0)
                {
                    description = "Had a positive conversation";
                    sentiment = 10;
                }
                else if (amount < -10)
                {
                    description = "Had a very negative interaction";
                    sentiment = -20;
                }
                else
                {
                    description = "Had an unpleasant exchange";
                    sentiment = -10;
                }

                var memory = new Storage.Models.NpcMemory
                {
                    CampaignId = storage.CurrentCampaignId,
                    NpcId = npc.StringId,
                    MemoryType = Storage.Models.MemoryTypes.Command,
                    Description = description,
                    Sentiment = sentiment,
                    GameDay = storage.GetCurrentGameDay(),
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                storage.Npcs.SaveMemory(memory);

                // Also log as event
                var gameEvent = new Storage.Models.GameEvent
                {
                    CampaignId = storage.CurrentCampaignId,
                    EventType = Storage.Models.EventTypes.RelationChanged,
                    PrimaryEntityId = npc.StringId,
                    Description = $"Relation with {npc.Name} changed from {oldRelation} to {newRelation}",
                    DataJson = Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        oldRelation,
                        newRelation,
                        change = amount
                    }),
                    GameDay = storage.GetCurrentGameDay(),
                    Timestamp = DateTime.UtcNow,
                    IsLlmGenerated = true,
                    WasDisplayed = true
                };

                storage.Events.SaveEvent(gameEvent);
            }
            catch { }
        }
    }
}

