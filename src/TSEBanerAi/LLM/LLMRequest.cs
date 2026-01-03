using System.Collections.Generic;

namespace TSEBanerAi.LLM
{
    /// <summary>
    /// Request to send to LLM provider
    /// </summary>
    public class LLMRequest
    {
        /// <summary>
        /// System prompt (instructions for LLM)
        /// </summary>
        public string SystemPrompt { get; set; }

        /// <summary>
        /// Conversation history (alternating user/assistant messages)
        /// </summary>
        public List<LLMMessage> Messages { get; set; } = new List<LLMMessage>();

        /// <summary>
        /// Maximum tokens to generate
        /// </summary>
        public int MaxTokens { get; set; } = 1024;

        /// <summary>
        /// Temperature (0.0-2.0, lower = more deterministic)
        /// </summary>
        public float Temperature { get; set; } = 0.7f;

        /// <summary>
        /// Top P sampling
        /// </summary>
        public float TopP { get; set; } = 0.9f;

        /// <summary>
        /// Whether to stream response
        /// </summary>
        public bool Stream { get; set; } = false;

        /// <summary>
        /// Custom stop sequences
        /// </summary>
        public List<string> StopSequences { get; set; }

        /// <summary>
        /// Request timeout in seconds
        /// </summary>
        public int TimeoutSeconds { get; set; } = 60;

        /// <summary>
        /// Context about current NPC (for logging/tracking)
        /// </summary>
        public string NpcId { get; set; }

        /// <summary>
        /// Request type for metrics
        /// </summary>
        public string RequestType { get; set; } = "dialogue";
    }

    /// <summary>
    /// Single message in conversation
    /// </summary>
    public class LLMMessage
    {
        /// <summary>
        /// Role: "user", "assistant", or "system"
        /// </summary>
        public string Role { get; set; }

        /// <summary>
        /// Message content
        /// </summary>
        public string Content { get; set; }

        public LLMMessage() { }

        public LLMMessage(string role, string content)
        {
            Role = role;
            Content = content;
        }

        public static LLMMessage User(string content) => new LLMMessage("user", content);
        public static LLMMessage Assistant(string content) => new LLMMessage("assistant", content);
        public static LLMMessage System(string content) => new LLMMessage("system", content);
    }
}

