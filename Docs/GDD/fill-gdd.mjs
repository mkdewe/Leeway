/**
 * Uzupełnia Leeway_GDD.docx (dokument zespołu) o brakujące sekcje.
 * Edytuje document.xml w miejscu, żeby zachować obrazy, czcionki i formatowanie oryginału.
 * Treść zespołu jest nadrzędna — skrypt podmienia wyłącznie akapity-placeholdery.
 */
import JSZip from "./node_modules/jszip/lib/index.js";
import { readFileSync, writeFileSync } from "fs";
import { NET_PER_UNIT, SCEN, fmt, breakEven , num } from "./lib-budget.mjs";
import { TR, TOTAL, EXPOSURE, EA_MONTH, V10_MONTH } from "./lib-tranches.mjs";

const IN  = process.env.IN  || "/workspace/Docs/GDD/Leeway_GDD.docx";
const OUT = process.env.OUT || "/workspace/Docs/GDD/Leeway_GDD_uzupelniony.docx";

/* ---------------- pomocniki WordprocessingML w stylu oryginału ---------------- */

const esc = (s) => String(s).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");

const run = (text, { b = false, i = false } = {}) =>
  `<w:r><w:rPr>${b ? '<w:b w:val="1"/><w:bCs w:val="1"/>' : ""}${i ? '<w:i w:val="1"/><w:iCs w:val="1"/>' : ""}<w:rtl w:val="0"/></w:rPr>` +
  `<w:t xml:space="preserve">${esc(text)}</w:t></w:r>`;

// akapit treści — dokładnie taki jak w rozdziale 4 oryginału
const P = (...parts) =>
  `<w:p><w:pPr><w:spacing w:after="240" w:before="240" w:lineRule="auto"/><w:jc w:val="both"/><w:rPr/></w:pPr>` +
  parts.map((p) => (typeof p === "string" ? run(p) : run(p[0], p[1]))).join("") +
  `</w:p>`;

// wcięty wiersz wyliczenia (dokument nie ma zdefiniowanych list, więc myślnik)
const L = (...parts) =>
  `<w:p><w:pPr><w:spacing w:after="80" w:before="80" w:lineRule="auto"/>` +
  `<w:ind w:left="454" w:hanging="227"/><w:jc w:val="both"/><w:rPr/></w:pPr>` +
  run("- ") + parts.map((p) => (typeof p === "string" ? run(p) : run(p[0], p[1]))).join("") +
  `</w:p>`;

let bmId = 900;
const H5 = (text) => {
  const id = ++bmId, anchor = "_dodane" + id;
  return `<w:p><w:pPr><w:pStyle w:val="Heading5"/><w:rPr/></w:pPr>` +
    `<w:bookmarkStart w:colFirst="0" w:colLast="0" w:name="${anchor}" w:id="${id}"/><w:bookmarkEnd w:id="${id}"/>` +
    run(text) + `</w:p>`;
};

// wiersz spisu treści w formacie oryginału
const TOC = (text, page) =>
  `<w:p><w:pPr><w:widowControl w:val="0"/><w:tabs><w:tab w:val="right" w:leader="none" w:pos="12000"/></w:tabs>` +
  `<w:spacing w:before="60" w:line="240" w:lineRule="auto"/><w:ind w:left="360" w:firstLine="0"/>` +
  `<w:rPr><w:rFonts w:ascii="Arial" w:cs="Arial" w:eastAsia="Arial" w:hAnsi="Arial"/><w:sz w:val="22"/><w:szCs w:val="22"/></w:rPr></w:pPr>` +
  `<w:r><w:rPr><w:rFonts w:ascii="Poppins" w:cs="Poppins" w:eastAsia="Poppins" w:hAnsi="Poppins"/>` +
  `<w:color w:val="000000"/><w:sz w:val="22"/><w:szCs w:val="22"/><w:rtl w:val="0"/></w:rPr>` +
  `<w:t xml:space="preserve">${esc(text)}</w:t><w:tab/><w:t xml:space="preserve">${page}</w:t></w:r></w:p>`;

const EMPTY = `<w:p><w:pPr><w:spacing w:after="0" w:before="0" w:lineRule="auto"/><w:rPr/></w:pPr></w:p>`;

const BORDER = ["top", "left", "bottom", "right", "insideH", "insideV"]
  .map((s) => `<w:${s} w:val="single" w:sz="4" w:space="0" w:color="BFBFBF"/>`).join("");

const cellP = (text, bold) =>
  `<w:p><w:pPr><w:spacing w:after="60" w:before="60" w:lineRule="auto"/><w:rPr/></w:pPr>${run(text, { b: bold })}</w:p>`;

const TBL = (rows, widths) =>
  `<w:tbl><w:tblPr><w:tblStyle w:val="TableNormal"/><w:tblW w:w="9029" w:type="dxa"/>` +
  `<w:tblBorders>${BORDER}</w:tblBorders><w:tblLayout w:type="fixed"/>` +
  `<w:tblCellMar><w:top w:w="60" w:type="dxa"/><w:left w:w="108" w:type="dxa"/>` +
  `<w:bottom w:w="60" w:type="dxa"/><w:right w:w="108" w:type="dxa"/></w:tblCellMar></w:tblPr>` +
  `<w:tblGrid>${widths.map((w) => `<w:gridCol w:w="${w}"/>`).join("")}</w:tblGrid>` +
  rows.map((r, ri) =>
    `<w:tr>${r.map((c, ci) =>
      `<w:tc><w:tcPr><w:tcW w:w="${widths[ci]}" w:type="dxa"/>` +
      (ri === 0 ? `<w:shd w:fill="F2F2F2" w:val="clear"/>` : "") +
      `</w:tcPr>${cellP(c, ri === 0)}</w:tc>`).join("")}</w:tr>`).join("") +
  `</w:tbl>${EMPTY}`;

