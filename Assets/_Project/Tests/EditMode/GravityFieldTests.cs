using System;
using Leeway.World.Domain;
using NUnit.Framework;
using UnityEngine;

namespace Leeway.Tests
{
    public class GravityFieldTests
    {
        private static void Near(Vector3 actual, Vector3 expected, float tolerance = 1e-5f) =>
            Assert.That((actual - expected).magnitude, Is.LessThan(tolerance));

        [Test] public void FlatModeMatchesWorldYGravityAndSeaLevel()
        {
            var field = GravityField.Flat(new Vector3(0, 7, 0), Vector3.up);
            Near(field.UpAt(new Vector3(30, -9, 50)), Vector3.up);
            Near(field.AccelerationAt(Vector3.zero), new Vector3(0, -9.81f, 0));
            Assert.That(field.AltitudeAt(new Vector3(30, 10, 50)), Is.EqualTo(3f));
        }

        [Test] public void FlatModeSupportsATranslatedTiltedPlane()
        {
            var field = GravityField.Flat(new Vector3(4, 2, 3), Vector3.right * 2, 5);
            Near(field.UpAt(Vector3.zero), Vector3.right);
            Near(field.AccelerationAt(Vector3.zero), Vector3.left * 5);
            Assert.That(field.AltitudeAt(new Vector3(1, 99, 100)), Is.EqualTo(-3f));
        }

        [TestCase(1,0,0)] [TestCase(-1,0,0)]
        [TestCase(0,1,0)] [TestCase(0,-1,0)]
        [TestCase(0,0,1)] [TestCase(0,0,-1)]
        public void RadialGravityPointsToTranslatedCentre(int x, int y, int z)
        {
            var center = new Vector3(20, -10000, 40);
            var field = GravityField.Radial(center, 10000, 12);
            var direction = new Vector3(x,y,z);
            var at = center + direction * 10003;
            Near(field.UpAt(at), direction);
            Near(field.AccelerationAt(at), -direction * 12);
            Assert.That(field.AltitudeAt(at), Is.EqualTo(3f).Within(0.002f));
        }

        [Test] public void CentreHasZeroForceAndDeterministicFrame()
        {
            var field = GravityField.Radial(new Vector3(1,2,3), 12000);
            Near(field.AccelerationAt(field.Origin), Vector3.zero);
            Near(field.UpAt(field.Origin), Vector3.up);
            Assert.That(field.AltitudeAt(field.Origin), Is.EqualTo(-12000));
            Near(field.FrameAt(field.Origin).Forward, Vector3.forward);
        }

        [Test] public void NearCentreDoesNotUseApproximateVectorEquality()
        {
            var field = GravityField.Radial(Vector3.zero, 10, 3);
            Near(field.AccelerationAt(Vector3.right * 1e-6f), Vector3.left * 3);
        }

        [Test] public void StrengthIsConstantAboveAndBelowReferenceSphere()
        {
            var field = GravityField.Radial(Vector3.zero, 12000);
            foreach(float distance in new[]{100f, 11995f, 12000f, 24000f})
                Assert.That(field.AccelerationAt(Vector3.up * distance).magnitude, Is.EqualTo(9.81f).Within(1e-5f));
            Assert.That(field.AltitudeAt(Vector3.up * 11995), Is.EqualTo(-5));
        }

        [Test] public void ZeroStrengthPreservesLocalUp()
        {
            var field = GravityField.Radial(Vector3.zero, 10, 0);
            Near(field.UpAt(Vector3.left), Vector3.left);
            Near(field.AccelerationAt(Vector3.left), Vector3.zero);
        }

        [Test] public void InvalidConfigurationIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => GravityField.Radial(Vector3.zero, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => GravityField.Radial(Vector3.zero, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => GravityField.Radial(Vector3.zero, 1, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => GravityField.Flat(Vector3.zero, Vector3.up, float.PositiveInfinity));
            Assert.Throws<ArgumentException>(() => GravityField.Flat(Vector3.zero, Vector3.zero));
            Assert.Throws<ArgumentException>(() => GravityField.Radial(new Vector3(float.NaN,0,0), 1));
        }
    }
}
