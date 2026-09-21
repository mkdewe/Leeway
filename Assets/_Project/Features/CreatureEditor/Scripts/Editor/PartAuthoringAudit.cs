using System;
using System.Collections.Generic;
using System.Linq;
using FishNet.Object;
using Leeway.Creature.Domain;
using UnityEditor;
using UnityEngine;

namespace Leeway.CreatureEditor.Authoring
{
    public enum PartIssueKind
    {
        /// <summary>The part will not work: it cannot be attached, cannot be shown, or will break the body.</summary>
        Error,

        /// <summary>It will work, but not the way its author thinks.</summary>
        Warning,
    }

    /// <summary>One audit finding, together with a fix if one can be applied mechanically.</summary>
    public sealed class PartIssue
    {
        public PartIssue(UnityEngine.Object context, PartIssueKind kind, string message,
            string fixLabel = null, Action fix = null)
        {
            Context = context;
            Kind = kind;
            Message = message;
            FixLabel = fixLabel;
            Fix = fix;
        }

        /// <summary>The asset to ping in the project when a report row is clicked.</summary>
        public UnityEngine.Object Context { get; }

        public PartIssueKind Kind { get; }
        public string Message { get; }

        /// <summary>The label on the fix button. <c>null</c> when this needs a human hand.</summary>
        public string FixLabel { get; }

        public Action Fix { get; }
        public bool CanFix => Fix != null;
    }

    /// <summary>
    /// Checks the part catalog for the things that break quietly in this project.
    /// </summary>
    /// <remarks>
    /// <para><b>Why a machine, when you can see it.</b> You cannot. A part with geometry along the
    /// wrong axis looks like a part in the preview, it just sticks out sideways. A leg with a
    /// mismatched <c>SegmentLength</c> only goes wrong in the playground, not in the editor where it
    /// was attached. A <c>NetworkObject</c> on a part prefab only speaks up when a second player joins.
    /// Each of those, meanwhile, is one number or one <c>GetComponentInChildren</c>.</para>
    ///
    /// <para><b>Why this is not a test.</b> Tests guard what is already correct against regression. The
    /// audit is for <b>adding</b> parts and says outright what is missing, and it can fix half the
    /// findings itself. Neither replaces the other — which is why <c>PartCatalogAuditTests</c> calls
    /// exactly this code instead of restating it in its own words.</para>
    /// </remarks>
    public static class PartAuthoringAudit
    {
        /// <summary>By what percentage <c>SegmentLength</c> may differ from the model's real length.</summary>
        private const float SegmentLengthTolerance = 0.1f;

        /// <summary>The key prefixes assigned to categories. Kept here because it is a naming convention, not a domain rule.</summary>
        private static readonly Dictionary<PartCategory, string> KeyPrefixes = new()
        {
            [PartCategory.Locomotion] = "loco",
            [PartCategory.Mouth] = "mouth",
            [PartCategory.Sense] = "sense",
            [PartCategory.Grasper] = "grasp",
            [PartCategory.Weapon] = "weapon",
            [PartCategory.Detail] = "detail",
        };

        public static List<PartIssue> Inspect(CreaturePartCatalog catalog)
        {
            var issues = new List<PartIssue>();

            if (catalog == null)
            {
                issues.Add(new PartIssue(null, PartIssueKind.Error, "No part catalog found in the project."));
                return issues;
            }

            foreach (CreaturePartDefinition part in catalog.Parts)
            {
                if (part == null)
                {
                    issues.Add(new PartIssue(catalog, PartIssueKind.Error,
                        "The catalog has an empty entry — remove it, because every read skips it in silence."));
                    continue;
                }

                InspectPart(part, issues);
            }

            ReportUnregistered(catalog, issues);
            return issues;
        }

        private static void InspectPart(CreaturePartDefinition part, List<PartIssue> issues)
        {
            InspectKey(part, issues);

            if (part.Prefab == null)
            {
                issues.Add(new PartIssue(part, PartIssueKind.Error,
                    $"{part.PartKey}: no prefab — the instantiator will skip this part and its tile will have no preview."));
                return;
            }

            PartGeometry geometry = PartGeometry.Measure(part.Prefab);

            if (!geometry.HasVisibleMesh)
            {
                issues.Add(new PartIssue(part.Prefab, PartIssueKind.Error,
                    $"{part.PartKey}: the prefab has not a single visible mesh — the tile will fall back to its name and cost."));
                return;
            }

            if (geometry.HasHiddenMesh)
            {
                issues.Add(new PartIssue(part.Prefab, PartIssueKind.Warning,
                    $"{part.PartKey}: the prefab carries disabled geometry. The thumbnail frame counts only the visible " +
                    "geometry, while the leg link length counts all of it. Those two numbers will be talking about different things."));
            }

            InspectAxis(part, geometry, issues);
            InspectHierarchy(part, issues);
            InspectLeg(part, geometry, issues);
        }