/* ================================ TREŚĆ ================================
   Rozdziały 1–3 po angielsku (tak jak istniejące 1.1, 1.2 i 2.1 zespołu),
   rozdziały 4–5 po polsku (tak jak istniejące 4.1 i 4.2).
   ====================================================================== */

const KEY_INFO = TBL([
  ["Field", "Value"],
  ["Title", "Leeway"],
  ["Genre", "Cooperative low-poly survival with creature evolution"],
  ["Platform", "Windows PC (Steam); consoles considered after 1.0"],
  ["Players", "1–4, online co-op, host–client through Steam"],
  ["Session length", "25–40 minutes per run"],
  ["Target audience", "16–30, co-op and friendslop players, Spore nostalgics, content creators"],
  ["Engine", "Unity 6 LTS (6000.4.7f1), URP, FishNet"],
  ["Business model", "Premium; Early Access followed by 1.0"],
  ["Status", "Playable prototype: creature editor, procedural rig, networking layer"],
  ["Team", "3 people"],
], [2700, 6329]);

/* ---------- 1.3 USP ---------- */
const USP = [
  P("Leeway sits on a crossing that none of its reference titles occupy. Games with a creature editor have no co-op; co-op games have no editor. The points below are what a player cannot get anywhere else."),
  P(["Body parts taken from real biology, with real consequences. ", { b: true }],
    "Every module is modelled on an existing, often bizarre animal, and carries an ability drawn from the biology of that animal. A part is never only a skin — it changes how the creature moves, fights and survives. What the creature looks like is a gameplay decision."),
  P(["A body that animates itself. ", { b: true }],
    "The skeleton, IK chains and gait are generated from whatever the player builds. A squat four-legged creature moves nothing like a tall two-legged one, and both were assembled by hand minutes earlier. Silhouette and movement identify a creature instantly, which is also what makes the game readable in a clip."),
  P(["Shared evolution, individual creatures. ", { b: true }],
    "Breed progression belongs to the whole team, while each player shapes their own life form. Co-op has a common goal without forcing everyone into the same build."),
  P(["Spore for an older audience. ", { b: true }],
    "The creative freedom of the creature stage, moved into a darker and more uncertain world — saturated but low-brightness colours, pixel-art-inspired textures, a tone that unsettles as much as it invites."),
  P(["Every run is assembled differently. ", { b: true }],
    "Islands, Blueprints and the traits on offer are randomised, so the same build is never optimal twice and experimenting stays cheaper than planning."),
];

/* ---------- 1.4 Key game pillars ---------- */
const PILLARS = [
  P("Four pillars govern every design decision. When a feature cannot be justified by one of them, it does not enter the game."),
  P(["Authorship. ", { b: true }],
    "The creature belongs to the player. It should be recognisable, unrepeatable, and worth talking about after the run ends. The player says my creature, never my character."),
  P(["Form follows function. ", { b: true }],
    "Appearance and capability are the same decision. Statistics are derived from the assembled body rather than written into a table, so players learn the rules by looking at what they built."),
  P(["Curiosity over instruction. ", { b: true }],
    "The game rewards testing an idea, not following a prescribed path. This follows directly from the game tone described in 4.1 — the intention is to return the player to unguided discovery."),
  P(["A shared story. ", { b: true }],
    "Failure is funny and worth recording. A run is remembered as something that happened to four people together, which is what carries the game outside its own player base."),
];

/* ---------- 2.2 Core mechanics ---------- */
const MECHANICS = [
  P("Five mechanics carry the loop described in 2.1. Each is given as what it is, how it works and why it matters."),
  P(["Blueprint discovery. ", { b: true }],
    "A Blueprint is the description of a single body part modelled on a real creature. Blueprints are hidden across the island — inside points of interest, guarded by stronger creatures, or dropped on defeat. They are the reason to leave the landing site and push into unfamiliar ground, and they are what a player remembers finding."),
  P(["Evolution Points. ", { b: true }],
    "Evolution Points are the currency of change, earned by defeating creatures and scaled to how dangerous the encounter was. They separate finding a part from being able to use it, which keeps combat necessary without making it the only activity, and they control how quickly a creature can be rebuilt inside a single run."),
  P(["Trait installation. ", { b: true }],
    "Spending Evolution Points attaches a discovered Blueprint to the creature. The part is placed on a procedural skeleton, and the rig, IK chains and gait are rebuilt around the new geometry. This is the central expressive act of the game: the moment the player stops consuming content and starts authoring."),
  P(["Physical consequence. ", { b: true }],
    "Mass, centre of gravity, stride length, reach and bite force follow from the assembled body. A heavy head pitches the creature forward; thin legs buckle under load; a long tail steadies a leap but slows a turn. None of this is hidden behind a stat screen, so a bad idea is visible immediately — and usually funny."),
  P(["Island clearing. ", { b: true }],
    "Each island carries an objective that ends the run when completed. It gives a session a defined shape, a natural stopping point, and a reason for a team to regroup rather than scatter indefinitely across the map."),
];

/* ---------- 2.3 Run progression ---------- */
const RUN_PROG = [
  P("A run is one island and lasts 25 to 40 minutes. Players enter with the creature they currently have, and the island escalates around them rather than gating them behind fixed checkpoints."),
  L(["Landing. ", { b: true }], "The team arrives at the edge of the island with whatever they built before departing. The first minutes are quiet and used for orientation."),
  L(["Expansion. ", { b: true }], "Players spread out to locate Blueprints and engage weaker creatures for Evolution Points. Risk is chosen, not imposed."),
  L(["Adaptation. ", { b: true }], "Blueprints found during the run can be installed in the field. A team that finds an aquatic limb early will take a different route through the island than one that did not."),
  L(["Objective. ", { b: true }], "The island's objective becomes reachable once the team is strong enough. It is deliberately readable from early on, so the run has a visible direction."),
  L(["Extraction. ", { b: true }], "Completing the objective banks everything collected. Dying costs the unbanked portion of the run, never the creature design itself."),
  P("The design intent is that a lost run stings without being punishing. Losing a creature the player spent an hour building would make experimentation expensive, and experimentation is the point of the game."),
];

