# Known TODOs

Loose ends that aren't tied to a task in [`tasks/`](tasks/). Add new items here
and remove them when done.

- **STs and sets.** The "ST" tier tag is never shown (the Armory Kiosk, inventory
  and tooltips only produce "T#" or "UT"). The client has no item-set data: no
  set field on `ItemDesc`, no set markers in the item XML, and the check in
  `ItemTile.UpdateTierTag` is commented out. Needs a decision on which items
  belong to sets and where that is stored, then the tag and any set bonuses.
- **Stats show -1 on load.** Loading in as any class shows -1 for the stats in the
  client. Not yet investigated; check what `InitPlayer` / `DbClient.BuildCharacter`
  put in `Character.Stats` and how the client reads those stat values.
