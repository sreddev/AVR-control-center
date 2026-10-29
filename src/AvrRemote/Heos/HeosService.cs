// AVR Desktop Control
// Copyright (c) 2026 Sergio (@sreddev)
// Licensed under MIT License
// https://github.com/sreddev/AVR-desktop-control

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvrRemote.Heos;

/// <summary>
/// Owns the HEOS connection and exposes everything the UI binds to: now
/// playing, progress, browse listings, search results, the queue and the
/// account. Progress arrives through HEOS change events rather than polling,
/// which is the only way the protocol reports playback position.
/// </summary>
public sealed partial class HeosService : ObservableObject, IAsyncDisposable
{
    private HeosClient? _client;
    private UpnpTransport? _upnp;
    private CancellationTokenSource? _pollCts;

    /// <summary>Host to reconnect to after a dropped connection; null once the user disconnects.</summary>
    private string? _host;
    private CancellationTokenSource? _reconnectCts;

    /// <summary>Bumped on every navigation so a slow, older listing can't overwrite a newer one.</summary>
    private int _loadVersion;

    [ObservableProperty] private bool _isAvailable;
    [ObservableProperty] private bool _isBusy;

    /// <summary>True = grid of cards, False = compact list. Toggled by the user.</summary>
    [ObservableProperty] private bool _gridView = true;

    // â”€â”€ Now playing â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    [ObservableProperty] private string _nowPlayingTitle = "";
    [ObservableProperty] private string _nowPlayingArtist = "";
    [ObservableProperty] private string _nowPlayingAlbum = "";
    [ObservableProperty] private string _nowPlayingImageUrl = "";
    [ObservableProperty] private string _playState = "stop";
    [ObservableProperty] private bool _isPlaying;

    // â”€â”€ Progress (driven by change events) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    [ObservableProperty] private long _positionMs;
    [ObservableProperty] private long _durationMs;

    public string PositionText => Format(PositionMs);
    public string DurationText => DurationMs > 0 ? Format(DurationMs) : "--:--";
    public double ProgressPercent => DurationMs > 0 ? PositionMs * 100.0 / DurationMs : 0;

    partial void OnPositionMsChanged(long value) => NotifyProgress();
    partial void OnDurationMsChanged(long value) => NotifyProgress();

    private void NotifyProgress()
    {
        OnPropertyChanged(nameof(PositionText));
        OnPropertyChanged(nameof(DurationText));
        OnPropertyChanged(nameof(ProgressPercent));
    }

