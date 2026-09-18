import {
  Document, Packer, Paragraph, TextRun, AlignmentType,
  PageBreak, Footer, PageNumber, convertInchesToTwip
} from "docx";
import { writeFileSync } from "fs";
import { PLAN_A, PLAN_B, A, B, NET_PER_UNIT, SCEN, fmt, fmtM, breakEven , num } from "./lib-budget.mjs";
import { TR, TOTAL, EXPOSURE, EA_MONTH, V10_MONTH } from "./lib-tranches.mjs";
import {
  NAVY, ACCENT, GREY, LIGHT, HEADBG,
  H1, H2, H3, P, rich, BUL, BULB, NUM, tbl, SPACER, CAPTION,
} from "./lib-docx.mjs";

/* ==================== MODEL TRANSZOWY (pod FreeMind) ==================== */

const body = [];
const add = (...x) => body.push(...x.flat());

/* ---------- STRONA TYTUŁOWA ---------- */
add(
  SPACER(1500),
  new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 80 },
    children: [new TextRun({ text: "LEEWAY", bold: true, size: 88, color: NAVY })] }),
  new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 420 },
    children: [new TextRun({ text: "Kooperacyjna gra o ewolucji stworków opartej na fizyce", size: 28, color: GREY })] }),
  new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 120 },
    children: [new TextRun({ text: "Propozycja współpracy wydawniczej", bold: true, size: 30 })] }),
  new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 900 },
    children: [new TextRun({ text: "Materiał przygotowany dla FreeMind S.A.", size: 24, color: GREY })] }),
  SPACER(200),
  new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 200 },
    children: [new TextRun({ text: `Pierwsza decyzja dotyczy ${fmt(TR[0].total)}, nie całego budżetu produkcji.`, bold: true, size: 26, color: ACCENT })] }),
  new Paragraph({ children: [new PageBreak()] }),
);

/* ---------- 1. KARTA PROJEKTU ---------- */
add(
  H1("1. Karta projektu"),
  P("Układ tej strony odpowiada wytycznym zgłoszeniowym FreeMind: jednozdaniowy opis, platformy docelowe i oczekiwane okno wydawnicze."),
  tbl(
    ["Pozycja", "Wartość"],
    [
      ["Opis jednym zdaniem", "Zaprojektuj własne stworzenie, zobacz jak radzi sobie z fizyką i przetrwaj z nim we czwórkę."],
      ["Gatunek", "Kooperacyjne przetrwanie z kreatorem stworków opartym na symulacji fizycznej"],
      ["Liczba graczy", "1–4, rozgrywka host-klient przez Steam"],
      ["Platforma startowa", "PC / Steam"],
      ["Platformy kolejne", "Xbox Series i PlayStation 5 po walidacji sprzedaży — analiza w rozdziale 10"],
      ["Oczekiwane okno wydawnicze", `Early Access: ${EA_MONTH}. miesiąc od startu. Wersja 1.0: ${V10_MONTH}. miesiąc`],
      ["Model sprzedaży", "Premium, 69 PLN w Early Access, 89 PLN w wersji 1.0"],
      ["Silnik", "Unity 6 LTS, URP, FishNet"],
      ["Status", "Działający prototyp: kreator, genom, rig proceduralny, chód, sieć"],
      ["Grywalna kompilacja", "Dostępna do przekazania w ciągu dwóch tygodni"],
      ["Materiał wideo", "Nagranie rozgrywki z prototypu — do przygotowania przed spotkaniem"],
      ["Zespół", "3 osoby: programista Unity na poziomie średnim (założyciel, B2B) oraz junior designer i junior artysta 3D na umowie zlecenia. Cztery lata pracy w branży, jeden wydany tytuł komercyjny"],
      ["Wnioskowane finansowanie", `${fmt(TR[0].total)} w pierwszej transzy, łącznie ${fmtM(TOTAL)} w trzech transzach z bramkami decyzyjnymi`],
    ],
    [26, 74]
  ),
  new Paragraph({ children: [new PageBreak()] }),
);

/* ---------- 2. STRESZCZENIE PROPOZYCJI ---------- */
add(
  H1("2. Streszczenie propozycji"),
  P("Nie prosimy o decyzję o finansowaniu całej produkcji. Proponujemy finansowanie etapowe, w którym każda kolejna transza jest uruchamiana dopiero po spełnieniu mierzalnego warunku — a pierwszy z nich to reakcja rynku, nie nasza deklaracja."),
  H2("2.1 Trzy transze"),
  tbl(
    ["Transza", "Okres", "Cel", "Kwota"],
    [
      ...TR.map(t => [`${t.id} — ${t.nazwa}`, `m-ce ${t.od}–${t.do}`, t.cel, fmt(t.total)]),
      { cells: ["RAZEM", `${TR[0].od}–${TR[2].do}`, "Pełny cykl do wersji 1.0", fmt(TOTAL)], bold: true, bg: LIGHT },
    ],
    [22, 11, 45, 22], { numFrom: 3, numTo: 3 }
  ),
  H2("2.2 Dlaczego ta struktura obniża Wasze ryzyko"),
  BULB("Pierwsza decyzja jest mała. ", `Transza 1 to ${fmt(TR[0].total)} przez cztery miesiące. Na tym etapie nie rozbudowujemy zespołu — pracują trzy osoby, które zbudowały obecny prototyp.`),
  BULB("Bramka oparta na danych rynkowych. ", "Transza 2 jest uruchamiana dopiero wtedy, gdy strona na Steam i publiczne demo pokażą realne zainteresowanie mierzone listą życzeń. Nie jest to ocena naszej prezentacji, tylko zachowania graczy."),
  BULB("Ekspozycja niższa od sumy budżetu. ", `Sprzedaż rusza w ${EA_MONTH}. miesiącu, czyli na ${V10_MONTH - EA_MONTH} miesięcy przed końcem finansowanego okresu. Transza 3 jest w znacznej części pokrywana z przychodów Early Access, dlatego maksymalne zaangażowanie kapitałowe wynosi ${fmtM(EXPOSURE)}, a nie ${fmtM(TOTAL)}.`),
  BULB("Możliwość zatrzymania bez straty całości. ", "Po bramce 1 można zakończyć współpracę, zachowując prawa uzgodnione w umowie i gotową stronę na Steam wraz z zebraną listą życzeń — czyli aktywo, nie odpisany koszt."),
  BULB("Kickstarter jako opcja. ", "Zebrana w transzy 1 lista życzeń jest jednocześnie bazą pod ewentualną zbiórkę, którą FreeMind wymienia wśród swoich form wsparcia. Może ona pokryć część transzy 2."),
  H2("2.3 Co dostajecie po każdej transzy"),
  tbl(
    ["Po transzy", "Materialny rezultat"],
    [
      ["1", "Publiczne demo na Steam, strona sklepowa, zwiastun, zmierzona lista życzeń, pełne dane o zainteresowaniu graczy"],
      ["2", "Gra w sprzedaży w Early Access, generująca przychód od 17. miesiąca"],
      ["3", "Wersja 1.0 z pełnym zakresem, biblioteką treści od graczy i gotowością do portów konsolowych"],
    ],
    [12, 88]
  ),
  new Paragraph({ children: [new PageBreak()] }),
);

