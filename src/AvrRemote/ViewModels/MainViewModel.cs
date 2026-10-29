// AVR Desktop Control
// Copyright (c) 2026 Sergio (@sreddev)
// Licensed under MIT License
// https://github.com/sreddev/AVR-desktop-control

using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AvrRemote.Avr;
using AvrRemote.Discovery;
using AvrRemote.Services;

namespace AvrRemote.ViewModels;

/// <summary>Root view model. Owns connection state and the discovered device list.</summary>
public partial class MainViewModel : ObservableObject
{
    private readonly ReceiverService _receiver;
    private readonly SsdpDiscoveryService _discovery;
    private readonly AppSettings _settings;
    private readonly AutoUpdateService _updater;
    private readonly Heos.HeosService _heos;

    public ReceiverState State => _receiver.State;
    public AppSettings Settings => _settings;
    public AutoUpdateService Updater => _updater;

    public ObservableCollection<ReceiverDescriptor> Devices { get; } = new();
    public ObservableCollection<SourceTile> SourceTiles { get; } = new();
    public ObservableCollection<KnownDevice> RecentDevices { get; } = new();
    public ObservableCollection<ChannelProfile> Profiles { get; } = new();
    public ObservableCollection<RadioStation> Stations { get; } = new();

    /// <summary>Inputs not hidden by the user; what every source picker shows.</summary>
    public ObservableCollection<SourceEntry> VisibleSources { get; } = new();

    /// <summary>Main zone, Zone 2 and Zone 3; the page hides zones the receiver lacks.</summary>
    public IReadOnlyList<ZonePanel> Zones { get; }

    /// <summary>False until the receiver reports a Zone 2 or Zone 3.</summary>
    public bool HasExtraZones => State.Zone2.IsPresent || State.Zone3.IsPresent;

    /// <summary>Main zone sleep timer, shown on the dashboard and in the widget.</summary>
    public AvrSetting MainSleep => State.Settings.ByKey["sleep"];

    // Cards of the Processing and Receiver pages.
    public SettingSection RoomCorrection { get; }
    public SettingSection DialogDynamics { get; }
    public SettingSection SurroundParameters { get; }
    public SettingSection SpeakerExtras { get; }
    public SettingSection Video { get; }
    public SettingSection DisplayPower { get; }
    public SettingSection Locks { get; }
    public SettingSection ZoneOptions { get; }

    /// <summary>Sources page: shows rename boxes and hide switches instead of plain tiles.</summary>
    [ObservableProperty] private bool _isEditingSources;

    /// <summary>Channels that the receiver reports as physically connected.</summary>
    public ObservableCollection<Avr.ChannelState> ActiveChannels { get; } = new();

    [ObservableProperty] private ReceiverDescriptor? _selectedDevice;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private string _manualHost = "";
    [ObservableProperty] private UpdateInfo? _availableUpdate;

