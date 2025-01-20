using Community.PowerToys.Run.Plugin.SteamRun.SteamAppInfo;
using ManagedCommon;
using Microsoft.Win32;
using System.ComponentModel;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Windows.Forms.Integration;
using ValveKeyValue;
using Wox.Plugin.Logger;

namespace Community.PowerToys.Run.Plugin.SteamRun
{
    internal static class SteamFinder
    {
        /// <summary>
        /// Finds the Steam installation directory.
        /// </summary>
        /// <returns>The Steam installation directory</returns>
        /// <exception cref="DirectoryNotFoundException">Thrown when the Steam installation directory is not found.</exception>
        /// <exception cref="SecurityException">Thrown when the user does not have the permissions required to read from the registry key.</exception>
        public static string FindInstallDir()
        {
            string? steamInstallDir = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;
            if (steamInstallDir != null)
            {
                return steamInstallDir.Replace("/", "\\");
            }
            steamInstallDir = Registry.GetValue(@"HKEY_LOCAL_MACHINE\Software\Valve\Steam", "InstallPath", null) as string;
            if (steamInstallDir != null)
            {
                return steamInstallDir.Replace("/", "\\");
            }
            throw new DirectoryNotFoundException("Steam installation directory not found.");
        }

        private static byte[]? libraryFoldersHash = null;
        private static List<SteamLibraryFolder>? previousLibraryFolders = null;
        /// <summary>
        /// Finds the Steam library folders.
        /// </summary>
        /// <param name="steamInstallDir">The Steam installation directory></param>
        /// <returns>List of Library Folders</returns>
        public static List<SteamLibraryFolder> FindLibraryFolders(string steamInstallDir)
        {
            var libraryFolders = new List<SteamLibraryFolder>();
            var libraryFoldersVDF = Path.Join(steamInstallDir, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(libraryFoldersVDF)) return [];

            var hash = SHA1.Create().ComputeHash(File.OpenRead(libraryFoldersVDF));
            if (libraryFoldersHash != null && hash.SequenceEqual(libraryFoldersHash) && previousLibraryFolders != null) return previousLibraryFolders;
            else libraryFoldersHash = hash;

            var kv = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
            KVObject data = kv.Deserialize(File.OpenRead(libraryFoldersVDF));
            foreach (var child in data.Children)
            {
                var timeLastUpdateVerified = child.Any(x => x.Name == "time_last_update_verified") ? (double)child["time_last_update_verified"] : 0.0;
                if (int.TryParse(child.Name, out int _))
                {
                    libraryFolders.Add(new SteamLibraryFolder(
                        (string?)child["path"],
                        (string?)child["label"],
                        (long?)child["contentid"],
                        (long?)child["totalsize"],
                        (long?)child["update_clean_bytes_tally"],
                        DateTime.UnixEpoch.AddSeconds(timeLastUpdateVerified),
                        child.Children.First(obj => obj.Name == "apps").Children.ToDictionary(x => int.Parse(x.Name), x => (long)x.Value)
                    ));
                }
            }
            previousLibraryFolders = libraryFolders;
            return libraryFolders;
        }

        /// <summary>
        /// Finds Steam apps in the given library folder.
        /// </summary>
        /// <param name="libraryFolder">The Steam library folder to search in</param>
        /// <returns>List of SteamApps</returns>
        public static List<SteamLibraryApp> FindApps(SteamLibraryFolder libraryFolder)
        {
            var apps = new List<SteamLibraryApp>();
            foreach (var appId in libraryFolder.Apps.Keys)
            {
                var appManifest = Path.Join(libraryFolder.Path, "steamapps", $"appmanifest_{appId}.acf");
                if (!File.Exists(appManifest)) continue;

                var kv = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
                KVObject data = kv.Deserialize(File.OpenRead(appManifest));

                var lastPlayed = data.Any(x => x.Name == "LastPlayed") ? (double)data["LastPlayed"] : 0.0;
                apps.Add(new SteamLibraryApp(
                    appId,
                    (string)data["name"] ?? "Unknown",
                    (string?)data["installdir"],
                    libraryFolder,
                    DateTime.UnixEpoch.AddSeconds(lastPlayed)
                ));
            }
            return apps;
        }

