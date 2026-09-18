<!-- PLIK GENEROWANY — nie edytuj ręcznie. Źródło: Tools/DiagramGen/gen.mjs -->

# Features/CreatureEditor/Data

```mermaid
classDiagram
    direction LR
    class CreatureBodyBuildSettings {
        <<ScriptableObject>>
        +int RingsPerSegment
        +int RadialSegments
        +int CapRings
        +Material BodyMaterial
        +float BoundsPadding
        +float MinColliderRadius
    }
    class CreaturePartCatalog {
        <<ScriptableObject>>
        [SF] CreaturePartDefinition[] _parts
        [SF] int _vertebraCost
        [SF] int _budget
        +IReadOnlyList~CreaturePartDefinition~ Parts
        +TryGetPart(int, CreaturePartDefinition) bool
        +GetByCategory(PartCategory) List~CreaturePartDefinition~
        +BuildRuleSet() PartRuleSet
    }
    class CreaturePartDefinition {
        <<ScriptableObject>>
        [SF] int _cachedPartId
        +string PartKey
        +string DisplayName
        +PartCategory Category
        +AttachmentSite AllowedSites
        +bool MirrorCapable
        +GameObject Prefab
        +float SkinOffset
        +PartStatContribution Stats
        +int BendPoints
        +float SegmentLength
        +int Cost
        +int PartId
        +LegSpec Leg
        +… i 1 dalszych
    }
    class AttachmentSite {
        <<enum>>
    }
    class LegLimits {
        <<static>>
    }
    class LegSpec {
        <<domain>>
    }
    class PartCategory {
        <<enum>>
    }
    class PartRule {
        <<domain>>
    }
    class PartRuleSet {
        <<domain>>
    }
    class PartStatContribution {
        <<domain>>
    }
    class StableHash {
        <<static>>
    }

    CreaturePartCatalog --> CreaturePartDefinition : inspektor
    CreaturePartCatalog --> PartCategory
    CreaturePartCatalog ..> PartRule
    CreaturePartCatalog --> PartRuleSet
    CreaturePartDefinition --> AttachmentSite
    CreaturePartDefinition ..> LegLimits
    CreaturePartDefinition --> LegSpec
    CreaturePartDefinition --> PartCategory
    CreaturePartDefinition --> PartRule
    CreaturePartDefinition --> PartStatContribution
    CreaturePartDefinition ..> StableHash
```

**Puste pudełka** to typy z innych obszarów, pokazane tylko dla kontekstu: `AttachmentSite`, `LegLimits`, `LegSpec`, `PartCategory`, `PartRule`, `PartRuleSet`, `PartStatContribution`, `StableHash`.

<details><summary>Pliki źródłowe (3)</summary>

- `Assets/_Project/Features/CreatureEditor/Scripts/Data/CreatureBodyBuildSettings.cs`
- `Assets/_Project/Features/CreatureEditor/Scripts/Data/CreaturePartCatalog.cs`
- `Assets/_Project/Features/CreatureEditor/Scripts/Data/CreaturePartDefinition.cs`

</details>
