using System;
using UnityEngine;

namespace Leeway.Online.UnityServices
{
    [CreateAssetMenu(menuName = "Leeway/Online/Unity Gaming Services Platform Provider", fileName = "UgsPlatformProvider")]
    public sealed class UgsPlatformProvider : OnlinePlatformProvider
    {
        [Tooltip("UGS environment name (Dashboard → Environments). Empty: the project's default (\"production\").")]
        [SerializeField] private string _environment;

        [Tooltip("Authentication profile. Empty: picked automatically — one per editor instance (main editor, " +
                 "each Multiplayer Play Mode player, each ParrelSync clone), so several instances on one " +
                 "machine sign in as different players. Overridden by the -ugs-profile <name> launch argument.")]
        [SerializeField] private string _profile;

        [Tooltip("Development builds without -ugs-profile get a fresh anonymous player on every launch, so two " +
                 "builds started on one machine do not share an identity. Release builds always keep one.")]
        [SerializeField] private bool _uniqueProfilePerDevelopmentBuild = true;

        [Tooltip("Lobby data keys that search may filter on server-side, in index order (S1…S5). UGS can only " +
                 "query indexed public data; every other filter is applied on the client.")]
        [SerializeField] private string[] _indexedDataKeys = { LobbyDataKeys.ProtocolVersion, "phase", "mode" };

        [Tooltip("How long initialization and sign-in may take before falling back to the next platform.")]
        [SerializeField, Min(1f)] private float _initializeTimeoutSeconds = 15f;

        public override PlatformId PlatformId => PlatformId.UnityGamingServices;

        public override IOnlinePlatform CreatePlatform() =>
            new UgsOnlinePlatform(new UgsPlatformOptions(
                _environment,
                UgsProfiles.Resolve(_profile, _uniqueProfilePerDevelopmentBuild),
                _indexedDataKeys ?? Array.Empty<string>(),
                TimeSpan.FromSeconds(_initializeTimeoutSeconds)));
    }
}
