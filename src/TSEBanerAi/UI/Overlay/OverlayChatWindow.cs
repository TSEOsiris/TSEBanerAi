using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TSEBanerAi.Dialogue;
using TSEBanerAi.LLM;
using TSEBanerAi.Settings;
using TSEBanerAi.UI.Autocomplete;
using TSEBanerAi.Utils;

namespace TSEBanerAi.UI.Overlay
{
    /// <summary>
    /// Main chat window component for overlay - styled like the test app
    /// </summary>
    public class OverlayChatWindow : IDisposable
    {
        private OverlayWindow _window;
        private OverlayRenderer _renderer;
        private OverlayInputHandler _inputHandler;
        private bool _isInitialized;
        private bool _isVisible;
        private string _currentNPCName = string.Empty;
        private List<ChatMessage> _messages = new List<ChatMessage>();
        private string _inputText = string.Empty;
        private int _scrollOffset = 0;
        private int _maxScroll = 0;
        private int _cursorBlinkCounter = 0;
        private int _cursorPosition = 0;
        
        // Mouse state
        private bool _isDragging = false;
        private int _dragStartScreenX, _dragStartScreenY;
        private int _windowStartX, _windowStartY;
        private bool _isResizing = false;
        private int _resizeStartWidth, _resizeStartHeight;
        private int _resizeStartScreenX, _resizeStartScreenY;
        private bool _inputFocused = true;
        private bool _wasMouseDown = false;

        // Autocomplete system
        private EntityIndex _entityIndex;
        private AutocompleteEngine _autocompleteEngine;
        private EntityContextBuilder _contextBuilder;
        private List<GameEntity> _suggestions = new List<GameEntity>();
        private int _selectedSuggestionIndex = -1;
        private bool _showingSuggestions = false;
        private List<SelectedEntity> _selectedEntities = new List<SelectedEntity>();
        
        // LLM Status
        private LLMStatusManager _llmStatusManager;
        
        // Settings
        private ChatSettings _settings;

        // Layout constants (matching test app)
        private const int Padding = 12;
        private const int HeaderHeight = 50;
        private const int MinInputHeight = 50;
        private const int SendButtonSize = 42;
        private const int CornerRadius = 12;

        // Theme colors (matching test app dark theme)
        private static readonly Color BgColor = Color.FromArgb(255, 25, 25, 35);
        private static readonly Color HeaderBgColor = Color.FromArgb(255, 35, 35, 50);
        private static readonly Color InputBgColor = Color.FromArgb(255, 40, 40, 55);
        private static readonly Color PlayerBubbleColor = Color.FromArgb(255, 58, 142, 65);
        private static readonly Color NpcBubbleColor = Color.FromArgb(255, 50, 55, 70);
        private static readonly Color TextColor = Color.White;
        private static readonly Color TextSecondaryColor = Color.FromArgb(255, 180, 180, 180);
        private static readonly Color SendButtonColor = Color.FromArgb(255, 58, 142, 65);
        private static readonly Color DebugActiveColor = Color.FromArgb(100, 100, 200, 100);
        private static readonly Color SuggestionBgColor = Color.FromArgb(255, 30, 30, 45);
        private static readonly Color SuggestionHighlightColor = Color.FromArgb(255, 50, 50, 70);

        // Performance
        private float _lastRenderTime;
        private const float RenderInterval = 1.0f / 30.0f;

        public event Action OnClose;
        public bool IsVisible => _isVisible;
        public int WindowWidth => _window.ChatWidth > 0 ? _window.ChatWidth : 400;
        public int WindowHeight => _window.ChatHeight > 0 ? _window.ChatHeight : 500;

        public OverlayChatWindow()
        {
            _window = new OverlayWindow();
            _renderer = new OverlayRenderer(_window);
            _inputHandler = new OverlayInputHandler(_window);
            
            _entityIndex = new EntityIndex();
            _autocompleteEngine = new AutocompleteEngine(_entityIndex);
            _contextBuilder = new EntityContextBuilder();
            _llmStatusManager = new LLMStatusManager();
            _settings = ChatSettings.Load();
        }

