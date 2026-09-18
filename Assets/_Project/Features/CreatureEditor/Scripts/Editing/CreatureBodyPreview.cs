using System.Collections.Generic;
using Leeway.Creature.Domain;
using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// A non-networked preview body. In the editor it is what you sculpt on (a full rebuild on every
    /// change), and outside play mode it lets the body generator be tested in isolation, before any
    /// networking is involved at all.
    /// </summary>
    /// <remarks>
    /// Rebuilding wholesale instead of updating incrementally is deliberate: at ~400 vertices it is
    /// free, and it removes a whole class of inconsistent-state bugs.
    /// </remarks>
    public class CreatureBodyPreview : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private CreaturePartCatalog _catalog;
        [SerializeField] private CreatureBodyBuildSettings _settings;
        [SerializeField] private CreatureStatTuning _tuning = CreatureStatTuning.Default;

        [Tooltip("The raycast target for \"click on the torso\" in the editor. The preview simulates no physics, so this is a trigger only.")]
        [SerializeField] private CapsuleCollider _corpusCollider;

        [Header("Preview")]
        [SerializeField] private int _starterSeed = 1;
        [SerializeField] private bool _drawBoneGizmos = true;

        private CreatureGenome _genome;
        private BuiltCreatureBody _body;
        private bool _isVisible = true;

        /// <summary>
        /// The legs of the creature being sculpted, placed in a standing pose.
        /// </summary>
        /// <remarks>
        /// The preview does not walk, but it <b>has to</b> show the leg exactly as it will run around
        /// the world a moment later. Previously sculpting showed the part's authored link — a single
        /// capsule — while the playground unfolded it into a bending chain, and the player had no way
        /// of knowing the two were the same leg.
        /// </remarks>
        private readonly List<ProceduralLeg> _legs = new();

        /// <summary>The sculpted creature's legs — the editor reads the joint positions from them for its handles.</summary>
        public IReadOnlyList<ProceduralLeg> Legs => _legs;

        /// <summary>Whether the sculpted body is currently shown.</summary>
        public bool IsVisible => _isVisible;

        public CreatureGenome Genome => _genome;
        public BuiltCreatureBody Body => _body;
        public CreaturePartCatalog Catalog => _catalog;
        public PartRuleSet Rules => _catalog != null ? _catalog.BuildRuleSet() : PartRuleSet.Empty;

        /// <summary>Raised after every rebuild — the HUD hangs its stat readout off this.</summary>
        public event System.Action<CreatureGenome, CreatureStats> Rebuilt;

        public void SetGenome(CreatureGenome genome)
        {
            _genome = genome;
            Rebuild();
        }

        /// <summary>
        /// Hides or shows the sculpted body along with its anchor collider.
        /// </summary>
        /// <remarks>
        /// We deliberately do <b>not</b> switch off the whole preview object: <see cref="CreatureEditorSession"/>
        /// sits on it with the working genome, and a disabled object also disappears from find-by-type.
        /// So we hide the visuals alone.
        /// </remarks>
        public void SetVisible(bool visible)
        {
            _isVisible = visible;

            if (_corpusCollider != null) _corpusCollider.enabled = visible;
            if (_body == null) return;

            if (_body.Renderer != null) _body.Renderer.enabled = visible;

            foreach (CreaturePartInstance part in _body.PartInstances)
                if (part.Object != null) part.Object.SetActive(visible);
        }

        public void Rebuild()
        {
            // The legs go before the body: they hold the parts' authored visuals, which have to return
            // under their own hips before anything is destroyed.
            CreatureLegFactory.Dispose(_legs);

            _body?.Dispose();
            _body = null;

            if (_genome == null) return;

            _body = CreatureBodyBuilder.Build(_genome, _catalog, _settings, transform, _tuning);
            if (_body == null) return;

            if (_corpusCollider != null)
                CreatureColliderFitter.Fit(_corpusCollider, _genome, CreatureRigBuilder.ComputeBoneToRoot(_genome), _settings);

            // A rebuild creates the renderer and the parts afresh, so they have to inherit the current
            // visibility state — otherwise a hidden preview would come back on screen.
            if (!_isVisible) SetVisible(false);

            // The order matters: the part grab handles fit themselves to the instances' bounds, and they
            // listen to exactly this event. Unfolding the leg only afterwards leaves the handle on the
            // authored link at the hip, instead of inflating it along the whole chain and stealing
            // clicks from neighbouring parts.
            Rebuilt?.Invoke(_genome, _body.Stats);

            BuildLegStance();
        }

        /// <summary>Unfolds the locomotion parts into chains and places them in a resting pose.</summary>
        private void BuildLegStance()
        {
            CreatureLegFactory.Build(_genome, Rules, _body, _legs);

            Vector3 forward = transform.rotation * SpineAnchor.Forward;
            foreach (ProceduralLeg leg in _legs) leg.Rest(forward);
        }

        [ContextMenu("Rebuild from starter genome")]
        public void RebuildFromStarter()
        {
            _genome = StarterGenomeFactory.Create(_starterSeed, Rules);
            Rebuild();
        }

        [ContextMenu("Add vertebra at the end")]
        public void AddTailVertebra()
        {
            if (_genome == null) return;

            if (GenomeEditOperations.TryAddVertebra(_genome, _genome.VertebraCount - 1, Rules, out GenomeError error))
                Rebuild();
            else
                Debug.LogWarning($"Vertebra not added: {error}", this);
        }

        [ContextMenu("Remove last vertebra")]
        public void RemoveTailVertebra()
        {
            if (_genome == null) return;

            if (GenomeEditOperations.TryRemoveVertebra(_genome, _genome.VertebraCount - 1, Rules, out GenomeError error))
                Rebuild();
            else
                Debug.LogWarning($"Vertebra not removed: {error}", this);
        }

        /// <summary>
        /// A rigging consistency check. A mismatch between the bone array and the bindpose array is the
        /// most common reason a runtime-generated skinned mesh turns into spaghetti — better to find
        /// that out from a single click.
        /// </summary>
        [ContextMenu("Check rigging consistency")]
        public void ValidateRig()
        {
            if (_body == null)
            {
                Debug.LogWarning("No body built — rebuild first.", this);
                return;
            }

            Mesh mesh = _body.GeneratedMesh;
            int bones = _body.Bones.Length;

            Debug.Log($"Bones: {bones} | bindposes: {mesh.bindposes.Length} | renderer.bones: {_body.Renderer.bones.Length} | " +
                      $"vertices: {mesh.vertexCount} | triangles: {mesh.triangles.Length / 3} | stats: {_body.Stats}", this);

            if (mesh.bindposes.Length != bones || _body.Renderer.bones.Length != bones)
                Debug.LogError("Bone and bindpose counts do not match — the skinning will be distorted.", this);

            if (mesh.boneWeights.Length != mesh.vertexCount)
                Debug.LogError("The bone weight count does not match the vertex count.", this);
        }

        private void OnDestroy()
        {
            CreatureLegFactory.Dispose(_legs);
            _body?.Dispose();
        }

        private void OnDrawGizmosSelected()
        {
            if (!_drawBoneGizmos || _body == null || _body.Bones == null) return;

            Gizmos.color = Color.cyan;
            for (int i = 0; i < _body.Bones.Length; i++)
            {
                Transform bone = _body.Bones[i];
                if (bone == null) continue;

                Gizmos.DrawWireSphere(bone.position, 0.04f);
                if (i > 0 && _body.Bones[i - 1] != null)
                    Gizmos.DrawLine(_body.Bones[i - 1].position, bone.position);
            }
        }
    }
}
