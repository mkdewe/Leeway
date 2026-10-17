using System;
using UnityEngine;
namespace Leeway.World.Domain
{
    public readonly struct SolarState
    {
        public readonly float Hour, Daylight;
        public readonly Vector3 SunDirection;
        public Vector3 MoonDirection => -SunDirection;
        public SolarState(float hour,Vector3 sun,float daylight)
        {Hour=hour;SunDirection=sun;Daylight=daylight;}
    }
    public static class SolarCycle
    {
        public static SolarState Evaluate(double elapsed,float startHour,float cycleMinutes)
        {
            double hours=startHour+elapsed/(Math.Max(1,cycleMinutes)*60.0)*24.0;
            float hour=(float)((hours%24+24)%24);
            float angle=(hour-6)*Mathf.PI/12;
            var sun=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle)*0.85f,Mathf.Sin(angle)*0.45f).normalized;
            float t=Mathf.InverseLerp(-0.18f,0.22f,sun.y);
            return new SolarState(hour,sun,t*t*(3-2*t));
        }
    }
}
