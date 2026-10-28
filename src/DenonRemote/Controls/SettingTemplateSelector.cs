// AVR Desktop Control
// Copyright (c) 2026 Sergio (@sreddev)
// Licensed under MIT License
// https://github.com/sreddev/AVR-desktop-control

using System.Windows;
using System.Windows.Controls;
using DenonRemote.Denon;

namespace DenonRemote.Controls;

/// <summary>Picks the row layout for an <see cref="AvrSetting"/> by its kind.</summary>
public sealed class SettingTemplateSelector : DataTemplateSelector
{
    public DataTemplate? Toggle { get; set; }
    public DataTemplate? Choice { get; set; }
    public DataTemplate? Level { get; set; }
    public DataTemplate? Action { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container) =>
        item is not AvrSetting s ? null : s.Kind switch
        {
            SettingKind.Toggle => Toggle,
            SettingKind.Choice => Choice,
            SettingKind.Level => Level,
            _ => Action
        };
}
