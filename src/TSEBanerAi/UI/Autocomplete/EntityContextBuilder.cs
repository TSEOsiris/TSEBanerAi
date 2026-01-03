using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TSEBanerAi.UI.Autocomplete
{
    /// <summary>
    /// Builds context information for LLM from selected entities
    /// </summary>
    public class EntityContextBuilder
    {
        private readonly List<SelectedEntity> _selectedEntities = new List<SelectedEntity>();

        /// <summary>
        /// Currently selected entities
        /// </summary>
        public IReadOnlyList<SelectedEntity> SelectedEntities => _selectedEntities;

        /// <summary>
        /// Add a selected entity
        /// </summary>
        public void AddEntity(SelectedEntity entity)
        {
            // Remove any existing selection at overlapping position
            _selectedEntities.RemoveAll(e => 
                (e.StartIndex <= entity.StartIndex && e.EndIndex >= entity.StartIndex) ||
                (e.StartIndex <= entity.EndIndex && e.EndIndex >= entity.EndIndex));

            _selectedEntities.Add(entity);
            
            // Sort by position
            _selectedEntities.Sort((a, b) => a.StartIndex.CompareTo(b.StartIndex));
        }

        /// <summary>
        /// Remove entity at position
        /// </summary>
        public void RemoveEntityAt(int position)
        {
            _selectedEntities.RemoveAll(e => 
                position >= e.StartIndex && position <= e.EndIndex);
        }

        /// <summary>
        /// Clear all selected entities
        /// </summary>
        public void Clear()
        {
            _selectedEntities.Clear();
        }

        /// <summary>
        /// Update positions after text change
        /// </summary>
        public void UpdatePositions(int changeStart, int oldLength, int newLength)
        {
            int delta = newLength - oldLength;
            
            // Remove entities that were affected by the change
            _selectedEntities.RemoveAll(e => 
                (changeStart >= e.StartIndex && changeStart < e.EndIndex) ||
                (changeStart < e.StartIndex && changeStart + oldLength > e.StartIndex));

            // Update positions of entities after the change
            foreach (var entity in _selectedEntities)
            {
                if (entity.StartIndex >= changeStart)
                {
                    entity.StartIndex += delta;
                    entity.EndIndex += delta;
                }
            }
        }

        /// <summary>
        /// Get entity at cursor position (if any)
        /// </summary>
        public SelectedEntity GetEntityAtPosition(int position)
        {
            return _selectedEntities.FirstOrDefault(e => 
                position >= e.StartIndex && position <= e.EndIndex);
        }

        /// <summary>
        /// Check if there are any selected entities
        /// </summary>
        public bool HasEntities => _selectedEntities.Count > 0;

        /// <summary>
        /// Build context string for LLM
        /// </summary>
        public string BuildLLMContext()
        {
            if (_selectedEntities.Count == 0)
                return "";

            var sb = new StringBuilder();
            sb.AppendLine("[CONTEXT]");
            sb.AppendLine("Referenced game entities (use their Database IDs for detailed information):");

            foreach (var selection in _selectedEntities)
            {
                var entity = selection.Entity;
                string typeStr = GetTypeString(entity.Type);
                
                sb.AppendLine($"- {typeStr}: {entity.Name} (ID: {entity.Id})");
                
                if (!string.IsNullOrEmpty(entity.Description))
                {
                    sb.AppendLine($"  Description: {entity.Description}");
                }
                
                if (!string.IsNullOrEmpty(entity.Context))
                {
                    sb.AppendLine($"  Context: {entity.Context}");
                }
            }

            sb.AppendLine("[/CONTEXT]");
            sb.AppendLine();

            return sb.ToString();
        }

        /// <summary>
        /// Build full message with context for LLM
        /// </summary>
        public string BuildFullMessage(string userMessage)
        {
            string context = BuildLLMContext();
            
            if (string.IsNullOrEmpty(context))
                return userMessage;

            return context + "User message: " + userMessage;
        }

        /// <summary>
        /// Get list of entity IDs for API call
        /// </summary>
        public List<string> GetEntityIds()
        {
            return _selectedEntities.Select(e => e.Entity.Id).ToList();
        }

        private string GetTypeString(EntityType type)
        {
            switch (type)
            {
                case EntityType.Hero: return "Hero";
                case EntityType.Settlement: return "Settlement";
                case EntityType.Kingdom: return "Kingdom";
                case EntityType.Clan: return "Clan";
                default: return "Entity";
            }
        }
    }
}



