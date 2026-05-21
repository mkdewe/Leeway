# Leeway — Claude Code Guidelines

## Projekt

**Leeway** to ambitna gra multiplayer inspirowana Spore — wieloetapowa (wiele trybów kamery i gameplay'u na różnych skalach), obsługująca tryby single, co-op i multiplayer. Projekt dopiero startuje (Unity 6, URP, jeden commit).

## Stack technologiczny

| Technologia | Wersja | Rola |
|---|---|---|
| Unity | 6000.4.7f1 | Silnik |
| URP | 17.4.0 | Render pipeline (osobne profile PC/Mobile) |
| FishNet | 4.7.2 (git) | Sieć / multiplayer |
| VContainer | 1.18.0 (git) | Dependency Injection |
| MessagePipe | (git) | Pub/sub messaging między systemami |
| UniTask | (git) | Async/await |
| LitMotion | (git) | Tweening / animacje wartości (R3 native) |
| Cinemachine | 3.1.6 | System kamer |
| Input System | 1.19.0 | Wejście gracza (New Input System) |
| ProBuilder | 6.0.9 | Modelowanie poziomów |
| VFX Graph | 17.4.0 | Efekty wizualne |
| AI Navigation | 2.0.12 | Pathfinding |
| Timeline | 1.8.12 | Sekwencje/animacje |

## Architektura sieciowa (FishNet)

- Model autorytetu: **Server Authoritative + Client-Side Prediction (CSP)**
- Klient aplikuje akcje natychmiast lokalnie (predykcja), serwer weryfikuje i koryguje (reconciliation)
- Kluczowe elementy FishNet CSP:
  - `PredictedObject` — dla obiektów z predykcją ruchu
  - `Replicate` + `Reconcile` — pattern w `NetworkBehaviour` do implementacji CSP
  - `ColliderRollback` — lag compensation dla hitscanów
- Baza: `NetworkBehaviour` zamiast `MonoBehaviour` dla obiektów sieciowych
- Używaj `ServerRpc` / `ObserversRpc` / `TargetRpc` zgodnie z odpowiedzialnością
- Serwer jest jedynym źródłem prawdy o stanie gry (zasoby, HP, ewolucja, interakcje)

## Struktura folderów (feature-based)

```
Assets/
  _Project/               ← własny kod gry (nie naruszaj FishNet/)
    Features/
      Player/
      Creature/
      Network/
      UI/
      ...
    Core/                 ← systemy wspólne (DI setup, bootstrapping)
    Shared/               ← typy, extensions, utilities
  FishNet/                ← nie edytuj
  Plugins/                ← nie edytuj
```

Namespace: `Leeway.<Feature>` (np. `Leeway.Player`, `Leeway.Network`).

## Zasady pracy z kodem

- Zawsze dziedzicz po `NetworkBehaviour` (nie `MonoBehaviour`) dla obiektów uczestniczących w sieci
- VContainer — rejestruj zależności przez `LifetimeScope`, nie używaj singletonów
- MessagePipe — do komunikacji między niezależnymi systemami (zamiast eventów Unity / statycznych delegatów)
- UniTask zamiast coroutine do async operations
- Nie twórz publicznych pól — używaj `[SerializeField] private`
- Sprawdzaj `read_console` po każdej modyfikacji skryptu przed kolejnym krokiem

## Git

- **Nigdy nie dodawaj `Co-Authored-By: Claude`** ani żadnego oznaczenia Claude'a w commitach
- Commit messages po polsku lub angielsku — konsekwentnie w jednym języku
- Nie commituj bez wyraźnego polecenia użytkownika

## Unity MCP

- Aktywna instancja: `Leeway@a0d50d8c686d99f3`
- Po każdej zmianie skryptu → `read_console` → sprawdź błędy kompilacji
- Sprawdzaj `editor_state.isCompiling` przed użyciem nowych typów
- Zawsze twórz prefaby dla reużywalnych obiektów

## Platformy docelowe

- **PC** (priorytet) + **konsole** (PS5, Xbox — do ustalenia w przyszłości)
- `PC_RPAsset.asset` — aktywna konfiguracja URP
- `Mobile_RPAsset.asset` — pozostałość po szablonie projektu, nieużywana, nie konfiguruj
