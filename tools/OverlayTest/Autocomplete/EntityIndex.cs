using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace OverlayTest.Autocomplete
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
        /// </summary>
        public void LoadFromFolder(string databasePath)
        {
            _entities.Clear();
            _prefixIndex.Clear();

            if (!Directory.Exists(databasePath))
            {
                Console.WriteLine($"Database folder not found: {databasePath}");
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

            Console.WriteLine($"EntityIndex loaded: {_entities.Count} entities");
        }

        private void LoadHeroes(string path)
        {
            try
            {
                string json = File.ReadAllText(path);
                using JsonDocument doc = JsonDocument.Parse(json);
                
                foreach (JsonElement hero in doc.RootElement.EnumerateArray())
                {
                    string id = hero.GetProperty("Id").GetString() ?? "";
                    string name = hero.GetProperty("Name").GetString() ?? "";
                    
                    if (string.IsNullOrEmpty(name)) continue;

                    string culture = hero.TryGetProperty("Culture", out var cultureEl) ? cultureEl.GetString() ?? "" : "";
                    string clan = hero.TryGetProperty("Clan", out var clanEl) ? clanEl.GetString() ?? "" : "";

                    var entity = new GameEntity
                    {
                        Id = id,
                        Type = EntityType.Hero,
                        Description = $"Герой из {culture}",
                        Context = $"Клан: {clan}"
                    };
                    entity.SetName(name);
                    _entities.Add(entity);
                }

                Console.WriteLine($"Loaded {_entities.Count(e => e.Type == EntityType.Hero)} heroes");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading heroes: {ex.Message}");
            }
        }

        private void LoadSettlements(string path)
        {
            try
            {
                string json = File.ReadAllText(path);
                using JsonDocument doc = JsonDocument.Parse(json);
                
                int count = 0;
                foreach (JsonElement settlement in doc.RootElement.EnumerateArray())
                {
                    string id = settlement.GetProperty("Id").GetString() ?? "";
                    string name = settlement.GetProperty("Name").GetString() ?? "";
                    
                    if (string.IsNullOrEmpty(name)) continue;

                    string culture = settlement.TryGetProperty("Culture", out var cultureEl) ? cultureEl.GetString() ?? "" : "";
                    string type = settlement.TryGetProperty("SettlementType", out var typeEl) ? typeEl.GetString() ?? "" : "";
                    string owner = settlement.TryGetProperty("OwnerName", out var ownerEl) ? ownerEl.GetString() ?? "" : "";

                    string description = type switch
                    {
                        "town" => "Город",
                        "castle" => "Замок",
                        "village" => "Деревня",
                        _ => "Поселение"
                    };

                    var entity = new GameEntity
                    {
                        Id = id,
                        Type = EntityType.Settlement,
                        Description = $"{description} ({culture})",
                        Context = $"Владелец: {owner}"
                    };
                    entity.SetName(name);
                    _entities.Add(entity);
                    count++;
                }

                Console.WriteLine($"Loaded {count} settlements");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading settlements: {ex.Message}");
            }
        }

        private void LoadKingdoms(string path)
        {
            try
            {
                string json = File.ReadAllText(path);
                using JsonDocument doc = JsonDocument.Parse(json);
                
                int count = 0;
                foreach (JsonElement kingdom in doc.RootElement.EnumerateArray())
                {
                    string id = kingdom.GetProperty("Id").GetString() ?? "";
                    string name = kingdom.GetProperty("Name").GetString() ?? "";
                    
                    if (string.IsNullOrEmpty(name)) continue;

                    string culture = kingdom.TryGetProperty("Culture", out var cultureEl) ? cultureEl.GetString() ?? "" : "";
                    string ruler = kingdom.TryGetProperty("RulerName", out var rulerEl) ? rulerEl.GetString() ?? "" : "";

                    var entity = new GameEntity
                    {
                        Id = id,
                        Type = EntityType.Kingdom,
                        Description = $"Королевство ({culture})",
                        Context = string.IsNullOrEmpty(ruler) ? "" : $"Правитель: {ruler}"
                    };
                    entity.SetName(name);
                    _entities.Add(entity);
                    count++;
                }

                Console.WriteLine($"Loaded {count} kingdoms");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading kingdoms: {ex.Message}");
            }
        }

        private void LoadClans(string path)
        {
            try
            {
                string json = File.ReadAllText(path);
                using JsonDocument doc = JsonDocument.Parse(json);
                
                int count = 0;
                foreach (JsonElement clan in doc.RootElement.EnumerateArray())
                {
                    string id = clan.GetProperty("Id").GetString() ?? "";
                    string name = clan.GetProperty("Name").GetString() ?? "";
                    
                    if (string.IsNullOrEmpty(name)) continue;

                    string culture = clan.TryGetProperty("Culture", out var cultureEl) ? cultureEl.GetString() ?? "" : "";
                    string leader = clan.TryGetProperty("LeaderName", out var leaderEl) ? leaderEl.GetString() ?? "" : "";
                    string kingdom = clan.TryGetProperty("Kingdom", out var kingdomEl) ? kingdomEl.GetString() ?? "" : "";

                    var entity = new GameEntity
                    {
                        Id = id,
                        Type = EntityType.Clan,
                        Description = $"Клан ({culture})",
                        Context = $"Лидер: {leader}, Королевство: {kingdom}"
                    };
                    entity.SetName(name);
                    _entities.Add(entity);
                    count++;
                }

                Console.WriteLine($"Loaded {count} clans");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading clans: {ex.Message}");
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

            Console.WriteLine($"Built prefix index with {_prefixIndex.Count} prefixes");
        }

        /// <summary>
        /// Search entities by prefix (case-insensitive)
        /// </summary>
        /// <param name="prefix">Search prefix (minimum 3 characters)</param>
        /// <param name="maxResults">Maximum number of results</param>
        /// <returns>Matching entities sorted by relevance</returns>
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
                // Filter indexed results by full prefix
                candidates = indexed
                    .Where(e => e.NameLower.StartsWith(prefixLower))
                    .ToList();
            }
            else
            {
                // Fallback to full scan (for prefixes not in index)
                candidates = _entities
                    .Where(e => e.NameLower.StartsWith(prefixLower))
                    .ToList();
            }

            // Sort by:
            // 1. Exact match first
            // 2. Then by entity type priority (Hero > Settlement > Kingdom > Clan)
            // 3. Then by name length (shorter names first)
            return candidates
                .OrderBy(e => e.NameLower == prefixLower ? 0 : 1)
                .ThenBy(e => GetTypePriority(e.Type))
                .ThenBy(e => e.Name.Length)
                .Take(maxResults)
                .ToList();
        }

        private int GetTypePriority(EntityType type)
        {
            return type switch
            {
                EntityType.Hero => 0,
                EntityType.Settlement => 1,
                EntityType.Kingdom => 2,
                EntityType.Clan => 3,
                _ => 4
            };
        }

        /// <summary>
        /// Get entity by ID
        /// </summary>
        public GameEntity? GetById(string id)
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



