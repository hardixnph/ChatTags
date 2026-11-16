using System.Reflection;
using System.Text.RegularExpressions;
using CounterStrikeSharp.API.Modules.Utils;

namespace ChatTags;

/// <summary>{GREEN}-style color names from <see cref="ChatColors"/>, plus {TEAMCOLOR}.</summary>
public static partial class ChatColorTags
{
    // '\x0A' (BlueGrey / Silver) is a line break, not a usable color
    private const char LineBreak = '\x0A';

    private static readonly Dictionary<string, char> Colors = typeof(ChatColors)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.FieldType == typeof(char))
        .GroupBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(g => g.Key, g => (char)g.First().GetValue(null)!, StringComparer.OrdinalIgnoreCase);

    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex TagPattern();

    /// <summary>Replaces color names. Unknown names stay as typed so a typo shows up in chat.</summary>
    public static string Resolve(string text, string teamColor)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains('{')) return text;

        return TagPattern().Replace(text, m =>
        {
            var name = m.Groups[1].Value;
            if (name.Equals("TEAMCOLOR", StringComparison.OrdinalIgnoreCase)) return teamColor;
            if (!Colors.TryGetValue(name, out var color)) return m.Value;
            return color == LineBreak ? "" : color.ToString();
        });
    }

    /// <summary>Names in <paramref name="text"/> that are not colors or can't be used, for config warnings.</summary>
    public static IEnumerable<string> Problems(string text)
    {
        if (string.IsNullOrEmpty(text)) yield break;

        foreach (Match m in TagPattern().Matches(text))
        {
            var name = m.Groups[1].Value;
            if (name.Equals("TEAMCOLOR", StringComparison.OrdinalIgnoreCase)) continue;
            if (!Colors.TryGetValue(name, out var color)) yield return $"{m.Value} is not a color";
            else if (color == LineBreak) yield return $"{m.Value} is a line break in chat, ignored";
        }
    }

    /// <summary>Removes control characters (color codes, line breaks) from player-typed text.</summary>
    public static string StripControl(string text) =>
        string.Concat(text.Where(c => !char.IsControl(c)));
}
