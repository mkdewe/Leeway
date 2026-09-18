# Dokumenty dla wydawcy / inwestora

Oba dokumenty są **generowane ze skryptów**, żeby liczby (budżet, osobomiesiące, próg
rentowności) pozostawały spójne po każdej zmianie założeń.

| Plik | Adresat | Generator |
|---|---|---|
| `Leeway_FreeMind_Propozycja.docx` | **FreeMind S.A.** — model transzowy, dopasowany do ich profilu | `gen-freemind.mjs` |
| `Leeway_GDD_Inwestor.docx` | Wydawca finansujący pełną produkcję — pełny GDD + Plan A/B | `gen-gdd.mjs` |

## Regeneracja

```bash
npm install docx        # jednorazowo
node gen-freemind.mjs
node gen-gdd.mjs
```

> Zamknij dokument w Wordzie przed regeneracją — otwarty plik blokuje zapis (`EACCES`).

## Struktura

| Plik | Zawartość |
|---|---|
| `lib-budget.mjs` | Stawki, narzut ZUS, Plan A / Plan B, scenariusze sprzedaży, formatowanie kwot |
| `lib-docx.mjs` | Pomocniki składu: nagłówki, akapity, listy, tabele, style |
| `gen-freemind.mjs` | Model transzowy (T1/T2/T3 + bramki G1–G3) i treść propozycji dla FreeMind |
| `gen-gdd.mjs` | Treść pełnego GDD i biznesplanu |

## Gdzie zmieniać założenia

**Wspólne** (`lib-budget.mjs`): stawki brutto i role — `R`; narzut ZUS — `ZUS`; obsada
i czas trwania Planu A/B — `PLAN_A.staff` / `PLAN_B.staff`; koszty pozarodzajowe —
`.other`; rezerwa i marketing — `reservePct` / `marketing`; przychód na egzemplarz —
`NET_PER_UNIT`; scenariusze sprzedaży — `SCEN`.

**Model transzowy** (`gen-freemind.mjs`, stała `T` na górze pliku): granice miesięcy
`od`/`do`, obsada `staff`, koszty `other`, rezerwa `reserve`. Miesiąc premiery Early
Access, ekspozycja kapitałowa i progi rentowności przeliczają się automatycznie.

Progi bramek decyzyjnych (m.in. 8 000 wishlist przy G1) są w treści rozdziału 6
i w Załączniku B — do uzgodnienia z wydawcą.
