# Task 03: Class Kiosk (In-Nexus Class Switching)

Status: **not started**
Depends on: [`02-join-menu-and-lobby-directory.md`](02-join-menu-and-lobby-directory.md)
Design doc reference: [`TRAINER_DESIGN.md` §5.2](../TRAINER_DESIGN.md)
Repos touched: `AlloyClient`, `alloy-server` (`GameServer`)

## Goal

Let a player switch to any class, any time, by interacting with a Class
Kiosk entity in Nexus — no unlock cost, no re-creating a character. Switch
is instant and live: stats, equipment slots, and base gear reset to that
class's default loadout immediately.

## Current state (for context)

- `PlayerDesc` (`Common/Resources/Xml/Descriptors/PlayerDesc.cs`, loaded
  via `XmlLibrary.PlayerDescs`) already has everything needed per class:
  `SlotTypes`, default `Equipment`, base `Stats`. No new data format
  needed.
- Client already has a fully decoupled class-select overlay used today
  only at character creation: `ClassContainer` (overlay) +
  `CharacterWheel` (carousel) + `ClassInfo` (detail panel showing starting
  equipment via `EquippedGrid`'s preview-only constructor) — all under
  `AlloyClient/Screens/Components/CharacterSelection/` and
  `AlloyClient/Screens/Components/Containers/ClassContainer.cs`.
- Today's only class-assignment path is `Create` (new character) →
  `DbClient.CreateCharacterAsync`, which copies `classDesc.Equipment` into
  `chr.ItemTypes`. There's no "change an existing live player's class"
  packet yet — closest precedent is `Reskin.cs` (visual/skin change only).

## Scope

### Server
- New packet, e.g. `ChangeClass` (incoming), alongside the existing
  `Create`/`Load`/`Reskin` handlers under
  `GameServer/Game/Network/Messaging/Incoming/`.
- Handler validates the requested class type exists in
  `XmlLibrary.PlayerDescs`, then on the live player entity:
  - Resets `StatType`s to that class's base stats.
  - Replaces equipped slots with that class's default `Equipment` (same
    copy-into-slots logic `DbClient.CreateCharacterAsync` uses today, just
    applied to a live entity instead of a freshly-created `Character` row).
  - Pushes the usual `Update`/stat-sync packets so the client reflects the
    change immediately.
- No DB write at all (consistent with the stateless model) — this just
  mutates in-memory entity state.

### Client
- Add a Class Kiosk entity type to the Nexus map/content (placement is
  content work, not engine work — one new object in the Nexus `.jm`/XML).
- Lift `ClassContainer`/`CharacterWheel`/`ClassInfo` out of the pre-login-
  only flow and present them as an `Overlay` triggered by interacting with
  the kiosk — reuse the same `InteractPanel`/proximity pattern already
  used for portals (`Game/Components/Hud/InteractPanel.cs`,
  `Game/Components/Hud/Panels/PortalPanel.cs`).
- "Play"/"Select" button in `ClassContainer` now sends the new
  `ChangeClass` packet instead of setting `GlobalData.CharacterType` for a
  pending character creation.

## Definition of done
- [ ] Walking up to the Class Kiosk in Nexus opens the class-select
      overlay (same visual as today's character-creation screen).
- [ ] Picking a class live-swaps the player's stats and equipment with no
      disconnect/reconnect.
- [ ] Switching classes repeatedly works with no leftover state from the
      previous class (e.g. no stale equipment in a slot the new class
      doesn't use).
- [ ] No DB writes occur as a result of switching classes.
- [ ] Works on both Windows and macOS.
