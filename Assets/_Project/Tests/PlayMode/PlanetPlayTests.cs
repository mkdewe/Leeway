using System.Collections;
using System.Linq;
using FishNet.Managing;
using Leeway.Planet;
using Leeway.World;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.TestTools;

namespace Leeway.Tests
{
    /// <summary>The planet in the running game: built on demand, flown to and from, and editable by the host.</summary>
    public class PlanetPlayTests
    {
        private NetworkManager _network;
        private PlanetView _planet;
        private PlanetCameraDirector _director;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Application.runInBackground = true;
            yield return CreatureEditorTestScene.EnsureLoaded();

            _network = Object.FindFirstObjectByType<NetworkManager>();
            var islandSeed = Object.FindFirstObjectByType<IslandSeed>(FindObjectsInactive.Include);
            typeof(IslandSeed).GetField("_fixedSeed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(islandSeed, 1);

            _network.ServerManager.StartConnection();
            _network.ClientManager.StartConnection();
            yield return Until(() => _network.IsServerStarted && _network.IsClientStarted && islandSeed.Seed != 0, "host and island seed");
            yield return Until(() => islandSeed.Generator.IsGenerated, "island");

            _planet = Object.FindFirstObjectByType<PlanetView>();
            _director = Object.FindFirstObjectByType<PlanetCameraDirector>();
            Assert.That(_planet, Is.Not.Null, "The scene has the planet.");
            Assert.That(_director, Is.Not.Null);
            yield return Until(() => _planet.PlayableWorld != null && _planet.PlayableWorld.IsReady, "playable planet collision ready");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // The scene is shared with other tests: the camera comes back down and the island is drawn again.
            if (_director) _director.ExitPlanetView();
            if (_director) yield return Until(() => !_director.IsAway, "camera back on the ground");

            if (_network)
            {
                _network.ClientManager.StopConnection();
                _network.ServerManager.StopConnection(true);
                yield return Until(() => !_network.IsServerStarted && !_network.IsClientStarted, "host stop");
            }

            yield return null;
        }

        private static bool IslandShown() =>
            Object.FindFirstObjectByType<IslandGenerator>().GetComponentsInChildren<Renderer>().Any(r => r.enabled);

        [UnityTest]
        public IEnumerator ScrollingOutLeavesForThePlanetAndComesBack()
        {
            var originalModel = _planet.Model;
            var originalCollisionWorld = _planet.PlayableWorld.Collisions;
            var originalIsland = Object.FindFirstObjectByType<IslandSeed>().Generator;
            var originalField = originalIsland.Field;
            Assert.That(originalIsland.Projection.Field, Is.SameAs(originalModel.GetPatch(0).Field));
            Assert.That(IslandShown(), Is.True, "The island is drawn before the flight.");

            _director.EnterPlanetView();
            Assert.That(_director.InPlanetView, Is.True);
            Assert.That(_planet.IsVisible, Is.True);
            yield return Until(() => _planet.IsBuilt, "planet built in the background");

            var planetCamera = _director.GetComponentInChildren<CinemachineCamera>();
            yield return Until(() => CinemachineCore.IsLive(planetCamera), "planet camera live");
            Assert.That(IslandShown(), Is.True, "The same spherical island stays rendered in space.");

            // The camera looks at the planet from outside it.
            float distance = (Camera.main.transform.position - _planet.Center).magnitude;
            Assert.That(distance, Is.GreaterThan(_planet.Radius), "outside the planet");

            _director.ExitPlanetView();
            yield return Until(() => IslandShown(), "the island to be shown again as the camera heads back");
            yield return Until(() => !_director.IsAway, "the camera back on the ground");
            Assert.That(_planet.IsVisible, Is.True, "The playable planet remains visible after descent.");
            Assert.That(_director.InPlanetView, Is.False);
            Assert.That(_planet.Model, Is.SameAs(originalModel), "Camera travel preserves the terrain model.");
            Assert.That(_planet.PlayableWorld.Collisions, Is.SameAs(originalCollisionWorld), "Camera travel preserves the physics world.");
            Assert.That(originalIsland.Field, Is.SameAs(originalField), "Camera travel preserves the island field.");
        }