    public MainViewModel(ReceiverService receiver, SsdpDiscoveryService discovery, AppSettings settings, AutoUpdateService updater, Heos.HeosService heos)
    {
        _receiver = receiver;
        _discovery = discovery;
        _settings = settings;
        _updater = updater;
        _heos = heos;
        _manualHost = settings.LastHost ?? "";

        // Seed the tiles used by the Sources page. Only the sources the AVR
        // has not hidden are shown; renamed inputs preserve the user label.
        foreach (var kv in AvrProtocol.DefaultSources)
            SourceTiles.Add(new SourceTile(kv.Key, kv.Value));

        SyncRecentDevices();
        SyncProfiles();
        SyncStations();

        var set = State.Settings;
        RoomCorrection = new SettingSection("Audyssey & room correction",
            "Room EQ and loudness compensation measured during setup.",
            set.InGroup(SettingGroup.RoomCorrection));
        DialogDynamics = new SettingSection("Dialog & dynamics",
            "Speech clarity and dynamic range.",
            set.InGroup(SettingGroup.DialogDynamics));
        SurroundParameters = new SettingSection("Surround parameters",
            "Stored per sound mode; some only take effect in specific modes.",
            set.InGroup(SettingGroup.Surround));
        SpeakerExtras = new SettingSection("Speakers & extras",
            "Model-specific outputs and enhancers.",
            set.InGroup(SettingGroup.Extras));
        Video = new SettingSection("Video & A/V sync",
            "HDMI routing, picture and lip-sync.",
            set.InGroup(SettingGroup.Video));
        DisplayPower = new SettingSection("Display & power",
            "Front panel and energy saving.",
            set.InGroup(SettingGroup.DisplayPower));
        Locks = new SettingSection("Locks & limits",
            "Protect the volume and the controls.",
            set.InGroup(SettingGroup.Locks));
        ZoneOptions = new SettingSection("Multi-zone",
            "Options shared by all zones.",
            set.InGroup(SettingGroup.Zones));

        Zones = new[]
        {
            new ZonePanel(State.Main, set.ByKey["sleep"]),
            new ZonePanel(State.Zone2, set.ByKey["z2sleep"]),
            new ZonePanel(State.Zone3, set.ByKey["z3sleep"])
        };

        foreach (var z in new[] { State.Zone2, State.Zone3 })
            z.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(Avr.ZoneState.IsPresent)) OnPropertyChanged(nameof(HasExtraZones));
            };

        // Apply the user's names and hidden flags to inputs as the receiver reports them.
        State.Sources.CollectionChanged += (_, e) =>
        {
            if (e.NewItems is not null)
                foreach (SourceEntry src in e.NewItems) AttachSource(src);
            RebuildVisibleSources();
        };

        // Tuner: keep the "now playing" highlight in step with the receiver,
        // and re-read the tuner whenever the input switches to it.
        State.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ReceiverState.TunerBand)
                               or nameof(ReceiverState.TunerFrequency)
                               or nameof(ReceiverState.TunerStationName))
                UpdateActiveStation();
        };
        State.Main.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(Avr.ZoneState.Source)) return;
            OnPropertyChanged(nameof(IsTunerSource));
            if (IsTunerSource) _ = _receiver.RefreshTunerAsync();
        };

        // The receiver sends its 56 preset slots as a burst of OPTPN lines.
        // Wait for the burst to settle, then seed an empty station list once.
        _presetImportDebounce = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(600)
        };
        _presetImportDebounce.Tick += (_, _) =>
        {
            _presetImportDebounce.Stop();
            if (_settings.RadioPresetsImported || _settings.RadioStations.Count > 0) return;
            var added = ImportReceiverPresets();
            if (added > 0) TunerMessage = L.TunerImported(added);
        };
        State.TunerPresets.CollectionChanged += (_, _) =>
        {
            _presetImportDebounce.Stop();
            _presetImportDebounce.Start();
        };
        // Connected fires from a background thread (ReceiverService.ConnectAsync),
        // so we hop to the UI dispatcher before mutating the observable collection.
        _receiver.Connected += (_, _) => System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            SyncRecentDevices();
            RefreshActiveChannels();
        });

        // Keep the channel list in sync as the AVR reports speaker changes.
        foreach (var ch in State.Channels)
            ch.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(Avr.ChannelState.IsSpeakerPresent))
                    System.Windows.Application.Current?.Dispatcher.Invoke(RefreshActiveChannels);
            };

        // Volume slider: when the user drags it the binding changes State.Main.Volume.
        // We listen to that change and send the command to the AVR with debounce.
        State.Main.PropertyChanged += OnMainZonePropertyChanged;

        // Zone 2 must never outlive the main zone. If Main reports OFF while
        // Zone 2 is still ON, push Z2OFF so the receiver matches expectations.
        State.Main.PropertyChanged += async (_, e) =>
        {
            if (e.PropertyName != nameof(Avr.ZoneState.IsOn)) return;
            if (!State.Main.IsOn && State.Zone2.IsOn)
                await _receiver.Zone2PowerAsync(false);
        };

        // Keep the percentage calculation accurate once the receiver reports
        // its configured maximum.
        State.Main.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Avr.ZoneState.VolumeMax))
                Converters.VolumeLabelConverter.Max = State.Main.VolumeMax;
        };

        Converters.VolumeLabelConverter.Unit = _settings.VolumeUnit;
        Converters.VolumeLabelConverter.Max = State.Main.VolumeMax;
    }

    private System.Threading.Timer? _volumeDebounce;

    // ── Sources: app-side names and hiding ──────────────────────────────────

    private void AttachSource(SourceEntry src)
    {
        src.Alias = _settings.SourceAliases.TryGetValue(src.Token, out var alias) ? alias : "";
        src.IsHidden = _settings.HiddenSources.Contains(src.Token, StringComparer.OrdinalIgnoreCase);
        src.PropertyChanged -= OnSourceEdited;
        src.PropertyChanged += OnSourceEdited;
    }

    private void OnSourceEdited(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not SourceEntry src) return;
        if (e.PropertyName == nameof(SourceEntry.Alias))
        {
            var alias = (src.Alias ?? "").Trim();
            if (alias.Length == 0) _settings.SourceAliases.Remove(src.Token);
            else _settings.SourceAliases[src.Token] = alias;
            _settings.Save();
            if (string.Equals(State.Main.Source, src.Token, StringComparison.OrdinalIgnoreCase))
                State.Main.SourceFriendly = src.Caption;
        }
        else if (e.PropertyName == nameof(SourceEntry.IsHidden))
        {
            _settings.HiddenSources.RemoveAll(t => string.Equals(t, src.Token, StringComparison.OrdinalIgnoreCase));
            if (src.IsHidden) _settings.HiddenSources.Add(src.Token);
            _settings.Save();
            RebuildVisibleSources();
        }
    }

    private void RebuildVisibleSources()
    {
        VisibleSources.Clear();
        foreach (var src in State.Sources)
            if (!src.IsHidden) VisibleSources.Add(src);
    }

    [RelayCommand]
    private void ToggleSourceEditing() => IsEditingSources = !IsEditingSources;

    // ── Quick Select / Smart Select ─────────────────────────────────────────

    [RelayCommand]
    private async Task QuickSelectAsync(QuickSelectSlot? slot)
    {
        if (slot is null) return;
        await _receiver.QuickSelectAsync(slot.Number);
    }

    /// <summary>Stores the receiver's current input, volume and sound settings in the slot.</summary>
    [RelayCommand]
    private async Task SaveQuickSelectAsync(QuickSelectSlot? slot)
    {
        if (slot is null) return;
        await _receiver.SaveQuickSelectAsync(slot.Number);
        StatusMessage = $"Current settings stored in {slot.Name}";
    }

    // ── Zones ───────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task ToggleZonePowerAsync(ZoneState? zone)
    {
        if (zone is null) return;
        await _receiver.ZonePowerAsync(zone.Id, !zone.IsOn);
    }

    [RelayCommand]
    private async Task ToggleZoneMuteAsync(ZoneState? zone)
    {
        if (zone is null) return;
        await _receiver.ZoneMuteAsync(zone.Id, !zone.IsMuted);
    }

    /// <summary>Called by the Zones page when the user picks an input for a zone.</summary>
    public Task SetZoneSourceAsync(ZoneState zone, string token) =>
        _receiver.ZoneSourceAsync(zone.Id, token);

    // ── Signal ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private Task RefreshSignalAsync() => _receiver.RefreshSignalAsync();

    private void OnMainZonePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(Avr.ZoneState.Volume)) return;
        // Clamp to AVR's real max (even though slider goes to 150 visually)
        var vol = Math.Max(0, Math.Min(State.Main.Volume, State.Main.VolumeMax));
        
        // Debounce: wait 300ms after last change before sending
        _volumeDebounce?.Dispose();
        _volumeDebounce = new System.Threading.Timer(_ =>
        {
            _ = _receiver.SetVolumeAsync(vol);
        }, null, 300, System.Threading.Timeout.Infinite);
    }

    private void SyncRecentDevices()
    {
        RecentDevices.Clear();
        foreach (var d in _settings.KnownDevices)
            RecentDevices.Add(d);
    }

    private void SyncProfiles()
    {
        Profiles.Clear();
        foreach (var p in _settings.ChannelProfiles)
            Profiles.Add(p);
    }

    /// <summary>
    /// Rebuilds <see cref="ActiveChannels"/> from the speaker matrix so the UI
    /// only shows sliders for speakers that actually exist in the setup.
    /// SW is excluded on purpose: the subwoofer uses its own protocol command
    /// (<c>PSSWL</c>) and already has a dedicated card in the Sound page.
    /// </summary>
    public void RefreshActiveChannels()
    {
        // Use IsSpeakerPresent (state 1 or 2) rather than IsSpeakerActive so the
        // list shows every speaker wired to the receiver. A channel can be
        // connected but idle — e.g. the centre and surrounds while the current
        // mode is plain STEREO — and the user still wants to trim it.
        var active = State.Channels
            .Where(c => c.IsSpeakerPresent
                     && c.Label != "SW"
                     && Avr.AvrProtocol.ChannelSupportsTrim(c.Label))
            .ToList();

        // Only rebuild when the set actually changed, otherwise the ItemsControl
        // flickers and loses slider focus while the user is dragging.
        if (active.Count == ActiveChannels.Count &&
            active.Select(c => c.Label).SequenceEqual(ActiveChannels.Select(c => c.Label)))
            return;

        ActiveChannels.Clear();
        foreach (var c in active) ActiveChannels.Add(c);
    }

    /// <summary>
    /// Flattens every trim back to 0 dB: channels, subwoofer, bass and treble.
    /// Local state is written first so the sliders snap immediately rather than
    /// waiting for the receiver to echo each confirmation.
    /// </summary>
    [RelayCommand]
    private async Task NormalizeChannelsAsync()
    {
        foreach (var ch in ActiveChannels)
        {
            ch.LevelDb = 0;
            await _receiver.SetChannelLevelAsync(ch.Label, 0);
        }

        State.SubwooferLevel = 0;
        State.Bass = 0;
        State.Treble = 0;

        await _receiver.SetSubwooferAsync(0);
        await _receiver.SetBassAsync(0);
        await _receiver.SetTrebleAsync(0);

        StatusMessage = "Levels normalized to 0 dB";
    }

    /// <summary>Available units for the volume readout, bound to the Settings combo.</summary>
    public IReadOnlyList<VolumeUnitOption> VolumeUnits { get; } = new[]
    {
        new VolumeUnitOption(VolumeUnit.Absolute, "Absolute value (52.5)"),
        new VolumeUnitOption(VolumeUnit.Decibels, "Decibels (-27.5 dB)"),
        new VolumeUnitOption(VolumeUnit.Percent,  "Percent (56%)")
    };

    public VolumeUnitOption SelectedVolumeUnit
    {
        get => VolumeUnits.FirstOrDefault(u => u.Unit == _settings.VolumeUnit) ?? VolumeUnits[0];
        set
        {
            if (value is null || value.Unit == _settings.VolumeUnit) return;
            _settings.VolumeUnit = value.Unit;
            _settings.Save();
            ApplyVolumeUnit();
            OnPropertyChanged();
        }
    }

    /// <summary>Theme choices, bound to the Settings combo.</summary>
    public IReadOnlyList<ThemeOption> Themes { get; } = new[]
    {
        new ThemeOption(AppTheme.Dark,  "Dark"),
        new ThemeOption(AppTheme.Light, "Light")
    };

    public ThemeOption SelectedTheme
    {
        get => Themes.FirstOrDefault(t => t.Theme == _settings.Theme) ?? Themes[0];
        set
        {
            if (value is null || value.Theme == _settings.Theme) return;
            _settings.Theme = value.Theme;
            _settings.Save();
            OnPropertyChanged();
            OnPropertyChanged(nameof(ThemeRestartPending));
        }
    }

    /// <summary>True when the saved theme differs from the one this process is running.</summary>
    public bool ThemeRestartPending => _settings.Theme != App.RunningTheme;

    [RelayCommand]
    private async Task RestartForThemeAsync()
    {
        if (System.Windows.Application.Current is App app)
            await app.RestartAsync();
    }

    /// <summary>
    /// Pushes the chosen unit into the converter and nudges every volume binding
    /// so the labels re-render without needing a restart.
    /// </summary>
    private void ApplyVolumeUnit()
    {
        Converters.VolumeLabelConverter.Unit = _settings.VolumeUnit;
        Converters.VolumeLabelConverter.Max = State.Main.VolumeMax;

        // Force the bindings to re-evaluate by re-setting the same value.
        var v = State.Main.Volume;
        State.Main.Volume = v == 0 ? 0.01 : 0;
        State.Main.Volume = v;
    }

    /// <summary>
    /// Captures the complete sound state — every channel trim, subwoofer, bass,
    /// treble and the tone-control switch — under the next free slot.
    /// </summary>
    [RelayCommand]
    private void SaveProfile()
    {
        var profile = new ChannelProfile
        {
            Name = $"Profile {_settings.ChannelProfiles.Count + 1}",
            SubwooferLevel = State.SubwooferLevel,
            Bass = State.Bass,
            Treble = State.Treble,
            ToneControlEnabled = State.ToneControlEnabled
        };

        // Store every channel the receiver knows about, not just the ones the
        // current surround mode happens to drive.
        foreach (var ch in State.Channels)
            if (ch.IsSpeakerPresent && Avr.AvrProtocol.ChannelSupportsTrim(ch.Label))
                profile.Levels[ch.Label] = ch.LevelDb;

        _settings.ChannelProfiles.Add(profile);
        _settings.Save();
        SyncProfiles();
        StatusMessage = $"Profile saved: {profile.Name}";
    }

    /// <summary>Applies a saved profile to the receiver and to local state.</summary>
    [RelayCommand]
    private async Task LoadProfileAsync(ChannelProfile? profile)
    {
        if (profile is null) return;

        foreach (var kv in profile.Levels)
        {
            var ch = State.Channel(kv.Key);
            if (ch is not null) ch.LevelDb = kv.Value;
            await _receiver.SetChannelLevelAsync(kv.Key, kv.Value);
        }

        State.SubwooferLevel = profile.SubwooferLevel;
        State.Bass = profile.Bass;
        State.Treble = profile.Treble;
        State.ToneControlEnabled = profile.ToneControlEnabled;

        await _receiver.SetSubwooferAsync(profile.SubwooferLevel);
        await _receiver.SetToneControlAsync(profile.ToneControlEnabled);
        await _receiver.SetBassAsync(profile.Bass);
        await _receiver.SetTrebleAsync(profile.Treble);

        StatusMessage = $"Profile loaded: {profile.Name}";
    }

    /// <summary>Removes a saved profile.</summary>
    [RelayCommand]
    private void DeleteProfile(ChannelProfile? profile)
    {
        if (profile is null) return;
        _settings.ChannelProfiles.Remove(profile);
        _settings.Save();
        SyncProfiles();
        StatusMessage = $"Profile deleted: {profile.Name}";
    }

    /// <summary>Writes profile changes (e.g. an inline rename) back to disk.</summary>
    public void PersistProfiles() => _settings.Save();

    // ── Tuner ───────────────────────────────────────────────────────────────

    /// <summary>Text typed in the manual tuning box ("95.5" or "1010").</summary>
    [ObservableProperty] private string _manualFrequency = "";

    /// <summary>Validation / feedback line shown on the Radio page.</summary>
    [ObservableProperty] private string _tunerMessage = "";

    [ObservableProperty] private string _stationEditorName = "";
    [ObservableProperty] private string _stationEditorFrequency = "";

    /// <summary>Station being edited, or null when the editor adds a new one.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditingStation))]
    [NotifyPropertyChangedFor(nameof(StationEditorTitle))]
    private RadioStation? _editingStation;

    public bool IsEditingStation => EditingStation is not null;
    public string StationEditorTitle => IsEditingStation ? L.TunerEditStation : L.TunerAddStation;

    public bool IsTunerSource =>
        string.Equals(State.Main.Source, "TUNER", StringComparison.OrdinalIgnoreCase);

    /// <summary>Saved name for the tuned frequency, falling back to the RDS name.</summary>
    public string CurrentStationName =>
        Stations.FirstOrDefault(s => s.IsActive)?.Name ?? State.TunerStationName;

    /// <summary>Name from the saved list only; empty for a station that isn't saved.</summary>
    public string SavedStationName => Stations.FirstOrDefault(s => s.IsActive)?.Name ?? "";

    private void SyncStations()
    {
        Stations.Clear();
        foreach (var s in _settings.RadioStations)
        {
            s.Number = Stations.Count + 1;
            Stations.Add(s);
        }
        UpdateActiveStation();
    }

    private void UpdateActiveStation()
    {
        foreach (var s in Stations)
            s.IsActive = State.TunerFrequency > 0 && s.Matches(State.TunerBand, State.TunerFrequency);
        OnPropertyChanged(nameof(CurrentStationName));
        OnPropertyChanged(nameof(SavedStationName));
    }

    [RelayCommand]
    private async Task TuneToManualFrequencyAsync()
    {
        if (!AvrProtocol.TryParseUserFrequency(ManualFrequency, out var band, out var freq))
        {
            TunerMessage = L.TunerInvalidFrequency;
            return;
        }
        TunerMessage = "";
        await _receiver.TuneAsync(band, freq);
    }

    [RelayCommand]
    private async Task SetTunerBandAsync(string? band)
    {
        if (!Enum.TryParse<TunerBand>(band, out var b)) return;
        await _receiver.SetTunerBandAsync(b);
    }

    [RelayCommand] private Task TuneStepUpAsync() => _receiver.TuneStepAsync(up: true);
    [RelayCommand] private Task TuneStepDownAsync() => _receiver.TuneStepAsync(up: false);
    [RelayCommand] private Task TuneSeekUpAsync() => _receiver.TuneSeekAsync(up: true);
    [RelayCommand] private Task TuneSeekDownAsync() => _receiver.TuneSeekAsync(up: false);

    [RelayCommand]
    private async Task SwitchToTunerAsync() => await _receiver.SelectSourceAsync("TUNER");

    [RelayCommand]
    private async Task PlayStationAsync(RadioStation? station)
    {
        if (station is null) return;
        TunerMessage = "";
        await _receiver.TuneAsync(station.Band, station.Frequency);
    }

    [RelayCommand] private Task NextStationAsync() => StepSavedStationAsync(+1);
    [RelayCommand] private Task PreviousStationAsync() => StepSavedStationAsync(-1);

    /// <summary>
    /// Cycles through the saved list, wrapping at both ends. When the current
    /// frequency isn't a saved one, "next" starts from the first entry and
    /// "previous" from the last.
    /// </summary>
    private Task StepSavedStationAsync(int delta)
    {
        if (Stations.Count == 0 || Stations.All(s => s.Skip)) return Task.CompletedTask;
        var current = Stations.ToList().FindIndex(s => s.IsActive);
        var next = current < 0 ? (delta > 0 ? -1 : Stations.Count) : current;
        // Walk past stations marked "skip"; at least one is playable (checked above).
        do next = (next + delta + Stations.Count) % Stations.Count;
        while (Stations[next].Skip);
        return PlayStationAsync(Stations[next]);
    }

    /// <summary>Includes or excludes a station from next / previous cycling.</summary>
    [RelayCommand]
    private void ToggleStationSkip(RadioStation? station)
    {
        if (station is null) return;
        station.Skip = !station.Skip;
        _settings.Save();
        TunerMessage = station.Skip ? $"{station.Name} is skipped by next / previous" : $"{station.Name} is back in the rotation";
    }

    /// <summary>Prefills the editor with whatever the receiver is tuned to.</summary>
    [RelayCommand]
    private void UseCurrentFrequency()
    {
        if (State.TunerFrequency <= 0) return;
        StationEditorFrequency = AvrProtocol.FormatTunerNumber(State.TunerBand, State.TunerFrequency);
        if (string.IsNullOrWhiteSpace(StationEditorName) && !string.IsNullOrWhiteSpace(State.TunerStationName))
            StationEditorName = State.TunerStationName;
    }

    [RelayCommand]
    private void EditStation(RadioStation? station)
    {
        if (station is null) return;
        EditingStation = station;
        StationEditorName = station.Name;
        StationEditorFrequency = AvrProtocol.FormatTunerNumber(station.Band, station.Frequency);
        TunerMessage = "";
    }

    [RelayCommand]
    private void CancelStationEdit()
    {
        EditingStation = null;
        StationEditorName = "";
        StationEditorFrequency = "";
        TunerMessage = "";
    }

    /// <summary>Adds a new station, or applies the editor to the one being edited.</summary>
    [RelayCommand]
    private void SaveStation()
    {
        var name = (StationEditorName ?? "").Trim();
        if (name.Length == 0)
        {
            TunerMessage = L.TunerNameRequired;
            return;
        }
        if (!AvrProtocol.TryParseUserFrequency(StationEditorFrequency, out var band, out var freq))
        {
            TunerMessage = L.TunerInvalidFrequency;
            return;
        }

        var station = EditingStation ?? new RadioStation();
        station.Name = name;
        station.Band = band;
        station.Frequency = freq;
        if (EditingStation is null) _settings.RadioStations.Add(station);
        _settings.Save();

        SyncStations();
        CancelStationEdit();
        TunerMessage = $"Station saved: {name}";
    }

    private readonly System.Windows.Threading.DispatcherTimer _presetImportDebounce;

    /// <summary>
    /// Copies the receiver's named presets into the saved list, in slot order,
    /// skipping frequencies that are already saved. Returns how many were added.
    /// </summary>
    private int ImportReceiverPresets()
    {
        var added = 0;
        foreach (var p in State.TunerPresets.OrderBy(p => p.Number))
        {
            if (_settings.RadioStations.Any(s => s.Matches(p.Band, p.Frequency))) continue;
            _settings.RadioStations.Add(new RadioStation { Name = p.Name, Band = p.Band, Frequency = p.Frequency });
            added++;
        }
        _settings.RadioPresetsImported = true;
        _settings.Save();
        SyncStations();
        return added;
    }

    /// <summary>Re-reads the receiver's presets and adds any not saved yet.</summary>
    [RelayCommand]
    private async Task ImportPresetsAsync()
    {
        await _receiver.RequestTunerPresetsAsync();
        // Replies arrive asynchronously on the dispatcher; give the burst time to land.
        await Task.Delay(1500);
        _presetImportDebounce.Stop();
        var added = ImportReceiverPresets();
        TunerMessage = added > 0 ? L.TunerImported(added) : L.TunerNoNewPresets;
    }

    [RelayCommand]
    private void DeleteStation(RadioStation? station)
    {
        if (station is null) return;
        if (ReferenceEquals(station, EditingStation)) CancelStationEdit();
        _settings.RadioStations.Remove(station);
        _settings.Save();
        SyncStations();
        TunerMessage = $"Station deleted: {station.Name}";
    }

    // ── HEOS ────────────────────────────────────────────────────────────────

    public Heos.HeosService Heos => _heos;

    [RelayCommand]
    private async Task HeosPlayPauseAsync() => await _heos.PlayPauseAsync();

    [RelayCommand]
    private async Task HeosNextAsync() => await _heos.NextAsync();

    [RelayCommand]
    private async Task HeosPreviousAsync() => await _heos.PreviousAsync();

    [RelayCommand]
    private async Task HeosRefreshAsync()
    {
        await _heos.RefreshNowPlayingAsync();
        await _heos.RefreshSourcesAsync();
        await _heos.RefreshAccountAsync();
        await _heos.RefreshQueueAsync();
    }

    [RelayCommand]
    private async Task HeosSignOutAsync() => await _heos.SignOutAsync();

    [RelayCommand]
    private async Task HeosBackAsync() => await _heos.GoBackAsync();

    [RelayCommand]
    private void HeosHome() => _heos.GoHome();

    public Task HeosSearchNowPlayingAsync(string term, string kind) =>
        _heos.SearchFromNowPlayingAsync(term, kind);

    public Task HeosSeekAsync(long ms) => _heos.SeekAsync(ms);

    [RelayCommand]
    private async Task HeosClearQueueAsync() => await _heos.ClearQueueAsync();

    [RelayCommand]
    private async Task HeosFavoritesAsync() => await _heos.OpenHeosFavoritesAsync();

    [RelayCommand]
    private async Task HeosTuneInFavoritesAsync() => await _heos.OpenTuneInFavoritesAsync();

    [RelayCommand]
    private async Task HeosAddNowPlayingToFavoritesAsync() => await _heos.AddNowPlayingToFavoritesAsync();

    public Task HeosAddToFavoritesAsync(Heos.BrowseItem i) => _heos.AddToFavoritesAsync(i);
    public Task HeosRemoveFromFavoritesAsync(Heos.BrowseItem i) => _heos.RemoveFromFavoritesAsync(i);

    // Called from the HEOS page code-behind, where the clicked item is known.
    public Task HeosOpenSourceAsync(Heos.MusicSource s) => _heos.OpenSourceAsync(s);
    public Task HeosOpenItemAsync(Heos.BrowseItem i) => _heos.OpenItemAsync(i);
    public Task HeosPlayItemAsync(Heos.BrowseItem i) => _heos.PlayItemAsync(i);
    public Task HeosQueueItemAsync(Heos.BrowseItem i) => _heos.QueueItemAsync(i);
    public Task HeosPlayQueueItemAsync(Heos.QueueItem q) => _heos.PlayQueueItemAsync(q);
    public Task HeosSearchAsync(string scid, string term) => _heos.SearchAsync(scid, term);

    /// <summary>Called by the HEOS page once the user submits the login form.</summary>
    public Task<bool> HeosSignInAsync(string user, string password) =>
        _heos.SignInAsync(user, password);

    /// <summary>Sends a single channel trim (called from the Sound page sliders).</summary>
    public Task SetChannelLevelAsync(string channel, double db) =>
        _receiver.SetChannelLevelAsync(channel, db);

    /// <summary>
    /// Called by <c>App.xaml.cs</c> at startup. Tries to reconnect to the last
    /// known device without blocking the UI thread; while waiting the user can
    /// already trigger a discovery scan.
    /// </summary>
    public async Task InitializeAsync()
    {
        // Auto-reconnect uses the fast HTTP probe against the last known host.
        // It runs before discovery so that a returning user gets a working
        // connection in ~300 ms rather than waiting for the 3 s SSDP scan.
        if (_settings.AutoConnect && !string.IsNullOrWhiteSpace(_settings.LastHost))
        {
            StatusMessage = $"Reconnecting to {_settings.LastFriendlyName ?? _settings.LastHost}…";
            var probed = await _discovery.ProbeAsync(_settings.LastHost);
            if (probed is not null)
            {
                await ConnectAsyncInternal(probed);
            }
            else
            {
                StatusMessage = "Last device is offline. Run discovery or add it manually.";
            }
        }

        // Kick off discovery in the background so the Devices page has a
        // populated list ready when the user opens it.
        _ = RefreshDevicesAsync();

        // Auto-update check
        if (_settings.AutoUpdate)
        {
            var update = await _updater.CheckAsync();
            if (update is not null) AvailableUpdate = update;
        }
    }

    [RelayCommand]
    private async Task RefreshDevicesAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Searching the network for Denon / Marantz devices…";
            var found = await _discovery.DiscoverAsync(TimeSpan.FromSeconds(3));
            Devices.Clear();
            foreach (var d in found) Devices.Add(d);
            if (Devices.Count == 0)
                StatusMessage = "No receivers found. Add the IP manually.";
            else
                StatusMessage = $"Found {Devices.Count} device(s).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (SelectedDevice is null) return;
        await ConnectAsyncInternal(SelectedDevice);
    }

    [RelayCommand]
    private async Task AddManualAsync()
    {
        var host = (ManualHost ?? "").Trim();
        if (string.IsNullOrEmpty(host)) return;
        IsBusy = true;
        try
        {
            StatusMessage = $"Probing {host}…";
            var probed = await _discovery.ProbeAsync(host);
            if (probed is null)
            {
                StatusMessage = $"Nothing answered at {host}. Check the IP and try again.";
                return;
            }
            if (!Devices.Contains(probed)) Devices.Add(probed);
            SelectedDevice = probed;
            await ConnectAsyncInternal(probed);
        }
        finally { IsBusy = false; }
    }

    private async Task ConnectAsyncInternal(ReceiverDescriptor d)
    {
        IsBusy = true;
        try
        {
            StatusMessage = $"Connecting to {d.FriendlyName ?? d.Model} at {d.Host}...";

            var ok = await _receiver.ConnectAsync(d);

            // HEOS lives on its own socket (port 1255). Connect it in the
            // background so a HEOS failure never blocks receiver control.
            if (ok) _ = _heos.ConnectAsync(d.Host);

            StatusMessage = ok
                ? ($"Connected - {State.FriendlyName}")
                : ($"Could not reach {d.Host}. Is Network Standby enabled?");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task PowerToggleAsync() => await _receiver.TogglePowerAsync();

    [RelayCommand]
    private async Task MuteToggleAsync() => await _receiver.ToggleMuteAsync();

    [RelayCommand]
    private async Task VolumeUpAsync() => await _receiver.VolumeUpAsync();

    [RelayCommand]
    private async Task VolumeDownAsync() => await _receiver.VolumeDownAsync();

    [RelayCommand]
    private async Task SelectSourceAsync(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return;
        await _receiver.SelectSourceAsync(token);
    }

    [RelayCommand]
    private async Task SetSurroundAsync(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode)) return;
        await _receiver.SetSurroundAsync(mode);
    }

    /// <summary>Sends a per-channel trim to the AVR (called by the dashboard popup).</summary>
    public Task SetChannelTrimAsync(string channel, double db) =>
        _receiver.SetChannelLevelAsync(channel, db);

    /// <summary>
    /// Explicit Zone 2 power toggle. Zone 2 is never enabled implicitly — only
    /// through this command — and it always goes down when Main powers off.
    /// </summary>
    [RelayCommand]
    private async Task ToggleZone2Async()
    {
        if (State.Zone2.IsOn)
            await _receiver.Zone2PowerAsync(false);
        else
            await _receiver.Zone2PowerAsync(true);
    }

    /// <summary>One-click connect from the sidebar shortcut list.</summary>
    [RelayCommand]
    private async Task ConnectToRecentAsync(KnownDevice? device)
    {
        if (device is null || string.IsNullOrWhiteSpace(device.Host)) return;
        StatusMessage = $"Connecting to {device.FriendlyName}…";
        var probed = await _discovery.ProbeAsync(device.Host);
        var target = probed ?? new ReceiverDescriptor(
            device.Host, "Denon", device.Model, device.FriendlyName, "");
        await ConnectAsyncInternal(target);
    }
}

/// <summary>Lightweight tile for the Sources page â€” token + caption.</summary>
public sealed record SourceTile(string Token, string Caption);

/// <summary>Combo entry for the volume-unit selector in Settings.</summary>
public sealed record ThemeOption(AppTheme Theme, string Caption);

public sealed record VolumeUnitOption(VolumeUnit Unit, string Caption)
{
    public override string ToString() => Caption;
}


