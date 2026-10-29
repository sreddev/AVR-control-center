// AVR Desktop Control
// Copyright (c) 2026 Sergio (@sreddev)
// Licensed under MIT License
// https://github.com/sreddev/AVR-desktop-control

namespace AvrRemote.Services;

/// <summary>
/// Central table of UI strings. The app is English-only; keeping the text
/// here makes it easy to reword or translate later from a single place.
/// </summary>
public static class L
{
    // -- About --
    public static string AboutTitle => "About";
    public static string AboutCreatedBy => "CREATED BY";
    public static string AboutDonate => "Donate with PayPal";
    public static string AboutDonateSoon => "PayPal donations - coming soon";
    public static string AboutTelegram => "Telegram @sreddev";
    public static string AboutFreeVersion => "Free version - If you like it, buy me a coffee";

    // -- Settings --
    public static string SettingsTitle => "Settings";
    public static string SettingsAutoConnect => "Connect automatically";
    public static string SettingsAutoConnectDesc => "Reconnects to the last known device at startup.";
    public static string SettingsCloseToTray => "Close to tray";
    public static string SettingsCloseToTrayDesc => "Closing the window minimizes to the system tray instead of quitting.";
    public static string SettingsStartMinimized => "Start minimized";
    public static string SettingsStartMinimizedDesc => "Launches the app to the tray so it doesn't steal focus.";
    public static string SettingsAutoUpdate => "Auto-update";
    public static string SettingsAutoUpdateDesc => "Checks GitHub (sreddev/AVR-desktop-control) for new releases at startup.";
    public static string SettingsVolumeUnit => "Volume unit";
    public static string SettingsVolumeUnitDesc => "How the master volume is displayed.";
    public static string SettingsTheme => "Theme";
    public static string SettingsThemeDesc => "Dark or Light. Applied when the app restarts.";
    public static string SettingsThemeRestart => "Restart now";

    // -- Navigation --
    public static string Devices => "Devices";
    public static string Sources => "Sources";
    public static string Sound => "Sound";
    public static string Zones => "Zones";
    public static string Processing => "Processing";
    public static string Receiver => "Receiver";
    public static string Dashboard => "Home";
    public static string Settings => "Settings";
    public static string Recent => "RECENT";

    // -- Connect page --
    public static string AddReceiver => "Add a receiver";
    public static string AddReceiverDesc => "Any Denon or Marantz on the same network.";
    public static string DiscoveredDevices => "Discovered devices";
    public static string Rescan => "Rescan";
    public static string Connect => "Connect";
    public static string AddByIp => "Add by IP";
    public static string AddByIpDesc => "Type the receiver's IP if it didn't appear above.";
    public static string AddAndConnect => "Add and connect";
    public static string DoesntShowUp => "Doesn't show up?";
    public static string DoesntShowUpDesc => "Make sure Network Standby is enabled on the receiver, and that your PC and the AVR are on the same subnet. UPnP / SSDP is used for discovery.";

    // -- Dashboard --
    public static string Volume => "VOLUME";
    public static string Source => "SOURCE";
    public static string Surround => "SURROUND";
    public static string Format => "FORMAT";
    public static string MasterVolume => "MASTER VOLUME";
    public static string InputSignal => "INPUT SIGNAL";
    public static string ActiveSpeakers => "ACTIVE SPEAKERS";
    public static string ClickSpeakerTrim => "Click a speaker to adjust its trim";

    // -- Sources page --
    public static string SourcesTitle => "Sources";
    public static string SourcesDesc => "Tap a tile to switch the main zone to that input.";
    public static string SurroundMode => "Surround Mode";
    public static string SurroundModeDesc => "Select the audio processing mode.";
    public static string CurrentMode => "CURRENT MODE";

    // -- Sound page --
    public static string SoundTitle => "Sound";
    public static string SoundDesc => "Tone control, subwoofer trim and DSP.";
    public static string ToneControl => "Tone control";
    public static string SubwooferTrim => "Subwoofer trim";
    public static string SubwooferTrimDesc => "Adjusts the subwoofer output in 0.5 dB steps.";

    // -- Tuner page --
    public static string Tuner => "Tuner";
    public static string TunerTitle => "AM / FM Tuner";
    public static string TunerDesc => "Tune stations and manage your saved favorites.";
    public static string TunerNowTuned => "NOW TUNED";
    public static string TunerRdsHint => "Station name broadcast over RDS. Many stations rotate several texts here.";
    public static string TunerNotActive => "The current input isn't the tuner. Tuning will switch to Tuner.";
    public static string TunerSwitchToTuner => "Switch to tuner";
    public static string TunerSeekDown => "Seek previous station";
    public static string TunerSeekUp => "Seek next station";
    public static string TunerStepDown => "Step down";
    public static string TunerStepUp => "Step up";
    public static string TunerManual => "MANUAL TUNING";
    public static string TunerManualHint => "FM in MHz (e.g. 95.5) or AM in kHz (e.g. 1010).";
    public static string TunerTune => "Tune";
    public static string TunerSaved => "Saved stations";
    public static string TunerSavedDesc => "Click a station to listen to it.";
    public static string TunerPrevStation => "Previous saved station";
    public static string TunerNextStation => "Next saved station";
    public static string TunerNoStations => "No saved stations yet. Add one below, or import the receiver's presets with the download button.";
    public static string TunerImport => "Import the receiver's presets";
    public static string TunerImported(int n) => $"Imported {n} station(s) from the receiver.";
    public static string TunerNoNewPresets => "No new presets found on the receiver.";
    public static string TunerAddStation => "ADD STATION";
    public static string TunerEditStation => "EDIT STATION";
    public static string TunerName => "Name";
    public static string TunerFrequency => "Frequency";
    public static string TunerUseCurrent => "Use current";
    public static string TunerSave => "Save";
    public static string TunerCancel => "Cancel";
    public static string TunerEdit => "Edit";
    public static string TunerDelete => "Delete";
    public static string TunerInvalidFrequency => FormattableString.Invariant($"Invalid frequency. FM {AvrRemote.Avr.AvrProtocol.FmMin}-{AvrRemote.Avr.AvrProtocol.FmMax} MHz, AM {AvrRemote.Avr.AvrProtocol.AmMin}-{AvrRemote.Avr.AvrProtocol.AmMax} kHz.");
    public static string TunerNameRequired => "Enter a name for the station.";

    // -- Zones page --
    public static string ZonesTitle => "Zones";
    public static string ZonesDesc => "Independent audio for other rooms.";
}
