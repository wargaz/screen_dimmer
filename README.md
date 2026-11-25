# screen_dimmer

Dims and tints the screen on Windows. Made by Claude Code.

A small tray app (it lives next to wifi and volume). Click the icon and a panel with two sliders pops up:

- **Brightness** – darkens the screen further than Windows allows, down to 5%.
- **Red tint** – from neutral, through warm orange, to fully red.

There is no schedule. The values stay where you put them.

## How it works

It uses the Windows Magnification API (`MagSetFullscreenColorEffect`), which applies a color matrix to the whole screen. Brightness and tint are combined into one diagonal matrix, so the colors are really filtered instead of covered by a semi-transparent overlay.

## Features

- Tray icon, left click opens the panel, right click opens a menu
- Starts with Windows (can be turned off in the panel)
- **Ctrl+Alt+End** resets the screen if it gets too dark
- Remembers the settings and reapplies them after sleep, unlock and display changes
- Follows the Windows light/dark theme and accent color

## Build

No install needed. It uses the C# compiler that ships with Windows (.NET Framework 4.8):

```powershell
.\build.ps1
```

The result is `bin\ScreenDimmer.exe`.

## Limitations

- Does not work while Windows Magnifier is running.
- Does not apply to the UAC prompt (secure desktop).
- Sets a neutral gamma ramp so the mouse pointer gets dimmed too. This can override Windows Night light.

## License

[CC0 1.0](LICENSE): public domain, do whatever you want with it.
