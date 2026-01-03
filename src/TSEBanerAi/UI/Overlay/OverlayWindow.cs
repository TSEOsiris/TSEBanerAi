using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using TSEBanerAi.Utils;

namespace TSEBanerAi.UI.Overlay
{
    /// <summary>
    /// Overlay window using WinAPI for rendering chat on top of game
    /// </summary>
    public class OverlayWindow : IDisposable
    {
        #region WinAPI P/Invoke

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetClientRect(IntPtr hwnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CreateWindowEx(
            uint dwExStyle, string lpClassName, string lpWindowName,
            uint dwStyle, int x, int y, int nWidth, int nHeight,
            IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyWindow(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ShowWindow(IntPtr hwnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hwnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hwnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hwnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern bool UpdateWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern bool InvalidateRect(IntPtr hwnd, IntPtr lpRect, bool bErase);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr SetFocus(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr SetActiveWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr SetCursor(IntPtr hCursor);

        [DllImport("user32.dll")]
        private static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);

        [DllImport("user32.dll")]
        private static extern IntPtr SetClassLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        private const int IDC_ARROW = 32512;
        private const int GCL_HCURSOR = -12;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        // Window styles
        private const uint WS_POPUP = 0x80000000;
        private const uint WS_VISIBLE = 0x10000000;
        private const uint WS_EX_TOPMOST = 0x00000008;
        private const uint WS_EX_LAYERED = 0x00080000;
        private const uint WS_EX_TRANSPARENT = 0x00000020;
        private const uint WS_EX_TOOLWINDOW = 0x00000080;
        private const uint WS_EX_NOACTIVATE = 0x08000000;

        // SetWindowPos flags
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_NOACTIVATE = 0x0010;

        // SetLayeredWindowAttributes flags
        private const uint LWA_COLORKEY = 0x00000001;
        private const uint LWA_ALPHA = 0x00000002;

        // GetWindowLong index
        private const int GWL_EXSTYLE = -20;

        // ShowWindow commands
        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;
        private const int SW_SHOWNOACTIVATE = 4;

        // Window positions
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_TOP = new IntPtr(0);

        #endregion

        private IntPtr _handle = IntPtr.Zero;
        private IntPtr _gameWindowHandle = IntPtr.Zero;
        private bool _isCreated;
        private bool _isVisible;
        private RECT _gameWindowRect;
        private int _chatX, _chatY, _chatWidth, _chatHeight;
        private bool _clickThrough = true;

        // No longer using color key transparency - using alpha transparency instead

        public IntPtr Handle => _handle;
        public bool IsCreated => _isCreated;
        public bool IsVisible => _isVisible;
        public int GameWindowWidth => _gameWindowRect.Right - _gameWindowRect.Left;
        public int GameWindowHeight => _gameWindowRect.Bottom - _gameWindowRect.Top;
        public int GameWindowX => _gameWindowRect.Left;
        public int GameWindowY => _gameWindowRect.Top;
        public IntPtr GameWindowHandle => _gameWindowHandle;
        public int ChatWidth => _chatWidth;
        public int ChatHeight => _chatHeight;
        public int ChatX => _chatX;
        public int ChatY => _chatY;

        // Store found window handle for EnumWindows callback
        private static IntPtr _foundWindowHandle = IntPtr.Zero;
        private static uint _targetProcessId = 0;
        private static string[] _searchPatterns = new[]
        {
            "Mount and Blade II Bannerlord",
            "Mount & Blade II Bannerlord",
            "Bannerlord"
        };

        /// <summary>
        /// Find game window using multiple methods
        /// </summary>
        private IntPtr FindGameWindow()
        {
            IntPtr hwnd = IntPtr.Zero;

            // Get current process ID first
            try
            {
                var currentProcess = Process.GetCurrentProcess();
                _targetProcessId = (uint)currentProcess.Id;
                ModLogger.LogDebug($"Current process ID: {_targetProcessId}, Name: {currentProcess.ProcessName}");
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Error getting process ID", ex);
            }

            // Method 1: Enumerate windows and find by partial title match (best for dynamic titles)
            try
            {
                _foundWindowHandle = IntPtr.Zero;
                EnumWindows(EnumWindowsByTitleCallback, IntPtr.Zero);
                if (_foundWindowHandle != IntPtr.Zero)
                {
                    StringBuilder sb = new StringBuilder(512);
                    GetWindowText(_foundWindowHandle, sb, 512);
                    ModLogger.LogDebug($"Found window by title pattern: {_foundWindowHandle}, Title: '{sb}'");
                    return _foundWindowHandle;
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Error enumerating windows by title", ex);
            }

            // Method 2: Find window of current process by MainWindowHandle
            try
            {
                var currentProcess = Process.GetCurrentProcess();
                hwnd = currentProcess.MainWindowHandle;
                if (hwnd != IntPtr.Zero)
                {
                    StringBuilder sb = new StringBuilder(512);
                    GetWindowText(hwnd, sb, 512);
                    ModLogger.LogDebug($"Found main window handle: {hwnd}, Title: '{sb}'");
                    return hwnd;
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Error getting process main window", ex);
            }

            // Method 3: Enumerate all windows and find by process ID
            try
            {
                _foundWindowHandle = IntPtr.Zero;
                EnumWindows(EnumWindowsByProcessCallback, IntPtr.Zero);
                if (_foundWindowHandle != IntPtr.Zero)
                {
                    StringBuilder sb = new StringBuilder(512);
                    GetWindowText(_foundWindowHandle, sb, 512);
                    ModLogger.LogDebug($"Found window by process ID: {_foundWindowHandle}, Title: '{sb}'");
                    return _foundWindowHandle;
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Error enumerating windows by process", ex);
            }

            ModLogger.LogError("All methods to find game window failed");
            return IntPtr.Zero;
        }

        /// <summary>
        /// Callback for EnumWindows - find by partial title match
        /// </summary>
        private static bool EnumWindowsByTitleCallback(IntPtr hWnd, IntPtr lParam)
        {
            // Check if window is visible
            if (!IsWindowVisible(hWnd))
                return true; // Continue enumeration

            // Get window title
            StringBuilder sb = new StringBuilder(512);
            GetWindowText(hWnd, sb, 512);
            string title = sb.ToString();

            if (string.IsNullOrEmpty(title))
                return true; // Continue enumeration

            // Check if title contains any of the patterns
            foreach (var pattern in _searchPatterns)
            {
                if (title.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // Verify it's a main window (has reasonable size)
                    if (GetWindowRect(hWnd, out RECT rect))
                    {
                        int width = rect.Right - rect.Left;
                        int height = rect.Bottom - rect.Top;

                        if (width > 400 && height > 300)
                        {
                            _foundWindowHandle = hWnd;
                            return false; // Stop enumeration
                        }
                    }
                }
            }

            return true; // Continue enumeration
        }

        /// <summary>
        /// Callback for EnumWindows - find by process ID
        /// </summary>
        private static bool EnumWindowsByProcessCallback(IntPtr hWnd, IntPtr lParam)
        {
            // Check if window belongs to our process
            GetWindowThreadProcessId(hWnd, out uint processId);
            if (processId != _targetProcessId)
                return true; // Continue enumeration

            // Check if window is visible
            if (!IsWindowVisible(hWnd))
                return true; // Continue enumeration

            // Get window rect to check if it's a main window (has size)
            if (GetWindowRect(hWnd, out RECT rect))
            {
                int width = rect.Right - rect.Left;
                int height = rect.Bottom - rect.Top;

                // Main game window should be reasonably large
                if (width > 400 && height > 300)
                {
                    _foundWindowHandle = hWnd;
                    return false; // Stop enumeration
                }
            }

            return true; // Continue enumeration
        }

        /// <summary>
        /// Create overlay window
        /// </summary>
        public bool Create()
        {
            if (_isCreated)
                return true;

            try
            {
                ModLogger.LogDebug("Creating overlay window...");

                // Find Bannerlord game window using multiple methods
                _gameWindowHandle = FindGameWindow();
                if (_gameWindowHandle == IntPtr.Zero)
                {
                    ModLogger.LogError("Could not find Bannerlord game window");
                    return false;
                }

                ModLogger.LogDebug($"Found game window: {_gameWindowHandle}");

                // Get game window bounds
                if (!GetWindowRect(_gameWindowHandle, out _gameWindowRect))
                {
                    ModLogger.LogError("Failed to get game window rect");
                    return false;
                }

                ModLogger.LogDebug($"Game window rect: {_gameWindowRect.Left},{_gameWindowRect.Top} - {_gameWindowRect.Right},{_gameWindowRect.Bottom}");

                // Calculate chat window size and position
                _chatWidth = (int)(GameWindowWidth * 0.3f);  // 30% of game window
                _chatHeight = (int)(GameWindowHeight * 0.7f); // 70% of game window
                _chatX = _gameWindowRect.Left + GameWindowWidth - _chatWidth - 20;
                _chatY = _gameWindowRect.Top + (int)(GameWindowHeight * 0.15f);

                ModLogger.LogDebug($"Chat window: {_chatWidth}x{_chatHeight} at ({_chatX}, {_chatY})");

                // Create overlay window with extended styles
                // WS_EX_TOPMOST: Always on top
                // WS_EX_LAYERED: Supports transparency (for alpha)
                // WS_EX_TOOLWINDOW: Don't show in taskbar
                // NOT using WS_EX_TRANSPARENT - we want to receive input!
                // NOT using WS_EX_NOACTIVATE - we want to be able to focus!
                uint exStyle = WS_EX_TOPMOST | WS_EX_LAYERED | WS_EX_TOOLWINDOW;
                uint style = WS_POPUP;

                // Create window sized exactly for chat (not full screen)
                _handle = CreateWindowEx(
                    exStyle,
                    "STATIC", // Use built-in STATIC class
                    "TSEBanerAi Overlay",
                    style,
                    _chatX,
                    _chatY,
                    _chatWidth,
                    _chatHeight,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    GetModuleHandle(null),
                    IntPtr.Zero
                );

                if (_handle == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    ModLogger.LogError($"Failed to create overlay window, error: {error}");
                    return false;
                }

                ModLogger.LogDebug($"Overlay window created: {_handle}, size: {_chatWidth}x{_chatHeight}");

                // Set layered window attributes for transparency
                // Use alpha transparency (230 = ~90% opaque)
                if (!SetLayeredWindowAttributes(_handle, 0, 230, LWA_ALPHA))
                {
                    int error = Marshal.GetLastWin32Error();
                    ModLogger.LogError($"Failed to set layered window attributes, error: {error}");
                }

                // Set normal arrow cursor (not loading cursor)
                IntPtr hCursor = LoadCursor(IntPtr.Zero, IDC_ARROW);
                if (hCursor != IntPtr.Zero)
                {
                    SetClassLongPtr(_handle, GCL_HCURSOR, hCursor);
                }

                _isCreated = true;
                ModLogger.LogDebug("Overlay window created successfully");
                return true;
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to create overlay window", ex);
                return false;
            }
        }

        /// <summary>
        /// Show overlay window
        /// </summary>
        public void Show()
        {
            if (!_isCreated || _handle == IntPtr.Zero)
                return;

            try
            {
                ModLogger.LogDebug("Showing overlay window...");

                // Recalculate chat position based on current game window
                UpdateGameWindowBounds();
                _chatX = _gameWindowRect.Left + GameWindowWidth - _chatWidth - 20;
                _chatY = _gameWindowRect.Top + (int)(GameWindowHeight * 0.15f);

                // Show window and activate it for input
                ShowWindow(_handle, SW_SHOW);

                // Position window at chat location (not full screen)
                SetWindowPos(_handle, HWND_TOPMOST,
                    _chatX, _chatY,
                    _chatWidth, _chatHeight,
                    SWP_SHOWWINDOW);

                _isVisible = true;
                ModLogger.LogDebug($"Overlay window shown at ({_chatX}, {_chatY}), size: {_chatWidth}x{_chatHeight}");
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to show overlay window", ex);
            }
        }

        /// <summary>
        /// Hide overlay window
        /// </summary>
        public void Hide()
        {
            if (!_isCreated || _handle == IntPtr.Zero)
                return;

            try
            {
                ShowWindow(_handle, SW_HIDE);
                _isVisible = false;
                ModLogger.LogDebug("Overlay window hidden");
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to hide overlay window", ex);
            }
        }

        /// <summary>
        /// Set chat window size and position within overlay
        /// </summary>
        public void SetChatSizeAndPosition(int chatX, int chatY, int chatWidth, int chatHeight)
        {
            // Only update if size changed significantly
            if (Math.Abs(_chatWidth - chatWidth) > 10 || Math.Abs(_chatHeight - chatHeight) > 10 ||
                Math.Abs(_chatX - chatX) > 10 || Math.Abs(_chatY - chatY) > 10)
            {
                _chatX = chatX;
                _chatY = chatY;
                _chatWidth = chatWidth;
                _chatHeight = chatHeight;

                // Move the overlay window to match chat position
                if (_isCreated && _handle != IntPtr.Zero && _isVisible)
                {
                    SetWindowPos(_handle, HWND_TOPMOST,
                        _chatX, _chatY,
                        _chatWidth, _chatHeight,
                        SWP_NOACTIVATE);
                }
            }
        }

        /// <summary>
        /// Set click-through mode
        /// </summary>
        public void SetClickThrough(bool clickThrough)
        {
            if (!_isCreated || _handle == IntPtr.Zero)
                return;

            if (_clickThrough == clickThrough)
                return;

            try
            {
                int exStyle = GetWindowLong(_handle, GWL_EXSTYLE);
                if (clickThrough)
                {
                    exStyle |= (int)WS_EX_TRANSPARENT;
                }
                else
                {
                    exStyle &= ~(int)WS_EX_TRANSPARENT;
                }
                SetWindowLong(_handle, GWL_EXSTYLE, exStyle);
                _clickThrough = clickThrough;
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to set click-through", ex);
            }
        }

        /// <summary>
        /// Set window alpha (0-255)
        /// </summary>
        public void SetAlpha(byte alpha)
        {
            if (!_isCreated || _handle == IntPtr.Zero)
                return;

            try
            {
                SetLayeredWindowAttributes(_handle, 0, alpha, LWA_ALPHA);
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to set alpha", ex);
            }
        }

        /// <summary>
        /// Update game window bounds (call periodically)
        /// </summary>
        public void UpdateGameWindowBounds()
        {
            if (_gameWindowHandle == IntPtr.Zero)
                return;

            try
            {
                RECT newRect;
                if (GetWindowRect(_gameWindowHandle, out newRect))
                {
                    // Check if bounds changed
                    if (newRect.Left != _gameWindowRect.Left ||
                        newRect.Top != _gameWindowRect.Top ||
                        newRect.Right != _gameWindowRect.Right ||
                        newRect.Bottom != _gameWindowRect.Bottom)
                    {
                        _gameWindowRect = newRect;

                        // Update overlay position
                        if (_isCreated && _handle != IntPtr.Zero && _isVisible)
                        {
                            SetWindowPos(_handle, HWND_TOPMOST,
                                _gameWindowRect.Left, _gameWindowRect.Top,
                                GameWindowWidth, GameWindowHeight,
                                SWP_NOACTIVATE);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to update game window bounds", ex);
            }
        }

        /// <summary>
        /// Get device context for rendering
        /// </summary>
        public IntPtr GetDC()
        {
            if (_handle == IntPtr.Zero)
                return IntPtr.Zero;
            return GetDC(_handle);
        }

        /// <summary>
        /// Release device context
        /// </summary>
        public void ReleaseDC(IntPtr hdc)
        {
            if (_handle != IntPtr.Zero && hdc != IntPtr.Zero)
            {
                ReleaseDC(_handle, hdc);
            }
        }

        /// <summary>
        /// Check if game window is minimized
        /// </summary>
        public bool IsGameWindowMinimized()
        {
            if (_gameWindowHandle == IntPtr.Zero)
                return false;
            return IsIconic(_gameWindowHandle);
        }

        /// <summary>
        /// Set cursor to normal arrow (call each frame if needed)
        /// </summary>
        public void SetArrowCursor()
        {
            IntPtr hCursor = LoadCursor(IntPtr.Zero, IDC_ARROW);
            if (hCursor != IntPtr.Zero)
            {
                SetCursor(hCursor);
            }
        }

        /// <summary>
        /// Focus the overlay window for input
        /// </summary>
        public void Focus()
        {
            if (_handle == IntPtr.Zero)
                return;

            try
            {
                SetForegroundWindow(_handle);
                SetActiveWindow(_handle);
                SetFocus(_handle);
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to focus overlay window", ex);
            }
        }

        /// <summary>
        /// Return focus to game window
        /// </summary>
        public void ReturnFocusToGame()
        {
            if (_gameWindowHandle == IntPtr.Zero)
                return;

            try
            {
                SetForegroundWindow(_gameWindowHandle);
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to return focus to game", ex);
            }
        }

        /// <summary>
        /// Get mouse position relative to this window
        /// </summary>
        public POINT GetMousePosition()
        {
            POINT pt = new POINT();
            if (_handle == IntPtr.Zero)
                return pt;

            GetCursorPos(out pt);
            ScreenToClient(_handle, ref pt);
            return pt;
        }

        /// <summary>
        /// Check if mouse is over this window
        /// </summary>
        public bool IsMouseOver()
        {
            if (_handle == IntPtr.Zero)
                return false;

            POINT pt;
            GetCursorPos(out pt);
            
            return pt.X >= _chatX && pt.X <= _chatX + _chatWidth &&
                   pt.Y >= _chatY && pt.Y <= _chatY + _chatHeight;
        }

        /// <summary>
        /// Check if this window has focus
        /// </summary>
        public bool HasFocus()
        {
            if (_handle == IntPtr.Zero)
                return false;
            return GetForegroundWindow() == _handle;
        }

        /// <summary>
        /// Get mouse position in screen coordinates
        /// </summary>
        public POINT GetMouseScreenPosition()
        {
            POINT pt = new POINT();
            GetCursorPos(out pt);
            return pt;
        }

        public void Dispose()
        {
            try
            {
                if (_handle != IntPtr.Zero)
                {
                    DestroyWindow(_handle);
                    _handle = IntPtr.Zero;
                }
                _isCreated = false;
                _isVisible = false;
                ModLogger.LogDebug("OverlayWindow disposed");
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Error disposing OverlayWindow", ex);
            }
        }
    }
}
