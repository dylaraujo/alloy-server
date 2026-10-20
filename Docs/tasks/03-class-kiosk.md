# Task 03: Class Kiosk (In-Nexus Class Switching)

Status: **implemented; verified manually on macOS with two clients (kiosk swap, no leftover entity, other player sees the change); Windows not tested**
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

## Implementation notes

- **Re-enter, don't mutate.** `Entity.ObjectType` is immutable and the client
  keys its sprite/class props off it, so `ChangeClass` (packet id 66, a `ushort`
  class type) makes `User.ChangeClass` unload the player entity and load a fresh
  one built by `DbClient.BuildCharacter` in the same world, same position, then
  re-sends `CreateSuccess` with the new entity id. No reconnect, no DB write
  (the in-memory session `Character` is just replaced).
- Server only accepts it in Nexus (`World.NEXUS_ID`), for a valid class that
  differs from the current one.
- Content: new static object `Class Kiosk` (`0xd000`, `<Class>ClassKiosk`) in
  `StaticObjects.xml`, placed in `Nexus.jm` at (81, 75) next to the spawn pad.
  Texture is a placeholder (reuses the Armoire sprite).
- Client: `ClassKioskPanel` (via `InteractPanel`) opens `ClassContainer(true)`,
  whose button reads "Select" and sends `ChangeClass`. `CreateSuccess` now goes
  through `Map.ResetLocalPlayer` so a second local player can replace the first;
  `HudView.CreatePlayerDependentAssets` already rebuilds the HUD on that.
- Fixed during verification: the old player entity lingered on the client after
  a swap (the server sends no drop for it), so `Map.ResetLocalPlayer` removes it.
  Also set a default system cursor in `Alloy.Engine/GameWindow.cs`; the cursor was
  invisible over the window on macOS.
- Known gap: the server does not check that the player is actually standing near
  a kiosk, only that they are in Nexus.

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
