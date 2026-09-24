using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Sentinel;

public class Net : MonoBehaviourPunCallbacks, IOnEventCallback
{
    private const byte PresenceEventCode = 147;

    private const byte LegacyProtocolVersion = 1;

    private const byte PlatformProtocolVersion = 2;

    private const float PresenceTimeoutSeconds = 14f;

    private static readonly byte[] presenceDomain = Encoding.UTF8.GetBytes("zx1-9f4c-presence-v1");

    public static Net Ins;

    private readonly RaiseEventOptions broadcastOptions = new()
    {
            Receivers     = ReceiverGroup.Others,
            CachingOption = EventCaching.DoNotCache,
    };

    private readonly List<string> expiredUserIds = new();

    private readonly Dictionary<string, PresenceState> presenceByUserId = new();

    private float nextBroadcastAt;

    private SHA256 sha256;

    private void Awake()
    {
        Ins    = this;
        sha256 = SHA256.Create();
    }

    private void Update()
    {
        RemoveExpiredPresence();

        if (!PhotonNetwork.InRoom || !Cfg.Broadcast.Value || Time.time < nextBroadcastAt)
        {
            return;
        }

        nextBroadcastAt = Time.time + 4f;

        Player localPlayer = PhotonNetwork.LocalPlayer;

        if (localPlayer == null || string.IsNullOrEmpty(localPlayer.UserId))
        {
            return;
        }

        byte[] legacyPacket = BuildPresencePacket(localPlayer.UserId, LegacyProtocolVersion, 10);

        PhotonNetwork.RaiseEvent(PresenceEventCode, legacyPacket, broadcastOptions, SendOptions.SendUnreliable);

        byte[] platformPacket = BuildPresencePacket(localPlayer.UserId, PlatformProtocolVersion, 12);

        platformPacket[10] = 1;
        platformPacket[11] = Detect.LocalPlatform switch
                             {
                                     "steam" => 1,
                                     "meta"  => 2,
                                     var _   => 0,
                             };

        PhotonNetwork.RaiseEvent(PresenceEventCode, platformPacket, broadcastOptions, SendOptions.SendUnreliable);
    }

    private void OnDestroy()
    {
        sha256?.Dispose();
        sha256 = null;

        presenceByUserId.Clear();

        if (Ins == this)
        {
            Ins = null;
        }
    }

    public void OnEvent(EventData eventData)
    {
        if (eventData.Code != PresenceEventCode
         || !PhotonNetwork.InRoom
         || PhotonNetwork.CurrentRoom == null
         || eventData.CustomData is not byte[] packet)
        {
            return;
        }

        bool hasPlatform = packet.Length == 12 && packet[0] == PlatformProtocolVersion;
        bool isLegacy    = packet.Length == 10 && packet[0] == LegacyProtocolVersion;

        if (!hasPlatform && !isLegacy || packet[1] > 1)
        {
            return;
        }

        if (hasPlatform && packet[11] > 2)
        {
            return;
        }

        Player player = PhotonNetwork.CurrentRoom.GetPlayer(eventData.Sender);

        if (player == null || player.IsLocal || string.IsNullOrEmpty(player.UserId))
        {
            return;
        }

        long timeWindow = CurrentTimeWindow();

        if (!ValidatePresence(packet, player.UserId, timeWindow)
         && !ValidatePresence(packet, player.UserId, timeWindow - 1)
         && !ValidatePresence(packet, player.UserId, timeWindow + 1))
        {
            return;
        }

        if (!presenceByUserId.TryGetValue(player.UserId, out PresenceState presence))
        {
            presence                        = new PresenceState();
            presenceByUserId[player.UserId] = presence;

            if (nextBroadcastAt - Time.time > 2f)
            {
                nextBroadcastAt = Time.time + Random.Range(0.3f, 1.1f);
            }
        }

        presence.lastSeenAt = Time.time;
        presence.menuOpen   = packet[1] == 1;

        if (hasPlatform)
        {
            presence.platformCode = packet[11];
        }
    }

