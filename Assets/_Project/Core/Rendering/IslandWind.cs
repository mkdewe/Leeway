using Leeway.Gravity;
using UnityEngine;
using UnityEngine.Rendering;
using Leeway.World.Domain;

namespace Leeway.Rendering
{
    /// <summary>Continuous changing wind for clouds, vegetation and visible air streaks.</summary>
    [ExecuteAlways,DisallowMultipleComponent]
    public sealed class IslandWind:MonoBehaviour
    {
        [SerializeField,Range(0,360),Tooltip("Initial direction: 0 = +Z, 90 = +X.")] private float _headingDegrees=65;
        [SerializeField,Range(0,2)] private float _strength=0.8f;
        [SerializeField,Range(0,0.15f)] private float _cloudSpeed=0.035f;
        [Header("Changing weather")]
        [SerializeField] private int _weatherSeed=7183;
        [SerializeField,Tooltip("Seconds between changes; 300–900 = 5–15 minutes.")] private Vector2 _changeIntervalSeconds=new(300,900);
        [SerializeField,Min(1)] private float _transitionSeconds=30;
        [SerializeField] private Vector2 _strengthRange=new(0.45f,1.15f);
        [Header("Air streaks")]
        [SerializeField] private Material _streakMaterial;
        [SerializeField,Min(0.5f)] private float _streakInterval=1.8f;
        [SerializeField,Range(0,0.5f)] private float _streakOpacity=0.3f;
        [SerializeField,Range(0.01f,0.2f)] private float _streakWidth=0.065f;
        [SerializeField,Range(0,0.6f),Tooltip("Gentle sideways and vertical movement, in metres.")] private float _streakWaveAmplitude=0.24f;
        [SerializeField,Range(0.2f,4),Tooltip("Speed of the wave travelling along each streak.")] private float _streakWaveSpeed=1.9f;
        private sealed class Streak
        {
            public LineRenderer Line;
            public Vector3 Origin,Travel,Heading;
            public float Born,Lifetime,Length,WavePhase,WaveRate;
            public bool Active;
            public readonly Vector3[] Points=new Vector3[32];
        }
        private readonly Streak[] _streaks=new Streak[8];
        private readonly System.Random _effectsRandom=new(19417);
        private WindTimeline _timeline;
        private WindState _state;
        private int _activeSeed;
        private bool _externalClock;
        private double _externalElapsed,_clockReceived,_localStart;
        private float _nextStreak,_effectiveStrength;
#if UNITY_EDITOR
        private double _editorStart,_nextRepaint;
#endif
        public Vector3 Direction=>DirectionAt(Camera.main?Camera.main.transform.position:transform.position);
        public Vector3 DirectionAt(Vector3 position)=>GravityWorld.FrameAt(position,gameObject.scene).ToWorld(new Vector3(_state.Direction.x,0,_state.Direction.y));
        public float Strength=>_effectiveStrength;
        public float CloudSpeed=>_cloudSpeed;
        public int ChangeIndex=>_state.ChangeIndex;
        public double SecondsUntilChange=>System.Math.Max(0,_state.NextChangeTime-Elapsed);
        public Vector2 CloudOffset=>_state.CloudOffset;
        /// <summary>Off while the camera is outside the air: the streaks that are in flight finish, no new ones start.</summary>
        public bool StreaksEnabled{get;set;}=true;
        public float WeatherTime=>(float)Elapsed;
        private double Elapsed
        {
            get
            {
                if(Application.isPlaying)return _externalClock?
                    _externalElapsed+System.Math.Max(0,Time.timeAsDouble-_clockReceived):
                    System.Math.Max(0,Time.timeAsDouble-_localStart);
#if UNITY_EDITOR
                return System.Math.Max(0,UnityEditor.EditorApplication.timeSinceStartup-_editorStart);
#else
                return Time.timeAsDouble;
#endif
            }
        }
        private void OnEnable()
        {
            _localStart=Time.timeAsDouble;
#if UNITY_EDITOR
            _editorStart=UnityEditor.EditorApplication.timeSinceStartup;
            UnityEditor.EditorApplication.update+=EditorTick;
#endif
            ResetTimeline(_weatherSeed);SampleAndApply();
            if(Application.isPlaying)CreateStreaks();
        }
        private void OnValidate()
        {
            _changeIntervalSeconds.x=Mathf.Max(1,_changeIntervalSeconds.x);
            _changeIntervalSeconds.y=Mathf.Max(_changeIntervalSeconds.x,_changeIntervalSeconds.y);
            _strengthRange.x=Mathf.Clamp(_strengthRange.x,0,2);
            _strengthRange.y=Mathf.Clamp(_strengthRange.y,_strengthRange.x,2);
            _timeline=null;
            if(isActiveAndEnabled)SampleAndApply();
        }
        private void ResetTimeline(int seed)
        {
            _activeSeed=seed;
            _timeline=new WindTimeline(seed,_headingDegrees,_strength,_changeIntervalSeconds.x,
                _changeIntervalSeconds.y,_transitionSeconds,_strengthRange.x,_strengthRange.y,_cloudSpeed);
        }
        /// <summary>IslandSeed supplies an authoritative epoch; every client evaluates the same weather.</summary>
        public void SetNetworkClock(int seed,double elapsed)
        {
            if(_timeline==null||_activeSeed!=seed)ResetTimeline(seed);
            _externalClock=true;_externalElapsed=System.Math.Max(0,elapsed);_clockReceived=Time.timeAsDouble;
            SampleAndApply();
        }
        public void ClearNetworkClock()
        {
            double elapsed=Elapsed;_externalClock=false;_localStart=Time.timeAsDouble-elapsed;
        }
        private void SampleAndApply()
        {
            if(_timeline==null)ResetTimeline(_activeSeed==0?_weatherSeed:_activeSeed);
            double time=Elapsed;
            _state=_timeline.Evaluate(time);
            float gust=1+0.09f*Mathf.Sin((float)time*0.21f)+0.045f*Mathf.Sin((float)time*0.79f+1.8f);
            _effectiveStrength=_state.Strength*gust;
            Shader.SetGlobalVector("_LeewayWind",new Vector4(_state.Direction.x,_state.Direction.y,_effectiveStrength,_cloudSpeed));
            Shader.SetGlobalVector("_LeewayCloudOffset",new Vector4(_state.CloudOffset.x,_state.CloudOffset.y,0,0));
            Shader.SetGlobalFloat("_LeewayWindTime",(float)time);
            Shader.SetGlobalFloat("_LeewayWindEnabled",1);
        }
#if UNITY_EDITOR
        private void EditorTick()
        {
            if(Application.isPlaying)return;
            SampleAndApply();
            if(UnityEditor.EditorApplication.timeSinceStartup<_nextRepaint)return;
            _nextRepaint=UnityEditor.EditorApplication.timeSinceStartup+0.1;
            UnityEditor.SceneView.RepaintAll();
        }
#endif
        private void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update-=EditorTick;
#endif
            Shader.SetGlobalFloat("_LeewayWindEnabled",0);
            foreach(var s in _streaks)if(s!=null&&s.Line)
            {if(Application.isPlaying)Destroy(s.Line.gameObject);else DestroyImmediate(s.Line.gameObject);}
            System.Array.Clear(_streaks,0,_streaks.Length);
        }
        private void CreateStreaks()
        {
            if(!_streakMaterial)return;
            for(int i=0;i<_streaks.Length;i++)
            {
                var go=new GameObject("Wind Streak "+i){hideFlags=HideFlags.HideAndDontSave};
                go.transform.SetParent(transform,false);
                var line=go.AddComponent<LineRenderer>();
                line.sharedMaterial=_streakMaterial;line.useWorldSpace=true;line.positionCount=32;
                line.widthMultiplier=_streakWidth;
                line.widthCurve=new AnimationCurve(new Keyframe(0,0),new Keyframe(0.3f,1),new Keyframe(1,0));
                line.numCapVertices=3;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
                line.enabled=false;_streaks[i]=new Streak{Line=line};
            }
            _nextStreak=Time.time+0.6f;
        }
        private float Range(float a,float b)=>Mathf.Lerp(a,b,(float)_effectsRandom.NextDouble());
        private bool TrySpawn(Camera camera,Streak s)
        {
            int water=LayerMask.NameToLayer("Water");
            int mask=water<0?~0:~(1<<water);
            for(int attempt=0;attempt<5;attempt++)
            {
                Vector3 origin=camera.ViewportToWorldPoint(new Vector3(Range(0.15f,0.85f),Range(0.2f,0.78f),Range(8,22)));
                Vector3 ray=origin-camera.transform.position;
                if(Physics.Raycast(camera.transform.position,ray.normalized,ray.magnitude+0.3f,mask,QueryTriggerInteraction.Ignore))continue;
                if(Physics.CheckSphere(origin,0.4f,mask,QueryTriggerInteraction.Ignore))continue;
                s.Origin=origin;s.Travel=Vector3.zero;s.Heading=Direction;
                s.Born=Time.time;s.Lifetime=Range(3.2f,4.8f);s.Length=Range(4,7);
                s.WavePhase=Range(0,Mathf.PI*2);s.WaveRate=Range(0.85f,1.15f);
                s.Active=true;s.Line.enabled=true;return true;
            }
            return false;
        }
        private void Update()
        {
            SampleAndApply();
            if(!Application.isPlaying||!_streakMaterial)return;
            if(_streaks[0]==null)CreateStreaks();
            Camera camera=Camera.main;if(!camera)return;
            if(StreaksEnabled&&_effectiveStrength>0.1f&&Time.time>=_nextStreak)
            {
                bool spawned=false;
                foreach(var s in _streaks)if(s!=null&&!s.Active&&TrySpawn(camera,s)){spawned=true;break;}
                _nextStreak=Time.time+(spawned?_streakInterval*Range(0.8f,1.5f):0.5f);
            }
            foreach(var s in _streaks)
            {
                if(s==null||!s.Active)continue;
                float age=Time.time-s.Born,life=age/s.Lifetime;
                if(life>=1){s.Active=false;s.Line.enabled=false;continue;}
                s.Heading=Vector3.Slerp(s.Heading,Direction,1-Mathf.Exp(-Time.deltaTime*2));
                s.Travel+=Direction*(2.5f+_effectiveStrength*2)*Time.deltaTime;
                Vector3 cross=Vector3.Cross(GravityWorld.UpAt(s.Origin+s.Travel,gameObject.scene),s.Heading);
                float alpha=Mathf.Pow(Mathf.Sin(life*Mathf.PI),2)*_streakOpacity*(0.5f+0.5f*Mathf.Clamp01(_effectiveStrength));
                s.Line.startColor=s.Line.endColor=new Color(0.86f,0.96f,0.91f,alpha);
                for(int j=0;j<s.Points.Length;j++)
                {
                    float u=j/(float)(s.Points.Length-1);
                    // Travelling waves deform the curve itself, rather than only translating it.
                    float phase=u*Mathf.PI*2-age*_streakWaveSpeed*s.WaveRate+s.WavePhase;
                    float envelope=0.45f+0.55f*Mathf.Sin(u*Mathf.PI);
                    float amplitude=_streakWaveAmplitude*Mathf.Lerp(0.7f,1.15f,Mathf.Clamp01(_effectiveStrength));
                    float sideways=Mathf.Sin(phase)+0.22f*Mathf.Sin(phase*1.7f+age*0.4f);
                    float vertical=Mathf.Sin(phase+1.2f)*0.6f;
                    s.Points[j]=s.Origin+s.Travel-s.Heading*u*s.Length+
                        cross*sideways*amplitude*envelope+
                        GravityWorld.UpAt(s.Origin+s.Travel,gameObject.scene)*(Mathf.Sin(u*Mathf.PI)*0.22f+vertical*amplitude*envelope);
                }
                s.Line.SetPositions(s.Points);
            }
        }
        private void OnDrawGizmosSelected()
        {
            Gizmos.color=new Color(0.75f,0.95f,0.9f);
            Vector3 start=transform.position+Vector3.up*3,end=start+Direction*8;
            Gizmos.DrawLine(start,end);Gizmos.DrawLine(end,end-Quaternion.Euler(0,25,0)*Direction*2);
            Gizmos.DrawLine(end,end-Quaternion.Euler(0,-25,0)*Direction*2);
        }
    }
}
