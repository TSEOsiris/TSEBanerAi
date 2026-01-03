using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TSEBanerAi.Context;
using TSEBanerAi.UI.Autocomplete;
using TSEBanerAi.Utils;

namespace TSEBanerAi.RAG
{
    /// <summary>
    /// Main RAG system for retrieving context from the game
    /// </summary>
    public class ContextRetriever
    {
        private static ContextRetriever _instance;
        private static readonly object _lock = new object();

        private readonly List<IContextProvider> _providers;
        private readonly EntityIndex _entityIndex;

        public static ContextRetriever Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new ContextRetriever();
                        }
                    }
                }
                return _instance;
            }
        }

        private ContextRetriever()
        {
            _providers = new List<IContextProvider>();
            _entityIndex = new EntityIndex();
            
            // Register default providers
            RegisterProvider(new HeroContextProvider());
            RegisterProvider(new SettlementContextProvider());
            RegisterProvider(new KingdomContextProvider());
            RegisterProvider(new DiplomacyContextProvider());
        }

        /// <summary>
        /// Register a context provider
        /// </summary>
        public void RegisterProvider(IContextProvider provider)
        {
            _providers.Add(provider);
            _providers.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            ModLogger.LogDebug($"[RAG] Registered provider: {provider.Name}");
        }

        /// <summary>
        /// Initialize the retriever (load entity index)
        /// </summary>
        public void Initialize()
        {
            try
            {
                _entityIndex.Refresh();
                ModLogger.LogDebug($"[RAG] Initialized with {_entityIndex.EntityCount} entities");
            }
            catch (Exception ex)
            {
                ModLogger.LogException("[RAG] Failed to initialize", ex);
            }
        }

        /// <summary>
        /// Extract entities mentioned in a message (with @ prefix or by name)
        /// </summary>
        public List<GameEntity> ExtractMentionedEntities(string message)
        {
            var entities = new List<GameEntity>();
            
            if (string.IsNullOrEmpty(message)) return entities;

            try
            {
                // Find @mentions
                var mentionPattern = new Regex(@"@(\w+(?:\s+\w+)?)");
                var matches = mentionPattern.Matches(message);
                
                foreach (Match match in matches)
                {
                    var name = match.Groups[1].Value;
                    var found = _entityIndex.Search(name, 1).FirstOrDefault();
                    if (found != null)
                    {
                        entities.Add(found);
                    }
                }

                // Also search for entity names without @
                var allEntities = _entityIndex.GetAllEntities();
                foreach (var entity in allEntities)
                {
                    if (message.IndexOf(entity.Name, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (!entities.Any(e => e.Id == entity.Id))
                        {
                            entities.Add(entity);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogException("[RAG] Failed to extract entities", ex);
            }

            return entities;
        }

        /// <summary>
        /// Retrieve context for mentioned entities
        /// </summary>
        public async Task<string> RetrieveContextForMessage(string message)
        {
            var contextBuilder = new StringBuilder();
            var mentionedEntities = ExtractMentionedEntities(message);

            if (mentionedEntities.Count == 0)
            {
                return string.Empty;
            }

            ModLogger.LogDebug($"[RAG] Found {mentionedEntities.Count} mentioned entities");

            foreach (var entity in mentionedEntities.Take(5)) // Limit to 5 to avoid token overflow
            {
                var query = new ContextQuery
                {
                    EntityId = entity.Id,
                    Type = MapEntityTypeToContextType(entity.Type),
                    OriginalMessage = message
                };

                var result = await RetrieveAsync(query);
                
                if (result.Success && !string.IsNullOrEmpty(result.Context))
                {
                    contextBuilder.AppendLine($"=== Information about {entity.Name} ===");
                    contextBuilder.AppendLine(result.Context);
                    contextBuilder.AppendLine();
                }
            }

            return contextBuilder.ToString();
        }

        /// <summary>
        /// Retrieve context using registered providers
        /// </summary>
        public async Task<ContextResult> RetrieveAsync(ContextQuery query)
        {
            foreach (var provider in _providers)
            {
                if (provider.CanHandle(query))
                {
                    try
                    {
                        var result = await provider.RetrieveAsync(query);
                        if (result.Success)
                        {
                            ModLogger.LogDebug($"[RAG] Retrieved context from {provider.Name}");
                            return result;
                        }
                    }
                    catch (Exception ex)
                    {
                        ModLogger.LogException($"[RAG] Provider {provider.Name} failed", ex);
                    }
                }
            }

            return ContextResult.Fail("No provider could handle the query");
        }

        private ContextType MapEntityTypeToContextType(EntityType entityType)
        {
            switch (entityType)
            {
                case EntityType.Hero: return ContextType.Hero;
                case EntityType.Settlement: return ContextType.Settlement;
                case EntityType.Kingdom: return ContextType.Kingdom;
                case EntityType.Clan: return ContextType.Clan;
                default: return ContextType.WorldState;
            }
        }
    }
}