/* ---------- 2.4 Player progression ---------- */
const PLAYER_PROG = [
  P("Progression runs on two tracks that deliberately do not compete with one another."),
  P(["Breed progression, shared by the team. ", { b: true }],
    "Species-level unlocks are common to everyone in the group. They widen what the whole team can attempt and give a reason to keep playing with the same people rather than starting over."),
  P(["Blueprint collection, personal to the player. ", { b: true }],
    "Every player keeps their own library of discovered parts and their own saved creatures. This is where individual identity lives, and it is the material that later feeds sharing between players."),
  P("Both tracks widen the range of what a player can build rather than raising numbers. A veteran has more options than a newcomer, not a stronger creature. This is a requirement rather than a preference: a game that spreads through groups of friends cannot afford a state in which the newest player in the lobby is dead weight."),
];

/* ---------- 3.1 Game flow ---------- */
const FLOW = [
  P("A typical session follows the same shape whether it is the first or the fiftieth, with the weight shifting from learning to optimising."),
  L(["Lobby. ", { b: true }], "The host opens a session; up to three players join by invitation or code."),
  L(["Editor. ", { b: true }], "Each player builds a new creature or picks one from their collection. First-time players spend most of their session here, and that is intended."),
  L(["Descent. ", { b: true }], "The team lands on a generated island. The objective is announced, the surroundings are not."),
  L(["The run. ", { b: true }], "Exploration, combat, Blueprint discovery and field evolution, as described in 2.1 and 2.3."),
  L(["Resolution. ", { b: true }], "The objective is completed and the team extracts, or the run is lost."),
  L(["Return. ", { b: true }], "Findings are banked into the shared breed progression and personal collections. The group either dissolves or departs for the next island."),
  P("A first session is expected to run long, because the editor absorbs attention before players understand what any of the parts do. By the tenth session the editor is used deliberately — players arrive with an idea they want to test, and the island exists to test it against."),
];

/* ---------- 3.2 World & content structure ---------- */
const WORLD = [
  P("The world is organised as discrete islands rather than one continuous map. Each island is assembled procedurally from hand-authored modules, which keeps composition under the control of a designer while leaving layout unpredictable. This is the single most important decision for production cost: it removes the need for a dedicated level designer to hand-place every location, and it makes replayability a property of the structure rather than something added later."),
  P("Islands are grouped into archetypes. An archetype defines the terrain modules, the creature population, the environmental hazards and the kinds of Blueprints that can be found there. Adding an archetype after release is the natural unit of a content update."),
  P(["Content is therefore counted in four units: ", { b: true }],
    "island archetypes, terrain modules within an archetype, creature types, and Blueprints. Section 5.1 gives target figures for Early Access and for version 1.0."),
  P("Between runs, players return to a light hub that holds the editor, the Blueprint collection and the shared breed progression. The hub carries no gameplay of its own — it exists to make the boundary between runs legible, and to keep the creature editor available without forcing a player into an island to reach it."),
];

/* ---------- 4.3 Audio & user interface ---------- */
const AUDIO_UI = [
  P("Warstwa dźwiękowa i interfejs podlegają tym samym założeniom, które opisano w 4.1 i 4.2. Świat ma być tajemniczy i lekko niepokojący, a jednocześnie czytelny, dlatego zarówno dźwięk, jak i interfejs operują oszczędnymi środkami i ustępują pola stworzeniu gracza."),
  P(["Wokalizacje stworzeń generowane proceduralnie. ", { b: true }],
    "Dźwięki wydawane przez stworzenie wynikają z jego budowy — masa ciała, wielkość i kształt pyska oraz długość szyi wpływają na wysokość, barwę i długość dźwięku. Rozwiązanie jest konieczne, a nie ozdobne: przy modułowej konstrukcji nie da się nagrać osobnego zestawu próbek dla każdej możliwej kombinacji części. Efektem ubocznym jest to, że stworzenie gracza brzmi równie indywidualnie, jak wygląda."),
  P(["Warstwa otoczenia oparta na ciszy. ", { b: true }],
    "Wyspy są udźwiękowione oszczędnie — wiatr, woda, odgłosy zwierząt poza polem widzenia. Muzyka pojawia się rzadko i sygnalizuje zmianę stanu: zbliżanie się zagrożenia, odkrycie ważnego miejsca, rozpoczęcie walki. Cisza jest tu narzędziem budowania niepokoju, a jednocześnie zostawia miejsce na to, co w grze kooperacyjnej najważniejsze, czyli rozmowę graczy między sobą."),
  P(["Dźwięk jako informacja. ", { b: true }],
    "Ponieważ statystyki wynikają z budowy ciała i nie są wypisane na ekranie, dużą część informacji zwrotnej przejmuje dźwięk: ciężar kroku, wysiłek przy wspinaczce, moment utraty równowagi, siła uderzenia. Gracz ma rozpoznawać kondycję swojego stworzenia bez patrzenia na wskaźniki."),
  P(["Interfejs poza edytorem — minimalny. ", { b: true }],
    "W trakcie rozgrywki na ekranie zostaje tylko to, czego nie da się przekazać inaczej. Ograniczona liczba wskaźników utrzymuje uwagę na świecie i na stworzeniu, a przy czterech graczach jednocześnie chroni ekran przed zagraceniem."),
  P(["Edytor jako najbardziej wymagający ekran w grze. ", { b: true }],
    "Edytor stworzeń jest jedynym miejscem o rozbudowanym interfejsie i to on decyduje o pierwszym wrażeniu z gry. Jego projekt podlega trzem regułom: skutek każdej zmiany ma być widoczny natychmiast na modelu, koszt części i budżet genomu mają być czytelne bez wchodzenia w podmenu, a cofnięcie decyzji ma być zawsze możliwe. Układ przygotowujemy z myślą o późniejszej obsłudze kontrolerem, ponieważ przeprojektowanie edytora pod pada po fakcie jest najdroższym elementem ewentualnego portu."),
  P(["Kolor jako element interfejsu. ", { b: true }],
    "Paleta opisana w 4.2 obowiązuje również interfejs. Ciemne, niskiej jasności tła i nasycone akcenty pozwalają wyróżnić elementy interaktywne bez wprowadzania obcej stylistyce warstwy graficznej."),
];

