using System.Collections.Concurrent;
using Microsoft.Win32;

namespace SmartStart;

/// <summary>Eine Zeile im Menü.</summary>
internal sealed class Entry
{
    public Entry(string path, string displayName, bool isFolder, string? folderTarget)
    {
        Path = path;
        DisplayName = displayName;
        IsFolder = isFolder;
        FolderTarget = folderTarget;
    }

    /// <summary>Echter Pfad (z. B. auch die .lnk-Datei).</summary>
    public string Path { get; }

    public string DisplayName { get; }

    /// <summary>Ordner oder Verknüpfung auf einen Ordner → hat Untermenü.</summary>
    public bool IsFolder { get; }

    /// <summary>Ordner, dessen Inhalt als Untermenü erscheint.</summary>
    public string? FolderTarget { get; }

    /// <summary>Zusatztext in Suchergebnissen (relativer Ordner).</summary>
    public string? SubText { get; init; }

    public Bitmap? Icon { get; set; }

    public bool IconRequested { get; set; }

    public int Depth { get; init; }
}

internal enum FolderStatus
{
    Ok,
    Empty,
    NoAccess,
    NotFound,
}

internal static class FolderReader
{
    private static readonly HashSet<string> HiddenExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".lnk", ".url", ".appref-ms", ".pif", ".website",
    };

    private static readonly HashSet<string> IgnoredNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "desktop.ini", "thumbs.db", ".DS_Store",
    };

    private static readonly ConcurrentDictionary<string, (string? Target, DateTime Stamp)> LinkCache = new(StringComparer.OrdinalIgnoreCase);

    public static (List<Entry> Entries, FolderStatus Status) Read(string folder, AppSettings settings)
    {
        var result = new List<Entry>();
        if (!Directory.Exists(folder))
        {
            return (result, FolderStatus.NotFound);
        }

        var (showHidden, showSuperHidden, hideExt) = ExplorerFlags(settings);
        try
        {
            var options = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                AttributesToSkip = 0,
                RecurseSubdirectories = false,
                ReturnSpecialDirectories = false,
            };

            foreach (var info in new DirectoryInfo(folder).EnumerateFileSystemInfos("*", options))
            {
                var e = CreateEntry(info, settings, showHidden, showSuperHidden, hideExt, null, 0);
                if (e != null)
                {
                    result.Add(e);
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            return (result, FolderStatus.NoAccess);
        }
        catch (IOException)
        {
            return (result, FolderStatus.NoAccess);
        }

        Sort(result);
        return (result, result.Count == 0 ? FolderStatus.Empty : FolderStatus.Ok);
    }

    public static void Sort(List<Entry> list)
    {
        list.Sort((a, b) =>
        {
            if (a.IsFolder != b.IsFolder)
            {
                return a.IsFolder ? -1 : 1;
            }

            return Native.StrCmpLogicalW(a.DisplayName, b.DisplayName);
        });
    }

    internal static Entry? CreateEntry(
        FileSystemInfo info,
        AppSettings settings,
        bool showHidden,
        bool showSuperHidden,
        bool hideExt,
        string? subText,
        int depth)
    {
        if (IgnoredNames.Contains(info.Name))
        {
            return null;
        }

        var attr = info.Attributes;
        bool hidden = attr.HasFlag(FileAttributes.Hidden);
        bool system = attr.HasFlag(FileAttributes.System);
        if (hidden && !showHidden)
        {
            return null;
        }

        if (hidden && system && !showSuperHidden)
        {
            return null;
        }

        if (info is DirectoryInfo)
        {
            return new Entry(info.FullName, info.Name, true, info.FullName) { SubText = subText, Depth = depth };
        }

        string ext = info.Extension;
        string name = hideExt || HiddenExtensions.Contains(ext)
            ? System.IO.Path.GetFileNameWithoutExtension(info.Name)
            : info.Name;
        if (string.IsNullOrEmpty(name))
        {
            name = info.Name;
        }

        string? folderTarget = null;
        if (settings.ResolveLinksToFolders && ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            folderTarget = ResolveFolderLink(info.FullName, info.LastWriteTimeUtc);
        }

        return new Entry(info.FullName, name, folderTarget != null, folderTarget) { SubText = subText, Depth = depth };
    }

    private static string? ResolveFolderLink(string lnk, DateTime stamp)
    {
        if (LinkCache.TryGetValue(lnk, out var cached) && cached.Stamp == stamp)
        {
            return cached.Target;
        }

        string? target = ShellLink.GetTarget(lnk);
        string? folder = null;
        if (target != null)
        {
            try
            {
                if (Directory.Exists(target))
                {
                    folder = target;
                }
            }
            catch
            {
                // ignorieren
            }
        }

        LinkCache[lnk] = (folder, stamp);
        return folder;
    }

    /// <summary>(Versteckte anzeigen, Geschützte Systemdateien anzeigen, Erweiterungen ausblenden).</summary>
    public static (bool ShowHidden, bool ShowSuperHidden, bool HideExt) ExplorerFlags(AppSettings settings)
    {
        bool showHidden = false, superHidden = false, hideExt = true;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
            if (key != null)
            {
                showHidden = key.GetValue("Hidden") is int h && h == 1;
                superHidden = key.GetValue("ShowSuperHidden") is int s && s == 1;
                hideExt = key.GetValue("HideFileExt") is not int e || e == 1;
            }
        }
        catch
        {
            // Standardwerte
        }

        switch (settings.HiddenFiles)
        {
            case HiddenFilesMode.Always:
                showHidden = true;
                superHidden = true;
                break;
            case HiddenFilesMode.Never:
                showHidden = false;
                superHidden = false;
                break;
        }

        return (showHidden, superHidden, hideExt);
    }
}

