using System;
using System.IO;
using Newtonsoft.Json;
using TSEBanerAi.Utils;

namespace TSEBanerAi.Settings
{
    /// <summary>
    /// Chat window settings that persist between sessions
    /// </summary>
    public class ChatSettings
    {
        /// <summary>
        /// Window X position (-1 = use default)
        /// </summary>
        public int WindowX { get; set; } = -1;
        
        /// <summary>
        /// Window Y position (-1 = use default)
        /// </summary>
        public int WindowY { get; set; } = -1;
        
        /// <summary>
        /// Window width
        /// </summary>
        public int WindowWidth { get; set; } = 400;
        
        /// <summary>
        /// Window height
        /// </summary>
        public int WindowHeight { get; set; } = 500;
        
        /// <summary>
        /// Selected theme index
        /// </summary>
        public int ThemeIndex { get; set; } = 0;
        
        /// <summary>
        /// Is debug mode enabled
        /// </summary>
        public bool IsDebugMode { get; set; } = false;

        // Settings file name
        private const string SettingsFileName = "chat_settings.json";
        
        /// <summary>
        /// Get full path to settings file (uses ModPaths)
        /// </summary>
        private static string GetSettingsPath()
        {
            return ModPaths.GetSettingsFilePath(SettingsFileName);
        }
        
        /// <summary>
        /// Load settings from file or return defaults
        /// </summary>
        public static ChatSettings Load()
        {
            try
            {
                string settingsPath = GetSettingsPath();
                
                if (File.Exists(settingsPath))
                {
                    string json = File.ReadAllText(settingsPath);
                    var settings = JsonConvert.DeserializeObject<ChatSettings>(json);
                    
                    if (settings != null)
                    {
                        ModLogger.LogDebug($"Chat settings loaded from {settingsPath}");
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to load chat settings", ex);
            }
            
            ModLogger.LogDebug("Using default chat settings");
            return new ChatSettings();
        }
        
        /// <summary>
        /// Save settings to file
        /// </summary>
        public void Save()
        {
            try
            {
                string settingsPath = GetSettingsPath();
                
                // Ensure directory exists
                string directory = Path.GetDirectoryName(settingsPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                string json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(settingsPath, json);
                
                ModLogger.LogDebug($"Chat settings saved to {settingsPath}");
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to save chat settings", ex);
            }
        }
        
        /// <summary>
        /// Update position
        /// </summary>
        public void UpdatePosition(int x, int y)
        {
            WindowX = x;
            WindowY = y;
        }
        
        /// <summary>
        /// Update size
        /// </summary>
        public void UpdateSize(int width, int height)
        {
            WindowWidth = width;
            WindowHeight = height;
        }
    }
}