        [UnityTest]
        public IEnumerator TheHostCanReshapeThePlanet()
        {
            _director.EnterPlanetView();
            Assert.That(_planet.CanEdit, Is.True);

            Assert.That(_planet.Apply(4800f, 0.5f, 20), Is.True);
            yield return Until(() => _planet.IsBuilt && _planet.Model.IslandCount == 20, "rebuilt with 20 islands");

            Assert.That(_planet.Model.Radius, Is.EqualTo(4800f).Within(0.1f));
            Assert.That(_planet.Model.LandFraction, Is.EqualTo(0.5f).Within(0.05f));

            // Out-of-range numbers are held to the limits.
            _planet.Apply(999999f, 2f, 500);
            Assert.That(_planet.Current().Radius, Is.EqualTo(Leeway.World.Domain.PlanetSettings.MaxRadius));
            Assert.That(_planet.Current().IslandCount, Is.EqualTo(Leeway.World.Domain.PlanetSettings.MaxIslands));
        }

        [UnityTest]
        public IEnumerator LeavingTheAirShowsTheStarSystemAndComingBackHidesIt()
        {
            var backdrop = Object.FindFirstObjectByType<SpaceBackdrop>();
            Assert.That(backdrop, Is.Not.Null, "The scene has the star system.");
            yield return Until(() => backdrop.Model != null, "the star system");
            Transform system = backdrop.transform.Find("StarSystem");
            Assert.That(system.gameObject.activeSelf, Is.False, "Nothing of space is drawn from the ground.");
            Assert.That(_director.Space, Is.EqualTo(0f).Within(0.01f));

            _director.EnterPlanetView();
            yield return Until(() => _director.Space >= 0.99f, "the camera to be out of the air");
            yield return Until(() => system.gameObject.activeSelf, "the star system to be drawn");
            Assert.That(Shader.GetGlobalFloat("_LeewaySpace"), Is.EqualTo(1f).Within(0.02f), "The sky goes to stars.");

            _director.ExitPlanetView();
            yield return Until(() => !_director.IsAway, "the camera back on the ground");
            yield return null;
            Assert.That(_director.Space, Is.EqualTo(0f).Within(0.01f));
            Assert.That(system.gameObject.activeSelf, Is.False);
            Assert.That(Shader.GetGlobalFloat("_LeewaySpace"), Is.EqualTo(0f).Within(0.01f), "The air is back.");
        }

        [UnityTest]
        public IEnumerator ZoomKeepsTheCurrentRotationAndTheGroundStillRefines()
        {
            Quaternion rotation=Camera.main.transform.rotation;
            _director.EnterPlanetView();
            float end=Time.realtimeSinceStartup+10;
            while(_director.Space<0.2f&&Time.realtimeSinceStartup<end)
            {
                yield return null;
                Assert.That(Quaternion.Angle(rotation,Camera.main.transform.rotation),Is.LessThan(0.1f),"Zoom must not bank, tilt or turn toward a preset orbital heading.");
                Vector3 view=Camera.main.WorldToViewportPoint(_director.FocusPoint);
                Assert.That(Vector2.Distance(new Vector2(view.x,view.y),new Vector2(0.5f,0.5f)),Is.LessThan(0.01f),"The creature remains the centre of the view.");
            }
            Assert.That(_director.Space,Is.GreaterThanOrEqualTo(0.2f));
            _director.ExitPlanetView();
            yield return Until(()=>!_director.IsAway,"return to the creature");
            yield return Until(()=>_planet.ShownTiles>100,"fine close terrain");
            Assert.That(_planet.ResidentTiles,Is.GreaterThan(96));
        }

        [UnityTest]
        public IEnumerator TheHostCanChangeTheStarSystem()
        {
            var backdrop = Object.FindFirstObjectByType<SpaceBackdrop>();
            yield return Until(() => backdrop.Model != null, "the star system");

            Assert.That(_planet.ApplySystem(7, 3), Is.True);
            yield return Until(() => backdrop.Model.Planets.Count == 7 && backdrop.Model.Moons.Count == 3, "the system to be rebuilt");

            _planet.ApplySystem(999, 999);
            Assert.That(_planet.Current().PlanetCount, Is.EqualTo(Leeway.World.Domain.PlanetSettings.MaxPlanets));
            Assert.That(_planet.Current().MoonCount, Is.EqualTo(Leeway.World.Domain.PlanetSettings.MaxMoons));
        }


