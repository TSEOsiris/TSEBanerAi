using System;
using System.Collections.Generic;

namespace TSEBanerAi.LLM
{
    /// <summary>
    /// State of an LLM request
    /// </summary>
    public enum LLMRequestState
    {
        /// <summary>
        /// No active request
        /// </summary>
        Idle,
        
        /// <summary>
        /// Initial processing, sending to LLM
        /// </summary>
        Thinking,
        
        /// <summary>
        /// Fetching additional context (RAG, database lookups)
        /// </summary>
        FetchingContext,
        
        /// <summary>
        /// LLM is generating the response
        /// </summary>
        Generating,
        
        /// <summary>
        /// Response is ready
        /// </summary>
        Complete
    }

    /// <summary>
    /// Debug log entry for LLM request
    /// </summary>
    public class DebugLogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Message { get; set; } = "";
        public LLMRequestState State { get; set; }

        public DebugLogEntry(string message, LLMRequestState state)
        {
            Timestamp = DateTime.Now;
            Message = message;
            State = state;
        }

        public override string ToString()
        {
            return $"[{Timestamp:HH:mm:ss}] {Message}";
        }
    }

    /// <summary>
    /// Status information for current LLM request
    /// </summary>
    public class LLMRequestStatus
    {
        public LLMRequestState State { get; set; } = LLMRequestState.Idle;
        public string NpcName { get; set; } = "NPC";
        public string UserMessage { get; set; } = "";
        public string Response { get; set; }
        public List<DebugLogEntry> DebugLog { get; set; } = new List<DebugLogEntry>();
        public int TokensGenerated { get; set; } = 0;
        public int TokensTotal { get; set; } = 0;
        public List<string> ContextSources { get; set; } = new List<string>();
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Get display text for normal mode
        /// </summary>
        public string GetNormalModeText()
        {
            switch (State)
            {
                case LLMRequestState.Thinking: return $"{NpcName} is thinking...";
                case LLMRequestState.FetchingContext: return $"{NpcName} is recalling...";
                case LLMRequestState.Generating: return $"{NpcName} is composing response...";
                default: return "";
            }
        }

        /// <summary>
        /// Get display text for debug mode
        /// </summary>
        public string GetDebugModeText()
        {
            switch (State)
            {
                case LLMRequestState.Thinking: return "Sending request to LLM...";
                case LLMRequestState.FetchingContext: return $"Fetching context: {string.Join(", ", ContextSources)}";
                case LLMRequestState.Generating: return $"Generating response ({TokensGenerated}/{TokensTotal} tokens)";
                case LLMRequestState.Complete: return "Response received";
                default: return "Waiting...";
            }
        }

        /// <summary>
        /// Check if request is active
        /// </summary>
        public bool IsActive
        {
            get { return State != LLMRequestState.Idle && State != LLMRequestState.Complete; }
        }

        /// <summary>
        /// Get elapsed time
        /// </summary>
        public TimeSpan Elapsed
        {
            get { return (EndTime ?? DateTime.Now) - StartTime; }
        }
    }
}



