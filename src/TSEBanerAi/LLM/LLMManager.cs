using System;
using System.Threading;
using System.Threading.Tasks;
using TSEBanerAi.Utils;

namespace TSEBanerAi.LLM
{
    /// <summary>
    /// Main LLM manager - singleton that provides access to LLM functionality
    /// </summary>
    public class LLMManager
    {
        private static LLMManager _instance;
        private static readonly object _lock = new object();

        private LLMRouter _router;
        private LLMSettings _settings;
        private bool _initialized;

        /// <summary>
        /// Singleton instance
        /// </summary>
        public static LLMManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new LLMManager();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// LLM Router for direct access
        /// </summary>
        public LLMRouter Router => _router;

        /// <summary>
        /// Current settings
        /// </summary>
        public LLMSettings Settings => _settings;

        /// <summary>
        /// Whether LLM is initialized
        /// </summary>
        public bool IsInitialized => _initialized;

        /// <summary>
        /// Current provider name
        /// </summary>
        public string CurrentProviderName => _router?.CurrentProvider?.Name ?? "None";

        private LLMManager()
        {
            _router = new LLMRouter();
            _settings = new LLMSettings();
        }

        /// <summary>
        /// Initialize LLM with default providers
        /// </summary>
        public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                ModLogger.LogDebug("=== LLMManager.Initialize START ===");

                // Add LM Studio provider (highest priority - local, fast)
                if (_settings.UseLMStudio)
                {
                    var lmStudio = new LMStudioProvider(
                        _settings.LMStudioBaseUrl,
                        _settings.LMStudioModel
                    );
                    _router.AddProvider(lmStudio);
                }

                // Add Ollama provider (default local)
                if (_settings.UseOllama)
                {
                    var ollama = new OllamaProvider(
                        _settings.OllamaBaseUrl,
                        _settings.OllamaModel
                    );
                    _router.AddProvider(ollama);
                }

                // Add API fallback if configured
                if (!string.IsNullOrEmpty(_settings.ApiKey))
                {
                    ILLMProvider apiProvider = null;
                    
                    switch (_settings.ApiProvider.ToLower())
                    {
                        case "groq":
                            apiProvider = ApiProvider.CreateGroq(_settings.ApiKey, _settings.ApiModel);
                            break;
                        case "together":
                            apiProvider = ApiProvider.CreateTogether(_settings.ApiKey, _settings.ApiModel);
                            break;
                        case "openrouter":
                            apiProvider = ApiProvider.CreateOpenRouter(_settings.ApiKey, _settings.ApiModel);
                            break;
                        default:
                            apiProvider = new ApiProvider(
                                _settings.ApiBaseUrl,
                                _settings.ApiKey,
                                _settings.ApiModel,
                                _settings.ApiProvider
                            );
                            break;
                    }

                    if (apiProvider != null)
                    {
                        _router.AddProvider(apiProvider);
                    }
                }

                // Check availability
                await _router.RefreshAvailabilityAsync(cancellationToken);

                _initialized = true;
                ModLogger.LogDebug($"LLM initialized. Active provider: {CurrentProviderName}");
                ModLogger.LogDebug("=== LLMManager.Initialize END ===");

                return true;
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to initialize LLM", ex);
                return false;
            }
        }

        /// <summary>
        /// Generate response for player message
        /// </summary>
        public async Task<LLMResponse> GenerateAsync(
            LLMRequest request, 
            CancellationToken cancellationToken = default)
        {
            if (!_initialized)
            {
                ModLogger.LogError("LLM not initialized");
                return LLMResponse.Fail("LLM not initialized");
            }

            // Apply settings to request
            if (request.MaxTokens <= 0)
                request.MaxTokens = _settings.MaxTokens;
            if (request.Temperature <= 0)
                request.Temperature = _settings.Temperature;
            if (request.TimeoutSeconds <= 0)
                request.TimeoutSeconds = _settings.TimeoutSeconds;

            return await _router.GenerateWithRetryAsync(
                request, 
                _settings.MaxRetries,
                cancellationToken
            );
        }

        /// <summary>
        /// Create a new prompt builder
        /// </summary>
        public PromptBuilder CreatePromptBuilder()
        {
            return new PromptBuilder();
        }

        /// <summary>
        /// Update settings
        /// </summary>
        public void UpdateSettings(LLMSettings settings)
        {
            _settings = settings ?? new LLMSettings();
        }
    }

    /// <summary>
    /// LLM configuration settings
    /// </summary>
    public class LLMSettings
    {
        // LM Studio settings (highest priority)
        public bool UseLMStudio { get; set; } = true;
        public string LMStudioBaseUrl { get; set; } = "http://localhost:1234";
        public string LMStudioModel { get; set; } = ""; // Empty = use loaded model

        // Ollama settings
        public bool UseOllama { get; set; } = true;
        public string OllamaBaseUrl { get; set; } = "http://localhost:11434";
        public string OllamaModel { get; set; } = "qwen3:8b";

        // API fallback settings
        public string ApiProvider { get; set; } = "groq";
        public string ApiBaseUrl { get; set; } = "";
        public string ApiKey { get; set; } = "";
        public string ApiModel { get; set; } = "llama-3.3-70b-versatile";

        // Generation settings
        public int MaxTokens { get; set; } = 1024;
        public float Temperature { get; set; } = 0.7f;
        public float TopP { get; set; } = 0.9f;
        public int TimeoutSeconds { get; set; } = 120; // Increased for local models
        public int MaxRetries { get; set; } = 2;

        // Context settings
        public int MaxHistoryMessages { get; set; } = 10;
        public int MaxMemories { get; set; } = 5;
    }
}

