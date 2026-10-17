using UnityEngine;
using UnityEngine.Rendering;
using Leeway.World.Domain;
namespace Leeway.Rendering
{
    /// <summary>One shared weather clock drives lighting, celestial sky and water reflections.</summary>
    [ExecuteAlways,DisallowMultipleComponent,DefaultExecutionOrder(100)]
    public sealed class IslandDayNight:MonoBehaviour
    {
        [Header("Day and night")]
        [SerializeField,Min(1),Tooltip("Real minutes for a complete 24-hour cycle.")] private float _cycleMinutes=24;
        [SerializeField,Range(0,24)] private float _startHour=10;
        [SerializeField,Range(0,24),Tooltip("Time shown outside Play Mode.")] private float _previewHour=12;
        [SerializeField,Tooltip("Freeze Play Mode at Preview Hour to inspect lighting.")] private bool _freezeForPreview;
        [Header("Celestial lights")]
        [SerializeField] private Light _sun;
        [SerializeField] private Light _moon;
        [SerializeField,Range(0,3)] private float _sunIntensity=1.15f;
        [SerializeField,Range(0,1)] private float _moonIntensity=0.24f;
        private IslandWind _wind;
        public float CurrentHour {get;private set;}
        public float Daylight {get;private set;}
        private void OnEnable()
        {
            _wind=GetComponent<IslandWind>();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update+=EditorTick;
#endif
            Apply();
        }
        private void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update-=EditorTick;
#endif
            Shader.SetGlobalFloat("_LeewayDayNightEnabled",0);
        }
        private void OnValidate(){_cycleMinutes=Mathf.Max(1,_cycleMinutes);}
        private void LateUpdate(){Apply();}
#if UNITY_EDITOR
        private void EditorTick(){if(!Application.isPlaying)Apply();}
#endif
        public void Preview(float hour){_previewHour=Mathf.Repeat(hour,24);_freezeForPreview=true;Apply();}
        public void ResumeCycle(){_freezeForPreview=false;Apply();}
        private static Color C(float r,float g,float b)=>new Color(r,g,b,1);
        private static float Fade(float a,float b,float v)
        {float t=Mathf.InverseLerp(a,b,v);return t*t*(3-2*t);}
        private void Apply()
        {
            if(!_sun||!_moon)return;
            bool preview=!Application.isPlaying||_freezeForPreview;
            double elapsed=preview?0:(_wind?_wind.WeatherTime:Time.timeAsDouble);
            var state=SolarCycle.Evaluate(elapsed,preview?_previewHour:_startHour,_cycleMinutes);
            CurrentHour=state.Hour;Daylight=state.Daylight;
            float altitude=state.SunDirection.y;
            float warm=1-Fade(0.02f,0.5f,altitude);
            _sun.transform.rotation=Quaternion.LookRotation(-state.SunDirection,Vector3.up);
            _moon.transform.rotation=Quaternion.LookRotation(-state.MoonDirection,Vector3.up);
            _sun.color=Color.Lerp(C(1,0.95f,0.83f),C(1,0.53f,0.29f),warm);
            _sun.intensity=_sunIntensity*Fade(-0.03f,0.2f,altitude);
            _moon.color=C(0.57f,0.70f,1);
            _moon.intensity=_moonIntensity*Fade(0,0.22f,-altitude);
            // Only one directional shadow map; switch at the horizon where both lights fade out.
            bool sunUp=altitude>=0;
            _sun.enabled=sunUp;_moon.enabled=!sunUp;
            RenderSettings.sun=sunUp?_sun:_moon;
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=Color.Lerp(C(0.045f,0.075f,0.15f),C(0.43f,0.55f,0.56f),Daylight);
            RenderSettings.ambientEquatorColor=Color.Lerp(C(0.025f,0.045f,0.085f),C(0.22f,0.32f,0.32f),Daylight);
            RenderSettings.ambientGroundColor=Color.Lerp(C(0.018f,0.025f,0.045f),C(0.11f,0.16f,0.17f),Daylight);
            float twilight=(1-Mathf.Abs(Daylight*2-1))*0.45f;
            RenderSettings.fogColor=Color.Lerp(Color.Lerp(C(0.035f,0.065f,0.12f),C(0.59f,0.72f,0.67f),Daylight),C(0.64f,0.40f,0.35f),twilight);
            RenderSettings.reflectionIntensity=Mathf.Lerp(0.35f,0.9f,Daylight);
            Shader.SetGlobalFloat("_LeewayDayNightEnabled",1);
            Shader.SetGlobalFloat("_LeewayDaylight",Daylight);
            Shader.SetGlobalVector("_LeewaySunDirection",state.SunDirection);
            Shader.SetGlobalVector("_LeewayMoonDirection",state.MoonDirection);
        }
    }
}