/* ---------- 3. DOPASOWANIE DO FREEMIND ---------- */
add(
  H1("3. Dlaczego Leeway i FreeMind"),
  P("Przygotowując tę propozycję, oparliśmy się na tym, jak FreeMind sam opisuje swoją strategię i profil."),
  tbl(
    ["Deklaracja FreeMind", "Jak odpowiada na nią Leeway"],
    [
      ["Wspieranie pomysłów, które inni wydawcy odrzucają", "Kreator stworków z pełną symulacją fizyczną jest projektem, który duzi wydawcy odrzucą jako zbyt ryzykowny technicznie. My mamy już działający prototyp tej najtrudniejszej części."],
      ["Nietypowe symulatory i mechaniki dające satysfakcję z samego obcowania z nimi", "Rdzeniem Leeway jest właśnie taka mechanika: gracz buduje stworzenie, patrzy jak ono chodzi lub się przewraca, i poprawia je. To pętla tego samego typu co w Waszym portfolio, tylko zastosowana do biologii zamiast do maszyn i budynków."],
      ["Gry o tematyce hobbystycznej, edukacyjnej i czasu wolnego", "Projektowanie stworzenia jest z natury zabawą w biomechanikę — gracz uczy się zależności między budową ciała a ruchem, bo gra go do tego zmusza, a nie dlatego, że tak zadeklarowaliśmy."],
      ["Gry o niskim i średnim budżecie na PC", `Transza 1 w kwocie ${fmt(TR[0].total)} mieści się w tej skali. Każda kolejna jest uruchamiana dopiero po potwierdzeniu popytu.`],
      ["Rev-share, finansowanie etapowe, wsparcie przy zbiórkach", "Nasza propozycja jest zbudowana dokładnie wokół finansowania etapowego. Warianty rozliczenia przedstawiamy w rozdziale 8, bez narzucania jednego z góry."],
      ["Wydawanie wieloplatformowe", "Rozdział 10 zawiera rzetelną analizę wykonalności per platforma. Nie obiecujemy premiery na sprzęcie, który nie udźwignie symulacji — ale wskazujemy, gdzie port jest realny i kiedy."],
      ["Przynależność do grupy PlayWay", "Gra kooperacyjna z silnym elementem tworzenia dobrze nadaje się do promocji krzyżowej w katalogu grupy, a nasz model walidacji listą życzeń jest zgodny z praktyką grupy."],
    ],
    [30, 70]
  ),
  H2("3.1 Czego nie ukrywamy"),
  P("Leeway nie jest symulatorem w rozumieniu Waszego dotychczasowego katalogu i nie jest grą o niskim budżecie w pełnym cyklu. Jest projektem droższym i trudniejszym technicznie niż typowy tytuł z Waszego portfolio — i dlatego proponujemy strukturę, w której o tym wyższym budżecie decydujecie dopiero po zobaczeniu, jak rynek reaguje na demo. Uważamy, że to uczciwsze niż przedstawienie projektu jako czegoś, czym nie jest."),
  H2("3.2 Punkty odniesienia w gatunku"),
  P("Gry kooperacyjne, które w ostatnich latach osiągnęły największe wyniki, powstawały w małych zespołach i krótkich cyklach. Traktujemy to jako wskazówkę projektową, a nie ciekawostkę — dlatego harmonogram Leeway skrócono do osiemnastu miesięcy, z premierą w Early Access po dwunastu."),
  tbl(
    ["Tytuł", "Zespół", "Czas produkcji", "Wynik"],
    [
      ["Lethal Company", "1 osoba", "—", "ok. 642 tys. egzemplarzy w pierwszym miesiącu"],
      ["Content Warning", "5 osób", "ok. miesiąc (wewnętrzny game jam)", "4,5 mln pobrań, 140 tys. graczy jednocześnie"],
      ["PEAK", "7 osób", "miesięczny game jam i rozbudowa", "10 mln egzemplarzy w dwa miesiące"],
      ["R.E.P.O.", "ok. 6–12 osób", "—", "230 tys. graczy jednocześnie w pierwszy weekend"],
      ["RV There Yet?", "mały zespół", "3 miesiące", "2,5 mln egzemplarzy w dwa tygodnie"],
      ["Among Us", "3 osoby", "—", "—"],
      ["Phasmophobia", "1 osoba na premierę", "—", "dziś zespół ponad 30 osób"],
    ],
    [20, 15, 27, 38]
  ),
  H3("Dlaczego Leeway jest droższy i dłuższy od tych tytułów"),
  BULB("Technologia jest tu kosztem, nie oszczędnością. ", "Wymienione gry mają celowo prostą warstwę techniczną: gotowe modele, proste animacje, podstawowa sieć. Leeway wymaga proceduralnego kreatora, rigu generowanego z geometrii, IK, chodu wyliczanego z budowy ciała i synchronizacji tego wszystkiego w sieci. To jest jedyny powód różnicy w budżecie i jednocześnie jedyny powód, dla którego tej gry nie da się szybko skopiować."),
  BULB("Ta praca jest już w znacznej części wykonana. ", "Kreator, genom, rig, IK i profile chodu działają w prototypie. Transza 1 nie finansuje budowy technologii, tylko sprawdzenie popytu — dlatego mieści się w skali i czasie porównywalnym z powyższymi tytułami."),
  BULB("Powyższe studia miały bufor na porażkę. ", "Landfall i Aggro Crab finansowały swoje game jamy z przychodów wcześniejszych gier, Nuggets Entertainment tworzą weterani Coffee Stain. Model miesięcznego jamu działa, gdy nieudany projekt nic nie kosztuje. My tego bufora nie mamy — i dlatego proponujemy strukturę transzową, w której to wydawca kontroluje moment zwiększenia zaangażowania."),
  new Paragraph({ children: [new PageBreak()] }),
);

