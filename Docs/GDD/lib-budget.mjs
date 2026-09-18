/* ============================ MODEL LICZBOWY ============================ */

// Dwie formy zatrudnienia:
//  - b2b      — faktura, brak narzutu po stronie zleceniodawcy; koszt = stawka
//  - zlecenie — umowa zlecenie z pełnym ZUS; koszt = brutto × 1,2048
//    (emerytalna 9,76% + rentowa 6,50% + wypadkowa 1,67% + FP 2,45% + FGŚP 0,10%)
export const ZUS = 1.2048;
export const cost = (r) =>
  r.forma === "b2b" ? r.stawka : Math.round(r.stawka * ZUS / 100) * 100;

export const R = {
  founder:  { name: "Mid Unity Developer — gameplay, sieć, kreator (założyciel)", stawka: 16000, forma: "b2b" },
  jrDesign: { name: "Junior Game Designer (+ sound design)", stawka: 6500, forma: "zlecenie" },
  jrArtist: { name: "Junior Artysta 3D", stawka: 6500, forma: "zlecenie" },
  techprog: { name: "Senior Programista — animacja proceduralna, fizyka, netcode", stawka: 19000, forma: "zlecenie" },
  techart:  { name: "Mid Tech Artist — rig, shadery, pipeline", stawka: 13000, forma: "zlecenie" },
  gameplay: { name: "Mid Programista gameplay — AI, systemy przetrwania", stawka: 13000, forma: "zlecenie" },
  content:  { name: "Content / Level Designer — generacja map, balans", stawka: 10000, forma: "zlecenie" },
  qa:       { name: "QA Specialist", stawka: 7000, forma: "zlecenie" },
  cm:       { name: "Community Manager (0,5 etatu)", stawka: 3500, forma: "zlecenie" },
};

// Warianty alternatywne — pełne finansowanie z góry, szerszy zakres treści.
export const PLAN_A = {
  months: 28, ea: 16,
  staff: [
    { r: R.founder,  from: 1,  to: 28 },
    { r: R.jrDesign, from: 1,  to: 28 },
    { r: R.techart,  from: 1,  to: 28 },
    { r: R.techprog, from: 3,  to: 28 },
    { r: R.jrArtist, from: 6,  to: 28 },
    { r: R.gameplay, from: 8,  to: 28 },
    { r: R.content,  from: 11, to: 28 },
    { r: R.qa,       from: 12, to: 28 },
    { r: R.cm,       from: 10, to: 28 },
  ],
  other: [
    ["Sprzęt (9 stanowisk)", 117000, "13 000 PLN / stanowisko"],
    ["Licencje Unity Pro", 130000, "Wymagane po przekroczeniu progu Unity Personal; ok. 9 000 PLN / stanowisko / rok"],
    ["Muzyka (kompozytor zewnętrzny)", 55000, "Sound design realizowany wewnętrznie"],
    ["Lokalizacja", 60000, "10 języków"],
    ["Playtesty zewnętrzne", 40000, "4 rundy"],
    ["Backend UGC i API modów", 50000, "Steam Workshop, walidacja, dokumentacja, moderacja"],
  ],
  reservePct: 0.12,
  marketing: [300000, 500000],
};

export const PLAN_B = {
  months: 20, ea: 11,
  staff: [
    { r: R.founder,  from: 1,  to: 20 },
    { r: R.jrDesign, from: 1,  to: 20 },
    { r: R.techart,  from: 1,  to: 20 },
    { r: R.techprog, from: 2,  to: 20 },
    { r: R.jrArtist, from: 5,  to: 20 },
    { r: R.qa,       from: 9,  to: 20 },
    { r: R.cm,       from: 8,  to: 20 },
  ],
  other: [
    ["Sprzęt (7 stanowisk)", 91000, "13 000 PLN / stanowisko"],
    ["Licencje Unity Pro", 80000, "Wymagane po przekroczeniu progu Unity Personal"],
    ["Muzyka (kompozytor zewnętrzny)", 35000, "Krótsza ścieżka"],
    ["Lokalizacja", 25000, "4 języki"],
    ["Playtesty zewnętrzne", 20000, "2 rundy"],
    ["Backend UGC", 25000, "Steam Workshop bez API modów"],
  ],
  reservePct: 0.10,
  marketing: [150000, 250000],
};

function calc(plan) {
  const rows = plan.staff.map(s => {
    const m = s.to - s.from + 1;
    const c = cost(s.r);
    return { name: s.r.name, stawka: s.r.stawka, forma: s.r.forma, monthly: c, from: s.from, to: s.to, months: m, total: c * m };
  });
  const personnel = rows.reduce((a, b) => a + b.total, 0);
  const otherSum = plan.other.reduce((a, b) => a + b[1], 0);
  const base = personnel + otherSum;
  const reserve = Math.round(base * plan.reservePct / 1000) * 1000;
  const total = base + reserve;
  const fte = rows.reduce((a, b) => a + b.months * (b.name.includes("0,5") ? 0.5 : 1), 0);
  return { rows, personnel, otherSum, base, reserve, total, fte };
}

export const A = calc(PLAN_A);
export const B = calc(PLAN_B);

// Przychody: cena EA 69 PLN, 1.0 89 PLN, efektywna cena po promocjach ~58 PLN brutto.
// Po VAT (śr. 19%) i prowizji Steam (30%) do studia trafia ≈ 35 PLN / egz.
export const NET_PER_UNIT = 35;
export const SCEN = [
  ["Pesymistyczny", 25000, "Słaby odbiór EA, brak zasięgów u twórców, sprzedaż głównie w promocjach"],
  ["Ostrożny", 75000, "Poprawny odbiór, stabilna niszowa społeczność, brak efektu wirusowego"],
  ["Bazowy", 200000, "Gra łapie zasięgi u średnich twórców, zdrowy Workshop, 2–3 fale sprzedaży"],
  ["Optymistyczny", 600000, "Wyraźny efekt wirusowy klipów ze stworkami, wejście do topek Steam"],
  ["Hit gatunku", 2000000, "Skala Lethal Company / Content Warning — możliwa, nieplanowalna"],
];

export const num = (n) =>
  String(Math.round(n)).replace(/\B(?=(\d{3})+(?!\d))/g, "\u00A0");

export const fmt = (n) =>
  String(Math.round(n)).replace(/\B(?=(\d{3})+(?!\d))/g, "\u00A0") + "\u00A0PLN";
export const fmtM = (n) => (n / 1000000).toFixed(2).replace(".", ",") + " mln PLN";
export const breakEven = (budget) => Math.round(budget / NET_PER_UNIT / 1000) * 1000;
