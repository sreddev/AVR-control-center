# AVR Desktop Control

**Full remote control for Denon and Marantz AV receivers on Windows.**

Volume, inputs, sound modes, Audyssey, surround parameters, zones, the AM/FM tuner with RDS, HEOS streaming and receiver settings, in one Fluent desktop app with a tray widget. It adapts to your model: options appear only when the receiver reports them.

[![Release](https://img.shields.io/github/v/release/sreddev/AVR-desktop-control?style=flat-square)](https://github.com/sreddev/AVR-desktop-control/releases/latest)
[![License](https://img.shields.io/github/license/sreddev/AVR-desktop-control?style=flat-square)](LICENSE)
![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6?style=flat-square)

<p align="center">
  <img src="docs/screenshot-home.png" width="800" alt="Home">
</p>

## Contents

- [Features](#features)
- [Download and install](#download-and-install)
- [Quick start](#quick-start)
- [Compatibility](#compatibility)
- [Screenshots](#screenshots)
- [Documentation](#documentation)
- [Build from source](#build-from-source)
- [Credits](#credits) · [License](#license)

---

## Features

| | |
|---|---|
| 🏠 **Home** | Volume, input, sound mode and live signal (format, 2.0 / 5.1 / 7.1.4 layout, sample rate, video). Quick / Smart Select slots, sleep timer, channel map with one-click speaker trim |
| 🎛️ **Sources** | Switch inputs and sound modes; rename or hide inputs in the app |
| 📻 **Tuner** | AM/FM with live **RDS** station name, manual tuning, seek / step, saved stations (import from the receiver, skip, previous / next) |
| 🎵 **HEOS** | Browse TuneIn, Tidal, local music, playlists and favorites; search, queue, working seek |
| 🔊 **Sound** | Per-speaker trim, bass / treble, subwoofer level and on/off, saved sound profiles |
| 🎧 **Processing** | Audyssey (MultEQ, Dynamic EQ, Reference Level, Dynamic Volume, LFC), Dirac Live, Dialog Enhancer, Cinema EQ, dynamic compression, per-mode surround parameters, Speaker A/B, Graphic EQ, Restorer, Bluetooth transmitter |
| 🏘️ **Zones** | Main, Zone 2 and Zone 3: power, volume, mute, input, sleep timer; All Zone Stereo |
| 📺 **Receiver** | HDMI audio / monitor / resolution, picture and video mode, Auto Lip Sync, audio delay, dimmer, ECO, auto standby, volume limit, panel and remote lock |
| 📌 **Tray** | Click for a floating mini remote, double-click for the full window, right-click for a quick menu |
| 🔄 **Updates** | Checks GitHub Releases at startup |

---

## Download and install

Get the latest version from [**Releases**](https://github.com/sreddev/AVR-desktop-control/releases/latest):

| File | |
|---|---|
| `AVRDesktopControl-<version>-Setup.exe` | **Installer** (recommended). Start-menu entry, optional desktop shortcut and start with Windows. No admin rights needed |
| `AVRDesktopControl-<version>.zip` | **Portable**: extract anywhere and run `AVRDesktopControl.exe` |

Both are self-contained, so no .NET installation is needed. Windows SmartScreen may warn about an unknown publisher the first time, because the app isn't code-signed: choose **More info → Run anyway**.

Settings, saved stations and profiles are stored in `%LocalAppData%\DenonRemote\settings.json` and survive updates.

---

## Quick start

1. Make sure the PC and the receiver are on the same network, and that **Network Standby** (Network Control) is on in the receiver's setup menu.
2. Start the app. It reconnects to the last receiver automatically. The first time, open **Devices** and pick your receiver, or type its IP address.
3. Use **Home** for everyday control, or click the tray icon for the mini remote.

The full walkthrough of every page is in the **[User Guide](docs/USER-GUIDE.md)**.

---

## Compatibility

| | |
|---|---|
| **Receivers** | Denon and Marantz network receivers (AVR-X, AVR-S, Cinema, NR, SR series). Features the model lacks are hidden automatically |
| **Tested with** | Marantz NR1508 · Denon AVR-S970H |
| **Windows** | 10 / 11, x64 |
| **Network** | Telnet 23 · HTTP 8080 / 80 · HEOS CLI 1255 · UPnP 60006, on the local network only |

---

## Screenshots

| | |
|---|---|
| <img src="docs/screenshot-tuner.png" alt="Tuner with RDS"> | <img src="docs/screenshot-processing.png" alt="Processing"> |
| **Tuner**: live RDS name and saved stations | **Processing**: Audyssey, dialog, surround |
| <img src="docs/screenshot-zones.png" alt="Zones"> | <img src="docs/screenshot-receiver.png" alt="Receiver"> |
| **Zones**: one card per zone | **Receiver**: video, display, power, locks |
| <img src="docs/screenshot-heos.png" alt="HEOS"> | <img src="docs/screenshot-sound.png" alt="Sound"> |
| **HEOS** streaming | **Sound**: trims, tone, subwoofer, profiles |

---

## Documentation

| Document | For |
|---|---|
| [User Guide](docs/USER-GUIDE.md) | Every page and control, tray widget, troubleshooting, FAQ |
| [Architecture](docs/ARCHITECTURE.md) | How the app talks to the receiver, project layout, protocol notes |
| [Contributing](CONTRIBUTING.md) | Development setup, adding a receiver setting, release process |
| [Changelog](CHANGELOG.md) | What changed in each version |

---

## Build from source

```bash
git clone https://github.com/sreddev/AVR-desktop-control.git
cd AVR-desktop-control/src/DenonRemote
dotnet run
```

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0). See [CONTRIBUTING.md](CONTRIBUTING.md) for release builds and the installer.

---

## Credits

Made with ♥ by **Sergio**

| | |
|---|---|
| GitHub | [@sreddev](https://github.com/sreddev) |
| Telegram | [@sreddev](https://t.me/sreddev) |
| Donate | [![PayPal](https://img.shields.io/badge/PayPal-Buy_me_a_coffee-blue?logo=paypal)](https://www.paypal.com/paypalme/sreddev?locale.x=ro_RO&country.x=RO) |

Denon, Marantz, HEOS and Audyssey are trademarks of their respective owners. This project is not affiliated with them.

## License

[MIT](LICENSE)
