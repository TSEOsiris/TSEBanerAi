using TaleWorlds.CampaignSystem;

namespace TSEBanerAi.Commands
{
    /// <summary>
    /// Interface for game commands that can be executed by NPC
    /// </summary>
    public interface IGameCommand
    {
        /// <summary>
        /// Command type identifier
        /// </summary>
        string CommandType { get; }

        /// <summary>
        /// Human-readable description of what this command does
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Check if command can be executed in current state
        /// </summary>
        bool CanExecute(Hero npc, CommandContext context);

        /// <summary>
        /// Execute the command
        /// </summary>
        CommandResult Execute(Hero npc, CommandContext context);

        /// <summary>
        /// Get reason why command cannot be executed
        /// </summary>
        string GetCannotExecuteReason(Hero npc, CommandContext context);
    }

    /// <summary>
    /// Context for command execution
    /// </summary>
    public class CommandContext
    {
        /// <summary>
        /// Target entity (settlement, hero, etc.)
        /// </summary>
        public string Target { get; set; }

        /// <summary>
        /// Amount or magnitude (for relation changes, etc.)
        /// </summary>
        public int? Amount { get; set; }

        /// <summary>
        /// Raw command JSON
        /// </summary>
        public string RawJson { get; set; }

        /// <summary>
        /// Was this command result of successful dice roll
        /// </summary>
        public bool WasDiceSuccess { get; set; }

        /// <summary>
        /// Player who issued the command
        /// </summary>
        public Hero Player { get; set; }
    }

    /// <summary>
    /// Result of command execution
    /// </summary>
    public class CommandResult
    {
        /// <summary>
        /// Whether command was executed successfully
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Human-readable result message
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Error message if failed
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// Whether a dice roll is required
        /// </summary>
        public bool RequiresDiceRoll { get; set; }

        /// <summary>
        /// Skill for dice roll
        /// </summary>
        public string DiceSkill { get; set; }

        /// <summary>
        /// Difficulty class for dice roll
        /// </summary>
        public int DiceDC { get; set; }

        public static CommandResult Ok(string message) => new CommandResult { Success = true, Message = message };
        public static CommandResult Fail(string error) => new CommandResult { Success = false, Error = error };
        public static CommandResult NeedsDice(string skill, int dc) => new CommandResult 
        { 
            Success = false, 
            RequiresDiceRoll = true, 
            DiceSkill = skill, 
            DiceDC = dc 
        };
    }
}

