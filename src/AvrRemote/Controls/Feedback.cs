// AVR Desktop Control
// Copyright (c) 2026 Sergio (@sreddev)
// Licensed under MIT License
// https://github.com/sreddev/AVR-desktop-control

using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;

namespace AvrRemote.Controls;

/// <summary>
/// One click-feedback language for the whole app:
/// <list type="bullet">
/// <item>Press: buttons and <see cref="IsPressableProperty"/> cards shrink slightly and ripple from the pointer.</item>
/// <item>Busy: a progress line runs along the bottom while an async command runs.</item>
/// <item>Done: a short accent glow when the command finishes or the <see cref="EchoProperty"/> value changes.</item>
/// <item>Confirm: <see cref="ConfirmProperty"/> buttons need a second click within 3 seconds.</item>
/// </list>
/// Animations are skipped when Windows "Show animations" is off.
/// </summary>
public static class Feedback
{
    /// <summary>Commands quicker than this get the ripple only, no progress line or glow.</summary>
    private static readonly TimeSpan BusyThreshold = TimeSpan.FromMilliseconds(250);

    private static bool Motion => SystemParameters.ClientAreaAnimation;

    /// <summary>Hooks every button in the app. Called once at startup.</summary>
    public static void Install()
    {
        EventManager.RegisterClassHandler(typeof(ButtonBase), UIElement.PreviewMouseLeftButtonDownEvent,
            new MouseButtonEventHandler(OnButtonDown), handledEventsToo: true);
        EventManager.RegisterClassHandler(typeof(ButtonBase), ButtonBase.ClickEvent,
            new RoutedEventHandler(OnButtonClick), handledEventsToo: true);
    }

    // ── Attached properties ─────────────────────────────────────────────────

    /// <summary>Gives a clickable card (a Border with a click handler) the same press and ripple as a button.</summary>
    public static readonly DependencyProperty IsPressableProperty = DependencyProperty.RegisterAttached(
        "IsPressable", typeof(bool), typeof(Feedback), new PropertyMetadata(false, OnIsPressableChanged));

    public static bool GetIsPressable(DependencyObject d) => (bool)d.GetValue(IsPressableProperty);
    public static void SetIsPressable(DependencyObject d, bool value) => d.SetValue(IsPressableProperty, value);

    /// <summary>
    /// The value the receiver reports for this element. When it changes, the
    /// busy line stops and the element glows: the receiver confirmed the change.
    /// </summary>
    public static readonly DependencyProperty EchoProperty = DependencyProperty.RegisterAttached(
        "Echo", typeof(object), typeof(Feedback), new PropertyMetadata(null, OnEchoChanged));

    public static object? GetEcho(DependencyObject d) => d.GetValue(EchoProperty);
    public static void SetEcho(DependencyObject d, object? value) => d.SetValue(EchoProperty, value);

    /// <summary>Destructive button: the first click arms it (red), the second within 3 s runs it.</summary>
    public static readonly DependencyProperty ConfirmProperty = DependencyProperty.RegisterAttached(
        "Confirm", typeof(bool), typeof(Feedback), new PropertyMetadata(false, OnConfirmChanged));

    public static bool GetConfirm(DependencyObject d) => (bool)d.GetValue(ConfirmProperty);
    public static void SetConfirm(DependencyObject d, bool value) => d.SetValue(ConfirmProperty, value);

    private static readonly DependencyProperty PressScaleProperty = DependencyProperty.RegisterAttached(
        "PressScale", typeof(ScaleTransform), typeof(Feedback));

    private static readonly DependencyProperty ArmedProperty = DependencyProperty.RegisterAttached(
        "Armed", typeof(ArmState), typeof(Feedback));

    // ── Public helpers ──────────────────────────────────────────────────────

    /// <summary>Shows the progress line until <see cref="EndPending"/>, an echo, or the timeout.</summary>
    public static void BeginPending(FrameworkElement el, TimeSpan? timeout = null) =>
        FeedbackAdorner.For(el)?.BeginPending(timeout ?? TimeSpan.FromSeconds(6));

    public static void EndPending(FrameworkElement el) => FeedbackAdorner.Find(el)?.EndPending();

    /// <summary>Short accent glow: "done".</summary>
    public static void Glow(FrameworkElement el) => FeedbackAdorner.For(el)?.Glow();

    /// <summary>
    /// Busy line while <paramref name="task"/> runs, then a glow. With
    /// <paramref name="always"/> the line shows at once, even for quick tasks.
    /// </summary>
    public static async Task Run(FrameworkElement el, Task task, bool always = false)
    {
        var shown = always;
        if (always) BeginPending(el);
        else if (await Task.WhenAny(task, Task.Delay(BusyThreshold)) != task)
        {
            BeginPending(el);
            shown = true;
        }
        var started = DateTime.UtcNow;
        try { await task; }
        catch { return; }   // the command reports its own errors
        if (!shown) return;
        // Keep the line long enough to be read as "working", not as a flicker.
        var left = TimeSpan.FromMilliseconds(400) - (DateTime.UtcNow - started);
        if (always && left > TimeSpan.Zero) await Task.Delay(left);
        EndPending(el);
        Glow(el);
    }

    // ── Press and ripple ────────────────────────────────────────────────────

