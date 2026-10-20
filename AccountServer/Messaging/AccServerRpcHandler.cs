using System;
using System.Threading.Tasks;
using Common;
using Common.Game;
using Common.Messaging;
using Common.Structs;
using Common.Utilities;
using StreamJsonRpc;

namespace AccountServer.Messaging;

public class AccServerRpcHandler : IAccountServerHandler {
    private static readonly Logger _log = new Logger(typeof(AccServerRpcHandler));

    public Guid ServerId { get; set; }

    private IGameServerRpc _proxy;

    public void Attach(IGameServerRpc proxy) {
        _proxy = proxy;
    }

    public void Close() {
        IpcServer.Clients.TryRemove(ServerId, out _);
        IpcServer.Worlds.TryRemove(ServerId, out _);
    }
    
    public Task GameServerConnected(Guid gameServerId, string name, string address, int port, int maxPlayers, bool adminOnly) {
        if (!IpcServer.Clients.TryAdd(gameServerId, _proxy))
            throw new InvalidOperationException($"Failed to add GameServer to dictionary: {gameServerId}");
        ServerId = gameServerId;
        IpcServer.Worlds[gameServerId] = new WorldInfo(gameServerId, name, address, port, 0, maxPlayers, adminOnly);
        
        _log.Info($"[RPC] GameServer ({gameServerId}) connected as '{name}' on {address}:{port}");
        return Task.CompletedTask;
    }

    public Task PlayerCountChanged(int playerCount) {
        if (IpcServer.Worlds.TryGetValue(ServerId, out var world))
            IpcServer.Worlds[ServerId] = world with { PlayerCount = playerCount };
        return Task.CompletedTask;
    }

    public async Task<GameInfoDto?> GetUserInfo(string name, int accountId) {
        return await _proxy.GetUserInfo(name, accountId);
    }
}
