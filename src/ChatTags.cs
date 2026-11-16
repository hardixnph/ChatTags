using System.Text;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Config;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.UserMessages;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;

namespace ChatTags;

public sealed class ChatTags : BasePlugin, IPluginConfig<ChatTagsConfig>
{
    public override string ModuleName => "ChatTags";
    // Set by the build (-p:Version=..., from the release tag)
    public override string ModuleVersion => typeof(ChatTags).Assembly.GetName().Version!.ToString(3);
    public override string ModuleAuthor => "Hardix";
    public override string ModuleDescription => "Chat and scoreboard tags per admin group, permission or SteamID";

    private const int TeamT = 2;
    private const int TeamCT = 3;
    private const int MaxClanBytes = 31; // m_szClan is char[32]

    public ChatTagsConfig Config { get; set; } = new();

    // Per slot: the player's own clan tag (Steam group) and the tag this plugin put there
    private readonly Dictionary<int, (string Clan, uint ClanId)> _originalClans = new();
    private readonly Dictionary<int, string> _appliedClans = new();

    public void OnConfigParsed(ChatTagsConfig config)
    {
        Config = config;

        var texts = new[] { config.TColor, config.CTColor, config.DeadPrefix, config.TeamChatPrefix }
            .Concat(config.Tags.SelectMany(t => new[] { t.ChatTag, t.NameColor, t.MessageColor }));
        foreach (var problem in texts.SelectMany(ChatColorTags.Problems).Distinct())
            Logger.LogWarning("Config: {Problem}", problem);

        foreach (var tag in config.Tags.Where(t => !IsValidMatch(t.Match)))
            Logger.LogWarning("Config: Match \"{Match}\" is not a SteamID64, #group, @permission or everyone", tag.Match);
    }

    public override void Load(bool hotReload)
    {
        AddCommandListener("say", (p, info) => OnSay(p, info, teamOnly: false));
        AddCommandListener("say_team", (p, info) => OnSay(p, info, teamOnly: true));

        RegisterListener<Listeners.OnClientAuthorized>((slot, _) => ApplyClanTagLater(slot));
        RegisterListener<Listeners.OnClientDisconnect>(slot =>
        {
            _originalClans.Remove(slot);
            _appliedClans.Remove(slot);
        });
        RegisterEventHandler<EventPlayerConnectFull>((e, _) =>
        {
            if (e.Userid is { IsValid: true } p) ApplyClanTagLater(p.Slot);
            return HookResult.Continue;
        });

        // The game puts the Steam group tag back whenever it likes (Steam data arrives late)
        AddTimer(2.0f, () => Players().ForEach(EnforceClanTag), TimerFlags.REPEAT);

        // The game gives teammate colors back continuously; clear them every tick so they don't flicker
        RegisterListener<Listeners.OnTick>(() =>
        {
            if (Config.HideTeammateColors) Players().ForEach(HideTeammateColor);
        });

        // Players already on the server (plugin reload mid-game)
        AddTimer(1.0f, () => Players().ForEach(ApplyClanTag));
    }

    public override void Unload(bool hotReload)
    {
        // Always give players their own tag back; a reload applies ours again
        Players().ForEach(RestoreOriginalClan);
    }

    [ConsoleCommand("css_chattags_reload", "Reload the ChatTags config")]
    [CommandHelper(whoCanExecute: CommandUsage.SERVER_ONLY)]
    public void OnReloadCommand(CCSPlayerController? caller, CommandInfo info)
    {
        OnConfigParsed(ConfigManager.Load<ChatTagsConfig>(Path.GetFileName(ModuleDirectory)));
        Players().ForEach(ApplyClanTag);
        info.ReplyToCommand($"[ChatTags] Config reloaded, {Config.Tags.Count} tags");
    }

    // ------------------------------------------------------------------ chat

    private HookResult OnSay(CCSPlayerController? player, CommandInfo info, bool teamOnly)
    {
        if (player is not { IsValid: true, IsBot: false, IsHLTV: false }) return HookResult.Continue;

        var text = ChatColorTags.StripControl(info.GetArg(1)).Trim();
        if (text.Length == 0 || IsCommand(text)) return HookResult.Continue;

        var tag = FindTag(player);
        if (tag == null || (tag.ChatTag.Length == 0 && tag.NameColor.Length == 0 && tag.MessageColor.Length == 0))
            return HookResult.Continue;

        var recipients = Players()
            .Where(p => !p.IsBot && (!teamOnly || p.TeamNum == player.TeamNum))
            .ToArray();

        // In the player's name the client colors \x03 with the game's own team color, and draws the
        // competitive teammate-color dot only if the player has a teammate color (HideTeammateColors off).
        // Spectators have no team color: plain server chat with the palette colors.
        bool inPlayersName = player.TeamNum is TeamT or TeamCT;
        string teamColor = inPlayersName ? "\x03" : PaletteTeamColor(player.TeamNum);

        var line = new StringBuilder(" ");
        if (!player.PawnIsAlive && player.TeamNum is TeamT or TeamCT)
            line.Append(ChatColorTags.Resolve(Config.DeadPrefix, teamColor));
        if (teamOnly)
            line.Append(ChatColorTags.Resolve(Config.TeamChatPrefix, teamColor));
        line.Append(ChatColorTags.Resolve(tag.ChatTag, teamColor))
            .Append(ChatColorTags.Resolve(tag.NameColor, teamColor))
            .Append(ChatColorTags.StripControl(player.PlayerName))
            .Append(ChatColors.Default)
            .Append(": ")
            .Append(ChatColorTags.Resolve(tag.MessageColor, teamColor))
            .Append(text);

        if (recipients.Length > 0)
        {
            if (inPlayersName) SendInPlayersName(player, line.ToString(), recipients);
            else foreach (var p in recipients) p.PrintToChat(line.ToString());
        }

        return HookResult.Handled;
    }

