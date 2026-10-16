# AGENTS.md

## Project Overview

Dynamic Mini Player is a Windows 11 desktop mini-player built with .NET 10, WPF, and Windows Forms. It reads media sessions exposed by Windows and provides playback controls, progress, artwork, a system-tray menu, optional startup, local preferences, and single-instance behavior.

The application is intentionally local-only: it has no accounts, telemetry, browser extensions, or application-owned Internet connections.

## Repository Layout

- `src/DynamicMiniPlayer/` — the main WPF application.
  - `ViewModels/` — observable state and commands used by the UI.
  - `Services/` — Windows integration, persistence, startup, tray, window placement, and player-state services.
  - `Models/` — shared media/session models.
  - `Assets/` — application icons and other packaged assets.
- `tests/DynamicMiniPlayer.Tests/` — MSTest unit tests.
- `scripts/` — build and publishing helpers.
- `artifacts/` — generated build/publish output; do not edit manually.
- `DynamicMiniPlayer.slnx` — solution file.
- `Directory.Build.props` — shared compiler settings.

## Environment and Prerequisites

- Windows 11 x64 is required for running the application and for Windows media/session integration.
- Install the .NET 10 SDK.
- The project targets `net10.0-windows10.0.19041.0` and uses WPF plus Windows Forms.
- Do not assume that browser-specific APIs or non-Windows environments can exercise the full application behavior.

## Standard Commands

Run these commands from the repository root:

```powershell
dotnet restore DynamicMiniPlayer.slnx
dotnet build DynamicMiniPlayer.slnx
dotnet test DynamicMiniPlayer.slnx
dotnet run --project src/DynamicMiniPlayer/DynamicMiniPlayer.csproj
```

To create a self-contained Windows x64 build:

```powershell
.\scripts\publish.ps1
```

Published output is written to `artifacts\publish\win-x64`.

## Development Guidelines

- Keep nullable reference types enabled and preserve the shared settings in `Directory.Build.props`.
- Follow the existing MVVM structure: keep presentation state and commands in `ViewModels`, platform integration in `Services`, and visual/layout concerns in XAML and code-behind.
- Prefer small, focused services and interfaces for Windows-specific behavior so logic can remain unit-testable.
- Preserve the existing single-instance, tray-icon, startup, window-placement, and local-settings behavior unless a change explicitly targets one of those areas.
- Treat media-session data as optional and incomplete. Controls must remain correctly enabled or disabled based on the capabilities reported by the source session.
- Avoid adding network access, telemetry, accounts, or browser extensions without an explicit product requirement.
- Keep user-facing text and accessibility behavior consistent with the existing UI.
- Add or update unit tests for changes to services, state transitions, progress estimation, settings persistence, or view-model behavior.

## Testing and Validation

- Run `dotnet test DynamicMiniPlayer.slnx` after code changes.
- Run `dotnet build DynamicMiniPlayer.slnx` when changing project files, XAML, assets, manifests, or platform integration.
- For UI or Windows integration changes, manually launch the app on Windows 11 and verify the relevant media-session, tray, startup, window, and single-instance behavior.
- Do not treat files under `bin/`, `obj/`, or `artifacts/` as source changes. Regenerate them with the appropriate build or publish command.

## Branching

- `main` holds stable releases; do not commit to it directly.
- `develop` is the integration branch. Create feature branches from `develop` and merge them back into it.
- Write documentation, code comments, and commit messages in English. User-facing UI text is currently in Spanish; keep it consistent unless a task targets localization.

## Change Scope

- Make the smallest change that satisfies the request.
- Do not rewrite unrelated code or reformat entire files without a clear reason.
- Preserve existing public behavior and compatibility with Windows 11 unless the task explicitly changes the product requirements.
- Before finishing, review the diff and report the validation commands that were run and any Windows-only checks that could not be performed.
