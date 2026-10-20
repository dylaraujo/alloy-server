# Task 08: Scale Lobby Directory to 10 Worlds

Status: **not started**
Depends on: all of [`02`](02-join-menu-and-lobby-directory.md)–[`07`](07-remove-moderation-scaffolding.md) — this is the last task, done once v1 (one world) is fully working
Design doc reference: [`TRAINER_DESIGN.md` §6.1b, §5.7](../TRAINER_DESIGN.md)
Repos touched: `alloy-server` (ops/config), not expected to need `AlloyClient` changes

## Goal

Go from the single `GameServer` v1 used for every prior task to 10
independent worlds, each its own isolated Nexus, all listed in the Join
menu with live player counts.

## Why this is last

Every other task in this plan is designed to work against a single world
with no cross-world logic. This task is intentionally **pure scale-out,
not new architecture** — if it turns out to need real code changes beyond
config/process count, that's a sign something in an earlier task
(`02-join-menu-and-lobby-directory.md` especially) assumed single-world
state that shouldn't have been assumed, and is worth revisiting rather than
patching around here.

## Scope
- Run 10 `GameServer` processes, each with its own port (already
  configurable per `gameServerConfig.xml`) and its own independent `Nexus`
  singleton — no shared state between them.
- Each registers with `AccountServer` on boot via the existing IPC
  registration path (extended in Task 02 to include a display name and
  live player count).
- `AccountServer`'s world-list HTTP route (added in Task 02) should
  already return however many worlds are currently registered — confirm
  it handles 10 correctly (ordering, display names like "Training Ground 1"
  … "Training Ground 10") rather than needing new logic.
- Decide process-management approach: manually started processes (simplest,
  matches "friends training together" scale) vs. a supervisor script.
  Not expected to need `AccountServer` to spawn/kill `GameServer` processes
  dynamically for v1 — that's a further scale-out, not part of this task.

## Definition of done
- [ ] 10 `GameServer` processes run simultaneously, each isolated (players
      in one don't see or affect another).
- [ ] Join menu lists all 10 with accurate live player counts.
- [ ] Joining different worlds from two clients confirms isolation (can't
      see each other across worlds).
- [ ] No code changes were needed beyond what Task 02 already built —
      if changes *were* needed, note what and why here, since it likely
      means Task 02's design should be revisited for future scale-ups.
