# Training Grounds Of The Mad God — Design Document

Status: **draft for discussion** — not yet approved for implementation.

## 1. Goal

Turn Alloy from a ROTMG-style persistent MMO into **Training Grounds Of The
Mad God**: a low-friction sandbox where you pick a class, instantly gear up
with anything in the game, optionally bring a pet, and jump into dungeon
fights with friends — with no accounts, no grinding, and no persistent
progression.

This is explicitly **not** "ROTMG with easier progression." Progression,
economy, and account identity are not being rebalanced — they're being
removed. What's left should feel like a practice/training mode: character
builds exist only to test them, not to earn them.

## 2. Core Pillars

1. **Instant class access** — any class, any time, switched in the Nexus
   with no unlock cost and no re-rolling a character.
2. **Full gear access in Nexus** — every item in the game is reachable from
   Nexus, for free, equipped instantly. No vendors, no currency, no RNG loot
   gating entry to a build.
3. **Pet, simplified** — one basic pet per player, with pet *level* freely
   adjustable (not earned via feeding/fusing).
4. **Dungeons are the content** — Nexus is a lobby covered in portals.
   Walking into one takes you (and whoever's with you) straight to a boss
   fight.
5. **No persistent progression** — no XP, levels, fame, gold, vault, guilds,
   leaderboards, or long-term account state. A session's build is disposable.
6. **Cross-platform** — both client and server run on Windows and macOS, not
   just Windows. Details and known gaps in §9.
7. **Multiple independent worlds (lobbies)** — launching the game shows a
   "Join" menu listing a fixed number of worlds (eventually 10, each its
   own isolated Nexus instance — v1 ships with just one, see §6.1b). You
   set your display name here. Grab a world, invite friends into that same
   one, train together — without every player on the server landing in one
   shared Nexus.

## 3. What this replaces (current state → target state)

| System | Current state (verified in code) | Target state |
|---|---|---|
| Login | Dual-hop: HTTP `AccountServer` verifies account, then TCP `GameServer` *re-verifies* the same password on `HELLO`. LiteDB-backed, password, bans, account locking. | Replaced by a **world/lobby select menu**: no password, just a display name and a pick from a fixed list of worlds (see §5.1/§5.7). |
| Server/world list | Client already has `ServersTitleScreen`/`ServerRect` reading a `ServerListData`, used today to pick a backend before logging in. `AccountServer` already tracks every live `GameServer` via IPC (`IpcServer.Clients`, `GetGameServer()` RPC). | Repurposed as the "Join" menu: lists the fixed set of worlds (start with 10), each backed by its own running `GameServer` process (or world instance), queried live from `AccountServer`. |
| Character creation | `Create` packet → `DbClient.CreateCharacterAsync` persists a `Character` row, seeds stats from `newCharsConfig.xml`, copies `PlayerDesc.Equipment` as starting gear. | Picking a class in the Nexus just re-skins/respecs the *live* player entity — no DB row, no "creation" step. |
| Class switching | Does not exist in-game. Class is chosen once at character creation (`ClassContainer` overlay, pre-login only). | `ClassContainer` reused as an in-Nexus overlay. New packet replaces live entity's class, base stats, and equipment slots on the spot. |
| Gearing up | Mob loot (mostly commented out) or vendors (`EntityType.Merchant` has **no handler in the live GameServer** — vendor code only exists in the legacy `GameServerOld`). | A Nexus "Armory" kiosk lets you equip anything directly, via `EntityInventory.SetItem`, bypassing loot/vendor paths entirely (they're unimplemented anyway). |
| Pets | `Character.PetType` field exists but is **never read or written anywhere**. No pet entity, component, or UI, client or server. | New, minimal, purely **virtual** (no sprite/rendering): one pet per player with 3 independently-leveled stats, no feeding/fusing/rarity system. |
| Dungeon entry | Static portal entities, each permanently wired at world-load to **one shared `World` instance** (`PortalData`) — not spawned per party. Matches how `Realm` portals work in Nexus today. | ROTMG-style wave instancing: a portal opens an instance on first entry and stays "live" for 60s, so a party can follow in; after 60s it switches, and the next entrant opens a fresh instance (§5.5). |
| Progression (XP/fame/gold/vault/guilds) | Already mostly **unbuilt**, not built-and-working: no `AddExp`/level-up code found, fame-on-death handler is stubbed, vendors need gold that's irrelevant without vendors. | Remove the account-side storage and UI for it; don't build the missing pieces. Characters spawn at a fixed, fully-specced power level. |
| Platform support | Server has **zero** Windows-specific API usage in active code and no pinned `RuntimeIdentifier`. Client has two real macOS gaps: audio native-library path resolution throws on macOS (`InternalUtils.GetAudioBinaryPath()`), and the offline font-atlas builder is Windows-exe-bound. Everything else (incl. Windows perf P/Invokes) is already OS-guarded correctly. | Fix the audio path gap; verify IPC/DB on macOS; playing the game works identically on both OSes (§9). Font-atlas rebuilding on macOS is out of scope for now. |

This matters for scoping: a lot of what sounds like "ripping out progression"
is actually "not building progression that was already half-missing." The
real work is the *new* stuff — class switching, the Armory, and pets — not
demolition.

## 4. Session flow (target)

1. **Launch** → "Join" menu: a text field to set your display name, and a
   list of worlds (start with 10 — "Training Ground 1" … "Training Ground
   10"), each showing how many players are currently in it. No login, no
   password, no character list.
2. **Pick a world** → connect straight into that world's Nexus as a default
   class. Nothing is remembered between sessions (fully stateless, §6.3) —
   reconnecting always starts fresh. Friends who pick the same world land
   in the same Nexus as you; other worlds are fully isolated (different
   players, different portal instances).
3. **In Nexus:**
   - Walk up to the **Class Kiosk** → `ClassContainer`-style overlay opens →
     pick any class → live-swapped (stats, equip slots, base gear reset to
     that class's default loadout).
   - Walk up to the **Armory** → browse all items (filterable by slot/tier)
     → click to equip instantly. No cost, no cooldown, no inventory limit
     beyond normal slot rules.
   - Walk up to the **Pet Kiosk** → toggle the (invisible) pet on/off, set
     levels for each of its 3 stats independently → effects apply
     immediately. There's no pet sprite in the world — it's a pure stat/
     ability modifier.
   - Walk into any **dungeon portal** scattered around Nexus → if the
     portal is mid-"wave" (someone opened it &lt;60s ago), you join their
     instance; otherwise you open a fresh one and the 60s window starts
     (§5.5).
4. **In a dungeon:** normal combat — the trainer changes don't touch combat
   resolution, enemy behaviors, or boss scripts. Multiple players can be in
   the same dungeon instance together (party play), same mechanism as today.
5. **Death / leave:** respawn back in that world's Nexus immediately, same
   build you had (no item loss, no death penalty — this is a trainer, not a
   roguelike run).

## 5. System-by-system plan

### 5.1 Identity / connection / world select
- The "Join" menu replaces `LoginContainer`/`CharacterListScreen` entirely.
  It reuses the existing `ServersTitleScreen`/`ServerRect` pattern (already
  built to list backends and let the player pick one before connecting) —
  just repointed at a lobby list instead of a login-gated server list, plus
  a name field where `LoginContainer`'s username field used to be.
- `AccountServer` keeps its IPC-hub role (§5.7) but drops password/account
  verification: no `DbClient.VerifyAccount`, no `Account.LockOwner`, no
  bans/mutes tied to an account row. The `HELLO` packet on `GameServer`
  carries a display name instead of username+password.
- Multiplayer still needs *a* name so players can see each other and party
  up — it's just chosen at the Join menu, not tied to any persisted account.
- **Fully stateless (§6.3, resolved):** no `Character`/`Account` DB row is
  created or persisted for a trainer session at all. Class, gear, and pet
  config live only in memory on the live entity for the lifetime of the
  connection. Disconnecting and reconnecting always starts fresh (default
  class, no pet, no gear) — there is nothing to restore.

### 5.2 Classes
- Reuse `PlayerDesc` (`XmlLibrary.PlayerDescs`) as-is — it already has base
  stats and default `Equipment` per class; no new data format needed.
- New server packet (e.g. `ChangeClass`) that: validates the class exists,
  resets the live player entity's `StatType`s to that class's base stats,
  replaces equipped slots with that class's default `Equipment`, and pushes
  the usual `Update`/stat-sync packets to the client.
- Client: lift `ClassContainer`/`CharacterWheel`/`ClassInfo` out of the
  pre-login flow and present them as an `Overlay` triggered by interacting
  with the Nexus Class Kiosk (same `InteractPanel`/proximity pattern already
  used for portals and vendors).

### 5.3 Armory (full loot access)
- New Nexus entity type (Armory kiosk) that opens a **browse-all-items**
  panel — essentially a static, free "vendor" over the full `XmlLibrary.ItemDescs`
  table, grouped by slot/tier instead of a rotating stock list.
- Clicking an item calls `EntityInventory.SetItem`/`SetItems` on the player's
  own inventory server-side (already enforces slot-type compatibility and
  syncs dirty slots) — no new inventory engine work needed, just a new
  packet + kiosk UI.
- This deliberately does not touch the (broken) `Merchant` entity type or the
  (mostly disabled) loot-table behaviors — it's a parallel, simpler path.

### 5.4 Pets — virtual, no sprite (§6.4, resolved for v1)
- One pet per player, **purely virtual** — no `Pet` entity, no rendering,
  no in-world presence at all. It's a stat/ability modifier attached
  directly to the player, nothing more.
- Structure: 3 stats, each with its **own independent level** (e.g. pick 3
  slots from the classic ROTMG pet-ability families — attack, defense,
  support/heal-type — the exact mapping is a follow-up research item, not
  locked in here). A stat at level 0 is effectively off.
- Server: a lightweight `PetLoadout` (3× stat-type + level) on the live
  player entity, in memory only (consistent with §6.3 — never persisted).
  New packet to set/update it; stat bonuses applied the same way equipment
  bonuses are today (`EntityStats`), just sourced from the pet loadout
  instead of an item.
- Client: Pet Kiosk UI = 3 independent level controls (sliders/steppers),
  no yard scene, no pet sprite/animation work needed at all. This is the
  cheapest version of "pets" that still lets you train with pet bonuses
  active.
- **Explicitly deferred for later research:** which 3 stat families to
  expose, their exact level curve/cap, and whether to eventually add a
  visual pet — none of that blocks building the virtual version first.

### 5.5 Dungeon portals — wave instancing with a 60s join timer (§6.2, resolved)
All dungeon portals in Nexus are always "live" (walkable at any time, no
key/cost to open one). Instancing works like real ROTMG's portal lifecycle:

- A portal entity holds: `CurrentInstance : World` (nullable) and
  `OpenedAt : DateTime?`.
- `UsePortal` handling (`GameServer/Game/Network/Messaging/Incoming/UsePortal.cs`):
  - If `CurrentInstance == null` **or** `Now - OpenedAt >= 60s`: create a
    brand-new `World` for that dungeon's `WorldConfig` (same mechanism
    `PortalData.LoadWorld()` uses today), set it as `CurrentInstance`, stamp
    `OpenedAt = Now`. This player opens the wave.
  - Otherwise (instance exists and is within the 60s window): teleport the
    player straight into the existing `CurrentInstance` — they join the
    same party/fight as whoever opened it.
  - Once a player is inside, they stay in that instance for as long as they
    want (the 60s timer only gates *new entries catching up*, it doesn't
    kick anyone or end the run).
- After 60s, the portal "switches": it keeps running the in-progress
  instance for whoever's still inside, but the *next* `UsePortal` on that
  portal entity opens a fresh instance and restarts the timer — exactly
  the classic ROTMG "portal closes, a new one opens" feel, just without a
  separate visible portal-closing animation/entity swap for v1.
- **Needs a visible timer:** the portal's `PortalPanel` (client) should show
  a countdown while a wave is open (e.g. "Closes in 43s") so players know
  whether walking in joins their friends or starts a new instance. Backed
  by the same `OpenedAt` the server already tracks — just surfaced in the
  portal's status packet/tooltip.
- Optional follow-up once the basics work: wire up `WorldConfig.LongLasting`
  (currently parsed but inert) to clean up an instance once it's both past
  its window *and* empty, instead of leaking `World` objects forever.

### 5.6 Progression removal
- No `Account`/`Character` DB rows at all for trainer sessions (§6.3,
  resolved) — `AccountStats`, `Character.Level/XpPoints/CurrentFame`,
  `VaultChests`, `Guild` membership simply have nothing to attach to, so
  there's no migration/cleanup step, just don't create the rows.
- Leave combat, enemy behaviors, and boss scripts untouched — "no
  progression" means no meta-progression, not easier fights.

### 5.8 Admin/moderation scaffolding removal (§6.5, resolved)
- Strip the partial ban/mute/kick scaffolding in `GameServer/Game/Chat/Commands/ModCommands.cs`
  (most of it is already commented-out/unported TODOs — this is deletion,
  not a port) and collapse the `CommandPermissionLevel` tier system down to
  whatever's actually needed for trainer-specific commands (e.g. a single
  "dev" tier for give-item/set-class/set-pet-level debug commands, if those
  are exposed as chat commands at all rather than only kiosk UI).
- No bans, mutes, or account-tied moderation — there's no account to
  moderate in a stateless, nameplate-only identity model anyway.

### 5.7 Lobby / world select ("Join" menu)
- **Client:** replace `TitleScreen` → `LoginContainer` → `CharacterListScreen`
  with a single `JoinScreen`: a name field (persisted locally like today's
  settings, not account-bound) plus a list of worlds, each rendered with
  `ServerRect`-style tiles (reusing the existing server-list UI pattern) —
  name, current player count, and a "Join" button. Selecting one connects
  the TCP socket directly to that world's `GameServer` and sends `HELLO`
  with the chosen display name instead of credentials.
- **Server topology — start simple:** 10 worlds = 10 separate `GameServer`
  processes, each booting its own independent `Nexus` singleton, listening
  on its own port (`gameServerConfig.xml` already supports a configurable
  port per instance). No cross-world state sharing needed — this matches
  "isolated lobbies" exactly and requires zero new multi-tenancy code inside
  `GameServer` itself.
- **`AccountServer`'s new job:** instead of account verification, it becomes
  purely a **lobby directory** — each `GameServer` process already registers
  itself over IPC on startup (`WebServerRpc.GameServerConnected`); extend
  that registration to include a display name ("Training Ground 3") and
  live player count, and add one new HTTP route the client's `JoinScreen`
  calls to fetch the current list of up-to-10 worlds + their player counts.
  This reuses `IpcServer.Clients`/`GetGameServer()` almost as-is.
- Player counts update live by having each `GameServer` push its count on
  connect/disconnect (small addition to `IGameServerRpc`/`IWebServerRpc` in
  `Common/Messaging/Proxies.cs`), so the Join menu doesn't need polling.

## 6. Decisions

All open questions from the first draft are now resolved for v1. Kept as a
record of *why*, since some (6.1b, 6.4) intentionally defer detail to later.

### 6.1 Login step — resolved
- No password/account verification. `AccountServer` stays alive as the
  **lobby directory** (§5.7) rather than being removed outright. Display
  name is set client-side at the Join menu and carried in `HELLO`; no
  account DB row is created for it.

### 6.1b World provisioning — resolved: Option A, scoped down to **one server for v1**
- Long-term: Option A (fixed `GameServer` processes, one per world,
  registering with `AccountServer` on boot) — config/ops change, not new
  code, matches friends-training-together scale.
- **Near-term scope cut:** don't build/test against 10 worlds yet. Get the
  Join menu, lobby directory, and one `GameServer` world fully working
  first. Going from 1 → 10 afterward is just running more processes with
  different config/ports — no architecture change needed once one world
  works end-to-end.

### 6.2 Dungeon instancing model — resolved: wave instancing, 60s join timer
- Neither pure "always shared" nor pure "always fresh per party." Portals
  are always live; the first entrant opens an instance and a 60-second
  window during which anyone else who walks in joins *that same* instance;
  after 60s, the portal switches and the next entrant opens a new instance.
  Full mechanics in §5.5, including the client-visible countdown.

### 6.3 Cross-session persistence — resolved: fully stateless / in-memory
- Nothing persists across disconnect/reconnect. No DB row per player at
  all for trainer sessions. Every connect starts at default class, no
  gear, no pet. Simplest possible model, and consistent with "disposable
  builds" in §1.

### 6.4 Pet scope — resolved for v1: virtual-only, 3 independently-leveled stats
- Literally no sprite/rendering — a pure stat/ability modifier on the
  player. 3 stats, each with its own level. Exact stat families, curve,
  and cap are explicitly deferred to follow-up research (§5.4) — not
  blocking the first implementation.

### 6.5 Moderation/admin tooling — resolved: remove the scaffolding
- Delete the partial ban/mute/kick code and collapse the permission-tier
  system to just what trainer-specific dev commands need. No account-tied
  moderation, consistent with §6.3's stateless model. Details in §5.8.

## 7. Explicit non-goals

- No account registration or password-based login — only a display-name
  field on the Join menu.
- No economy (gold, fame, vault, trading, guilds).
- No XP/leveling/unlocks of any kind — everything is available from minute one.
- No loot RNG as a gate to power — the Armory *is* the loot system now.
- No death penalties beyond "you're back in Nexus."

## 8. Suggested implementation order

**v1 target: one world, fully working, with all five pillars.** Scaling to
10 worlds is a config/deployment step after v1, not part of this sequence.

Each step below is tracked as its own task doc in `Docs/tasks/`, in the
order they should be done — macOS compatibility is deliberately first so
every later task is developed/tested on both platforms from the start, not
ported to Mac at the end.

1. [`01-macos-compatibility.md`](tasks/01-macos-compatibility.md) — fix the
   audio path gap (§9.2) and verify server IPC/DB on macOS. Low risk, and
   unblocks everything after it being developed/tested on a Mac.
2. [`02-join-menu-and-lobby-directory.md`](tasks/02-join-menu-and-lobby-directory.md) —
   Join menu + lobby directory, backed by **one** `GameServer` (§5.1/§5.7).
   The new entry point; also where stateless `HELLO` (no password) and
   `AccountServer`'s account-verification removal happen.
3. [`03-class-kiosk.md`](tasks/03-class-kiosk.md) — Class Kiosk +
   `ChangeClass` packet (reuses the most existing code on both sides —
   lowest risk, immediate payoff).
4. [`04-armory-kiosk.md`](tasks/04-armory-kiosk.md) — Armory kiosk +
   item-grant packet (reuses `EntityInventory.SetItem`, biggest "feel" win
   for a trainer).
5. [`05-dungeon-portal-wave-instancing.md`](tasks/05-dungeon-portal-wave-instancing.md) —
   Dungeon portals with wave instancing + 60s timer (§5.5) — the one
   genuinely new piece of engine code in this phase.
6. [`06-virtual-pet-system.md`](tasks/06-virtual-pet-system.md) — Virtual
   pet system (§5.4) — no rendering work, just the stat loadout + packet +
   kiosk sliders.
7. [`07-remove-moderation-scaffolding.md`](tasks/07-remove-moderation-scaffolding.md) —
   Cleanup pass: remove ban/mute/kick scaffolding and collapse permission
   tiers (§5.8).
8. [`08-scale-to-ten-worlds.md`](tasks/08-scale-to-ten-worlds.md) — **Then:**
   scale the lobby directory from 1 → 10 worlds (§6.1b) — pure
   config/process-count change, no new code expected.

## 9. Cross-platform support (Windows + macOS)

Both client and server must run on Windows and macOS — not "Windows, with
macOS best-effort." A direct audit of both repos found the stack is already
**mostly** cross-platform; the remaining gaps are small and enumerated below
rather than requiring a rework.

### 9.1 Server (`alloy-server`) — already effectively cross-platform
- Zero Windows-specific API calls (`RuntimeInformation`/`OSPlatform`/P-Invoke/
  Registry) found anywhere in the active `Common/`, `AccountServer/`, or
  `GameServer/` projects (the legacy `GameServerOld/` wasn't audited and
  isn't in scope per `AGENTS.md`).
- No `RuntimeIdentifier` pinned in any active `.csproj` — builds are
  framework-dependent `net10.0`, not OS-locked.
- `Common/Messaging/IpcServer.cs`/`IpcClient.cs` use `NamedPipeServerStream`/
  `NamedPipeClientStream` for `AccountServer`↔`GameServer` IPC — .NET
  implements these over Unix domain sockets on macOS/Linux, so this should
  already work, but **hasn't been exercised on macOS yet**. Treat as
  "verify," not "known broken."
- LiteDB and StreamJsonRpc are both cross-platform by design.
- Config/resource path loading already resolves via `AppContext.BaseDirectory`
  + `Path.Combine` (fixed in `265a0e3`), not hardcoded Windows-style paths.
- **Action:** mostly verification — run `AccountServer` + `GameServer`
  end-to-end on macOS once and confirm the IPC pipe and DB file both come up
  cleanly, rather than expecting code changes.

### 9.2 Client (`AlloyClient`) — two real gaps, both small
- **Audio — real bug:** `Alloy.Audio/Utils/InternalUtils.cs:GetAudioBinaryPath()`
  only resolves a native OpenAL binary path for Linux/FreeBSD/Windows and
  **throws `NotSupportedException` on macOS** — there's no `OSPlatform.OSX`
  branch. This is purely a missing path mapping, not a missing library: the
  `Silk.NET.OpenAL.Soft.Native` NuGet package already ships `libopenal.dylib`
  for both `osx-x64` and `osx-arm64` (confirmed present in the build
  output). Fix is a one-branch addition mapping macOS → `osx-x64`/
  `osx-arm64` → `libopenal.dylib`.
- **Windows-only perf tuning — already safe, no fix needed:**
  `Alloy.Engine/GameWindow.cs`'s "Windows Jank" region P/Invokes
  `kernel32`/`winmm.dll` to pin thread affinity and raise timer resolution.
  Both call sites are already guarded by `OperatingSystem.IsWindows()`
  (`GameWindow.cs:77-78, 113-114`), so on macOS these are simply skipped —
  confirmed non-issue, noted only so no one "fixes" it unnecessarily.
- **Offline asset pipeline — lower priority, not needed to play:**
  `Alloy.ContentBuilder`'s `FontBuilder` (MSDF font-atlas generation) shells
  out to a bundled Windows-only `msdf-atlas-gen-w64.exe`. It already has a
  fallback (look for a native `msdf-atlas-gen` on `PATH`, or run under
  Wine) but that path is untested. This only matters for rebuilding font
  atlases on a Mac — it doesn't block running or playing the game, since
  built atlases ship as regular content. The content builder's other
  native deps (`libglfw`, `libstbi`, `libassimp`) already have macOS
  binaries present in the build output.

### 9.3 Definition of done for "runs on Mac"
- `dotnet run --project AccountServer` and `dotnet run --project GameServer`
  both come up cleanly on macOS, IPC pipe connects, LiteDB file opens.
- `AlloyClient` launches on macOS, audio initializes without the
  `NotSupportedException` (i.e. the §9.2 fix is in), and a full
  Join → Nexus → gear up → dungeon loop works end-to-end identically to
  Windows.
- Font/atlas rebuilding on macOS is explicitly **not** part of this bar —
  only playing/running the game is.
