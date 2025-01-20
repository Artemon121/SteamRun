using System.Diagnostics;

namespace Community.PowerToys.Run.Plugin.SteamRun
{
    internal record SteamShortcut(
        int AppID,
        string AppName,
        string? Exe,
        string? StartDir
    ) {
        /// <summary>
        /// Run the shortcut
        /// </summary>
        public void Run()
        {
            // https://github.com/SteamRE/SteamKit/blob/8ba50d18ba7cc9c47dc1138c0ef25f517bb153c4/SteamKit2/SteamKit2/Types/GameID.cs
            ulong gameId = unchecked(((ulong)(uint)AppID) << 32 | ((ulong)1) << 25);
            Process.Start(new ProcessStartInfo
            {
                FileName = $"steam://rungameid/{gameId}",
                UseShellExecute = true,
            });
        }
    }
}
