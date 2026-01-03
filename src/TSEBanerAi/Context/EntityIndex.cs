using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TSEBanerAi.Utils;

namespace TSEBanerAi.Context
{
    /// <summary>
    /// Indexed search for game entities (for autocomplete)
    /// </summary>
    public class EntityIndex
    {
        private static EntityIndex _instance;
        private static readonly object _lock = new object();

        private List<EntityEntry> _entries;
        private DateTime _lastRefresh;
        private readonly TimeSpan _refreshInterval = TimeSpan.FromMinutes(5);

        public static EntityIndex Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new EntityIndex();
                        }
                    }
                }
                return _instance;
            }
        }

        private EntityIndex()
        {
            _entries = new List<EntityEntry>();
            _lastRefresh = DateTime.MinValue;
        }

        /// <summary>
        /// Refresh entity index from game data
        /// </summary>
        public void Refresh(bool force = false)
        {
            if (!force && DateTime.UtcNow - _lastRefresh < _refreshInterval)
            {
                return;
            }

            try
            {
                ModLogger.LogDebug("Refreshing entity index...");
                var entries = new List<EntityEntry>();

                // Index heroes
                foreach (var hero in Hero.AllAliveHeroes)
                {
                    entries.Add(new EntityEntry
                    {
                        Id = hero.StringId,
                        Name = hero.Name?.ToString() ?? "Unknown",
                        Type = EntityType.Hero,
                        SubType = GetHeroSubType(hero),
                        SearchTerms = GetHeroSearchTerms(hero)
                    });
                }

                // Index settlements
                foreach (var settlement in Settlement.All)
                {
                    entries.Add(new EntityEntry
                    {
                        Id = settlement.StringId,
                        Name = settlement.Name?.ToString() ?? "Unknown",
                        Type = EntityType.Settlement,
                        SubType = settlement.IsTown ? "Town" : settlement.IsCastle ? "Castle" : "Village",
                        SearchTerms = GetSettlementSearchTerms(settlement)
                    });
                }

                // Index kingdoms
                foreach (var kingdom in Kingdom.All.Where(k => !k.IsEliminated))
                {
                    entries.Add(new EntityEntry
                    {
                        Id = kingdom.StringId,
                        Name = kingdom.Name?.ToString() ?? "Unknown",
                        Type = EntityType.Kingdom,
                        SubType = "Kingdom",
                        SearchTerms = GetKingdomSearchTerms(kingdom)
                    });
                }

                // Index clans
                foreach (var clan in Clan.All.Where(c => !c.IsEliminated))
                {
                    entries.Add(new EntityEntry
                    {
                        Id = clan.StringId,
                        Name = clan.Name?.ToString() ?? "Unknown",
                        Type = EntityType.Clan,
                        SubType = clan.IsMinorFaction ? "Minor Faction" : "Clan",
                        SearchTerms = GetClanSearchTerms(clan)
                    });
                }

                _entries = entries;
                _lastRefresh = DateTime.UtcNow;
                ModLogger.LogDebug($"Entity index refreshed: {entries.Count} entries");
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to refresh entity index", ex);
            }
        }

        /// <summary>
        /// Search for entities by query
        /// </summary>
        public List<EntityEntry> Search(string query, int maxResults = 10, EntityType? filterType = null)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return new List<EntityEntry>();
            }

            // Ensure index is fresh
            Refresh();

            query = query.ToLowerInvariant();
            var results = new List<(EntityEntry entry, int score)>();

            foreach (var entry in _entries)
            {
                if (filterType.HasValue && entry.Type != filterType.Value)
                {
                    continue;
                }

                int score = CalculateMatchScore(entry, query);
                if (score > 0)
                {
                    results.Add((entry, score));
                }
            }

            return results
                .OrderByDescending(r => r.score)
                .Take(maxResults)
                .Select(r => r.entry)
                .ToList();
        }

        /// <summary>
        /// Get entity by ID
        /// </summary>
        public EntityEntry GetById(string id)
        {
            Refresh();
            return _entries.FirstOrDefault(e => e.Id == id);
        }

        private int CalculateMatchScore(EntityEntry entry, string query)
        {
            int score = 0;

            // Exact name match
            if (entry.Name.ToLowerInvariant() == query)
            {
                return 100;
            }

            // Name starts with query
            if (entry.Name.ToLowerInvariant().StartsWith(query))
            {
                score += 50;
            }
            // Name contains query
            else if (entry.Name.ToLowerInvariant().Contains(query))
            {
                score += 30;
            }

            // Search terms match
            foreach (var term in entry.SearchTerms)
            {
                if (term.ToLowerInvariant().Contains(query))
                {
                    score += 10;
                }
            }

            return score;
        }

        #region Helper Methods

        private string GetHeroSubType(Hero hero)
        {
            if (hero.IsLord) return "Lord";
            if (hero.IsWanderer) return "Wanderer";
            if (hero.IsMerchant) return "Merchant";
            if (hero.IsNotable) return "Notable";
            return "Commoner";
        }

        private List<string> GetHeroSearchTerms(Hero hero)
        {
            var terms = new List<string>();
            
            if (hero.Clan != null)
            {
                terms.Add(hero.Clan.Name?.ToString());
            }
            if (hero.Clan?.Kingdom != null)
            {
                terms.Add(hero.Clan.Kingdom.Name?.ToString());
            }
            if (hero.Culture != null)
            {
                terms.Add(hero.Culture.Name?.ToString());
            }
            
            terms.Add(GetHeroSubType(hero));
            
            return terms.Where(t => !string.IsNullOrEmpty(t)).ToList();
        }

        private List<string> GetSettlementSearchTerms(Settlement settlement)
        {
            var terms = new List<string>();
            
            if (settlement.OwnerClan != null)
            {
                terms.Add(settlement.OwnerClan.Name?.ToString());
            }
            if (settlement.OwnerClan?.Kingdom != null)
            {
                terms.Add(settlement.OwnerClan.Kingdom.Name?.ToString());
            }
            if (settlement.Culture != null)
            {
                terms.Add(settlement.Culture.Name?.ToString());
            }
            
            terms.Add(settlement.IsTown ? "Town" : settlement.IsCastle ? "Castle" : "Village");
            
            return terms.Where(t => !string.IsNullOrEmpty(t)).ToList();
        }

        private List<string> GetKingdomSearchTerms(Kingdom kingdom)
        {
            var terms = new List<string>();
            
            if (kingdom.Leader != null)
            {
                terms.Add(kingdom.Leader.Name?.ToString());
            }
            if (kingdom.Culture != null)
            {
                terms.Add(kingdom.Culture.Name?.ToString());
            }
            
            return terms.Where(t => !string.IsNullOrEmpty(t)).ToList();
        }

        private List<string> GetClanSearchTerms(Clan clan)
        {
            var terms = new List<string>();
            
            if (clan.Leader != null)
            {
                terms.Add(clan.Leader.Name?.ToString());
            }
            if (clan.Kingdom != null)
            {
                terms.Add(clan.Kingdom.Name?.ToString());
            }
            if (clan.Culture != null)
            {
                terms.Add(clan.Culture.Name?.ToString());
            }
            
            return terms.Where(t => !string.IsNullOrEmpty(t)).ToList();
        }

        #endregion
    }

    /// <summary>
    /// Entry in entity index
    /// </summary>
    public class EntityEntry
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public EntityType Type { get; set; }
        public string SubType { get; set; }
        public List<string> SearchTerms { get; set; } = new List<string>();
    }

    /// <summary>
    /// Entity types
    /// </summary>
    public enum EntityType
    {
        Hero,
        Settlement,
        Kingdom,
        Clan
    }
}

