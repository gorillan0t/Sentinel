namespace Sentinel;

public sealed class FriendEntry
{

    public FriendEntry(string arg1, string arg2, bool arg3)
    {
        Id            = arg1 ?? "";
        Name          = arg2 ?? "";
        ServerManaged = arg3;
    }
    public string Id { get; }

    public string Name { get; }

    public bool ServerManaged { get; }
}