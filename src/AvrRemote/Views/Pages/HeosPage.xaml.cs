// AVR Control Center
// Copyright (c) 2026 Sergio (@sreddev)
// Licensed under MIT License
// https://github.com/sreddev/AVR-control-center

using System.Windows;
using System.Windows.Input;
using AvrRemote.Heos;
using AvrRemote.ViewModels;

namespace AvrRemote.Views.Pages;

public partial class HeosPage : PageBase
{
    /// <summary>Width the queue panel had before it was collapsed.</summary>
    private double _queueWidth = 260;
    private bool _queueCollapsed;

    public HeosPage() => InitializeComponent();

    private void OnToggleQueue(object sender, RoutedEventArgs e)
    {
        _queueCollapsed = !_queueCollapsed;

        if (_queueCollapsed)
        {
            _queueWidth = ColQueue.ActualWidth > 40 ? ColQueue.ActualWidth : _queueWidth;
            ColQueue.Width = new GridLength(0);
            QueuePanel.Visibility = Visibility.Collapsed;
            QueueSplitter.Visibility = Visibility.Collapsed;
            ShowQueueBtn.Visibility = Visibility.Visible;
        }
        else
        {
            ColQueue.Width = new GridLength(_queueWidth);
            QueuePanel.Visibility = Visibility.Visible;
            QueueSplitter.Visibility = Visibility.Visible;
            ShowQueueBtn.Visibility = Visibility.Collapsed;
        }
    }

    // â”€â”€ Browsing â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private void OnSourceClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.Tag is MusicSource src && DataContext is MainViewModel vm)
            _ = vm.HeosOpenSourceAsync(src);
    }

    /// <summary>
    /// Containers drill in; playable items start playing. Items that are both
    /// (an album, for instance) drill in so the user can pick a track.
    /// </summary>
    private void OnItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement el || el.Tag is not BrowseItem item) return;
        if (DataContext is not MainViewModel vm) return;

        if (item.IsContainer) _ = vm.HeosOpenItemAsync(item);
        else if (item.IsPlayable) _ = vm.HeosPlayItemAsync(item);
    }

    private void OnQueueClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.Tag is BrowseItem item && DataContext is MainViewModel vm)
            _ = vm.HeosQueueItemAsync(item);
        e.Handled = true;
    }

    private void OnAddFavoriteClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.Tag is BrowseItem item && DataContext is MainViewModel vm)
            _ = vm.HeosAddToFavoritesAsync(item);
        e.Handled = true;
    }

    private void OnRemoveFavoriteClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.Tag is BrowseItem item) ConfirmRemove(item);
        e.Handled = true;
    }

    /// <summary>Removing changes the HEOS account on every device, so ask first.</summary>
    private void ConfirmRemove(BrowseItem item)
    {
        if (!item.IsHeosFavorite || DataContext is not MainViewModel vm) return;
        var answer = MessageBox.Show(Window.GetWindow(this)!, $"Remove \"{item.Name}\" from HEOS Favorites?",
            "HEOS Favorites", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        if (answer == MessageBoxResult.Yes) _ = vm.HeosRemoveFromFavoritesAsync(item);
    }

    /// <summary>
    /// Keyboard use of the listing: arrows move between cards (directional
    /// navigation), Enter or Space activates, Delete removes a HEOS Favorite,
    /// Backspace goes up a level.
    /// </summary>
    private void OnBrowseKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        var item = (Keyboard.FocusedElement as FrameworkElement)?.Tag as BrowseItem;

        switch (e.Key)
        {
            case Key.Delete when item is { IsHeosFavorite: true }:
                ConfirmRemove(item);
                e.Handled = true;
                break;
            case Key.Back when vm.Heos.CanGoBack:
                vm.HeosBackCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private void OnQueueItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.Tag is QueueItem item && DataContext is MainViewModel vm)
            _ = vm.HeosPlayQueueItemAsync(item);
    }

    // â”€â”€ Now-playing shortcuts â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private System.Windows.Threading.DispatcherTimer? _seekDebounce;

    /// <summary>
    /// Debounced seek: fires 400ms after the user stops dragging the slider.
    /// Only triggers when the user is actually dragging, not on push events.
    /// </summary>
    private void OnSeekChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (vm.Heos.DurationMs <= 0) return;
        if (!((System.Windows.Controls.Slider)sender).IsMouseCaptureWithin) return;

        var ms = (long)e.NewValue;
        _seekDebounce?.Stop();
        _seekDebounce = new System.Windows.Threading.DispatcherTimer
        {
            Interval = System.TimeSpan.FromMilliseconds(400)
        };
        _seekDebounce.Tick += (_, _) =>
        {
            _seekDebounce.Stop();
            _ = vm.HeosSeekAsync(ms);
        };
        _seekDebounce.Start();
    }

    /// <summary>Clicking the artist name searches the open service for them.</summary>
    private void OnArtistClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        _ = vm.HeosSearchNowPlayingAsync(vm.Heos.NowPlayingArtist, "Artist");
    }

    /// <summary>Clicking the album name searches the open service for it.</summary>
    private void OnAlbumClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        _ = vm.HeosSearchNowPlayingAsync(vm.Heos.NowPlayingAlbum, "Album");
    }

    // â”€â”€ Search â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private void OnSearchClick(object sender, RoutedEventArgs e) => RunSearch();

    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        RunSearch();
        e.Handled = true;
    }

    private void RunSearch()
    {
        if (DataContext is not MainViewModel vm) return;
        var term = SearchBox.Text?.Trim() ?? "";
        if (term.Length == 0) return;

        // Default to "Track" (scid 3) when nothing is selected — broadest match
        var scid = "3";
        if (SearchKind.SelectedItem is Heos.SearchCriteria criteria)
            scid = criteria.Scid;

        _ = vm.HeosSearchAsync(scid, term);
    }

    // â”€â”€ Account â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private void OnSignInClick(object sender, RoutedEventArgs e) => TrySignIn();

    private void OnPasswordKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        TrySignIn();
        e.Handled = true;
    }

    /// <summary>
    /// Hands the credentials to the view model. Nothing is cached here: the
    /// PasswordBox is cleared as soon as the request is dispatched.
    /// </summary>
    private void TrySignIn()
    {
        if (DataContext is not MainViewModel vm) return;

        var user = HeosUser.Text?.Trim() ?? "";
        var pass = HeosPass.Password ?? "";
        if (user.Length == 0 || pass.Length == 0) return;

        _ = vm.HeosSignInAsync(user, pass);
        HeosPass.Clear();
    }
}

