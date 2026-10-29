# Changelog

## v2.2 (2026-10-29)

### HEOS Favorites
- **♥ Favorites** button on the HEOS page opens HEOS Favorites and TuneIn Favorites, with tabs to switch between them
- Add a station to HEOS Favorites from any listing (♡ on the tile or right-click), or add what's playing now (♡ next to the transport buttons)
- Remove a HEOS Favorite with ✕, the right-click menu or the Del key, after a confirmation
- Keyboard navigation in listings: arrows, Enter, Del, Backspace

### Fixes
- HEOS Favorites didn't play: they were sent as `play_stream`, which the receiver rejects for favorites. They now use `play_preset`
- Lists longer than 100 entries were cut off. Listings are now read page by page
- A dropped HEOS connection (receiver standby, network blip) left every list empty until the app was restarted. The app now detects it (heartbeat) and reconnects
- Two listings loading at once could swap or lose each other's results
- Empty or failed listings showed a blank panel; they now say why
- Names with `&` showed as `%26`, and non-Latin characters could be garbled
- Radio stations no longer show an "Add to queue" button; HEOS can't queue stations

## v2.1 (2026-10-28)

### App icon
- Redrawn brighter and bolder so it stands out on the taskbar: a light olive gradient tile with a thicker white ring and dot
- Rounded-square shape. A circle only covers about 79 % of its slot, so the icon looks larger in the same space
- Rendered natively at 12 sizes (16, 20, 24, 30, 32, 36, 40, 48, 64, 96, 128, 256 px). The 24, 30 and 36 px sizes the taskbar uses at 100–150 % scaling were missing, so Windows used to shrink a 32 px image and blur it

### Documentation
- **README** is restructured as a landing page: feature table, download and install (including the SmartScreen note), quick start, compatibility, screenshot gallery, documentation index
- New **[User Guide](docs/USER-GUIDE.md)**: every page and control, the tray widget and menu, troubleshooting and FAQ, with screenshots
- New **[Architecture](docs/ARCHITECTURE.md)**: connections and ports, project layout, data flow, the data-driven settings model and protocol notes
- **CONTRIBUTING** rewritten: development setup, pull requests, adding a receiver setting in one line, code style, and the full release process
- Fresh screenshots of all ten pages with the new icon, replacing the four old ones

### Fixes
- The "New version" banner in Settings still showed when no update existed. Its binding now falls back to hidden

### Code
- Renamed from Denon to AVR throughout the code: solution `AVRDesktopControl.sln`, project `src/AvrRemote/AvrRemote.csproj`, namespace `AvrRemote`, protocol folder `Avr/` with `AvrTelnetClient`, `AvrHttpClient`, `AvrEventParser` and `AvrProtocol`
- Settings now live in `%LocalAppData%\AvrRemote`. On first start the app copies `settings.json` from the old `DenonRemote` folder, so stations, profiles and devices carry over
- The crash log is now `%TEMP%\AvrRemote-crash.log`
- Removed the stale 1.0.0 installer binary from `output/`

## v2.0 — AVR Desktop Control (2026-10-28)

The first release under the new name. It brings the official Denon / Marantz AVR Remote app's feature set to the desktop and reorganizes the app around it.

