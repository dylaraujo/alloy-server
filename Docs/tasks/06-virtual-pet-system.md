# Task 06: Virtual Pet System

Status: **not started**
Depends on: [`02-join-menu-and-lobby-directory.md`](02-join-menu-and-lobby-directory.md)
Design doc reference: [`TRAINER_DESIGN.md` §5.4, §6.4](../TRAINER_DESIGN.md)
Repos touched: `AlloyClient`, `alloy-server` (`GameServer`)

## Goal

A basic pet system that is purely a stat/ability modifier — **no sprite,
no rendering, no in-world presence at all**. One pet per player, with 3
independently-leveled stats. Set via a Nexus Pet Kiosk.

**Explicitly deferred to later research** (don't block this task on these):
which 3 stat families to expose, the exact level curve/cap, and whether to
eventually add a visual pet. Pick a reasonable placeholder set to get the
plumbing working end-to-end; revisit the specifics once this task is done.

## Current state (for context)

- **No pet system exists server-side at all.** `Character.PetType`
  (`Common/Database/Models/Character.cs`) exists as a field but is never
  read or written anywhere else in the codebase — confirmed via repo-wide
  search.
- **No pet UI/rendering exists client-side either.** No pet yard, pet
  stats panel, or pet networking packets.
- Client does have unused content assets implying pets were once planned:
  `AlloyClient/Content/Xmls/Pets.xml`, `Permapets.xml` — worth a quick look
  for inspiration on stat-family naming/tiers, but not required reading
  since this is a from-scratch virtual system, not a port of that data
  model.
- This is the only fully-greenfield task in the plan — there's nothing to
  reuse on either side beyond general patterns (`EntityStats` for applying
  bonuses, packet patterns from other tasks).

## Scope

### Server
- New lightweight structure on the live player entity — e.g. a
  `PetLoadout` holding 3 slots of `(StatFamily, Level)`. In memory only,
  never persisted (consistent with the stateless model — Task 02).
- New packet, e.g. `SetPetLoadout` (incoming): client sends the 3
  stat-family/level pairs; server validates ranges and applies stat
  bonuses the same way equipment bonuses are applied today via
  `EntityStats` — just sourced from the pet loadout instead of an item.
- A level of 0 for a given stat slot means that stat is off.
- Pick a placeholder set of 3 stat families for v1 (e.g. attack power,
  defense, and a support/utility stat) — this is intentionally not
  over-researched; swap it out later without needing to touch the
  plumbing (packet, in-memory structure, kiosk UI) built in this task.

### Client
- Add a Pet Kiosk entity type to the Nexus map/content.
- New Pet Kiosk panel (triggered via the same `InteractPanel` proximity
  pattern as the other kiosks): 3 independent level controls (sliders or
  steppers), one per stat slot. No pet yard scene, no sprite/animation
  work.
- Changing a level sends `SetPetLoadout`; effects should be reflected
  immediately (e.g. via whatever stat-update packet equipment changes
  already trigger).

## Definition of done
- [ ] Pet Kiosk panel shows 3 independent level controls and lets the
      player set each one.
- [ ] Setting a level applies the corresponding stat bonus to the live
      player entity immediately, with no sprite/visual pet anywhere in
      the world.
- [ ] Setting a level to 0 removes that stat's bonus.
- [ ] Nothing is persisted — reconnecting resets pet levels to 0/off.
- [ ] Works on both Windows and macOS.
