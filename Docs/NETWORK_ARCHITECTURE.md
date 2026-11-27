# Wild Bunch — Network Architecture

## 1. Архитектурная модель
Сетевой мультиплеер построен по схеме **Server-Authoritative Listen Server**:
- Хост игровой сессии одновременно выполняет роль сервера и локального участника (Host / Client 0).
- Подключение до 3 внешних клиентов осуществляется через транспорт `FizzySteamworks`, использующий защищенные сокеты Valve (`SteamNetworkingSockets`) и сквозное пробитие NAT через Steam Datagram Relay (SDR).
- Сервер полностью авторитетен над состоянием игрового мира:
  - Формирование сида процедурного банка (`LevelGenerator`).
  - Прочность и момент разрушения стен (`DestructibleWall`).
  - Поведение, выбор целей и спавн охранников (`EnemyManager` / `EnemyController`).
  - Содержимое кузова фургона, расчет добычи, квот и проверка Дона (`GameManager`).
- Клиенты отправляют команды-запросы (`[Command]`), интерполируют позиции персонажей (`NetworkTransformReliable`) и локально воспроизводят эффекты (звуки FMOD, партиклы, анимации, покачивание камеры).

---

## 2. Структура модулей (`Assets/Scripts/Networking/`)

### 2.1 Core (`Assets/Scripts/Networking/Core/`)
* **`NetworkSessionManager.cs`**
  * Наследник `Mirror.NetworkManager`.
  * Автоматически инстанциируется перед загрузкой сцен через `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`.
  * Настроен на транспорт `FizzySteamworks`, управляет спавном сетевого префаба игрока `NetworkPlayer.prefab` и распределяет стартовые позиции вокруг точек появления.

### 2.2 Steam (`Assets/Scripts/Networking/Steam/`)
* **`SteamBootstrap.cs`**
  * Инициализирует `SteamAPI.Init()` перед запуском игры.
  * Безопасный fallback: при отключенном Steam или отсутствии файла `steam_appid.txt` переключает игру в `Offline Mode` без падений.
  * Обрабатывает тики Steam callbacks (`SteamAPI.RunCallbacks()`) и события оверлея / приглашений.
* **`SteamUserData.cs`**
  * Загружает Steam-никнеймы и среднеразмерные аватарки друзей (`Texture2D`) с инверсией RGBA-буфера для движка Unity.

### 2.3 Lobby (`Assets/Scripts/Networking/Lobby/`)
* **`SteamLobbyManager.cs`**
  * Создает и управляет Steam Matchmaking лобби (`ELobbyType.k_ELobbyTypeFriendsOnly`).
  * **Изоляция Spacewar 480:** задает метаданные:
    * `game_id`: `volgiant_wild_bunch`
    * `game_version`: текущая версия сборки
    * `protocol_version`: версия сетевого протокола
    * `session_state`: фаза игры ("Lobby", "Heist", "Motel", "BossRoom")
    * `host_steam_id`: 64-битный Steam ID хоста
  * Валидирует метаданные при входе, блокируя подключение к посторонним сессиям Spacewar.
  * Открывает окно приглашения друзей Steam.

### 2.4 Players (`Assets/Scripts/Networking/Players/`)
* **`NetworkPlayerIdentity.cs`**
  * Разделяет локального и удаленного игрока:
    * **`isLocalPlayer == true`:** активна камера, `AudioListener`, скрипты ввода `PlayerMovement`, `PlayerCamera`, `PlayerInteraction`. 3D-тело переводится в режим `ShadowsOnly` (отбрасывает тени, но не заслоняет обзор).
    * **`isLocalPlayer == false`:** камера, слушатель и ввод отключены. 3D-тело полностью видимо, над головой создается парящий 3D-неймплейт с никнеймом, смотрящий на локальную камеру.
* **`NetworkItemSync.cs`**
  * Синхронизирует активный слот инвентаря (`[SyncVar] activeSlotIndex`), показывая молот или оружие в руках удаленного персонажа.
  * Синхронизирует наклон взгляда по вертикали (`LookPitch`) и анимации приседания (`IsCrouching`).

### 2.5 World (`Assets/Scripts/Networking/World/`)
* **`NetworkWorldState.cs`**
  * Синхронизирует детерминированный сид уровня `m_LevelSeed` через `[SyncVar]`.
  * Синхронизирует флаг начала тревоги `m_IsHeistActive`.
  * Синхронизирует переменные экономики: `currentDay`, `operationTargetQuota`, `accumulatedOperationMoney`, `depositedMoney`.
  * Содержит RPC:
    * `RpcBreakWallAtPosition(Vector3 pos)` — синхронный взрыв/пролом стены для всех клиентов.
    * `RpcTeleportTeam(Vector3 pos, Quaternion rot)` — сброс и телепортация `CharacterController` всей команды.
    * `RpcShowDonResults(...)` — синхронное открытие окна отчета Дона у всех участников.
    * `RpcShowLoadingScreen(bool show)` — синхронный черный экран загрузки.
  * Содержит Commands:
    * `CmdDamageWallAtPosition(Vector3 pos, int damage)` — запрос клиента на урон по стене.
    * `CmdDamageEnemy(uint enemyNetId, int damage, Vector3 attackerPos)` — запрос клиента на урон по охраннику.
    * `CmdRequestExtraction()` — запрос клиента на завершение вылазки у фургона.

### 2.6 Debug (`Assets/Scripts/Networking/Debug/`)
* **`NetworkDebugOverlay.cs`**
  * Внутриигровой оверлей на клавише **F8**.
  * Отображает: статус Steam, никнейм, режим Host/Client, Steam ID лобби, Ping / RTT, список участников лобби.
  * Быстрые кнопки: создание лобби, приглашение друзей, выход.
