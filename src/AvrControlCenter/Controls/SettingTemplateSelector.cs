// AVR Control Center
// Copyright (c) 2026 Sergio (@sreddev)
// Licensed under MIT License
// https://github.com/sreddev/AVR-control-center

using System.Windows;
using System.Windows.Controls;
using AvrControlCenter.Avr;

namespace AvrControlCenter.Controls;

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
