using System.Runtime.CompilerServices;

namespace Sentinel.Sentinel;

public sealed class FriendEntry(string string2, string string3, bool bool1)
{

    [field: CompilerGenerated]
    public string Id { get; } = string2 ?? "";

    [field: CompilerGenerated]
    public string Name { get; } = string3 ?? "";

    [field: CompilerGenerated]
    public bool ServerManaged { get; } = bool1;
}