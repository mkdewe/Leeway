<!-- PLIK GENEROWANY — nie edytuj ręcznie. Źródło: Tools/DiagramGen/gen.mjs -->

# Shared/Data

```mermaid
classDiagram
    direction LR
    class CellEntityConfig {
        <<ScriptableObject>>
        +float BaseSpeed
        +float MaxSpeed
        +float BaseHp
        +float BaseSize
        +float MaxSize
        +float Drag
        +float EatSizeRatio
        +float GrowthPerEat
        +float SpeedFloorRatio
        +float FleeDistance
    }
    class CreatureEntityConfig {
        <<ScriptableObject>>
        +float BaseSpeed
        +float Drag
        +float RotationSpeed
        +float BaseHp
        +float AttackDamage
        +float AttackRange
        +float AttackCooldown
        +float MaxFood
        +float FleeDistance
    }

```

<details><summary>Pliki źródłowe (2)</summary>

- `Assets/_Project/Shared/Data/CellEntityConfig.cs`
- `Assets/_Project/Shared/Data/CreatureEntityConfig.cs`

</details>
