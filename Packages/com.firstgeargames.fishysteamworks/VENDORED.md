# FishySteamworks (vendored)

Źródło: https://github.com/FirstGearGames/FishySteamworks — tag `4.1.1`, licencja BSD-2-Clause (`LICENSE.txt`).

Oryginalne repo nie zawiera assembly definition, więc nie da się go zainstalować jako pakiet UPM z gita.
Jedyne zmiany względem upstreamu:

- dodany `Runtime/FishySteamworks.asmdef` (referencje: `FishNet.Runtime`, `com.rlabrecque.steamworks.net`),
- usunięty `SteamManager.unitypackage` — inicjalizację Steam API robi `Leeway.Online.Steam`,
- dodany ten plik i `package.json`.

Aktualizacja: podmień zawartość `Runtime/` na nowszą z upstreamu, zachowaj asmdef.
