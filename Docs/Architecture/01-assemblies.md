<!-- PLIK GENEROWANY — nie edytuj ręcznie. Źródło: Tools/DiagramGen/gen.mjs -->

# Graf assembly (asmdef)

Najgrubsza granica w projekcie. Assembly to jedyny poziom, na którym Unity
faktycznie *wymusza* kierunek zależności — czego tu nie ma, tego kod nie
skompiluje. Dlatego ten diagram jest kontraktem, a nie opisem.

```mermaid
flowchart LR
    subgraph LEEWAY["Assets/_Project"]
        Assembly_CSharp["Assembly-CSharp"]
        Leeway_CreatureEditor["Leeway.CreatureEditor"]
        Leeway_Domain["Leeway.Domain"]
    end
    subgraph EXT["Zewnętrzne (granica grafu)"]
        FishNet_Runtime["FishNet.Runtime"]
        LitMotion["LitMotion"]
        MessagePipe["MessagePipe"]
        UniTask["UniTask"]
        Unity_Cinemachine["Unity.Cinemachine"]
        Unity_InputSystem["Unity.InputSystem"]
        Unity_TextMeshPro["Unity.TextMeshPro"]
        UnityEngine_UI["UnityEngine.UI"]
    end
    Assembly_CSharp --> Leeway_CreatureEditor
    Assembly_CSharp --> Leeway_Domain
    Leeway_CreatureEditor --> FishNet_Runtime
    Leeway_CreatureEditor --> Leeway_Domain
    Leeway_CreatureEditor --> LitMotion
    Leeway_CreatureEditor --> MessagePipe
    Leeway_CreatureEditor --> UniTask
    Leeway_CreatureEditor --> Unity_Cinemachine
    Leeway_CreatureEditor --> Unity_InputSystem
    Leeway_CreatureEditor --> Unity_TextMeshPro
    Leeway_CreatureEditor --> UnityEngine_UI
```

| Assembly | Typów |
|---|---|
| `Assembly-CSharp` | 28 |
| `Leeway.CreatureEditor` | 49 |
| `Leeway.Domain` | 49 |
