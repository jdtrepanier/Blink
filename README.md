# Blink 👁️

A tiny WPF tray app that reminds you to rest your eyes. Every _N_ minutes it
blanks **every monitor** with a calm full-screen overlay for a short break, then
gets out of your way.

## Features

- Lives in the notification tray (sleeping-eye icon, drawn at runtime — no asset files).
- Covers **all monitors** with a topmost overlay during a break (mixed-DPI aware).
- Configurable **break interval** and **break length** via spin (up/down) controls.
- **Multi-language UI**: English, Français, Español (or follow the OS).
- **Rest now** (or double-click the tray icon) to take a break on demand.
- **Pause / Resume** from the tray menu.
- Optional **skip** (Esc or click) so a break never traps you.
- **Start with Windows** (per-user, no admin required).
- Settings persist to `%AppData%\Blink\settings.json`.

Defaults: a **1-minute** break every **30 minutes**.

## Build & run

Requires the .NET 10 SDK.

```bash
dotnet build -c Release
./bin/Release/net10.0-windows/Blink.exe
```

Or publish a self-contained single file:

```bash
dotnet publish -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

## Tray menu

| Item          | Action                                            |
| ------------- | ------------------------------------------------- |
| Pause/Resume  | Stop or restart the schedule                      |
| Rest now      | Start a break immediately                         |
| Settings...   | Interval, break length, language, skip, auto-start |
| Exit          | Quit                                              |

## Localization

UI strings live in `Resources/Strings.resx` (English, neutral),
`Strings.fr.resx`, and `Strings.es.resx`; the build produces `fr/` and `es/`
satellite assemblies. Pick a language in **Settings** (the tray menu, tooltip,
and any subsequently opened window update immediately). To add a language, drop
in a `Strings.<culture>.resx` and add it to the language list in
`SettingsWindow.xaml.cs`.

## Project layout

- `App.xaml(.cs)` — tray icon, scheduling, break orchestration, culture setup.
- `OverlayWindow.xaml(.cs)` — the blank per-monitor break overlay + countdown.
- `SettingsWindow.xaml(.cs)` — options dialog.
- `NumericUpDown.xaml(.cs)` — themed spin control for the numeric settings.
- `AppSettings.cs` — settings model + JSON persistence.
- `StartupManager.cs` — start-with-Windows via the HKCU Run key.
- `IconFactory.cs` — draws the sleeping-face artwork (round face, closed eyes,
  rising "zZz"); the single source for both the tray icon and `app.ico`.
- `app.ico` — the executable/window icon (multi-resolution: 16–256px).
  Regenerate it from the same artwork with: `Blink.exe --export-icon app.ico`
  (then rebuild so the exe re-embeds it).
- `Strings.cs` + `Resources/Strings*.resx` — localized text.