/* ---------- 4. GRA ---------- */
add(
  H1("4. Gra"),
  H2("4.1 Rdzeń"),
  P("Leeway jest grą o konsekwencjach projektowania. Gracz nie wybiera postaci z listy — buduje ją, a fizyka weryfikuje jego pomysł. Ciężka głowa przechyla stworzenie do przodu. Zbyt cienkie nogi łamią się pod obciążeniem. Długi ogon stabilizuje skok, ale spowalnia obroty w ciasnych przejściach. Żaden z tych efektów nie jest wpisany w tabelę statystyk — wszystkie wynikają z symulacji, dlatego gracze uczą się ich intuicyjnie i natychmiast chcą sprawdzić kolejny pomysł."),
  H2("4.2 Pętla rozgrywki"),
  H3("Sesja: 20–40 minut"),
  NUM("Gospodarz zakłada sesję, dołącza do niej od jednego do trzech znajomych."),
  NUM("Gracze wchodzą do kreatora i tworzą lub wybierają stworzenie z własnej kolekcji."),
  NUM("Drużyna ląduje na proceduralnie wygenerowanej mapie biomu z jasno określonym celem."),
  NUM("Gracze eksplorują, zdobywają pożywienie, unikają lub zwalczają miejscowe stworzenia."),
  NUM("Drużyna realizuje cel i ewakuuje się albo ginie; zdobyty materiał genetyczny trafia do kolekcji."),
  H3("Między sesjami"),
  P("Materiał genetyczny odblokowuje nowe części ciała i warianty w kreatorze. Progresja dotyczy katalogu możliwości, a nie mocy liczbowej — nowy gracz nigdy nie jest bezużyteczny w drużynie weteranów. Jest to warunek konieczny dla gry, która rozprzestrzenia się w grupach znajomych."),
  H3("Długoterminowo"),
  P("Gracze publikują stworzenia w Steam Workshop, pobierają cudze, remiksują je i nagrywają klipy. To najtańsze źródło treści w całym projekcie i najskuteczniejszy kanał marketingowy po premierze."),
  H2("4.3 Kreator stworków"),
  BULB("Kręgosłup: ", "gracz rozciąga, wygina i skaluje segmenty; siatka ciała powstaje proceduralnie wokół niego."),
  BULB("Części ciała: ", "kończyny, głowy, oczy, pyski, skrzydła, ogony i ozdoby doklejane w dowolnym miejscu, z symetrią, skalowaniem i rotacją."),
  BULB("Rig proceduralny: ", "szkielet, ograniczenia stawów, łańcuchy IK i profil chodu powstają automatycznie z geometrii zbudowanej przez gracza."),
  BULB("Budżet genomu: ", "każda część kosztuje punkty z puli — ogranicza to nadużycia w rozgrywce i koszt obliczeniowy symulacji."),
  BULB("Statystyki wyprowadzane, nie wpisywane: ", "masa, środek ciężkości, zasięg kroku, prędkość i siła wynikają z budowy ciała."),
  BULB("Walidacja: ", "walidator odrzuca konstrukcje niemożliwe do symulacji, zanim trafią do gry lub do Workshopu."),
  H2("4.4 Fizyka i animacja proceduralna"),
  P("Ruch stworzenia nie jest odtwarzany z gotowych animacji. Generator kroku tworzy chód na podstawie liczby i długości kończyn, ciało reaguje na nierówności terenu, kolizje i utratę równowagi. Źle wyważone stworzenie realnie się przewraca — i to jest rdzeń humoru w tej grze, a zarazem powód, dla którego materiał z rozgrywki nadaje się na klipy."),
  H2("4.5 Kooperacja"),
  P("Rozgrywka toczy się w modelu host-klient: jeden z graczy hostuje sesję przez usługi Steam, pozostali dołączają zaproszeniem lub kodem. Nie planujemy serwerów dedykowanych przed wersją 1.0 — przy sesji do czterech graczy nie wnoszą wartości proporcjonalnej do stałego kosztu utrzymania. Serwer pozostaje źródłem prawdy dla stanu gry, natomiast właściciel stworzenia ma autorytet nad jego symulacją fizyczną, a serwer waliduje wynik. Przez sieć przesyłany jest genom, a nie geometria — kilkaset bajtów zamiast megabajtów siatki."),
  H2("4.6 Świat"),
  P("Każda sesja toczy się na wygenerowanej mapie biomu, budowanej z ręcznie zaprojektowanych modułów łączonych proceduralnie. Rozwiązanie radykalnie obniża koszt produkcji treści i zapotrzebowanie na projektantów poziomów, jednocześnie zwiększając regrywalność. Na premierę Early Access przewidujemy dwa biomy, na wersję 1.0 trzeci. Kolejne są naturalnym materiałem na aktualizacje po premierze, finansowane z przychodów."),
  H2("4.7 Treści tworzone przez graczy"),
  BULB("Etap 1 — dzielenie się stworkami: ", "publikowanie i pobieranie genomów przez Steam Workshop, ocenianie i remiksowanie. Genom jest już serializowalny, więc koszt wdrożenia jest umiarkowany. W zakresie transzy 2."),
  BULB("Etap 2 — mody: ", "udokumentowane API pozwalające dodawać części ciała, przeciwniki i moduły map. Poza zakresem finansowania — planowane jako pierwsza duża aktualizacja po wersji 1.0, opłacana z przychodów."),
  new Paragraph({ children: [new PageBreak()] }),
);

/* ---------- 5. STAN PROJEKTU ---------- */
add(
  H1("5. Stan projektu"),
  P("W grze tego typu całe ryzyko techniczne skupia się w jednym miejscu: czy da się zbudować kreator o pełnej swobodzie, którego wytwory poruszają się sensownie i dają się zsynchronizować w sieci. Ta część jest już zaimplementowana i działa."),
  H2("5.1 Co jest gotowe"),
  tbl(
    ["Obszar", "Stan"],
    [
      ["Genom stworka", "Pełny model danych: kręgi, kończyny, części, orientacje i osadzenia, reguły i limity łączenia"],
      ["Budżet genomu", "System punktowy ograniczający złożoność konstrukcji i koszt symulacji"],
      ["Walidacja", "Walidator odrzucający niepoprawne konstrukcje wraz z typologią błędów"],
      ["Serializacja", "Kodek genomu i stabilne hashowanie — fundament pod zapis, sieć i Steam Workshop"],
      ["Generowanie ciała", "Proceduralny generator siatki kręgosłupa, budowniczy rigu, dopasowanie zderzaczy, instancjonowanie części"],
      ["Ruch", "Proceduralne nogi z IK, zawieszenie, profile chodu, deterministyczny generator losowy"],
      ["Kreator", "Sterowanie, kamera, podgląd ciała, interfejs, operacje edycji genomu"],
      ["Sieć", "Szkielet oparty na FishNet z modelem serwer-autorytatywnym"],
      ["Higiena projektu", "Warstwa domenowa odseparowana od silnika, testy w trybie edytora i gry, automatycznie generowana dokumentacja architektury"],
    ],
    [24, 76]
  ),
  H2("5.2 Co pozostaje do zrobienia"),
  P("Pełna pętla rozgrywki, przeciwnicy i walka, generator map biomów, progresja, integracja ze Steam Workshop, warstwa audio, treść i optymalizacja. To praca duża, ale przewidywalna — w przeciwieństwie do warstwy, którą właśnie domknęliśmy."),
  H2("5.3 Dlaczego to istotne dla decyzji"),
  P("Typowe zgłoszenie na tym etapie to prezentacja i obietnica. Tutaj najtrudniejszy technicznie element można uruchomić i obejrzeć. Transza 1 nie finansuje sprawdzenia, czy technologia zadziała — ona finansuje sprawdzenie, czy gracze tego chcą.", { b: true }),
  new Paragraph({ children: [new PageBreak()] }),
);

