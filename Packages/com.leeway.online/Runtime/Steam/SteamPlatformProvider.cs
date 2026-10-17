using UnityEngine;

namespace Leeway.Online.Steam
{
    [CreateAssetMenu(menuName = "Leeway/Online/Steam Platform Provider", fileName = "SteamPlatformProvider")]
    public sealed class SteamPlatformProvider : OnlinePlatformProvider
    {
        [Tooltip("Steam App ID. 480 is Valve's public test app (Spacewar) — fine for development, replace before release.")]
        [SerializeField] private uint _appId = 480;

        [Tooltip("In a release build not started from Steam, relaunch through Steam and quit. Never applies in the editor or development builds.")]
        [SerializeField] private bool _restartThroughSteamIfNecessary;

        public override PlatformId PlatformId => PlatformId.Steam;

        public override IOnlinePlatform CreatePlatform() =>
            new SteamOnlinePlatform(new SteamPlatformOptions(_appId, _restartThroughSteamIfNecessary));
    }

    public readonly struct SteamPlatformOptions
    {
        public readonly uint AppId;
        public readonly bool RestartThroughSteamIfNecessary;

        public SteamPlatformOptions(uint appId, bool restartThroughSteamIfNecessary)
        {
            AppId = appId;
            RestartThroughSteamIfNecessary = restartThroughSteamIfNecessary;
        }
    }
}
