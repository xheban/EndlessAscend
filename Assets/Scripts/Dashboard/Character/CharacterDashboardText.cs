using System.Text;
using MyGame.Common;
using UnityEngine;

public static class CharacterDashboardText
{
    public static string FormatModValue(ModOp op, float value)
    {
        switch (op)
        {
            case ModOp.Flat:
            {
                var v = Mathf.RoundToInt(value);
                if (Mathf.Abs(value - v) < 0.001f)
                    return (v > 0 ? "+" : "") + v;
                return (value > 0 ? "+" : "") + value.ToString("0.##");
            }

            case ModOp.Percent:
            {
                var v = Mathf.RoundToInt(value);
                if (Mathf.Abs(value - v) < 0.001f)
                    return (v > 0 ? "+" : "") + v + "%";
                return (value > 0 ? "+" : "") + value.ToString("0.##") + "%";
            }

            default:
                return value.ToString("0.##");
        }
    }

    public static string NiceEnum(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return raw;

        var sb = new StringBuilder(raw.Length + 8);
        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];
            if (i > 0 && char.IsUpper(c) && char.IsLower(raw[i - 1]))
                sb.Append(' ');
            sb.Append(c);
        }

        return sb.ToString().Replace("Hp", "HP");
    }
}
