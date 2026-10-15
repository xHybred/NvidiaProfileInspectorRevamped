namespace nvidiaProfileInspector.Services
{
    using Microsoft.Win32;
    using nvidiaProfileInspector.Common;
    using nvidiaProfileInspector.Common.Helper;
    using nvidiaProfileInspector.Native.WINAPI;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Drawing;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Interop;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;

    public sealed class InstalledGameInfo
    {
        public string DisplayName { get; set; }
        public string ExeName { get; set; }
        public string ExePath { get; set; }
        public string Source { get; set; }
        public string ProfileName { get; set; }
        public List<string> ProfileNames { get; set; } = new List<string>();
        public int ProfileCount { get; set; }
        public bool IsUserProfile { get; set; }
    }

    public class InstalledGamesService
    {
        private readonly DrsScannerService _scannerService;
        private readonly object _cacheSync = new object();
        private List<InstalledGameInfo> _cachedResults;

        private static readonly ConcurrentDictionary<string, ImageSource> _iconCache =
            new ConcurrentDictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);

        private static readonly string[] ExcludedExeFragments = new[]
        {
            "unins", "uninstall", "setup", "redist", "crash", "report",
            "easyanticheat", "battleye", "cefprocess", "unitycrashhandler"
        };

        public InstalledGamesService(DrsScannerService scannerService)
        {
            _scannerService = scannerService;
        }

        public bool HasCachedResults
        {
            get { lock (_cacheSync) { return _cachedResults != null; } }
        }

        public List<InstalledGameInfo> GetCachedResults()
        {
            lock (_cacheSync) { return _cachedResults == null ? new List<InstalledGameInfo>() : new List<InstalledGameInfo>(_cachedResults); }
        }

        public void ClearCache()
        {
            lock (_cacheSync) { _cachedResults = null; }
        }

        private static string GetPersistedFilename()
        {
            try { return Path.Combine(UserSettings.GetProductDataFolder(), "installed-games.xml"); }
            catch { return null; }
        }

        public bool TryRestorePersistedResults()
        {
            lock (_cacheSync)
            {
                if (_cachedResults != null)
                    return _cachedResults.Count > 0;
            }

            try
            {
                var path = GetPersistedFilename();
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    return false;

                var stored = XMLHelper<List<InstalledGameInfo>>.DeserializeFromXMLFile(path);
                if (stored == null)
                    return false;

                var valid = stored
                    .Where(g => g != null && !string.IsNullOrEmpty(g.ExePath) && File.Exists(g.ExePath) && !string.IsNullOrEmpty(g.ProfileName))
                    .ToList();

                lock (_cacheSync) { _cachedResults = valid; }
                return valid.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        private void SavePersistedResults()
        {
            try
            {
                var path = GetPersistedFilename();
                if (string.IsNullOrEmpty(path))
                    return;

                List<InstalledGameInfo> snapshot;
                lock (_cacheSync) { snapshot = _cachedResults == null ? new List<InstalledGameInfo>() : new List<InstalledGameInfo>(_cachedResults); }
                XMLHelper<List<InstalledGameInfo>>.SerializeToXmlFile(snapshot, path, Encoding.Unicode, true);
            }
            catch { }
        }

        public ImageSource GetIconForProfile(string profileName)
        {
            if (string.IsNullOrEmpty(profileName))
                return null;

            InstalledGameInfo game = null;
            lock (_cacheSync)
            {
                if (_cachedResults != null)
                {
                    game = _cachedResults.FirstOrDefault(g =>
                        string.Equals(g.ProfileName, profileName, StringComparison.OrdinalIgnoreCase) ||
                        (g.ProfileNames != null && g.ProfileNames.Any(p =>
                            string.Equals(p, profileName, StringComparison.OrdinalIgnoreCase))));
                }
            }

            if (game == null)
                return null;
            return GetIcon(game.ExePath);
        }

        public void HideGame(string exeName)
        {
            if (string.IsNullOrWhiteSpace(exeName))
                return;

            try
            {
                var settings = UserSettings.LoadSettings();
                if (settings.HiddenGameExes == null)
                    settings.HiddenGameExes = new List<string>();
                var key = exeName.Trim().ToLowerInvariant();
                if (!settings.HiddenGameExes.Any(x => string.Equals(x, key, StringComparison.OrdinalIgnoreCase)))
                {
                    settings.HiddenGameExes.Add(key);
                    settings.SaveSettings();
                }
            }
            catch { }

            lock (_cacheSync)
            {
                if (_cachedResults != null)
                    _cachedResults.RemoveAll(g => string.Equals(g.ExeName, exeName.Trim(), StringComparison.OrdinalIgnoreCase));
            }
            SavePersistedResults();
        }

        public async Task<List<InstalledGameInfo>> ScanAsync(IProgress<string> progress, CancellationToken token)
        {
            var found = await Task.Run(() => CollectCandidates(progress, token), token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();

            var byExe = found
                .GroupBy(c => c.ExeName.ToLowerInvariant())
                .Select(g => g.First())
                .Take(400)
                .ToList();

            progress?.Report("Matching driver profiles...");
            var matches = await Task.Run(() => _scannerService.FindProfilesUsingApplications(byExe.Select(c => c.ExeName)), token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();

            var hidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var savedHidden = UserSettings.LoadSettings()?.HiddenGameExes;
                if (savedHidden != null)
                    foreach (var h in savedHidden)
                        if (!string.IsNullOrWhiteSpace(h))
                            hidden.Add(h.Trim());
            }
            catch { }

            var results = new List<InstalledGameInfo>();
            foreach (var candidate in byExe)
            {
                token.ThrowIfCancellationRequested();
                if (!matches.TryGetValue(candidate.ExeName.ToLowerInvariant(), out var profiles) || profiles.Count == 0)
                    continue;
                if (hidden.Contains(candidate.ExeName))
                    continue;

                var profile = profiles[0];
                results.Add(new InstalledGameInfo
                {
                    DisplayName = candidate.DisplayName,
                    ExeName = candidate.ExeName,
                    ExePath = candidate.ExePath,
                    Source = candidate.Source,
                    ProfileName = profile,
                    ProfileNames = new List<string>(profiles),
                    ProfileCount = profiles.Count,
                    IsUserProfile = _scannerService.UserProfiles.Contains(profile)
                });
            }

            results.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));

            progress?.Report("Loading icons...");
            await Task.Run(() =>
            {
                foreach (var game in results)
                {
                    token.ThrowIfCancellationRequested();
                    GetIcon(game.ExePath);
                }
            }, token).ConfigureAwait(false);

            lock (_cacheSync) { _cachedResults = results; }
            SavePersistedResults();
            return new List<InstalledGameInfo>(results);
        }

        public static ImageSource GetIcon(string exePath)
        {
            if (string.IsNullOrEmpty(exePath))
                return null;

            if (_iconCache.TryGetValue(exePath, out var cached))
                return cached;

            try
            {
                if (!File.Exists(exePath))
                    return null;

                using (var icon = Icon.ExtractAssociatedIcon(exePath))
                {
                    if (icon == null)
                        return null;

                    var bitmap = Imaging.CreateBitmapSourceFromHIcon(
                        icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(48, 48));
                    bitmap.Freeze();
                    _iconCache[exePath] = bitmap;
                    return bitmap;
                }
            }
            catch
            {
                return null;
            }
        }

        private sealed class Candidate
        {
            public string DisplayName;
            public string ExeName;
            public string ExePath;
            public string Source;
        }

        private List<Candidate> CollectCandidates(IProgress<string> progress, CancellationToken token)
        {
            var list = new List<Candidate>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            Action<Candidate> add = c =>
            {
                if (c == null || string.IsNullOrEmpty(c.ExePath) || string.IsNullOrEmpty(c.ExeName))
                    return;
                if (!seen.Add(c.ExeName.ToLowerInvariant()))
                    return;
                if (list.Count < 400)
                    list.Add(c);
            };

            var sources = new[]
            {
                Tuple.Create("Scanning Steam libraries...", (Func<CancellationToken, IEnumerable<Candidate>>)ScanSteam),
                Tuple.Create("Scanning Epic library...", (Func<CancellationToken, IEnumerable<Candidate>>)ScanEpic),
                Tuple.Create("Scanning GOG library...", (Func<CancellationToken, IEnumerable<Candidate>>)ScanGog),
                Tuple.Create("Scanning EA library...", (Func<CancellationToken, IEnumerable<Candidate>>)ScanEa),
                Tuple.Create("Scanning Ubisoft library...", (Func<CancellationToken, IEnumerable<Candidate>>)ScanUbisoft),
                Tuple.Create("Scanning Start Menu...", (Func<CancellationToken, IEnumerable<Candidate>>)ScanStartMenu),
            };

            foreach (var source in sources)
            {
                progress?.Report(source.Item1);
                foreach (var c in source.Item2(token))
                {
                    token.ThrowIfCancellationRequested();
                    add(c);
                }
            }

            return list;
        }

        private static bool IsExcludedExeName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return true;
            var lower = fileName.ToLowerInvariant();
            return ExcludedExeFragments.Any(lower.Contains);
        }

        private static string PrettifyExeName(string fileName)
        {
            try { return Path.GetFileNameWithoutExtension(fileName); }
            catch { return fileName; }
        }

        #region Steam

        private IEnumerable<Candidate> ScanSteam(CancellationToken token)
        {
            var steamPath = GetSteamPath();
            if (string.IsNullOrEmpty(steamPath))
                yield break;

            foreach (var library in GetSteamLibraries(steamPath))
            {
                token.ThrowIfCancellationRequested();
                string steamapps;
                try { steamapps = Path.Combine(library, "steamapps"); }
                catch { continue; }
                if (!Directory.Exists(steamapps))
                    continue;

                string[] manifests;
                try { manifests = Directory.GetFiles(steamapps, "appmanifest_*.acf", SearchOption.TopDirectoryOnly); }
                catch { continue; }

                foreach (var manifest in manifests)
                {
                    token.ThrowIfCancellationRequested();
                    Candidate candidate = null;
                    try { candidate = ScanSteamManifest(library, manifest); }
                    catch { candidate = null; }
                    if (candidate != null)
                        yield return candidate;
                }
            }
        }

        private Candidate ScanSteamManifest(string libraryPath, string manifestPath)
        {
            string content;
            try { content = File.ReadAllText(manifestPath); }
            catch { return null; }

            var name = ExtractVdfValue(content, "name");
            var installDir = ExtractVdfValue(content, "installdir");
            if (string.IsNullOrEmpty(installDir))
                return null;

            string dir;
            try { dir = Path.Combine(libraryPath, "steamapps", "common", installDir); }
            catch { return null; }
            if (!Directory.Exists(dir))
                return null;

            var exe = PickBestExe(dir);
            if (string.IsNullOrEmpty(exe))
                return null;

            return new Candidate
            {
                DisplayName = string.IsNullOrEmpty(name) ? PrettifyExeName(exe) : name,
                ExeName = Path.GetFileName(exe),
                ExePath = exe,
                Source = "Steam"
            };
        }

        private static string PickBestExe(string installDir)
        {
            List<string> files;
            try
            {
                files = Directory.EnumerateFiles(installDir, "*.exe", SearchOption.AllDirectories)
                    .Take(500)
                    .Where(p => !IsExcludedExeName(Path.GetFileName(p)))
                    .ToList();
            }
            catch { return null; }

            if (files.Count == 0)
                return null;

            string folderName = "";
            try { folderName = new DirectoryInfo(installDir).Name.ToLowerInvariant(); }
            catch { }

            return files
                .OrderBy(p => ScoreExe(p, installDir, folderName))
                .ThenBy(p => p.Length)
                .FirstOrDefault();
        }

        private static int ScoreExe(string path, string installDir, string folderName)
        {
            var fileName = (Path.GetFileNameWithoutExtension(path) ?? "").ToLowerInvariant();
            int depth = 0;
            try
            {
                var relative = path.Substring(installDir.Length).TrimStart(Path.DirectorySeparatorChar);
                depth = relative.Count(ch => ch == Path.DirectorySeparatorChar);
            }
            catch { }

            var score = depth * 10;
            if (!string.IsNullOrEmpty(folderName))
            {
                if (fileName == folderName) score -= 50;
                else if (fileName.Contains(folderName)) score -= 20;
            }
            if (fileName.Contains("launcher")) score += 20;
            return score;
        }

        private static string GetSteamPath()
        {
            var candidates = new List<string>();
            try
            {
                foreach (var entry in new[]
                {
                    Tuple.Create(Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath"),
                    Tuple.Create(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath"),
                    Tuple.Create(Registry.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath"),
                })
                {
                    try
                    {
                        using (var key = entry.Item1.OpenSubKey(entry.Item2, false))
                        {
                            var value = key?.GetValue(entry.Item3, null) as string;
                            if (!string.IsNullOrEmpty(value))
                            {
                                if (value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                                    value = Path.GetDirectoryName(value);
                                if (!string.IsNullOrEmpty(value))
                                    candidates.Add(value);
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }

            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam"));

            foreach (var candidate in candidates)
            {
                try { if (!string.IsNullOrEmpty(candidate) && Directory.Exists(candidate)) return candidate; }
                catch { }
            }
            return string.Empty;
        }

        private static IEnumerable<string> GetSteamLibraries(string steamPath)
        {
            var collected = new List<string> { steamPath };
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { steamPath };
            string foldersPath = null;
            try { foldersPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf"); }
            catch { foldersPath = null; }

            if (foldersPath != null && File.Exists(foldersPath))
            {
                string content = null;
                try { content = File.ReadAllText(foldersPath); }
                catch { content = null; }

                if (content != null)
                {
                    foreach (Match m in Regex.Matches(content, "\"path\"\\s*\"(?<path>[^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                    {
                        var path = m.Groups["path"].Value.Replace(@"\\", @"\");
                        try
                        {
                            if (!string.IsNullOrEmpty(path) && Directory.Exists(path) && seen.Add(path))
                                collected.Add(path);
                        }
                        catch { }
                    }
                }
            }

            foreach (var library in collected)
                yield return library;
        }

        private static string ExtractVdfValue(string content, string key)
        {
            if (string.IsNullOrEmpty(content))
                return string.Empty;
            var match = Regex.Match(content, "\"" + Regex.Escape(key) + "\"\\s*\"(?<value>[^\"]*)\"",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return match.Success ? match.Groups["value"].Value : string.Empty;
        }

        #endregion

        #region Epic

        private IEnumerable<Candidate> ScanEpic(CancellationToken token)
        {
            var dirs = new List<string>();
            try
            {
                var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                if (!string.IsNullOrEmpty(programData))
                    dirs.Add(Path.Combine(programData, "Epic", "EpicGamesLauncher", "Data", "Manifests"));
            }
            catch { }

            foreach (var dir in dirs.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!Directory.Exists(dir))
                    continue;

                string[] files;
                try { files = Directory.GetFiles(dir, "*.item", SearchOption.TopDirectoryOnly); }
                catch { continue; }

                foreach (var file in files)
                {
                    token.ThrowIfCancellationRequested();
                    Candidate candidate = null;
                    try { candidate = ScanEpicManifest(file); }
                    catch { candidate = null; }
                    if (candidate != null)
                        yield return candidate;
                }
            }
        }

        private Candidate ScanEpicManifest(string manifestPath)
        {
            string content;
            try { content = File.ReadAllText(manifestPath); }
            catch { return null; }

            var launchExe = ExtractJsonValue(content, "LaunchExecutable");
            var installLocation = ExtractJsonValue(content, "InstallLocation");
            var displayName = ExtractJsonValue(content, "DisplayName");
            if (string.IsNullOrEmpty(displayName))
                displayName = ExtractJsonValue(content, "AppName");
            if (string.IsNullOrEmpty(launchExe))
                return null;

            var normalized = launchExe.Replace('/', '\\');
            string fullPath = normalized;
            if (!Path.IsPathRooted(normalized) && !string.IsNullOrEmpty(installLocation))
            {
                try { fullPath = Path.Combine(installLocation, normalized); }
                catch { return null; }
            }

            if (!File.Exists(fullPath))
            {
                // Fall back to locating the launch exe inside the install folder.
                if (string.IsNullOrEmpty(installLocation) || !Directory.Exists(installLocation))
                    return null;
                try
                {
                    fullPath = Directory.EnumerateFiles(installLocation, Path.GetFileName(normalized), SearchOption.AllDirectories).FirstOrDefault();
                }
                catch { return null; }
                if (string.IsNullOrEmpty(fullPath))
                    return null;
            }

            var exeName = Path.GetFileName(fullPath);
            if (IsExcludedExeName(exeName))
                return null;

            return new Candidate
            {
                DisplayName = string.IsNullOrEmpty(displayName) ? PrettifyExeName(exeName) : displayName,
                ExeName = exeName,
                ExePath = fullPath,
                Source = "Epic"
            };
        }

        private static string ExtractJsonValue(string content, string propertyName)
        {
            if (string.IsNullOrEmpty(content))
                return string.Empty;
            var match = Regex.Match(content, "\"" + Regex.Escape(propertyName) + "\"\\s*:\\s*\"(?<value>(?:\\\\.|[^\"])*)\"",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return match.Success ? match.Groups["value"].Value : string.Empty;
        }

        #endregion

        #region GOG / EA / Ubisoft


        private IEnumerable<Candidate> ScanGog(CancellationToken token)
        {
            var roots = new[]
            {
                Tuple.Create(Registry.LocalMachine, @"SOFTWARE\GOG.com\Games"),
                Tuple.Create(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\GOG.com\Games"),
            };

            foreach (var root in roots)
            {
                RegistryKey key = null;
                try { key = root.Item1.OpenSubKey(root.Item2, false); }
                catch { continue; }
                if (key == null)
                    continue;

                string[] gameKeys;
                try { gameKeys = key.GetSubKeyNames(); }
                catch { try { key.Close(); } catch { } continue; }

                foreach (var gameKey in gameKeys)
                {
                    token.ThrowIfCancellationRequested();
                    Candidate candidate = null;
                    try
                    {
                        using (var game = key.OpenSubKey(gameKey, false))
                            candidate = ScanLauncherGameKey(game, "GOG", new[] { "gameName", "name", "title" });
                    }
                    catch { candidate = null; }
                    if (candidate != null)
                        yield return candidate;
                }

                try { key.Close(); } catch { }
            }
        }

        private IEnumerable<Candidate> ScanEa(CancellationToken token)
        {
            var roots = new[]
            {
                Tuple.Create(Registry.LocalMachine, @"SOFTWARE\EA Games"),
                Tuple.Create(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\EA Games"),
            };

            foreach (var root in roots)
            {
                RegistryKey key = null;
                try { key = root.Item1.OpenSubKey(root.Item2, false); }
                catch { continue; }
                if (key == null)
                    continue;

                string[] gameKeys;
                try { gameKeys = key.GetSubKeyNames(); }
                catch { try { key.Close(); } catch { } continue; }

                foreach (var gameKey in gameKeys)
                {
                    token.ThrowIfCancellationRequested();
                    Candidate candidate = null;
                    try
                    {
                        using (var game = key.OpenSubKey(gameKey, false))
                            candidate = ScanLauncherGameKey(game, "EA", new[] { "DisplayName", "gameName", "name", "title" });
                    }
                    catch { candidate = null; }
                    if (candidate != null)
                        yield return candidate;
                }

                try { key.Close(); } catch { }
            }

            foreach (var folder in new[]
            {
                @"C:\Program Files\EA Games",
                @"C:\Program Files (x86)\EA Games",
            })
            {
                if (!Directory.Exists(folder))
                    continue;

                string[] dirs;
                try { dirs = Directory.GetDirectories(folder); }
                catch { continue; }

                foreach (var dir in dirs)
                {
                    token.ThrowIfCancellationRequested();
                    string exe = null;
                    try { exe = PickBestExe(dir); }
                    catch { exe = null; }
                    if (string.IsNullOrEmpty(exe))
                        continue;

                    string display = "";
                    try { display = new DirectoryInfo(dir).Name; }
                    catch { display = PrettifyExeName(exe); }

                    yield return new Candidate
                    {
                        DisplayName = display,
                        ExeName = Path.GetFileName(exe),
                        ExePath = exe,
                        Source = "EA"
                    };
                }
            }
        }

        private IEnumerable<Candidate> ScanUbisoft(CancellationToken token)
        {
            var roots = new[]
            {
                Tuple.Create(Registry.LocalMachine, @"SOFTWARE\Ubisoft\Launcher\Installs"),
                Tuple.Create(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs"),
            };

            foreach (var root in roots)
            {
                RegistryKey key = null;
                try { key = root.Item1.OpenSubKey(root.Item2, false); }
                catch { continue; }
                if (key == null)
                    continue;

                string[] gameKeys;
                try { gameKeys = key.GetSubKeyNames(); }
                catch { try { key.Close(); } catch { } continue; }

                foreach (var gameKey in gameKeys)
                {
                    token.ThrowIfCancellationRequested();
                    Candidate candidate = null;
                    try
                    {
                        using (var game = key.OpenSubKey(gameKey, false))
                            candidate = ScanLauncherGameKey(game, "Ubisoft", new[] { "DisplayName", "name", "title" });
                    }
                    catch { candidate = null; }
                    if (candidate != null)
                        yield return candidate;
                }

                try { key.Close(); } catch { }
            }
        }

        /// <summary>Tolerant per-game parsing for GOG/EA/Ubisoft registry keys:
        /// tries several display-name and exe/path value shapes, falls back to
        /// picking the best exe inside a discovered install folder.</summary>
        private Candidate ScanLauncherGameKey(RegistryKey gameKey, string source, string[] displayKeys)
        {
            if (gameKey == null)
                return null;

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var valueName in gameKey.GetValueNames())
                {
                    string text = null;
                    try { text = gameKey.GetValue(valueName, null) as string; }
                    catch { continue; }
                    if (!string.IsNullOrEmpty(text))
                        values[valueName] = text;
                }
            }
            catch { return null; }

            if (values.Count == 0)
                return null;

            string display = "";
            foreach (var displayKey in displayKeys)
            {
                string found;
                if (values.TryGetValue(displayKey, out found) && !string.IsNullOrWhiteSpace(found))
                {
                    display = found.Trim();
                    break;
                }
            }

            string baseDir = "";
            foreach (var dirKey in new[] { "path", "installdir", "installationpath", "gamedir", "root", "installlocation", "Install Dir" })
            {
                string found;
                if (values.TryGetValue(dirKey, out found) && !string.IsNullOrWhiteSpace(found) && Directory.Exists(found.Trim()))
                {
                    baseDir = found.Trim();
                    break;
                }
            }

            foreach (var pair in values)
            {
                var text = pair.Value.Trim(' ', '"');
                if (!text.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    continue;

                string full = text;
                if (!Path.IsPathRooted(full) && !string.IsNullOrEmpty(baseDir))
                {
                    try { full = Path.Combine(baseDir, full); }
                    catch { continue; }
                }

                try
                {
                    if (File.Exists(full))
                    {
                        var exeName = Path.GetFileName(full);
                        if (!IsExcludedExeName(exeName))
                        {
                            return new Candidate
                            {
                                DisplayName = string.IsNullOrEmpty(display) ? PrettifyExeName(exeName) : display,
                                ExeName = exeName,
                                ExePath = Path.GetFullPath(full),
                                Source = source
                            };
                        }
                    }
                }
                catch { }
            }

            if (!string.IsNullOrEmpty(baseDir))
            {
                string exe = null;
                try { exe = PickBestExe(baseDir); }
                catch { exe = null; }
                if (!string.IsNullOrEmpty(exe))
                {
                    var exeName = Path.GetFileName(exe);
                    string folderDisplay = display;
                    if (string.IsNullOrEmpty(folderDisplay))
                    {
                        try { folderDisplay = new DirectoryInfo(baseDir).Name; }
                        catch { folderDisplay = PrettifyExeName(exeName); }
                    }

                    return new Candidate
                    {
                        DisplayName = folderDisplay,
                        ExeName = exeName,
                        ExePath = exe,
                        Source = source
                    };
                }
            }

            return null;
        }

        #endregion

        #region Start Menu
        private IEnumerable<Candidate> ScanStartMenu(CancellationToken token)
        {
            var roots = new List<string>();
            try
            {
                roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs"));
                roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs"));
            }
            catch { }

            foreach (var root in roots)
            {
                if (!Directory.Exists(root))
                    continue;

                string[] links;
                try { links = Directory.GetFiles(root, "*.lnk", SearchOption.AllDirectories); }
                catch { continue; }

                foreach (var link in links)
                {
                    token.ThrowIfCancellationRequested();
                    Candidate candidate = null;
                    try { candidate = ScanShortcut(link); }
                    catch { candidate = null; }
                    if (candidate != null)
                        yield return candidate;
                }
            }
        }

        private Candidate ScanShortcut(string linkPath)
        {
            ShellLink link;
            try { link = new ShellLink(linkPath); }
            catch { return null; }

            string target;
            try { target = link.Target; }
            catch { return null; }
            if (string.IsNullOrEmpty(target) || !target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                return null;

            string fullPath;
            try { fullPath = Path.GetFullPath(target); }
            catch { return null; }
            if (!File.Exists(fullPath))
                return null;

            var exeName = Path.GetFileName(fullPath);
            if (IsExcludedExeName(exeName))
                return null;

            string displayName;
            try { displayName = Path.GetFileNameWithoutExtension(linkPath); }
            catch { displayName = PrettifyExeName(exeName); }

            return new Candidate
            {
                DisplayName = displayName,
                ExeName = exeName,
                ExePath = fullPath,
                Source = "Start Menu"
            };
        }

        #endregion
    }
}
