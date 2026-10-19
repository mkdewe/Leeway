using System;
using System.Collections.Generic;
using Leeway.CreatureEditor;
using Leeway.Gravity;
using Leeway.World;
using Leeway.World.Domain;
using UnityEngine;

namespace Leeway.Planet
{
    /// <summary>Owns the playable surface. Camera distance only changes render LOD.
    /// Players are admitted after a cooked collision tile exists at the home hub.</summary>
    public sealed class PlanetPlayableWorld : MonoBehaviour
    {
        private PlanetView _view;
        private IslandGenerator _home;
        private PlanetModel _pending, _active;
        private Transform _collisionRoot;
        private PlanetCollisionTiles _collisions;
        private GravityWorld _gravity;
        private PlanetCollisionTiles _previousCollisions;
        private Transform _previousRoot;
        private readonly List<Vector3> _targets=new();
        private readonly List<Vector3> _requests=new();
        private readonly List<Vector3> _renderTargets=new();
        private readonly Dictionary<int,IslandGenerator> _details=new();
        private Vector3 _center;
        private bool _suspended, _activating;
        public bool IsPreparing=>_pending!=null||_activating;
        public bool IsReady=>_active!=null&&_pending==null&&!_activating;
        public PlanetCollisionTiles Collisions=>_collisions;
        public Vector3 Center=>_center;
        public bool Suspended
        {
            get=>_suspended;
            set { _suspended=value; if(value)GravityWorld.MarkSceneReady(gameObject.scene);
                else if(_view!=null&&_view.Model!=null&&_active!=_view.Model)OnRebuilt(); }
        }

        public void Initialize(PlanetView view,IslandGenerator home)
        {
            _view=view;_home=home;
            _home.PlanetSurfaceOwnsGround=true;
            _view.Rebuilt+=OnRebuilt;
            GravityWorld.BeginLoading(gameObject.scene);
        }

        private void OnRebuilt()
        {
            if(_suspended)return;
            GravityWorld.BeginLoading(gameObject.scene);
            _pending=_view.Model;
            PrepareCollisions(_pending,_view.Center);
        }

        private void PrepareCollisions(PlanetModel model,Vector3 center)
        {
            if (_active != null && _previousCollisions == null)
            {
                _previousCollisions=_collisions; _previousRoot=_collisionRoot;
            }
            else
            {
                _collisions?.Dispose();
                if(_collisionRoot!=null)Destroy(_collisionRoot.gameObject);
            }
            _collisionRoot=new GameObject("Planet collision tiles").transform;
            _collisionRoot.SetParent(transform,false);
            _collisionRoot.position=center;
            _collisions=new PlanetCollisionTiles(model,_collisionRoot,FindFirstObjectByType<CreaturePhysicsWorld>());
        }

