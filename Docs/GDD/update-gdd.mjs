/**
 * Aktualizuje Leeway_GDD.docx (wersja zespołu z 15.09).
 * Edycja w miejscu: zachowuje obrazy, czcionki, style i numerowanie oryginału.
 * Nie przywraca niczego, co zespół usunął. Sekcji 5.4 nie dotyka.
 */
import JSZip from "./node_modules/jszip/lib/index.js";
import { readFileSync, writeFileSync } from "fs";

const IN  = process.env.IN  || "Leeway_GDD.docx";
const OUT = process.env.OUT || "Leeway_GDD_v2.docx";

/* ------------------------- pomocniki w stylu dokumentu ------------------------- */

const esc = (s) => String(s).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");

const run = (t, { b = false, i = false } = {}) =>
  `<w:r><w:rPr>${b ? '<w:b w:val="1"/><w:bCs w:val="1"/>' : ""}${i ? '<w:i w:val="1"/><w:iCs w:val="1"/>' : ""}<w:rtl w:val="0"/></w:rPr>` +
  `<w:t xml:space="preserve">${esc(t)}</w:t></w:r>`;

const runs = (parts) => parts.map((p) => (typeof p === "string" ? run(p) : run(p[0], p[1]))).join("");

// akapit treści — wcięcie pierwszego wiersza i justowanie, tak jak w oryginale
const P = (...parts) =>
  `<w:p><w:pPr><w:ind w:firstLine="720"/><w:jc w:val="both"/><w:rPr/></w:pPr>${runs(parts)}</w:p>`;

// punkt listy — korzysta z numerowania zdefiniowanego w dokumencie (numId 1)
const LI = (...parts) =>
  `<w:p><w:pPr><w:numPr><w:ilvl w:val="0"/><w:numId w:val="1"/></w:numPr>` +
  `<w:ind w:left="720" w:hanging="360"/><w:jc w:val="both"/><w:rPr><w:u w:val="none"/></w:rPr></w:pPr>${runs(parts)}</w:p>`;

let bm = 800;
const HEAD = (lvl, text) => {
  const id = ++bm;
  return `<w:p><w:pPr><w:pStyle w:val="Heading${lvl}"/><w:rPr/></w:pPr>` +
    `<w:bookmarkStart w:colFirst="0" w:colLast="0" w:name="_add${id}" w:id="${id}"/><w:bookmarkEnd w:id="${id}"/>` +
    run(text) + `</w:p>`;
};
const H5 = (t) => HEAD(5, t);
const H6 = (t) => HEAD(6, t);

const EMPTY = `<w:p><w:pPr><w:rPr/></w:pPr></w:p>`;

const BORDER = ["top","left","bottom","right","insideH","insideV"]
  .map((s) => `<w:${s} w:val="single" w:sz="4" w:space="0" w:color="BFBFBF"/>`).join("");

const cellP = (t, bold) =>
  `<w:p><w:pPr><w:spacing w:after="40" w:before="40" w:lineRule="auto"/><w:rPr/></w:pPr>${run(t, { b: bold })}</w:p>`;

const TBL = (rows, widths) =>
  `<w:tbl><w:tblPr><w:tblStyle w:val="TableNormal"/><w:tblW w:w="${widths.reduce((a,b)=>a+b,0)}" w:type="dxa"/>` +
  `<w:tblBorders>${BORDER}</w:tblBorders><w:tblLayout w:type="fixed"/>` +
  `<w:tblCellMar><w:top w:w="50" w:type="dxa"/><w:left w:w="100" w:type="dxa"/>` +
  `<w:bottom w:w="50" w:type="dxa"/><w:right w:w="100" w:type="dxa"/></w:tblCellMar></w:tblPr>` +
  `<w:tblGrid>${widths.map((w) => `<w:gridCol w:w="${w}"/>`).join("")}</w:tblGrid>` +
  rows.map((r, ri) =>
    `<w:tr>${r.map((c, ci) =>
      `<w:tc><w:tcPr><w:tcW w:w="${widths[ci]}" w:type="dxa"/>` +
      (ri === 0 ? `<w:shd w:fill="F2F2F2" w:val="clear"/>` : "") +
      `</w:tcPr>${cellP(c, ri === 0)}</w:tc>`).join("")}</w:tr>`).join("") +
  `</w:tbl>${EMPTY}`;

/* ============================ 2.2.2 KREATOR I ARENA ============================ */
/* Opis rozwinięty na podstawie tego, co jest zaimplementowane w projekcie. */

