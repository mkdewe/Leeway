using System;
using System.Collections.Generic;
using UnityEngine;

namespace Leeway.World.Domain
{
    public readonly struct WindState
    {
        public readonly Vector2 Direction, CloudOffset;
        public readonly float Strength;
        public readonly int ChangeIndex;
        public readonly double NextChangeTime;
        public WindState(Vector2 direction,float strength,Vector2 cloudOffset,int index,double next)
        {Direction=direction;Strength=strength;CloudOffset=cloudOffset;ChangeIndex=index;NextChangeTime=next;}
    }

    /// <summary>Seeded wind schedule shared by local play, host and late-joining clients.
    /// Direction changes use smooth velocity blending; cloud drift is its continuous integral.</summary>
    public sealed class WindTimeline
    {
        private sealed class Segment
        {
            public double Start,Next;
            public Vector2 From,To,Offset;
            public float Transition;
        }
        private readonly List<Segment> _segments=new();
        private readonly System.Random _random;
        private readonly float _minInterval,_maxInterval,_transition,_minStrength,_maxStrength,_cloudSpeed;
        public WindTimeline(int seed,float initialHeading=65,float initialStrength=0.8f,
            float minInterval=300,float maxInterval=900,float transition=30,
            float minStrength=0.45f,float maxStrength=1.15f,float cloudSpeed=0.035f)
        {
            _random=new System.Random(seed);
            _minInterval=Mathf.Max(1,minInterval);_maxInterval=Mathf.Max(_minInterval,maxInterval);
            _transition=Mathf.Clamp(transition,0,_minInterval*0.5f);
            _minStrength=Mathf.Max(0,minStrength);_maxStrength=Mathf.Max(_minStrength,maxStrength);
            _cloudSpeed=Mathf.Max(0,cloudSpeed);
            Vector2 initial=Direction(initialHeading)*Mathf.Max(0,initialStrength);
            _segments.Add(new Segment{Start=0,Next=Range(_minInterval,_maxInterval),From=initial,To=initial,Transition=0});
        }
        public WindState Evaluate(double elapsed)
        {
            if(double.IsNaN(elapsed)||double.IsInfinity(elapsed))throw new ArgumentOutOfRangeException(nameof(elapsed));
            elapsed=Math.Max(0,elapsed);
            while(elapsed>=_segments[_segments.Count-1].Next)
            {
                var previous=_segments[_segments.Count-1];
                float heading=Mathf.Atan2(previous.To.x,previous.To.y)*Mathf.Rad2Deg;
                float turn=Range(45,125)*(_random.Next(2)==0?-1:1);
                double start=previous.Next;
                _segments.Add(new Segment{Start=start,Next=start+Range(_minInterval,_maxInterval),
                    From=previous.To,To=Direction(heading+turn)*Range(_minStrength,_maxStrength),
                    Transition=_transition,Offset=previous.Offset+Integral(previous,start-previous.Start)*_cloudSpeed});
            }
            int lo=0,hi=_segments.Count-1;
            while(lo<hi){int mid=(lo+hi+1)/2;if(_segments[mid].Start<=elapsed)lo=mid;else hi=mid-1;}
            var segment=_segments[lo];
            double local=elapsed-segment.Start;
            float t=segment.Transition<=0?1:Mathf.Clamp01((float)local/segment.Transition);
            float ease=t*t*(3-2*t);
            Vector2 velocity=Vector2.Lerp(segment.From,segment.To,ease);
            Vector2 direction=velocity.sqrMagnitude>0.000001f?velocity.normalized:segment.To.normalized;
            return new WindState(direction,velocity.magnitude,
                segment.Offset+Integral(segment,local)*_cloudSpeed,lo,segment.Next);
        }
        private static Vector2 Integral(Segment s,double elapsed)
        {
            if(s.Transition<=0)return s.To*(float)elapsed;
            double dt=Math.Min(elapsed,s.Transition),duration=s.Transition;
            double easeIntegral=dt*dt*dt/(duration*duration)-0.5*dt*dt*dt*dt/(duration*duration*duration);
            return s.From*(float)dt+(s.To-s.From)*(float)easeIntegral+s.To*(float)Math.Max(0,elapsed-duration);
        }
        private static Vector2 Direction(float heading)
        {float r=heading*Mathf.Deg2Rad;return new Vector2(Mathf.Sin(r),Mathf.Cos(r));}
        private float Range(float a,float b)=>Mathf.Lerp(a,b,(float)_random.NextDouble());
    }
}
