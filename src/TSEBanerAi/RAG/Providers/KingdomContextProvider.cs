using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TSEBanerAi.Context;
using TSEBanerAi.Utils;

namespace TSEBanerAi.RAG
{
    /// <summary>
    /// Provides context about kingdoms
    /// </summary>
    public class KingdomContextProvider : IContextProvider
    {
        public string Name => "Kingdom Provider";
        public int Priority => 30;

        public bool CanHandle(ContextQuery query)
        {
            return query.Type == ContextType.Kingdom;
        }

        public Task<ContextResult> RetrieveAsync(ContextQuery query)
        {
            try
            {
                Kingdom kingdom = null;
                
                if (!string.IsNullOrEmpty(query.EntityId))
                {
                    kingdom = Kingdom.All.FirstOrDefault(k => k.StringId == query.EntityId);
                    
                    if (kingdom == null)
                    {
                        kingdom = GameContextBuilder.Instance.FindKingdomByName(query.EntityId);
                    }
                }

                if (kingdom == null)
                {
                    return Task.FromResult(ContextResult.Fail($"Kingdom not found: {query.EntityId}"));
                }

                var context = BuildKingdomContext(kingdom);
                return Task.FromResult(ContextResult.Ok(context, Name));
            }
            catch (System.Exception ex)
            {
                ModLogger.LogException("[KingdomProvider] Failed", ex);
                return Task.FromResult(ContextResult.Fail(ex.Message));
            }
        }

        private string BuildKingdomContext(Kingdom kingdom)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine($"Name: {kingdom.Name}");
            
            if (kingdom.Culture != null)
                sb.AppendLine($"Culture: {kingdom.Culture.Name}");
            
            if (kingdom.Leader != null)
                sb.AppendLine($"Ruler: {kingdom.Leader.Name}");
            
            sb.AppendLine($"Clans: {kingdom.Clans.Count}");
            sb.AppendLine($"Fiefs: {kingdom.Fiefs.Count()}");
            sb.AppendLine($"Total Strength: {(int)kingdom.TotalStrength}");

            // List major clans
            sb.Append("Major Clans: ");
            foreach (var clan in kingdom.Clans.OrderByDescending(c => c.Tier).Take(5))
            {
                sb.Append($"{clan.Name}, ");
            }
            sb.AppendLine();

            // Wars
            var enemies = kingdom.Stances.Where(s => s.IsAtWar).ToList();
            if (enemies.Count > 0)
            {
                sb.Append("At War With: ");
                foreach (var stance in enemies)
                {
                    var enemy = stance.Faction1 == kingdom ? stance.Faction2 : stance.Faction1;
                    sb.Append($"{enemy.Name}, ");
                }
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine("At War With: None (at peace)");
            }

            // Allies
            var allies = kingdom.Stances.Where(s => s.IsAllied).ToList();
            if (allies.Count > 0)
            {
                sb.Append("Allied With: ");
                foreach (var stance in allies)
                {
                    var ally = stance.Faction1 == kingdom ? stance.Faction2 : stance.Faction1;
                    sb.Append($"{ally.Name}, ");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}