        private static byte[]? appLogosHash = null;
        private static Dictionary<int, string>? previousAppLogos = null;
        /// <summary>
        /// Finds logos for all steam apps.
        /// </summary>
        /// <param name="steamInstallDir">The Steam installation directory></param>
        /// <returns>Dictionary that maps app ID to app logo path</returns>
        public static Dictionary<int, string> FindAppLogos(string steamInstallDir)
        {
            var appLogos = new Dictionary<int, string>();
            var libraryCache = Path.Join(steamInstallDir, "appcache", "librarycache");
            var libraryCacheAssetsVDF = Path.Join(libraryCache, "assets.vdf");
            if (!File.Exists(libraryCacheAssetsVDF))
            {
                // Valve seems to have recently changed the path
                libraryCacheAssetsVDF = Path.Join(libraryCache, "assetcache.vdf");
                if (!File.Exists(libraryCacheAssetsVDF)) return appLogos;
            };

            var hash = SHA1.Create().ComputeHash(File.OpenRead(libraryCacheAssetsVDF));
            if (appLogosHash != null && hash.SequenceEqual(appLogosHash) && previousAppLogos != null) return previousAppLogos;
            else appLogosHash = hash;

            var stream = File.OpenRead(libraryCacheAssetsVDF);
            var kv = KVSerializer.Create(KVSerializationFormat.KeyValues1Binary);
            KVObject data = kv.Deserialize(stream);
            foreach (var child in data.Children.First(obj => obj.Name == "8"))
            {
                if (int.TryParse(child.Name, out int appId))
                {
                    appLogos.Add(appId, Path.Join(libraryCache, appId.ToString(), (string)child["4f"]));
                }
            }
            previousAppLogos = appLogos;
            return appLogos;
        }

        private static byte[]? appInfoHash = null;
        private static Dictionary<int, AppInfo>? previousAppInfoDict = null;
        /// <summary>
        /// Find and parse app info for all Steam apps.
        /// </summary>
        /// <param name="steamInstallDir">The Steam installation directory></param>
        /// <returns>Dictionary that maps app ID to <see cref="AppInfo" /></returns>
        public static Dictionary<int, AppInfo> FindAppInfo(string steamInstallDir)
        {
            var appInfoDict = new Dictionary<int, AppInfo>();
            var appInfoVDF = Path.Join(steamInstallDir, "appcache", "appinfo.vdf");
            if (!File.Exists(appInfoVDF)) return appInfoDict;

            var hash = SHA1.Create().ComputeHash(File.OpenRead(appInfoVDF));
            if (appInfoHash != null && hash.SequenceEqual(appInfoHash) && previousAppInfoDict != null) return previousAppInfoDict;
            else appInfoHash = hash;

            var appInfoParser = new AppInfoParser();
            appInfoParser.Read(appInfoVDF);

            appInfoParser.Apps.ForEach((app) => appInfoDict.Add((int)app.AppID, app));

            previousAppInfoDict = appInfoDict;
            return appInfoDict;
        }

        /// <summary>
        /// Find Steam app type for the given app ID.
        /// </summary>
        /// <param name="steamInstallDir">The Steam installation directory></param>
        /// <param name="appID">The app ID to search for</param>
        public static ESteamAppType FindAppType(string steamInstallDir, int appID)
        {
            var appInfoDict = FindAppInfo(steamInstallDir);
            if (!appInfoDict.TryGetValue(appID, out AppInfo? appInfo)) return ESteamAppType.Unknown;
            var common = appInfo.Data!.Children.First(obj => obj.Name == "common");
            if (common == null) return ESteamAppType.Unknown;
            var type = common.Any(x => x.Name == "type") ? (string)common["type"] : "unknown";
            return Enum.TryParse<ESteamAppType>(type, true, out var result) ? result : ESteamAppType.Unknown;
        }

