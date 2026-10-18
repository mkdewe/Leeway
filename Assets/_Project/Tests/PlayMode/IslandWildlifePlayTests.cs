using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using FishNet.Managing;
using Leeway.Combat;
using Leeway.Creature.Domain;
using Leeway.CreatureEditor;
using Leeway.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Leeway.Tests
{
    public class IslandWildlifePlayTests
    {
        private NetworkManager _network;
        private CreatureBody _attacker,_victim;
        private CreatureCombatant _combat;
        [UnitySetUp] public IEnumerator SetUp()
        {
            Application.runInBackground=true;
            yield return CreatureEditorTestScene.EnsureLoaded();
            _network=UnityEngine.Object.FindFirstObjectByType<NetworkManager>();
            var islandSeed=UnityEngine.Object.FindFirstObjectByType<IslandSeed>(FindObjectsInactive.Include);
            typeof(IslandSeed).GetField("_fixedSeed",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(islandSeed,1);
            _network.ServerManager.StartConnection();_network.ClientManager.StartConnection();
            yield return Wait(()=>_network.IsServerStarted&&_network.IsClientStarted,"Host startup");
            yield return Wait(()=>UnityEngine.Object.FindObjectsByType<NpcCreatureAgent>(FindObjectsSortMode.None).Length>=30,"Population spawn");
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if(_network){_network.ClientManager.StopConnection();_network.ServerManager.StopConnection(true);
                yield return Wait(()=>!_network.IsServerStarted&&!_network.IsClientStarted,"Host stop");}
            yield return null;
        }
        [UnityTest] public IEnumerator PopulationUsesFiveSpeciesAndDryHabitat()
        {
            var agents=UnityEngine.Object.FindObjectsByType<NpcCreatureAgent>(FindObjectsSortMode.None);
            Assert.That(agents.Select(a=>a.Species).Distinct().Count(),Is.EqualTo(9));
            var island=UnityEngine.Object.FindFirstObjectByType<IslandGenerator>();
            foreach(var a in agents)
            {
                var p=island.WorldToField(a.transform.position);
                Assert.That(island.Field.IsLand(p.x,p.z),Is.True,a.SpeciesName);
                var d=p-island.Field.HubCenter;d.y=0;
                Assert.That(d.magnitude,Is.GreaterThan(24),a.SpeciesName);
            }
            yield break;
        }
        private IEnumerator Arrange(IslandSpecies species,float gap=1.1f)
        {
            var agents=UnityEngine.Object.FindObjectsByType<NpcCreatureAgent>(FindObjectsSortMode.None);
            var a=agents.First(x=>x.Species==species);
            var b=agents.First(x=>x.Species==IslandSpecies.MossGrazer);
            _attacker=a.GetComponent<CreatureBody>();_victim=b.GetComponent<CreatureBody>();_combat=a.GetComponent<CreatureCombatant>();
            var island=UnityEngine.Object.FindFirstObjectByType<IslandGenerator>();
            var start=new Vector3(10,0,0);
            float ground=Mathf.Max(island.GroundHeight(start),island.GroundHeight(start+Vector3.forward*2));
            start.y=ground+1;
            foreach(var actor in new[]{a,b})
            {
                actor.enabled=false;
                var controller=actor.GetComponent<PlaygroundCreatureController>();controller.ServerDrive(Vector3.forward,0);
                typeof(PlaygroundCreatureController).GetField("_yaw",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(controller,0f);
                var rb=actor.GetComponent<Rigidbody>();rb.interpolation=RigidbodyInterpolation.None;rb.constraints=RigidbodyConstraints.FreezeAll;
                rb.position=start;actor.transform.position=start;rb.rotation=Quaternion.identity;actor.transform.rotation=Quaternion.identity;
                rb.linearVelocity=Vector3.zero;start.z+=gap;
                actor.GetComponent<CreaturePuppet>()?.Teleport(); // the physical body does not follow a teleport by itself
            }
            Physics.SyncTransforms();yield return new WaitForFixedUpdate();yield return null;
            Assert.That(_attacker.IsInEditorPad,Is.False);Assert.That(_victim.IsInEditorPad,Is.False);
        }
        [UnityTest] public IEnumerator StrikeHasWindupDealsDamageAndRejectsSpam()
        {
            yield return Arrange(IslandSpecies.Rockhorn);
            float before=_victim.Hp;float damage=_combat.AttackFor(CreatureAbility.Bite).Damage;
            Assert.That(_combat.ServerUseAbility(CreatureAbility.Claw),Is.False,"No arms means no claw attack.");
            Assert.That(_combat.ServerUseAbility(CreatureAbility.Bite),Is.True);
            Assert.That(_combat.IsWindingUp,Is.True);Assert.That(_victim.Hp,Is.EqualTo(before));
            Assert.That(_combat.ServerUseAbility(CreatureAbility.Horn),Is.False,"Changing attacks must not bypass cooldown.");
            yield return Wait(()=>!_combat.IsWindingUp,"Attack impact");yield return new WaitForSeconds(0.6f);
            Assert.That(_victim.Hp,Is.EqualTo(before-damage).Within(0.01f),"The telegraphed strike must hit a target in front.");
            Assert.That(_combat.ServerUseAbility(CreatureAbility.Bite),Is.False);
        }
        [UnityTest] public IEnumerator TargetOutsideTheStrikeAreaIsNotHurt()
        {
            yield return Arrange(IslandSpecies.Rockhorn);
            float before=_victim.Hp;
            var rb=_victim.GetComponent<Rigidbody>();var far=_attacker.transform.position+Vector3.forward*3.6f;
            rb.position=far;_victim.transform.position=far;_victim.GetComponent<CreaturePuppet>()?.Teleport();
            Physics.SyncTransforms();yield return new WaitForFixedUpdate();
            Assert.That(_combat.ServerUseAbility(CreatureAbility.Bite),Is.True);
            var phases=new System.Collections.Generic.List<AttackPhase>();
            var life=_attacker.GetComponent<CreatureLife>();life.AttackPhaseChanged+=(a,p)=>phases.Add(p);
            yield return Wait(()=>!_combat.IsWindingUp,"Attack impact");yield return new WaitForSeconds(0.6f);
            Assert.That(_victim.Hp,Is.EqualTo(before),"Nothing stood in the jaw's area while the window was open.");
            Assert.That(phases,Does.Contain(AttackPhase.Active),"The hit window event frame was reached.");
        }
        [UnityTest] public IEnumerator KnockdownCancelsPendingHornStrike()
        {
            yield return Arrange(IslandSpecies.Rockhorn);
            float before=_victim.Hp;
            Assert.That(_combat.ServerUseAbility(CreatureAbility.Horn),Is.True);
            Assert.That(_attacker.Knockdown(Vector3.forward*50,0),Is.True);
            yield return new WaitForSeconds(0.7f);
            Assert.That(_combat.IsWindingUp,Is.False);Assert.That(_victim.Hp,Is.EqualTo(before));
        }
        [UnityTest] public IEnumerator ScanHasCooldownAndCorpseCanBeConsumedOnce()
        {
            yield return Arrange(IslandSpecies.BrookGatherer,2f);
            Assert.That(_combat.ServerUseAbility(CreatureAbility.Scan),Is.True);
            Assert.That(_combat.ServerUseAbility(CreatureAbility.Scan),Is.False);
            _attacker.TakeDamage(35);float before=_attacker.Hp;_victim.TakeDamage(99999);
            Assert.That(_combat.ServerUseAbility(CreatureAbility.Forage),Is.True);
            Assert.That(_attacker.Hp,Is.GreaterThan(before));Assert.That(_attacker.Hp,Is.LessThanOrEqualTo(_attacker.MaxHp));
            Assert.That(_combat.ServerUseAbility(CreatureAbility.Forage),Is.False);
            yield return null;
            Assert.That(!_victim||!_victim.NetworkObject.IsSpawned,Is.True);
        }
        
        [UnityTest] public IEnumerator FriendlyFireRearAndTerrainBlockHits()
        {
            yield return Arrange(IslandSpecies.Rockhorn);
            var other=_victim.GetComponent<CreatureCombatant>();byte team=other.Team;
            other.ConfigureTeam(_combat.Team);float before=_victim.Hp;
            Assert.That(_combat.ServerUseAbility(CreatureAbility.Bite),Is.True);
            yield return Wait(()=>!_combat.IsWindingUp,"Friendly strike");yield return new WaitForSeconds(0.6f);
            Assert.That(_victim.Hp,Is.EqualTo(before));
            other.ConfigureTeam(team);yield return Wait(()=>_combat.CooldownRemaining<=0,"Cooldown");
            var rb=_victim.GetComponent<Rigidbody>();var behind=_attacker.transform.position-Vector3.forward*2;
            rb.position=behind;_victim.transform.position=behind;Physics.SyncTransforms();yield return new WaitForFixedUpdate();
            Assert.That(_combat.ServerUseAbility(CreatureAbility.Bite),Is.True);
            yield return Wait(()=>!_combat.IsWindingUp,"Rear strike");yield return new WaitForSeconds(0.6f);Assert.That(_victim.Hp,Is.EqualTo(before));
            yield return Wait(()=>_combat.CooldownRemaining<=0,"Cooldown");
            var front=_attacker.transform.position+Vector3.forward*1.1f;rb.position=front;_victim.transform.position=front;
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Combat LOS test wall";
            wall.transform.position=_attacker.transform.position+Vector3.forward*0.55f;wall.transform.localScale=new Vector3(3,3,0.25f);
            Physics.SyncTransforms();yield return new WaitForFixedUpdate();
            Assert.That(_combat.ServerUseAbility(CreatureAbility.Bite),Is.True);
            yield return Wait(()=>!_combat.IsWindingUp,"Blocked strike");yield return new WaitForSeconds(0.6f);UnityEngine.Object.Destroy(wall);
            Assert.That(_victim.Hp,Is.EqualTo(before));
        }
        [UnityTest] public IEnumerator ArmsAssistAllyAndPlayerRespawnsWithSameGenome()
        {
            yield return Arrange(IslandSpecies.BrookGatherer,2f);
            _victim.GetComponent<CreatureCombatant>().ConfigureTeam(_combat.Team);
            Assert.That(_victim.Knockdown(Vector3.forward*30,0),Is.True);
            Assert.That(_combat.ServerUseAbility(CreatureAbility.Forage),Is.True);
            Assert.That(_victim.IsKnockedDown,Is.False,"Arms must actually lift the ally.");
            var player=UnityEngine.Object.FindObjectsByType<CreatureBody>(FindObjectsSortMode.None).First(b=>b.IsOwner);
            var expected=IslandSpeciesLibrary.Create(IslandSpecies.BrookGatherer,player.Rules);
            Assert.That(player.ServerSetGenome(expected,out _),Is.True);
            player.TakeDamage(99999);
            Assert.That(player.IsAlive,Is.False);Assert.That(player.IsKnockedDown,Is.True);
            yield return Wait(()=>UnityEngine.Object.FindObjectsByType<CreatureBody>(FindObjectsSortMode.None)
                .Any(b=>b.IsOwner&&b!=player&&b.IsAlive),"Player respawn");
            var replacement=UnityEngine.Object.FindObjectsByType<CreatureBody>(FindObjectsSortMode.None).First(b=>b.IsOwner&&b.IsAlive);
            Assert.That(replacement.Genome.PartCount,Is.EqualTo(expected.PartCount));
            CollectionAssert.AreEqual(expected.Parts.Select(p=>p.PartId),replacement.Genome.Parts.Select(p=>p.PartId));
            CollectionAssert.AreEqual(expected.Vertebrae.Select(v=>v.Radius),replacement.Genome.Vertebrae.Select(v=>v.Radius));
            Assert.That(replacement.Hp,Is.EqualTo(replacement.MaxHp));
        }
        private static IEnumerator Wait(Func<bool> predicate,string step)
        {
            float deadline=Time.realtimeSinceStartup+35;
            for(int i=0;!predicate();i++)
            {if(Time.realtimeSinceStartup>deadline)Assert.Fail(step+" timed out");yield return null;}
        }
    }
}