        public bool Initialize()
        {
            if (_isInitialized)
                return true;

            try
            {
                ModLogger.LogDebug("Initializing OverlayChatWindow...");

                _window.UpdateGameWindowBounds();

                if (!_window.Create())
                {
                    ModLogger.LogError("Failed to create overlay window");
                    return false;
                }

                if (_settings.WindowWidth > 0 && _settings.WindowHeight > 0)
                {
                    _window.SetChatSizeAndPosition(
                        _settings.WindowX >= 0 ? _settings.WindowX : _window.ChatX,
                        _settings.WindowY >= 0 ? _settings.WindowY : _window.ChatY,
                        _settings.WindowWidth,
                        _settings.WindowHeight);
                }

                if (!_renderer.Initialize(WindowWidth, WindowHeight))
                {
                    ModLogger.LogError("Failed to initialize renderer");
                    return false;
                }

                _entityIndex.LoadFromModFolder();
                ModLogger.LogDebug($"Entity index loaded: {_entityIndex.EntityCount} entities");

                _llmStatusManager.IsDebugMode = _settings.IsDebugMode;

                // No default greeting - let NPC respond naturally to player
                _isInitialized = true;
                ModLogger.LogDebug("OverlayChatWindow initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to initialize OverlayChatWindow", ex);
                return false;
            }
        }

        public void Show(string npcDisplayName)
        {
            if (!_isInitialized)
            {
                ModLogger.LogWarning("Cannot show chat: not initialized");
                return;
            }

            try
            {
                _currentNPCName = npcDisplayName;
                _isVisible = true;
                _inputFocused = true;
                _window.Show();
                _window.SetAlpha(255);
                _window.Focus(); // Focus window for input
                _inputHandler.IsEnabled = true;
                ModLogger.LogDebug($"OverlayChatWindow shown for NPC: {npcDisplayName}");
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to show overlay chat window", ex);
            }
        }

        public void Hide()
        {
            if (!_isVisible) return;

            try
            {
                _isVisible = false;
                _isDragging = false;
                _isResizing = false;
                _window.Hide();
                _window.ReturnFocusToGame(); // Return focus to game
                _inputHandler.IsEnabled = false;
                SaveSettings();
                ModLogger.LogDebug("OverlayChatWindow hidden");
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to hide overlay chat window", ex);
            }
        }

        public void Update(float deltaTime)
        {
            if (!_isInitialized || !_isVisible) return;

            try
            {
                _cursorBlinkCounter++;
                if (_cursorBlinkCounter > 120) _cursorBlinkCounter = 0; // Slow blink (~2 second cycle at 60fps)

                _inputHandler.Update();

                if (_llmStatusManager.IsActive)
                {
                    _llmStatusManager.UpdateAnimation(deltaTime);
                }

                // Set normal cursor when over our window
                if (_window.IsMouseOver())
                {
                    _window.SetArrowCursor();
                }

                // Handle mouse input
                HandleMouseInput();

                // Handle keyboard input
                HandleKeyInput();
                HandleTextInput();
                HandleAutocomplete();
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to update overlay chat window", ex);
            }
        }

        public void Render()
        {
            if (!_isInitialized || !_isVisible) return;

            try
            {
                float currentTime = (float)DateTime.Now.TimeOfDay.TotalSeconds;
                if (currentTime - _lastRenderTime < RenderInterval && _lastRenderTime > 0)
                    return;

                _renderer.BeginFrame();

                int inputHeight = CalculateInputHeight();
                int messagesBottom = WindowHeight - inputHeight - Padding * 2;
                int messagesTop = HeaderHeight + Padding;
                int messagesHeight = messagesBottom - messagesTop;

                // Background
                _renderer.FillRectangle(0, 0, WindowWidth, WindowHeight, BgColor);

                // Header
                DrawHeader();

                // Messages
                DrawMessages(messagesTop, messagesHeight);

                // LLM Status
                if (_llmStatusManager.IsActive)
                {
                    DrawLLMStatus(messagesBottom - 55);
                }

                // Debug panel
                if (_llmStatusManager.IsDebugMode && _llmStatusManager.DebugLog.Count > 0)
                {
                    DrawDebugPanel(messagesTop);
                }

                // Input area
                DrawInputArea(inputHeight);

                // Send button
                DrawSendButton(inputHeight);

                // Suggestions dropdown
                if (_showingSuggestions && _suggestions.Count > 0)
                {
                    DrawSuggestions(inputHeight);
                }

                _renderer.EndFrame();
                _lastRenderTime = currentTime;
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to render overlay chat window", ex);
            }
        }

