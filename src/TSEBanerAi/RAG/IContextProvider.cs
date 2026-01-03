using System.Collections.Generic;
using System.Threading.Tasks;

namespace TSEBanerAi.RAG
{
    /// <summary>
    /// Interface for context providers that can retrieve information from the game
    /// </summary>
    public interface IContextProvider
    {
        /// <summary>
        /// Provider name for logging
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Priority (lower = checked first)
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Check if this provider can handle the query
        /// </summary>
        bool CanHandle(ContextQuery query);

        /// <summary>
        /// Retrieve context for the query
        /// </summary>
        Task<ContextResult> RetrieveAsync(ContextQuery query);
    }

    /// <summary>
    /// Query for context retrieval
    /// </summary>
    public class ContextQuery
    {
        /// <summary>
        /// Type of context requested
        /// </summary>
        public ContextType Type { get; set; }

        /// <summary>
        /// Entity ID or name to query about
        /// </summary>
        public string EntityId { get; set; }

        /// <summary>
        /// Additional parameters
        /// </summary>
        public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Original player message for context
        /// </summary>
        public string OriginalMessage { get; set; }
    }

    /// <summary>
    /// Types of context that can be requested
    /// </summary>
    public enum ContextType
    {
        /// <summary>
        /// Information about a specific hero/NPC
        /// </summary>
        Hero,

        /// <summary>
        /// Information about a settlement (town, castle, village)
        /// </summary>
        Settlement,

        /// <summary>
        /// Information about a clan
        /// </summary>
        Clan,

        /// <summary>
        /// Information about a kingdom
        /// </summary>
        Kingdom,

        /// <summary>
        /// Current wars and diplomacy
        /// </summary>
        Diplomacy,

        /// <summary>
        /// Relationship between two entities
        /// </summary>
        Relationship,

        /// <summary>
        /// Historical events from database
        /// </summary>
        History,

        /// <summary>
        /// General world state
        /// </summary>
        WorldState
    }

    /// <summary>
    /// Result of context retrieval
    /// </summary>
    public class ContextResult
    {
        /// <summary>
        /// Whether retrieval was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Retrieved context as formatted text
        /// </summary>
        public string Context { get; set; }

        /// <summary>
        /// Source of the context (for debugging)
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// Related entities found (for further queries)
        /// </summary>
        public List<string> RelatedEntities { get; set; } = new List<string>();

        /// <summary>
        /// Error message if failed
        /// </summary>
        public string Error { get; set; }

        public static ContextResult Ok(string context, string source)
        {
            return new ContextResult { Success = true, Context = context, Source = source };
        }

        public static ContextResult Fail(string error)
        {
            return new ContextResult { Success = false, Error = error };
        }
    }
}

