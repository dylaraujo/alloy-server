using Common.Network;
using Common.Resources.Xml;
using Common.Resources.Xml.Descriptors;
using Common.Utilities.Collections;
using GameServer.Game.Worlds;

namespace GameServer.Game.Network.Messaging.Incoming;

[Packet(PacketId.GRANTITEM)]
public record GrantItem : IIncomingPacket {
    private const int EquipSlots = 4;
    private const int InvSlots = 20;

    public ushort ItemType;

    public async Task Handle(User user) {
        var world = user.GameInfo.World;
        if (user.GameInfo.State != GameState.Playing || world.Id != World.NEXUS_ID)
            return;

        if (!XmlLibrary.ItemDescs.TryGetValue(ItemType, out var desc) || desc.SlotType == 0)
            return;

        GameLogic.Enqueue(() => {
            if (user.GameInfo.State != GameState.Playing)
                return;

            ref var inv = ref world.EntityInventories.Get(user.GameInfo.PlayerId);
            if (inv.Id == EntityId.Null || !inv.OwnedBy(user.GameInfo.Account.Id))
                return;

            var item = new Item(desc.Root);
            for (var i = 0; i < EquipSlots; i++) {
                if (inv[i] == null && inv.IsEquippable(item, i)) {
                    inv.SetItem(i, item);
                    return;
                }
            }

            for (var i = EquipSlots; i < InvSlots; i++) {
                if (inv[i] == null) {
                    inv.SetItem(i, item);
                    return;
                }
            }
        });
    }

    public void Read(ref SpanReader rdr) {
        ItemType = rdr.ReadUInt16();
    }
}
