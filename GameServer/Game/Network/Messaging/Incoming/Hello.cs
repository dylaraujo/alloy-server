using System;
using System.IO;
using System.Linq;
using System.Text;
using Common;
using Common.Database;
using Common.Database.Models;
using Common.Network;
using Common.Resources.Config;
using Common.Resources.Xml;
using Common.Utilities;
using GameServer.Game;
using GameServer.Game.Network;
using GameServer.Game.Network.Messaging;
using GameServer.Game.Network.Messaging.Outgoing;
using GameServer.Game.Worlds;

namespace GameServer.Game.Network.Messaging.Incoming;

[Packet(PacketId.HELLO)]
public record Hello : IIncomingPacket {
    public string BuildVersion;
    public int GameId;
    public string MapJSON;
    public int MapJSONLength;
    public string Name;

    public void Read(ref SpanReader rdr) {
        BuildVersion = rdr.ReadUTF();
        GameId = rdr.ReadInt32();
        Name = rdr.ReadUTF();
        MapJSONLength = rdr.ReadInt32();
        MapJSON = Encoding.UTF8.GetString(rdr.ReadBytes(MapJSONLength));
    }

    public async Task Handle(User user) {
        if (BuildVersion != GameServerConfig.Config.Version) {
            user.SendFailure(Failure.INCORRECT_VERSION, GameServerConfig.Config.Version);
            return;
        }
        
        var acc = user.GameInfo.Account;
        var chr = user.GameInfo.Char;
        if (user.State != ConnectionState.Reconnecting) {
            var name = Name?.Trim();
            if (!IsValidName(name)) {
                user.SendFailure(Failure.DEFAULT, "Invalid name.");
                return;
            }

            acc = CreateSession(user, name);
            chr = DbClient.BuildCharacter(0, GetDefaultClass(), 0);
        }

        if (acc == null || chr == null) {
            user.SendFailure(Failure.DEFAULT, "Invalid user state.");
            return;
        }
        
        if (GameServerConfig.Config.AdminOnly && !acc.IsAdmin) {
            user.SendFailure(Failure.DEFAULT, "Admin only server.");
            return;
        }
        
        RealmManager.Worlds.TryGetValue(GameId, out var world);
        
        if (GameId == World.TEST_ID) {
            user.SendFailure(Failure.FORCE_CLOSE_GAME, "Not available");
            return;
            // TODO: Test world
            // if (!acc.IsAdmin) {
            //     user.SendFailure(Failure.FORCE_CLOSE_GAME, "Only players with admin permissions can make test maps.");
            //     return;
            // }
            //
            // if (MapJSONLength < 1 || string.IsNullOrEmpty(MapJSON)) {
            //     user.SendFailure(Failure.FORCE_CLOSE_GAME, "Invalid test map data.");
            //     return;
            // }
            //
            // var mapFolder = $"{GameServerConfig.Config.WorldsDir}/testMaps"; // Save map in directory
            //
            // if (!Directory.Exists(mapFolder))
            //     Directory.CreateDirectory(mapFolder);
            //
            // File.WriteAllText($"{mapFolder}/{acc.Name}_{DateTime.Now.Ticks}.jm", MapJSON);
            //
            // world = new TestWorld("Test");
            // world.DisplayName = $"{acc.Name}'s Test World";
            // world.LoadJsonMap(MapJSON, world.DisplayName);
            // RealmManager.AddWorld(world);
        }
        else if (world == null) {
            user.SendFailure(Failure.DEFAULT, $"Invalid target world: {GameId}");
            return;
        }
        
        var seed = (uint)new Random().Next(1, int.MaxValue);
        user.SetGameInfo(acc, seed, world);
        
        user.SendPacket(new MapInfo(
            world.Map.Data.Width,
            world.Map.Data.Height,
            world.Config.Name,
            world.DisplayName,
            seed,
            world.Config.Background,
            world.Config.ShowDisplays,
            world.Config.AllowTeleport,
            world.Music,
            world.Config.Difficulty));

        if (world.Deleted) {
            user.SendFailure(Failure.DEFAULT, "Invalid world.");
            return;
        }

        user.Load(chr, world);
    }

    private const int MaxNameLength = 16;

    private static bool IsValidName(string name) {
        if (string.IsNullOrEmpty(name) || name.Length > MaxNameLength)
            return false;
        return name.All(c => char.IsLetterOrDigit(c) || c == ' ' || c == '_' || c == '-');
    }

    private static ushort GetDefaultClass() {
        var desc = XmlLibrary.Id2Player("Wizard") ?? XmlLibrary.PlayerDescs.Values.First();
        return desc.ObjectType;
    }

    private static Account CreateSession(User user, string name) {
        return new Account {
            Id = user.Id,
            Name = name,
            CreatedAt = DateTime.UtcNow,
            Stats = new AccountStats {
                ClassStats = XmlLibrary.PlayerDescs.Keys.Select(type => new ClassStats { ObjectType = type }).ToList()
            }
        };
    }
}