        private void LateUpdate()
        {
            if(_suspended||_view==null||_activating)return;
            if(_pending==null&&_active==null&&_view.Model!=null)OnRebuilt();
            if(_collisions==null)return;
            _targets.Clear();_renderTargets.Clear();
            var bodies=FindObjectsByType<CreatureBody>(FindObjectsSortMode.None);
            if(_pending!=null)
            {
                Vector3 hub=_pending.GetPatch(0).Field.HubCenter+Vector3.up;
                _targets.Add(_pending.GetPatch(0).ToWorld(hub,_view.Center));
                _renderTargets.Add(_targets[0]);
                foreach(var body in bodies)
                {
                    Locate(body.transform.position,out int index,out Vector3 at);
                    index=Mathf.Min(index,_pending.PatchCount-1);
                    _targets.Add(_pending.GetPatch(index).ToWorld(at,_view.Center));
                    if(body.OwnerId>=0)_renderTargets.Add(_targets[_targets.Count-1]);
                }
            }
            else
            {
                _renderTargets.Add(_home.HubPosition);
                foreach(var body in bodies)
                {
                    Vector3 point=body.transform.position;
                    var puppet=body.GetComponent<CreaturePuppet>();
                    if(body.IsKnockedDown&&puppet!=null&&puppet.TryGetBodyCentre(out var fallen))point=fallen;
                    _targets.Add(point);
                    if(body.OwnerId>=0)_renderTargets.Add(point);
                    if(body.TryGetComponent<Rigidbody>(out var rb))
                    {
                        _targets.Add(point+rb.linearVelocity*2);
                        if(body.OwnerId>=0)_renderTargets.Add(point+rb.linearVelocity*2);
                    }
                }
                if(_targets.Count==0)_targets.Add(_home.HubPosition+_home.LocalUp(_home.HubPosition));
            }
            _view.SetGroundAnchors(_renderTargets,_collisions.Level);
            _targets.AddRange(_requests);
            _collisions.Update(_targets);
            if (_previousCollisions != null)
            {
                var oldTargets=new List<Vector3>();
                foreach(var body in bodies)oldTargets.Add(body.transform.position);
                _previousCollisions.Update(oldTargets);
            }
            for(int i=_requests.Count-1;i>=0;i--)if(_collisions.ReadyAt(_requests[i]))_requests.RemoveAt(i);
            bool allReady=true;
            foreach(var point in _targets)allReady&=_collisions.ReadyAt(point);
            if(_pending!=null&&allReady)StartCoroutine(Activate(bodies));
            if(_active!=null)
            {
                // Details are generated only on visited islands; all islands already have ground/collisions.
                for(int i=1;i<_active.PatchCount;i++)
                {
                    if(_details.ContainsKey(i))continue;
                    bool visited=false;
                    foreach(var body in bodies)
                        if(body.OwnerId>=0&&_active.GetPatch(i).WeightAt((body.transform.position-_center).normalized)>0.2f)visited=true;
                    if(!visited)continue;
                    var go=new GameObject("Planet island "+i);go.transform.SetParent(transform,false);
                    var generator=go.AddComponent<IslandGenerator>();
                    _details[i]=generator;
                    StartCoroutine(BuildDetails(generator,i));
                    break;
                }
            }
        }

        private System.Collections.IEnumerator BuildDetails(IslandGenerator generator,int index)
        {
            yield return generator.GenerateOnPlanetAsync(_active.GetPatch(index),_center,_home.Biome);
            IslandGenerator.NotifyPlanetIslandReady(generator,index);
            FindFirstObjectByType<CreaturePhysicsWorld>()?.MirrorEnvironment();
        }

