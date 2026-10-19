using System;
using UnityEngine;
namespace Leeway.World.Domain
{
    public static class SurfaceReflection
    {
        /// <summary>Reflection in an arbitrary world plane; applying twice restores the point.</summary>
        public static Matrix4x4 Matrix(Vector3 point,Vector3 normal)
        {
            if(normal.sqrMagnitude<1e-12f)throw new ArgumentException("Plane normal must be nonzero.");
            normal.Normalize();
            float x=normal.x,y=normal.y,z=normal.z,d=-Vector3.Dot(normal,point);
            var m=Matrix4x4.identity;
            m.m00=1-2*x*x;m.m01=-2*x*y;m.m02=-2*x*z;m.m03=-2*d*x;
            m.m10=-2*y*x;m.m11=1-2*y*y;m.m12=-2*y*z;m.m13=-2*d*y;
            m.m20=-2*z*x;m.m21=-2*z*y;m.m22=1-2*z*z;m.m23=-2*d*z;
            return m;
        }
    }
}