        [UnityTest]
        public IEnumerator ASecondIslandStreamsCollisionAndKeepsTheCreatureStanding()
        {
            var model=_planet.Model;
            Assert.That(model.PatchCount, Is.GreaterThan(1));
            var patch=model.GetPatch(1);
            yield return Until(()=>Object.FindObjectsByType<Leeway.CreatureEditor.CreatureBody>(FindObjectsSortMode.None).Any(b=>b.IsOwner),"local player spawn");
            var body=Object.FindObjectsByType<Leeway.CreatureEditor.CreatureBody>(FindObjectsSortMode.None).First(b=>b.IsOwner);
            var rigid=body.GetComponent<Rigidbody>();
            Vector3 point=patch.ToWorld(patch.Field.HubCenter,_planet.Center);
            yield return Until(()=>Leeway.Gravity.GravityWorld.EnsureGround(point,body.gameObject.scene),"collision on second island");
            var frame=Leeway.Gravity.GravityWorld.FrameAt(point,body.gameObject.scene);
            float stand=Leeway.Creature.Domain.GenomeStatRules.StandHeight(body.Genome,body.Rules);
            rigid.position=point+frame.Up*stand;rigid.rotation=frame.Rotation(0);rigid.linearVelocity=Vector3.zero;
            body.transform.SetPositionAndRotation(rigid.position,rigid.rotation);
            body.GetComponent<Leeway.CreatureEditor.CreaturePuppet>().Teleport();
            Physics.SyncTransforms();
            yield return Until(()=>Object.FindObjectsByType<IslandGenerator>(FindObjectsSortMode.None).Any(g=>g.PlanetIslandIndex==1&&g.Projection!=null&&ReferenceEquals(g.Projection.Field,patch.Field)&&g.transform.Find("GeneratedIsland").gameObject.activeSelf),"second island details");
            yield return new WaitForSeconds(1);
            Vector3 up=Leeway.Gravity.GravityWorld.UpAt(body.transform.position,body.gameObject.scene);
            int mask=~((1<<body.gameObject.layer)|(1<<LayerMask.NameToLayer("CreatureRagdoll")));
            Assert.That(Physics.Raycast(body.transform.position+up*0.05f,-up,out var hit,10,mask,QueryTriggerInteraction.Ignore),Is.True);
            Assert.That(hit.distance-0.05f,Is.EqualTo(stand).Within(0.3f));
            Assert.That(Vector3.Dot(body.transform.up,up),Is.GreaterThan(0.99f));
            Assert.That(_planet.Model,Is.SameAs(model));
        }