### Highlights
- **AVR Desktop Control**: new name, green icon, new repository [sreddev/AVR-desktop-control](https://github.com/sreddev/AVR-desktop-control)
- **Three new pages**: Processing, Zones and Receiver
- **Quick Select / Smart Select**, **sleep timers**, a live **signal panel** and the **RDS station name** on the Tuner
- **Adapts to each model**: every new option stays hidden until the connected receiver reports it
- Fixed: tone, subwoofer and Zone 2 volume sliders on the Sound page never reached the receiver

### Rebrand
- The app is now **AVR Desktop Control** (`AVRDesktopControl.exe`), maintained by Sergio ([@sreddev](https://github.com/sreddev))
- New green app icon: title bar, taskbar, tray, installer and exe, all six icon sizes
- Window titles, tray tooltip, installer, README, CONTRIBUTING and every source file header carry the new name and author
- About card: "Created by" Sergio, [Telegram @sreddev](https://t.me/sreddev), a working [PayPal donate](https://www.paypal.com/paypalme/sreddev?locale.x=ro_RO&country.x=RO) button and a link to the repository
- Settings still live in `%LocalAppData%\DenonRemote`, so saved stations, profiles and devices carry over from earlier versions

### Navigation
The sidebar is grouped into three sections:
- **Listening:** Home, Sources, Tuner, HEOS
- **Audio:** Sound, Processing, Zones
- **System:** Receiver, Devices

"Radio" is renamed **Tuner** everywhere.

### Processing (new page)
- **Audyssey & room correction**: MultEQ, Dynamic EQ, Reference Level Offset, Dynamic Volume, LFC and containment amount, Dirac Live slot
- **Dialog & dynamics**: Dialog Enhancer, Dialog Level (on/off and trim), Cinema EQ, Dynamic Compression, Loudness Management
- **Surround parameters**: Center Spread, Panorama, Dimension, Center Width, Center Image, Delay Time, Effect Level, Room Size, LFE level, Neural:X. They are re-read after every sound-mode change, because the receiver stores them per mode
- **Speakers & extras**: Speaker A/B, Graphic EQ, Compressed Audio Restorer, Bluetooth transmitter and its output mode
- The current sound mode is shown in the page header

### Zones (new page)
- Main, Zone 2 and **Zone 3** cards, each with power, mute, volume, input and sleep timer
- All Zone Stereo
- Extra zones appear only when the receiver reports them. Zone 2 still never turns on by itself, and the main power button still switches it off

### Receiver (new page)
- **Device**: name, brand and model, IP address, maximum volume
- **Video & A/V sync**: HDMI audio output, HDMI monitor output, output resolution, video mode, picture mode, aspect ratio, Auto Lip Sync, audio delay
- **Display & power**: front panel dimmer, ECO mode, auto standby
- **Locks & limits**: volume limit, front panel lock (with or without volume), IR remote lock

### Home
- **Quick Select** (Denon) / **Smart Select** (Marantz) bar. Slot names are read from the receiver. Click a slot to recall it; right-click to store the current input, volume and sound settings in it
- **Sleep timer** for the main zone
- The FORMAT card becomes a **SIGNAL** card: signal type, channel layout (2.0 / 5.1 / 7.1.4), sample rate, decoder, video in → out and active speaker count. Click it to refresh
- On the tuner, the Source card also shows the frequency and the live RDS name
- The header shows the real brand and model instead of a fixed "DENON"
- The page scrolls, so the speaker map is never cut off

### Tuner
- Under the frequency, the page shows the **live RDS station name (PS)** with an RDS badge, next to the name you saved for the station. Many stations rotate several texts there, and the line follows them
- Saved stations can be marked **skip**, so previous / next pass over them

### Sources
- New **Customize** mode: rename inputs or hide them from every source picker (Home, Zones, Sources). Names live in the app; the receiver keeps its own
- Inputs without a custom receiver name show a friendly caption ("Tuner", "HEOS Music") instead of the raw token

### Sound
- Subwoofer output on/off switch, on models that report it
- The Zones card moved to the new Zones page

### Tray widget and tray menu
- Quick / Smart Select slots in the widget and as a tray submenu
- Sleep timer in the widget

### Settings
- **Start minimized** is on by default for new installs
- **Auto-update** checks the GitHub Releases of this repository. The Download button opens the release's ZIP
- The About card shows the real version. It used to always say "v1.0.0"
- The "New version" banner only appears when an update exists

### Fixes
- Bass, Treble, Subwoofer level, Tone Control and Zone 2 volume on the Sound page now reach the receiver. Before, the sliders only changed the on-screen value
- Zone replies such as channel levels are no longer mistaken for a zone's input
- Receiver echoes are no longer sent back to the receiver as new commands
- LFE level no longer reads "-0 dB"

### Refactoring and architecture
- **Data-driven receiver settings**. `Denon/AvrSetting.cs` models any telnet parameter as a Toggle, Choice, Level or Action. `Denon/AvrSettingsCatalog.cs` defines about 45 of them in one place: prefix, query, options, range, encoding, UI group and description. Adding a parameter is one catalog line, and the UI picks it up automatically
- **Support detection**. Receivers stay silent for parameters they lack, so a setting is shown only after its query is answered. Support resets when you connect to another receiver
- **Safe writes**
  - Replies are routed to settings by the longest matching prefix
  - Settings that share a prefix accept only their own values (`PSDIL ON` vs `PSDIL 50`, `BTTX ON` vs `BTTX SP`)
  - A slider ignores receiver echoes while you drag it
  - Values are never written before the receiver has reported them, and ranges widen to fit what the receiver reports. Together these stop WPF slider coercion from sending a command nobody asked for
- **Remote vs user changes**. The event parser flags the state while it applies receiver lines (`ReceiverState.IsApplyingRemote`). Listeners use the flag to send only real user edits
- **ReceiverService**
  - Sends every catalog query on connect
  - Debounces slider commands per control
  - Re-reads signal info after an input change, plus the per-mode parameters after a sound-mode change
  - One zone API (power, mute, volume, input) for Main / Zone 2 / Zone 3
  - Picks `MSQUICK` or `MSSMART` from the receiver's reply, falling back to the brand
- **Event parser**
  - Catalog lines are handled first
  - One zone handler covers Zone 2 and Zone 3, with strict input validation
  - New parsing for Quick Select slots and names (`SSQSNZMA`), signal type (`SSINFAISSIG`), video resolution (`SSINFSIGRES`), decoder (`SYSMI`) and channel layout
  - The old ECO parser moved into the catalog
- **UI building blocks**
  - Reusable row templates (`Views/SettingTemplates.xaml`) chosen by `Controls/SettingTemplateSelector.cs`, plus a section card template
  - View models `SettingSection` and `ZonePanel`
  - Templates are merged at application level, because pages are created before they join the window
- **New state**: Zone 3, Quick Select slots, signal information, input aliases and hidden flags, station skip
- **AutoUpdateService** rewritten for the GitHub Releases API (tag `vX.Y.Z`, ZIP asset, User-Agent header)
- **Version source**: the version comes only from `<Version>` in `DenonRemote.csproj`, and GitHub release tags must match it

### Build and repository
- Self-contained, compressed single-file build (`dotnet publish -r win-x64 -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true`)
- Inno Setup installer, now in English, installs per user without admin rights
- The APK and installer output are git-ignored
- Removed: the old update manifest (`updates/denon/update.json`), the v1.5.0 release notes, a stale 1.0.0 installer binary and the old Radio screenshot
- New screenshots: Home, Tuner, Processing, HEOS
- Commands marked "verified" in the catalog were read back from a Marantz NR1508

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