/* ---------- 6. MODEL WSPÓŁPRACY ---------- */
add(
  H1("6. Model współpracy"),
  H2("6.1 Bramki decyzyjne"),
  P("Każda transza kończy się bramką: mierzalnym warunkiem, od którego zależy uruchomienie kolejnej. Warunki są zaproponowane do negocjacji — istotna jest zasada, że o kontynuacji rozstrzyga liczba, a nie opinia."),
  tbl(
    ["Bramka", "Kiedy", "Warunek uruchomienia kolejnej transzy", "Jeśli warunek nie zostanie spełniony"],
    [
      ["G1", `koniec ${TR[0].do}. miesiąca`, "Publiczne demo i strona na Steam działają od minimum sześciu tygodni, a lista życzeń przekracza uzgodniony próg (proponujemy 8 000). Dodatkowo: mediana czasu spędzonego w kreatorze powyżej 20 minut.", "Współpraca kończy się albo zostaje przedłużona o krótki etap korygujący. Materiały i zebrana lista życzeń pozostają aktywem."],
      ["G2", `koniec ${TR[1].do}. miesiąca`, "Kompilacja przechodzi testy jakości, gra jest gotowa do wystawienia w Early Access, materiały sklepowe i lokalizacja są ukończone.", "Przesunięcie premiery o jeden lub dwa miesiące z rezerwy, albo ograniczenie zakresu premiery."],
      ["G3", `1 mies. po premierze EA`, "Sprzedaż w pierwszym miesiącu na poziomie scenariusza ostrożnego lub wyższym.", "Transza 3 zostaje skrócona do wariantu utrzymaniowego — mniejszy zespół, ograniczony zakres aktualizacji."],
    ],
    [8, 16, 42, 34]
  ),
  H2("6.2 Ekspozycja kapitałowa"),
  P(`Sprzedaż rusza w ${EA_MONTH}. miesiącu. Przychody z Early Access pokrywają istotną część transzy 3, dlatego maksymalne zaangażowanie gotówkowe nie jest sumą wszystkich transz.`),
  tbl(
    ["Pozycja", "Kwota"],
    [
      ["Transza 1 — przed jakimkolwiek sygnałem rynkowym", fmt(TR[0].total)],
      ["Transza 1 + 2 — maksymalna ekspozycja przed pierwszym przychodem", fmt(EXPOSURE)],
      ["Transza 3 — w istotnej części pokrywana z przychodów Early Access", fmt(TR[2].total)],
      { cells: ["Suma nominalna wszystkich transz", fmt(TOTAL)], bold: true, bg: LIGHT },
      ["Średni koszt miesięczny w transzy 1", fmt(Math.round(TR[0].total / TR[0].len / 100) * 100)],
      ["Średni koszt miesięczny w transzy 2", fmt(Math.round(TR[1].total / TR[1].len / 100) * 100)],
    ],
    [70, 30], { numFrom: 1, numTo: 1 }
  ),
  H2("6.3 Zasady rozliczenia transz"),
  BUL("Wypłata w ratach miesięcznych na podstawie faktur, a nie jednorazowo z góry."),
  BUL("Rezerwa na ryzyko uruchamiana za zgodą wydawcy, na wniosek z uzasadnieniem."),
  BUL("Raport postępu co miesiąc, grywalna kompilacja co dwa miesiące."),
  BUL("Niewykorzystana część transzy przechodzi na kolejną albo pomniejsza kolejną wypłatę."),
  new Paragraph({ children: [new PageBreak()] }),
);

/* ---------- 7. BUDŻET ---------- */
const tranzaBudget = (t, nr) => [
  H2(`7.${nr} ${t.id} — ${t.nazwa} (miesiące ${t.od}–${t.do})`),
  P(t.cel),
  H3("Koszty osobowe"),
  tbl(
    ["Rola", "Forma", "M-ce", "Koszt / mies.", "Razem"],
    [
      ...t.rows.map(r => [r.name, r.forma === "b2b" ? "B2B" : "zlecenie", `${r.from}–${r.to}`, fmt(r.monthly), fmt(r.total)]),
      { cells: ["RAZEM OSOBOWE", "", "", "", fmt(t.personnel)], bold: true, bg: LIGHT },
    ],
    [42, 12, 10, 18, 18], { numFrom: 3, numTo: 4 }
  ),
  H3("Koszty pozostałe"),
  tbl(
    ["Pozycja", "Kwota", "Uwagi"],
    [
      ...t.other.map(([n, v, u]) => [n, fmt(v), u]),
      { cells: ["RAZEM POZOSTAŁE", fmt(t.otherSum), ""], bold: true, bg: LIGHT },
    ],
    [33, 15, 52], { numFrom: 1, numTo: 1 }
  ),
  H3("Suma transzy"),
  tbl(
    ["Pozycja", "Kwota"],
    [
      ["Koszty osobowe", fmt(t.personnel)],
      ["Koszty pozostałe", fmt(t.otherSum)],
      [`Rezerwa na ryzyko (${Math.round(t.reserve * 100)}%)`, fmt(t.reserve)],
      { cells: [`${t.id.toUpperCase()} RAZEM`, fmt(t.total)], bold: true, bg: LIGHT },
    ],
    [70, 30], { numFrom: 1, numTo: 1 }
  ),
];

add(
  H1("7. Budżet"),
  H2("7.0 Założenia"),
  BULB("Dwie formy zatrudnienia: ", "założyciel rozlicza się w modelu B2B — koszt równa się kwocie faktury, bez narzutu. Pozostałe osoby pracują na umowie zlecenia z pełnym ubezpieczeniem społecznym, gdzie koszt to wynagrodzenie brutto powiększone o 20,48%. Forma jest wskazana przy każdej roli w tabelach poniżej."),
  BULB("Stawki: ", "rynek polski, praca zdalna. Rdzeń zespołu to jeden programista na poziomie średnim i dwie osoby na poziomie juniorskim. Role dokładane w transzach 2 i 3 są wyceniane na poziomie odpowiadającym ich odpowiedzialności — w szczególności starszy programista odpowiedzialny za animację proceduralną i fizykę."),
  BULB("Czego w budżecie nie ma: ", "outsourcingu grafiki trójwymiarowej, kosztów obsługi księgowej i administracyjnej, opłaty Steam Direct oraz jakichkolwiek kosztów marketingu i produkcji materiałów promocyjnych. Księgowość prowadzona jest we własnym zakresie, a marketing w całości po stronie wydawcy."),
  BULB("Licencje: ", "w transzy 1 wystarcza bezpłatny Unity Personal. Od transzy 2 łączne finansowanie przekracza próg 200 000 USD w dwunastu miesiącach, co zgodnie z warunkami licencyjnymi Unity wymusza przejście na Unity Pro. Nie planujemy płatnych licencji na oprogramowanie graficzne ani na środowisko programistyczne."),
  BULB("Stawki: ", "rynek polski, poziom średni i starszy, praca zdalna."),
  BULB("Wyłączenia: ", "budżet nie obejmuje portów konsolowych, certyfikacji, serwerów dedykowanych ani wsparcia po wersji 1.0 — pozycje te opisano w rozdziałach 10 i 13."),
  BULB("Waloryzacja: ", "kwoty podano w wartościach bieżących. Przy trzyletnim cyklu rekomendujemy zapis o corocznej waloryzacji stawek o 5%."),
  ...tranzaBudget(TR[0], 1),
  new Paragraph({ children: [new PageBreak()] }),
  ...tranzaBudget(TR[1], 2),
  new Paragraph({ children: [new PageBreak()] }),
  ...tranzaBudget(TR[2], 3),
  H2("7.4 Zestawienie"),
  tbl(
    ["Transza", "M-ce", "Zespół", "Osobowe", "Pozostałe", "Rezerwa", "Razem"],
    [
      ...TR.map(t => [t.id, `${t.od}–${t.do}`, `${t.peak}`, fmt(t.personnel), fmt(t.otherSum), fmt(t.reserve), fmt(t.total)]),
      { cells: ["RAZEM", `1–${TR[2].do}`, "", fmt(TR.reduce((a,b)=>a+b.personnel,0)), fmt(TR.reduce((a,b)=>a+b.otherSum,0)), fmt(TR.reduce((a,b)=>a+b.reserve,0)), fmt(TOTAL)], bold: true, bg: LIGHT },
    ],
    [13, 9, 8, 18, 18, 16, 18], { numFrom: 1, numTo: 6 }
  ),
  new Paragraph({ children: [new PageBreak()] }),
);

