# Clawd

A desktop pet crab that lives on your screen. Built with C# and Avalonia.

Clawd walks along the bottom of your screen, can be dragged around, wears hats, dances, and even chats with you via Claude CLI.

## Features

- **18 animation states** — walking, sleeping, dancing, jumping, detective mode, and more
- **Drag & drop** — pick up the crab and place it anywhere on screen
- **Hat system** — right-click to cycle through hats (top hat, crown, party hat, cowboy, etc.)
- **Friend crab** — toggle a buddy crab to keep Clawd company
- **Chat with Clawd** — integrated chat window powered by Claude CLI (requires `claude` installed)
- **System tray** — feed, pet, dance, change hat, toggle friend, or quit from the tray
- **Pixel art** — hand-crafted pixel sprites with smooth animations
- **Cross-platform** — runs on macOS, Linux, and Windows

## Requirements

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Claude CLI](https://github.com/anthropics/claude-code) (optional, for chat feature)

## Run

```bash
dotnet run --project src/Clawd/Clawd.csproj
```

## Build

```bash
dotnet build src/Clawd/Clawd.csproj
```

## Publish (single-file)

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
| Right-click | Context menu (feed, pet, dance, hat, friend, chat) |
| Drag | Pick up and place the crab anywhere |
| Double-click | Pet the crab |
| Tray icon | Access all actions from system tray |

## Chat

Right-click and select "Chat with Clawd" to open a chat window. Clawd uses the Claude CLI under the hood — it can help with questions, research, coding, and more. Press Enter to send, Shift+Enter for newlines, Escape to close.

## License

GPL-3.0 — see [LICENSE](LICENSE)
