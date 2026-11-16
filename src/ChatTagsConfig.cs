using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;

namespace ChatTags;

public sealed class ChatTagsConfig : BasePluginConfig
{
    [JsonPropertyName("ConfigVersion")] public override int Version { get; set; } = 1;

    // Competitive teammate colors (circles over heads, radar, scoreboard, dot in front of chat messages) forced off.
    // Names keep the game's exact team colors either way.
    [JsonPropertyName("HideTeammateColors")] public bool HideTeammateColors { get; set; } = false;

    // {TEAMCOLOR} for players not on T or CT (spectators), whose messages can't carry a team color.
    // The game's exact team colors aren't in the server chat palette; these are the closest.
    [JsonPropertyName("TColor")] public string TColor { get; set; } = "{GOLD}";
    [JsonPropertyName("CTColor")] public string CTColor { get; set; } = "{LIGHTBLUE}";

    // In front of a dead player's message. Empty = nothing.
    [JsonPropertyName("DeadPrefix")] public string DeadPrefix { get; set; } = "{WHITE}☠ {DEFAULT}";

    // In front of a say_team message. Empty = nothing.
    [JsonPropertyName("TeamChatPrefix")] public string TeamChatPrefix { get; set; } = "{DEFAULT}(Team) ";

    // First matching entry wins. Match: SteamID64, "#group", "@permission" or "everyone".
    [JsonPropertyName("Tags")] public List<TagEntry> Tags { get; set; } =
    [
        new() { Match = "#css/admin", ChatTag = "{DARKRED}[Administrator] ", Scoreboard = "Administrator" },
        new() { Match = "@vip/vip", ChatTag = "{GOLD}[VIP] ", Scoreboard = "VIP" },
    ];
}

public sealed class TagEntry
{
    [JsonPropertyName("Match")] public string Match { get; set; } = "";

    // Before the name. Empty = no chat tag.
    [JsonPropertyName("ChatTag")] public string ChatTag { get; set; } = "";

    [JsonPropertyName("NameColor")] public string NameColor { get; set; } = "{TEAMCOLOR}";

    [JsonPropertyName("MessageColor")] public string MessageColor { get; set; } = "{DEFAULT}";

    // Clan tag shown next to the name on the scoreboard and kill feed (max 31 bytes). Empty = player's own.
    [JsonPropertyName("Scoreboard")] public string Scoreboard { get; set; } = "";
}
