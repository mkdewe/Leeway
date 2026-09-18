import {
  Document, Packer, Paragraph, TextRun, AlignmentType,
  PageBreak, Footer, PageNumber, convertInchesToTwip
} from "docx";
import { writeFileSync } from "fs";
import {
  PLAN_A, PLAN_B, A, B, NET_PER_UNIT, SCEN, fmt, fmtM, breakEven, num,
} from "./lib-budget.mjs";
import {
  NAVY, ACCENT, GREY, LIGHT, HEADBG,
  H1, H2, H3, P, rich, BUL, BULB, NUM, tbl, SPACER, CAPTION,
} from "./lib-docx.mjs";

/* ============================ TREŚĆ DOKUMENTU ============================ */

const body = [];
const add = (...x) => body.push(...x.flat());

/* ---------- STRONA TYTUŁOWA ---------- */
add(
  SPACER(1800),
  new Paragraph({
    alignment: AlignmentType.CENTER, spacing: { after: 80 },
    children: [new TextRun({ text: "LEEWAY", bold: true, size: 88, color: NAVY })],
  }),
  new Paragraph({
    alignment: AlignmentType.CENTER, spacing: { after: 420 },
    children: [new TextRun({ text: "Kooperacyjna gra o ewolucji stworków opartej na fizyce", size: 28, color: GREY })],
  }),
  new Paragraph({
    alignment: AlignmentType.CENTER, spacing: { after: 120 },
    children: [new TextRun({ text: "Dokument projektowy i biznesplan produkcji", bold: true, size: 30 })],
  }),
  new Paragraph({
    alignment: AlignmentType.CENTER, spacing: { after: 1200 },
    children: [new TextRun({ text: "Materiał dla inwestora / wydawcy", size: 24, color: GREY })],
  }),
  tbl(
    ["Parametr", "Wartość"],
    [
      ["Gatunek", "Kooperacyjna gra przetrwania z kreatorem stworków opartym na fizyce"],
      ["Platforma docelowa", "PC (Steam); konsole jako opcja do decyzji wydawcy"],
      ["Liczba graczy", "1–4 (solo oraz co-op, host-klient przez Steam)"],
      ["Silnik i technologia", "Unity 6 (6000.4.7f1), URP 17.4, FishNet 4.7.2, VContainer, UniTask"],
      ["Model wydania", "Early Access → wersja 1.0"],
      ["Model sprzedaży", "Premium, 69 PLN w Early Access / 89 PLN w 1.0"],
      ["Status", "Działający prototyp technologiczny kreatora i rigu proceduralnego"],
      ["Zespół dziś", "3 osoby (core team)"],
      ["Wnioskowane finansowanie", "Pełne finansowanie produkcji przez wydawcę"],
      ["Data dokumentu", "wrzesień 2026 — wersja 0.9, szkic roboczy"],
    ],
    [30, 70]
  ),
  new Paragraph({ children: [new PageBreak()] }),
);

/* ---------- 1. STRESZCZENIE WYKONAWCZE ---------- */
add(
  H1("1. Streszczenie wykonawcze"),
  P("Leeway to kooperacyjna gra dla 1–4 graczy, w której gracze projektują własne stworzenia w swobodnym kreatorze, a następnie próbują nimi przetrwać w proceduralnie generowanym świecie. Kluczowa różnica względem podobnych gier polega na tym, że stworek nie jest animowany ręcznie — jego ruch wynika z fizyki i proceduralnej animacji opartej na kształcie, jaki nadał mu gracz. Zwierzę o czterech krótkich nogach porusza się inaczej niż dwunożny olbrzym na cienkich odnóżach, a decyzje projektowe gracza mają bezpośrednie, obserwowalne konsekwencje w rozgrywce."),
  P("Ta zasada tworzy pętlę, która jednocześnie jest źródłem rozgrywki i materiału marketingowego: gracze projektują absurdalne stworzenia, obserwują, jak zachowują się one w fizyce, dzielą się nagraniami, a następnie iterują. Gatunek gier kooperacyjnych o niskim progu wejścia i wysokim potencjale wirusowym — określany potocznie jako „friendslop” — należy obecnie do najszybciej rosnących segmentów Steam, a Leeway wnosi do niego wyróżnik, którego nie ma żadna z gier referencyjnych: pełnoprawny kreator stworków w duchu Spore."),
  H2("1.1 Czego dotyczy wniosek"),
  P("Wnioskujemy o pełne finansowanie produkcji. Przedstawiamy dwa warianty zakresu, oba z rozpisanym budżetem, zespołem i harmonogramem:"),
  BULB("Plan A (rekomendowany) — ", `${PLAN_A.months} miesięcy do wersji 1.0, premiera Early Access w ${PLAN_A.ea}. miesiącu, zespół docelowy 9 osób, budżet produkcyjny ${fmtM(A.total)}.`),
  BULB("Plan B (minimum wydawnicze) — ", `${PLAN_B.months} miesięcy do wersji 1.0, premiera Early Access w ${PLAN_B.ea}. miesiącu, zespół docelowy 7 osób, budżet produkcyjny ${fmtM(B.total)}.`),
  P("Plan B jest wykonalny i prowadzi do sprzedawalnej gry, ale osiąga to kosztem objętości treści, liczby biomów i wsparcia dla modów. Plan A traktujemy jako wariant docelowy, Plan B jako zabezpieczenie budżetowe i punkt odniesienia dla negocjacji zakresu."),
  H2("1.2 Najważniejsze liczby"),
  tbl(
    ["Wskaźnik", "Plan A (rekomendowany)", "Plan B (minimum)"],
    [
      ["Czas do Early Access", `${PLAN_A.ea} miesięcy`, `${PLAN_B.ea} miesięcy`],
      ["Czas do wersji 1.0", `${PLAN_A.months} miesięcy`, `${PLAN_B.months} miesięcy`],
      ["Zespół docelowy (szczyt)", `${PLAN_A.staff.length} osób`, `${PLAN_B.staff.length} osób`],
      ["Nakład pracy", `${Math.round(A.fte)} osobomiesięcy`, `${Math.round(B.fte)} osobomiesięcy`],
      ["Budżet produkcyjny", fmt(A.total), fmt(B.total)],
      ["Budżet marketingowy (osobno)", `${fmt(PLAN_A.marketing[0])} – ${fmt(PLAN_A.marketing[1])}`, `${fmt(PLAN_B.marketing[0])} – ${fmt(PLAN_B.marketing[1])}`],
      ["Próg rentowności", `ok. ${num(breakEven(A.total + PLAN_A.marketing[0]))} egz.`, `ok. ${num(breakEven(B.total + PLAN_B.marketing[0]))} egz.`],
    ],
    [34, 33, 33]
  ),
  CAPTION("Próg rentowności policzony dla budżetu produkcyjnego powiększonego o dolny próg budżetu marketingowego, przy przychodzie netto 35 PLN na sprzedany egzemplarz."),
  H2("1.3 Dlaczego ten zespół"),
  BULB("Doświadczenie: ", "cztery lata pracy w gamedevie i jeden wydany komercyjnie tytuł w dorobku członków zespołu."),
  BULB("Najtrudniejszy element jest już zaadresowany: ", "prototyp zawiera działający system genomu stworka, proceduralny generator rigu i siatki kręgosłupa, IK nóg, profile chodu, deterministyczny generator losowy oraz kodek i walidator genomu. To fundament zarówno kreatora, jak i późniejszego dzielenia się stworkami przez Steam Workshop."),
  BULB("Architektura pod multiplayer od pierwszego dnia: ", "projekt jest zbudowany na FishNet z modelem serwer-autorytatywnym i predykcją po stronie klienta, z rozdzieleniem warstwy domenowej od warstwy Unity i sieci. Multiplayer nie jest doklejany na końcu, co jest najczęstszą przyczyną poślizgów w grach kooperacyjnych."),
  new Paragraph({ children: [new PageBreak()] }),
);

