<!-- PLIK GENEROWANY — nie edytuj ręcznie. Źródło: Tools/DiagramGen/gen.mjs -->

# Features/CreatureEditor/Build

```mermaid
classDiagram
    direction LR
    class BuiltCreatureBody {
        +Transform Armature
        +Transform[] Bones
        +SkinnedMeshRenderer Renderer
        +Mesh GeneratedMesh
        +CreaturePartInstance[] PartInstances
        +CreatureStats Stats
    }
    class CreatureBodyBuilder {
        <<static>>
        +string BodyObjectName$
        +Build(CreatureGenome, CreaturePartCatalog, CreatureBodyBuildSettings, Transform, CreatureStatTuning) BuiltCreatureBody$
    }
    class CreatureColliderFitter {
        <<static>>
        +Fit(CapsuleCollider, CreatureGenome, Matrix4x4[], CreatureBodyBuildSettings) void$
    }
    class CreaturePartInstance {
        <<struct>>
        +GameObject Object
        +int GeneIndex
        +bool Mirrored
    }
    class CreaturePartInstantiator {
        <<static>>
        +Instantiate(CreatureGenome, Transform[], CreaturePartCatalog) CreaturePartInstance[]$
    }
    class CreatureRigBuilder {
        <<static>>
        +string ArmatureName$
        +Build(CreatureGenome, Transform, Transform) Transform[]$
        +ComputeBoneToRoot(CreatureGenome) Matrix4x4[]$
    }
    class ProceduralLeg {
        +LegSpec Spec
        +GaitProfile Gait
        +Vector3 FootPosition
        +Vector3 HipPosition
        +Tick(float, Vector3, LayerMask, float, float) void
    }
    class SpineMeshGenerator {
        <<static>>
        +string MeshName$
        +Generate(CreatureGenome, Matrix4x4[], CreatureBodyBuildSettings, Matrix4x4[]) Mesh$
    }
    class CreatureBodyBuildSettings {
        <<ScriptableObject>>
    }
    class CreatureGenome {
        <<domain>>
    }
    class CreaturePartCatalog {
        <<ScriptableObject>>
    }
    class CreaturePartDefinition {
        <<ScriptableObject>>
    }
    class CreatureStatTuning {
        <<domain>>
    }
    class CreatureStats {
        <<domain>>
    }
    class GaitProfile {
        <<domain>>
    }
    class GenomeStatRules {
        <<static>>
    }
    class LegSpec {
        <<domain>>
    }
    class LimbIk {
        <<static>>
    }
    class PartCategory {
        <<enum>>
    }
    class PartGene {
        <<domain>>
    }
    class PartPlacement {
        <<static>>
    }
    class PartPose {
        <<domain>>
    }
    class PartRuleSet {
        <<domain>>
    }
    class SpineAnchor {
        <<static>>
    }
    class VertebraGene {
        <<domain>>
    }

    BuiltCreatureBody --> CreaturePartInstance
    BuiltCreatureBody --> CreatureStats
    CreatureBodyBuilder --> BuiltCreatureBody
    CreatureBodyBuilder --> CreatureBodyBuildSettings
    CreatureBodyBuilder --> CreatureGenome
    CreatureBodyBuilder --> CreaturePartCatalog
    CreatureBodyBuilder ..> CreaturePartInstance
    CreatureBodyBuilder ..> CreaturePartInstantiator
    CreatureBodyBuilder ..> CreatureRigBuilder
    CreatureBodyBuilder ..> CreatureStats
    CreatureBodyBuilder --> CreatureStatTuning
    CreatureBodyBuilder ..> GenomeStatRules
    CreatureBodyBuilder ..> PartRuleSet
    CreatureBodyBuilder ..> SpineMeshGenerator
    CreatureColliderFitter --> CreatureBodyBuildSettings
    CreatureColliderFitter --> CreatureGenome
    CreaturePartInstantiator --> CreatureGenome
    CreaturePartInstantiator --> CreaturePartCatalog
    CreaturePartInstantiator ..> CreaturePartDefinition
    CreaturePartInstantiator --> CreaturePartInstance
    CreaturePartInstantiator ..> PartCategory
    CreaturePartInstantiator ..> PartGene
    CreaturePartInstantiator ..> PartPlacement
    CreaturePartInstantiator ..> PartPose
    CreatureRigBuilder --> CreatureGenome
    CreatureRigBuilder ..> SpineAnchor
    CreatureRigBuilder ..> VertebraGene
    ProceduralLeg --> GaitProfile
    ProceduralLeg --> LegSpec
    ProceduralLeg ..> LimbIk
    SpineMeshGenerator --> CreatureBodyBuildSettings
    SpineMeshGenerator --> CreatureGenome
```

**Puste pudełka** to typy z innych obszarów, pokazane tylko dla kontekstu: `CreatureBodyBuildSettings`, `CreatureGenome`, `CreaturePartCatalog`, `CreaturePartDefinition`, `CreatureStatTuning`, `CreatureStats`, `GaitProfile`, `GenomeStatRules`, `LegSpec`, `LimbIk`, `PartCategory`, `PartGene`, `PartPlacement`, `PartPose`, `PartRuleSet`, `SpineAnchor`, `VertebraGene`.

<details><summary>Pliki źródłowe (7)</summary>

- `Assets/_Project/Features/CreatureEditor/Scripts/Build/BuiltCreatureBody.cs`
- `Assets/_Project/Features/CreatureEditor/Scripts/Build/CreatureBodyBuilder.cs`
- `Assets/_Project/Features/CreatureEditor/Scripts/Build/CreatureColliderFitter.cs`
- `Assets/_Project/Features/CreatureEditor/Scripts/Build/CreaturePartInstantiator.cs`
- `Assets/_Project/Features/CreatureEditor/Scripts/Build/CreatureRigBuilder.cs`
- `Assets/_Project/Features/CreatureEditor/Scripts/Build/ProceduralLeg.cs`
- `Assets/_Project/Features/CreatureEditor/Scripts/Build/SpineMeshGenerator.cs`

</details>
