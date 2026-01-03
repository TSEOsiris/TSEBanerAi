using System;
using System.Collections.Generic;
using System.Data.SQLite;
using TSEBanerAi.Storage.Models;
using TSEBanerAi.Utils;

namespace TSEBanerAi.Storage.Repositories
{
    /// <summary>
    /// Repository for NPC snapshots and memory operations
    /// </summary>
    public class NpcRepository
    {
        private readonly CampaignDatabase _db;

        public NpcRepository(CampaignDatabase database)
        {
            _db = database ?? throw new ArgumentNullException(nameof(database));
        }

        #region NPC Snapshots

        /// <summary>
        /// Save or update NPC snapshot
        /// </summary>
        public bool SaveSnapshot(NpcSnapshot snapshot)
        {
            try
            {
                string sql = @"
                    INSERT OR REPLACE INTO npc_snapshots 
                    (npc_id, campaign_id, name, age, gender, culture, clan_name, clan_id,
                     kingdom_name, kingdom_id, occupation, is_alive, relation_with_player,
                     trait_valor, trait_mercy, trait_honor, trait_generosity, trait_calculating,
                     current_location_id, current_location_name, gold, party_size,
                     last_updated, game_day_updated, skills_json, owned_fiefs_json)
                    VALUES 
                    (@npc_id, @campaign_id, @name, @age, @gender, @culture, @clan_name, @clan_id,
                     @kingdom_name, @kingdom_id, @occupation, @is_alive, @relation_with_player,
                     @trait_valor, @trait_mercy, @trait_honor, @trait_generosity, @trait_calculating,
                     @current_location_id, @current_location_name, @gold, @party_size,
                     @last_updated, @game_day_updated, @skills_json, @owned_fiefs_json)";

                int rows = _db.ExecuteNonQuery(sql,
                    new SQLiteParameter("@npc_id", snapshot.NpcId),
                    new SQLiteParameter("@campaign_id", snapshot.CampaignId),
                    new SQLiteParameter("@name", snapshot.Name),
                    new SQLiteParameter("@age", snapshot.Age),
                    new SQLiteParameter("@gender", snapshot.Gender),
                    new SQLiteParameter("@culture", snapshot.Culture ?? (object)DBNull.Value),
                    new SQLiteParameter("@clan_name", snapshot.ClanName ?? (object)DBNull.Value),
                    new SQLiteParameter("@clan_id", snapshot.ClanId ?? (object)DBNull.Value),
                    new SQLiteParameter("@kingdom_name", snapshot.KingdomName ?? (object)DBNull.Value),
                    new SQLiteParameter("@kingdom_id", snapshot.KingdomId ?? (object)DBNull.Value),
                    new SQLiteParameter("@occupation", snapshot.Occupation ?? (object)DBNull.Value),
                    new SQLiteParameter("@is_alive", snapshot.IsAlive ? 1 : 0),
                    new SQLiteParameter("@relation_with_player", snapshot.RelationWithPlayer),
                    new SQLiteParameter("@trait_valor", snapshot.TraitValor),
                    new SQLiteParameter("@trait_mercy", snapshot.TraitMercy),
                    new SQLiteParameter("@trait_honor", snapshot.TraitHonor),
                    new SQLiteParameter("@trait_generosity", snapshot.TraitGenerosity),
                    new SQLiteParameter("@trait_calculating", snapshot.TraitCalculating),
                    new SQLiteParameter("@current_location_id", snapshot.CurrentLocationId ?? (object)DBNull.Value),
                    new SQLiteParameter("@current_location_name", snapshot.CurrentLocationName ?? (object)DBNull.Value),
                    new SQLiteParameter("@gold", snapshot.Gold),
                    new SQLiteParameter("@party_size", snapshot.PartySize),
                    new SQLiteParameter("@last_updated", snapshot.LastUpdated.ToString("O")),
                    new SQLiteParameter("@game_day_updated", snapshot.GameDayUpdated),
                    new SQLiteParameter("@skills_json", snapshot.SkillsJson ?? (object)DBNull.Value),
                    new SQLiteParameter("@owned_fiefs_json", snapshot.OwnedFiefsJson ?? (object)DBNull.Value)
                );

                return rows > 0;
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to save NPC snapshot", ex);
                return false;
            }
        }

