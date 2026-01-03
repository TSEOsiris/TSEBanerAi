using System;
using System.Collections.Generic;
using System.Data.SQLite;
using TSEBanerAi.Storage.Models;
using TSEBanerAi.Utils;

namespace TSEBanerAi.Storage.Repositories
{
    /// <summary>
    /// Repository for game event operations
    /// </summary>
    public class EventRepository
    {
        private readonly CampaignDatabase _db;

        public EventRepository(CampaignDatabase database)
        {
            _db = database ?? throw new ArgumentNullException(nameof(database));
        }

        /// <summary>
        /// Save a new game event
        /// </summary>
        public long SaveEvent(GameEvent gameEvent)
        {
            try
            {
                string sql = @"
                    INSERT INTO game_events 
                    (campaign_id, event_type, primary_entity_id, secondary_entity_id, 
                     description, data_json, game_day, timestamp, is_llm_generated, was_displayed)
                    VALUES 
                    (@campaign_id, @event_type, @primary_entity_id, @secondary_entity_id,
                     @description, @data_json, @game_day, @timestamp, @is_llm_generated, @was_displayed)";

                _db.ExecuteNonQuery(sql,
                    new SQLiteParameter("@campaign_id", gameEvent.CampaignId),
                    new SQLiteParameter("@event_type", gameEvent.EventType),
                    new SQLiteParameter("@primary_entity_id", gameEvent.PrimaryEntityId ?? (object)DBNull.Value),
                    new SQLiteParameter("@secondary_entity_id", gameEvent.SecondaryEntityId ?? (object)DBNull.Value),
                    new SQLiteParameter("@description", gameEvent.Description),
                    new SQLiteParameter("@data_json", gameEvent.DataJson ?? (object)DBNull.Value),
                    new SQLiteParameter("@game_day", gameEvent.GameDay),
                    new SQLiteParameter("@timestamp", gameEvent.Timestamp.ToString("O")),
                    new SQLiteParameter("@is_llm_generated", gameEvent.IsLlmGenerated ? 1 : 0),
                    new SQLiteParameter("@was_displayed", gameEvent.WasDisplayed ? 1 : 0)
                );

                return _db.GetLastInsertId();
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to save game event", ex);
                return -1;
            }
        }

        /// <summary>
        /// Get recent events
        /// </summary>
        public List<GameEvent> GetRecentEvents(string campaignId, int limit = 50)
        {
            var events = new List<GameEvent>();
            try
            {
                string sql = @"
                    SELECT * FROM game_events 
                    WHERE campaign_id = @campaign_id
                    ORDER BY game_day DESC, timestamp DESC
                    LIMIT @limit";

                using (var reader = _db.ExecuteReader(sql,
                    new SQLiteParameter("@campaign_id", campaignId),
                    new SQLiteParameter("@limit", limit)))
                {
                    while (reader.Read())
                    {
                        events.Add(MapToGameEvent(reader));
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to get recent events", ex);
            }
            return events;
        }

        /// <summary>
        /// Get events by type
        /// </summary>
        public List<GameEvent> GetEventsByType(string campaignId, string eventType, int limit = 20)
        {
            var events = new List<GameEvent>();
            try
            {
                string sql = @"
                    SELECT * FROM game_events 
                    WHERE campaign_id = @campaign_id AND event_type = @event_type
                    ORDER BY game_day DESC, timestamp DESC
                    LIMIT @limit";

                using (var reader = _db.ExecuteReader(sql,
                    new SQLiteParameter("@campaign_id", campaignId),
                    new SQLiteParameter("@event_type", eventType),
                    new SQLiteParameter("@limit", limit)))
                {
                    while (reader.Read())
                    {
                        events.Add(MapToGameEvent(reader));
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to get events by type", ex);
            }
            return events;
        }

        /// <summary>
        /// Get events involving entity
        /// </summary>
        public List<GameEvent> GetEventsForEntity(string campaignId, string entityId, int limit = 20)
        {
            var events = new List<GameEvent>();
            try
            {
                string sql = @"
                    SELECT * FROM game_events 
                    WHERE campaign_id = @campaign_id 
                    AND (primary_entity_id = @entity_id OR secondary_entity_id = @entity_id)
                    ORDER BY game_day DESC, timestamp DESC
                    LIMIT @limit";

                using (var reader = _db.ExecuteReader(sql,
                    new SQLiteParameter("@campaign_id", campaignId),
                    new SQLiteParameter("@entity_id", entityId),
                    new SQLiteParameter("@limit", limit)))
                {
                    while (reader.Read())
                    {
                        events.Add(MapToGameEvent(reader));
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to get events for entity", ex);
            }
            return events;
        }

        /// <summary>
        /// Mark event as displayed
        /// </summary>
        public bool MarkAsDisplayed(long eventId)
        {
            try
            {
                string sql = "UPDATE game_events SET was_displayed = 1 WHERE id = @id";
                int rows = _db.ExecuteNonQuery(sql, new SQLiteParameter("@id", eventId));
                return rows > 0;
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to mark event as displayed", ex);
                return false;
            }
        }

        private GameEvent MapToGameEvent(SQLiteDataReader reader)
        {
            var evt = new GameEvent
            {
                Id = Convert.ToInt64(reader["id"]),
                CampaignId = reader["campaign_id"].ToString(),
                EventType = reader["event_type"].ToString(),
                Description = reader["description"].ToString(),
                GameDay = Convert.ToInt32(reader["game_day"]),
                Timestamp = DateTime.Parse(reader["timestamp"].ToString()),
                IsLlmGenerated = Convert.ToInt32(reader["is_llm_generated"]) == 1,
                WasDisplayed = Convert.ToInt32(reader["was_displayed"]) == 1
            };

            if (reader["primary_entity_id"] != DBNull.Value)
                evt.PrimaryEntityId = reader["primary_entity_id"].ToString();
            if (reader["secondary_entity_id"] != DBNull.Value)
                evt.SecondaryEntityId = reader["secondary_entity_id"].ToString();
            if (reader["data_json"] != DBNull.Value)
                evt.DataJson = reader["data_json"].ToString();

            return evt;
        }
    }
}