/* ---------- 5.1 Game scope ---------- */
const SCOPE = [
  P("Zakres opisano w dwóch punktach: na premierę w Early Access i na wersję 1.0. Podział wynika z modelu finansowania przedstawionego w 5.4 — gra wchodzi do sprzedaży w dwunastym miesiącu produkcji i dalej rozwija się na oczach graczy."),
  TBL([
    ["Element", "Early Access", "Wersja 1.0"],
    ["Długość pojedynczego runu", "25–40 minut", "25–40 minut"],
    ["Liczba graczy", "1–4", "1–4"],
    ["Archetypy wysp", "2", "3"],
    ["Blueprinty (części ciała)", "ok. 60", "ok. 70"],
    ["Typy stworzeń", "ok. 10", "ok. 14"],
    ["Starcia z bossami", "—", "1"],
    ["Języki", "4", "6"],
    ["Dzielenie się stworzeniami", "Steam Workshop", "Steam Workshop"],
    ["API modów", "—", "Pierwsza duża aktualizacja po 1.0"],
  ], [2600, 3200, 3229]),
  P(["Systemy w zakresie finansowania: ", { b: true }],
    "edytor stworzeń z modułową konstrukcją ciała, proceduralny generator rigu wraz z IK i chodem, symulacja fizyczna ciała, system Blueprintów i Evolution Points, walka i sztuczna inteligencja stworzeń, generator wysp z modułów, progresja rasy i kolekcja gracza, warstwa sieciowa dla czterech graczy oraz integracja ze Steam Workshop."),
  P(["Poza zakresem finansowania: ", { b: true }],
    "publiczne API modów, czwarty i kolejne archetypy wysp, serwery dedykowane, wersje konsolowe. Wszystkie te elementy pozostają w planie produktu, ale finansowane są z przychodów po premierze 1.0. Traktujemy to jako świadome ograniczenie zakresu, a nie rezygnację."),
];

/* ---------- 5.2 Development status ---------- */
const STATUS = [
  P("W grze tego typu całe ryzyko techniczne skupia się w jednym miejscu: czy da się zbudować edytor o swobodnej, modułowej konstrukcji, którego wytwory poruszają się sensownie i dają się zsynchronizować w sieci. Ta część jest już zaimplementowana i działa w prototypie."),
  L(["Genom stworzenia. ", { b: true }], "Pełny model danych — kręgi, kończyny, części, orientacje i osadzenia wraz z regułami oraz limitami łączenia."),
  L(["Budżet genomu. ", { b: true }], "System punktowy ograniczający złożoność konstrukcji, a przez to również koszt obliczeniowy symulacji."),
  L(["Walidacja. ", { b: true }], "Walidator odrzucający konstrukcje niemożliwe do symulacji, wraz z typologią błędów."),
  L(["Serializacja. ", { b: true }], "Kodek genomu i stabilne hashowanie — podstawa zapisu, przesyłania przez sieć oraz późniejszego dzielenia się stworzeniami."),
  L(["Generowanie ciała. ", { b: true }], "Proceduralny generator siatki kręgosłupa, budowniczy rigu, dopasowanie zderzaczy i instancjonowanie części."),
  L(["Ruch. ", { b: true }], "Proceduralne nogi z IK, zawieszenie, profile chodu oraz deterministyczny generator losowy."),
  L(["Edytor. ", { b: true }], "Sterowanie, kamera, podgląd ciała, interfejs i operacje edycji genomu."),
  L(["Warstwa sieciowa. ", { b: true }], "Szkielet oparty na FishNet w modelu serwer-autorytatywnym."),
  L(["Higiena projektu. ", { b: true }], "Warstwa domenowa odseparowana od silnika, testy w trybie edytora i w trybie gry, automatycznie generowana dokumentacja architektury."),
  P("Do zrobienia pozostaje pełna pętla runu, walka i sztuczna inteligencja stworzeń, generator wysp, system Blueprintów i Evolution Points, progresja, integracja ze Steam Workshop, warstwa audio, treść i optymalizacja. Jest to praca duża, ale przewidywalna — w odróżnieniu od warstwy, którą właśnie domknęliśmy."),
];

