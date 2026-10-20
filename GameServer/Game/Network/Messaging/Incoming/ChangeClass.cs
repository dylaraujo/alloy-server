using Common.Database;
using Common.Network;
using Common.Resources.Xml;
using GameServer.Game.Worlds;

namespace GameServer.Game.Network.Messaging.Incoming;

[Packet(PacketId.CHANGECLASS)]
public record ChangeClass : IIncomingPacket {
    public ushort ClassType;

    public async Task Handle(User user) {
        if (user.GameInfo.State != GameState.Playing || user.GameInfo.World.Id != World.NEXUS_ID)
            return;

        if (!XmlLibrary.PlayerDescs.ContainsKey(ClassType) || user.GameInfo.Char.ObjectType == ClassType)
            return;

        user.ChangeClass(DbClient.BuildCharacter(user.GameInfo.Char.CharId, ClassType, 0));
    }

    public void Read(ref SpanReader rdr) {
        ClassType = rdr.ReadUInt16();
    }
}
