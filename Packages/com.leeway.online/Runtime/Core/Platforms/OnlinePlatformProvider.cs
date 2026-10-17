using UnityEngine;

namespace Leeway.Online
{
    /// <summary>
    /// A factory for one backend, authored as an asset.
    /// </summary>
    /// <remarks>
    /// <para>An abstract factory as a ScriptableObject is what keeps the composition root free of
    /// <c>#if STEAM</c>: the game lists provider assets in priority order, and a backend whose assembly
    /// is not compiled for the current target (Steam on a console) simply turns into a missing script
    /// that the bootstrapper skips.</para>
    ///
    /// <para>Backend-specific settings (Steam app id, EOS deployment id …) live on the concrete asset,
    /// right next to the code that reads them.</para>
    /// </remarks>
    public abstract class OnlinePlatformProvider : ScriptableObject
    {
        public abstract PlatformId PlatformId { get; }

        /// <summary>
        /// Whether this backend can run in the current player at all (target platform, build flavour).
        /// Cheap, synchronous and side-effect free — real availability is decided by
        /// <see cref="IOnlinePlatform.InitializeAsync"/>.
        /// </summary>
        public virtual bool IsSupportedOnCurrentRuntime => true;

        /// <summary>Creates an uninitialized platform. The caller owns it and must dispose it.</summary>
        public abstract IOnlinePlatform CreatePlatform();
    }
}
