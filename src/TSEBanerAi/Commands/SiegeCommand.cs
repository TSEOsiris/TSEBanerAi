using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TSEBanerAi.Context;
using TSEBanerAi.Utils;

namespace TSEBanerAi.Commands
{
    /// <summary>
    /// Command for NPC to besiege a settlement
    /// </summary>
    public class SiegeCommand : IGameCommand
    {
        public string CommandType => "siege";
        public string Description => "NPC besieges a settlement";

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

            // High relation required for siege (very risky operation)
            int relation = (int)npc.GetRelationWithPlayer();
            if (relation < 30 && !context.WasDiceSuccess) return false;

            // Need enough troops
            if (party.MemberRoster.TotalManCount < 100) return false;

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

                // Must be a town or castle
                if (!settlement.IsTown && !settlement.IsCastle)
                {
                    return CommandResult.Fail($"{settlement.Name} cannot be besieged (must be town or castle)");
                }

                var party = npc.PartyBelongedTo;

                // Check if they can actually besiege (are they at war with owner?)
                var ownerKingdom = settlement.OwnerClan?.Kingdom;
                if (ownerKingdom != null && npc.Clan?.Kingdom != null)
                {
                    if (!npc.Clan.Kingdom.IsAtWarWith(ownerKingdom))
                    {
                        return CommandResult.Fail($"Cannot besiege {settlement.Name} - not at war with {ownerKingdom.Name}");
                    }
                }

                // Set AI to besiege settlement
                party.Ai.SetMoveBesiegeSettlement(settlement);

                ModLogger.LogDebug($"NPC {npc.Name} is now besieging {settlement.Name}");

                CreateMemory(npc, $"Agreed to besiege {settlement.Name}");

                return CommandResult.Ok($"{npc.Name} will besiege {settlement.Name}.");
            }
            catch (System.Exception ex)
            {
                ModLogger.LogException($"Failed to execute siege command", ex);
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

            var party = npc.PartyBelongedTo;
            if (party.MemberRoster.TotalManCount < 100) return $"{npc.Name}'s party is too small for a siege";

            int relation = (int)npc.GetRelationWithPlayer();
            if (relation < 30) return $"{npc.Name} won't risk a siege for you (relation: {relation})";

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
                    MemoryType = Storage.Models.MemoryTypes.Combat,
                    Description = description,
                    Sentiment = -5, // Risky operation
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

