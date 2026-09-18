# Edytor stworków — checklista testów manualnych

Procedury, których **nie da się** pokryć testem automatycznym. Reszta pętli edytora
jest już zautomatyzowana — przed sięgnięciem po tę listę odpal:

```
Test Runner → EditMode  (211 testów)
Test Runner → PlayMode  (34 testy, host FishNeta w jednym procesie)
```

Ta checklista dotyczy wyłącznie dwóch rzeczy, których automat nie widzi:
**wyglądu** i **rozjazdu między dwiema fizycznymi maszynami**.

## Setup
1. Otwórz scenę `Assets/Scenes/CreatureEditor.unity`.
2. Wejdź w **Play Mode**.
3. W lewym górnym rogu kliknij **Host (Server + Client)**.

## Smoke test (host lokalny)
- [ ] Brak błędów w konsoli po starcie hosta.
- [ ] Stworek spawnuje się na niebieskiej płycie edytora i **stoi**, nie zapada się w podłogę.
- [ ] Stworek zostaje **tam, gdzie się pojawił** — nie zjeżdża do środka świata.
      Zjazd do (0,0,0) oznacza, że ktoś znowu woła metodę uzgadniającą poza `CreateReconcile`.
- [ ] W trakcie rzeźbienia widać **wyłącznie** rzeźbionego stworka — jego kopia w grze
      jest schowana, więc na arenie nie stoją dwa te same stwory.
- [ ] Po **Zastosuj** / **Tab** kopia w grze wraca, a podgląd znika.
- [ ] Powrót do rzeźbienia po commicie znowu chowa stwora w grze.

## Arena
- [ ] Teren jest kolisty, zielony, z żywopłotem po całym obwodzie.
- [ ] Marsz w dowolną stronę zatrzymuje się na żywopłocie — **nie da się wyjść** poza arenę.
- [ ] Nie da się też przelecieć przez podłogę ani zawisnąć w powietrzu przy krawędzi.
- [ ] Widać niebo: pogodny gradient z rozświetleniem przy horyzoncie.
- [ ] Drzewa, krzewy i trzciny stoją przy obrzeżu; środek areny zostaje wolny na rozgrywkę.
- [ ] Ciało wygląda jak ciało: gładka tuba wzdłuż kręgosłupa, bez skręceń i wywiniętych trójkątów.
- [ ] Części (nogi/szczęka/oko) siedzą na kościach i **nie migoczą** ani nie odstają.
- [ ] Panel po lewej pokazuje kręgi, części, HP, masę, prędkość **oraz stan waluty**.
- [ ] Paleta po prawej wypełnia się częściami z katalogu, pogrupowanymi po kategorii.
- [ ] Kręgosłup to **jeden ciągły łańcuch kapsułek**, nie sznur oddzielnych kulek —
      między sąsiednimi kręgami nie widać przerw, także na zgięciach.

## Rzeźbienie
- [ ] **PPM + mysz** obraca kamerę wokół podglądu.
- [ ] **LPM** na kręgu zaznacza go — panel pokazuje „Zaznaczenie: kreg N".
- [ ] Przeciąganie zaznaczonego kręgu przesuwa go, a **kręg nie ucieka spod kursora** przy szybkim ruchu.
- [ ] Ciągnięcie kręgu rozciąga **tylko jego odcinek** — reszta ciała stoi w miejscu,
      stworek nie rozjeżdża się na całej długości.
- [ ] Odcinka nie da się ścisnąć do zera ani rozciągnąć bez końca; przy skrajnym
      ciągnięciu sąsiedni odcinek zatrzymuje się na minimum i **nie przekręca się** do tyłu.
- [ ] Scroll zmienia grubość kręgu tylko w dozwolonym zakresie.

## Wydłużanie kręgosłupa
- [ ] Klik w korpus odsłania kręgi **oraz dwie strzałki** — jedną od strony głowy,
      drugą od ogona.
- [ ] Najechanie na strzałkę podświetla ją.
- [ ] Klik w strzałkę dokłada **jeden kręg** po tej stronie; reszta ciała zostaje na miejscu.
- [ ] Dokładanie od strony głowy przenosi części na właściwe kręgi — nie zostają
      zawieszone na cudzym.
- [ ] Próba dołożenia głowy, gdy na starej wisi część dozwolona wyłącznie na głowie,
      jest odrzucana w całości i **nie zostawia** dołożonego kręgu.