    private static void SendInPlayersName(CCSPlayerController sender, string line, CCSPlayerController[] recipients)
    {
        using var msg = UserMessage.FromPartialName("SayText2");
        msg.SetUInt("entityindex", sender.Index);
        msg.SetBool("chat", true);
        msg.SetString("messagename", line);
        msg.SetString("param1", "");
        msg.SetString("param2", "");
        msg.SetString("param3", "");
        msg.SetString("param4", "");
        msg.Send(new RecipientFilter(recipients));
    }

    private string PaletteTeamColor(int team) => team switch
    {
        TeamT => ChatColorTags.Resolve(Config.TColor, ""),
        TeamCT => ChatColorTags.Resolve(Config.CTColor, ""),
        _ => ChatColors.Default.ToString(),
    };

    // Chat commands (!rank, /menu) and admin chat (@) pass through untouched
    private static bool IsCommand(string text) =>
        text.StartsWith('@') ||
        CoreConfig.PublicChatTrigger.Concat(CoreConfig.SilentChatTrigger)
            .Any(t => !string.IsNullOrEmpty(t) && text.StartsWith(t, StringComparison.Ordinal));

    // ------------------------------------------------------------------ matching

    private TagEntry? FindTag(CCSPlayerController player)
    {
        if (player.IsBot || player.IsHLTV) return null;

        var steamId = player.AuthorizedSteamID?.SteamId64.ToString();
        return Config.Tags.FirstOrDefault(t => Matches(t.Match, player, steamId));
    }

    private static bool Matches(string match, CCSPlayerController player, string? steamId)
    {
        if (string.IsNullOrWhiteSpace(match)) return false;
        if (match.Equals("everyone", StringComparison.OrdinalIgnoreCase)) return true;
        if (steamId == null) return false; // groups and permissions need an authorized SteamID
        if (match.StartsWith('#')) return AdminManager.PlayerInGroup(player, match);
        if (match.StartsWith('@')) return AdminManager.PlayerHasPermissions(player, match);
        return match == steamId;
    }

    private static bool IsValidMatch(string match) =>
        match.Equals("everyone", StringComparison.OrdinalIgnoreCase) ||
        (match.Length > 1 && match[0] is '#' or '@') ||
        ulong.TryParse(match, out _);

    // ------------------------------------------------------------------ scoreboard

    private void ApplyClanTagLater(int slot)
    {
        AddTimer(2.0f, () =>
        {
            var player = Utilities.GetPlayerFromSlot(slot);
            if (player is { IsValid: true }) ApplyClanTag(player);
        });
    }

    private void ApplyClanTag(CCSPlayerController player)
    {
        if (!player.IsValid || player.IsBot || player.IsHLTV || player.AuthorizedSteamID == null) return;

        SaveOriginalClan(player);

        var tag = TruncateUtf8(FindTag(player)?.Scoreboard ?? "", MaxClanBytes);
        if (tag.Length == 0)
        {
            // No scoreboard tag (any more): the player keeps or gets back their own
            RestoreOriginalClan(player);
            _appliedClans.Remove(player.Slot);
            return;
        }

        _appliedClans[player.Slot] = tag;
        SetClan(player, tag, 0);
    }

    private void EnforceClanTag(CCSPlayerController player)
    {
        if (!player.IsValid || !_appliedClans.TryGetValue(player.Slot, out var tag)) return;

        SaveOriginalClan(player);
        SetClan(player, tag, 0);
    }

    // A clan tag other than ours came from the game: it's the player's own
    private void SaveOriginalClan(CCSPlayerController player)
    {
        if (!_appliedClans.TryGetValue(player.Slot, out var applied) || player.Clan != applied)
            _originalClans[player.Slot] = (player.Clan, player.ClanId32bit);
    }

    private void RestoreOriginalClan(CCSPlayerController player)
    {
        if (!player.IsValid || !_appliedClans.ContainsKey(player.Slot) ||
            !_originalClans.TryGetValue(player.Slot, out var original)) return;

        SetClan(player, original.Clan, original.ClanId);
    }

    // Group id 0 so the text isn't taken for a real Steam group
    private static void SetClan(CCSPlayerController player, string clan, uint clanId)
    {
        if (player.Clan != clan)
        {
            player.Clan = clan;
            Utilities.SetStateChanged(player, "CCSPlayerController", "m_szClan");
        }

        if (player.ClanId32bit != clanId)
        {
            player.ClanId32bit = clanId;
            Utilities.SetStateChanged(player, "CCSPlayerController", "m_unClanId32bit");
        }
    }

    private static string TruncateUtf8(string value, int maxBytes)
    {
        if (Encoding.UTF8.GetByteCount(value) <= maxBytes) return value;

        var result = new StringBuilder();
        int bytes = 0;
        foreach (var rune in value.EnumerateRunes())
        {
            bytes += rune.Utf8SequenceLength;
            if (bytes > maxBytes) break;
            result.Append(rune.ToString());
        }
        return result.ToString();
    }

    // ------------------------------------------------------------------ teammate colors

    private static void HideTeammateColor(CCSPlayerController player)
    {
        if (player.CompTeammateColor < 0) return;
        player.CompTeammateColor = -1;
        Utilities.SetStateChanged(player, "CCSPlayerController", "m_iCompTeammateColor");
    }

    private static List<CCSPlayerController> Players() =>
        Utilities.GetPlayers().Where(p => p is { IsValid: true, IsHLTV: false }).ToList();
}