/* ---------- 8. STRUKTURA PRAWNA ---------- */
add(
  H1("8. Warianty struktury współpracy"),
  P("Nie zajmujemy stanowiska w tej sprawie przed rozmową. Poniżej zestawiamy trzy struktury spotykane na rynku, wraz z tym, co każda z nich oznacza dla obu stron. Naszym celem jest znalezienie układu, w którym FreeMind ma zwrot proporcjonalny do podjętego ryzyka, a zespół zachowuje motywację do prowadzenia gry przez kilka lat po premierze."),
  tbl(
    ["Wariant", "Co otrzymuje wydawca", "Co pozostaje u zespołu", "Kiedy ma sens"],
    [
      ["A. Podział przychodów bez udziałów", "Ustalony udział w przychodach netto po zwrocie nakładów; prawo pierwszeństwa do kolejnych tytułów", "Całość praw do marki, technologii i spółki", "Najlepiej dopasowany do samej transzy 1, gdzie kwota jest niewielka, a ryzyko ograniczone"],
      ["B. Spółka celowa, pakiet mniejszościowy", "Udziały do 49%, dywidenda i udział w przychodach, miejsce w radzie nadzorczej", "Kontrola operacyjna i decyzje o kierunku gry; zespół pozostaje właścicielem większościowym", "Przy finansowaniu transz 2 i 3, gdy zaangażowanie kapitałowe rośnie do kilku milionów"],
      ["C. Spółka celowa, pakiet większościowy", "Kontrola kapitałowa, konsolidacja wyniku w grupie, pełny dostęp do zasobów i promocji krzyżowej grupy", "Pakiet mniejszościowy, wynagrodzenia, program motywacyjny; utrata kontroli nad spółką", "Układ spotykany w grupie PlayWay; daje największy dostęp do kapitału i zaplecza grupy"],
    ],
    [20, 28, 26, 26]
  ),
  H2("8.1 Nasze stanowisko wyjściowe"),
  BUL("Jesteśmy otwarci na każdy z trzech wariantów i traktujemy to jako przedmiot negocjacji, nie warunek brzegowy."),
  BUL("Zależy nam na zachowaniu praw do technologii kreatora także poza tym tytułem — jest ona podstawą naszej dalszej działalności."),
  BUL("Preferujemy strukturę stopniowaną: wariant A dla transzy 1, przejście do wariantu B lub C dopiero przy uruchomieniu większego finansowania."),
  BUL("Oczekujemy przejrzystych zasad zwrotu nakładów oraz jasno określonej kolejności zaspokajania stron z przychodów."),
  new Paragraph({ children: [new PageBreak()] }),
);

/* ---------- 9. HARMONOGRAM ---------- */
add(
  H1("9. Harmonogram"),
  P("Osiemnaście miesięcy do wersji 1.0, z premierą w Early Access w 12. miesiącu. Harmonogram jest celowo napięty — w tym gatunku szybkość wejścia na rynek jest przewagą, a nie kompromisem. Kamienie milowe rozliczane są grywalną kompilacją albo pomiarem, nie deklaracją postępu."),
  H2("9.1 Transza 1 — walidacja rynkowa (miesiące 1–4)"),
  tbl(
    ["Kamień", "M-c", "Rezultat weryfikowalny"],
    [
      ["KM 1", "2", "Pętla kreator–rozgrywka: stworzenie zbudowane przez gracza wchodzi do sceny, chodzi po nierównym terenie, przewraca się przy złym wyważeniu i wstaje. Czterech graczy w jednej sesji przy opóźnieniu 150 ms."],
      ["KM 2", "3", "Demo zamknięte: 15 minut rozgrywki, kreator z katalogiem 40 części, jeden biom. Testy z graczami spoza zespołu."],
      ["KM 3", "4", "Publikacja strony na Steam, zwiastun, udostępnienie publicznego dema. Start pomiaru listy życzeń."],
      ["BRAMKA G1", "4 + 6 tyg.", "Ocena listy życzeń i danych o zachowaniu graczy. Decyzja o transzy 2."],
    ],
    [11, 12, 77]
  ),
  H2("9.2 Transza 2 — produkcja do Early Access (miesiące 5–12)"),
  tbl(
    ["Kamień", "M-c", "Rezultat weryfikowalny"],
    [
      ["KM 4", "7", "Kreator w pełnej wersji: budowa, zapis, wczytanie i przesłanie stworka przez sieć; katalog 60 części"],
      ["KM 5", "9", "Walka z przeciwnikami i sztuczna inteligencja; pełna pętla sesji z celem i ewakuacją; drugi biom"],
      ["KM 6", "10", "Progresja międzysesyjna, integracja ze Steam Workshop, demo na Steam Next Fest"],
      ["KM 7", "11", "Zewnętrzne testy z graczami, optymalizacja, lokalizacja"],
      ["BRAMKA G2", "12", "Kandydat do wydania: testy jakości, materiały sklepowe"],
      ["PREMIERA EARLY ACCESS", `${EA_MONTH}`, "Wejście do sprzedaży"],
    ],
    [11, 12, 77]
  ),
  H2("9.3 Transza 3 — Early Access do wersji 1.0 (miesiące 13–18)"),
  tbl(
    ["Okres", "M-ce", "Zawartość"],
    [
      ["Stabilizacja", "13–14", "Intensywne poprawki na podstawie zgłoszeń, balans, korekta krzywej wejścia dla nowych graczy"],
      ["BRAMKA G3", "13", "Ocena sprzedaży pierwszego miesiąca. Decyzja o pełnym lub skróconym zakresie transzy 3"],
      ["Aktualizacja 1", "15–16", "Trzeci biom, rozbudowa katalogu części, starcie z bossem"],
      ["Aktualizacja 2", "17–18", "Kolejni przeciwnicy, narzędzia społecznościowe wokół Workshopu, pełny przegląd jakości"],
      ["PREMIERA 1.0", `${V10_MONTH}`, "Domknięcie zakresu finansowanego"],
    ],
    [24, 12, 64]
  ),
  H2("9.4 Co świadomie zostaje poza tym harmonogramem"),
  P("Skrócenie cyklu z dwudziestu ośmiu miesięcy do osiemnastu wymaga cięć. Wymieniamy je wprost, żeby nie były odkryciem w trakcie produkcji."),
  tbl(
    ["Element", "Pierwotny zakres", "W tym harmonogramie"],
    [
      ["Biomy na wersję 1.0", "4", "3 — czwarty jako aktualizacja po premierze"],
      ["Katalog części ciała", "ok. 120", "ok. 70"],
      ["Starcia z bossami", "4", "1"],
      ["Publiczne API modów", "W zakresie 1.0", "Pierwsza duża aktualizacja po 1.0, z przychodów"],
      ["Lokalizacja", "10 języków", "6 języków"],
      ["Okno Early Access", "12 miesięcy, 3 aktualizacje sezonowe", "6 miesięcy, 2 aktualizacje"],
      ["Content designer w zespole", "Etat od 11. miesiąca", "Rola rozdzielona między projektanta i programistę gameplay"],
    ],
    [26, 34, 40]
  ),
  P("Nienaruszone pozostają kreator o pełnej swobodzie, symulacja fizyczna, kooperacja dla czterech graczy i Steam Workshop dla stworków. To one decydują o tym, czy gra zadziała — reszta jest objętością, którą można dołożyć po premierze.", { b: true }),
  new Paragraph({ children: [new PageBreak()] }),
);

