# ChatMix

A lightweight Windows 11 tray app that mixes **voice chat** volume against **everything else**,
controlled entirely by global hotkeys — built so an Elgato Stream Deck can drive it using its
built-in **Hotkey** system action, with no plugin required.

ChatMix controls real Windows audio sessions directly via the Core Audio API (NAudio's
`AudioSessionManager` / `SimpleAudioVolume`). No virtual audio cable, no Voicemeeter.

## Features

- **Two volume groups**
  - **Chat** — any active audio session whose process matches a configurable list (default: `discord.exe`)
  - **Everything Else** — every other active audio session
- **Global hotkeys** (default bindings use F13–F18 — unlabeled keys with no physical key and no
  conflicts with other software, which is exactly why they're a popular Stream Deck choice):
  | Action | Default key |
  |---|---|
  | Chat Volume Up | F13 |
  | Chat Volume Down | F14 |
  | Everything Volume Up | F15 |
  | Everything Volume Down | F16 |
  | Toggle Mute Chat | F17 |
  | Toggle Duck Chat (drop chat to a low preset, press again to restore) | F18 |
- Volume changes apply to **every** matching session in a group simultaneously (e.g. all of
  Discord's audio sessions move together).
- **Live re-detection** — sessions are re-scanned periodically, so relaunching Discord or opening
  a new game mid-session is picked up automatically with no restart.
- **System tray icon** showing current Chat / Everything Else volume, with a Settings window for
  editing the chat process list, hotkeys, and step size, plus a "Start with Windows" toggle.
- **On-screen overlay** that briefly shows the new volume after a hotkey press, then auto-hides.
- Settings are stored as plain JSON at `%AppData%\ChatMix\settings.json`.
- No telemetry, no unnecessary dependencies (just NAudio + standard .NET/WPF).

## Project layout

```
ChatMix.csproj        Project file (net8.0-windows, WPF + WinForms)
App.xaml / App.xaml.cs  App entry point, wires everything together
Models/                Settings, hotkey binding, and action data types
Services/              AudioSessionService, HotkeyService, SettingsService, StartupService
UI/                    Tray icon, on-screen overlay, settings window
```

## Building

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
dotnet build
```

## Publishing a standalone .exe

This produces a single self-contained `ChatMix.exe` that runs on a machine with no .NET install:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

The finished executable lands in `publish\ChatMix.exe`.

## Running at Windows startup

Right-click the tray icon and check **"Start with Windows."** This writes a per-user
`HKCU\Software\Microsoft\Windows\CurrentVersion\Run` entry — no admin rights required.

## Setting up an Elgato Stream Deck

No plugin needed — ChatMix's hotkeys are plain system-wide `RegisterHotKey` bindings, so the
Stream Deck's built-in **Hotkey** action can trigger them directly:

1. Drag the **Hotkey** action (under *System*) onto a button in the Stream Deck app.
2. Click the down-arrow next to the hotkey field — this opens a key list that includes **F13–F24**,
   which aren't on a physical keyboard but are selectable directly here.
3. Pick the key matching the action you want (see the default bindings table above).
4. Repeat for each action you want on a button. If you have fewer physical keys than actions,
   consider dropping **Toggle Mute Chat** — **Toggle Duck Chat** already covers the common
   "quiet Discord fast" case — or use a Stream Deck folder/page for a second layer.

Bindings can be changed any time from ChatMix's tray menu → **Settings → Hotkeys** (click a box,
press the new combo, Escape clears it) — just remember to update the matching Stream Deck button too.
