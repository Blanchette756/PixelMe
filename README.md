<p align="center">
  <img src="src/PixelMe/Assets/PixelMe.ico" width="96" alt="PixelMe logo" />
</p>

<h1 align="center">PixelMe</h1>

<p align="center">
  A free and open-source dynamic island for Windows.<br/>
  <i>Island dinamis untuk Windows — gratis dan open source.</i>
</p>

---

PixelMe adds a small, animated island to the top of your screen. Click it and it opens into a card with your music, clipboard, files, reminders, widgets and more (prefer hover? turn it on in Settings). It is built to stay light: when nothing is happening it uses **0% CPU**.

## Features

| | Feature | Notes |
|---|---|---|
| ✅ | Animated island, notch or floating style | Spring animations, real background blur on the Glass theme |
| ✅ | Themes: Black, Glass, Graphite, Light, Wallpaper | Real acrylic blur; Wallpaper theme takes its colours from your wallpaper |
| ✅ | Volume and charging pop-ups | The island shows a volume bar when you use the volume keys |
| ✅ | Dock (taskbar replacement) | Optional. Pinned and open apps, Start, quick settings, clock. The Windows taskbar comes back automatically |
| ✅ | Update check | Tells you when a new version is on GitHub |
| ✅ | Music from any app | Album art, colours from the cover, wavy progress bar, seek, volume |
| ✅ | Synced lyrics | Optional, uses [lrclib.net](https://lrclib.net) |
| ✅ | Clipboard history | Text, links, images and files, with pinning |
| ✅ | File shelf | Drop files on the island, drag them out into any app |
| ✅ | Send to phone | QR code, same Wi-Fi, works only while the code is on screen |
| ✅ | Reminders and quick timers | Snooze, done, repeat |
| ✅ | One-key microphone mute | `Ctrl+Alt+M` in every app, call timer while the mic is in use |
| ✅ | Quick controls | Volume, brightness, Wi-Fi, Bluetooth, power mode. Night light and Focus open Windows Settings |
| ✅ | Widgets | Clock, world clock, weather, battery, stopwatch, PC stats, to-do, photos |
| ✅ | Notifications | Optional; Windows must allow PixelMe to read notifications |
| ✅ | AI agent monitor | Alerts when Claude Code or Codex needs your input |
| ✅ | Game strip | CPU, GPU and RAM while a fullscreen app is open |
| ✅ | Web drawer | Pinned websites in a drawer under the island |
| ✅ | Stream tools | Twitch / YouTube live chat drawer, OBS recording indicator |
| ✅ | Installer | Per-user, no admin rights needed |
| 🚧 | FPS and temperatures in the game strip | Need extra drivers or admin rights |
| 🚧 | Pin widgets to the desktop | Planned |
| 🚧 | Tray icons in the dock | Planned |

If the Windows taskbar ever stays hidden after using the dock, run `PixelMe.exe --restore-taskbar` or simply start PixelMe again with the dock turned off.

## Install

Download `PixelMe-Setup-x.y.z.exe` from [Releases](https://github.com/Blanchette756/PixelMe/releases) and run it.

Windows SmartScreen may warn you because the installer is not code-signed. Click **More info → Run anyway**.

**Requirements:** Windows 10 version 2004 (build 19041) or newer, or Windows 11, 64-bit.

## AI agent alerts

PixelMe can tell you when a coding agent is waiting for you. Open the island, go to the **AI agents** tab and copy the snippet for your agent.

- **Claude Code:** paste the copied `hooks` block into `~/.claude/settings.json`.
- **Codex:** paste the copied `notify = [...]` line into `~/.codex/config.toml`.

Any other tool can send a signal too:

```
PixelMe.exe --agent "My Agent" --state waiting --message "Needs approval" --project my-repo
```

`--state` can be `waiting`, `working` or `done`.

## Releasing a new version

Push a version tag and GitHub Actions builds the installer and a portable zip, then publishes the release with the notes from `RELEASE_NOTES.md`:

```
git tag v1.0.0
git push origin v1.0.0
```

## Building from source

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
2. Clone and run:
   ```
   git clone https://github.com/Blanchette756/PixelMe.git
   cd PixelMe
   dotnet run --project src/PixelMe
   ```
3. Build the installer (needs [Inno Setup 6](https://jrsoftware.org/isinfo.php)):
   ```
   powershell -ExecutionPolicy Bypass -File tools/publish.ps1
   ```
   The installer is written to `artifacts/`.

## Where PixelMe keeps its data

| What | Location |
|---|---|
| Settings, clipboard, shelf, reminders, to-do | `%APPDATA%\PixelMe` |
| Clipboard images, web drawer data, logs | `%LOCALAPPDATA%\PixelMe` |

Your clipboard, files and notifications never leave your PC. PixelMe only goes online for the features you turn on: weather (open-meteo.com), lyrics (lrclib.net), pinned websites, and live chat.

## License

PixelMe is licensed under the [GNU General Public License v3.0](LICENSE). You are free to use, study, share and modify it. If you distribute a modified version, it must also be released under GPL-3.0 with its source code.