        private void DrawHeader()
        {
            _renderer.FillRectangle(0, 0, WindowWidth, HeaderHeight, HeaderBgColor);
            _renderer.DrawText(_currentNPCName, Padding + 10, (HeaderHeight - 16) / 2f, TextColor, 13, true);

            // Debug button with background
            int debugBtnX = WindowWidth - 75;
            int debugBtnY = 12;
            int debugBtnW = 60;
            int debugBtnH = HeaderHeight - 24;
            
            var debugBgColor = _llmStatusManager.IsDebugMode 
                ? Color.FromArgb(100, 100, 200, 100) 
                : Color.FromArgb(50, 255, 255, 255);
            _renderer.FillRectangle(debugBtnX, debugBtnY, debugBtnW, debugBtnH, debugBgColor, 5);
            
            string debugText = _llmStatusManager.IsDebugMode ? "Debug ●" : "Debug";
            var debugColor = _llmStatusManager.IsDebugMode ? Color.LightGreen : TextSecondaryColor;
            _renderer.DrawText(debugText, debugBtnX + 5, debugBtnY + 4, debugColor, 9);
        }

        private void DrawMessages(int messagesTop, int messagesHeight)
        {
            _renderer.SetClip(new RectangleF(0, messagesTop, WindowWidth, messagesHeight));

            int y = messagesTop + messagesHeight + _scrollOffset;
            int totalContentHeight = 0;

            for (int i = _messages.Count - 1; i >= 0; i--)
            {
                var msg = _messages[i];
                int bubbleHeight = DrawMessageBubble(msg, y);
                y -= bubbleHeight + 12;
                totalContentHeight += bubbleHeight + 12;
                if (y < messagesTop - 300) break;
            }

            _maxScroll = Math.Max(0, totalContentHeight - messagesHeight + 50);
            _renderer.ResetClip();
        }

        private int DrawMessageBubble(ChatMessage msg, int bottomY)
        {
            int maxBubbleWidth = WindowWidth - Padding * 4 - 40;
            int bubblePadding = 12;

            var textSize = _renderer.MeasureText(msg.Text, 11, maxBubbleWidth);
            string timeText = msg.GameDateString;
            var timeSize = _renderer.MeasureText(timeText, 8);

            int bubbleWidth = (int)Math.Max(textSize.Width, 80) + bubblePadding * 2;
            bubbleWidth = Math.Min(bubbleWidth, maxBubbleWidth);

            textSize = _renderer.MeasureText(msg.Text, 11, bubbleWidth - bubblePadding * 2);

            int bubbleHeight = (int)textSize.Height + (int)timeSize.Height + bubblePadding * 2 + 8;
            int bubbleX = msg.IsPlayer ? WindowWidth - Padding - bubbleWidth - 8 : Padding + 8;
            int bubbleY = bottomY - bubbleHeight;

            var bubbleColor = msg.IsPlayer ? PlayerBubbleColor : NpcBubbleColor;
            _renderer.FillRectangle(bubbleX, bubbleY, bubbleWidth, bubbleHeight, bubbleColor, CornerRadius);

            var msgRect = new RectangleF(bubbleX + bubblePadding, bubbleY + bubblePadding, 
                                         bubbleWidth - bubblePadding * 2, textSize.Height + 5);
            var textColor = msg.IsPlayer ? Color.White : TextColor;
            _renderer.DrawTextWrapped(msg.Text, msgRect, textColor, 11);

            var timeColor = Color.FromArgb(150, textColor);
            _renderer.DrawText(timeText, bubbleX + bubbleWidth - timeSize.Width - bubblePadding, 
                              bubbleY + bubbleHeight - timeSize.Height - 6, timeColor, 8);

            return bubbleHeight;
        }

        private void DrawLLMStatus(int statusY)
        {
            _renderer.FillRectangle(Padding, statusY, 250, 40, NpcBubbleColor, CornerRadius);
            string statusText = _llmStatusManager.NormalModeText + _llmStatusManager.GetAnimatedDots();
            _renderer.DrawText(statusText, Padding + 15, statusY + 12, TextColor, 11);
        }

        private void DrawDebugPanel(int top)
        {
            int panelHeight = 80;
            _renderer.FillRectangle(Padding, top, WindowWidth - Padding * 2, panelHeight, 
                                   Color.FromArgb(230, 40, 40, 55), 5);

            int logY = top + 10;
            int startIdx = Math.Max(0, _llmStatusManager.DebugLog.Count - 3);
            for (int i = startIdx; i < _llmStatusManager.DebugLog.Count && logY < top + panelHeight - 10; i++)
            {
                var entry = _llmStatusManager.DebugLog[i];
                _renderer.DrawText(entry.ToString(), Padding + 10, logY, Color.FromArgb(170, 255, 170), 9);
                logY += 18;
            }
        }

