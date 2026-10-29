// AVR Control Center
// Copyright (c) 2026 Sergio (@sreddev)
// Licensed under MIT License
// https://github.com/sreddev/AVR-control-center

using AvrControlCenter.Avr;
using Xunit;

namespace AvrControlCenter.Tests;

public class AvrEventParserTests
{
    private readonly ReceiverState _state = new();
    private readonly AvrEventParser _parser;

    public AvrEventParserTests() => _parser = new AvrEventParser(_state);

    private void Feed(params string[] lines)
    {
        foreach (var line in lines) _parser.Apply(line);
    }

    [Fact]
    public void Zm_sets_main_power_and_marks_it_known()
    {
        Assert.False(_state.PowerKnown);
        Feed("ZMON");
        Assert.True(_state.Main.IsOn);
        Assert.True(_state.PowerKnown);
        Feed("ZMOFF");
        Assert.False(_state.Main.IsOn);
    }

    [Fact]
    public void Pwstandby_turns_every_zone_off()
    {
        Feed("ZMON", "Z2ON", "Z3ON", "PWSTANDBY");
        Assert.False(_state.Main.IsOn);
        Assert.False(_state.Zone2.IsOn);
        Assert.False(_state.Zone3.IsOn);
        Assert.True(_state.PowerKnown);
    }

    [Fact]
    public void Pwon_is_ignored_because_it_covers_any_zone()
    {
        Feed("PWON");
        Assert.False(_state.Main.IsOn);
        Assert.False(_state.PowerKnown);
    }

    [Fact]
    public void Volume_and_max_are_parsed_separately()
    {
        Feed("MV455", "MVMAX 80");
        Assert.Equal(45.5, _state.Main.Volume);
        Assert.Equal(80, _state.Main.VolumeMax);
    }

    [Fact]
    public void Mute_and_source()
    {
        Feed("MUON", "SITUNER");
        Assert.True(_state.Main.IsMuted);
        Assert.Equal("TUNER", _state.Main.Source);
        Feed("MUOFF");
        Assert.False(_state.Main.IsMuted);
    }

    [Fact]
    public void Surround_mode()
    {
        Feed("MSSTEREO");
        Assert.Equal("STEREO", _state.SurroundMode);
    }

    [Fact]
    public void Zone2_power_volume_and_presence()
    {
        Feed("Z2ON", "Z235");
        Assert.True(_state.Zone2.IsPresent);
        Assert.True(_state.Zone2.IsOn);
        Assert.Equal(35, _state.Zone2.Volume);
    }

    [Fact]
    public void Tuner_frequency_and_rds_name()
    {
        Feed("TFAN009560", "TFANNAME R CLUJ FM");
        Assert.Equal(TunerBand.FM, _state.TunerBand);
        Assert.Equal(95.6, _state.TunerFrequency);
        Assert.Equal("R CLUJ FM", _state.TunerStationName);
    }

    [Fact]
    public void Remote_lines_are_flagged_while_applied()
    {
        var seen = false;
        _state.Main.PropertyChanged += (_, _) => seen |= _state.IsApplyingRemote;
        Feed("MV40");
        Assert.True(seen);
        Assert.False(_state.IsApplyingRemote);
    }
}