        [UnityTest]
        public IEnumerator CloseSurfaceRefinesAndSurvivesCacheEvictionWhileStandingStill()
        {
            yield return Until(()=>_planet.ShownTiles>50 || _planet.ResidentTiles>800, "useful close terrain refinement");
            Assert.That(_planet.ShownTiles,Is.GreaterThan(50),"Useful sibling replacements must complete before unused fine leaves consume the tile budget.");
            yield return new WaitForSeconds(1);
            var terrain=(PlanetTerrain)typeof(PlanetView).GetField("_terrain",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(_planet);
            var camera=Camera.main;
            int before=terrain.ShownTiles;
            for(int tick=0;tick<420;tick++)terrain.Update(camera,true);
            Quaternion rotation=camera.transform.rotation;
            try
            {
                camera.transform.rotation=rotation*Quaternion.Euler(0,2,0);
                terrain.Update(camera,true);
                Assert.That(terrain.ShownTiles,Is.GreaterThan(before/2),"A small camera turn after cache expiry must preserve detailed coverage instead of falling back to root tiles.");
            }
            finally {camera.transform.rotation=rotation;}
        }


        [UnityTest]
        public IEnumerator OneWheelStepOnlyChangesDistanceWithoutLaunchingAnOverview()
        {
            Quaternion rotation=Camera.main.transform.rotation;
            Vector3 position=Camera.main.transform.position;
            _director.ZoomBy(-1);
            float initial=_director.Distance;
            Assert.That(Vector3.Distance(Camera.main.transform.position,position),Is.LessThan(0.001f),"Input itself must not teleport the camera.");
            Assert.That(_director.TargetDistance,Is.LessThan(initial*1.2f),"One notch must remain one incremental zoom step.");
            float target=_director.TargetDistance;
            yield return new WaitForSeconds(0.8f);
            Assert.That(_director.Distance,Is.EqualTo(target).Within(0.03f));
            Assert.That(Quaternion.Angle(rotation,Camera.main.transform.rotation),Is.LessThan(0.1f));
            yield return new WaitForSeconds(0.5f);
            Assert.That(_director.Distance,Is.EqualTo(target).Within(0.01f),"The camera must stop when the wheel stops.");
            Assert.That(_director.Space,Is.EqualTo(0).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator ZoomRespectsCreatureClearanceAndTheMaximumDistance()
        {
            _director.ZoomBy(-1000);
            Assert.That(_director.TargetDistance,Is.EqualTo(_director.MaximumZoomDistance).Within(0.01f));
            _director.ZoomBy(1000);
            Assert.That(_director.TargetDistance,Is.EqualTo(_director.MinimumZoomDistance).Within(0.01f));
            yield return new WaitForSeconds(1);
            Assert.That(_director.Distance,Is.GreaterThanOrEqualTo(_director.MinimumZoomDistance));
            Assert.That(_director.Distance,Is.LessThanOrEqualTo(_director.MaximumZoomDistance));
        }

        [UnityTest]
        public IEnumerator ZoomingInFromTheFarSideReturnsToTheCreatureWithoutCrossingThePlanet()
        {
            _director.ZoomBy(-1000);
            yield return Until(()=>_director.Distance>_planet.Radius*3,"safe orbit distance");
            _director.OrbitByDegrees(new Vector2(180,-110));
            yield return new WaitForSeconds(1);
            Vector3 up=(_director.FocusPoint-_planet.Center).normalized;
            Assert.That(Vector3.Dot((Camera.main.transform.position-_planet.Center).normalized,up),Is.LessThan(0),"The fixture is on the far side of the planet.");
            Quaternion before=Camera.main.transform.rotation;
            _director.ZoomBy(1);
            Assert.That(_director.IsRecalling,Is.True);
            Assert.That(Quaternion.Angle(before,Camera.main.transform.rotation),Is.LessThan(0.001f),"Recall starts with the current pose.");
            float end=Time.realtimeSinceStartup+5;
            while(_director.IsRecalling&&Time.realtimeSinceStartup<end)
            {
                yield return null;
                Assert.That(Vector3.Distance(Camera.main.transform.position,_planet.Center),Is.GreaterThan(_planet.Radius),"Return must travel around the planet, never through it.");
                var point=Camera.main.WorldToViewportPoint(_director.FocusPoint);
                Assert.That(Vector2.Distance(new Vector2(point.x,point.y),new Vector2(0.5f,0.5f)),Is.LessThan(0.01f));
            }
            Assert.That(_director.IsRecalling,Is.False);
            Assert.That(Vector3.Dot((Camera.main.transform.position-_planet.Center).normalized,up),Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator SurfaceWheelHandoffAddsOneStepAtTheActualCurrentPose()
        {
            var orbit=Object.FindFirstObjectByType<Leeway.CreatureEditor.CreatureOrbitCamera>();
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var play=(CinemachineCamera)typeof(Leeway.CreatureEditor.CreatureOrbitCamera).GetField("_playCamera",flags).GetValue(orbit);
            yield return Until(()=>play.Follow!=null,"play camera target");
            int priority=play.Priority.Value;
            play.Priority.Value=40;
            typeof(Leeway.CreatureEditor.CreatureOrbitCamera).GetField("_zoom",flags).SetValue(orbit,3f);
            typeof(Leeway.CreatureEditor.CreatureOrbitCamera).GetField("_smoothedZoom",flags).SetValue(orbit,3f);
            try
            {
                yield return Until(()=>CinemachineCore.IsLive(play),"surface camera live");
                yield return new WaitForSeconds(0.3f);
                Quaternion rotation=Camera.main.transform.rotation;
                Vector3 position=Camera.main.transform.position;
                Assert.That(orbit.ZoomOverride,Is.Not.Null);
                Assert.That(orbit.ZoomOverride(-120),Is.True);
                Assert.That(Vector3.Distance(position,Camera.main.transform.position),Is.LessThan(0.001f));
                Assert.That(_director.TargetDistance,Is.LessThan(_director.Distance*1.2f));
                Assert.That(_director.InPlanetView,Is.False,"Extending ordinary zoom must not launch an overview.");
                yield return new WaitForSeconds(0.5f);
                Assert.That(Quaternion.Angle(rotation,Camera.main.transform.rotation),Is.LessThan(0.1f));
                Assert.That(_director.Space,Is.EqualTo(0).Within(0.01f));
            }
            finally {play.Priority.Value=priority;}
        }


        [UnityTest]
        public IEnumerator ExtendedCameraKeepsFallenCreatureInFrameWithoutRotating()
        {
            yield return Until(()=>Object.FindObjectsByType<Leeway.CreatureEditor.CreatureBody>(FindObjectsSortMode.None).Any(b=>b.IsOwner),"local player");
            var body=Object.FindObjectsByType<Leeway.CreatureEditor.CreatureBody>(FindObjectsSortMode.None).First(b=>b.IsOwner);
            var rig=Object.FindFirstObjectByType<Leeway.CreatureEditor.CreatureEditorCameraRig>();
            rig.SetMode(Leeway.CreatureEditor.CreatureEditorMode.Play);
            yield return new WaitForSeconds(0.5f);
            yield return Until(()=>!Camera.main.GetComponent<CinemachineBrain>().IsBlending,"play camera blend complete");
            _director.ZoomBy(-1);
            Quaternion rotation=Camera.main.transform.rotation;
            Assert.That(body.Knockdown(body.Surface.Heading(0)*12+body.Up*3,1),Is.True);
            float end=Time.realtimeSinceStartup+0.8f;
            while(Time.realtimeSinceStartup<end)
            {
                yield return null;
                Assert.That(Quaternion.Angle(rotation,Camera.main.transform.rotation),Is.LessThan(0.2f),
                    "A rolling body must not turn the orbit camera.");
                var point=Camera.main.WorldToViewportPoint(_director.FocusPoint);
                Assert.That(Vector2.Distance(new Vector2(point.x,point.y),new Vector2(0.5f,0.5f)),Is.LessThan(0.02f));
            }
            rig.SetMode(Leeway.CreatureEditor.CreatureEditorMode.Sculpt);
        }


        [UnityTest]
        public IEnumerator PublishedGroundMatchesCollisionAndNeverChangesWithZoom()
        {
            yield return Until(()=>Object.FindObjectsByType<Leeway.CreatureEditor.CreatureBody>(FindObjectsSortMode.None).Any(b=>b.IsOwner),"local player");
            var body=Object.FindObjectsByType<Leeway.CreatureEditor.CreatureBody>(FindObjectsSortMode.None).First(b=>b.IsOwner);
            Assert.That(Leeway.Gravity.GravityWorld.IsSceneReady(body.gameObject.scene),Is.True);
            yield return Until(()=>_planet.StableGroundReadyAt(body.transform.position),"stable visible gameplay ground");
            int level=_planet.PlayableWorld.Collisions.Level;
            var key=Leeway.World.Domain.PlanetTileKey.At(body.transform.position-_planet.Center,level);
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var terrain=(PlanetTerrain)typeof(PlanetView).GetField("_terrain",flags).GetValue(_planet);
            var nodes=(System.Collections.IDictionary)typeof(PlanetTerrain).GetField("_nodes",flags).GetValue(terrain);
            object node=nodes[key];
            var nodeFlags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public;
            var mesh=(Mesh)node.GetType().GetField("Mesh",nodeFlags).GetValue(node);
            var tile=(GameObject)node.GetType().GetField("Object",nodeFlags).GetValue(node);
            Assert.That(mesh,Is.Not.Null);
            Assert.That(tile.activeInHierarchy,Is.True,"The final gameplay mesh is already presented at spawn.");
            var collision=Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None).First(c=>c.name=="Collision "+key);
            var probe=new GameObject("Visible ground test ray"){layer=2};
            try
            {
                probe.transform.SetPositionAndRotation(tile.transform.position,tile.transform.rotation);
                var rendered=probe.AddComponent<MeshCollider>();rendered.sharedMesh=mesh;
                Vector3 direction=key.CenterDirection;
                float height=_planet.Model.Sample(direction).GroundHeight;
                var ray=new Ray(_planet.Center+direction*(_planet.Radius+height+20),-direction);
                Assert.That(rendered.Raycast(ray,out var visibleHit,50),Is.True);
                Assert.That(collision.Raycast(ray,out var physicalHit,50),Is.True);
                Assert.That(Vector3.Distance(visibleHit.point,physicalHit.point),Is.LessThan(0.005f),
                    "A visible hill must not stand above the collision body the creature walks on.");
                var vertices=mesh.vertices;
                _director.EnterPlanetView();
                yield return Until(()=>_director.Space>0.9f,"orbital view");
                Assert.That(node.GetType().GetField("Mesh",nodeFlags).GetValue(node),Is.SameAs(mesh),
                    "Zoom cannot replace gameplay triangles with a different terrain height.");
                _director.ExitPlanetView();
                yield return Until(()=>!_director.IsAway,"return to creature");
                Assert.That(node.GetType().GetField("Mesh",nodeFlags).GetValue(node),Is.SameAs(mesh));
                CollectionAssert.AreEqual(vertices,mesh.vertices,"Ground geometry is immutable after publication.");
            }
            finally{Object.Destroy(probe);}
        }

        private static IEnumerator Until(System.Func<bool> condition, string what, float timeout = 25f)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out waiting for " + what);
                yield return null;
            }
        }
    }
}
