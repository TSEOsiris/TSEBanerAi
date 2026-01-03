using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TSEBanerAi.Utils;

namespace TSEBanerAi.LLM
{
    /// <summary>
    /// LLM provider for LM Studio local server
    /// LM Studio provides OpenAI-compatible API on localhost:1234
    /// </summary>
    public class LMStudioProvider : ILLMProvider
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _model;
        private bool _isAvailable;
        private DateTime _lastCheck;

        public string Name => "LM Studio";
        public bool IsAvailable => _isAvailable;
        public int Priority => 0; // Highest priority - try LM Studio first

        /// <summary>
        /// Create LM Studio provider with default settings (localhost:1234)
        /// </summary>
        public LMStudioProvider() : this("http://localhost:1234", "")
        {
        }

        /// <summary>
        /// Create LM Studio provider with custom settings
        /// </summary>
        /// <param name="baseUrl">Base URL (default: http://localhost:1234)</param>
        /// <param name="model">Model name (empty = use loaded model)</param>
        public LMStudioProvider(string baseUrl, string model = "")
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _model = model;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(180) // LM Studio can be slow on first request
            };
            _isAvailable = false;
            _lastCheck = DateTime.MinValue;
        }

        public string GetModelName() => string.IsNullOrEmpty(_model) ? "loaded-model" : _model;

        public async Task<bool> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
        {
            ModLogger.LogDebug($"[LMStudio] CheckAvailabilityAsync called. BaseURL: {_baseUrl}");
            
            try
            {
                // Cache availability check for 30 seconds
                if ((DateTime.UtcNow - _lastCheck).TotalSeconds < 30)
                {
                    ModLogger.LogDebug($"[LMStudio] Using cached availability: {_isAvailable}");
                    return _isAvailable;
                }

                ModLogger.LogDebug($"[LMStudio] Checking {_baseUrl}/v1/models...");
                
                // LM Studio uses OpenAI-compatible /v1/models endpoint
                var response = await _httpClient.GetAsync($"{_baseUrl}/v1/models", cancellationToken);
                _isAvailable = response.IsSuccessStatusCode;
                _lastCheck = DateTime.UtcNow;

                ModLogger.LogDebug($"[LMStudio] Check result: {response.StatusCode}, Available: {_isAvailable}");

                if (_isAvailable)
                {
                    // Try to get loaded model name
                    var content = await response.Content.ReadAsStringAsync();
                    var json = JObject.Parse(content);
                    var models = json["data"] as JArray;
                    if (models != null && models.Count > 0)
                    {
                        var modelId = models[0]["id"]?.ToString();
                        ModLogger.LogDebug($"[LMStudio] Available at {_baseUrl}, model: {modelId}");
                    }
                    else
                    {
                        ModLogger.LogDebug($"[LMStudio] Available at {_baseUrl} (no model info)");
                    }
                }

                return _isAvailable;
            }
            catch (Exception ex)
            {
                ModLogger.LogDebug($"[LMStudio] Not available: {ex.Message}");
                _isAvailable = false;
                _lastCheck = DateTime.UtcNow;
                return false;
            }
        }

        public async Task<LLMResponse> GenerateAsync(LLMRequest request, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();

            ModLogger.LogDebug($"[LMStudio] GenerateAsync called. URL: {_baseUrl}/v1/chat/completions");

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
                    ["messages"] = messages,
                    ["max_tokens"] = request.MaxTokens,
                    ["temperature"] = request.Temperature,
                    ["top_p"] = request.TopP,
                    ["stream"] = false
                };

                // Only add model if specified (otherwise LM Studio uses loaded model)
                if (!string.IsNullOrEmpty(_model))
                {
                    requestBody["model"] = _model;
                }

                ModLogger.LogDebug($"[LMStudio] Request body built. Messages: {messages.Count}, MaxTokens: {request.MaxTokens}");

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

                    ModLogger.LogDebug($"[LMStudio] Sending HTTP POST to {_baseUrl}/v1/chat/completions...");

                    var httpResponse = await _httpClient.PostAsync(
                        $"{_baseUrl}/v1/chat/completions",
                        content,
                        cts.Token
                    );

                    ModLogger.LogDebug($"[LMStudio] HTTP Response: {httpResponse.StatusCode}");

                    if (!httpResponse.IsSuccessStatusCode)
                    {
                        var error = await httpResponse.Content.ReadAsStringAsync();
                        ModLogger.LogError($"[LMStudio] Error response: {error}");
                        return LLMResponse.Fail($"LM Studio error: {httpResponse.StatusCode} - {error}", Name);
                    }

                    var responseText = await httpResponse.Content.ReadAsStringAsync();
                    ModLogger.LogDebug($"[LMStudio] Response received, length: {responseText.Length} chars");
                    
                    var responseJson = JObject.Parse(responseText);

                    stopwatch.Stop();

                    var responseContent = responseJson["choices"]?[0]?["message"]?["content"]?.ToString() ?? "";
                    var modelUsed = responseJson["model"]?.ToString() ?? GetModelName();

                    ModLogger.LogDebug($"[LMStudio] Parsed content length: {responseContent.Length}, Model: {modelUsed}");

                    var response = LLMResponse.Ok(responseContent, Name, modelUsed);
                    response.ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds;

                    // Get token counts
                    var usage = responseJson["usage"];
                    if (usage != null)
                    {
                        response.PromptTokens = usage["prompt_tokens"]?.Value<int>() ?? 0;
                        response.CompletionTokens = usage["completion_tokens"]?.Value<int>() ?? 0;
                    }

                    // Clean response (remove thinking tags if present)
                    response.Content = CleanResponse(response.Content);

                    return response;
                }
            }
            catch (OperationCanceledException)
            {
                return LLMResponse.Fail("Request timed out", Name);
            }
            catch (Exception ex)
            {
                ModLogger.LogException("LM Studio request failed", ex);
                return LLMResponse.Fail($"LM Studio error: {ex.Message}", Name);
            }
        }

        /// <summary>
        /// Clean LLM response (remove thinking tags, etc.)
        /// </summary>
        private string CleanResponse(string response)
        {
            if (string.IsNullOrEmpty(response))
                return response;

            // Remove <think>...</think> tags and content
            var thinkStart = response.IndexOf("<think>", StringComparison.OrdinalIgnoreCase);
            while (thinkStart >= 0)
            {
                var thinkEnd = response.IndexOf("</think>", thinkStart, StringComparison.OrdinalIgnoreCase);
                if (thinkEnd > thinkStart)
                {
                    response = response.Remove(thinkStart, thinkEnd - thinkStart + 8);
                }
                else
                {
                    // No closing tag, remove to end of string
                    response = response.Substring(0, thinkStart);
                    break;
                }
                thinkStart = response.IndexOf("<think>", StringComparison.OrdinalIgnoreCase);
            }

            return response.Trim();
        }
    }
}

