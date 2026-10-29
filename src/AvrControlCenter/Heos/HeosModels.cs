// AVR Control Center
// Copyright (c) 2026 Sergio (@sreddev)
// Licensed under MIT License
// https://github.com/sreddev/AVR-control-center

using System.Collections.Generic;

namespace AvrControlCenter.Heos;

/// <summary>A HEOS music source: streaming service, local server or input.</summary>
public sealed record MusicSource(
    string Name,
    string Sid,
    string ImageUrl,
    string Type,
    bool Available,
    string Username);

/// <summary>Fixed source ids from the HEOS CLI specification.</summary>
public static class HeosSid
{
    public const string TuneIn = "3";
    public const string Favorites = "1028";
}

/// <summary>
/// One row in a browse listing. Can be a container the user drills into
/// (album, playlist, genre) or a playable item (track, station).
/// <paramref name="Position"/> is the 1-based index in the listing; HEOS
/// Favorites are played by this number (play_preset).
/// </summary>
public sealed record BrowseItem(
    string Name,
    string Sid,
    string Cid,
    string Mid,
    string ImageUrl,
    string Type,
    bool IsContainer,
    bool IsPlayable,
    int Position = 0)
{
    public bool IsStation => Type == "station";
    public bool IsHeosFavorite => Sid == HeosSid.Favorites && !IsContainer;

    /// <summary>Stations can't be queued; HEOS only queues tracks and containers.</summary>
    public bool CanQueue => IsPlayable && !IsStation;

    /// <summary>The receiver offers "Add to HEOS Favorites" for stations outside the favorites list.</summary>
    public bool CanAddToFavorites => IsStation && !IsHeosFavorite && !string.IsNullOrEmpty(Mid);

    /// <summary>Secondary line shown under the name in the list.</summary>
    public string Subtitle => Type switch
    {
        "song"      => "Song",
        "album"     => "Album",
        "artist"    => "Artist",
        "playlist"  => "Playlist",
        "station"   => "Station",
        "container" => "Folder",
        "heos_service" => "Service",
        _ => Type
    };
}

/// <summary>An entry in the playback queue.</summary>
public sealed record QueueItem(
    string QueueId,
    string Title,
    string Artist,
    string Album,
    string ImageUrl);

/// <summary>A way to search a service, e.g. "Artist" with scid 1.</summary>
public sealed record SearchCriteria(string Name, string Scid);

/// <summary>One level in the browse trail, used for the back navigation.</summary>
public sealed record BrowseCrumb(string Label, string Sid, string? Cid);
