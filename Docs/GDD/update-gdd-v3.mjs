/**
 * Uzupełnia Leeway_GDD.docx (wersja z 20.09) o:
 *   5.2.2 Stack technologiczny  - tabela funkcjonalność / rozwiązanie / status
 *   5.4   Potrzeby wydawnicze   - wyliczenie budżetu
 * Edycja w miejscu: zachowuje obrazy, czcionki i style oryginału.
 */
import JSZip from "./node_modules/jszip/lib/index.js";
import { readFileSync, writeFileSync } from "fs";
import {
  PLAN_BAZOWY, PLAN_OUT, REZERWA, RDZEN_MIES, MIES_BAZOWY, MIES_OUT,
  MAGDA_FAKTURA, MAGDA_VAT, NETTO_NA_EGZ,
} from "./finanse.mjs";

const IN = process.env.IN || "Leeway_GDD.docx";
const OUT = process.env.OUT || "Leeway_GDD_v3.docx";

const esc = (s) => String(s).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
const kw = (n) => Math.round(n).toString().replace(/\B(?=(\d{3})+(?!\d))/g, " ") + " PLN";
const mies = (n) => { const d = n % 10, s = n % 100;
  return n + (d >= 2 && d <= 4 && !(s >= 12 && s <= 14) ? " miesiące" : " miesięcy"); };
const lb = (n) => Math.round(n).toString().replace(/\B(?=(\d{3})+(?!\d))/g, " ");

const run = (t, { b = false, i = false } = {}) =>
  `<w:r><w:rPr>${b ? '<w:b w:val="1"/><w:bCs w:val="1"/>' : ""}${i ? '<w:i w:val="1"/><w:iCs w:val="1"/>' : ""}<w:rtl w:val="0"/></w:rPr>` +
  `<w:t xml:space="preserve">${esc(t)}</w:t></w:r>`;
const runs = (p) => p.map((x) => (typeof x === "string" ? run(x) : run(x[0], x[1]))).join("");
const P = (...p) => `<w:p><w:pPr><w:ind w:firstLine="720"/><w:jc w:val="both"/><w:rPr/></w:pPr>${runs(p)}</w:p>`;
const LI = (...p) => `<w:p><w:pPr><w:numPr><w:ilvl w:val="0"/><w:numId w:val="1"/></w:numPr>` +
  `<w:ind w:left="720" w:hanging="360"/><w:jc w:val="both"/><w:rPr><w:u w:val="none"/></w:rPr></w:pPr>${runs(p)}</w:p>`;
const EMPTY = `<w:p><w:pPr><w:rPr/></w:pPr></w:p>`;
let bmId = 700;
const H6 = (t) => { const id = ++bmId; return `<w:p><w:pPr><w:pStyle w:val="Heading6"/><w:rPr/></w:pPr>` +
  `<w:bookmarkStart w:colFirst="0" w:colLast="0" w:name="_n${id}" w:id="${id}"/><w:bookmarkEnd w:id="${id}"/>${run(t)}</w:p>`; };

const BORDER = ["top","left","bottom","right","insideH","insideV"]
  .map((s) => `<w:${s} w:val="single" w:sz="4" w:space="0" w:color="BFBFBF"/>`).join("");
const cellP = (t, b) => `<w:p><w:pPr><w:spacing w:after="30" w:before="30" w:lineRule="auto"/><w:rPr/></w:pPr>${run(t, { b })}</w:p>`;
const TBL = (rows, w) => {
  const komorka = (c, ci, ri) =>
    '<w:tc><w:tcPr><w:tcW w:w="' + w[ci] + '" w:type="dxa"/>' +
    (ri === 0 ? '<w:shd w:fill="F2F2F2" w:val="clear"/>' : "") +
    "</w:tcPr>" + cellP(c, ri === 0) + "</w:tc>";
  const wiersz = (r, ri) => "<w:tr>" + r.map((c, ci) => komorka(c, ci, ri)).join("") + "</w:tr>";
  return '<w:tbl><w:tblPr><w:tblStyle w:val="TableNormal"/><w:tblW w:w="' + w.reduce((x, y) => x + y, 0) + '" w:type="dxa"/>' +
    "<w:tblBorders>" + BORDER + '</w:tblBorders><w:tblLayout w:type="fixed"/>' +
    '<w:tblCellMar><w:top w:w="40" w:type="dxa"/><w:left w:w="90" w:type="dxa"/>' +
    '<w:bottom w:w="40" w:type="dxa"/><w:right w:w="90" w:type="dxa"/></w:tblCellMar></w:tblPr>' +
    "<w:tblGrid>" + w.map((x) => '<w:gridCol w:w="' + x + '"/>').join("") + "</w:tblGrid>" +
    rows.map(wiersz).join("") + "</w:tbl>" + EMPTY;
};

