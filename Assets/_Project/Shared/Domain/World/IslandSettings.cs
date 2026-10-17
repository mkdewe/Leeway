using System;
using UnityEngine;

namespace Leeway.World.Domain
{
    /// <summary>
    /// Shape parameters of one island archetype. The seed picks the concrete island;
    /// these values decide what all islands of the archetype have in common.
    /// </summary>
    [Serializable]
    public sealed class IslandSettings
    {
        [field: Header("Grid")]
        [field: Tooltip("Edge length of the generated square, in metres.")]
        [field: SerializeField] public float Size { get; private set; } = 520f;
        [field: Tooltip("Cells per edge. The low-poly look comes from keeping this coarse.")]
        [field: Range(16, 320)]
        [field: SerializeField] public int Resolution { get; private set; } = 208;

        [field: Header("Coast")]
        [field: Tooltip("Coast distance from the centre as a fraction of half the size.")]
        [field: Range(0.2f, 0.95f)]
        [field: SerializeField] public float CoastRadius { get; private set; } = 0.62f;
        [field: Tooltip("How far the coastline wanders in and out (fraction of the coast radius).")]
        [field: Range(0f, 0.6f)]
        [field: SerializeField] public float CoastIrregularity { get; private set; } = 0.25f;
        [field: Range(0.2f, 6f)]
        [field: SerializeField] public float CoastFrequency { get; private set; } = 2.2f;
        [field: Tooltip("Width of the band in which land drops into the sea (fraction of half the size).")]
        [field: Range(0.01f, 0.3f)]
        [field: SerializeField] public float ShoreWidth { get; private set; } = 0.08f;
        [field: SerializeField] public float SeaFloorDepth { get; private set; } = -5f;

        [field: Header("Relief")]
        [field: SerializeField] public float BeachHeight { get; private set; } = 0.6f;
        [field: SerializeField] public float HillHeight { get; private set; } = 14f;
        [field: Tooltip("Extra height rising toward the island's centre.")]
        [field: SerializeField] public float CentralRise { get; private set; } = 10f;
        [field: Range(0.2f, 8f)]
        [field: SerializeField] public float HillFrequency { get; private set; } = 2.4f;
        [field: Range(1, 6)]
        [field: SerializeField] public int HillOctaves { get; private set; } = 4;

        [field: Header("Terraces")]
        [field: Tooltip("0 = smooth hills, 1 = flat plateaus with cliffs between them.")]
        [field: Range(0f, 1f)]
        [field: SerializeField] public float TerraceStrength { get; private set; } = 0.75f;
        [field: SerializeField] public float TerraceStep { get; private set; } = 4f;
        [field: Range(0.01f, 0.5f)]
        [field: SerializeField] public float TerraceSharpness { get; private set; } = 0.12f;

        [field: Header("Safe hub")]
        [field: SerializeField] public float HubRadius { get; private set; } = 16f;
        [field: SerializeField] public float HubBlend { get; private set; } = 7f;
        [field: Tooltip("Hub distance from the centre as a fraction of the coast radius at its angle.")]
        [field: Range(0f, 0.8f)]
        [field: SerializeField] public float HubDistance { get; private set; } = 0.35f;
        [field: SerializeField] public float HubMinHeight { get; private set; } = 1.5f;

        [field: Header("Lakes and rivers")]
        [field: Range(0, 8)]
        [field: SerializeField] public int LakeCount { get; private set; } = 3;
        [field: SerializeField] public float LakeRadiusMin { get; private set; } = 11f;
        [field: SerializeField] public float LakeRadiusMax { get; private set; } = 24f;
        [field: SerializeField] public float LakeDepth { get; private set; } = 2.5f;
        [field: Tooltip("Rivers that start at a spring in the hills instead of at a lake.")]
        [field: Range(0, 6)]
        [field: SerializeField] public int SpringCount { get; private set; } = 2;
        [field: SerializeField] public float RiverWidth { get; private set; } = 5f;
        [field: SerializeField] public float RiverDepth { get; private set; } = 1.4f;
        [field: Tooltip("Width of the sloped bank on each side of a river or lake, in metres.")]
        [field: SerializeField] public float BankWidth { get; private set; } = 5f;
        [field: Range(0f, 1.5f)]
        [field: SerializeField] public float RiverMeander { get; private set; } = 0.6f;

        [field: Header("Caves")]
        [field: Range(0, 8)]
        [field: SerializeField] public int CaveCount { get; private set; } = 3;
        [field: SerializeField] public float CaveRadius { get; private set; } = 7f;
        [field: Tooltip("How much higher the ground behind a cave mouth must be, so caves sit in cliffs.")]
        [field: SerializeField] public float CaveMinCliff { get; private set; } = 2.8f;

        public float SeaLevel => 0f;
        public float HalfSize => Size * 0.5f;
        public float CellSize => Size / Resolution;
    }
}
