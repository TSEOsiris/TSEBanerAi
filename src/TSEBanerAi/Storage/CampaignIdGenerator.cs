using System;
using System.Security.Cryptography;
using System.Text;
using TaleWorlds.CampaignSystem;
using TSEBanerAi.Utils;

namespace TSEBanerAi.Storage
{
    /// <summary>
    /// Generates unique campaign identifiers based on player name, clan and start date
    /// </summary>
    public static class CampaignIdGenerator
    {
        /// <summary>
        /// Generate campaign ID from current campaign state
        /// </summary>
        /// <returns>Unique campaign identifier or null if campaign not available</returns>
        public static string GenerateCampaignId()
        {
            try
            {
                if (Campaign.Current == null)
                {
                    ModLogger.LogError("Cannot generate campaign ID: Campaign.Current is null");
                    return null;
                }

                var hero = Campaign.Current.MainParty?.LeaderHero;
                if (hero == null)
                {
                    ModLogger.LogError("Cannot generate campaign ID: MainParty.LeaderHero is null");
                    return null;
                }

                string playerName = hero.Name?.ToString() ?? "Unknown";
                string clanName = hero.Clan?.Name?.ToString() ?? "NoClan";
                
                // Use UniqueGameId as stable campaign identifier (more reliable than CampaignStartTime)
                string uniqueId = Campaign.Current.UniqueGameId ?? "unknown";

                return GenerateHash(playerName, clanName, uniqueId);
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to generate campaign ID", ex);
                return null;
            }
        }

        /// <summary>
        /// Generate campaign ID from provided values (for loading existing campaigns)
        /// </summary>
        public static string GenerateHash(string playerName, string clanName, string startDate)
        {
            string combined = $"{playerName}|{clanName}|{startDate}";
            
            using (var sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
                
                // Take first 16 chars of hex for readability
                var sb = new StringBuilder();
                for (int i = 0; i < 8; i++)
                {
                    sb.Append(bytes[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }

        /// <summary>
        /// Get database file path for campaign
        /// </summary>
        public static string GetDatabasePath(string campaignId)
        {
            return ModPaths.GetDatabaseFilePath($"campaign_{campaignId}.db");
        }

        /// <summary>
        /// Check if database exists for campaign
        /// </summary>
        public static bool DatabaseExists(string campaignId)
        {
            string path = GetDatabasePath(campaignId);
            return System.IO.File.Exists(path);
        }
    }
}

