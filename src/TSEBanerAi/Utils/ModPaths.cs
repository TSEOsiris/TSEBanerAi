using System;
using System.IO;
using System.Reflection;
using TaleWorlds.Library;
using TaleWorlds.ModuleManager;

namespace TSEBanerAi.Utils
{
    /// <summary>
    /// Provides paths for mod data storage
    /// Automatically detects module folder location (Modules or Workshop)
    /// </summary>
    public static class ModPaths
    {
        private static string _modulePath = null;
        private static bool _initialized = false;

        /// <summary>
        /// Module ID used for path resolution
        /// </summary>
        public const string ModuleId = "TSEBanerAi";

        /// <summary>
        /// Root path to the module folder
        /// </summary>
        public static string ModulePath
        {
            get
            {
                if (!_initialized)
                {
                    Initialize();
                }
                return _modulePath;
            }
        }

        /// <summary>
        /// Path to Data folder (for database, settings, etc.)
        /// </summary>
        public static string DataPath => Path.Combine(ModulePath, "Data");

        /// <summary>
        /// Path to Database folder
        /// </summary>
        public static string DatabasePath => Path.Combine(DataPath, "Database");

        /// <summary>
        /// Path to Settings folder
        /// </summary>
        public static string SettingsPath => Path.Combine(DataPath, "Settings");

        /// <summary>
        /// Path to Logs folder
        /// </summary>
        public static string LogsPath => Path.Combine(ModulePath, "Logs");

        /// <summary>
        /// Initialize paths - called automatically on first access
        /// </summary>
        public static void Initialize()
        {
            if (_initialized)
                return;

            _modulePath = FindModulePath();
            _initialized = true;

            // Ensure directories exist
            EnsureDirectoriesExist();
        }

        /// <summary>
        /// Find module path using multiple methods
        /// </summary>
        private static string FindModulePath()
        {
            string path = null;

            // Method 1: Try ModuleHelper (Bannerlord API)
            try
            {
                path = ModuleHelper.GetModuleFullPath(ModuleId);
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                {
                    return path;
                }
            }
            catch (Exception)
            {
                // ModuleHelper may not be available yet
            }

            // Method 2: Try to find via BasePath + Modules
            try
            {
                string basePath = BasePath.Name;
                if (!string.IsNullOrEmpty(basePath))
                {
                    path = Path.Combine(basePath, "Modules", ModuleId);
                    if (Directory.Exists(path))
                    {
                        return path;
                    }
                }
            }
            catch (Exception)
            {
                // BasePath may not be initialized
            }

            // Method 3: Find via Assembly location
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                string assemblyPath = assembly.Location;
                
                if (!string.IsNullOrEmpty(assemblyPath))
                {
                    // Assembly is in: ModulePath/bin/Win64_Shipping_Client/TSEBanerAi.dll
                    // Go up 3 levels to get module root
                    var dir = new DirectoryInfo(Path.GetDirectoryName(assemblyPath));
                    
                    // Go up from Win64_Shipping_Client -> bin -> ModulePath
                    if (dir.Parent != null && dir.Parent.Parent != null)
                    {
                        path = dir.Parent.Parent.FullName;
                        if (Directory.Exists(path) && 
                            (File.Exists(Path.Combine(path, "SubModule.xml")) ||
                             dir.Parent.Parent.Name == ModuleId))
                        {
                            return path;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Assembly location may not be available
            }

            // Method 4: Fallback to Documents folder (legacy support)
            try
            {
                path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Mount and Blade II Bannerlord",
                    "Configs",
                    ModuleId
                );
                Directory.CreateDirectory(path);
                return path;
            }
            catch (Exception)
            {
                // Last resort: use temp folder
                return Path.Combine(Path.GetTempPath(), ModuleId);
            }
        }

        /// <summary>
        /// Ensure all required directories exist
        /// </summary>
        private static void EnsureDirectoriesExist()
        {
            try
            {
                Directory.CreateDirectory(DataPath);
                Directory.CreateDirectory(DatabasePath);
                Directory.CreateDirectory(SettingsPath);
                Directory.CreateDirectory(LogsPath);
            }
            catch (Exception ex)
            {
                // Log to console if directories can't be created
                Console.WriteLine($"[TSEBanerAi] Failed to create directories: {ex.Message}");
            }
        }

        /// <summary>
        /// Get full path to a file in the Data folder
        /// </summary>
        public static string GetDataFilePath(string fileName)
        {
            return Path.Combine(DataPath, fileName);
        }

        /// <summary>
        /// Get full path to a file in the Database folder
        /// </summary>
        public static string GetDatabaseFilePath(string fileName)
        {
            return Path.Combine(DatabasePath, fileName);
        }

        /// <summary>
        /// Get full path to a file in the Settings folder
        /// </summary>
        public static string GetSettingsFilePath(string fileName)
        {
            return Path.Combine(SettingsPath, fileName);
        }

        /// <summary>
        /// Get full path to a log file
        /// </summary>
        public static string GetLogFilePath(string fileName)
        {
            return Path.Combine(LogsPath, fileName);
        }
    }
}



