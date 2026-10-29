// AVR Control Center
// Copyright (c) 2026 Sergio (@sreddev)
// Licensed under MIT License
// https://github.com/sreddev/AVR-control-center

using System.Collections.Generic;

namespace AvrControlCenter.Avr;

/// <summary>
/// Static maps used to interpret Denon/Marantz AVR telnet responses.
/// Reference: Denon AVR Control Protocol (undocumented public spec used by
/// third-party integrations such as denonavr and heos-cli).
/// </summary>
public static class AvrProtocol
{
    /// <summary>
    /// Channel layout used by <c>OPINFINS</c> and <c>OPINFASP</c> strings.
    /// Each character is a digit representing the state of one channel
    /// (0 = not present / 1 = present but inactive / 2 = active).
    /// The order below matches the way modern Denon receivers (Gen 2, X-series,
    /// S-series 2020+) publish those strings.
    /// </summary>
    public static readonly string[] SignalChannels =
    {
        "FL",  "FR",  "C",   "SW",
        "SL",  "SR",  "SBL", "SBR",
        "SB",  "FHL", "FHR", "TFL",
        "TFR", "TML", "TMR", "TRL",
        "TRR", "RRL", "RRR", "FWL",
        "FWR", "LFE", "EXT"
    };

    /// <summary>
    /// Channels the AVR exposes a <c>CV&lt;label&gt;</c> trim for. LFE and EXT
    /// don't have their own per-channel level in this protocol.
    /// </summary>
    public static bool ChannelSupportsTrim(string label) => label switch
    {
        "FL" or "FR" or "C" or "SW"
        or "SL" or "SR" or "SBL" or "SBR" or "SB"
        or "FHL" or "FHR"
        or "TFL" or "TFR" or "TML" or "TMR" or "TRL" or "TRR"
        or "RRL" or "RRR" or "FWL" or "FWR" => true,
        _ => false
    };

    /// <summary>
    /// Human readable audio format detected on the current source
    /// (payload of the <c>SYSDA</c> telnet event).
    /// </summary>
    public static string PrettifyAudioFormat(string raw)
    {
        var v = (raw ?? string.Empty).Trim();
        return v switch
        {
            "PCM" => "PCM",
            "DOLBY DIGITAL" => "Dolby Digital",
            "DOLBY DIGITAL PLUS" => "Dolby Digital+",
            "DOLBY TRUEHD" => "Dolby TrueHD",
            "DTS" => "DTS",
            "DTS-HD MA" => "DTS-HD Master Audio",
            "DTS-HD HR" => "DTS-HD HR",
            _ => string.IsNullOrWhiteSpace(v) ? "-" : v
        };
    }

    /// <summary>
    /// Input signal type from <c>SSINFAISSIG</c>. Only the codes confirmed on
    /// a receiver are named; others are shown as their code.
    /// </summary>
    public static string DescribeSignalCode(string code) => code switch
    {
        "01" => "Analog",
        "02" => "PCM",
        "" => "-",
        _ => "Digital (" + code + ")"
    };

    /// <summary>
    /// Channel layout of the input signal from an <c>OPINFINS</c> matrix,
    /// counted as base.LFE.height: "2.0", "5.1", "7.1.4".
    /// </summary>
    public static string DescribeLayout(string matrix)
    {
        int main = 0, lfe = 0, height = 0;
        for (int i = 0; i < matrix.Length && i < SignalChannels.Length; i++)
        {
            if (matrix[i] != '2') continue;
            switch (SignalChannels[i])
            {
                case "SW" or "LFE": lfe = 1; break;
                case "FHL" or "FHR" or "TFL" or "TFR" or "TML" or "TMR"
                    or "TRL" or "TRR" or "RRL" or "RRR": height++; break;
                case "EXT": break;
                default: main++; break;
            }
        }
        if (main + lfe + height == 0) return "-";
        return height > 0 ? $"{main}.{lfe}.{height}" : $"{main}.{lfe}";
    }

    /// <summary>
    /// Convert a Denon volume token (MV50, MV505, MV0) into a floating value
    /// on the 0..MVMAX display scale where 0.5 dB steps are supported.
    /// </summary>
    public static double? ParseVolume(string mv)
    {
        if (string.IsNullOrWhiteSpace(mv)) return null;
        var digits = mv.Trim();
        if (digits.Length == 3) // half step: 505 â†’ 50.5
        {
            if (int.TryParse(digits[..2], out var whole) &&
                int.TryParse(digits[2..], out var frac))
                return whole + frac / 10.0;
        }
        if (int.TryParse(digits, out var i))
            return i;
        return null;
    }

    /// <summary>
    /// Encode a display volume (0..99, half steps allowed) into the token used
    /// by the AVR telnet command <c>MV</c>.
    /// </summary>
    public static string FormatVolume(double value)
    {
        value = Clamp(value, 0, 99);
        var rounded = System.Math.Round(value * 2, System.MidpointRounding.AwayFromZero) / 2.0;
        var whole = (int)System.Math.Floor(rounded);
        var half = rounded - whole >= 0.25;
        return half
            ? $"{whole:00}5"
            : $"{whole:00}";
    }

