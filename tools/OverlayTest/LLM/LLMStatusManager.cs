using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace OverlayTest.LLM
{
    /// <summary>
    /// Manages LLM request status and provides UI rendering
    /// </summary>
    public class LLMStatusManager
    {
        private LLMRequestStatus _currentStatus = new();
        private float _spinnerAngle = 0f;
        private int _dotsCount = 0;
        private int _animationTick = 0;
        private bool _isDebugMode = false;
        private bool _isDebugPanelExpanded = true;
        
        /// <summary>
        /// Event fired when status changes
        /// </summary>
        public event Action<LLMRequestState>? StateChanged;
        
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
            get => _isDebugMode;
            set => _isDebugMode = value;
        }
        
        /// <summary>
        /// Is debug panel expanded
        /// </summary>
        public bool IsDebugPanelExpanded
        {
            get => _isDebugPanelExpanded;
            set => _isDebugPanelExpanded = value;
        }
        
        /// <summary>
        /// Current spinner angle for animation
        /// </summary>
        public float SpinnerAngle => _spinnerAngle;
        
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
            
            AddDebugLog("Запрос отправлен", LLMRequestState.Thinking);
            StateChanged?.Invoke(LLMRequestState.Thinking);
        }
        
        /// <summary>
        /// Update state to fetching context
        /// </summary>
        public void SetFetchingContext(List<string> sources)
        {
            _currentStatus.State = LLMRequestState.FetchingContext;
            _currentStatus.ContextSources = sources;
            
            AddDebugLog($"Загрузка контекста: {string.Join(", ", sources)}", LLMRequestState.FetchingContext);
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
            
            AddDebugLog("Генерация ответа...", LLMRequestState.Generating);
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
            
            AddDebugLog($"Ответ получен за {_currentStatus.Elapsed.TotalSeconds:F1}с", LLMRequestState.Complete);
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
        /// Update animation (call from timer)
        /// </summary>
        public void UpdateAnimation()
        {
            _animationTick++;
            
            // Rotate spinner
            _spinnerAngle += 10f;
            if (_spinnerAngle >= 360f)
                _spinnerAngle = 0f;
            
            // Animate dots (every 15 ticks)
            if (_animationTick % 15 == 0)
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
        /// Draw spinner at specified location
        /// </summary>
        public void DrawSpinner(Graphics g, float x, float y, float size, Color color)
        {
            float radius = size / 2 - 2;
            float cx = x + size / 2;
            float cy = y + size / 2;
            
            // Draw arc segments with varying opacity
            for (int i = 0; i < 8; i++)
            {
                float angle = _spinnerAngle + i * 45;
                float alpha = 255 - i * 30;
                if (alpha < 50) alpha = 50;
                
                using var pen = new Pen(Color.FromArgb((int)alpha, color), 2.5f);
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                
                float startAngle = angle;
                float sweepAngle = 30;
                
                g.DrawArc(pen, cx - radius, cy - radius, radius * 2, radius * 2, startAngle, sweepAngle);
            }
        }
        
        /// <summary>
        /// Draw status bubble in chat
        /// </summary>
        public void DrawStatusBubble(Graphics g, Rectangle bounds, Font font, Color bubbleColor, Color textColor)
        {
            if (!IsActive)
                return;
            
            string text = _currentStatus.GetNormalModeText();
            if (string.IsNullOrEmpty(text))
                return;
            
            int padding = 12;
            int spinnerSize = 20;
            
            // Measure text
            var textSize = g.MeasureString(text, font);
            int bubbleWidth = (int)textSize.Width + spinnerSize + padding * 3;
            int bubbleHeight = Math.Max((int)textSize.Height + padding * 2, spinnerSize + padding * 2);
            
            // Position at left (NPC side)
            var bubbleRect = new Rectangle(bounds.X + padding, bounds.Y, bubbleWidth, bubbleHeight);
            
            // Draw bubble background
            using (var path = CreateRoundedRect(bubbleRect, 12))
            using (var brush = new SolidBrush(bubbleColor))
            {
                g.FillPath(brush, path);
            }
            
            // Draw spinner
            float spinnerX = bubbleRect.X + padding;
            float spinnerY = bubbleRect.Y + (bubbleHeight - spinnerSize) / 2f;
            DrawSpinner(g, spinnerX, spinnerY, spinnerSize, textColor);
            
            // Draw text
            using (var textBrush = new SolidBrush(textColor))
            {
                float textX = spinnerX + spinnerSize + 8;
                float textY = bubbleRect.Y + (bubbleHeight - textSize.Height) / 2f;
                g.DrawString(text, font, textBrush, textX, textY);
            }
        }
        
        /// <summary>
        /// Draw debug panel
        /// </summary>
        public void DrawDebugPanel(Graphics g, Rectangle bounds, Font font, Color bgColor, Color textColor, Color headerColor)
        {
            if (!_isDebugMode || _currentStatus.DebugLog.Count == 0)
                return;
            
            int padding = 8;
            int headerHeight = 25;
            int lineHeight = 18;
            
            // Calculate panel height
            int contentHeight = _isDebugPanelExpanded 
                ? Math.Min(_currentStatus.DebugLog.Count * lineHeight + padding * 2, 120)
                : 0;
            int totalHeight = headerHeight + contentHeight;
            
            var panelRect = new Rectangle(bounds.X, bounds.Y, bounds.Width, totalHeight);
            
            // Draw panel background
            using (var bgBrush = new SolidBrush(Color.FromArgb(200, bgColor)))
            {
                g.FillRectangle(bgBrush, panelRect);
            }
            
            // Draw header
            var headerRect = new Rectangle(panelRect.X, panelRect.Y, panelRect.Width, headerHeight);
            using (var headerBrush = new SolidBrush(headerColor))
            {
                g.FillRectangle(headerBrush, headerRect);
            }
            
            // Draw expand/collapse arrow
            string arrow = _isDebugPanelExpanded ? "▼" : "►";
            using (var arrowFont = new Font("Segoe UI", 8))
            using (var textBrush = new SolidBrush(textColor))
            {
                g.DrawString($"{arrow} Debug", arrowFont, textBrush, headerRect.X + padding, headerRect.Y + 5);
                
                // Draw current status on the right
                string status = _currentStatus.GetDebugModeText();
                var statusSize = g.MeasureString(status, arrowFont);
                g.DrawString(status, arrowFont, textBrush, headerRect.Right - statusSize.Width - padding, headerRect.Y + 5);
            }
            
            // Draw log entries if expanded
            if (_isDebugPanelExpanded && contentHeight > 0)
            {
                var contentRect = new Rectangle(panelRect.X, panelRect.Y + headerHeight, panelRect.Width, contentHeight);
                g.SetClip(contentRect);
                
                using (var logFont = new Font("Consolas", 8))
                using (var logBrush = new SolidBrush(Color.FromArgb(200, textColor)))
                {
                    int y = contentRect.Y + padding;
                    
                    // Draw from newest to oldest, but only last few entries
                    int startIdx = Math.Max(0, _currentStatus.DebugLog.Count - 5);
                    for (int i = startIdx; i < _currentStatus.DebugLog.Count; i++)
                    {
                        var entry = _currentStatus.DebugLog[i];
                        g.DrawString(entry.ToString(), logFont, logBrush, contentRect.X + padding, y);
                        y += lineHeight;
                    }
                }
                
                g.ResetClip();
            }
        }
        
        /// <summary>
        /// Get height of debug panel
        /// </summary>
        public int GetDebugPanelHeight()
        {
            if (!_isDebugMode || _currentStatus.DebugLog.Count == 0)
                return 0;
            
            int headerHeight = 25;
            int lineHeight = 18;
            int padding = 8;
            
            int contentHeight = _isDebugPanelExpanded 
                ? Math.Min(_currentStatus.DebugLog.Count * lineHeight + padding * 2, 120)
                : 0;
            
            return headerHeight + contentHeight;
        }
        
        /// <summary>
        /// Check if point is in debug header (for click handling)
        /// </summary>
        public bool IsPointInDebugHeader(Point point, Rectangle debugPanelBounds)
        {
            if (!_isDebugMode)
                return false;
            
            var headerRect = new Rectangle(debugPanelBounds.X, debugPanelBounds.Y, debugPanelBounds.Width, 25);
            return headerRect.Contains(point);
        }
        
        /// <summary>
        /// Toggle debug panel expanded state
        /// </summary>
        public void ToggleDebugPanel()
        {
            _isDebugPanelExpanded = !_isDebugPanelExpanded;
        }
        
        private GraphicsPath CreateRoundedRect(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}



