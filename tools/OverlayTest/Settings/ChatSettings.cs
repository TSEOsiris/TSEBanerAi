using System;
using System.IO;
using System.Text.Json;

namespace OverlayTest.Settings
{
    /// <summary>
    /// Chat window settings that persist between sessions
    /// </summary>
    public class ChatSettings
    {
        public int WindowX { get; set; } = -1; // -1 means use default
        public int WindowY { get; set; } = -1;
        public int WindowWidth { get; set; } = 400;
        public int WindowHeight { get; set; } = 500;
        public int ThemeIndex { get; set; } = 0;
        public bool IsDebugMode { get; set; } = false;
        
        // File path for settings
        private static string SettingsFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TSEBanerAi");
        
        private static string SettingsPath => Path.Combine(SettingsFolder, "chat_settings.json");
        
        /// <summary>
        /// Load settings from file or return defaults
        /// </summary>
        public static ChatSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    var settings = JsonSerializer.Deserialize<ChatSettings>(json);
                    if (settings != null)
                    {
                        Console.WriteLine($"Settings loaded from {SettingsPath}");
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load settings: {ex.Message}");
            }
            
            Console.WriteLine("Using default settings");
            return new ChatSettings();
        }
        
        /// <summary>
        /// Save settings to file
        /// </summary>
        public void Save()
        {
            try
            {
                // Ensure directory exists
                if (!Directory.Exists(SettingsFolder))
                {
                    Directory.CreateDirectory(SettingsFolder);
                }
                
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(SettingsPath, json);
                
                Console.WriteLine($"Settings saved to {SettingsPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save settings: {ex.Message}");
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

