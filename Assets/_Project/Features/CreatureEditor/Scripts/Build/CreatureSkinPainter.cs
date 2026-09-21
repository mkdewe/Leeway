using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// Puts a colour and a coat pattern onto renderers.
    /// </summary>
    /// <remarks>
    /// <para>Everything goes through a <see cref="MaterialPropertyBlock"/>: the creature's paintwork
    /// is per-renderer state, and writing it into materials would mean one material instance per part
    /// per creature — and, worse, two players on the same map wearing each other's colours.</para>
    ///
    /// <para><b>The keyword is the catch.</b> Property blocks can swap a normal map's texture but
    /// cannot switch <c>_NORMALMAP</c> on, because keywords belong to the material. So the shared skin
    /// material ships with a flat normal map assigned and the keyword already on, and this only ever
    /// changes which texture is sampled. A part whose material has no normal map simply comes out
    /// flat — coloured correctly, without the relief.</para>
    ///
    /// <para>Bare skin is painted just as deliberately as a coat is: a white base map and the flat
    /// normal. Leaving the properties unset would let a part keep the markings it had a moment ago,
    /// because a block carries over what it is not asked to change.</para>
    /// </remarks>
    public static class CreatureSkinPainter
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
        private static readonly int BumpMapId = Shader.PropertyToID("_BumpMap");
        private static readonly int BumpScaleId = Shader.PropertyToID("_BumpScale");

        private static MaterialPropertyBlock _block;
        private static Texture2D _flatNormal;

        /// <summary>Paints every renderer under <paramref name="root"/>.</summary>
        public static void Paint(GameObject root, Color32 tint, SkinPattern pattern)
        {
            if (root == null) return;

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                Paint(renderer, tint, pattern);
        }

        public static void Paint(Renderer renderer, Color32 tint, SkinPattern pattern)
        {
            if (renderer == null) return;

            _block ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(_block);

            _block.SetColor(BaseColorId, tint);

            if (pattern == null)
            {
                _block.SetTexture(BaseMapId, Texture2D.whiteTexture);
                _block.SetVector(BaseMapStId, new Vector4(1f, 1f, 0f, 0f));
                _block.SetTexture(BumpMapId, FlatNormal());
                _block.SetFloat(BumpScaleId, 0f);
            }
            else
            {
                _block.SetTexture(BaseMapId, pattern.BaseMap != null ? pattern.BaseMap : Texture2D.whiteTexture);
                _block.SetVector(BaseMapStId, new Vector4(pattern.Tiling, pattern.Tiling, 0f, 0f));
                _block.SetTexture(BumpMapId, pattern.NormalMap != null ? pattern.NormalMap : FlatNormal());
                _block.SetFloat(BumpScaleId, pattern.NormalMap != null ? pattern.NormalStrength : 0f);
            }

            renderer.SetPropertyBlock(_block);
        }

        /// <summary>
        /// Copies the paintwork from one renderer to another.
        /// </summary>
        /// <remarks>
        /// <c>Object.Instantiate</c> does not copy a property block, so anything that clones a painted
        /// object — the leg chain, above all — has to carry it over by hand or the copy comes out in
        /// the colour the model was authored in.
        /// </remarks>
        public static void CopyPaint(Renderer from, Renderer to)
        {
            if (from == null || to == null) return;

            _block ??= new MaterialPropertyBlock();
            from.GetPropertyBlock(_block);
            to.SetPropertyBlock(_block);
        }

        /// <summary>
        /// A normal map that says "no relief here" — the tangent-space up vector, as a texture.
        /// </summary>
        /// <remarks>
        /// Generated rather than kept as an asset, so nothing in the project can be reimported into
        /// something that is not flat. Colour <c>(0.5, 0.5, 1)</c> in a linear texture, which is what
        /// a normal map sampler reads as "unchanged".
        /// </remarks>
        private static Texture2D FlatNormal()
        {
            if (_flatNormal != null) return _flatNormal;

            _flatNormal = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false, linear: true)
            {
                name = "FlatNormal",
                hideFlags = HideFlags.HideAndDontSave,
            };

            _flatNormal.SetPixel(0, 0, new Color(0.5f, 0.5f, 1f, 1f));
            _flatNormal.Apply();

            return _flatNormal;
        }
    }
}
