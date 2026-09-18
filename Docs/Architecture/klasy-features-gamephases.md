<!-- PLIK GENEROWANY — nie edytuj ręcznie. Źródło: Tools/DiagramGen/gen.mjs -->

# Features/GamePhases

```mermaid
classDiagram
    direction LR
    class CellPhaseGameManager {
        <<NetworkBehaviour>>
        [SF] GameObject _playerCellPrefab
        [SF] GameObject _foodCellPrefab
        [SF] GameObject _predatorCellPrefab
        [SF] int _foodCount
        [SF] int _predatorCount
        [SF] float _worldRadius
        [SF] float _foodRespawnDelay
        [Server] SpawnPlayerCell(NetworkConnection) void
        [Server] HandlePlayerDisconnected(NetworkConnection) void
        [Server] SpawnNpcCells() void
        [Server] SpawnFood() void
        [Server] SpawnPredator() void
    }
    class CreatureEditorGameManager {
        <<NetworkBehaviour>>
        [SF] GameObject _playgroundCreaturePrefab
        [SF] CreaturePartCatalog _catalog
        [SF] int _worldSeed
        [SF] float _spawnRadius
        [SF] float _spawnHeight
        [Server] SpawnPlayerBody(NetworkConnection) void
        [Server] HandlePlayerDisconnected(NetworkConnection) void
    }
    class CreaturePhaseGameManager {
        <<NetworkBehaviour>>
        [SF] GameObject _playerCreaturePrefab
        [SF] GameObject _preyCreaturePrefab
        [SF] GameObject _predatorCreaturePrefab
        [SF] GameObject _foodItemPrefab
        [SF] int _foodCount
        [SF] int _preyCount
        [SF] int _predatorCount
        [SF] float _worldRadius
        [SF] float _foodRespawnDelay
        [Server] SpawnPlayerCreature(NetworkConnection) void
        [Server] HandlePlayerDisconnected(NetworkConnection) void
        [Server] SpawnEnvironment() void
        [Server] SpawnFood() void
        [Server] SpawnPrey() void
        +… i 1 dalszych
    }
    class CellEntity {
        <<NetworkBehaviour>>
    }
    class CreatureBody {
        <<NetworkBehaviour>>
    }
    class CreatureGenome {
        <<domain>>
    }
    class CreaturePartCatalog {
        <<ScriptableObject>>
    }
    class FoodItem {
        <<NetworkBehaviour>>
    }
    class GenomeError {
        <<enum>>
    }
    class PartRuleSet {
        <<domain>>
    }
    class StarterGenomeFactory {
        <<static>>
    }

    CellPhaseGameManager ..> CellEntity
    CreatureEditorGameManager ..> CreatureBody
    CreatureEditorGameManager ..> CreatureGenome
    CreatureEditorGameManager --> CreaturePartCatalog : inspektor
    CreatureEditorGameManager ..> GenomeError
    CreatureEditorGameManager ..> PartRuleSet
    CreatureEditorGameManager ..> StarterGenomeFactory
    CreaturePhaseGameManager ..> FoodItem
```

**Puste pudełka** to typy z innych obszarów, pokazane tylko dla kontekstu: `CellEntity`, `CreatureBody`, `CreatureGenome`, `CreaturePartCatalog`, `FoodItem`, `GenomeError`, `PartRuleSet`, `StarterGenomeFactory`.

<details><summary>Pliki źródłowe (3)</summary>

- `Assets/_Project/Features/GamePhases/CellPhase/CellPhaseGameManager.cs`
- `Assets/_Project/Features/GamePhases/CreatureEditorPhase/CreatureEditorGameManager.cs`
- `Assets/_Project/Features/GamePhases/CreaturePhase/CreaturePhaseGameManager.cs`

</details>
