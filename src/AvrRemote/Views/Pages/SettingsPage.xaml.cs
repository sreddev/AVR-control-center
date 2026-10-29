// AVR Desktop Control
// Copyright (c) 2026 Sergio (@sreddev)
// Licensed under MIT License
// https://github.com/sreddev/AVR-desktop-control

using System.Diagnostics;
using System.Windows;
using AvrRemote.Services;
using AvrRemote.ViewModels;

namespace AvrRemote.Views.Pages;

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
        UnitLabel.Text = L.SettingsVolumeUnit;
        UnitDesc.Text = L.SettingsVolumeUnitDesc;
        ThemeLabel.Text = L.SettingsTheme;
        ThemeDesc.Text = L.SettingsThemeDesc;
        ThemeRestartBtn.Content = L.SettingsThemeRestart;
        ModifiedByLabel.Text = L.AboutCreatedBy;
        TelegramBtn.Content = L.AboutTelegram;
        DonateBtn.Content = DonateUrl is null ? L.AboutDonateSoon : L.AboutDonate;
        DonateBtn.IsEnabled = DonateUrl is not null;
        // Read straight from the assembly: the DataContext isn't set yet in the constructor.
        VersionText.Text = "v" + AutoUpdateService.GetCurrentVersion() + " · .NET 8 · WPF";
        UpdateAvailRun.Text = "New version:";
    }

    /// <summary>Sergio's PayPal.Me link. Null keeps the button disabled ("coming soon").</summary>
    private static readonly string? DonateUrl = "https://www.paypal.com/paypalme/sreddev?locale.x=ro_RO&country.x=RO";

    private void OnTelegramClick(object sender, RoutedEventArgs e) =>
        OpenUrl("https://t.me/sreddev");

    private void OnDonateClick(object sender, RoutedEventArgs e)
    {
        if (DonateUrl is not null) OpenUrl(DonateUrl);
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

