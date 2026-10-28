# Changelog

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
- Auto-update from haussmed.cl/denon/

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

### Update server files

Upload the contents of `updates/denon/` to `https://haussmed.cl/denon/`:
- `update.json` — version manifest
- `DenonDesktopControl-1.5.0.zip` — standalone executable (~65 MB)
