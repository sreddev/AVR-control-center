// AVR Control Center
// Copyright (c) 2026 Sergio (@sreddev)
// Licensed under MIT License
// https://github.com/sreddev/AVR-control-center

using System;
using System.Linq;
using System.Windows;

namespace AvrRemote.Avr;

public sealed class AvrEventParser
{
    private readonly ReceiverState _state;

    public AvrEventParser(ReceiverState state) => _state = state;

    public void Feed(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        Application.Current?.Dispatcher.BeginInvoke(new Action(() => Apply(line)));
    }

    /// <summary>Applies one receiver line on the calling thread. Internal for the tests.</summary>
    internal void Apply(string line)
    {
        _state.IsApplyingRemote = true;
        try { ApplyCore(line.TrimEnd()); }
        finally { _state.IsApplyingRemote = false; }
    }

    private void ApplyCore(string s)
    {
        // Catalog parameters first (Audyssey, video, dimmer, sleep timers...):
        // their prefixes are specific, and Z2SLP must not reach the zone parser.
        if (_state.Settings.TryApply(s)) return;

        // Power. Note: PW reports ON when ANY zone is on, so it is NOT the
        // main-zone state. ZM (Zone Main) is the authoritative main power flag.
        if (s == "PWSTANDBY")
        {
            // Whole unit asleep: every zone is off.
            _state.Main.IsOn = false;
            _state.Zone2.IsOn = false;
            _state.Zone3.IsOn = false;
            _state.PowerKnown = true;
            return;
        }
        if (s == "PWON") return;                       // ignore: ambiguous
        if (s == "ZMON") { _state.Main.IsOn = true; _state.PowerKnown = true; return; }
        if (s == "ZMOFF") { _state.Main.IsOn = false; _state.PowerKnown = true; return; }

        if (s.StartsWith("MVMAX ", StringComparison.Ordinal))
        {
            if (int.TryParse(s.AsSpan(6).Trim(), out var max))
                _state.Main.VolumeMax = max;
            return;
        }
        if (s.StartsWith("MV", StringComparison.Ordinal) && !s.StartsWith("MVMAX"))
        {
            var payload = s[2..];
            var mv = AvrProtocol.ParseVolume(payload);
            if (mv is not null) _state.Main.Volume = mv.Value;
            return;
        }

        if (s.StartsWith("Z2", StringComparison.Ordinal)) { ApplyZone(_state.Zone2, s[2..]); return; }
        if (s.StartsWith("Z3", StringComparison.Ordinal)) { ApplyZone(_state.Zone3, s[2..]); return; }

        if (s == "MUON") { _state.Main.IsMuted = true; return; }
        if (s == "MUOFF") { _state.Main.IsMuted = false; return; }

        if (s.StartsWith("SI", StringComparison.Ordinal))
        {
            var src = s[2..];
            _state.Main.Source = src;
            _state.Main.SourceFriendly = LookupSourceFriendly(src);
            return;
        }

        // Quick Select (Denon) / Smart Select (Marantz): "MSQUICK2", "MSSMART0" (none active).
        foreach (var qs in new[] { "MSQUICK", "MSSMART" })
        {
            if (!s.StartsWith(qs, StringComparison.Ordinal)) continue;
            var rest = s[qs.Length..].Trim();
            if (rest.EndsWith("MEMORY", StringComparison.Ordinal)) return;
            if (int.TryParse(rest, out var slot)) ApplyQuickSelect(qs, slot);
            return;
        }
        // Slot names: "SSQSNZMAQS1 Smart Select 1"
        if (s.StartsWith("SSQSNZMAQS", StringComparison.Ordinal))
        {
            var rest = s[10..];
            var space = rest.IndexOf(' ');
            if (space > 0 && int.TryParse(rest[..space], out var n))
                QuickSelectSlot(n).Name = rest[(space + 1)..].Trim();
            return;
        }
        if (s.StartsWith("SSQSN", StringComparison.Ordinal)) return;

        if (s.StartsWith("MS", StringComparison.Ordinal))
        {
            _state.SurroundMode = s[2..].Trim();
            return;
        }

        // Analog tuner. TFANNAME must be checked before the TFAN frequency.
        if (s.StartsWith("TFANNAME", StringComparison.Ordinal))
        {
            _state.TunerStationName = s[8..].Trim();
            return;
        }
        if (s.StartsWith("TFAN", StringComparison.Ordinal))
        {
            var tuned = AvrProtocol.ParseTunerFrequency(s[4..]);
            if (tuned is not null)
            {
                // A new frequency invalidates the previous station's RDS name.
                if (tuned.Value.Frequency != _state.TunerFrequency) _state.TunerStationName = "";
                _state.TunerBand = tuned.Value.Band;
                _state.TunerFrequency = tuned.Value.Frequency;
            }
            return;
        }
        if (s.StartsWith("TMAN", StringComparison.Ordinal))
        {
            foreach (var token in s[4..].Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                switch (token)
                {
                    case "FM": _state.TunerBand = TunerBand.FM; break;
                    case "AM": _state.TunerBand = TunerBand.AM; break;
                    case "AUTO": _state.TunerAutoMode = true; break;
                    case "MANUAL": _state.TunerAutoMode = false; break;
                }
            }
            return;
        }

        // Receiver preset list: OPTPN<nn><name, 8 chars><frequency, 6 digits>
        // e.g. "OPTPN01Rock FM  009220". Unused slots have a blank name.
        if (s.StartsWith("OPTPN", StringComparison.Ordinal))
        {
            if (s.Length < 13 || !int.TryParse(s.AsSpan(5, 2), out var number)) return;
            var tuned = AvrProtocol.ParseTunerFrequency(s[^6..]);
            var name = s[7..^6].Trim();
            var existing = _state.TunerPresets.FirstOrDefault(p => p.Number == number);
            if (existing is not null) _state.TunerPresets.Remove(existing);
            if (tuned is null || name.Length == 0) return;
            _state.TunerPresets.Add(new TunerPreset(number, name, tuned.Value.Band, tuned.Value.Frequency));
            return;
        }

        if (s.StartsWith("PSBAS ", StringComparison.Ordinal))
        {
            if (int.TryParse(s.AsSpan(6).Trim(), out var v)) _state.Bass = v - 50;
            return;
        }
        if (s.StartsWith("PSTRE ", StringComparison.Ordinal))
        {
            if (int.TryParse(s.AsSpan(6).Trim(), out var v)) _state.Treble = v - 50;
            return;
        }
        if (s.StartsWith("PSSWL ", StringComparison.Ordinal))
        {
            if (int.TryParse(s.AsSpan(6).Trim(), out var v)) _state.SubwooferLevel = v - 50;
            return;
        }
        if (s.StartsWith("PSTONE CTRL ", StringComparison.Ordinal))
        {
            _state.ToneControlEnabled = s.EndsWith("ON", StringComparison.Ordinal);
            return;
        }

        if (s.StartsWith("NSFRN ", StringComparison.Ordinal))
        {
            _state.FriendlyName = s[6..].Trim();
            return;
        }

        if (s.StartsWith("SYSDA", StringComparison.Ordinal))
        {
            _state.AudioFormat = AvrProtocol.PrettifyAudioFormat(s[5..]);
            return;
        }
        if (s.StartsWith("SSINFAISFSV ", StringComparison.Ordinal))
        {
            var fs = s[12..].Trim();
            _state.SampleRate = fs is "NON" or "" ? "-" : fs;
            return;
        }
        if (s.StartsWith("SSINFAISSIG ", StringComparison.Ordinal))
        {
            _state.InputSignal = AvrProtocol.DescribeSignalCode(s[12..].Trim());
            return;
        }
        // Video resolution: "SSINFSIGRES I1080P60" (input) / "SSINFSIGRES O---" (output)
        if (s.StartsWith("SSINFSIGRES ", StringComparison.Ordinal))
        {
            var v = s[12..].Trim();
            if (v.Length == 0) return;
            var res = v[1..].Trim('-', ' ');
            if (res.Length == 0) res = "-";
            if (v[0] == 'I') _state.VideoInput = res;
            else if (v[0] == 'O') _state.VideoOutput = res;
            return;
        }
        if (s.StartsWith("SYSMI ", StringComparison.Ordinal))
        {
            _state.SoundModeInfo = s[6..].Trim();
            return;
        }

        if (s.StartsWith("OPINFINS ", StringComparison.Ordinal))
        {
            ApplyMatrix(s[9..].Trim(), input: true);
            return;
        }
        if (s.StartsWith("OPINFASP ", StringComparison.Ordinal))
        {
            ApplyMatrix(s[9..].Trim(), input: false);
            return;
        }

        // Channel level trim
        if (s.StartsWith("CV", StringComparison.Ordinal) && s.Length > 2 && s != "CVEND")
        {
            var spaceIdx = s.IndexOf(' ');
            if (spaceIdx > 2 && spaceIdx < s.Length - 1)
            {
                var channel = s.Substring(2, spaceIdx - 2);
                var raw = s[(spaceIdx + 1)..].Trim();
                var parsed = AvrProtocol.ParseVolume(raw);
                if (parsed is not null)
                {
                    var target = _state.Channel(channel);
                    // Skip the echo of a command we just sent, otherwise the
                    // slider the user is dragging gets yanked back and forth.
                    if (target is not null && !target.IsEchoSuppressed)
                        target.LevelDb = parsed.Value - 50;
                }
            }
            return;
        }

        // Source renames: SSFUN<token> <user label>
        if (s.StartsWith("SSFUN", StringComparison.Ordinal))
        {
            if (s == "SSFUN END") return;
            var payload = s[5..];
            string token = "", label = "";
            foreach (var kt in new[] { "SAT/CBL", "MPLAY", "GAME1", "GAME2", "AUX1", "AUX2", "BD", "TV", "CD", "PHONO", "TUNER", "NET", "BT", "IRADIO" })
            {
                if (payload.StartsWith(kt, StringComparison.OrdinalIgnoreCase))
                { token = kt; label = payload[kt.Length..].Trim(); break; }
            }
            if (string.IsNullOrEmpty(token)) return;
            var existing = _state.Sources.FirstOrDefault(x => string.Equals(x.Token, token, StringComparison.OrdinalIgnoreCase));
            if (existing is not null) existing.Rename = label;
            else _state.Sources.Add(new SourceEntry { Token = token, Display = DefaultCaption(token), Rename = label });
            return;
        }

        // Source visibility: SSSOD<token> USE|DEL
        if (s.StartsWith("SSSOD", StringComparison.Ordinal))
        {
            if (s == "SSSOD END") return;
            var payload = s[5..];
            foreach (var kt in new[] { "SAT/CBL", "MPLAY", "GAME1", "GAME2", "AUX1", "AUX2", "BD", "TV", "CD", "PHONO", "TUNER", "NET", "BT", "IRADIO" })
            {
                if (payload.StartsWith(kt, StringComparison.OrdinalIgnoreCase))
                {
                    var rest = payload[kt.Length..].Trim();
                    if (rest == "DEL")
                    {
                        var toRemove = _state.Sources.FirstOrDefault(x => string.Equals(x.Token, kt, StringComparison.OrdinalIgnoreCase));
                        if (toRemove is not null) _state.Sources.Remove(toRemove);
                    }
                    else if (rest == "USE")
                    {
                        if (!_state.Sources.Any(x => string.Equals(x.Token, kt, StringComparison.OrdinalIgnoreCase)))
                            _state.Sources.Add(new SourceEntry { Token = kt, Display = DefaultCaption(kt), Rename = "" });
                    }
                    break;
                }
            }
            return;
        }
    }

