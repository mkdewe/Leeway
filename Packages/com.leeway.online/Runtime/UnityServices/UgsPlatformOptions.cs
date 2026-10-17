using System;

namespace Leeway.Online.UnityServices
{
    public sealed class UgsPlatformOptions
    {
        public string Environment { get; }

        /// <summary>The authentication profile, or null for the default one.</summary>
        public string Profile { get; }

        public UgsDataIndex Index { get; }
        public TimeSpan InitializeTimeout { get; }

        public UgsPlatformOptions(string environment, string profile, string[] indexedDataKeys, TimeSpan initializeTimeout)
        {
            Environment = string.IsNullOrWhiteSpace(environment) ? null : environment.Trim();
            Profile = profile;
            Index = new UgsDataIndex(indexedDataKeys);
            InitializeTimeout = initializeTimeout;
        }
    }
}
