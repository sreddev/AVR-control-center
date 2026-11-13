// AVR Control Center
// Copyright (c) 2026 Sergio (@sreddev)
// Licensed under MIT License
// https://github.com/sreddev/AVR-control-center

using System.Text.Json;
using AvrControlCenter.Services;
using Xunit;

namespace AvrControlCenter.Tests;

/// <summary>What a first install starts with: no settings file, or one without the key.</summary>
public class AppSettingsDefaultsTests
{
    [Fact]
    public void Start_minimized_is_off_on_a_fresh_install()
    {
        Assert.False(new AppSettings().StartMinimized);
    }

    [Fact]
    public void Start_minimized_is_off_when_the_settings_file_lacks_it()
    {
        Assert.False(JsonSerializer.Deserialize<AppSettings>("{}")!.StartMinimized);
    }

    [Fact]
    public void Other_preference_switches_default_to_on()
    {
        var s = new AppSettings();
        Assert.True(s.AutoConnect);
        Assert.True(s.CloseToTray);
        Assert.True(s.AutoUpdate);
    }
}
