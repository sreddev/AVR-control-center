// AVR Desktop Control
// Copyright (c) 2026 Sergio (@sreddev)
// Licensed under MIT License
// https://github.com/sreddev/AVR-desktop-control_tuner

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DenonRemote.Denon;

/// <summary>
/// Full, observable snapshot of an AVR. Owned by <see cref="ReceiverService"/>,
/// consumed by every view model. This is a plain observable object so the UI
/// can bind to its properties directly through DataContext / x:Bind.
/// </summary>
public partial class ReceiverState : ObservableObject
{
    // â”€â”€ Identity â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    [ObservableProperty] private string _friendlyName = "AVR";
    [ObservableProperty] private string _modelName = "";
    [ObservableProperty] private string _brand = "Denon";
    [ObservableProperty] private string _ipAddress = "";
    [ObservableProperty] private bool _isConnected;

    // â”€â”€ Zones â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public ZoneState Main { get; } = new(ZoneId.Main, "Main");
    public ZoneState Zone2 { get; } = new(ZoneId.Zone2, "Zone 2");
    public ZoneState Zone3 { get; } = new(ZoneId.Zone3, "Zone 3");

    /// <summary>
    /// True while the event parser applies a receiver line. Property listeners
    /// use it to tell receiver echoes from user edits, so an echo is never sent back.
    /// </summary>
    public bool IsApplyingRemote { get; internal set; }

    // ── Extended settings (Audyssey, video, dimmer...) ────────────────────
    /// <summary>Every parameter of <see cref="AvrSettingsCatalog"/>, hidden until the receiver answers.</summary>
    public AvrSettingSet Settings { get; } = AvrSettingsCatalog.Create();

    // ── Quick Select (Denon) / Smart Select (Marantz) ───────────────────────
    /// <summary>"MSQUICK" or "MSSMART", whichever the receiver answered to. Empty if neither.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasQuickSelect))]
    [NotifyPropertyChangedFor(nameof(QuickSelectTitle))]
    private string _quickSelectPrefix = "";

    public bool HasQuickSelect => QuickSelectPrefix.Length > 0;
    public string QuickSelectTitle => QuickSelectPrefix == "MSSMART" ? "Smart Select" : "Quick Select";

    public ObservableCollection<QuickSelectSlot> QuickSelects { get; } = new();

