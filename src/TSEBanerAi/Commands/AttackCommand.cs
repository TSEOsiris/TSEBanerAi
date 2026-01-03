using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TSEBanerAi.Context;
using TSEBanerAi.Utils;

namespace TSEBanerAi.Commands
{
    /// <summary>
    /// Command for NPC to attack a target (enemy party/lord)
    /// </summary>
    public class AttackCommand : IGameCommand
    {
        public string CommandType => "attack";
        public string Description => "NPC attacks a specified enemy";

        public bool CanExecute(Hero npc, CommandContext context)
        {
            if (npc == null) return false;
            if (!npc.IsAlive) return false;
            if (npc.PartyBelongedTo == null) return false;

            var party = npc.PartyBelongedTo;
            if (party == null || !party.IsActive) return false;
            if (party.LeaderHero != npc) return false;

            // Must have a target
            if (string.IsNullOrEmpty(context?.Target)) return false;

            // High relation or dice roll required
            int relation = (int)npc.GetRelationWithPlayer();
            if (relation < 20 && !context.WasDiceSuccess) return false;

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

                // Find target
                var targetHero = GameContextBuilder.Instance.FindHeroByName(context.Target);
                MobileParty targetParty = null;

                if (targetHero != null && targetHero.PartyBelongedTo != null)
                {
                    targetParty = targetHero.PartyBelongedTo;
                }
                else
                {
                    // Try to find party by leader name
                    targetParty = MobileParty.All
                        .FirstOrDefault(p => p.LeaderHero?.Name?.ToString()?.ToLower().Contains(context.Target.ToLower()) == true);
                }

                if (targetParty == null)
                {
                    return CommandResult.Fail($"Cannot find target: {context.Target}");
                }

                var party = npc.PartyBelongedTo;

                // Check if they can actually attack (are they at war?)
                if (npc.Clan?.Kingdom != null && targetParty.LeaderHero?.Clan?.Kingdom != null)
                {
                    if (!npc.Clan.Kingdom.IsAtWarWith(targetParty.LeaderHero.Clan.Kingdom))
                    {
                        return CommandResult.Fail($"Cannot attack {targetParty.LeaderHero?.Name?.ToString()} - not at war");
                    }
                }

                // Set AI to engage target
                party.Ai.SetMoveEngageParty(targetParty);

                string targetName = targetParty.LeaderHero?.Name?.ToString() ?? context.Target;
                ModLogger.LogDebug($"NPC {npc.Name} is now attacking {targetName}");

                CreateMemory(npc, $"Agreed to attack {targetName}");

                return CommandResult.Ok($"{npc.Name} will attack {targetName}.");
            }
            catch (System.Exception ex)
            {
                ModLogger.LogException($"Failed to execute attack command", ex);
                return CommandResult.Fail($"Command failed: {ex.Message}");
            }
        }

        public string GetCannotExecuteReason(Hero npc, CommandContext context)
        {
            if (npc == null) return "No NPC specified";
            if (!npc.IsAlive) return $"{npc.Name} is dead";
            if (npc.PartyBelongedTo == null) return $"{npc.Name} has no party";
            if (npc.PartyBelongedTo.LeaderHero != npc) return $"{npc.Name} is not the party leader";
            if (string.IsNullOrEmpty(context?.Target)) return "No target specified";

            int relation = (int)npc.GetRelationWithPlayer();
            if (relation < 20) return $"{npc.Name} doesn't trust you enough for this (relation: {relation})";

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
                    Sentiment = 0,
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