/* ---------- 2. WIZJA ---------- */
add(
  H1("2. Wizja produktu"),
  H2("2.1 Pitch"),
  P("„Zaprojektuj stworzenie. Zobacz, jak się przewraca. Popraw je. Przetrwaj razem ze znajomymi.”", { i: true }),
  P("Leeway jest grą o konsekwencjach projektowania. Gracz nie wybiera postaci z listy — buduje ją, a fizyka bezlitośnie weryfikuje jego pomysł. Ciężka głowa przechyla stworzenie do przodu. Zbyt cienkie nogi łamią się pod obciążeniem. Długi ogon stabilizuje skok, ale spowalnia obroty w ciasnych korytarzach. Każdy z tych efektów wynika z symulacji, a nie z ukrytej tabelki statystyk, dlatego gracze uczą się ich intuicyjnie i natychmiast chcą testować kolejne pomysły."),
  H2("2.2 Filary projektowe"),
  tbl(
    ["Filar", "Co oznacza w praktyce", "Czego świadomie nie robimy"],
    [
      ["Autorstwo", "Stworzenie gracza jest rozpoznawalne i niepowtarzalne; gracz mówi o nim „mój stworek”, nie „moja postać”.", "Nie dajemy gotowych bohaterów ani klas postaci."],
      ["Fizyka jako sędzia", "Statystyki wynikają z budowy ciała, a nie z suwaków. Symulacja jest źródłem prawdy.", "Nie ukrywamy wyniku za ekranem ładowania ani nie normalizujemy stworków do wspólnego szablonu."],
      ["Śmiech we czwórkę", "Porażka jest zabawna i warta nagrania. Największą wartością sesji jest wspólna historia.", "Nie karzemy graczy utratą wielogodzinnego postępu ani nie budujemy rozgrywki wymagającej precyzji."],
      ["Krótka sesja, długi ogon", "Sesja trwa 20–40 minut i domyka się. Progresja i kolekcja żyją między sesjami.", "Nie budujemy MMO, świata ciągłego ani gospodarki wymagającej serwerów."],
    ],
    [18, 47, 35]
  ),
  H2("2.3 Doprecyzowanie zakresu względem pierwotnej koncepcji"),
  P("Pierwotna koncepcja przewidywała wieloetapową strukturę wzorowaną na Spore, z osobnymi trybami kamery i rozgrywki dla kolejnych skal rozwoju cywilizacji. W toku prac zakres został świadomie zawężony do jednego, znacznie pogłębionego etapu — etapu stworzenia — rozbudowanego o pełną symulację fizyczną. Decyzja jest celowa i traktujemy ją jako kontraktowo zamrożoną."),
  BULB("Uzasadnienie ryzyka: ", "każdy dodatkowy etap to w praktyce osobna gra z własnym zestawem mechanik, interfejsu, sztucznej inteligencji i balansu. Gry wieloetapowe od małych zespołów niemal zawsze kończą się pięcioma płytkimi trybami zamiast jednego dobrego."),
  BULB("Uzasadnienie rynkowe: ", "wyróżnikiem Leeway jest kreator i fizyka, a nie liczba trybów. Gracze udostępniają klipy ze śmiesznymi stworkami, nie zrzuty ekranu z drzewka technologii."),
  BULB("Uzasadnienie produkcyjne: ", "jeden etap pozwala osiągnąć jakość wystarczającą do premiery w Early Access w rozsądnym czasie i finansować dalsze prace z przychodów."),
  new Paragraph({ children: [new PageBreak()] }),
);

/* ---------- 3. RYNEK ---------- */
add(
  H1("3. Rynek i pozycjonowanie"),
  H2("3.1 Grupa docelowa"),
  BULB("Rdzeń: ", "gracze w wieku 16–30 lat grający w grupach 2–4 znajomych, aktywni na Discordzie, oglądający i publikujący krótkie klipy wideo. To odbiorcy Lethal Company, Content Warning i REPO."),
  BULB("Druga grupa: ", "gracze przywiązani do Spore i do samego aktu tworzenia — kreatywni, gotowi spędzić godzinę w samym edytorze, bardzo aktywni w Steam Workshop."),
  BULB("Trzecia grupa: ", "widzowie twórców internetowych. Gra jest projektowana pod oglądalność: sylwetka stworka jest czytelna na miniaturze, a porażka jest wizualnie zabawna."),
  H2("3.2 Gry referencyjne"),
  tbl(
    ["Tytuł", "Co potwierdza", "Czego Leeway nie powtarza"],
    [
      ["Spore (2008)", "Popyt na swobodny kreator stworków utrzymuje się od kilkunastu lat, a sama społeczność kreatora przeżyła grę.", "Rozczłonkowanie na pięć płytkich etapów i brak trybu kooperacyjnego."],
      ["Lethal Company", "Kooperacja 1–4 osób z krótką sesją i wysoką wartością nagraniową potrafi zdominować Steam przy budżecie indie.", "Brak jakiejkolwiek personalizacji postaci i wynikający z tego ograniczony długi ogon."],
      ["Content Warning", "Mechanika nastawiona wprost na tworzenie materiału wideo działa jako motor marketingowy.", "Bardzo wąska pętla, szybko wyczerpująca się bez treści od graczy."],
      ["Totally Accurate Battle Simulator", "Fizyka jako źródło humoru sprzedaje się bez potrzeby realizmu i wysokiego budżetu artystycznego.", "Brak rozgrywki kooperacyjnej i progresji."],
      ["Valheim / Raft", "Model Early Access z regularnymi aktualizacjami buduje wieloletnią sprzedaż.", "Skala świata i długość sesji nieadekwatne do naszego zespołu."],
    ],
    [18, 44, 38]
  ),
  H2("3.3 Wyróżnik"),
  P("Żadna z gier referencyjnych nie łączy swobodnego kreatora postaci z kooperacyjną rozgrywką opartą na fizyce. Gry z kreatorem nie mają kooperacji, gry kooperacyjne nie mają kreatora. Leeway zajmuje to skrzyżowanie, a wsparcie dla Steam Workshop zamienia kreatywność graczy w bezpłatny, samopodtrzymujący się dopływ treści i widoczności."),
  H2("3.4 Ryzyko rynkowe"),
  P("Segment gier kooperacyjnych jest zatłoczony i rządzi się dynamiką hitów: większość tytułów nie przebija się, a nieliczne osiągają wyniki o dwa rzędy wielkości wyższe od średniej. Prognoza finansowa w rozdziale 11 uwzględnia to wprost, a próg rentowności jest tak dobrany, by wariant ostrożny nie oznaczał straty."),
  new Paragraph({ children: [new PageBreak()] }),
);

