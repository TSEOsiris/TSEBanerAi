using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using TSEBanerAi.Utils;

namespace TSEBanerAi.UI.Overlay
{
    /// <summary>
    /// Input handler for overlay window
    /// </summary>
    public class OverlayInputHandler
    {
        #region WinAPI Input P/Invoke

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        // Virtual key codes
        private const int VK_LBUTTON = 0x01;
        private const int VK_RBUTTON = 0x02;
        private const int VK_RETURN = 0x0D;
        private const int VK_BACK = 0x08;
        private const int VK_ESCAPE = 0x1B;

        #endregion

        private OverlayWindow? _overlayWindow;
        private Dictionary<int, bool> _keyStates = new Dictionary<int, bool>();
        private Dictionary<int, bool> _previousKeyStates = new Dictionary<int, bool>();
        private POINT _mousePosition;
        private POINT _previousMousePosition;
        private bool _isEnabled;

        public bool IsEnabled
        {
            get => _isEnabled;
            set => _isEnabled = value;
        }

        public OverlayInputHandler(OverlayWindow? overlayWindow = null)
        {
            _overlayWindow = overlayWindow;
            _keyStates = new Dictionary<int, bool>();
            _previousKeyStates = new Dictionary<int, bool>();
            _mousePosition = new POINT();
            _previousMousePosition = new POINT();
            _isEnabled = true;
        }

        /// <summary>
        /// Update input state
        /// </summary>
        public void Update()
        {
            if (!_isEnabled)
                return;

            try
            {
                // Update previous states
                _previousKeyStates = new Dictionary<int, bool>(_keyStates);
                _previousMousePosition = _mousePosition;

                // Get mouse position
                GetCursorPos(out _mousePosition);

                // Convert to chat window client coordinates
                if (_overlayWindow != null)
                {
                    _mousePosition.x -= _overlayWindow.ChatX;
                    _mousePosition.y -= _overlayWindow.ChatY;
                }

                // Update key states - all keys we need
                _keyStates[VK_LBUTTON] = (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0;
                _keyStates[VK_RBUTTON] = (GetAsyncKeyState(VK_RBUTTON) & 0x8000) != 0;
                _keyStates[VK_RETURN] = (GetAsyncKeyState(VK_RETURN) & 0x8000) != 0;
                _keyStates[VK_BACK] = (GetAsyncKeyState(VK_BACK) & 0x8000) != 0;
                _keyStates[VK_ESCAPE] = (GetAsyncKeyState(VK_ESCAPE) & 0x8000) != 0;
                
                // Shift key
                _keyStates[0x10] = (GetAsyncKeyState(0x10) & 0x8000) != 0;
                
                // Arrow keys
                _keyStates[0x25] = (GetAsyncKeyState(0x25) & 0x8000) != 0; // Left
                _keyStates[0x26] = (GetAsyncKeyState(0x26) & 0x8000) != 0; // Up
                _keyStates[0x27] = (GetAsyncKeyState(0x27) & 0x8000) != 0; // Right
                _keyStates[0x28] = (GetAsyncKeyState(0x28) & 0x8000) != 0; // Down
                
                // Tab, Delete, Home, End
                _keyStates[0x09] = (GetAsyncKeyState(0x09) & 0x8000) != 0; // Tab
                _keyStates[0x2E] = (GetAsyncKeyState(0x2E) & 0x8000) != 0; // Delete
                _keyStates[0x24] = (GetAsyncKeyState(0x24) & 0x8000) != 0; // Home
                _keyStates[0x23] = (GetAsyncKeyState(0x23) & 0x8000) != 0; // End
                
                // F9 for debug toggle
                _keyStates[0x78] = (GetAsyncKeyState(0x78) & 0x8000) != 0;
                
                // Character keys (A-Z, 0-9, Space)
                for (int vk = 0x20; vk <= 0x5A; vk++)
                {
                    _keyStates[vk] = (GetAsyncKeyState(vk) & 0x8000) != 0;
                }
                
                // Punctuation keys
                _keyStates[0xBC] = (GetAsyncKeyState(0xBC) & 0x8000) != 0; // comma
                _keyStates[0xBE] = (GetAsyncKeyState(0xBE) & 0x8000) != 0; // period
                _keyStates[0xBF] = (GetAsyncKeyState(0xBF) & 0x8000) != 0; // slash
                _keyStates[0xBA] = (GetAsyncKeyState(0xBA) & 0x8000) != 0; // semicolon
                _keyStates[0xDE] = (GetAsyncKeyState(0xDE) & 0x8000) != 0; // quote
                _keyStates[0xBD] = (GetAsyncKeyState(0xBD) & 0x8000) != 0; // minus
                _keyStates[0xBB] = (GetAsyncKeyState(0xBB) & 0x8000) != 0; // equals
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to update input", ex);
            }
        }

        /// <summary>
        /// Check if key is pressed (current frame)
        /// </summary>
        public bool IsKeyPressed(int vKey)
        {
            if (!_isEnabled)
                return false;
            return _keyStates.TryGetValue(vKey, out var state) && state;
        }

        /// <summary>
        /// Check if key was just pressed (this frame)
        /// </summary>
        public bool IsKeyDown(int vKey)
        {
            if (!_isEnabled)
                return false;
            return _keyStates.TryGetValue(vKey, out var current) && current &&
                   (!_previousKeyStates.TryGetValue(vKey, out var previous) || !previous);
        }

        /// <summary>
        /// Check if mouse button is pressed
        /// </summary>
        public bool IsMousePressed(int button = 0)
        {
            if (!_isEnabled)
                return false;
            int vKey = button == 0 ? VK_LBUTTON : VK_RBUTTON;
            return _keyStates.TryGetValue(vKey, out var state) && state;
        }

        /// <summary>
        /// Check if mouse was just clicked
        /// </summary>
        public bool IsMouseDown(int button = 0)
        {
            if (!_isEnabled)
                return false;
            int vKey = button == 0 ? VK_LBUTTON : VK_RBUTTON;
            return IsKeyDown(vKey);
        }

        /// <summary>
        /// Get mouse position (relative to overlay window)
        /// </summary>
        public (int x, int y) GetMousePosition()
        {
            return (_mousePosition.x, _mousePosition.y);
        }

        /// <summary>
        /// Check if mouse is over rectangle
        /// </summary>
        public bool IsMouseOver(float x, float y, float width, float height)
        {
            if (!_isEnabled)
                return false;
            return _mousePosition.x >= x && _mousePosition.x <= x + width &&
                   _mousePosition.y >= y && _mousePosition.y <= y + height;
        }
    }
}



