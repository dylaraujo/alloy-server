# Task 04: Armory Kiosk (Full Loot Access)

Status: **implemented; both projects build, not yet run in-game (Windows and macOS untested)**
Depends on: [`02-join-menu-and-lobby-directory.md`](02-join-menu-and-lobby-directory.md)
Design doc reference: [`TRAINER_DESIGN.md` §5.3](../TRAINER_DESIGN.md)
Repos touched: `AlloyClient`, `alloy-server` (`GameServer`)

## Goal

A Nexus kiosk where every item in the game is one click away — free,
instant, no currency, no RNG. This is the "gear up before the dungeon"
pillar.

## Current state (for context)

- Mob loot tables are mostly commented out
  (`GameServer/Game/Entities/Behaviors/Library/BehaviorLib.Shore.cs` and
  friends) and `EntityType.Merchant` has **no handler in the live
  `GameServer`** — vendor/buy logic only exists in the legacy
  `GameServerOld`. There is currently no working path to get items into
  your inventory other than the `/spawn` dev command (which spawns world
  entities, not inventory items).
- The reusable primitive that *does* work today:
  `GameServer/Game/Entities/Components/EntityInventory.cs`'s
  `SetItem(int slot, Item item)` / `SetItems(...)` — already enforces
  per-slot `SlotType` compatibility and flags dirty slots for client sync.
  No new inventory engine work is needed; this task is a new packet + new
  kiosk UI on top of existing plumbing.
- Item definitions: `Common/Resources/Xml/Descriptors/Item.cs` /
  `XmlLibrary.ItemDescs`, same XML source the client's tooltips already
  read from.

## Scope

### Server
- New packet, e.g. `GrantItem` (incoming): client sends an item type ID +
  target slot (or let the server pick the first valid slot for that
  item's `SlotType`). Handler resolves `XmlLibrary.ItemDescs[type]`,
  constructs an `Item`, and calls `EntityInventory.SetItem`/`SetItems` on
  the requesting player's own inventory. No cost, no cooldown, no stock
  limit.
- Deliberately does **not** touch `EntityType.Merchant` or the loot-table
  behaviors — this is a parallel, simpler path, not a fix for the broken
  vendor system.
- Basic validation: reject if the player doesn't own the targeted
  inventory (always true for "grant to self" today, but worth an explicit
  check so this can't be used to write into another player's inventory).

### Client
- Add an Armory kiosk entity type to the Nexus map/content.
- New "browse all items" panel triggered by interacting with the kiosk
  (same `InteractPanel` proximity pattern as the Class Kiosk/portals):
  grouped by slot type and/or tier, built from the full `ObjectLibrary`/
  `ItemDescs` table rather than a rotating stock list like the old
  `NexusMerchants.xml` vendors.
- Clicking an item sends `GrantItem`; existing `ItemTile`/tooltip
  components can be reused to render each browsable entry, since they
  already know how to render an item icon + stat tooltip from an
  `ItemDesc`.

## Implementation notes

- `GrantItem` (packet id 67, a `ushort` item type). The server picks the slot:
  the first *empty* equip slot (0-3) that accepts the item's `SlotType`
  (`EntityInventory.IsEquippable`), otherwise the first empty slot in 4-19. It
  never replaces an equipped item, so players can stock spare weapons/tomes.
  Nothing happens if the inventory is full. A fresh `Item` is built from the
  descriptor on every grant, so no instance data is shared. Rejected unless
  the player is in Nexus and the item exists with a non-zero `SlotType`.
  No DB write.
- Content: static object `Armory Kiosk` (`0xd001`, `<Class>ArmoryKiosk</Class>`)
  in the client's `StaticObjects.xml`, placed in `Nexus.jm` at (83, 75), two tiles
  east of the Class Kiosk. Texture is a placeholder (Armoire sprite).
- Client: `ArmoryKioskPanel` (via `InteractPanel`) opens `ArmoryContainer`, which
  lists `ObjectLibrary.TypeToItem` sorted by slot type, tier, then name, with a
  slot-type filter (`<` / `>`) and paging. Tiles are the existing preview-mode
  `ItemTile`, which gained an `OnClicked` callback.
- Known gap: as with the Class Kiosk, the server does not check that the player
  is standing near the kiosk, only that they are in Nexus.

## Definition of done
- [ ] Interacting with the Armory kiosk opens a panel listing every item
      in `XmlLibrary.ItemDescs`, filterable/grouped in some reasonable way
      (slot and/or tier at minimum).
- [ ] Clicking any item equips/grants it to the player instantly, with no
      cost and no stock limit.
- [ ] Granting an item respects slot-type rules (can't force a weapon into
      an armor slot, etc.) — reusing `EntityInventory.SetItem`'s existing
      validation.
- [ ] Repeated granting (swap gear in and out freely) works with no server
      errors or desync between client and server inventory state.
- [ ] Works on both Windows and macOS.