        /// <summary>
        /// A part's axis is <c>+Z</c> — and that is not a matter of style.
        /// </summary>
        /// <remarks>
        /// <c>PartOrientation.LookOutward</c> composes the rotation through <c>Quaternion.LookRotation</c>,
        /// so it is <c>+Z</c> that aims away from the body, and for a locomotion part, at the ground. A
        /// model built along a different axis will stick out sideways, and one built along <c>−Z</c>
        /// will grow into the body instead of out of it.
        /// </remarks>
        private static void InspectAxis(CreaturePartDefinition part, PartGeometry geometry, List<PartIssue> issues)
        {
            Bounds local = geometry.Local;

            if (local.max.z <= 0.001f)
            {
                issues.Add(new PartIssue(part.Prefab, PartIssueKind.Error,
                    $"{part.PartKey}: all the geometry lies on the negative side of Z (down to {local.min.z:F3}). " +
                    "The model is built backwards — it will grow into the body instead of out of it."));
                return;
            }

            if (-local.min.z > local.max.z)
            {
                issues.Add(new PartIssue(part.Prefab, PartIssueKind.Warning,
                    $"{part.PartKey}: the geometry reaches further back ({-local.min.z:F3}) than forward ({local.max.z:F3}). " +
                    "The part will be more sunk into the body than sticking out of it — deliberate only for an eye, antenna or nostril."));
            }

            float longest = Mathf.Max(local.size.x, Mathf.Max(local.size.y, local.size.z));
            if (local.size.z < longest * 0.5f && part.Category == PartCategory.Locomotion)
            {
                issues.Add(new PartIssue(part.Prefab, PartIssueKind.Warning,
                    $"{part.PartKey}: this locomotion part is shortest along Z (X {local.size.x:F3}, Y {local.size.y:F3}, Z {local.size.z:F3}). " +
                    "The leg chain stretches its links along Z precisely — this model will come apart."));
            }
        }

        private static void InspectHierarchy(CreaturePartDefinition part, List<PartIssue> issues)
        {
            if (part.Prefab.GetComponentInChildren<NetworkObject>(true) != null)
            {
                issues.Add(new PartIssue(part.Prefab, PartIssueKind.Error,
                    $"{part.PartKey}: the prefab has a NetworkObject. Parts are reconstructed by every client from the " +
                    "genome — spawning them over the network means a duplicate body and wasted bandwidth."));
            }

            foreach (Transform node in part.Prefab.GetComponentsInChildren<Transform>(true))
            {
                Vector3 scale = node.localScale;
                if (scale.x >= 0f && scale.y >= 0f && scale.z >= 0f) continue;

                issues.Add(new PartIssue(part.Prefab, PartIssueKind.Error,
                    $"{part.PartKey}: negative scale on \"{node.name}\" ({scale}). " +
                    "It flips the triangle winding, breaks the lighting and turns colliders inside out. " +
                    "Symmetry is done by the instantiator, recomputing the position — never by scale."));
                break;
            }
        }

        /// <summary>
        /// How far the limb reaches once the foot the catalog fits by default is in its socket.
        /// </summary>
        /// <remarks>
        /// Measured by actually assembling the thing, because the socket carries the limb's own scale
        /// and its own rotation — reproducing that arithmetic here would be a second implementation of
        /// the instantiator, free to disagree with it.
        /// </remarks>
        private static float AssembledReach(CreaturePartDefinition part)
        {
            if (!part.AcceptsFitting || part.DefaultFitting == null || part.DefaultFitting.Prefab == null) return 0f;
            if (part.Prefab == null) return 0f;

            GameObject limb = UnityEngine.Object.Instantiate(part.Prefab);
            try
            {
                Transform socket = null;
                foreach (Transform node in limb.GetComponentsInChildren<Transform>(true))
                    if (node.name == CreaturePartDefinition.SocketName) socket = node;

                if (socket == null) return 0f;

                GameObject foot = UnityEngine.Object.Instantiate(part.DefaultFitting.Prefab, socket);
                foot.transform.localPosition = Vector3.zero;
                foot.transform.localRotation = Quaternion.identity;
                foot.transform.localScale = Vector3.one;

                float reach = 0f;
                foreach (MeshFilter filter in limb.GetComponentsInChildren<MeshFilter>(true))
                {
                    Mesh mesh = filter.sharedMesh;
                    if (mesh == null) continue;

                    Bounds b = mesh.bounds;
                    for (int corner = 0; corner < 8; corner++)
                    {
                        var point = new Vector3(
                            (corner & 1) == 0 ? b.min.x : b.max.x,
                            (corner & 2) == 0 ? b.min.y : b.max.y,
                            (corner & 4) == 0 ? b.min.z : b.max.z);

                        reach = Mathf.Max(reach, limb.transform.InverseTransformPoint(filter.transform.TransformPoint(point)).z);
                    }
                }

                return reach;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(limb);
            }
        }