/* ---------- 10. PLATFORMY ---------- */
add(
  H1("10. Platformy"),
  P("FreeMind deklaruje wydawanie wieloplatformowe od pierwszego dnia. Traktujemy to poważnie i dlatego przedstawiamy rzetelną analizę zamiast deklaracji. Ograniczeniem nie jest tu grafika, lecz budżet procesora: każde stworzenie w scenie to osobny rig proceduralny z własną symulacją, a takich obiektów jest jednocześnie kilkanaście."),
  tbl(
    ["Platforma", "Wykonalność", "Uzasadnienie i warunki"],
    [
      ["PC / Steam", "Wysoka — platforma startowa", "Pełna kontrola nad budżetem wydajnościowym, skalowalne ustawienia, natywne wsparcie dla Workshopu i rozgrywki sieciowej przez Steam"],
      ["Xbox Series X/S, PlayStation 5", "Wysoka — port realny", "Sprzęt udźwignie symulację przy zachowaniu obecnych założeń. Port 6–9 miesięcy, zespół 2–3 osoby, koszt 450 000 – 700 000 PLN na obie platformy. Uruchamiany po potwierdzeniu sprzedaży na PC"],
      ["Xbox One, PlayStation 4", "Niska", "Brak marginesu mocy procesora na równoczesną symulację wielu rigów proceduralnych. Port wymagałby obniżenia liczby aktorów w scenie do poziomu zmieniającego rozgrywkę"],
      ["Nintendo Switch", "Niska", "Ta sama bariera co wyżej, dodatkowo ograniczona pamięć. Nie rekomendujemy"],
      ["Nintendo Switch 2", "Do zbadania", "Wymaga osobnej analizy wykonalności po premierze na PC. Nie deklarujemy tego portu przed pomiarami na docelowym sprzęcie"],
      ["Meta Quest VR", "Bardzo niska", "Poza barierą wydajnościową występuje bariera projektowa: kreator i kooperacyjne przetrwanie w rzeczywistości wirtualnej to inna gra, nie port. Byłby to osobny projekt o własnym budżecie"],
    ],
    [20, 18, 62]
  ),
  H2("10.1 Nasza rekomendacja"),
  P("Premiera na PC, a decyzja o portach na konsole obecnej generacji po potwierdzeniu sprzedaży. Kolejność ta jest korzystna również dla wydawcy: port finansowany z przychodów zrealizowanej gry jest inwestycją o znanym ryzyku, a port robiony równolegle z produkcją jest kosztem obciążającym projekt, który nie udowodnił jeszcze popytu."),
  P("Jeżeli premiera wieloplatformowa jest dla FreeMind warunkiem brzegowym współpracy, jesteśmy gotowi omówić przeprojektowanie założeń symulacji pod słabszy sprzęt — z zastrzeżeniem, że oznacza to obniżenie liczby stworzeń w scenie, a więc zmianę charakteru rozgrywki. Wolimy powiedzieć to teraz niż odkryć na etapie certyfikacji.", { i: true }),
  new Paragraph({ children: [new PageBreak()] }),
);

/* ---------- 11. PROGNOZA ---------- */
add(
  H1("11. Prognoza przychodów"),
  H2("11.1 Założenia"),
  BULB("Cena: ", "69 PLN w Early Access, 89 PLN po premierze wersji 1.0."),
  BULB("Efektywna cena sprzedaży: ", "około 58 PLN brutto po uwzględnieniu obniżek sezonowych i regionalnych różnic cenowych."),
  BULB("Przychód netto: ", `około ${NET_PER_UNIT} PLN na egzemplarz, po podatku od towarów i usług (średnio 19%) i prowizji Steam (30%).`),
  BULB("Horyzont: ", "24 miesiące od premiery w Early Access. Kwoty przed podziałem między strony."),
  H2("11.2 Scenariusze"),
  tbl(
    ["Scenariusz", "Sprzedaż", "Przychód netto", "Opis"],
    SCEN.map(([n, u, d]) => [n, num(u) + " egz.", fmtM(u * NET_PER_UNIT), d]),
    [16, 15, 17, 52], { numFrom: 1, numTo: 2 }
  ),
  H2("11.3 Próg rentowności"),
  tbl(
    ["Zakres finansowania", "Kwota", "Próg rentowności", "Scenariusz pokrywający"],
    [
      ["Transza 1", fmt(TR[0].total), `ok. ${num(breakEven(TR[0].total))} egz.`, "Poniżej pesymistycznego"],
      ["Transze 1 + 2 (ekspozycja maksymalna)", fmt(EXPOSURE), `ok. ${num(breakEven(EXPOSURE))} egz.`, "Między pesymistycznym a ostrożnym"],
      ["Wszystkie trzy transze", fmt(TOTAL), `ok. ${num(breakEven(TOTAL))} egz.`, "Między ostrożnym a bazowym"],
    ],
    [34, 20, 22, 24], { numFrom: 1, numTo: 2 }
  ),
  P("Najważniejsza liczba w tym rozdziale znajduje się w pierwszym wierszu. Transza 1 zwraca się przy sprzedaży poniżej scenariusza pesymistycznego — a więc decyzja o jej uruchomieniu nie zależy od tego, czy Leeway okaże się przebojem.", { b: true }),
  new Paragraph({ children: [new PageBreak()] }),
);