- [ ] Klik w pustkę chowa kręgi razem ze strzałkami.
- [ ] **Scroll** nad zaznaczonym kręgiem zmienia jego grubość; ciało przebudowuje się płynnie.
- [ ] **E** dodaje kręg, **Q** usuwa; `[` i `]` przeskakują zaznaczenie.
- [ ] Próba doczepienia płetwy ogonowej do głowy → komunikat „ta czesc nie pasuje do tego kregu".
- [ ] Odczyt HP w panelu rośnie razem z liczbą kręgów **na żywo**, przed zatwierdzeniem.
- [ ] Waluta w panelu spada przy dokładaniu kręgów i części, a rośnie przy ich usuwaniu.
- [ ] Da się doczepić **wiele** oczu, ust i par nóg naraz — kategorie nie mają już limitów.
- [ ] **Backspace** cofa wszystko do stanu z otwarcia sesji.

## Chwytanie części
> Kręgosłup **nie musi** być odsłonięty — części łapie się zawsze.

- [ ] Najechanie na oko / pysk / nogi podświetla **tę** część, nie kręg pod nią.
- [ ] Przeciąganie części przesuwa ją po ciele; część zostaje na swoim kręgu.
- [ ] Ciągnięcie lewej sztuki pary (oko, nogi) idzie **za kursorem**, a nie w przeciwną stronę.
- [ ] Puszczenie przycisku **poza stworkiem** kasuje część, a waluta wraca do puli.
- [ ] Puszczenie **na ciele** zostawia część w nowym miejscu.
- [ ] **Tab** w trakcie ciągnięcia nie zostawia „przyklejonej" części — po powrocie do
      rzeźbienia nic nie jedzie za kursorem.

## Paleta — przeciąganie i zakładki
- [ ] Pasek zakładek pokazuje sześć kategorii: Ruch, Usta, Zmysly, Chwytaki, Bron, Ozdoby.
- [ ] Klik w zakładkę podmienia listę części; aktywna zakładka jest wyróżniona kolorem.
- [ ] Kafelek pokazuje nazwę części i jej koszt.
- [ ] **Sam klik w kafelek niczego nie doczepia** — część bierze się przeciągnięciem.
- [ ] W trakcie ciągnięcia leci półprzezroczysty duch części: **zielony** nad ciałem,
      **czerwony** poza nim.
- [ ] Duch siada na skórze w tej samej pozie, w której wyląduje część po puszczeniu.
- [ ] Puszczenie nad ciałem doczepia część **w tym miejscu**, a nie na zaznaczonym kręgu.
- [ ] Puszczenie poza stworkiem nie doczepia niczego i nie zabiera waluty.

## Osadzenie i orientacja części
> Reguła: część siada na **zmierzonej** skórze i patrzy **od ciała**.
> Geometrię pilnują testy, tu chodzi o to, czy to wygląda dobrze.

- [ ] Żadna część nie zapada się w tuszę ciała ani nie wisi w powietrzu obok niej.
- [ ] Pogrubienie kręgu scrollem **wypycha** siedzące na nim części na zewnątrz razem ze skórą,
      po tym samym kierunku — bez obracania ich.
- [ ] Przeciąganie kręgu **niesie ze sobą** doczepione do niego części; trzymają się
      swojego punktu przyczepu i nie krążą wokół innego pivota.
- [ ] Obrót kręgu obraca jego części razem z nim, jak sztywno przyklejone.
- [ ] Ruszanie **sąsiednim** kręgiem ani jego pogrubianie **nie rusza** części
      siedzącej na kręgu obok.
- [ ] Oko przeciągnięte na wierzch głowy patrzy w górę, na bok — w bok, pod spód — w dół.
      Obrót nadąża **w trakcie** ciągnięcia, nie dopiero po puszczeniu.
- [ ] **Nogi zawsze celują stopą w podłoże**, niezależnie od tego, gdzie na ciele je
      doczepisz — także gdy siedzą na samym boku albo na grzbiecie.
- [ ] Pozostałe części (oczy, pysk, rogi) nadal patrzą **od ciała**, wzdłuż jego normalnej.
- [ ] Pysk na przedniej krawędzi głowy patrzy **do przodu**, nie w górę.
- [ ] Część przesuwana z boku na czubek głowy zmienia kierunek **płynnie**, bez przeskoku.
- [ ] Lewa sztuka pary jest lustrzanym odbiciem prawej, bez wywróconego cieniowania.

