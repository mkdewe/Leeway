/**
 * Aktualizuje wyłącznie 5.4 (liczby) i dopisuje 5.5 Harmonogram prac.
 * Nie dotyka 5.1, 5.2 ani 5.3 - treść tych sekcji jest po stronie zespołu.
 */
import JSZip from "./node_modules/jszip/lib/index.js";
import { readFileSync, writeFileSync } from "fs";
import { PLAN, RDZEN_MIES, NETTO_NA_EGZ } from "./finanse.mjs";
import { OM_BAZA, OM_AI, WYDAJNOSC, MIESIACE, ETAPY_MIES } from "./szacunek.mjs";

const IN = process.env.IN || "Leeway_GDD_v3.docx";
const OUT = process.env.OUT || "Leeway_GDD_v4.docx";

const esc = (s) => String(s).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
const kw = (n) => Math.round(n).toString().replace(/\B(?=(\d{3})+(?!\d))/g, " ") + " PLN";
const lb = (n) => Math.round(n).toString().replace(/\B(?=(\d{3})+(?!\d))/g, " ");
const mies = (n) => { const d = n % 10, r = n % 100;
  return n + (d >= 2 && d <= 4 && !(r >= 12 && r <= 14) ? " miesiące" : " miesięcy"); };

const run = (t, { b = false } = {}) =>
  `<w:r><w:rPr>${b ? '<w:b w:val="1"/><w:bCs w:val="1"/>' : ""}<w:rtl w:val="0"/></w:rPr>` +
  `<w:t xml:space="preserve">${esc(t)}</w:t></w:r>`;
const runs = (p) => p.map((x) => (typeof x === "string" ? run(x) : run(x[0], x[1]))).join("");
const P = (...p) => `<w:p><w:pPr><w:ind w:firstLine="720"/><w:jc w:val="both"/><w:rPr/></w:pPr>${runs(p)}</w:p>`;
const LI = (...p) => `<w:p><w:pPr><w:numPr><w:ilvl w:val="0"/><w:numId w:val="1"/></w:numPr>` +
  `<w:ind w:left="720" w:hanging="360"/><w:jc w:val="both"/><w:rPr><w:u w:val="none"/></w:rPr></w:pPr>${runs(p)}</w:p>`;
const EMPTY = `<w:p><w:pPr><w:rPr/></w:pPr></w:p>`;
let bmId = 600;
const HEAD = (lvl, t) => { const id = ++bmId;
  return `<w:p><w:pPr><w:pStyle w:val="Heading${lvl}"/><w:rPr/></w:pPr>` +
    `<w:bookmarkStart w:colFirst="0" w:colLast="0" w:name="_h${id}" w:id="${id}"/><w:bookmarkEnd w:id="${id}"/>${run(t)}</w:p>`; };
const H5 = (t) => HEAD(5, t), H6 = (t) => HEAD(6, t);

const BORDER = ["top","left","bottom","right","insideH","insideV"]
  .map((x) => `<w:${x} w:val="single" w:sz="4" w:space="0" w:color="BFBFBF"/>`).join("");
const cellP = (t, b) => `<w:p><w:pPr><w:spacing w:after="30" w:before="30" w:lineRule="auto"/><w:rPr/></w:pPr>${run(t, { b })}</w:p>`;
const TBL = (rows, w) => {
  const tc = (c, ci, ri) => '<w:tc><w:tcPr><w:tcW w:w="' + w[ci] + '" w:type="dxa"/>' +
    (ri === 0 ? '<w:shd w:fill="F2F2F2" w:val="clear"/>' : "") + "</w:tcPr>" + cellP(c, ri === 0) + "</w:tc>";
  return '<w:tbl><w:tblPr><w:tblStyle w:val="TableNormal"/><w:tblW w:w="' + w.reduce((a, b) => a + b, 0) + '" w:type="dxa"/>' +
    "<w:tblBorders>" + BORDER + '</w:tblBorders><w:tblLayout w:type="fixed"/>' +
    '<w:tblCellMar><w:top w:w="40" w:type="dxa"/><w:left w:w="90" w:type="dxa"/>' +
    '<w:bottom w:w="40" w:type="dxa"/><w:right w:w="90" w:type="dxa"/></w:tblCellMar></w:tblPr>' +
    "<w:tblGrid>" + w.map((x) => '<w:gridCol w:w="' + x + '"/>').join("") + "</w:tblGrid>" +
    rows.map((r, ri) => "<w:tr>" + r.map((c, ci) => tc(c, ci, ri)).join("") + "</w:tr>").join("") +
    "</w:tbl>" + EMPTY;
};