        private void DrawInputArea(int inputHeight)
        {
            int inputTop = WindowHeight - inputHeight - Padding;
            int inputWidth = WindowWidth - Padding * 2 - SendButtonSize - 10;

            _renderer.FillRectangle(Padding, inputTop, inputWidth, inputHeight, InputBgColor, CornerRadius);

            // Text or placeholder
            string displayText = string.IsNullOrEmpty(_inputText) ? "Type a message..." : _inputText;
            var textColor = string.IsNullOrEmpty(_inputText) ? TextSecondaryColor : TextColor;
            _renderer.DrawText(displayText, Padding + 15, inputTop + 15, textColor, 11);

            // Cursor - thin line, height of one text line only (visible for first half of cycle)
            if (_cursorBlinkCounter < 60 && _inputFocused)
            {
                float cursorX = Padding + 15;
                float cursorY = inputTop + 14;
                float cursorHeight = 18; // Height of one line of text
                
                if (!string.IsNullOrEmpty(_inputText) && _cursorPosition > 0)
                {
                    var textBeforeCursor = _inputText.Substring(0, Math.Min(_cursorPosition, _inputText.Length));
                    var textWidth = _renderer.MeasureText(textBeforeCursor, 11).Width;
                    cursorX += textWidth;
                }
                
                _renderer.DrawLine(cursorX, cursorY, cursorX, cursorY + cursorHeight, TextColor, 2);
            }
        }

        private void DrawSendButton(int inputHeight)
        {
            int inputTop = WindowHeight - inputHeight - Padding;
            int buttonX = WindowWidth - Padding - SendButtonSize;
            int buttonY = inputTop + (inputHeight - SendButtonSize) / 2;

            _renderer.FillRectangle(buttonX, buttonY, SendButtonSize, SendButtonSize, SendButtonColor, SendButtonSize / 2);
            _renderer.DrawArrow(buttonX + SendButtonSize / 2f, buttonY + SendButtonSize / 2f, Color.White);
        }

        private void DrawSuggestions(int inputHeight)
        {
            int inputTop = WindowHeight - inputHeight - Padding;
            int dropdownHeight = Math.Min(_suggestions.Count, 5) * 35 + 10;
            int dropdownY = inputTop - dropdownHeight - 5;
            int dropdownWidth = WindowWidth - Padding * 2 - SendButtonSize - 10;

            _renderer.FillRectangle(Padding, dropdownY, dropdownWidth, dropdownHeight, SuggestionBgColor, 8);

            int y = dropdownY + 5;
            for (int i = 0; i < _suggestions.Count && i < 5; i++)
            {
                var entity = _suggestions[i];
                bool isSelected = i == _selectedSuggestionIndex;

                if (isSelected)
                {
                    _renderer.FillRectangle(Padding + 5, y, dropdownWidth - 10, 30, SuggestionHighlightColor, 5);
                }

                var typeColor = GetEntityTypeColor(entity.Type);
                _renderer.FillRectangle(Padding + 10, y + 7, 16, 16, typeColor, 8);
                _renderer.DrawText(entity.Name, Padding + 35, y + 5, TextColor, 11);
                _renderer.DrawText(entity.Description, Padding + 35, y + 20, TextSecondaryColor, 9);

                y += 35;
            }
        }

        private Color GetEntityTypeColor(EntityType type)
        {
            switch (type)
            {
                case EntityType.Hero: return Color.FromArgb(255, 65, 105, 225);
                case EntityType.Settlement: return Color.FromArgb(255, 60, 179, 113);
                case EntityType.Kingdom: return Color.FromArgb(255, 255, 215, 0);
                case EntityType.Clan: return Color.FromArgb(255, 147, 112, 219);
                default: return Color.Gray;
            }
        }

