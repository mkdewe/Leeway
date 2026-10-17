using System;
using System.Collections.Generic;
using Leeway.World.Domain;
using UnityEngine;

namespace Leeway.World
{
    /// <summary>
    /// One island archetype's asset set (GDD 3.2): terrain colours and materials, water and the
    /// props that grow on it. Swapping the biome gives the same generator a different feel.
    /// </summary>
    [CreateAssetMenu(menuName = "Leeway/World/Island Biome", fileName = "Biome_")]
    public sealed class IslandBiome : ScriptableObject
    {
        [Serializable]
        public sealed class ScatterLayer
        {
            [SerializeField] private string _name;
            [SerializeField] private GameObject[] _prefabs;
            [SerializeField] private ScatterRule _rule;
            [Tooltip("Tilts the prop toward the ground normal (0 = always upright).")]
            [SerializeField, Range(0f, 1f)] private float _alignToNormal;
            [Tooltip("Pushes the prop into the ground so slopes don't show its base.")]
            [SerializeField] private float _sink;
            [Tooltip("Merge the visuals into one mesh per chunk (colliders and lights stay separate). Off for prefabs with animation or LODs.")]
            [SerializeField] private bool _combineMeshes = true;

            public string Name => _name;
            public IReadOnlyList<GameObject> Prefabs => _prefabs;
            public ScatterRule Rule => _rule;
            public float AlignToNormal => _alignToNormal;
            public float Sink => _sink;
            public bool CombineMeshes => _combineMeshes;

            public ScatterLayer(string name, GameObject[] prefabs, ScatterRule rule, float alignToNormal, float sink, bool combineMeshes = true)
            {
                _combineMeshes = combineMeshes;
                _name = name;
                _prefabs = prefabs;
                _rule = rule;
                _alignToNormal = alignToNormal;
                _sink = sink;
            }
        }

        [SerializeField] private IslandSettings _shape = new();

        [Header("Terrain")]
        [SerializeField] private Material _groundMaterial;
        [SerializeField] private Material _waterMaterial;
        [SerializeField] private Color _sand = new Color32(0x8E, 0x9A, 0x83, 0xFF);
        [SerializeField] private Color _grass = new Color32(0x41, 0x6B, 0x57, 0xFF);
        [SerializeField] private Color _grassDark = new Color32(0x2E, 0x52, 0x45, 0xFF);
        [SerializeField] private Color _rock = new Color32(0x45, 0x50, 0x5A, 0xFF);
        [SerializeField] private Color _seaBed = new Color32(0x3B, 0x60, 0x64, 0xFF);
        [Tooltip("Steepest slope still painted as grass, in degrees.")]
        [SerializeField, Range(0f, 90f)] private float _grassMaxSlope = 32f;

        [Header("Props")]
        [SerializeField] private List<ScatterLayer> _layers = new();

        [Header("Landmarks")]
        [SerializeField] private GameObject _cavePrefab;
        [Tooltip("Standing stones ringing the safe hub, so it reads as the pack's den.")]
        [SerializeField] private GameObject[] _hubStones;
        [SerializeField, Range(0, 24)] private int _hubStoneCount = 11;

        public IslandSettings Shape => _shape;
        public Material GroundMaterial => _groundMaterial;
        public Material WaterMaterial => _waterMaterial;
        public Color Sand => _sand;
        public Color Grass => _grass;
        public Color GrassDark => _grassDark;
        public Color Rock => _rock;
        public Color SeaBed => _seaBed;
        public float GrassMaxSlope => _grassMaxSlope;
        public IReadOnlyList<ScatterLayer> Layers => _layers;
        public GameObject CavePrefab => _cavePrefab;
        public IReadOnlyList<GameObject> HubStones => _hubStones;
        public int HubStoneCount => _hubStoneCount;

#if UNITY_EDITOR
        public void EditorSetup(Material ground, Material water, List<ScatterLayer> layers,
            GameObject cave, GameObject[] hubStones)
        {
            _groundMaterial = ground;
            _waterMaterial = water;
            _layers = layers;
            _cavePrefab = cave;
            _hubStones = hubStones;
        }
#endif
    }
}
