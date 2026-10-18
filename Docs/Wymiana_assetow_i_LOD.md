# Wymiana modeli, tekstur i poziomy detalu (LOD)

Gra jest przygotowana na to, że modele, tekstury i materiały będą stopniowo podmieniane na te od grafików.
Nic w kodzie nie zależy od konkretnego mesha - zależy od **konwencji** poniżej.

## Modele z LOD-ami

1. Nazwij węzeł (albo jego rodzica) z sufiksem `_LOD0`, `_LOD1`, `_LOD2`… (np. `Drzewo_LOD0`, `Drzewo_LOD1`).
   Wszystko pod takim węzłem należy do tego poziomu. Węzły bez sufiksu (np. `Fruit`) są rysowane na każdym poziomie.
2. Alternatywnie: prefab z gotowym `LODGroup` - zostaje użyty bez zmian.
3. Numery nie muszą być ciągłe (`_LOD0` i `_LOD3` to dwa poziomy).
4. Model bez poziomów dostaje jeden poziom i jest ukrywany, gdy zajmuje mało miejsca na ekranie.

Obsługują to: rośliny (`IslandFlora`), propsy wyspy niescalane, a dla scalanych w chunki (`CombineMeshes`)
chunk dostaje własny `LODGroup` złożony z uproszczonych meshy wszystkich propsów w nim.
Dla scalanych propsów **do chunka trafiają tylko meshe odpowiedniego poziomu** - nie trzeba już wyłączać scalania
dla prefabów z LOD-ami.

## Kiedy poziom się zmienia

`Assets/_Project/Core/Rendering/Resources/LodProfile.asset` (menu Create → Leeway → Rendering → Lod Profile):
progi przejść (udział wysokości ekranu), próg znikania i globalny `lodBias`. Te same liczby dla wszystkich modeli,
więc nowy model z inną liczbą poziomów działa bez żadnej konfiguracji.

## Shadery i GPU Resident Drawer

`Leeway/Toon` ma wsparcie DOTS instancing, więc obiekty z tym shaderem rysuje GPU Resident Drawer
z GPU occlusion culling (włączone w `PC_RPAsset`). Warunki, by nowy materiał z tego korzystał:

- shader `Leeway/Toon` (lub nowy shader z tym samym blokiem `UNITY_DOTS_INSTANCING_START`),
- brak `MaterialPropertyBlock` na rendererze (taki renderer wraca do zwykłego rysowania),
- nowa właściwość materiału = dopisać ją do bloku DOTS w `Toon.shader`.

Wymiana tekstur i materiałów nie wymaga nic poza zachowaniem nazw właściwości (`_BaseMap`, `_BaseColor`, `_EmissionColor`).

## Czego ta konwencja NIE robi

- Baked occlusion culling: wyspa jest generowana w czasie gry, więc nie ma czego wypiec. Zastępuje go GPU occlusion culling.
