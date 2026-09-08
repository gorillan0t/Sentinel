using System;
using System.Collections.Generic;
using System.Threading;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace Sentinel;

public class Friends : MonoBehaviour, IInRoomCallbacks
{
    private const string FriendsKey = "zx_friends";

    private const string ShareActivityKey = "zx_friend_activity";

    public static Friends Ins;

    private static readonly List<string> friendIds = new();

    private static readonly Dictionary<string, string> namesById = new(StringComparer.Ordinal);

    private static string presenceSignature = "";

    private static long revision;

    public static FriendState State { get; private set; } = FriendState.CreateLocal(Array.Empty<FriendView>(), true, DateTime.UtcNow);

    public static bool Busy => false;

    public static bool ShareActivity { get; private set; } = true;

    public static string Status { get; private set; } = "";

    public static long Revision => Interlocked.Read(ref revision);

    private void Awake()
    {
        Ins = this;
        LoadLocalFriends();
        PhotonNetwork.AddCallbackTarget(this);
    }

    public static void Reset()
    {
        Status            = "";
        presenceSignature = "";
        RebuildState();
    }

    private void OnDestroy()
    {
        PhotonNetwork.RemoveCallbackTarget(this);

        if (Ins == this)
        {
            Ins = null;
        }
    }

    public void OnPlayerEnteredRoom(Player player)
    {
        if (player != null && IsFriend(player.UserId))
        {
            Notify.Send(player.NickName + " joined!", Theme.Good);
        }

        presenceSignature = "";
        CaptureRoom();
    }

    public void OnPlayerLeftRoom(Player other)
    {
        presenceSignature = "";
        CaptureRoom();
    }

    public void OnRoomPropertiesUpdate(Hashtable properties) { }

    public void OnPlayerPropertiesUpdate(Player target, Hashtable properties)
    {
        if (target == null || !IsFriend(target.UserId))
        {
            return;
        }

        namesById[target.UserId] = target.NickName;
        SaveLocalFriends();
        RebuildState();
    }

    public void OnMasterClientSwitched(Player newMaster) { }

    public static void CaptureRoom()
    {
        string currentSignature = BuildPresenceSignature();

        if (presenceSignature == currentSignature)
        {
            return;
        }

        presenceSignature = currentSignature;
        RebuildState();
    }

    public static bool IsFriend(string userId) => !string.IsNullOrEmpty(userId) && friendIds.Contains(userId);

    public static string NameOf(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return "Unknown";
        }

        if (namesById.TryGetValue(userId, out string name) && !string.IsNullOrEmpty(name))
        {
            return name;
        }

        return userId.Length <= 12 ? userId : userId.Substring(0, 12);
    }

    public static void Add(string userId, string displayName)
    {
        if (string.IsNullOrEmpty(userId) || friendIds.Contains(userId))
        {
            return;
        }

        friendIds.Add(userId);

        if (!string.IsNullOrEmpty(displayName))
        {
            namesById[userId] = displayName;
        }

        Status = "Friend added locally";

        SaveLocalFriends();
        RebuildState();
    }

    public static void Remove(string id)
    {
        if (string.IsNullOrEmpty(id) || !friendIds.Remove(id))
        {
            return;
        }

        namesById.Remove(id);
        Status = "Friend removed";

        SaveLocalFriends();
        RebuildState();
    }

    public static void Respond(string requestId, bool accept)
    {
        Status = "Friend requests are not used by the standalone build";
        Interlocked.Increment(ref revision);
    }

    public static void SetSharing(bool enabled)
    {
        ShareActivity = enabled;
        PlayerPrefs.SetInt(ShareActivityKey, enabled ? 1 : 0);
        PlayerPrefs.Save();

        Status = enabled ? "Friend activity enabled" : "Friend activity disabled";
        RebuildState();
    }

    private static void LoadLocalFriends()
    {
        friendIds.Clear();
        namesById.Clear();

        ShareActivity = PlayerPrefs.GetInt(ShareActivityKey, 1) != 0;

        string[] entries = PlayerPrefs.GetString(FriendsKey, "").Split('|');

        foreach (string entry in entries)
        {
            string[] parts = entry.Split(':');

            if (parts.Length == 0 || string.IsNullOrEmpty(parts[0]))
            {
                continue;
            }

            friendIds.Add(parts[0]);

            if (parts.Length > 1 && !string.IsNullOrEmpty(parts[1]))
            {
                namesById[parts[0]] = parts[1];
            }
        }

        RebuildState();
    }

    private static void SaveLocalFriends()
    {
        List<string> entries = new();

        foreach (string userId in friendIds)
        {
            namesById.TryGetValue(userId, out string name);
            entries.Add(userId + ":" + (name ?? ""));
        }

        PlayerPrefs.SetString(FriendsKey, string.Join("|", entries));
        PlayerPrefs.Save();
    }

    private static void RebuildState()
    {
        List<FriendView> friends = new();
        DateTime         now     = DateTime.UtcNow;

        foreach (string userId in friendIds)
        {
            Player player   = FindPlayer(userId);
            string name     = player?.NickName ?? NameOf(userId);
            string activity = player == null ? "offline" : "online";
            string roomCode = player == null || !ShareActivity ? "" : PhotonNetwork.CurrentRoom?.Name ?? "";

            if (!string.IsNullOrEmpty(name))
            {
                namesById[userId] = name;
            }

            friends.Add(new FriendView(userId, userId, name, activity, roomCode, ""));
        }

        State = FriendState.CreateLocal(friends.ToArray(), ShareActivity, now);
        Interlocked.Increment(ref revision);
    }

    private static Player FindPlayer(string userId)
    {
        if (!PhotonNetwork.InRoom)
        {
            return null;
        }

        foreach (Player player in PhotonNetwork.PlayerList)
        {
            if (player.UserId == userId)
            {
                return player;
            }
        }

        return null;
    }

    private static string BuildPresenceSignature()
    {
        if (!PhotonNetwork.InRoom)
        {
            return "offline";
        }

        List<string> playerIds = new();

        foreach (Player player in PhotonNetwork.PlayerList)
        {
            if (!string.IsNullOrEmpty(player.UserId))
            {
                playerIds.Add(player.UserId + ":" + player.NickName);
            }
        }

        playerIds.Sort(StringComparer.Ordinal);

        return PhotonNetwork.CurrentRoom.Name + "|" + string.Join("|", playerIds);
    }
}