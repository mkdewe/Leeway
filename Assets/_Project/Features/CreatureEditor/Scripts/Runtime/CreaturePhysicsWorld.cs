using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Leeway.CreatureEditor
{
    /// <summary>
    /// A physics scene of its own for the creatures' puppets, and a copy of the world for them to hit.
    /// </summary>
    /// <remarks>
    /// <para><b>Why the puppets move out.</b> FishNet's prediction replays a tick by resimulating
    /// physics several times, and it does that to the <b>whole</b> scene. A chain of jointed bodies
    /// does not survive being stepped repeatedly without having its state restored — the ragdoll runs
    /// several times too fast and tears itself apart. The way out, which the old ragdoll's own comment
    /// already pointed at, is a scene the network does not touch, stepped once per physics frame by
    /// hand.</para>
    ///
    /// <para><b>Why the world is copied into it.</b> A physics scene is sealed: bodies in it collide
    /// with nothing outside. So the arena's static colliders are mirrored in — collider-only ghosts,
    /// no meshes, no renderers — and a creature that falls over lands on the floor it can see.
    /// Everything that moves stays where it was: only the immovable world is worth copying.</para>
    ///
    /// <para><b>Creature against creature still works</b>, and works better: every puppet lives in this
    /// one scene, so muscles meet muscles here rather than in the middle of the prediction loop.</para>
    ///
    /// <para>Stepped late (<see cref="DefaultExecutionOrder"/>) so PuppetMaster has already written its
    /// muscle drives for this frame — simulating first would advance the bodies a frame behind the pose
    /// they are being pulled towards.</para>
    /// </remarks>
    [DefaultExecutionOrder(1000)]
    public class CreaturePhysicsWorld : MonoBehaviour
    {
        [Tooltip("Which layers are copied in as the world the puppets collide with. Static colliders only.")]
        [SerializeField] private LayerMask _environmentLayers = 1;

        [Tooltip("Rebuild the copy of the world whenever this many seconds pass. 0 copies it once, at startup.")]
        [SerializeField, Min(0f)] private float _refreshSeconds;

        private Scene _scene;
        private PhysicsScene _physics;
        private bool _created;
        private float _nextRefresh;

        private readonly List<GameObject> _ghosts = new();

        /// <summary>Whether the separate scene exists and can be stepped.</summary>
        public bool IsReady => _created && _scene.IsValid();

        private void Awake()
        {
            _scene = SceneManager.CreateScene("CreaturePhysics",
                new CreateSceneParameters(LocalPhysicsMode.Physics3D));

            _created = _scene.IsValid();
            if (!_created)
            {
                Debug.LogError("Could not create the creatures' physics scene — puppets will stay in the shared one.", this);
                return;
            }

            _physics = _scene.GetPhysicsScene();
            MirrorEnvironment();
        }

        private void OnDestroy()
        {
            if (!_created) return;

            // The scene owns the ghosts and the puppets that were moved into it, so unloading it is the
            // whole teardown. Guarded because on the way out of play mode Unity may have unloaded it
            // already.
            if (_scene.IsValid() && _scene.isLoaded) SceneManager.UnloadSceneAsync(_scene);
            _created = false;
        }

        private void FixedUpdate()
        {
            if (!IsReady) return;

            _physics.Simulate(Time.fixedDeltaTime);

            if (_refreshSeconds <= 0f || Time.time < _nextRefresh) return;

            _nextRefresh = Time.time + _refreshSeconds;
            MirrorEnvironment();
        }

        /// <summary>
        /// Moves a puppet into the separate scene.
        /// </summary>
        /// <remarks>
        /// It has to be detached from the creature first: a GameObject cannot be parented across
        /// scenes. That costs nothing — the muscles were never carried by the hierarchy, they are
        /// pulled onto their targets by force, and those targets stay in the main scene where the
        /// creature is.
        /// </remarks>
        public bool Adopt(GameObject puppet)
        {
            if (!IsReady || puppet == null) return false;

            puppet.transform.SetParent(null, true);
            SceneManager.MoveGameObjectToScene(puppet, _scene);

            return true;
        }

        /// <summary>
        /// Copies the world's static colliders into the physics scene.
        /// </summary>
        /// <remarks>
        /// <para>Only what cannot move: a collider with a <c>Rigidbody</c> is something the game
        /// simulates elsewhere, and a stale copy of it would be a wall standing where a crate used to
        /// be. Arena floors, walls and rocks are exactly what belongs here.</para>
        ///
        /// <para>The ghosts carry a collider and nothing else — no renderer, no script, no mesh
        /// instance of their own (a <c>MeshCollider</c> shares the source mesh). The cost is a
        /// transform and a shape per piece of scenery.</para>
        /// </remarks>
        [ContextMenu("Mirror the environment")]
        public void MirrorEnvironment()
        {
            if (!IsReady) return;

            foreach (GameObject ghost in _ghosts)
                if (ghost != null) Destroy(ghost);
            _ghosts.Clear();

            foreach (Collider source in FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (source == null || source.isTrigger) continue;
                if ((_environmentLayers.value & (1 << source.gameObject.layer)) == 0) continue;
                if (source.gameObject.scene == _scene) continue;
                if (source.attachedRigidbody != null) continue;

                GameObject ghost = Mirror(source);
                if (ghost == null) continue;

                SceneManager.MoveGameObjectToScene(ghost, _scene);
                _ghosts.Add(ghost);
            }
        }

        /// <summary>One collider-only copy, posed in world space.</summary>
        private static GameObject Mirror(Collider source)
        {
            var ghost = new GameObject(source.name + " (physics)");
            ghost.layer = source.gameObject.layer;

            Transform t = ghost.transform;
            t.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            t.localScale = source.transform.lossyScale;

            switch (source)
            {
                case BoxCollider box:
                    BoxCollider boxGhost = ghost.AddComponent<BoxCollider>();
                    boxGhost.center = box.center;
                    boxGhost.size = box.size;
                    break;

                case SphereCollider sphere:
                    SphereCollider sphereGhost = ghost.AddComponent<SphereCollider>();
                    sphereGhost.center = sphere.center;
                    sphereGhost.radius = sphere.radius;
                    break;

                case CapsuleCollider capsule:
                    CapsuleCollider capsuleGhost = ghost.AddComponent<CapsuleCollider>();
                    capsuleGhost.center = capsule.center;
                    capsuleGhost.radius = capsule.radius;
                    capsuleGhost.height = capsule.height;
                    capsuleGhost.direction = capsule.direction;
                    break;

                case MeshCollider mesh when mesh.sharedMesh != null:
                    MeshCollider meshGhost = ghost.AddComponent<MeshCollider>();
                    meshGhost.sharedMesh = mesh.sharedMesh;

                    // Static scenery is concave as often as not, and a non-convex mesh collider is legal
                    // precisely because nothing here ever moves.
                    meshGhost.convex = mesh.convex;
                    break;

                default:
                    Destroy(ghost);
                    return null;
            }

            return ghost;
        }
    }
}
