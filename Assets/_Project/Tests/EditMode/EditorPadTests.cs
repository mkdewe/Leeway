using Leeway.CreatureEditor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Leeway.Tests
{
    /// <summary>
    /// Tests for the commit gate. One line of logic, but both of its rules were written against a
    /// concrete defect, so they are worth nailing down.
    /// </summary>
    public class EditorPadTests
    {
        private const string PrefabPath = "Assets/_Project/Features/CreatureEditor/Prefabs/PlaygroundCreature.prefab";

        private GameObject _instance;
        private CreatureBody _body;

        [SetUp]
        public void SetUp()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, $"No creature prefab at {PrefabPath}.");

            _instance = Object.Instantiate(prefab);
            _body = _instance.GetComponent<CreatureBody>();
            Assert.IsNotNull(_body, "The creature prefab has no CreatureBody component.");
        }

        [TearDown]
        public void TearDown()
        {
            if (_instance != null) Object.DestroyImmediate(_instance);
        }

        [Test]
        public void Body_StartsOutsideAnyPad()
        {
            Assert.IsFalse(_body.IsInEditorPad, "A freshly spawned creature stands in no pad.");
        }

        [Test]
        public void EnterThenExit_ReturnsOutside()
        {
            _body.EnterEditorPad();
            Assert.IsTrue(_body.IsInEditorPad);

            _body.ExitEditorPad();
            Assert.IsFalse(_body.IsInEditorPad);
        }

        /// <summary>
        /// Why this is a counter and not a <c>bool</c>: crossing between two touching pads, the player
        /// enters the second one before leaving the first. On a flag, that exit would clear the pad they
        /// are currently standing in.
        /// </summary>
        [Test]
        public void OverlappingPads_ExitingOneKeepsBodyInside()
        {
            _body.EnterEditorPad();
            _body.EnterEditorPad();

            _body.ExitEditorPad();
            Assert.IsTrue(_body.IsInEditorPad, "Leaving one of two pads cleared both.");

            _body.ExitEditorPad();
            Assert.IsFalse(_body.IsInEditorPad, "Leaving both pads did not let the creature out.");
        }

        /// <summary>
        /// A surplus <c>OnTriggerExit</c> (say, when a pad is destroyed under a standing creature) must
        /// not drive the counter below zero — otherwise the next entry would not unlock the commit.
        /// </summary>
        [Test]
        public void SurplusExit_DoesNotDriveCounterNegative()
        {
            _body.ExitEditorPad();
            _body.ExitEditorPad();

            _body.EnterEditorPad();
            Assert.IsTrue(_body.IsInEditorPad, "The counter went below zero and one entry stopped being enough.");
        }

        [Test]
        public void TryResolveBody_AcceptsColliderOnBodyItself()
        {
            var collider = _instance.GetComponent<CapsuleCollider>();
            Assert.IsNotNull(collider, "The creature prefab lost its locomotion capsule.");

            Assert.IsTrue(EditorPad.TryResolveBody(collider, out CreatureBody resolved));
            Assert.AreSame(_body, resolved);
        }

        /// <summary>
        /// Were the lookup going through <c>GetComponentInParent</c>, after Phase 6 every ragdoll bone
        /// would bump the counter on its own, and a trailing tail would keep the creature "in the pad"
        /// long after it had left.
        /// </summary>
        [Test]
        public void TryResolveBody_RejectsChildCollider()
        {
            var bone = new GameObject("FakeRagdollBone");
            bone.transform.SetParent(_instance.transform, false);
            var boneCollider = bone.AddComponent<SphereCollider>();

            Assert.IsFalse(EditorPad.TryResolveBody(boneCollider, out CreatureBody resolved),
                "A child collider was taken for the creature — the pad counter will drift once ragdolls are in.");
            Assert.IsNull(resolved);
        }
    }
}
