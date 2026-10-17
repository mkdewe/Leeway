# Leeway Online (`com.leeway.online`)

Warstwa usług online niezależna od platformy: tożsamość gracza, lobby, zaproszenia i „adres hosta”
dla netcode'u. Backendy podłącza się jak wtyczki — gra rozmawia wyłącznie z interfejsami.

Pakiet nie zależy od kodu gry (można go wynieść do osobnego repo). Wymaga UniTask.

## Assembly

| Assembly | Zawartość | Kiedy się kompiluje |
|---|---|---|
| `Leeway.Online` | abstrakcje (`IOnlinePlatform`, `ILobbyService`, `ILobby`, `ISocialService`), `OnlineResult`, `LobbyQuery`, `NetworkEndpoint`, backendy **Offline** i **Local** | zawsze |
| `Leeway.Online.Steam` | backend Steam (Steamworks.NET) | gdy jest pakiet `com.rlabrecque.steamworks.net`; tylko PC/Editor; `DISABLESTEAMWORKS` wyłącza |
| `Leeway.Online.UnityServices` | backend Unity Gaming Services (Authentication + Lobby) | gdy jest pakiet `com.unity.services.multiplayer` |
| `Leeway.Online.FishNet` | most do FishNet: `TransportAdapter` (strategia per schemat endpointu), `TransportRouter`, adapter Tugboat | gdy jest FishNet |
| `Leeway.Online.FishNet.Steam` | adapter FishySteamworks (Steam relay / P2P) | gdy są FishNet + FishySteamworks + Steamworks.NET |
| `Leeway.Online.Tests` | testy EditMode (kontrakt lobby na backendzie Local) | testy |

Warunkowa kompilacja idzie przez `versionDefines` w asmdefach — żadnych ręcznych `#define`.

## Wzorce

- **Fasada** — `IOnlinePlatform` skupia identity/lobby/social jednego backendu.
- **Fabryka abstrakcyjna jako asset** — `OnlinePlatformProvider` (ScriptableObject). Gra trzyma listę
  providerów; backend niekompilowany na danej platformie staje się brakującym skryptem i jest pomijany.
- **Łańcuch odpowiedzialności** — `OnlinePlatformBootstrapper` próbuje providerów po kolei
  (np. Steam → Local), zawsze kończąc na `OfflinePlatform`.
- **Null Object** — `OfflinePlatform`, `NullLobbyService`, `NullSocialService`: brak funkcji = błąd
  `NotSupported`, nigdy `null`.
- **Template Method** — `LobbyBase` / `LobbyServiceBase`: wspólne księgowanie członków, właściciela,
  zdarzeń; backend dopisuje tylko `Read…/Write…`.
- **Adapter** — `SteamCallResults` zamienia call-resulty Steama na `UniTask`.
- **Builder** — `LobbyQuery.Create()...Build()`.
- **Result** — operacje online zwracają `OnlineResult` zamiast rzucać wyjątki (anulowanie też).
- **Strategy** — `TransportAdapter` per schemat (`udp`, `steam`, …).

## Przepływ lobby → mecz

Lobby to czysty stan platformy — w lobby nikt nie jest połączony netcodem. Host przy starcie:
podnosi serwer, publikuje endpointy (`__endpoints`, np. `steam|7656…|0;udp|192.168.1.5|7770`),
przestawia dane lobby na „w grze”. Klienci widzą zmianę danych i łączą się z pierwszym endpointem,
który obsługują ich transporty. Dołączenie do lobby, które już gra, łączy od razu (late join, invite).

## Nowy backend (EOS, PSN…)

1. Nowy folder + asmdef (`Leeway.Online.Eos`) z `versionDefines` na pakiet SDK i `includePlatforms`.
2. `EosLobby : LobbyBase`, `EosLobbyService : LobbyServiceBase`, `EosPlatform : IOnlinePlatform`
   (deklaruje `Capabilities` — UI samo ukryje to, czego nie ma).
3. `EosPlatformProvider : OnlinePlatformProvider` z ustawieniami SDK.
4. Jeśli platforma ma własny transport: `TransportAdapter<TTransport>` w asmdefie mostu.
5. Asset providera dopisać do `NetworkSessionSettings` w kolejności preferencji.

Testy kontraktu z `LocalPlatformTests` są dobrym szablonem do sprawdzenia nowego backendu.

## Steam — uwagi

- App ID ustawia się na assecie `SteamPlatformProvider` (domyślnie 480 = Spacewar, tylko do testów).
  Platforma sama ustawia zmienną `SteamAppId` dla procesu; `steam_appid.txt` jest zbędny (i w `.gitignore`).
- Inicjalizację Steam API robi wyłącznie `SteamOnlinePlatform` — nie dodawaj `SteamManager`.
- Bez działającego klienta Steam bootstrapper przechodzi na następnego providera (w projekcie: Local).

## Unity Gaming Services — uwagi

- Wymaga projektu podpiętego do Unity Cloud (Project Settings → Services) i włączonego Lobby w Dashboardzie.
  Bez tego inicjalizacja się nie uda i bootstrapper przejdzie dalej.
- Logowanie anonimowe. **Profil** Authentication jest wybierany per instancja edytora (hash `Application.dataPath`),
  więc główny edytor, gracze Multiplayer Play Mode i klony ParrelSync to różni gracze. Buildy deweloperskie
  dostają nowy profil przy każdym starcie; `-ugs-profile <nazwa>` nadpisuje wybór.
- Kod lobby (`ILobby.JoinCode`) to 6-znakowy kod UGS; `JoinAsync` przyjmuje kod albo pełne id.
- Wyszukiwanie po stronie serwera działa tylko dla kluczy zindeksowanych (S1…S5, lista na assecie providera);
  reszta filtrów jest sprawdzana lokalnie.
- Zapisy (`SetData`, `SetLocalMemberData`, `SetJoinable`) są od razu widoczne lokalnie i wysyłane jednym
  żądaniem na klatkę. Dzięki temu para „endpointy → faza” dociera do gości razem i nie łamie limitów Lobby.
- Zmiany przychodzą przez eventy lobby (Wire), a zapasowo przez polling. Host wysyła heartbeat co 15 s.
- Brak czatu (Lobby go nie ma; to robi Vivox) i brak Relay: host publikuje adres LAN (Tugboat). Działa na jednym PC
  i w jednej sieci. Przez internet potrzebny jest transport z Relay (np. FishyUnityTransport + adapter).