/* ---------- 4. ROZGRYWKA ---------- */
add(
  H1("4. Rozgrywka"),
  H2("4.1 Pętla rozgrywki"),
  P("Rozgrywka dzieli się na trzy zagnieżdżone pętle o różnej długości."),
  H3("Pętla sesji (20–40 minut)"),
  NUM("Gospodarz zakłada sesję, dołącza do niej od jednego do trzech znajomych."),
  NUM("Gracze wchodzą do kreatora i tworzą lub wybierają stworzenie z własnej kolekcji."),
  NUM("Drużyna ląduje na proceduralnie wygenerowanej mapie biomu z jasno określonym celem sesji."),
  NUM("Gracze eksplorują, zdobywają pożywienie i zasoby, unikają lub zwalczają miejscowe stworzenia."),
  NUM("Drużyna realizuje cel sesji i ewakuuje się albo ginie; zebrane materiały genetyczne trafiają do kolekcji."),
  H3("Pętla ewolucji (kilka sesji)"),
  P("Materiał genetyczny zdobyty w sesjach odblokowuje nowe części ciała, warianty i modyfikatory w kreatorze. Progresja dotyczy katalogu możliwości, a nie mocy liczbowej: gracz zyskuje więcej opcji projektowych, a nie automatycznie silniejszego stworka. Dzięki temu nowi gracze nigdy nie są bezużyteczni w drużynie weteranów, co jest warunkiem koniecznym dla gry kooperacyjnej rozprzestrzeniającej się w grupach znajomych."),
  H3("Pętla społecznościowa (długoterminowa)"),
  P("Gracze publikują swoje stworzenia w Steam Workshop, pobierają cudze, remiksują je i nagrywają klipy. Ta pętla jest najtańszym źródłem treści w całym projekcie i najskuteczniejszym kanałem marketingowym po premierze."),
  H2("4.2 Kreator stworków"),
  P("Kreator jest głównym wyróżnikiem produktu i największą inwestycją technologiczną. Zakładamy pełną swobodę projektowania, wzorowaną na Spore, a nie wybór spośród gotowych szkieletów."),
  BULB("Kręgosłup: ", "gracz rozciąga, wygina i skaluje kręgosłup złożony z segmentów; siatka ciała jest generowana proceduralnie wokół niego."),
  BULB("Części ciała: ", "kończyny, głowy, oczy, pyski, skrzydła, ogony i elementy ozdobne doklejane w dowolnym miejscu, z obsługą symetrii, skalowania i rotacji."),
  BULB("Rig proceduralny: ", "szkielet, ograniczenia stawów, łańcuchy IK i profile chodu powstają automatycznie na podstawie geometrii, jaką zbudował gracz."),
  BULB("Budżet genomu: ", "każda część kosztuje punkty z puli. Ogranicza to zarówno nadużycia w rozgrywce, jak i koszt obliczeniowy symulacji."),
  BULB("Statystyki wyprowadzane, nie wpisywane: ", "masa, środek ciężkości, zasięg kroku, prędkość, siła ugryzienia i wytrzymałość wynikają z budowy ciała."),
  BULB("Malowanie i materiały: ", "warstwy kolorów i wzorów nakładane na wygenerowaną siatkę."),
  BULB("Walidacja: ", "walidator genomu odrzuca konstrukcje niemożliwe do symulacji lub psujące rozgrywkę, zanim trafią do gry lub do Workshop."),
  P("Status: rdzeń tego systemu istnieje już w prototypie — zaimplementowane są genom, reguły i limity części, budżet genomu, generator rigu i siatki kręgosłupa, proceduralne nogi z IK, profile chodu, walidator oraz kodek serializacji genomu.", { i: true }),
  H2("4.3 Fizyka i animacja proceduralna"),
  P("Ruch stworzenia nie jest odtwarzany z gotowych animacji. Silnik chodu generuje kroki na podstawie długości i liczby kończyn, a ciało reaguje na nierówności terenu, kolizje i utratę równowagi. Zderzenia, upadki i próby wspinaczki są rozstrzygane przez symulację."),
  tbl(
    ["Element", "Rozwiązanie", "Znaczenie dla rozgrywki"],
    [
      ["Chód", "Proceduralny generator kroku sterowany profilem chodu wyliczonym z genomu", "Sylwetka stworka jest natychmiast rozpoznawalna po sposobie poruszania się"],
      ["Równowaga", "Kontrola środka ciężkości z reakcją na zbocza i pchnięcia", "Źle wyważone stworzenie realnie się przewraca — to rdzeń humoru w grze"],
      ["Kolizje i obrażenia", "Model wytrzymałości kończyn i uderzeń zależny od masy i prędkości", "Budowa ciała przekłada się wprost na przeżywalność"],
      ["Skalowanie kosztu", "Poziomy szczegółowości symulacji zależne od odległości i liczby aktorów", "Utrzymanie płynności przy czterech graczach i stadach stworzeń NPC"],
    ],
    [16, 44, 40]
  ),
  H2("4.4 Przetrwanie i progresja"),
  P("Warstwa przetrwania jest celowo lekka — ma dostarczać presji i powodów do wychodzenia w teren, a nie zarządzania tabelami zasobów."),
  BUL("Sytość i wytrzymałość jako główne liczniki; brak rozbudowanego zarządzania ekwipunkiem."),
  BUL("Pożywienie zależne od budowy ciała: roślinożerca, mięsożerca i wszystkożerca mają różne ścieżki zdobywania energii i różne ryzyka."),
  BUL("Zagrożenia środowiskowe: temperatura, teren, zjawiska pogodowe zależne od biomu."),
  BUL("Śmierć stworzenia kończy jego udział w sesji, ale nie kasuje jego projektu w kolekcji; drużyna może kontynuować."),
  BUL("Progresja międzysesyjna odblokowuje części i warianty w kreatorze, nie mnożniki obrażeń."),
  H2("4.5 Walka PvE"),
  P("Starcia z miejscowymi stworzeniami są rozstrzygane w tej samej symulacji fizycznej co ruch. Atak to fizyczne uderzenie, ugryzienie lub taranowanie, a jego skuteczność zależy od masy, prędkości i geometrii ciała. Sztuczna inteligencja przeciwników opiera się na prostych zachowaniach stadnych, terytorialnych i ucieczkowych; bossowie są projektowani jako zagadki przestrzenne wymagające współpracy czterech graczy, nie jako testy refleksu."),
  H2("4.6 Świat i generacja proceduralna"),
  P("Świat jest sesyjny: każda rozgrywka toczy się na wygenerowanej mapie biomu, dobranej pod cel sesji. Rozwiązanie to radykalnie obniża koszt produkcji treści i nakład pracy projektantów poziomów, jednocześnie zwiększając regrywalność."),
  BULB("Plan A — 4 biomy na wersję 1.0: ", "las, mokradła, pustynia skalna i strefa wulkaniczna, każdy z własną fauną, zagrożeniami i materiałem genetycznym."),
  BULB("Plan B — 2 biomy na wersję 1.0, ", "pozostałe dodawane po premierze jako bezpłatne aktualizacje."),
  BULB("Generacja: ", "mapy budowane z ręcznie zaprojektowanych modułów łączonych proceduralnie — utrzymuje jakość kompozycji przy zachowaniu losowości."),
  BULB("Ziarno generatora ", "jest współdzielone przez sieć, dzięki czemu wszyscy gracze widzą identyczny świat bez przesyłania geometrii."),
  H2("4.7 Kooperacja i architektura sieciowa"),
  P("Rozgrywka toczy się w modelu host-klient: jeden z graczy hostuje sesję przez usługi Steam, pozostali dołączają przez zaproszenie lub kod. Nie planujemy serwerów dedykowanych przed wersją 1.0 — przy sesji 2–4 graczy nie wnoszą one wartości proporcjonalnej do stałego kosztu utrzymania i etatu administracyjnego."),
  BULB("Autorytet: ", "serwer pozostaje źródłem prawdy dla stanu gry — zasobów, punktów życia, postępu i wyników interakcji."),
  BULB("Fizyka własnego stworzenia: ", "właściciel stworzenia ma autorytet nad jego symulacją, a serwer waliduje wynik pod kątem dopuszczalnych prędkości i pozycji. Pełna predykcja z rekoncyliacją dla całej symulacji ciała miękkiego jest niewykonalna w budżecie, a to rozwiązanie daje ten sam efekt odczuwalny dla gracza."),
  BULB("Predykcja po stronie klienta ", "obejmuje akcje krytyczne dla odczucia sterowania: ruch, skok, atak i interakcje."),
  BULB("Synchronizacja stworków: ", "przesyłany jest genom, a nie geometria; ciało jest odtwarzane lokalnie z tego samego opisu — kilkaset bajtów zamiast megabajtów siatki."),
  BULB("Tolerancja rozbieżności: ", "gra jest projektowana tak, by drobne różnice w symulacji między klientami nie wpływały na rozstrzygnięcia — nie ma tu rywalizacji między graczami, więc dokładność fizyki nie musi być absolutna."),
  H2("4.8 Treści tworzone przez graczy"),
  P("Wsparcie dla treści od graczy jest planowane w dwóch etapach i objęte zakresem wersji 1.0 w Planie A."),
  BULB("Etap 1 — dzielenie się stworkami (Early Access): ", "publikowanie i pobieranie genomów przez Steam Workshop, ocenianie, remiksowanie cudzych projektów. Genom jest już serializowalny, więc koszt wdrożenia jest umiarkowany."),
  BULB("Etap 2 — mody (wersja 1.0): ", "udokumentowane API pozwalające dodawać części ciała, przeciwniki i moduły map."),
  BULB("Moderacja: ", "narzędzia zgłaszania i filtrowania po stronie gry, oparte na mechanizmach Steam Workshop; walidator genomu odrzuca konstrukcje psujące rozgrywkę."),
  P("W Planie B etap 1 przesuwa się na premierę 1.0, a etap 2 poza zakres finansowania.", { i: true }),
  new Paragraph({ children: [new PageBreak()] }),
);