const KREATOR = [
  P("Kreator stworka to wyekstrahowany obszar gry w którym gracz dokonuje ewolucji poprzez przebudowywanie modelu swojego potworka dodając, usuwając bądź edytując już istniejące. W każdej chwili jest w stanie przetestować swoje dzieło na współdzielonej z innymi graczami arenie. Arena jest miejscem gdzie dostępne są różne aktywności mające pomóc w ocenie siły i użyteczności zastosowanej konfiguracji modelu. Jest to też miejsce rozrywki i potencjalnej rywalizacji pomiędzy graczami - mogą oni tutaj wchodzić ze sobą w walkę - nic przy tym nie tracąc."),
  P("Obszar ten działa w dwóch trybach, pomiędzy którymi gracz przełącza się jednym przyciskiem bez wychodzenia z lokacji. W trybie budowania kamera ustawia się na stworzeniu i odblokowane zostają narzędzia edycji. W trybie testu gracz przejmuje kontrolę nad swoim stworem i wychodzi z nim na arenę. Brak ekranu ładowania pomiędzy tymi trybami jest decyzją projektową - skrócenie dystansu między pomysłem a sprawdzeniem go jest tym, co napędza eksperymentowanie."),
  H6("2.2.2.1 Budowanie stworzenia"),
  P("Konstrukcja stworzenia opiera się na kręgosłupie złożonym z kręgów oraz na częściach ciała doczepianych do poszczególnych kręgów."),
  LI(["Kręgosłup. ", { b: true }], "Gracz dokłada i usuwa kręgi, wydłuża i skraca kręgosłup od strony głowy albo ogona, przesuwa poszczególne kręgi względem siebie, zmienia ich promień oraz obrót. Siatka ciała jest generowana proceduralnie wokół tak ukształtowanego kręgosłupa, więc każda zmiana jest widoczna natychmiast na modelu."),
  LI(["Paleta części. ", { b: true }], "Części pogrupowane są w zakładki odpowiadające kategoriom: lokomocja, paszcza, zmysły, chwytaki, broń i pancerz oraz ozdoby. Każda pozycja w palecie ma własną miniaturę renderowaną z modelu, a nowa kategoria tworzy własną zakładkę bez potrzeby zmian w interfejsie."),
  LI(["Osadzanie części. ", { b: true }], "Część przeciąga się z palety na wybrany krąg. Następnie można ją przesuwać wzdłuż ciała, obracać, przypisać do innego kręgu oraz odbić symetrycznie, jeżeli dana część na to pozwala. Podczas przeciągania wyświetlany jest podgląd docelowego położenia."),
  LI(["Kończyny. ", { b: true }], "Nogi mają własną specyfikację opisującą między innymi liczbę punktów zgięcia. Nie jest to kwestia wyglądu - liczba i długość segmentów przekłada się bezpośrednio na prędkość poruszania się stworzenia."),
  LI(["Uchwyty i gizma. ", { b: true }], "Edycja odbywa się bezpośrednio na modelu za pomocą uchwytów kręgów, gizma obrotu części, gizma wydłużania kręgosłupa oraz uchwytów kości kończyn. Gracz nie wpisuje liczb - operuje na bryle."),
  H6("2.2.2.2 Budżet i ograniczenia"),
  P("Jedynym ograniczeniem, które gracz widzi, jest budżet stworzenia. Każdy krąg i każda część ma swoją cenę, a licznik pozostałych oraz wydanych środków aktualizuje się na bieżąco podczas budowania. Budżet pełni dwie funkcje naraz: ogranicza nadużycia w rozgrywce i utrzymuje koszt obliczeniowy symulacji w ryzach."),
  P("Poza budżetem obowiązują twarde limity techniczne, których gracz zwykle nie odczuwa: od dwóch do sześćdziesięciu czterech kręgów, do sześćdziesięciu czterech części, a także ograniczenia promienia kręgów, długości segmentów, skali i odsunięcia części. Cały zapis stworzenia musi zmieścić się w czterech kilobajtach, ponieważ w takiej postaci przesyłany jest przez sieć."),
  P("Konstrukcje niemożliwe do zbudowania lub do zasymulowania odrzuca walidator, a komunikat dla gracza formułowany jest prostym językiem, bez kodów błędów. Ten sam zestaw reguł liczony jest niezależnie po stronie klienta i serwera - serwer nigdy nie ufa limitom przysłanym przez klienta."),
  H6("2.2.2.3 Statystyki wyprowadzane z budowy ciała"),
  P("Statystyki nie są przez nikogo wpisywane ani przydzielane. Wynikają z tego, co gracz zbudował, i zmieniają się na oczach gracza w trakcie edycji. Kreator pokazuje je na żywo."),
  TBL([
    ["Statystyka", "Z czego wynika"],
    ["Punkty życia", "Liczba i rozmiar kręgów"],
    ["Masa", "Objętość kręgów; przekłada się dalej na prędkość i na siłę uderzenia"],
    ["Prędkość ruchu", "Masa, liczba i budowa kończyn, liczba punktów zgięcia nóg"],
    ["Szybkość skrętu", "Masa oraz rozkład masy wzdłuż ciała"],
    ["Obrażenia", "Zamontowane części z kategorii broni i paszczy"],
    ["Zasięg zmysłów", "Części z kategorii zmysłów"],
    ["Stabilność", "Rozstaw kończyn i wysokość środka masy"],
  ], [2600, 6429]),
  P("Stabilność jest przykładem tego, jak wygląd przekłada się na rozgrywkę. Stworzenie utrzymuje się na nogach dopóki moment siły odśrodkowej działający na zewnętrzną stopę jest mniejszy od momentu przywracającego, pochodzącego od ciężaru. Szerokie i niskie stworzenie trzyma się terenu, a wysokie na cienkich nogach wywraca się na pierwszym ostrym zakręcie. Stworzenie bez nóg nie ma się na czym przewrócić - leży brzuchem na ziemi i porusza się inaczej."),
  H6("2.2.2.4 Arena"),
  P("Arena jest przestrzenią wspólną dla całej drużyny. Gracz porusza się po niej swoim stworzeniem w ośmiu kierunkach, z kamerą orbitalną, i sprawdza w praktyce to, co przed chwilą zbudował."),
  LI(["Utrata równowagi. ", { b: true }], "Stworzenie przechyla się pod obciążeniem bocznym, a po przekroczeniu własnej granicy wywraca się. Upadek i podnoszenie się zajmują ułamek sekundy, w czasie którego gracz nie ma kontroli. To najczytelniejsza informacja zwrotna, jaką daje kreator: źle wyważony projekt nie wymaga tłumaczenia."),
  LI(["Fizyczne przewracanie. ", { b: true }], "Wywrócone stworzenie przechodzi w tryb fizyczny, w którym kości kręgosłupa są symulowane jako łańcuch brył sztywnych. Symulacja jest wyłącznie kosmetyczna i liczona lokalnie u każdego gracza - pozycja, w której stworzenie wstaje, jest natomiast rozstrzygana przez serwer i identyczna u wszystkich."),
  LI(["Popychanie. ", { b: true }], "Gracze mogą popychać swoje stworzenia nawzajem. Cel popchnięcia wybiera serwer własnym testem przestrzennym, a nie klient - dzięki temu spreparowany pakiet nie pozwoli przewracać graczy z drugiego końca mapy. Na arenie nie wiąże się to z żadną stratą, co czyni ją miejscem zabawy, a nie kolejnym źródłem ryzyka."),
  LI(["Aktywności testowe. ", { b: true }], "Arena ma docelowo zawierać zestaw aktywności pozwalających ocenić przydatność buildu przed wyruszeniem na wyspę - próby prędkości, wytrzymałości i siły uderzenia, a także manekiny treningowe."),
  P("Arena pełni też funkcję społeczną. Jest miejscem, w którym drużyna spotyka się pomiędzy wyprawami, ogląda nawzajem swoje stworzenia i sprawdza pomysły bez konsekwencji. W grze kooperacyjnej ta przestrzeń jest równie ważna jak sama rozgrywka - to tutaj powstaje większość materiału, którym gracze dzielą się później na zewnątrz."),
];

