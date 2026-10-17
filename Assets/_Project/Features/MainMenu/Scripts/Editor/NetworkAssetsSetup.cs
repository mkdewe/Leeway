using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FishNet.Managing;
using FishNet.Managing.Transporting;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
using Leeway.Network.Session;
using Leeway.Online;
using Leeway.Online.FishNet;
using UnityEditor;
using UnityEngine;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace Leeway.MainMenu.Editor
{
    /// <summary>
    /// Creates (or completes) the connection assets: platform providers, transport adapters, the
    /// Steam NetworkManager variant, the session settings and the VContainer root scope.
    /// </summary>
    /// <remarks>
    /// Idempotent and non-destructive: existing assets are reused and only missing references are
    /// filled in, so it is safe to re-run after tweaking values by hand. Backend types that are not
    /// compiled (Steam on a machine without Steamworks.NET) are looked up by name and skipped.
    /// </remarks>
    internal static class NetworkAssetsSetup
    {
        public const string SettingsFolder = "Assets/_Project/Features/Network/Settings";
        public const string NetworkPrefabFolder = "Assets/_Project/Features/Network/Prefabs";
        public const string BaseNetworkManagerPath = NetworkPrefabFolder + "/NetworkManager.prefab";
        public const string SteamNetworkManagerPath = NetworkPrefabFolder + "/NetworkManager_Steam.prefab";
        public const string SettingsPath = SettingsFolder + "/NetworkSessionSettings.asset";
        public const string BootstrapFolder = "Assets/_Project/Core/Bootstrap";
        public const string RootScopePrefabPath = BootstrapFolder + "/ProjectLifetimeScope.prefab";
        public const string VContainerSettingsPath = BootstrapFolder + "/VContainerSettings.asset";

        private const string SteamProviderType = "Leeway.Online.Steam.SteamPlatformProvider, Leeway.Online.Steam";
        private const string UgsProviderType = "Leeway.Online.UnityServices.UgsPlatformProvider, Leeway.Online.UnityServices";
        private const string LocalProviderType = "Leeway.Online.Local.LocalPlatformProvider, Leeway.Online";
        private const string TugboatAdapterType = "Leeway.Online.FishNet.TugboatTransportAdapter, Leeway.Online.FishNet";
        private const string SteamAdapterType = "Leeway.Online.FishNet.Steam.FishySteamworksTransportAdapter, Leeway.Online.FishNet.Steam";
        private const string FishySteamworksType = "FishySteamworks.FishySteamworks, FishySteamworks";
        private const string RootScopeType = "Leeway.Core.ProjectLifetimeScope, Assembly-CSharp";

        [MenuItem("Leeway/Online/Setup Network Assets", priority = 100)]
        public static void Run()
        {
            EnsureFolder(SettingsFolder);

            var steamProvider = CreateIfMissing<OnlinePlatformProvider>(SteamProviderType, SettingsFolder + "/SteamPlatformProvider.asset");
            var ugsProvider = CreateIfMissing<OnlinePlatformProvider>(UgsProviderType, SettingsFolder + "/UgsPlatformProvider.asset");
            var localProvider = CreateIfMissing<OnlinePlatformProvider>(LocalProviderType, SettingsFolder + "/LocalPlatformProvider.asset");
            var steamAdapter = CreateIfMissing<TransportAdapter>(SteamAdapterType, SettingsFolder + "/FishySteamworksTransportAdapter.asset");
            var tugboatAdapter = CreateIfMissing<TransportAdapter>(TugboatAdapterType, SettingsFolder + "/TugboatTransportAdapter.asset");

            var baseNetworkManager = AssetDatabase.LoadAssetAtPath<NetworkManager>(BaseNetworkManagerPath);
            if (baseNetworkManager == null)
            {
                Debug.LogError($"[NetworkAssetsSetup] {BaseNetworkManagerPath} not found — cannot create the session settings.");
                return;
            }
            var steamNetworkManager = CreateSteamNetworkManagerVariant();

            var settings = AssetDatabase.LoadAssetAtPath<NetworkSessionSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<NetworkSessionSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }

            var so = new SerializedObject(settings);
            var providers = so.FindProperty("_platformProviders");
            FillListIfEmpty(providers, new Object[] { steamProvider, ugsProvider, localProvider });
            // Backends added after the first run: slot UGS in before the Local fallback.
            InsertBeforeIfMissing(providers, ugsProvider, localProvider);
            FillListIfEmpty(so.FindProperty("_transportAdapters"), new Object[] { steamAdapter, tugboatAdapter });
            var defaultNm = so.FindProperty("_defaultNetworkManager");
            if (defaultNm.objectReferenceValue == null) defaultNm.objectReferenceValue = baseNetworkManager;

            var platformNms = so.FindProperty("_platformNetworkManagers");
            if (platformNms.arraySize == 0 && steamNetworkManager != null)
            {
                platformNms.arraySize = 1;
                var entry = platformNms.GetArrayElementAtIndex(0);
                entry.FindPropertyRelative("PlatformId").stringValue = PlatformId.Steam.Value;
                entry.FindPropertyRelative("Prefab").objectReferenceValue = steamNetworkManager;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            SetupRootScope(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("[NetworkAssetsSetup] Network assets are in place.", settings);
        }

        /// <summary>A variant of the base NetworkManager that serves Steam relay and LAN at once through Multipass.</summary>
        private static NetworkManager CreateSteamNetworkManagerVariant()
        {
            var fishyType = Type.GetType(FishySteamworksType);
            if (fishyType == null)
            {
                Debug.LogWarning("[NetworkAssetsSetup] FishySteamworks is not compiled — skipping the Steam NetworkManager.");
                return null;
            }

            var existing = AssetDatabase.LoadAssetAtPath<NetworkManager>(SteamNetworkManagerPath);
            if (existing != null) return existing;

            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BaseNetworkManagerPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            try
            {
                var transportManager = instance.GetComponentInChildren<TransportManager>(true);
                var host = transportManager.gameObject;

                var tugboat = host.GetComponent<Tugboat>() ?? host.AddComponent<Tugboat>();
                var fishy = (Transport)host.AddComponent(fishyType);
                var multipass = host.AddComponent<Multipass>();

                // Peer-to-peer: players host on their own machines; there is no Steam game server.
                var fishySo = new SerializedObject(fishy);
                fishySo.FindProperty("_peerToPeer").boolValue = true;
                fishySo.ApplyModifiedPropertiesWithoutUndo();

                var multipassSo = new SerializedObject(multipass);
                var transports = multipassSo.FindProperty("_transports");
                transports.arraySize = 2;
                transports.GetArrayElementAtIndex(0).objectReferenceValue = fishy;
                transports.GetArrayElementAtIndex(1).objectReferenceValue = tugboat;
                multipassSo.ApplyModifiedPropertiesWithoutUndo();

                var tmSo = new SerializedObject(transportManager);
                tmSo.FindProperty("Transport").objectReferenceValue = multipass;
                tmSo.ApplyModifiedPropertiesWithoutUndo();

                instance.name = "NetworkManager_Steam";
                var saved = PrefabUtility.SaveAsPrefabAsset(instance, SteamNetworkManagerPath);
                return saved.GetComponent<NetworkManager>();
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static void SetupRootScope(NetworkSessionSettings settings)
        {
            var scopeType = Type.GetType(RootScopeType);
            if (scopeType == null)
            {
                Debug.LogError("[NetworkAssetsSetup] ProjectLifetimeScope is not compiled yet.");
                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RootScopePrefabPath);
            if (prefab == null)
            {
                var go = new GameObject("ProjectLifetimeScope");
                go.AddComponent(scopeType);
                prefab = PrefabUtility.SaveAsPrefabAsset(go, RootScopePrefabPath);
                Object.DestroyImmediate(go);
            }

            var scope = (LifetimeScope)prefab.GetComponent(scopeType);
            var scopeSo = new SerializedObject(scope);
            var settingsField = scopeSo.FindProperty("_networkSettings");
            if (settingsField.objectReferenceValue == null)
            {
                settingsField.objectReferenceValue = settings;
                scopeSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(prefab);
            }

            var vcSettings = AssetDatabase.LoadAssetAtPath<VContainerSettings>(VContainerSettingsPath)
                             ?? PlayerSettings.GetPreloadedAssets().OfType<VContainerSettings>().FirstOrDefault();
            if (vcSettings == null)
            {
                vcSettings = ScriptableObject.CreateInstance<VContainerSettings>();
                AssetDatabase.CreateAsset(vcSettings, VContainerSettingsPath);
            }

            var vcSo = new SerializedObject(vcSettings);
            var rootField = vcSo.FindProperty("RootLifetimeScope");
            if (rootField.objectReferenceValue == null) rootField.objectReferenceValue = scope;
            vcSo.ApplyModifiedPropertiesWithoutUndo();

            // VContainer reads its settings from the preloaded assets in players.
            var preloaded = PlayerSettings.GetPreloadedAssets().Where(a => a != null).ToList();
            if (!preloaded.Contains(vcSettings))
            {
                preloaded.Add(vcSettings);
                PlayerSettings.SetPreloadedAssets(preloaded.ToArray());
            }
        }

        private static T CreateIfMissing<T>(string typeName, string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var type = Type.GetType(typeName);
            if (type == null)
            {
                Debug.LogWarning($"[NetworkAssetsSetup] {typeName} is not compiled for this editor — skipping {Path.GetFileName(path)}.");
                return null;
            }

            var asset = (T)ScriptableObject.CreateInstance(type);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void FillListIfEmpty(SerializedProperty list, IEnumerable<Object> values)
        {
            if (list.arraySize > 0) return;
            foreach (var value in values)
            {
                if (value == null) continue;
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = value;
            }
        }

        private static void InsertBeforeIfMissing(SerializedProperty list, Object value, Object before)
        {
            if (value == null) return;
            int index = list.arraySize;
            for (int i = 0; i < list.arraySize; i++)
            {
                var current = list.GetArrayElementAtIndex(i).objectReferenceValue;
                if (current == value) return;
                if (current == before && index == list.arraySize) index = i;
            }

            list.InsertArrayElementAtIndex(index);
            list.GetArrayElementAtIndex(index).objectReferenceValue = value;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
