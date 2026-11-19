<p align="center">
  <img src="src/PixelMe/Assets/PixelMe.ico" width="96" alt="PixelMe logo" />
</p>

<h1 align="center">PixelMe</h1>

<p align="center">
  A free and open-source dynamic island for Windows.<br/>
  <i>Island dinamis untuk Windows — gratis dan open source.</i>
</p>

---

PixelMe adds a small, animated island to the top of your screen. Hover it and it grows into a card with your music, clipboard, files, reminders, widgets and more. It is built to stay light: when nothing is happening it uses **0% CPU**.

## Status

PixelMe is in early development. The roadmap below shows what works today.

### Roadmap

- [x] Animated island (floating or notch style) with spring animations
- [x] Themes: Glass, Black, Graphite, Light
- [x] Settings window, tray icon, start with Windows
- [x] Auto-hide while a fullscreen app is open
- [x] English and Bahasa Indonesia
- [ ] Music controls for any app, album art, synced lyrics
- [ ] Clipboard history (text, links, images, files) with pinning
- [ ] File shelf (drop files, drag them out again)
- [ ] Reminders and timers
- [ ] One-key microphone mute with call timer
- [ ] Quick controls (volume, brightness, Wi-Fi, Bluetooth, night light, focus, power mode)
- [ ] Widgets (clock, world clock, weather, to-do, photos, battery, timer, PC stats)
- [ ] Notifications in the island
- [ ] AI agent monitor (alerts when a coding agent needs your input)
- [ ] Game strip (FPS, CPU, GPU, RAM, temperatures)
- [ ] Twitch / YouTube chat drawer and OBS recording indicator
- [ ] Web drawer for pinned websites
- [ ] Send files to your phone with a QR code
- [ ] Optional taskbar replacement
- [ ] Installer

## Requirements

- Windows 10 version 2004 (build 19041) or newer, or Windows 11
- 64-bit (x64)

## Building from source

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
2. Clone the repository:
   ```
   git clone https://github.com/Blanchette756/PixelMe.git
   cd PixelMe
   ```
3. Build and run:
   ```
   dotnet run --project src/PixelMe
   ```

To create a standalone `.exe` that does not need .NET installed:

```
dotnet publish src/PixelMe -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

## Where PixelMe keeps its data

| What | Location |
|---|---|
| Settings | `%APPDATA%\PixelMe\settings.json` |
| Logs | `%LOCALAPPDATA%\PixelMe\logs` |

Nothing is sent anywhere. Everything stays on your PC.

## License

PixelMe is licensed under the [GNU General Public License v3.0](LICENSE). You are free to use, study, share and modify it. If you distribute a modified version, it must also be released under GPL-3.0 with its source code.
