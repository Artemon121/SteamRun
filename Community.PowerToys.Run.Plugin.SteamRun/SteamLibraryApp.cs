using System.Diagnostics;
using System.IO;

namespace Community.PowerToys.Run.Plugin.SteamRun
{
    internal class SteamLibraryApp(
        int id,
        string name,
        string? installDir,
        SteamLibraryFolder libraryFolder,
        DateTime? lastPlayed,
        ESteamAppType? type = null)
    {
        public int ID { get; } = id;
        public string Name { get; } = name;
        public string? InstallDir { get; } = installDir;
        public SteamLibraryFolder LibraryFolder { get; } = libraryFolder;
        public DateTime? LastPlayed { get; } = lastPlayed;
        private ESteamAppType? Type = type;

        /// <summary>
        /// Get Steam app type for this app
        /// </summary>
        /// <param name="steamInstallDir">The Steam installation directory></param>
        public ESteamAppType GetType(string steamInstallDir)
        {
            if (Type != null) return (ESteamAppType)Type;

            Type = SteamFinder.FindAppType(steamInstallDir, ID);
            return (ESteamAppType)Type;
        }

        /// <summary>
        /// Run the app
        /// </summary>
        public void Run()
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = $"steam://launch/{ID}/dialog",
                UseShellExecute = true,
            });
        }

        /// <summary>
        /// Show the app in the Steam Library
        /// </summary>
        public void ShowInLibrary()
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = $"steam://open/games/details/{ID}",
                UseShellExecute = true,
            });
        }

        /// <summary>
        /// Open install directory of the app 
        /// </summary>
        /// <param name="steamInstallDir">The Steam installation directory</param>
        public void OpenInstallDirectory(string steamInstallDir)
        {
            var type = GetType(steamInstallDir);
            var steamappsSubfolder = type switch
            {
                ESteamAppType.Music => "music",
                _ => "common",
            };
            var path = Path.Join(LibraryFolder.Path, "steamapps", steamappsSubfolder, InstallDir);

            if (!Path.Exists(path)) path = steamInstallDir;

            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
            });
        }
    }
}
