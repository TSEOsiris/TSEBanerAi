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
    /// LLM provider for local Ollama server
    /// </summary>
    public class OllamaProvider : ILLMProvider
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _model;
        private bool _isAvailable;
        private DateTime _lastCheck;

        public string Name => "Ollama";
        public bool IsAvailable => _isAvailable;
        public int Priority => 1; // Try Ollama first

        /// <summary>
        /// Create Ollama provider with default settings
        /// </summary>
        public OllamaProvider() : this("http://localhost:11434", "qwen3:8b")
        {
        }

        /// <summary>
        /// Create Ollama provider with custom settings
        /// </summary>
        public OllamaProvider(string baseUrl, string model)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _model = model;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(120)
            };
            _isAvailable = false;
            _lastCheck = DateTime.MinValue;
        }

        public string GetModelName() => _model;

        public async Task<bool> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Cache availability check for 30 seconds
                if ((DateTime.UtcNow - _lastCheck).TotalSeconds < 30)
                {
                    return _isAvailable;
                }

                var response = await _httpClient.GetAsync($"{_baseUrl}/api/tags", cancellationToken);
                _isAvailable = response.IsSuccessStatusCode;
                _lastCheck = DateTime.UtcNow;

                if (_isAvailable)
                {
                    ModLogger.LogDebug($"Ollama is available at {_baseUrl}");
                }

                return _isAvailable;
            }
            catch (Exception ex)
            {
                ModLogger.LogDebug($"Ollama not available: {ex.Message}");
                _isAvailable = false;
                _lastCheck = DateTime.UtcNow;
                return false;
            }
        }

        public async Task<LLMResponse> GenerateAsync(LLMRequest request, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Build messages array for Ollama chat API
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
                    ["stream"] = false,
                    ["options"] = new JObject
                    {
                        ["temperature"] = request.Temperature,
                        ["top_p"] = request.TopP,
                        ["num_predict"] = request.MaxTokens
                    }
                };

                var content = new StringContent(
                    requestBody.ToString(),
                    Encoding.UTF8,
                    "application/json"
                );

                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    cts.CancelAfter(TimeSpan.FromSeconds(request.TimeoutSeconds));

                    var httpResponse = await _httpClient.PostAsync(
                        $"{_baseUrl}/api/chat",
                        content,
                        cts.Token
                    );

                    if (!httpResponse.IsSuccessStatusCode)
                    {
                        var error = await httpResponse.Content.ReadAsStringAsync();
                        return LLMResponse.Fail($"Ollama error: {httpResponse.StatusCode} - {error}", Name);
                    }

                    var responseText = await httpResponse.Content.ReadAsStringAsync();
                    var responseJson = JObject.Parse(responseText);

                    stopwatch.Stop();

                    var response = LLMResponse.Ok(
                        responseJson["message"]?["content"]?.ToString() ?? "",
                        Name,
                        _model
                    );

                    response.ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds;

                    // Try to get token counts
                    if (responseJson["prompt_eval_count"] != null)
                        response.PromptTokens = responseJson["prompt_eval_count"].Value<int>();
                    if (responseJson["eval_count"] != null)
                        response.CompletionTokens = responseJson["eval_count"].Value<int>();

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
                ModLogger.LogException("Ollama request failed", ex);
                return LLMResponse.Fail($"Ollama error: {ex.Message}", Name);
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