/* ========================= 5.2.2 STACK TECHNOLOGICZNY ========================= */

const STACK = [
  P("Projekt działa na Unity 6 w wersji LTS z renderowaniem URP. Poniższa tabela zestawia funkcjonalności potrzebne do zbudowania gry w zakresie opisanym w 5.1 wraz z rozwiązaniem, które je realizuje, oraz stanem wdrożenia."),
  H6("Rdzeń projektu"),
  TBL([
    ["Funkcjonalność", "Rozwiązanie / plugin", "Status"],
    ["Silnik", "Unity 6000.4.7f1 LTS", "W projekcie"],
    ["Renderowanie", "Universal Render Pipeline 17.4.0", "W projekcie"],
    ["Fizyka", "Unity Physics (PhysX) - moduł wbudowany", "W projekcie"],
    ["Sieć i multiplayer", "FishNet 4.7.2", "W projekcie"],
    ["Wstrzykiwanie zależności", "VContainer 1.18.0", "W projekcie"],
    ["Komunikacja między systemami", "MessagePipe", "W projekcie"],
    ["Operacje asynchroniczne", "UniTask", "W projekcie"],
    ["Animacja wartości i przejścia", "LitMotion", "W projekcie"],
    ["Kamery", "Cinemachine 3.1.6", "W projekcie"],
    ["Wejście gracza", "Input System 1.19.0", "W projekcie"],
    ["Interfejs użytkownika", "uGUI 2.0.0 + TextMeshPro", "W projekcie"],
    ["Efekty wizualne", "VFX Graph 17.4.0", "W projekcie"],
    ["Nawigacja przeciwników", "AI Navigation 2.0.12", "W projekcie"],
    ["Blokowanie poziomów", "ProBuilder 6.0.9", "W projekcie"],
    ["Sekwencje i animacje czasowe", "Timeline 1.8.12", "W projekcie"],
    ["Testy automatyczne", "Unity Test Framework 1.6.0", "W projekcie"],
    ["Testy z wieloma klientami", "Multiplayer Play Mode 2.0.2", "W projekcie"],
    ["Kontrola wersji", "Git oraz Unity Version Control", "W projekcie"],
  ], [3000, 4100, 1929]),
  H6("Do wdrożenia"),
  TBL([
    ["Funkcjonalność", "Rozwiązanie / plugin", "Uwagi"],
    ["Silnik dźwięku", "FMOD Studio wraz z integracją Unity", "Wariant licencji zależny od wielkości finansowania - patrz 5.4"],
    ["Integracja ze Steam", "Steamworks.NET lub Facepunch.Steamworks", "Warunek dla lobby, zaproszeń i późniejszego Workshopu"],
    ["Transport sieciowy przez Steam", "FishySteamworks (transport dla FishNet)", "Pozwala grać przez Steam bez przekierowania portów"],
    ["Lobby, zaproszenia, dołączanie", "Steam Matchmaking i Steam Lobbies", "Obsługa przepływu opisanego w 3.1"],
    ["Generowanie wysp", "Rozwiązanie własne na zestawach assetów", "Opisane w 3.2; do napisania od podstaw"],
    ["Zapis stanu gry i postępu", "Rozwiązanie własne", "Kodek genomu już istnieje i jest podstawą zapisu"],
    ["Lokalizacja", "Unity Localization", "Na premierę jeden język, docelowo trzy lub więcej"],
    ["Profilowanie pamięci", "Memory Profiler", "Niezbędne przy wielu stworzeniach z proceduralnym rigiem naraz"],
    ["Renderowanie wielu instancji", "GPU Resident Drawer i instancing w URP", "Wbudowane w URP, wymaga konfiguracji"],
    ["Oświetlenie statyczne", "Adaptive Probe Volumes w URP", "Wbudowane, wymaga konfiguracji pod styl z rozdziału 4"],
  ], [3000, 3400, 2629]),
  H6("Po premierze w Early Access"),
  TBL([
    ["Funkcjonalność", "Rozwiązanie / plugin", "Uwagi"],
    ["Dzielenie się stworzeniami", "Steamworks UGC (Steam Workshop)", "Zapis genomu jest już przygotowany pod tę funkcję"],
    ["Osiągnięcia i statystyki", "Steam Achievements i Steam Stats", "Sensowne dopiero po ustabilizowaniu balansu"],
    ["Raportowanie błędów", "Sentry lub Unity Cloud Diagnostics", "Do rozważenia wraz ze wzrostem liczby graczy"],
    ["Ładowanie modów (kod)", "BepInEx 5, dystrybucja przez Thunderstore", "Standard gatunku. Działa bez pracy po naszej stronie, pod warunkiem pozostania przy backendzie Mono"],
    ["Dodawanie części ciała przez graczy", "Własny loader katalogu części, ładowany jako wtyczka BepInEx", "Serwer musi znać reguły i cenę części pochodzącej z moda"],
  ], [3000, 3400, 2629]),
  H6("Uwaga o modowaniu"),
  P("Nie planujemy pisać własnego systemu wtyczek. W tym gatunku standardem jest BepInEx, czyli zewnętrzny loader podmieniający kod gry w czasie działania, wraz z dystrybucją modów przez Thunderstore. Tak zmodowane są Lethal Company i pozostałe tytuły, do których się porównujemy, przy czym w żadnym z nich nie było to funkcją dostarczoną przez twórców - społeczność zrobiła to sama. Oznacza to, że mody powstaną niezależnie od tego, czy je zaplanujemy, a jedyne realne pytanie brzmi, czy im nie przeszkodzimy."),
  P(["Warunek techniczny jest jeden i trzeba go pilnować od teraz. ", { b: true }], "BepInEx w stabilnej wersji 5 działa na backendzie skryptowym Mono. Projekt korzysta obecnie z Mono, ponieważ jest to ustawienie domyślne i nikt go nie zmieniał. Przejście na IL2CPP, kuszące ze względu na wydajność przy symulacji fizycznej, zepchnęłoby modowanie na wersję 6, która pozostaje w fazie przedpremierowej i nie ładuje wtyczek napisanych pod wersję 5. Decyzję o backendzie traktujemy więc jako decyzję o modowalności gry, a nie wyłącznie o wydajności."),
  P("Sam BepInEx nie rozwiązuje jednak problemu, na którym nam najbardziej zależy, czyli dodawania przez graczy nowych części ciała. Serwer jest autorytatywny i sprawdza każde stworzenie względem własnego zestawu reguł oraz wycenia je w budżecie, a zapis genomu odwołuje się do identyfikatorów części. Część dołożona wyłącznie po stronie klienta zostanie odrzucona, a stworzenie zapisane z jej użyciem przestanie się otwierać u kogoś, kto tego moda nie ma. Dlatego obok BepInEksa przewidujemy cienką warstwę własną: ładowanie katalogu części z paczki moda razem z regułami i ceną, trwałe identyfikatory oraz sprawdzenie zgodności zestawu modów między graczami przy wejściu do sesji."),
  P("Taki podział jest tańszy niż pierwotnie zakładaliśmy. Najdroższy element, czyli sam loader wtyczek, dostajemy gotowy. Po naszej stronie zostaje wyłącznie to, czego nikt za nas nie zrobi, bo wymaga dostępu do walidatora i do budżetu stworzenia."),
  H6("Narzędzia wspomagające"),
  P("Poza stosem uruchomieniowym w projekcie działa integracja MCP dla Unity, pozwalająca operować na edytorze i konsoli z poziomu narzędzi zewnętrznych, oraz generator dokumentacji architektury tworzący diagramy zależności między modułami. Oba służą wyłącznie zespołowi i nie trafiają do wydanej gry."),
  P("W projekcie pozostają dwa elementy odziedziczone po szablonie Unity, nieużywane w Leeway: profil renderowania dla urządzeń mobilnych oraz pakiet Visual Scripting. Nie są konfigurowane i przewidziane są do usunięcia przy najbliższym porządkowaniu zależności."),
];

