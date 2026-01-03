namespace TSEBanerAi.Storage
{
    /// <summary>
    /// SQL schema definitions for campaign database
    /// </summary>
    public static class DatabaseSchema
    {
        public const string Version = "1.0.0";

        /// <summary>
        /// Table to store schema version for migrations
        /// </summary>
        public const string CreateSchemaVersionTable = @"
            CREATE TABLE IF NOT EXISTS schema_version (
                version TEXT PRIMARY KEY,
                applied_at TEXT NOT NULL
            );
        ";

        /// <summary>
        /// Campaign info table
        /// </summary>
        public const string CreateCampaignInfoTable = @"
            CREATE TABLE IF NOT EXISTS campaign_info (
                campaign_id TEXT PRIMARY KEY,
                player_name TEXT NOT NULL,
                clan_name TEXT NOT NULL,
                start_date TEXT NOT NULL,
                created_at TEXT NOT NULL,
                last_played_at TEXT NOT NULL,
                current_day INTEGER NOT NULL DEFAULT 0,
                is_iron_man INTEGER NOT NULL DEFAULT 1,
                mod_version TEXT NOT NULL,
                language TEXT NOT NULL DEFAULT 'EN'
            );
        ";

        /// <summary>
        /// Chat messages table
        /// </summary>
        public const string CreateChatMessagesTable = @"
            CREATE TABLE IF NOT EXISTS chat_messages (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                campaign_id TEXT NOT NULL,
                npc_id TEXT NOT NULL,
                npc_name TEXT NOT NULL,
                is_player_message INTEGER NOT NULL,
                content TEXT NOT NULL,
                game_day INTEGER NOT NULL,
                timestamp TEXT NOT NULL,
                dice_roll INTEGER,
                dice_modifier INTEGER,
                dice_dc INTEGER,
                dice_success INTEGER,
                command_json TEXT,
                location_id TEXT,
                tokens_used INTEGER,
                response_time_ms INTEGER,
                FOREIGN KEY (campaign_id) REFERENCES campaign_info(campaign_id)
            );
            
            CREATE INDEX IF NOT EXISTS idx_chat_messages_campaign ON chat_messages(campaign_id);
            CREATE INDEX IF NOT EXISTS idx_chat_messages_npc ON chat_messages(campaign_id, npc_id);
            CREATE INDEX IF NOT EXISTS idx_chat_messages_day ON chat_messages(campaign_id, game_day);
        ";

        /// <summary>
        /// NPC memory table
        /// </summary>
        public const string CreateNpcMemoryTable = @"
            CREATE TABLE IF NOT EXISTS npc_memory (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                campaign_id TEXT NOT NULL,
                npc_id TEXT NOT NULL,
                memory_type TEXT NOT NULL,
                description TEXT NOT NULL,
                sentiment INTEGER NOT NULL DEFAULT 0,
                game_day INTEGER NOT NULL,
                created_at TEXT NOT NULL,
                is_active INTEGER NOT NULL DEFAULT 1,
                related_message_id INTEGER,
                expires_on_day INTEGER,
                FOREIGN KEY (campaign_id) REFERENCES campaign_info(campaign_id),
                FOREIGN KEY (related_message_id) REFERENCES chat_messages(id)
            );
            
            CREATE INDEX IF NOT EXISTS idx_npc_memory_campaign ON npc_memory(campaign_id);
            CREATE INDEX IF NOT EXISTS idx_npc_memory_npc ON npc_memory(campaign_id, npc_id);
            CREATE INDEX IF NOT EXISTS idx_npc_memory_active ON npc_memory(campaign_id, npc_id, is_active);
        ";

        /// <summary>
        /// Game events table
        /// </summary>
        public const string CreateGameEventsTable = @"
            CREATE TABLE IF NOT EXISTS game_events (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                campaign_id TEXT NOT NULL,
                event_type TEXT NOT NULL,
                primary_entity_id TEXT,
                secondary_entity_id TEXT,
                description TEXT NOT NULL,
                data_json TEXT,
                game_day INTEGER NOT NULL,
                timestamp TEXT NOT NULL,
                is_llm_generated INTEGER NOT NULL DEFAULT 0,
                was_displayed INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (campaign_id) REFERENCES campaign_info(campaign_id)
            );
            
            CREATE INDEX IF NOT EXISTS idx_game_events_campaign ON game_events(campaign_id);
            CREATE INDEX IF NOT EXISTS idx_game_events_type ON game_events(campaign_id, event_type);
            CREATE INDEX IF NOT EXISTS idx_game_events_day ON game_events(campaign_id, game_day);
        ";

        /// <summary>
        /// NPC snapshots table (cached data for LLM context)
        /// </summary>
        public const string CreateNpcSnapshotsTable = @"
            CREATE TABLE IF NOT EXISTS npc_snapshots (
                npc_id TEXT NOT NULL,
                campaign_id TEXT NOT NULL,
                name TEXT NOT NULL,
                age INTEGER NOT NULL,
                gender TEXT NOT NULL,
                culture TEXT,
                clan_name TEXT,
                clan_id TEXT,
                kingdom_name TEXT,
                kingdom_id TEXT,
                occupation TEXT,
                is_alive INTEGER NOT NULL DEFAULT 1,
                relation_with_player INTEGER NOT NULL DEFAULT 0,
                trait_valor INTEGER NOT NULL DEFAULT 0,
                trait_mercy INTEGER NOT NULL DEFAULT 0,
                trait_honor INTEGER NOT NULL DEFAULT 0,
                trait_generosity INTEGER NOT NULL DEFAULT 0,
                trait_calculating INTEGER NOT NULL DEFAULT 0,
                current_location_id TEXT,
                current_location_name TEXT,
                gold INTEGER NOT NULL DEFAULT 0,
                party_size INTEGER NOT NULL DEFAULT 0,
                last_updated TEXT NOT NULL,
                game_day_updated INTEGER NOT NULL,
                skills_json TEXT,
                owned_fiefs_json TEXT,
                PRIMARY KEY (campaign_id, npc_id),
                FOREIGN KEY (campaign_id) REFERENCES campaign_info(campaign_id)
            );
            
            CREATE INDEX IF NOT EXISTS idx_npc_snapshots_campaign ON npc_snapshots(campaign_id);
        ";

        /// <summary>
        /// All schema creation statements in order
        /// </summary>
        public static readonly string[] AllTables = new[]
        {
            CreateSchemaVersionTable,
            CreateCampaignInfoTable,
            CreateChatMessagesTable,
            CreateNpcMemoryTable,
            CreateGameEventsTable,
            CreateNpcSnapshotsTable
        };
    }
}