/* ---------- 5. ART I AUDIO ---------- */
add(
  H1("5. Kierunek artystyczny i audio"),
  H2("5.1 Styl wizualny"),
  P("Przyjmujemy styl stylizowany, zbliżony do kreskówkowego: czytelne sylwetki, nasycone kolory, uproszczone materiały, minimalna liczba detali na częściach ciała. Decyzja jest zarówno artystyczna, jak i ekonomiczna."),
  BULB("Koszt: ", "styl stylizowany jest około dwu- do trzykrotnie tańszy w produkcji od półrealistycznego i wymaga mniejszego zespołu graficznego."),
  BULB("Wymóg techniczny: ", "kreator generuje siatkę proceduralnie, a styl realistyczny wymagałby poprawnego zachowania skóry, sierści i deformacji dla dowolnej konfiguracji ciała — ryzyko nieakceptowalne w tym budżecie."),
  BULB("Czytelność: ", "stworki muszą być rozpoznawalne na miniaturze filmu i w ruchu, przy czterech postaciach na ekranie jednocześnie."),
  BULB("Trwałość: ", "stylizacja starzeje się wolniej, co ma znaczenie przy dwuletnim okresie Early Access."),
  H2("5.2 Audio"),
  P("Sound design realizowany jest wewnętrznie przez projektanta gry, który łączy tę rolę z prowadzeniem projektu. Rozwiązanie jest ekonomiczne, ale obarczone ryzykiem obciążenia jednej osoby trzema funkcjami — w rozdziale 12 traktujemy to jako zidentyfikowane ryzyko z zaplanowaną mitygacją."),
  BULB("Dźwięki stworzeń ", "są generowane proceduralnie z parametrów genomu — wysokość, barwa i długość wokalizy zależą od masy i budowy pyska, dzięki czemu każdy stworek brzmi inaczej bez ręcznego nagrywania tysięcy próbek."),
  BULB("Muzyka ", "zlecana kompozytorowi zewnętrznemu: ścieżka adaptacyjna reagująca na napięcie sesji. Budżet ujęty osobno w rozdziale 10."),
  new Paragraph({ children: [new PageBreak()] }),
);

/* ---------- 6. TECHNOLOGIA ---------- */
add(
  H1("6. Technologia"),
  H2("6.1 Stos technologiczny"),
  tbl(
    ["Technologia", "Wersja", "Rola w projekcie"],
    [
      ["Unity", "6000.4.7f1", "Silnik gry; wersja LTS z długim wsparciem, kluczowa przy dwuletnim Early Access"],
      ["Universal Render Pipeline", "17.4.0", "Renderowanie; niskie wymagania sprzętowe zwiększają zasięg gry"],
      ["FishNet", "4.7.2", "Warstwa sieciowa; model serwer-autorytatywny z predykcją po stronie klienta"],
      ["VContainer", "1.18.0", "Wstrzykiwanie zależności; brak singletonów ułatwia testowanie systemów"],
      ["MessagePipe", "—", "Komunikacja między niezależnymi systemami bez sztywnych powiązań"],
      ["UniTask", "—", "Operacje asynchroniczne bez narzutu korutyn Unity"],
      ["Cinemachine", "3.1.6", "System kamer dla trybu rozgrywki i kreatora"],
      ["Input System", "1.19.0", "Obsługa wejścia; gotowość pod pady i ewentualne porty konsolowe"],
      ["VFX Graph", "17.4.0", "Efekty wizualne"],
      ["AI Navigation", "2.0.12", "Nawigacja przeciwników po generowanych mapach"],
    ],
    [24, 16, 60]
  ),
  H2("6.2 Architektura"),
  P("Kod gry jest podzielony na moduły odpowiadające funkcjom produktu, z wydzieloną warstwą domenową niezależną od Unity i od warstwy sieciowej. Genom stworka, reguły części, limity, walidacja i kodek serializacji znajdują się w tej warstwie, co pozwala je testować jednostkowo bez uruchamiania silnika i ponownie wykorzystać po stronie narzędzi oraz Workshopu."),
  P("Projekt posiada już zautomatyzowaną dokumentację architektury — generowane diagramy zależności między modułami i diagramy klas — oraz zestawy testów w trybie edytora i w trybie gry. Dla wydawcy oznacza to mierzalną kontrolę nad długiem technicznym i realną możliwość rozszerzania zespołu bez utraty tempa."),
  H2("6.3 Największe ryzyka techniczne i sposób ich domknięcia"),
  tbl(
    ["Ryzyko", "Dlaczego jest poważne", "Jak je domykamy"],
    [
      ["Synchronizacja fizyki stworków w sieci", "Symulacja ciał wieloczłonowych nie jest deterministyczna między maszynami; klasyczna rekoncyliacja jest nieosiągalna w tym budżecie", "Autorytet lokalny nad własnym stworzeniem z walidacją po stronie serwera; rozgrywka zaprojektowana tak, by nie wymagała dokładności na poziomie klatki"],
      ["Wydajność przy czterech graczach i stadach NPC", "Każde stworzenie to osobny rig proceduralny i osobna symulacja", "Poziomy szczegółowości symulacji, budżet kości i części w genomie, przetwarzanie równoległe (Jobs/Burst), stały budżet klatki weryfikowany testami wydajności od pierwszego kamienia milowego"],
      ["Jakość ruchu dowolnie zbudowanego stworka", "Kreator dopuszcza konstrukcje, których nikt nie przewidział", "Walidator genomu, ograniczenia w budżecie części, biblioteka profili chodu i obowiązkowe testy na wygenerowanych losowo genomach"],
      ["Nadużycia w treściach od graczy", "Genomy publikowane w Workshop mogą psuć rozgrywkę lub wydajność", "Walidacja genomu przy imporcie, twarde limity, moderacja oparta o mechanizmy Steam"],
    ],
    [22, 36, 42]
  ),
  new Paragraph({ children: [new PageBreak()] }),
);

/* ---------- 7. ZAKRES A vs B ---------- */
add(
  H1("7. Zakres: Plan A i Plan B"),
  P("Poniższa tabela jest podstawą negocjacji zakresu. Plan B nie jest „gorszą wersją tej samej gry” — jest świadomym ograniczeniem objętości treści przy zachowaniu nienaruszonego rdzenia rozgrywki. Kreator i fizyka pozostają w obu wariantach, ponieważ bez nich produkt traci rację bytu."),
  tbl(
    ["Element", "Plan A (rekomendowany)", "Plan B (minimum)"],
    [
      ["Kreator stworków", "Pełna swoboda: dowolny kręgosłup, skalowanie, symetria, malowanie warstwowe", "Pełna swoboda, mniejszy katalog części i uproszczone malowanie"],
      ["Katalog części ciała na 1.0", "ok. 120 części", "ok. 50 części"],
      ["Fizyka i chód proceduralny", "Pełny zakres", "Pełny zakres — element nienegocjowalny"],
      ["Biomy na 1.0", "4", "2"],
      ["Typy przeciwników na 1.0", "ok. 20", "ok. 10"],
      ["Bossowie", "4 starcia kooperacyjne", "1 starcie kooperacyjne"],
      ["Tryb solo", "Pełne wsparcie z balansem dla jednego gracza", "Obsługiwany, balans nastawiony na drużynę"],
      ["Steam Workshop (stworki)", "Wdrożenie w Early Access", "Wdrożenie dopiero na 1.0"],
      ["Mody i publiczne API", "W zakresie wersji 1.0", "Poza zakresem — dopiero po 1.0"],
      ["Lokalizacja", "10 języków", "4 języki"],
      ["Serwery dedykowane", "Opcja rozważana po 1.0", "Poza zakresem"],
      ["Konsole", "Osobna wycena, decyzja wydawcy", "Poza zakresem"],
      ["Okno Early Access", "12 miesięcy, 3 duże aktualizacje sezonowe", "9 miesięcy, 2 aktualizacje"],
    ],
    [26, 39, 35]
  ),
  H2("7.1 Rekomendacja"),
  P(`Rekomendujemy Plan A. Gry kooperacyjne zarabiają w długim ogonie, a długi ogon budują treści od graczy i regularne aktualizacje. Plan B ogranicza jedno i drugie: przesuwa Workshop poza okres Early Access i wycina mody, czyli dokładnie te elementy, które w grach referencyjnych odpowiadają za utrzymanie sprzedaży w drugim i trzecim roku. Różnica w budżecie produkcyjnym wynosi ${fmtM(A.total - B.total)}, co odpowiada około ${num(breakEven(A.total - B.total))} sprzedanym egzemplarzom.`),
  new Paragraph({ children: [new PageBreak()] }),
);

