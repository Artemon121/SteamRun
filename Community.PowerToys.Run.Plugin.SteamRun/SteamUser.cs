namespace Community.PowerToys.Run.Plugin.SteamRun
{
    internal record SteamUser(
        ulong SteamID,
        string? AccountName,
        string? PersonaName,
        bool? RememberPassword,
        bool? WantsOfflineMode,
        bool? SkipOfflineModeWarning,
        bool? AllowAutoLogin,
        bool? MostRecent,
        DateTime Timestamp
    )
    {
        public static uint SteamID64ToAccountId(ulong steamID)
        {
            return (uint)(steamID - 76561197960265728);
        }
    };
}
