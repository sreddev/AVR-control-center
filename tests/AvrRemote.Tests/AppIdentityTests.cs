// AVR Control Center
// Copyright (c) 2026 Sergio (@sreddev)
// Licensed under MIT License
// https://github.com/sreddev/AVR-control-center

using System.Reflection;
using AvrRemote.Services;
using Xunit;

namespace AvrRemote.Tests;

/// <summary>Name, version and update source: what the rename must keep consistent.</summary>
public class AppIdentityTests
{
    private static readonly Assembly App = typeof(AutoUpdateService).Assembly;

    [Fact]
    public void Product_and_executable_carry_the_new_name()
    {
        Assert.Equal("AVRControlCenter", App.GetName().Name);
        Assert.Equal("AVR Control Center", App.GetCustomAttribute<AssemblyProductAttribute>()?.Product);
    }

    [Fact]
    public void Updates_come_from_the_renamed_repository()
    {
        Assert.Equal("sreddev/AVR-control-center", AutoUpdateService.RepoSlug);
        Assert.Equal("https://github.com/sreddev/AVR-control-center/releases", AutoUpdateService.ReleasesUrl);
    }

    [Fact]
    public void Current_version_matches_the_assembly()
    {
        var expected = App.GetName().Version!.ToString(3);
        Assert.Equal(expected, AutoUpdateService.GetCurrentVersion());
    }

    [Theory]
    [InlineData("3.0.0", "2.5.0", true)]
    [InlineData("2.5.1", "2.5.0", true)]
    [InlineData("2.5.0", "2.5.0", false)]
    [InlineData("2.4.9", "2.5.0", false)]
    [InlineData("not-a-version", "2.5.0", false)]
    public void IsNewer_compares_numerically(string remote, string local, bool expected) =>
        Assert.Equal(expected, AutoUpdateService.IsNewer(remote, local));
}
