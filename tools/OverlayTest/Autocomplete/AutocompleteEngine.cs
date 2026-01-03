using System;
using System.Collections.Generic;
using System.Linq;

namespace OverlayTest.Autocomplete
{
    /// <summary>
    /// Result of autocomplete analysis
    /// </summary>
    public class AutocompleteResult
    {
        /// <summary>
        /// Whether there are suggestions to show
        /// </summary>
        public bool HasSuggestions => Suggestions.Count > 0;

        /// <summary>
        /// List of matching entities
        /// </summary>
        public List<GameEntity> Suggestions { get; set; } = new List<GameEntity>();

        /// <summary>
        /// The word being typed that triggered autocomplete
        /// </summary>
        public string CurrentWord { get; set; } = "";

        /// <summary>
        /// Start position of the current word in input text
        /// </summary>
        public int WordStartIndex { get; set; }

        /// <summary>
        /// End position of the current word in input text
        /// </summary>
        public int WordEndIndex { get; set; }
    }

    /// <summary>
    /// Engine for analyzing input text and providing autocomplete suggestions
    /// </summary>
    public class AutocompleteEngine
    {
        private readonly EntityIndex _entityIndex;
        private const int MinPrefixLength = 3;
        private const int MaxSuggestions = 5;

        public AutocompleteEngine(EntityIndex entityIndex)
        {
            _entityIndex = entityIndex;
        }

        /// <summary>
        /// Analyze input text at cursor position and return suggestions
        /// </summary>
        /// <param name="text">Full input text</param>
        /// <param name="cursorPosition">Current cursor position</param>
        /// <returns>Autocomplete result with suggestions</returns>
        public AutocompleteResult Analyze(string text, int cursorPosition)
        {
            var result = new AutocompleteResult();

            if (string.IsNullOrEmpty(text) || cursorPosition <= 0)
                return result;

            // Find the word at cursor position
            (string word, int startIndex, int endIndex) = ExtractWordAtCursor(text, cursorPosition);

            if (word.Length < MinPrefixLength)
                return result;

            result.CurrentWord = word;
            result.WordStartIndex = startIndex;
            result.WordEndIndex = endIndex;

            // Search for matching entities
            result.Suggestions = _entityIndex.SearchByPrefix(word, MaxSuggestions);

            return result;
        }

        /// <summary>
        /// Extract the word being typed at cursor position
        /// </summary>
        private (string word, int startIndex, int endIndex) ExtractWordAtCursor(string text, int cursorPosition)
        {
            if (cursorPosition > text.Length)
                cursorPosition = text.Length;

            // Find word start (go back until we hit a separator)
            int startIndex = cursorPosition;
            while (startIndex > 0 && IsWordChar(text[startIndex - 1]))
            {
                startIndex--;
            }

            // Find word end (go forward until we hit a separator)
            int endIndex = cursorPosition;
            while (endIndex < text.Length && IsWordChar(text[endIndex]))
            {
                endIndex++;
            }

            if (startIndex >= endIndex)
                return ("", 0, 0);

            string word = text.Substring(startIndex, endIndex - startIndex);
            return (word, startIndex, endIndex);
        }

        /// <summary>
        /// Check if character is part of a word (letter or digit)
        /// </summary>
        private bool IsWordChar(char c)
        {
            // Allow letters (including Cyrillic), digits, and some special chars
            return char.IsLetterOrDigit(c) || c == '-' || c == '\'';
        }

        /// <summary>
        /// Apply selected entity to input text
        /// </summary>
        /// <param name="text">Current input text</param>
        /// <param name="entity">Selected entity</param>
        /// <param name="wordStart">Start of word to replace</param>
        /// <param name="wordEnd">End of word to replace</param>
        /// <returns>New text with entity name inserted</returns>
        public (string newText, int newCursorPosition, SelectedEntity selection) ApplySelection(
            string text, 
            GameEntity entity, 
            int wordStart, 
            int wordEnd)
        {
            string originalWord = text.Substring(wordStart, wordEnd - wordStart);
            
            // Replace the word with entity name
            string before = text.Substring(0, wordStart);
            string after = text.Substring(wordEnd);
            
            // Add space after entity name if there's more text and no space
            string entityName = entity.Name;
            if (after.Length > 0 && after[0] != ' ')
            {
                entityName += " ";
            }
            
            string newText = before + entityName + after;
            
            int newCursor = wordStart + entityName.Length;

            var selection = new SelectedEntity(
                entity, 
                wordStart, 
                wordStart + entity.Name.Length,
                originalWord);

            return (newText, newCursor, selection);
        }
    }
}