/* ---------- 12. RYZYKA ---------- */
add(
  H1("12. Rejestr ryzyk"),
  tbl(
    ["Ryzyko", "Wpływ", "Praw.", "Mitygacja"],
    [
      ["Demo nie generuje wystarczającego zainteresowania (bramka G1)", "Wysoki", "Średnie", "Jest to zaplanowany wynik, nie awaria. Ekspozycja ograniczona do transzy 1; strona na Steam i zebrana lista życzeń pozostają aktywem"],
      ["Spadek wydajności przy czterech graczach i stadach przeciwników", "Wysoki", "Wysokie", "Budżet klatki ustalony i mierzony automatycznie od pierwszego kamienia milowego; poziomy szczegółowości symulacji; twarde limity w budżecie genomu"],
      ["Rozszerzanie zakresu w stronę kolejnych etapów wzorowanych na Spore", "Wysoki", "Wysokie", "Zakres jednego etapu zamrożony w umowie; propozycje spoza zakresu trafiają na listę po wersji 1.0"],
      ["Nieudana rekrutacja starszego programisty do transzy 2", "Wysoki", "Średnie", "Rekrutacja startuje w trakcie transzy 1, przed potrzebą; rezerwa pozwala podnieść stawkę; wariant awaryjny z kontraktorem"],
      ["Zerowa obecność marketingowa na starcie", "Wysoki", "Pewne", "Jest to główny cel transzy 1. Strona na Steam w 4. miesiącu, materiały z prac od 2. miesiąca, Community Manager od 6. miesiąca"],
      ["Oczekiwanie premiery wieloplatformowej niemożliwej technicznie", "Średni", "Wysokie", "Analiza per platforma w rozdziale 10 przedstawiona przed podpisaniem umowy, a nie w trakcie produkcji"],
      ["Kumulacja trzech ról na projektancie gry (projekt, produkcja, dźwięk)", "Średni", "Wysokie", "Zlecenie części sound designu na zewnątrz po wejściu w Early Access; wsparcie produkcyjne po stronie wydawcy"],
      ["Mały zespół i zależność od pojedynczych osób", "Wysoki", "Średnie", "Dokumentacja architektury, przeglądy kodu, testy automatyczne, warstwa domenowa odseparowana od silnika"],
      ["Nieodpowiednie treści publikowane przez graczy", "Średni", "Wysokie", "Walidacja genomu przy imporcie, twarde limity, moderacja oparta o mechanizmy Steam"],
      ["Konkurencja z podobną koncepcją", "Średni", "Średnie", "Skrócenie drogi do publicznego dema; przewaga oparta na jakości kreatora, którego nie da się skopiować szybko"],
    ],
    [27, 10, 9, 54], { numFrom: 1, numTo: 2 }
  ),
  new Paragraph({ children: [new PageBreak()] }),
);

/* ---------- 13. PO PREMIERZE ---------- */
add(
  H1("13. Po premierze wersji 1.0"),
  tbl(
    ["Okres", "Zakres", "Koszt miesięczny"],
    [
      ["Miesiące 1–3", "Intensywne poprawki, reakcja na zgłoszenia, korekty balansu. Pełny zespół w gotowości.", "150 000 – 200 000 PLN"],
      ["Miesiące 4–12", "Utrzymanie, mniejsze aktualizacje, wsparcie społeczności modderskiej. Zespół zredukowany do rdzenia.", "70 000 – 90 000 PLN"],
      ["Powyżej 12 miesięcy", "Opcjonalnie: dodatki kosmetyczne, porty konsolowe, serwery dedykowane. Decyzja zależna od sprzedaży.", "Do ustalenia osobno"],
    ],
    [18, 58, 24], { numFrom: 2, numTo: 2 }
  ),
  P("Rekomendujemy zabezpieczenie w umowie środków na co najmniej trzy miesiące wsparcia po premierze 1.0. Brak takiego zabezpieczenia jest jedną z najczęstszych przyczyn utraty wartości gry tuż po premierze — zespół zostaje rozwiązany dokładnie wtedy, gdy zgłoszeń jest najwięcej."),
  H2("13.1 Dodatkowe źródła przychodu"),
  BULB("Kosmetyczne zestawy części ciała ", "jako opcjonalne, płatne dodatki, bez wpływu na rozgrywkę. Nie wcześniej niż trzy miesiące po premierze 1.0."),
  BULB("Publiczne API modów ", "jako pierwsza duża aktualizacja po wersji 1.0 — przeniesione poza finansowanie, ale pozostaje w planie produktu, bo to ono buduje najdłuższy ogon sprzedaży."),
  BULB("Porty konsolowe ", "zgodnie z analizą w rozdziale 10."),
  BULB("Duży dodatek z nowym biomem ", "rozważany przy scenariuszu bazowym lub lepszym."),
  P("Nie planujemy mikropłatności wpływających na rozgrywkę ani przepustek sezonowych. W grze kooperacyjnej dla znajomych każda taka forma monetyzacji kosztuje więcej w zaufaniu, niż przynosi w przychodzie.", { b: true }),
  new Paragraph({ children: [new PageBreak()] }),
);

/* ---------- 14. NASTĘPNE KROKI ---------- */
add(
  H1("14. Czego potrzebujemy i następne kroki"),
  H2("14.1 Czego oczekujemy od FreeMind"),
  BULB("Finansowanie transzy 1 ", `w kwocie ${fmt(TR[0].total)}, wypłacane w ratach miesięcznych.`),
  BULB("Uzgodnienie warunków bramki G1 ", "przed startem — progu listy życzeń i sposobu pomiaru, aby decyzja o transzy 2 była bezsporna."),
  BULB("Prowadzenie marketingu ", "i obecność w kanałach grupy: kampania wokół strony na Steam, kontakt z twórcami internetowymi, obecność na festiwalach, promocja krzyżowa w katalogu."),
  BULB("Obsługę relacji z Valve ", "oraz, w razie decyzji o portach, z producentami konsol i procesami certyfikacji."),
  BULB("Warunki brzegowe struktury współpracy ", "dla transz 2 i 3, zgodnie z wariantami z rozdziału 8."),
  H2("14.2 Zgodność ze zgłoszeniem"),
  tbl(
    ["Wymaganie FreeMind", "Status"],
    [
      ["Jednozdaniowy opis gry", "Gotowe — rozdział 1"],
      ["Krótki materiał wideo z rozgrywki", "Do przygotowania przed spotkaniem — nagranie z obecnego prototypu kreatora"],
      ["Platformy docelowe", "Gotowe — rozdział 10, wraz z analizą wykonalności"],
      ["Oczekiwane okno wydawnicze", `Gotowe — Early Access w ${EA_MONTH}. miesiącu, wersja 1.0 w ${V10_MONTH}. miesiącu od startu`],
      ["Grywalna kompilacja", "Dostępna — prototyp kreatora do przekazania w ciągu dwóch tygodni"],
    ],
    [38, 62]
  ),
  H2("14.3 Proponowane kroki"),
  tbl(
    ["Krok", "Termin", "Po czyjej stronie"],
    [
      ["Nagranie i przekazanie materiału wideo z prototypu", "do 1 tygodnia", "Zespół"],
      ["Przekazanie grywalnej kompilacji kreatora", "do 2 tygodni", "Zespół"],
      ["Spotkanie: zakres transzy 1 i warunki bramki G1", "—", "Wspólnie"],
      ["Wybór wariantu struktury współpracy", "—", "Wspólnie"],
      ["Warunki brzegowe umowy", "—", "FreeMind"],
      ["Start transzy 1 i rozpoczęcie rekrutacji pod transzę 2", "dzień podpisania umowy", "Zespół"],
    ],
    [50, 26, 24]
  ),
  new Paragraph({ children: [new PageBreak()] }),
);

