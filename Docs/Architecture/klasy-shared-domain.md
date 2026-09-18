<!-- PLIK GENEROWANY — nie edytuj ręcznie. Źródło: Tools/DiagramGen/gen.mjs -->

# Shared/Domain

```mermaid
classDiagram
    direction LR
    class CellRules {
        <<static>>
        +CanEat(bool, bool, float, float, float) bool$
        +MoveSpeed(float, float, float, float, float) float$
        +GrowthAmount(float, float) float$
    }
    class CreaturePerception {
        <<domain>>
        +Vector3 SelfPosition
        +float FleeDistance
        +bool HasThreat
        +Vector3 ThreatPosition
        +bool HasTarget
        +Vector3 TargetPosition
        +Vector3 WanderTarget
    }
    class CreatureRules {
        <<static>>
        +CanAttack(bool, bool, float, float) bool$
        +ApplyDamage(float, float) float$
        +AddFood(float, float, float) float$
    }
    class FoodBehavior {
        <<domain>>
        +DecideTarget(NpcPerception) Vector2
    }
    class INpcBehavior {
        <<interface>>
    }
    class INpcCreatureBehavior {
        <<interface>>
    }
    class NpcBehavior {
        <<domain>>
        +DecideTarget(NpcPerception) Vector2
        #Flee(NpcPerception) Vector2$
    }
    class NpcBehaviorFactory {
        <<static>>
        +Create(NpcType) INpcBehavior$
    }
    class NpcCreatureBehavior {
        <<domain>>
        +DecideTarget(CreaturePerception) Vector3
        #Flee(CreaturePerception) Vector3$
    }
    class NpcCreatureBehaviorFactory {
        <<static>>
        +Create(NpcCreatureType) INpcCreatureBehavior$
    }
    class NpcCreatureType {
        <<enum>>
    }
    class NpcPerception {
        <<domain>>
        +Vector2 SelfPosition
        +float FleeDistance
        +bool HasThreat
        +Vector2 ThreatPosition
        +bool HasFood
        +Vector2 FoodPosition
        +Vector2 WanderTarget
    }
    class NpcType {
        <<enum>>
    }
    class PredatorBehavior {
        <<domain>>
        +DecideTarget(NpcPerception) Vector2
    }
    class PredatorCreatureBehavior {
        <<domain>>
        +DecideTarget(CreaturePerception) Vector3
    }
    class PreyBehavior {
        <<domain>>
        +DecideTarget(CreaturePerception) Vector3
    }

    NpcBehavior <|-- FoodBehavior
    FoodBehavior --> NpcPerception
    INpcBehavior ..> NpcPerception
    INpcCreatureBehavior ..> CreaturePerception
    NpcBehaviorFactory ..> FoodBehavior
    NpcBehaviorFactory --> INpcBehavior
    NpcBehaviorFactory --> NpcType
    NpcBehaviorFactory ..> PredatorBehavior
    INpcBehavior <|.. NpcBehavior
    NpcBehavior --> NpcPerception
    NpcCreatureBehavior --> CreaturePerception
    NpcCreatureBehaviorFactory --> INpcCreatureBehavior
    NpcCreatureBehaviorFactory --> NpcCreatureType
    NpcCreatureBehaviorFactory ..> PredatorCreatureBehavior
    NpcCreatureBehaviorFactory ..> PreyBehavior
    INpcCreatureBehavior <|.. NpcCreatureBehavior
    NpcBehavior <|-- PredatorBehavior
    PredatorBehavior --> NpcPerception
    PredatorCreatureBehavior --> CreaturePerception
    NpcCreatureBehavior <|-- PredatorCreatureBehavior
    PreyBehavior --> CreaturePerception
    NpcCreatureBehavior <|-- PreyBehavior
```

<details><summary>Pliki źródłowe (4)</summary>

- `Assets/_Project/Shared/Domain/CellRules.cs`
- `Assets/_Project/Shared/Domain/CreatureRules.cs`
- `Assets/_Project/Shared/Domain/NpcBehaviors.cs`
- `Assets/_Project/Shared/Domain/NpcCreatureBehaviors.cs`

</details>
