// AVR Control Center
// Copyright (c) 2026 Sergio (@sreddev)
// Licensed under MIT License
// https://github.com/sreddev/AVR-control-center

using System.Diagnostics;
using System.Windows;
using AvrControlCenter.Services;
using AvrControlCenter.ViewModels;

namespace AvrControlCenter.Views.Pages;

public partial class SettingsPage : PageBase
{
    protected override bool RequiresPower => false;

    public SettingsPage()
    {
        InitializeComponent();
        ApplyLocalization();
    }

    private void ApplyLocalization()
    {
        TitleText.Text = L.SettingsTitle;
        SubtitleText.Text = "Application preferences";
        AutoConnLabel.Text = L.SettingsAutoConnect;
        AutoConnDesc.Text = L.SettingsAutoConnectDesc;
        TrayLabel.Text = L.SettingsCloseToTray;
        TrayDesc.Text = L.SettingsCloseToTrayDesc;
        MinLabel.Text = L.SettingsStartMinimized;
        MinDesc.Text = L.SettingsStartMinimizedDesc;
        UpdateLabel.Text = L.SettingsAutoUpdate;
        UpdateDesc.Text = L.SettingsAutoUpdateDesc;
        if (AutoUpdateService.IsPackaged)
            UpdateRow.Visibility = UpdateRowLine.Visibility = Visibility.Collapsed;
        UnitLabel.Text = L.SettingsVolumeUnit;
        UnitDesc.Text = L.SettingsVolumeUnitDesc;
        ThemeLabel.Text = L.SettingsTheme;
        ThemeDesc.Text = L.SettingsThemeDesc;
        ThemeRestartBtn.Content = L.SettingsThemeRestart;
        ModifiedByLabel.Text = L.AboutCreatedBy;
        TelegramBtn.Content = L.AboutTelegram;
        SupportLabel.Text = L.AboutSupport;
        // The content is a layout grid, so screen readers need the name spelled out.
        System.Windows.Automation.AutomationProperties.SetName(SupportBtn, L.AboutSupport);
        PayPalLabel.Text = L.AboutDonatePayPal;
        PayPalDesc.Text = L.AboutDonatePayPalDesc;
        RevolutLabel.Text = L.AboutDonateRevolut;
        RevolutDesc.Text = L.AboutDonateRevolutDesc;
        // Read straight from the assembly: the DataContext isn't set yet in the constructor.
        VersionText.Text = "v" + AutoUpdateService.GetCurrentVersion() + " · .NET 8 · WPF";
        UpdateAvailRun.Text = "New version:";
    }

    /// <summary>PayPal donate page: €5 suggested, "Buy me a coffee ☕" (the ☕ percent-encoded).</summary>
    internal const string PayPalDonateUrl =
        "https://www.paypal.com/donate/?business=M3KKGFD9NCZH8&amount=5&no_recurring=0" +
        "&item_name=Buy+me+a+coffee+%E2%98%95+Keeps+my+free+apps+alive+and+updated.&currency_code=EUR";

    internal const string RevolutUrl = "https://revolut.me/sreddev";

    private void OnTelegramClick(object sender, RoutedEventArgs e) =>
        OpenUrl("https://t.me/sreddev");

    /// <summary>A left click opens the menu under the button (right click opens it too).</summary>
    private void OnSupportClick(object sender, RoutedEventArgs e)
    {
        SupportMenu.PlacementTarget = SupportBtn;
        SupportMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        SupportMenu.MinWidth = SupportBtn.ActualWidth;
        SupportMenu.IsOpen = true;
    }

    private void OnPayPalClick(object sender, RoutedEventArgs e) => OpenUrl(PayPalDonateUrl);

    private void OnRevolutClick(object sender, RoutedEventArgs e) => OpenUrl(RevolutUrl);

    /// <summary>
    /// The preference switches bind straight to <see cref="AppSettings"/>, which
    /// doesn't save itself. Click fires after the binding has pushed the new value.
    /// </summary>
    private void OnPreferenceToggled(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.Settings.Save();
    }

    private void OnUpdateClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.AvailableUpdate is not null)
            vm.Updater.ApplyUpdate(vm.AvailableUpdate);
    }

    private static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { /* ignored */ }
    }
}

