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
    }
}
