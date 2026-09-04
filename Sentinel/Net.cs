using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Sentinel.Sentinel;

public class Net : MonoBehaviourPunCallbacks, IOnEventCallback
{

    private const byte byte_0 = 147;

    private const byte byte_1 = 1;

    private static readonly byte[] presenceDomain = Encoding.UTF8.GetBytes("zx1-9f4c-presence-v1");

    public static Net Ins;

    private readonly RaiseEventOptions broadcastOptions;

    private readonly List<string> expiredUserIds;

    private readonly Dictionary<string, PresenceState> presenceByUserId;

    private float nextBroadcastTime;

    private SHA256 sha256;

    public Net()
    {
        presenceByUserId = new Dictionary<string, PresenceState>();
        expiredUserIds   = new List<string>();
        broadcastOptions = new RaiseEventOptions
        {
                Receivers     = 0,
                CachingOption = 0,
        };
    }

    private void Awake()
    {
        Ins    = this;
        sha256 = SHA256.Create();
    }

    private void Update()
    {
        if (presenceByUserId.Count > 0)
        {
            expiredUserIds.Clear();
            foreach (KeyValuePair<string, PresenceState> item in presenceByUserId)
            {
                if (Time.time - item.Value.lastSeenAt > 14f)
                {
                    expiredUserIds.Add(item.Key);
                }
            }

            foreach (string expiredUserId in expiredUserIds)
            {
                presenceByUserId.Remove(expiredUserId);
            }
        }

        if (PhotonNetwork.InRoom && Cfg.Broadcast.Value && Time.time >= nextBroadcastTime)
        {
            nextBroadcastTime = Time.time + 4f;
            Player localPlayer = PhotonNetwork.LocalPlayer;
            if (localPlayer != null && !string.IsNullOrEmpty(localPlayer.UserId))
            {
                byte[] array = new byte[10]
                {
                        1,
                        RingMenu.IsOpen ? (byte)1 : (byte)0,
                        0,
                        0,
                        0,
                        0,
                        0,
                        0,
                        0,
                        0,
                };

                Array.Copy(BuildPresenceHash(localPlayer.UserId, CurrentTimeWindow()), 0, array, 2, 8);
                PhotonNetwork.RaiseEvent(147, array, broadcastOptions, SendOptions.SendUnreliable);
            }
        }
    }

    private void OnDestroy() => sha256?.Dispose();

    public void OnEvent(EventData ev)
    {
        if (ev.Code != 147 || !PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null || !(ev.CustomData is byte[] array) || array.Length != 10 || array[0] != 1)
        {
            return;
        }

        Player player = PhotonNetwork.CurrentRoom.GetPlayer(ev.Sender);
        if (player == null || player.IsLocal || string.IsNullOrEmpty(player.UserId))
        {
            return;
        }

        long num = CurrentTimeWindow();
        if (!ValidatePresence(array, player.UserId, num) && !ValidatePresence(array, player.UserId, num - 1L) && !ValidatePresence(array, player.UserId, num + 1L))
        {
            return;
        }

        if (!presenceByUserId.TryGetValue(player.UserId, out PresenceState value))
        {
            value = presenceByUserId[player.UserId] = new PresenceState();
            if (nextBroadcastTime - Time.time > 2f)
            {
                nextBroadcastTime = Time.time + Random.Range(0.3f, 1.1f);
            }
        }

        value.lastSeenAt = Time.time;
        value.menuOpen   = array[1] == 1;
    }

    public static bool Has(string id)
    {
        if (Ins != null && !string.IsNullOrEmpty(id))
        {
            return Ins.presenceByUserId.ContainsKey(id);
        }

        return false;
    }

    public static bool HasRig(VRRig rig)
    {
        if (rig != null && rig.Creator != null)
        {
            return Has(rig.Creator.UserId);
        }

        return false;
    }

    public static bool MenuOpenRig(VRRig rig)
    {
        if (rig != null && rig.Creator != null)
        {
            return MenuOpen(rig.Creator.UserId);
        }

        return false;
    }

    public static bool MenuOpen(string id)
    {
        if (Ins != null && !string.IsNullOrEmpty(id) && Ins.presenceByUserId.TryGetValue(id, out PresenceState value))
        {
            return value.menuOpen;
        }

        return false;
    }

    public override void OnJoinedRoom()
    {
        presenceByUserId.Clear();
        nextBroadcastTime = Time.time + Random.Range(0.4f, 1.2f);
    }

    public override void OnLeftRoom() => presenceByUserId.Clear();

    public override void OnPlayerLeftRoom(Player other)
    {
        if (other != null && !string.IsNullOrEmpty(other.UserId))
        {
            presenceByUserId.Remove(other.UserId);
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (Time.time + 1.5f < nextBroadcastTime)
        {
            nextBroadcastTime = Time.time + Random.Range(1.2f, 2f);
        }
    }

    private bool ValidatePresence(byte[] packet, string userId, long timeWindow)
    {
        byte[] array = BuildPresenceHash(userId, timeWindow);
        int    num   = 0;
        while (true)
        {
            if (num < 8)
            {
                if (packet[2 + num] != array[num])
                {
                    break;
                }

                num++;

                continue;
            }

            return true;
        }

        return false;
    }

    private static long CurrentTimeWindow() => (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds / 30L;

    private byte[] BuildPresenceHash(string userId, long timeWindow)
    {
        byte[] bytes  = Encoding.UTF8.GetBytes(userId);
        byte[] bytes2 = BitConverter.GetBytes(timeWindow);
        byte[] array  = new byte[presenceDomain.Length + bytes.Length + bytes2.Length];
        Buffer.BlockCopy(presenceDomain, 0, array, 0,                                    presenceDomain.Length);
        Buffer.BlockCopy(bytes,          0, array, presenceDomain.Length,                bytes.Length);
        Buffer.BlockCopy(bytes2,         0, array, presenceDomain.Length + bytes.Length, bytes2.Length);

        return sha256.ComputeHash(array);
    }

    private class PresenceState
    {
        public float lastSeenAt;

        public bool menuOpen;
    }
}