/* ---------- 5.3 Team & production ---------- */
const TEAM = [
  P("Projekt prowadzą trzy osoby, które odpowiadają za obecny prototyp. Członkowie zespołu mają za sobą cztery lata pracy w branży gier oraz udział w jednym wydanym komercyjnie tytule."),
  TBL([
    ["Rola", "Forma", "Zakres odpowiedzialności"],
    ["Mid Unity Developer (założyciel)", "B2B", "Gameplay, warstwa sieciowa, edytor stworzeń, prowadzenie projektu"],
    ["Junior Game Designer", "umowa zlecenie", "Projekt rozgrywki, balans, sound design"],
    ["Junior Artysta 3D", "umowa zlecenie", "Modele, tekstury, styl wizualny"],
  ], [2900, 1800, 4329]),
  P(["Plan rozbudowy zespołu. ", { b: true }],
    "Zespół rośnie wraz z projektem, a nie przed nim. W pierwszych czterech miesiącach pracują wyłącznie obecne trzy osoby. Od piątego miesiąca dołącza starszy programista odpowiedzialny za animację proceduralną, fizykę i netcode — jest to najtrudniejsza do obsadzenia rola w całym projekcie i rekrutację rozpoczynamy natychmiast po podpisaniu umowy. W dalszej kolejności dochodzą tech artist, programista gameplay, QA oraz community manager na pół etatu. Docelowa wielkość zespołu to osiem osób."),
  P(["Harmonogram. ", { b: true }],
    "Osiemnaście miesięcy do wersji 1.0, z premierą w Early Access w dwunastym miesiącu. Harmonogram jest celowo napięty. Gry kooperacyjne, które w ostatnich latach osiągnęły największe wyniki — Lethal Company, Content Warning, PEAK, R.E.P.O., RV There Yet? — powstawały w zespołach liczących od jednej do kilkunastu osób i w cyklach liczonych w miesiącach, nie w latach. W tym gatunku szybkość wejścia na rynek jest przewagą, a nie kompromisem."),
  P(["Dlaczego mimo to potrzebujemy więcej czasu niż tamte tytuły. ", { b: true }],
    "Wymienione gry mają celowo prostą warstwę techniczną: gotowe modele, proste animacje, podstawowa sieć. Leeway wymaga proceduralnego edytora, rigu generowanego z geometrii, IK, chodu wyliczanego z budowy ciała i zsynchronizowania tego wszystkiego w sieci. To jedyny powód różnicy w czasie i budżecie — i jednocześnie jedyny powód, dla którego tej gry nie da się szybko skopiować. Istotne jest też to, że ta praca jest już w znacznej części wykonana, co opisano w 5.2."),
  P(["Zarządzanie ryzykiem zakresu. ", { b: true }],
    "Inspiracja Spore w naturalny sposób ciągnie projekt w stronę kolejnych etapów rozwoju cywilizacji. Zakres jednego etapu traktujemy jako zamrożony. Każdy pomysł wykraczający poza niego trafia na listę po wersji 1.0, a nie do bieżącej produkcji."),
];

/* ---------- 5.4 Publisher ask ---------- */
const ASK = [
  P("Nie prosimy o decyzję o finansowaniu całej produkcji. Proponujemy finansowanie etapowe, w którym każda kolejna transza uruchamiana jest po spełnieniu mierzalnego warunku, a pierwszym z nich jest reakcja rynku, nie nasza deklaracja."),
  TBL([
    ["Transza", "Miesiące", "Cel", "Kwota"],
    ["1 — Walidacja rynkowa", "1–4", "Publiczne demo, strona na Steam, zwiastun, zmierzona lista życzeń", "145 400 PLN"],
    ["2 — Produkcja do Early Access", "5–12", "Premiera w Early Access i wejście do sprzedaży", "949 700 PLN"],
    ["3 — Early Access do 1.0", "13–18", "Sześć miesięcy aktualizacji i domknięcie wersji 1.0", "787 000 PLN"],
    ["Razem", "1–18", "Pełny cykl do wersji 1.0", "1 882 100 PLN"],
  ], [2500, 1100, 3629, 1800]),
  P(["Bramki decyzyjne. ", { b: true }],
    "Transza 2 zostaje uruchomiona, jeżeli po sześciu tygodniach od publikacji strony na Steam lista życzeń przekroczy uzgodniony próg. Transza 3 zostaje uruchomiona po ocenie sprzedaży pierwszego miesiąca Early Access. Progi są przedmiotem negocjacji — istotna jest zasada, że o kontynuacji rozstrzyga liczba, a nie opinia."),
  P(["Ograniczona ekspozycja. ", { b: true }],
    "Sprzedaż rusza w dwunastym miesiącu, czyli na sześć miesięcy przed końcem finansowanego okresu, więc transza 3 jest w istotnej części pokrywana z przychodów. Maksymalne zaangażowanie kapitałowe przed pierwszym przychodem wynosi 1 095 100 PLN, a pierwsza decyzja dotyczy 145 400 PLN. Przy przychodzie netto około 35 PLN na egzemplarz transza 1 zwraca się przy sprzedaży rzędu czterech tysięcy kopii."),
  P(["Czego oczekujemy. ", { b: true }],
    "Finansowania zgodnie z powyższym harmonogramem, wypłacanego w ratach miesięcznych. Prowadzenia marketingu i obecności w kanałach wydawcy — budżet marketingowy nie jest ujęty w powyższych kwotach i zakładamy, że pozostaje po stronie wydawcy. Obsługi relacji z Valve, a w razie decyzji o portach również z producentami konsol. Wsparcia produkcyjnego po wejściu w Early Access."),
  P(["Co dajemy w zamian. ", { b: true }],
    "Raport postępu co miesiąc i grywalną kompilację co dwa miesiące. Gotowość do rozmowy o każdej z trzech struktur rozliczenia: podziału przychodów bez udziałów, spółki celowej z pakietem mniejszościowym oraz spółki celowej z pakietem większościowym. Zależy nam przy tym na zachowaniu praw do technologii edytora również poza tym tytułem, ponieważ stanowi ona podstawę naszej dalszej działalności."),
  P("Szczegółowy rozpis transz wraz z budżetem pozycja po pozycji, harmonogramem kamieni milowych, analizą platform i rejestrem ryzyk znajduje się w osobnym dokumencie towarzyszącym niniejszemu GDD."),
];

