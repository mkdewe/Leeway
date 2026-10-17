using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Leeway.Rendering
{
    /// <summary>
    /// Stylizacja z GDD (rozdz. 4.2): jasnosc obrazu mapowana na paleta conceptu,
    /// posteryzacja na pasy, mgla w kolorze palety i kontury z glebi/normalnych.
    /// Nasycone, jasne piksele (akcenty) przechodza przez mapowanie bez zmian.
    /// Renderuje to <see cref="PaletteGradeFeature"/>; ten komponent tylko trzyma parametry w Volume.
    /// </summary>
    [Serializable]
    [VolumeComponentMenu("Leeway/Palette Grade")]
    [SupportedOnRenderPipeline(typeof(UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset))]
    public sealed class PaletteGrade : VolumeComponent, IPostProcessComponent
    {
        [Header("Palette (ciemne -> jasne)")]
        public ClampedFloatParameter intensity = new(0f, 0f, 1f);
        public ColorParameter shadowDeep = new(new Color32(0x18, 0x22, 0x2B, 0xFF), false, false, true);
        public ColorParameter shadow = new(new Color32(0x36, 0x49, 0x58, 0xFF), false, false, true);
        public ColorParameter shadowMid = new(new Color32(0x3B, 0x60, 0x64, 0xFF), false, false, true);
        public ColorParameter mid = new(new Color32(0x55, 0x82, 0x8B, 0xFF), false, false, true);
        public ColorParameter light = new(new Color32(0x87, 0xBB, 0xA2, 0xFF), false, false, true);
        public ColorParameter highlight = new(new Color32(0xC9, 0xE4, 0xCA, 0xFF), false, false, true);

        [Header("Tone")]
        public ClampedFloatParameter blackPoint = new(0.08f, 0f, 1f);
        public ClampedFloatParameter whitePoint = new(1.05f, 0.1f, 1.5f);
        public ClampedFloatParameter toneGamma = new(1.5f, 0.5f, 3f);
        public ClampedIntParameter bands = new(5, 2, 16);
        public ClampedFloatParameter bandSoftness = new(0.2f, 0f, 1f);
        public ClampedFloatParameter chromaKeep = new(0.2f, 0f, 1f);

        [Header("Accents")]
        public ClampedFloatParameter accentThreshold = new(0.7f, 0f, 1f);
        public ClampedFloatParameter accentKeep = new(1f, 0f, 1f);

        [Header("Fog")]
        public MinFloatParameter fogStart = new(15f, 0f);
        public MinFloatParameter fogEnd = new(120f, 0.01f);
        public ClampedFloatParameter fogDensity = new(0.7f, 0f, 1f);
        public ClampedFloatParameter fogLuminance = new(0.85f, 0f, 1f);
        public FloatParameter fogHeight = new(0f);
        public ClampedFloatParameter fogHeightFalloff = new(0.08f, 0f, 1f);

        [Header("Outline")]
        public ClampedFloatParameter outlineStrength = new(0.8f, 0f, 1f);
        public ClampedFloatParameter outlineThickness = new(1f, 0.5f, 4f);
        public ClampedFloatParameter outlineDepthThreshold = new(0.08f, 0.001f, 1f);
        public ClampedFloatParameter outlineNormalThreshold = new(0.8f, 0.01f, 2f);
        public ColorParameter outlineColor = new(new Color32(0x10, 0x18, 0x1F, 0xFF), false, false, true);
        public MinFloatParameter outlineFadeDistance = new(60f, 1f);

        public bool IsActive() => intensity.value > 0f;
    }
}