        private int CalculateInputHeight()
        {
            if (string.IsNullOrEmpty(_inputText)) return MinInputHeight;
            
            // Calculate available width for text (minus padding and send button)
            int availableWidth = WindowWidth - 120;
            
            // Measure text to see if it wraps
            var textSize = _renderer.MeasureText(_inputText, 11, availableWidth);
            
            // Single line height is approximately 18-20 pixels for 11pt font
            // Only expand if text actually wraps (height > single line threshold)
            const int singleLineHeight = 22;
            
            if (textSize.Height <= singleLineHeight)
            {
                // Text fits on one line, no expansion needed
                return MinInputHeight;
            }
            
            // Calculate actual lines (only count additional lines after first)
            int additionalLines = (int)Math.Ceiling((textSize.Height - singleLineHeight) / 18f);
            return Math.Min(150, MinInputHeight + additionalLines * 22);
        }

        private void HandleMouseInput()
        {
            // Use screen coordinates for smooth dragging/resizing
            var screenPos = _window.GetMouseScreenPosition();
            var localPos = _window.GetMousePosition();
            
            bool isMouseDown = _inputHandler.IsMousePressed();
            bool mouseJustDown = isMouseDown && !_wasMouseDown;
            _wasMouseDown = isMouseDown;

            int inputHeight = CalculateInputHeight();
            int inputTop = WindowHeight - inputHeight - Padding;
            int sendButtonX = WindowWidth - Padding - SendButtonSize;

            // Handle dragging (header area) - use screen coordinates
            if (mouseJustDown && localPos.Y < HeaderHeight && localPos.Y > 0 && 
                localPos.X > 0 && localPos.X < WindowWidth - 80)
            {
                _isDragging = true;
                _dragStartScreenX = screenPos.X;
                _dragStartScreenY = screenPos.Y;
                _windowStartX = _window.ChatX;
                _windowStartY = _window.ChatY;
            }

            if (_isDragging)
            {
                if (isMouseDown)
                {
                    // Use screen coordinates for smooth movement
                    int deltaX = screenPos.X - _dragStartScreenX;
                    int deltaY = screenPos.Y - _dragStartScreenY;
                    _window.SetChatSizeAndPosition(
                        _windowStartX + deltaX,
                        _windowStartY + deltaY,
                        _window.ChatWidth,
                        _window.ChatHeight);
                }
                else
                {
                    _isDragging = false;
                    SaveSettings();
                }
            }

            // Handle resize (bottom-right corner) - use screen coordinates
            if (mouseJustDown && localPos.X > WindowWidth - 20 && localPos.Y > WindowHeight - 20)
            {
                _isResizing = true;
                _resizeStartScreenX = screenPos.X;
                _resizeStartScreenY = screenPos.Y;
                _resizeStartWidth = _window.ChatWidth;
                _resizeStartHeight = _window.ChatHeight;
            }

            if (_isResizing)
            {
                if (isMouseDown)
                {
                    int deltaX = screenPos.X - _resizeStartScreenX;
                    int deltaY = screenPos.Y - _resizeStartScreenY;
                    int newWidth = Math.Max(300, _resizeStartWidth + deltaX);
                    int newHeight = Math.Max(400, _resizeStartHeight + deltaY);
                    _window.SetChatSizeAndPosition(
                        _window.ChatX,
                        _window.ChatY,
                        newWidth,
                        newHeight);
                    _renderer.Initialize(newWidth, newHeight);
                }
                else
                {
                    _isResizing = false;
                    SaveSettings();
                }
            }

            // Only handle clicks if not dragging/resizing
            if (!_isDragging && !_isResizing)
            {
                // Handle debug button click (in header, right side)
                if (mouseJustDown &&
                    localPos.X >= WindowWidth - 80 && localPos.X <= WindowWidth - 10 &&
                    localPos.Y >= 10 && localPos.Y <= HeaderHeight - 10)
                {
                    _llmStatusManager.IsDebugMode = !_llmStatusManager.IsDebugMode;
                    _settings.IsDebugMode = _llmStatusManager.IsDebugMode;
                    _settings.Save();
                    ModLogger.LogDebug($"Debug mode toggled: {_llmStatusManager.IsDebugMode}");
                }

                // Handle send button click
                if (mouseJustDown && 
                    localPos.X >= sendButtonX && localPos.X <= sendButtonX + SendButtonSize &&
                    localPos.Y >= inputTop && localPos.Y <= inputTop + inputHeight)
                {
                    SendMessage();
                }

                // Handle input area click
                if (mouseJustDown &&
                    localPos.X >= Padding && localPos.X <= WindowWidth - Padding - SendButtonSize - 10 &&
                    localPos.Y >= inputTop && localPos.Y <= inputTop + inputHeight)
                {
                    _inputFocused = true;
                    _cursorPosition = _inputText.Length;
                }
            }
        }

