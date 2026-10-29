// AVR Desktop Control
// Copyright (c) 2026 Sergio (@sreddev)
// Licensed under MIT License
// https://github.com/sreddev/AVR-desktop-control

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AvrRemote.Avr;
using AvrRemote.Discovery;

namespace AvrRemote.Services;

/// <summary>
/// Single facade every view model talks to. Owns the telnet connection, the
/// HTTP fallback and the observable state. All state mutations are marshalled
/// onto the UI dispatcher so bindings always fire on the right thread â€” that's
/// the difference between the status bar lighting up and staying grey.
/// </summary>
public sealed class ReceiverService : IAsyncDisposable
{
    private readonly AppSettings _settings;

    private AvrTelnetClient? _telnet;
    private AvrHttpClient? _http;
    private AvrEventParser? _parser;
    private CancellationTokenSource? _pollCts;

    public ReceiverState State { get; } = new();

    public event EventHandler? StateChanged;
    public event EventHandler? Connected;   // fired once per successful ConnectAsync

    public ReceiverService(AppSettings settings)
    {
        _settings = settings;

        // Extended parameters send themselves when the user changes them.
        foreach (var s in State.Settings.All)
            s.CommandRequested += OnSettingCommand;

        // Tone, subwoofer and zone volumes are bound straight to the state, so
        // user edits are picked up here. Receiver echoes are skipped.
        State.PropertyChanged += OnStatePropertyChanged;
        State.Main.PropertyChanged += OnMainPropertyChanged;
        State.Zone2.PropertyChanged += OnZonePropertyChanged;
        State.Zone3.PropertyChanged += OnZonePropertyChanged;
    }

    /// <summary>Signal, Quick Select and zone queries sent on every connection.</summary>
    private static readonly string[] ExtendedQueries =
    {
        "Z3?",
        "MSQUICK ?", "MSSMART ?", "SSQSNZMA ?",
        "SYSMI ?", "SSINFAISSIG ?", "SSINFSIGRES ?", "OPINFINS ?", "OPINFASP ?"
    };

    private static readonly string[] SignalQueries =
    {
        "MS?", "SYSMI ?", "SSINFAISSIG ?", "SSINFAISFSV ?", "SSINFSIGRES ?", "OPINFINS ?", "OPINFASP ?"
    };

    // â”€â”€ Connection â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public async Task<bool> ConnectAsync(ReceiverDescriptor descriptor, CancellationToken ct = default)
    {
        await DisconnectAsync().ConfigureAwait(false);

        // Identity: pushed to UI first so the sidebar and status bar update
        // instantly, even before telnet negotiates.
        Ui(() =>
        {
            State.IsConnecting = true;
            State.PowerKnown = false;
            State.PowerTransit = PowerTransit.None;
            State.IpAddress = descriptor.Host;
            State.ModelName = descriptor.Model;
            State.Brand = descriptor.Manufacturer;
            State.FriendlyName = string.IsNullOrWhiteSpace(descriptor.FriendlyName)
                ? descriptor.Model
                : descriptor.FriendlyName;
        });

        _telnet = new AvrTelnetClient(descriptor.Host);
        _http = new AvrHttpClient(descriptor.Host);
        _parser = new AvrEventParser(State);
        _telnet.LineReceived += (_, line) => _parser.Feed(line);
        _telnet.ConnectionChanged += (_, ok) => Ui(() =>
        {
            // Telnet dropping doesn't mean we lost the receiver â€” HTTP might
            // still respond. So we only flag as disconnected when both are
            // unreachable (checked at query time by SendOrHttp).
            if (ok) State.IsConnected = true;
            StateChanged?.Invoke(this, EventArgs.Empty);
        });

        // A different receiver may lack features the previous one had.
        Ui(() =>
        {
            State.Settings.ResetSupport();
            State.Zone2.IsPresent = false;
            State.Zone3.IsPresent = false;
            State.QuickSelectPrefix = "";
            State.QuickSelects.Clear();
        });

        var telnetOk = await _telnet.ConnectAsync(ct).ConfigureAwait(false);
        bool httpOk = false;
        if (telnetOk)
        {
            await _telnet.SendManyAsync(
                "ZM?", "MV?", "MU?", "SI?", "MS?",
                "PSBAS ?", "PSTRE ?", "PSTONE CTRL ?", "PSSWL ?",
                "SSINFAISFSV ?", "ECO?",
                "Z2?", "NSFRN ?",
                "CV?",
                "SSFUN ?", "SSSOD ?",
                "SSSPC ?",                // speaker configuration (which channels exist)
                "TFAN?", "TMAN?", "TFANNAME?",
                "OPTPN ?"                 // tuner preset names + frequencies
            ).ConfigureAwait(false);
            await _telnet.SendManyAsync(ExtendedQueries).ConfigureAwait(false);
            await _telnet.SendManyAsync(State.Settings.All
                .Where(s => s.Query is not null)
                .Select(s => s.Query!)
                .Distinct()
                .ToArray()).ConfigureAwait(false);
        }
        else
        {
            httpOk = await HydrateFromHttpAsync(ct).ConfigureAwait(false);
        }

        var success = telnetOk || httpOk;
        Ui(() =>
        {
            State.IsConnected = success;
            State.IsConnecting = false;
        });
        if (telnetOk) _ = EnsurePowerKnownAsync(ct);
        if (success)
        {
            _settings.RememberDevice(descriptor.Host,
                                     descriptor.FriendlyName,
                                     descriptor.Model);
            _settings.LastHost = descriptor.Host;
            _settings.LastFriendlyName = descriptor.FriendlyName;
            _settings.Save();
            Connected?.Invoke(this, EventArgs.Empty);
        }

        StartResilienceLoop();
        return success;
    }