## Osie rotacji
- [ ] Pierwszy klik w część ją zaznacza; **drugi klik** (klik, nie przeciągnięcie) odsłania
      trzy pierścienie: czerwony X, zielony Y, niebieski Z.
- [ ] Kolejny klik w część chowa pierścienie.
- [ ] Najechanie na pierścień rozjaśnia go, a część pod spodem **przestaje** się podświetlać.
- [ ] Ciągnięcie pierścienia obraca część wokół tej osi i nie ucieka spod kursora.
- [ ] Po obróceniu części ręcznie i przeciągnięciu jej w inne miejsce ciała korekta **zostaje**
      — część nadal patrzy od ciała, tylko z twoim przechyleniem.
- [ ] Klik w pustkę zdejmuje zaznaczenie razem z pierścieniami.
- [ ] Gizmo trzyma się części także w trakcie jej przeciągania.

## Symetria
- [ ] **M** na zaznaczonej części przełącza jedna sztuka ↔ para; przycisk w panelu robi to samo.
- [ ] Przycisk pokazuje aktualny stan i jest **szary**, gdy nic nie zaznaczono.
- [ ] Przy części bez wersji lustrzanej (szczęka, dziób) przycisk jest szary i pisze „brak pary".
- [ ] Zejście z pary do jednej sztuki zostawia prawą sztukę, a nie obie albo żadnej.

## Zatwierdzanie
- [ ] Przycisk **Zastosuj** jest szary, dopóki nie ma zmian.
- [ ] **Enter** / Zastosuj → status „Zatwierdzono", stworek w playgroundzie przebiera się w nową wersję.
- [ ] Zatwierdzenie **kończy rzeźbienie**: podgląd znika, kamera przesiada się za stworka
      w grze, a WSAD od razu nim rusza. Na ekranie zostaje jedno ciało.
- [ ] **Tab** przełącza tam i z powrotem; powrót do rzeźbienia przywraca podgląd.
- [ ] Zejdź z płyty edytora i spróbuj zatwierdzić → „wejdz na plyte edytora, zeby zatwierdzic".
- [ ] Spam Enter → „za szybko — odczekaj chwile".

## Sterowanie
> Wejście jest względem **kamery**, a stworek obraca się w stronę, którą wciskasz.

- [ ] **PPM + mysz** obraca kamerę wokół stworka; sam stworek stoi w miejscu.
- [ ] Wciśnięcie kierunku **obraca stworka** ku niemu płynnie — bez przeskoku
      i bez sunięcia bokiem.
- [ ] Przy zawracaniu (**S**) stworek najpierw kręci się w miejscu, a rusza dopiero
      gdy jest już zwrócony w tamtą stronę.
- [ ] Prędkość narasta w miarę ustawiania się na kurs, zamiast wskakiwać od razu.
- [ ] Puszczenie klawisza daje **wyhamowanie**, a nie zatrzymanie jak ucięte nożem.
- [ ] Pchnięcie (**F** od drugiego stwora) realnie odrzuca i wybrzmiewa — pęd nie znika
      w następnej klatce.
- [ ] Cięższy stworek rozpędza się i hamuje **wolniej** niż lekki.
- [ ] Kombinacje (**W+A**, **W+D**…) dają ukosy i **nie są szybsze** niż marsz prosto.
- [ ] Po obróceniu kamery ten sam klawisz prowadzi zgodnie z **nowym** ujęciem.
- [ ] Stworek zawsze idzie tam, gdzie ma **oczy i pysk** — nigdy ogonem naprzód.
- [ ] Stworek obraca się wokół **środka tułowia**, a nie wokół własnego nosa.
- [ ] Dołożenie kręgów nie przesuwa stwora względem kamery — zaczepienie zostaje
      w środku ciała, a tusza rośnie symetrycznie.

## Zawieszenie
- [ ] Tusza **wisi** nad gruntem; między brzuchem a ziemią widać prześwit na nogi.
- [ ] Stworek nie osiada powoli w dół po zespawnowaniu ani nie drga w pionie.
- [ ] Zejście z krawędzi płyty edytora daje opadnięcie i miękkie wyprostowanie nóg,
      a nie sztywne wbicie w podłoże.
- [ ] Stworek bez nóg leży brzuchem na ziemi — zawieszenie nie ma czego prostować.
- [ ] Wejście **jedną** nogą na próg podnosi stworka od razu, a nie dopiero gdy
      przeszkoda znajdzie się pod jego środkiem.
