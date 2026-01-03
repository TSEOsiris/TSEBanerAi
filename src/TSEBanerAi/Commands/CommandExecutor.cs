using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TSEBanerAi.Dialogue;
using TSEBanerAi.Storage;
using TSEBanerAi.Storage.Models;
using TSEBanerAi.Utils;

namespace TSEBanerAi.Commands
{
    /// <summary>
    /// Executes game commands from LLM responses
    /// </summary>
    public class CommandExecutor
    {
        private static CommandExecutor _instance;
        private static readonly object _lock = new object();

        private readonly Dictionary<string, IGameCommand> _commands;

        public static CommandExecutor Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new CommandExecutor();
                        }
                    }
                }
                return _instance;
            }
        }

        private CommandExecutor()
        {
            _commands = new Dictionary<string, IGameCommand>(StringComparer.OrdinalIgnoreCase);
            RegisterDefaultCommands();
        }

        /// <summary>
        /// Register default commands
        /// </summary>
        private void RegisterDefaultCommands()
        {
            // Basic commands (Phase 1)
            RegisterCommand(new FollowCommand());
            RegisterCommand(new UnfollowCommand());
            
            // Extended commands (Phase 2)
            RegisterCommand(new PatrolCommand());
            RegisterCommand(new AttackCommand());
            RegisterCommand(new SiegeCommand());
            RegisterCommand(new ChangeRelationCommand());
        }

        /// <summary>
        /// Register a command
        /// </summary>
        public void RegisterCommand(IGameCommand command)
        {
            if (command == null) return;
            _commands[command.CommandType] = command;
            ModLogger.LogDebug($"Registered command: {command.CommandType}");
        }

        /// <summary>
        /// Execute command from GameCommand data
        /// </summary>
        public CommandResult ExecuteCommand(Hero npc, GameCommand command)
        {
            if (npc == null)
            {
                return CommandResult.Fail("No NPC specified");
            }

            if (command == null || string.IsNullOrEmpty(command.CommandType))
            {
                return CommandResult.Fail("No command specified");
            }

            try
            {
                if (!_commands.TryGetValue(command.CommandType, out var handler))
                {
                    ModLogger.LogWarning($"Unknown command type: {command.CommandType}");
                    return CommandResult.Fail($"Unknown command: {command.CommandType}");
                }

                var context = new CommandContext
                {
                    Target = command.Target,
                    Amount = command.Amount,
                    RawJson = command.RawJson,
                    Player = Hero.MainHero
                };

                // Check if command can be executed
                if (!handler.CanExecute(npc, context))
                {
                    string reason = handler.GetCannotExecuteReason(npc, context);
                    ModLogger.LogDebug($"Command {command.CommandType} cannot be executed: {reason}");
                    return CommandResult.Fail(reason);
                }

                // Execute command
                var result = handler.Execute(npc, context);

                // Log event
                LogCommandEvent(npc, command, result);

                // Show in-game message
                if (result.Success)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"[TSEBanerAi] {result.Message}", 
                        Colors.Green
                    ));
                }

                return result;
            }
            catch (Exception ex)
            {
                ModLogger.LogException($"Failed to execute command {command.CommandType}", ex);
                return CommandResult.Fail($"Command execution failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Execute command from DialogueResponse
        /// </summary>
        public CommandResult ExecuteFromResponse(Hero npc, DialogueResponse response)
        {
            if (response?.Command == null)
            {
                return CommandResult.Fail("No command in response");
            }

            return ExecuteCommand(npc, response.Command);
        }

        /// <summary>
        /// Check if command is registered
        /// </summary>
        public bool HasCommand(string commandType)
        {
            return !string.IsNullOrEmpty(commandType) && _commands.ContainsKey(commandType);
        }

        /// <summary>
        /// Get list of registered command types
        /// </summary>
        public IEnumerable<string> GetRegisteredCommands()
        {
            return _commands.Keys;
        }

        /// <summary>
        /// Log command event to database
        /// </summary>
        private void LogCommandEvent(Hero npc, GameCommand command, CommandResult result)
        {
            try
            {
                var storage = StorageManager.Instance;
                if (!storage.IsInitialized) return;

                var gameEvent = new GameEvent
                {
                    CampaignId = storage.CurrentCampaignId,
                    EventType = EventTypes.PlayerCommand,
                    PrimaryEntityId = npc?.StringId,
                    Description = result.Success 
                        ? $"Executed {command.CommandType} command on {npc?.Name}"
                        : $"Failed {command.CommandType} command on {npc?.Name}: {result.Error}",
                    DataJson = Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        command = command.CommandType,
                        target = command.Target,
                        success = result.Success,
                        message = result.Message,
                        error = result.Error
                    }),
                    GameDay = storage.GetCurrentGameDay(),
                    Timestamp = DateTime.UtcNow,
                    IsLlmGenerated = true,
                    WasDisplayed = true
                };

                storage.Events.SaveEvent(gameEvent);
            }
            catch (Exception ex)
            {
                ModLogger.LogException("Failed to log command event", ex);
            }
        }
    }
}

