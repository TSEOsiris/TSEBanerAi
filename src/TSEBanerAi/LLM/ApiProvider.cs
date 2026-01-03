using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TSEBanerAi.Utils;

namespace TSEBanerAi.LLM
{
    /// <summary>
    /// LLM provider for external APIs (OpenAI-compatible)
    /// Supports Groq, Together.ai, OpenRouter, etc.
    /// </summary>
    public class ApiProvider : ILLMProvider
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _apiKey;
        private readonly string _model;
        private readonly string _providerName;
        private bool _isAvailable;
        private DateTime _lastCheck;

        public string Name => _providerName;
        public bool IsAvailable => _isAvailable;
        public int Priority => 2; // Fallback after Ollama

        /// <summary>
        /// Create API provider for Groq
        /// </summary>
        public static ApiProvider CreateGroq(string apiKey, string model = "llama-3.3-70b-versatile")
        {
            return new ApiProvider(
                "https://api.groq.com/openai/v1",
                apiKey,
                model,
                "Groq"
            );
        }

        /// <summary>
        /// Create API provider for Together.ai
        /// </summary>
        public static ApiProvider CreateTogether(string apiKey, string model = "meta-llama/Llama-3-70b-chat-hf")
        {
            return new ApiProvider(
                "https://api.together.xyz/v1",
                apiKey,
                model,
                "Together"
            );
        }

        /// <summary>
        /// Create API provider for OpenRouter
        /// </summary>
        public static ApiProvider CreateOpenRouter(string apiKey, string model = "meta-llama/llama-3.1-70b-instruct")
        {
            return new ApiProvider(
                "https://openrouter.ai/api/v1",
                apiKey,
                model,
                "OpenRouter"
            );
        }

        /// <summary>
        /// Create custom API provider
        /// </summary>
        public ApiProvider(string baseUrl, string apiKey, string model, string providerName)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _apiKey = apiKey;
            _model = model;
            _providerName = providerName;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(90)
            };
            
            if (!string.IsNullOrEmpty(_apiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
            }

            _isAvailable = !string.IsNullOrEmpty(_apiKey);
            _lastCheck = DateTime.MinValue;
        }

        public string GetModelName() => _model;

        public async Task<bool> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
        {
            // If no API key, not available
            if (string.IsNullOrEmpty(_apiKey))
            {
                _isAvailable = false;
                return false;
            }

            try
            {
                // Cache availability check for 60 seconds
                if ((DateTime.UtcNow - _lastCheck).TotalSeconds < 60)
                {
                    return _isAvailable;
                }

                var response = await _httpClient.GetAsync($"{_baseUrl}/models", cancellationToken);
                _isAvailable = response.IsSuccessStatusCode;
                _lastCheck = DateTime.UtcNow;

                if (_isAvailable)
                {
                    ModLogger.LogDebug($"{_providerName} API is available");
                }

                return _isAvailable;
            }
            catch (Exception ex)
            {
                ModLogger.LogDebug($"{_providerName} API not available: {ex.Message}");
                _isAvailable = false;
                _lastCheck = DateTime.UtcNow;
                return false;
            }
        }

        public async Task<LLMResponse> GenerateAsync(LLMRequest request, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                return LLMResponse.Fail("API key not configured", Name);
            }

            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Build messages array for OpenAI-compatible API
                var messages = new JArray();

                // Add system message
                if (!string.IsNullOrEmpty(request.SystemPrompt))
                {
                    messages.Add(new JObject
                    {
                        ["role"] = "system",
                        ["content"] = request.SystemPrompt
                    });
                }

                // Add conversation messages
                foreach (var msg in request.Messages)
                {
                    messages.Add(new JObject
                    {
                        ["role"] = msg.Role,
                        ["content"] = msg.Content
                    });
                }

                var requestBody = new JObject
                {
                    ["model"] = _model,
                    ["messages"] = messages,
                    ["max_tokens"] = request.MaxTokens,
                    ["temperature"] = request.Temperature,
                    ["top_p"] = request.TopP,
                    ["stream"] = false
                };

                if (request.StopSequences != null && request.StopSequences.Count > 0)
                {
                    requestBody["stop"] = new JArray(request.StopSequences);
                }

                var content = new StringContent(
                    requestBody.ToString(),
                    Encoding.UTF8,
                    "application/json"
                );

                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    cts.CancelAfter(TimeSpan.FromSeconds(request.TimeoutSeconds));

                    var httpResponse = await _httpClient.PostAsync(
                        $"{_baseUrl}/chat/completions",
                        content,
                        cts.Token
                    );

                    if (!httpResponse.IsSuccessStatusCode)
                    {
                        var error = await httpResponse.Content.ReadAsStringAsync();
                        return LLMResponse.Fail($"{Name} error: {httpResponse.StatusCode} - {error}", Name);
                    }

                    var responseText = await httpResponse.Content.ReadAsStringAsync();
                    var responseJson = JObject.Parse(responseText);

                    stopwatch.Stop();

                    var responseContent = responseJson["choices"]?[0]?["message"]?["content"]?.ToString() ?? "";
                    
                    var response = LLMResponse.Ok(responseContent, Name, _model);
                    response.ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds;

                    // Get token counts
                    var usage = responseJson["usage"];
                    if (usage != null)
                    {
                        response.PromptTokens = usage["prompt_tokens"]?.Value<int>() ?? 0;
                        response.CompletionTokens = usage["completion_tokens"]?.Value<int>() ?? 0;
                    }

                    return response;
                }
            }
            catch (OperationCanceledException)
            {
                return LLMResponse.Fail("Request timed out", Name);
            }
            catch (Exception ex)
            {
                ModLogger.LogException($"{Name} request failed", ex);
                return LLMResponse.Fail($"{Name} error: {ex.Message}", Name);
            }
        }
    }
}

