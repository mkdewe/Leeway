using System.Collections.Generic;
using Leeway.World.Domain;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Leeway.Gravity
{
    /// <summary>Scene-owned field. Scenes without a provider retain flat Unity gravity.
    /// The transform marks the planet centre; the radius is the sea-level reference.</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class GravityWorld : MonoBehaviour
    {
        [SerializeField] private bool _radial = true;
        [SerializeField, Min(1f)] private float _radius = 3000f;
        [SerializeField, Min(0f)] private float _strength = 9.81f;
        private static readonly Dictionary<int, GravityWorld> Worlds = new();
        private GravityField _field;
        private Vector3 _origin;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() { Worlds.Clear(); Loading.Clear(); }

        private static readonly HashSet<int> Loading = new();
        public static void BeginLoading(Scene scene) => Loading.Add(scene.handle);
        public static void MarkSceneReady(Scene scene) => Loading.Remove(scene.handle);
        public static bool IsSceneReady(Scene scene) => !Loading.Contains(scene.handle);
        public System.Func<Vector3,float?> WaterRadiusSampler { get; set; }
        public System.Func<Vector3,bool> GroundReady { get; set; }
        public System.Action<Vector3> RequestGround { get; set; }

        public static bool EnsureGround(Vector3 point,Scene scene)
        {
            if(!IsSceneReady(scene))return false;
            if(!Worlds.TryGetValue(scene.handle,out var world)||world==null||world.GroundReady==null)return true;
            if(world.GroundReady(point))return true;
            world.RequestGround?.Invoke(point);
            return false;
        }

        public static bool TryWaterSurface(Vector3 point,Scene scene,out Vector3 surface)
        {
            surface=default;
            if(!Worlds.TryGetValue(scene.handle,out var world)||world==null||world.WaterRadiusSampler==null)return false;
            Vector3 direction=(point-world.transform.position).normalized;
            float? radius=world.WaterRadiusSampler(direction);
            if(!radius.HasValue)return false;
            surface=world.transform.position+direction*radius.Value;
            return true;
        }

        private void LateUpdate()
        {
            if(!_radial)return;
            Vector3 center=transform.position;
            Shader.SetGlobalVector("_LeewayPlanet",new Vector4(center.x,center.y,center.z,_radius));
        }

        private void OnEnable()
        {
            if (Worlds.TryGetValue(gameObject.scene.handle, out var other) && other != null && other != this)
            {
                Debug.LogError("Only one GravityWorld is supported per scene.", this);
                enabled = false;
                return;
            }
            Worlds[gameObject.scene.handle] = this;
            Rebuild();
        }

        private void OnDisable()
        {
            if (Worlds.TryGetValue(gameObject.scene.handle, out var current) && current == this)
                Worlds.Remove(gameObject.scene.handle);
            if (Worlds.Count == 0) Shader.SetGlobalVector("_LeewayPlanet", Vector4.zero);
        }

        public void Configure(bool radial, float radius, float strength)
        {
            // Validate before changing the live configuration.
            var next = radial ? GravityField.Radial(transform.position, radius, strength)
                : GravityField.Flat(transform.position, transform.up, strength);
            _radial = radial; _radius = radius; _strength = strength;
            _field = next; _origin = transform.position;
        }

        private void Rebuild() => Configure(_radial, _radius, _strength);

        private GravityField Field
        {
            get
            {
                if (_field == null || !_origin.Equals(transform.position) ||
                    (!_radial && _field.FlatUp != transform.up)) Rebuild();
                return _field;
            }
        }

        public static GravityField ForScene(Scene scene) =>
            Worlds.TryGetValue(scene.handle, out var world) && world != null && world.isActiveAndEnabled ? world.Field : null;

        public static Vector3 AccelerationAt(Vector3 position, Scene scene) =>
            ForScene(scene)?.AccelerationAt(position) ?? Physics.gravity;

        public static Vector3 UpAt(Vector3 position, Scene scene)
        {
            var field = ForScene(scene);
            if (field != null) return field.UpAt(position);
            return Physics.gravity.sqrMagnitude > 1e-12f ? -Physics.gravity.normalized : Vector3.up;
        }

        public static SurfaceFrame FrameAt(Vector3 position, Scene scene) => new SurfaceFrame(UpAt(position, scene));
    }
}