/* ---------- 5.5 Production schedule ---------- */
const HARMONOGRAM = [
  P("Osiemnaście miesięcy do wersji 1.0, z premierą w Early Access w dwunastym miesiącu. Harmonogram jest rozliczany kamieniami milowymi, a każdy kamień kończy się grywalną kompilacją albo pomiarem, nie deklaracją postępu. Trzy z nich są jednocześnie bramkami decyzyjnymi dla wydawcy."),
  P(["Faza 1: walidacja rynkowa, miesiące 1-4. ", { b: true }],
    "Pracują wyłącznie trzy obecne osoby. Celem fazy nie jest zbudowanie gry, tylko sprawdzenie, czy gracze jej chcą."),
  TBL([
    ["M-c", "Zakres prac", "Rezultat weryfikowalny"],
    ["1", "Doprecyzowanie designu, struktura danych Blueprintów, pierwszy zestaw modułowych części ciała", "Dokumentacja systemu Blueprintów i 15 części w docelowym stylu"],
    ["2", "Domknięcie ryzyka technicznego: rig proceduralny i sieć", "KM 1 - stworzenie z losowego genomu chodzi po nierównym terenie, przewraca się przy złym wyważeniu i wstaje; czterech graczy w jednej sesji przy opóźnieniu 150 ms"],
    ["3", "Pętla edytor-rozgrywka na jednej wyspie, testy wewnętrzne", "KM 2 - demo zamknięte: 15 minut rozgrywki, 40 Blueprintów, jeden archetyp wyspy; testy z graczami spoza zespołu"],
    ["4", "Materiały sklepowe, publikacja, start kampanii", "KM 3 - strona na Steam, zwiastun, publiczne demo; start pomiaru listy życzeń"],
    ["4 + 6 tyg.", "Ocena reakcji rynku", "BRAMKA G1 - lista życzeń powyżej uzgodnionego progu i mediana czasu w edytorze powyżej 20 minut. Decyzja o transzy 2"],
  ], [900, 3200, 4929]),
  P(["Faza 2: produkcja do Early Access, miesiące 5-12. ", { b: true }],
    "Zespół rośnie z trzech do ośmiu osób. Najważniejsza rekrutacja, czyli starszy programista odpowiedzialny za animację proceduralną i fizykę, musi być domknięta na początek tej fazy."),
  TBL([
    ["M-c", "Zakres prac", "Rezultat weryfikowalny"],
    ["5", "Dołącza starszy programista. Rozbudowa rigu pod docelowy zakres, implementacja systemu Blueprintów", "Blueprint znaleziony w świecie daje się zainstalować na stworzeniu"],
    ["6", "Dołączają tech artist i community manager. Pipeline modeli modułowych, Evolution Points", "Powtarzalny proces dodawania nowej części ciała od modelu do gry"],
    ["7", "Dołącza programista gameplay. Edytor w pełnej wersji", "KM 4 - budowa, zapis, wczytanie i przesłanie stworzenia przez sieć; 60 Blueprintów"],
    ["8", "Generator wysp z modułów, pierwszy archetyp w docelowej jakości", "Wyspa generowana z ziarna, identyczna u wszystkich graczy w sesji"],
    ["9", "Walka, sztuczna inteligencja stworzeń, cel runu", "KM 5 - pełna pętla runu: lądowanie, eksploracja, walka, ewolucja, ewakuacja; drugi archetyp wyspy"],
    ["10", "Dołącza QA. Progresja rasy i kolekcja gracza, Steam Workshop", "KM 6 - dzielenie się stworzeniami działa; kompilacja na Steam Next Fest"],
    ["11", "Optymalizacja, lokalizacja, testy zewnętrzne", "KM 7 - budżet klatki dotrzymany przy czterech graczach i stadzie stworzeń; lokalizacja na 4 języki"],
    ["12", "Domknięcie wydania", "BRAMKA G2 i PREMIERA EARLY ACCESS"],
  ], [900, 3200, 4929]),
  P(["Faza 3: Early Access do wersji 1.0, miesiące 13-18. ", { b: true }],
    "Gra jest w sprzedaży i rozwija się na oczach graczy. Rytm aktualizacji jest ważniejszy od objętości pojedynczej łatki - widoczność na Steam nagradza regularność, a społeczność ocenia zespół po przewidywalności."),
  TBL([
    ["M-c", "Zakres prac", "Rezultat weryfikowalny"],
    ["13", "Stabilizacja: poprawki co tydzień, reakcja na zgłoszenia", "BRAMKA G3 - ocena sprzedaży pierwszego miesiąca. Decyzja o pełnym lub skróconym zakresie transzy 3"],
    ["14", "Balans, korekta krzywej wejścia dla nowych graczy", "Mierzalny spadek odsetka graczy rezygnujących w pierwszym runie"],
    ["15-16", "Aktualizacja 1", "Trzeci archetyp wyspy, rozbudowa katalogu Blueprintów do 70, starcie z bossem"],
    ["17-18", "Aktualizacja 2 i domknięcie", "Kolejne typy stworzeń, narzędzia społecznościowe, lokalizacja na 6 języków, pełny przegląd jakości"],
    ["18", "Wydanie", "PREMIERA WERSJI 1.0"],
  ], [900, 3200, 4929]),
  P(["Raportowanie. ", { b: true }],
    "Raport postępu co miesiąc, grywalna kompilacja co dwa miesiące. Każdy kamień milowy jest naturalnym punktem kontrolnym i może zostać zweryfikowany samodzielnie przez wydawcę."),
  P(["Co świadomie zostaje poza harmonogramem. ", { b: true }],
    "Czwarty i kolejne archetypy wysp, publiczne API modów, serwery dedykowane oraz wersje konsolowe. Wszystkie pozostają w planie produktu, ale finansowane są z przychodów po premierze 1.0. Nienaruszone pozostają edytor, symulacja fizyczna, kooperacja dla czterech graczy i Steam Workshop - to one decydują o tym, czy gra zadziała, a reszta jest objętością, którą można dołożyć później."),
];

