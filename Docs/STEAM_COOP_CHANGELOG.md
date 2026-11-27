# Wild Bunch — Steam Co-op Integration Changelog

## [Branch: feature/steam-coop] — Интеграция 4-Player Steam Co-op

### Новые файлы и модули:

#### 1. Инфраструктура и Steam
* **`steam_appid.txt`** — файл идентификатора тестового приложения Steam Spacewar (`480`).
* **`Assets/Scripts/Networking/Config/NetworkSessionConfig.cs`** — централизованная конфигурация сетевой сессии (App ID, Protocol Version, Game ID `volgiant_wild_bunch`, Max Players).
* **`Assets/Scripts/Networking/Steam/SteamBootstrap.cs`** — инициализация Steamworks SDK, вызовы `SteamAPI.RunCallbacks()`, безопасный переход в Offline Mode при отсутствии клиента Steam, подписка на оверлей и приглашения друзей.
* **`Assets/Scripts/Networking/Steam/SteamUserData.cs`** — получение никнеймов и загрузка аватарок Steam в формате `Texture2D`.

#### 2. Сетевое ядро и лобби
* **`Assets/Scripts/Networking/Core/NetworkSessionManager.cs`** — кастомный менеджер сети на базе Mirror и FizzySteamworks с контролем подключений и распределением точек спавна.
* **`Assets/Scripts/Networking/Lobby/SteamLobbyManager.cs`** — создание лобби `FriendsOnly`, валидация метаданных Spacewar 480, обработка входа по приглашению.
* **`Assets/Scripts/Networking/Debug/NetworkDebugOverlay.cs`** — диагностический UI на клавише **F8** с отображением RTT/пинга, данных Steam и кнопками подключения.

#### 3. Сетевые сущности игроков
* **`Assets/Scripts/Networking/Players/NetworkPlayerIdentity.cs`** — сетевое владение персонажем: отключение чужих камер/аудиослушателей, отображение 3D-неймплейтов над головами удаленных игроков.
* **`Assets/Scripts/Networking/Players/NetworkItemSync.cs`** — синхронизация предмета в руках (молот/оружие), угла наклона взгляда (`LookPitch`) и приседания (`IsCrouching`).
* **`Assets/Resources/NetworkPlayer.prefab`** — специализированный сетевой префаб игрока с компонентами `NetworkIdentity`, `NetworkTransformReliable`, `NetworkPlayerIdentity`, `NetworkItemSync`.
* **`Assets/Scripts/Networking/Editor/NetworkPrefabGenerator.cs`** — автоматический генератор префаба `NetworkPlayer` из базового `Player.prefab`.

#### 4. Синхронизация мира и игрового цикла
* **`Assets/Scripts/Networking/World/NetworkWorldState.cs`** — авторитетная синхронизация сида процедурного банка `m_LevelSeed`, статуса тревоги `isHeistActive`, экономики 3-дневного цикла (`currentDay`, `operationTargetQuota`, `accumulatedOperationMoney`, `depositedMoney`) и RPC команд телепортации, разрушения стен и результатов Дона.

---

### Изменения в существующих файлах проекта:

1. **`Assets/Scripts/UI/PauseMenuUI.cs`**
   * Внедрена проверка `IsNetworkSessionActive()`. В мультиплеере открытие паузы отключает локальный ввод и освобождает курсор, **не замораживая** мир (`Time.timeScale = 1f`). Одиночный режим сохраняет `Time.timeScale = 0f`.

2. **`Assets/Scripts/Inventory/PlayerInventory.cs`**
   * Устранен критический баг `Destroy(gameObject)` при обнаружении нескольких экземпляров инвентаря.
   * Статическое свойство `Instance`, подписки на New Input System (`SwitchSlotEvent`, `ScrollSlotEvent`, `DropEvent`) и обновление худа оружия `UpdateWeaponText()` ограничены исключительно локальным игроком (`isLocalPlayer`).

3. **`Assets/Scripts/Player/PlayerHealth.cs`**
   * Обновление полоски здоровья и красные вспышки урона (`UpdateHealthUI()`) изолированы для локального игрока (`isLocalPlayer`), предотвращая появление ложных вспышек урона у тиммейтов.

4. **`Assets/Scripts/Player/PlayerMovement.cs`, `PlayerCamera.cs`, `PlayerInteraction.cs`**
   * Статические ссылки `Instance` теперь устанавливаются строго при `IsLocalPlayer()`, предотвращая перезапись ссылок на камеру и интерактивность при подключении удаленных клиентов.

5. **`Assets/Scripts/Generation/LevelGenerator.cs`**
   * Добавлен публичный метод `SetSeed(int seed)` и флаг `useSpecificSeed`.
   * Перед генерацией комнат и спавном лута вызывается `Random.InitState(currentSeed)`, обеспечивая 100% идентичность банка на всех машинах.

6. **`Assets/Scripts/Environment/DestructibleWall.cs`**
   * На клиентах `TakeDamage` автоматически перенаправляет урон авторитетному серверу через `CmdDamageWallAtPosition`.
   * Сервер производит расчет разрушения и рассылает `RpcBreakWallAtPosition`, гарантируя одновременное разрушение стен у всех игроков.

7. **`Assets/Scripts/Enemies/EnemyManager.cs` & `Assets/Scripts/Enemies/EnemyController.cs`**
   * В `Update()` внедрено разграничение авторитета: расчет ИИ, поиск путей `NavMeshAgent` и логика штурмовых волн выполняются **только сервером**. У клиентов агент NavMesh отключается.
   * `EnemyHealth.TakeDamage` на клиентах отправляет команду серверу `CmdDamageEnemy`.

8. **`Assets/Scripts/Environment/VanConsole.cs`**
   * На клиентах нажатие [E] отправляет запрос `CmdRequestExtraction()` на сервер.

9. **`Assets/Scripts/Core/GameManager.cs`**
   * Метод `TeleportPlayer` переработан для командного перемещения: сдвигает всех участников `activePlayers` с индивидуальными смещениями и рассылает клиентам `RpcTeleportTeam`.
   * В `ExtractionRoutine()` и при показе отчета Дона добавлена рассылка RPC `RpcShowDonResults` и синхронизация экономики через `NetworkWorldState`.
