using System;
using Leeway.World.Domain;
using NUnit.Framework;
using UnityEngine;

namespace Leeway.Tests
{
    public class SurfaceFrameTests
    {
        private static void Near(Vector3 actual, Vector3 expected, float tolerance = 1e-5f) =>
            Assert.That((actual - expected).magnitude, Is.LessThan(tolerance));

        [Test] public void FlatFramePreservesExistingAxesAndYaw()
        {
            var frame = new SurfaceFrame(Vector3.up);
            Near(frame.Right, Vector3.right);
            Near(frame.Forward, Vector3.forward);
            Near(frame.Heading(90), Vector3.right);
            Near(frame.Heading(-90), Vector3.left);
            Assert.That(Quaternion.Angle(frame.Rotation(73), Quaternion.Euler(0,73,0)), Is.LessThan(0.05f));
        }

        [Test] public void FramesAreOrthonormalAcrossSphereIncludingPolesAndFallbackBoundary()
        {
            var ups = new[]{Vector3.up, Vector3.down, Vector3.forward, Vector3.back,
                Vector3.right, Vector3.left, new Vector3(0.00099f,0,1),
                new Vector3(0.00101f,0,1), new Vector3(2,-3,5)};
            foreach(var up in ups)
            {
                var f = new SurfaceFrame(up);
                Near(f.Up, up.normalized);
                Assert.That(f.Right.magnitude, Is.EqualTo(1).Within(1e-5f));
                Assert.That(f.Forward.magnitude, Is.EqualTo(1).Within(1e-5f));
                Assert.That(Mathf.Abs(Vector3.Dot(f.Up,f.Forward)), Is.LessThan(1e-5f));
                Assert.That(Mathf.Abs(Vector3.Dot(f.Up,f.Right)), Is.LessThan(1e-5f));
                Near(Vector3.Cross(f.Right,f.Up), f.Forward);
                Near(new SurfaceFrame(up).Forward, f.Forward);
            }
        }

        [Test] public void VectorConversionRoundTripsWithoutLosingVerticalVelocity()
        {
            var f = new SurfaceFrame(new Vector3(2,-3,5));
            var velocity = new Vector3(-12,8,4);
            Near(f.ToWorld(f.ToLocal(velocity)), velocity);
            Near(f.Tangent(velocity) + f.Up * f.Vertical(velocity), velocity);
            Assert.That(Mathf.Abs(Vector3.Dot(f.Tangent(velocity), f.Up)), Is.LessThan(1e-5f));
        }

        [TestCase(-179f)] [TestCase(-90f)] [TestCase(0f)] [TestCase(90f)] [TestCase(179f)]
        public void YawAndOrientationRoundTripOnOppositeSides(float yaw)
        {
            foreach(var up in new[]{Vector3.up,Vector3.down,Vector3.forward,Vector3.back,new Vector3(2,3,-5)})
            {
                var f = new SurfaceFrame(up);
                var heading = f.Heading(yaw);
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(f.YawOf(heading),yaw)), Is.LessThan(0.001f));
                Near(f.Rotation(yaw) * Vector3.forward, heading);
                Near(f.Rotation(yaw) * Vector3.up, f.Up);
            }
        }

        [Test] public void VerticalOrZeroHeadingHasDefinedYaw()
        {
            var f = new SurfaceFrame(new Vector3(2,3,4));
            Assert.That(f.YawOf(Vector3.zero), Is.EqualTo(0));
            Assert.That(f.YawOf(f.Up), Is.EqualTo(0));
        }

        [Test] public void InvalidUpIsRejected()
        {
            Assert.Throws<ArgumentException>(() => new SurfaceFrame(Vector3.zero));
            Assert.Throws<ArgumentException>(() => new SurfaceFrame(new Vector3(0,float.NaN,0)));
            Assert.Throws<ArgumentException>(() => new SurfaceFrame(new Vector3(0,0,float.PositiveInfinity)));
        }
    }
}
