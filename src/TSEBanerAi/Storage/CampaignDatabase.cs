using System;
using System.Data.SQLite;
using System.IO;
using TSEBanerAi.Utils;

namespace TSEBanerAi.Storage
{
    /// <summary>
    /// Main database manager for campaign data
    /// </summary>
    public class CampaignDatabase : IDisposable
    {
        private SQLiteConnection _connection;
        private readonly string _campaignId;
        private readonly string _databasePath;
        private bool _disposed;

        /// <summary>
        /// Current campaign ID
        /// </summary>
        public string CampaignId => _campaignId;

        /// <summary>
        /// Whether database is connected
        /// </summary>
        public bool IsConnected => _connection != null && _connection.State == System.Data.ConnectionState.Open;

        /// <summary>
        /// Create database manager for campaign
        /// </summary>
        public CampaignDatabase(string campaignId)
        {
            _campaignId = campaignId ?? throw new ArgumentNullException(nameof(campaignId));
            _databasePath = CampaignIdGenerator.GetDatabasePath(campaignId);
        }

        /// <summary>
        /// Initialize database connection and create schema if needed
        /// </summary>
        public bool Initialize()
        {
            try
            {
                ModLogger.LogDebug($"Initializing database for campaign: {_campaignId}");
                ModLogger.LogDebug($"Database path: {_databasePath}");

                // Ensure directory exists
                string directory = Path.GetDirectoryName(_databasePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                bool isNewDatabase = !File.Exists(_databasePath);

                // Create connection string
                var connectionString = new SQLiteConnectionStringBuilder
                {
                    DataSource = _databasePath,
                    Version = 3,
                    ForeignKeys = true,
                    JournalMode = SQLiteJournalModeEnum.Wal,
                    SyncMode = SynchronizationModes.Normal
                }.ToString();

                _connection = new SQLiteConnection(connectionString);
                _connection.Open();

                if (isNewDatabase)
                {
                    CreateSchema();
                }
                else
                {
                    ValidateSchema();
                }

                ModLogger.LogDebug($"Database initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to initialize database", ex);
                return false;
            }
        }

        /// <summary>
        /// Create all database tables
        /// </summary>
        private void CreateSchema()
        {
            ModLogger.LogDebug("Creating database schema...");

            using (var transaction = _connection.BeginTransaction())
            {
                try
                {
                    foreach (var sql in DatabaseSchema.AllTables)
                    {
                        using (var cmd = new SQLiteCommand(sql, _connection, transaction))
                        {
                            cmd.ExecuteNonQuery();
                        }
                    }

                    // Insert schema version
                    string insertVersion = @"
                        INSERT OR REPLACE INTO schema_version (version, applied_at) 
                        VALUES (@version, @applied_at)";

                    using (var cmd = new SQLiteCommand(insertVersion, _connection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@version", DatabaseSchema.Version);
                        cmd.Parameters.AddWithValue("@applied_at", DateTime.UtcNow.ToString("O"));
                        cmd.ExecuteNonQuery();
                    }

                    transaction.Commit();
                    ModLogger.LogDebug($"Schema created successfully (version {DatabaseSchema.Version})");
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        /// <summary>
        /// Validate existing schema and run migrations if needed
        /// </summary>
        private void ValidateSchema()
        {
            try
            {
                string query = "SELECT version FROM schema_version ORDER BY applied_at DESC LIMIT 1";
                using (var cmd = new SQLiteCommand(query, _connection))
                {
                    var result = cmd.ExecuteScalar();
                    string currentVersion = result?.ToString() ?? "0.0.0";
                    
                    ModLogger.LogDebug($"Database schema version: {currentVersion}");

                    if (currentVersion != DatabaseSchema.Version)
                    {
                        ModLogger.LogDebug($"Schema migration needed: {currentVersion} -> {DatabaseSchema.Version}");
                        // TODO: Implement migrations when needed
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to validate schema", ex);
                // Schema might be corrupted, try recreating
                CreateSchema();
            }
        }

        /// <summary>
        /// Execute non-query SQL command
        /// </summary>
        public int ExecuteNonQuery(string sql, params SQLiteParameter[] parameters)
        {
            using (var cmd = new SQLiteCommand(sql, _connection))
            {
                if (parameters != null)
                {
                    cmd.Parameters.AddRange(parameters);
                }
                return cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Execute scalar SQL command
        /// </summary>
        public object ExecuteScalar(string sql, params SQLiteParameter[] parameters)
        {
            using (var cmd = new SQLiteCommand(sql, _connection))
            {
                if (parameters != null)
                {
                    cmd.Parameters.AddRange(parameters);
                }
                return cmd.ExecuteScalar();
            }
        }

        /// <summary>
        /// Execute query and return reader
        /// </summary>
        public SQLiteDataReader ExecuteReader(string sql, params SQLiteParameter[] parameters)
        {
            var cmd = new SQLiteCommand(sql, _connection);
            if (parameters != null)
            {
                cmd.Parameters.AddRange(parameters);
            }
            return cmd.ExecuteReader();
        }

        /// <summary>
        /// Create a new command with connection
        /// </summary>
        public SQLiteCommand CreateCommand()
        {
            return new SQLiteCommand(_connection);
        }

        /// <summary>
        /// Begin a transaction
        /// </summary>
        public SQLiteTransaction BeginTransaction()
        {
            return _connection.BeginTransaction();
        }

        /// <summary>
        /// Get last inserted row ID
        /// </summary>
        public long GetLastInsertId()
        {
            return _connection.LastInsertRowId;
        }

        /// <summary>
        /// Dispose database connection
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _connection?.Close();
                _connection?.Dispose();
                _disposed = true;
            }
        }
    }
}