/* ============================ 5.3.2 MODEL PRACY ============================ */

const ZESPOL_32 = [
  P("Gra powstaje w obecnym, trzyosobowym składzie. Nie planujemy budowania większego zespołu - przy tej wielkości każda nowa osoba oznacza tygodnie wdrożenia i stałe koszty koordynacji, a tempo traci ten, kto wdraża."),
  P(["Wąskim gardłem jest programowanie. ", { b: true }], "Cała warstwa systemowa, sieciowa i fizyczna przechodzi przez jedną osobę i to ona wyznacza datę premiery. Prace graficzne i projektowe mieszczą się w tym samym oknie czasowym z zapasem."),
  P("Widzimy tu przestrzeń na wsparcie z zewnątrz. Część zadań programistycznych daje się wydzielić bez naruszania rdzenia, podobnie jak część prac graficznych i cała warstwa audio. Zlecenie ich na czas produkcji skraca harmonogram o kilka miesięcy i jest opisane w 5.4 jako osobny wariant. Nie wykluczamy też dotrudnienia jednej osoby na stałe, jeżeli wydawca uzna, że skrócenie czasu wejścia na rynek jest warte tej inwestycji."),
];

/* ========================== 5.4 POTRZEBY WYDAWNICZE ========================== */

const FINANSE = [
  P("Wyliczenie obejmuje produkcję do premiery w Early Access w zakresie opisanym w 5.1, realizowaną obecnym trzyosobowym zespołem. Kwoty w złotych, jako pełny koszt po stronie projektu."),
  H6("5.4.1 Czas realizacji"),
  P("Szacunek oparliśmy na dwóch niezależnych podstawach: na rozpisaniu pozostałych prac oraz na czasie, jakiego na porównywalne tytuły potrzebowały zespoły podobnej wielkości."),
  TBL([
    ["Tytuł", "Zespół", "Czas do premiery w Early Access"],
    ["Valheim", "5 osób", "ok. 2,5 roku, produkcja od zera"],
    ["Raft", "3 osoby rosnące do 7", "ok. 1,5 roku od prototypu"],
    ["Hollow Knight", "3 osoby", "ok. 3 lata do pełnej premiery"],
  ], [2700, 2700, 3629]),
  P("Na tym tle Leeway ma dwie istotne przewagi. Najtrudniejsza technicznie warstwa, czyli kreator, rig proceduralny i szkielet sieciowy, jest już zbudowana, co odróżnia nas od zespołów zaczynających od zera. Zakres wejścia do Early Access jest też celowo wąski i wynosi jeden archetyp wyspy, około dwudziestu blueprintów i jedno starcie z bossem."),
  P("Przy trzyosobowym zespole daje to " + mies(MIES_BAZOWY) + " do premiery. Zlecenie na zewnątrz prac, które da się prowadzić równolegle, skraca ten czas do " + mies(MIES_OUT) + "."),
  H6("5.4.2 Koszt"),
  TBL([
    ["Wariant", "Czas", "Budżet", "Próg zwrotu"],
    ["Zespół własny", mies(MIES_BAZOWY), kw(PLAN_BAZOWY.razem), lb(PLAN_BAZOWY.progZwrotu) + " egz."],
    ["Ze wsparciem zewnętrznym", mies(MIES_OUT), kw(PLAN_OUT.razem), lb(PLAN_OUT.progZwrotu) + " egz."],
  ], [3000, 1900, 2100, 2029]),
  P("Sześć miesięcy krótszy harmonogram kosztuje " + kw(PLAN_OUT.razem - PLAN_BAZOWY.razem) + " więcej. Rekomendujemy ten wariant: wcześniejsze wejście na rynek oznacza wcześniejszy przychód i krótszy okres finansowania bez sprzedaży."),
  TBL([
    ["Osoba", "Rola", "Forma", "Stawka / mies.", "Koszt projektu / mies."],
    ...PLAN_BAZOWY.wiersze.map((o) => [o.osoba, o.rola, o.forma, kw(o.stawka), kw(o.koszt)]),
    ["Razem", "", "", "", kw(RDZEN_MIES)],
  ], [2200, 1900, 1500, 1700, 1729]),
  TBL([
    ["Pozycja", "Zespół własny", "Ze wsparciem zewnętrznym"],
    ["Wynagrodzenia zespołu", kw(PLAN_BAZOWY.osobowe), kw(PLAN_OUT.osobowe)],
    ["Zlecenia zewnętrzne", "-", kw(PLAN_OUT.outsourcing)],
    ["Pozostałe koszty", kw(PLAN_BAZOWY.poz), kw(PLAN_OUT.poz)],
    ["Rezerwa na ryzyko (" + Math.round(REZERWA * 100) + " procent)", kw(PLAN_BAZOWY.rezerwa), kw(PLAN_OUT.rezerwa)],
    ["BUDŻET RAZEM", kw(PLAN_BAZOWY.razem), kw(PLAN_OUT.razem)],
    ["Średni koszt miesięczny", kw(PLAN_BAZOWY.razem / PLAN_BAZOWY.miesiace), kw(PLAN_OUT.razem / PLAN_OUT.miesiace)],
  ], [3400, 2700, 2929]),
  H6("5.4.3 Próg zwrotu"),
  P("Przy cenie 69 złotych w Early Access do projektu trafia około " + kw(NETTO_NA_EGZ) + " z każdego sprzedanego egzemplarza."),
  TBL([
    ["Wariant", "Budżet", "Liczba egzemplarzy do zwrotu nakładów"],
    ["Zespół własny", kw(PLAN_BAZOWY.razem), lb(PLAN_BAZOWY.progZwrotu) + " egz."],
    ["Ze wsparciem zewnętrznym", kw(PLAN_OUT.razem), lb(PLAN_OUT.progZwrotu) + " egz."],
  ], [3000, 2500, 3529]),
  P("Próg mieści się poniżej trzydziestu tysięcy egzemplarzy w obu wariantach. Dla porównania, tytuły kooperacyjne z tego segmentu, które w ogóle znalazły odbiorców, sprzedają się w dziesiątkach i setkach tysięcy kopii. Nie jest to gwarancja wyniku, natomiast pokazuje, że projekt nie wymaga sukcesu na skalę przeboju, aby zwrócić nakłady."),
  H6("5.4.4 Czego budżet nie obejmuje"),
  LI(["Marketingu. ", { b: true }], "Zakładamy, że prowadzi go wydawca."),
  LI(["Okresu po premierze w Early Access. ", { b: true }], "Utrzymanie zespołu po wejściu do sprzedaży to " + kw(RDZEN_MIES) + " miesięcznie. Część tej kwoty pokrywają przychody, dlatego zapotrzebowanie zależy od wyniku premiery i wymaga osobnego ustalenia."),
  LI(["Wersji konsolowych i serwerów dedykowanych. ", { b: true }], "Poza zakresem na tym etapie."),
];

