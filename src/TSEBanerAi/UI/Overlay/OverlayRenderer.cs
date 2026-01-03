using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using TSEBanerAi.Utils;

namespace TSEBanerAi.UI.Overlay
{
    /// <summary>
    /// GDI+ based renderer for overlay window with antialiasing and rounded corners
    /// </summary>
    public class OverlayRenderer : IDisposable
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

        private OverlayWindow _window;
        private int _width;
        private int _height;
        private bool _isInitialized;
        private Graphics _graphics;
        private Bitmap _bitmap;
        private Graphics _bitmapGraphics;

        // Theme colors (ARGB format) - matching test app dark theme
        public static readonly Color BackgroundColor = Color.FromArgb(255, 25, 25, 35);
        public static readonly Color HeaderColor = Color.FromArgb(255, 35, 35, 50);
        public static readonly Color InputBgColor = Color.FromArgb(255, 40, 40, 55);
        public static readonly Color PlayerBubbleColor = Color.FromArgb(255, 58, 142, 65);
        public static readonly Color NpcBubbleColor = Color.FromArgb(255, 50, 55, 70);
        public static readonly Color TextColor = Color.White;
        public static readonly Color TextSecondaryColor = Color.FromArgb(255, 180, 180, 180);
        public static readonly Color SendButtonColor = Color.FromArgb(255, 58, 142, 65);

        public OverlayRenderer(OverlayWindow window)
        {
            _window = window;
        }

        /// <summary>
        /// Initialize renderer with dimensions
        /// </summary>
        public bool Initialize(int width, int height)
        {
            if (_isInitialized && _width == width && _height == height)
                return true;

            Cleanup();

            try
            {
                _width = Math.Max(100, width);
                _height = Math.Max(100, height);

                // Create bitmap for double buffering
                _bitmap = new Bitmap(_width, _height);
                _bitmapGraphics = Graphics.FromImage(_bitmap);
                _bitmapGraphics.SmoothingMode = SmoothingMode.AntiAlias;
                _bitmapGraphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                _isInitialized = true;
                ModLogger.LogDebug($"OverlayRenderer initialized: {_width}x{_height}");
                return true;
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to initialize OverlayRenderer", ex);
                return false;
            }
        }

        /// <summary>
        /// Begin rendering frame
        /// </summary>
        public void BeginFrame()
        {
            if (!_isInitialized || _bitmapGraphics == null)
                return;

            // Clear with background color
            _bitmapGraphics.Clear(BackgroundColor);
        }