    public async Task DisconnectAsync()
    {
        _pollCts?.Cancel();
        _pollCts = null;
        if (_telnet is not null)
        {
            await _telnet.DisposeAsync().ConfigureAwait(false);
            _telnet = null;
        }
        Ui(() => State.IsConnected = false);
    }

    private async Task<bool> HydrateFromHttpAsync(CancellationToken ct)
    {
        if (_http is null) return false;
        var status = await _http.GetMainStatusAsync(ct).ConfigureAwait(false);
        if (status is null) return false;
        Ui(() =>
        {
            State.Main.IsOn = status.Value.Power;
            State.PowerKnown = true;
            State.Main.Source = status.Value.Input;
            State.Main.SourceFriendly = status.Value.Input;
            State.Main.Volume = status.Value.Volume;
            State.Main.IsMuted = status.Value.Mute;
        });
        return true;
    }

    /// <summary>
    /// Telnet normally answers ZM? within a second. If it stays silent, ask
    /// the HTTP status so the indicators don't sit on "Connecting…".
    /// </summary>
    private async Task EnsurePowerKnownAsync(CancellationToken ct)
    {
        try { await Task.Delay(3000, ct).ConfigureAwait(false); }
        catch (OperationCanceledException) { return; }
        if (State.PowerKnown) return;
        if (!await HydrateFromHttpAsync(ct).ConfigureAwait(false))
            Ui(() => State.PowerKnown = true);   // fall back to the last ZM state
    }

