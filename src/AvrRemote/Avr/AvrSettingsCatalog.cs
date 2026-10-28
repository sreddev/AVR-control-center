// AVR Desktop Control
// Copyright (c) 2026 Sergio (@sreddev)
// Licensed under MIT License
// https://github.com/sreddev/AVR-desktop-control

using System.Collections.Generic;

namespace AvrRemote.Avr;

/// <summary>
/// Receiver parameters beyond volume / input / tone, modelled on what the
/// official Denon and Marantz AVR Remote apps expose.
///
/// Entries marked "verified" were read back from a Marantz NR1508; the rest
/// follow the published Denon/Marantz control protocol. Models that lack a
/// parameter never answer its query, so those rows stay hidden.
/// </summary>
public static class AvrSettingsCatalog
{
    public static AvrSettingSet Create() => new(Build());

    private static IEnumerable<AvrSetting> Build()
    {
        // ── Audyssey & room correction ──────────────────────────────────────
        yield return Choice(SettingGroup.RoomCorrection, "multeq", "MultEQ",
            "Room correction curve measured by Audyssey setup.",
            "PSMULTEQ:", "PSMULTEQ: ?",                                     // verified
            ("AUDYSSEY", "Reference"), ("BYP.LR", "L/R Bypass"), ("FLAT", "Flat"),
            ("MANUAL", "Manual"), ("OFF", "Off"));
        yield return Toggle(SettingGroup.RoomCorrection, "dyneq", "Dynamic EQ",
            "Keeps bass and surround balanced at low volume.",
            "PSDYNEQ ", "PSDYNEQ ?");                                       // verified
        yield return Choice(SettingGroup.RoomCorrection, "reflev", "Reference level offset",
            "How much Dynamic EQ compensates. Lower it for music mixed loud.",
            "PSREFLEV ", "PSREFLEV ?",                                      // verified
            ("0", "0 dB"), ("5", "5 dB"), ("10", "10 dB"), ("15", "15 dB"));
        yield return Choice(SettingGroup.RoomCorrection, "dynvol", "Dynamic Volume",
            "Evens out loud adverts and quiet dialogue.",
            "PSDYNVOL ", "PSDYNVOL ?",                                      // verified
            ("HEV", "Heavy"), ("MED", "Medium"), ("LIT", "Light"), ("OFF", "Off"));
        yield return Toggle(SettingGroup.RoomCorrection, "lfc", "Audyssey LFC",
            "Low Frequency Containment: keeps bass from disturbing neighbours.",
            "PSLFC ", "PSLFC ?");
        yield return Level(SettingGroup.RoomCorrection, "cntamt", "LFC containment amount",
            "1 = least, 7 = most containment.",
            "PSCNTAMT ", "PSCNTAMT ?", 1, 7, 1);
        yield return Choice(SettingGroup.RoomCorrection, "dirac", "Dirac Live",
            "Dirac Live filter slot (models with Dirac Live).",
            "PSDIRAC ", "PSDIRAC ?",
            ("1", "Slot 1"), ("2", "Slot 2"), ("3", "Slot 3"), ("OFF", "Off"));

        // ── Dialog & dynamics ───────────────────────────────────────────────
        yield return Choice(SettingGroup.DialogDynamics, "deh", "Dialog Enhancer",
            "Lifts voices above music and effects.",
            "PSDEH ", "PSDEH ?",
            ("OFF", "Off"), ("LOW", "Low"), ("MED", "Medium"), ("HIGH", "High"));
        yield return Toggle(SettingGroup.DialogDynamics, "dil", "Dialog level adjust",
            "Turns the dialog level trim below on or off.",
            "PSDIL ", "PSDIL ?");                                           // verified
        yield return Level(SettingGroup.DialogDynamics, "dillevel", "Dialog level",
            "Centre-channel dialogue trim.",
            "PSDIL ", null, -12, 12, 1, offset: 50, unit: "dB");            // verified (PSDIL 50)
        yield return Toggle(SettingGroup.DialogDynamics, "cinemaeq", "Cinema EQ",
            "Softens harsh treble on film soundtracks.",
            "PSCINEMA EQ.", "PSCINEMA EQ. ?");                              // verified
        yield return Choice(SettingGroup.DialogDynamics, "drc", "Dynamic compression",
            "Reduces the gap between quiet and loud passages.",
            "PSDRC ", "PSDRC ?",                                            // verified
            ("AUTO", "Auto"), ("LOW", "Low"), ("MID", "Medium"), ("HI", "High"), ("OFF", "Off"));
        yield return Toggle(SettingGroup.DialogDynamics, "lom", "Loudness management",
            "Applies the dialogue normalisation embedded in Dolby / DTS streams.",
            "PSLOM ", "PSLOM ?");                                           // verified

        // ── Surround parameters (meaning depends on the sound mode) ─────────
        yield return Toggle(SettingGroup.Surround, "ces", "Center Spread",
            "Spreads the centre channel across the front speakers.",
            "PSCES ", "PSCES ?");
        yield return Toggle(SettingGroup.Surround, "pan", "Panorama",
            "Wraps the front image around the sides (Pro Logic music).",
            "PSPAN ", "PSPAN ?");                                           // verified
        yield return Level(SettingGroup.Surround, "dim", "Dimension",
            "Shifts the sound field towards the front or the rear.",
            "PSDIM ", "PSDIM ?", 0, 6, 1);                                  // verified
        yield return Level(SettingGroup.Surround, "cen", "Center Width",
            "How much of the centre channel goes to the front left/right.",
            "PSCEN ", "PSCEN ?", 0, 7, 1);                                  // verified
        yield return Level(SettingGroup.Surround, "cei", "Center Image",
            "Blends the centre channel into the front speakers.",
            "PSCEI ", "PSCEI ?", 0, 1, 0.1, factor: 10);                    // verified
        yield return Level(SettingGroup.Surround, "del", "Delay time",
            "Reflection delay of the DSP sound field.",
            "PSDEL ", "PSDEL ?", 0, 300, 10, digits: 3, unit: "ms");        // verified
        yield return Level(SettingGroup.Surround, "eff", "Effect level",
            "Strength of the DSP surround effect.",
            "PSEFF ", "PSEFF ?", 0, 15, 1);                                 // verified
        yield return Choice(SettingGroup.Surround, "rsz", "Room size",
            "Simulated room size for the DSP modes.",
            "PSRSZ ", "PSRSZ ?",
            ("S", "Small"), ("MS", "Medium-small"), ("M", "Medium"), ("ML", "Medium-large"), ("L", "Large"));
        yield return Level(SettingGroup.Surround, "lfe", "LFE level",
            "Level of the low-frequency effects channel.",
            "PSLFE ", "PSLFE ?", -10, 0, 1, factor: -1, unit: "dB");        // verified
        yield return Toggle(SettingGroup.Surround, "neural", "Neural:X upmix",
            "Upmixes to height speakers in Dolby Surround modes.",
            "PSNEURAL ", "PSNEURAL ?");

        // ── Speakers & extras (model specific) ──────────────────────────────
        yield return Toggle(SettingGroup.Sound, "swr", "Subwoofer output",
            "Turns the subwoofer pre-out on or off.",
            "PSSWR ", "PSSWR ?");                                           // verified
        yield return Choice(SettingGroup.Extras, "front", "Front speakers",
            "Speaker A/B switching on stereo-capable models.",
            "PSFRONT ", "PSFRONT?",
            ("SPA", "Speaker A"), ("SPB", "Speaker B"), ("A+B", "A + B"));
        yield return Toggle(SettingGroup.Extras, "geq", "Graphic EQ",
            "Applies the graphic equalizer set up on the receiver.",
            "PSGEQ ", "PSGEQ ?");                                           // verified
        yield return Choice(SettingGroup.Extras, "rstr", "Compressed Audio Restorer",
            "Restores detail lost in MP3 / AAC and Bluetooth audio.",
            "PSRSTR ", "PSRSTR ?",
            ("OFF", "Off"), ("MODE1", "Mode 1"), ("MODE2", "Mode 2"), ("MODE3", "Mode 3"));
        yield return Toggle(SettingGroup.Extras, "bttx", "Bluetooth transmitter",
            "Streams the receiver's audio to Bluetooth headphones.",
            "BTTX ", "BTTX ?");
        yield return Choice(SettingGroup.Extras, "bttxmode", "Bluetooth output mode",
            "Play on the headphones only, or on headphones and speakers.",
            "BTTX ", null, strict: true,
            ("SP", "Headphones + speakers"), ("BT", "Headphones only"));

        // ── Video & A/V sync ────────────────────────────────────────────────
        yield return Choice(SettingGroup.Video, "vsaudio", "HDMI audio output",
            "Play HDMI audio through the receiver or pass it to the TV.",
            "VSAUDIO ", "VSAUDIO ?",                                        // verified
            ("AMP", "Receiver"), ("TV", "TV"));
        yield return Choice(SettingGroup.Video, "moni", "HDMI monitor output",
            "Which HDMI output is used on dual-output models.",
            "VSMONI", "VSMONI ?",
            ("AUTO", "Auto (both)"), ("1", "Monitor 1"), ("2", "Monitor 2"));
        yield return Choice(SettingGroup.Video, "sch", "Output resolution",
            "Video scaler output resolution.",
            "VSSCH", "VSSCH ?",
            ("AUTO", "Auto"), ("48P", "480p / 576p"), ("10I", "1080i"), ("72P", "720p"),
            ("10P", "1080p"), ("10P24", "1080p 24 Hz"), ("4K", "4K"), ("4KF", "4K 60/50"));
        yield return Choice(SettingGroup.Video, "vpm", "Video mode",
            "Video processing priority.",
            "VSVPM", "VSVPM ?",
            ("AUTO", "Auto"), ("GAME", "Game"), ("MOVI", "Movie"), ("BYPS", "Bypass"));
        yield return Choice(SettingGroup.Video, "pv", "Picture mode",
            "Picture adjustment preset.",
            "PV", "PV?", strict: true,
            ("OFF", "Off"), ("STD", "Standard"), ("MOV", "Movie"), ("VVD", "Vivid"),
            ("STM", "Stream"), ("CTM", "Custom"), ("DAY", "ISF Day"), ("NGT", "ISF Night"));
        yield return Choice(SettingGroup.Video, "asp", "Aspect ratio",
            "Aspect ratio for analog video sources.",
            "VSASP", "VSASP ?",
            ("NRM", "Normal"), ("FUL", "Full"));
        yield return Toggle(SettingGroup.Video, "als", "Auto Lip Sync",
            "Lets a compatible TV correct audio delay automatically.",
            "SSALSSET ", "SSALS ?");                                        // verified
        yield return Level(SettingGroup.Video, "delay", "Audio delay",
            "Delays the sound to match a slow TV picture.",
            "PSDELAY ", "PSDELAY ?", 0, 500, 10, digits: 3, unit: "ms");    // verified

        // ── Display & power ─────────────────────────────────────────────────
        yield return Choice(SettingGroup.DisplayPower, "dimmer", "Display dimmer",
            "Front panel brightness.",
            "DIM ", "DIM ?",                                                // verified
            ("BRI", "Bright"), ("DIM", "Dim"), ("DAR", "Dark"), ("OFF", "Off"));
        yield return Choice(SettingGroup.DisplayPower, "eco", "ECO mode",
            "Limits power draw. Auto adapts it to the volume.",
            "ECO", "ECO?", strict: true,                                    // verified
            ("ON", "On"), ("AUTO", "Auto"), ("OFF", "Off"));
        yield return Choice(SettingGroup.DisplayPower, "stby", "Auto standby",
            "Switches off after this long without input or operation.",
            "STBY", "STBY?",                                                // verified
            ("15M", "15 minutes"), ("30M", "30 minutes"), ("60M", "60 minutes"),
            ("2H", "2 hours"), ("4H", "4 hours"), ("8H", "8 hours"), ("OFF", "Off"));

        // ── Locks & limits ──────────────────────────────────────────────────
        yield return Choice(SettingGroup.Locks, "vollim", "Volume limit",
            "Highest master volume the receiver accepts.",
            "SSVCTZMALIM ", "SSVCTZMALIM ?",
            ("60", "60"), ("70", "70"), ("80", "80 (0 dB)"), ("OFF", "Off"));
        yield return Action(SettingGroup.Locks, "panellock", "Front panel lock",
            "Locks the receiver's own buttons (models that support it).",
            ("SYPANEL LOCK ON", "Lock"), ("SYPANEL+V LOCK ON", "Lock incl. volume"), ("SYPANEL LOCK OFF", "Unlock"));
        yield return Action(SettingGroup.Locks, "remotelock", "IR remote lock",
            "Ignores the infrared remote control (models that support it).",
            ("SYREMOTE LOCK ON", "Lock"), ("SYREMOTE LOCK OFF", "Unlock"));

        // ── Zones & sleep ───────────────────────────────────────────────────
        yield return Toggle(SettingGroup.Zones, "allzonestereo", "All Zone Stereo",
            "Plays the main zone's stereo audio in every zone.",
            "MNZST ", "MNZST ?");
        yield return Sleep("sleep", "Sleep timer", "SLP", "SLP?");          // verified
        yield return Sleep("z2sleep", "Zone 2 sleep timer", "Z2SLP", "Z2SLP?");
        yield return Sleep("z3sleep", "Zone 3 sleep timer", "Z3SLP", "Z3SLP?");
    }

