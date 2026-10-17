using System.Collections.Generic;
using Leeway.Creature.Domain;
using PaintIn3D;
using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// The creature's skin as a texture the player can paint on, through Paint in 3D.
    /// </summary>
    /// <remarks>
    /// <para><b>What changed.</b> Colour and coat used to be two numbers in the genome, applied to the
    /// whole carcass through a property block. They still seed the skin — but from there the texture is
    /// the truth: a brush stroke changes pixels, not a gene, and what the creature looks like is
    /// whatever has been painted onto it.</para>
    ///
    /// <para><b>The body only.</b> The carcass gets a paintable texture; the attached parts keep the
    /// genome's colour and coat. A paintable texture is a render texture per renderer, and a creature
    /// with a dozen parts — whose legs clone their links — would be a dozen render targets walking
    /// around. The skin is where markings are read anyway.</para>
    ///
    /// <para><b>The texture is per creature.</b> <c>P3dMaterialCloner</c> gives this carcass its own
    /// copy of the shared material, otherwise the first brush stroke would paint every creature in the
    /// scene that shares it.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class CreatureSkinCanvas : MonoBehaviour
    {
        /// <summary>The material property the skin texture is bound to. URP/Lit reads the albedo from it.</summary>
        private const string AlbedoSlot = "_BaseMap";

        /// <summary>The layer the paint colliders live on. It collides with nothing — the brush raycasts it and no physics touches it.</summary>
        public const string PaintLayer = "CreaturePaint";

        [Tooltip("How much detail the skin holds, in texels per metre of creature. The texture is sized " +
                 "from this and the body's own measurements, so a stroke is as sharp on a big creature " +
                 "as on a small one. 500 is about two millimetres per texel.")]
        [SerializeField, Range(200f, 2000f)] private float _texelsPerMetre = 500f;

        [Tooltip("The smallest skin texture. Below this even a tiny creature looks smudged.")]
        [SerializeField] private int _minResolution = 256;

        [Tooltip("The largest skin texture. This is the memory ceiling: a creature carries one of these.")]
        [SerializeField] private int _maxResolution = 2048;

        [Tooltip("Whether the attached parts can be painted too. Every part renderer then costs a render " +
                 "texture of its own, and a leg's links each count as one.")]
        [SerializeField] private bool _paintableParts = true;

        [Tooltip("Resolution for a part. Far smaller than the body's: a leg link is a few centimetres of skin.")]
        [SerializeField] private int _partResolution = 128;

        [Tooltip("The colours and coats the skin is seeded from before anyone paints on it.")]
        [SerializeField] private CreatureSkinPalette _palette;

        private P3dPaintable _paintable;
        private P3dPaintableTexture _texture;
        private P3dPaintSphere _brush;

        /// <summary>One canvas per part renderer, in the order the parts were built.</summary>
        private readonly List<P3dPaintableTexture> _parts = new();

        /// <summary>The texture being painted, or <c>null</c> before a body has been built.</summary>
        public P3dPaintableTexture Texture => _texture;

        public bool IsReady => _texture != null && _paintable != null && _paintable.Activated;

        /// <summary>
        /// Hangs a fresh canvas on a freshly built carcass.
        /// </summary>
        /// <remarks>
        /// A rebuild destroys the renderer, and with it the paintable texture — so the paint is lost
        /// whenever the player changes the body. That is the honest behaviour for now: the strokes live
        /// in pixels on a mesh whose UVs have just been regenerated, and there is nothing to map the old
        /// pixels onto the new skin with.
        /// </remarks>
        public void Rebuild(BuiltCreatureBody body, CreatureGenome genome, PartRuleSet rules = null)
        {
            // The paint outlives the carcass it was painted on. The renderer is about to be replaced,
            // so the pixels are lifted off the old skin first and laid back onto the new one at the end
            // — otherwise growing a vertebra would wipe everything the player had painted.
            byte[] carried = ToPng();

            _paintable = null;
            _texture = null;

            Renderer renderer = body?.Renderer;
            if (renderer == null) return;

            // The skin is measured, not assumed: a fixed square spread a fixed number of texels over
            // whatever surface the creature happened to have, so a long body got a handful of texels
            // per centimetre and every stroke came out smeared. See SkinTextureSize.
            Vector2Int size = SkinTextureSize(genome);

            _texture = MakePaintable(renderer, size.x, size.y);
            if (_texture == null) return;

            _paintable = renderer.GetComponent<P3dPaintable>();

            TakeOverFromPropertyBlock(renderer);
            SeedFromGenome(genome);

            // The UVs are regenerated with the mesh, but their layout is not: u still runs around the
            // body and v along it. So the old skin lands roughly where it was painted — a stripe across
            // the back stays a stripe across the back, on a body one vertebra longer.
            if (carried != null) LoadPng(carried);

            if (_paintableParts) MakePartsPaintable(body, genome, rules);
        }

        /// <summary>Whether a part will be unfolded into a leg chain, and so cloned.</summary>
        private static bool IsLeg(CreatureGenome genome, PartRuleSet rules, int geneIndex)
            => rules != null && GenomeStatRules.TryResolveLeg(genome, rules, geneIndex, out _, out _);

        /// <summary>
        /// How many texels the skin gets, worked out from the body it has to cover.
        /// </summary>
        /// <remarks>
        /// <para><b>Why not a fixed square.</b> The mesh's <c>u</c> runs around the body and <c>v</c>
        /// along it, each from 0 to 1 — so a square texture spreads the same number of texels over a
        /// circumference of 40 cm as over a body 3 m long. On anything but a perfectly round creature
        /// the texels come out rectangular, and a brush stroke is then sharp across the body and
        /// smeared along it. That is the "the paint blurs" everyone sees, and no amount of brush
        /// tuning fixes it, because the texture simply has no detail left in that direction.</para>
        ///
        /// <para>So the two sides are sized <b>separately</b>, each from the distance it covers: the
        /// width from the widest circumference, the height from the length of the spine. Texels come
        /// out square and the same size on every creature, big or small — and a long body gets a tall
        /// texture rather than a blurry one.</para>
        ///
        /// <para>Rounded to powers of two (render textures and mip maps prefer it), and clamped at both
        /// ends: below the floor the skin is mush, and above the ceiling a creature costs more memory
        /// than the rest of the scene.</para>
        /// </remarks>
        public Vector2Int SkinTextureSize(CreatureGenome genome)
        {
            if (genome == null || genome.VertebraCount == 0)
                return new Vector2Int(_minResolution, _minResolution);

            float length = 0f;
            float widest = 0f;

            for (int i = 0; i < genome.VertebraCount; i++)
            {
                VertebraGene vertebra = genome.GetVertebra(i);

                // Vertebra 0 sits at the root; every other offset is the step from the one before.
                if (i > 0) length += vertebra.LocalOffset.magnitude;
                widest = Mathf.Max(widest, vertebra.Radius);
            }

            // The domed ends are skin too, and they are about a radius deep apiece.
            length += widest * 2f;

            return new Vector2Int(
                Fit(2f * Mathf.PI * widest),
                Fit(length));
        }

        /// <summary>One side of the texture: the distance it covers, at the wanted density, as a power of two.</summary>
        private int Fit(float metres)
        {
            float wanted = Mathf.Max(1f, metres) * Mathf.Max(64f, _texelsPerMetre);
            int power = Mathf.NextPowerOfTwo(Mathf.CeilToInt(wanted));

            return Mathf.Clamp(power, _minResolution, _maxResolution);
        }

        /// <summary>
        /// Turns one renderer into something that can be painted on, and hands back its texture.
        /// </summary>
        /// <remarks>
        /// <para>Three components make a canvas in Paint in 3D, and all three matter:
        /// <c>P3dMaterialCloner</c> gives this renderer its own copy of the shared material, without
        /// which the first stroke would paint every creature that shares it;
        /// <c>P3dPaintable</c> is the model; <c>P3dPaintableTexture</c> is the picture and the material
        /// property it is bound to.</para>
        ///
        /// <para>Activation is called by hand, because the mesh only exists at this moment — every one
        /// of P3D's own activation points (Awake, Start) has already gone by.</para>
        /// </remarks>
        private static P3dPaintableTexture MakePaintable(Renderer renderer, int width, int height)
        {
            if (renderer == null) return null;

            GameObject host = renderer.gameObject;

            if (host.GetComponent<P3dMaterialCloner>() == null) host.AddComponent<P3dMaterialCloner>();

            P3dPaintable paintable = host.GetComponent<P3dPaintable>();
            if (paintable == null)
            {
                paintable = host.AddComponent<P3dPaintable>();
                paintable.Activation = P3dPaintable.ActivationType.OnFirstUse;
            }

            P3dPaintableTexture texture = host.GetComponent<P3dPaintableTexture>();
            if (texture == null) texture = host.AddComponent<P3dPaintableTexture>();

            texture.Slot = new P3dSlot(0, AlbedoSlot);
            texture.Width = width;
            texture.Height = height;

            paintable.Activate();
            renderer.SetPropertyBlock(null);

            AddPaintCollider(renderer);

            return texture;
        }

        /// <summary>
        /// Gives a paintable renderer a collider shaped like the thing the player can see.
        /// </summary>
        /// <remarks>
        /// <para><b>This is what makes the brush land where the cursor is.</b> The only collider on the
        /// carcass is the locomotion capsule — a single fat cylinder around the whole body. A brush
        /// raycasting that gets a point on the capsule's surface, which near the head and tail is a
        /// hand's width away from the skin, so the paint goes down beside where it was aimed or smears
        /// across a surface the sphere only clips.</para>
        ///
        /// <para>So the painted surface gets its own mesh collider, on a layer that collides with
        /// nothing at all. It exists to be raycast: no physics touches it, and the puppet does not end
        /// up resting against its own skin.</para>
        ///
        /// <para>The mesh is the one the generator built, which is the pose the genome describes — the
        /// same pose the renderer is showing while the creature stands in the editor.</para>
        /// </remarks>
        private static void AddPaintCollider(Renderer renderer)
        {
            Mesh mesh = renderer is SkinnedMeshRenderer skinned
                ? skinned.sharedMesh
                : renderer.GetComponent<MeshFilter>()?.sharedMesh;

            if (mesh == null) return;

            MeshCollider collider = renderer.GetComponent<MeshCollider>();
            if (collider == null) collider = renderer.gameObject.AddComponent<MeshCollider>();

            collider.sharedMesh = mesh;
            collider.convex = false;

            int layer = LayerMask.NameToLayer(PaintLayer);
            if (layer >= 0) renderer.gameObject.layer = layer;
        }

        /// <summary>
        /// Gives every attached part a small canvas of its own.
        /// </summary>
        /// <remarks>
        /// <para>A part is a separate mesh with separate UVs, so it cannot share the body's texture —
        /// it needs its own, and that is a render texture per part. Hence the much lower resolution:
        /// what is being painted is a horn or a leg link, not a flank.</para>
        ///
        /// <para>Seeded from the part's own colour in the genome, so a part that nobody paints looks
        /// exactly as it did before any of this existed.</para>
        ///
        /// <para><b>Legs are left out, and the reason is Paint in 3D's.</b> A leg is unfolded into a
        /// chain by cloning its visuals, and a clone carries copies of the painting components. Cleaning
        /// those copies up releases the render texture the <b>original</b> was painting into — measured:
        /// the leg's canvas is alive on the frame it is made and gone on the next. Legs therefore keep
        /// the colour and coat from their gene, which the palette still sets per part; everything else
        /// takes a brush.</para>
        /// </remarks>
        private void MakePartsPaintable(BuiltCreatureBody body, CreatureGenome genome, PartRuleSet rules)
        {
            _parts.Clear();
            if (body?.PartInstances == null) return;

            foreach (CreaturePartInstance instance in body.PartInstances)
            {
                if (instance.Object == null) continue;
                if (IsLeg(genome, rules, instance.GeneIndex)) continue;

                Color32 tint = genome != null && instance.GeneIndex < genome.PartCount
                    ? genome.GetPart(instance.GeneIndex).ResolveTint(genome.SecondaryColor)
                    : (Color32)Color.white;

                SkinPattern pattern = null;
                if (_palette != null && genome != null && instance.GeneIndex < genome.PartCount)
                    pattern = _palette.Get(genome.GetPart(instance.GeneIndex).PatternId);

                foreach (Renderer renderer in instance.Object.GetComponentsInChildren<Renderer>(true))
                {
                    P3dPaintableTexture texture = MakePaintable(renderer, _partResolution, _partResolution);
                    if (texture == null || texture.Current == null) continue;

                    texture.Clear(pattern != null && pattern.BaseMap != null ? pattern.BaseMap : Texture2D.whiteTexture, tint);
                    _parts.Add(texture);
                }
            }
        }

        /// <summary>
        /// Hands the skin over from the old tinting path to the canvas.
        /// </summary>
        /// <remarks>
        /// <para>The body build paints the carcass through a <c>MaterialPropertyBlock</c>
        /// (<c>CreatureSkinPainter</c>), and a property block <b>overrides the material</b> — including
        /// the very texture Paint in 3D has just bound to <c>_BaseMap</c>. Left in place, every stroke
        /// would land in a texture nobody is drawing: the creature stays flat and the painting looks
        /// broken while working perfectly.</para>
        ///
        /// <para>The material's own colour goes white for the same reason. It multiplies the texture,
        /// and the shipped body material is green — a sand-coloured skin would come out olive.</para>
        /// </remarks>
        private static void TakeOverFromPropertyBlock(Renderer renderer)
        {
            renderer.SetPropertyBlock(null);
            renderer.material.SetColor("_BaseColor", Color.white);
        }

        /// <summary>
        /// Paints a dab at a point on the skin.
        /// </summary>
        /// <param name="worldPoint">Where the cursor hit the carcass.</param>
        /// <param name="radius">Brush radius in world units, so a brush covers the same patch of a big creature as of a small one.</param>
        public void Paint(Vector3 worldPoint, Color color, float radius, float hardness = 3f,
            P3dPaintableTexture target = null)
        {
            if (!IsReady) return;

            P3dPaintSphere brush = Brush();
            brush.Color = color;
            brush.Radius = radius;
            brush.Hardness = hardness;

            // Painting one named texture rather than everything in range: creatures stand close together
            // and share a material, and a brush that painted "whatever is under the cursor" would leave
            // marks on the neighbour. Which texture is decided by what the cursor hit — the carcass, or
            // one of the parts hanging off it.
            brush.TargetTexture = target != null ? target : _texture;

            brush.HandleHitPoint(false, 0, 1f, 0, worldPoint, Quaternion.identity);
        }

        /// <summary>
        /// The canvas belonging to whatever was hit: a part's own, the body's, or none.
        /// </summary>
        /// <remarks>
        /// <c>null</c> means "this cannot be painted" — a leg, which shares its skin with its clones and
        /// therefore has no canvas. Falling back to the body there would be worse than doing nothing:
        /// the player would aim at a leg and leave a mark somewhere on the torso behind it.
        /// </remarks>
        public P3dPaintableTexture CanvasFor(Transform hit)
        {
            if (hit == null) return _texture;

            P3dPaintableTexture found = hit.GetComponentInParent<P3dPaintableTexture>();
            if (found != null) return found;

            // The carcass collider is not on the renderer, so a hit on the body finds no paintable by
            // walking up. What tells the two apart is the grab handle: every part instance carries one,
            // the carcass does not.
            return hit.GetComponentInParent<PartHandle>() == null ? _texture : null;
        }

        /// <summary>
        /// Paints a stroke between two points on the skin.
        /// </summary>
        /// <remarks>
        /// A dab per frame is not a stroke: move the cursor faster than the brush is wide and the trail
        /// comes out dotted. Paint in 3D fills the gap between the two points itself, so a fast drag
        /// still draws a line.
        /// </remarks>
        public void PaintLine(Vector3 from, Vector3 to, Color color, float radius, float hardness = 3f,
            P3dPaintableTexture target = null)
        {
            if (!IsReady) return;

            P3dPaintSphere brush = Brush();
            brush.Color = color;
            brush.Radius = radius;
            brush.Hardness = hardness;
            brush.TargetTexture = target != null ? target : _texture;

            brush.HandleHitLine(false, 0, 1f, 0, from, to, Quaternion.identity, false);
        }

        /// <summary>Floods the whole skin with one colour and coat — the way a creature starts, and the way to start over.</summary>
        public void Fill(Color color, SkinPattern pattern)
        {
            if (!IsReady) return;

            Texture2D mask = pattern != null ? pattern.BaseMap : null;
            _texture.Clear(mask != null ? mask : Texture2D.whiteTexture, color);
        }

        /// <summary>The painted body skin as a PNG.</summary>
        public byte[] ToPng() => IsReady ? _texture.GetPngData() : null;

        /// <summary>Puts a saved body skin back on the creature.</summary>
        public void LoadPng(byte[] data)
        {
            if (!IsReady || data == null || data.Length == 0) return;

            _texture.LoadFromData(data);
        }

        /// <summary>
        /// Everything painted on this creature, in one blob: the body first, then every part.
        /// </summary>
        /// <remarks>
        /// <para>One blob rather than a picture per canvas, because everything that carries a skin —
        /// the preset file, the chunked network transfer — then carries all of it or none of it. A
        /// creature whose legs arrived and whose body did not would be worse than one still wearing its
        /// base coat.</para>
        ///
        /// <para>The parts are in build order, which is derived from the genome: same genome, same
        /// order, on every machine. The genome travels with the skin in both directions, so the two
        /// cannot disagree.</para>
        /// </remarks>
        public byte[] ToBundle()
        {
            if (!IsReady) return null;

            var pictures = new List<byte[]> { _texture.GetPngData() };
            foreach (P3dPaintableTexture part in _parts)
                pictures.Add(part != null ? part.GetPngData() : null);

            using var stream = new System.IO.MemoryStream();
            using var writer = new System.IO.BinaryWriter(stream);

            writer.Write(pictures.Count);
            foreach (byte[] picture in pictures)
            {
                writer.Write(picture?.Length ?? 0);
                if (picture != null) writer.Write(picture);
            }

            return stream.ToArray();
        }

        /// <summary>
        /// Puts a whole painted creature back on: body and parts.
        /// </summary>
        /// <remarks>
        /// A blob written by a creature with more parts than this one has is read as far as it fits and
        /// no further. That is the case when a preset is loaded onto a body that has since been
        /// rebuilt — and it has to end in a creature missing some paint, never in an exception.
        /// </remarks>
        public void LoadBundle(byte[] bundle)
        {
            if (!IsReady || bundle == null || bundle.Length < sizeof(int)) return;

            try
            {
                using var stream = new System.IO.MemoryStream(bundle);
                using var reader = new System.IO.BinaryReader(stream);

                int count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    int length = reader.ReadInt32();
                    if (length <= 0) continue;

                    byte[] picture = reader.ReadBytes(length);

                    if (i == 0) LoadPng(picture);
                    else if (i - 1 < _parts.Count && _parts[i - 1] != null) _parts[i - 1].LoadFromData(picture);
                }
            }
            catch (System.IO.EndOfStreamException)
            {
                Debug.LogWarning("The painted skin ended sooner than it said it would — the creature is wearing what arrived.", this);
            }
        }

        /// <summary>
        /// The base coat: the genome's colour and pattern, painted into the texture rather than set on
        /// the material.
        /// </summary>
        /// <remarks>
        /// This is the bridge between the two worlds. The palette the player already had keeps working —
        /// it now fills the canvas instead of tinting a renderer — and everything painted afterwards
        /// lands on top of it.
        /// </remarks>
        private void SeedFromGenome(CreatureGenome genome)
        {
            if (genome == null) return;

            SkinPattern pattern = _palette != null ? _palette.Get(genome.BodyPattern) : null;
            Fill(genome.PrimaryColor, pattern);
        }

        /// <summary>
        /// The brush, made once and kept.
        /// </summary>
        /// <remarks>
        /// P3D's paint components are MonoBehaviours that read their settings off themselves, so a brush
        /// is a GameObject. One per creature, reconfigured per stroke — building one per dab would
        /// allocate a GameObject for every frame the player holds the mouse down.
        /// </remarks>
        private P3dPaintSphere Brush()
        {
            if (_brush != null) return _brush;

            var brushObject = new GameObject("SkinBrush");
            brushObject.transform.SetParent(transform, false);

            _brush = brushObject.AddComponent<P3dPaintSphere>();
            _brush.BlendMode = P3dBlendMode.AlphaBlend(Vector4.one);

            return _brush;
        }
    }
}