    public static bool Has(string id) => GetPresence(id) != null;

    public static bool HasRig(VRRig rig) => rig?.Creator != null && Has(rig.Creator.UserId);

    public static bool MenuOpenRig(VRRig rig) => rig?.Creator != null && MenuOpen(rig.Creator.UserId);

    public static bool MenuOpen(string id) => GetPresence(id)?.menuOpen ?? false;

    public static string Platform(string id)
    {
        PresenceState presence = GetPresence(id);

        if (presence == null)
        {
            return "unknown";
        }

        return presence.platformCode switch
               {
                       1     => "steam",
                       2     => "meta",
                       var _ => "unknown",
               };
    }

    public override void OnJoinedRoom()
    {
        presenceByUserId.Clear();
        nextBroadcastAt = Time.time + Random.Range(0.4f, 1.2f);
    }

    public override void OnLeftRoom() => presenceByUserId.Clear();

    public override void OnPlayerLeftRoom(Player other)
    {
        if (other == null || string.IsNullOrEmpty(other.UserId))
        {
            return;
        }

        presenceByUserId.Remove(other.UserId);
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (Time.time + 1.5f < nextBroadcastAt)
        {
            nextBroadcastAt = Time.time + Random.Range(1.2f, 2f);
        }
    }

    private static PresenceState GetPresence(string id)
    {
        if (Ins == null || !PhotonNetwork.InRoom || string.IsNullOrEmpty(id))
        {
            return null;
        }

        if (!Ins.presenceByUserId.TryGetValue(id, out PresenceState presence))
        {
            return null;
        }

        return Time.time - presence.lastSeenAt <= PresenceTimeoutSeconds ? presence : null;
    }

    private void RemoveExpiredPresence()
    {
        if (presenceByUserId.Count == 0)
        {
            return;
        }

        expiredUserIds.Clear();

        foreach (KeyValuePair<string, PresenceState> entry in presenceByUserId)
        {
            if (Time.time - entry.Value.lastSeenAt > PresenceTimeoutSeconds)
            {
                expiredUserIds.Add(entry.Key);
            }
        }

        foreach (string userId in expiredUserIds)
        {
            presenceByUserId.Remove(userId);
        }
    }

    private byte[] BuildPresencePacket(string userId, byte protocolVersion, int length)
    {
        byte[] packet = new byte[length];

        packet[0] = protocolVersion;
        packet[1] = RingMenu.IsOpen || PcMenu.IsOpen ? (byte)1 : (byte)0;

        Array.Copy(BuildPresenceHash(userId, CurrentTimeWindow()), 0, packet, 2, 8);

        return packet;
    }

    private bool ValidatePresence(byte[] packet, string userId, long timeWindow)
    {
        byte[] expectedHash = BuildPresenceHash(userId, timeWindow);

        for (int i = 0; i < 8; i++)
        {
            if (packet[i + 2] != expectedHash[i])
            {
                return false;
            }
        }

        return true;
    }

    private static long CurrentTimeWindow()
    {
        DateTime epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        return (long)(DateTime.UtcNow - epoch).TotalSeconds / 30;
    }

    private byte[] BuildPresenceHash(string userId, long timeWindow)
    {
        byte[] userIdBytes = Encoding.UTF8.GetBytes(userId);
        byte[] timeBytes   = BitConverter.GetBytes(timeWindow);
        byte[] payload     = new byte[presenceDomain.Length + userIdBytes.Length + timeBytes.Length];

        Buffer.BlockCopy(presenceDomain, 0, payload, 0,                                          presenceDomain.Length);
        Buffer.BlockCopy(userIdBytes,    0, payload, presenceDomain.Length,                      userIdBytes.Length);
        Buffer.BlockCopy(timeBytes,      0, payload, presenceDomain.Length + userIdBytes.Length, timeBytes.Length);

        return sha256.ComputeHash(payload);
    }

    private sealed class PresenceState
    {
        public float lastSeenAt;

        public bool menuOpen;

        public byte platformCode;
    }
}