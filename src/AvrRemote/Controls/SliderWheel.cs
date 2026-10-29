// AVR Control Center
// Copyright (c) 2026 Sergio (@sreddev)
// Licensed under MIT License
// https://github.com/sreddev/AVR-control-center

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AvrRemote.Controls;

/// <summary>
/// Mouse wheel over a slider moves it one <see cref="Slider.SmallChange"/>
/// per notch (0.5 on the volume sliders, the remote's step) instead of
/// scrolling the page. Touchpads send smaller deltas; they add up to a notch.
/// </summary>
public static class SliderWheel
{
    private const int Notch = 120;   // Mouse.MouseWheelDeltaForOneLine

    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(SliderWheel), new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject d) => (bool)d.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject d, bool value) => d.SetValue(IsEnabledProperty, value);

    /// <summary>Wheel delta not yet turned into a step.</summary>
    private static readonly DependencyProperty PendingDeltaProperty = DependencyProperty.RegisterAttached(
        "PendingDelta", typeof(int), typeof(SliderWheel), new PropertyMetadata(0));

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Slider slider) return;
        slider.PreviewMouseWheel -= OnWheel;
        if ((bool)e.NewValue) slider.PreviewMouseWheel += OnWheel;
    }

    private static void OnWheel(object sender, MouseWheelEventArgs e)
    {
        var slider = (Slider)sender;
        // Disabled (receiver in standby): let the page scroll as usual.
        if (!slider.IsEnabled) return;
        e.Handled = true;

        var delta = (int)slider.GetValue(PendingDeltaProperty) + e.Delta;
        var notches = delta / Notch;
        slider.SetValue(PendingDeltaProperty, delta - notches * Notch);
        if (notches == 0) return;

        var value = slider.Value + notches * slider.SmallChange;
        slider.Value = Math.Clamp(value, slider.Minimum, slider.Maximum);
    }
}