        private void HandleTextInput()
        {
            // Only handle text input when window has focus
            if (!_inputFocused || !_window.HasFocus()) return;

            // Handle character input (A-Z, a-z, 0-9, space, etc.)
            for (int vk = 0x20; vk <= 0x5A; vk++) // Space to Z
            {
                if (_inputHandler.IsKeyDown(vk))
                {
                    char c = GetCharFromVK(vk);
                    if (c != '\0')
                    {
                        _inputText = _inputText.Insert(_cursorPosition, c.ToString());
                        _cursorPosition++;
                    }
                }
            }

            // Handle Russian characters (Cyrillic range)
            for (int vk = 0xC0; vk <= 0xDF; vk++)
            {
                if (_inputHandler.IsKeyDown(vk))
                {
                    char c = GetCharFromVK(vk);
                    if (c != '\0')
                    {
                        _inputText = _inputText.Insert(_cursorPosition, c.ToString());
                        _cursorPosition++;
                    }
                }
            }

            // Handle backspace
            if (_inputHandler.IsKeyDown(0x08) && _cursorPosition > 0)
            {
                _inputText = _inputText.Remove(_cursorPosition - 1, 1);
                _cursorPosition--;
                
                // Update selected entities
                foreach (var entity in _selectedEntities.ToList())
                {
                    if (_cursorPosition >= entity.StartIndex && _cursorPosition <= entity.EndIndex)
                    {
                        _selectedEntities.Remove(entity);
                    }
                }
            }

            // Handle delete
            if (_inputHandler.IsKeyDown(0x2E) && _cursorPosition < _inputText.Length)
            {
                _inputText = _inputText.Remove(_cursorPosition, 1);
            }

            // Handle left arrow
            if (_inputHandler.IsKeyDown(0x25) && _cursorPosition > 0)
            {
                _cursorPosition--;
            }

            // Handle right arrow
            if (_inputHandler.IsKeyDown(0x27) && _cursorPosition < _inputText.Length)
            {
                _cursorPosition++;
            }

            // Handle Home
            if (_inputHandler.IsKeyDown(0x24))
            {
                _cursorPosition = 0;
            }

            // Handle End
            if (_inputHandler.IsKeyDown(0x23))
            {
                _cursorPosition = _inputText.Length;
            }
        }

        private char GetCharFromVK(int vk)
        {
            // Simple mapping - in real implementation should use ToUnicodeEx
            bool shift = _inputHandler.IsKeyPressed(0x10); // VK_SHIFT
            
            if (vk >= 0x41 && vk <= 0x5A) // A-Z
            {
                char c = (char)vk;
                return shift ? c : char.ToLower(c);
            }
            if (vk >= 0x30 && vk <= 0x39) // 0-9
            {
                if (shift)
                {
                    char[] shiftNums = { ')', '!', '@', '#', '$', '%', '^', '&', '*', '(' };
                    return shiftNums[vk - 0x30];
                }
                return (char)vk;
            }
            if (vk == 0x20) return ' '; // Space
            if (vk == 0xBC) return shift ? '<' : ',';
            if (vk == 0xBE) return shift ? '>' : '.';
            if (vk == 0xBF) return shift ? '?' : '/';
            if (vk == 0xBA) return shift ? ':' : ';';
            if (vk == 0xDE) return shift ? '"' : '\'';
            if (vk == 0xBD) return shift ? '_' : '-';
            if (vk == 0xBB) return shift ? '+' : '=';
            
            return '\0';
        }