    // â”€â”€ Sound â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    [ObservableProperty] private string _surroundMode = "-";
    [ObservableProperty] private string _audioFormat = "-";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SignalDetail))]
    private string _sampleRate = "-";
    [ObservableProperty] private double _bass = 0;         // -12..+12 dB
    [ObservableProperty] private double _treble = 0;       // -12..+12 dB
    [ObservableProperty] private double _subwooferLevel = 0; // -12..+12 dB
    [ObservableProperty] private bool _toneControlEnabled;

    // ── Signal information ──────────────────────────────────────────────────
    /// <summary>Input signal type from <c>SSINFAISSIG</c> ("Analog", "PCM"...).</summary>
    [ObservableProperty] private string _inputSignal = "-";

    /// <summary>"48 kHz · Dolby Atmos": sample rate and decoder, skipping unknown parts.</summary>
    public string SignalDetail =>
        string.Join(" · ", new[] { SampleRate, SoundModeInfo }.Where(x => !string.IsNullOrWhiteSpace(x) && x != "-"));

    /// <summary>"1080p60 → 4K60 · 5 speakers", or "No video · 2 speakers".</summary>
    public string VideoDetail =>
        (VideoInput == "-" && VideoOutput == "-" ? "No video" : $"{VideoInput} → {VideoOutput}")
        + (ActiveSpeakerCount > 0 ? $" · {ActiveSpeakerCount} speakers" : "");
    /// <summary>Input channel layout derived from <c>OPINFINS</c>, e.g. "5.1" or "7.1.4".</summary>
    [ObservableProperty] private string _inputChannelLayout = "-";
    /// <summary>Number of speakers currently driven (from <c>OPINFASP</c>).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VideoDetail))]
    private int _activeSpeakerCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VideoDetail))]
    private string _videoInput = "-";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VideoDetail))]
    private string _videoOutput = "-";

    /// <summary>Decoder description from <c>SYSMI</c>, e.g. "Stereo" or "Dolby Atmos".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SignalDetail))]
    private string _soundModeInfo = "";

    // â”€â”€ Channel matrix â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    /// <summary>Ordered channels (FL, FR, C, SW, SL, SR ...) used by the UI grid.</summary>
    public ObservableCollection<ChannelState> Channels { get; }

    // â”€â”€ Sources / inputs â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    /// <summary>User-facing source list â€” filtered to entries the receiver has not hidden.</summary>
    public ObservableCollection<SourceEntry> Sources { get; } = new();

    // â”€â”€ Now Playing (HEOS) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    [ObservableProperty] private string _playingTitle = "";
    [ObservableProperty] private string _playingArtist = "";
    [ObservableProperty] private string _playingAlbum = "";
    [ObservableProperty] private string _playingArtUrl = "";
    [ObservableProperty] private bool _isPlaying;

    // ── Analog tuner ────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TunerFrequencyLabel))]
    private TunerBand _tunerBand = TunerBand.FM;

    /// <summary>MHz on FM, kHz on AM. 0 until the receiver reports it.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TunerFrequencyLabel))]
    private double _tunerFrequency;

    /// <summary>AUTO = seek + stereo reception; MANUAL = step tuning (mono on FM).</summary>
    [ObservableProperty] private bool _tunerAutoMode = true;

    /// <summary>RDS station name, on models that report <c>TFANNAME</c>.</summary>
    [ObservableProperty] private string _tunerStationName = "";

    public string TunerFrequencyLabel => TunerFrequency > 0
        ? DenonProtocol.FormatTunerLabel(TunerBand, TunerFrequency)
        : "-";

    /// <summary>Named presets stored in the receiver (from <c>OPTPN</c>), unused slots excluded.</summary>
    public ObservableCollection<TunerPreset> TunerPresets { get; } = new();

    public ReceiverState()
    {
        Channels = new ObservableCollection<ChannelState>(
            DenonProtocol.SignalChannels.Select(c => new ChannelState(c)));
        Main.IsPresent = true;
    }

    public ChannelState? Channel(string label) =>
        Channels.FirstOrDefault(c => c.Label == label);
}

/// <summary>A tuner preset slot as stored in the receiver (1..56).</summary>
public sealed record TunerPreset(int Number, string Name, TunerBand Band, double Frequency);

/// <summary>Entry in the source list. Value is the token sent to the AVR (SI ...).</summary>
public partial class SourceEntry : ObservableObject
{
    [ObservableProperty] private string _token = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Caption))]
    [NotifyPropertyChangedFor(nameof(ReceiverName))]
    private string _display = "";

    /// <summary>Name set on the receiver (from <c>SSFUN</c>).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Caption))]
    [NotifyPropertyChangedFor(nameof(ReceiverName))]
    private string _rename = "";

    /// <summary>Name set in this app; wins over the receiver's.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Caption))]
    private string _alias = "";

    /// <summary>Hidden from the source pickers in this app.</summary>
    [ObservableProperty] private bool _isHidden;

    /// <summary>The receiver's own name for the input, ignoring the app alias.</summary>
    public string ReceiverName => string.IsNullOrWhiteSpace(Rename) ? Display : Rename;

    public string Caption =>
        !string.IsNullOrWhiteSpace(Alias) ? Alias
        : !string.IsNullOrWhiteSpace(Rename) ? Rename
        : Display;
}

/// <summary>One Quick Select / Smart Select memory slot.</summary>
public partial class QuickSelectSlot : ObservableObject
{
    public int Number { get; init; }
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private bool _isActive;
}

