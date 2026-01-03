using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TSEBanerAi.Context;
using TSEBanerAi.Utils;

namespace TSEBanerAi.Commands
{
    /// <summary>
    /// Command for NPC to patrol around a settlement
    /// </summary>
    public class PatrolCommand : IGameCommand
    {
        public string CommandType => "patrol";
        public string Description => "NPC patrols around a specified settlement";

        public bool CanExecute(Hero npc, CommandContext context)
        {
            if (npc == null) return false;
            if (!npc.IsAlive) return false;
            if (npc.PartyBelongedTo == null) return false;

            var party = npc.PartyBelongedTo;
            if (party == null || !party.IsActive) return false;
            if (party.LeaderHero != npc) return false;

            // Must have a target settlement
            if (string.IsNullOrEmpty(context?.Target)) return false;

            // Check relation threshold
            int relation = (int)npc.GetRelationWithPlayer();
            if (relation < 0 && !context.WasDiceSuccess) return false;

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

                // Find target settlement
                var settlement = GameContextBuilder.Instance.FindSettlementByName(context.Target);
                if (settlement == null)
                {
                    return CommandResult.Fail($"Cannot find settlement: {context.Target}");
                }

                var party = npc.PartyBelongedTo;

                // Set AI to patrol around settlement
                party.Ai.SetMovePatrolAroundSettlement(settlement);

                ModLogger.LogDebug($"NPC {npc.Name} is now patrolling around {settlement.Name}");

                CreateMemory(npc, $"Agreed to patrol around {settlement.Name}");

                return CommandResult.Ok($"{npc.Name} will patrol around {settlement.Name}.");
            }
            catch (System.Exception ex)
            {
                ModLogger.LogException($"Failed to execute patrol command", ex);
                return CommandResult.Fail($"Command failed: {ex.Message}");
            }
        }

        public string GetCannotExecuteReason(Hero npc, CommandContext context)
        {
            if (npc == null) return "No NPC specified";
            if (!npc.IsAlive) return $"{npc.Name} is dead";
            if (npc.PartyBelongedTo == null) return $"{npc.Name} has no party";
            if (npc.PartyBelongedTo.LeaderHero != npc) return $"{npc.Name} is not the party leader";
            if (string.IsNullOrEmpty(context?.Target)) return "No target settlement specified";

            int relation = (int)npc.GetRelationWithPlayer();
            if (relation < 0) return $"{npc.Name} doesn't trust you enough (relation: {relation})";

            return "Unknown reason";
        }

        private void CreateMemory(Hero npc, string description)
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
                    Description = description,
                    Sentiment = 5,
                    GameDay = storage.GetCurrentGameDay(),
                    CreatedAt = System.DateTime.UtcNow,
                    IsActive = true
                };

                storage.Npcs.SaveMemory(memory);
            }
            catch { }
        }
    }
}

