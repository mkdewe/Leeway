using System.Collections;
using Leeway.CreatureEditor;
using Leeway.Planet;
using Leeway.World.Domain;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Leeway.Tests
{
    public class PlanetCollisionPlayTests
    {
        private GameObject _root;
        private PlanetCollisionTiles _tiles;

        [UnityTearDown] public IEnumerator TearDown()
        {
            _tiles?.Dispose();
            if(_root)Object.Destroy(_root);
            yield return null;
        }

        [UnityTest] public IEnumerator CookedTilesMatchGroundAndMirrorIntoPuppetSceneThenUnload()
        {
            yield return CreatureEditorTestScene.EnsureLoaded();
            var physics=Object.FindFirstObjectByType<CreaturePhysicsWorld>();
            _root=new GameObject("Collision residency regression");
            _root.transform.position=new Vector3(20000,-10000,20000);
            var model=new PlanetModel(3000,0.7f,9,5,190);
            _tiles=new PlanetCollisionTiles(model,_root.transform,physics) {RequestRadius=24,KeepFrames=0};
            Vector3 direction=new Vector3(1,0.07f,0.11f).normalized;
            Vector3 point=_root.transform.position+direction*3010;
            var targets=new[]{point};
            float deadline=Time.realtimeSinceStartup+12;
            while(!_tiles.ReadyAt(point)&&Time.realtimeSinceStartup<deadline)
            {
                _tiles.Update(targets);
                yield return null;
            }
            Assert.That(_tiles.ReadyAt(point),Is.True,"A collision tile under the player is published after cooking.");
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            Assert.That(Physics.Raycast(point,-direction,out var hit,100,1,QueryTriggerInteraction.Ignore),Is.True);
            float expected=model.Radius+model.Sample(direction).GroundHeight;
            Assert.That(Vector3.Distance(hit.point,_root.transform.position),Is.EqualTo(expected).Within(0.04f));
            var scene=physics.SimulationScene.GetPhysicsScene();
            Assert.That(scene.Raycast(point,-direction,out var ghostHit,100,1,QueryTriggerInteraction.Ignore),Is.True);
            Assert.That(Vector3.Distance(hit.point,ghostHit.point),Is.LessThan(0.01f));
            Debug.Log("PLANET collision resident="+_tiles.ResidentTiles+" uploadPeakMs="+_tiles.PeakUploadMilliseconds+" bakePeakMs="+_tiles.PeakBakeMilliseconds);
            _tiles.Dispose();
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            Assert.That(Physics.Raycast(point,-direction,100,1,QueryTriggerInteraction.Ignore),Is.False);
            Assert.That(scene.Raycast(point,-direction,out _,100,1,QueryTriggerInteraction.Ignore),Is.False);
        }


        [UnityTest] public IEnumerator CollisionCoversTileEdgesAndCubeFaceSeams()
        {
            yield return CreatureEditorTestScene.EnsureLoaded();
            var physics=Object.FindFirstObjectByType<CreaturePhysicsWorld>();
            _root=new GameObject("Collision seam regression");
            _root.transform.position=new Vector3(20000,-10000,20000);
            var model=new PlanetModel(3000,0.7f,9,5,190);
            _tiles=new PlanetCollisionTiles(model,_root.transform,physics){RequestRadius=4};
            var directions=new[]{Vector3.right,new Vector3(1,1,0).normalized,new Vector3(1,1,1).normalized};
            var points=new Vector3[directions.Length];
            for(int i=0;i<points.Length;i++)points[i]=_root.transform.position+directions[i]*3100;
            float deadline=Time.realtimeSinceStartup+12;
            bool ready=false;
            while(!ready&&Time.realtimeSinceStartup<deadline)
            {
                _tiles.Update(points);
                ready=true;
                foreach(var point in points)ready&=_tiles.ReadyAt(point);
                yield return null;
            }
            Assert.That(ready,Is.True);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            for(int i=0;i<points.Length;i++)
            {
                Assert.That(Physics.Raycast(points[i],-directions[i],out var hit,200,1,QueryTriggerInteraction.Ignore),Is.True,"seam "+directions[i]);
                Assert.That(physics.SimulationScene.GetPhysicsScene().Raycast(points[i],-directions[i],out var ghost,200,1,QueryTriggerInteraction.Ignore),Is.True);
                Assert.That(Vector3.Distance(hit.point,ghost.point),Is.LessThan(0.02f));
            }
        }

        [UnityTest] public IEnumerator OneColliderCanBeAddedUpdatedAndRemovedWithoutRebuildingTheWorld()
        {
            yield return CreatureEditorTestScene.EnsureLoaded();
            var physics=Object.FindFirstObjectByType<CreaturePhysicsWorld>();
            _root=new GameObject("Incremental mirror regression");
            _root.transform.position=new Vector3(30000,30000,30000);
            var collider=_root.AddComponent<BoxCollider>();
            physics.RegisterEnvironment(collider);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            var scene=physics.SimulationScene.GetPhysicsScene();
            Assert.That(scene.Raycast(_root.transform.position+Vector3.up*2,Vector3.down,out _,4,1),Is.True);
            _root.transform.position+=Vector3.right*10;
            collider.size=new Vector3(3,1,3);
            physics.RegisterEnvironment(collider);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            Assert.That(scene.Raycast(_root.transform.position+Vector3.up*2,Vector3.down,out _,4,1),Is.True);
            physics.UnregisterEnvironment(collider);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            Assert.That(scene.Raycast(_root.transform.position+Vector3.up*2,Vector3.down,out _,4,1),Is.False);
        }
    }
}
