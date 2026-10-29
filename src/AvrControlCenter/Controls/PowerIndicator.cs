// AVR Control Center
// Copyright (c) 2026 Sergio (@sreddev)
// Licensed under MIT License
// https://github.com/sreddev/AVR-control-center

using System.Windows;
using System.Windows.Controls;
using AvrControlCenter.Avr;

namespace AvrControlCenter.Controls;

/// <summary>
/// Dot and label pill for <see cref="ReceiverState.Power"/>. The same control
/// sits in the status bar, on Home and in the page banners, so the receiver
/// state reads the same everywhere: green On, amber Standby, grey ring
/// Offline, pulsing grey while connecting or changing.
/// </summary>
public sealed class PowerIndicator : Control
{
    static PowerIndicator()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(PowerIndicator),
            new FrameworkPropertyMetadata(typeof(PowerIndicator)));
    }

    public static readonly DependencyProperty PowerProperty = DependencyProperty.Register(
        nameof(Power), typeof(PowerState), typeof(PowerIndicator),
        new PropertyMetadata(PowerState.Offline, OnPowerChanged));

    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(PowerIndicator), new PropertyMetadata(""));

    private static readonly DependencyPropertyKey IsBusyKey = DependencyProperty.RegisterReadOnly(
        nameof(IsBusy), typeof(bool), typeof(PowerIndicator), new PropertyMetadata(false));

    public static readonly DependencyProperty IsBusyProperty = IsBusyKey.DependencyProperty;

    public PowerState Power
    {
        get => (PowerState)GetValue(PowerProperty);
        set => SetValue(PowerProperty, value);
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Connecting or changing power: the dot pulses.</summary>
    public bool IsBusy => (bool)GetValue(IsBusyProperty);

    private static void OnPowerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var indicator = (PowerIndicator)d;
        var busy = IsTransitional((PowerState)e.NewValue);
        indicator.SetValue(IsBusyKey, busy);
        // Glow once the receiver settles after a wait.
        if (!busy && IsTransitional((PowerState)e.OldValue) && indicator.IsLoaded)
            Feedback.Glow(indicator);
    }

    private static bool IsTransitional(PowerState state) =>
        state is PowerState.Connecting or PowerState.TurningOn or PowerState.GoingToStandby;
}