const l = (n) => n.toFixed(1).replace(".", ",");

/* ------------------------------ 5.4.1 czas ------------------------------ */
const CZAS_WNIOSEK = [
  P("Rozpisanie pozostałych prac daje " + l(OM_BAZA) + " osobomiesiąca po stronie programowania. Część tej pracy zdejmuje wsparcie modeli językowych przy pisaniu kodu, co sprowadza ją do " + l(OM_AI) + " osobomiesiąca, czyli o " + Math.round((1 - OM_AI / OM_BAZA) * 100) + " procent mniej."),
  P(["Ta oszczędność rozkłada się bardzo nierówno. ", { b: true }],
    "Przy kodzie powtarzalnym i dobrze opisanym w dokumentacji, jak integracja ze Steam, interfejs czy zapis stanu, sięga połowy. Przy dobieraniu zachowania fizyki albo szukaniu przyczyny rozjazdu między graczami jest niewielka, bo o tempie decyduje tam uruchamianie gry i ocena efektu, a nie samo pisanie kodu. Dlatego nie stosujemy jednego wskaźnika do całości, tylko szacujemy każde zadanie osobno."),
  P("Przy jednym programiście i realnym czasie produktywnym rzędu " + Math.round(WYDAJNOSC * 100) + " procent daje to " + MIESIACE + " miesięcy do premiery w Early Access."),
];

/* --------------------------------- 5.4.2 -------------------------------- */
const KOSZT = [
  TBL([
    ["Osoba", "Rola", "Forma", "Koszt projektu / mies.", "Razem za " + MIESIACE + " mies."],
    ...PLAN.wiersze.map((o) => [o.osoba, o.rola, o.forma, kw(o.koszt), kw(o.razem)]),
    ["BUDŻET RAZEM", "", "", kw(RDZEN_MIES), kw(PLAN.razem)],
  ], [2200, 1900, 1400, 1800, 1729]),
];

/* --------------------------------- 5.4.3 -------------------------------- */
const PROG = [
  P("Przy cenie 69 złotych w Early Access do projektu trafia około " + kw(NETTO_NA_EGZ) + " z każdego sprzedanego egzemplarza. Przy budżecie " + kw(PLAN.razem) + " oznacza to, że nakłady zwracają się przy sprzedaży około " + lb(PLAN.progZwrotu) + " egzemplarzy."),
  P("Tytuły kooperacyjne z tego segmentu, które znalazły odbiorców, sprzedają się w dziesiątkach i setkach tysięcy kopii. Nie jest to gwarancja wyniku, natomiast pokazuje, że projekt nie wymaga sukcesu na skalę przeboju, aby zwrócić nakłady."),
];

/* ------------------------- 5.5 Harmonogram prac ------------------------- */
const T = (rows) => TBL(rows, [4900, 4129]);