/* ================================ 5.1 ZAKRES GRY ================================ */

const ZAKRES_INTRO = [
  P("Zakres opisano w dwóch punktach: na premierę w Early Access oraz na wersję 1.0. Wejście do Early Access traktujemy jako plan minimum - najmniejszy zestaw, który da się sprzedać i który pozwala rzetelnie ocenić, czy gra znajduje odbiorców. Wszystko, czego w tej kolumnie nie ma, jest świadomie odłożone, a nie przeoczone."),
];

const ZAKRES_TABELA = TBL([
  ["Element", "Early Access (plan minimum)", "Wersja 1.0"],
  ["Długość pojedynczego runu", "25-40 minut", "25-40 minut"],
  ["Liczba graczy", "1-4", "1-4"],
  ["Tryb sieciowy", "Host-klient przez Steam", "Host-klient przez Steam"],
  ["Archetypy wysp", "1", "3 oraz wyspa bossa"],
  ["Starcia z bossami", "1", "5"],
  ["Blueprinty (części ciała)", "ok. 20", "ok. 70"],
  ["Kategorie części", "Lokomocja, paszcza, broń", "Wszystkie sześć kategorii"],
  ["Typy przeciwników", "ok. 6", "ok. 20"],
  ["Kreator stworka", "Pełna edycja kręgosłupa i części", "Dodatkowo kolory i warianty materiałów"],
  ["Arena", "Test buildu i swobodna walka między graczami", "Dodatkowo aktywności testowe i manekiny"],
  ["Safe hub", "Kreator i arena", "Rozbudowany o dalsze funkcje drużyny"],
  ["Głosowanie nad traitami", "Tak", "Tak"],
  ["Poziomy ewolucji", "Uproszczona ścieżka", "Pełna ścieżka z traitami zależnymi od poziomu"],
  ["Fizyczne przewracanie", "Tak", "Tak"],
  ["Muzyka", "Warstwa podstawowa", "Ścieżka adaptacyjna"],
  ["Języki", "4", "6"],
  ["Obsługa kontrolera", "Nie", "Tak"],
  ["Osiągnięcia Steam", "Nie", "Tak"],
  ["Dzielenie się stworzeniami", "Nie", "Steam Workshop"],
  ["API modów", "Nie", "Pierwsza duża aktualizacja po 1.0"],
  ["Serwery dedykowane", "Nie", "Do rozważenia po premierze"],
  ["Wersje konsolowe", "Nie", "Do rozważenia po premierze"],
], [2400, 3300, 3329]);

