using System.Text;

namespace MyGame.Common
{
    public static class StatTooltipLibrary
    {
        private enum Influence
        {
            Slight,
            Improve,
            Great,
        }

        public static string GetBaseStatTooltip(BaseStatType stat)
        {
            // Keep this intentionally general so it stays accurate even if you tweak formulas.
            // Wording rules:
            // - Greatly improves: if this stat is the ONLY base stat contributing to an advanced stat.
            // - Improves: if this stat is the higher contributor in a combo.
            // - Slightly improves: if this stat is the lower contributor in a combo.

            var sb = new StringBuilder();

            switch (stat)
            {
                case BaseStatType.Strength:
                    sb.AppendLine("Strength");
                    Append(sb, Influence.Slight, "Max HP");
                    Append(sb, Influence.Improve, "Attack Power");
                    Append(sb, Influence.Slight, "Physical Defense");
                    break;

                case BaseStatType.Agility:
                    sb.AppendLine("Agility");
                    Append(sb, Influence.Great, "Attack Speed");
                    Append(sb, Influence.Slight, "Cast Speed");
                    Append(sb, Influence.Improve, "Evasion");
                    Append(sb, Influence.Slight, "Attack Power");
                    break;

                case BaseStatType.Intelligence:
                    sb.AppendLine("Intelligence");
                    Append(sb, Influence.Improve, "Max Mana");
                    Append(sb, Influence.Great, "Magic Power");
                    Append(sb, Influence.Improve, "Cast Speed");
                    Append(sb, Influence.Slight, "Magical Defense");
                    break;

                case BaseStatType.Endurance:
                    sb.AppendLine("Endurance");
                    Append(sb, Influence.Improve, "Max HP");
                    Append(sb, Influence.Improve, "Physical Defense");
                    Append(sb, Influence.Improve, "Magical Defense");
                    Append(sb, Influence.Slight, "Out-of-combat HP/Mana regeneration");
                    break;

                case BaseStatType.Spirit:
                    sb.AppendLine("Spirit");
                    Append(sb, Influence.Improve, "Max Mana");
                    Append(sb, Influence.Great, "Accuracy");
                    Append(sb, Influence.Slight, "Evasion");
                    Append(sb, Influence.Improve, "Out-of-combat HP/Mana regeneration");
                    break;

                default:
                    return stat.ToString();
            }

            sb.AppendLine();
            sb.Append("Scaling depends on level/tier.");

            return sb.ToString();
        }

        private static void Append(StringBuilder sb, Influence influence, string what)
        {
            sb.Append("- ");

            switch (influence)
            {
                case Influence.Slight:
                    sb.Append("Slightly improves ");
                    break;
                case Influence.Improve:
                    sb.Append("Improves ");
                    break;
                case Influence.Great:
                    sb.Append("Greatly improves ");
                    break;
            }

            sb.AppendLine(what);
        }

        public static string GetBaseStatTooltip(string statRowId)
        {
            if (string.IsNullOrWhiteSpace(statRowId))
                return string.Empty;

            // Supports StatsPanel row ids: Str/Agi/Int/End/Spr
            return statRowId switch
            {
                "Str" => GetBaseStatTooltip(BaseStatType.Strength),
                "Agi" => GetBaseStatTooltip(BaseStatType.Agility),
                "Int" => GetBaseStatTooltip(BaseStatType.Intelligence),
                "End" => GetBaseStatTooltip(BaseStatType.Endurance),
                "Spr" => GetBaseStatTooltip(BaseStatType.Spirit),
                _ => string.Empty,
            };
        }
    }
}
