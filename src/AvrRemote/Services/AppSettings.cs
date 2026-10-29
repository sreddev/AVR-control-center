// AVR Desktop Control
// Copyright (c) 2026 Sergio (@sreddev)
// Licensed under MIT License
// https://github.com/sreddev/AVR-desktop-control

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AvrRemote.Services;

/// <summary>User-editable preferences persisted to %LocalAppData%.</summary>
public sealed class AppSettings
{
    public string? LastHost { get; set; }
    public string? LastFriendlyName { get; set; }
    public bool AutoConnect { get; set; } = true;
    public bool StartMinimized { get; set; } = true;
    public bool CloseToTray { get; set; } = true;
    public bool AutoUpdate { get; set; } = true;

    /// <summary>How the master volume is presented: Absolute, Decibels or Percent.</summary>
    public VolumeUnit VolumeUnit { get; set; } = VolumeUnit.Absolute;

    /// <summary>Colour theme; applied at startup, so a change needs a restart.</summary>
    public AppTheme Theme { get; set; } = AppTheme.Dark;

    /// <summary>Devices the user connected to at least once, most recent first.</summary>
    public List<KnownDevice> KnownDevices { get; set; } = new();

    /// <summary>Saved per-channel level presets ("Profile 1", "Movies", etc.).</summary>
    public List<ChannelProfile> ChannelProfiles { get; set; } = new();

    /// <summary>User's saved AM/FM stations, in display order.</summary>
    public List<RadioStation> RadioStations { get; set; } = new();

    /// <summary>
    /// Set once the receiver's presets were copied into <see cref="RadioStations"/>
    /// automatically, so deleting every station doesn't bring them back.
    /// </summary>
    public bool RadioPresetsImported { get; set; }

    /// <summary>App-side input names, by receiver token ("CD" → "Turntable").</summary>
    public Dictionary<string, string> SourceAliases { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Input tokens hidden from the source pickers.</summary>
    public List<string> HiddenSources { get; set; } = new();

    /// <summary>Record a successful connection so the sidebar can show it.</summary>
    public void RememberDevice(string host, string friendlyName, string model)
    {
        KnownDevices.RemoveAll(d => string.Equals(d.Host, host, StringComparison.OrdinalIgnoreCase));
        KnownDevices.Insert(0, new KnownDevice
        {
            Host = host,
            FriendlyName = string.IsNullOrWhiteSpace(friendlyName) ? model : friendlyName,
            Model = model,
            LastConnectedUtc = DateTime.UtcNow
        });
        // Keep the list small â€” 5 is enough to make the sidebar useful without cluttering.
        if (KnownDevices.Count > 5)
            KnownDevices = KnownDevices.Take(5).ToList();
    }

    [JsonIgnore]
    private static readonly string DirPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AvrRemote");

    [JsonIgnore]
    private static readonly string FilePath = Path.Combine(DirPath, "settings.json");

    // Earlier versions stored settings under %LocalAppData%\DenonRemote.
    [JsonIgnore]
    private static readonly string LegacyFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DenonRemote", "settings.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath) && File.Exists(LegacyFilePath))
            {
                Directory.CreateDirectory(DirPath);
                File.Copy(LegacyFilePath, FilePath);
            }
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<AppSettings>(json, JsonOpts) ?? new AppSettings();
            }
        }
        catch { /* fall through to defaults */ }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(DirPath);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOpts));
        }
        catch { /* best effort */ }
    }
}

/// <summary>
/// Display unit for the master volume.
/// Denon's internal scale runs 0..98 where 80 equals 0 dB reference.
/// </summary>
public enum VolumeUnit
{
    /// <summary>Raw receiver value, e.g. "52.5" — matches the front panel.</summary>
    Absolute,
    /// <summary>Relative to reference, e.g. "-27.5 dB".</summary>
    Decibels,
    /// <summary>Share of the configured maximum, e.g. "56%".</summary>
    Percent
}

/// <summary>Colour theme: picks Views/Palette.Dark.xaml or Views/Palette.Light.xaml.</summary>
public enum AppTheme
{
    Dark,
    Light
}

/// <summary>Serialized entry in <see cref="AppSettings.KnownDevices"/>.</summary>
public sealed class KnownDevice
{
    public string Host { get; set; } = "";
    public string FriendlyName { get; set; } = "";
    public string Model { get; set; } = "";
    public DateTime LastConnectedUtc { get; set; }
}

/// <summary>
/// A named snapshot of per-channel trim levels. Lets the user store a balance
/// for movies, another for music, and recall it with one click.
/// Observable so inline renaming updates the chip immediately.
/// </summary>
public sealed class ChannelProfile : System.ComponentModel.INotifyPropertyChanged
{
    private string _name = "";

    public string Name
    {
        get => _name;
        set
        {
            if (_name == value) return;
            _name = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Name)));
        }
    }

    /// <summary>Channel label (FL, FR, C, SL, SR...) mapped to its trim in dB.</summary>
    public Dictionary<string, double> Levels { get; set; } = new();

    public double SubwooferLevel { get; set; }
    public double Bass { get; set; }
    public double Treble { get; set; }
    public bool ToneControlEnabled { get; set; }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// A saved analog radio station. Kept in the app rather than in the receiver's
/// preset memory so it can carry a free-form name and be edited from here.
/// Observable so edits and the "now playing" highlight update the list live.
/// </summary>
public sealed partial class RadioStation : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private string _name = "";

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    [CommunityToolkit.Mvvm.ComponentModel.NotifyPropertyChangedFor(nameof(FrequencyLabel))]
    private Avr.TunerBand _band = Avr.TunerBand.FM;

    /// <summary>MHz on FM, kHz on AM.</summary>
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    [CommunityToolkit.Mvvm.ComponentModel.NotifyPropertyChangedFor(nameof(FrequencyLabel))]
    private double _frequency;

    /// <summary>Left out when cycling with next / previous station.</summary>
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private bool _skip;

    /// <summary>True while the receiver is tuned to this station. Not persisted.</summary>
    [property: JsonIgnore]
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private bool _isActive;

    /// <summary>1-based position in the saved list, shown as "01", "02"... Not persisted.</summary>
    [property: JsonIgnore]
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    [CommunityToolkit.Mvvm.ComponentModel.NotifyPropertyChangedFor(nameof(NumberLabel))]
    private int _number;

    [JsonIgnore]
    public string NumberLabel => Number.ToString("00");

    [JsonIgnore]
    public string FrequencyLabel => Avr.AvrProtocol.FormatTunerLabel(Band, Frequency);

    public bool Matches(Avr.TunerBand band, double frequency) =>
        Band == band && Math.Abs(Frequency - frequency) < 0.001;
}

