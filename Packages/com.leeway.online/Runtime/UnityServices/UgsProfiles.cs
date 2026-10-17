using System;
using System.Text;
using UnityEngine;

namespace Leeway.Online.UnityServices
{
    /// <summary>
    /// Picks the Authentication profile. UGS keeps the anonymous player's session token in PlayerPrefs
    /// per profile, and every editor on a machine shares PlayerPrefs — without distinct profiles the
    /// main editor and a Multiplayer Play Mode player would sign in as the same player and could
    /// never meet in one lobby.
    /// </summary>
    internal static class UgsProfiles
    {
        private const string LaunchArgument = "-ugs-profile";

        // Authentication accepts up to 30 characters: letters, digits, '-' and '_'.
        private const int MaxLength = 30;

        public static string Resolve(string configured, bool uniquePerDevelopmentBuild)
        {
            if (TryReadLaunchArgument(out var fromArgs)) return Sanitize(fromArgs);
            if (!string.IsNullOrWhiteSpace(configured)) return Sanitize(configured);

#if UNITY_EDITOR
            // Every editor instance — the main one, Multiplayer Play Mode players (Library/VP/…),
            // ParrelSync clones — runs from its own project folder, which makes a stable per-instance key.
            return "editor-" + Fnv1a(Application.dataPath).ToString("x8");
#else
            if (uniquePerDevelopmentBuild && Debug.isDebugBuild)
                return "dev-" + Guid.NewGuid().ToString("N").Substring(0, 12);
            return null;
#endif
        }

        private static bool TryReadLaunchArgument(out string value)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (!string.Equals(args[i], LaunchArgument, StringComparison.OrdinalIgnoreCase)) continue;
                value = args[i + 1];
                return !string.IsNullOrWhiteSpace(value);
            }
            value = null;
            return false;
        }

        private static string Sanitize(string profile)
        {
            var builder = new StringBuilder(Math.Min(profile.Length, MaxLength));
            foreach (char c in profile.Trim())
            {
                if (builder.Length == MaxLength) break;
                builder.Append(char.IsLetterOrDigit(c) && c < 128 || c == '-' || c == '_' ? c : '_');
            }
            return builder.Length > 0 ? builder.ToString() : null;
        }

        // string.GetHashCode is not guaranteed stable across runs; the profile must be.
        private static uint Fnv1a(string text)
        {
            uint hash = 2166136261;
            foreach (char c in text)
            {
                hash ^= c;
                hash *= 16777619;
            }
            return hash;
        }
    }
}
