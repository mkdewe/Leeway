using System.Collections.Generic;
using Leeway.Creature.Domain;
using Leeway.CreatureEditor;
using Leeway.Gravity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using UnityEngine.TestTools;

namespace Leeway.Tests
{
    public class RadialPhysicsTests
    {
        private Scene _scene;
        private PlanetTestGravityScope _scope;
        private GameObject _root;
        private readonly List<ProceduralLeg> _legs = new();
        private readonly Vector3 _center = new Vector3(10000, -20000, 30000);

        [UnitySetUp] public IEnumerator SetUp()
        {
            yield return CreatureEditorTestScene.EnsureLoaded();
            _scope=new PlanetTestGravityScope();
            _scene = Object.FindFirstObjectByType<CreaturePhysicsWorld>().SimulationScene;
            _root = new GameObject("Radial regression");
            _root.transform.position = _center;
            _root.AddComponent<GravityWorld>().Configure(true, 3000, 9.81f);
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            foreach(var leg in _legs) leg.Dispose();
            _legs.Clear();
            Object.DestroyImmediate(_root);
            _scope?.Dispose();
            yield return null;
        }

        [TestCase(1,0,0)] [TestCase(0,-1,0)] [TestCase(0,0,-1)]
        public void CustomForceFallsRadiallyWithoutGlobalGravityAndWithoutMassDependence(int x,int y,int z)
        {
            var up = new Vector3(x,y,z);
            foreach(float mass in new[]{1f, 20f})
            {
                var go = new GameObject("Falling body");
                go.transform.SetParent(_root.transform);
                go.transform.position = _center + up * 3010;
                var rb = go.AddComponent<Rigidbody>();
                rb.mass = mass;
                rb.linearDamping = 0;
                var gravity = go.AddComponent<GravityBody>();
                gravity.SourceScene = _root.scene;
                gravity.ExternallyStepped = true;
                go.transform.SetParent(null);
                SceneManager.MoveGameObjectToScene(go, _scene);
                for(int tick=0;tick<50;tick++)
                {
                    gravity.Step();
                    _scene.GetPhysicsScene().Simulate(0.02f);
                }
                Assert.That(rb.useGravity, Is.False);
                Assert.That(Vector3.Dot(rb.linearVelocity, up), Is.EqualTo(-9.81f).Within(0.02f));
                Assert.That(Vector3.ProjectOnPlane(rb.linearVelocity, up).magnitude, Is.LessThan(0.01f));
                Assert.That(Vector3.Dot(rb.position-_center,up), Is.EqualTo(3010-5.0031f).Within(0.04f));
                Object.DestroyImmediate(go);
            }
        }

        [Test] public void IndependentPhysicsSceneKeepsItsSourceField()
        {
            var go = new GameObject("Adopted body");
            go.transform.position = _center + Vector3.right * 3005;
            var rb = go.AddComponent<Rigidbody>();
            var body = go.AddComponent<GravityBody>();
            body.SourceScene = _root.scene;
            body.ExternallyStepped = true;
            SceneManager.MoveGameObjectToScene(go, _scene);
            body.Step();
            _scene.GetPhysicsScene().Simulate(0.02f);
            Assert.That(rb.linearVelocity.x, Is.LessThan(-0.19f));
            Assert.That(Mathf.Abs(rb.linearVelocity.y), Is.LessThan(0.001f));
            Object.DestroyImmediate(go);
        }
    }
}
