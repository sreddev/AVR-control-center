// AVR Control Center
// Copyright (c) 2026 Sergio (@sreddev)
// Licensed under MIT License
// https://github.com/sreddev/AVR-control-center

using AvrControlCenter.Avr;
using Xunit;

namespace AvrControlCenter.Tests;

public class AvrProtocolTests
{
    [Theory]
    [InlineData("50", 50.0)]
    [InlineData("505", 50.5)]
    [InlineData("00", 0.0)]
    [InlineData(" 98 ", 98.0)]
    public void ParseVolume_reads_whole_and_half_steps(string token, double expected) =>
        Assert.Equal(expected, AvrProtocol.ParseVolume(token));

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    public void ParseVolume_rejects_garbage(string token) =>
        Assert.Null(AvrProtocol.ParseVolume(token));

    [Theory]
    [InlineData(50.0, "50")]
    [InlineData(50.5, "505")]
    [InlineData(50.3, "505")]   // rounds to the nearest half step
    [InlineData(5.0, "05")]
    [InlineData(150.0, "99")]   // clamped to the protocol maximum
    [InlineData(-3.0, "00")]
    public void FormatVolume_encodes_mv_token(double value, string expected) =>
        Assert.Equal(expected, AvrProtocol.FormatVolume(value));

    [Theory]
    [InlineData(10.0)]
    [InlineData(42.5)]
    [InlineData(80.0)]
    public void Volume_round_trips(double value) =>
        Assert.Equal(value, AvrProtocol.ParseVolume(AvrProtocol.FormatVolume(value)));

    [Theory]
    [InlineData(87.5, "008750")]
    [InlineData(95.6, "009560")]
    [InlineData(1050, "105000")]
    public void FormatTunerFrequency_is_six_digits(double frequency, string expected) =>
        Assert.Equal(expected, AvrProtocol.FormatTunerFrequency(frequency));

    [Fact]
    public void ParseTunerFrequency_splits_fm_and_am()
    {
        Assert.Equal((TunerBand.FM, 95.6), AvrProtocol.ParseTunerFrequency("009560"));
        Assert.Equal((TunerBand.AM, 1050.0), AvrProtocol.ParseTunerFrequency("105000"));
        Assert.Null(AvrProtocol.ParseTunerFrequency("12345"));
    }

    [Theory]
    [InlineData("95.5", TunerBand.FM, 95.5)]
    [InlineData("95,5", TunerBand.FM, 95.5)]
    [InlineData("1010", TunerBand.AM, 1010.0)]
    public void TryParseUserFrequency_infers_band(string text, TunerBand band, double frequency)
    {
        Assert.True(AvrProtocol.TryParseUserFrequency(text, out var b, out var f));
        Assert.Equal(band, b);
        Assert.Equal(frequency, f);
    }

    [Theory]
    [InlineData("")]
    [InlineData("200")]
    [InlineData("radio")]
    public void TryParseUserFrequency_rejects_out_of_band(string text) =>
        Assert.False(AvrProtocol.TryParseUserFrequency(text, out _, out _));

    [Fact]
    public void FormatTunerLabel_matches_front_panel()
    {
        Assert.Equal("95.60 MHz", AvrProtocol.FormatTunerLabel(TunerBand.FM, 95.6));
        Assert.Equal("1010 kHz", AvrProtocol.FormatTunerLabel(TunerBand.AM, 1010));
    }
}
