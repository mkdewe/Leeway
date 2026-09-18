<!-- PLIK GENEROWANY — nie edytuj ręcznie. Źródło: Tools/DiagramGen/gen.mjs -->

# Przegląd — zależności między obszarami

Węzeł to katalog, liczba na strzałce to ile par typów tworzy tę zależność.
Strzałka w stronę `Shared/Domain` jest zdrowa; strzałka **z** `Shared/Domain`
w stronę `Features` oznaczałaby, że domena zaczęła zależeć od Unity i sieci.

```mermaid
flowchart TD
    g_Core_Bootstrap["Core/Bootstrap<br/>4 typów"]
    g_Features_Camera["Features/Camera<br/>2 typów"]
    g_Features_Creature_Scripts["Features/Creature<br/>9 typów"]
    g_Features_CreatureEditor_Scripts_Build["Features/CreatureEditor/Build<br/>8 typów"]
    g_Features_CreatureEditor_Scripts_Data["Features/CreatureEditor/Data<br/>3 typów"]
    g_Features_CreatureEditor_Scripts_Editing["Features/CreatureEditor/Editing<br/>20 typów"]
    g_Features_CreatureEditor_Scripts_Messages["Features/CreatureEditor/Messages<br/>6 typów"]
    g_Features_CreatureEditor_Scripts_Runtime["Features/CreatureEditor/Runtime<br/>12 typów"]
    g_Features_GamePhases["Features/GamePhases<br/>3 typów"]
    g_Features_Network["Features/Network<br/>1 typów"]
    g_Features_Player_Scripts["Features/Player<br/>6 typów"]
    g_Features_UI["Features/UI<br/>1 typów"]
    g_Shared_Data["Shared/Data<br/>2 typów"]
    g_Shared_Domain["Shared/Domain<br/>16 typów"]
    g_Shared_Domain_CreatureEditor["Shared/Domain/CreatureEditor<br/>33 typów"]
    g_Core_Bootstrap -->|2| g_Features_Creature_Scripts
    g_Core_Bootstrap -->|6| g_Features_CreatureEditor_Scripts_Messages
    g_Features_Camera -->|3| g_Features_Creature_Scripts
    g_Features_Creature_Scripts -->|1| g_Features_Player_Scripts
    g_Features_Creature_Scripts -->|2| g_Shared_Data
    g_Features_Creature_Scripts -->|11| g_Shared_Domain
    g_Features_CreatureEditor_Scripts_Build -->|6| g_Features_CreatureEditor_Scripts_Data
    g_Features_CreatureEditor_Scripts_Build -->|19| g_Shared_Domain_CreatureEditor
    g_Features_CreatureEditor_Scripts_Data -->|10| g_Shared_Domain_CreatureEditor
    g_Features_CreatureEditor_Scripts_Editing -->|7| g_Features_CreatureEditor_Scripts_Build
    g_Features_CreatureEditor_Scripts_Editing -->|8| g_Features_CreatureEditor_Scripts_Data
    g_Features_CreatureEditor_Scripts_Editing -->|8| g_Features_CreatureEditor_Scripts_Messages
    g_Features_CreatureEditor_Scripts_Editing -->|1| g_Features_CreatureEditor_Scripts_Runtime
    g_Features_CreatureEditor_Scripts_Editing -->|37| g_Shared_Domain_CreatureEditor
    g_Features_CreatureEditor_Scripts_Messages -->|1| g_Features_CreatureEditor_Scripts_Editing
    g_Features_CreatureEditor_Scripts_Messages -->|2| g_Features_CreatureEditor_Scripts_Runtime
    g_Features_CreatureEditor_Scripts_Messages -->|2| g_Shared_Domain_CreatureEditor
    g_Features_CreatureEditor_Scripts_Runtime -->|7| g_Features_CreatureEditor_Scripts_Build
    g_Features_CreatureEditor_Scripts_Runtime -->|2| g_Features_CreatureEditor_Scripts_Data
    g_Features_CreatureEditor_Scripts_Runtime -->|1| g_Features_CreatureEditor_Scripts_Editing
    g_Features_CreatureEditor_Scripts_Runtime -->|2| g_Features_CreatureEditor_Scripts_Messages
    g_Features_CreatureEditor_Scripts_Runtime -->|24| g_Shared_Domain_CreatureEditor
    g_Features_GamePhases -->|2| g_Features_Creature_Scripts
    g_Features_GamePhases -->|1| g_Features_CreatureEditor_Scripts_Data
    g_Features_GamePhases -->|1| g_Features_CreatureEditor_Scripts_Runtime
    g_Features_GamePhases -->|4| g_Shared_Domain_CreatureEditor
    g_Features_Player_Scripts -->|4| g_Features_Creature_Scripts
    g_Features_UI -->|2| g_Features_Creature_Scripts
```

## Diagramy klas

- [Core/Bootstrap](./klasy-core-bootstrap.md) — 4 typów
- [Features/Camera](./klasy-features-camera.md) — 2 typów
- [Features/Creature](./klasy-features-creature-scripts.md) — 9 typów
- [Features/CreatureEditor/Build](./klasy-features-creatureeditor-scripts-build.md) — 8 typów
- [Features/CreatureEditor/Data](./klasy-features-creatureeditor-scripts-data.md) — 3 typów
- [Features/CreatureEditor/Editing](./klasy-features-creatureeditor-scripts-editing.md) — 20 typów
- [Features/CreatureEditor/Messages](./klasy-features-creatureeditor-scripts-messages.md) — 6 typów
- [Features/CreatureEditor/Runtime](./klasy-features-creatureeditor-scripts-runtime.md) — 12 typów
- [Features/GamePhases](./klasy-features-gamephases.md) — 3 typów
- [Features/Network](./klasy-features-network.md) — 1 typów
- [Features/Player](./klasy-features-player-scripts.md) — 6 typów
- [Features/UI](./klasy-features-ui.md) — 1 typów
- [Shared/Data](./klasy-shared-data.md) — 2 typów
- [Shared/Domain](./klasy-shared-domain.md) — 16 typów
- [Shared/Domain/CreatureEditor](./klasy-shared-domain-creatureeditor.md) — 33 typów
