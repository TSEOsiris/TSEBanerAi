using System;
using System.IO;

namespace TSEBanerAi.Utils
{
    public static class ModLogger
    {
        private static string _logFilePath = string.Empty;
        private static readonly object _lock = new object();
        private static bool _initialized = false;

        /// <summary>
        /// Initialize logger - can be called explicitly or will auto-init on first log
        /// </summary>
        public static void Initialize()
        {
            if (_initialized)
                return;

            try
            {
                // Use ModPaths for log directory
                var logsPath = ModPaths.LogsPath;
                Directory.CreateDirectory(logsPath);
                _logFilePath = Path.Combine(logsPath, $"TSEBanerAi_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");
                _initialized = true;
                
                LogDebug("=== TSEBanerAi Log Started ===");
                LogDebug($"Timestamp: {DateTime.Now}");
                LogDebug($"Module path: {ModPaths.ModulePath}");
                LogDebug($"Log file: {_logFilePath}");
                LogDebug("");
            }
            catch
            {
                // Fallback to temp folder
                try
                {
                    _logFilePath = Path.Combine(Path.GetTempPath(), $"TSEBanerAi_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");
                    _initialized = true;
                }
                catch
                {
                    // Ignore
                }
            }
        }

        private static void EnsureInitialized()
        {
            if (!_initialized)
            {
                Initialize();
            }
        }

        public static void LogDebug(string message)
        {
            try
            {
                EnsureInitialized();
                lock (_lock)
                {
                    var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [DEBUG] {message}";
                    File.AppendAllText(_logFilePath, logMessage + Environment.NewLine);
                }
            }
            catch
            {
                // Ignore
            }
        }

        public static void LogError(string message)
        {
            try
            {
                EnsureInitialized();
                lock (_lock)
                {
                    var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ERROR] {message}";
                    File.AppendAllText(_logFilePath, logMessage + Environment.NewLine);
                }
            }
            catch
            {
                // Ignore
            }
        }

        public static void LogException(string message, Exception ex)
        {
            try
            {
                EnsureInitialized();
                lock (_lock)
                {
                    var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [EXCEPTION] {message}: {ex}";
                    File.AppendAllText(_logFilePath, logMessage + Environment.NewLine);
                }
            }
            catch
            {
                // Ignore
            }
        }

        public static void LogWarning(string message)
        {
            try
            {
                EnsureInitialized();
                lock (_lock)
                {
                    var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [WARNING] {message}";
                    File.AppendAllText(_logFilePath, logMessage + Environment.NewLine);
                }
            }
            catch
            {
                // Ignore
            }
        }
    }
}



