<!-- PLIK GENEROWANY — nie edytuj ręcznie. Źródło: Tools/DiagramGen/gen.mjs -->

# Architektura Leeway — diagramy

```bash
node Tools/DiagramGen/gen.mjs        # przegeneruj wszystko z kodu
node Tools/DiagramGen/validate.mjs   # sprawdź składnię parserem Mermaid
```

Walidator wymaga jednorazowego `cd Tools/DiagramGen && npm install`.
Ustawienia — poziom szczegółowości i listy pomijanych składowych:
`Tools/DiagramGen/diagram.config.json`.

1. [Przegląd zależności między obszarami](./00-overview.md)
2. [Graf assembly (asmdef)](./01-assemblies.md)
3. [Przepływ komunikatów (MessagePipe)](./02-messages.md)
4. Diagramy klas — spis w [przeglądzie](./00-overview.md#diagramy-klas)

## Jak czytać

| Zapis | Znaczenie |
|---|---|
| `<<NetworkBehaviour>>` | dziedziczy po typie silnika — hierarchia Unity/FishNet celowo nie jest rozwijana |
| `[SF]` | pole `[SerializeField]`, czyli wpięte w inspektorze lub prefabie |
| `[Sync]` | `SyncVar`/`SyncList` — stan replikowany przez FishNet |
| `[Server]`, `[ServerRpc]`, `[ObserversRpc]`, `[TargetRpc]` | granica autorytetu |
| `[Replicate]`, `[Reconcile]` | predykcja po stronie klienta (CSP) |
| `A --> B` | A trzyma referencję do B (pole/właściwość) |
| `A ..> B` | A używa B w implementacji |
| `A <\|-- B` | B dziedziczy po A |

Pokazujemy kontrakt, nie implementację. Ukryte celowo: `Awake`/`Start`/`Update`/`OnDestroy`, `OnStartServer`/`OnStartNetwork`
i pozostały cykl życia, składowe generowane przez codegen FishNet oraz prywatne
metody pomocnicze. Wyjątkiem są składowe prywatne z atrybutem Unity/FishNet
(`[SerializeField]`, `SyncVar`, `[Replicate]`, `[ServerRpc]`) — one *są* kontraktem,
tyle że wobec silnika i sieci, nie wobec innych klas C#.
