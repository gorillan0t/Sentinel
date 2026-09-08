using System;
using System.Collections.Generic;

namespace Sentinel;

public static class FriendRoster
{
    public static List<FriendEntry> Merge(IEnumerable<FriendEntry> local, IEnumerable<FriendEntry> server)
    {
        List<string>                    list       = new();
        Dictionary<string, FriendEntry> dictionary = new(StringComparer.Ordinal);
        MergeSource(local,  list, dictionary);
        MergeSource(server, list, dictionary);
        List<FriendEntry> list2 = new(list.Count);
        foreach (string item in list)
        {
            list2.Add(dictionary[item]);
        }

        return list2;
    }

    private static void MergeSource(IEnumerable<FriendEntry> arg1, List<string> arg2, Dictionary<string, FriendEntry> arg3)
    {
        if (arg1 == null)
        {
            return;
        }

        foreach (FriendEntry item in arg1)
        {
            if (item != null && !string.IsNullOrEmpty(item.Id))
            {
                if (!arg3.ContainsKey(item.Id))
                {
                    arg2.Add(item.Id);
                }

                arg3[item.Id] = item;
            }
        }
    }
}