const ZAKRES_EA = [
  H6("5.1.1 Czego nie będzie w Early Access"),
  P("Najważniejsze cięcia względem docelowego zakresu dotyczą objętości treści, a nie rdzenia rozgrywki. Kreator, arena, fizyka, kooperacja i pełna pętla runu działają od pierwszego dnia sprzedaży - inaczej gra nie nadawałaby się do oceny."),
  LI(["Jeden archetyp wyspy zamiast trzech. ", { b: true }], "Run kończy się szybciej i ma mniejszą zmienność między kolejnymi podejściami. Jest to największe pojedyncze cięcie i to ono najmocniej ogranicza regrywalność na starcie."),
  LI(["Około dwudziestu blueprintów zamiast siedemdziesięciu. ", { b: true }], "Przestrzeń buildów jest wyraźnie węższa, a część kategorii części nie pojawia się wcale. Zmysły, chwytaki i ozdoby wchodzą dopiero po premierze."),
  LI(["Brak dzielenia się stworzeniami. ", { b: true }], "Integracja ze Steam Workshop wymaga backendu, walidacji importowanych genomów i narzędzi moderacji. Zapis stworzenia jest już przygotowany pod tę funkcję, ale sama integracja nie zmieści się przed premierą."),
  LI(["Brak obsługi kontrolera. ", { b: true }], "Kreator jest ekranem o najbardziej rozbudowanym interfejsie w całej grze i jego przeprojektowanie pod pada to osobna praca, nie drobna adaptacja."),
  LI(["Brak osiągnięć i rankingów. ", { b: true }], "Elementy retencyjne, które mają sens dopiero wtedy, gdy balans jest ustabilizowany."),
  LI(["Ograniczona warstwa audio. ", { b: true }], "Na premierę wchodzi zestaw efektów i podstawowe tło dźwiękowe. Ścieżka adaptacyjna reagująca na napięcie runu jest zadaniem na okres po premierze."),
  LI(["Brak serwerów dedykowanych i wersji konsolowych. ", { b: true }], "Obie pozycje są zależne od wyników sprzedaży i nie deklarujemy ich przed premierą."),
];

const ZAKRES_CZAS = [
  H6("5.1.2 Czas spędzony w Early Access"),
  P("Świadomie nie podajemy daty wersji 1.0. Deklarujemy zakres wejścia do Early Access oraz tempo aktualizacji, natomiast moment domknięcia wersji pełnej wynika z tego, jak gra zostanie przyjęta, a nie z założenia przyjętego przed premierą. Praktyka rynkowa daje tu jednoznaczne wskazówki."),
  LI("Mediana pobytu w Early Access dla gier, które opuściły go w 2025 roku, wyniosła 437 dni, czyli około czternastu miesięcy. Średnia była wyraźnie wyższa i wyniosła 643 dni, co pokazuje, jak mocno rozkład jest rozciągnięty."),
  LI("Ponad połowa gier opuszcza Early Access w mniej niż dwanaście miesięcy, a 76 procent najlepiej zarabiających tytułów spędziło w nim od sześciu do osiemnastu miesięcy."),
  LI("Najlepszy przyrost nowych graczy notują gry przebywające w Early Access od czterech do dziewięciu miesięcy. Badania wskazują jednocześnie, że nadmiernie długi Early Access obniża sprzedaż po premierze wersji pełnej."),
  LI("Skrajności w tym samym gatunku są duże. Lethal Company pozostaje w Early Access od października 2023 roku i wciąż jest jednym z największych sukcesów komercyjnych segmentu. Valheim spędził w nim pięć lat, wychodząc z Early Access dopiero we wrześniu 2026 roku."),
  P("Wniosek jest taki, że długość Early Access jest narzędziem, a nie terminem. Zakładamy rytm aktualizacji co kilka tygodni w pierwszych miesiącach po premierze i większe aktualizacje treści w odstępach kwartalnych, a decyzję o wersji 1.0 podejmujemy wtedy, gdy zakres z prawej kolumny powyższej tabeli zostanie faktycznie osiągnięty."),
];

/* ============================ 5.2 STAN ROZWOJU GRY ============================ */
/* Higiena projektu usunięta. Lista uzupełniona o to, co faktycznie działa,
   a czego w poprzedniej wersji brakowało: arena, ragdoll, popychanie, paleta, statystyki. */

