// AVR Desktop Control
// Copyright (c) 2026 Sergio (@sreddev)
// Licensed under MIT License
// https://github.com/sreddev/AVR-desktop-control

namespace AvrRemote.Views.Pages;

/// <summary>
/// AM/FM tuner: manual tuning, seek/step, and the saved-station list.
/// Everything is bound to <see cref="ViewModels.MainViewModel"/> commands.
/// </summary>
public partial class TunerPage : PageBase
{
    public TunerPage() => InitializeComponent();
}
