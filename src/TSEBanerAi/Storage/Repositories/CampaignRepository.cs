using System;
using System.Data.SQLite;
using TSEBanerAi.Storage.Models;
using TSEBanerAi.Utils;

namespace TSEBanerAi.Storage.Repositories
{
    /// <summary>
    /// Repository for campaign info operations
    /// </summary>
    public class CampaignRepository
    {
        private readonly CampaignDatabase _db;

        public CampaignRepository(CampaignDatabase database)
        {
            _db = database ?? throw new ArgumentNullException(nameof(database));
        }

        /// <summary>
        /// Save or update campaign info
        /// </summary>
        public bool SaveCampaignInfo(CampaignInfo info)
        {
            try
            {
                string sql = @"
                    INSERT OR REPLACE INTO campaign_info 
                    (campaign_id, player_name, clan_name, start_date, created_at, last_played_at, 
                     current_day, is_iron_man, mod_version, language)
                    VALUES 
                    (@campaign_id, @player_name, @clan_name, @start_date, @created_at, @last_played_at,
                     @current_day, @is_iron_man, @mod_version, @language)";

                int rows = _db.ExecuteNonQuery(sql,
                    new SQLiteParameter("@campaign_id", info.CampaignId),
                    new SQLiteParameter("@player_name", info.PlayerName),
                    new SQLiteParameter("@clan_name", info.ClanName),
                    new SQLiteParameter("@start_date", info.StartDate),
                    new SQLiteParameter("@created_at", info.CreatedAt.ToString("O")),
                    new SQLiteParameter("@last_played_at", info.LastPlayedAt.ToString("O")),
                    new SQLiteParameter("@current_day", info.CurrentDay),
                    new SQLiteParameter("@is_iron_man", info.IsIronMan ? 1 : 0),
                    new SQLiteParameter("@mod_version", info.ModVersion),
                    new SQLiteParameter("@language", info.Language)
                );

                return rows > 0;
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to save campaign info", ex);
                return false;
            }
        }

        /// <summary>
        /// Get campaign info by ID
        /// </summary>
        public CampaignInfo GetCampaignInfo(string campaignId)
        {
            try
            {
                string sql = "SELECT * FROM campaign_info WHERE campaign_id = @campaign_id";
                
                using (var reader = _db.ExecuteReader(sql, new SQLiteParameter("@campaign_id", campaignId)))
                {
                    if (reader.Read())
                    {
                        return MapToCampaignInfo(reader);
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to get campaign info", ex);
                return null;
            }
        }

        /// <summary>
        /// Update last played timestamp and current day
        /// </summary>
        public bool UpdateLastPlayed(string campaignId, int currentDay)
        {
            try
            {
                string sql = @"
                    UPDATE campaign_info 
                    SET last_played_at = @last_played_at, current_day = @current_day
                    WHERE campaign_id = @campaign_id";

                int rows = _db.ExecuteNonQuery(sql,
                    new SQLiteParameter("@campaign_id", campaignId),
                    new SQLiteParameter("@last_played_at", DateTime.UtcNow.ToString("O")),
                    new SQLiteParameter("@current_day", currentDay)
                );

                return rows > 0;
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to update last played", ex);
                return false;
            }
        }

        private CampaignInfo MapToCampaignInfo(SQLiteDataReader reader)
        {
            return new CampaignInfo
            {
                CampaignId = reader["campaign_id"].ToString(),
                PlayerName = reader["player_name"].ToString(),
                ClanName = reader["clan_name"].ToString(),
                StartDate = reader["start_date"].ToString(),
                CreatedAt = DateTime.Parse(reader["created_at"].ToString()),
                LastPlayedAt = DateTime.Parse(reader["last_played_at"].ToString()),
                CurrentDay = Convert.ToInt32(reader["current_day"]),
                IsIronMan = Convert.ToInt32(reader["is_iron_man"]) == 1,
                ModVersion = reader["mod_version"].ToString(),
                Language = reader["language"].ToString()
            };
        }
    }
}