    /// <summary>
    /// Keeps the socket alive and re-hydrates state after brief drops. Runs on
    /// a background thread; every state touch is dispatched to the UI.
    /// </summary>
    private void StartResilienceLoop()
    {
        _pollCts = new CancellationTokenSource();
        var token = _pollCts.Token;
        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try { await Task.Delay(TimeSpan.FromSeconds(15), token).ConfigureAwait(false); }
                catch { return; }
                if (_telnet is null) return;
                if (!_telnet.IsConnected)
                {
                    var ok = await _telnet.ConnectAsync(token).ConfigureAwait(false);
                    if (ok)
                        await _telnet.SendManyAsync("ZM?", "MV?", "MU?", "SI?", "MS?", "Z2?", "Z3?");
                    else
                        await HydrateFromHttpAsync(token).ConfigureAwait(false);
                }
                else
                {
                    await _telnet.SendAsync("ZM?", token).ConfigureAwait(false);
                }
            }
        }, token);
    }

    // â”€â”€ Commands â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    /// <summary>
    /// Powers on the MAIN zone only. We deliberately avoid <c>PWON</c> because
    /// on Denon receivers that wakes every zone (including Zone 2), which is
    /// not what the user expects when pressing the main power button.
    /// </summary>
    public async Task PowerOnAsync()
    {
        BeginPowerTransit(PowerTransit.TurningOn);
        await SendOrHttp("ZMON").ConfigureAwait(false);
        // Belt and braces: if the unit came out of standby it may have restored
        // Zone 2, so force it off unless the user turns it on explicitly.
        await SendOrHttp("Z2OFF").ConfigureAwait(false);
    }

    /// <summary>
    /// Turns the whole unit off. Zone 2 goes down with it, which matches the
    /// expectation that Zone 2 never stays alive after Main is switched off.
    /// </summary>
    public async Task StandbyAsync()
    {
        BeginPowerTransit(PowerTransit.GoingToStandby);
        await SendOrHttp("Z2OFF").ConfigureAwait(false);
        await SendOrHttp("PWSTANDBY").ConfigureAwait(false);
    }

    public async Task TogglePowerAsync()
    {
        if (State.Main.IsOn) await StandbyAsync();
        else                 await PowerOnAsync();
    }

    private CancellationTokenSource? _transitCts;

    /// <summary>
    /// Shows "Turning on…" / "Going to standby…" until the receiver confirms
    /// (see <see cref="ReceiverState"/>), or for 10 seconds at most.
    /// </summary>
    private void BeginPowerTransit(PowerTransit transit)
    {
        _transitCts?.Cancel();
        var cts = _transitCts = new CancellationTokenSource();
        Ui(() => State.PowerTransit = transit);
        _ = Task.Run(async () =>
        {
            try { await Task.Delay(TimeSpan.FromSeconds(10), cts.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
            Ui(() => { if (State.PowerTransit == transit) State.PowerTransit = PowerTransit.None; });
        });
    }

    public Task MuteAsync(bool on) => SendOrHttp(on ? "MUON" : "MUOFF");
    public Task ToggleMuteAsync() => MuteAsync(!State.Main.IsMuted);

    public Task SetVolumeAsync(double value) => SendOrHttp("MV" + AvrProtocol.FormatVolume(value));
    public Task VolumeUpAsync() => SendOrHttp("MVUP");
    public Task VolumeDownAsync() => SendOrHttp("MVDOWN");

    public Task SelectSourceAsync(string token) => SendOrHttp("SI" + token);
    public Task SetSurroundAsync(string mode) => SendOrHttp("MS" + mode);

    public Task SetBassAsync(double db)
    {
        var raw = 50 + (int)Math.Round(Clamp(db, -6, 6));
        return SendOrHttp("PSBAS " + raw);
    }

    public Task SetTrebleAsync(double db)
    {
        var raw = 50 + (int)Math.Round(Clamp(db, -6, 6));
        return SendOrHttp("PSTRE " + raw);
    }

    public Task SetSubwooferAsync(double db)
    {
        var raw = 50 + (int)Math.Round(Clamp(db, -12, 12));
        return SendOrHttp("PSSWL " + raw);
    }

    public Task SetToneControlAsync(bool on) => SendOrHttp("PSTONE CTRL " + (on ? "ON" : "OFF"));

    /// <summary>
    /// Adjusts an individual channel's trim on the AVR. <paramref name="db"/>
    /// is clamped to â€“12..+12 dB with 0.5 dB precision (protocol tokens 38..62).
    /// </summary>
    public Task SetChannelLevelAsync(string channel, double db)
    {
        if (!AvrProtocol.ChannelSupportsTrim(channel)) return Task.CompletedTask;

        // Mute the receiver's echo for a moment so the slider stays where the
        // user put it instead of bouncing when the confirmation arrives.
        State.Channel(channel)?.SuppressEcho(TimeSpan.FromMilliseconds(700));

        var raw = 50 + Math.Round(Clamp(db, -12, 12) * 2, MidpointRounding.AwayFromZero) / 2.0;
        var whole = (int)Math.Floor(raw);
        var half = raw - whole >= 0.25;
        var token = half ? $"{whole:00}5" : $"{whole:00}";
        return SendOrHttp($"CV{channel} {token}");
    }

    public Task Zone2PowerAsync(bool on) => ZonePowerAsync(ZoneId.Zone2, on);

    // ── Zones 2 and 3 ───────────────────────────────────────────────────────
    // Same command set, prefixed "Z2" or "Z3". The main zone uses ZM/MV/MU/SI.

    private static string ZonePrefix(ZoneId zone) => zone == ZoneId.Zone3 ? "Z3" : "Z2";

    public Task ZonePowerAsync(ZoneId zone, bool on) =>
        zone == ZoneId.Main
            ? (on ? PowerOnAsync() : SendOrHttp("ZMOFF"))
            : SendOrHttp(ZonePrefix(zone) + (on ? "ON" : "OFF"));

    public Task ZoneMuteAsync(ZoneId zone, bool on) =>
        zone == ZoneId.Main ? MuteAsync(on) : SendOrHttp(ZonePrefix(zone) + (on ? "MUON" : "MUOFF"));

    public Task ZoneSourceAsync(ZoneId zone, string token) =>
        zone == ZoneId.Main ? SelectSourceAsync(token) : SendOrHttp(ZonePrefix(zone) + token);

    private Task ZoneVolumeAsync(ZoneId zone, double v) =>
        SendOrHttp(ZonePrefix(zone) + AvrProtocol.FormatVolume(v));

    // ── Quick Select / Smart Select ─────────────────────────────────────────

    /// <summary>"MSQUICK" on Denon, "MSSMART" on Marantz; falls back to the brand before the receiver answers.</summary>
    private string QuickSelectCommand =>
        State.QuickSelectPrefix.Length > 0 ? State.QuickSelectPrefix
        : State.Brand.Contains("Marantz", StringComparison.OrdinalIgnoreCase) ? "MSSMART" : "MSQUICK";

    /// <summary>Recalls a slot: input, volume, surround mode and sound settings in one go.</summary>
    public Task QuickSelectAsync(int slot) => SendOrHttp(QuickSelectCommand + slot);

    /// <summary>Stores the current input, volume and sound settings in a slot.</summary>
    public Task SaveQuickSelectAsync(int slot) => SendOrHttp($"{QuickSelectCommand}{slot} MEMORY");

    // ── Signal information ──────────────────────────────────────────────────

    /// <summary>Asks for the input signal, layout, resolution and decoder again.</summary>
    public Task RefreshSignalAsync() =>
        _telnet is not null && _telnet.IsConnected
            ? _telnet.SendManyAsync(SignalQueries)
            : Task.CompletedTask;

    // ── Change tracking ─────────────────────────────────────────────────────

    // One pending command per key, so a slider drag sends only its final value.
    private readonly Dictionary<string, CancellationTokenSource> _pending = new();
    private CancellationTokenSource? _requeryCts;

    private void OnSettingCommand(AvrSetting setting, string command)
    {
        if (setting.Kind == SettingKind.Level) Debounce("set:" + setting.Key, command);
        else _ = SendOrHttp(command);
    }

    private void OnStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ReceiverState.SurroundMode))
        {
            // Surround and processing parameters are stored per sound mode.
            ScheduleRequery(includeSettings: true);
            return;
        }
        if (State.IsApplyingRemote) return;
        switch (e.PropertyName)
        {
            case nameof(ReceiverState.Bass): Debounce("bas", () => SetBassAsync(State.Bass)); break;
            case nameof(ReceiverState.Treble): Debounce("tre", () => SetTrebleAsync(State.Treble)); break;
            case nameof(ReceiverState.SubwooferLevel): Debounce("swl", () => SetSubwooferAsync(State.SubwooferLevel)); break;
            case nameof(ReceiverState.ToneControlEnabled): _ = SetToneControlAsync(State.ToneControlEnabled); break;
        }
    }

    private void OnMainPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ZoneState.Source)) ScheduleRequery(includeSettings: false);
    }

    private void OnZonePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (State.IsApplyingRemote || sender is not ZoneState zone) return;
        if (e.PropertyName == nameof(ZoneState.Volume))
            Debounce("vol" + zone.Id, () => ZoneVolumeAsync(zone.Id, zone.Volume));
    }

    /// <summary>
    /// After an input or sound mode change, re-reads the signal panel (and the
    /// per-mode parameters) once the receiver has settled.
    /// </summary>
    private void ScheduleRequery(bool includeSettings)
    {
        _requeryCts?.Cancel();
        var cts = _requeryCts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            try { await Task.Delay(1500, cts.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
            if (_telnet is null || !_telnet.IsConnected) return;
            await _telnet.SendManyAsync(SignalQueries).ConfigureAwait(false);
            if (!includeSettings) return;
            await _telnet.SendManyAsync(State.Settings.All
                .Where(s => s.Query is not null && s.Group is SettingGroup.Surround
                                                     or SettingGroup.DialogDynamics
                                                     or SettingGroup.RoomCorrection)
                .Select(s => s.Query!)
                .Distinct()
                .ToArray()).ConfigureAwait(false);
        });
    }

    private void Debounce(string key, string command) => Debounce(key, () => SendOrHttp(command));

    private void Debounce(string key, Func<Task> send)
    {
        CancellationTokenSource cts;
        lock (_pending)
        {
            if (_pending.TryGetValue(key, out var old)) old.Cancel();
            cts = _pending[key] = new CancellationTokenSource();
        }
        _ = Task.Run(async () =>
        {
            try { await Task.Delay(220, cts.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
            await send().ConfigureAwait(false);
        });
    }

    // ── Analog tuner ────────────────────────────────────────────────────────
    // TF / TP / TM commands are ignored unless the main input is TUNER, so
    // every tuner action first switches the input when needed.

    /// <summary>Tunes directly to a frequency (MHz on FM, kHz on AM).</summary>
    public async Task TuneAsync(TunerBand band, double frequency)
    {
        await EnsureTunerSourceAsync().ConfigureAwait(false);
        // Band is always sent: the cached value can be stale after a front-panel change.
        await SendOrHttp(band == TunerBand.AM ? "TMANAM" : "TMANFM").ConfigureAwait(false);
        await SendOrHttp("TFAN" + AvrProtocol.FormatTunerFrequency(frequency)).ConfigureAwait(false);
    }

    public async Task SetTunerBandAsync(TunerBand band)
    {
        await EnsureTunerSourceAsync().ConfigureAwait(false);
        await SendOrHttp(band == TunerBand.AM ? "TMANAM" : "TMANFM").ConfigureAwait(false);
    }

    /// <summary>
    /// Moves one frequency step using the receiver's regional step size.
    /// Stepping requires MANUAL mode; AUTO is restored afterwards because on
    /// Denon units MANUAL also forces FM to mono.
    /// </summary>
    public async Task TuneStepAsync(bool up)
    {
        await EnsureTunerSourceAsync().ConfigureAwait(false);
        var restoreAuto = State.TunerAutoMode;
        if (restoreAuto) await SendOrHttp("TMANMANUAL").ConfigureAwait(false);
        await SendOrHttp(up ? "TFANUP" : "TFANDOWN").ConfigureAwait(false);
        if (restoreAuto) await SendOrHttp("TMANAUTO").ConfigureAwait(false);
    }

    /// <summary>Seeks to the next receivable station (AUTO mode search).</summary>
    public async Task TuneSeekAsync(bool up)
    {
        await EnsureTunerSourceAsync().ConfigureAwait(false);
        if (!State.TunerAutoMode) await SendOrHttp("TMANAUTO").ConfigureAwait(false);
        await SendOrHttp(up ? "TFANUP" : "TFANDOWN").ConfigureAwait(false);
    }

    /// <summary>Re-reads frequency, band and RDS name from the receiver.</summary>
    public Task RefreshTunerAsync() =>
        _telnet is not null && _telnet.IsConnected
            ? _telnet.SendManyAsync("TFAN?", "TMAN?", "TFANNAME?")
            : Task.CompletedTask;

    /// <summary>Asks the receiver to re-send its preset list (arrives as OPTPN events).</summary>
    public Task RequestTunerPresetsAsync() =>
        _telnet is not null && _telnet.IsConnected
            ? _telnet.SendAsync("OPTPN ?")
            : Task.CompletedTask;

    private async Task EnsureTunerSourceAsync()
    {
        if (string.Equals(State.Main.Source, "TUNER", StringComparison.OrdinalIgnoreCase)) return;
        await SendOrHttp("SITUNER").ConfigureAwait(false);
        // The AVR drops tuner commands that arrive while the input is still switching.
        await Task.Delay(800).ConfigureAwait(false);
    }

    private async Task SendOrHttp(string cmd)
    {
        if (_telnet is not null && _telnet.IsConnected)
        {
            await _telnet.SendAsync(cmd).ConfigureAwait(false);
            return;
        }
        if (_http is not null)
            await _http.SendDirectAsync(cmd).ConfigureAwait(false);
    }

    private static double Clamp(double v, double min, double max) =>
        v < min ? min : v > max ? max : v;

    /// <summary>Marshal an action onto the WPF UI thread.</summary>
    private static void Ui(Action action)
    {
        var app = Application.Current;
        if (app is null) return;
        if (app.Dispatcher.CheckAccess()) action();
        else app.Dispatcher.Invoke(action);
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
    }
}


