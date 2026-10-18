using System.Collections.Generic;
using UnityEngine;

namespace Leeway.Rendering
{
    /// <summary>
    /// Reads and builds the levels of detail of a model from how it is named, so that a model made by an artist
    /// needs no set-up in Unity: put <c>_LOD1</c> after the name of its simpler meshes and they are used at distance.
    /// </summary>
    public static class LodConvention
    {
        /// <summary>Marks a renderer that belongs to every level.</summary>
        public const int Shared = -1;

        /// <summary>
        /// The level each renderer of the model belongs to: from its LODGroup if it has one, otherwise from the
        /// <c>_LOD</c> suffix of its name or of a node above it, otherwise <see cref="Shared"/>.
        /// </summary>
        public static Dictionary<Renderer, int> LevelsOf(GameObject model)
        {
            var levels = new Dictionary<Renderer, int>();

            LODGroup group = model.GetComponent<LODGroup>();
            if (group != null)
            {
                LOD[] authored = group.GetLODs();
                for (int i = 0; i < authored.Length; i++)
                {
                    foreach (Renderer renderer in authored[i].renderers)
                    {
                        if (renderer == null) continue;
                        // In the first level and others too: the same mesh at every distance.
                        levels[renderer] = levels.ContainsKey(renderer) ? Shared : i;
                    }
                }
                return levels;
            }

            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
                levels[renderer] = LevelFromName(renderer.transform, model.transform);
            return levels;
        }

        /// <summary>The number from a <c>_LOD</c> suffix on the node or the nodes above it, or <see cref="Shared"/>.</summary>
        public static int LevelFromName(Transform node, Transform root)
        {
            for (Transform t = node; t != null; t = t == root ? null : t.parent)
            {
                int at = t.name.LastIndexOf(LodProfile.Suffix, System.StringComparison.OrdinalIgnoreCase);
                if (at < 0) continue;
                if (int.TryParse(t.name.Substring(at + LodProfile.Suffix.Length), out int level) && level >= 0) return level;
            }
            return Shared;
        }

        /// <summary>
        /// Gives an instantiated model a LODGroup if it has none: its named levels in order, and - when it has only one -
        /// the distance beyond which it is not drawn. A model with its own LODGroup is left alone.
        /// </summary>
        /// <returns>The group, or null if the model has no renderers to group.</returns>
        public static LODGroup Ensure(GameObject instance, bool cullWhenSmall = true)
        {
            if (instance.TryGetComponent(out LODGroup existing)) return existing;

            Dictionary<Renderer, int> levels = LevelsOf(instance);
            if (levels.Count == 0) return null;

            // Levels are used in order whatever their numbers: LOD0 and LOD2 with no LOD1 are two levels.
            var distinct = new SortedSet<int>();
            foreach (int level in levels.Values) if (level != Shared) distinct.Add(level);
            if (distinct.Count == 0) distinct.Add(0);
            var order = new List<int>(distinct);
            int count = order.Count;

            LodProfile profile = LodProfile.Current;

            // Each level lasts until the object is as small on screen as its number says; the last number is where it is culled.
            var lods = new LOD[count];
            for (int i = 0; i < count; i++)
            {
                var renderers = new List<Renderer>();
                foreach (KeyValuePair<Renderer, int> pair in levels)
                    if (pair.Value == order[i] || pair.Value == Shared) renderers.Add(pair.Key);

                bool last = i == count - 1;
                float height = last ? (cullWhenSmall ? profile.CullHeight : 0f) : profile.TransitionAfter(i);
                lods[i] = new LOD(height, renderers.ToArray());
            }

            var group = instance.AddComponent<LODGroup>();
            group.SetLODs(lods);
            group.fadeMode = LODFadeMode.None;
            group.RecalculateBounds();
            return group;
        }
    }
}
