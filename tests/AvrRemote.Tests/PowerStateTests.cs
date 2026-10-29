// AVR Control Center
// Copyright (c) 2026 Sergio (@sreddev)
// Licensed under MIT License
// https://github.com/sreddev/AVR-control-center

using System.Collections.Generic;
using AvrRemote.Avr;
using Xunit;

namespace AvrRemote.Tests;

/// <summary>The derived power state shown by the status bar, Home, tray and page banners.</summary>
public class PowerStateTests
{
    private static ReceiverState Connected(bool known = true) =>
        new() { IsConnected = true, PowerKnown = known, FriendlyName = "Living Room" };

    [Fact]
    public void Offline_when_not_connected()
    {
        var s = new ReceiverState();
        Assert.Equal(PowerState.Offline, s.Power);
        Assert.False(s.IsOperable);
        Assert.Equal("AVR Control Center · Offline", s.PowerTooltip);
    }

    [Fact]
    public void Connecting_while_the_connection_runs()
    {
        var s = new ReceiverState { IsConnecting = true };
        Assert.Equal(PowerState.Connecting, s.Power);
    }

    [Fact]
    public void Connecting_until_the_receiver_reports_power()
    {
        // Never guess "Standby" before ZM, PWSTANDBY or the HTTP status arrives.
        Assert.Equal(PowerState.Connecting, Connected(known: false).Power);
    }

    [Fact]
    public void Standby_and_on()
    {
        var s = Connected();
        Assert.Equal(PowerState.Standby, s.Power);
        Assert.Equal("Standby", s.PowerLabel);
        s.Main.IsOn = true;
        Assert.Equal(PowerState.On, s.Power);
        Assert.True(s.IsOperable);
        Assert.Equal("Living Room · On", s.PowerTooltip);
    }

    [Fact]
    public void Zone_only_when_main_is_off_but_another_zone_plays()
    {
        var s = Connected();
        s.Zone2.IsOn = true;
        Assert.Equal(PowerState.ZoneOnly, s.Power);
        Assert.Equal("Standby · Zone 2 on", s.PowerLabel);
        Assert.False(s.IsOperable);
    }

    [Fact]
    public void Transit_clears_when_the_receiver_confirms()
    {
        var s = Connected();
        s.PowerTransit = PowerTransit.TurningOn;
        Assert.Equal(PowerState.TurningOn, s.Power);
        s.Main.IsOn = true;
        Assert.Equal(PowerTransit.None, s.PowerTransit);
        Assert.Equal(PowerState.On, s.Power);

        s.PowerTransit = PowerTransit.GoingToStandby;
        Assert.Equal(PowerState.GoingToStandby, s.Power);
        s.Main.IsOn = false;
        Assert.Equal(PowerState.Standby, s.Power);
    }

    [Fact]
    public void Zone_changes_raise_power_notifications()
    {
        var s = Connected();
        var raised = new List<string?>();
        s.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        s.Main.IsOn = true;
        Assert.Contains(nameof(ReceiverState.Power), raised);
        Assert.Contains(nameof(ReceiverState.IsOperable), raised);
        Assert.Contains(nameof(ReceiverState.PowerLabel), raised);
        Assert.Contains(nameof(ReceiverState.PowerTooltip), raised);
    }
}
