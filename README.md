# Dynamic Mini Player

A universal mini player for Windows 11. It reads the media session that Windows receives from Chrome, Edge, Firefox, Spotify, and other compatible apps, and lets you control playback without switching windows.

## Features

- Compact card above the taskbar that opens on demand from the system tray and hides when you click outside it.
- Optional persistent mode that shows the card automatically when something starts playing.
- Artwork, title, artist, and a progress bar that supports click and drag seeking.
- Previous, play/pause, next, and skip back/forward 10 seconds controls.
- Automatic session selection by Windows, plus a manual picker for switching between sessions.
- System tray icon and menu.
- Optional launch at startup, local preferences, and single-instance behavior.
- No accounts, no telemetry, and no app-owned Internet connections.

## Usage

- **Left-click** the tray icon to show or hide the player.
- **Right-click** the tray icon for display mode, media source, launch at startup, settings, and exit.

| Shortcut | Action |
| --- | --- |
| `Space` | Play / pause |
| `←` / `→` | Skip back / forward 10 s |
| `Esc` | Hide the player |

> The app's user interface is currently in Spanish.

## Development

Requirements: Windows 11 x64 and the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
dotnet restore DynamicMiniPlayer.slnx
dotnet build DynamicMiniPlayer.slnx
dotnet test DynamicMiniPlayer.slnx
dotnet run --project src/DynamicMiniPlayer/DynamicMiniPlayer.csproj
```

Repository layout:

- `src/DynamicMiniPlayer/` — WPF application (`ViewModels`, `Services`, `Models`, `Assets`).
- `tests/DynamicMiniPlayer.Tests/` — MSTest unit tests.
- `scripts/` — build and publishing helpers.

To create a self-contained build:

```powershell
.\scripts\publish.ps1
```

The executable is written to `artifacts\publish\win-x64`. To pin it, run it and use **right-click → Pin to taskbar** on its icon. Windows does not allow an app to pin itself permanently without user confirmation.

## Branches

- `main` — stable releases.
- `develop` — ongoing development; open pull requests against this branch.

## Compatibility notes

The information and controls available depend on what each source publishes to the Windows media controls. If a page does not allow skipping, going back, or seeking, the corresponding control is disabled. No browser extensions are required.

## License

Distributed under the MIT License. See [LICENSE](LICENSE).