/* ---------- ZAŁĄCZNIK A ---------- */
add(
  H1("Załącznik A. Warianty alternatywne — finansowanie jednorazowe"),
  P("Model transzowy z rozdziału 6 jest naszą rekomendacją dla FreeMind. Dla porządku przedstawiamy też dwa warianty klasyczne, w których wydawca podejmuje jedną decyzję o finansowaniu całej produkcji. Różnią się one od modelu transzowego rozkładem ryzyka, a nie zakresem gry."),
  tbl(
    ["Wskaźnik", "Model transzowy (rekomendowany)", "Wariant pełny", "Wariant minimalny"],
    [
      ["Czas do Early Access", `${EA_MONTH} mies.`, `${PLAN_A.ea} mies.`, `${PLAN_B.ea} mies.`],
      ["Czas do wersji 1.0", `${V10_MONTH} mies.`, `${PLAN_A.months} mies.`, `${PLAN_B.months} mies.`],
      ["Zespół docelowy", `${TR[2].peak} osób`, `${PLAN_A.staff.length} osób`, `${PLAN_B.staff.length} osób`],
      ["Budżet produkcyjny", fmt(TOTAL), fmt(A.total), fmt(B.total)],
      ["Pierwsza decyzja finansowa", fmt(TR[0].total), fmt(A.total), fmt(B.total)],
      ["Maksymalna ekspozycja", fmt(EXPOSURE), fmt(A.total), fmt(B.total)],
      ["Zakres wersji 1.0", "3 biomy, Workshop, 6 języków", "4 biomy, mody, 10 języków", "2 biomy, bez modów, 4 języki"],
      ["Liczba punktów wyjścia", "3 bramki decyzyjne", "1 — po premierze Early Access", "1 — po premierze Early Access"],
    ],
    [24, 26, 25, 25], { numFrom: 1, numTo: 3 }
  ),
  P("Warianty alternatywne obejmują szerszy zakres treści, ale wymagają podjęcia decyzji o całej kwocie przed jakimkolwiek sygnałem z rynku i wydłużają czas do premiery do dwóch i pół roku. Model transzowy przenosi decyzję na moment, w którym dostępne są dane o zachowaniu graczy, i wprowadza grę do sprzedaży po dwunastu miesiącach — dlatego uważamy go za korzystniejszy dla obu stron.", { i: true }),
  P("Szczegółowy rozpis obu wariantów, wraz z pełnym opisem projektu, znajduje się w dokumencie „Leeway — dokument projektowy i biznesplan produkcji”, dostępnym na życzenie."),
  SPACER(300),
  H1("Załącznik B. Założenia przyjęte do wyliczeń"),
  tbl(
    ["Założenie", "Wartość", "Uwagi"],
    [
      ["Narzut na umowę zlecenie (pełny ZUS)", "20,48%", "Emerytalna 9,76%, rentowa 6,50%, wypadkowa 1,67%, Fundusz Pracy 2,45%, FGŚP 0,10%"],
      ["Narzut na B2B", "brak", "Koszt równy kwocie faktury"],
      ["Mid Unity Developer — założyciel", "16 000 PLN / mies. (B2B)", "Stawka rynkowa dla poziomu średniego, praca zdalna"],
      ["Junior Game Designer i Junior Artysta 3D", "6 500 PLN brutto / mies.", "Umowa zlecenie; koszt 7 800 PLN / mies. każda"],
      ["Senior Programista — fizyka i animacja proceduralna", "19 000 PLN brutto / mies.", "Rola dokładana w transzy 2; najtrudniejsza do obsadzenia"],
      ["Mid Tech Artist i Mid Programista gameplay", "13 000 PLN brutto / mies.", "Role dokładane w transzie 2"],
      ["QA Specialist", "7 000 PLN brutto / mies.", "Rola dokładana pod koniec transzy 2"],
      ["Community Manager", "3 500 PLN brutto / mies.", "Pół etatu"],
      ["Licencja Unity", "Personal w transzy 1, Pro od transzy 2", "Unity Personal bezpłatny poniżej 200 000 USD przychodu lub finansowania w 12 mies.; Unity Pro 2 310 USD / stanowisko / rok"],
      ["Koszt stanowiska pracy", "13 000 PLN", "Jednorazowo, dla osób dokładanych w transzy 2"],
      ["Prowizja platformy Steam", "30%", "Bez progów obniżających prowizję"],
      ["Podatek od towarów i usług", "19% średnio", "Ważony strukturą rynków"],
      ["Przychód netto na egzemplarz", `${NET_PER_UNIT} PLN`, "Przy efektywnej cenie sprzedaży ok. 58 PLN brutto"],
      ["Rezerwa na ryzyko", "10% (transza 1), 12% (transze 2 i 3)", "Uruchamiana za zgodą wydawcy"],
      ["Próg listy życzeń przy bramce G1", "8 000 (do negocjacji)", "Mierzony 6 tygodni po publikacji strony na Steam"],
      ["Waloryzacja wynagrodzeń", "nieuwzględniona", "Rekomendowane 5% rocznie"],
    ],
    [30, 24, 46]
  ),
  SPACER(240),
  P("Wszystkie kwoty podano w złotych, w wartościach z września 2026 roku. Dokument nie stanowi oferty w rozumieniu przepisów prawa handlowego.", { i: true, size: 18, color: GREY }),
);

/* ============================ SKŁAD ============================ */

const doc = new Document({
  creator: "Leeway",
  title: "Leeway — propozycja współpracy wydawniczej dla FreeMind S.A.",
  description: "Etapowy model finansowania, budżet, harmonogram i zakres projektu Leeway",
  numbering: {
    config: [{
      reference: "steps",
      levels: [{
        level: 0, format: "decimal", text: "%1.", alignment: AlignmentType.START,
        style: { paragraph: { indent: { left: 520, hanging: 260 } } },
      }],
    }],
  },
  styles: {
    default: {
      document: { run: { font: "Calibri", size: 21, color: "1A1A1A" } },
      heading1: { run: { font: "Calibri", size: 34, bold: true, color: NAVY }, paragraph: { spacing: { before: 400, after: 180 } } },
      heading2: { run: { font: "Calibri", size: 26, bold: true, color: NAVY }, paragraph: { spacing: { before: 280, after: 120 } } },
      heading3: { run: { font: "Calibri", size: 23, bold: true, color: ACCENT }, paragraph: { spacing: { before: 200, after: 100 } } },
    },
  },
  sections: [{
    properties: {
      page: { margin: {
        top: convertInchesToTwip(0.85), bottom: convertInchesToTwip(0.85),
        left: convertInchesToTwip(0.85), right: convertInchesToTwip(0.85),
      } },
    },
    footers: {
      default: new Footer({
        children: [new Paragraph({
          alignment: AlignmentType.CENTER,
          children: [
            new TextRun({ text: "Leeway — propozycja współpracy dla FreeMind S.A.   |   str. ", size: 16, color: GREY }),
            new TextRun({ children: [PageNumber.CURRENT], size: 16, color: GREY }),
          ],
        })],
      }),
    },
    children: body,
  }],
});

const out = process.env.OUT || "/workspace/Docs/GDD/Leeway_FreeMind_Propozycja.docx";
const buf = await Packer.toBuffer(doc);
writeFileSync(out, buf);
console.log("OK ->", out, (buf.length / 1024).toFixed(0) + " KB");
TR.forEach(t => console.log(`${t.id} (m${t.od}-${t.do}, ${t.peak} os.):`, fmt(t.total)));
console.log("RAZEM:", fmt(TOTAL), "| Ekspozycja maks.:", fmt(EXPOSURE));
console.log("EA:", EA_MONTH, "| 1.0:", V10_MONTH, "| break-even T1:", breakEven(TR[0].total));
