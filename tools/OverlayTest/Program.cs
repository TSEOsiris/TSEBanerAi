using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using OverlayTest.Autocomplete;
using OverlayTest.LLM;
using OverlayTest.Settings;

namespace OverlayTest
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TestForm());
        }
    }

    public class TestForm : Form
    {
        private ChatOverlayForm? _overlay;
        private Button _toggleButton;
        private TrackBar _widthSlider;
        private TrackBar _heightSlider;
        private Label _widthLabel;
        private Label _heightLabel;
        private ComboBox _themeCombo;

        public TestForm()
        {
            Text = "Fake Bannerlord Window - Тестирование оверлея";
            Size = new Size(1280, 720);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(30, 30, 40);

            Paint += (s, e) =>
            {
                using var font = new Font("Segoe UI", 24, FontStyle.Bold);
                using var brush = new SolidBrush(Color.FromArgb(80, 255, 255, 255));
                e.Graphics.DrawString("Симуляция игрового окна Bannerlord", font, brush, 50, 50);

                using var font2 = new Font("Segoe UI", 14);
                e.Graphics.DrawString("Нажмите F10 или кнопку 'Показать чат'", font2, brush, 50, 100);
                e.Graphics.DrawString("Перетаскивание за header, Escape для закрытия", font2, brush, 50, 130);
            };

            var panel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 80,
                BackColor = Color.FromArgb(40, 40, 55)
            };
            Controls.Add(panel);

            _toggleButton = new Button
            {
                Text = "Показать чат (F10)",
                Location = new Point(20, 20),
                Size = new Size(150, 35),
                BackColor = Color.FromArgb(70, 130, 200),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _toggleButton.Click += (s, e) => ToggleOverlay();
            panel.Controls.Add(_toggleButton);

            _widthLabel = new Label { Text = "Ширина: 30%", Location = new Point(200, 10), Size = new Size(100, 20), ForeColor = Color.White };
            panel.Controls.Add(_widthLabel);
            _widthSlider = new TrackBar { Location = new Point(200, 30), Size = new Size(120, 45), Minimum = 20, Maximum = 50, Value = 30 };
            _widthSlider.ValueChanged += (s, e) => { _widthLabel.Text = $"Ширина: {_widthSlider.Value}%"; UpdateOverlaySize(); };
            panel.Controls.Add(_widthSlider);

            _heightLabel = new Label { Text = "Высота: 70%", Location = new Point(340, 10), Size = new Size(100, 20), ForeColor = Color.White };
            panel.Controls.Add(_heightLabel);
            _heightSlider = new TrackBar { Location = new Point(340, 30), Size = new Size(120, 45), Minimum = 40, Maximum = 90, Value = 70 };
            _heightSlider.ValueChanged += (s, e) => { _heightLabel.Text = $"Высота: {_heightSlider.Value}%"; UpdateOverlaySize(); };
            panel.Controls.Add(_heightSlider);

            var themeLabel = new Label { Text = "Тема:", Location = new Point(480, 10), Size = new Size(60, 20), ForeColor = Color.White };
            panel.Controls.Add(themeLabel);
            _themeCombo = new ComboBox { Location = new Point(480, 30), Size = new Size(130, 25), DropDownStyle = ComboBoxStyle.DropDownList };
            _themeCombo.Items.AddRange(new object[] { "Тёмная", "Светлая", "Синяя", "Зелёная" });
            _themeCombo.SelectedIndex = 0;
            _themeCombo.SelectedIndexChanged += (s, e) => _overlay?.SetTheme(_themeCombo.SelectedIndex);
            panel.Controls.Add(_themeCombo);

            LocationChanged += (s, e) => UpdateOverlayPosition();
            SizeChanged += (s, e) => UpdateOverlaySize();

            KeyPreview = true;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.F10) ToggleOverlay(); };
        }

        private void ToggleOverlay()
        {
            if (_overlay == null || _overlay.IsDisposed)
            {
                _overlay = new ChatOverlayForm(this);
                _overlay.SetTheme(_themeCombo.SelectedIndex);
                UpdateOverlaySize();
                _overlay.Show();
                _toggleButton.Text = "Скрыть чат (F10)";
            }
            else if (_overlay.Visible)
            {
                _overlay.Hide();
                _toggleButton.Text = "Показать чат (F10)";
            }
            else
            {
                _overlay.Show();
                _toggleButton.Text = "Скрыть чат (F10)";
            }
        }

        private void UpdateOverlayPosition()
        {
            if (_overlay != null && !_overlay.IsDisposed)
            {
                int chatWidth = (int)(ClientSize.Width * _widthSlider.Value / 100f);
                int chatHeight = (int)(ClientSize.Height * _heightSlider.Value / 100f);
                int chatX = Right - chatWidth - 30;
                int chatY = Top + (int)(ClientSize.Height * 0.10f);
                _overlay.UpdatePosition(chatX, chatY, chatWidth, chatHeight);
            }
        }

        private void UpdateOverlaySize() => UpdateOverlayPosition();

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _overlay?.Close();
            base.OnFormClosed(e);
        }
    }

    public class ChatMessage
    {
        public string Text { get; set; } = "";
        public bool IsPlayer { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string SenderName { get; set; } = "";
    }

    public class ChatOverlayForm : Form
    {
        private Form _parent;
        private int _themeIndex = 0;
        private List<ChatMessage> _messages = new();
        private string _inputText = "";
        private int _scrollOffset = 0;
        private int _maxScroll = 0;
        private System.Windows.Forms.Timer _updateTimer;
        private Rectangle _sendButtonRect;
        private Rectangle _inputRect;
        private Rectangle _messagesRect;
        private Rectangle _scrollBarRect;
        private Rectangle _scrollThumbRect;
        private bool _inputFocused = false;
        private int _cursorBlinkCounter = 0;
        private bool _isDragging = false;
        private Point _dragStart;
        private bool _isMouseOverMessages = false;
        private bool _isScrollDragging = false;
        private int _scrollDragStartY;
        private int _scrollDragStartOffset;
        private float _scrollBarOpacity = 0f;
        private int _cursorPosition = 0; // Position in _inputText
        
        // Resize
        private bool _isResizing = false;
        private ResizeDirection _resizeDir = ResizeDirection.None;
        private Point _resizeStart;
        private Size _resizeStartSize;
        private Point _resizeStartLocation;
        private const int ResizeBorder = 8;
        
        // Autocomplete system
        private EntityIndex _entityIndex;
        private AutocompleteEngine _autocompleteEngine;
        private SuggestionDropdown _suggestionDropdown;
        private EntityContextBuilder _contextBuilder;
        private AutocompleteResult? _currentAutocomplete;
        private List<SelectedEntity> _selectedEntities = new();
        
        // Database path (can be customized)
        private string _databasePath = @"C:\Users\darka\OneDrive\Документы\Mount and Blade II Bannerlord\Configs\TSEBanerAi\Database";
        
        // Settings persistence
        private ChatSettings _settings;
        private bool _settingsLoaded = false;
        
        // LLM Status
        private LLMStatusManager _llmStatusManager;
        private Rectangle _debugButtonRect;
        private Rectangle _debugPanelRect;
        private string _npcName = "Торговец Ульрих";
        
        private enum ResizeDirection
        {
            None, Left, Right, Top, Bottom,
            TopLeft, TopRight, BottomLeft, BottomRight
        }

        // Theme: [Background, Header, InputBg, PlayerBubble, NpcBubble, Text, TextSecondary]
        private static readonly Color[][] Themes = new[]
        {
            new[] { Color.FromArgb(25, 25, 35), Color.FromArgb(35, 35, 50), Color.FromArgb(40, 40, 55), Color.FromArgb(58, 142, 65), Color.FromArgb(50, 55, 70), Color.White, Color.FromArgb(180, 180, 180) },
            new[] { Color.FromArgb(245, 245, 250), Color.FromArgb(85, 140, 200), Color.FromArgb(250, 250, 255), Color.FromArgb(220, 248, 198), Color.White, Color.FromArgb(40, 40, 50), Color.FromArgb(100, 100, 110) },
            new[] { Color.FromArgb(15, 25, 45), Color.FromArgb(25, 45, 75), Color.FromArgb(20, 35, 60), Color.FromArgb(40, 120, 180), Color.FromArgb(35, 50, 80), Color.White, Color.FromArgb(150, 180, 210) },
            new[] { Color.FromArgb(15, 35, 25), Color.FromArgb(25, 55, 40), Color.FromArgb(20, 45, 35), Color.FromArgb(60, 160, 100), Color.FromArgb(35, 65, 50), Color.White, Color.FromArgb(150, 210, 170) },
        };

        public ChatOverlayForm(Form parent)
        {
            _parent = parent;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            DoubleBuffered = true;
            
            // Load saved settings
            _settings = ChatSettings.Load();
            
            // Apply saved size
            Size = new Size(_settings.WindowWidth, _settings.WindowHeight);
            _themeIndex = _settings.ThemeIndex;
            
            // Initialize LLM status manager
            _llmStatusManager = new LLMStatusManager();
            _llmStatusManager.IsDebugMode = _settings.IsDebugMode;

            // Initialize autocomplete system
            _entityIndex = new EntityIndex();
            _entityIndex.LoadFromFolder(_databasePath);
            _autocompleteEngine = new AutocompleteEngine(_entityIndex);
            _suggestionDropdown = new SuggestionDropdown();
            _contextBuilder = new EntityContextBuilder();

            _messages.Add(new ChatMessage { Text = "Приветствую тебя, путник! Давно не видел здесь новых лиц.", IsPlayer = false, SenderName = "Торговец Ульрих", Timestamp = DateTime.Now.AddMinutes(-5) });
            _messages.Add(new ChatMessage { Text = "Здравствуй! Я ищу работу.", IsPlayer = true, SenderName = "Игрок", Timestamp = DateTime.Now.AddMinutes(-4) });
            _messages.Add(new ChatMessage { Text = "Работу, говоришь? Есть одно дело... Бандиты разоряют караваны на северной дороге. Если разберёшься с ними, заплачу щедро.", IsPlayer = false, SenderName = "Торговец Ульрих", Timestamp = DateTime.Now.AddMinutes(-3) });
            _messages.Add(new ChatMessage { Text = "Сколько заплатишь?", IsPlayer = true, SenderName = "Игрок", Timestamp = DateTime.Now.AddMinutes(-2) });
            _messages.Add(new ChatMessage { Text = "500 денариев и моя благодарность. Поверь, она многого стоит в этих краях.", IsPlayer = false, SenderName = "Торговец Ульрих", Timestamp = DateTime.Now.AddMinutes(-1) });
            _messages.Add(new ChatMessage { Text = "Хорошо, я возьмусь за это дело.", IsPlayer = true, SenderName = "Игрок", Timestamp = DateTime.Now.AddSeconds(-30) });
            _messages.Add(new ChatMessage { Text = "Отлично! Они обычно прячутся в лесу к северу от города. Будь осторожен, их там немало.", IsPlayer = false, SenderName = "Торговец Ульрих", Timestamp = DateTime.Now });

            MouseDown += OnMouseDown;
            MouseMove += OnMouseMove;
            MouseUp += OnMouseUp;
            MouseWheel += OnMouseWheel;
            MouseLeave += (s, e) => { _isMouseOverMessages = false; };
            KeyDown += OnKeyDown;
            KeyPress += OnKeyPress;

            _updateTimer = new System.Windows.Forms.Timer { Interval = 30 };
            _updateTimer.Tick += (s, e) =>
            {
                _cursorBlinkCounter++;
                if (_cursorBlinkCounter > 16) _cursorBlinkCounter = 0;

                // Плавное появление/исчезновение scrollbar
                float targetOpacity = _isMouseOverMessages ? 1f : 0f;
                _scrollBarOpacity += (targetOpacity - _scrollBarOpacity) * 0.2f;
                
                // Update LLM status animation
                _llmStatusManager.UpdateAnimation();

                Invalidate();
            };
            _updateTimer.Start();
            
            // Apply saved position after form is shown
            Load += (s, e) => ApplySavedPosition();
            
            // Save settings when window is moved or resized
            LocationChanged += (s, e) => SavePositionIfLoaded();
            SizeChanged += (s, e) => SaveSizeIfLoaded();
        }
        
        private void ApplySavedPosition()
        {
            // If we have saved position, use it
            if (_settings.WindowX >= 0 && _settings.WindowY >= 0)
            {
                // Make sure the position is on screen
                var screen = Screen.FromPoint(new Point(_settings.WindowX, _settings.WindowY));
                int x = Math.Max(screen.WorkingArea.Left, Math.Min(_settings.WindowX, screen.WorkingArea.Right - Width));
                int y = Math.Max(screen.WorkingArea.Top, Math.Min(_settings.WindowY, screen.WorkingArea.Bottom - Height));
                Location = new Point(x, y);
            }
            _settingsLoaded = true;
        }
        
        private void SavePositionIfLoaded()
        {
            if (_settingsLoaded && !_isResizing && !_isDragging)
            {
                _settings.UpdatePosition(Location.X, Location.Y);
                _settings.Save();
            }
        }
        
        private void SaveSizeIfLoaded()
        {
            if (_settingsLoaded && !_isResizing)
            {
                _settings.UpdateSize(Width, Height);
                _settings.Save();
            }
        }

        private void OnMouseDown(object? sender, MouseEventArgs e)
        {
            // Check autocomplete dropdown first
            if (_suggestionDropdown.IsVisible && _suggestionDropdown.HandleMouseClick(e.Location))
            {
                ApplyAutocomplete();
                return;
            }
            
            var resizeDir = GetResizeDirection(e.Location);
            
            if (resizeDir != ResizeDirection.None)
            {
                _isResizing = true;
                _resizeDir = resizeDir;
                _resizeStart = PointToScreen(e.Location);
                _resizeStartSize = Size;
                _resizeStartLocation = Location;
                _inputFocused = false;
                _suggestionDropdown.Hide();
            }
            else if (_scrollThumbRect.Contains(e.Location) && _isMouseOverMessages)
            {
                _isScrollDragging = true;
                _scrollDragStartY = e.Y;
                _scrollDragStartOffset = _scrollOffset;
            }
            else if (_debugButtonRect.Contains(e.Location))
            {
                // Toggle debug mode
                _llmStatusManager.IsDebugMode = !_llmStatusManager.IsDebugMode;
                _settings.IsDebugMode = _llmStatusManager.IsDebugMode;
                _settings.Save();
                Invalidate();
            }
            else if (_llmStatusManager.IsDebugMode && _llmStatusManager.IsPointInDebugHeader(e.Location, _debugPanelRect))
            {
                // Toggle debug panel expansion
                _llmStatusManager.ToggleDebugPanel();
                Invalidate();
            }
            else if (_sendButtonRect.Contains(e.Location))
            {
                SendMessage();
            }
            else if (_inputRect.Contains(e.Location))
            {
                _inputFocused = true;
                // Set cursor position based on click (simplified)
                _cursorPosition = _inputText.Length;
                Focus();
            }
            else if (e.Y < 50 && e.Y > ResizeBorder)
            {
                _isDragging = true;
                _dragStart = e.Location;
                _inputFocused = false;
                _suggestionDropdown.Hide();
            }
            else
            {
                _inputFocused = false;
                _suggestionDropdown.Hide();
            }
        }
        
        private ResizeDirection GetResizeDirection(Point p)
        {
            bool left = p.X < ResizeBorder;
            bool right = p.X > Width - ResizeBorder;
            bool top = p.Y < ResizeBorder;
            bool bottom = p.Y > Height - ResizeBorder;
            
            if (top && left) return ResizeDirection.TopLeft;
            if (top && right) return ResizeDirection.TopRight;
            if (bottom && left) return ResizeDirection.BottomLeft;
            if (bottom && right) return ResizeDirection.BottomRight;
            if (left) return ResizeDirection.Left;
            if (right) return ResizeDirection.Right;
            if (top) return ResizeDirection.Top;
            if (bottom) return ResizeDirection.Bottom;
            
            return ResizeDirection.None;
        }

        private void OnMouseMove(object? sender, MouseEventArgs e)
        {
            _isMouseOverMessages = _messagesRect.Contains(e.Location);
            
            // Update autocomplete dropdown hover state
            if (_suggestionDropdown.IsVisible)
            {
                _suggestionDropdown.UpdateHover(e.Location);
                if (_suggestionDropdown.ContainsPoint(e.Location))
                {
                    Invalidate();
                }
            }

            if (_isResizing)
            {
                var screenPos = PointToScreen(e.Location);
                int dx = screenPos.X - _resizeStart.X;
                int dy = screenPos.Y - _resizeStart.Y;
                
                int newX = _resizeStartLocation.X;
                int newY = _resizeStartLocation.Y;
                int newW = _resizeStartSize.Width;
                int newH = _resizeStartSize.Height;
                
                const int minW = 250;
                const int minH = 300;
                
                switch (_resizeDir)
                {
                    case ResizeDirection.Right:
                        newW = Math.Max(minW, _resizeStartSize.Width + dx);
                        break;
                    case ResizeDirection.Left:
                        newW = Math.Max(minW, _resizeStartSize.Width - dx);
                        newX = _resizeStartLocation.X + _resizeStartSize.Width - newW;
                        break;
                    case ResizeDirection.Bottom:
                        newH = Math.Max(minH, _resizeStartSize.Height + dy);
                        break;
                    case ResizeDirection.Top:
                        newH = Math.Max(minH, _resizeStartSize.Height - dy);
                        newY = _resizeStartLocation.Y + _resizeStartSize.Height - newH;
                        break;
                    case ResizeDirection.BottomRight:
                        newW = Math.Max(minW, _resizeStartSize.Width + dx);
                        newH = Math.Max(minH, _resizeStartSize.Height + dy);
                        break;
                    case ResizeDirection.BottomLeft:
                        newW = Math.Max(minW, _resizeStartSize.Width - dx);
                        newX = _resizeStartLocation.X + _resizeStartSize.Width - newW;
                        newH = Math.Max(minH, _resizeStartSize.Height + dy);
                        break;
                    case ResizeDirection.TopRight:
                        newW = Math.Max(minW, _resizeStartSize.Width + dx);
                        newH = Math.Max(minH, _resizeStartSize.Height - dy);
                        newY = _resizeStartLocation.Y + _resizeStartSize.Height - newH;
                        break;
                    case ResizeDirection.TopLeft:
                        newW = Math.Max(minW, _resizeStartSize.Width - dx);
                        newX = _resizeStartLocation.X + _resizeStartSize.Width - newW;
                        newH = Math.Max(minH, _resizeStartSize.Height - dy);
                        newY = _resizeStartLocation.Y + _resizeStartSize.Height - newH;
                        break;
                }
                
                SetBounds(newX, newY, newW, newH);
            }
            else if (_isDragging)
            {
                Location = new Point(Location.X + e.X - _dragStart.X, Location.Y + e.Y - _dragStart.Y);
            }
            else if (_isScrollDragging)
            {
                int deltaY = e.Y - _scrollDragStartY;
                float scrollRatio = (float)deltaY / (_messagesRect.Height - 50);
                _scrollOffset = _scrollDragStartOffset - (int)(scrollRatio * _maxScroll);
                _scrollOffset = Math.Clamp(_scrollOffset, 0, _maxScroll);
            }
            else
            {
                // Изменяем курсор в зависимости от позиции
                var resizeDir = GetResizeDirection(e.Location);
                Cursor = resizeDir switch
                {
                    ResizeDirection.Left or ResizeDirection.Right => Cursors.SizeWE,
                    ResizeDirection.Top or ResizeDirection.Bottom => Cursors.SizeNS,
                    ResizeDirection.TopLeft or ResizeDirection.BottomRight => Cursors.SizeNWSE,
                    ResizeDirection.TopRight or ResizeDirection.BottomLeft => Cursors.SizeNESW,
                    _ => Cursors.Default
                };
            }
        }

        private void OnMouseUp(object? sender, MouseEventArgs e)
        {
            bool wasDragging = _isDragging;
            bool wasResizing = _isResizing;
            
            _isDragging = false;
            _isScrollDragging = false;
            _isResizing = false;
            
            // Save settings after drag or resize completes
            if (_settingsLoaded && (wasDragging || wasResizing))
            {
                _settings.UpdatePosition(Location.X, Location.Y);
                _settings.UpdateSize(Width, Height);
                _settings.Save();
            }
        }

        private void OnMouseWheel(object? sender, MouseEventArgs e)
        {
            // Очень быстрый скролл (было /120, теперь /8 = в 15 раз быстрее)
            _scrollOffset += e.Delta / 8;
            _scrollOffset = Math.Clamp(_scrollOffset, 0, _maxScroll);
            Invalidate();
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            // Handle autocomplete navigation first
            if (_suggestionDropdown.IsVisible)
            {
                if (e.KeyCode == Keys.Tab || e.KeyCode == Keys.Enter)
                {
                    ApplyAutocomplete();
                    e.SuppressKeyPress = true;
                    e.Handled = true;
                    return;
                }
                else if (e.KeyCode == Keys.Up)
                {
                    _suggestionDropdown.SelectPrevious();
                    e.SuppressKeyPress = true;
                    Invalidate();
                    return;
                }
                else if (e.KeyCode == Keys.Down)
                {
                    _suggestionDropdown.SelectNext();
                    e.SuppressKeyPress = true;
                    Invalidate();
                    return;
                }
                else if (e.KeyCode == Keys.Escape)
                {
                    _suggestionDropdown.Hide();
                    e.SuppressKeyPress = true;
                    Invalidate();
                    return;
                }
            }
            
            if (e.KeyCode == Keys.Enter && _inputFocused)
            {
                SendMessage();
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                Hide();
            }
            else if (e.KeyCode == Keys.Back && _inputText.Length > 0 && _cursorPosition > 0)
            {
                // Check if we're deleting within a selected entity
                var entityAtCursor = _selectedEntities.Find(se => 
                    _cursorPosition > se.StartIndex && _cursorPosition <= se.EndIndex);
                
                if (entityAtCursor != null)
                {
                    // Remove entire entity
                    _inputText = _inputText.Remove(entityAtCursor.StartIndex, entityAtCursor.EndIndex - entityAtCursor.StartIndex);
                    _cursorPosition = entityAtCursor.StartIndex;
                    _selectedEntities.Remove(entityAtCursor);
                    UpdateEntityPositions(entityAtCursor.StartIndex, entityAtCursor.EndIndex - entityAtCursor.StartIndex, 0);
                }
                else
                {
                    _inputText = _inputText.Remove(_cursorPosition - 1, 1);
                    _cursorPosition--;
                    UpdateEntityPositions(_cursorPosition, 1, 0);
                }
                
                UpdateAutocomplete();
                Invalidate();
            }
            else if (e.KeyCode == Keys.Delete && _cursorPosition < _inputText.Length)
            {
                var entityAtCursor = _selectedEntities.Find(se => 
                    _cursorPosition >= se.StartIndex && _cursorPosition < se.EndIndex);
                
                if (entityAtCursor != null)
                {
                    _inputText = _inputText.Remove(entityAtCursor.StartIndex, entityAtCursor.EndIndex - entityAtCursor.StartIndex);
                    _cursorPosition = entityAtCursor.StartIndex;
                    _selectedEntities.Remove(entityAtCursor);
                    UpdateEntityPositions(entityAtCursor.StartIndex, entityAtCursor.EndIndex - entityAtCursor.StartIndex, 0);
                }
                else
                {
                    _inputText = _inputText.Remove(_cursorPosition, 1);
                    UpdateEntityPositions(_cursorPosition, 1, 0);
                }
                
                UpdateAutocomplete();
                Invalidate();
            }
            else if (e.KeyCode == Keys.Left && _cursorPosition > 0)
            {
                _cursorPosition--;
                Invalidate();
            }
            else if (e.KeyCode == Keys.Right && _cursorPosition < _inputText.Length)
            {
                _cursorPosition++;
                Invalidate();
            }
            else if (e.KeyCode == Keys.Home)
            {
                _cursorPosition = 0;
                Invalidate();
            }
            else if (e.KeyCode == Keys.End)
            {
                _cursorPosition = _inputText.Length;
                Invalidate();
            }
        }

        private void OnKeyPress(object? sender, KeyPressEventArgs e)
        {
            if (_inputFocused && !char.IsControl(e.KeyChar))
            {
                // Insert character at cursor position
                _inputText = _inputText.Insert(_cursorPosition, e.KeyChar.ToString());
                _cursorPosition++;
                
                // Update entity positions
                UpdateEntityPositions(_cursorPosition - 1, 0, 1);
                
                // Trigger autocomplete
                UpdateAutocomplete();
                
                Invalidate();
            }
        }
        
        private void UpdateAutocomplete()
        {
            _currentAutocomplete = _autocompleteEngine.Analyze(_inputText, _cursorPosition);
            
            if (_currentAutocomplete.HasSuggestions)
            {
                // Show dropdown above input field
                int dropdownX = _inputRect.X + 10;
                int dropdownY = _inputRect.Y;
                _suggestionDropdown.Show(_currentAutocomplete.Suggestions, dropdownX, dropdownY, _inputRect.Width);
            }
            else
            {
                _suggestionDropdown.Hide();
            }
        }
        
        private void ApplyAutocomplete()
        {
            if (_currentAutocomplete == null || !_currentAutocomplete.HasSuggestions)
                return;
            
            var selectedEntity = _suggestionDropdown.SelectedEntity;
            if (selectedEntity == null)
                return;
            
            // Apply the selection
            var (newText, newCursor, selection) = _autocompleteEngine.ApplySelection(
                _inputText,
                selectedEntity,
                _currentAutocomplete.WordStartIndex,
                _currentAutocomplete.WordEndIndex);
            
            // Update positions of existing entities
            int oldLen = _currentAutocomplete.WordEndIndex - _currentAutocomplete.WordStartIndex;
            int newLen = selectedEntity.Name.Length;
            UpdateEntityPositions(_currentAutocomplete.WordStartIndex, oldLen, newLen);
            
            // Add new selected entity
            _selectedEntities.Add(selection);
            _contextBuilder.AddEntity(selection);
            
            _inputText = newText;
            _cursorPosition = newCursor;
            
            // Hide dropdown
            _suggestionDropdown.Hide();
            _currentAutocomplete = null;
            
            Invalidate();
        }
        
        private void UpdateEntityPositions(int changeStart, int oldLen, int newLen)
        {
            int delta = newLen - oldLen;
            
            // Remove entities that overlap with the change
            _selectedEntities.RemoveAll(e => 
                (changeStart >= e.StartIndex && changeStart < e.EndIndex) ||
                (changeStart < e.StartIndex && changeStart + oldLen > e.StartIndex));
            
            // Update positions of entities after the change
            foreach (var entity in _selectedEntities)
            {
                if (entity.StartIndex >= changeStart + oldLen)
                {
                    entity.StartIndex += delta;
                    entity.EndIndex += delta;
                }
            }
        }

        private void SendMessage()
        {
            if (!string.IsNullOrWhiteSpace(_inputText) && !_llmStatusManager.IsActive)
            {
                // Build LLM context from selected entities
                string llmMessage = _contextBuilder.BuildFullMessage(_inputText);
                
                // Log the message with context (for debugging)
                Console.WriteLine("=== LLM Message ===");
                Console.WriteLine(llmMessage);
                Console.WriteLine("===================");
                
                // Add visual message
                _messages.Add(new ChatMessage { Text = _inputText, IsPlayer = true, SenderName = "Игрок", Timestamp = DateTime.Now });
                
                string userMessage = _inputText;
                
                // Clear input and state
                _inputText = "";
                _cursorPosition = 0;
                _scrollOffset = 0;
                _selectedEntities.Clear();
                _contextBuilder.Clear();
                _suggestionDropdown.Hide();

                // Start LLM request simulation
                SimulateLLMRequest(userMessage);
                
                Invalidate();
            }
        }
        
        private void SimulateLLMRequest(string userMessage)
        {
            // Start the request
            _llmStatusManager.StartRequest(_npcName, userMessage);
            
            // Simulate state transitions
            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    // State 1: Thinking (1-2 seconds)
                    await System.Threading.Tasks.Task.Delay(1500);
                    
                    if (IsDisposed) return;
                    Invoke((Action)(() =>
                    {
                        // State 2: Fetching context
                        var sources = new List<string>();
                        if (_contextBuilder.HasEntities)
                        {
                            sources.AddRange(new[] { "heroes.json", "settlements.json" });
                        }
                        else
                        {
                            sources.Add("general_knowledge");
                        }
                        _llmStatusManager.SetFetchingContext(sources);
                        Invalidate();
                    }));
                    
                    await System.Threading.Tasks.Task.Delay(1200);
                    
                    if (IsDisposed) return;
                    Invoke((Action)(() =>
                    {
                        // State 3: Generating
                        _llmStatusManager.SetGenerating(350);
                        Invalidate();
                    }));
                    
                    // Simulate token generation
                    for (int i = 0; i < 10; i++)
                    {
                        await System.Threading.Tasks.Task.Delay(150);
                        if (IsDisposed) return;
                        
                        int tokens = (i + 1) * 35;
                        Invoke((Action)(() =>
                        {
                            _llmStatusManager.UpdateTokens(tokens);
                            Invalidate();
                        }));
                    }
                    
                    await System.Threading.Tasks.Task.Delay(300);
                    
                    if (IsDisposed) return;
                    Invoke((Action)(() =>
                    {
                        // Complete with response
                        string response = GetSimulatedResponse(userMessage);
                        _llmStatusManager.Complete(response);
                        
                        // Add response message
                        _messages.Add(new ChatMessage 
                        { 
                            Text = response, 
                            IsPlayer = false, 
                            SenderName = _npcName, 
                            Timestamp = DateTime.Now 
                        });
                        
                        // Reset status after a delay
                        System.Threading.Tasks.Task.Delay(500).ContinueWith(_ =>
                        {
                            if (!IsDisposed)
                            {
                                Invoke((Action)(() =>
                                {
                                    _llmStatusManager.Reset();
                                    Invalidate();
                                }));
                            }
                        });
                        
                        Invalidate();
                    }));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"LLM simulation error: {ex.Message}");
                }
            });
        }
        
        private string GetSimulatedResponse(string userMessage)
        {
            // Simple mock responses based on user input
            string lowerMsg = userMessage.ToLower();
            
            if (lowerMsg.Contains("привет") || lowerMsg.Contains("здравствуй"))
            {
                return "Приветствую тебя, путник! Чем могу помочь?";
            }
            else if (lowerMsg.Contains("торговл") || lowerMsg.Contains("товар"))
            {
                return "Торговля - моё призвание! У меня есть отличные товары из далёких земель. Могу рассказать о ценах на зерно, железо или шёлк.";
            }
            else if (lowerMsg.Contains("война") || lowerMsg.Contains("бой") || lowerMsg.Contains("армия"))
            {
                return "Война... Тяжёлые времена настали. Слышал, что Вландия готовит поход на север. Лучше держаться подальше от границ.";
            }
            else if (lowerMsg.Contains("королев") || lowerMsg.Contains("корол"))
            {
                return "О королевствах я знаю немало! Империя раздроблена на три части, Вландия крепнет на западе, а Стургия терпит набеги с юга.";
            }
            else
            {
                return "Интересный вопрос! Дай-ка подумаю... Знаешь, за годы странствий я многое повидал. Расскажи подробнее, что тебя интересует?";
            }
        }

        public void SetTheme(int index)
        {
            _themeIndex = Math.Clamp(index, 0, Themes.Length - 1);
            
            // Save theme setting
            if (_settingsLoaded)
            {
                _settings.ThemeIndex = _themeIndex;
                _settings.Save();
            }
            
            Invalidate();
        }

        public void UpdatePosition(int x, int y, int width, int height)
        {
            Location = new Point(x, y);
            Size = new Size(width, height);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var theme = Themes[_themeIndex];
            int padding = 12;
            int headerHeight = 50;
            int inputHeight = CalculateInputHeight();

            // Квадратный фон (без закругления)
            using var bgBrush = new SolidBrush(theme[0]);
            g.FillRectangle(bgBrush, 0, 0, Width, Height);

            // Header
            using var headerBrush = new SolidBrush(theme[1]);
            g.FillRectangle(headerBrush, 0, 0, Width, headerHeight);

            using var titleFont = new Font("Segoe UI", 13, FontStyle.Bold);
            using var textBrush = new SolidBrush(theme[5]);
            g.DrawString(_npcName, titleFont, textBrush, padding + 10, (headerHeight - titleFont.Height) / 2);
            
            // Debug button in header
            using var debugFont = new Font("Segoe UI", 9);
            string debugText = _llmStatusManager.IsDebugMode ? "Debug ●" : "Debug";
            var debugSize = g.MeasureString(debugText, debugFont);
            _debugButtonRect = new Rectangle(Width - (int)debugSize.Width - padding - 10, 
                                             (headerHeight - (int)debugSize.Height) / 2, 
                                             (int)debugSize.Width + 10, 
                                             (int)debugSize.Height + 4);
            
            // Draw debug button
            Color debugBtnColor = _llmStatusManager.IsDebugMode 
                ? Color.FromArgb(100, 100, 200, 100) 
                : Color.FromArgb(50, theme[5]);
            using (var debugBtnBrush = new SolidBrush(debugBtnColor))
            using (var debugBtnPath = CreateRoundedRect(_debugButtonRect, 4))
            {
                g.FillPath(debugBtnBrush, debugBtnPath);
            }
            using (var debugTextBrush = new SolidBrush(_llmStatusManager.IsDebugMode ? Color.LightGreen : theme[6]))
            {
                g.DrawString(debugText, debugFont, debugTextBrush, _debugButtonRect.X + 5, _debugButtonRect.Y + 2);
            }

            // Messages area
            int messagesTop = headerHeight + padding;
            int messagesBottom = Height - inputHeight - padding * 2;
            int messagesHeight = messagesBottom - messagesTop;
            _messagesRect = new Rectangle(0, messagesTop, Width, messagesHeight);

            g.SetClip(_messagesRect);

            // Draw messages
            int y = messagesBottom + _scrollOffset;
            int totalContentHeight = 0;
            
            for (int i = _messages.Count - 1; i >= 0; i--)
            {
                var msg = _messages[i];
                int bubbleHeight = DrawMessageBubble(g, msg, y, theme, padding, out int actualHeight);
                y -= actualHeight + 12;
                totalContentHeight += actualHeight + 12;
                if (y < messagesTop - 300) break;
            }

            _maxScroll = Math.Max(0, totalContentHeight - messagesHeight + 50);

            g.ResetClip();
            
            // Draw LLM status bubble if active (outside clip area, at bottom of messages)
            if (_llmStatusManager.IsActive)
            {
                using var statusFont = new Font("Segoe UI", 11);
                // Position at bottom of messages area, just above input
                int statusY = messagesBottom - 55;
                var statusBounds = new Rectangle(padding, statusY, Width - padding * 2, 50);
                _llmStatusManager.DrawStatusBubble(g, statusBounds, statusFont, theme[4], theme[5]);
            }
            
            // Draw debug panel if enabled
            if (_llmStatusManager.IsDebugMode && _llmStatusManager.Status.DebugLog.Count > 0)
            {
                int debugPanelHeight = _llmStatusManager.GetDebugPanelHeight();
                _debugPanelRect = new Rectangle(0, messagesTop, Width, debugPanelHeight);
                
                using var debugPanelFont = new Font("Segoe UI", 9);
                _llmStatusManager.DrawDebugPanel(g, _debugPanelRect, debugPanelFont, 
                    Color.FromArgb(40, 40, 55), theme[5], Color.FromArgb(50, 50, 70));
            }

            // Scrollbar (появляется при наведении)
            if (_scrollBarOpacity > 0.05f && _maxScroll > 0)
            {
                int scrollBarWidth = 6;
                int scrollBarX = Width - scrollBarWidth - 4;
                int scrollBarHeight = messagesHeight - 20;
                
                float visibleRatio = (float)messagesHeight / (messagesHeight + _maxScroll);
                int thumbHeight = Math.Max(30, (int)(scrollBarHeight * visibleRatio));
                float scrollRatio = _maxScroll > 0 ? (float)_scrollOffset / _maxScroll : 0;
                int thumbY = messagesTop + 10 + (int)((scrollBarHeight - thumbHeight) * (1 - scrollRatio));

                _scrollBarRect = new Rectangle(scrollBarX, messagesTop + 10, scrollBarWidth, scrollBarHeight);
                _scrollThumbRect = new Rectangle(scrollBarX, thumbY, scrollBarWidth, thumbHeight);

                // Фон скроллбара
                using var trackBrush = new SolidBrush(Color.FromArgb((int)(30 * _scrollBarOpacity), theme[5]));
                using var trackPath = CreateRoundedRect(_scrollBarRect, 3);
                g.FillPath(trackBrush, trackPath);

                // Thumb скроллбара
                using var thumbBrush = new SolidBrush(Color.FromArgb((int)(150 * _scrollBarOpacity), theme[5]));
                using var thumbPath = CreateRoundedRect(_scrollThumbRect, 3);
                g.FillPath(thumbBrush, thumbPath);
            }

            // Input area (закруглённое)
            int inputTop = Height - inputHeight - padding;
            _inputRect = new Rectangle(padding, inputTop, Width - padding * 2 - 55, inputHeight);

            using var inputBrush = new SolidBrush(theme[2]);
            using var inputPath = CreateRoundedRect(_inputRect, 12);
            g.FillPath(inputBrush, inputPath);

            using var inputFont = new Font("Segoe UI", 11);
            
            // Draw input text with highlighted entities
            DrawInputWithHighlights(g, inputFont, theme);

            // Cursor
            if (_inputFocused && _cursorBlinkCounter < 8)
            {
                float cursorX = _inputRect.X + 15;
                float cursorY = _inputRect.Y + 12;
                
                if (!string.IsNullOrEmpty(_inputText))
                {
                    // Calculate cursor position based on text before cursor
                    string textBeforeCursor = _inputText.Substring(0, _cursorPosition);
                    var textSize = g.MeasureString(textBeforeCursor, inputFont);
                    var maxWidth = _inputRect.Width - 30;
                    
                    if (textSize.Width < maxWidth)
                    {
                        cursorX += textSize.Width;
                    }
                    else
                    {
                        var lines = WrapText(textBeforeCursor, inputFont, (int)maxWidth, g);
                        if (lines.Count > 0)
                        {
                            var lastLineWidth = g.MeasureString(lines[lines.Count - 1], inputFont).Width;
                            cursorX += lastLineWidth;
                            cursorY += (lines.Count - 1) * inputFont.Height;
                        }
                    }
                }
                
                using var cursorPen = new Pen(theme[5], 2);
                g.DrawLine(cursorPen, cursorX, cursorY, cursorX, cursorY + inputFont.Height);
            }
            
            // Draw autocomplete dropdown
            _suggestionDropdown.Draw(g);

            // Send button (закруглённый)
            _sendButtonRect = new Rectangle(Width - padding - 45, inputTop + (inputHeight - 42) / 2, 42, 42);
            using var sendBrush = new SolidBrush(theme[3]);
            using var sendPath = CreateRoundedRect(_sendButtonRect, 21);
            g.FillPath(sendBrush, sendPath);

            using var arrowPen = new Pen(Color.White, 2.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            int arrowCx = _sendButtonRect.X + 21;
            int arrowCy = _sendButtonRect.Y + 21;
            g.DrawLine(arrowPen, arrowCx - 8, arrowCy, arrowCx + 6, arrowCy);
            g.DrawLine(arrowPen, arrowCx + 1, arrowCy - 6, arrowCx + 7, arrowCy);
            g.DrawLine(arrowPen, arrowCx + 1, arrowCy + 6, arrowCx + 7, arrowCy);
        }

        private int DrawMessageBubble(Graphics g, ChatMessage msg, int bottomY, Color[] theme, int padding, out int totalHeight)
        {
            int maxBubbleWidth = Width - padding * 4 - 40;
            int bubblePadding = 12;

            using var msgFont = new Font("Segoe UI", 11);
            using var timeFont = new Font("Segoe UI", 8);

            var textSize = g.MeasureString(msg.Text, msgFont, maxBubbleWidth);
            string timeText = msg.Timestamp.ToString("dd.MM.yyyy HH:mm");
            var timeSize = g.MeasureString(timeText, timeFont);

            int bubbleWidth = (int)Math.Max(textSize.Width, 80) + bubblePadding * 2;
            bubbleWidth = Math.Min(bubbleWidth, maxBubbleWidth);

            textSize = g.MeasureString(msg.Text, msgFont, bubbleWidth - bubblePadding * 2);

            int bubbleHeight = (int)textSize.Height + (int)timeSize.Height + bubblePadding * 2 + 8;
            totalHeight = bubbleHeight;

            int bubbleX = msg.IsPlayer ? Width - padding - bubbleWidth - 8 : padding + 8;
            int bubbleY = bottomY - bubbleHeight;

            var bubbleRect = new Rectangle(bubbleX, bubbleY, bubbleWidth, bubbleHeight);
            var bubbleColor = msg.IsPlayer ? theme[3] : theme[4];
            using var bubbleBrush = new SolidBrush(bubbleColor);
            using var bubblePath = CreateRoundedRect(bubbleRect, 12);
            g.FillPath(bubbleBrush, bubblePath);

            var textColor = msg.IsPlayer && _themeIndex == 0 ? Color.White : theme[5];
            using var txtBrush = new SolidBrush(textColor);
            var msgRect = new RectangleF(bubbleX + bubblePadding, bubbleY + bubblePadding, bubbleWidth - bubblePadding * 2, textSize.Height + 5);
            g.DrawString(msg.Text, msgFont, txtBrush, msgRect);

            var timeColor = Color.FromArgb(150, textColor);
            using var timeBrush = new SolidBrush(timeColor);
            g.DrawString(timeText, timeFont, timeBrush, bubbleX + bubbleWidth - timeSize.Width - bubblePadding, bubbleY + bubbleHeight - timeSize.Height - 6);

            return bubbleHeight;
        }

        private void DrawInputWithHighlights(Graphics g, Font font, Color[] theme)
        {
            var textRect = new RectangleF(_inputRect.X + 15, _inputRect.Y + 10, _inputRect.Width - 30, _inputRect.Height - 20);
            
            if (string.IsNullOrEmpty(_inputText))
            {
                // Draw placeholder
                using var placeholderBrush = new SolidBrush(theme[6]);
                g.DrawString("Напишите сообщение...", font, placeholderBrush, textRect);
                return;
            }
            
            // Sort entities by position
            var sortedEntities = new List<SelectedEntity>(_selectedEntities);
            sortedEntities.Sort((a, b) => a.StartIndex.CompareTo(b.StartIndex));
            
            float x = textRect.X;
            float y = textRect.Y;
            int currentPos = 0;
            
            // Use TextRenderer for more accurate text measurement
            var measureFlags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
            
            using var normalBrush = new SolidBrush(theme[5]);
            
            foreach (var entity in sortedEntities)
            {
                // Draw text before entity
                if (currentPos < entity.StartIndex)
                {
                    string beforeText = _inputText.Substring(currentPos, entity.StartIndex - currentPos);
                    var beforeSize = TextRenderer.MeasureText(g, beforeText, font, Size.Empty, measureFlags);
                    TextRenderer.DrawText(g, beforeText, font, new Point((int)x, (int)y), theme[5], measureFlags);
                    x += beforeSize.Width;
                }
                
                // Draw highlighted entity
                string entityText = _inputText.Substring(entity.StartIndex, entity.EndIndex - entity.StartIndex);
                var entitySize = TextRenderer.MeasureText(g, entityText, font, Size.Empty, measureFlags);
                
                // Draw background for entity with small padding
                var entityColor = SuggestionDropdown.GetTypeColor(entity.Entity.Type);
                int highlightPadding = 3;
                var bgRect = new RectangleF(
                    x - highlightPadding, 
                    y - 1, 
                    entitySize.Width + highlightPadding * 2, 
                    entitySize.Height + 2);
                
                using (var entityBgPath = CreateRoundedRectF(bgRect, 4))
                using (var entityBgBrush = new SolidBrush(Color.FromArgb(60, entityColor)))
                {
                    g.FillPath(entityBgBrush, entityBgPath);
                }
                
                // Draw entity text
                TextRenderer.DrawText(g, entityText, font, new Point((int)x, (int)y), entityColor, measureFlags);
                
                // Move x past the entity plus a small gap
                x += entitySize.Width + 2;
                currentPos = entity.EndIndex;
            }
            
            // Draw remaining text
            if (currentPos < _inputText.Length)
            {
                string afterText = _inputText.Substring(currentPos);
                TextRenderer.DrawText(g, afterText, font, new Point((int)x, (int)y), theme[5], measureFlags);
            }
        }
        
        private GraphicsPath CreateRoundedRectF(RectangleF rect, int radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private List<string> WrapText(string text, Font font, int maxWidth, Graphics g)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text)) return lines;

            var words = text.Split(' ');
            string currentLine = "";

            foreach (var word in words)
            {
                string testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
                var size = g.MeasureString(testLine, font);

                if (size.Width > maxWidth && !string.IsNullOrEmpty(currentLine))
                {
                    lines.Add(currentLine);
                    currentLine = word;
                }
                else
                {
                    currentLine = testLine;
                }
            }

            if (!string.IsNullOrEmpty(currentLine))
                lines.Add(currentLine);

            return lines;
        }

        private int CalculateInputHeight()
        {
            if (string.IsNullOrEmpty(_inputText)) return 50;
            using var g = CreateGraphics();
            using var font = new Font("Segoe UI", 11);
            
            // Use TextRenderer for accurate measurement without extra padding
            var maxSize = new Size(Width - 120, 9999);
            var textSize = TextRenderer.MeasureText(g, _inputText, font, maxSize, 
                TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
            
            int lines = Math.Max(1, (int)Math.Ceiling((float)textSize.Height / font.Height));
            return Math.Min(150, Math.Max(50, (lines - 1) * 22 + 50)); // First line is 50, each additional +22
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

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x00000080;
                return cp;
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _updateTimer?.Stop();
            _updateTimer?.Dispose();
            base.OnFormClosed(e);
        }
    }
}
