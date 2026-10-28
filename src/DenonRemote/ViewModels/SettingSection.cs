// AVR Desktop Control
// Copyright (c) 2026 Sergio (@sreddev)
// Licensed under MIT License
// https://github.com/sreddev/AVR-desktop-control_tuner

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using DenonRemote.Denon;

namespace DenonRemote.ViewModels;

/// <summary>
/// A titled card of receiver settings. <see cref="HasAny"/> follows the
/// settings' support flags so a card can show an empty-state hint when the
/// connected model has none of them.
/// </summary>
public sealed class SettingSection : ObservableObject
{
    public string Title { get; }
    public string Subtitle { get; }
    public IReadOnlyList<AvrSetting> Items { get; }

    public bool HasAny => Items.Any(i => i.IsSupported);

    public SettingSection(string title, string subtitle, IEnumerable<AvrSetting> items)
    {
        Title = title;
        Subtitle = subtitle;
        Items = items.ToList();
        foreach (var item in Items)
            item.PropertyChanged += OnItemChanged;
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AvrSetting.IsSupported))
            OnPropertyChanged(nameof(HasAny));
    }
}

/// <summary>One zone on the Zones page: its live state plus its sleep timer.</summary>
public sealed class ZonePanel
{
    public ZoneState Zone { get; }
    public AvrSetting Sleep { get; }
    public bool IsMain => Zone.Id == ZoneId.Main;

    public ZonePanel(ZoneState zone, AvrSetting sleep)
    {
        Zone = zone;
        Sleep = sleep;
    }
}
