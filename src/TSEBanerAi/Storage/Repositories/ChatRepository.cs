using System;
using System.Collections.Generic;
using System.Data.SQLite;
using TSEBanerAi.Storage.Models;
using TSEBanerAi.Utils;

namespace TSEBanerAi.Storage.Repositories
{
    /// <summary>
    /// Repository for chat message operations
    /// </summary>
    public class ChatRepository
    {
        private readonly CampaignDatabase _db;

        public ChatRepository(CampaignDatabase database)
        {
            _db = database ?? throw new ArgumentNullException(nameof(database));
        }

        /// <summary>
        /// Save a new chat message
        /// </summary>
        public long SaveMessage(ChatMessage message)
        {
            try
            {
                string sql = @"
                    INSERT INTO chat_messages 
                    (campaign_id, npc_id, npc_name, is_player_message, content, game_day, timestamp,
                     dice_roll, dice_modifier, dice_dc, dice_success, command_json, location_id,
                     tokens_used, response_time_ms)
                    VALUES 
                    (@campaign_id, @npc_id, @npc_name, @is_player_message, @content, @game_day, @timestamp,
                     @dice_roll, @dice_modifier, @dice_dc, @dice_success, @command_json, @location_id,
                     @tokens_used, @response_time_ms)";

                _db.ExecuteNonQuery(sql,
                    new SQLiteParameter("@campaign_id", message.CampaignId),
                    new SQLiteParameter("@npc_id", message.NpcId),
                    new SQLiteParameter("@npc_name", message.NpcName),
                    new SQLiteParameter("@is_player_message", message.IsPlayerMessage ? 1 : 0),
                    new SQLiteParameter("@content", message.Content),
                    new SQLiteParameter("@game_day", message.GameDay),
                    new SQLiteParameter("@timestamp", message.Timestamp.ToString("O")),
                    new SQLiteParameter("@dice_roll", message.DiceRoll.HasValue ? (object)message.DiceRoll.Value : DBNull.Value),
                    new SQLiteParameter("@dice_modifier", message.DiceModifier.HasValue ? (object)message.DiceModifier.Value : DBNull.Value),
                    new SQLiteParameter("@dice_dc", message.DiceDC.HasValue ? (object)message.DiceDC.Value : DBNull.Value),
                    new SQLiteParameter("@dice_success", message.DiceSuccess.HasValue ? (object)(message.DiceSuccess.Value ? 1 : 0) : DBNull.Value),
                    new SQLiteParameter("@command_json", string.IsNullOrEmpty(message.CommandJson) ? DBNull.Value : (object)message.CommandJson),
                    new SQLiteParameter("@location_id", string.IsNullOrEmpty(message.LocationId) ? DBNull.Value : (object)message.LocationId),
                    new SQLiteParameter("@tokens_used", message.TokensUsed.HasValue ? (object)message.TokensUsed.Value : DBNull.Value),
                    new SQLiteParameter("@response_time_ms", message.ResponseTimeMs.HasValue ? (object)message.ResponseTimeMs.Value : DBNull.Value)
                );

                return _db.GetLastInsertId();
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to save chat message", ex);
                return -1;
            }
        }

        /// <summary>
        /// Get chat history with NPC (most recent first)
        /// </summary>
        public List<ChatMessage> GetChatHistory(string campaignId, string npcId, int limit = 50)
        {
            var messages = new List<ChatMessage>();
            try
            {
                string sql = @"
                    SELECT * FROM chat_messages 
                    WHERE campaign_id = @campaign_id AND npc_id = @npc_id
                    ORDER BY timestamp DESC
                    LIMIT @limit";

                using (var reader = _db.ExecuteReader(sql,
                    new SQLiteParameter("@campaign_id", campaignId),
                    new SQLiteParameter("@npc_id", npcId),
                    new SQLiteParameter("@limit", limit)))
                {
                    while (reader.Read())
                    {
                        messages.Add(MapToChatMessage(reader));
                    }
                }

                // Reverse to get chronological order
                messages.Reverse();
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to get chat history", ex);
            }
            return messages;
        }

        /// <summary>
        /// Get recent messages across all NPCs
        /// </summary>
        public List<ChatMessage> GetRecentMessages(string campaignId, int limit = 100)
        {
            var messages = new List<ChatMessage>();
            try
            {
                string sql = @"
                    SELECT * FROM chat_messages 
                    WHERE campaign_id = @campaign_id
                    ORDER BY timestamp DESC
                    LIMIT @limit";

                using (var reader = _db.ExecuteReader(sql,
                    new SQLiteParameter("@campaign_id", campaignId),
                    new SQLiteParameter("@limit", limit)))
                {
                    while (reader.Read())
                    {
                        messages.Add(MapToChatMessage(reader));
                    }
                }

                messages.Reverse();
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to get recent messages", ex);
            }
            return messages;
        }

        /// <summary>
        /// Get message count for NPC
        /// </summary>
        public int GetMessageCount(string campaignId, string npcId)
        {
            try
            {
                string sql = @"
                    SELECT COUNT(*) FROM chat_messages 
                    WHERE campaign_id = @campaign_id AND npc_id = @npc_id";

                var result = _db.ExecuteScalar(sql,
                    new SQLiteParameter("@campaign_id", campaignId),
                    new SQLiteParameter("@npc_id", npcId));

                return Convert.ToInt32(result);
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to get message count", ex);
                return 0;
            }
        }

        private ChatMessage MapToChatMessage(SQLiteDataReader reader)
        {
            var msg = new ChatMessage
            {
                Id = Convert.ToInt64(reader["id"]),
                CampaignId = reader["campaign_id"].ToString(),
                NpcId = reader["npc_id"].ToString(),
                NpcName = reader["npc_name"].ToString(),
                IsPlayerMessage = Convert.ToInt32(reader["is_player_message"]) == 1,
                Content = reader["content"].ToString(),
                GameDay = Convert.ToInt32(reader["game_day"]),
                Timestamp = DateTime.Parse(reader["timestamp"].ToString())
            };

            if (reader["dice_roll"] != DBNull.Value)
                msg.DiceRoll = Convert.ToInt32(reader["dice_roll"]);
            if (reader["dice_modifier"] != DBNull.Value)
                msg.DiceModifier = Convert.ToInt32(reader["dice_modifier"]);
            if (reader["dice_dc"] != DBNull.Value)
                msg.DiceDC = Convert.ToInt32(reader["dice_dc"]);
            if (reader["dice_success"] != DBNull.Value)
                msg.DiceSuccess = Convert.ToInt32(reader["dice_success"]) == 1;
            if (reader["command_json"] != DBNull.Value)
                msg.CommandJson = reader["command_json"].ToString();
            if (reader["location_id"] != DBNull.Value)
                msg.LocationId = reader["location_id"].ToString();
            if (reader["tokens_used"] != DBNull.Value)
                msg.TokensUsed = Convert.ToInt32(reader["tokens_used"]);
            if (reader["response_time_ms"] != DBNull.Value)
                msg.ResponseTimeMs = Convert.ToInt32(reader["response_time_ms"]);

            return msg;
        }
    }
}