/* ---------- 8. ZESPÓŁ ---------- */
add(
  H1("8. Zapotrzebowanie kadrowe"),
  H2("8.1 Zespół obecny"),
  P("Projekt prowadzą trzy osoby, które odpowiadają za obecny prototyp technologiczny:"),
  BULB("Programista gameplay i sieci — ", "mechaniki rozgrywki oraz warstwa sieciowa oparta na FishNet."),
  BULB("Artysta 3D / tech artist — ", "styl wizualny, rigging, shadery, pipeline generowania siatek."),
  BULB("Projektant gry i producent — ", "projekt rozgrywki, balans, prowadzenie projektu; docelowo również sound design."),
  P("Członkowie zespołu mają za sobą cztery lata pracy w branży gier oraz udział w jednym wydanym komercyjnie tytule.", { i: true }),
  H2("8.2 Zespół docelowy — Plan A"),
  tbl(
    ["Rola", "Od m-ca", "Do m-ca", "Mies.", "Koszt / mies."],
    A.rows.map(r => [r.name, String(r.from), String(r.to), String(r.months), fmt(r.monthly)]),
    [46, 11, 11, 10, 22],
    { numFrom: 1 }
  ),
  CAPTION("Koszt miesięczny to pełny koszt zleceniodawcy przy umowie zleceniu z pełnym ubezpieczeniem społecznym (wynagrodzenie brutto powiększone o 20,48% narzutu)."),
  H2("8.3 Zespół docelowy — Plan B"),
  tbl(
    ["Rola", "Od m-ca", "Do m-ca", "Mies.", "Koszt / mies."],
    B.rows.map(r => [r.name, String(r.from), String(r.to), String(r.months), fmt(r.monthly)]),
    [46, 11, 11, 10, 22],
    { numFrom: 1 }
  ),
  H2("8.4 Uwagi do planu zatrudnienia"),
  BULB("Zatrudnianie etapami. ", "Zespół rośnie wraz z projektem. Zatrudnianie wszystkich od pierwszego miesiąca podniosłoby budżet o kilkaset tysięcy złotych bez przyspieszenia prac — w fazie przedprodukcyjnej wąskim gardłem są decyzje projektowe, nie liczba rąk."),
  BULB("Kluczowa rekrutacja. ", "Starszy programista odpowiedzialny za kreator, animację proceduralną i fizykę jest najtrudniejszą do obsadzenia rolą w całym projekcie. Rekrutację rozpoczynamy natychmiast po podpisaniu umowy; harmonogram zakłada jej domknięcie do 3. miesiąca (Plan A) lub 2. miesiąca (Plan B)."),
  BULB("Outsourcing. ", "Część grafiki trójwymiarowej oraz materiały marketingowe realizowane są przez wykonawców zewnętrznych. Pozwala to skalować produkcję treści bez zwiększania stałych kosztów zespołu."),
  BULB("Ryzyko kumulacji ról. ", "Projektant gry łączy trzy funkcje: projektowanie, produkcję i sound design. W Planie A zakładamy, że po wejściu w Early Access część obowiązków produkcyjnych przejmuje wydawca lub zewnętrzny producent, a sound design zostaje częściowo zlecony na zewnątrz."),
  new Paragraph({ children: [new PageBreak()] }),
);

/* ---------- 9. HARMONOGRAM ---------- */
add(
  H1("9. Harmonogram i kamienie milowe"),
  P("Harmonogram jest zbudowany wokół kamieni milowych rozliczanych weryfikowalnym rezultatem — grywalną kompilacją lub pomiarem — a nie deklaracją postępu. Każdy kamień milowy jest naturalnym punktem kontrolnym dla wydawcy i punktem decyzyjnym o kontynuacji finansowania."),
  H2("9.1 Plan A — 28 miesięcy"),
  H3("Faza 0: Przedprodukcja i domknięcie ryzyka technicznego (miesiące 1–4)"),
  tbl(
    ["Kamień", "M-c", "Rezultat weryfikowalny"],
    [
      ["KM 1", "2", "Dowód wykonalności fizyki: stworzenie z losowo wygenerowanego genomu porusza się stabilnie po nierównym terenie, przewraca się przy złym wyważeniu i wstaje"],
      ["KM 2", "4", "Dowód wykonalności sieci: czterech graczy z własnymi stworkami w jednej sesji, pomiar stabilności przy opóźnieniu 150 ms; decyzja o przejściu dalej albo o korekcie modelu autorytetu"],
    ],
    [10, 8, 82], { numFrom: 1 }
  ),
  H3("Faza 1: Vertical slice (miesiące 5–10)"),
  tbl(
    ["Kamień", "M-c", "Rezultat weryfikowalny"],
    [
      ["KM 3", "7", "Kreator w pierwszej pełnej wersji: budowa, zapis, wczytanie i przesłanie stworka przez sieć; katalog 40 części"],
      ["KM 4", "9", "Pełna pętla sesji na jednym biomie: cel, przetrwanie, zdobywanie materiału genetycznego, ewakuacja"],
      ["KM 5", "10", "Vertical slice — 30 minut rozgrywki w jakości docelowej. Uruchomienie strony na Steam, publikacja pierwszego zwiastuna, start zbierania listy życzeń"],
    ],
    [10, 8, 82], { numFrom: 1 }
  ),
  H3("Faza 2: Produkcja do Early Access (miesiące 11–15)"),
  tbl(
    ["Kamień", "M-c", "Rezultat weryfikowalny"],
    [
      ["KM 6", "12", "Walka PvE i sztuczna inteligencja przeciwników; drugi biom; progresja międzysesyjna"],
      ["KM 7", "14", "Integracja Steam Workshop dla stworków; demo na festiwal Steam Next Fest; zewnętrzny playtest z udziałem graczy spoza zespołu"],
      ["KM 8", "15", "Kandydat do wydania: optymalizacja, przejście testów QA, lokalizacja, materiały sklepowe"],
    ],
    [10, 8, 82], { numFrom: 1 }
  ),
  rich([["Miesiąc 16 — PREMIERA EARLY ACCESS", true]], { after: 160 }),
  H3("Faza 3: Early Access (miesiące 16–27)"),
  tbl(
    ["Okres", "M-ce", "Zawartość"],
    [
      ["Stabilizacja", "16–18", "Intensywne patchowanie na podstawie zgłoszeń, poprawki balansu, korekta krzywej wejścia dla nowych graczy"],
      ["Aktualizacja sezonowa 1", "19–21", "Trzeci biom, rozbudowa katalogu części, pierwsze starcie z bossem"],
      ["Aktualizacja sezonowa 2", "22–24", "Czwarty biom, kolejni przeciwnicy, narzędzia społecznościowe wokół Workshopu"],
      ["Aktualizacja sezonowa 3", "25–27", "Publiczne API modów, pozostałe starcia z bossami, pełny przegląd jakości przed 1.0"],
    ],
    [24, 12, 64], { numFrom: 1 }
  ),
  rich([["Miesiąc 28 — PREMIERA WERSJI 1.0", true]], { after: 160 }),
  H3("Faza 4: Wsparcie po premierze (miesiące 29–34, poza budżetem podstawowym)"),
  BUL("Poprawki i patche stabilizacyjne przez pierwsze trzy miesiące po premierze 1.0."),
  BUL("Kosmetyczne zestawy części jako opcjonalne, płatne dodatki."),
  BUL("Porty konsolowe jako osobny projekt, jeśli wydawca podejmie taką decyzję."),
  BUL("Zespół zredukowany do rdzenia; koszt szacowany na 150 000 – 200 000 PLN miesięcznie przy pełnej obsadzie lub 70 000 – 90 000 PLN przy zespole utrzymaniowym."),
  H2("9.2 Plan B — 20 miesięcy"),
  tbl(
    ["Faza", "M-ce", "Zakres i rezultat"],
    [
      ["Przedprodukcja", "1–3", "Domknięcie ryzyka fizyki i sieci w jednym połączonym kamieniu milowym w 3. miesiącu"],
      ["Vertical slice", "4–7", "Kreator, pełna pętla sesji na jednym biomie, strona na Steam i pierwszy zwiastun w 7. miesiącu"],
      ["Produkcja do Early Access", "8–10", "Walka PvE, drugi biom, progresja, optymalizacja, kandydat do wydania"],
      ["PREMIERA EARLY ACCESS", "11", "Wejście na rynek z węższym zakresem niż w Planie A"],
      ["Okno Early Access", "11–19", "Stabilizacja oraz dwie aktualizacje: przeciwnicy i boss, następnie Steam Workshop"],
      ["PREMIERA 1.0", "20", "Domknięcie zakresu; mody i kolejne biomy przeniesione poza budżet"],
    ],
    [26, 10, 64], { numFrom: 1 }
  ),
  P("Plan B osiąga premierę wcześniej i taniej, ale wchodzi w Early Access z materiałem na około 9 miesięcy aktualizacji zamiast 12, bez Workshopu w momencie startu. Oznacza to słabszy efekt wirusowy w kluczowych pierwszych tygodniach — a to właśnie one decydują o wyniku finansowym gry kooperacyjnej.", { i: true }),
  new Paragraph({ children: [new PageBreak()] }),
);