/* ============================== PODMIANY W PLIKU ============================== */

const zip = await JSZip.loadAsync(readFileSync(IN));
let xml = await zip.file("word/document.xml").async("string");
const przed = xml.length;
let ok = 0, blad = 0;

const paraEnd = (src, frag, nth = 1) => {
  let i = -1; for (let k = 0; k < nth; k++) { i = src.indexOf(frag, i + 1); if (i < 0) return -1; }
  return src.indexOf("</w:p>", i) + 6;
};
const paraStart = (src, frag, nth = 1) => {
  let i = -1; for (let k = 0; k < nth; k++) { i = src.indexOf(frag, i + 1); if (i < 0) return -1; }
  return src.lastIndexOf("<w:p ", i);
};

// 5.2.2 — treść wstawiana pod pustym nagłówkiem (2. wystąpienie = nagłówek w treści, nie spis treści)
{
  const b = paraEnd(xml, "5.2.2 Stack technologiczny");
  if (b < 6) { console.error("  BŁĄD: 5.2.2"); blad++; }
  else { xml = xml.slice(0, b) + STACK.join("") + xml.slice(b); console.log("  uzupełniono: 5.2.2 Stack technologiczny"); ok++; }
}

// 5.4 — podmiana urwanego zdania na pełne wyliczenie
{
  const a = paraStart(xml, "Proponujemy finansowanie etapowe");
  const b = paraEnd(xml, "Proponujemy finansowanie etapowe");
  if (a < 0 || b < 6) { console.error("  BŁĄD: 5.4"); blad++; }
  else { xml = xml.slice(0, a) + FINANSE.join("") + xml.slice(b); console.log("  uzupełniono: 5.4 Potrzeby wydawnicze"); ok++; }
}