    /// <summary>
    /// Common Denon input sources with a friendly caption suitable for the UI.
    /// The internal key is what the AVR uses in the <c>SI</c> command.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> DefaultSources = new Dictionary<string, string>
    {
        ["CBL/SAT"] = "Cable / SAT",
        ["MPLAY"] = "Media Player",
        ["BD"] = "Blu-ray",
        ["GAME"] = "Game",
        ["AUX1"] = "AUX 1",
        ["AUX2"] = "AUX 2",
        ["TV"] = "TV Audio",
        ["CD"] = "CD",
        ["PHONO"] = "Phono",
        ["TUNER"] = "Tuner",
        ["NET"] = "HEOS Music",
        ["BT"] = "Bluetooth",
        ["IRADIO"] = "Internet Radio",
        ["SPOTIFY"] = "Spotify",
        ["TIDAL"] = "Tidal",
        ["AMAZON"] = "Amazon Music",
        ["DEEZER"] = "Deezer",
        ["USB/IPOD"] = "USB / iPod"
    };

    /// <summary>Denon surround modes (payload of MS command).</summary>
    public static readonly IReadOnlyDictionary<string, string> SurroundModes = new Dictionary<string, string>
    {
        ["MOVIE"] = "Movie",
        ["MUSIC"] = "Music",
        ["GAME"] = "Game",
        ["DIRECT"] = "Direct",
        ["PURE DIRECT"] = "Pure Direct",
        ["STEREO"] = "Stereo",
        ["AUTO"] = "Auto",
        ["DOLBY DIGITAL"] = "Dolby",
        ["DTS SURROUND"] = "DTS",
        ["MCH STEREO"] = "Multi Ch Stereo",
        ["ROCK ARENA"] = "Rock Arena",
        ["JAZZ CLUB"] = "Jazz Club",
        ["MONO MOVIE"] = "Mono Movie",
        ["MATRIX"] = "Matrix",
        ["VIDEO GAME"] = "Video Game",
        ["VIRTUAL"] = "Virtual"
    };

    // ── Analog tuner ────────────────────────────────────────────────────────

    /// <summary>Tunable FM range in MHz (covers the Americas and Europe).</summary>
    public const double FmMin = 87.5, FmMax = 108.0;

    /// <summary>Tunable AM range in kHz (covers 9 kHz and 10 kHz step regions).</summary>
    public const double AmMin = 520, AmMax = 1710;

    /// <summary>
    /// Encode a frequency for the <c>TFAN</c> command: the value times 100 in
    /// six digits. FM is in MHz (87.50 → 008750), AM in kHz (1050 → 105000).
    /// </summary>
    public static string FormatTunerFrequency(double frequency) =>
        ((int)System.Math.Round(frequency * 100, System.MidpointRounding.AwayFromZero)).ToString("000000");

    /// <summary>
    /// Decode the six-digit payload of a <c>TFAN</c> event. Per the protocol
    /// spec, values below 050000 are FM (MHz) and the rest are AM (kHz).
    /// </summary>
    public static (TunerBand Band, double Frequency)? ParseTunerFrequency(string payload)
    {
        var digits = (payload ?? string.Empty).Trim();
        if (digits.Length != 6 || !int.TryParse(digits, out var raw)) return null;
        return (raw < 50000 ? TunerBand.FM : TunerBand.AM, raw / 100.0);
    }

    /// <summary>
    /// Parse a frequency typed by the user. The band is inferred from the
    /// number: "95.5" (or "95,5") is FM, "1010" is AM.
    /// </summary>
    public static bool TryParseUserFrequency(string? text, out TunerBand band, out double frequency)
    {
        band = TunerBand.FM;
        frequency = 0;
        var t = (text ?? string.Empty).Trim().Replace(',', '.');
        if (!double.TryParse(t, System.Globalization.NumberStyles.Float,
                             System.Globalization.CultureInfo.InvariantCulture, out var v))
            return false;

        if (v >= FmMin && v <= FmMax) { band = TunerBand.FM; frequency = System.Math.Round(v, 2); return true; }
        if (v >= AmMin && v <= AmMax) { band = TunerBand.AM; frequency = System.Math.Round(v); return true; }
        return false;
    }

    /// <summary>Bare number as the front panel shows it: "95.50" or "1010".</summary>
    public static string FormatTunerNumber(TunerBand band, double frequency) =>
        frequency.ToString(band == TunerBand.FM ? "0.00" : "0", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Front-panel style label: "95.50 MHz" or "1010 kHz".</summary>
    public static string FormatTunerLabel(TunerBand band, double frequency) =>
        FormatTunerNumber(band, frequency) + (band == TunerBand.FM ? " MHz" : " kHz");

    private static double Clamp(double v, double min, double max) =>
        v < min ? min : v > max ? max : v;
}

/// <summary>Analog tuner band (payload of <c>TMAN</c>).</summary>
public enum TunerBand { FM, AM }