const OPIS_ETAPU = [
  "Zaczynamy od rdzenia, bo wszystko inne się na nim opiera. Dopóki stworzenie nie porusza się i nie przewraca w sposób, który gracz rozumie i akceptuje, nie ma sensu budować na tym reszty gry - każda zmiana w fizyce oznaczałaby poprawianie tego, co już zostało zrobione.",
  "Kolejny etap to doprowadzenie do stanu, w którym czterech graczy widzi to samo. Symulacja fizyczna jest tu najtrudniejszym przypadkiem, bo drobna różnica u jednego gracza szybko narasta i kończy się rozjazdem.",
  "Największy etap pod względem ilości pracy. Powstaje tu wszystko, co gracz widzi jako zawartość gry: mapa, przeciwnicy, interfejs oraz kolejne części ciała, z których każda wnosi własne działanie.",
  "Na koniec optymalizacja, dźwięk i przygotowanie wydania.",
];

const HARMONOGRAM = [
  H5("5.5 Harmonogram prac"),
  P("Produkcja podzielona jest na cztery etapy. Kolejność nie jest dowolna - wynika z tego, że każdy kolejny etap opiera się na poprzednim, a odwrócenie tej kolejności oznaczałoby wykonywanie tej samej pracy dwa razy."),
  ...ETAPY_MIES.flatMap((e, i) => [
    H6("Etap " + (i + 1) + ": " + e.nazwa.toLowerCase() + ", miesiące " + e.od + "-" + e.do),
    P(OPIS_ETAPU[i]),
    T([["Zakres prac", "Nakład"], ...e.zadania.map(([n, om]) => [n, l(om) + " osobomies."])]),
  ]),
  H6("Punkty kontrolne"),
  P("Trzy momenty pozwalają ocenić postęp bez polegania na raportach. Każdy kończy się kompilacją, którą można uruchomić samodzielnie."),
  LI(["Koniec etapu 1, miesiąc " + ETAPY_MIES[0].do + ". ", { b: true }], "Stworzenie porusza się, walczy i wchodzi w interakcje ze światem w sposób, który da się ocenić grając."),
  LI(["Koniec etapu 2, miesiąc " + ETAPY_MIES[1].do + ". ", { b: true }], "Czterech graczy gra razem przez Steam i widzi ten sam przebieg rozgrywki."),
  LI(["Koniec etapu 3, miesiąc " + ETAPY_MIES[2].do + ". ", { b: true }], "Pełna rozgrywka od bazy przez mapę do bossa. Od tego momentu zostaje praca wykończeniowa."),
];

/* ============================== PODMIANY ============================== */

const zip = await JSZip.loadAsync(readFileSync(IN));
let xml = await zip.file("word/document.xml").async("string");
const przed = xml.length;
let ok = 0, blad = 0;

const pStart = (frag, nth = 1) => { let i = -1; for (let k = 0; k < nth; k++) { i = xml.indexOf(frag, i + 1); if (i < 0) return -1; } return xml.lastIndexOf("<w:p ", i); };
const pEnd = (frag, nth = 1) => { let i = -1; for (let k = 0; k < nth; k++) { i = xml.indexOf(frag, i + 1); if (i < 0) return -1; } return xml.indexOf("</w:p>", i) + 6; };
const tblEnd = (od) => xml.indexOf("</w:tbl>", od) + 8;

// 5.4.1 - wniosek po tabeli porównawczej
{
  const t = xml.indexOf("Hollow Knight");
  const koniec = t < 0 ? -1 : tblEnd(t);
  if (koniec < 8) { console.error("  BŁĄD: 5.4.1"); blad++; }
  else { xml = xml.slice(0, koniec) + CZAS_WNIOSEK.join("") + xml.slice(koniec); console.log("  5.4.1 - dopisano wniosek o czasie"); ok++; }
}

// 5.4.2 - od tabeli wariantów do końca tabeli zbiorczej
{
  // nagłówek w treści, a nie wpis w spisie treści -> drugie wystąpienie
  const h = xml.indexOf("5.4.2 Koszt", xml.indexOf("5.4.2 Koszt") + 1);
  const start = h < 0 ? -1 : xml.indexOf("<w:tbl>", h);
  const z = xml.indexOf("Średni koszt miesięczny");
  const koniec = z < 0 ? -1 : tblEnd(z);
  if (start < 0 || koniec < 8) { console.error("  BŁĄD: 5.4.2"); blad++; }
  else { xml = xml.slice(0, start) + KOSZT.join("") + xml.slice(koniec); console.log("  5.4.2 - przeliczono koszty"); ok++; }
}

