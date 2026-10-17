using System.Collections.Generic;
using System.Reflection;
using Leeway.Creature.Domain;
using Leeway.CreatureEditor;
using NUnit.Framework;
using UnityEngine;
namespace Leeway.Tests
{
    public class TerrainFootContactTests
    {
        private GameObject _root;
        private readonly List<ProceduralLeg> _legs=new();
        [SetUp] public void SetUp(){_root=new GameObject("Terrain foot regression");}
        [TearDown] public void TearDown()
        {foreach(var leg in _legs)leg.Dispose();_legs.Clear();Object.DestroyImmediate(_root);}
        private GameObject Box(float x,float top)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.transform.SetParent(_root.transform);
            go.transform.position=new Vector3(5000+x,top-0.1f,0);go.transform.localScale=new Vector3(0.7f,0.2f,4);return go;
        }
        private ProceduralLeg Leg(float x,float phase)
        {
            var hip=new GameObject("Hip").transform;hip.SetParent(_root.transform);
            hip.position=new Vector3(5000+x,1,0);
            var leg=new ProceduralLeg(new LegSpec(1,0.65f),hip,phase);leg.Rest(Vector3.forward);
            _legs.Add(leg);return leg;
        }
        [Test] public void FeetFollowDifferentGroundHeightsIndependently()
        {
            var low=Box(-0.45f,0);var high=Box(0.45f,0.25f);Physics.SyncTransforms();
            var left=Leg(-0.45f,0);var right=Leg(0.45f,0);
            left.Tick(0.1f,Vector3.forward,0.35f,1,80,1);
            right.Tick(0.1f,Vector3.forward,0.35f,1,80,1);
            Assert.That(right.FootPosition.y-left.FootPosition.y,Is.EqualTo(0.25f).Within(0.03f));
            Assert.That(left.HasGroundContact&&right.HasGroundContact,Is.True);
            high.transform.position+=Vector3.up*0.15f;Physics.SyncTransforms();
            left.Tick(0.1f,Vector3.forward,0.35f,1,80,1);
            right.Tick(0.1f,Vector3.forward,0.35f,1,80,1);
            Assert.That(right.FootPosition.y-left.FootPosition.y,Is.EqualTo(0.4f).Within(0.03f));
        }
        [Test] public void BodyOverHollowDoesNotFreezeSupportedLegs()
        {
            Box(-0.45f,0);Box(0.45f,0.2f);Physics.SyncTransforms();
            var left=Leg(-0.45f,0);var right=Leg(0.45f,0.5f);
            Assert.That(Physics.Raycast(new Vector3(5000,1.1f,0),Vector3.down,1.5f,1),Is.False);
            Assert.That(left.HasGroundWithinReach(1,0.2f)||right.HasGroundWithinReach(1,0.2f),Is.True);
            var host=new GameObject("Locomotion");host.transform.SetParent(_root.transform);
            var gait=host.AddComponent<CreatureLocomotion>();
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            typeof(CreatureLocomotion).GetField("_body",flags).SetValue(gait,host.GetComponent<CreatureBody>());
            typeof(CreatureLocomotion).GetField("_standHeight",flags).SetValue(gait,1f);
            var list=(List<ProceduralLeg>)typeof(CreatureLocomotion).GetField("_legs",flags).GetValue(gait);
            list.Add(left);list.Add(right);
            bool airborne=(bool)typeof(CreatureLocomotion).GetMethod("CheckAirborne",flags).Invoke(gait,new object[]{new Vector3(5000,1,0)});
            list.Clear();Assert.That(airborne,Is.False);
            left.Tick(0.7f,Vector3.forward,0.4f,1,80,1);var first=left.FootPosition;
            left.Tick(0.9f,Vector3.forward,0.4f,1,80,1);
            Assert.That(Vector3.Distance(first,left.FootPosition),Is.GreaterThan(0.05f));
        }
        [Test] public void SprintRequiresLegsAndGroundAndIsBounded()
        {
            Assert.That(CreatureSprintRules.Speed(3,true,true,true,1.45f),Is.EqualTo(4.35f).Within(0.001f));
            Assert.That(CreatureSprintRules.Speed(3,true,false,true,1.45f),Is.EqualTo(3));
            Assert.That(CreatureSprintRules.Speed(3,true,true,false,1.45f),Is.EqualTo(3));
            Assert.That(CreatureSprintRules.Speed(3,false,true,true,1.45f),Is.EqualTo(3));
            Assert.That(CreatureSprintRules.Speed(3,true,true,true,100),Is.EqualTo(6));
        }
    }
}