    private static void OnButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ButtonBase button || !HasFeedback(button)) return;
        // Nested buttons (a heart inside a tile): only the innermost reacts.
        if (!ReferenceEquals(Innermost(e.OriginalSource as DependencyObject), button)) return;
        Press(button, e);
    }

    private static void OnButtonClick(object sender, RoutedEventArgs e)
    {
        if (!ReferenceEquals(sender, e.OriginalSource) || sender is not ButtonBase button) return;
        if (button.Command is not IAsyncRelayCommand command) return;
        // Click is raised just before the command runs: look once it has started.
        button.Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (command.ExecutionTask is { IsCompleted: false } task) _ = Run(button, task);
        });
    }

    private static void OnIsPressableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement el) return;
        el.PreviewMouseLeftButtonDown -= OnPressableDown;
        if ((bool)e.NewValue) el.PreviewMouseLeftButtonDown += OnPressableDown;
    }

    private static void OnPressableDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement el) return;
        // A button inside the card handles its own feedback.
        if (Innermost(e.OriginalSource as DependencyObject) is not null) return;
        Press(el, e);
    }

    /// <summary>Plain buttons, toggle buttons and navigation items; not switches, checkboxes or scrollbars.</summary>
    private static bool HasFeedback(ButtonBase b)
    {
        if (b is RepeatButton or CheckBox or RadioButton or Wpf.Ui.Controls.ToggleSwitch) return false;
        for (DependencyObject? p = b; p is not null; p = VisualTreeHelper.GetParent(p))
            if (p is ScrollBar or Slider or ComboBox or Wpf.Ui.Controls.TitleBar) return false;
        return true;
    }

    private static ButtonBase? Innermost(DependencyObject? source)
    {
        for (var p = source; p is not null; p = p is Visual ? VisualTreeHelper.GetParent(p) : LogicalTreeHelper.GetParent(p))
            if (p is ButtonBase b) return b;
        return null;
    }

    private static void Press(FrameworkElement el, MouseButtonEventArgs e)
    {
        if (!Motion) return;
        FeedbackAdorner.For(el)?.Ripple(e.GetPosition(el));

        var scale = PressScale(el);
        if (scale is null) return;
        // Big cards shrink by a few pixels, not a fixed percentage.
        Animate(scale, Math.Max(0.97, 1 - 6 / Math.Max(el.ActualWidth, 1)), 80);

        var released = false;
        MouseButtonEventHandler? up = null;
        MouseEventHandler? leave = null;
        void Release()
        {
            if (released) return;
            released = true;
            el.PreviewMouseLeftButtonUp -= up;
            el.MouseLeave -= leave;
            el.LostMouseCapture -= leave;
            Animate(scale, 1, 160);
        }
        up = (_, _) => Release();
        leave = (_, _) => Release();
        el.PreviewMouseLeftButtonUp += up;
        el.MouseLeave += leave;
        el.LostMouseCapture += leave;
    }

    /// <summary>Our own ScaleTransform; null when the element already has another transform.</summary>
    private static ScaleTransform? PressScale(FrameworkElement el)
    {
        if (el.GetValue(PressScaleProperty) is ScaleTransform own && ReferenceEquals(el.RenderTransform, own))
            return own;
        if (el.RenderTransform is not null && !el.RenderTransform.Value.IsIdentity) return null;
        var scale = new ScaleTransform();
        el.RenderTransformOrigin = new Point(0.5, 0.5);
        el.RenderTransform = scale;
        el.SetValue(PressScaleProperty, scale);
        return scale;
    }

    private static void Animate(ScaleTransform scale, double to, int ms)
    {
        var anim = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
    }

    // ── Echo ────────────────────────────────────────────────────────────────

    private static void OnEchoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // The first value, and values set while the page loads, are not changes.
        if (d is not FrameworkElement el || !el.IsLoaded || e.OldValue is null) return;
        if (Equals(e.OldValue, e.NewValue)) return;
        EndPending(el);
        Glow(el);
    }

    // ── Two-click confirm ───────────────────────────────────────────────────

    private sealed class ArmState
    {
        public required DispatcherTimer Timer { get; init; }
        public required ToolTip Tip { get; init; }
        public object? PreviousTip { get; init; }
    }

    private static void OnConfirmChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement el) return;
        el.PreviewMouseLeftButtonDown -= OnConfirmMouseDown;
        el.PreviewKeyDown -= OnConfirmKeyDown;
        if (!(bool)e.NewValue) return;
        el.PreviewMouseLeftButtonDown += OnConfirmMouseDown;
        el.PreviewKeyDown += OnConfirmKeyDown;
    }

    private static void OnConfirmMouseDown(object sender, MouseButtonEventArgs e) => Confirm((FrameworkElement)sender, e);

    private static void OnConfirmKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space) Confirm((FrameworkElement)sender, e);
    }

    /// <summary>First press arms and swallows the input; the second one goes through.</summary>
    private static void Confirm(FrameworkElement el, RoutedEventArgs e)
    {
        if (el.GetValue(ArmedProperty) is ArmState)
        {
            Disarm(el);
            return;
        }
        e.Handled = true;

        var tip = new ToolTip
        {
            Content = "Click again to confirm",
            PlacementTarget = el,
            Placement = PlacementMode.Top
        };
        var state = new ArmState
        {
            Timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) },
            Tip = tip,
            PreviousTip = el.ToolTip
        };
        state.Timer.Tick += (_, _) => Disarm(el);
        el.SetValue(ArmedProperty, state);
        el.ToolTip = tip;
        tip.IsOpen = true;
        state.Timer.Start();
        FeedbackAdorner.For(el)?.SetArmed(true);
    }

    private static void Disarm(FrameworkElement el)
    {
        if (el.GetValue(ArmedProperty) is not ArmState state) return;
        state.Timer.Stop();
        state.Tip.IsOpen = false;
        el.ToolTip = state.PreviousTip;
        el.ClearValue(ArmedProperty);
        FeedbackAdorner.Find(el)?.SetArmed(false);
    }
}
