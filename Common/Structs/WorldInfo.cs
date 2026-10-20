using System;

namespace Common.Structs;

public readonly record struct WorldInfo(Guid Guid, string Name, string Address, int Port, int PlayerCount, int MaxPlayers, bool AdminOnly);
