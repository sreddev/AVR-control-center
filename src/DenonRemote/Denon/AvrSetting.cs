// AVR Desktop Control
// Copyright (c) 2026 Sergio (@sreddev)
// Licensed under MIT License
// https://github.com/sreddev/AVR-desktop-control_tuner

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DenonRemote.Denon;

/// <summary>How a setting is presented and encoded.</summary>
public enum SettingKind
{
    /// <summary>Two states, e.g. <c>PSDYNEQ ON</c> / <c>PSDYNEQ OFF</c>.</summary>
    Toggle,
    /// <summary>One value out of a list, e.g. <c>PSDRC AUTO|LOW|MID|HI|OFF</c>.</summary>
    Choice,
    /// <summary>A number sent as a zero-padded token, e.g. <c>PSDIM 03</c>.</summary>
    Level,
    /// <summary>Fire-and-forget commands with no read-back, e.g. panel lock.</summary>
    Action
}

/// <summary>Where a setting is shown in the UI.</summary>
public enum SettingGroup
{
    /// <summary>Shown inline on the Sound page rather than in a section.</summary>
    Sound,
    RoomCorrection,
    DialogDynamics,
    Surround,
    Extras,
    Video,
    DisplayPower,
    Locks,
    Zones,
    Sleep
}

/// <summary>A selectable value. For <see cref="SettingKind.Action"/>, Value is the full command.</summary>
public sealed record SettingOption(string Value, string Caption)
{
    public override string ToString() => Caption;
}

/// <summary>
/// One receiver parameter that follows the usual telnet pattern: the command
/// is <see cref="Prefix"/> + value, the receiver confirms with the same text,
/// and <see cref="Query"/> asks for the current value.
///
/// The receiver simply stays silent for parameters it doesn't have, so
/// <see cref="IsSupported"/> only turns true once a reply was parsed. The UI
/// hides unsupported rows, which adapts every page to the connected model.
/// </summary>
public sealed class AvrSetting : ObservableObject
{
    public string Key { get; init; } = "";
    public string Label { get; init; } = "";
    public string Description { get; init; } = "";
    public SettingGroup Group { get; init; }
    public SettingKind Kind { get; init; }

    /// <summary>Command and reply prefix, including any trailing space ("PSDYNEQ ").</summary>
    public string Prefix { get; init; } = "";

    /// <summary>Status request, e.g. "PSDYNEQ ?". Null for settings without read-back.</summary>
    public string? Query { get; init; }

    // Toggle
    public string OnValue { get; init; } = "ON";
    public string OffValue { get; init; } = "OFF";

    // Choice / Action
    public ObservableCollection<SettingOption> Options { get; } = new();

    /// <summary>
    /// Choice only: accept replies that aren't in <see cref="Options"/> (added
    /// on the fly). Off when another setting shares the prefix, e.g. BTTX.
    /// </summary>
    public bool AcceptUnlisted { get; init; } = true;

    /// <summary>Choice only: caption for a value that wasn't listed, e.g. "SLP045" → "45 min".</summary>
    public Func<string, string>? CaptionFor { get; init; }

    // Level: token = round(value * Factor + Offset), zero-padded to Digits.
    private double _min, _max = 10;

    /// <summary>Slider range. Widened if the receiver reports a value outside it.</summary>
    public double Min { get => _min; init => _min = value; }
    public double Max { get => _max; init => _max = value; }
    public double Step { get; init; } = 1;
    public double Factor { get; init; } = 1;
    public double Offset { get; init; }
    public int Digits { get; init; } = 2;
    public string Unit { get; init; } = "";

    /// <summary>Raised when the user changes the value. The argument is the full command.</summary>
    public event Action<AvrSetting, string>? CommandRequested;

    private string _raw = "";
    private bool _isSupported;
    private double _number;
    private DateTime _suppressUntil;

    /// <summary>True once the receiver reported this parameter (always true for actions).</summary>
    public bool IsSupported
    {
        get => _isSupported;
        set => SetProperty(ref _isSupported, value);
    }

    /// <summary>Last value as the receiver spells it ("ON", "AUDYSSEY", "03").</summary>
    public string RawValue => _raw;

    public bool IsOn
    {
        get => string.Equals(_raw, OnValue, StringComparison.OrdinalIgnoreCase);
        set
        {
            // Writes before the receiver reported the value can only come from
            // control initialisation, never from the user (the row is hidden).
            if (!IsSupported || value == IsOn) return;
            SetLocal(value ? OnValue : OffValue);
            Request(Prefix + _raw);
        }
    }

    public SettingOption? Selected
    {
        get => Options.FirstOrDefault(o => string.Equals(o.Value, _raw, StringComparison.OrdinalIgnoreCase));
        set
        {
            if (!IsSupported || value is null || string.Equals(value.Value, _raw, StringComparison.OrdinalIgnoreCase)) return;
            SetLocal(value.Value);
            Request(Prefix + value.Value);
        }
    }

