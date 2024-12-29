using System.Collections.ObjectModel;
using ValveKeyValue;

// https://github.com/SteamDatabase/SteamAppInfo
namespace Community.PowerToys.Run.Plugin.SteamRun.SteamAppInfo
{
    internal class AppInfo
    {
        public uint AppID { get; set; }

        public uint InfoState { get; set; }

        public DateTime LastUpdated { get; set; }

        public ulong Token { get; set; }

        public ReadOnlyCollection<byte>? Hash { get; set; }
        public ReadOnlyCollection<byte>? BinaryDataHash { get; set; }

        public uint ChangeNumber { get; set; }

        public KVObject? Data { get; set; }
    }
}
