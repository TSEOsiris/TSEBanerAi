using System;

namespace OverlayTest.Autocomplete
{
    /// <summary>
    /// Type of game entity for categorization and display
    /// </summary>
    public enum EntityType
    {
        Hero,       // Lords, companions, notable characters
        Settlement, // Towns, castles, villages
        Kingdom,    // Factions/kingdoms
        Clan        // Noble clans
    }

    /// <summary>
    /// Represents a game entity that can be referenced in chat
    /// </summary>
    public class GameEntity
    {
        /// <summary>
        /// Unique identifier from game data (e.g., "lord_1_1", "town_V2")
        /// </summary>
        public string Id { get; set; } = "";

        /// <summary>
        /// Display name (e.g., "Люкон", "Правенд")
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Type of entity for categorization
        /// </summary>
        public EntityType Type { get; set; }

        /// <summary>
        /// Short description for tooltip (e.g., "King of Vlandia")
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// Additional context info (culture, kingdom, etc.)
        /// </summary>
        public string Context { get; set; } = "";

        /// <summary>
        /// Lowercase name for fast prefix matching
        /// </summary>
        public string NameLower { get; private set; } = "";

        /// <summary>
        /// Set name and update lowercase version
        /// </summary>
        public void SetName(string name)
        {
            Name = name;
            NameLower = name.ToLowerInvariant();
        }

        public override string ToString()
        {
            return $"[{Type}] {Name} ({Id})";
        }
    }

    /// <summary>
    /// Represents an entity that was selected/highlighted by user
    /// </summary>
    public class SelectedEntity
    {
        /// <summary>
        /// The game entity that was selected
        /// </summary>
        public GameEntity Entity { get; set; }

        /// <summary>
        /// Start position in the input text
        /// </summary>
        public int StartIndex { get; set; }

        /// <summary>
        /// End position in the input text
        /// </summary>
        public int EndIndex { get; set; }

        /// <summary>
        /// The original text that was replaced
        /// </summary>
        public string OriginalText { get; set; } = "";

        public SelectedEntity(GameEntity entity, int start, int end, string originalText)
        {
            Entity = entity;
            StartIndex = start;
            EndIndex = end;
            OriginalText = originalText;
        }
    }
}