    // ── Builders ───────────────────────────────────────────────────────────

    private static AvrSetting Toggle(SettingGroup g, string key, string label, string desc, string prefix, string? query) =>
        new() { Group = g, Key = key, Label = label, Description = desc, Kind = SettingKind.Toggle, Prefix = prefix, Query = query };

    private static AvrSetting Choice(SettingGroup g, string key, string label, string desc,
                                     string prefix, string? query, params (string Value, string Caption)[] options) =>
        Choice(g, key, label, desc, prefix, query, strict: false, options);

    private static AvrSetting Choice(SettingGroup g, string key, string label, string desc,
                                     string prefix, string? query, bool strict, params (string Value, string Caption)[] options)
    {
        var s = new AvrSetting
        {
            Group = g, Key = key, Label = label, Description = desc,
            Kind = SettingKind.Choice, Prefix = prefix, Query = query, AcceptUnlisted = !strict
        };
        foreach (var (v, c) in options) s.Options.Add(new SettingOption(v, c));
        return s;
    }

    private static AvrSetting Level(SettingGroup g, string key, string label, string desc,
                                    string prefix, string? query, double min, double max, double step,
                                    double factor = 1, double offset = 0, int digits = 2, string unit = "") =>
        new()
        {
            Group = g, Key = key, Label = label, Description = desc, Kind = SettingKind.Level,
            Prefix = prefix, Query = query, Min = min, Max = max, Step = step,
            Factor = factor, Offset = offset, Digits = digits, Unit = unit
        };

    private static AvrSetting Action(SettingGroup g, string key, string label, string desc,
                                     params (string Command, string Caption)[] actions)
    {
        var s = new AvrSetting
        {
            Group = g, Key = key, Label = label, Description = desc,
            Kind = SettingKind.Action, IsSupported = true
        };
        foreach (var (cmd, c) in actions) s.Options.Add(new SettingOption(cmd, c));
        return s;
    }

    /// <summary>Sleep timers report the minutes left, so any "NNN" reply is accepted.</summary>
    private static AvrSetting Sleep(string key, string label, string prefix, string query)
    {
        var s = new AvrSetting
        {
            Group = SettingGroup.Sleep, Key = key, Label = label,
            Description = "Switches the zone off after the chosen time.",
            Kind = SettingKind.Choice, Prefix = prefix, Query = query,
            CaptionFor = v => int.TryParse(v, out var m) ? $"{m} min" : v
        };
        s.Options.Add(new SettingOption("OFF", "Off"));
        foreach (var m in new[] { 10, 20, 30, 60, 90, 120 })
            s.Options.Add(new SettingOption(m.ToString("000"), $"{m} min"));
        return s;
    }
}
