using System.Collections.Generic;
using Leeway.Creature.Domain;
using Leeway.CreatureEditor;
using Leeway.Gravity;
using NUnit.Framework;
using UnityEngine;

namespace Leeway.Tests
{
    public class RadialFootContactTests
    {
        private GameObject _root;
        private readonly List<ProceduralLeg> _legs = new();

        [TearDown] public void TearDown()
        {
            foreach(var leg in _legs) leg.Dispose();
            _legs.Clear();
            if(_root != null) Object.DestroyImmediate(_root);
        }

        [TestCase(1,0,0)] [TestCase(0,-1,0)] [TestCase(0,0,-1)]
        public void FeetProbeAndStepAlongLocalUpOnSphere(int x,int y,int z)
        {
            var up=new Vector3(x,y,z);
            var center=new Vector3(20000,-15000,20000);
            _root=new GameObject("Radial feet");
            _root.transform.position=center;
            _root.AddComponent<GravityWorld>().Configure(true,3000,9.81f);
            var ground=new GameObject("Sphere collider");
            ground.transform.SetParent(_root.transform,false);
            ground.AddComponent<SphereCollider>().radius=3000;
            var hip=new GameObject("Hip").transform;
            hip.SetParent(_root.transform);
            hip.position=center+up*3001;
            var frame=new Leeway.World.Domain.SurfaceFrame(up);
            var leg=new ProceduralLeg(new LegSpec(1,0.65f),hip,0);
            _legs.Add(leg);
            leg.Rest(frame.Forward);
            Physics.SyncTransforms();
            leg.Tick(0,frame.Forward,0.35f,1,80,1,0);
            Assert.That(leg.HasGroundContact,Is.True);
            Assert.That((leg.FootPosition-center).magnitude,Is.EqualTo(3000.015f).Within(0.03f));
            Assert.That(Vector3.Dot(leg.GroundNormal,up),Is.GreaterThan(0.99f));
            hip.position += frame.Forward * 0.15f;
            leg.Plan(frame.Forward,0,hip.position,frame.Forward,1,true,0.2f);
            Assert.That(leg.PlannerHasGround,Is.True);
            Assert.That(Vector3.Distance(leg.FootPosition,hip.position),Is.LessThanOrEqualTo(leg.Spec.Reach));
        }
    }
}
