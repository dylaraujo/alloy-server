# Task 07: Remove Moderation/Admin Scaffolding

Status: **not started**
Depends on: [`02-join-menu-and-lobby-directory.md`](02-join-menu-and-lobby-directory.md) through [`06-virtual-pet-system.md`](06-virtual-pet-system.md) (do this cleanup pass after the feature work, not before)
Design doc reference: [`TRAINER_DESIGN.md` §5.8, §6.5](../TRAINER_DESIGN.md)
Repos touched: `alloy-server` (`GameServer`)

## Goal

Delete the partial ban/mute/kick scaffolding and collapse the
`CommandPermissionLevel` tier system down to just what trainer-specific
dev commands need. No account-tied moderation — there's no account to
moderate in a stateless, name-only identity model (Task 02) anyway.

## Current state (for context)

- `GameServer/Game/Chat/Commands/ModCommands.cs`: `find`/`kick`/`announce`
  are live; large swaths of `kill`/`ban`/`unban`/`mute`/`unmute`/`summon`/
  `closerealm`/`godAll` are commented out as unported TODOs from the
  legacy server — this task is **deletion of dead code**, not porting
  anything.
- `GameServer/Game/Chat/Commands/CreativeCommands.cs` has a `spawn`
  command currently gated at `CommandPermissionLevel.Player` (the lowest
  tier) with a commented-out world-type check — looks accidentally wide
  open; decide whether to keep `spawn` at all for trainer mode (it spawns
  world entities, not inventory items — somewhat redundant once the
  Armory kiosk, Task 04, exists) or fold its useful parts into a proper
  dev-tier command.
- `GameServer/Game/Chat/Commands/DevCommands.cs` has `reloadbehaviors` at
  `CommandPermissionLevel.Developer`.
- `CommandPermissionLevel` tiers (`GameServer/Game/Chat/Commands/Command.cs`)
  currently span `Player` → `Admin`/`Developer` — more granularity than a
  friends-only trainer server needs.
- Related account-side moderation (`BanRecord` lookups in `Hello.cs`,
  mute/ban collections in `Common/Database/Models/`) should already be
  moot after Task 02 removes account verification from `HELLO` entirely —
  double check nothing here still references a dropped account path.

## Scope
- Delete the commented-out TODO command stubs in `ModCommands.cs` rather
  than leaving them as dead code.
- Decide and implement a minimal permission model — e.g. a single "dev"
  tier for whatever debug commands are still useful (give-item/set-class/
  set-pet-level, if exposed as chat commands rather than only kiosk UI),
  collapsing the rest of the tier enum if it's no longer meaningfully used.
- Remove ban/mute data model pieces (`Common/Database/Models/`, any
  `Mutes`/`Bans` LiteDB collections and their `DbClient` indexing) if
  nothing references them anymore after this cleanup.
- Keep `kick`/`find`/`announce` if still useful for a friends server, or
  cut them too — this is a judgment call at implementation time, not
  something locked in by the design doc.

## Definition of done
- [ ] No commented-out/unported TODO command stubs remain in
      `ModCommands.cs`.
- [ ] `CommandPermissionLevel` reflects only the tiers actually in use.
- [ ] `/spawn`'s permission level is deliberately chosen, not left at an
      accidentally-open default.
- [ ] No leftover ban/mute DB models or indexes that nothing references.
- [ ] `dotnet build RealmServer.sln` is clean with no new warnings.
