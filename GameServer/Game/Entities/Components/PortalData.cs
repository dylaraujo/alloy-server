using System;
using System.Reflection;
using Common;
using Common.Game;
using Common.Resources.World;
using Common.Structs;
using Common.Utilities;
using Common.Utilities.Collections;
using GameServer.Game.Network;
using GameServer.Game.Worlds;

namespace GameServer.Game.Entities.Components;

public struct PortalData : IEntityIdentifiable, IDisposable {
    private static readonly Dictionary<string, Type> _worldTypes = [];
    private static readonly Logger _log = new(typeof(PortalData));
    private static readonly object _waveLock = new();

    public static readonly TimeSpan JoinWindow = TimeSpan.FromSeconds(60);

    static PortalData() {
        var asm = Assembly.GetExecutingAssembly();
        foreach (var type in asm.GetTypes()) {
            if (type == typeof(World) || !type.IsSubclassOf(typeof(World)))
                continue;

            _worldTypes[type.Name] = type;
        }
    }
    
    public EntityId Id { get; set; }

    public bool DisplayPlayerCount;
    public bool Disabled;
    
    private readonly World _world;
    private World _worldLink;
    private Type _waveType;
    private WorldConfig _waveConfig;

    public World CurrentInstance;
    public DateTime? OpenedAt;
    
    public PortalData(World world, ref Entity en) {
        Id = en.Id;
        _world = world;

        LoadWorld(ref en);
    }

    private void LoadWorld(ref Entity en) {
        if (en.Desc.RealmPortal)
            return;

        var worldName = en.Desc.DungeonName;
        var worldConfig = WorldLibrary.WorldConfigs.Values.FirstOrDefault(i => i.DisplayName == worldName);
        if (worldConfig.Name == null) {
            _log.Error($"World '{worldName}' not found ({en.Desc.ObjectId})");
            return;
        }

        if (!_worldTypes.TryGetValue(worldConfig.Name, out var worldType)) {
            _log.Error($"World logic doesn't exist for '{worldName}' ({en.Desc.ObjectId})");
            return;
        }
        
        if (worldConfig.Id == 0) {
            _waveType = worldType;
            _waveConfig = worldConfig;
            return;
        }

        Init(RealmManager.Worlds[worldConfig.Id]);
    }

    private World GetWaveInstance() {
        lock (_waveLock) {
            var now = DateTime.UtcNow;
            if (CurrentInstance == null || CurrentInstance.Deleted || now - OpenedAt.Value >= JoinWindow) {
                var world = (World)Activator.CreateInstance(_waveType, _waveConfig.Id, -1, _waveConfig);
                RealmManager.AddWorld(world);
                CurrentInstance = world;
                OpenedAt = now;
            }

            return CurrentInstance;
        }
    }

    public void Init(World worldLink) {
        RealmManager.AddWorld(worldLink);
        LinkTo(worldLink);
    }

    public void LinkTo(World worldLink) {
        _worldLink = worldLink;
    }

    public World GetWorldInstance(User user) {
        if (_waveType != null)
            return GetWaveInstance();

        if (_worldLink == null)
            return null;
        return _worldLink.GetInstance(user);
    }

    public void Tick(ref RealmTime time) {
        ref var portalStats = ref _world.EntityStats.Get(Id);
        portalStats.Set(StatType.PortalUsable, Disabled ? 0 : 1);

        if (_waveType != null) {
            TickWave(ref portalStats);
            return;
        }

        if (_worldLink == null)
            return;

        if (_worldLink.Deleted) {
            _world.LeaveWorld(Id);
        }
        
        if (DisplayPlayerCount) {
            ref var stats = ref _world.EntityStats.Get(Id);
            stats.Set(StatType.Name, $"{_worldLink.DisplayName} ({_worldLink.Users.Count}/{_worldLink.Config.MaxPlayers})");
        }
    }
    
    private void TickWave(ref EntityStats stats) {
        var remaining = OpenedAt.HasValue ? JoinWindow - (DateTime.UtcNow - OpenedAt.Value) : TimeSpan.Zero;
        if (remaining <= TimeSpan.Zero) {
            CurrentInstance = null;
            OpenedAt = null;
            stats.Set(StatType.Name, _waveConfig.DisplayName);
            return;
        }

        stats.Set(StatType.Name, $"{_waveConfig.DisplayName} [{(int)Math.Ceiling(remaining.TotalSeconds)}s]");
    }

    public void Dispose() {
        
    }
}