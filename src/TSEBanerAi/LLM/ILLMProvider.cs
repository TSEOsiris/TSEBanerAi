using System.Threading;
using System.Threading.Tasks;

namespace TSEBanerAi.LLM
{
    /// <summary>
    /// Interface for LLM providers (Ollama, API, etc.)
    /// </summary>
    public interface ILLMProvider
    {
        /// <summary>
        /// Provider name for logging
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Whether provider is available
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Priority (lower = try first)
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Check if provider is available
        /// </summary>
        Task<bool> CheckAvailabilityAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Generate response
        /// </summary>
        Task<LLMResponse> GenerateAsync(LLMRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get current model name
        /// </summary>
        string GetModelName();
    }
}

