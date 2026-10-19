using System;
using UnityEngine;

namespace Leeway.World.Domain
{
    /// <summary>Stable cube-sphere address shared by rendering and nearby collision tiles.</summary>
    public readonly struct PlanetTileKey : IEquatable<PlanetTileKey>
    {
        public readonly int Face, Level, X, Y;
        public PlanetTileKey(int face,int level,int x,int y) { Face=face;Level=level;X=x;Y=y; }
        private static readonly Vector3[] Normals={Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
        private static readonly Vector3[] U={Vector3.back,Vector3.forward,Vector3.right,Vector3.right,Vector3.right,Vector3.left};
        private static readonly Vector3[] V={Vector3.up,Vector3.up,Vector3.back,Vector3.forward,Vector3.up,Vector3.up};

        public static Vector3 Direction(int face,float u,float v) =>
            (Normals[face]+U[face]*Mathf.Tan(u*Mathf.PI*0.25f)+V[face]*Mathf.Tan(v*Mathf.PI*0.25f)).normalized;

        public static PlanetTileKey At(Vector3 direction,int level)
        {
            if(level<0 || level>20)throw new ArgumentOutOfRangeException(nameof(level));
            if(direction.sqrMagnitude<1e-12f)throw new ArgumentException("Direction must be nonzero.",nameof(direction));
            int face=0;
            float best=Vector3.Dot(direction,Normals[0]);
            for(int i=1;i<6;i++){float dot=Vector3.Dot(direction,Normals[i]);if(dot>best){best=dot;face=i;}}
            float u=Mathf.Atan(Vector3.Dot(direction,U[face])/best)*(4f/Mathf.PI);
            float v=Mathf.Atan(Vector3.Dot(direction,V[face])/best)*(4f/Mathf.PI);
            int cells=1<<level;
            return new PlanetTileKey(face,level,Mathf.Clamp(Mathf.FloorToInt((u+1)*0.5f*cells),0,cells-1),
                Mathf.Clamp(Mathf.FloorToInt((v+1)*0.5f*cells),0,cells-1));
        }

        public Vector3 CenterDirection => Direction(Face,-1f+(2f*X+1)/(1<<Level),-1f+(2f*Y+1)/(1<<Level));
        public bool Equals(PlanetTileKey other)=>Face==other.Face&&Level==other.Level&&X==other.X&&Y==other.Y;
        public override bool Equals(object other)=>other is PlanetTileKey key&&Equals(key);
        public override int GetHashCode()=>HashCode.Combine(Face,Level,X,Y);
        public override string ToString()=>$"{Face}/{Level}/{X},{Y}";
    }
}