        private void HandleKeyInput()
        {
            // Only handle keyboard when window has focus
            if (!_window.HasFocus()) return;

            // Enter to send or accept suggestion
            if (_inputHandler.IsKeyDown(0x0D))
            {
                if (_showingSuggestions && _selectedSuggestionIndex >= 0)
                    AcceptSuggestion();
                else
                    SendMessage();
            }

            // Tab to accept suggestion
            if (_inputHandler.IsKeyDown(0x09) && _showingSuggestions && _selectedSuggestionIndex >= 0)
            {
                AcceptSuggestion();
            }

            // Arrow keys for suggestions
            if (_showingSuggestions && _suggestions.Count > 0)
            {
                if (_inputHandler.IsKeyDown(0x26))
                    _selectedSuggestionIndex = Math.Max(0, _selectedSuggestionIndex - 1);
                if (_inputHandler.IsKeyDown(0x28))
                    _selectedSuggestionIndex = Math.Min(_suggestions.Count - 1, _selectedSuggestionIndex + 1);
            }

            // Escape
            if (_inputHandler.IsKeyDown(0x1B))
            {
                if (_showingSuggestions)
                {
                    _showingSuggestions = false;
                    _suggestions.Clear();
                }
                else
                {
                    Hide();
                    OnClose?.Invoke();
                }
            }

            // F9 toggle debug
            if (_inputHandler.IsKeyDown(0x78))
            {
                _llmStatusManager.IsDebugMode = !_llmStatusManager.IsDebugMode;
                _settings.IsDebugMode = _llmStatusManager.IsDebugMode;
                _settings.Save();
            }
        }

        private void HandleAutocomplete()
        {
            if (!string.IsNullOrEmpty(_inputText) && _cursorPosition > 0)
            {
                var result = _autocompleteEngine.Analyze(_inputText, _cursorPosition);
                if (result.HasSuggestions)
                {
                    _suggestions = result.Suggestions;
                    _showingSuggestions = true;
                    if (_selectedSuggestionIndex < 0 || _selectedSuggestionIndex >= _suggestions.Count)
                        _selectedSuggestionIndex = 0;
                }
                else
                {
                    _showingSuggestions = false;
                    _suggestions.Clear();
                    _selectedSuggestionIndex = -1;
                }
            }
            else
            {
                _showingSuggestions = false;
                _suggestions.Clear();
                _selectedSuggestionIndex = -1;
            }
        }

        private void AcceptSuggestion()
        {
            if (_selectedSuggestionIndex < 0 || _selectedSuggestionIndex >= _suggestions.Count)
                return;

            var entity = _suggestions[_selectedSuggestionIndex];
            var result = _autocompleteEngine.Analyze(_inputText, _cursorPosition);

            if (result.HasSuggestions)
            {
                var applied = _autocompleteEngine.ApplySelection(_inputText, entity, result.WordStartIndex, result.WordEndIndex);
                _inputText = applied.Item1;
                _cursorPosition = applied.Item2;
                _selectedEntities.Add(applied.Item3);
                _contextBuilder.AddEntity(applied.Item3);
            }

            _showingSuggestions = false;
            _suggestions.Clear();
            _selectedSuggestionIndex = -1;
        }

        private void SendMessage()
        {
            if (string.IsNullOrWhiteSpace(_inputText)) return;

            string messageToSend = _contextBuilder.HasEntities
                ? _contextBuilder.BuildFullMessage(_inputText)
                : _inputText;

            AddMessage(_inputText, true);
            _llmStatusManager.StartRequest(_currentNPCName, messageToSend);

            // Store message and clear input immediately
            string messageToProcess = messageToSend;
            _inputText = string.Empty;
            _cursorPosition = 0;
            _selectedEntities.Clear();
            _contextBuilder.Clear();

            // Send to LLM asynchronously
            SendToLLMAsync(messageToProcess);
        }

