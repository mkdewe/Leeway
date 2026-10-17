/**
 * Pracochłonność do premiery w Early Access. Zespół trzyosobowy, bez zleceń zewnętrznych.
 *
 * Kolumna "ai" to udział pracy, który realnie zdejmuje wsparcie modeli językowych
 * przy pisaniu kodu. Nie jest to jedna liczba dla całego projektu, bo efekt jest
 * skrajnie nierówny: przy kodzie powtarzalnym i dobrze opisanym w dokumentacji
 * (integracje, interfejs, zapis stanu) sięga połowy, natomiast przy dobieraniu
 * zachowania fizyki albo szukaniu rozjazdu w symulacji jest marginalny, bo pracę
 * wyznacza tam uruchamianie gry i ocena wzrokiem, a nie pisanie kodu.
 */

export const ETAPY = [
  {
    nazwa: "Fizyka, walka, lokomocja i interakcje z obiektami",
    zadania: [
      ["Fizyka, przewracanie się i poruszanie stworzeń", 3.5, 0.15],
      ["Interakcje z obiektami w świecie", 1.0, 0.30],
      ["Walka", 1.5, 0.30],
    ],
  },
  {
    nazwa: "Warstwa sieciowa",
    zadania: [
      ["Zgodność symulacji u wszystkich graczy", 2.0, 0.15],
      ["Lobby, wspólna baza, przechodzenie między etapami", 1.5, 0.40],
      ["Integracja ze Steam", 1.0, 0.45],
    ],
  },
  {
    nazwa: "Mapa, interfejs, przeciwnicy i części ciała",
    zadania: [
      ["Generator mapy", 1.5, 0.35],
      ["Interfejs gry", 1.0, 0.45],
      ["Przeciwnicy i ich zachowania", 1.5, 0.30],
      ["Boss", 1.0, 0.25],
      ["Działanie poszczególnych części ciała", 3.0, 0.30],
      ["Blueprinty, punkty ewolucji, progresja i zapis", 2.0, 0.40],
    ],
  },
  {
    nazwa: "Optymalizacja i wydanie",
    zadania: [
      ["Optymalizacja", 1.5, 0.20],
      ["Dźwięk", 0.5, 0.40],
      ["Testy, poprawki i przygotowanie wydania", 1.0, 0.20],
    ],
  },
];

export const GRAFIKA_OM = 14.5;
export const DESIGN_OM = 12.0;

/** Czas produktywny: reszta to prowadzenie projektu, spotkania i przestoje. */
export const WYDAJNOSC = 0.85;

export const etapOM = (e) => e.zadania.reduce((a, [, om, ai]) => a + om * (1 - ai), 0);
export const etapBaza = (e) => e.zadania.reduce((a, [, om]) => a + om, 0);

export const OM_BAZA = ETAPY.reduce((a, e) => a + etapBaza(e), 0);
export const OM_AI = ETAPY.reduce((a, e) => a + etapOM(e), 0);
export const MIESIACE = Math.round(OM_AI / WYDAJNOSC);

/** Podział miesięcy na etapy, proporcjonalnie do pracochłonności. */
export const ETAPY_MIES = (() => {
  let start = 1;
  return ETAPY.map((e, i) => {
    const dl = i === ETAPY.length - 1
      ? MIESIACE - start + 1
      : Math.max(1, Math.round(etapOM(e) / OM_AI * MIESIACE));
    const w = { nazwa: e.nazwa, od: start, do: start + dl - 1, zadania: e.zadania };
    start += dl;
    return w;
  });
})();