// 5.3.2 — model pracy bez rekrutacji
{
  const a2 = paraStart(xml, "W pierwszych czterech miesiącach pracują");
  const b2 = paraEnd(xml, "Docelowa wielkość zespołu to osiem osób");
  if (a2 < 0 || b2 < 6) { console.error("  BŁĄD: 5.3.2"); blad++; }
  else { xml = xml.slice(0, a2) + ZESPOL_32.join("") + xml.slice(b2); console.log("  uzupełniono: 5.3.2 Model pracy"); ok++; }
}

// 5.1 — usunięcie nieaktualnego odwołania do premiery w 12. miesiącu
{
  const a3 = paraStart(xml, "Zakres opisano w dwóch punktach");
  const b3 = paraEnd(xml, "Zakres opisano w dwóch punktach");
  if (a3 < 0) { console.error("  BŁĄD: 5.1"); blad++; }
  else {
    xml = xml.slice(0, a3) + P("Zakres opisano w dwóch punktach: na premierę w Early Access i na wersję 1.0. Wejście do Early Access traktujemy jako plan minimum, czyli najmniejszy zestaw, który da się sprzedać i który pozwala ocenić, czy gra znajduje odbiorców. Harmonogram i koszt dojścia do tego zakresu opisano w 5.4.") + xml.slice(b3);
    console.log("  poprawiono: 5.1 zdanie wprowadzające"); ok++;
  }
}

