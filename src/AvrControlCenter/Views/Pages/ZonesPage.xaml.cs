// AVR Control Center
// Copyright (c) 2026 Sergio (@sreddev)
// Licensed under MIT License
// https://github.com/sreddev/AVR-control-center

using System;
using System.Windows.Controls;
using AvrControlCenter.Avr;
using AvrControlCenter.ViewModels;

namespace AvrControlCenter.Views.Pages;

/// <summary>Main zone, Zone 2 and Zone 3 side by side.</summary>
public partial class ZonesPage : PageBase
{
    public ZonesPage() => InitializeComponent();

    /// <summary>
    /// Sends the input only when the user picked it from the list, so the
    /// receiver's own source reports (which move the selection) aren't echoed back.
    /// </summary>
    private void OnZoneSourcePicked(object? sender, EventArgs e)
    {
        if (sender is not ComboBox { Tag: ZoneState zone, SelectedItem: SourceEntry src }) return;
        if (DataContext is not MainViewModel vm) return;
        if (string.Equals(zone.Source, src.Token, StringComparison.OrdinalIgnoreCase)) return;
        _ = vm.SetZoneSourceAsync(zone, src.Token);
    }
}
