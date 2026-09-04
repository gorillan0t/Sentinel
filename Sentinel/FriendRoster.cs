using System;
using System.Collections.Generic;

namespace Sentinel.Sentinel;

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

    private static void MergeSource(IEnumerable<FriendEntry> source, List<string> orderedIds, Dictionary<string, FriendEntry> entriesById)
    {
        if (source == null)
        {
            return;
        }

        foreach (FriendEntry item in source)
        {
            if (item != null && !string.IsNullOrEmpty(item.Id))
            {
                if (!entriesById.ContainsKey(item.Id))
                {
                    orderedIds.Add(item.Id);
                }

                entriesById[item.Id] = item;
            }
        }
    }
}