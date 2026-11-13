// AVR Control Center
// Copyright (c) 2026 Sergio (@sreddev)
// Licensed under MIT License
// https://github.com/sreddev/AVR-control-center

using System;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace AvrControlCenter.Services;

/// <summary>
/// Lightweight auto-update. On startup, the app asks the GitHub Releases API
/// for the latest release of <see cref="RepoSlug"/>. If its tag (e.g. "v1.6.5")
/// is newer than the running build, the Settings page shows a banner whose
/// button downloads the release's Setup.exe (or opens the release page if there
/// is none). An MSIX/Store install never checks: Windows updates the package.
///
/// The app's current version is read from the assembly, which gets it from
/// &lt;Version&gt; in AvrControlCenter.csproj. Tag each GitHub release "v" + that value.
/// </summary>
public sealed class AutoUpdateService
{
    public const string RepoSlug = "sreddev/AVR-control-center";
    public const string RepoUrl = "https://github.com/" + RepoSlug;
    public const string ReleasesUrl = RepoUrl + "/releases";

    private const string LatestReleaseApi = "https://api.github.com/repos/" + RepoSlug + "/releases/latest";

    private static readonly HttpClient Http = CreateClient();

    public string CurrentVersion { get; } = GetCurrentVersion();

    /// <summary>
    /// True when running from an MSIX package (Microsoft Store or sideloaded).
    /// Packaged apps are updated by Windows, so the app must not offer its own updates.
    /// </summary>
    public static bool IsPackaged { get; } = DetectPackaged();

    private static bool DetectPackaged()
    {
        try
        {
            uint length = 0;
            // ERROR_INSUFFICIENT_BUFFER (122) means there is a package identity;
            // APPMODEL_ERROR_NO_PACKAGE (15700) means a plain desktop install.
            return GetCurrentPackageFullName(ref length, null) == 122;
        }
        catch { return false; }   // API missing on very old Windows
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref uint packageFullNameLength, char[]? packageFullName);

    /// <summary>"1.6.5" style version of the running build.</summary>
    public static string GetCurrentVersion() =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

    /// <summary>
    /// Checks the latest GitHub release. Returns its details if it is newer
    /// than the running build, null otherwise (including on any error).
    /// </summary>
    public async Task<UpdateInfo?> CheckAsync()
    {
        if (IsPackaged) return null;
        try
        {
            var json = await Http.GetStringAsync(LatestReleaseApi).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tag = root.GetProperty("tag_name").GetString() ?? "";
            var remoteVersion = tag.TrimStart('v', 'V');
            if (!IsNewer(remoteVersion, CurrentVersion))
                return null;

            var url = root.TryGetProperty("html_url", out var h) ? h.GetString() ?? ReleasesUrl : ReleasesUrl;
            if (root.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString() ?? "";
                    if (name.EndsWith("-Setup.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        url = asset.GetProperty("browser_download_url").GetString() ?? url;
                        break;
                    }
                }
            }

            // The release title ("v1.6.1 — Army Green") reads better in the
            // one-line banner than the full markdown body.
            var notes = root.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";

            return new UpdateInfo(remoteVersion, url, notes);
        }
        catch
        {
            return null; // silent on error (offline, rate-limited, no releases yet)
        }
    }

    /// <summary>Opens the download URL in the browser.</summary>
    public void ApplyUpdate(UpdateInfo info)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = info.Url,
                UseShellExecute = true
            });
        }
        catch { /* best effort */ }
    }

    private static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        // GitHub's API rejects requests without a User-Agent.
        http.DefaultRequestHeaders.UserAgent.ParseAdd("AVRControlCenter/" + GetCurrentVersion());
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return http;
    }

    internal static bool IsNewer(string remote, string local)
    {
        if (Version.TryParse(remote, out var r) && Version.TryParse(local, out var l))
            return r > l;
        return false;
    }
}

public sealed record UpdateInfo(string Version, string Url, string Notes);