- [ ] Cięższy stworek (więcej kręgów i części) daje się popchnąć **słabiej** niż lekki —
      masa z genomu trafia do fizyki.

## Chód proceduralny
> Cykl kroku i solver IK są przypięte testami. Tu chodzi o to, czy chód **wygląda** jak chód.

- [ ] Stworek **stoi na nogach**, a nie leży brzuchem na ziemi — pod tułowiem jest prześwit.
- [ ] Stopy **dotykają gruntu**, a nie wiszą nad nim ani nie muskają go czubkiem.
- [ ] Nogi zostają **zgięte** w spoczynku — nie prostują się na sztywno na pełny wyprost.
- [ ] Nogi mają widoczne zgięcia; kolano ugina się **do przodu**, nie w bok ani do tyłu.
- [ ] W marszu stopy stawiają się **na przemian**, a nie obunóż.
- [ ] Stopa stojąca na ziemi **nie ślizga się** — zostaje w miejscu, dopóki jej nie podniesie.
- [ ] Zatrzymanie zatrzymuje przebieranie nogami; ruszenie wznawia je płynnie.
- [ ] Krępe nogi (1 zgięcie) chodzą **skocznie**, z wysokim podnoszeniem stopy.
- [ ] Smukłe/kopytne (2 zgięcia) idą zwykłym krokiem ze zginanym kolanem.
- [ ] Płetwy i macki (3–4 zgięcia) suną **nisko i płynnie**, owadzim ruchem.
- [ ] Więcej zgięć = wyższa prędkość w panelu statystyk.
- [ ] Wymiana nóg w edytorze i **Zastosuj** przebudowuje nogi — nie zostają stare człony.

## Powalanie
- [ ] Podejdź do drugiego stworka i naciśnij **F** — cel się przewraca.
- [ ] Ragdoll wygląda jak upadek, a nie jak eksplozja ani jak zacięcie w miejscu.
- [ ] Leżący stworek **nie reaguje na WSAD**.
- [ ] Po ~2 s wstaje płynnie (wtopienie do pozy spoczynkowej, nie skok w jednej klatce).
- [ ] Stworek wstaje **pionowo** i stoi na podłodze, nie w niej.
- [ ] Dwa leżące ragdolle **nie przenikają się nawzajem** ani nie popychają cudzych kapsuł.

## Sieć (dwie instancje)
> Wymaga buildu albo ParrelSync / Unity MPPM. Tego jednego automat nie zastąpi.
- [ ] Drugi klient dołącza przez **Start Client** i widzi stworka pierwszego gracza.
- [ ] Zatwierdzenie u klienta A zmienia stworka A **u obu**.
- [ ] Klient dołączający **po** zatwierdzeniu widzi wersję aktualną, nie startową.
- [ ] Brak „gumowania" przy ruchu (predykcja i uzgadnianie działają).
- [ ] Popchnięcie u A przewraca cel **u obu**, a po wstaniu stworek stoi w **tym samym miejscu** na obu instancjach.
      Pozy kości w trakcie leżenia **mogą się różnić** — to celowe, synchronizowana jest tylko pozycja wstania.
- [ ] Rozłączenie klienta despawnuje jego stworka u pozostałych.

## Ograniczenia, których nie wolno naruszyć

- [ ] `NetworkManager` → `TimeManager` → **`Physics Mode` musi zostać `Unity`**, nie `TimeManager`.

  Przy `TimeManager` menedżer predykcji woła `SimulatePhysics` w pętli powtórek uzgadniania,
  a to uruchamia globalne `Physics.Simulate` — resymulując **każdą** bryłę w scenie bez resetu
  stanu. Ragdolle chodziłyby kilkakrotnie za szybko i eksplodowały. Pilnuje tego test
  `CreatureKnockdownTests.PhysicsMode_StaysUnity`, ale warto wiedzieć, **dlaczego** tam jest.

- [ ] `Project Settings → Player → **Run In Background** musi zostać włączone.`

  Bez tego klient po alt-tabie zamraża pętlę gry i wypada z serwera. Wyłączenie tej opcji
  zawiesza też testy PlayMode w sposób wyglądający na zwiechę całego edytora.

- [ ] Macierz kolizji (`Project Settings → Physics`): `CreatureRagdoll` koliduje **wyłącznie**
  z `Default` (podłoże). Włączenie kolizji z `Creature` sprawia, że ragdoll popycha predykowane
  kapsuły i wywołuje walkę uzgadniania.

## Po teście
- [ ] Wyjdź z Play Mode.
