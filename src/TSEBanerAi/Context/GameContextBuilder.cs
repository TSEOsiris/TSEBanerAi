using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TSEBanerAi.Storage;
using TSEBanerAi.Storage.Models;
using TSEBanerAi.Utils;

namespace TSEBanerAi.Context
{
    /// <summary>
    /// Builds context from game data for LLM prompts
    /// </summary>
    public class GameContextBuilder
    {
        private static GameContextBuilder _instance;
        private static readonly object _lock = new object();

        /// <summary>
        /// Singleton instance
        /// </summary>
        public static GameContextBuilder Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new GameContextBuilder();
                        }
                    }
                }
                return _instance;
            }
        }

        private GameContextBuilder() { }

        #region NPC Context

        /// <summary>
        /// Get or create NPC snapshot (lazy loading with cache)
        /// </summary>
        public NpcSnapshot GetNpcSnapshot(Hero hero, bool forceUpdate = false)
        {
            if (hero == null) return null;

            try
            {
                // First, try to safely check if storage is available
                bool storageAvailable = false;
                string campaignId = "";
                int currentDay = GetCurrentGameDay();
                
                try
                {
                    var storage = StorageManager.Instance;
                    storageAvailable = storage.IsInitialized;
                    if (storageAvailable)
                    {
                        campaignId = storage.CurrentCampaignId ?? "";
                        currentDay = storage.GetCurrentGameDay();
                    }
                }
                catch
                {
                    // Storage not available (e.g., SQLite not loaded)
                    storageAvailable = false;
                }
                
                if (!storageAvailable)
                {
                    // If storage not ready, create snapshot without caching
                    return CreateNpcSnapshot(hero, campaignId);
                }

                var storageInst = StorageManager.Instance;

                // Check cache (unless force update)
                if (!forceUpdate)
                {
                    var cached = storageInst.Npcs.GetSnapshot(campaignId, hero.StringId);
                    if (cached != null && !storageInst.Npcs.NeedsUpdate(campaignId, hero.StringId, currentDay))
                    {
                        return cached;
                    }
                }

                // Create new snapshot
                var snapshot = CreateNpcSnapshot(hero, campaignId);
                snapshot.GameDayUpdated = currentDay;

                // Save to cache
                storageInst.Npcs.SaveSnapshot(snapshot);

                return snapshot;
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to get NPC snapshot", ex);
                // Fallback: create snapshot without caching
                try
                {
                    return CreateNpcSnapshot(hero, "");
                }
                catch
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Create NPC snapshot from Hero
        /// </summary>
        private NpcSnapshot CreateNpcSnapshot(Hero hero, string campaignId)
        {
            var snapshot = new NpcSnapshot
            {
                NpcId = hero.StringId,
                CampaignId = campaignId,
                Name = hero.Name?.ToString() ?? "Unknown",
                Age = (int)hero.Age,
                Gender = hero.IsFemale ? "Female" : "Male",
                IsAlive = hero.IsAlive,
                LastUpdated = DateTime.UtcNow
            };

            // Culture
            if (hero.Culture != null)
            {
                snapshot.Culture = hero.Culture.Name?.ToString();
            }

            // Clan
            if (hero.Clan != null)
            {
                snapshot.ClanId = hero.Clan.StringId;
                snapshot.ClanName = hero.Clan.Name?.ToString();
            }

            // Kingdom
            if (hero.Clan?.Kingdom != null)
            {
                snapshot.KingdomId = hero.Clan.Kingdom.StringId;
                snapshot.KingdomName = hero.Clan.Kingdom.Name?.ToString();
            }

            // Occupation
            snapshot.Occupation = GetOccupation(hero);

            // Relation with player
            var mainHero = Hero.MainHero;
            if (mainHero != null && hero != mainHero)
            {
                snapshot.RelationWithPlayer = (int)hero.GetRelationWithPlayer();
            }

            // Traits
            snapshot.TraitValor = GetTraitLevel(hero, DefaultTraits.Valor);
            snapshot.TraitMercy = GetTraitLevel(hero, DefaultTraits.Mercy);
            snapshot.TraitHonor = GetTraitLevel(hero, DefaultTraits.Honor);
            snapshot.TraitGenerosity = GetTraitLevel(hero, DefaultTraits.Generosity);
            snapshot.TraitCalculating = GetTraitLevel(hero, DefaultTraits.Calculating);

            // Location
            if (hero.CurrentSettlement != null)
            {
                snapshot.CurrentLocationId = hero.CurrentSettlement.StringId;
                snapshot.CurrentLocationName = hero.CurrentSettlement.Name?.ToString();
            }

            // Gold and party
            snapshot.Gold = hero.Gold;
            if (hero.PartyBelongedTo != null)
            {
                snapshot.PartySize = hero.PartyBelongedTo.MemberRoster?.TotalManCount ?? 0;
            }

            // Skills (top 5)
            snapshot.SkillsJson = GetTopSkillsJson(hero);

            // Owned fiefs
            snapshot.OwnedFiefsJson = GetOwnedFiefsJson(hero);

            return snapshot;
        }

        private string GetOccupation(Hero hero)
        {
            if (hero.IsLord) return "Lord";
            if (hero.IsWanderer) return "Wanderer";
            if (hero.IsMerchant) return "Merchant";
            if (hero.IsNotable) return "Notable";
            if (hero.IsArtisan) return "Artisan";
            if (hero.IsGangLeader) return "Gang Leader";
            if (hero.IsRuralNotable) return "Rural Notable";
            if (hero.IsUrbanNotable) return "Urban Notable";
            if (hero.IsMinorFactionHero) return "Minor Faction Hero";
            return "Commoner";
        }

        private int GetTraitLevel(Hero hero, TraitObject trait)
        {
            try
            {
                int value = hero.GetTraitLevel(trait);
                // Clamp to -2 to 2
                return Math.Max(-2, Math.Min(2, value));
            }
            catch
            {
                return 0;
            }
        }

        private string GetTopSkillsJson(Hero hero)
        {
            try
            {
                var skills = new Dictionary<string, int>();
                
                // Get key skills
                var skillObjects = new[]
                {
                    DefaultSkills.OneHanded, DefaultSkills.TwoHanded, DefaultSkills.Polearm,
                    DefaultSkills.Bow, DefaultSkills.Crossbow, DefaultSkills.Throwing,
                    DefaultSkills.Riding, DefaultSkills.Athletics, DefaultSkills.Crafting,
                    DefaultSkills.Scouting, DefaultSkills.Tactics, DefaultSkills.Roguery,
                    DefaultSkills.Charm, DefaultSkills.Leadership, DefaultSkills.Trade,
                    DefaultSkills.Steward, DefaultSkills.Medicine, DefaultSkills.Engineering
                };
                
                foreach (var skill in skillObjects)
                {
                    int value = hero.GetSkillValue(skill);
                    if (value > 0)
                    {
                        skills[skill.Name.ToString()] = value;
                    }
                }

                // Take top 5
                var topSkills = skills
                    .OrderByDescending(s => s.Value)
                    .Take(5)
                    .ToDictionary(s => s.Key, s => s.Value);

                return JsonConvert.SerializeObject(topSkills);
            }
            catch
            {
                return "{}";
            }
        }

        private string GetOwnedFiefsJson(Hero hero)
        {
            try
            {
                if (hero.Clan == null) return "[]";

                var fiefs = hero.Clan.Fiefs
                    .Select(f => new { id = f.StringId, name = f.Name?.ToString(), type = f.IsTown ? "Town" : f.IsCastle ? "Castle" : "Village" })
                    .ToList();

                return JsonConvert.SerializeObject(fiefs);
            }
            catch
            {
                return "[]";
            }
        }

        #endregion

        #region World Context

        /// <summary>
        /// Get player hero info
        /// </summary>
        public string GetPlayerName()
        {
            return Hero.MainHero?.Name?.ToString() ?? "Player";
        }

        /// <summary>
        /// Get current game day (days since year 0)
        /// </summary>
        public int GetCurrentGameDay()
        {
            if (Campaign.Current == null) return 0;
            // Use CampaignTime.Now to get current day
            var now = CampaignTime.Now;
            return (int)now.ToDays;
        }

        /// <summary>
        /// Get kingdoms summary
        /// </summary>
        public List<KingdomInfo> GetKingdomsSummary()
        {
            var kingdoms = new List<KingdomInfo>();

            try
            {
                foreach (var kingdom in Kingdom.All.Where(k => !k.IsEliminated))
                {
                    kingdoms.Add(new KingdomInfo
                    {
                        Id = kingdom.StringId,
                        Name = kingdom.Name?.ToString(),
                        RulerName = kingdom.Leader?.Name?.ToString(),
                        ClanCount = kingdom.Clans.Count,
                        FiefCount = kingdom.Fiefs.Count(),
                        TotalStrength = (int)kingdom.TotalStrength,
                        IsAtWarWithPlayer = kingdom.IsAtWarWith(Hero.MainHero?.Clan?.Kingdom)
                    });
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to get kingdoms summary", ex);
            }

            return kingdoms;
        }

        /// <summary>
        /// Get active wars
        /// </summary>
        public List<WarInfo> GetActiveWars()
        {
            var wars = new List<WarInfo>();

            try
            {
                var processedPairs = new HashSet<string>();

                foreach (var kingdom in Kingdom.All.Where(k => !k.IsEliminated))
                {
                    foreach (var enemy in kingdom.Stances.Where(s => s.IsAtWar))
                    {
                        var otherId = enemy.Faction1 == kingdom ? enemy.Faction2.StringId : enemy.Faction1.StringId;
                        var pairKey = string.Compare(kingdom.StringId, otherId) < 0 
                            ? $"{kingdom.StringId}_{otherId}" 
                            : $"{otherId}_{kingdom.StringId}";

                        if (processedPairs.Contains(pairKey)) continue;
                        processedPairs.Add(pairKey);

                        wars.Add(new WarInfo
                        {
                            Kingdom1 = kingdom.Name?.ToString(),
                            Kingdom2 = enemy.Faction2 == kingdom 
                                ? enemy.Faction1.Name?.ToString() 
                                : enemy.Faction2.Name?.ToString()
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to get active wars", ex);
            }

            return wars;
        }

        /// <summary>
        /// Get settlement info
        /// </summary>
        public SettlementInfo GetSettlementInfo(Settlement settlement)
        {
            if (settlement == null) return null;

            try
            {
                return new SettlementInfo
                {
                    Id = settlement.StringId,
                    Name = settlement.Name?.ToString(),
                    Type = settlement.IsTown ? "Town" : settlement.IsCastle ? "Castle" : "Village",
                    OwnerClan = settlement.OwnerClan?.Name?.ToString(),
                    OwnerKingdom = settlement.OwnerClan?.Kingdom?.Name?.ToString(),
                    Culture = settlement.Culture?.Name?.ToString(),
                    Prosperity = settlement.IsTown || settlement.IsCastle 
                        ? (int)(settlement.Town?.Prosperity ?? 0) 
                        : (int)(settlement.Village?.Hearth ?? 0)
                };
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to get settlement info", ex);
                return null;
            }
        }

        #endregion

        #region Search Methods

        /// <summary>
        /// Find hero by name (partial match)
        /// </summary>
        public Hero FindHeroByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            try
            {
                name = name.ToLowerInvariant();
                return Hero.AllAliveHeroes.FirstOrDefault(h => 
                    h.Name?.ToString().ToLowerInvariant().Contains(name) == true);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Find settlement by name (partial match)
        /// </summary>
        public Settlement FindSettlementByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            try
            {
                name = name.ToLowerInvariant();
                return Settlement.All.FirstOrDefault(s => 
                    s.Name?.ToString().ToLowerInvariant().Contains(name) == true);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Find kingdom by name (partial match)
        /// </summary>
        public Kingdom FindKingdomByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            try
            {
                name = name.ToLowerInvariant();
                return Kingdom.All.FirstOrDefault(k => 
                    k.Name?.ToString().ToLowerInvariant().Contains(name) == true);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get list of enemy kingdoms for a hero's faction
        /// </summary>
        public List<string> GetEnemyKingdoms(Hero hero)
        {
            var enemies = new List<string>();
            
            try
            {
                if (hero?.Clan?.Kingdom == null) return enemies;
                
                var kingdom = hero.Clan.Kingdom;
                foreach (var stance in kingdom.Stances.Where(s => s.IsAtWar))
                {
                    var enemy = stance.Faction1 == kingdom ? stance.Faction2 : stance.Faction1;
                    if (enemy != null && !string.IsNullOrEmpty(enemy.Name?.ToString()))
                    {
                        enemies.Add(enemy.Name.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to get enemy kingdoms", ex);
            }
            
            return enemies;
        }

        /// <summary>
        /// Get list of allied kingdoms for a hero's faction
        /// </summary>
        public List<string> GetAlliedKingdoms(Hero hero)
        {
            var allies = new List<string>();
            
            try
            {
                if (hero?.Clan?.Kingdom == null) return allies;
                
                var kingdom = hero.Clan.Kingdom;
                foreach (var stance in kingdom.Stances.Where(s => s.IsAllied))
                {
                    var ally = stance.Faction1 == kingdom ? stance.Faction2 : stance.Faction1;
                    if (ally != null && !string.IsNullOrEmpty(ally.Name?.ToString()))
                    {
                        allies.Add(ally.Name.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to get allied kingdoms", ex);
            }
            
            return allies;
        }

        #endregion
    }

    #region Context Models

    public class KingdomInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string RulerName { get; set; }
        public int ClanCount { get; set; }
        public int FiefCount { get; set; }
        public int TotalStrength { get; set; }
        public bool IsAtWarWithPlayer { get; set; }
    }

    public class WarInfo
    {
        public string Kingdom1 { get; set; }
        public string Kingdom2 { get; set; }
    }

    public class SettlementInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string OwnerClan { get; set; }
        public string OwnerKingdom { get; set; }
        public string Culture { get; set; }
        public int Prosperity { get; set; }
    }

    #endregion
}