    private static string Format(long ms)
    {
        var t = TimeSpan.FromMilliseconds(ms);
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}"
                                 : $"{t.Minutes}:{t.Seconds:00}";
    }

    // â”€â”€ Account â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    [ObservableProperty] private bool _isSignedIn;
    [ObservableProperty] private string _accountUsername = "";
    [ObservableProperty] private string _accountMessage = "";

    // â”€â”€ Browsing â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public ObservableCollection<MusicSource> Sources { get; } = new();
    public ObservableCollection<BrowseItem> Items { get; } = new();
    public ObservableCollection<QueueItem> Queue { get; } = new();
    public ObservableCollection<SearchCriteria> SearchOptions { get; } = new();

    /// <summary>Navigation trail; the last entry is where we are now.</summary>
    private readonly List<BrowseCrumb> _trail = new();

    [ObservableProperty] private string _currentLocation = "";
    [ObservableProperty] private bool _canGoBack;

    /// <summary>Shown in place of the listing when it is empty or failed to load.</summary>
    [ObservableProperty] private string _emptyMessage = "";

    /// <summary>Short-lived feedback after an action ("Added to HEOS Favorites").</summary>
    [ObservableProperty] private string _statusMessage = "";

    /// <summary>"heos" or "tunein" while a favorites list is open, otherwise empty.</summary>
    [ObservableProperty] private string _favoritesTab = "";

    public bool IsHeosFavoritesOpen => FavoritesTab == "heos";
    public bool IsTuneInFavoritesOpen => FavoritesTab == "tunein";

    partial void OnFavoritesTabChanged(string value)
    {
        OnPropertyChanged(nameof(IsHeosFavoritesOpen));
        OnPropertyChanged(nameof(IsTuneInFavoritesOpen));
    }

    // â”€â”€ Connection â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private static void Log(string message)
    {
        try
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AvrHeos.log");
            System.IO.File.AppendAllText(path, $"{DateTime.Now:HH:mm:ss.fff}  {message}{Environment.NewLine}");
        }
        catch { }
    }

    public async Task<bool> ConnectAsync(string host, CancellationToken ct = default)
    {
        await DisconnectAsync().ConfigureAwait(false);
        _host = host;
        return await OpenAsync(host, ct).ConfigureAwait(false);
    }

    private async Task<bool> OpenAsync(string host, CancellationToken ct)
    {
        await CloseClientAsync().ConfigureAwait(false);
        Log($"ConnectAsync host={host}");

        var client = new HeosClient(host);
        client.EventReceived += OnHeosEvent;
        client.Disconnected += OnClientDisconnected;
        _client = client;
        _upnp = new UpnpTransport(host);

        bool ok;
        try { ok = await client.ConnectAsync(ct).ConfigureAwait(false); }
        catch (Exception ex) { Log($"threw: {ex.Message}"); ok = false; }

        Log($"result={ok} pid={client.PlayerId ?? "(null)"}");
        Ui(() => IsAvailable = ok);
        if (!ok)
        {
            await CloseClientAsync().ConfigureAwait(false);
            return false;
        }

        await RefreshAccountAsync(ct).ConfigureAwait(false);
        await RefreshSourcesAsync(ct).ConfigureAwait(false);
        await RefreshNowPlayingAsync(ct).ConfigureAwait(false);
        await RefreshQueueAsync(ct).ConfigureAwait(false);
        StartPolling();

        // After a reconnect, reload whatever listing was open.
        if (_trail.Count >= 2)
            await LoadCurrentAsync(ct).ConfigureAwait(false);
        return true;
    }

    public async Task DisconnectAsync()
    {
        _host = null;
        _reconnectCts?.Cancel();
        _reconnectCts = null;
        await CloseClientAsync().ConfigureAwait(false);
    }

    private async Task CloseClientAsync()
    {
        _pollCts?.Cancel();
        _pollCts = null;
        if (_client is not null)
        {
            _client.EventReceived -= OnHeosEvent;
            _client.Disconnected -= OnClientDisconnected;
            await _client.DisposeAsync().ConfigureAwait(false);
            _client = null;
        }
        Ui(() => IsAvailable = false);
    }

    /// <summary>
    /// The socket closed under us (receiver in standby, network drop). Every
    /// request would now fail silently, so reconnect in the background.
    /// </summary>
    private void OnClientDisconnected(object? sender, EventArgs e)
    {
        if (!ReferenceEquals(sender, _client)) return;
        Log("connection lost");
        Ui(() => IsAvailable = false);
        _ = ReconnectLoopAsync();
    }

    private async Task ReconnectLoopAsync()
    {
        if (_host is null || _reconnectCts is not null) return;
        var cts = new CancellationTokenSource();
        _reconnectCts = cts;
        var delay = TimeSpan.FromSeconds(2);
        try
        {
            while (!cts.IsCancellationRequested && _host is { } host)
            {
                await Task.Delay(delay, cts.Token).ConfigureAwait(false);
                if (await OpenAsync(host, cts.Token).ConfigureAwait(false)) return;
                delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 60));
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (ReferenceEquals(_reconnectCts, cts)) _reconnectCts = null;
        }
    }

    /// <summary>
    /// Handles unsolicited events. The progress event is what makes the seek
    /// bar move; the rest tell us to re-read the current track or queue.
    /// </summary>
    private void OnHeosEvent(object? sender, HeosEvent e)
    {
        switch (e.Command)
        {
            case "event/player_now_playing_progress":
            {
                var cur = ValueOf(e.Message, "cur_pos");
                var dur = ValueOf(e.Message, "duration");
                Ui(() =>
                {
                    if (long.TryParse(cur, out var c)) PositionMs = c;
                    if (long.TryParse(dur, out var d)) DurationMs = d;
                });
                break;
            }
            case "event/player_now_playing_changed":
                _ = RefreshNowPlayingAsync();
                break;
            case "event/player_state_changed":
            {
                var state = ValueOf(e.Message, "state");
                if (state is not null)
                    Ui(() =>
                    {
                        PlayState = state;
                        IsPlaying = state.Equals("play", StringComparison.OrdinalIgnoreCase);
                    });
                break;
            }
            case "event/player_queue_changed":
                _ = RefreshQueueAsync();
                break;
        }
    }

    private static string? ValueOf(string message, string key)
    {
        foreach (var part in message.Split('&'))
            if (part.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
                return part[(key.Length + 1)..];
        return null;
    }

    private void StartPolling()
    {
        // Events cover most changes, but a slow heartbeat keeps us honest if a
        // notification is ever missed.
        // A heartbeat that goes unanswered twice means the connection is dead
        // without the socket noticing; aborting it triggers the reconnect.
        _pollCts = new CancellationTokenSource();
        var token = _pollCts.Token;
        var client = _client;
        _ = Task.Run(async () =>
        {
            var missed = 0;
            while (!token.IsCancellationRequested)
            {
                try { await Task.Delay(TimeSpan.FromSeconds(15), token).ConfigureAwait(false); }
                catch { return; }
                if (client is null) return;
                missed = await client.HeartbeatAsync(token).ConfigureAwait(false) ? 0 : missed + 1;
                if (missed >= 2)
                {
                    Log("heartbeat lost");
                    client.Abort();
                    return;
                }
                await RefreshNowPlayingAsync(token).ConfigureAwait(false);
            }
        }, token);
    }

    // â”€â”€ Now playing â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public async Task RefreshNowPlayingAsync(CancellationToken ct = default)
    {
        if (_client is null) return;
        var media = await _client.GetNowPlayingAsync(ct).ConfigureAwait(false);
        var state = await _client.GetPlayStateAsync(ct).ConfigureAwait(false);

        Ui(() =>
        {
            if (media is not null)
            {
                // Internet radio reports a station name instead of a song title.
                NowPlayingTitle = string.IsNullOrWhiteSpace(media.Title) ? media.Station : media.Title;
                NowPlayingArtist = media.Artist;
                NowPlayingAlbum = media.Album;
                NowPlayingImageUrl = media.ImageUrl;
            }
            if (state is not null)
            {
                PlayState = state;
                IsPlaying = state.Equals("play", StringComparison.OrdinalIgnoreCase);
            }
        });
    }

    // â”€â”€ Sources â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public async Task RefreshSourcesAsync(CancellationToken ct = default)
    {
        if (_client is null) return;
        var json = await _client.GetMusicSourcesRawAsync(ct).ConfigureAwait(false);
        if (json is null) return;

        var parsed = new List<MusicSource>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("payload", out var payload)) return;
            foreach (var item in payload.EnumerateArray())
            {
                var available = item.TryGetProperty("available", out var av) &&
                                av.ToString().Equals("true", StringComparison.OrdinalIgnoreCase);
                parsed.Add(new MusicSource(
                    Name:     Str(item, "name"),
                    Sid:      Str(item, "sid"),
                    ImageUrl: Str(item, "image_url"),
                    Type:     Str(item, "type"),
                    Available: available,
                    Username: Str(item, "service_username")));
            }
        }
        catch { return; }

        Ui(() =>
        {
            Sources.Clear();
            // Usable sources first so the good stuff is reachable immediately.
            foreach (var s in parsed.OrderByDescending(s => s.Available)) Sources.Add(s);
        });
    }

    // â”€â”€ Browsing â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Opens the root of a source. The trail always starts with a synthetic
    /// "Servicios" crumb so the back button can return to the service picker
    /// from the very first level.
    /// </summary>
    public async Task OpenSourceAsync(MusicSource source, CancellationToken ct = default)
    {
        _trail.Clear();
        _trail.Add(new BrowseCrumb("Servicios", "", null));   // sentinel root
        _trail.Add(new BrowseCrumb(source.Name, source.Sid, null));
        await LoadCurrentAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Returns to the service picker, clearing the listing.</summary>
    public void GoHome()
    {
        _trail.Clear();
        Interlocked.Increment(ref _loadVersion);
        Ui(() =>
        {
            Items.Clear();
            SearchOptions.Clear();
            CurrentLocation = "";
            CanGoBack = false;
            EmptyMessage = "";
            FavoritesTab = "";
            IsBusy = false;
        });
    }

    /// <summary>Drills into a container, pushing it onto the trail.</summary>
    public async Task OpenItemAsync(BrowseItem item, CancellationToken ct = default)
    {
        if (!item.IsContainer) return;

        // Guard against the Tidal "My Music" infinite loop: if the new cid
        // is identical to one already on the trail, the receiver is sending
        // us in circles.
        if (_trail.Any(c => string.Equals(c.Cid, item.Cid, StringComparison.OrdinalIgnoreCase)
                         && string.Equals(c.Sid, item.Sid, StringComparison.OrdinalIgnoreCase)))
            return;

        _trail.Add(new BrowseCrumb(item.Name, item.Sid, item.Cid));
        await LoadCurrentAsync(ct).ConfigureAwait(false);
    }

    public async Task GoBackAsync(CancellationToken ct = default)
    {
        if (_trail.Count <= 1) return;
        _trail.RemoveAt(_trail.Count - 1);

        // Only the sentinel left: we're back at the service picker.
        if (_trail.Count == 1)
        {
            GoHome();
            return;
        }
        await LoadCurrentAsync(ct).ConfigureAwait(false);
    }

    private async Task LoadCurrentAsync(CancellationToken ct)
    {
        if (_client is null || _trail.Count < 2) return;
        var crumb = _trail[^1];
        var version = Interlocked.Increment(ref _loadVersion);
        var tab = crumb.Sid == HeosSid.Favorites ? "heos"
                : crumb.Sid == HeosSid.TuneIn && IsTuneInFavoritesCid(crumb.Cid) ? "tunein"
                : "";

        Ui(() =>
        {
            IsBusy = true;
            EmptyMessage = "";
            FavoritesTab = tab;
            // Skip the sentinel when rendering the breadcrumb.
            CurrentLocation = string.Join("  >  ", _trail.Skip(1).Select(c => c.Label));
            CanGoBack = true;
        });

        var (parsed, error) = await BrowseAllAsync(crumb.Sid, crumb.Cid, ct).ConfigureAwait(false);
        if (version != _loadVersion) return;   // the user has moved on

        // Detect the Tidal firmware bug where certain containers (My Music,
        // What's New) return the root-level items instead of their contents.
        if (parsed.Count >= 3 && _trail.Count > 2 &&
            parsed.All(i => i.IsContainer && !i.IsPlayable) &&
            parsed.Any(i => i.Name == "Playlists") &&
            parsed.Any(i => i.Name == "Genres"))
        {
            // This container doesn't work. Go back and report it.
            _trail.RemoveAt(_trail.Count - 1);
            Ui(() =>
            {
                CurrentLocation = string.Join("  >  ", _trail.Skip(1).Select(c => c.Label));
                CanGoBack = _trail.Count > 1;
                IsBusy = false;
                // Leave Items unchanged so the user sees where they were
            });
            return;
        }

        // Load the service's search options when we enter it (index 1 is the
        // service itself, index 0 being the sentinel root).
        if (_trail.Count == 2)
            await LoadSearchCriteriaAsync(crumb.Sid, ct).ConfigureAwait(false);
        if (version != _loadVersion) return;

        var empty = error is not null ? $"Couldn't load this list: {error}"
                  : parsed.Count > 0 ? ""
                  : tab == "heos" ? "No HEOS Favorites yet. Use the heart on a station, or on Now Playing, to add one."
                  : tab == "tunein" ? "No TuneIn Favorites. They are managed in your TuneIn account."
                  : "This list is empty.";

        Ui(() =>
        {
            Items.Clear();
            foreach (var i in parsed) Items.Add(i);
            EmptyMessage = empty;
            IsBusy = false;
        });
    }

    /// <summary>
    /// Reads a whole listing. HEOS returns at most 100 rows per request, so
    /// longer lists (TuneIn categories, big favorites lists) are paged.
    /// </summary>
    private async Task<(List<BrowseItem> Items, string? Error)> BrowseAllAsync(
        string sid, string? cid, CancellationToken ct)
    {
        const int page = 100;
        var all = new List<BrowseItem>();
        while (_client is { } client)
        {
            var json = await client.BrowseAsync(sid, cid, all.Count, all.Count + page - 1, ct)
                .ConfigureAwait(false);
            var error = HeosClient.ErrorOf(json);
            if (error is not null) return (all, all.Count == 0 ? error : null);

            var rows = ParseBrowse(json, sid, all.Count);
            all.AddRange(rows);

            // "count" is the full size; some services report 0, so also stop on a short page.
            _ = int.TryParse(HeosClient.ValueFromMessage(json!, "count"), out var count);
            if (rows.Count < page || (count > 0 && all.Count >= count) || all.Count >= 2000) break;
        }
        return (all, null);
    }

    private static List<BrowseItem> ParseBrowse(string? json, string sid, int offset = 0)
    {
        var list = new List<BrowseItem>();
        if (json is null) return list;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("payload", out var payload)) return list;
            if (payload.ValueKind != JsonValueKind.Array) return list;

            foreach (var item in payload.EnumerateArray())
            {
                var container = Str(item, "container").Equals("yes", StringComparison.OrdinalIgnoreCase);
                var playable = Str(item, "playable").Equals("yes", StringComparison.OrdinalIgnoreCase);
                // Nested services (e.g. inside "Local Music") carry their own sid.
                var itemSid = Str(item, "sid");
                list.Add(new BrowseItem(
                    Name:        HeosClient.DecodeName(Str(item, "name")),
                    Sid:         string.IsNullOrEmpty(itemSid) ? sid : itemSid,
                    Cid:         Str(item, "cid"),
                    Mid:         Str(item, "mid"),
                    ImageUrl:    Str(item, "image_url"),
                    Type:        Str(item, "type"),
                    IsContainer: container,
                    IsPlayable:  playable,
                    Position:    offset + list.Count + 1));
            }
        }
        catch { }
        return list;
    }

    // ── Favorites ───────────────────────────────────────────────────────────

    /// <summary>TuneIn's own favorites (its "presets") live in a container at the TuneIn root.</summary>
    private static bool IsTuneInFavoritesCid(string? cid) =>
        cid is not null && cid.Contains("c%3Dpresets", StringComparison.OrdinalIgnoreCase);

    /// <summary>Opens the HEOS Favorites list (the receiver's own presets).</summary>
    public Task OpenHeosFavoritesAsync(CancellationToken ct = default)
    {
        var source = Sources.FirstOrDefault(s => s.Sid == HeosSid.Favorites)
                     ?? new MusicSource("Favorites", HeosSid.Favorites, "", "heos_service", true, "");
        return OpenSourceAsync(source, ct);
    }

    /// <summary>Opens TuneIn > Favorites, the stations saved in the TuneIn account.</summary>
    public async Task OpenTuneInFavoritesAsync(CancellationToken ct = default)
    {
        if (_client is null) return;
        var tuneIn = Sources.FirstOrDefault(s => s.Sid == HeosSid.TuneIn);
        if (tuneIn is null)
        {
            Flash("TuneIn is not available on this receiver");
            return;
        }

        _trail.Clear();
        _trail.Add(new BrowseCrumb("Servicios", "", null));   // sentinel root
        _trail.Add(new BrowseCrumb(tuneIn.Name, tuneIn.Sid, null));

        var (root, error) = await BrowseAllAsync(tuneIn.Sid, null, ct).ConfigureAwait(false);
        var favorites = root.FirstOrDefault(i => i.IsContainer && IsTuneInFavoritesCid(i.Cid))
                        ?? root.FirstOrDefault(i => i.IsContainer &&
                               i.Name.Equals("Favorites", StringComparison.OrdinalIgnoreCase));
        if (favorites is null)
        {
            // No favorites container (not signed in to TuneIn): show the TuneIn root instead.
            await LoadCurrentAsync(ct).ConfigureAwait(false);
            Flash(error ?? "TuneIn has no Favorites folder. Sign in to TuneIn in the HEOS app.");
            return;
        }

        _trail.Add(new BrowseCrumb(favorites.Name, favorites.Sid, favorites.Cid));
        await LoadSearchCriteriaAsync(tuneIn.Sid, ct).ConfigureAwait(false);
        await LoadCurrentAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Adds a browsed station (TuneIn and other radio services) to HEOS Favorites.</summary>
    public async Task AddToFavoritesAsync(BrowseItem item, CancellationToken ct = default)
    {
        if (_client is null || !item.CanAddToFavorites) return;
        var reply = await _client.AddStationToFavoritesAsync(item.Sid, item.Mid, item.Name, ct)
            .ConfigureAwait(false);
        var error = HeosClient.ErrorOf(reply);
        Flash(error is null ? $"Added to HEOS Favorites: {item.Name}" : $"Couldn't add {item.Name}: {error}");
    }

    /// <summary>Adds the station playing now to HEOS Favorites.</summary>
    public async Task AddNowPlayingToFavoritesAsync(CancellationToken ct = default)
    {
        if (_client is null) return;
        var reply = await _client.AddNowPlayingToFavoritesAsync(ct).ConfigureAwait(false);
        var error = HeosClient.ErrorOf(reply);
        Flash(error is null ? $"Added to HEOS Favorites: {NowPlayingTitle}"
                            : $"Couldn't add to HEOS Favorites: {error}");
        if (error is null && FavoritesTab == "heos")
            await LoadCurrentAsync(ct).ConfigureAwait(false);
    }

    public async Task RemoveFromFavoritesAsync(BrowseItem item, CancellationToken ct = default)
    {
        if (_client is null || !item.IsHeosFavorite) return;
        var reply = await _client.RemoveFromFavoritesAsync(item.Mid, ct).ConfigureAwait(false);
        var error = HeosClient.ErrorOf(reply);
        Flash(error is null ? $"Removed from HEOS Favorites: {item.Name}" : $"Couldn't remove {item.Name}: {error}");
        // Positions shift after a removal, so reload rather than patch the list.
        if (FavoritesTab == "heos")
            await LoadCurrentAsync(ct).ConfigureAwait(false);
    }

    private CancellationTokenSource? _flashCts;

    /// <summary>Shows <see cref="StatusMessage"/> for a few seconds.</summary>
    private void Flash(string message)
    {
        _flashCts?.Cancel();
        var cts = new CancellationTokenSource();
        _flashCts = cts;
        Ui(() => StatusMessage = message);
        _ = Task.Delay(TimeSpan.FromSeconds(5), cts.Token).ContinueWith(t =>
        {
            if (!t.IsCanceled) Ui(() => StatusMessage = "");
        }, TaskScheduler.Default);
    }

    private async Task LoadSearchCriteriaAsync(string sid, CancellationToken ct)
    {
        if (_client is null) return;
        var json = await _client.GetSearchCriteriaAsync(sid, ct).ConfigureAwait(false);
        var list = new List<SearchCriteria>();
        if (json is not null)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("payload", out var payload) &&
                    payload.ValueKind == JsonValueKind.Array)
                {
                    foreach (var c in payload.EnumerateArray())
                        list.Add(new SearchCriteria(Str(c, "name"), Str(c, "scid")));
                }
            }
            catch { }
        }
        Ui(() =>
        {
            SearchOptions.Clear();
            foreach (var c in list) SearchOptions.Add(c);
        });
    }

    /// <summary>Runs a search inside the source currently being browsed.</summary>
    public async Task SearchAsync(string scid, string term, CancellationToken ct = default)
    {
        if (_client is null || _trail.Count < 2 || string.IsNullOrWhiteSpace(term)) return;
        var service = _trail[1];   // index 0 is the sentinel root

        var version = Interlocked.Increment(ref _loadVersion);
        Ui(() => IsBusy = true);
        var json = await _client.SearchAsync(service.Sid, scid, term, ct).ConfigureAwait(false);
        if (version != _loadVersion) return;
        var parsed = ParseBrowse(json, service.Sid);
        var error = HeosClient.ErrorOf(json);

        Ui(() =>
        {
            Items.Clear();
            foreach (var i in parsed) Items.Add(i);
            CurrentLocation = $"{service.Label}  >  Search: {term}";
            CanGoBack = true;
            FavoritesTab = "";
            EmptyMessage = error is not null ? $"Search failed: {error}"
                         : parsed.Count == 0 ? $"Nothing found for \"{term}\"." : "";
            IsBusy = false;
        });
    }

    /// <summary>
    /// Searches the currently open service for a term taken from the now-playing
    /// card, so clicking an artist or album name jumps straight to it.
    /// </summary>
    public async Task SearchFromNowPlayingAsync(string term, string kind, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(term)) return;

        // Fall back to the first available service that supports searching.
        if (_trail.Count < 2)
        {
            var service = Sources.FirstOrDefault(s => s.Available && s.Type == "music_service");
            if (service is null) return;
            await OpenSourceAsync(service, ct).ConfigureAwait(false);
        }

        // Artist = scid 1, Album = scid 2 in the HEOS criteria list.
        var scid = SearchOptions.FirstOrDefault(o =>
            o.Name.Equals(kind, StringComparison.OrdinalIgnoreCase))?.Scid
            ?? (kind.Equals("Artist", StringComparison.OrdinalIgnoreCase) ? "1" : "2");

        await SearchAsync(scid, term, ct).ConfigureAwait(false);
    }

    // â”€â”€ Playback of browsed items â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>Plays the item now, replacing whatever was queued.</summary>
    public async Task PlayItemAsync(BrowseItem item, CancellationToken ct = default)
    {
        if (_client is null) return;
        var container = string.IsNullOrEmpty(item.Cid) && _trail.Count > 0
            ? _trail[^1].Cid ?? ""
            : item.Cid;

        if (item.IsHeosFavorite)
        {
            // HEOS Favorites are presets: play_stream rejects sid 1028.
            var error = HeosClient.ErrorOf(await _client.PlayPresetAsync(item.Position, ct).ConfigureAwait(false));
            if (error is not null) Flash($"Couldn't play {item.Name}: {error}");
        }
        else if (!string.IsNullOrEmpty(item.Mid))
        {
            // Radio stations need the friendly name so the receiver can label them.
            if (item.Type.Equals("station", StringComparison.OrdinalIgnoreCase))
                await _client.PlayStationAsync(item.Sid, container, item.Mid, item.Name, ct)
                    .ConfigureAwait(false);
            else
                await _client.PlayStreamAsync(item.Sid, container, item.Mid, ct)
                    .ConfigureAwait(false);
        }
        else if (!string.IsNullOrEmpty(item.Cid))
        {
            // Whole album or playlist: replace the queue and start playing.
            await _client.AddToQueueAsync(item.Sid, item.Cid, null, aid: 4, ct)
                .ConfigureAwait(false);
        }

        await Task.Delay(600, ct).ConfigureAwait(false);
        await RefreshNowPlayingAsync(ct).ConfigureAwait(false);
        await RefreshQueueAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Appends the item to the end of the queue.</summary>
    public async Task QueueItemAsync(BrowseItem item, CancellationToken ct = default)
    {
        if (_client is null || !(item.CanQueue || item.IsContainer)) return;
        var container = string.IsNullOrEmpty(item.Cid) && _trail.Count > 0
            ? _trail[^1].Cid ?? ""
            : item.Cid;
        await _client.AddToQueueAsync(item.Sid, container, item.Mid, aid: 3, ct).ConfigureAwait(false);
        await Task.Delay(500, ct).ConfigureAwait(false);
        await RefreshQueueAsync(ct).ConfigureAwait(false);
    }

    // â”€â”€ Queue â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public async Task RefreshQueueAsync(CancellationToken ct = default)
    {
        if (_client is null) return;
        var json = await _client.GetQueueAsync(ct: ct).ConfigureAwait(false);
        var list = new List<QueueItem>();
        if (json is not null)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("payload", out var payload) &&
                    payload.ValueKind == JsonValueKind.Array)
                {
                    foreach (var q in payload.EnumerateArray())
                        list.Add(new QueueItem(
                            QueueId:  Str(q, "qid"),
                            Title:    Str(q, "song"),
                            Artist:   Str(q, "artist"),
                            Album:    Str(q, "album"),
                            ImageUrl: Str(q, "image_url")));
                }
            }
            catch { }
        }
        Ui(() =>
        {
            Queue.Clear();
            foreach (var q in list) Queue.Add(q);
        });
    }

    public async Task PlayQueueItemAsync(QueueItem item, CancellationToken ct = default)
    {
        if (_client is null) return;
        await _client.PlayQueueItemAsync(item.QueueId, ct).ConfigureAwait(false);
        await Task.Delay(500, ct).ConfigureAwait(false);
        await RefreshNowPlayingAsync(ct).ConfigureAwait(false);
    }

    public async Task ClearQueueAsync(CancellationToken ct = default)
    {
        if (_client is null) return;
        await _client.ClearQueueAsync(ct).ConfigureAwait(false);
        await RefreshQueueAsync(ct).ConfigureAwait(false);
    }

    // â”€â”€ Account â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public async Task RefreshAccountAsync(CancellationToken ct = default)
    {
        if (_client is null) return;
        var user = await _client.CheckAccountAsync(ct).ConfigureAwait(false);
        Log($"account user={user ?? "(null)"}");
        Ui(() =>
        {
            AccountUsername = user ?? "";
            IsSignedIn = !string.IsNullOrEmpty(user);
        });
    }

    public async Task<bool> SignInAsync(string username, string password, CancellationToken ct = default)
    {
        if (_client is null) return false;
        Ui(() => AccountMessage = "Signing in...");

        var (ok, message) = await _client.SignInAsync(username, password, ct).ConfigureAwait(false);
        Ui(() => AccountMessage = message);

        if (ok)
        {
            await RefreshAccountAsync(ct).ConfigureAwait(false);
            await RefreshSourcesAsync(ct).ConfigureAwait(false);
        }
        return ok;
    }

    public async Task SignOutAsync(CancellationToken ct = default)
    {
        if (_client is null) return;
        await _client.SignOutAsync(ct).ConfigureAwait(false);
        await RefreshAccountAsync(ct).ConfigureAwait(false);
        await RefreshSourcesAsync(ct).ConfigureAwait(false);
        Ui(() => AccountMessage = "Signed out");
    }

    // â”€â”€ Transport â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public Task PlayPauseAsync() =>
        _client is null ? Task.CompletedTask
                        : IsPlaying ? _client.PauseAsync() : _client.PlayAsync();

    public Task NextAsync() => _client?.NextAsync() ?? Task.CompletedTask;
    public Task PreviousAsync() => _client?.PreviousAsync() ?? Task.CompletedTask;

    /// <summary>Seeks to an absolute position in milliseconds using UPnP AVTransport.</summary>
    public async Task SeekAsync(long ms)
    {
        if (_upnp is null || ms < 0) return;
        await _upnp.SeekAsync(ms).ConfigureAwait(false);
    }

    // â”€â”€ Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) ? v.ToString() : "";

    private static void Ui(Action action)
    {
        var app = Application.Current;
        if (app is null) return;
        if (app.Dispatcher.CheckAccess()) action();
        else app.Dispatcher.Invoke(action);
    }

    public async ValueTask DisposeAsync() => await DisconnectAsync().ConfigureAwait(false);
}