const STAN = [
  P("Poniżej wymieniono wyłącznie te elementy, które są zaimplementowane i działają w projekcie. W grze tego typu ryzyko techniczne skupia się w jednym miejscu - w kreatorze i w tym, czy jego wytwory dają się animować i zsynchronizować w sieci. Ta część jest już domknięta."),
  H6("Kreator i genom"),
  LI(["Model stworzenia. ", { b: true }], "Kręgosłup, kończyny i części wraz z ich położeniem, obrotem i regułami łączenia."),
  LI(["Operacje edycji. ", { b: true }], "Dokładanie i usuwanie kręgów, wydłużanie kręgosłupa od głowy i od ogona, zmiana promienia, przesunięcia i obrotu, doczepianie i odczepianie części, zmiana kości, obrót, skala oraz symetria."),
  LI(["Paleta części. ", { b: true }], "Sześć kategorii w osobnych zakładkach, z miniaturami renderowanymi z modeli. Dodanie kategorii nie wymaga pracy nad interfejsem."),
  LI(["Budżet stworzenia. ", { b: true }], "System punktowy wyceniający kręgi i części, z licznikiem aktualizowanym na żywo podczas budowania."),
  LI(["Walidacja. ", { b: true }], "Odrzucanie konstrukcji niemożliwych do zbudowania lub zasymulowania, z komunikatami formułowanymi dla gracza, a nie dla logów. Reguły liczone niezależnie po stronie klienta i serwera."),
  LI(["Statystyki wyprowadzane z budowy ciała. ", { b: true }], "Punkty życia, masa, prędkość, szybkość skrętu, obrażenia, zasięg zmysłów i stabilność, przeliczane i pokazywane w trakcie edycji."),
  LI(["Zapis i przesyłanie. ", { b: true }], "Kompaktowy zapis stworzenia wraz z sumą kontrolną - podstawa zapisu na dysk, przesyłania przez sieć i późniejszego dzielenia się stworzeniami."),
  H6("Ciało i ruch"),
  LI(["Generowanie ciała. ", { b: true }], "Proceduralna siatka kręgosłupa, budowa szkieletu, dopasowanie zderzaczy i osadzanie części."),
  LI(["Chód. ", { b: true }], "Proceduralne nogi z kinematyką odwrotną, zawieszenie i profile chodu wyliczane z budowy kończyn."),
  LI(["Równowaga. ", { b: true }], "Przechylanie się pod obciążeniem bocznym i wywracanie po przekroczeniu granicy wynikającej z rozstawu nóg i wysokości środka masy."),
  LI(["Fizyczne przewracanie. ", { b: true }], "Łańcuch brył sztywnych na kościach kręgosłupa, budowany razem z ciałem, uruchamiany przy upadku. Symulacja lokalna i kosmetyczna, z autorytatywnym miejscem podniesienia."),
  H6("Arena i sieć"),
  LI(["Dwa tryby obszaru. ", { b: true }], "Przełączanie między budowaniem a testem bez ekranu ładowania."),
  LI(["Sterowanie stworzeniem. ", { b: true }], "Ruch w ośmiu kierunkach z kamerą orbitalną, przyspieszanie i hamowanie zależne od statystyk stworzenia."),
  LI(["Popychanie. ", { b: true }], "Interakcja fizyczna między stworzeniami graczy, z wyborem celu po stronie serwera."),
  LI(["Warstwa sieciowa. ", { b: true }], "Szkielet oparty na FishNet w modelu serwer-autorytatywnym, z przesyłaniem stworzeń w postaci zapisu genomu zamiast geometrii."),
];

const PLANY = [
  P("Do zrobienia pozostaje warstwa wizualna w docelowym stylu, pełna pętla runu, walka i sztuczna inteligencja przeciwników, generator wysp, system blueprintów i punktów ewolucji, głosowanie drużyny nad traitami, progresja, warstwa audio, treść oraz optymalizacja. Po stronie infrastruktury dochodzą lobby, przechodzenie między wyspami i obsługa śmierci gracza. Jest to praca duża, ale przewidywalna - w odróżnieniu od warstwy, którą właśnie domknęliśmy."),
];

/* ============================ 5.3 ZESPÓŁ I PRODUKCJA ============================ */

const ZESPOL_OSOBY = [
  P("Zespół liczy trzy osoby i pracuje w pełni zdalnie. Poniżej krótka charakterystyka każdej z nich wraz z zakresem odpowiedzialności."),
  LI(["Dariusz Dziel - Unity Developer, prowadzenie projektu. ", { b: true }],
     "Cztery lata pracy w branży gier oraz udział w jednym wydanym komercyjnie tytule. W Leeway odpowiada za warstwę sieciową, kreator stworzeń, architekturę projektu oraz decyzje techniczne. Autor działającego prototypu opisanego w 5.2.1. [do uzupełnienia: tytuł wydanej gry i poprzednie miejsce pracy]"),
  LI(["Joanna Dziel - Game Designer. ", { b: true }],
     "Odpowiada za projekt rozgrywki, balans systemu traitów i punktów ewolucji, strukturę runu oraz warstwę dźwiękową. Prowadzi dokumentację projektową. [do uzupełnienia: doświadczenie i wcześniejsze projekty]"),
  LI(["Magdalena Pławińska - Artystka 3D. ", { b: true }],
     "Odpowiada za modele stworzeń i środowiska, tekstury, kierunek wizualny opisany w rozdziale 4 oraz za interfejs użytkownika. Przygotowuje zestawy assetów, z których generowane są wyspy. [do uzupełnienia: doświadczenie i wcześniejsze projekty]"),
];

