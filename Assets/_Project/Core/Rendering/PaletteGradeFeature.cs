using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Leeway.Rendering
{
    /// <summary>
    /// Renderer Feature dla <see cref="PaletteGrade"/>. Dziala przed post-processingiem URP,
    /// zeby bloom dalej lapal akcenty (emisje), ktore mapowanie palety przepuszcza.
    /// </summary>
    public sealed class PaletteGradeFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader _shader;
        [SerializeField] private RenderPassEvent _passEvent = RenderPassEvent.BeforeRenderingPostProcessing;

        private Material _material;
        private PaletteGradePass _pass;

        public override void Create()
        {
            if (_shader == null) _shader = Shader.Find("Hidden/Leeway/PaletteGrade");
            if (_shader == null) return;

            _material = CoreUtils.CreateEngineMaterial(_shader);
            _pass = new PaletteGradePass(_material) { renderPassEvent = _passEvent };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_pass == null) return;

            CameraType cameraType = renderingData.cameraData.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection) return;

            // A camera with post-processing off asked for a plain image - e.g. the part palette's thumbnail
            // renderer, which needs a clean transparent background, not a graded and outlined one.
            if (!renderingData.cameraData.postProcessEnabled) return;

            var grade = VolumeManager.instance.stack.GetComponent<PaletteGrade>();
            if (grade == null || !grade.IsActive()) return;

            _pass.ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
            _pass.requiresIntermediateTexture = true;
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
        }

        private sealed class PaletteGradePass : ScriptableRenderPass
        {
            private const string PassName = "Leeway Palette Grade";

            private static readonly int PaletteId = Shader.PropertyToID("_Palette");
            private static readonly int GradeId = Shader.PropertyToID("_Grade");
            private static readonly int Grade2Id = Shader.PropertyToID("_Grade2");
            private static readonly int FogId = Shader.PropertyToID("_Fog");
            private static readonly int FogHeightId = Shader.PropertyToID("_FogHeight");
            private static readonly int OutlineId = Shader.PropertyToID("_Outline");
            private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");

            private readonly Material _material;
            private readonly Vector4[] _palette = new Vector4[6];

            public PaletteGradePass(Material material)
            {
                _material = material;
                profilingSampler = new ProfilingSampler(PassName);
            }

            private class PassData
            {
                public TextureHandle Source;
                public Material Material;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;

                var grade = VolumeManager.instance.stack.GetComponent<PaletteGrade>();
                if (grade == null || !grade.IsActive()) return;

                ApplyParameters(grade);

                TextureHandle source = resources.activeColorTexture;
                TextureDesc desc = renderGraph.GetTextureDesc(source);
                desc.name = "_PaletteGradeTarget";
                desc.clearBuffer = false;
                TextureHandle target = renderGraph.CreateTexture(desc);

                using (var builder = renderGraph.AddRasterRenderPass<PassData>(PassName, out var data, profilingSampler))
                {
                    data.Source = source;
                    data.Material = _material;

                    builder.UseTexture(source);
                    // _CameraDepthTexture i _CameraNormalsTexture sa globalne.
                    builder.UseAllGlobalTextures(true);
                    builder.SetRenderAttachment(target, 0);
                    builder.SetRenderFunc(static (PassData d, RasterGraphContext ctx) =>
                        Blitter.BlitTexture(ctx.cmd, d.Source, new Vector4(1f, 1f, 0f, 0f), d.Material, 0));
                }

                resources.cameraColor = target;
            }

            private void ApplyParameters(PaletteGrade g)
            {
                // SetVectorArray nie konwertuje gamma -> linear tak jak SetColor.
                _palette[0] = g.shadowDeep.value.linear;
                _palette[1] = g.shadow.value.linear;
                _palette[2] = g.shadowMid.value.linear;
                _palette[3] = g.mid.value.linear;
                _palette[4] = g.light.value.linear;
                _palette[5] = g.highlight.value.linear;
                _material.SetVectorArray(PaletteId, _palette);

                _material.SetVector(GradeId, new Vector4(
                    g.intensity.value, g.blackPoint.value, Mathf.Max(g.whitePoint.value, g.blackPoint.value + 0.01f), g.bands.value));
                _material.SetVector(Grade2Id, new Vector4(
                    g.bandSoftness.value, g.chromaKeep.value, g.accentThreshold.value, g.accentKeep.value));
                _material.SetVector(FogId, new Vector4(
                    g.fogStart.value, Mathf.Max(g.fogEnd.value, g.fogStart.value + 0.01f), g.fogDensity.value, g.fogLuminance.value));
                _material.SetVector(FogHeightId, new Vector4(g.fogHeight.value, g.fogHeightFalloff.value, g.toneGamma.value, 0f));
                _material.SetVector(OutlineId, new Vector4(
                    g.outlineStrength.value, g.outlineThickness.value, g.outlineDepthThreshold.value, g.outlineNormalThreshold.value));

                Color outline = g.outlineColor.value.linear;
                _material.SetVector(OutlineColorId, new Vector4(outline.r, outline.g, outline.b, g.outlineFadeDistance.value));
            }
        }
    }
}
