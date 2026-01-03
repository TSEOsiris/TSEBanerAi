using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TSEBanerAi.Utils;

namespace TSEBanerAi.Dice
{
    /// <summary>
    /// Calculates D20 modifiers based on skills, traits, and context
    /// </summary>
    public static class ModifierCalculator
    {
        // Skill to D&D-style ability score conversion
        // Bannerlord skills go 0-300+, we convert to -2 to +10 modifier range
        private const int SkillPerPoint = 25; // Every 25 skill points = +1 modifier
        private const int BaseModifier = -2;  // Modifier at skill 0

        /// <summary>
        /// Calculate total modifier for a skill check
        /// </summary>
        public static int CalculateModifier(Hero player, Hero npc, string skill)
        {
            if (player == null) return 0;

            int modifier = 0;

            // Add skill modifier
            modifier += GetSkillModifier(player, skill);

            // Add trait modifiers
            modifier += GetTraitModifiers(player, skill);

            // Add relation modifier
            if (npc != null)
            {
                modifier += GetRelationModifier(player, npc);
            }

            // Add context modifiers (war, alliance, etc.)
            if (npc != null)
            {
                modifier += GetContextModifier(player, npc);
            }

            ModLogger.LogDebug($"Modifier calculation for {skill}: skill={GetSkillModifier(player, skill)}, " +
                              $"traits={GetTraitModifiers(player, skill)}, relation={GetRelationModifier(player, npc)}, " +
                              $"context={GetContextModifier(player, npc)}, total={modifier}");

            return modifier;
        }

        /// <summary>
        /// Get modifier from skill level
        /// </summary>
        public static int GetSkillModifier(Hero hero, string skill)
        {
            if (hero == null || string.IsNullOrEmpty(skill)) return 0;

            try
            {
                int skillValue = 0;

                switch (skill.ToLower())
                {
                    case "charm":
                        skillValue = hero.GetSkillValue(DefaultSkills.Charm);
                        break;
                    case "leadership":
                        skillValue = hero.GetSkillValue(DefaultSkills.Leadership);
                        break;
                    case "roguery":
                        skillValue = hero.GetSkillValue(DefaultSkills.Roguery);
                        break;
                    case "trade":
                        skillValue = hero.GetSkillValue(DefaultSkills.Trade);
                        break;
                    case "steward":
                        skillValue = hero.GetSkillValue(DefaultSkills.Steward);
                        break;
                    case "tactics":
                        skillValue = hero.GetSkillValue(DefaultSkills.Tactics);
                        break;
                    default:
                        // Default to charm for social checks
                        skillValue = hero.GetSkillValue(DefaultSkills.Charm);
                        break;
                }

                // Convert to modifier: every 25 points = +1
                return BaseModifier + (skillValue / SkillPerPoint);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Get modifier from personality traits
        /// </summary>
        public static int GetTraitModifiers(Hero hero, string skill)
        {
            if (hero == null) return 0;

            try
            {
                int modifier = 0;

                switch (skill.ToLower())
                {
                    case "charm":
                        // Generosity helps with charm
                        modifier += hero.GetTraitLevel(DefaultTraits.Generosity);
                        // Honor helps with honest persuasion
                        modifier += hero.GetTraitLevel(DefaultTraits.Honor) / 2;
                        break;

                    case "leadership":
                        // Valor helps with leadership
                        modifier += hero.GetTraitLevel(DefaultTraits.Valor);
                        // Generosity makes troops loyal
                        modifier += hero.GetTraitLevel(DefaultTraits.Generosity);
                        break;

                    case "roguery":
                        // Calculating helps with deception
                        modifier += hero.GetTraitLevel(DefaultTraits.Calculating);
                        // Negative honor helps with lies
                        modifier -= hero.GetTraitLevel(DefaultTraits.Honor) / 2;
                        break;

                    case "intimidation":
                        // Cruelty (negative mercy) helps with intimidation
                        modifier -= hero.GetTraitLevel(DefaultTraits.Mercy);
                        // Valor shows strength
                        modifier += hero.GetTraitLevel(DefaultTraits.Valor);
                        break;
                }

                return modifier;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Get modifier from relationship with NPC
        /// </summary>
        public static int GetRelationModifier(Hero player, Hero npc)
        {
            if (player == null || npc == null) return 0;

            try
            {
                int relation = (int)npc.GetRelationWithPlayer();

                // Convert relation (-100 to 100) to modifier (-3 to +3)
                if (relation >= 50) return 3;
                if (relation >= 20) return 2;
                if (relation >= 0) return 1;
                if (relation >= -20) return 0;
                if (relation >= -50) return -1;
                return -3;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Get modifier from context (war, alliance, same kingdom, etc.)
        /// </summary>
        public static int GetContextModifier(Hero player, Hero npc)
        {
            if (player == null || npc == null) return 0;

            try
            {
                int modifier = 0;

                var playerKingdom = player.Clan?.Kingdom;
                var npcKingdom = npc.Clan?.Kingdom;

                // Same kingdom bonus
                if (playerKingdom != null && npcKingdom != null && playerKingdom == npcKingdom)
                {
                    modifier += 2;
                }
                // At war penalty
                else if (playerKingdom != null && npcKingdom != null && playerKingdom.IsAtWarWith(npcKingdom))
                {
                    modifier -= 3;
                }

                // Same clan bonus
                if (player.Clan != null && npc.Clan != null && player.Clan == npc.Clan)
                {
                    modifier += 3;
                }

                // Same culture bonus
                if (player.Culture != null && npc.Culture != null && player.Culture == npc.Culture)
                {
                    modifier += 1;
                }

                return modifier;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Calculate DC based on NPC traits and context
        /// </summary>
        public static int CalculateDC(Hero npc, string checkType)
        {
            int baseDC = 10;

            if (npc == null) return baseDC;

            try
            {
                switch (checkType.ToLower())
                {
                    case "persuasion":
                    case "charm":
                        // Calculating NPCs are harder to persuade
                        baseDC += npc.GetTraitLevel(DefaultTraits.Calculating) * 2;
                        // Honorable NPCs are easier to persuade honestly
                        baseDC -= npc.GetTraitLevel(DefaultTraits.Honor);
                        break;

                    case "intimidation":
                        // Valorous NPCs are harder to intimidate
                        baseDC += npc.GetTraitLevel(DefaultTraits.Valor) * 2;
                        // Cautious NPCs are easier to intimidate
                        baseDC -= Math.Abs(Math.Min(0, npc.GetTraitLevel(DefaultTraits.Valor)));
                        break;

                    case "deception":
                        // Calculating NPCs see through lies
                        baseDC += npc.GetTraitLevel(DefaultTraits.Calculating) * 2;
                        // Honorable NPCs expect honesty
                        baseDC += npc.GetTraitLevel(DefaultTraits.Honor);
                        break;

                    case "bribe":
                        // Generous NPCs are less interested in bribes
                        baseDC += npc.GetTraitLevel(DefaultTraits.Generosity) * 2;
                        // Greedy (negative generosity) are easier to bribe
                        break;
                }

                // Clamp DC to reasonable range
                return Math.Max(5, Math.Min(25, baseDC));
            }
            catch
            {
                return baseDC;
            }
        }
    }
}