const ZESPOL_PLAN = [
  P("Zespół rozwijamy w modelu wewnętrznym. Nie planujemy stałego outsourcingu żadnego z obszarów - przy tak małym składzie koszt koordynacji zewnętrznych wykonawców przewyższa zysk, a kluczowe elementy gry, czyli kreator i styl wizualny, wymagają ciągłej pracy wewnątrz zespołu, a nie zleceń zamykanych pojedynczymi dostawami."),
  P("Rozbudowa zespołu jest stopniowa i podporządkowana potrzebom produkcji. Pierwszą i najważniejszą rekrutacją jest starszy programista odpowiedzialny za animację proceduralną, fizykę i warstwę sieciową. Jest to najtrudniejsza do obsadzenia rola w całym projekcie i jednocześnie ta, która najmocniej odblokowuje tempo prac. W dalszej kolejności rozważamy dołączenie programisty gameplayu oraz drugiej osoby po stronie grafiki."),
  H6("Testy"),
  P("Odrębną kwestią są testy. Nie planujemy zatrudniać testerów na stałe - przy zespole tej wielkości etat testerski jest trudny do uzasadnienia, a większość błędów wychodzi w codziennej pracy nad grą. Zamiast tego zakładamy trzy uzupełniające się formy testowania."),
  LI(["Testy wewnętrzne w trybie ciągłym. ", { b: true }], "Cały zespół gra w grę regularnie, a sesje kooperacyjne są jedynym sposobem, by wychwycić błędy pojawiające się wyłącznie przy kilku graczach jednocześnie."),
  LI(["Płatne sesje testowe przed kamieniami milowymi. ", { b: true }], "Testerzy angażowani doraźnie, na konkretne sesje, przede wszystkim przed publicznym demem i przed premierą w Early Access. Interesuje nas głównie to, czy nowy gracz rozumie kreator bez tłumaczenia."),
  LI(["Zamknięta grupa testowa. ", { b: true }], "Społeczność zbierana wokół strony na Steam i kanału Discord, dostająca wczesne kompilacje w zamian za informację zwrotną. Jest to jednocześnie najtańsza forma testów i pierwsza grupa graczy, którzy kupią grę w dniu premiery."),
  P("Rolę QA w klasycznym rozumieniu planujemy obsadzić dopiero wtedy, gdy zespół przekroczy sześć osób, a liczba systemów wchodzących ze sobą w interakcje sprawi, że testy wewnętrzne przestaną wystarczać."),
];

/* ============================ 5.5 HARMONOGRAM PRODUKCJI ============================ */

