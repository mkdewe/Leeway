# Cell Stage — checklista testów manualnych

Procedury, których nie da się sensownie pokryć testem jednostkowym (feel, sieć,
złożenie sceny). Wykonaj przed scaleniem zmian dotykających fazy komórki.

## Setup
1. Otwórz scenę `Assets/Scenes/CellStage.unity`.
2. Wejdź w **Play Mode**.
3. W lewym górnym rogu kliknij **Host (Server + Client)**.

## Smoke test (host lokalny)
- [ ] Brak błędów/ostrzeżeń w konsoli po starcie hosta.
- [ ] Spawnuje się komórka gracza (zielona) oraz NPC (żółte jedzenie, czerwone drapieżniki).
- [ ] Sterowanie WSAD / strzałki rusza graczem.
- [ ] Kamera podąża za graczem i oddala się wraz ze wzrostem.
- [ ] HUD pokazuje rozmiar, pasek HP i ewolucji; tekst statusu "Zyjesz!".

## Rozgrywka
- [ ] Wejście w żółtą komórkę → gracz rośnie, jedzenie znika.
- [ ] Po chwili zjedzone jedzenie respawnuje się (domyślnie ~8 s).
- [ ] Drapieżniki aktywnie zbliżają się do mniejszych komórek (polują).
- [ ] Kontakt z większym drapieżnikiem → gracz ginie, status "Umarles...".
- [ ] Po urośnięciu gracz potrafi zjeść drapieżnika (rozmiar > ~1.73).

## Sieć (dwie instancje)
> Wymaga buildu lub Multiplayer Play Mode (ParrelSync / Unity MPPM).
- [ ] Drugi klient dołącza przez **Start Client**.
- [ ] Ruch i wzrost obu graczy są spójne na obu instancjach.
- [ ] Brak widocznego "gumowania" przy ruchu (predykcja/rekonsyliacja działa).
- [ ] Rozłączenie klienta despawnuje jego komórkę u pozostałych.

## Po teście
- [ ] Wyjdź z Play Mode (zrzuty ekranu z `Assets/Screenshots/` są ignorowane przez git).
