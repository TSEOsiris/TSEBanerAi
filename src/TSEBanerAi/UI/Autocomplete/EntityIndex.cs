using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using TSEBanerAi.Utils;

namespace TSEBanerAi.UI.Autocomplete
{
    /// <summary>
    /// Index of all game entities with fast prefix search
    /// </summary>
    public class EntityIndex
    {
        private readonly List<GameEntity> _entities = new List<GameEntity>();
        private readonly Dictionary<string, List<GameEntity>> _prefixIndex = new Dictionary<string, List<GameEntity>>();
        
        public int EntityCount => _entities.Count;
        public bool IsLoaded => _entities.Count > 0;

        /// <summary>
        /// Load all entities from JSON files in the database folder
        /// Uses ModPaths.DatabasePath by default
        /// </summary>
        public void LoadFromModFolder()
        {
            LoadFromFolder(ModPaths.DatabasePath);
        }

        /// <summary>
        /// Load all entities from JSON files in the specified folder
        /// </summary>
        public void LoadFromFolder(string databasePath)
        {
            _entities.Clear();
            _prefixIndex.Clear();

            if (!Directory.Exists(databasePath))
            {
                ModLogger.LogWarning($"Database folder not found: {databasePath}");
                return;
            }

            // Load heroes
            string heroesPath = Path.Combine(databasePath, "heroes.json");
            if (File.Exists(heroesPath))
            {
                LoadHeroes(heroesPath);
            }

            // Load settlements
            string settlementsPath = Path.Combine(databasePath, "settlements.json");
            if (File.Exists(settlementsPath))
            {
                LoadSettlements(settlementsPath);
            }

            // Load kingdoms
            string kingdomsPath = Path.Combine(databasePath, "kingdoms.json");
            if (File.Exists(kingdomsPath))
            {
                LoadKingdoms(kingdomsPath);
            }

            // Load clans
            string clansPath = Path.Combine(databasePath, "clans.json");
            if (File.Exists(clansPath))
            {
                LoadClans(clansPath);
            }

            // Build prefix index for fast searching
            BuildPrefixIndex();

            ModLogger.LogDebug($"EntityIndex loaded: {_entities.Count} entities");
        }

        private void LoadHeroes(string path)
        {
            try
            {
                string json = File.ReadAllText(path);
                JArray heroes = JArray.Parse(json);
                
                foreach (JObject hero in heroes)
                {
                    string id = hero.Value<string>("Id") ?? "";
                    string name = hero.Value<string>("Name") ?? "";
                    
                    if (string.IsNullOrEmpty(name)) continue;

                    string culture = hero.Value<string>("Culture") ?? "";
                    string clan = hero.Value<string>("Clan") ?? "";

                    var entity = new GameEntity
                    {
                        Id = id,
                        Type = EntityType.Hero,
                        Description = $"Hero from {culture}",
                        Context = $"Clan: {clan}"
                    };
                    entity.SetName(name);
                    _entities.Add(entity);
                }

                ModLogger.LogDebug($"Loaded {_entities.Count(e => e.Type == EntityType.Hero)} heroes");
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Error loading heroes", ex);
            }
        }

        private void LoadSettlements(string path)
        {
            try
            {
                string json = File.ReadAllText(path);
                JArray settlements = JArray.Parse(json);
                
                int count = 0;
                foreach (JObject settlement in settlements)
                {
                    string id = settlement.Value<string>("Id") ?? "";
                    string name = settlement.Value<string>("Name") ?? "";
                    
                    if (string.IsNullOrEmpty(name)) continue;

                    string culture = settlement.Value<string>("Culture") ?? "";
                    string type = settlement.Value<string>("SettlementType") ?? "";
                    string owner = settlement.Value<string>("OwnerName") ?? "";

                    string description;
                    switch (type)
                    {
                        case "town": description = "Town"; break;
                        case "castle": description = "Castle"; break;
                        case "village": description = "Village"; break;
                        default: description = "Settlement"; break;
                    }

                    var entity = new GameEntity
                    {
                        Id = id,
                        Type = EntityType.Settlement,
                        Description = $"{description} ({culture})",
                        Context = $"Owner: {owner}"
                    };
                    entity.SetName(name);
                    _entities.Add(entity);
                    count++;
                }

                ModLogger.LogDebug($"Loaded {count} settlements");
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Error loading settlements", ex);
            }
        }