const HARMONOGRAM = [
  H5("5.5 Harmonogram produkcji"),
  P("Harmonogram obejmuje drogę do premiery w Early Access i kończy się na niej. Zgodnie z tym, co opisano w 5.1.2, nie wyznaczamy daty wersji 1.0 - dalszy rozwój prowadzimy rytmem aktualizacji, a nie datą w kalendarzu."),
  P("Prace rozliczamy kamieniami milowymi. Każdy z nich kończy się grywalną kompilacją albo pomiarem, a nie deklaracją postępu, dzięki czemu postęp może zweryfikować osoba spoza zespołu."),
  H6("5.5.1 Faza 1: domknięcie prototypu i wejście na rynek z demem"),
  P("Pracuje obecny, trzyosobowy zespół. Celem fazy nie jest zbudowanie gry, lecz doprowadzenie kreatora i areny do stanu, w którym można je pokazać publicznie, oraz sprawdzenie, czy gracze reagują."),
  TBL([
    ["Miesiąc", "Zakres prac", "Rezultat"],
    ["1", "Warstwa wizualna kreatora w docelowym stylu, pierwszy zestaw blueprintów, struktura danych traitów", "Kreator wygląda jak gra, a nie jak narzędzie. Piętnaście części w docelowym stylu"],
    ["2", "Pełna pętla kreator - arena, aktywności testowe, dopracowanie równowagi i przewracania", "Kompilacja, w której czterech graczy buduje stworzenia i testuje je razem na arenie"],
    ["3", "Pierwszy archetyp wyspy, podstawowi przeciwnicy, walka, punkty ewolucji", "Grywalny run od safe hubu przez wyspę do powrotu"],
    ["4", "Głosowanie nad traitami, poziomy ewolucji, szlify i testy zewnętrzne", "Demo zamknięte: pełny run w uproszczonym zakresie, testowany przez graczy spoza zespołu"],
    ["5", "Materiały sklepowe, strona na Steam, publikacja dema", "Strona na Steam, zwiastun, publiczne demo, start zbierania listy życzeń"],
  ], [1000, 4300, 3729]),
  P("Publikacja strony na Steam w piątym miesiącu jest osobnym celem, nie produktem ubocznym. Lista życzeń zbierana od tego momentu jest jedyną wiarygodną przesłanką przy decyzji o skali dalszych prac, a jednocześnie buduje grupę graczy, która kupi grę w dniu premiery."),
  H6("5.5.2 Faza 2: produkcja do Early Access"),
  P("Faza zakłada rozbudowę zespołu opisaną w 5.3.2 oraz doprowadzenie zakresu do lewej kolumny tabeli z 5.1."),
  TBL([
    ["Miesiąc", "Zakres prac", "Rezultat"],
    ["6-7", "Dołącza starszy programista. Rozbudowa systemu blueprintów, pełny zestaw około dwudziestu części, druga kategoria przeciwników", "Przestrzeń buildów wystarczająca, by dwa runy różniły się od siebie"],
    ["8-9", "Generator wyspy z zestawów assetów, przechodzenie między etapami, obsługa śmierci gracza", "Wyspa generowana proceduralnie, identyczna u wszystkich graczy w sesji"],
    ["10", "Walka z bossem, zamknięcie pętli runu, lobby i przepływ sesji", "Pełny run zakończony walką z bossem"],
    ["11", "Warstwa audio, interfejs, lokalizacja na cztery języki", "Gra w stanie nadającym się do pokazania szerokiej publiczności"],
    ["12", "Optymalizacja, testy zewnętrzne, poprawki, materiały premierowe", "Kandydat do wydania: stabilna kompilacja i komplet materiałów sklepowych"],
  ], [1000, 4300, 3729]),
  P("Dwunasty miesiąc jest najwcześniejszym rozsądnym terminem premiery w Early Access, a nie terminem gwarantowanym. Jeżeli testy z piątego miesiąca pokażą, że kreator wymaga przebudowy, przesunięcie premiery jest tańsze niż wejście na rynek z rozwiązaniem, którego gracze nie rozumieją."),
  H6("5.5.3 Po premierze w Early Access"),
  P("Po premierze przechodzimy na rytm aktualizacji. Jest on ważniejszy od objętości pojedynczej łatki - widoczność na Steam nagradza regularność, a społeczność ocenia zespół po przewidywalności, nie po rozmiarze pojedynczej aktualizacji."),
  LI(["Pierwsze trzy miesiące. ", { b: true }], "Poprawki co jeden do dwóch tygodni, reakcja na zgłoszenia, korekta balansu i krzywej wejścia dla nowych graczy. W tym okresie nie dodajemy treści - domykamy to, co już jest."),
  LI(["Kolejne miesiące. ", { b: true }], "Większe aktualizacje treści w odstępach kwartalnych, każda dokładająca jeden wyraźny element z prawej kolumny tabeli z 5.1: archetyp wyspy, zestaw blueprintów, bossa albo kategorię części."),
  LI(["Decyzja o wersji 1.0. ", { b: true }], "Podejmowana wtedy, gdy zakres docelowy zostanie faktycznie osiągnięty, a tempo napływu nowych graczy przestanie rosnąć. Dane rynkowe przytoczone w 5.1.2 wskazują, że najczęściej mieści się to w przedziale od sześciu do osiemnastu miesięcy od premiery w Early Access."),
];

/* ============================== PODMIANY W PLIKU ============================== */

const zip = await JSZip.loadAsync(readFileSync(IN));
let xml = await zip.file("word/document.xml").async("string");
const przed = xml.length;

const paraStart = (src, frag, nth = 1) => {
  let i = -1; for (let k = 0; k < nth; k++) { i = src.indexOf(frag, i + 1); if (i < 0) return -1; }
  return src.lastIndexOf("<w:p ", i);
};
const paraEnd = (src, frag, nth = 1) => {
  let i = -1; for (let k = 0; k < nth; k++) { i = src.indexOf(frag, i + 1); if (i < 0) return -1; }
  return src.indexOf("</w:p>", i) + 6;
};

let ok = 0, blad = 0;
// podmiana zakresu od akapitu z `od` do akapitu z `do`
const zamien = (od, doo, tresc, nazwa) => {
  const a = paraStart(xml, od), b = paraEnd(xml, doo);
  if (a < 0 || b < 6 || b <= a) { console.error("  BŁĄD:", nazwa); blad++; return; }
  xml = xml.slice(0, a) + (Array.isArray(tresc) ? tresc.join("") : tresc) + xml.slice(b);
  console.log("  zaktualizowano:", nazwa); ok++;
};

