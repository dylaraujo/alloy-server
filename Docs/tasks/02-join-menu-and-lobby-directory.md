# Task 02: Join Menu & Lobby Directory

Status: **implemented; server flow verified over raw TCP/HTTP, client builds but the Join screen itself has not been run through a GUI session yet**
Depends on: [`01-macos-compatibility.md`](01-macos-compatibility.md)
Design doc reference: [`TRAINER_DESIGN.md` §5.1, §5.7, §6.1, §6.1b](../TRAINER_DESIGN.md)
Repos touched: `TOTMGClient` (`AlloyClient`), `TOTMGServer` (`AccountServer`, `GameServer`, `Common`)

## Goal

Replace the current login flow (HTTP account verification → TCP re-verification
→ character list) with a single "Join" menu: set a display name, pick a
world from a list, connect straight into that world's Nexus. No password,
no account DB row, no character list screen.

**v1 scope cut:** build and test against **one** world/`GameServer` only.
Scaling the lobby list to 10 worlds is Task 08, after everything else works.

## Current state (for context)

- Client flow today: `TitleScreen` → `LoginContainer` (username/password,
  HTTP `AppEngine`/`AccountServer` call) → `CharacterListScreen` →
  `GameScreen`. Server list exists already as `ServersTitleScreen`/
  `ServerRect`, reading `ServerListData`.
- Server flow today: `AccountServer` verifies account over HTTP, then
  `GameServer`'s `HELLO` packet handler (`GameServer/Game/Network/Messaging/Incoming/Hello.cs`)
  re-verifies the same password via `DbClient.VerifyAccount`, which also
  acquires the cross-process `Account.LockOwner`.
- `AccountServer` already tracks every connected `GameServer` over IPC
  (`Common/Messaging/IpcServer.cs`'s `Clients`, `GetGameServer()` RPC in
  `Common/Messaging/Proxies.cs`).

## Scope

### Client
- New `JoinScreen` replacing `TitleScreen` → `LoginContainer` →
  `CharacterListScreen`:
  - A name field. Store it locally (same pattern as today's `Settings`/
    `account.xml`, not account-bound).
  - A list of worlds, rendered with `ServerRect`-style tiles (reuse the
    existing component): world name + live player count + "Join" button.
  - Selecting a world connects the TCP socket directly to that world's
    `GameServer` and sends `HELLO` with the display name — no HTTP call to
    `AccountServer` for login.
- Remove/bypass `RegisterContainer` (no account registration at all).

### Server — `GameServer`
- `Networking/Packets/Outgoing/Hello.cs` (client) and
  `GameServer/Game/Network/Messaging/Incoming/Hello.cs` (server): change
  payload from username+password to a display name string. Drop the call
  to `DbClient.VerifyAccount` entirely for trainer mode — no account
  lookup, no `LockOwner`, no ban check tied to an account row.
- No `Create`/`Load` character packets needed in the old sense — on
  connect, spawn the player entity directly into that world's Nexus with a
  default class (Task 03 adds the actual class-switch UX on top of this).

### Server — `AccountServer`
- Remove the account-verification HTTP routes used by login
  (`AccountServer/Handlers/Account/{Register,Verify}.cs`) from the trainer
  flow — either delete them or leave unused (your call at implementation
  time; deleting is cleaner given §6.3's "fully stateless" decision, since
  there's no account system left to serve).
- Add one new HTTP route: list current worlds + live player counts. Backed
  by `IpcServer.Clients`/`GetGameServer()` — for v1 this list has exactly
  one entry.
- Extend the `GameServerConnected`/IPC registration
  (`Common/Messaging/Proxies.cs`, `IGameServerRpc`/`IWebServerRpc`) so each
  `GameServer` reports a display name (e.g. "Training Ground 1") and
  pushes live player count on connect/disconnect, so the Join menu doesn't
  need to poll.

## Implementation notes

- **Ephemeral session, not a DB row.** `HELLO` builds an in-memory `Account`
  (`Id = User.Id`, never persisted) and a `Character` via the new
  `DbClient.BuildCharacter` factory (also used by `CreateCharacterAsync`), so
  the rest of GameServer, which reads `GameInfo.Account`/`Char`, is untouched.
  The default class is `Wizard` (falls back to the first loaded `PlayerDesc`).
  Task 03 swaps this out.
- `HELLO` now auto-spawns: after `MapInfo` the server calls `user.Load`. The
  server `Load`/`Create` packet handlers and the client `Load`/`Create`
  packets were deleted; reconnects (portals) reuse the session's account/char.
- Display name rules (server + client): 1–16 chars, letters/digits/space/`_`/`-`.
  Names are not required to be unique.
- RPC: `GameServerConnected` now carries name/address/port/max players/admin-only
  and is called *after* the socket server starts; new `PlayerCountChanged` is
  pushed from `RealmManager.UserConnected/UserDisconnected`. `AccountServer`
  keeps them in `IpcServer.Worlds` and drops the entry in `Close()`.
- `AccountServer`: new `POST /lobby/list` (same `<Servers>` XML the old
  `ServerListData` parsed). Deleted `Account/{Register,Verify,PurchaseCharSlot}`
  and `Char/List`, plus the `LockOwner` cleanup in `Close()`/startup. Still
  present and account-based (Task 07/cleanup): `Char/{Delete,Fame}`, `Guild/*`,
  `Legends/*`.
- Client: `JoinScreen` (name field persisted in `settings.xml`, tiles built from
  `ServerRect`, refresh/exit buttons) replaces `TitleScreen`,
  `ServersTitleScreen`, `CharacterListScreen`, `LoginContainer`,
  `RegisterContainer`, `AccountOverlay`; `LoginData`/`AccountData`/
  `CharacterListData`/`NewsData` and the local `account.xml` handling are gone.
  A tile click joins (there is no separate "Join" button). Death and
  disconnect return to `JoinScreen`.
- Fixed while here: `User.Unload(false)` threw a `NullReferenceException` (and
  killed the server) when a connection was dropped before world assignment,
  e.g. any rejected `HELLO`.
- Known gap: a rejected `HELLO` (bad name/version) closes the socket but the
  `Failure` packet does not reach the client, so the client just returns to
  the Join screen with no message.

## Definition of done
- [ ] Launching the client shows the Join menu (name field + one world
      listed with a live player count), not the old login screen.
- [ ] Setting a name and clicking "Join" connects directly to `GameServer`
      and spawns into Nexus — no password prompt anywhere.
- [ ] `AccountServer` serves the world list over HTTP, sourced from its
      live IPC registry, not a hardcoded list.
- [ ] No `Account`/`Character` DB row is created for a trainer session.
- [ ] Two clients with different names can join the same world and see
      each other in Nexus.
- [ ] Works on both Windows and macOS (per Task 01).