        private static void InspectLeg(CreaturePartDefinition part, PartGeometry geometry, List<PartIssue> issues)
        {
            if (part.Category != PartCategory.Locomotion) return;

            // A limb is measured with its foot in: the leg model ends at the ankle, but what the
            // creature stands on is the assembled limb, and that is what the chain's length describes.
            float authored = Mathf.Max(geometry.ReachAlongZ, AssembledReach(part));
            if (authored <= LegLimits.MinSegmentLength) return;

            // A whole-limb model spans the entire chain, not one link of it: the model's reach has to
            // match the sum of the links, or the skinned leg walks at a length it was not drawn at.
            int links = part.WholeLimb ? Mathf.Max(1, part.Leg.SegmentCount) : 1;
            float authoredPerLink = authored / links;

            float declared = part.SegmentLength;
            float drift = Mathf.Abs(declared - authoredPerLink) / Mathf.Max(authoredPerLink, 0.0001f);
            if (drift <= SegmentLengthTolerance) return;

            float clamped = Mathf.Clamp(authoredPerLink, LegLimits.MinSegmentLength, LegLimits.MaxSegmentLength);
            CreaturePartDefinition captured = part;

            // The fix goes towards the model, because it is the only one of the two that can be done
            // with a button — but it is not the right one a priori. Lengthening the link changes the
            // leg's reach, and through it the stance height and the stride, i.e. the gameplay. The other
            // route is to shorten the model. That choice belongs to the author, not to the audit.
            issues.Add(new PartIssue(part, PartIssueKind.Warning,
                $"{part.PartKey}: SegmentLength is {declared:F3} while the model reaches {authored:F3} along Z" +
                (links > 1 ? $" over {links} links, i.e. {authoredPerLink:F3} each" : string.Empty) +
                $" ({drift * 100f:F0}% apart). The chain squeezes or stretches the link by that factor, " +
                $"so the leg looks different from the attached model. Either set {clamped:F3} — " +
                "which changes the leg's reach, and so the stance height and the stride — or shorten the model to " +
                $"{declared:F3} and leave the gameplay alone.",
                $"Set {clamped:F3}",
                () => SetSegmentLength(captured, clamped)));
        }

        private static void InspectKey(CreaturePartDefinition part, List<PartIssue> issues)
        {
            if (string.IsNullOrWhiteSpace(part.PartKey)) return;

            if (!KeyPrefixes.TryGetValue(part.Category, out string expected)) return;

            if (part.PartKey.StartsWith(expected + ".", StringComparison.Ordinal)) return;

            issues.Add(new PartIssue(part, PartIssueKind.Warning,
                $"{part.PartKey}: category {part.Category} conventionally carries the prefix \"{expected}.\". " +
                "Do not change the key if the part is already in saved genomes — the hash is their address."));
        }

        /// <summary>
        /// A definition outside the catalog is invisible: it is not in the palette and no genome can resolve it.
        /// </summary>
        private static void ReportUnregistered(CreaturePartCatalog catalog, List<PartIssue> issues)
        {
            var registered = new HashSet<CreaturePartDefinition>(catalog.Parts.Where(part => part != null));

            foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(CreaturePartDefinition)}"))
            {
                var part = AssetDatabase.LoadAssetAtPath<CreaturePartDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (part == null || registered.Contains(part)) continue;

                CreaturePartDefinition captured = part;

                issues.Add(new PartIssue(part, PartIssueKind.Warning,
                    $"{part.PartKey}: the definition is in the project but not in the catalog — the palette will not " +
                    "show it, and no genome can resolve its identifier.",
                    "Add to catalog",
                    () => Register(catalog, captured)));
            }
        }

        /// <summary>Adds a definition to the catalog. Public, because the part creator uses it too.</summary>
        public static void Register(CreaturePartCatalog catalog, CreaturePartDefinition part)
        {
            if (catalog == null || part == null) return;

            var serialized = new SerializedObject(catalog);
            SerializedProperty parts = serialized.FindProperty("_parts");

            for (int i = 0; i < parts.arraySize; i++)
            {
                if (parts.GetArrayElementAtIndex(i).objectReferenceValue == part) return;
            }

            parts.InsertArrayElementAtIndex(parts.arraySize);
            parts.GetArrayElementAtIndex(parts.arraySize - 1).objectReferenceValue = part;

            serialized.ApplyModifiedProperties();
            AssetDatabase.SaveAssetIfDirty(catalog);
        }

        /// <summary>
        /// The field is a property with <c>[field: SerializeField]</c>, so in the file it sits under the
        /// compiler-generated backing field's name.
        /// </summary>
        private static void SetSegmentLength(CreaturePartDefinition part, float value)
        {
            var serialized = new SerializedObject(part);
            serialized.FindProperty("<SegmentLength>k__BackingField").floatValue = value;
            serialized.ApplyModifiedProperties();
            AssetDatabase.SaveAssetIfDirty(part);
        }

        /// <summary>The project's part catalog. <c>null</c> when there is none — the caller has to handle that.</summary>
        public static CreaturePartCatalog FindCatalog()
        {
            string[] found = AssetDatabase.FindAssets($"t:{nameof(CreaturePartCatalog)}");
            return found.Length == 0
                ? null
                : AssetDatabase.LoadAssetAtPath<CreaturePartCatalog>(AssetDatabase.GUIDToAssetPath(found[0]));
        }
    }
}
