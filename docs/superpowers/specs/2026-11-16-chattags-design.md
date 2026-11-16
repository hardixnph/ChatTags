# ChatTags - design

## Intent

Replace CS2-Tags with a small, clean CounterStrikeSharp plugin. CS2-Tags grew redundant paths and settings
that are no longer needed. VIPCore's `VIP_Tag` stays as it is.

## Goals

- Chat prefix per admin group (`#`), permission (`@`), SteamID64, or everyone.
- Names in the game's exact team colors: T/CT chat is sent in the player's name (`SayText2`), so the client
  colors `\x03` itself, like the game's own chat.
- Message in a configurable color.
- Scoreboard tag via the clan-tag field: enforced when Steam resets it, original restored on unload.
- Option to hide competitive teammate colors, which also removes the dot in front of chat messages.
- Tags in `say_team` too, sent to the sender's team only.

## Non-goals

- Gags, VIP integration, menus.

## Anti-goals

- Breaking chat commands (`!rank`, `/menu`) or admin chat (`@`).
- Sending chat in the team entity's name: the client drops those messages.

## Constraints

- CounterStrikeSharp API 1.0.379, `net10.0`, same as the other plugins in this workspace.
- Server chat colors are limited to `ChatColors`. `BlueGrey`/`Silver` (`\x0A`, a line break) are rejected.
- A server message needs a leading space, or a color code at the very start is ignored.
- `m_szClan` holds 31 bytes of UTF-8.

## Approaches

1. New plugin reading the old `tags.json` format: drop-in, but keeps the old loose shape.
2. **New plugin with a typed CounterStrikeSharp config** - picked by the user. Standard
   `configs/plugins/ChatTags/ChatTags.json`, generated on first load, typed fields; tags are re-entered once.
3. Trim CS2-Tags in place: least work, keeps the redundancy the user wants gone.

## Config (`configs/plugins/ChatTags/ChatTags.json`)

```json
{
  "HideTeammateColors": false,
  "TColor": "{GOLD}",
  "CTColor": "{LIGHTBLUE}",
  "DeadPrefix": "{WHITE}☠ {DEFAULT}",
  "TeamChatPrefix": "{DEFAULT}(Team) ",
  "Tags": [
    { "Match": "#css/admin", "ChatTag": "{DARKRED}[Administrator] ", "NameColor": "{TEAMCOLOR}", "MessageColor": "{DEFAULT}", "Scoreboard": "Administrator" },
    { "Match": "@vip/vip",   "ChatTag": "{GOLD}[VIP] ",              "NameColor": "{TEAMCOLOR}", "MessageColor": "{DEFAULT}", "Scoreboard": "VIP" }
  ],
  "ConfigVersion": 1
}
```

- `Tags` is an ordered list; **the first matching entry wins** (explicit priority instead of the old fixed
  SteamID > group > permission order). `Match`: SteamID64, `#group`, `@permission`, or `everyone`.
- Any field may be empty. A player with no match is left alone (game's own chat, own clan tag).
- `{TEAMCOLOR}` is `\x03`, the game's exact team color, for T/CT. Spectators have no team color: their
  messages go through `PrintToChat` with `TColor` / `CTColor` / default. Color names are case-insensitive.
- Bots: no tags.

## Behaviour

- **Chat**: `say` / `say_team` listeners. Skip when empty, when the message starts with a CounterStrikeSharp
  chat trigger (core config) or `@`, or when the player matches no tag. Otherwise build
  ` {dead}{teamchat}{ChatTag}{NameColor}{name}{DEFAULT}: {MessageColor}{text}` and send it to everyone
  (`say`) or the sender's team (`say_team`), then return `Handled`:
  - T/CT: `SayText2` with `entityindex` = sender, `chat` = true, the line as `messagename`, empty params;
  - spectators: `PrintToChat` with the palette colors.
- **Scoreboard**: apply on authorize and on full connect (+2 s), re-apply every 2 s if the game reset it,
  restore the saved original on unload. Truncate to 31 UTF-8 bytes without splitting a character.
- **Teammate colors**: only when `HideTeammateColors` is on, every tick set `m_iCompTeammateColor` to -1.
- **Command**: `css_chattags_reload` (server console) re-reads the config and re-applies scoreboard tags.

## Left to the builder

- Verify in game that a gagged player stays silent: listener order with the server's admin/gag plugin.
- Retire CS2-Tags on the server when ChatTags is deployed (both on = double messages).
