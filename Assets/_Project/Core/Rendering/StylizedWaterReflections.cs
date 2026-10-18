using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Leeway.Rendering
{
    /// <summary>Low resolution planar reflections for generated sea/lake levels.
    /// Water is excluded from captures; the main camera applies the palette once.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class StylizedWaterReflections : MonoBehaviour
    {
        [SerializeField, Range(128, 1024)] private int _resolution = 512;
        [SerializeField, Range(1, 4)] private int _maxPlanes = 4;
        [SerializeField, Min(10)] private float _farClip = 800f;
        private sealed class Plane
        {
            public float Height, Area;
            public Bounds Bounds;
        }
        private readonly List<Plane> _planes = new();
        private readonly RenderTexture[] _textures = new RenderTexture[4];
        [SerializeField,Range(64,256)] private int _environmentResolution=128;
        [SerializeField,Min(1)] private float _environmentRefreshSeconds=4;
        private ReflectionProbe _environment;
        private int _environmentJob=-1;
        private double _nextEnvironment;
        private Vector3 _pendingEnvironmentPosition,_environmentPosition;
        private bool _environmentReady;
        [SerializeField] private int _reflectionRendererIndex = 1;
        private Camera _capture;
        private bool _rendering;
        private double _nextScan;
        private int _meshSignature;
        // Where the planes were last worked out for a given set of water meshes: switching the sea off and on again
        // (as the planet view does) must not cost a pass over every vertex of it each time.
        private readonly Dictionary<int, List<Plane>> _planeCache = new();

        /// <summary>While true no reflections are drawn: the camera is too far from the water for them to matter.</summary>
        public bool Suspended { get; set; }
        private static readonly int CountId = Shader.PropertyToID("_LeewayReflectionCount");
        private void OnEnable() { _meshSignature = 0; _nextScan = 0; RenderPipelineManager.beginCameraRendering += Render; }
        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= Render;
            Shader.SetGlobalFloat(CountId, 0);
            Shader.SetGlobalFloat("_LeewayWaterEnvironmentValid",0);
            if(_environment)Release(_environment.gameObject);
            _environment=null;_environmentReady=false;_environmentJob=-1;
            foreach (var tex in _textures) if (tex) { tex.Release(); Release(tex); }
            Array.Clear(_textures, 0, _textures.Length);
            if (_capture) Release(_capture.gameObject);
            _capture = null;
        }
        private static void Release(UnityEngine.Object value)
        {
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
        private void Scan()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (now < _nextScan) return;
            _nextScan = now + 1;
            int waterLayer = LayerMask.NameToLayer("Water");
            // Only the project's water shader, not unrelated objects on the Water layer.
            var filters = FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)
                .Where(f => f.sharedMesh && f.TryGetComponent<MeshRenderer>(out var r) &&
                    r.enabled && r.sharedMaterial && r.sharedMaterial.shader.name == "Leeway/ToonWater").ToArray();
            int signature = 17;
            foreach (var f in filters.OrderBy(f => f.GetInstanceID()))
                signature = unchecked(signature * 31 + f.sharedMesh.GetInstanceID() + f.transform.localToWorldMatrix.GetHashCode());
            if (signature == _meshSignature) return;
            _meshSignature = signature;
            _planes.Clear();
            if (_planeCache.TryGetValue(signature, out var cached)) { _planes.AddRange(cached); return; }
            var levels = new Dictionary<int, Plane>();
            foreach (var f in filters)
            {
                if (!f.sharedMesh.isReadable) continue;
                var v = f.sharedMesh.vertices;
                var t = f.sharedMesh.triangles;
                var matrix = f.transform.localToWorldMatrix;
                for (int j = 0; j < t.Length; j += 3)
                {
                    Vector3 a = matrix.MultiplyPoint3x4(v[t[j]]);
                    Vector3 b = matrix.MultiplyPoint3x4(v[t[j+1]]);
                    Vector3 c = matrix.MultiplyPoint3x4(v[t[j+2]]);
                    if (Mathf.Abs(a.y-b.y) > 0.025f || Mathf.Abs(a.y-c.y) > 0.025f) continue;
                    float area = Vector3.Cross(b-a,c-a).magnitude * 0.5f;
                    if (area < 0.001f) continue;
                    int key = Mathf.RoundToInt(a.y * 10);
                    if (!levels.TryGetValue(key, out var plane))
                        levels[key] = plane = new Plane { Height = a.y, Bounds = new Bounds(a, Vector3.zero) };
                    plane.Area += area;
                    plane.Bounds.Encapsulate(a); plane.Bounds.Encapsulate(b); plane.Bounds.Encapsulate(c);
                }
            }
            foreach (var p in levels.Values.OrderByDescending(p => p.Area).Take(_maxPlanes))
            { p.Bounds.Expand(new Vector3(1,1,1)); _planes.Add(p); }
            if (_planeCache.Count > 8) _planeCache.Clear();
            _planeCache[signature] = new List<Plane>(_planes);
        }

        private void UpdateEnvironment(Camera source)
        {
            if(!QualitySettings.realtimeReflectionProbes)return;
            if(!_environment)
            {
                var go=new GameObject("Leeway Water Environment"){hideFlags=HideFlags.HideAndDontSave};
                _environment=go.AddComponent<ReflectionProbe>();
                _environment.mode=ReflectionProbeMode.Realtime;
                _environment.refreshMode=ReflectionProbeRefreshMode.ViaScripting;
                _environment.timeSlicingMode=ReflectionProbeTimeSlicingMode.IndividualFaces;
                _environment.resolution=_environmentResolution;
                _environment.hdr=true;_environment.shadowDistance=0;
                _environment.nearClipPlane=0.15f;_environment.farClipPlane=Mathf.Min(500,_farClip);
                _environment.size=new Vector3(240,180,240);
                _environment.clearFlags=ReflectionProbeClearFlags.Skybox;
            }
            double now=Time.realtimeSinceStartupAsDouble;
            if(_environmentJob>=0&&_environment.IsFinishedRendering(_environmentJob))
            {_environmentPosition=_pendingEnvironmentPosition;_environmentReady=true;_environmentJob=-1;}
            if(_environmentReady&&_environment.realtimeTexture)
            {
                Shader.SetGlobalTexture("_LeewayWaterEnvironment",_environment.realtimeTexture);
                Shader.SetGlobalVector("_LeewayWaterEnvironmentHDR",_environment.textureHDRDecodeValues);
                Shader.SetGlobalVector("_LeewayWaterEnvironmentPosition",_environmentPosition);
                Shader.SetGlobalVector("_LeewayWaterEnvironmentExtents",new Vector4(120,90,120,0));
                Shader.SetGlobalFloat("_LeewayWaterEnvironmentValid",1);
            }
            if(_environmentJob>=0||now<_nextEnvironment)return;
            _nextEnvironment=now+_environmentRefreshSeconds;
            _pendingEnvironmentPosition=source.transform.position;
            _environment.transform.position=_pendingEnvironmentPosition;
            int water=LayerMask.NameToLayer("Water");
            _environment.cullingMask=source.cullingMask&~(1<<water)&~(1<<5);
            _environmentJob=_environment.RenderProbe();
        }

        private void Render(ScriptableRenderContext context, Camera source)
        {
            if (_rendering || !source || source.cameraType == CameraType.Reflection ||
                source.cameraType == CameraType.Preview || source.stereoEnabled) return;
            // Thumbnails and UI cameras must not trigger extra world renders.
            int layer = LayerMask.NameToLayer("Water");
            if (layer < 0 || (source.cullingMask & (1 << layer)) == 0 ||
                source.GetUniversalAdditionalCameraData().renderType == CameraRenderType.Overlay) return;
            Scan();
            Shader.SetGlobalFloat(CountId, 0);
            if (Suspended) return;
            if (_planes.Count == 0) return;
            UpdateEnvironment(source);
            var frustum = GeometryUtility.CalculateFrustumPlanes(source);
            if (!_capture)
            {
                var go = new GameObject("Leeway Water Reflection Camera") { hideFlags = HideFlags.HideAndDontSave };
                _capture = go.AddComponent<Camera>(); _capture.enabled = false;
                go.AddComponent<UniversalAdditionalCameraData>();
            }
            bool inverted = GL.invertCulling;
            _rendering = true;
            int count = 0;
            try
            {
                foreach (var plane in _planes)
                {
                    if (source.transform.position.y <= plane.Height + 0.05f ||
                        !GeometryUtility.TestPlanesAABB(frustum, plane.Bounds)) continue;
                    int width = Mathf.Max(64, Mathf.RoundToInt(_resolution * Mathf.Min(1,source.aspect)));
                    int height = Mathf.Max(64, Mathf.RoundToInt(_resolution / Mathf.Max(1,source.aspect)));
                    if (!_textures[count] || _textures[count].width != width || _textures[count].height != height)
                    {
                        if (_textures[count]) { _textures[count].Release(); Release(_textures[count]); }
                        _textures[count] = new RenderTexture(width,height,24,RenderTextureFormat.ARGBHalf)
                        { name = "Leeway Water Reflection " + count, hideFlags = HideFlags.HideAndDontSave,
                          filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                        _textures[count].Create();
                    }
                    _capture.CopyFrom(source);
                    _capture.enabled = false;
                    _capture.cameraType = CameraType.Reflection;
                    _capture.targetTexture = _textures[count];
                    _capture.clearFlags = CameraClearFlags.Skybox;
                    _capture.cullingMask = source.cullingMask & ~(1 << layer);
                    _capture.useOcclusionCulling = false;
                    _capture.farClipPlane = Mathf.Min(source.farClipPlane,_farClip);
                    var data = _capture.GetUniversalAdditionalCameraData();
                    data.SetRenderer(_reflectionRendererIndex);
                    data.renderType = CameraRenderType.Base;
                    data.renderPostProcessing = false;
                    data.renderShadows = false;
                    data.requiresColorOption = CameraOverrideOption.Off;
                    data.requiresDepthOption = CameraOverrideOption.Off;
                    Matrix4x4 mirror = Matrix4x4.identity;
                    mirror.m11 = -1; mirror.m13 = 2 * plane.Height;
                    _capture.transform.position = mirror.MultiplyPoint(source.transform.position);
                    _capture.transform.rotation = Quaternion.LookRotation(
                        mirror.MultiplyVector(source.transform.forward), mirror.MultiplyVector(source.transform.up));
                    _capture.worldToCameraMatrix = source.worldToCameraMatrix * mirror;
                    Vector3 point = _capture.worldToCameraMatrix.MultiplyPoint(new Vector3(0,plane.Height+0.05f,0));
                    Vector3 normal = _capture.worldToCameraMatrix.MultiplyVector(Vector3.up).normalized;
                    _capture.projectionMatrix = source.projectionMatrix;
                    _capture.projectionMatrix = _capture.CalculateObliqueMatrix(
                        new Vector4(normal.x,normal.y,normal.z,-Vector3.Dot(point,normal)));
                    GL.invertCulling = !inverted;
                    // This synchronous API supports rendering inside beginCameraRendering.
                    // Keep the suppression local until URP exposes a context-aware replacement.
#pragma warning disable CS0618
                    UniversalRenderPipeline.RenderSingleCamera(context, _capture);
#pragma warning restore CS0618
                    GL.invertCulling = inverted;
                    Shader.SetGlobalTexture("_LeewayReflection"+count,_textures[count]);
                    Shader.SetGlobalMatrix("_LeewayReflectionVP"+count,
                        GL.GetGPUProjectionMatrix(_capture.projectionMatrix,true)*_capture.worldToCameraMatrix);
                    Shader.SetGlobalFloat("_LeewayReflectionHeight"+count,plane.Height);
                    count++;
                }
            }
            finally { GL.invertCulling = inverted; _rendering = false; Shader.SetGlobalFloat(CountId,count); }
        }
    }
}
