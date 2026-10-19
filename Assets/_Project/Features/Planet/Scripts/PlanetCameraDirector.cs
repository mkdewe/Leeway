using System;
using Leeway.CreatureEditor;
using Leeway.Rendering;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Leeway.Planet
{
    /// <summary>One distance-controlled view centred on the creature. Zoom never chooses a new heading.
    /// The auxiliary camera takes over at the exact live pose and extends the surface camera's range.</summary>
    [DefaultExecutionOrder(30)]
    public sealed class PlanetCameraDirector : MonoBehaviour
    {
        [SerializeField] private PlanetView _planet;
        [SerializeField] private CinemachineCamera _playCamera;
        [SerializeField] private CreatureOrbitCamera _orbit;
        [SerializeField] private SpaceBackdrop _backdrop;
        [Header("Continuous zoom")]
        [SerializeField,Min(0.1f)] private float _minimumDistance=2f;
        [SerializeField,Min(1f)] private float _maximumRadiusFactor=8f;
        [SerializeField,Range(0.02f,0.5f)] private float _zoomStep=0.12f;
        [SerializeField,Min(0.02f)] private float _zoomSmoothTime=0.18f;
        [SerializeField,Min(0.1f)] private float _maximumLogZoomSpeed=3f;
        [Header("Rotation and return")]
        [SerializeField] private float _dragSensitivity=0.28f;
        [SerializeField,Min(0.1f)] private float _damping=12f;
        [SerializeField,Min(0.1f)] private float _recallSeconds=0.8f;

        private const int ActivePriority=50;
        private const float PlanetFar=600000f;
        private static readonly int PlanetViewId=Shader.PropertyToID("_LeewayPlanetView");
        private CinemachineCamera _planetCamera;
        private CinemachineBrain _brain;
        private CinemachineVirtualCameraBase _ground;
        private CinemachineVirtualCameraBase.StandbyUpdateMode _groundStandby;
        private CinemachineBlendDefinition _savedBlend;
        private bool _blendOverridden;
        private float _switchedAt;
        private Transform _target;
        private Vector3 _focusOffset,_fixedFocus,_eye;
        private Quaternion _rotation,_requestedRotation,_surfaceRelativeRotation,_recallFrom;
        private float _distance,_requestedDistance,_logDistance,_zoomVelocity;
        private float _surfaceDistance,_surfaceLimit,_minimum,_maximum,_nextLimits;
        private float _recallElapsed,_recallDistance,_space;
        private bool _controlling,_overview,_returning,_recalling,_dragging;
        private bool _airEffects=true;
        private IslandWind _wind;
        private StylizedWaterReflections _reflections;
        private Func<float,bool> _zoomHook;

        public float Space=>_space;
        public bool InPlanetView=>_overview;
        public bool IsAway=>_overview||_returning||(_controlling&&_distance>_surfaceLimit*1.03f);
        public float Distance=>_controlling?_distance:(_orbit!=null?_orbit.SurfaceMaximumDistance:0f);
        public float TargetDistance=>_requestedDistance;
        public float MinimumZoomDistance=>_minimum;
        public float MaximumZoomDistance=>_maximum;
        public bool IsRecalling=>_recalling;
        public Vector3 FocusPoint=>_target!=null?_target.position+_focusOffset:_fixedFocus;
        public event Action<bool> ViewChanged;

        private void Awake()
        {
            var go=new GameObject("CM_Planet");go.transform.SetParent(transform,false);
            _planetCamera=go.AddComponent<CinemachineCamera>();
            _planetCamera.Priority.Value=0;
            _zoomHook=HandleSurfaceScroll;
        }

        private void Start()=>Bind();
        private void OnEnable(){if(_zoomHook!=null)Bind();}
        private void Bind()
        {
            if(_orbit==null)_orbit=FindAnyObjectByType<CreatureOrbitCamera>();
            if(_playCamera==null)
                foreach(var camera in FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None))
                    if(camera.TryGetComponent<CinemachineFollow>(out _)){_playCamera=camera;break;}
            if(_backdrop==null)_backdrop=FindAnyObjectByType<SpaceBackdrop>();
            if(_orbit!=null)_orbit.ZoomOverride=_zoomHook;
        }

        private void Update()
        {
            if(_planet==null)return;
            if(_controlling)
            {
                ReadInput();
                RefreshLimits();
                if(!_returning)SetOverview(_requestedDistance>Mathf.Max(_surfaceLimit*1.03f,_planet.Radius*0.2f));
            }
            _planet.Prefetch(_controlling||(_orbit!=null&&_orbit.AtFarLimit));
            UpdateAtmosphere();
            SettleBlend();
        }

        private void LateUpdate()
        {
            // The focus has consumed this frame's puppet pose before the camera follows it.
            if (_planet != null && _controlling) StepPose(Time.unscaledDeltaTime);
        }

        private bool HandleSurfaceScroll(float raw)
        {
            if(_controlling)return true;
            if(raw>=0||_orbit==null||!_orbit.AtFarLimit)return false;
            if(!TakeControl())return false;
            ZoomBy(Notches(raw));
            return true;
        }

        private static float Notches(float raw)=>Mathf.Clamp(Mathf.Abs(raw)>=10f?raw/120f:raw,-10f,10f);

        /// <summary>Input-device-independent zoom. Positive steps approach, negative steps withdraw.</summary>
        public void ZoomBy(float steps)
        {
            if(!float.IsFinite(steps)||Mathf.Approximately(steps,0))return;
            if(!_controlling&&!TakeControl())return;
            RefreshLimits(true);
            _returning=false;
            if(steps>0&&!_recalling&&NeedsRecall())BeginRecall();
            if(steps<0&&_recalling){_recalling=false;_requestedRotation=_rotation;}
            _requestedDistance=Mathf.Clamp(_requestedDistance*Mathf.Exp(-Mathf.Clamp(steps,-100f,100f)*_zoomStep),_minimum,_maximum);
            SetOverview(_requestedDistance>Mathf.Max(_surfaceLimit*1.03f,_planet.Radius*0.2f));
        }

        /// <summary>Explicit overview command, e.g. a button. Mouse-wheel zoom never calls this command.</summary>
        public void EnterPlanetView()
        {
            if(!_controlling&&!TakeControl())return;
            _returning=false;
            RefreshLimits(true);
            float fov=_planetCamera.Lens.FieldOfView;
            _requestedDistance=Mathf.Clamp(_planet.Radius/Mathf.Sin(fov*0.5f*Mathf.Deg2Rad)*1.3f,_minimum,_maximum);
            SetOverview(true);
        }

        public void ExitPlanetView()
        {
            if(!_controlling){SetOverview(false);return;}
            _returning=true;_dragging=false;
            _requestedDistance=Mathf.Clamp(_surfaceDistance,_minimum,_maximum);
            if(NeedsRecall())BeginRecall();
            SetOverview(false);
        }

        private bool TakeControl()
        {
            if(_planet==null||!_planet.EnsureBuilt())return false;
            if(_planet.PlayableWorld!=null&&!_planet.PlayableWorld.IsReady)return false;
            Bind();
            var main=Camera.main;
            if(main==null||!main.TryGetComponent(out _brain))return false;
            _ground=_brain.ActiveVirtualCamera as CinemachineVirtualCameraBase;
            if(_ground==null||_ground==_planetCamera)return false;
            var groundCamera=_ground as CinemachineCamera;
            _target=groundCamera!=null?(groundCamera.LookAt!=null?groundCamera.LookAt:groundCamera.Follow):null;
            if(_target==null&&_orbit!=null)_target=_orbit.Target;
            Vector3 targetPoint=_target!=null?_target.position:_planet.Anchor;
            var state=_ground.State;
            if(state.HasLookAt())targetPoint=state.ReferenceLookAt;
            // Keep the first pose exact, including an authored composition offset on the creature.
            _rotation=_requestedRotation=main.transform.rotation;
            float distance=Mathf.Max(0.1f,Vector3.Dot(targetPoint-main.transform.position,main.transform.forward));
            Vector3 focus=main.transform.position+main.transform.forward*distance;
            _fixedFocus=focus;_focusOffset=_target!=null?focus-_target.position:Vector3.zero;
            _distance=_requestedDistance=_surfaceDistance=distance;
            _logDistance=Mathf.Log(distance);_zoomVelocity=0;
            _surfaceLimit=_orbit!=null?_orbit.SurfaceMaximumDistance:Mathf.Max(15,distance);
            _surfaceRelativeRotation=Quaternion.Inverse(SurfaceRotation())*_rotation;
            _groundStandby=_ground.StandbyUpdate;
            _ground.StandbyUpdate=CinemachineVirtualCameraBase.StandbyUpdateMode.Always;
            _planetCamera.transform.SetPositionAndRotation(main.transform.position,_rotation);
            _planetCamera.Lens=state.Lens;
            _controlling=true;_returning=_recalling=false;
            if(_orbit!=null)_orbit.Suspended=true;
            RefreshLimits(true);
            UseCut();
            _planetCamera.Priority.Value=ActivePriority;
            _eye=main.transform.position;
            return true;
        }

        private void RefreshLimits(bool force=false)
        {
            if(!force&&Time.unscaledTime<_nextLimits)return;
            _nextLimits=Time.unscaledTime+0.5f;
            _minimum=Mathf.Max(_minimumDistance,_orbit!=null?_orbit.MinimumDistance:0.1f);
            _maximum=Mathf.Max(_minimum,_planet.Radius*_maximumRadiusFactor);
            _requestedDistance=Mathf.Clamp(_requestedDistance,_minimum,_maximum);
        }

        private Vector3 LocalUp()
        {
            if(_target!=null)return Leeway.Gravity.GravityWorld.UpAt(FocusPoint,_target.gameObject.scene);
            return (FocusPoint-_planet.Center).normalized;
        }
        private Quaternion SurfaceRotation()=>new Leeway.World.Domain.SurfaceFrame(LocalUp()).Rotation(0);
        private bool NeedsRecall()=>Vector3.Dot(-(_rotation*Vector3.forward),LocalUp())<0.03f;
        private void BeginRecall()
        {
            _recalling=true;_recallElapsed=0;_recallFrom=_rotation;
            // Rotating at this radius cannot send the camera through the planet.
            _recallDistance=Mathf.Max(_distance,_planet.Radius*2.2f);
        }

        /// <summary>Manual orbit only. Zoom does not invoke rotation.</summary>
        public void OrbitByDegrees(Vector2 degrees)
        {
            if(!_controlling||_recalling)return;
            Vector3 up=LocalUp();
            Quaternion yaw=Quaternion.AngleAxis(degrees.x,up)*_requestedRotation;
            float pitch=Mathf.Asin(Mathf.Clamp(-Vector3.Dot(yaw*Vector3.forward,up),-1,1))*Mathf.Rad2Deg;
            float minimum=_distance>_planet.Radius*2.2f?-85f:5f;
            float next=Mathf.Clamp(pitch+degrees.y,minimum,85f);
            _requestedRotation=Quaternion.AngleAxis(next-pitch,yaw*Vector3.right)*yaw;
        }

        private void ReadInput()
        {
            var keyboard=Keyboard.current;
            if(keyboard!=null&&keyboard.escapeKey.wasPressedThisFrame){ExitPlanetView();return;}
            var mouse=Mouse.current;if(mouse==null)return;
            bool overUi=EventSystem.current!=null&&EventSystem.current.IsPointerOverGameObject();
            bool wide=_distance>_planet.Radius*0.15f;
            bool pressed=mouse.rightButton.wasPressedThisFrame||(wide&&mouse.leftButton.wasPressedThisFrame);
            bool held=mouse.rightButton.isPressed||(wide&&mouse.leftButton.isPressed);
            if(pressed&&!overUi)_dragging=true;
            if(!held)_dragging=false;
            if(_dragging){var delta=mouse.delta.ReadValue();OrbitByDegrees(new Vector2(delta.x,-delta.y)*_dragSensitivity);}
            float raw=overUi?0:mouse.scroll.ReadValue().y;
            if(!Mathf.Approximately(raw,0))ZoomBy(Notches(raw));
        }

        private void StepPose(float dt)
        {
            if(_recalling)
            {
                _recallElapsed+=dt;
                float t=Mathf.SmoothStep(0,1,Mathf.Clamp01(_recallElapsed/_recallSeconds));
                _rotation=Quaternion.Slerp(_recallFrom,SurfaceRotation()*_surfaceRelativeRotation,t);
                _requestedRotation=_rotation;
                if(t>=1)_recalling=false;
            }
            else
            {
                _rotation=Quaternion.Slerp(_rotation,_requestedRotation,1-Mathf.Exp(-_damping*dt));
                if(_distance<_surfaceLimit*2&&!NeedsRecall())
                    _surfaceRelativeRotation=Quaternion.Inverse(SurfaceRotation())*_rotation;
            }
            float target=_recalling?_recallDistance:_requestedDistance;
            _logDistance=Mathf.SmoothDamp(_logDistance,Mathf.Log(Mathf.Max(0.1f,target)),ref _zoomVelocity,
                _zoomSmoothTime,_maximumLogZoomSpeed,dt);
            _distance=Mathf.Clamp(Mathf.Exp(_logDistance),_minimum,_maximum);
            _eye=FocusPoint-(_rotation*Vector3.forward)*_distance;
            _planetCamera.ForceCameraPosition(_eye,_rotation);
            if(_orbit!=null)_orbit.SetWorldHeading(_rotation*Vector3.forward);

            var lens=_planetCamera.Lens;
            float altitude=Mathf.Max(0,(_eye-_planet.Center).magnitude-_planet.Radius);
            lens.NearClipPlane=Mathf.Clamp(altitude*0.002f,0.03f,5f);
            lens.FarClipPlane=Mathf.Lerp(Mathf.Max(_planet.Radius*4,_distance+_planet.Radius*2),PlanetFar,
                Mathf.SmoothStep(0,1,Mathf.InverseLerp(150,_planet.Radius*0.3f,altitude)));
            _planetCamera.Lens=lens;
            if(_returning&&!_recalling&&Mathf.Abs(_logDistance-Mathf.Log(_requestedDistance))<0.001f)HandBack();
        }

        private void HandBack()
        {
            if(_orbit!=null){_orbit.AdoptView(_rotation,_distance);_orbit.Suspended=false;}
            if(_ground!=null)
            {
                _ground.ForceCameraPosition(_eye,_rotation);
                _ground.StandbyUpdate=_groundStandby;
            }
            UseCut();_planetCamera.Priority.Value=0;
            _controlling=_returning=_recalling=false;_target=null;_ground=null;
            SetOverview(false);
        }

        private void SetOverview(bool value)
        {
            if(_overview==value)return;_overview=value;ViewChanged?.Invoke(value);
        }

        private void UpdateAtmosphere()
        {
            var main=Camera.main;
            Vector3 eye=_controlling?_eye:main!=null?main.transform.position:_planet.Anchor;
            float top=Mathf.Clamp(_planet.Radius*0.3f,300,2000);
            float bottom=Mathf.Min(180,top*0.5f);
            float altitude=(eye-_planet.Center).magnitude-_planet.Radius;
            _space=Mathf.SmoothStep(0,1,Mathf.InverseLerp(bottom,top,altitude));
            if(_backdrop!=null)_backdrop.Space=_space;
            SetAirEffects(_space<0.4f);
            if(_reflections==null)_reflections=FindAnyObjectByType<StylizedWaterReflections>();
            if(_reflections!=null)_reflections.Suspended=altitude>120;
            Shader.SetGlobalFloat(PlanetViewId,_space);
            _planet.SetHaze(0);
            if(!_planet.IsVisible)_planet.SetVisible(true);
        }
        private void SetAirEffects(bool inAir)
        {
            if(inAir==_airEffects)return;_airEffects=inAir;
            var main=Camera.main;
            if(main!=null)foreach(var motes in main.GetComponentsInChildren<ParticleSystem>(true))
            {var emission=motes.emission;emission.enabled=inAir;if(!inAir)motes.Clear();}
            if(_wind==null)_wind=FindAnyObjectByType<IslandWind>();
            if(_wind!=null)_wind.StreaksEnabled=inAir;
        }
        private void UseCut()
        {
            if(_brain==null)return;
            if(!_blendOverridden){_savedBlend=_brain.DefaultBlend;_blendOverridden=true;}
            _brain.DefaultBlend=new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut,0);
            _switchedAt=Time.unscaledTime;
        }
        private void SettleBlend()
        {
            if(!_blendOverridden||_brain==null||_brain.IsBlending||Time.unscaledTime-_switchedAt<0.2f)return;
            _brain.DefaultBlend=_savedBlend;_blendOverridden=false;
        }
        private void OnDisable()
        {
            if(_orbit!=null){_orbit.Suspended=false;if(_orbit.ZoomOverride==_zoomHook)_orbit.ZoomOverride=null;}
            if(_ground!=null)_ground.StandbyUpdate=_groundStandby;
            if(_planetCamera!=null)_planetCamera.Priority.Value=0;
            if(_blendOverridden&&_brain!=null)_brain.DefaultBlend=_savedBlend;
            _blendOverridden=false;_controlling=_returning=_recalling=false;_ground=null;_target=null;
            SetOverview(false);Shader.SetGlobalFloat(PlanetViewId,0);
            if(_reflections!=null)_reflections.Suspended=false;
            if(_wind!=null)_wind.StreaksEnabled=true;
        }
    }
}