        private void LoadKingdoms(string path)
        {
            try
            {
                string json = File.ReadAllText(path);
                JArray kingdoms = JArray.Parse(json);
                
                int count = 0;
                foreach (JObject kingdom in kingdoms)
                {
                    string id = kingdom.Value<string>("Id") ?? "";
                    string name = kingdom.Value<string>("Name") ?? "";
                    
                    if (string.IsNullOrEmpty(name)) continue;

                    string culture = kingdom.Value<string>("Culture") ?? "";
                    string ruler = kingdom.Value<string>("RulerName") ?? "";

                    var entity = new GameEntity
                    {
                        Id = id,
                        Type = EntityType.Kingdom,
                        Description = $"Kingdom ({culture})",
                        Context = string.IsNullOrEmpty(ruler) ? "" : $"Ruler: {ruler}"
                    };
                    entity.SetName(name);
                    _entities.Add(entity);
                    count++;
                }

                ModLogger.LogDebug($"Loaded {count} kingdoms");
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Error loading kingdoms", ex);
            }
        }

        private void LoadClans(string path)
        {
            try
            {
                string json = File.ReadAllText(path);
                JArray clans = JArray.Parse(json);
                
                int count = 0;
                foreach (JObject clan in clans)
                {
                    string id = clan.Value<string>("Id") ?? "";
                    string name = clan.Value<string>("Name") ?? "";
                    
                    if (string.IsNullOrEmpty(name)) continue;

                    string culture = clan.Value<string>("Culture") ?? "";
                    string leader = clan.Value<string>("LeaderName") ?? "";
                    string kingdom = clan.Value<string>("Kingdom") ?? "";

                    var entity = new GameEntity
                    {
                        Id = id,
                        Type = EntityType.Clan,
                        Description = $"Clan ({culture})",
                        Context = $"Leader: {leader}, Kingdom: {kingdom}"
                    };
                    entity.SetName(name);
                    _entities.Add(entity);
                    count++;
                }

                ModLogger.LogDebug($"Loaded {count} clans");
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Error loading clans", ex);
            }
        }

        /// <summary>
        /// Build prefix index for fast O(1) prefix lookups
        /// </summary>
        private void BuildPrefixIndex()
        {
            _prefixIndex.Clear();

            foreach (var entity in _entities)
            {
                string nameLower = entity.NameLower;
                
                // Index all prefixes of length 1-5
                for (int len = 1; len <= Math.Min(5, nameLower.Length); len++)
                {
                    string prefix = nameLower.Substring(0, len);
                    
                    if (!_prefixIndex.ContainsKey(prefix))
                    {
                        _prefixIndex[prefix] = new List<GameEntity>();
                    }
                    _prefixIndex[prefix].Add(entity);
                }
            }

            ModLogger.LogDebug($"Built prefix index with {_prefixIndex.Count} prefixes");
        }

        /// <summary>
        /// Get all loaded entities
        /// </summary>
        public IReadOnlyList<GameEntity> GetAllEntities()
        {
            return _entities.AsReadOnly();
        }

        /// <summary>
        /// Search entities by name (partial match, case-insensitive)
        /// </summary>
        public List<GameEntity> Search(string query, int maxResults = 10)
        {
            if (string.IsNullOrEmpty(query) || query.Length < 2)
                return new List<GameEntity>();

            var lowerQuery = query.ToLowerInvariant();
            return _entities
                .Where(e => e.Name.ToLowerInvariant().Contains(lowerQuery))
                .Take(maxResults)
                .ToList();
        }

        /// <summary>
        /// Search entities by prefix (case-insensitive)
        /// </summary>
        public List<GameEntity> SearchByPrefix(string prefix, int maxResults = 5)
        {
            if (string.IsNullOrEmpty(prefix) || prefix.Length < 3)
                return new List<GameEntity>();

            string prefixLower = prefix.ToLowerInvariant();
            
            // Try to find in prefix index first (for short prefixes)
            string indexKey = prefixLower.Length <= 5 ? prefixLower : prefixLower.Substring(0, 5);
            
            List<GameEntity> candidates;
            if (_prefixIndex.TryGetValue(indexKey, out var indexed))
            {
                candidates = indexed
                    .Where(e => e.NameLower.StartsWith(prefixLower))
                    .ToList();
            }
            else
            {
                candidates = _entities
                    .Where(e => e.NameLower.StartsWith(prefixLower))
                    .ToList();
            }

            return candidates
                .OrderBy(e => e.NameLower == prefixLower ? 0 : 1)
                .ThenBy(e => GetTypePriority(e.Type))
                .ThenBy(e => e.Name.Length)
                .Take(maxResults)
                .ToList();
        }

        private int GetTypePriority(EntityType type)
        {
            switch (type)
            {
                case EntityType.Hero: return 0;
                case EntityType.Settlement: return 1;
                case EntityType.Kingdom: return 2;
                case EntityType.Clan: return 3;
                default: return 4;
            }
        }

        /// <summary>
        /// Get entity by ID
        /// </summary>
        public GameEntity GetById(string id)
        {
            return _entities.FirstOrDefault(e => e.Id == id);
        }

        /// <summary>
        /// Get all entities of a specific type
        /// </summary>
        public IEnumerable<GameEntity> GetByType(EntityType type)
        {
            return _entities.Where(e => e.Type == type);
        }
    }
}



