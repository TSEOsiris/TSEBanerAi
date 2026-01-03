using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TSEBanerAi.LLM;
using TSEBanerAi.Utils;

namespace TSEBanerAi.RAG
{
    /// <summary>
    /// Manages LLM queries with context retrieval loop
    /// Phase 1: LLM analyzes message and requests context
    /// Phase 2: Context is retrieved and final response generated
    /// </summary>
    public class LLMQueryManager
    {
        private static LLMQueryManager _instance;
        private static readonly object _lock = new object();

        /// <summary>
        /// Maximum number of context retrieval rounds
        /// </summary>
        public int MaxContextRounds { get; set; } = 2;

        /// <summary>
        /// Whether to use multi-phase context retrieval
        /// </summary>
        public bool UseMultiPhase { get; set; } = true;

        public static LLMQueryManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new LLMQueryManager();
                        }
                    }
                }
                return _instance;
            }
        }

        private LLMQueryManager() { }

        /// <summary>
        /// Process a message with optional multi-phase context retrieval
        /// </summary>
        public async Task<QueryResult> ProcessMessageAsync(
            LLMRequest baseRequest,
            string playerMessage,
            CancellationToken cancellationToken = default)
        {
            var result = new QueryResult();
            var additionalContext = new StringBuilder();

            try
            {
                // Phase 0: RAG - Extract entities from message and get context
                var ragContext = await ContextRetriever.Instance.RetrieveContextForMessage(playerMessage);
                if (!string.IsNullOrEmpty(ragContext))
                {
                    additionalContext.AppendLine("=== ADDITIONAL CONTEXT (RAG) ===");
                    additionalContext.AppendLine(ragContext);
                    result.ContextSources.Add("RAG");
                    ModLogger.LogDebug($"[LLMQuery] RAG retrieved context: {ragContext.Length} chars");
                }

                if (UseMultiPhase)
                {
                    // Phase 1: Ask LLM if it needs more context
                    var contextNeeds = await AnalyzeContextNeedsAsync(baseRequest, playerMessage, ragContext, cancellationToken);
                    
                    if (contextNeeds.NeedsMoreContext && contextNeeds.Queries.Count > 0)
                    {
                        ModLogger.LogDebug($"[LLMQuery] LLM requested {contextNeeds.Queries.Count} context queries");
                        
                        // Phase 1.5: Retrieve requested context
                        foreach (var query in contextNeeds.Queries)
                        {
                            var contextResult = await ContextRetriever.Instance.RetrieveAsync(query);
                            if (contextResult.Success)
                            {
                                additionalContext.AppendLine($"=== {query.Type}: {query.EntityId} ===");
                                additionalContext.AppendLine(contextResult.Context);
                                result.ContextSources.Add($"{query.Type}:{query.EntityId}");
                            }
                        }
                    }
                }

                // Phase 2: Generate final response with all context
                var enrichedRequest = EnrichRequest(baseRequest, additionalContext.ToString());
                var response = await LLMManager.Instance.GenerateAsync(enrichedRequest, cancellationToken);

                result.Response = response;
                result.Success = response.Success;

                return result;
            }
            catch (Exception ex)
            {
                ModLogger.LogException("[LLMQuery] Failed to process message", ex);
                result.Success = false;
                result.Error = ex.Message;
                return result;
            }
        }

        /// <summary>
        /// Phase 1: Ask LLM what context it needs
        /// </summary>
        private async Task<ContextNeedsResult> AnalyzeContextNeedsAsync(
            LLMRequest baseRequest,
            string playerMessage,
            string existingContext,
            CancellationToken cancellationToken)
        {
            var result = new ContextNeedsResult();

            try
            {
                // Build analysis prompt
                var analysisPrompt = BuildContextAnalysisPrompt(playerMessage, existingContext);
                
                var analysisRequest = new LLMRequest
                {
                    SystemPrompt = analysisPrompt,
                    Messages = new List<LLMMessage>
                    {
                        LLMMessage.User(playerMessage)
                    },
                    MaxTokens = 256, // Short response for analysis
                    Temperature = 0.3f // More deterministic
                };

                var response = await LLMManager.Instance.GenerateAsync(analysisRequest, cancellationToken);

                if (response.Success)
                {
                    // Parse context requests from response
                    result = ParseContextNeeds(response.Content);
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogException("[LLMQuery] Context analysis failed", ex);
            }

            return result;
        }

        private string BuildContextAnalysisPrompt(string playerMessage, string existingContext)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("You are a context analyzer for a Bannerlord dialogue system.");
            sb.AppendLine("Determine if additional game information is needed to answer the player's message.");
            sb.AppendLine();
            sb.AppendLine("Available context types:");
            sb.AppendLine("- HERO: Information about a specific character");
            sb.AppendLine("- SETTLEMENT: Information about a town, castle, or village");
            sb.AppendLine("- KINGDOM: Information about a kingdom/faction");
            sb.AppendLine("- DIPLOMACY: Current wars and alliances");
            sb.AppendLine();
            
            if (!string.IsNullOrEmpty(existingContext))
            {
                sb.AppendLine("Already available context:");
                sb.AppendLine(existingContext);
                sb.AppendLine();
            }
            
            sb.AppendLine("If you need more information, respond with JSON:");
            sb.AppendLine("```json");
            sb.AppendLine("{\"need_context\": true, \"queries\": [{\"type\": \"HERO\", \"entity\": \"name\"}]}");
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("If you have enough context, respond with:");
            sb.AppendLine("```json");
            sb.AppendLine("{\"need_context\": false}");
            sb.AppendLine("```");

            return sb.ToString();
        }

        private ContextNeedsResult ParseContextNeeds(string response)
        {
            var result = new ContextNeedsResult();

            try
            {
                // Extract JSON from response
                var jsonMatch = Regex.Match(response, @"\{[^{}]*\""need_context\""[^{}]*\}|\{[^{}]*\""queries\""[^{}]*\[.*?\][^{}]*\}", 
                    RegexOptions.Singleline);
                
                if (jsonMatch.Success)
                {
                    var json = JObject.Parse(jsonMatch.Value);
                    result.NeedsMoreContext = json["need_context"]?.Value<bool>() ?? false;

                    if (result.NeedsMoreContext && json["queries"] is JArray queries)
                    {
                        foreach (JObject q in queries)
                        {
                            var typeStr = q["type"]?.ToString()?.ToUpperInvariant() ?? "";
                            var entity = q["entity"]?.ToString() ?? "";

                            if (!string.IsNullOrEmpty(entity))
                            {
                                var query = new ContextQuery
                                {
                                    EntityId = entity,
                                    Type = ParseContextType(typeStr)
                                };
                                result.Queries.Add(query);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogDebug($"[LLMQuery] Failed to parse context needs: {ex.Message}");
            }

            return result;
        }

        private ContextType ParseContextType(string type)
        {
            switch (type)
            {
                case "HERO": return ContextType.Hero;
                case "SETTLEMENT": return ContextType.Settlement;
                case "KINGDOM": return ContextType.Kingdom;
                case "DIPLOMACY": return ContextType.Diplomacy;
                case "CLAN": return ContextType.Clan;
                default: return ContextType.WorldState;
            }
        }

        private LLMRequest EnrichRequest(LLMRequest baseRequest, string additionalContext)
        {
            if (string.IsNullOrEmpty(additionalContext))
                return baseRequest;

            var enrichedPrompt = baseRequest.SystemPrompt + "\n\n" + additionalContext;

            return new LLMRequest
            {
                SystemPrompt = enrichedPrompt,
                Messages = baseRequest.Messages,
                MaxTokens = baseRequest.MaxTokens,
                Temperature = baseRequest.Temperature,
                TopP = baseRequest.TopP,
                StopSequences = baseRequest.StopSequences,
                TimeoutSeconds = baseRequest.TimeoutSeconds,
                NpcId = baseRequest.NpcId
            };
        }
    }

    /// <summary>
    /// Result of context needs analysis
    /// </summary>
    public class ContextNeedsResult
    {
        public bool NeedsMoreContext { get; set; }
        public List<ContextQuery> Queries { get; set; } = new List<ContextQuery>();
    }

    /// <summary>
    /// Result of full query processing
    /// </summary>
    public class QueryResult
    {
        public bool Success { get; set; }
        public LLMResponse Response { get; set; }
        public List<string> ContextSources { get; set; } = new List<string>();
        public string Error { get; set; }
    }
}

