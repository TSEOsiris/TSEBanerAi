using System;
using System.Collections.Generic;

namespace TSEBanerAi.LLM
{
    /// <summary>
    /// Manages LLM request status
    /// </summary>
    public class LLMStatusManager
    {
        private LLMRequestStatus _currentStatus = new LLMRequestStatus();
        private float _spinnerAngle = 0f;
        private int _dotsCount = 0;
        private int _animationTick = 0;
        private bool _isDebugMode = false;
        private bool _isDebugPanelExpanded = true;
        
        /// <summary>
        /// Event fired when status changes
        /// </summary>
        public event Action<LLMRequestState> StateChanged;
        
        /// <summary>
        /// Current request status
        /// </summary>
        public LLMRequestStatus Status => _currentStatus;
        
        /// <summary>
        /// Is there an active request
        /// </summary>
        public bool IsActive => _currentStatus.IsActive;
        
        /// <summary>
        /// Is debug mode enabled
        /// </summary>
        public bool IsDebugMode
        {
            get { return _isDebugMode; }
            set { _isDebugMode = value; }
        }
        
        /// <summary>
        /// Is debug panel expanded
        /// </summary>
        public bool IsDebugPanelExpanded
        {
            get { return _isDebugPanelExpanded; }
            set { _isDebugPanelExpanded = value; }
        }
        
        /// <summary>
        /// Current spinner angle for animation
        /// </summary>
        public float SpinnerAngle => _spinnerAngle;
        
        /// <summary>
        /// Get the current status text for normal mode
        /// </summary>
        public string NormalModeText => _currentStatus.GetNormalModeText();
        
        /// <summary>
        /// Get the current status text for debug mode
        /// </summary>
        public string DebugModeText => _currentStatus.GetDebugModeText();
        
        /// <summary>
        /// Get debug log entries
        /// </summary>
        public IReadOnlyList<DebugLogEntry> DebugLog => _currentStatus.DebugLog;
        
        /// <summary>
        /// Start a new LLM request
        /// </summary>
        public void StartRequest(string npcName, string userMessage)
        {
            _currentStatus = new LLMRequestStatus
            {
                State = LLMRequestState.Thinking,
                NpcName = npcName,
                UserMessage = userMessage,
                StartTime = DateTime.Now
            };
            
            AddDebugLog("Request sent", LLMRequestState.Thinking);
            StateChanged?.Invoke(LLMRequestState.Thinking);
        }
        
        /// <summary>
        /// Update state to fetching context
        /// </summary>
        public void SetFetchingContext(List<string> sources)
        {
            _currentStatus.State = LLMRequestState.FetchingContext;
            _currentStatus.ContextSources = sources;
            
            AddDebugLog($"Loading context: {string.Join(", ", sources)}", LLMRequestState.FetchingContext);
            StateChanged?.Invoke(LLMRequestState.FetchingContext);
        }
        
        /// <summary>
        /// Update state to generating
        /// </summary>
        public void SetGenerating(int totalTokens = 500)
        {
            _currentStatus.State = LLMRequestState.Generating;
            _currentStatus.TokensTotal = totalTokens;
            _currentStatus.TokensGenerated = 0;
            
            AddDebugLog("Generating response...", LLMRequestState.Generating);
            StateChanged?.Invoke(LLMRequestState.Generating);
        }
        
        /// <summary>
        /// Update tokens generated count
        /// </summary>
        public void UpdateTokens(int generated)
        {
            _currentStatus.TokensGenerated = generated;
        }
        
        /// <summary>
        /// Complete the request with response
        /// </summary>
        public void Complete(string response)
        {
            _currentStatus.State = LLMRequestState.Complete;
            _currentStatus.Response = response;
            _currentStatus.EndTime = DateTime.Now;
            
            AddDebugLog($"Response received in {_currentStatus.Elapsed.TotalSeconds:F1}s", LLMRequestState.Complete);
            StateChanged?.Invoke(LLMRequestState.Complete);
        }
        
        /// <summary>
        /// Reset to idle state
        /// </summary>
        public void Reset()
        {
            _currentStatus = new LLMRequestStatus();
            StateChanged?.Invoke(LLMRequestState.Idle);
        }
        
        /// <summary>
        /// Add debug log entry
        /// </summary>
        public void AddDebugLog(string message, LLMRequestState state)
        {
            _currentStatus.DebugLog.Add(new DebugLogEntry(message, state));
        }
        
        /// <summary>
        /// Update animation (call from game loop)
        /// </summary>
        public void UpdateAnimation(float deltaTime)
        {
            _animationTick++;
            
            // Rotate spinner (10 degrees per frame at ~60fps)
            _spinnerAngle += 600f * deltaTime;
            if (_spinnerAngle >= 360f)
                _spinnerAngle -= 360f;
            
            // Animate dots (every ~0.5 seconds)
            if (_animationTick % 30 == 0)
            {
                _dotsCount = (_dotsCount + 1) % 4;
            }
        }
        
        /// <summary>
        /// Get animated dots string
        /// </summary>
        public string GetAnimatedDots()
        {
            return new string('.', _dotsCount + 1);
        }
        
        /// <summary>
        /// Toggle debug panel expanded state
        /// </summary>
        public void ToggleDebugPanel()
        {
            _isDebugPanelExpanded = !_isDebugPanelExpanded;
        }
    }
}



