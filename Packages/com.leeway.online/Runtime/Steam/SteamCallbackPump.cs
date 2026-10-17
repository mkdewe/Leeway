using Cysharp.Threading.Tasks;
using Steamworks;

namespace Leeway.Online.Steam
{
    /// <summary>
    /// Pumps <see cref="SteamAPI.RunCallbacks"/> once per frame from the player loop.
    /// </summary>
    /// <remarks>
    /// A player-loop item instead of a MonoBehaviour: the platform is plain C# owned by the DI
    /// container, and must not depend on some GameObject surviving scene loads.
    /// </remarks>
    internal sealed class SteamCallbackPump : IPlayerLoopItem
    {
        private bool _running = true;

        public static SteamCallbackPump Start()
        {
            var pump = new SteamCallbackPump();
            PlayerLoopHelper.AddAction(PlayerLoopTiming.PreUpdate, pump);
            return pump;
        }

        public void Stop() => _running = false;

        public bool MoveNext()
        {
            if (!_running) return false;
            SteamAPI.RunCallbacks();
            return true;
        }
    }
}
