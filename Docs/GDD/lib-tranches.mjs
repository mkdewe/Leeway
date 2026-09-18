import { R, cost } from "./lib-budget.mjs";

export const T = [
  {
    id: "Transza 1", nazwa: "Walidacja rynkowa", od: 1, do: 4, reserve: 0.10,
    cel: "Grywalne demo kreatora i pętli rozgrywki, strona na Steam, pierwszy zwiastun, start zbierania listy życzeń.",
    staff: [
      { r: R.founder,  from: 1, to: 4 },
      { r: R.jrDesign, from: 1, to: 4 },
      { r: R.jrArtist, from: 1, to: 4 },
    ],
    other: [
      ["Narzędzia i usługi", 6000, "Kontrola wersji, hosting strony, pojedyncze zasoby ze sklepu. Unity Personal pozostaje bezpłatny — finansowanie transzy nie zbliża się do progu 200 000 USD"],
    ],
  },
  {
    id: "Transza 2", nazwa: "Produkcja do Early Access", od: 5, do: 12, reserve: 0.12,
    cel: "Doprowadzenie gry do premiery w Early Access: kreator w pełnej wersji, walka, dwa biomy, Steam Workshop, optymalizacja.",
    staff: [
      { r: R.founder,  from: 5,  to: 12 },
      { r: R.jrDesign, from: 5,  to: 12 },
      { r: R.jrArtist, from: 5,  to: 12 },
      { r: R.techprog, from: 5,  to: 12 },
      { r: R.techart,  from: 6,  to: 12 },
      { r: R.gameplay, from: 7,  to: 12 },
      { r: R.qa,       from: 10, to: 12 },
      { r: R.cm,       from: 6,  to: 12 },
    ],
    other: [
      ["Sprzęt (4 nowe stanowiska)", 52000, "13 000 PLN / stanowisko — komputer, monitory, peryferia"],
      ["Licencje Unity Pro", 36000, "Od tej transzy wymagane: łączne finansowanie przekracza próg 200 000 USD dla Unity Personal. 2 310 USD / stanowisko / rok, 6 stanowisk, 8 miesięcy"],
      ["Muzyka (kompozytor zewnętrzny)", 30000, "Ścieżka adaptacyjna na premierę Early Access"],
      ["Lokalizacja", 20000, "4 języki na start: EN, PL, DE, ZH-Hans"],
      ["Playtesty zewnętrzne", 15000, "Dwie rundy przed premierą Early Access"],
    ],
  },
  {
    id: "Transza 3", nazwa: "Early Access do wersji 1.0", od: 13, do: 18, reserve: 0.12,
    cel: "Sześć miesięcy aktualizacji, trzeci biom, starcie z bossem, rozbudowa katalogu części i domknięcie wersji 1.0.",
    staff: [
      { r: R.founder,  from: 13, to: 18 },
      { r: R.jrDesign, from: 13, to: 18 },
      { r: R.jrArtist, from: 13, to: 18 },
      { r: R.techprog, from: 13, to: 18 },
      { r: R.techart,  from: 13, to: 18 },
      { r: R.gameplay, from: 13, to: 18 },
      { r: R.qa,       from: 13, to: 18 },
      { r: R.cm,       from: 13, to: 18 },
    ],
    other: [
      ["Licencje Unity Pro", 27000, "6 stanowisk, 6 miesięcy"],
      ["Muzyka — uzupełnienie", 15000, "Ścieżki do trzeciego biomu i starcia z bossem"],
      ["Lokalizacja — rozszerzenie", 25000, "Dojście do 6 języków na premierę 1.0"],
      ["Backend treści od graczy", 35000, "Steam Workshop, walidacja genomów, narzędzia moderacji"],
      ["Playtesty zewnętrzne", 10000, "Runda przed premierą 1.0"],
    ],
  },
];

export function calcT(t) {
  const rows = t.staff.map(s => {
    const m = s.to - s.from + 1, c = cost(s.r);
    return { name: s.r.name, forma: s.r.forma, monthly: c, from: s.from, to: s.to, months: m, total: c * m };
  });
  const personnel = rows.reduce((a, b) => a + b.total, 0);
  const otherSum = t.other.reduce((a, b) => a + b[1], 0);
  const base = personnel + otherSum;
  const reserve = Math.round(base * t.reserve / 1000) * 1000;
  return { ...t, rows, personnel, otherSum, base, reserve, total: base + reserve,
           len: t.do - t.od + 1, peak: t.staff.length };
}

export const TR = T.map(calcT);
export const TOTAL = TR.reduce((a, b) => a + b.total, 0);
export const EXPOSURE = TR[0].total + TR[1].total;   // realna ekspozycja przed pierwszym przychodem
export const EA_MONTH = TR[1].do;                     // premiera Early Access (koniec transzy 2)
export const V10_MONTH = TR[2].do;                    // premiera 1.0
