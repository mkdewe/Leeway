using UnityEngine;

namespace Leeway.CreatureEditor.Authoring
{
    /// <summary>
    /// Measures a part prefab's bounds in the space of its root.
    /// </summary>
    /// <remarks>
    /// <para><b>The same arithmetic as the body build.</b> <c>CreaturePartInstantiator.SeatDepth</c>
    /// takes the smallest <c>z</c> to seat the part against the skin, and
    /// <c>ProceduralLeg.MeasureLength</c> the largest, to learn the authored length of a leg link.
    /// Both compute over the corners of the mesh bounds transformed into root space — and this is
    /// exactly the same, so the audit talks about the numbers the game actually lives by rather than
    /// about an approximation of its own.</para>
    ///
    /// <para>We compute over the <b>corners of <c>mesh.bounds</c></b> rather than over vertices,
    /// because that is what the production code does. Whether the mesh bounds themselves cover the
    /// vertices is checked separately by <c>PartThumbnailFramingTests</c> — a different question, at a
    /// different cost.</para>
    /// </remarks>
    public readonly struct PartGeometry
    {
        /// <summary>Whether the prefab has anything the camera will draw.</summary>
        public readonly bool HasVisibleMesh;

        /// <summary>The bounds of the visible geometry in the prefab root's space.</summary>
        public readonly Bounds Local;

        /// <summary>Whether the prefab carries geometry nobody will see — disabled, or on an inactive object.</summary>
        public readonly bool HasHiddenMesh;

        private PartGeometry(bool hasVisibleMesh, Bounds local, bool hasHiddenMesh)
        {
            HasVisibleMesh = hasVisibleMesh;
            Local = local;
            HasHiddenMesh = hasHiddenMesh;
        }

        /// <summary>How far the geometry reaches along <c>+Z</c> — the authored length of a leg link.</summary>
        public float ReachAlongZ => HasVisibleMesh ? Mathf.Max(0f, Local.max.z) : 0f;

        /// <summary>How far behind the root the geometry starts — the instantiator seats the part against the skin by that much.</summary>
        public float SeatDepth => HasVisibleMesh ? Mathf.Max(0f, Local.min.z) : 0f;

        public static PartGeometry Measure(GameObject prefab)
        {
            if (prefab == null) return new PartGeometry(false, default, false);

            Transform root = prefab.transform;
            var bounds = new Bounds();
            bool any = false;
            bool hidden = false;

            foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null) continue;

                if (!IsVisible(filter, root))
                {
                    hidden = true;
                    continue;
                }

                Bounds local = mesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    var point = new Vector3(
                        (corner & 1) == 0 ? local.min.x : local.max.x,
                        (corner & 2) == 0 ? local.min.y : local.max.y,
                        (corner & 4) == 0 ? local.min.z : local.max.z);

                    Vector3 inRoot = root.InverseTransformPoint(filter.transform.TransformPoint(point));

                    if (!any)
                    {
                        bounds = new Bounds(inRoot, Vector3.zero);
                        any = true;
                        continue;
                    }

                    bounds.Encapsulate(inRoot);
                }
            }

            return new PartGeometry(any, bounds, hidden);
        }

        /// <summary>
        /// Whether this mesh will make it to the screen.
        /// </summary>
        /// <remarks>
        /// On a prefab in the project <c>activeInHierarchy</c> is always false — the prefab stands in
        /// no scene. So the <c>activeSelf</c> chain has to be walked by hand, up to the prefab root.
        /// </remarks>
        private static bool IsVisible(Component component, Transform root)
        {
            if (!component.TryGetComponent(out Renderer renderer) || !renderer.enabled) return false;

            for (Transform node = component.transform; node != null; node = node.parent)
            {
                if (!node.gameObject.activeSelf) return false;
                if (node == root) break;
            }

            return true;
        }
    }
}