    /// <summary>Zone 2 / Zone 3 replies, already stripped of "Z2" / "Z3".</summary>
    private void ApplyZone(ZoneState zone, string rest)
    {
        zone.IsPresent = true;
        switch (rest)
        {
            case "ON": zone.IsOn = true; return;
            case "OFF": zone.IsOn = false; return;
            case "MUON": zone.IsMuted = true; return;
            case "MUOFF": zone.IsMuted = false; return;
        }
        if (rest.Length is >= 2 and <= 3 && int.TryParse(rest, out _))
        {
            var v = AvrProtocol.ParseVolume(rest);
            if (v is not null) zone.Volume = v.Value;
            return;
        }
        // Only accept real input names: zones also report channel levels,
        // tone and other parameters on the same prefix.
        if (rest == "SOURCE" || AvrProtocol.DefaultSources.ContainsKey(rest) ||
            _state.Sources.Any(x => string.Equals(x.Token, rest, StringComparison.OrdinalIgnoreCase)))
        {
            zone.Source = rest;
            zone.SourceFriendly = rest == "SOURCE" ? "Main zone source" : LookupSourceFriendly(rest);
        }
    }

    private void ApplyQuickSelect(string prefix, int active)
    {
        _state.QuickSelectPrefix = prefix;
        if (_state.QuickSelects.Count == 0)
        {
            // Names arrive separately (SSQSNZMA); until then use the receiver's default labels.
            var title = prefix == "MSSMART" ? "Smart Select" : "Quick Select";
            for (int i = 1; i <= 4; i++)
                _state.QuickSelects.Add(new QuickSelectSlot { Number = i, Name = $"{title} {i}" });
        }
        foreach (var slot in _state.QuickSelects)
            slot.IsActive = slot.Number == active;
    }

