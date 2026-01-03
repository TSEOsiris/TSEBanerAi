using System;
using TaleWorlds.CampaignSystem;
using TSEBanerAi.Storage.Models;
using TSEBanerAi.Storage.Repositories;
using TSEBanerAi.Utils;

namespace TSEBanerAi.Storage
{
    /// <summary>
    /// Main storage manager - singleton that manages campaign database and repositories
    /// </summary>
    public class StorageManager : IDisposable
    {
        private static StorageManager _instance;
        private static readonly object _lock = new object();

        private CampaignDatabase _database;
        private CampaignRepository _campaignRepo;
        private ChatRepository _chatRepo;
        private NpcRepository _npcRepo;
        private EventRepository _eventRepo;

        private string _currentCampaignId;
        private bool _disposed;

        /// <summary>
        /// Singleton instance
        /// </summary>
        public static StorageManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new StorageManager();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Current campaign ID
        /// </summary>
        public string CurrentCampaignId => _currentCampaignId;

        /// <summary>
        /// Whether storage is initialized and connected
        /// </summary>
        public bool IsInitialized => _database != null && _database.IsConnected;

        /// <summary>
        /// Campaign repository
        /// </summary>
        public CampaignRepository Campaigns => _campaignRepo;

        /// <summary>
        /// Chat message repository
        /// </summary>
        public ChatRepository Chat => _chatRepo;

        /// <summary>
        /// NPC repository (snapshots and memory)
        /// </summary>
        public NpcRepository Npcs => _npcRepo;

        /// <summary>
        /// Event repository
        /// </summary>
        public EventRepository Events => _eventRepo;

        private StorageManager()
        {
        }

        /// <summary>
        /// Initialize storage for current campaign
        /// Call this when campaign is loaded
        /// </summary>
        public bool Initialize()
        {
            try
            {
                ModLogger.LogDebug("=== StorageManager.Initialize START ===");

                // Generate campaign ID
                string campaignId = CampaignIdGenerator.GenerateCampaignId();
                if (string.IsNullOrEmpty(campaignId))
                {
                    ModLogger.LogError("Failed to generate campaign ID");
                    return false;
                }

                // If already initialized with same campaign, just return
                if (_currentCampaignId == campaignId && IsInitialized)
                {
                    ModLogger.LogDebug("Storage already initialized for this campaign");
                    return true;
                }

                // Close existing connection if different campaign
                if (_database != null)
                {
                    ModLogger.LogDebug("Closing previous database connection");
                    _database.Dispose();
                }

                _currentCampaignId = campaignId;
                ModLogger.LogDebug($"Campaign ID: {campaignId}");

                // Create and initialize database
                _database = new CampaignDatabase(campaignId);
                if (!_database.Initialize())
                {
                    ModLogger.LogError("Failed to initialize database");
                    return false;
                }

                // Create repositories
                _campaignRepo = new CampaignRepository(_database);
                _chatRepo = new ChatRepository(_database);
                _npcRepo = new NpcRepository(_database);
                _eventRepo = new EventRepository(_database);

                // Save or update campaign info
                SaveCurrentCampaignInfo();

                ModLogger.LogDebug("=== StorageManager.Initialize END ===");
                return true;
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to initialize StorageManager", ex);
                return false;
            }
        }

        /// <summary>
        /// Save current campaign info to database
        /// </summary>
        private void SaveCurrentCampaignInfo()
        {
            try
            {
                if (Campaign.Current == null) return;

                var hero = Campaign.Current.MainParty?.LeaderHero;
                if (hero == null) return;

                // Use CampaignTime.Now instead of CampaignStartTime (which doesn't exist)
                var now = CampaignTime.Now;

                var existing = _campaignRepo.GetCampaignInfo(_currentCampaignId);
                
                var info = new CampaignInfo
                {
                    CampaignId = _currentCampaignId,
                    PlayerName = hero.Name?.ToString() ?? "Unknown",
                    ClanName = hero.Clan?.Name?.ToString() ?? "NoClan",
                    StartDate = Campaign.Current.UniqueGameId ?? "unknown",
                    CreatedAt = existing?.CreatedAt ?? DateTime.UtcNow,
                    LastPlayedAt = DateTime.UtcNow,
                    CurrentDay = (int)now.ToDays,
                    IsIronMan = true, // Required for this mod
                    ModVersion = SubModule.ModuleVersion,
                    Language = GetGameLanguage()
                };

                _campaignRepo.SaveCampaignInfo(info);
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to save campaign info", ex);
            }
        }

        /// <summary>
        /// Get current game language
        /// </summary>
        private string GetGameLanguage()
        {
            try
            {
                // Try to get from game settings
                var language = TaleWorlds.Localization.LocalizedTextManager.GetLanguageIds(false);
                if (language != null && language.Count > 0)
                {
                    return language[0];
                }
                return "EN";
            }
            catch
            {
                return "EN";
            }
        }

        /// <summary>
        /// Get current game day (days since year 0)
        /// </summary>
        public int GetCurrentGameDay()
        {
            try
            {
                if (Campaign.Current == null) return 0;
                return (int)CampaignTime.Now.ToDays;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Update last played timestamp (call on save)
        /// </summary>
        public void UpdateLastPlayed()
        {
            if (!IsInitialized) return;

            try
            {
                int currentDay = GetCurrentGameDay();
                _campaignRepo.UpdateLastPlayed(_currentCampaignId, currentDay);
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to update last played", ex);
            }
        }

        /// <summary>
        /// Shutdown storage
        /// </summary>
        public void Shutdown()
        {
            try
            {
                ModLogger.LogDebug("StorageManager shutting down");
                
                if (IsInitialized)
                {
                    UpdateLastPlayed();
                }

                _database?.Dispose();
                _database = null;
                _campaignRepo = null;
                _chatRepo = null;
                _npcRepo = null;
                _eventRepo = null;
                _currentCampaignId = null;
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Error during StorageManager shutdown", ex);
            }
        }

        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                Shutdown();
                _disposed = true;
            }
        }
    }
}

