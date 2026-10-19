using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using FishNet.Managing;
using Leeway.Creature.Domain;
using Leeway.CreatureEditor;
using Leeway.Gravity;
using Leeway.World;
using Leeway.World.Domain;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Leeway.Tests
{
    public class RadialCreaturePlayTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private PlanetTestGravityScope _scope;
        private NetworkManager _network;
        private PlaygroundCreatureController _player;
        private GameObject _sphere;
        private Rigidbody _rb;
        private Vector3 _center = new Vector3(0,-3000,0);
        private float Radius = 3000;

        [UnitySetUp] public IEnumerator SetUp()
        {
            Application.runInBackground = true;
            yield return CreatureEditorTestScene.EnsureLoaded();
            _network = Object.FindFirstObjectByType<NetworkManager>();
            var seed = Object.FindFirstObjectByType<IslandSeed>(FindObjectsInactive.Include);
            typeof(IslandSeed).GetField("_fixedSeed",Private).SetValue(seed,1);
            _network.ServerManager.StartConnection();
            _network.ClientManager.StartConnection();
            yield return Until(()=>_network.IsServerStarted && _network.IsClientStarted,"host");
            yield return Until(()=>Object.FindObjectsByType<PlaygroundCreatureController>(FindObjectsSortMode.None).Any(b=>b.IsOwner),"player");
            _player=Object.FindObjectsByType<PlaygroundCreatureController>(FindObjectsSortMode.None).First(b=>b.IsOwner);
            yield return Until(()=>GravityWorld.IsSceneReady(_player.gameObject.scene),"production planet ready");
            _scope=new PlanetTestGravityScope();
            _rb=_player.GetComponent<Rigidbody>();
            _sphere=new GameObject("Radial creature test sphere");
            _sphere.transform.position=_center;
            _sphere.AddComponent<SphereCollider>().radius=Radius;
            _sphere.AddComponent<GravityWorld>().Configure(true,Radius,9.81f);
            Physics.SyncTransforms();
            Object.FindFirstObjectByType<CreaturePhysicsWorld>().MirrorEnvironment();
            Place(Vector3.right);
            yield return new WaitForSeconds(1.5f);
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            if(_network)
            {
                _network.ClientManager.StopConnection();
                _network.ServerManager.StopConnection(true);
                yield return Until(()=>!_network.IsServerStarted && !_network.IsClientStarted,"host stop");
            }
            if(_sphere) Object.Destroy(_sphere);
            yield return null;
            var world=Object.FindFirstObjectByType<CreaturePhysicsWorld>();
            if(world)world.MirrorEnvironment();
            _scope?.Dispose();
        }

        private void Place(Vector3 up)
        {
            float stand=GenomeStatRules.StandHeight(_player.Genome,_player.Rules);
            _rb.position=_center+up*(Radius+stand);
            _rb.rotation=new SurfaceFrame(up).Rotation(0);
            _rb.linearVelocity=Vector3.zero;
            _rb.angularVelocity=Vector3.zero;
            _player.transform.SetPositionAndRotation(_rb.position,_rb.rotation);
            typeof(PlaygroundCreatureController).GetField("_yaw",Private).SetValue(_player,0f);
            typeof(PlaygroundCreatureController).GetField("_previousPlanarVelocity",Private).SetValue(_player,Vector3.zero);
            _player.GetComponent<CreatureLocomotion>().Resume();
            _player.GetComponent<CreaturePuppet>().Teleport();
            Physics.SyncTransforms();
        }

        private float Altitude => (_rb.position-_center).magnitude-Radius;

        [UnityTest] public IEnumerator StandingAndJumpWorkOnSideUndersideAndChartPole()
        {
            foreach(var up in new[]{Vector3.right,Vector3.down,Vector3.back})
            {
                Place(up);
                yield return new WaitForSeconds(2f);
                float stand=GenomeStatRules.StandHeight(_player.Genome,_player.Rules);
                Assert.That(_rb.useGravity,Is.False);
                Assert.That(_player.IsKnockedDown,Is.False);
                Assert.That(Altitude,Is.EqualTo(stand).Within(0.25f),"standing altitude "+up);
                Assert.That(Vector3.Dot(_player.transform.up,up),Is.GreaterThan(0.99f));
                float start=Altitude, peak=start;
                typeof(PlaygroundCreatureController).GetField("_jumpQueued",Private).SetValue(_player,true);
                float end=Time.time+1.8f;
                while(Time.time<end){yield return null;peak=Mathf.Max(peak,Altitude);}
                Debug.Log("RADIAL jump up="+up+" stand="+stand+" rise="+(peak-start)+" endAltitude="+Altitude);
                Assert.That(peak-start,Is.GreaterThan(0.1f),"jump lifts along local up");
                Assert.That(Altitude,Is.GreaterThan(stand-0.3f));
                var puppet=_player.GetComponent<CreaturePuppet>().PuppetMaster;
                Assert.That(puppet.muscles.All(m=>!m.rigidbody.useGravity),Is.True);
                float maxError=puppet.muscles.Max(m=>Vector3.Distance(m.rigidbody.position,m.target.position));
                Debug.Log("RADIAL pinned muscle max target distance="+maxError);
                Assert.That(maxError,Is.LessThan(3f),"bounded puppet after jumping");
            }
        }

        [UnityTest] public IEnumerator KnockdownRecoversUprightOnTheSideOfSphere()
        {
            Assert.That(_player.Knockdown(Vector3.right*6+Vector3.forward*8,1),Is.True);
            Assert.That(_player.IsKnockedDown,Is.True);
            yield return Until(()=>!_player.IsKnockedDown,"radial recovery",12);
            yield return new WaitForSeconds(1);
            Vector3 up=(_rb.position-_center).normalized;
            Assert.That(Vector3.Dot(_player.transform.up,up),Is.GreaterThan(0.98f));
            float stand=GenomeStatRules.StandHeight(_player.Genome,_player.Rules);
            Assert.That(Altitude,Is.EqualTo(stand).Within(0.3f));
            Debug.Log("RADIAL recovery altitude="+Altitude+" upright="+Vector3.Dot(_player.transform.up,up));
        }


        [UnityTest] public IEnumerator WaterAndWingsUseRadialVertical()
        {
            var water=new GameObject("Radial water");
            water.transform.SetParent(_sphere.transform,false);
            water.layer=4;
            var shape=water.AddComponent<SphereCollider>();
            shape.radius=Radius+5;
            shape.isTrigger=true;
            _rb.position=_center+Vector3.right*(Radius+5.1f);
            _player.transform.position=_rb.position;
            _rb.linearVelocity=Vector3.zero;
            _player.GetComponent<CreaturePuppet>().Teleport();
            Physics.SyncTransforms();
            yield return new WaitForSeconds(2);
            Debug.Log("RADIAL swimming altitude="+Altitude+" verticalSpeed="+Vector3.Dot(_rb.linearVelocity,_player.Up));
            Assert.That(Altitude,Is.EqualTo(5.1f).Within(0.25f));
            Assert.That(Mathf.Abs(Vector3.Dot(_rb.linearVelocity,_player.Up)),Is.LessThan(0.6f));
            Object.Destroy(water);
            yield return null;
            Place(Vector3.right);

            var genome=_player.Genome.Clone();
            int wingId=StableHash.PartId(CreatureFlightRules.WingKey);
            var wing=new PartGene(wingId,1,new Vector3(1,0.75f,0),Quaternion.identity,1,true,false,default,default,0,0);
            Assert.That(GenomeEditOperations.TryAttachPart(genome,wing,_player.Rules,out _),Is.True);
            Assert.That(_player.ServerSetGenome(genome,out _),Is.True);
            yield return new WaitForSeconds(1.5f);
            float start=Altitude,peak=start,stamina=_player.Stamina;
            for(int i=0;i<14;i++)
            {
                typeof(PlaygroundCreatureController).GetField("_jumpQueued",Private).SetValue(_player,true);
                typeof(PlaygroundCreatureController).GetField("_flapQueued",Private).SetValue(_player,true);
                yield return new WaitForSeconds(0.3f);
                peak=Mathf.Max(peak,Altitude);
            }
            Debug.Log("RADIAL flight rise="+(peak-start)+" staminaSpent="+(stamina-_player.Stamina));
            Assert.That(peak-start,Is.GreaterThan(1.6f));
            Assert.That(_player.Stamina,Is.LessThan(stamina-20));
            float deadline=Time.time+4;
            while(Vector3.Dot(_rb.linearVelocity,_player.Up)>-0.5f && Time.time<deadline)yield return null;
            for(int i=0;i<30;i++)
            {
                typeof(PlaygroundCreatureController).GetField("_glideHeld",Private).SetValue(_player,true);
                yield return null;
            }
            float sink=-Vector3.Dot(_rb.linearVelocity,_player.Up);
            float allowed=CreatureFlightRules.GlideSinkSpeed(CreatureFlightRules.WingPower(_player.Genome,_player.Rules),_player.Stats.Mass);
            Debug.Log("RADIAL glide sink="+sink+" nominal="+allowed);
            Assert.That(sink,Is.LessThan(allowed+0.8f));
        }

        [UnityTest] public IEnumerator StandingPuppetIsBoundedAtOneFiveAndTwelveKilometreRadii()
        {
            foreach(float radius in new[]{1000f,5000f,12000f})
            {
                Radius=radius;
                _center=new Vector3(0,-radius,0);
                _sphere.transform.position=_center;
                _sphere.GetComponent<SphereCollider>().radius=radius;
                _sphere.GetComponent<GravityWorld>().Configure(true,radius,9.81f);
                Physics.SyncTransforms();
                Object.FindFirstObjectByType<CreaturePhysicsWorld>().MirrorEnvironment();
                Place(Vector3.down);
                yield return new WaitForSeconds(1.5f);
                float min=Altitude,max=Altitude,peakMuscleSpeed=0;
                float end=Time.time+1;
                while(Time.time<end)
                {
                    yield return new WaitForFixedUpdate();
                    min=Mathf.Min(min,Altitude);max=Mathf.Max(max,Altitude);
                    var puppet=_player.GetComponent<CreaturePuppet>().PuppetMaster;
                    peakMuscleSpeed=Mathf.Max(peakMuscleSpeed,puppet.muscles.Max(m=>m.rigidbody.linearVelocity.magnitude));
                }
                Debug.Log("RADIAL precision radius="+radius+" worldMagnitude="+_rb.position.magnitude+" altitudeRange="+min+".."+max+" rootJitter="+(max-min)+" peakMuscleSpeed="+peakMuscleSpeed);
                Assert.That(_player.IsKnockedDown,Is.False);
                Assert.That(max-min,Is.LessThan(0.15f));
                Assert.That(peakMuscleSpeed,Is.LessThan(5));
            }
        }

        [UnityTest] public IEnumerator TeleportMovesPuppetBeforeTheNextSimulation()
        {
            foreach(var muscle in _player.GetComponent<CreaturePuppet>().PuppetMaster.muscles)
                if(!muscle.rigidbody.isKinematic)muscle.rigidbody.linearVelocity=Vector3.forward*4;
            Place(Vector3.back);
            foreach(var muscle in _player.GetComponent<CreaturePuppet>().PuppetMaster.muscles)
            {
                Assert.That(Vector3.Distance(muscle.rigidbody.position,muscle.target.position),Is.LessThan(0.01f));
                Assert.That(muscle.rigidbody.linearVelocity.magnitude,Is.LessThan(0.001f));
            }
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(_player.IsKnockedDown,Is.False);
        }

        private static IEnumerator Until(Func<bool> condition,string step,float seconds=10)
        {
            float end=Time.realtimeSinceStartup+seconds;
            while(!condition() && Time.realtimeSinceStartup<end)yield return null;
            Assert.That(condition(),Is.True,"Timed out: "+step);
        }
    }
}
