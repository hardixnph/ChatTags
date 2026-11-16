# ChatTags

[![Build & Release](https://github.com/hardixnph/ChatTags/actions/workflows/release.yml/badge.svg)](https://github.com/hardixnph/ChatTags/actions/workflows/release.yml)
[![Latest release](https://img.shields.io/github/v/release/hardixnph/ChatTags)](https://github.com/hardixnph/ChatTags/releases/latest)
[![License: GPL v3](https://img.shields.io/badge/license-GPLv3-blue.svg)](LICENSE)

A lightweight [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp) plugin for CS2 that gives
players chat tags and scoreboard tags based on their admin group, permission or SteamID.

```
[Administrator] hardix: gg
```

## Features

- **Chat tags** per admin group, permission, SteamID64 or everyone. The first matching entry wins.
- **Exact team colors**: names use the game's own T / CT colors, the same as normal in-game chat.
- **Scoreboard tags** next to the name, on the scoreboard and in the kill feed. The tag is re-applied when
  Steam resets it, and the player's own clan tag comes back when the plugin unloads.
- **Team chat** with a configurable `(Team)` prefix, sent to the sender's team only.
- **Optional hiding of competitive teammate colors** (circles over heads, radar, scoreboard, and the dot in
  front of chat messages).
- Chat commands (`!`, `/`) and admin chat (`@`) are never touched.
- Typos in color names are reported in the server log when the config loads.

## Requirements

- CounterStrikeSharp **v1.0.379** or newer

## Installation

1. Download the latest `ChatTags-vX.Y.Z.zip` from [Releases](https://github.com/hardixnph/ChatTags/releases/latest).
2. Extract it into `game/csgo/`. The plugin lands in `addons/counterstrikesharp/plugins/ChatTags/`.
3. Restart the server or change map. The config is created at
   `addons/counterstrikesharp/configs/plugins/ChatTags/ChatTags.json`.

> **Migrating from CS2-Tags:** remove CS2-Tags from the server, otherwise every message is shown twice.

## Configuration

```json
{
  "HideTeammateColors": false,
  "TColor": "{GOLD}",
  "CTColor": "{LIGHTBLUE}",
  "DeadPrefix": "{WHITE}☠ {DEFAULT}",
  "TeamChatPrefix": "{DEFAULT}(Team) ",
  "Tags": [
    {
      "Match": "#css/admin",
      "ChatTag": "{DARKRED}[Administrator] ",
      "NameColor": "{TEAMCOLOR}",
      "MessageColor": "{DEFAULT}",
      "Scoreboard": "Administrator"
    },
    {
      "Match": "@vip/vip",
      "ChatTag": "{GOLD}[VIP] ",
      "NameColor": "{TEAMCOLOR}",
      "MessageColor": "{DEFAULT}",
      "Scoreboard": "VIP"
    }
  ],
  "ConfigVersion": 1
}
```

### General

| Setting | Description |
|---|---|
| `HideTeammateColors` | Removes the competitive teammate colors, including the dot in front of chat messages. Names keep their team colors. |
| `TColor` / `CTColor` | Color of `{TEAMCOLOR}` for spectators, who have no team color of their own. |
| `DeadPrefix` | Shown in front of a dead player's message. Empty to disable. |
| `TeamChatPrefix` | Shown in front of team chat messages. Empty to disable. |

### Tags

Entries are checked from top to bottom and **the first match wins**, so put the most specific ones first.
A player who matches nothing keeps the normal game chat and their own clan tag.

| Field | Description |
|---|---|
| `Match` | A SteamID64 (`76561198000000000`), an admin group (`#css/admin`), a permission (`@vip/vip`) or `everyone`. |
| `ChatTag` | Text in front of the name. Empty for no chat tag. |
| `NameColor` | Color of the name. `{TEAMCOLOR}` uses the game's team color. |
| `MessageColor` | Color of the message. |
| `Scoreboard` | Clan tag on the scoreboard and kill feed, max 31 bytes. Empty to keep the player's own. |

### Colors

`{DEFAULT}` `{WHITE}` `{DARKRED}` `{RED}` `{LIGHTRED}` `{GREEN}` `{LIME}` `{OLIVE}` `{YELLOW}` `{LIGHTYELLOW}`
`{GOLD}` `{ORANGE}` `{LIGHTBLUE}` `{BLUE}` `{DARKBLUE}` `{PURPLE}` `{LIGHTPURPLE}` `{MAGENTA}` `{GREY}`
and `{TEAMCOLOR}`. Names are case-insensitive.

`{BLUEGREY}` and `{SILVER}` are ignored: in chat, their code is a line break.

## Commands

| Command | Who | Description |
|---|---|---|
| `css_chattags_reload` | Server console | Reloads the config and re-applies scoreboard tags. |

## Building

```bash
dotnet build src/ChatTags.csproj -c Release
```

The plugin is written to `src/bin/Release/net10.0/ChatTags.dll`.

## License

[GPL-3.0](LICENSE)
