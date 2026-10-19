using System;
using UnityEngine;

namespace Leeway.World.Domain
{
    /// <summary>An island chart backed by the original full height field: lakes, rivers and cave
    /// footprints retain their source data. Horizontal chart scale fits the island's planetary
    /// footprint; elevation remains in metres. All sampling is scene-independent and worker-safe.</summary>
    public sealed class PlanetIslandPatch
    {
        public IslandHeightField Field { get; }
        public Vector3 Direction { get; }
        public float Radius { get; }
        public float HorizontalScale { get; }
        public SurfaceFrame Frame { get; }
        private readonly float _innerCos,_outerCos;
        private readonly bool _outerBelowPi;
        public float Reach => Field.Settings.HalfSize * 0.95f * HorizontalScale;

        public PlanetIslandPatch(IslandHeightField field,Vector3 direction,float radius,float horizontalScale=1)
        {
            Field=field??throw new ArgumentNullException(nameof(field));
            if(radius<=0 || horizontalScale<=0)throw new ArgumentOutOfRangeException(nameof(radius));
            Direction=direction.normalized;
            if(Direction.sqrMagnitude<0.5f)throw new ArgumentException("Direction must be nonzero.");
            Radius=radius;HorizontalScale=horizontalScale;
            Frame=new SurfaceFrame(Direction);
            float outer=Reach/Radius;
            _innerCos=Mathf.Cos(Mathf.Min(Mathf.PI,outer*0.82f));
            _outerCos=Mathf.Cos(Mathf.Min(Mathf.PI,outer));
            _outerBelowPi=outer<Mathf.PI;
        }

        public Vector3 DirectionAt(float x,float z)
        {
            Vector3 tangent=Frame.Right*x+Frame.Forward*z;
            float distance=tangent.magnitude;
            if(distance<1e-7f)return Direction;
            float angle=distance*HorizontalScale/Radius;
            return Direction*Mathf.Cos(angle)+tangent/distance*Mathf.Sin(angle);
        }

        public Vector3 SurfaceVector(Vector3 chartPoint) =>
            DirectionAt(chartPoint.x,chartPoint.z)*(Radius+chartPoint.y-Field.Settings.SeaLevel);

        public Vector3 ToWorld(Vector3 chartPoint,Vector3 center)=>center+SurfaceVector(chartPoint);

        public Vector2 ChartAt(Vector3 direction)
        {
            direction.Normalize();
            float dot=Mathf.Clamp(Vector3.Dot(Direction,direction),-1,1);
            Vector3 tangent=direction-Direction*dot;
            float sine=tangent.magnitude;
            if(sine<1e-7f)return Vector2.zero;
            float metres=Mathf.Atan2(sine,dot)*Radius/HorizontalScale;
            tangent*=metres/sine;
            return new Vector2(Vector3.Dot(tangent,Frame.Right),Vector3.Dot(tangent,Frame.Forward));
        }

        public Vector3 ToField(Vector3 worldPoint,Vector3 center)
        {
            Vector3 offset=worldPoint-center;
            Vector2 chart=ChartAt(offset);
            return new Vector3(chart.x,offset.magnitude-Radius+Field.Settings.SeaLevel,chart.y);
        }

        public Quaternion RotationAt(Vector3 chartPoint) =>
            Quaternion.FromToRotation(Direction,DirectionAt(chartPoint.x,chartPoint.z))*Frame.Rotation(0);

        public Vector3 NormalToWorld(Vector3 chartPoint,Vector3 normal) =>
            RotationAt(chartPoint)*new Vector3(normal.x/HorizontalScale,normal.y,normal.z/HorizontalScale).normalized;

        public float WeightAt(Vector3 direction)
        {
            float dot=Vector3.Dot(Direction,direction);
            float length=direction.magnitude;
            if(dot>=_innerCos*length)return 1f;
            if(_outerBelowPi&&dot<=_outerCos*length)return 0f;
            float angle=Mathf.Atan2(Vector3.Cross(Direction,direction).magnitude,dot);
            float t=Mathf.Clamp01((angle*Radius/Reach-0.82f)/0.18f);
            return 1-t*t*(3-2*t);
        }
    }
}
