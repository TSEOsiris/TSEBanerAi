using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace OverlayTest.Autocomplete
{
    /// <summary>
    /// Visual dropdown for autocomplete suggestions
    /// </summary>
    public class SuggestionDropdown
    {
        // Layout
        private const int ItemHeight = 40;
        private const int ItemPadding = 8;
        private const int IconSize = 24;
        private const int MaxVisibleItems = 5;
        private const int CornerRadius = 8;
        private const int DropdownPadding = 4;

        // State
        private List<GameEntity> _suggestions = new List<GameEntity>();
        private int _selectedIndex = 0;
        private Rectangle _bounds;
        private bool _isVisible = false;

        // Colors
        private Color _backgroundColor = Color.FromArgb(240, 45, 45, 55);
        private Color _itemHoverColor = Color.FromArgb(255, 60, 60, 75);
        private Color _textColor = Color.White;
        private Color _descriptionColor = Color.FromArgb(180, 180, 180);

        // Entity type colors (for icons/highlights)
        private static readonly Dictionary<EntityType, Color> TypeColors = new Dictionary<EntityType, Color>
        {
            { EntityType.Hero, Color.FromArgb(255, 100, 150, 255) },      // Blue
            { EntityType.Settlement, Color.FromArgb(255, 100, 200, 100) }, // Green
            { EntityType.Kingdom, Color.FromArgb(255, 255, 200, 100) },   // Gold
            { EntityType.Clan, Color.FromArgb(255, 180, 100, 255) }       // Purple
        };

        // Entity type icons (simple symbols)
        private static readonly Dictionary<EntityType, string> TypeIcons = new Dictionary<EntityType, string>
        {
            { EntityType.Hero, "H" },       // Person icon substitute
            { EntityType.Settlement, "S" }, // Building icon substitute
            { EntityType.Kingdom, "K" },    // Crown icon substitute
            { EntityType.Clan, "C" }        // Shield icon substitute
        };

        public bool IsVisible => _isVisible;
        public int SelectedIndex => _selectedIndex;
        public int SuggestionCount => _suggestions.Count;
        public Rectangle Bounds => _bounds;

        /// <summary>
        /// Get currently selected entity
        /// </summary>
        public GameEntity? SelectedEntity => 
            _selectedIndex >= 0 && _selectedIndex < _suggestions.Count 
                ? _suggestions[_selectedIndex] 
                : null;

        /// <summary>
        /// Show dropdown with suggestions at specified position
        /// </summary>
        /// <param name="suggestions">List of suggestions to show</param>
        /// <param name="anchorX">X position (left edge)</param>
        /// <param name="anchorY">Y position (bottom edge - dropdown appears above)</param>
        /// <param name="maxWidth">Maximum width of dropdown</param>
        public void Show(List<GameEntity> suggestions, int anchorX, int anchorY, int maxWidth)
        {
            if (suggestions == null || suggestions.Count == 0)
            {
                Hide();
                return;
            }

            _suggestions = suggestions;
            _selectedIndex = 0;

            // Calculate dropdown size
            int itemCount = Math.Min(suggestions.Count, MaxVisibleItems);
            int height = itemCount * ItemHeight + DropdownPadding * 2;
            int width = Math.Min(maxWidth - 20, 350);

            // Position above anchor point
            _bounds = new Rectangle(
                anchorX,
                anchorY - height,
                width,
                height);

            _isVisible = true;
        }

        /// <summary>
        /// Hide dropdown
        /// </summary>
        public void Hide()
        {
            _isVisible = false;
            _suggestions.Clear();
            _selectedIndex = 0;
        }

        /// <summary>
        /// Move selection up
        /// </summary>
        public void SelectPrevious()
        {
            if (_suggestions.Count == 0) return;
            _selectedIndex = (_selectedIndex - 1 + _suggestions.Count) % _suggestions.Count;
        }

        /// <summary>
        /// Move selection down
        /// </summary>
        public void SelectNext()
        {
            if (_suggestions.Count == 0) return;
            _selectedIndex = (_selectedIndex + 1) % _suggestions.Count;
        }

        /// <summary>
        /// Select item at mouse position
        /// </summary>
        /// <returns>True if an item was clicked</returns>
        public bool HandleMouseClick(Point mousePos)
        {
            if (!_isVisible || !_bounds.Contains(mousePos))
                return false;

            int relativeY = mousePos.Y - _bounds.Y - DropdownPadding;
            int clickedIndex = relativeY / ItemHeight;

            if (clickedIndex >= 0 && clickedIndex < _suggestions.Count)
            {
                _selectedIndex = clickedIndex;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Check if mouse is over dropdown
        /// </summary>
        public bool ContainsPoint(Point point)
        {
            return _isVisible && _bounds.Contains(point);
        }

        /// <summary>
        /// Update hover state based on mouse position
        /// </summary>
        public void UpdateHover(Point mousePos)
        {
            if (!_isVisible || !_bounds.Contains(mousePos))
                return;

            int relativeY = mousePos.Y - _bounds.Y - DropdownPadding;
            int hoverIndex = relativeY / ItemHeight;

            if (hoverIndex >= 0 && hoverIndex < _suggestions.Count)
            {
                _selectedIndex = hoverIndex;
            }
        }

        /// <summary>
        /// Draw the dropdown
        /// </summary>
        public void Draw(Graphics g)
        {
            if (!_isVisible || _suggestions.Count == 0)
                return;

            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Draw background with rounded corners
            using (GraphicsPath path = CreateRoundedRectPath(_bounds, CornerRadius))
            using (SolidBrush bgBrush = new SolidBrush(_backgroundColor))
            {
                g.FillPath(bgBrush, path);
            }

            // Draw shadow effect (simple border)
            using (GraphicsPath path = CreateRoundedRectPath(_bounds, CornerRadius))
            using (Pen shadowPen = new Pen(Color.FromArgb(80, 0, 0, 0), 2))
            {
                g.DrawPath(shadowPen, path);
            }

            // Draw items
            int y = _bounds.Y + DropdownPadding;
            for (int i = 0; i < Math.Min(_suggestions.Count, MaxVisibleItems); i++)
            {
                DrawItem(g, _suggestions[i], i, y, i == _selectedIndex);
                y += ItemHeight;
            }
        }

        private void DrawItem(Graphics g, GameEntity entity, int index, int y, bool isSelected)
        {
            Rectangle itemRect = new Rectangle(
                _bounds.X + DropdownPadding,
                y,
                _bounds.Width - DropdownPadding * 2,
                ItemHeight);

            // Draw selection highlight
            if (isSelected)
            {
                using (GraphicsPath path = CreateRoundedRectPath(itemRect, 6))
                using (SolidBrush hoverBrush = new SolidBrush(_itemHoverColor))
                {
                    g.FillPath(hoverBrush, path);
                }
            }

            // Get type color
            Color typeColor = TypeColors.TryGetValue(entity.Type, out var c) ? c : Color.Gray;

            // Draw type icon (circle with letter)
            int iconX = itemRect.X + ItemPadding;
            int iconY = itemRect.Y + (ItemHeight - IconSize) / 2;
            Rectangle iconRect = new Rectangle(iconX, iconY, IconSize, IconSize);

            using (SolidBrush iconBgBrush = new SolidBrush(typeColor))
            {
                g.FillEllipse(iconBgBrush, iconRect);
            }

            // Draw icon letter
            string iconText = TypeIcons.TryGetValue(entity.Type, out var icon) ? icon : "?";
            using (Font iconFont = new Font("Arial", 10, FontStyle.Bold))
            using (SolidBrush iconTextBrush = new SolidBrush(Color.White))
            {
                SizeF textSize = g.MeasureString(iconText, iconFont);
                float textX = iconRect.X + (IconSize - textSize.Width) / 2;
                float textY = iconRect.Y + (IconSize - textSize.Height) / 2;
                g.DrawString(iconText, iconFont, iconTextBrush, textX, textY);
            }

            // Draw entity name
            int textX2 = iconX + IconSize + ItemPadding;
            int textWidth = itemRect.Right - textX2 - ItemPadding;

            using (Font nameFont = new Font("Arial", 11, FontStyle.Bold))
            using (SolidBrush nameBrush = new SolidBrush(_textColor))
            {
                Rectangle nameRect = new Rectangle(textX2, itemRect.Y + 4, textWidth, 20);
                g.DrawString(entity.Name, nameFont, nameBrush, nameRect, 
                    new StringFormat { Trimming = StringTrimming.EllipsisCharacter });
            }

            // Draw description
            using (Font descFont = new Font("Arial", 9))
            using (SolidBrush descBrush = new SolidBrush(_descriptionColor))
            {
                Rectangle descRect = new Rectangle(textX2, itemRect.Y + 22, textWidth, 16);
                g.DrawString(entity.Description, descFont, descBrush, descRect,
                    new StringFormat { Trimming = StringTrimming.EllipsisCharacter });
            }
        }

        private GraphicsPath CreateRoundedRectPath(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = radius * 2;

            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();

            return path;
        }

        /// <summary>
        /// Get color for entity type (for external use in highlighting)
        /// </summary>
        public static Color GetTypeColor(EntityType type)
        {
            return TypeColors.TryGetValue(type, out var c) ? c : Color.Gray;
        }
    }
}