        /// <summary>
        /// Find all source mods in the given library folder.
        /// </summary>
        /// <param name="libraryFolder">The Steam library folder to search in</param>
        /// <returns>List of Source Mods</returns>
        public static List<SourceMod> FindSourceMods(SteamLibraryFolder libraryFolder)
        {
            var sourceModsFolder = Path.Join(libraryFolder.Path, "steamapps", "sourcemods");
            if (!Directory.Exists(sourceModsFolder)) return [];

            var sourceMods = new List<SourceMod>();
            Directory.EnumerateDirectories(sourceModsFolder).ToList().ForEach((mod) =>
            {
                var modInfo = Path.Join(mod, "gameinfo.txt");
                if (!File.Exists(modInfo)) return;
                var kv = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
                KVObject data = kv.Deserialize(File.OpenRead(modInfo));

                sourceMods.Add(new SourceMod(
                    (string)data["game"],
                    mod,
                    (int)data.Children.First(data => data.Name == "FileSystem")["SteamAppId"]
                ));
            });

            return sourceMods;
        }


        private static byte[]? loginUsersHash = null;
        private static Dictionary<ulong, SteamUser>? previousLoginUsers = null;
        /// <summary>
        /// Find all logged in users
        /// </summary>
        /// <param name="steamInstallDir">The Steam installation directory></param>
        /// <returns>Dictionary that maps SteamID64 to <see cref="SteamUser" /></returns>
        public static Dictionary<ulong, SteamUser> FindLoginUsers(string steamInstallDir)
        {
            var steamUsers = new Dictionary<ulong, SteamUser>();
            var configFolder = Path.Join(steamInstallDir, "config");
            if (!Directory.Exists(configFolder)) return steamUsers;

            var loginUsersVDF = Path.Join(configFolder, "loginusers.vdf");
            if (!File.Exists(loginUsersVDF)) return steamUsers;

            var hash = SHA1.Create().ComputeHash(File.OpenRead(loginUsersVDF));
            if (loginUsersHash != null && hash.SequenceEqual(loginUsersHash) && previousLoginUsers != null) return previousLoginUsers;
            else loginUsersHash = hash;

            var stream = File.OpenRead(loginUsersVDF);
            var kv = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
            KVObject data = kv.Deserialize(stream);
            foreach (var child in data.Children)
            {
                var timestamp = child.Any(x => x.Name == "Timestamp") ? (double)child["Timestamp"] : 0.0;
                if (ulong.TryParse(child.Name, out ulong steamId))
                {
                    Log.Info((string)child["PersonaName"], typeof(Main));
                    steamUsers.Add(steamId, new SteamUser(
                        steamId,
                        (string?)child["AccountName"],
                        (string?)child["PersonaName"],
                        Convert.ToBoolean((int?)child["RememberPassword"]),
                        Convert.ToBoolean((int?)child["WantsOfflineMode"]),
                        Convert.ToBoolean((int?)child["SkipOfflineModeWarning"]),
                        Convert.ToBoolean((int?)child["AllowAutoLogin"]),
                        Convert.ToBoolean((int?)child["MostRecent"]),
                        DateTime.UnixEpoch.AddSeconds(timestamp)
                    ));
                }
            }
            previousLoginUsers = steamUsers;
            return steamUsers;
        }


        /// <summary>
        /// Finds all game shortcuts for the given user
        /// </summary>
        /// <param name="steamInstallDir">The Steam installation directory></param>
        /// <param name="user">The Steam user for which to find the shortcuts></param>
        public static List<SteamShortcut> FindShortcuts(string steamInstallDir, SteamUser user)
        {
            var shortcuts = new List<SteamShortcut>();
            var userConfigFolder = Path.Join(steamInstallDir, "userdata", SteamUser.SteamID64ToAccountId(user.SteamID).ToString(), "config");
            if (!Path.Exists(userConfigFolder)) return [];
            var shortcutsVDF = Path.Join(userConfigFolder, "shortcuts.vdf");
            if (!File.Exists(shortcutsVDF)) return [];

            var stream = File.OpenRead(shortcutsVDF);
            var kv = KVSerializer.Create(KVSerializationFormat.KeyValues1Binary);
            KVObject data = kv.Deserialize(stream);
            foreach (var child in data.Children)
            {
                shortcuts.Add(new SteamShortcut(
                    (int)child["appid"],
                    (string)child["AppName"],
                    (string?)child["Exe"],
                    (string?)child["StartDir"]
                ));
            }
            return shortcuts;
        }
    }
}
