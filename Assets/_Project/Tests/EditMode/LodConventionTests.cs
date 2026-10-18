using System.Collections.Generic;
using Leeway.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace Leeway.Tests
{
    public class LodConventionTests
    {
        private readonly List<GameObject> _made = new();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made) if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
        }

        private GameObject Model(params string[] childNames)
        {
            var root = new GameObject("Model");
            _made.Add(root);
            foreach (string name in childNames)
            {
                var child = GameObject.CreatePrimitive(PrimitiveType.Cube);
                child.name = name;
                child.transform.SetParent(root.transform, false);
            }
            return root;
        }

        [Test]
        public void ANodeNamedWithASuffixBelongsToThatLevel()
        {
            GameObject model = Model("Tree_LOD0", "Tree_LOD1", "Fruit");
            Dictionary<Renderer, int> levels = LodConvention.LevelsOf(model);

            Assert.That(levels[model.transform.Find("Tree_LOD0").GetComponent<Renderer>()], Is.EqualTo(0));
            Assert.That(levels[model.transform.Find("Tree_LOD1").GetComponent<Renderer>()], Is.EqualTo(1));
            Assert.That(levels[model.transform.Find("Fruit").GetComponent<Renderer>()], Is.EqualTo(LodConvention.Shared));
        }

        [Test]
        public void ASuffixOnANodeAboveCountsForEverythingUnderIt()
        {
            var model = new GameObject("Model");
            _made.Add(model);
            var group = new GameObject("Body_LOD2").transform;
            group.SetParent(model.transform, false);
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mesh.name = "Mesh";
            mesh.transform.SetParent(group, false);

            Assert.That(LodConvention.LevelsOf(model)[mesh.GetComponent<Renderer>()], Is.EqualTo(2));
        }

        [Test]
        public void ALodGroupIsBuiltFromTheNamedLevelsInOrder()
        {
            GameObject model = Model("Tree_LOD0", "Tree_LOD1", "Tree_LOD2");

            LODGroup group = LodConvention.Ensure(model);

            Assert.That(group.lodCount, Is.EqualTo(3));
            LOD[] lods = group.GetLODs();
            Assert.That(lods[0].renderers.Length, Is.EqualTo(1));
            Assert.That(lods[0].renderers[0].name, Is.EqualTo("Tree_LOD0"));
            Assert.That(lods[2].renderers[0].name, Is.EqualTo("Tree_LOD2"));
            Assert.That(lods[0].screenRelativeTransitionHeight, Is.GreaterThan(lods[1].screenRelativeTransitionHeight));
            Assert.That(lods[1].screenRelativeTransitionHeight, Is.GreaterThan(lods[2].screenRelativeTransitionHeight));
        }

        [Test]
        public void LevelsWithGapsInTheirNumbersAreStillUsedInOrder()
        {
            GameObject model = Model("A_LOD0", "A_LOD3");

            LODGroup group = LodConvention.Ensure(model);

            Assert.That(group.lodCount, Is.EqualTo(2), "No empty level for the missing numbers.");
            Assert.That(group.GetLODs()[1].renderers[0].name, Is.EqualTo("A_LOD3"));
        }

        [Test]
        public void SharedPartsAreDrawnInEveryLevel()
        {
            GameObject model = Model("Tree_LOD0", "Tree_LOD1", "Fruit");

            LOD[] lods = LodConvention.Ensure(model).GetLODs();

            Assert.That(lods[0].renderers.Length, Is.EqualTo(2));
            Assert.That(lods[1].renderers.Length, Is.EqualTo(2));
        }

        [Test]
        public void AModelWithNoLevelsIsOneLevelThatIsCulledWhenSmall()
        {
            GameObject model = Model("Rock");

            LODGroup group = LodConvention.Ensure(model);

            Assert.That(group.lodCount, Is.EqualTo(1));
            Assert.That(group.GetLODs()[0].screenRelativeTransitionHeight, Is.EqualTo(LodProfile.Current.CullHeight).Within(1e-5f));
        }

        [Test]
        public void AModelThatBringsItsOwnLodGroupIsLeftAlone()
        {
            GameObject model = Model("Tree_LOD0", "Tree_LOD1");
            var own = model.AddComponent<LODGroup>();
            own.SetLODs(new[] { new LOD(0.5f, new[] { model.transform.GetChild(0).GetComponent<Renderer>() }), new LOD(0.1f, new[] { model.transform.GetChild(1).GetComponent<Renderer>() }) });

            LODGroup group = LodConvention.Ensure(model);

            Assert.That(group, Is.SameAs(own));
            Assert.That(group.GetLODs()[0].screenRelativeTransitionHeight, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(LodConvention.LevelsOf(model)[model.transform.GetChild(1).GetComponent<Renderer>()], Is.EqualTo(1));
        }

        [Test]
        public void AModelWithNothingToDrawGetsNoGroup()
        {
            var model = new GameObject("Empty");
            _made.Add(model);

            Assert.That(LodConvention.Ensure(model), Is.Null);
        }
    }
}
