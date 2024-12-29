namespace Community.PowerToys.Run.Plugin.SteamRun
{
    internal record SteamLibraryFolder(
        string? Path,
        string? Label,
        long? ContentID,
        long? TotalSize,
        long? UpdateCleanBytesTally,
        DateTime? TimeLastUpdateVerified,
        Dictionary<int, long> Apps
    );
}