/* ---------- 10. BUDŻET ---------- */
const budgetSection = (label, plan, c) => [
  H2(label),
  H3("Koszty osobowe"),
  tbl(
    ["Rola", "Mies.", "Koszt / mies.", "Razem"],
    [
      ...c.rows.map(r => [r.name, String(r.months), fmt(r.monthly), fmt(r.total)]),
      { cells: ["RAZEM KOSZTY OSOBOWE", "", "", fmt(c.personnel)], bold: true, bg: LIGHT },
    ],
    [50, 10, 19, 21], { numFrom: 1 }
  ),
  H3("Koszty pozostałe"),
  tbl(
    ["Pozycja", "Kwota", "Uwagi"],
    [
      ...plan.other.map(([n, v, note]) => [n, fmt(v), note]),
      { cells: ["RAZEM KOSZTY POZOSTAŁE", fmt(c.otherSum), ""], bold: true, bg: LIGHT },
    ],
    [33, 16, 51], { numFrom: 1, numTo: 1 }
  ),
  H3("Podsumowanie"),
  tbl(
    ["Pozycja", "Kwota"],
    [
      ["Koszty osobowe", fmt(c.personnel)],
      ["Koszty pozostałe", fmt(c.otherSum)],
      ["Suma bezpośrednia", fmt(c.base)],
      [`Rezerwa na ryzyko (${Math.round(plan.reservePct * 100)}%)`, fmt(c.reserve)],
      { cells: ["BUDŻET PRODUKCYJNY RAZEM", fmt(c.total)], bold: true, bg: LIGHT },
      [`Budżet marketingowy (osobno, po stronie wydawcy)`, `${fmt(plan.marketing[0])} – ${fmt(plan.marketing[1])}`],
      { cells: ["ŁĄCZNE ZAANGAŻOWANIE WYDAWCY", `${fmt(c.total + plan.marketing[0])} – ${fmt(c.total + plan.marketing[1])}`], bold: true, bg: LIGHT },
      ["Średni koszt miesięczny produkcji", fmt(Math.round(c.total / plan.months / 1000) * 1000)],
    ],
    [58, 42], { numFrom: 1 }
  ),
];

add(
  H1("10. Budżet"),
  H2("10.1 Założenia"),
  BULB("Forma zatrudnienia: ", "umowa zlecenie z pełnym ubezpieczeniem społecznym. Pełny koszt zleceniodawcy to wynagrodzenie brutto powiększone o 20,48% (składki emerytalna, rentowa, wypadkowa, Fundusz Pracy i FGŚP)."),
  BULB("Stawki: ", "odpowiadają rynkowi polskiemu dla ról gamedevowych na poziomie średnim i starszym, w warunkach pracy zdalnej."),
  BULB("Waluta: ", "wszystkie kwoty w złotych, w wartościach z chwili sporządzenia dokumentu, bez indeksacji o inflację. Przy Planie A zalecamy wpisanie do umowy corocznej waloryzacji stawek o 5%."),
  BULB("Rezerwa na ryzyko: ", "12% w Planie A i 10% w Planie B. Pozycja przeznaczona na poślizgi rekrutacyjne, przedłużenie kamieni milowych i nieprzewidziane koszty licencji."),
  BULB("Marketing: ", "wykazany osobno, ponieważ w większości umów wydawniczych pozostaje po stronie wydawcy i nie wchodzi do budżetu deweloperskiego."),
  BULB("Wyłączenia: ", "budżet nie obejmuje portów konsolowych, certyfikacji platformowych, serwerów dedykowanych ani wsparcia po wersji 1.0. Pozycje te opisano osobno w rozdziałach 13 i 14."),
  ...budgetSection("10.2 Plan A — budżet szczegółowy", PLAN_A, A),
  new Paragraph({ children: [new PageBreak()] }),
  ...budgetSection("10.3 Plan B — budżet szczegółowy", PLAN_B, B),
  H2("10.4 Porównanie"),
  tbl(
    ["Pozycja", "Plan A", "Plan B", "Różnica"],
    [
      ["Czas trwania", `${PLAN_A.months} mies.`, `${PLAN_B.months} mies.`, `${PLAN_A.months - PLAN_B.months} mies.`],
      ["Nakład pracy", `${Math.round(A.fte)} os.-mies.`, `${Math.round(B.fte)} os.-mies.`, `${Math.round(A.fte - B.fte)} os.-mies.`],
      ["Koszty osobowe", fmt(A.personnel), fmt(B.personnel), fmt(A.personnel - B.personnel)],
      ["Koszty pozostałe", fmt(A.otherSum), fmt(B.otherSum), fmt(A.otherSum - B.otherSum)],
      ["Rezerwa", fmt(A.reserve), fmt(B.reserve), fmt(A.reserve - B.reserve)],
      { cells: ["BUDŻET PRODUKCYJNY", fmt(A.total), fmt(B.total), fmt(A.total - B.total)], bold: true, bg: LIGHT },
    ],
    [28, 24, 24, 24], { numFrom: 1 }
  ),
  new Paragraph({ children: [new PageBreak()] }),
);