        private async void SendToLLMAsync(string message)
        {
            try
            {
                ModLogger.LogDebug($"SendToLLMAsync: Sending message to LLM: {message.Substring(0, Math.Min(50, message.Length))}...");
                
                // Update status
                _llmStatusManager.SetGenerating(0);

                // Check if DialogueHandler has active NPC
                if (DialogueHandler.Instance.CurrentNpc == null)
                {
                    ModLogger.LogDebug("SendToLLMAsync: No current NPC, using direct LLM call");
                    
                    // Direct LLM call without NPC context
                    var request = new LLMRequest
                    {
                        SystemPrompt = "You are an NPC in Mount & Blade II: Bannerlord. Respond in character, briefly and naturally.",
                        Messages = new System.Collections.Generic.List<LLMMessage>
                        {
                            LLMMessage.User(message)
                        },
                        MaxTokens = 512,
                        Temperature = 0.7f
                    };

                    var response = await LLMManager.Instance.GenerateAsync(request);
                    
                    ModLogger.LogDebug($"SendToLLMAsync: LLM response received. Success: {response.Success}, Provider: {response.Provider}");
                    
                    if (response.Success)
                    {
                        _llmStatusManager.Complete(response.Content);
                        AddMessage(response.Content, false);
                        ModLogger.LogDebug($"SendToLLMAsync: Response added to chat. Time: {response.ResponseTimeMs}ms, Tokens: {response.TotalTokens}");
                    }
                    else
                    {
                        _llmStatusManager.Complete($"[Error: {response.Error}]");
                        AddMessage($"*{_currentNPCName} seems distracted* ({response.Error})", false);
                        ModLogger.LogError($"SendToLLMAsync: LLM error: {response.Error}");
                    }
                }
                else
                {
                    ModLogger.LogDebug($"SendToLLMAsync: Using DialogueHandler for NPC: {DialogueHandler.Instance.CurrentNpc.Name}");
                    
                    // Use DialogueHandler for full context
                    var response = await DialogueHandler.Instance.SendMessageAsync(message);
                    
                    ModLogger.LogDebug($"SendToLLMAsync: DialogueHandler response. Success: {response.Success}");
                    
                    if (response.Success)
                    {
                        string displayText = response.DisplayContent ?? response.Content;
                        _llmStatusManager.Complete(displayText);
                        AddMessage(displayText, false);
                        
                        // Handle commands if present
                        if (response.Command != null)
                        {
                            ModLogger.LogDebug($"SendToLLMAsync: Command detected: {response.Command.CommandType}");
                            var cmdResult = Commands.CommandExecutor.Instance.ExecuteFromResponse(
                                DialogueHandler.Instance.CurrentNpc, response);
                            if (!cmdResult.Success)
                            {
                                ModLogger.LogDebug($"SendToLLMAsync: Command failed: {cmdResult.Error}");
                            }
                        }
                        
                        // Handle dice request if present
                        if (response.DiceRequest != null)
                        {
                            ModLogger.LogDebug($"SendToLLMAsync: Dice request: {response.DiceRequest.Skill} DC {response.DiceRequest.DC}");
                            // TODO: Trigger dice UI
                        }
                    }
                    else
                    {
                        _llmStatusManager.Complete($"[Error: {response.Error}]");
                        AddMessage($"*{_currentNPCName} seems distracted* ({response.Error})", false);
                        ModLogger.LogError($"SendToLLMAsync: DialogueHandler error: {response.Error}");
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogException("SendToLLMAsync failed", ex);
                _llmStatusManager.Complete("[Error]");
                AddMessage($"*{_currentNPCName} seems confused* (Error: {ex.Message})", false);
            }
        }

        public void OnLLMResponse(string response)
        {
            _llmStatusManager.Complete(response);
            AddMessage(response, false);
        }

        public void AddMessage(string text, bool isPlayer)
        {
            _messages.Add(new ChatMessage
            {
                Text = text,
                IsPlayer = isPlayer,
                GameDateString = GetGameDateString()
            });
        }

        public void ClearMessages()
        {
            _messages.Clear();
        }

        private void SaveSettings()
        {
            _settings.UpdatePosition(_window.ChatX, _window.ChatY);
            _settings.UpdateSize(_window.ChatWidth, _window.ChatHeight);
            _settings.Save();
        }

        public void Dispose()
        {
            try
            {
                Hide();
                _renderer?.Dispose();
                _window?.Dispose();
                _isInitialized = false;
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Error disposing OverlayChatWindow", ex);
            }
        }

        private class ChatMessage
        {
            public string Text { get; set; } = string.Empty;
            public bool IsPlayer { get; set; }
            public string GameDateString { get; set; } = string.Empty;
        }

        /// <summary>
        /// Get current in-game date string in Calradian format
        /// </summary>
        private static string GetGameDateString()
        {
            try
            {
                if (Campaign.Current == null)
                {
                    return DateTime.Now.ToString("dd.MM.yyyy HH:mm");
                }

                var now = CampaignTime.Now;
                string[] seasons = { "Spring", "Summer", "Autumn", "Winter" };
                int seasonIndex = (int)now.GetSeasonOfYear;
                if (seasonIndex < 0 || seasonIndex > 3) seasonIndex = 0;
                
                // GetDayOfSeason returns 0-20, but game displays 1-21
                int dayOfSeason = now.GetDayOfSeason + 1;
                
                return $"{seasons[seasonIndex]} {dayOfSeason}, {now.GetYear}";
            }
            catch
            {
                return DateTime.Now.ToString("dd.MM.yyyy HH:mm");
            }
        }
    }
}
