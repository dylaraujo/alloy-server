# Task 05: Dungeon Portals — Wave Instancing with 60s Join Timer

Status: **not started**
Depends on: [`02-join-menu-and-lobby-directory.md`](02-join-menu-and-lobby-directory.md)
Design doc reference: [`TRAINER_DESIGN.md` §5.5, §6.2](../TRAINER_DESIGN.md)
Repos touched: `AlloyClient`, `alloy-server` (`GameServer`)

## Goal

All dungeon portals in Nexus are always "live" — walk in any time, no key,
no cost. The first player to walk in opens an instance and a **60-second
join window**; anyone else who walks into that same portal during the
window joins the same instance (same fight, with their party). After 60s,
the portal "switches": the in-progress instance keeps running for whoever's
inside, but the next person to walk in opens a **fresh** instance and
restarts the timer.

This is the one genuinely new piece of engine code in the whole plan —
everything else so far has been reusing existing mechanisms.

## Current state (for context)

- Portal entities are a generic component (`PortalData`,
  `GameServer/Game/Entities/Components/PortalData.cs`), managed by
  `PortalDatasManager`. Today, `PortalData`'s constructor calls
  `LoadWorld()` **exactly once** at world-load time, permanently linking a
  static portal entity to **one shared `World` instance** for its entire
  lifetime. There is no per-use instancing at all today.
- Entry flow: client sends `UsePortal`
  (`GameServer/Game/Network/Messaging/Incoming/UsePortal.cs`) with the
  portal's `ObjectId` → server resolves the linked `World` → `user.ReconnectTo(world)`.
- `Vault.cs` (`GameServer/Game/Worlds/Logic/Vault.cs`) is the closest
  existing pattern for "create a fresh instance on demand, keyed by
  something, recreate if the old one is gone" — worth modeling off of,
  generalized from "one per account" to "one per wave."
- `WorldConfig.LongLasting` is parsed from world JSON config but
  **currently inert** — no code reads it. Optional follow-up (not blocking
  this task) to use it for cleaning up empty, expired instances.

## Scope

### Server
- Change `PortalData` (or wrap it) to hold, per portal entity:
  `CurrentInstance : World` (nullable) and `OpenedAt : DateTime?` instead
  of a single permanently-bound `World`.
- `UsePortal` handling:
  - If `CurrentInstance == null` **or** `DateTime.UtcNow - OpenedAt >= TimeSpan.FromSeconds(60)`:
    create a brand-new `World` for that portal's dungeon `WorldConfig`
    (same `LoadWorld()`-style mechanism used today, just invoked per-use
    instead of once), set it as `CurrentInstance`, stamp
    `OpenedAt = DateTime.UtcNow`. This player opens the wave.
  - Otherwise: teleport the player into the existing `CurrentInstance` —
    they join whoever's already in there.
  - Being inside an instance has no time limit — the 60s timer only gates
    whether a *new* `UsePortal` call joins the existing instance or starts
    a new one. It never kicks players already inside.
- Expose remaining window time somewhere the client can read it (e.g. a
  field on the portal's status/update packet, or computed client-side from
  a synced `OpenedAt` timestamp) so the UI countdown (below) has something
  to show.
- Optional follow-up, not blocking: once an instance is both past its 60s
  window *and* empty, use `WorldConfig.LongLasting` (or similar) to clean
  it up instead of leaking `World` objects indefinitely.

### Client
- `Game/Components/Hud/Panels/PortalPanel.cs`: add a visible countdown
  while a wave is open (e.g. "Closes in 43s"), so players can tell at a
  glance whether walking in joins their friends or starts a fresh run.
  Hide/omit the countdown if no wave is currently open (i.e. next entry
  would start a new one).
- No changes needed to the "Enter" button/`UsePortal` send path itself —
  the behavior difference (join vs. new instance) is entirely server-side.

## Definition of done
- [ ] First player to use a dungeon portal creates a new instance and
      starts a visible 60s countdown on that portal.
- [ ] A second player using the same portal within 60s lands in the same
      instance as the first (confirm by seeing each other in the dungeon).
- [ ] A player using the same portal after the 60s window lands in a
      **new**, empty instance — the original instance (if still occupied)
      keeps running independently.
- [ ] Players already inside an instance are never disconnected/moved by
      the timer — it only affects new `UsePortal` calls.
- [ ] Works for at least one full dungeon end-to-end (walk in, fight,
      walk/die back to Nexus) with 2+ simultaneous players.
- [ ] Works on both Windows and macOS.