/* ---------- 11. PROGNOZA ---------- */
add(
  H1("11. Prognoza przychodów i próg rentowności"),
  H2("11.1 Założenia"),
  BULB("Cena: ", "69 PLN w Early Access, 89 PLN po premierze wersji 1.0."),
  BULB("Efektywna cena sprzedaży: ", "około 58 PLN brutto po uwzględnieniu obniżek sezonowych i regionalnych różnic cenowych."),
  BULB("Przychód netto studia: ", `około ${NET_PER_UNIT} PLN na egzemplarz, po odliczeniu podatku od towarów i usług (średnio 19%) oraz prowizji platformy Steam (30%).`),
  BULB("Horyzont: ", "24 miesiące od premiery w Early Access."),
  BULB("Podział z wydawcą: ", "nieuwzględniony — kwoty są przychodem przed rozliczeniem z wydawcą i przed zwrotem nakładów."),
  H2("11.2 Scenariusze"),
  tbl(
    ["Scenariusz", "Sprzedaż (24 mies.)", "Przychód netto", "Opis"],
    SCEN.map(([n, u, d]) => [n, num(u) + " egz.", fmtM(u * NET_PER_UNIT), d]),
    [16, 16, 16, 52], { numFrom: 1, numTo: 2 }
  ),
  CAPTION("Przychód netto oznacza kwotę wpływającą do projektu po prowizji platformy i podatku, przed podziałem z wydawcą i przed zwrotem nakładów."),
  H2("11.3 Próg rentowności"),
  tbl(
    ["Wariant", "Zaangażowanie wydawcy", "Próg rentowności", "Scenariusz pokrywający"],
    [
      ["Plan A", fmt(A.total + PLAN_A.marketing[0]), `ok. ${num(breakEven(A.total + PLAN_A.marketing[0]))} egz.`, "Między ostrożnym a bazowym"],
      ["Plan B", fmt(B.total + PLAN_B.marketing[0]), `ok. ${num(breakEven(B.total + PLAN_B.marketing[0]))} egz.`, "Powyżej ostrożnego"],
    ],
    [14, 26, 24, 36], { numFrom: 1, numTo: 2 }
  ),
  H2("11.4 Ograniczenie ekspozycji wydawcy"),
  P("Model Early Access istotnie zmniejsza realne ryzyko wydawcy w stosunku do kwoty nominalnej budżetu. W Planie A sprzedaż rusza w 16. miesiącu, a więc na 12 miesięcy przed końcem finansowanego okresu. Przychody z tego okna pokrywają część kosztów fazy 3, przez co maksymalne zaangażowanie kapitałowe wydawcy w dowolnym momencie jest niższe niż suma budżetu."),
  P("Dodatkowo premiera Early Access jest pierwszym twardym punktem decyzyjnym opartym na danych rynkowych, a nie na prognozie. Jeśli wynik pierwszych tygodni okaże się wyraźnie poniżej scenariusza ostrożnego, dalszy zakres można skrócić do wariantu zbliżonego do Planu B, ograniczając stratę."),
  new Paragraph({ children: [new PageBreak()] }),
);

/* ---------- 12. RYZYKA ---------- */
add(
  H1("12. Rejestr ryzyk"),
  tbl(
    ["Ryzyko", "Wpływ", "Praw.", "Mitygacja"],
    [
      ["Symulacja fizyczna stworków nie daje się zsynchronizować w sieci w akceptowalnej jakości", "Krytyczny", "Średnie", "Kamień milowy 2 w 4. miesiącu jest twardym punktem decyzyjnym; w razie niepowodzenia przechodzimy na model z autorytetem lokalnym i luźniejszą walidacją serwerową, co jest już zaplanowane jako wariant zapasowy"],
      ["Spadek wydajności przy czterech graczach i stadach przeciwników", "Wysoki", "Wysokie", "Budżet klatki ustalony i mierzony automatycznie od pierwszego kamienia milowego; poziomy szczegółowości symulacji; twarde limity w budżecie genomu"],
      ["Rozszerzanie zakresu w stronę kolejnych etapów wzorowanych na Spore", "Wysoki", "Wysokie", "Zakres jednego etapu zamrożony w umowie; wszystkie propozycje spoza zakresu trafiają na listę po wersji 1.0"],
      ["Nieudana rekrutacja starszego programisty odpowiedzialnego za kreator i fizykę", "Wysoki", "Średnie", "Rekrutacja rozpoczęta natychmiast po podpisaniu umowy; rezerwa budżetowa pozwala podnieść stawkę; wariant awaryjny w postaci kontraktora na okres przedprodukcji"],
      ["Brak jakiejkolwiek obecności marketingowej w chwili startu", "Wysoki", "Pewne", "Strona na Steam uruchamiana przy kamieniu milowym 5; publikowanie materiałów z prac w mediach społecznościowych od 5. miesiąca; Community Manager od 10. miesiąca; udział w Steam Next Fest przed premierą Early Access"],
      ["Słabe przyjęcie premiery Early Access", "Krytyczny", "Średnie", "Demo i zewnętrzne playtesty przed premierą; punkt decyzyjny o skróceniu zakresu po pierwszym miesiącu sprzedaży"],
      ["Kumulacja trzech ról na jednej osobie (projekt, produkcja, sound design)", "Średni", "Wysokie", "Częściowe zlecenie sound designu na zewnątrz po wejściu w Early Access; przejęcie części obowiązków produkcyjnych przez wydawcę"],
      ["Mały zespół i wysoka zależność od pojedynczych osób", "Wysoki", "Średnie", "Utrzymywana dokumentacja architektury, przeglądy kodu, testy automatyczne, rozdzielenie warstwy domenowej od silnika"],
      ["Nieodpowiednie treści publikowane przez graczy w Steam Workshop", "Średni", "Wysokie", "Walidacja genomu przy imporcie, twarde limity, zgłaszanie i moderacja oparte o mechanizmy platformy"],
      ["Zmiana warunków licencjonowania silnika Unity", "Średni", "Niskie", "Wersja LTS z długim wsparciem; warstwa domenowa niezależna od silnika ogranicza koszt ewentualnej migracji"],
      ["Konkurencja wypuszcza grę o zbliżonej koncepcji", "Średni", "Średnie", "Skrócenie drogi do Early Access; przewaga oparta na jakości kreatora, którego nie da się skopiować szybko"],
    ],
    [26, 10, 9, 55], { numFrom: 1, numTo: 2 }
  ),
  new Paragraph({ children: [new PageBreak()] }),
);

/* ---------- 13. PO PREMIERZE ---------- */
add(
  H1("13. Plan po premierze"),
  H2("13.1 Okres Early Access"),
  P("Okres Early Access nie jest przerwą w produkcji, lecz jej najintensywniejszą fazą. Planujemy stały rytm: poprawki co tydzień lub dwa w pierwszych trzech miesiącach, następnie duża aktualizacja treści co trzy miesiące. Rytm jest ważniejszy od objętości pojedynczej aktualizacji — algorytmy widoczności Steam nagradzają regularność, a społeczność ocenia studio po przewidywalności, nie po rozmiarze łatek."),
  H2("13.2 Wsparcie po wersji 1.0"),
  tbl(
    ["Okres", "Zakres", "Szacunkowy koszt miesięczny"],
    [
      ["Miesiące 1–3 po 1.0", "Intensywne poprawki, reakcja na zgłoszenia, korekty balansu. Pełny zespół pozostaje w gotowości.", "150 000 – 200 000 PLN"],
      ["Miesiące 4–12 po 1.0", "Utrzymanie, mniejsze aktualizacje treści, wsparcie społeczności modderskiej. Zespół zredukowany do rdzenia.", "70 000 – 90 000 PLN"],
      ["Powyżej 12 miesięcy", "Opcjonalne: płatne dodatki kosmetyczne, porty konsolowe, serwery dedykowane. Decyzja zależna od wyników sprzedaży.", "Do ustalenia osobno"],
    ],
    [20, 56, 24], { numFrom: 2, numTo: 2 }
  ),
  P("Rekomendujemy zabezpieczenie w umowie z wydawcą środków na co najmniej trzy miesiące wsparcia po premierze 1.0. Brak takiego zabezpieczenia jest jedną z najczęstszych przyczyn utraty wartości gry tuż po premierze: zespół zostaje rozwiązany dokładnie w chwili, gdy zgłoszeń jest najwięcej."),
  H2("13.3 Dodatkowe źródła przychodu"),
  BULB("Kosmetyczne zestawy części ciała ", "jako opcjonalne, płatne dodatki — bez wpływu na rozgrywkę i bez podziału społeczności. Pierwszy zestaw nie wcześniej niż trzy miesiące po premierze 1.0."),
  BULB("Duży dodatek fabularny ", "z nowym biomem i zestawem mechanik — rozważany dopiero przy scenariuszu bazowym lub lepszym."),
  BULB("Porty konsolowe ", "jako osobny projekt opisany w rozdziale 14."),
  P("Nie planujemy mikropłatności wpływających na rozgrywkę, przepustek sezonowych ani żadnej formy monetyzacji naruszającej zaufanie graczy w grze kooperacyjnej dla znajomych.", { b: true }),
);

