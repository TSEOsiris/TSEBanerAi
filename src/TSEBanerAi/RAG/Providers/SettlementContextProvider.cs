using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.Settlements;
using TSEBanerAi.Context;
using TSEBanerAi.Utils;

namespace TSEBanerAi.RAG
{
    /// <summary>
    /// Provides context about settlements
    /// </summary>
    public class SettlementContextProvider : IContextProvider
    {
        public string Name => "Settlement Provider";
        public int Priority => 20;

        public bool CanHandle(ContextQuery query)
        {
            return query.Type == ContextType.Settlement;
        }

        public Task<ContextResult> RetrieveAsync(ContextQuery query)
        {
            try
            {
                Settlement settlement = null;
                
                if (!string.IsNullOrEmpty(query.EntityId))
                {
                    settlement = Settlement.Find(query.EntityId);
                    
                    if (settlement == null)
                    {
                        settlement = GameContextBuilder.Instance.FindSettlementByName(query.EntityId);
                    }
                }

                if (settlement == null)
                {
                    return Task.FromResult(ContextResult.Fail($"Settlement not found: {query.EntityId}"));
                }

                var context = BuildSettlementContext(settlement);
                return Task.FromResult(ContextResult.Ok(context, Name));
            }
            catch (System.Exception ex)
            {
                ModLogger.LogException("[SettlementProvider] Failed", ex);
                return Task.FromResult(ContextResult.Fail(ex.Message));
            }
        }

        private string BuildSettlementContext(Settlement settlement)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine($"Name: {settlement.Name}");
            
            string type = settlement.IsTown ? "Town" : settlement.IsCastle ? "Castle" : "Village";
            sb.AppendLine($"Type: {type}");
            
            if (settlement.Culture != null)
                sb.AppendLine($"Culture: {settlement.Culture.Name}");
            
            if (settlement.OwnerClan != null)
            {
                sb.AppendLine($"Owner Clan: {settlement.OwnerClan.Name}");
                if (settlement.OwnerClan.Kingdom != null)
                    sb.AppendLine($"Kingdom: {settlement.OwnerClan.Kingdom.Name}");
            }

            // Town/Castle specific
            if (settlement.Town != null)
            {
                sb.AppendLine($"Prosperity: {(int)settlement.Town.Prosperity}");
                sb.AppendLine($"Loyalty: {(int)settlement.Town.Loyalty}");
                sb.AppendLine($"Security: {(int)settlement.Town.Security}");
                sb.AppendLine($"Garrison: {settlement.Town.GarrisonParty?.MemberRoster?.TotalManCount ?? 0}");
                
                if (settlement.Town.Governor != null)
                    sb.AppendLine($"Governor: {settlement.Town.Governor.Name}");
            }

            // Village specific
            if (settlement.Village != null)
            {
                sb.AppendLine($"Hearths: {(int)settlement.Village.Hearth}");
                if (settlement.Village.TradeBound != null)
                    sb.AppendLine($"Bound to: {settlement.Village.TradeBound.Name}");
            }

            // Notables
            if (settlement.Notables != null && settlement.Notables.Count > 0)
            {
                sb.Append("Notables: ");
                foreach (var notable in settlement.Notables)
                {
                    sb.Append($"{notable.Name}, ");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}

