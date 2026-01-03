using System;

namespace TSEBanerAi.LLM
{
    /// <summary>
    /// Response from LLM provider
    /// </summary>
    public class LLMResponse
    {
        /// <summary>
        /// Whether request was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Generated text content
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// Error message if failed
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// Provider that generated this response
        /// </summary>
        public string Provider { get; set; }

        /// <summary>
        /// Model used
        /// </summary>
        public string Model { get; set; }

        /// <summary>
        /// Tokens used in prompt
        /// </summary>
        public int PromptTokens { get; set; }

        /// <summary>
        /// Tokens generated
        /// </summary>
        public int CompletionTokens { get; set; }

        /// <summary>
        /// Total tokens used
        /// </summary>
        public int TotalTokens => PromptTokens + CompletionTokens;

        /// <summary>
        /// Response time in milliseconds
        /// </summary>
        public int ResponseTimeMs { get; set; }

        /// <summary>
        /// Request timestamp
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether this was a fallback response
        /// </summary>
        public bool IsFallback { get; set; }

        /// <summary>
        /// Create successful response
        /// </summary>
        public static LLMResponse Ok(string content, string provider, string model)
        {
            return new LLMResponse
            {
                Success = true,
                Content = content,
                Provider = provider,
                Model = model
            };
        }

        /// <summary>
        /// Create failed response
        /// </summary>
        public static LLMResponse Fail(string error, string? provider = null)
        {
            return new LLMResponse
            {
                Success = false,
                Error = error,
                Provider = provider ?? ""
            };
        }

        /// <summary>
        /// Create fallback response
        /// </summary>
        public static LLMResponse Fallback(string content)
        {
            return new LLMResponse
            {
                Success = true,
                Content = content,
                Provider = "fallback",
                Model = "static",
                IsFallback = true
            };
        }
    }
}

