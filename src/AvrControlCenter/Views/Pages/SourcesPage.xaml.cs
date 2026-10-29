// AVR Control Center
// Copyright (c) 2026 Sergio (@sreddev)
// Licensed under MIT License
// https://github.com/sreddev/AVR-control-center

using System.Windows;
using AvrControlCenter.ViewModels;

namespace AvrControlCenter.Views.Pages;

public partial class SourcesPage : PageBase
{
    public SourcesPage() => InitializeComponent();

    private void OnSourceClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.Tag is string token && DataContext is MainViewModel vm)
            _ = vm.SelectSourceCommand.ExecuteAsync(token);
    }

    private void OnSurroundClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.Tag is string mode && DataContext is MainViewModel vm)
            _ = vm.SetSurroundCommand.ExecuteAsync(mode);
    }
}
