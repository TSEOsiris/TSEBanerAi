using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TSEBanerAi.Context;
using TSEBanerAi.Utils;

namespace TSEBanerAi.RAG
{
    /// <summary>
    /// Provides context about heroes/NPCs
    /// </summary>
    public class HeroContextProvider : IContextProvider
    {
        public string Name => "Hero Provider";
        public int Priority => 10;

        public bool CanHandle(ContextQuery query)
        {
            return query.Type == ContextType.Hero;
        }

        public Task<ContextResult> RetrieveAsync(ContextQuery query)
        {
            try
            {
                // Find hero by ID or name
                Hero hero = null;
                
                if (!string.IsNullOrEmpty(query.EntityId))
                {
                    hero = Hero.FindFirst(h => h.StringId == query.EntityId);
                    
                    if (hero == null)
                    {
                        // Try by name
                        hero = GameContextBuilder.Instance.FindHeroByName(query.EntityId);
                    }
                }

                if (hero == null)
                {
                    return Task.FromResult(ContextResult.Fail($"Hero not found: {query.EntityId}"));
                }

                var context = BuildHeroContext(hero);
                return Task.FromResult(ContextResult.Ok(context, Name));
            }
            catch (System.Exception ex)
            {
                ModLogger.LogException("[HeroProvider] Failed", ex);
                return Task.FromResult(ContextResult.Fail(ex.Message));
            }
        }

        private string BuildHeroContext(Hero hero)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine($"Name: {hero.Name}");
            sb.AppendLine($"Age: {(int)hero.Age}, Gender: {(hero.IsFemale ? "Female" : "Male")}");
            
            if (hero.Culture != null)
                sb.AppendLine($"Culture: {hero.Culture.Name}");
            
            if (hero.Clan != null)
            {
                sb.AppendLine($"Clan: {hero.Clan.Name}");
                if (hero.Clan.Leader == hero)
                    sb.AppendLine("Role: Clan Leader");
            }
            
            if (hero.Clan?.Kingdom != null)
            {
                sb.AppendLine($"Kingdom: {hero.Clan.Kingdom.Name}");
                if (hero.Clan.Kingdom.Leader == hero)
                    sb.AppendLine("Role: Ruler");
            }

            // Occupation
            if (hero.IsLord) sb.AppendLine("Title: Lord");
            else if (hero.IsWanderer) sb.AppendLine("Title: Wanderer/Companion");
            else if (hero.IsMerchant) sb.AppendLine("Title: Merchant");
            else if (hero.IsNotable) sb.AppendLine("Title: Notable");

            // Current location
            if (hero.CurrentSettlement != null)
                sb.AppendLine($"Location: {hero.CurrentSettlement.Name}");
            else if (hero.PartyBelongedTo != null)
                sb.AppendLine("Location: Traveling with party");

            // Relation with player
            var relation = hero.GetRelationWithPlayer();
            sb.AppendLine($"Relation with player: {(int)relation}");

            // Party info
            if (hero.PartyBelongedTo != null && hero.PartyBelongedTo.LeaderHero == hero)
            {
                sb.AppendLine($"Party size: {hero.PartyBelongedTo.MemberRoster?.TotalManCount ?? 0}");
            }

            return sb.ToString();
        }
    }
}