    public double Number
    {
        get => _number;
        set
        {
            // A slider coerces its value into its range and may write it back;
            // only accept values once the receiver reported one.
            if (!IsSupported) return;
            var v = Math.Clamp(Math.Round(value / Step) * Step, Math.Min(Min, Max), Math.Max(Min, Max));
            if (Math.Abs(v - _number) < 1e-9) return;
            _number = v;
            _raw = Encode(v);
            // Keep the receiver's echo from yanking the slider back mid-drag.
            _suppressUntil = DateTime.UtcNow.AddMilliseconds(900);
            OnPropertyChanged();
            OnPropertyChanged(nameof(NumberLabel));
            Request(Prefix + _raw);
        }
    }

    public string NumberLabel
    {
        get
        {
            var digits = Step < 1 ? "0.#" : "0";
            // "+ 0.0" turns -0 (LFE at 0 dB) into a plain 0.
            var text = (_number + 0.0).ToString(digits, CultureInfo.InvariantCulture);
            if (text == "-0") text = "0";
            if (Unit == "dB" && _number > 0) text = "+" + text;
            return string.IsNullOrEmpty(Unit) ? text : $"{text} {Unit}";
        }
    }

    /// <summary>Sends one of the <see cref="SettingKind.Action"/> commands.</summary>
    public void Run(SettingOption action) => Request(action.Value);

    private CommunityToolkit.Mvvm.Input.RelayCommand<SettingOption?>? _runCommand;

    /// <summary>Action buttons bind here with the option as parameter.</summary>
    public CommunityToolkit.Mvvm.Input.RelayCommand<SettingOption?> RunCommand =>
        _runCommand ??= new(o => { if (o is not null) Run(o); });

    /// <summary>
    /// Offers a receiver line to this setting. Returns false when the line is
    /// not for us (wrong prefix, or a value another setting on the same prefix owns).
    /// </summary>
    public bool TryApply(string line)
    {
        if (Kind == SettingKind.Action || !line.StartsWith(Prefix, StringComparison.Ordinal)) return false;
        var value = line[Prefix.Length..].Trim();
        if (value.Length == 0) return false;

        switch (Kind)
        {
            case SettingKind.Toggle:
                if (!string.Equals(value, OnValue, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(value, OffValue, StringComparison.OrdinalIgnoreCase))
                    return false;
                IsSupported = true;
                SetLocal(value.ToUpperInvariant());
                return true;

            case SettingKind.Choice:
                var known = Options.Any(o => string.Equals(o.Value, value, StringComparison.OrdinalIgnoreCase));
                if (!known)
                {
                    if (!AcceptUnlisted) return false;
                    Options.Add(new SettingOption(value, CaptionFor?.Invoke(value) ?? value));
                }
                IsSupported = true;
                SetLocal(Options.First(o => string.Equals(o.Value, value, StringComparison.OrdinalIgnoreCase)).Value);
                return true;

            case SettingKind.Level:
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var token))
                    return false;
                IsSupported = true;
                if (DateTime.UtcNow < _suppressUntil) return true;
                _raw = value;
                _number = (token - Offset) / Factor;
                // Never let the slider clamp (and send back) a value the receiver reported.
                if (_number < Math.Min(_min, _max)) { _min = _number; OnPropertyChanged(nameof(Min)); }
                if (_number > Math.Max(_min, _max)) { _max = _number; OnPropertyChanged(nameof(Max)); }
                OnPropertyChanged(nameof(Number));
                OnPropertyChanged(nameof(NumberLabel));
                return true;
        }
        return false;
    }

    private string Encode(double value)
    {
        var token = (int)Math.Round(value * Factor + Offset, MidpointRounding.AwayFromZero);
        return token.ToString(new string('0', Digits), CultureInfo.InvariantCulture);
    }

    private void SetLocal(string raw)
    {
        if (_raw == raw) return;
        _raw = raw;
        OnPropertyChanged(nameof(RawValue));
        OnPropertyChanged(nameof(IsOn));
        OnPropertyChanged(nameof(Selected));
    }

    private void Request(string command) => CommandRequested?.Invoke(this, command);
}

/// <summary>Every setting of a receiver, with lookups by key and by group.</summary>
public sealed class AvrSettingSet
{
    public IReadOnlyList<AvrSetting> All { get; }

    /// <summary>Key lookup for XAML: <c>{Binding State.Settings.ByKey[dyneq]}</c>.</summary>
    public IReadOnlyDictionary<string, AvrSetting> ByKey { get; }

    // Longest prefix first, so "PSDELAY " is tried before anything shorter.
    private readonly AvrSetting[] _parseOrder;

    public AvrSettingSet(IEnumerable<AvrSetting> settings)
    {
        All = settings.ToList();
        ByKey = All.ToDictionary(s => s.Key);
        _parseOrder = All.Where(s => s.Kind != SettingKind.Action)
                         .OrderByDescending(s => s.Prefix.Length)
                         .ToArray();
    }

    public IEnumerable<AvrSetting> InGroup(SettingGroup group) => All.Where(s => s.Group == group);

    /// <summary>Routes a receiver line to the first setting that accepts it.</summary>
    public bool TryApply(string line)
    {
        foreach (var s in _parseOrder)
            if (s.TryApply(line)) return true;
        return false;
    }

    /// <summary>Hides everything again, e.g. after switching to another receiver.</summary>
    public void ResetSupport()
    {
        foreach (var s in All)
            if (s.Kind != SettingKind.Action) s.IsSupported = false;
    }
}
