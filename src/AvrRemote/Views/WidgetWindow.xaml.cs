// AVR Control Center
// Copyright (c) 2026 Sergio (@sreddev)
// Licensed under MIT License
// https://github.com/sreddev/AVR-control-center

using System;
using System.Windows;
using System.Windows.Input;
using AvrRemote.Services;
using AvrRemote.ViewModels;
using Wpf.Ui.Controls;

namespace AvrRemote.Views;

public partial class WidgetWindow : FluentWindow
{
    public event EventHandler? OpenFullRequested;

    public WidgetWindow(MainViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
    }

    /// <summary>
    /// Positions the widget above the system tray (bottom-right of the primary screen).
    /// </summary>
    public void ShowNearTray()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 12;
        Top = workArea.Bottom - Height - 12;
        Show();
        Activate();
    }

    public void Toggle()
    {
        if (IsVisible) Hide();
        else ShowNearTray();
    }

    private void OnSourceClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.Tag is string tag && DataContext is MainViewModel vm)
            _ = vm.SelectSourceCommand.ExecuteAsync(tag);
    }

    private void OnOpenFull(object sender, MouseButtonEventArgs e)
    {
        Hide();
        OpenFullRequested?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        // Auto-hide when losing focus (click outside)
        Hide();
    }
}