/* ---------- 5.6 Financial plan ---------- */
const FINANSE = [
  P("Budżet policzono od dołu, z obsady i kosztów rzeczywistych, a nie z góry przyjętej kwoty. Wszystkie wartości podano w złotych, w cenach bieżących, bez indeksacji o inflację."),
  P(["Formy zatrudnienia. ", { b: true }],
    "Założyciel rozlicza się w modelu B2B - koszt równa się kwocie faktury, bez narzutu. Pozostałe osoby pracują na umowie zlecenia z pełnym ubezpieczeniem społecznym, gdzie koszt to wynagrodzenie brutto powiększone o 20,48 procent, na co składają się składka emerytalna, rentowa, wypadkowa, Fundusz Pracy i FGŚP."),
  P(["Czego w budżecie nie ma. ", { b: true }],
    "Outsourcingu grafiki trójwymiarowej, obsługi księgowej i administracyjnej, opłaty Steam Direct oraz jakichkolwiek kosztów marketingu i produkcji materiałów promocyjnych. Księgowość prowadzona jest we własnym zakresie, a marketing w całości po stronie wydawcy. Nie planujemy również płatnych licencji na oprogramowanie graficzne ani na środowisko programistyczne."),
  P(["Licencja silnika. ", { b: true }],
    "W transzy 1 wystarcza bezpłatny Unity Personal. Od transzy 2 łączne finansowanie przekracza próg 200 tysięcy dolarów w dwunastu miesiącach, co zgodnie z warunkami licencyjnymi Unity wymusza przejście na Unity Pro. Koszt ujęto w kosztach pozostałych obu późniejszych transz."),
  P(["Budżet w podziale na transze. ", { b: true }],
    "Każda transza jest uruchamiana dopiero po spełnieniu warunku opisanego w 5.5."),
  TBL([
    ["Transza", "M-ce", "Zespół", "Osobowe", "Pozostałe", "Rezerwa", "Razem"],
    ...TR.map((t) => [t.id.replace("Transza ", ""), t.od + "-" + t.do, String(t.peak),
      fmt(t.personnel), fmt(t.otherSum), fmt(t.reserve), fmt(t.total)]),
    ["Razem", "1-" + V10_MONTH, "", fmt(TR.reduce((a, b) => a + b.personnel, 0)),
      fmt(TR.reduce((a, b) => a + b.otherSum, 0)), fmt(TR.reduce((a, b) => a + b.reserve, 0)), fmt(TOTAL)],
  ], [800, 750, 800, 1620, 1620, 1620, 1819]),
  P(["Rezerwa na ryzyko. ", { b: true }],
    "Dziesięć procent w transzy 1 i dwanaście procent w transzach 2 i 3. Pozycja przeznaczona na poślizgi rekrutacyjne, przedłużenie kamieni milowych i nieprzewidziane koszty licencji. Uruchamiana za zgodą wydawcy, na wniosek z uzasadnieniem."),
  P(["Ekspozycja kapitałowa. ", { b: true }],
    "Sprzedaż rusza w dwunastym miesiącu, czyli na sześć miesięcy przed końcem finansowanego okresu, więc transza 3 jest w istotnej części pokrywana z przychodów Early Access. Maksymalne zaangażowanie gotówkowe przed pierwszym przychodem wynosi " + fmt(EXPOSURE) + ", a pierwsza decyzja finansowa dotyczy " + fmt(TR[0].total) + "."),
  P(["Model sprzedaży. ", { b: true }],
    "Premium, 69 złotych w Early Access i 89 złotych po premierze wersji 1.0. Po uwzględnieniu obniżek sezonowych i regionalnych różnic cenowych efektywna cena sprzedaży wynosi około 58 złotych brutto. Po odliczeniu podatku od towarów i usług, średnio 19 procent, oraz prowizji platformy Steam w wysokości 30 procent do projektu trafia około " + NET_PER_UNIT + " złotych na egzemplarz."),
  P(["Scenariusze sprzedaży. ", { b: true }],
    "Horyzont 24 miesięcy od premiery w Early Access. Kwoty przed podziałem z wydawcą i przed zwrotem nakładów."),
  TBL([
    ["Scenariusz", "Sprzedaż", "Przychód netto", "Opis"],
    ...SCEN.map(([n, u, d]) => [n, num(u) + " egz.", fmt(u * NET_PER_UNIT), d]),
  ], [1500, 1400, 1600, 4529]),
  P(["Próg rentowności. ", { b: true }],
    "Transza 1 zwraca się przy sprzedaży około " + num(breakEven(TR[0].total)) +
    " egzemplarzy, czyli poniżej scenariusza pesymistycznego. Transze 1 i 2 łącznie przy około " +
    num(breakEven(EXPOSURE)) + " egzemplarzy. Całość finansowania przy około " +
    num(breakEven(TOTAL)) + " egzemplarzy, co mieści się między scenariuszem ostrożnym a bazowym."),
  P("Najważniejsza liczba w tej sekcji dotyczy transzy 1. Decyzja o jej uruchomieniu nie wymaga założenia, że Leeway okaże się przebojem - wymaga jedynie założenia, że gra w ogóle znajdzie odbiorców."),
  P(["Po premierze wersji 1.0. ", { b: true }],
    "Rekomendujemy zabezpieczenie środków na co najmniej trzy miesiące wsparcia po premierze, szacowanych na 150 do 200 tysięcy złotych miesięcznie przy pełnej obsadzie, a następnie 70 do 90 tysięcy przy zespole utrzymaniowym. Brak takiego zabezpieczenia jest jedną z najczęstszych przyczyn utraty wartości gry tuż po premierze, ponieważ zespół zostaje rozwiązany dokładnie wtedy, gdy zgłoszeń jest najwięcej."),
  P(["Czego nie planujemy. ", { b: true }],
    "Mikropłatności wpływających na rozgrywkę ani przepustek sezonowych. W grze kooperacyjnej dla znajomych każda taka forma monetyzacji kosztuje więcej w zaufaniu, niż przynosi w przychodzie. Dodatkowe źródła przychodu ograniczamy do kosmetycznych zestawów części ciała, nie wcześniej niż trzy miesiące po premierze 1.0."),
];