// 5.4.3 - od akapitu o cenie do akapitu zamykającego
{
  const a = pStart("Przy cenie 69 złotych");
  const b = pEnd("nie wymaga sukcesu na skalę przeboju");
  if (a < 0 || b < 6) { console.error("  BŁĄD: 5.4.3"); blad++; }
  else { xml = xml.slice(0, a) + PROG.join("") + xml.slice(b); console.log("  5.4.3 - przeliczono próg zwrotu"); ok++; }
}

// 5.4.3 - usunięcie tabeli wariantów, zostaje sam tekst
{
  const t = xml.indexOf("Liczba egzemplarzy do zwrotu nakładów");
  if (t >= 0) {
    const st = xml.lastIndexOf("<w:tbl>", t), en = xml.indexOf("</w:tbl>", t) + 8;
    xml = xml.slice(0, st) + xml.slice(en);
    console.log("  5.4.3 - usunięto tabelę wariantów");
  }
}

// 5.4.4 - aktualizacja kwoty utrzymania zespołu
{
  const a = pStart("Okresu po premierze w Early Access");
  const b = pEnd("Okresu po premierze w Early Access");
  if (a < 0) { console.error("  BŁĄD: 5.4.4"); blad++; }
  else {
    xml = xml.slice(0, a) + LI(["Okresu po premierze w Early Access. ", { b: true }],
      "Utrzymanie zespołu po wejściu do sprzedaży to " + kw(RDZEN_MIES) + " miesięcznie. Część tej kwoty pokrywają przychody, dlatego zapotrzebowanie zależy od wyniku premiery i wymaga osobnego ustalenia.") + xml.slice(b);
    console.log("  5.4.4 - zaktualizowano kwotę utrzymania"); ok++;
  }
}

// 5.5 - harmonogram na końcu dokumentu
{
  const s = xml.indexOf("<w:sectPr");
  if (s < 0) { console.error("  BŁĄD: 5.5"); blad++; }
  else { xml = xml.slice(0, s) + HARMONOGRAM.join("") + xml.slice(s); console.log("  5.5 - dodano harmonogram prac"); ok++; }
}

// spis treści
{
  const wzor = "5.4.4 Czego budżet nie obejmuje";
  const a = pStart(wzor), b = pEnd(wzor);
  if (a < 0) { console.error("  BŁĄD: spis treści"); blad++; }
  else {
    const klon = (t) => { let f = true;
      return xml.slice(a, b).replace(/<w:t xml:space="preserve">[\s\S]*?<\/w:t>/g, (m) =>
        f ? (f = false, `<w:t xml:space="preserve">${esc(t)}</w:t>`) : m); };
    xml = xml.slice(0, b) + ["5.5 Harmonogram prac"].map(klon).join("") + xml.slice(b);
    console.log("  spis treści - dopisano 5.5");
  }
}

let dash = 0;
xml = xml.replace(/<w:t([^>]*)>([\s\S]*?)<\/w:t>/g, (m, at, t) => {
  const z2 = t.replace(/[—–]/g, "-"); if (z2 !== t) dash += (t.match(/[—–]/g) || []).length;
  return `<w:t${at}>${z2}</w:t>`; });

zip.file("word/document.xml", xml);
writeFileSync(OUT, await zip.generateAsync({ type: "nodebuffer", compression: "DEFLATE" }));
console.log("\nzmian:", ok, "| błędów:", blad, "| myślników:", dash, "|", przed, "->", xml.length);
console.log("zapisano:", OUT);
