using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TSEBanerAi.Utils;

namespace TSEBanerAi.RAG
{
    /// <summary>
    /// Provides context about current diplomacy state
    /// </summary>
    public class DiplomacyContextProvider : IContextProvider
    {
        public string Name => "Diplomacy Provider";
        public int Priority => 40;

        public bool CanHandle(ContextQuery query)
        {
            return query.Type == ContextType.Diplomacy || query.Type == ContextType.WorldState;
        }

        public Task<ContextResult> RetrieveAsync(ContextQuery query)
        {
            try
            {
                var context = BuildDiplomacyContext();
                return Task.FromResult(ContextResult.Ok(context, Name));
            }
            catch (System.Exception ex)
            {
                ModLogger.LogException("[DiplomacyProvider] Failed", ex);
                return Task.FromResult(ContextResult.Fail(ex.Message));
            }
        }

        private string BuildDiplomacyContext()
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("=== Current World State ===");
            sb.AppendLine();

            // Active wars
            sb.AppendLine("Active Wars:");
            var processedPairs = new System.Collections.Generic.HashSet<string>();
            
            foreach (var kingdom in Kingdom.All.Where(k => !k.IsEliminated))
            {
                foreach (var stance in kingdom.Stances.Where(s => s.IsAtWar))
                {
                    var otherId = stance.Faction1 == kingdom ? stance.Faction2.StringId : stance.Faction1.StringId;
                    var pairKey = string.Compare(kingdom.StringId, otherId) < 0 
                        ? $"{kingdom.StringId}_{otherId}" 
                        : $"{otherId}_{kingdom.StringId}";

                    if (processedPairs.Contains(pairKey)) continue;
                    processedPairs.Add(pairKey);

                    var enemy = stance.Faction1 == kingdom ? stance.Faction2 : stance.Faction1;
                    sb.AppendLine($"  - {kingdom.Name} vs {enemy.Name}");
                }
            }

            if (processedPairs.Count == 0)
            {
                sb.AppendLine("  - No active wars");
            }

            sb.AppendLine();

            // Kingdom power rankings
            sb.AppendLine("Kingdom Power Rankings:");
            var kingdoms = Kingdom.All
                .Where(k => !k.IsEliminated)
                .OrderByDescending(k => k.TotalStrength)
                .ToList();

            int rank = 1;
            foreach (var kingdom in kingdoms.Take(6))
            {
                sb.AppendLine($"  {rank}. {kingdom.Name} - Strength: {(int)kingdom.TotalStrength}, Fiefs: {kingdom.Fiefs.Count()}");
                rank++;
            }

            return sb.ToString();
        }
    }
}