// 2.2.2 — rozwinięcie opisu kreatora i areny
zamien("wyekstrahowany obszar", "wyekstrahowany obszar", KREATOR, "2.2.2 Kreator stworka i arena");

// 5.1 — akapit wstępny wraz z istniejącą tabelą
{
  const a = paraStart(xml, "Zakres opisano w dwóch punktach");
  const t = xml.indexOf("</w:tbl>", a);
  if (a < 0 || t < 0) { console.error("  BŁĄD: 5.1 Zakres gry"); blad++; }
  else {
    xml = xml.slice(0, a) + [...ZAKRES_INTRO, ZAKRES_TABELA, ...ZAKRES_EA, ...ZAKRES_CZAS].join("") + xml.slice(t + 8);
    console.log("  zaktualizowano: 5.1 Zakres gry (tabela, cięcia EA, czas w EA)"); ok++;
  }
}

// 5.2.1 — lista stanu; usuwa pozycję "Higiena projektu"
zamien("Genom stworzenia", "automatycznie generowana dokumentacja architektury", STAN, "5.2.1 Aktualny stan gry");

// 5.2.2 — plany
zamien("Do zrobienia pozostaje", "Do zrobienia pozostaje", PLANY, "5.2.2 Plany na przyszłość");

// 5.3.1 — charakterystyka osobowa po tabeli zespołu
{
  const t = xml.indexOf("Magdalena Pławińska");
  const koniec = t < 0 ? -1 : xml.indexOf("</w:tbl>", t);
  if (koniec < 0) { console.error("  BŁĄD: 5.3.1 charakterystyka"); blad++; }
  else {
    xml = xml.slice(0, koniec + 8) + ZESPOL_OSOBY.join("") + xml.slice(koniec + 8);
    console.log("  zaktualizowano: 5.3.1 Bieżący zespół (charakterystyka osobowa)"); ok++;
  }
}

// 5.3.2 — plan rozbudowy i testy
zamien("W pierwszych czterech miesiącach pracują", "Docelowa wielkość zespołu to osiem osób", ZESPOL_PLAN, "5.3.2 Plan rozbudowy zespołu i testy");

// 5.5 — harmonogram na końcu dokumentu
{
  const s = xml.indexOf("<w:sectPr");
  if (s < 0) { console.error("  BŁĄD: 5.5 Harmonogram"); blad++; }
  else { xml = xml.slice(0, s) + HARMONOGRAM.join("") + xml.slice(s); console.log("  dodano: 5.5 Harmonogram produkcji"); ok++; }
}

// spis treści — klonowanie istniejącego wiersza z podmianą tekstu
const tocClone = (wzor, tekst) => {
  const a = paraStart(xml, wzor), b = paraEnd(xml, wzor);
  if (a < 0) return null;
  let p = xml.slice(a, b);
  // pierwszy <w:t> to etykieta, drugi to numer strony
  let pierwszy = true;
  p = p.replace(/<w:t xml:space="preserve">[\s\S]*?<\/w:t>/g, (m) => {
    if (pierwszy) { pierwszy = false; return `<w:t xml:space="preserve">${esc(tekst)}</w:t>`; }
    return m;
  });
  return p;
};
const tocPo = (wzor, wpisy) => {
  const b = paraEnd(xml, wzor);
  if (b < 6) { console.error("  BŁĄD: spis treści przy", wzor); blad++; return; }
  const kod = wpisy.map((t) => tocClone(wzor, t)).filter(Boolean).join("");
  xml = xml.slice(0, b) + kod + xml.slice(b);
  console.log("  spis treści: dopisano", wpisy.length, "poz. za", wzor);
};
tocPo("5.4 Potrzeby wydawnicze", ["5.5 Harmonogram produkcji", "5.5.1 Faza 1: domknięcie prototypu i wejście na rynek z demem", "5.5.2 Faza 2: produkcja do Early Access", "5.5.3 Po premierze w Early Access"]);
tocPo("5.1 Zakres gry", ["5.1.1 Czego nie będzie w Early Access", "5.1.2 Czas spędzony w Early Access"]);

// myślniki: pauza i półpauza na dywiz
let dash = 0;
xml = xml.replace(/<w:t([^>]*)>([\s\S]*?)<\/w:t>/g, (m, a, t) => {
  const z = t.replace(/[—–]/g, "-");
  if (z !== t) dash += (t.match(/[—–]/g) || []).length;
  return `<w:t${a}>${z}</w:t>`;
});

zip.file("word/document.xml", xml);
const buf = await zip.generateAsync({ type: "nodebuffer", compression: "DEFLATE" });
writeFileSync(OUT, buf);
console.log("\nzmian:", ok, "| błędów:", blad, "| myślników poprawiono:", dash);
console.log("document.xml:", przed, "->", xml.length);
console.log("zapisano:", OUT, (buf.length / 1024 / 1024).toFixed(2), "MB");