// spis treści — klon istniejącego wiersza z podmianą etykiety
const tocKlon = (wzor, tekst) => {
  const a = paraStart(xml, wzor), b = paraEnd(xml, wzor);
  if (a < 0) return null;
  let pierwszy = true;
  return xml.slice(a, b).replace(/<w:t xml:space="preserve">[\s\S]*?<\/w:t>/g, (m) =>
    pierwszy ? (pierwszy = false, `<w:t xml:space="preserve">${esc(tekst)}</w:t>`) : m);
};
{
  const b = paraEnd(xml, "5.4 Potrzeby wydawnicze");
  const wpisy = ["5.4.1 Czas realizacji", "5.4.2 Koszt", "5.4.3 Próg zwrotu", "5.4.4 Czego budżet nie obejmuje"];
  if (b < 6) { console.error("  BŁĄD: spis treści"); blad++; }
  else {
    xml = xml.slice(0, b) + wpisy.map((t) => tocKlon("5.4 Potrzeby wydawnicze", t)).filter(Boolean).join("") + xml.slice(b);
    console.log("  spis treści: dopisano", wpisy.length, "pozycji");
  }
}

// myślniki na dywiz
let dash = 0;
xml = xml.replace(/<w:t([^>]*)>([\s\S]*?)<\/w:t>/g, (m, a, t) => {
  const z = t.replace(/[—–]/g, "-");
  if (z !== t) dash += (t.match(/[—–]/g) || []).length;
  return `<w:t${a}>${z}</w:t>`;
});

zip.file("word/document.xml", xml);
const buf = await zip.generateAsync({ type: "nodebuffer", compression: "DEFLATE" });
writeFileSync(OUT, buf);
console.log("\nzmian:", ok, "| błędów:", blad, "| myślników:", dash);
console.log("document.xml:", przed, "->", xml.length, "| zapisano:", OUT, (buf.length/1024/1024).toFixed(2), "MB");
