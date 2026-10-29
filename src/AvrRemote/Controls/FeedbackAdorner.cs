// AVR Control Center
// Copyright (c) 2026 Sergio (@sreddev)
// Licensed under MIT License
// https://github.com/sreddev/AVR-control-center

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace AvrRemote.Controls;

/// <summary>
/// Draws the <see cref="Feedback"/> effects on top of one element: ripples,
/// the busy line, the glow and the armed (red) state. Added on first use and
/// removed again once nothing is showing, so idle elements carry no overlay.
/// </summary>
internal sealed class FeedbackAdorner : Adorner
{
    private const double RippleMs = 450;
    private const double GlowMs = 700;
    private const double PendingLoopMs = 1100;

    private static readonly Stopwatch Clock = Stopwatch.StartNew();

    private readonly List<(Point At, double Start)> _ripples = new();
    private double _glowStart = double.NaN;
    private double _pendingStart = double.NaN;
    private double _pendingUntil;
    private bool _armed;
    private bool _ticking;

    private FeedbackAdorner(UIElement element) : base(element)
    {
        IsHitTestVisible = false;
    }

    private static double Now => Clock.Elapsed.TotalMilliseconds;

    /// <summary>The element's adorner, created when needed. Null outside an adorner layer (popups).</summary>
    public static FeedbackAdorner? For(UIElement element)
    {
        if (Find(element) is { } existing) return existing;
        var layer = AdornerLayer.GetAdornerLayer(element);
        if (layer is null) return null;
        var adorner = new FeedbackAdorner(element);
        layer.Add(adorner);
        return adorner;
    }

    public static FeedbackAdorner? Find(UIElement element) =>
        AdornerLayer.GetAdornerLayer(element)?.GetAdorners(element)?.OfType<FeedbackAdorner>().FirstOrDefault();

    public void Ripple(Point at)
    {
        _ripples.Add((at, Now));
        Tick();
    }

    public void Glow()
    {
        _glowStart = Now;
        Tick();
    }

    public void BeginPending(TimeSpan timeout)
    {
        if (double.IsNaN(_pendingStart)) _pendingStart = Now;
        _pendingUntil = Now + timeout.TotalMilliseconds;
        Tick();
    }

    public void EndPending()
    {
        _pendingStart = double.NaN;
        Tick();
    }

    public void SetArmed(bool armed)
    {
        _armed = armed;
        Tick();
    }

    private bool IsActive =>
        _ripples.Count > 0 || !double.IsNaN(_glowStart) || !double.IsNaN(_pendingStart) || _armed;

    private void Tick()
    {
        InvalidateVisual();
        if (_ticking) return;
        _ticking = true;
        CompositionTarget.Rendering += OnFrame;
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        var now = Now;
        _ripples.RemoveAll(r => now - r.Start > RippleMs);
        if (now - _glowStart > GlowMs) _glowStart = double.NaN;
        if (now > _pendingUntil) _pendingStart = double.NaN;
        InvalidateVisual();
        if (IsActive) return;

        CompositionTarget.Rendering -= OnFrame;
        _ticking = false;
        AdornerLayer.GetAdornerLayer(AdornedElement)?.Remove(this);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var size = AdornedElement.RenderSize;
        if (size.Width <= 0 || size.Height <= 0) return;
        var now = Now;
        var bounds = new Rect(size);
        var radius = CornerRadiusOf(AdornedElement);
        var accent = ResourceColor("AccentTextColor", Colors.YellowGreen);

        dc.PushClip(new RectangleGeometry(bounds, radius, radius));

        if (_armed) dc.DrawRectangle(Brush(ResourceColor("DangerColor", Colors.IndianRed), 0.14), null, bounds);

        // Ripple: grows from the pointer to the far corner while fading out.
        var reach = Math.Sqrt(size.Width * size.Width + size.Height * size.Height);
        foreach (var (at, start) in _ripples)
        {
            var t = Math.Clamp((now - start) / RippleMs, 0, 1);
            var r = reach * (1 - Math.Pow(1 - t, 3));
            dc.DrawEllipse(Brush(accent, 0.28 * (1 - t)), null, at, r, r);
        }

        // Busy: a short segment sliding along the bottom edge.
        if (!double.IsNaN(_pendingStart))
        {
            var phase = (now - _pendingStart) % PendingLoopMs / PendingLoopMs;
            var segment = Math.Max(24, size.Width * 0.35);
            var x = -segment + phase * (size.Width + segment);
            dc.DrawRectangle(Brush(accent, 0.15), null, new Rect(0, size.Height - 2, size.Width, 2));
            dc.DrawRectangle(Brush(accent, 1), null, new Rect(x, size.Height - 2, segment, 2));
        }

        dc.Pop();

        if (!double.IsNaN(_glowStart))
        {
            var a = 1 - Math.Clamp((now - _glowStart) / GlowMs, 0, 1);
            var outer = Rect.Inflate(bounds, 2, 2);
            dc.DrawRoundedRectangle(null, new Pen(Brush(accent, 0.25 * a), 4), outer, radius + 2, radius + 2);
            dc.DrawRoundedRectangle(null, new Pen(Brush(accent, 0.9 * a), 1.5), Rect.Inflate(bounds, -0.75, -0.75), radius, radius);
        }

        if (_armed)
            dc.DrawRoundedRectangle(null, new Pen(Brush(ResourceColor("DangerColor", Colors.IndianRed), 1), 1.5),
                Rect.Inflate(bounds, -0.75, -0.75), radius, radius);
    }

    /// <summary>The corner radius of a card, or of a button's template border.</summary>
    private static double CornerRadiusOf(UIElement element)
    {
        if (element is Border border) return border.CornerRadius.TopLeft;
        if (element is Control && VisualTreeHelper.GetChildrenCount(element) > 0
            && VisualTreeHelper.GetChild(element, 0) is Border root)
            return root.CornerRadius.TopLeft;
        return 6;
    }

    private static Color ResourceColor(string key, Color fallback) =>
        Application.Current?.TryFindResource(key) is Color c ? c : fallback;

    private static SolidColorBrush Brush(Color color, double opacity)
    {
        var brush = new SolidColorBrush(color) { Opacity = Math.Clamp(opacity, 0, 1) };
        brush.Freeze();
        return brush;
    }
}
