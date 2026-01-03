using System;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using TSEBanerAi.Utils;

namespace TSEBanerAi.Dialogue
{
    /// <summary>
    /// Parses LLM responses to extract commands, dice requests, and clean content
    /// </summary>
    public static class ResponseParser
    {
        // Regex patterns for JSON extraction
        private static readonly Regex JsonBlockPattern = new Regex(
            @"```json\s*\n?([\s\S]*?)\n?```",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        private static readonly Regex InlineJsonPattern = new Regex(
            @"\{[^{}]*(?:""command""|""dice_request"")[^{}]*\}",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        /// <summary>
        /// Parse command from LLM response
        /// </summary>
        public static GameCommand ParseCommand(string content)
        {
            if (string.IsNullOrEmpty(content)) return null;

            try
            {
                // Try to find JSON block first
                var match = JsonBlockPattern.Match(content);
                string jsonStr = match.Success ? match.Groups[1].Value.Trim() : null;

                // Fall back to inline JSON
                if (string.IsNullOrEmpty(jsonStr))
                {
                    var inlineMatch = InlineJsonPattern.Match(content);
                    if (inlineMatch.Success)
                    {
                        jsonStr = inlineMatch.Value;
                    }
                }

                if (string.IsNullOrEmpty(jsonStr)) return null;

                var json = JObject.Parse(jsonStr);

                // Check if it's a command
                if (json["command"] != null)
                {
                    var command = new GameCommand
                    {
                        CommandType = json["command"]?.ToString()?.ToLower(),
                        RawJson = jsonStr
                    };

                    // Parse params
                    var paramsObj = json["params"];
                    if (paramsObj != null)
                    {
                        command.Target = paramsObj["target"]?.ToString();
                        if (paramsObj["amount"] != null)
                        {
                            command.Amount = paramsObj["amount"].Value<int>();
                        }
                    }

                    ModLogger.LogDebug($"Parsed command: {command.CommandType}, target: {command.Target}");
                    return command;
                }

                return null;
            }
            catch (Exception ex)
            {
                ModLogger.LogDebug($"Failed to parse command: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Parse dice request from LLM response
        /// </summary>
        public static DiceRequest ParseDiceRequest(string content)
        {
            if (string.IsNullOrEmpty(content)) return null;

            try
            {
                // Try to find JSON block first
                var match = JsonBlockPattern.Match(content);
                string jsonStr = match.Success ? match.Groups[1].Value.Trim() : null;

                // Fall back to inline JSON
                if (string.IsNullOrEmpty(jsonStr))
                {
                    var inlineMatch = InlineJsonPattern.Match(content);
                    if (inlineMatch.Success)
                    {
                        jsonStr = inlineMatch.Value;
                    }
                }

                if (string.IsNullOrEmpty(jsonStr)) return null;

                var json = JObject.Parse(jsonStr);

                // Check if it's a dice request
                if (json["dice_request"]?.Value<bool>() == true)
                {
                    var request = new DiceRequest
                    {
                        Skill = json["skill"]?.ToString()?.ToLower() ?? "charm",
                        DC = json["dc"]?.Value<int>() ?? 15,
                        Reason = json["reason"]?.ToString()
                    };

                    ModLogger.LogDebug($"Parsed dice request: skill={request.Skill}, DC={request.DC}");
                    return request;
                }

                return null;
            }
            catch (Exception ex)
            {
                ModLogger.LogDebug($"Failed to parse dice request: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Clean content by removing JSON blocks
        /// </summary>
        public static string CleanContent(string content)
        {
            if (string.IsNullOrEmpty(content)) return content;

            try
            {
                // Remove JSON code blocks
                var cleaned = JsonBlockPattern.Replace(content, "");

                // Remove inline JSON patterns for commands/dice
                cleaned = InlineJsonPattern.Replace(cleaned, "");

                // Clean up whitespace
                cleaned = Regex.Replace(cleaned, @"\n\s*\n\s*\n", "\n\n");
                cleaned = cleaned.Trim();

                return cleaned;
            }
            catch
            {
                return content;
            }
        }

        /// <summary>
        /// Extract all JSON objects from content
        /// </summary>
        public static string[] ExtractAllJson(string content)
        {
            if (string.IsNullOrEmpty(content)) return Array.Empty<string>();

            try
            {
                var matches = JsonBlockPattern.Matches(content);
                var results = new string[matches.Count];
                
                for (int i = 0; i < matches.Count; i++)
                {
                    results[i] = matches[i].Groups[1].Value.Trim();
                }

                return results;
            }
            catch
            {
                return Array.Empty<string>();
            }
        }
    }
}

