// AVR Desktop Control
// Copyright (c) 2026 Sergio (@sreddev)
// Licensed under MIT License
// https://github.com/sreddev/AVR-desktop-control

using System;
using System.Windows;
using AvrRemote.Discovery;
using AvrRemote.Services;
using AvrRemote.ViewModels;
using AvrRemote.Views;
using Microsoft.Extensions.DependencyInjection;

namespace AvrRemote;

public partial class App : Application
{
    private IServiceProvider? _services;

    /// <summary>
    /// Held for the lifetime of the process so a second launch can detect that
    /// an instance is already running. Two instances would fight over the
    /// receiver's telnet and HEOS sockets.
    /// </summary>
    private static System.Threading.Mutex? _singleInstance;

    private const string RestartArg = "--restart";

    /// <summary>True when this process was started by <see cref="RestartAsync"/>.</summary>
    public static bool IsRestart { get; private set; }

    /// <summary>The theme this process is running with (changes apply on restart).</summary>
    public static AppTheme RunningTheme { get; private set; } = AppTheme.Dark;

    private async void OnStartup(object sender, StartupEventArgs e)
    {
        IsRestart = Array.IndexOf(e.Args, RestartArg) >= 0;
        _singleInstance = new System.Threading.Mutex(true, @"Local\AVRDesktopControl", out var isFirst);
        if (!isFirst && IsRestart)
        {
            // The old process may still be closing its sockets: give it a moment.
            try { isFirst = _singleInstance.WaitOne(TimeSpan.FromSeconds(10)); }
            catch (System.Threading.AbandonedMutexException) { isFirst = true; }
        }
        if (!isFirst)
        {
            // Another copy owns the sockets: bring it forward and bail out.
            Shutdown();
            return;
        }

        var settings = AppSettings.Load();
        ApplyTheme(settings.Theme);

        // WPF-UI defaults to the Windows accent color. Override it with the
        // app's army green (AccentColor in Theme.xaml) so every
        // "Appearance=Primary" button, toggle track and slider thumb picks it
        // up automatically. All four shades are set explicitly: the
        // single-colour overload lightens the fill for dark themes, which
        // drops white button text below 4.5:1. These keep it at 4.7:1 or better.
        Wpf.Ui.Appearance.ApplicationAccentColorManager.Apply(
            System.Windows.Media.Color.FromRgb(0x61, 0x7D, 0x2E),   // system
            System.Windows.Media.Color.FromRgb(0x61, 0x7D, 0x2E),   // primary (button fill)
            System.Windows.Media.Color.FromRgb(0x5E, 0x7A, 0x2E),   // secondary
            System.Windows.Media.Color.FromRgb(0x5A, 0x74, 0x29));  // tertiary

        // Global exception handler â€” writes to %TEMP%\AvrRemote-crash.log
        DispatcherUnhandledException += (_, ev) =>
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AvrRemote-crash.log");
            System.IO.File.WriteAllText(path, ev.Exception.ToString());
            MessageBox.Show(ev.Exception.ToString(), "AvrRemote crashed");
            ev.Handled = true;
            Shutdown();
        };
        AppDomain.CurrentDomain.UnhandledException += (_, ev) =>
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AvrRemote-crash.log");
            System.IO.File.WriteAllText(path, ev.ExceptionObject?.ToString() ?? "unknown");
        };

        var services = new ServiceCollection();
        services.AddSingleton(settings);
        services.AddSingleton<SsdpDiscoveryService>();
        services.AddSingleton<ReceiverService>();
        services.AddSingleton<AutoUpdateService>();
        services.AddSingleton<Heos.HeosService>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
        _services = services.BuildServiceProvider();

        var vm = _services.GetRequiredService<MainViewModel>();
        var window = _services.GetRequiredService<MainWindow>();
        window.Show();

        // Runs asynchronously â€” the UI is already alive by the time this awaits.
        await vm.InitializeAsync();
    }

    /// <summary>
    /// Every brush is a StaticResource, so the palette is chosen once, before
    /// any window exists: swap the dark palette for the light one and switch
    /// WPF-UI's own control colours to match.
    /// </summary>
    private void ApplyTheme(AppTheme theme)
    {
        RunningTheme = theme;
        if (theme != AppTheme.Light) return;

        Wpf.Ui.Appearance.ApplicationThemeManager.Apply(
            Wpf.Ui.Appearance.ApplicationTheme.Light, Wpf.Ui.Controls.WindowBackdropType.None, updateAccent: false);

        var merged = Resources.MergedDictionaries;
        for (var i = 0; i < merged.Count; i++)
        {
            if (merged[i].Source?.OriginalString.EndsWith("Palette.Dark.xaml", StringComparison.OrdinalIgnoreCase) != true)
                continue;
            merged[i] = new ResourceDictionary { Source = new Uri("/Views/Palette.Light.xaml", UriKind.Relative) };
            break;
        }
    }

    /// <summary>
    /// Closes the receiver and HEOS connections, then starts a fresh copy of
    /// the app (used to apply a theme change) and exits.
    /// </summary>
    public async Task RestartAsync()
    {
        if (_services is IAsyncDisposable disposable)
            await disposable.DisposeAsync();
        _services = null;

        _singleInstance?.ReleaseMutex();
        _singleInstance?.Dispose();
        _singleInstance = null;

        var exe = Environment.ProcessPath;
        if (exe is not null)
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe, RestartArg) { UseShellExecute = false });
        Shutdown();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_services is IAsyncDisposable disposable)
            await disposable.DisposeAsync();
        base.OnExit(e);
    }
}

