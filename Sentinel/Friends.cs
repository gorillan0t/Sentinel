using System.Collections.Generic;
using ExitGames.Client.Photon;
using Newtonsoft.Json.Linq;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace Sentinel.Sentinel;

public class Friends : MonoBehaviour, IInRoomCallbacks
{
    public static Friends Ins;

    public static readonly List<string> All = new();

    private static readonly Dictionary<string, string> localNamesById = new();

    private static readonly List<FriendEntry> serverFriends = new();

    private static HashSet<string> friendIds = new();

    private static Dictionary<string, string> namesById = new();

    private void Awake()
    {
        Ins = this;
        All.Clear();
        localNamesById.Clear();
        string[] array = PlayerPrefs.GetString("zx_friends", "").Split('|');
        for (int i = 0; i < array.Length; i++)
        {
            string[] array2 = array[i].Split(':');
            if (!string.IsNullOrEmpty(array2[0]))
            {
                All.Add(array2[0]);
                if (array2.Length >= 2 && !string.IsNullOrEmpty(array2[1]))
                {
                    localNamesById[array2[0]] = array2[1];
                }
            }
        }

        RebuildIndex();
        PhotonNetwork.AddCallbackTarget(this);
    }

    private void OnDestroy() => PhotonNetwork.RemoveCallbackTarget(this);

    public void OnPlayerEnteredRoom(Player player)
    {
        if (IsFriend(player.UserId))
        {
            Notify.Send(player.NickName + " joined!", Theme.Good);
        }
    }

    public void OnPlayerLeftRoom(Player other) { }

    public void OnRoomPropertiesUpdate(Hashtable props) { }

    public void OnPlayerPropertiesUpdate(Player target, Hashtable props) { }

    public void OnMasterClientSwitched(Player newMaster) { }

    private static void SaveLocalFriends()
    {
        List<string> list = new();
        foreach (string item in All)
        {
            localNamesById.TryGetValue(item, out string value);
            list.Add(item + ":" + value);
        }

        PlayerPrefs.SetString("zx_friends", string.Join("|", list));
        PlayerPrefs.Save();
    }

    public static bool IsFriend(string userId)
    {
        if (!string.IsNullOrEmpty(userId))
        {
            return friendIds.Contains(userId);
        }

        return false;
    }

    public static string NameOf(string userId)
    {
        namesById.TryGetValue(userId, out string value);
        string text = value;
        if (text == null)
        {
            if (userId.Length <= 12)
            {
                return userId;
            }

            text = userId.Substring(0, 12);
        }

        return text;
    }

    public static void Add(string userId, string displayName)
    {
        if (!string.IsNullOrEmpty(userId) && !All.Contains(userId))
        {
            All.Add(userId);
            if (!string.IsNullOrEmpty(displayName))
            {
                localNamesById[userId] = displayName;
            }

            SaveLocalFriends();
            RebuildIndex();
        }
    }

    public static void Remove(string userId)
    {
        if (All.Remove(userId))
        {
            localNamesById.Remove(userId);
            SaveLocalFriends();
            RebuildIndex();
        }
    }

    public static void ApplyServer(string json)
    {
        try
        {
            JObject           obj  = JObject.Parse(json);
            List<FriendEntry> list = new();
            JToken            obj2 = obj["friends"];
            JArray            val  = (JArray)(obj2 is JArray ? obj2 : null);
            if (val != null)
            {
                foreach (JObject item in val)
                {
                    string text    = (string)item["game_id"];
                    string string_ = (string)item["display_name"];
                    if (!string.IsNullOrEmpty(text))
                    {
                        list.Add(new FriendEntry(text, string_, true));
                    }
                }
            }

            lock (serverFriends)
            {
                serverFriends.Clear();
                serverFriends.AddRange(list);
                RebuildIndex();
            }
        }
        catch { }
    }

    private static void RebuildIndex()
    {
        List<FriendEntry> list = new();
        foreach (string item in All)
        {
            localNamesById.TryGetValue(item, out string value);
            list.Add(new FriendEntry(item, value, false));
        }

        List<FriendEntry>          list2      = FriendRoster.Merge(list, serverFriends);
        HashSet<string>            hashSet    = new();
        Dictionary<string, string> dictionary = new();
        foreach (FriendEntry item2 in list2)
        {
            hashSet.Add(item2.Id);
            if (!string.IsNullOrEmpty(item2.Name))
            {
                dictionary[item2.Id] = item2.Name;
            }
        }

        namesById = dictionary;
        friendIds = hashSet;
    }
}