        /// <summary>
        /// Get NPC snapshot by ID
        /// </summary>
        public NpcSnapshot GetSnapshot(string campaignId, string npcId)
        {
            try
            {
                string sql = "SELECT * FROM npc_snapshots WHERE campaign_id = @campaign_id AND npc_id = @npc_id";

                using (var reader = _db.ExecuteReader(sql,
                    new SQLiteParameter("@campaign_id", campaignId),
                    new SQLiteParameter("@npc_id", npcId)))
                {
                    if (reader.Read())
                    {
                        return MapToNpcSnapshot(reader);
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to get NPC snapshot", ex);
                return null;
            }
        }

        /// <summary>
        /// Check if snapshot needs update (older than specified game days)
        /// </summary>
        public bool NeedsUpdate(string campaignId, string npcId, int currentGameDay, int maxAge = 1)
        {
            var snapshot = GetSnapshot(campaignId, npcId);
            if (snapshot == null) return true;
            return (currentGameDay - snapshot.GameDayUpdated) >= maxAge;
        }

        #endregion

        #region NPC Memory

        /// <summary>
        /// Save NPC memory
        /// </summary>
        public long SaveMemory(NpcMemory memory)
        {
            try
            {
                string sql = @"
                    INSERT INTO npc_memory 
                    (campaign_id, npc_id, memory_type, description, sentiment, game_day, 
                     created_at, is_active, related_message_id, expires_on_day)
                    VALUES 
                    (@campaign_id, @npc_id, @memory_type, @description, @sentiment, @game_day,
                     @created_at, @is_active, @related_message_id, @expires_on_day)";

                _db.ExecuteNonQuery(sql,
                    new SQLiteParameter("@campaign_id", memory.CampaignId),
                    new SQLiteParameter("@npc_id", memory.NpcId),
                    new SQLiteParameter("@memory_type", memory.MemoryType),
                    new SQLiteParameter("@description", memory.Description),
                    new SQLiteParameter("@sentiment", memory.Sentiment),
                    new SQLiteParameter("@game_day", memory.GameDay),
                    new SQLiteParameter("@created_at", memory.CreatedAt.ToString("O")),
                    new SQLiteParameter("@is_active", memory.IsActive ? 1 : 0),
                    new SQLiteParameter("@related_message_id", memory.RelatedMessageId.HasValue ? (object)memory.RelatedMessageId.Value : DBNull.Value),
                    new SQLiteParameter("@expires_on_day", memory.ExpiresOnDay.HasValue ? (object)memory.ExpiresOnDay.Value : DBNull.Value)
                );

                return _db.GetLastInsertId();
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to save NPC memory", ex);
                return -1;
            }
        }

        /// <summary>
        /// Get active memories for NPC
        /// </summary>
        public List<NpcMemory> GetActiveMemories(string campaignId, string npcId, int currentGameDay)
        {
            var memories = new List<NpcMemory>();
            try
            {
                string sql = @"
                    SELECT * FROM npc_memory 
                    WHERE campaign_id = @campaign_id 
                    AND npc_id = @npc_id 
                    AND is_active = 1
                    AND (expires_on_day IS NULL OR expires_on_day > @current_day)
                    ORDER BY game_day DESC";

                using (var reader = _db.ExecuteReader(sql,
                    new SQLiteParameter("@campaign_id", campaignId),
                    new SQLiteParameter("@npc_id", npcId),
                    new SQLiteParameter("@current_day", currentGameDay)))
                {
                    while (reader.Read())
                    {
                        memories.Add(MapToNpcMemory(reader));
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to get NPC memories", ex);
            }
            return memories;
        }

        /// <summary>
        /// Deactivate a memory
        /// </summary>
        public bool DeactivateMemory(long memoryId)
        {
            try
            {
                string sql = "UPDATE npc_memory SET is_active = 0 WHERE id = @id";
                int rows = _db.ExecuteNonQuery(sql, new SQLiteParameter("@id", memoryId));
                return rows > 0;
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to deactivate memory", ex);
                return false;
            }
        }

        #endregion

        #region Mapping

        private NpcSnapshot MapToNpcSnapshot(SQLiteDataReader reader)
        {
            var snapshot = new NpcSnapshot
            {
                NpcId = reader["npc_id"].ToString(),
                CampaignId = reader["campaign_id"].ToString(),
                Name = reader["name"].ToString(),
                Age = Convert.ToInt32(reader["age"]),
                Gender = reader["gender"].ToString(),
                IsAlive = Convert.ToInt32(reader["is_alive"]) == 1,
                RelationWithPlayer = Convert.ToInt32(reader["relation_with_player"]),
                TraitValor = Convert.ToInt32(reader["trait_valor"]),
                TraitMercy = Convert.ToInt32(reader["trait_mercy"]),
                TraitHonor = Convert.ToInt32(reader["trait_honor"]),
                TraitGenerosity = Convert.ToInt32(reader["trait_generosity"]),
                TraitCalculating = Convert.ToInt32(reader["trait_calculating"]),
                Gold = Convert.ToInt32(reader["gold"]),
                PartySize = Convert.ToInt32(reader["party_size"]),
                LastUpdated = DateTime.Parse(reader["last_updated"].ToString()),
                GameDayUpdated = Convert.ToInt32(reader["game_day_updated"])
            };

            if (reader["culture"] != DBNull.Value)
                snapshot.Culture = reader["culture"].ToString();
            if (reader["clan_name"] != DBNull.Value)
                snapshot.ClanName = reader["clan_name"].ToString();
            if (reader["clan_id"] != DBNull.Value)
                snapshot.ClanId = reader["clan_id"].ToString();
            if (reader["kingdom_name"] != DBNull.Value)
                snapshot.KingdomName = reader["kingdom_name"].ToString();
            if (reader["kingdom_id"] != DBNull.Value)
                snapshot.KingdomId = reader["kingdom_id"].ToString();
            if (reader["occupation"] != DBNull.Value)
                snapshot.Occupation = reader["occupation"].ToString();
            if (reader["current_location_id"] != DBNull.Value)
                snapshot.CurrentLocationId = reader["current_location_id"].ToString();
            if (reader["current_location_name"] != DBNull.Value)
                snapshot.CurrentLocationName = reader["current_location_name"].ToString();
            if (reader["skills_json"] != DBNull.Value)
                snapshot.SkillsJson = reader["skills_json"].ToString();
            if (reader["owned_fiefs_json"] != DBNull.Value)
                snapshot.OwnedFiefsJson = reader["owned_fiefs_json"].ToString();

            return snapshot;
        }

        private NpcMemory MapToNpcMemory(SQLiteDataReader reader)
        {
            var memory = new NpcMemory
            {
                Id = Convert.ToInt64(reader["id"]),
                CampaignId = reader["campaign_id"].ToString(),
                NpcId = reader["npc_id"].ToString(),
                MemoryType = reader["memory_type"].ToString(),
                Description = reader["description"].ToString(),
                Sentiment = Convert.ToInt32(reader["sentiment"]),
                GameDay = Convert.ToInt32(reader["game_day"]),
                CreatedAt = DateTime.Parse(reader["created_at"].ToString()),
                IsActive = Convert.ToInt32(reader["is_active"]) == 1
            };

            if (reader["related_message_id"] != DBNull.Value)
                memory.RelatedMessageId = Convert.ToInt64(reader["related_message_id"]);
            if (reader["expires_on_day"] != DBNull.Value)
                memory.ExpiresOnDay = Convert.ToInt32(reader["expires_on_day"]);

            return memory;
        }

        #endregion
    }
}

