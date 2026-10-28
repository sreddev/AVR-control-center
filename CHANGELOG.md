# Changelog

## v2.0 — AVR Desktop Control (2026-10-28)

### Changes

**Rebrand**
- The app is now **AVR Desktop Control** (`AVRDesktopControl.exe`), maintained by Sergio ([@sreddev](https://github.com/sreddev)). Settings are still read from `%LocalAppData%\DenonRemote`, so saved stations and profiles carry over
- New green app icon
- The GitHub repository is now [sreddev/AVR-desktop-control_tuner](https://github.com/sreddev/AVR-desktop-control_tuner)
- "Radio" is renamed **Tuner**. Below the station name, the tuner now shows the RDS name the station broadcasts, live, with an RDS badge

**Settings**
- "Start minimized" is now on by default for new installs (existing `settings.json` values are kept)
- Auto-update now checks the GitHub Releases API of [sreddev/AVR-desktop-control_tuner](https://github.com/sreddev/AVR-desktop-control_tuner/releases) instead of haussmed.cl; the Download button opens the release's ZIP asset
- Fixed the About card always showing "v1.0.0": the version is now read from the assembly, i.e. `<Version>` in `DenonRemote.csproj`, which must match the GitHub release tag (`v2.0.0`)
- About card: "Created by" Sergio, Telegram [@sreddev](https://t.me/sreddev), PayPal donate button ([paypal.me/sreddev](https://www.paypal.com/paypalme/sreddev?locale.x=ro_RO&country.x=RO)), link to the GitHub repo

**Features from the official Denon / Marantz AVR Remote app**

Every new control stays hidden until the receiver answers its status query, so each page only shows what the connected model supports.

- **Processing** page (new)
  - Audyssey: MultEQ, Dynamic EQ, Reference Level Offset, Dynamic Volume, LFC and containment amount, Dirac Live slot
  - Dialog & dynamics: Dialog Enhancer, Dialog Level, Cinema EQ, Dynamic Compression, Loudness Management
  - Surround parameters: Center Spread, Panorama, Dimension, Center Width, Center Image, Delay Time, Effect Level, Room Size, LFE level, Neural:X. They are re-read after every sound-mode change, since the receiver stores them per mode
  - Speakers & extras: Speaker A/B, Graphic EQ, Compressed Audio Restorer, Bluetooth transmitter and its output mode
- **Zones** page (new): Main, Zone 2 and Zone 3 cards with power, mute, volume, input and sleep timer, plus All Zone Stereo. Zone 3 is supported for the first time, and extra zones appear only when the receiver reports them
- **Receiver** page (new)
  - Device info
  - Video & A/V sync: HDMI audio output, HDMI monitor, output resolution, video mode, picture mode, aspect ratio, Auto Lip Sync, audio delay
  - Display & power: dimmer, ECO mode, auto standby
  - Locks & limits: volume limit, front panel lock, IR remote lock
- **Home**
  - Quick Select (Denon) / Smart Select (Marantz) bar. Slot names are read from the receiver; click a slot to recall it, right-click to store the current input, volume and sound settings in it
  - Sleep timer
  - The FORMAT card becomes a SIGNAL card: signal type, channel layout (2.0 / 5.1 / 7.1.4), sample rate, decoder, video in → out and number of active speakers. Click it to refresh
- **Sources**: a new "Customize" mode renames inputs or hides them from every source picker. Names are stored in the app; the receiver keeps its own
- **Tuner**: saved stations can be marked "skip" so previous / next passes over them
- **Sound**: subwoofer output on/off switch. The Zones card moved to the new Zones page
- **Tray widget and tray menu**: Quick / Smart Select slots and the sleep timer
- Navigation is grouped as listening (Home, Sources, Tuner, HEOS), audio (Sound, Processing, Zones) and system (Receiver, Devices)

**Fixes**
- Bass, Treble, Subwoofer level, Tone Control and the Zone 2 volume on the Sound page now actually reach the receiver; before, the sliders only changed the on-screen value
- The Home header shows the real brand (it always said "DENON")
- The "New version" banner in Settings no longer shows when there is no update
- Inputs without a custom receiver name show a friendly caption ("Tuner", "HEOS Music") instead of the raw token
- Zone replies such as channel levels are no longer mistaken for a zone's input

## v1.6.1 — Army Green (2026-10-28)

### Changes

**New colour scheme**
- The red accent is replaced by a bright army green throughout the app: buttons, toggles, sliders, channel chips, station badges, highlights and status dots
- Two shades keep everything readable on the dark theme: olive `#617D2E` for fills under white text (4.7:1, the brightest that still passes WCAG AA), light olive `#B8CC6E` for accent-coloured text and outlines (9.8:1)
- Primary buttons (Power, Tune, Save, Play…) pin WPF-UI's accent shades explicitly; its automatic dark-theme tint would have dropped white button text below 4.5:1
- Mute indicator follows the accent colour

**English-only interface**
- Every Spanish text is now English: page titles, labels, tooltips, buttons, HEOS sign-in and queue, Sound and Sources pages, status messages, volume unit names and HEOS item types
- The automatic Spanish mode (based on the Windows language) is removed; all strings live in one English table (`Services/Localization.cs`)
- Fixed garbled characters in a few strings ("â€¦" now shows as "…", the tray menu's "Volume –", the widget's "Open full window ›")
- Frequency validation messages always use "87.5" style numbers, whatever the Windows regional format

**Docs**
- New screenshots for Home, Radio and HEOS; the Radio page is now shown in the README
- `CONTRIBUTING.md` and `RELEASE-v1.5.0.md` translated to English

## v1.6.0 — Radio Edition (2026-10-28)

### New features

**AM / FM Radio (Tuner)**
- New "Radio" page showing the tuned frequency, band (FM/AM), AUTO/MANUAL mode and RDS station name
- Manual tuning by typing a frequency (FM in MHz, AM in kHz; the band is detected automatically)
- Station seek and step tuning using the receiver's regional step size
- Saved stations: add, edit, delete, click to listen, previous/next
- Saved stations are numbered (01, 02, 03…) and the station currently tuned is highlighted
- The receiver's own presets (names + frequencies, via `OPTPN ?`) are imported automatically the first time, and can be re-imported with the download button; duplicates are skipped and the receiver is never modified
- Tuning switches the main zone input to TUNER automatically

## v1.5.0 — HEOS Edition (2026-08-23)

### New features

**Full HEOS support**
- Service browsing: Tidal, TuneIn, Local Music, Playlists, History, Favorites
- Search within services by Artist, Album, Track and Playlist
- Direct playback from search results
- Play queue with cover art — click to jump, clear button
- Transport controls: play/pause, next, previous
- **Seek (forward/back)** — found to work via UPnP AVTransport (not via the CLI)
- Interactive progress bar with current time and duration
- HEOS account sign-in (required for Tidal, TuneIn, Deezer)
- Services shown as square tiles with logos
- Blurred album-cover background (glass effect)
- Clicking an artist or album searches for it in the service automatically
- Collapsible queue panel with movable splitters
- Infinite-loop detection in Tidal "My Music" folders (firmware limitation)

**Sound**
- Per-channel level control (FL, FR, C, SL, SR) with individual sliders
- Volume profiles: save, load, rename, delete
- Normalize button (all channels to 0 dB)
- Zones built into the Sound page (no separate tab anymore)
- Volume unit selector: absolute, decibels or percent

**General**
- Visual mute indicator (icon changes + red "MUTE" text)
- Automatic Spanish/English localization based on the Windows language
- Clickable Dashboard cards: Source opens the source picker, Surround opens the modes
- Full text in the metrics (no truncation)
- Movable splitters in HEOS to adapt the layout
- Floating widget from the tray
- Single instance (mutex) — two copies can't run at once
- Auto-update check at startup

### Fixes

- **Zone 2**: no longer turns on by itself. Power On uses `ZMON` (Main only)
- **Volume slider**: 300 ms debounce, no more runaway changes
- **HEOS connection**: asynchronous reader that handles "command under process" responses
- **Channels**: echo suppression while dragging sliders (no bouncing)
- **Dark mode**: high-contrast text brushes (WCAG 4.5:1+)
- **Services**: square tiles always visible

### Technical notes on the blurred background

The blurred cover-art background uses a `Rectangle` with an `ImageBrush` +
`BlurEffect` in WPF. Limitations found:

1. WPF has no CSS-style `backdrop-filter` — the blur is applied to the image,
   not to the content on top of it
2. Cards use a semi-transparent background (`#88141820`) to simulate the
   glass effect. The result is subtle but works.
3. The receiver doesn't support seek via the HEOS CLI (`Command not recognized`)
   but accepts it via UPnP AVTransport SOAP on port 60006. The official app
   uses the same protocol for its time bar.
4. Tidal's "My Music" and "What's New" folders return the root in a loop
   through the CLI — a HEOS firmware bug on S-series models. Search works as
   a workaround.