        /// <summary>
        /// End rendering frame and copy to window
        /// </summary>
        public void EndFrame()
        {
            if (!_isInitialized || _bitmap == null)
                return;

            try
            {
                IntPtr hwnd = _window.Handle;
                if (hwnd == IntPtr.Zero)
                    return;

                IntPtr hdc = GetDC(hwnd);
                if (hdc == IntPtr.Zero)
                    return;

                try
                {
                    using (var g = Graphics.FromHdc(hdc))
                    {
                        g.DrawImage(_bitmap, 0, 0);
                    }
                }
                finally
                {
                    ReleaseDC(hwnd, hdc);
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed in EndFrame", ex);
            }
        }

        /// <summary>
        /// Get graphics object for custom drawing
        /// </summary>
        public Graphics Graphics => _bitmapGraphics;

        /// <summary>
        /// Draw filled rectangle with optional rounded corners
        /// </summary>
        public void FillRectangle(float x, float y, float width, float height, Color color, float cornerRadius = 0)
        {
            if (_bitmapGraphics == null) return;

            try
            {
                using (var brush = new SolidBrush(color))
                {
                    if (cornerRadius > 0)
                    {
                        using (var path = CreateRoundedRect(new RectangleF(x, y, width, height), cornerRadius))
                        {
                            _bitmapGraphics.FillPath(brush, path);
                        }
                    }
                    else
                    {
                        _bitmapGraphics.FillRectangle(brush, x, y, width, height);
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to fill rectangle", ex);
            }
        }

        /// <summary>
        /// Draw text
        /// </summary>
        public void DrawText(string text, float x, float y, Color color, float fontSize = 11f, bool bold = false)
        {
            if (_bitmapGraphics == null || string.IsNullOrEmpty(text)) return;

            try
            {
                var style = bold ? FontStyle.Bold : FontStyle.Regular;
                using (var font = new Font("Segoe UI", fontSize, style))
                using (var brush = new SolidBrush(color))
                {
                    _bitmapGraphics.DrawString(text, font, brush, x, y);
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to draw text", ex);
            }
        }

        /// <summary>
        /// Draw text with word wrap
        /// </summary>
        public void DrawTextWrapped(string text, RectangleF rect, Color color, float fontSize = 11f)
        {
            if (_bitmapGraphics == null || string.IsNullOrEmpty(text)) return;

            try
            {
                using (var font = new Font("Segoe UI", fontSize))
                using (var brush = new SolidBrush(color))
                {
                    _bitmapGraphics.DrawString(text, font, brush, rect);
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to draw wrapped text", ex);
            }
        }

        /// <summary>
        /// Measure text size
        /// </summary>
        public SizeF MeasureText(string text, float fontSize = 11f, float maxWidth = 0)
        {
            if (_bitmapGraphics == null || string.IsNullOrEmpty(text))
                return SizeF.Empty;

            try
            {
                using (var font = new Font("Segoe UI", fontSize))
                {
                    if (maxWidth > 0)
                    {
                        return _bitmapGraphics.MeasureString(text, font, (int)maxWidth);
                    }
                    return _bitmapGraphics.MeasureString(text, font);
                }
            }
            catch
            {
                return new SizeF(text.Length * fontSize * 0.6f, fontSize * 1.5f);
            }
        }

        /// <summary>
        /// Draw a line
        /// </summary>
        public void DrawLine(float x1, float y1, float x2, float y2, Color color, float width = 2f)
        {
            if (_bitmapGraphics == null) return;

            try
            {
                using (var pen = new Pen(color, width))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    _bitmapGraphics.DrawLine(pen, x1, y1, x2, y2);
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to draw line", ex);
            }
        }

        /// <summary>
        /// Draw an arrow (for send button)
        /// </summary>
        public void DrawArrow(float centerX, float centerY, Color color, float size = 8f)
        {
            if (_bitmapGraphics == null) return;

            try
            {
                using (var pen = new Pen(color, 2.5f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    // Horizontal line
                    _bitmapGraphics.DrawLine(pen, centerX - size, centerY, centerX + size * 0.75f, centerY);
                    // Top arrow line
                    _bitmapGraphics.DrawLine(pen, centerX + size * 0.125f, centerY - size * 0.75f, centerX + size * 0.875f, centerY);
                    // Bottom arrow line
                    _bitmapGraphics.DrawLine(pen, centerX + size * 0.125f, centerY + size * 0.75f, centerX + size * 0.875f, centerY);
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to draw arrow", ex);
            }
        }

        /// <summary>
        /// Set clipping region
        /// </summary>
        public void SetClip(RectangleF rect)
        {
            _bitmapGraphics?.SetClip(rect);
        }

        /// <summary>
        /// Reset clipping region
        /// </summary>
        public void ResetClip()
        {
            _bitmapGraphics?.ResetClip();
        }

        /// <summary>
        /// Create rounded rectangle path
        /// </summary>
        public static GraphicsPath CreateRoundedRect(RectangleF rect, float radius)
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

        private void Cleanup()
        {
            try
            {
                _bitmapGraphics?.Dispose();
                _bitmapGraphics = null;
                _bitmap?.Dispose();
                _bitmap = null;
                _isInitialized = false;
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Error cleaning up OverlayRenderer", ex);
            }
        }

        public void Dispose()
        {
            Cleanup();
        }
    }
}