        private System.Collections.IEnumerator Activate(CreatureBody[] bodies)
        {
            _activating=true;
            var frozen=new Dictionary<Rigidbody,bool>();
            var readyVelocities=new Dictionary<Rigidbody,Vector3>();
            var model=_pending;
            Vector3 nextCenter=_view.Center;
            var coordinates=new Vector3[bodies.Length];
            var islands=new int[bodies.Length];
            var rotations=new Quaternion[bodies.Length];
            var velocities=new Vector3[bodies.Length];
            for(int i=0;i<bodies.Length;i++)
            {
                Locate(bodies[i].transform.position,out islands[i],out coordinates[i]);
                Quaternion old=_active!=null?_active.GetPatch(islands[i]).RotationAt(coordinates[i]):_home.transform.rotation;
                islands[i]=Mathf.Min(islands[i],model.PatchCount-1);
                rotations[i]=Quaternion.Inverse(old)*bodies[i].transform.rotation;
                velocities[i]=bodies[i].TryGetComponent<Rigidbody>(out var rb)?Quaternion.Inverse(old)*rb.linearVelocity:Vector3.zero;
            }
            foreach(var body in bodies)if(body.TryGetComponent<Rigidbody>(out var rigid)){frozen[rigid]=rigid.isKinematic;rigid.isKinematic=true;}
            try
            {
            foreach(var entry in _details)if(entry.Key!=0&&entry.Value!=null)Destroy(entry.Value.gameObject);
            _details.Clear();
            _center=nextCenter;
            yield return _home.GenerateOnPlanetAsync(model.GetPatch(0),_center,reveal:false);
            _details[0]=_home;
            if(_gravity==null)
            {
                var go=new GameObject("Planet gravity");go.transform.SetParent(transform,false);
                _gravity=go.AddComponent<GravityWorld>();
            }
            _gravity.transform.position=_center;
            _gravity.Configure(true,model.Radius,9.81f);
            _gravity.WaterRadiusSampler=direction=>
            {
                var sample=model.Sample(direction);
                return sample.GroundHeight<sample.WaterHeight+0.05f?(float?)(model.Radius+sample.WaterHeight):null;
            };
            _gravity.GroundReady=point=>_collisions!=null&&_collisions.ReadyAt(point)&&_view.StableGroundReadyAt(point);
            _gravity.RequestGround=point=>{if(_requests.Count<256&&!_requests.Contains(point))_requests.Add(point);};
            for(int i=0;i<bodies.Length;i++)
            {
                if(bodies[i]==null)continue;
                var patch=model.GetPatch(islands[i]);
                Quaternion basis=patch.RotationAt(coordinates[i]);
                Vector3 position=patch.ToWorld(coordinates[i],_center);
                bodies[i].transform.SetPositionAndRotation(position,basis*rotations[i]);
                if(bodies[i].TryGetComponent<Rigidbody>(out var rb))
                {
                    rb.position=position;rb.rotation=basis*rotations[i];
                    readyVelocities[rb]=basis*velocities[i];
                }
                bodies[i].GetComponent<CreaturePuppet>()?.Teleport();

            }
            foreach(var flora in FindObjectsByType<IslandFlora>(FindObjectsSortMode.None))flora.BuildArchipelago(model,_center);
            IslandGenerator.NotifyPlanetIslandReady(_home,0);
            Physics.SyncTransforms();
            FindFirstObjectByType<CreaturePhysicsWorld>()?.MirrorEnvironment();
            _previousCollisions?.Dispose();_previousCollisions=null;
            if(_previousRoot!=null)Destroy(_previousRoot.gameObject);
            _previousRoot=null;
            // Do not release bodies or publish the world while the render mesh is still
            // replacing its coarse fallback. Details have been projected but remain hidden.
            while(!_view.SurfacePrepared)yield return null;
            _home.SetPlanetPresentation(true);
            _active=model;_pending=null;
            }
            finally
            {
                foreach(var pair in frozen)if(pair.Key!=null)
                {
                    pair.Key.isKinematic=pair.Value;
                    if(!pair.Value&&readyVelocities.TryGetValue(pair.Key,out var velocity))pair.Key.linearVelocity=velocity;
                }
                _activating=false;
                if(_active==model&&_pending==null)
                {
                    _view.PublishPreparedSurface();
                    GravityWorld.MarkSceneReady(gameObject.scene);
                    foreach(var body in bodies)if(body!=null)body.GetComponent<CreatureLocomotion>()?.Resume();
                }
            }
        }

        private void Locate(Vector3 point,out int island,out Vector3 chart)
        {
            island=0;
            if(_active==null){chart=_home.WorldToField(point);return;}
            Vector3 direction=(point-_center).normalized;
            float best=float.NegativeInfinity;
            for(int i=0;i<_active.PatchCount;i++)
            {
                float dot=Vector3.Dot(direction,_active.GetPatch(i).Direction);
                if(dot>best){best=dot;island=i;}
            }
            chart=_active.GetPatch(island).ToField(point,_center);
        }

        private void OnEnable()
        {
            if(_view==null||_view.Model==null||_suspended)return;
            if(_active==_view.Model&&_pending==null)GravityWorld.MarkSceneReady(gameObject.scene);
            else OnRebuilt();
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            _activating=false;
        }

        private void OnDestroy()
        {
            if(_view!=null)_view.Rebuilt-=OnRebuilt;
            _collisions?.Dispose();_previousCollisions?.Dispose();
            if(_gravity!=null)Destroy(_gravity.gameObject);
            GravityWorld.MarkSceneReady(gameObject.scene);
        }
    }
}
