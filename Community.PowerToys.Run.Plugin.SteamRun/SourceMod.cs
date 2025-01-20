using System.Diagnostics;

namespace Community.PowerToys.Run.Plugin.SteamRun
{
    internal record SourceMod(
        string Name,
        string Path,
        int BaseAppId
    )
    {
        /// <summary>
        /// Run the mod
        /// </summary>
        public void Run()
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = $"steam://launch/{BaseAppId}/-steam -game \"{Path}\"".Replace(" ", "%20"),
                UseShellExecute = true,
            });
        }
    };
}