    private QuickSelectSlot QuickSelectSlot(int number)
    {
        var slot = _state.QuickSelects.FirstOrDefault(q => q.Number == number);
        if (slot is not null) return slot;
        slot = new QuickSelectSlot { Number = number };
        var index = _state.QuickSelects.TakeWhile(q => q.Number < number).Count();
        _state.QuickSelects.Insert(index, slot);
        return slot;
    }

    private void ApplyMatrix(string payload, bool input)
    {
        var channels = _state.Channels;
        for (int i = 0; i < payload.Length && i < channels.Count; i++)
        {
            var c = payload[i];
            var active = c == '2';
            var present = c == '1' || c == '2';

            if (input)
            {
                channels[i].IsInputActive = active;
            }
            else
            {
                channels[i].IsSpeakerActive = active;
                channels[i].IsSpeakerPresent = present;
            }
        }

        if (input) _state.InputChannelLayout = AvrProtocol.DescribeLayout(payload);
        else _state.ActiveSpeakerCount = payload.Count(c => c == '2');
    }

    private static string DefaultCaption(string token) =>
        AvrProtocol.DefaultSources.TryGetValue(token, out var friendly) ? friendly : token;

    private string LookupSourceFriendly(string token)
    {
        var src = _state.Sources.FirstOrDefault(x => string.Equals(x.Token, token, StringComparison.OrdinalIgnoreCase));
        if (src is not null) return src.Caption;
        if (AvrProtocol.DefaultSources.TryGetValue(token, out var friendly)) return friendly;
        return token;
    }
}