/* ---------- 14. KONSOLE ---------- */
add(
  H2("14. Konsole — opcja do decyzji wydawcy"),
  P("Porty konsolowe traktujemy jako osobny projekt uruchamiany po premierze wersji 1.0 na PC. Silnik i system wejścia są od początku konfigurowane z myślą o obsłudze kontrolera, co obniża późniejszy koszt konwersji."),
  tbl(
    ["Pozycja", "Szacunek"],
    [
      ["Czas realizacji", "6–9 miesięcy od decyzji, równolegle ze wsparciem wersji PC"],
      ["Zespół", "2–3 osoby: programista portowania, tech artist, QA"],
      ["Koszt", "450 000 – 700 000 PLN na dwie platformy"],
      ["Główne wyzwania", "Budżet wydajnościowy symulacji fizycznej na sprzęcie konsolowym, przeprojektowanie interfejsu kreatora pod kontroler, wymogi certyfikacyjne dla treści tworzonych przez graczy"],
      ["Warunek uruchomienia", "Sprzedaż na PC na poziomie scenariusza bazowego lub wyższym"],
    ],
    [24, 76]
  ),
  P("Największym ryzykiem portu nie jest wydajność, lecz interfejs kreatora. Swobodne projektowanie stworka jest naturalne przy myszy i wymaga osobnego projektu obsługi na kontrolerze — tę pracę wyceniamy jako część powyższego budżetu, nie jako drobną adaptację.", { i: true }),
  new Paragraph({ children: [new PageBreak()] }),
);

/* ---------- 15. OCZEKIWANIA ---------- */
add(
  H1("15. Czego oczekujemy od wydawcy"),
  H2("15.1 Zakres współpracy"),
  BULB("Pełne finansowanie produkcji ", `zgodnie z jednym z przedstawionych wariantów: ${fmt(A.total)} dla Planu A lub ${fmt(B.total)} dla Planu B, wypłacane w transzach powiązanych z kamieniami milowymi.`),
  BULB("Budżet i realizacja marketingu ", "wykazane osobno, po stronie wydawcy: zwiastuny, grafiki sklepowe, obsługa prasy i twórców internetowych, obecność na festiwalach."),
  BULB("Wsparcie produkcyjne ", "po wejściu w Early Access — odciążenie projektanta gry z części obowiązków producenckich."),
  BULB("Obsługa platform ", "— relacje z Valve, a w przypadku decyzji o portach również z producentami konsol i procesy certyfikacyjne."),
  BULB("Środki na wsparcie po premierze ", "wersji 1.0 zabezpieczone w umowie na co najmniej trzy miesiące."),
  H2("15.2 Punkty do uzgodnienia"),
  BUL("Podział przychodów oraz kolejność i zasady zwrotu nakładów."),
  BUL("Prawa własności intelektualnej — zespół oczekuje zachowania praw do marki i technologii kreatora."),
  BUL("Zakres zamrożony: jeden etap rozgrywki. Rozszerzenia poza ten zakres wymagają aneksu i dodatkowego budżetu."),
  BUL("Punkty decyzyjne o kontynuacji finansowania powiązane z kamieniami milowymi 2, 5 oraz z pierwszym miesiącem sprzedaży w Early Access."),
  BUL("Waloryzacja stawek o 5% rocznie przy Planie A."),
  BUL("Zasady rozliczenia i uruchamiania rezerwy na ryzyko."),
  H2("15.3 Status i następne kroki"),
  tbl(
    ["Krok", "Termin", "Odpowiedzialny"],
    [
      ["Prezentacja dokumentu i rozmowa o zakresie", "—", "Zespół"],
      ["Udostępnienie działającej kompilacji prototypu kreatora", "do 2 tygodni", "Zespół"],
      ["Uzgodnienie wariantu (Plan A lub Plan B) i harmonogramu transz", "—", "Wspólnie"],
      ["Warunki brzegowe umowy", "—", "Wydawca"],
      ["Start rekrutacji starszego programisty (kreator i fizyka)", "dzień podpisania umowy", "Zespół"],
      ["Kamień milowy 1 — dowód wykonalności fizyki", "2. miesiąc od startu", "Zespół"],
    ],
    [50, 26, 24]
  ),
  new Paragraph({ children: [new PageBreak()] }),
);

/* ---------- ZAŁĄCZNIK ---------- */
add(
  H1("Załącznik A. Założenia przyjęte do wyliczeń"),
  P("Dokument jest szkicem roboczym w wersji 0.9. Poniżej zebrano wszystkie założenia, które wpływają na liczby — każde z nich podlega weryfikacji i negocjacji."),
  tbl(
    ["Założenie", "Przyjęta wartość", "Uwagi"],
    [
      ["Narzut na wynagrodzenie (umowa zlecenie, pełny ZUS)", "20,48%", "Składki emerytalna 9,76%, rentowa 6,50%, wypadkowa 1,67%, Fundusz Pracy 2,45%, FGŚP 0,10%"],
      ["Wynagrodzenie brutto — starszy programista", "19 000 – 20 000 PLN / mies.", "Rynek polski, praca zdalna"],
      ["Wynagrodzenie brutto — programista i tech artist", "15 000 – 16 000 PLN / mies.", "Poziom średni i starszy"],
      ["Wynagrodzenie brutto — artysta 3D i content designer", "12 000 PLN / mies.", "Poziom średni"],
      ["Wynagrodzenie brutto — QA", "8 000 PLN / mies.", "Poziom średni"],
      ["Koszt stanowiska pracy", "13 000 PLN", "Komputer, monitory, peryferia; jednorazowo"],
      ["Prowizja platformy Steam", "30%", "Bez uwzględnienia progów obniżających prowizję"],
      ["Podatek od towarów i usług", "19% średnio", "Wartość ważona strukturą rynków"],
      ["Przychód netto na egzemplarz", `${NET_PER_UNIT} PLN`, "Przy efektywnej cenie sprzedaży około 58 PLN brutto"],
      ["Rezerwa na ryzyko", "12% (Plan A), 10% (Plan B)", "Uruchamiana na zasadach do uzgodnienia z wydawcą"],
      ["Waloryzacja wynagrodzeń", "nieuwzględniona", "Przy Planie A zalecane 5% rocznie"],
    ],
    [30, 22, 48]
  ),
  SPACER(240),
  P("Wszystkie kwoty podano w złotych, w wartościach z września 2026 roku. Dokument nie stanowi oferty w rozumieniu przepisów prawa handlowego.", { i: true, size: 18, color: GREY }),
);

/* ============================ SKŁAD DOKUMENTU ============================ */

const doc = new Document({
  creator: "Leeway",
  title: "Leeway — dokument projektowy i biznesplan produkcji",
  description: "Szkic GDD i biznesplanu dla inwestora / wydawcy",
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
      page: {
        margin: {
          top: convertInchesToTwip(0.85), bottom: convertInchesToTwip(0.85),
          left: convertInchesToTwip(0.85), right: convertInchesToTwip(0.85),
        },
      },
    },
    footers: {
      default: new Footer({
        children: [new Paragraph({
          alignment: AlignmentType.CENTER,
          children: [
            new TextRun({ text: "Leeway — dokument projektowy i biznesplan produkcji   |   wersja 0.9   |   str. ", size: 16, color: GREY }),
            new TextRun({ children: [PageNumber.CURRENT], size: 16, color: GREY }),
          ],
        })],
      }),
    },
    children: body,
  }],
});

const out = process.env.OUT || "/workspace/Docs/GDD/Leeway_GDD_Inwestor.docx";
const buf = await Packer.toBuffer(doc);
writeFileSync(out, buf);
console.log("OK ->", out, (buf.length / 1024).toFixed(0) + " KB");
console.log("Plan A: budżet", fmt(A.total), "| osobomiesiące", Math.round(A.fte), "| break-even", breakEven(A.total + PLAN_A.marketing[0]));
console.log("Plan B: budżet", fmt(B.total), "| osobomiesiące", Math.round(B.fte), "| break-even", breakEven(B.total + PLAN_B.marketing[0]));