/* ========================= PODMIANA W DOKUMENCIE ========================= */

const ZADANIA = [
  ["3-5 konkretnych elementów",                    USP,          "1.3 USP"],
  ["3-4 najważniejsze filary projektu",            PILLARS,      "1.4 Key game pillars"],
  ["3-5 mechanik, które tworzą pętlę",             MECHANICS,    "2.2 Core mechanics"],
  ["na czym polega progresja rozgrywki",           RUN_PROG,     "2.3 Run progression"],
  ["na czym polega progresja gracza",              PLAYER_PROG,  "2.4 Player progression"],
  ["jak wygląda typowa rozgrywka",                 FLOW,         "3.1 Game flow"],
  ["w jaki sposób zorganizowany jest świat",       WORLD,        "3.2 World & content structure"],
  ["najważniejsze założenia dotyczące UX/UI",      AUDIO_UI,     "4.3 Audio & user interface"],
  ["jak duża ma być gra",                          SCOPE,        "5.1 Game scope"],
  ["co już istnieje, na jakim etapie jest projekt", STATUS,      "5.2 Development status"],
  ["kto robi grę +",                               TEAM,         "5.3 Team & production"],
  ["czego oczekujemy od wydawcy",                  ASK,          "5.4 Publisher ask"],
];

const zip = await JSZip.loadAsync(readFileSync(IN));
let xml = await zip.file("word/document.xml").async("string");
const przed = xml.length;

// akapit zawierający dany fragment tekstu
const zakresAkapitu = (src, frag) => {
  const i = src.indexOf(frag);
  if (i < 0) return null;
  const a = src.lastIndexOf("<w:p ", i);
  const b = src.indexOf("</w:p>", i);
  if (a < 0 || b < 0) return null;
  return [a, b + 6];
};

let ok = 0;
for (const [frag, tresc, nazwa] of ZADANIA) {
  const z = zakresAkapitu(xml, frag);
  if (!z) { console.error("  POMINIĘTO (brak placeholdera):", nazwa); continue; }
  xml = xml.slice(0, z[0]) + tresc.join("") + xml.slice(z[1]);
  console.log("  uzupełniono:", nazwa);
  ok++;
}

// tabela kluczowych informacji przed istniejącym akapitem w 1.1
const z11 = zakresAkapitu(xml, " is a cooperative low-poly survival game");
if (z11) { xml = xml.slice(0, z11[0]) + KEY_INFO + xml.slice(z11[0]); console.log("  uzupełniono: 1.1 Title key informations (tabela)"); ok++; }
else console.error("  POMINIĘTO: 1.1 Title key informations");

// literówka w nagłówku i w spisie treści
const literowki = xml.split("TItle key informations").length - 1;
xml = xml.replaceAll("TItle key informations", "Title key informations");

// --- nowe sekcje 5.5 i 5.6 na końcu treści ---
const sectPr = xml.indexOf("<w:sectPr");
if (sectPr < 0) { console.error("nie znaleziono sectPr"); process.exit(1); }
const dodatek =
  H5("5.5 Production schedule") + HARMONOGRAM.join("") +
  H5("5.6 Financial plan") + FINANSE.join("");
xml = xml.slice(0, sectPr) + dodatek + xml.slice(sectPr);
console.log("  dodano: 5.5 Production schedule");
console.log("  dodano: 5.6 Financial plan");
ok += 2;

// --- wpisy w spisie treści, zaraz za 5.4 ---
const zToc = zakresAkapitu(xml, "5.4 Publisher ask");
if (zToc) {
  xml = xml.slice(0, zToc[1]) + TOC("5.5 Production schedule", 6) + TOC("5.6 Financial plan", 6) + xml.slice(zToc[1]);
  console.log("  dodano: wpisy w spisie treści dla 5.5 i 5.6");
} else console.error("  POMINIĘTO: wpisy w spisie treści");

// --- myślniki: pauza i półpauza na zwykły dywiz, wyłącznie w tekście ---
let myslniki = 0;
xml = xml.replace(/<w:t([^>]*)>([\s\S]*?)<\/w:t>/g, (m, attr, tekst) => {
  const zmieniony = tekst.replace(/[\u2014\u2013]/g, "-");
  if (zmieniony !== tekst) myslniki += (tekst.match(/[\u2014\u2013]/g) || []).length;
  return "<w:t" + attr + ">" + zmieniony + "</w:t>";
});
console.log("  zamieniono myślników na dywiz:", myslniki);

zip.file("word/document.xml", xml);
const buf = await zip.generateAsync({ type: "nodebuffer", compression: "DEFLATE" });
writeFileSync(OUT, buf);

console.log("\nuzupełnionych sekcji:", ok, "z", ZADANIA.length + 1);
console.log("poprawionych literówek „TItle”:", literowki);
console.log("document.xml:", przed, "->", xml.length, "znaków");
console.log("zapisano:", OUT, (buf.length / 1024 / 1024).toFixed(2), "MB");