/// <summary>Rekursiver Suchindex eines Ordners.</summary>
internal sealed class FolderIndex
{
    private const int MaxItems = 30000;
    private static readonly ConcurrentDictionary<string, FolderIndex> Cache = new(StringComparer.OrdinalIgnoreCase);

    private readonly string root;
    private List<Entry>? items;
    private Task? building;
    private DateTime builtAt;

    private FolderIndex(string root)
    {
        this.root = root;
    }

    public bool IsReady => items != null;

    public static FolderIndex Get(string folder) => Cache.GetOrAdd(folder, f => new FolderIndex(f));

    /// <summary>Startet (Neu-)Aufbau im Hintergrund, falls nötig. Alte Ergebnisse bleiben bis dahin nutzbar.</summary>
    public Task EnsureBuilt(AppSettings settings, bool forceRefresh = false)
    {
        lock (this)
        {
            if (building != null && !building.IsCompleted)
            {
                return building;
            }

            if (items != null && !forceRefresh && DateTime.UtcNow - builtAt < TimeSpan.FromSeconds(30))
            {
                return Task.CompletedTask;
            }

            building = Task.Run(() =>
            {
                var list = Build(settings);
                items = list;
                builtAt = DateTime.UtcNow;
            });
            return building;
        }
    }

    private List<Entry> Build(AppSettings settings)
    {
        var list = new List<Entry>();
        var (showHidden, showSuperHidden, hideExt) = FolderReader.ExplorerFlags(settings);
        var queue = new Queue<(string Dir, int Depth)>();
        queue.Enqueue((root, 0));
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (queue.Count > 0 && list.Count < MaxItems)
        {
            var (dir, depth) = queue.Dequeue();
            if (!visited.Add(dir))
            {
                continue;
            }

            string rel = Path.GetRelativePath(root, dir);
            string? sub = rel == "." ? null : rel;
            IEnumerable<FileSystemInfo> infos;
            try
            {
                infos = new DirectoryInfo(dir).EnumerateFileSystemInfos("*", new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    AttributesToSkip = 0,
                    ReturnSpecialDirectories = false,
                }).ToList();
            }
            catch
            {
                continue;
            }

            foreach (var info in infos)
            {
                var e = FolderReader.CreateEntry(info, settings, showHidden, showSuperHidden, hideExt, sub, depth);
                if (e == null)
                {
                    continue;
                }

                list.Add(e);
                bool isReparse = info.Attributes.HasFlag(FileAttributes.ReparsePoint);
                if (info is DirectoryInfo && !isReparse && depth < 12)
                {
                    queue.Enqueue((info.FullName, depth + 1));
                }
            }
        }

        return list;
    }

    private static string Compact(string s) =>
        s.Replace(" ", string.Empty).Replace("-", string.Empty).Replace("_", string.Empty).Replace(".", string.Empty);

    public List<Entry> Search(string query, int max = 150)
    {
        var snapshot = items;
        if (snapshot == null)
        {
            return new List<Entry>();
        }

        string[] tokens = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            return new List<Entry>();
        }

        string q = query.Trim();
        string compactQuery = Compact(q);
        var hits = new List<(Entry E, int Score)>();
        foreach (var e in snapshot)
        {
            bool all = true;
            foreach (string t in tokens)
            {
                if (e.DisplayName.IndexOf(t, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    all = false;
                    break;
                }
            }

            // Leerzeichen-tolerant: "3dsmax" findet "3ds Max", "3ds max" findet "3dsmax"
            if (!all && !Compact(e.DisplayName).Contains(compactQuery, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            int score = e.Depth * 10;
            if (e.DisplayName.Equals(q, StringComparison.OrdinalIgnoreCase))
            {
                score -= 1000;
            }
            else if (e.DisplayName.StartsWith(q, StringComparison.OrdinalIgnoreCase))
            {
                score -= 500;
            }
            else if (e.DisplayName.Contains(" " + tokens[0], StringComparison.OrdinalIgnoreCase))
            {
                score -= 200;
            }

            hits.Add((e, score));
        }

        return hits
            .OrderBy(h => h.Score)
            .ThenBy(h => h.E.IsFolder ? 1 : 0)
            .ThenBy(h => h.E.DisplayName, Comparer<string>.Create(Native.StrCmpLogicalW))
            .Take(max)
            .Select(h => new Entry(h.E.Path, h.E.DisplayName, h.E.IsFolder, h.E.FolderTarget) { SubText = h.E.SubText, Depth = h.E.Depth })
            .ToList();
    }
}
