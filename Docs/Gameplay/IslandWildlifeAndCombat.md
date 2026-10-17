# Pierwsze populacje i walka na wyspie
## Uruchomienie
Otwórz `Assets/Scenes/CreatureEditor.unity`, włącz Play i uruchom hosta z NetworkHUD.
W panelu wzorców edytora wybierz gatunek oznaczony „(wyspa)”, zmień go dowolnie i zastosuj.
Wyjdź poza krąg edytora: chroni on budującego gracza przed walką.
Populacje tworzy wyłącznie serwer; klienci otrzymują zwykłe sieciowe ciała i genomy.

## Gatunki
| Gatunek | Budowa z katalogu | Siedlisko i zachowanie |
| --- | --- | --- |
| Mchoryjek | Niski tułów, cztery ssacze nogi, kopyta, mała paszcza | Łąki; dwie grupy po trzy; ucieka przed łowcą i bliskim graczem |
| Biegus trzcinowy | Smukły tułów, para ptasich nóg, łapy, duże oczy | Niskie wybrzeża; dwie grupy po trzy; płochliwy |
| Zębacz rdzawy | Duża paszcza, cztery ptasie nogi na łapach | Wyższe tereny; dwie pary; poluje na mniejsze gatunki i graczy, ranny ucieka |
| Rogacz skalny | Gruby tułów, cztery kopytne nogi, róg | Wyżyny; para; broni okolicy domu i reaguje na atak |
| Chwytacz potokowy | Pionowy tułów, dwie nogi, ramiona z dłońmi ośmiornicy | Suche brzegi potoków i jezior; grupa trzech; unika drapieżników |

Wzorce: `Assets/_Project/Features/Combat/Data/Species/`.
Porównanie rzeczywistych modeli z edytora: `Assets/Screenshots/island_species_concepts.png`.
Gatunki są poprawnymi genomami gracza; nie mają ukrytych, osobnych statystyk.

## Sterowanie
- LPM: główny atak wynikający z budowy.
- R: róg, jeżeli istnieje; w przeciwnym razie ramiona lub atak główny.
- 1 / 2 / 3: ugryzienie / zamach ramion / pchnięcie rogiem. Brak części blokuje zdolność.
- Q: czujność z oczu; przez pięć sekund zwiększa zasięg oznaczeń zwierząt; odnowienie osiem sekund.
- E: ramionami podnieś przewróconego sojusznika; paszczą lub ramionami żeruj na pokonanym zwierzęciu.
  Żerowanie przywraca do 22% maksymalnego HP, zużywa ciało raz i nie przekracza maksymalnego HP.
- Spacja: skok wynikający z nóg. F: dotychczasowe odepchnięcie. PPM: obrót kamery.

Ataki mają wspólne odnowienie, osobny zamach, zasięg i łuk trafienia. Róg ma dłuższe ostrzeżenie,
węższy łuk i silniejszy odrzut. Teren blokuje trafienia, własna drużyna i strefa edytora są chronione.
Przewrócenie lub śmierć przerywa zamach. Serwer wybiera cel i obrażenia; klient przesyła tylko rodzaj zdolności.
Zwierzę pozostaje jako ciało przez 40 sekund. Brakujące zwierzę odradza się po dalszych 60 sekundach.
Gracz wraca po sześciu sekundach z tym samym genomem. Ręcznie malowana tekstura skóry nie jest obecnie
przenoszona przez odrodzenie; zachowane są kolory i wzór zapisane w genomie.

## Strojenie
`Assets/_Project/Features/Combat/Data/IslandPopulation.asset`: liczby i wielkości grup, zakresy wysokości,
nachylenie terenu, bliskość słodkiej wody, promień terytorium, limit populacji i bezpieczny promień obozu.
Domyślnie 21 zwierząt, limit 24. Gdy siedlisko nie mieści grupy, plan nie wymusza spawnu w wodzie.
Na obiekcie CreatureEditorGameManager komponent NpcCreatureSpawner wskazuje profil, wyspę i prefab.
Jedno ciało powstaje co 0,25 sekundy. NPC używają skóry do 512 px i podejmują decyzje pięć razy na sekundę.
AI kieruje zwykłym kontrolerem stworzenia, omija wodę i ostre uskoki oraz wraca na terytorium.
To lokalne sterowanie terenem, bez pełnego wyszukiwania ścieżek wokół całych gór i rzek.
Wartości ciosów wynikają z `BodyAbilityRules`, umiejscowienia i skali części oraz `CombatTuning`.

## Sprawdzanie
EditMode: IslandWildlifeRulesTests, CombatRulesTests, EnemyGenomeTests, NpcCreatureBehaviorTests.
PlayMode: IslandWildlifePlayTests — rzeczywisty host, populacje, trafienia po zamachu, odnowienia,
przerwanie ataku, sojusznicy, ataki od tyłu, przeszkody, pomoc, żerowanie i odrodzenie gracza.
Testy hosta nie zastępują sesji dwóch osobnych klientów z opóźnieniem sieciowym.
