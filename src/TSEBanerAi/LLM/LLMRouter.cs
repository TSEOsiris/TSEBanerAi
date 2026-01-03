using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TSEBanerAi.Utils;

namespace TSEBanerAi.LLM
{
    /// <summary>
    /// Routes requests to available LLM providers with fallback support
    /// </summary>
    public class LLMRouter
    {
        private readonly List<ILLMProvider> _providers;
        private readonly FallbackResponses _fallback;
        private ILLMProvider _preferredProvider;

        /// <summary>
        /// Currently active provider
        /// </summary>
        public ILLMProvider CurrentProvider => _preferredProvider ?? _providers.FirstOrDefault(p => p.IsAvailable);

        /// <summary>
        /// All registered providers
        /// </summary>
        public IReadOnlyList<ILLMProvider> Providers => _providers.AsReadOnly();

        public LLMRouter()
        {
            _providers = new List<ILLMProvider>();
            _fallback = new FallbackResponses();
        }

        /// <summary>
        /// Add provider to router
        /// </summary>
        public void AddProvider(ILLMProvider provider)
        {
            if (provider == null) return;
            
            _providers.Add(provider);
            _providers.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            
            ModLogger.LogDebug($"Added LLM provider: {provider.Name} (priority {provider.Priority})");
        }

        /// <summary>
        /// Set preferred provider by name
        /// </summary>
        public void SetPreferredProvider(string providerName)
        {
            _preferredProvider = _providers.FirstOrDefault(p => 
                p.Name.Equals(providerName, StringComparison.OrdinalIgnoreCase));
            
            if (_preferredProvider != null)
            {
                ModLogger.LogDebug($"Preferred provider set to: {_preferredProvider.Name}");
            }
        }

        /// <summary>
        /// Check all providers for availability
        /// </summary>
        public async Task RefreshAvailabilityAsync(CancellationToken cancellationToken = default)
        {
            // Create a copy to avoid collection modification during iteration
            var providersCopy = _providers.ToList();
            foreach (var provider in providersCopy)
            {
                await provider.CheckAvailabilityAsync(cancellationToken);
            }
        }

        /// <summary>
        /// Generate response using best available provider
        /// </summary>
        public async Task<LLMResponse> GenerateAsync(LLMRequest request, CancellationToken cancellationToken = default)
        {
            if (_providers.Count == 0)
            {
                ModLogger.LogError("No LLM providers configured");
                return _fallback.GetFallbackResponse("no_provider");
            }

            // Get providers to try (preferred first, then by priority)
            var providersToTry = GetProvidersToTry();

            foreach (var provider in providersToTry)
            {
                try
                {
                    // Check availability
                    if (!await provider.CheckAvailabilityAsync(cancellationToken))
                    {
                        ModLogger.LogDebug($"Provider {provider.Name} not available, trying next");
                        continue;
                    }

                    ModLogger.LogDebug($"Sending request to {provider.Name}");
                    var response = await provider.GenerateAsync(request, cancellationToken);

                    if (response.Success)
                    {
                        ModLogger.LogDebug($"Response received from {provider.Name} in {response.ResponseTimeMs}ms");
                        return response;
                    }

                    ModLogger.LogDebug($"Provider {provider.Name} failed: {response.Error}");
                }
                catch (Exception ex)
                {
                    ModLogger.LogException($"Error with provider {provider.Name}", ex);
                }
            }

            // All providers failed, return fallback
            ModLogger.LogError("All LLM providers failed, using fallback response");
            return _fallback.GetFallbackResponse("error");
        }

        /// <summary>
        /// Generate with retry logic
        /// </summary>
        public async Task<LLMResponse> GenerateWithRetryAsync(
            LLMRequest request, 
            int maxRetries = 2,
            CancellationToken cancellationToken = default)
        {
            LLMResponse lastResponse = null;

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                if (attempt > 0)
                {
                    ModLogger.LogDebug($"Retry attempt {attempt}/{maxRetries}");
                    await Task.Delay(1000 * attempt, cancellationToken); // Exponential backoff
                }

                lastResponse = await GenerateAsync(request, cancellationToken);

                if (lastResponse.Success && !lastResponse.IsFallback)
                {
                    return lastResponse;
                }
            }

            // Return fallback if all retries failed
            return lastResponse ?? _fallback.GetFallbackResponse("error");
        }

        private IEnumerable<ILLMProvider> GetProvidersToTry()
        {
            // If preferred provider is set and available, try it first
            if (_preferredProvider != null)
            {
                yield return _preferredProvider;
            }

            // Then try all providers by priority
            foreach (var provider in _providers.OrderBy(p => p.Priority))
            {
                if (provider != _preferredProvider)
                {
                    yield return provider;
                }
            }
        }
    }

    /// <summary>
    /// Fallback responses when LLM is unavailable
    /// </summary>
    public class FallbackResponses
    {
        private readonly Dictionary<string, string[]> _responses;
        private readonly Random _random;

        public FallbackResponses()
        {
            _random = new Random();
            _responses = new Dictionary<string, string[]>
            {
                ["no_provider"] = new[]
                {
                    "*looks at you expectantly*",
                    "I'm listening...",
                    "What is it you need?"
                },
                ["error"] = new[]
                {
                    "*seems distracted for a moment*",
                    "I... my thoughts are elsewhere. What were you saying?",
                    "*pauses thoughtfully*"
                },
                ["timeout"] = new[]
                {
                    "*takes a moment to consider*",
                    "Give me a moment to think about that...",
                    "*appears to be deep in thought*"
                }
            };
        }

        public LLMResponse GetFallbackResponse(string category)
        {
            if (!_responses.TryGetValue(category, out var options))
            {
                options = _responses["error"];
            }

            var content = options[_random.Next(options.Length)];
            return LLMResponse.Fallback(content);
        }
    }
}

