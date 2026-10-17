using Leeway.CreatureEditor;
using UnityEngine;
using UnityEngine.Rendering;
namespace Leeway.Combat
{
    /// <summary>Presentation only: an attack's readable windup and brief hit flash.</summary>
    [RequireComponent(typeof(CreatureCombatant))]
    public sealed class CreatureCombatPresentation:MonoBehaviour
    {
        [SerializeField] private Material _telegraphMaterial;
        private CreatureCombatant _combatant;private CreatureBody _body;
        private LineRenderer _line;private MaterialPropertyBlock _block;
        private float _flashUntil;private Renderer[] _renderers;private Color[] _baseEmission;
        private readonly Vector3[] _points=new Vector3[25];
        private Vector3 _heading;
        private bool _wasWinding;
        private void Awake()
        {
            _combatant=GetComponent<CreatureCombatant>();_body=GetComponent<CreatureBody>();_block=new MaterialPropertyBlock();
            _body.BodyRebuilt+=OnBuilt;
            if(!_telegraphMaterial)return;
            var go=new GameObject("Attack warning");go.transform.SetParent(transform,false);
            _line=go.AddComponent<LineRenderer>();_line.sharedMaterial=_telegraphMaterial;
            _line.positionCount=_points.Length;_line.useWorldSpace=true;_line.widthMultiplier=0.045f;
            _line.shadowCastingMode=ShadowCastingMode.Off;_line.receiveShadows=false;_line.enabled=false;
        }
        private void OnBuilt(Leeway.Creature.Domain.CreatureGenome g,Leeway.Creature.Domain.CreatureStats s)
        {_renderers=null;_baseEmission=null;}
        public void Flash()
        {
            if(_renderers==null)
            {
                _renderers=GetComponentsInChildren<Renderer>();_baseEmission=new Color[_renderers.Length];
                for(int i=0;i<_renderers.Length;i++)
                {
                    var r=_renderers[i];if(!r||!r.sharedMaterial||!r.sharedMaterial.HasProperty("_EmissionColor"))continue;
                    r.GetPropertyBlock(_block);Color color=_block.GetColor("_EmissionColor");
                    _baseEmission[i]=color.maxColorComponent>0?color:r.sharedMaterial.GetColor("_EmissionColor");
                }
            }
            _flashUntil=Time.time+0.18f;
        }
        private void LateUpdate()
        {
            if(_renderers!=null)
            {
                float flash=Mathf.Clamp01((_flashUntil-Time.time)/0.18f);
                for(int i=0;i<_renderers.Length;i++)
                {
                    var r=_renderers[i];if(!r||!r.sharedMaterial||!r.sharedMaterial.HasProperty("_EmissionColor"))continue;
                    r.GetPropertyBlock(_block);_block.SetColor("_EmissionColor",_baseEmission[i]+new Color(0.6f,0.14f,0.04f)*flash);r.SetPropertyBlock(_block);
                }
                if(flash<=0)_renderers=null;
            }
            bool windup=_combatant.IsWindingUp;
            if(windup&&!_wasWinding)_heading=transform.forward;
            _wasWinding=windup;
            if(!_line)return;_line.enabled=windup;
            if(!windup)return;
            var spec=_combatant.AttackFor(_combatant.PendingAbility);
            float progress=_combatant.WindupProgress;
            float height=0;
            if(Physics.Raycast(transform.position+Vector3.up,Vector3.down,out var hit,6,1,QueryTriggerInteraction.Ignore))height=hit.point.y-transform.position.y+0.04f;
            for(int i=0;i<_points.Length;i++)
            {
                float a=Mathf.Lerp(-spec.ArcDegrees*0.5f,spec.ArcDegrees*0.5f,i/(float)(_points.Length-1));
                _points[i]=transform.position+Vector3.up*height+(Quaternion.Euler(0,a,0)*_heading)*spec.Reach*Mathf.Lerp(0.85f,1,progress);
            }
            _line.startColor=_line.endColor=Color.Lerp(new Color(1,0.7f,0.25f,0.25f),new Color(1,0.25f,0.12f,0.9f),progress);
            _line.SetPositions(_points);
        }
        private void OnDestroy(){if(_body)_body.BodyRebuilt-=OnBuilt;}
    }
}
