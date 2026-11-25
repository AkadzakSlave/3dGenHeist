# Wild Bunch — Network Architecture

## 1. Концепция
Архитектура кооператива построена по схеме **Server-Authoritative Listen Server**:
- Хост сессии является одновременно сервером и локальным клиентом (Client 0 / Host).
- До 3 клиентов подключаются к хосту через защищённые P2P-сокеты Steam (`FizzySteamworks` транспорт).
- Сервер авторитетно управляет состоянием мира:
  - Процедурная генерация банка (Server Seed & Rooms layout).
  - Разрушение стен и прочность объектов (`DestructibleWall`).
  - Спавн, синхронизация и поведение охранников (`EnemyController`).
  - Спавн лута, инвентарь грузовика, проверка квот и подсчёт денег.
  - Покупки в магазине и подтверждение транзакций.
- Клиенты отправляют запросы на действия (`Command`), интерполируют позиции персонажей и локально воспроизводят эффекты (визуальные партиклы, звуки, покачивание камеры, виньетки урона).

## 2. Разделение игрока (Player Separation)
Для каждого подключённого игрока инстанциируется префаб `NetworkPlayer`:
- **Локальный игрок (`isLocalPlayer == true`):**
  - Активен `CharacterController`, `PlayerMovement`, `PlayerInteraction`, система ввода (New Input System).
  - Активна локальная камера от первого лица и `AudioListener`.
  - Активен локальный UI (`HeistUI`, прицел, здоровье, инвентарь).
- **Удалённые игроки (`isLocalPlayer == false`):**
  - Камера и `AudioListener` отключены.
  - Скрипты ввода и локального UI отключены.
  - Включена интерполяция позиции/вращения (`NetworkTransform`).
  - Включена видимая 3D-модель персонажа с синхронизацией анимаций (`NetworkAnimator`) и предмета в руках.

## 3. Структура модулей кода (`Assets/Scripts/Networking/`)
- `Core/` — `NetworkSessionManager` (наследник `NetworkManager`), управление состояниями сессии.
- `Steam/` — `SteamBootstrap`, `SteamUserData` (интеграция Steamworks.NET, Steam callbacks).
- `Lobby/` — `SteamLobbyManager` (создание, поиск, фильтрация по game_id `volgiant_wild_bunch`, приглашения друзей).
- `Players/` — `NetworkPlayerIdentity`, `NetworkPlayerController`, `NetworkPlayerInventory`.
- `World/` — Сетевая синхронизация процедурного сида `LevelGenerator`, сетевые хуки для `DestructibleWall` и `LootItem`.
- `Debug/` — `NetworkDebugOverlay` (F8 панель со статусом Steam, пингом, режимом Host/Client).
