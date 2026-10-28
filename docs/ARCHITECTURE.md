# Architecture

How AVR Desktop Control talks to a Denon / Marantz receiver, and how the code is organised. For development setup and the release process, see [CONTRIBUTING.md](../CONTRIBUTING.md).

## Tech stack

| Component | Technology |
|---|---|
| Framework | [.NET 8](https://dotnet.microsoft.com/) + WPF |
| UI | [WPF-UI](https://github.com/lepoco/wpfui) 3.0.5 (Fluent / Mica) |
| MVVM | [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) 8.4 |
| Tray | [H.NotifyIcon](https://github.com/HavenDV/H.NotifyIcon) |
| Installer | [Inno Setup 6](https://jrsoftware.org/isinfo.php) |

## Connections

| Port | Protocol | Used for |
|---|---|---|
| 23 | Telnet (Denon/Marantz control protocol) | Every command, plus real-time push events. The main channel |
| 8080 / 80 | HTTP `goform` | Fallback while telnet is unavailable; powering on from standby; initial state |
| 1255 | [HEOS CLI](https://assets.denon.com/documentmaster/uk/heos_cli_protocolspecification.pdf) | Streaming: browse, search, queue, now playing, account |
| 60006 | UPnP AVTransport (SOAP) | Seeking within HEOS tracks, which the HEOS CLI can't do |
| 1900 | SSDP | Discovery, plus a subnet scan |
| 443 | GitHub Releases API | Update check at startup (optional) |

## Project layout

```
src/DenonRemote/
├── App.xaml(.cs)            Startup, DI container, single instance, accent colours, crash log
├── Assets/app.ico           App icon (12 sizes, 16–256 px)
├── Denon/                   Receiver protocol
│   ├── DenonTelnetClient    Socket, send queue (40 ms spacing), line reader
│   ├── DenonHttpClient      goform fallback (port 8080, then 80)
│   ├── DenonEventParser     Turns receiver lines into state changes
│   ├── DenonProtocol        Token encoding: volume, tuner frequency, signal layout…
│   ├── ReceiverState        Observable model of the whole receiver
│   ├── ZoneState            Per-zone power / volume / mute / input
│   ├── ChannelState         Per-channel signal, speaker and trim state
│   ├── AvrSetting           One data-driven parameter (see below)
│   └── AvrSettingsCatalog   The list of ~45 parameters
├── Discovery/               SSDP + subnet probe
├── Heos/                    HEOS CLI client, models, service, UPnP seek
├── Services/
│   ├── ReceiverService      Facade the UI talks to: connect, commands, change tracking
│   ├── AppSettings          settings.json (%LocalAppData%\DenonRemote)
│   ├── AutoUpdateService    GitHub Releases check
│   └── Localization         All UI strings
├── ViewModels/
│   ├── MainViewModel        Root view model (commands, collections)
│   └── SettingSection       SettingSection (titled card) and ZonePanel
├── Controls/                ChannelChip, SettingTemplateSelector
├── Converters/              Binding converters
└── Views/
    ├── MainWindow, WidgetWindow
    ├── Theme.xaml           Colours, typography, card styles
    ├── SettingTemplates.xaml  Row templates for AvrSetting + section card
    └── Pages/               Home (Dashboard), Sources, Tuner, HEOS, Sound,
                             Processing, Zones, Receiver, Devices (Connect), Settings
```

## Data flow

```
 Receiver ──telnet line──► DenonTelnetClient ──► DenonEventParser ──► ReceiverState ──binding──► XAML
    ▲                                                                       │
    └──── SendOrHttp ◄── ReceiverService ◄── user edit (PropertyChanged / command)
```

- **Events, not polling.** The receiver pushes a line for every change (`MV455`, `SITUNER`, `TFANNAME RFI`…). The parser applies it on the UI thread.
- **Echo safety.** While the parser applies a line, `ReceiverState.IsApplyingRemote` is true. Listeners in `ReceiverService` and `MainViewModel` ignore those changes, so a value from the receiver is never sent back as a new command.
- **Debounce.** Sliders send only their final value, about 220 ms after the last move. Each control has its own timer, and the master volume uses 300 ms.
- **Resilience.** Every 15 s the service checks the telnet socket. It reconnects if needed, and falls back to HTTP for basic state.

## Data-driven receiver settings

Most parameters follow one pattern: the command is a prefix plus a value, the receiver confirms with the same text, and a query asks for the current value. `AvrSetting` models that pattern once:

| Kind | Example | UI |
|---|---|---|
| `Toggle` | `PSDYNEQ ON` / `PSDYNEQ OFF` | switch |
| `Choice` | `PSDRC AUTO\|LOW\|MID\|HI\|OFF` | drop-down |
| `Level` | `PSDIM 03`, `PSLFE 05` (= −5 dB) | slider + value |
| `Action` | `SYPANEL LOCK ON` | buttons (no read-back) |

`AvrSettingsCatalog` declares each parameter in one line: group, key, label, description, prefix, query, options or range. The pages render whole groups through `SettingSection` → `SettingTemplates.xaml`, so **adding a parameter needs no UI code** (see [CONTRIBUTING](../CONTRIBUTING.md#adding-a-receiver-setting)).

How it stays safe and model-aware:

- **Support detection.** A row is hidden until its query is answered. Receivers stay silent for parameters they lack, which is how every page adapts to the model. Support resets when you connect to another receiver.
- **Prefix routing.** Incoming lines go to the setting with the longest matching prefix. Settings that share a prefix only accept their own values: `PSDIL ON` is the toggle and `PSDIL 50` is the level; `BTTX ON` is the switch and `BTTX SP` is the mode.
- **No accidental writes.**
  - A setting ignores writes until the receiver has reported its value. WPF can push a slider's coerced value back to its binding, and this keeps that from becoming a command.
  - Slider ranges widen to include any value the receiver reports.
  - Replies are ignored for 0.9 s after a user drag, so the slider doesn't jump back.
- **Per-mode values.** After every sound-mode change, the surround, dialog and Audyssey queries are sent again, because the receiver stores them per mode.

## Protocol notes

| Topic | Detail |
|---|---|
| **Power** | `PW` reports ON when *any* zone is on, so `ZM` is the main-zone flag. Power-on sends `ZMON` + `Z2OFF`, because `PWON` wakes every zone |
| **Volume** | `MV` uses 0–98 with half steps as three digits (`MV455` = 45.5); 80 = 0 dB |
| **Channel trims** | `CV<ch> <nn>` with 50 = 0 dB and half steps as three digits |
| **Tuner** | `TFAN` takes six digits, value × 100 (`TFAN009550` = 95.50 MHz, `TFAN101000` = 1010 kHz). Tuner commands are ignored unless the input is TUNER, so the app switches input first. Stepping needs MANUAL mode, which forces FM mono, so AUTO is restored after each step |
| **RDS** | `TFANNAME` carries the RDS station name (PS). The receiver pushes it whenever it changes; stations often rotate several texts. RadioText and PTY aren't available over telnet on tested models |
| **Presets** | Read with `OPTPN ?`. There's no command to rename or erase one, so saved stations live in the app |
| **Quick Select** | Denon uses `MSQUICKn`, Marantz `MSSMARTn`. `n MEMORY` stores; `SSQSNZMA ?` lists the slot names |
| **Signal** | `SSINFAISSIG` = signal type, `SSINFAISFSV` = sample rate, `SSINFSIGRES I…/O…` = video in/out, `SYSMI` = decoder, `OPINFINS` / `OPINFASP` = channel matrices |
| **Zones** | `Z2…` / `Z3…` mirror the main-zone commands. A zone counts as present once it answers |
| **HEOS seek** | The HEOS CLI rejects seek; the app uses UPnP `AVTransport:Seek` on port 60006 |
| **Tidal "My Music"** | Personal folders loop back to the root through the CLI (an S-series firmware bug). Search by playlist name instead |
| **Blurred background** | WPF has no `backdrop-filter`, so a `BlurEffect` on an `ImageBrush` sits behind semi-transparent cards |

Commands marked `// verified` in `AvrSettingsCatalog.cs` were read back from a Marantz NR1508. The others follow the published Denon/Marantz control protocol.

## Settings file

`%LocalAppData%\DenonRemote\settings.json` holds:
- preferences
- recent receivers (last 5)
- sound profiles
- saved stations (with *skip*)
- input aliases and hidden inputs
- the "presets imported" flag

The folder keeps its historical name so settings carry over from earlier versions.
