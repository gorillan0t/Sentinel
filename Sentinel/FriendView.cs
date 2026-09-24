using System;
using System.Runtime.CompilerServices;

namespace Sentinel;

public sealed class FriendView
{

    internal FriendView(string id, string gameId, string name, string status, string room, string requestId)
    {
        Id        = id;
        GameId    = gameId;
        Name      = name;
        Status    = status;
        Room      = room;
        RequestId = requestId;
    }
    public string Id { get; }

    public string GameId { get; }

    public string Name { get; }

    public string Status { get; }

    public string Room { get; }

    public string RequestId { get; }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public string Activity(DateTime now, DateTime receivedAt)
    {
        if (Status == "pending")
        {
            return "Pending";
        }

        if (!(Status == "hidden"))
        {
            if (!(now - receivedAt >= TimeSpan.FromSeconds(30.0)) && !(now < receivedAt))
            {
                if (!(Status == "offline"))
                {
                    if (Status != "online")
                    {
                        return "Status unavailable";
                    }

                    if (Room.Length > 0)
                    {
                        return "Online / " + Room;
                    }

                    return "Online / Not in a room";
                }

                return "Offline";
            }

            return "Status unavailable";
        }

        return "Hidden";
    }
}