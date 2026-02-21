# Clawd

A desktop pet crab that lives on your screen. Built with C# and Avalonia.

Clawd walks along the bottom of your screen, can be dragged around, wears hats, dances, reacts to weather, monitors your system, and even chats with you via Claude CLI.

## Features

- **21 animation states** — walking, sleeping, dancing, jumping, detective mode, edge walking, peeking, hiding, and more
- **Drag & drop** — pick up the crab and place it anywhere on screen
- **Hat system** — cycle through hats (top hat, crown, party hat, cowboy, beret) + seasonal hats (Santa, Pumpkin, Bunny, Witch)
- **Friend crab** — toggle a buddy crab to keep Clawd company
- **Chat with Clawd** — integrated chat window powered by Claude CLI with markdown rendering
- **Weather** — opt-in weather via Open-Meteo (no API key needed). Crab wears umbrella in rain, sunglasses in sun, scarf in snow
- **Mood system** — Clawd's mood changes based on how you interact (Happy, Content, Neutral, Hungry, Sad, Lonely)
- **Pomodoro timer** — focus/break timer with celebration on completion
- **System monitor** — subtle visual hints for CPU/RAM usage (sweat drops, blush)
- **Cheeky mode** — crab walks on screen edges, peeks from sides, and hides (toggle in settings)
- **Multi-monitor** — crab can walk between screens
- **Reminders** — "remind me in 5 min to stretch" in chat
- **Clipboard summary** — right-click to summarize clipboard contents via Claude
- **Seasonal hats** — auto-selected based on the month (Santa in December, Pumpkin in October, etc.)
- **System tray** — feed, pet, dance, change hat, toggle friend, settings, or quit from the tray
- **Pixel art** — hand-crafted pixel sprites with smooth animations
- **Cross-platform** — runs on macOS, Linux, and Windows

## Download

Grab the latest release from [Releases](https://github.com/era6-6-6/Clawd/releases). Available for:
- **macOS** (Apple Silicon & Intel)
- **Linux** (x64)
- **Windows** (x64)

## Build from source

### Requirements

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Claude CLI](https://github.com/anthropics/claude-code) (optional, for chat feature)

### Run

```bash
dotnet run --project src/Clawd/Clawd.csproj
```

### Build

```bash
dotnet build src/Clawd/Clawd.csproj
```

### Publish (single-file)

```bash
# macOS (Apple Silicon)
dotnet publish src/Clawd/Clawd.csproj -r osx-arm64 --self-contained -p:PublishSingleFile=true

# macOS (Intel)
dotnet publish src/Clawd/Clawd.csproj -r osx-x64 --self-contained -p:PublishSingleFile=true

# Linux
dotnet publish src/Clawd/Clawd.csproj -r linux-x64 --self-contained -p:PublishSingleFile=true

# Windows
dotnet publish src/Clawd/Clawd.csproj -r win-x64 --self-contained -p:PublishSingleFile=true
```

## Controls

| Action | Effect |
|--------|--------|
| Right-click | Context menu (feed, pet, dance, hat, friend, chat, pomodoro, clipboard) |
| Drag | Pick up and place the crab anywhere |
| Double-click | Pet the crab |
| Tray icon | Access all actions + settings from system tray |

## Chat

Right-click and select "Chat with Clawd" to open a chat window. Clawd uses the Claude CLI under the hood — it can help with questions, research, coding, and more. Supports markdown rendering (code blocks, bold, headers, bullet points). Press Enter to send, Shift+Enter for newlines, Escape to close.

**Chat commands:**
- `summarize clipboard` — summarize whatever is on your clipboard
- `remind me in X min/hours to ...` — set a reminder

## Settings

Open from the tray icon. Configure:
- **Weather city** — enter your city to enable weather accessories
- **Cheeky mode** — toggle edge walking, peeking, and hiding behaviors

## License

GPL-3.0 — see [LICENSE](LICENSE)
