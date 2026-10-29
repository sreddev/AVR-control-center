// AVR Desktop Control
// Copyright (c) 2026 Sergio (@sreddev)
// Licensed under MIT License
// https://github.com/sreddev/AVR-desktop-control

using System.Windows;
using System.Windows.Controls;

namespace AvrRemote.Controls;

/// <summary>
/// Wraps a page that only works while the receiver is on. When it isn't, a
/// banner explains why (with Turn on or Open Devices) and the page content is
/// dimmed and disabled. The template lives in Controls/Power.xaml;
/// <see cref="Views.Pages.PageBase"/> adds the gate to every page that needs it.
/// </summary>
public sealed class PowerGate : ContentControl
{
    static PowerGate()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(PowerGate),
            new FrameworkPropertyMetadata(typeof(PowerGate)));
        FocusableProperty.OverrideMetadata(typeof(PowerGate), new FrameworkPropertyMetadata(false));
    }

    private Button? _devices;

    public override void OnApplyTemplate()
    {
        if (_devices is not null) _devices.Click -= OnDevicesClick;
        base.OnApplyTemplate();
        _devices = GetTemplateChild("PART_Devices") as Button;
        if (_devices is not null) _devices.Click += OnDevicesClick;
    }

    private void OnDevicesClick(object sender, RoutedEventArgs e) =>
        (Window.GetWindow(this) as Views.MainWindow)?.NavigateTo(typeof(Views.Pages.ConnectPage));
}
