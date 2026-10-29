// AVR Desktop Control
// Copyright (c) 2026 Sergio (@sreddev)
// Licensed under MIT License
// https://github.com/sreddev/AVR-desktop-control

using AvrRemote.Services;

namespace AvrRemote.Views.Pages;

public partial class ConnectPage : PageBase
{
    protected override bool RequiresPower => false;

    public ConnectPage()
    {
        InitializeComponent();
        ApplyLocalization();
    }

    private void ApplyLocalization()
    {
        HeroTitle.Text = L.AddReceiver;
        HeroSubtitle.Text = L.AddReceiverDesc;
        DiscTitle.Text = L.DiscoveredDevices;
        RescanBtn.Content = L.Rescan;
        ConnectBtn.Content = L.Connect;
        IpTitle.Text = L.AddByIp;
        IpDesc.Text = L.AddByIpDesc;
        AddBtn.Content = L.AddAndConnect;
        HelpTitle.Text = L.DoesntShowUp;
        HelpDesc.Text = L.DoesntShowUpDesc;
        EmptyTitle.Text = "Nothing here yet";
        EmptyDesc.Text = "Press Rescan or add the IP manually.";
    }
}
