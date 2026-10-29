// AVR Control Center
// Copyright (c) 2026 Sergio (@sreddev)
// Licensed under MIT License
// https://github.com/sreddev/AVR-control-center

using System.Windows;
using System.Windows.Controls;
using AvrRemote.Controls;

namespace AvrRemote.Views.Pages;

/// <summary>
/// Base class for every navigation page. Ensures the DataContext is
/// inherited from the parent window at load time. RelativeSource bindings on
/// the UserControl root do not work reliably inside WPF-UI's NavigationView,
/// so we set it imperatively here.
/// </summary>
public abstract class PageBase : UserControl
{
    protected PageBase()
    {
        Loaded += OnLoaded;
    }

    /// <summary>
    /// Pages that send commands to the receiver are wrapped in a
    /// <see cref="PowerGate"/>: a banner and dimmed content while it isn't on.
    /// Home, Devices and Settings opt out.
    /// </summary>
    protected virtual bool RequiresPower => true;

    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);
        if (!RequiresPower || newContent is null or PowerGate) return;
        // Detach the page content first: an element can only have one parent.
        var gate = new PowerGate();
        Content = gate;
        gate.Content = newContent;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is null || DataContext == this)
            DataContext = Window.GetWindow(this)?.DataContext;
    }
}

