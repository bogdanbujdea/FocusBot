namespace FocusBot.Core.Entities;

/// <summary>Client categories. Must match server <c>ClientType</c> (serialized as int in JSON).</summary>
public enum ClientType
{
    Desktop = 1,
    Extension = 2,
}

/// <summary>Runtime host. Must match server <c>ClientHost</c> (serialized as int in JSON).</summary>
public enum ClientHost
{
    Unknown = 0,
    Windows = 1,
    Chrome = 2,
    Edge = 3,
}
