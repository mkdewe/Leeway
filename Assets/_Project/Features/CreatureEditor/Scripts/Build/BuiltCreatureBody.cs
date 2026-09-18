using System;
using Leeway.Creature.Domain;
using UnityEngine;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// The result of one body build: the bone hierarchy, the generated mesh, the part instances and
    /// the derived stats. The owner is responsible for calling <see cref="Dispose"/> before every
    /// rebuild.
    /// </summary>
    public sealed class BuiltCreatureBody : IDisposable
    {
        public Transform Armature { get; }
        public Transform[] Bones { get; }
        public SkinnedMeshRenderer Renderer { get; }

        /// <summary>A procedurally generated mesh — it is not an asset, so it has to be released by hand.</summary>
        public Mesh GeneratedMesh { get; }

        public CreaturePartInstance[] PartInstances { get; }
        public CreatureStats Stats { get; }

        private bool _disposed;

        public BuiltCreatureBody(Transform armature, Transform[] bones, SkinnedMeshRenderer renderer,
            Mesh generatedMesh, CreaturePartInstance[] partInstances, CreatureStats stats)
        {
            Armature = armature;
            Bones = bones;
            Renderer = renderer;
            GeneratedMesh = generatedMesh;
            PartInstances = partInstances;
            Stats = stats;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (PartInstances != null)
            {
                for (int i = 0; i < PartInstances.Length; i++)
                    DestroySafely(PartInstances[i].Object);
            }

            if (Renderer != null) DestroySafely(Renderer.gameObject);
            if (Armature != null) DestroySafely(Armature.gameObject);

            // Without this, one mesh would leak per commit.
            DestroySafely(GeneratedMesh);
        }

        /// <summary>The editor calls this outside play mode, where <c>Destroy</c> is deferred and the object would survive to the end of the frame.</summary>
        private static void DestroySafely(UnityEngine.Object target)
        {
            if (target == null) return;

            if (Application.isPlaying) UnityEngine.Object.Destroy(target);
            else UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
