using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TSEBanerAi.Utils;

namespace TSEBanerAi.Commands
{
    /// <summary>
    /// Command for NPC to follow the player
    /// </summary>
    public class FollowCommand : IGameCommand
    {
        public string CommandType => "follow";
        public string Description => "NPC follows the player's party";

        public bool CanExecute(Hero npc, CommandContext context)
        {
            if (npc == null) return false;
            if (!npc.IsAlive) return false;
            if (npc.PartyBelongedTo == null) return false;
            
            // NPC must have a mobile party to follow
            var party = npc.PartyBelongedTo;
            if (party == null || !party.IsActive) return false;

            // NPC must be party leader or have control
            if (party.LeaderHero != npc) return false;

            // Check relation threshold (can be bypassed with dice roll)
            int relation = (int)npc.GetRelationWithPlayer();
            if (relation < -20 && !context.WasDiceSuccess) return false;

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

                var party = npc.PartyBelongedTo;
                var playerParty = MobileParty.MainParty;

                if (party == null || playerParty == null)
                {
                    return CommandResult.Fail("Cannot find parties");
                }

                // Set AI behavior to follow player
                party.Ai.SetMoveEscortParty(playerParty);

                ModLogger.LogDebug($"NPC {npc.Name} is now following player");

                // Create memory of this command
                CreateFollowMemory(npc, context);

                return CommandResult.Ok($"{npc.Name} will now follow you.");
            }
            catch (System.Exception ex)
            {
                ModLogger.LogException($"Failed to execute follow command for {npc?.Name}", ex);
                return CommandResult.Fail($"Command failed: {ex.Message}");
            }
        }

        public string GetCannotExecuteReason(Hero npc, CommandContext context)
        {
            if (npc == null) return "No NPC specified";
            if (!npc.IsAlive) return $"{npc.Name} is dead";
            if (npc.PartyBelongedTo == null) return $"{npc.Name} has no party";
            
            var party = npc.PartyBelongedTo;
            if (party.LeaderHero != npc) return $"{npc.Name} is not the party leader";

            int relation = (int)npc.GetRelationWithPlayer();
            if (relation < -20) return $"{npc.Name} dislikes you too much (relation: {relation})";

            return "Unknown reason";
        }

        private void CreateFollowMemory(Hero npc, CommandContext context)
        {
            try
            {
                var storage = Storage.StorageManager.Instance;
                if (!storage.IsInitialized) return;

                var memory = new Storage.Models.NpcMemory
                {
                    CampaignId = storage.CurrentCampaignId,
                    NpcId = npc.StringId,
                    MemoryType = Storage.Models.MemoryTypes.Command,
                    Description = "Agreed to follow the player",
                    Sentiment = 10, // Positive - shows trust
                    GameDay = storage.GetCurrentGameDay(),
                    CreatedAt = System.DateTime.UtcNow,
                    IsActive = true
                };

                storage.Npcs.SaveMemory(memory);
            }
            catch { }
        }
    }

    /// <summary>
    /// Command for NPC to stop following the player
    /// </summary>
    public class UnfollowCommand : IGameCommand
    {
        public string CommandType => "unfollow";
        public string Description => "NPC stops following the player";

        public bool CanExecute(Hero npc, CommandContext context)
        {
            if (npc == null) return false;
            if (!npc.IsAlive) return false;
            if (npc.PartyBelongedTo == null) return false;

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

                var party = npc.PartyBelongedTo;
                if (party == null)
                {
                    return CommandResult.Fail("Cannot find party");
                }

                // Clear AI behavior - party will go back to default behavior
                party.Ai.SetMoveModeHold();

                ModLogger.LogDebug($"NPC {npc.Name} stopped following player");

                return CommandResult.Ok($"{npc.Name} will no longer follow you.");
            }
            catch (System.Exception ex)
            {
                ModLogger.LogException($"Failed to execute unfollow command for {npc?.Name}", ex);
                return CommandResult.Fail($"Command failed: {ex.Message}");
            }
        }

        public string GetCannotExecuteReason(Hero npc, CommandContext context)
        {
            if (npc == null) return "No NPC specified";
            if (!npc.IsAlive) return $"{npc.Name} is dead";
            if (npc.PartyBelongedTo == null) return $"{npc.Name} has no party";

            return "Unknown reason";
        }
    }
}

