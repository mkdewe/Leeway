using Leeway.World.Domain;
using NUnit.Framework;
using UnityEngine;
namespace Leeway.Tests
{
    public class SurfaceReflectionTests
    {
        [TestCase(0,1,0)] [TestCase(1,0,0)] [TestCase(0,0,-1)] [TestCase(2,-3,5)]
        public void ReflectionPreservesPlaneAndReversesSignedDistance(int x,int y,int z)
        {
            var normal=new Vector3(x,y,z).normalized;
            var point=new Vector3(11,-13,7);
            var source=new Vector3(-3,5,9);
            var matrix=SurfaceReflection.Matrix(point,normal);
            var reflected=matrix.MultiplyPoint3x4(source);
            Assert.That(Vector3.Distance(matrix.MultiplyPoint3x4(point),point),Is.LessThan(1e-4f));
            Assert.That(Vector3.Dot(reflected-point,normal),Is.EqualTo(-Vector3.Dot(source-point,normal)).Within(1e-4f));
            Assert.That(Vector3.Distance(matrix.MultiplyPoint3x4(reflected),source),Is.LessThan(1e-4f));
            Assert.That(matrix.determinant,Is.EqualTo(-1).Within(1e-4f));
        }
    }
}
