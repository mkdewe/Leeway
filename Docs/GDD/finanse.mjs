/**
 * Koszt projektu Leeway - zespół trzyosobowy, bez rekrutacji.
 * Stawki podatkowo-składkowe: Polska 2026. Do dokumentu trafiają wyłącznie wyniki.
 */

import { MIESIACE } from "./szacunek.mjs";

const DUZY_ZUS = 1926.76;                                     // społeczne z chorobowym + FP/FS
const PREF_ZUS = 1441.80 * (0.1952 + 0.08 + 0.0245 + 0.0167); // ZUS preferencyjny
const ZDROW_MIN = 432.54, PROG = 120000, KZ = 3600;
const PLATNIK = 0.2048;                                        // narzut zleceniodawcy

/** Miesięczny dochód netto z działalności na skali, model roczny (uwzględnia próg 120 tys.). */
export function jdgNetto(faktura, spolMies) {
  const przychod = faktura * 12, spol = spolMies * 12, dochod = przychod - spol;
  const zdrow = Math.max(0.09 * dochod, ZDROW_MIN * 12);
  const pit = dochod <= PROG ? Math.max(0, dochod * 0.12 - KZ)
                             : PROG * 0.12 - KZ + (dochod - PROG) * 0.32;
  return (przychod - spol - zdrow - pit) / 12;
}
const szukaj = (f, cel) => { let lo = 0, hi = 60000; for (let i = 0; i < 100; i++) { const m = (lo + hi) / 2; f(m) < cel ? lo = m : hi = m; } return hi; };

/* ----------------------------- rdzeń zespołu ----------------------------- */

// Dariusz: cel 11 000 zł na rękę, istniejąca działalność (duży ZUS)
export const DARIUSZ_FAKTURA = 13000;   // stawka ustalona przez zespół

// Magda: ustalone 8 800 netto na fakturze; VAT odliczalny, więc koszt = kwota netto
export const MAGDA_FAKTURA = 8800;
export const MAGDA_VAT = 0.23;
export const MAGDA_NETTO = jdgNetto(MAGDA_FAKTURA, PREF_ZUS);

// Joanna: to samo na rękę co Magda, umowa zlecenie (poniżej 26 lat, ulga dla młodych)
const zlec = (b) => { const s = b * 0.1126; return { netto: b - s - (b - s) * 0.09, koszt: b * (1 + PLATNIK) }; };
export const JOANNA_BRUTTO = 8800;      // brutto umowy zlecenia, stawka ustalona przez zespół
// Studentka poniżej 26 lat: brak składek społecznych i zdrowotnej po obu stronach,
// więc kwota brutto, kwota na rękę i koszt projektu są sobie równe.
export const JOANNA_KOSZT = JOANNA_BRUTTO;

/** Spółka nie odlicza VAT, więc faktury B2B obciążają projekt kwotą brutto. */
export const VAT = 0.23;
const brutto = (n) => Math.round(n * (1 + VAT));

export const RDZEN = [
  { osoba: "Dariusz Dziel", rola: "Unity Developer", forma: "Działalność",
    stawka: DARIUSZ_FAKTURA, koszt: brutto(DARIUSZ_FAKTURA) },
  { osoba: "Magdalena Pławińska", rola: "Artystka 3D", forma: "Działalność",
    stawka: MAGDA_FAKTURA, koszt: brutto(MAGDA_FAKTURA) },
  { osoba: "Joanna Dziel", rola: "Game Designer", forma: "Zlecenie",
    stawka: JOANNA_BRUTTO, koszt: JOANNA_KOSZT },
];
export const RDZEN_MIES = RDZEN.reduce((a, o) => a + o.koszt, 0);

/* -------------------------------- budżet --------------------------------- */

export const NETTO_NA_EGZ = 35;   // po prowizji platformy i podatku, przy cenie 69 zł

export const MIESIACE_PROD = MIESIACE;

export const PLAN = (() => {
  const wiersze = RDZEN.map((o) => ({ ...o, razem: o.koszt * MIESIACE_PROD }));
  const razem = wiersze.reduce((a, b) => a + b.razem, 0);
  return { wiersze, miesiace: MIESIACE_PROD, razem,
           progZwrotu: Math.round(razem / NETTO_NA_EGZ / 500) * 500 };
})();
