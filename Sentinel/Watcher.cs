using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace Sentinel.Sentinel;

public class Watcher : MonoBehaviourPunCallbacks
{
    private readonly HashSet<string> announcedDetections = new();

    private readonly Dictionary<string, float> pendingPlayers = new();

    private readonly List<string> readyPlayerIds = new();

    private float initialScanAt = -1f;

    private void Update()
    {
        if (!PhotonNetwork.InRoom)
        {
            return;
        }

        if (initialScanAt > 0f && Time.time >= initialScanAt)
        {
            initialScanAt = -1f;
            Player[] playerList = PhotonNetwork.PlayerList;
            foreach (Player val in playerList)
            {
                if (!val.IsLocal)
                {
                    InspectPlayer(val);
                }
            }
        }

        if (pendingPlayers.Count == 0)
        {
            return;
        }

        readyPlayerIds.Clear();
        foreach (KeyValuePair<string, float> pendingPlayer in pendingPlayers)
        {
            if (Time.time >= pendingPlayer.Value)
            {
                readyPlayerIds.Add(pendingPlayer.Key);
            }
        }

        foreach (string readyPlayerId in readyPlayerIds)
        {
            pendingPlayers.Remove(readyPlayerId);
            Player val2 = Detect.Find(readyPlayerId);
            if (val2 != null)
            {
                InspectPlayer(val2);
            }
        }
    }

    public override void OnJoinedRoom()
    {
        announcedDetections.Clear();
        pendingPlayers.Clear();
        initialScanAt = Time.time + 4f;
    }

    public override void OnLeftRoom()
    {
        announcedDetections.Clear();
        pendingPlayers.Clear();
        Detect.ClearScans();

        initialScanAt = -1f;
    }

    public override void OnPlayerEnteredRoom(Player p)
    {
        if (p != null && !p.IsLocal)
        {
            pendingPlayers[p.UserId] = Time.time + 4f;
        }
    }

    public override void OnPlayerPropertiesUpdate(Player p, Hashtable changed)
    {
        if (p == null || p.IsLocal)
        {
            return;
        }

        Detect.Invalidate(p);

        if (!pendingPlayers.ContainsKey(p.UserId))
        {
            pendingPlayers[p.UserId] = Time.time + 0.5f;
        }
    }

    private void InspectPlayer(Player player_0)
    {
        string value = Cfg.NotifMode.Value;
        if (value == "off")
        {
            return;
        }

        Scan scan = Detect.Get(player_0);
        foreach (Entry cheat in scan.Cheats)
        {
            if (announcedDetections.Add(player_0.UserId + "|" + cheat.Name))
            {
                Notify.Send(player_0.NickName + " is using " + cheat.Name, Theme.Bad);
            }
        }

        if (value == "cheats")
        {
            return;
        }

        foreach (Entry mod in scan.Mods)
        {
            if (announcedDetections.Add(player_0.UserId + "|" + mod.Name))
            {
                Notify.Send(player_0.NickName + " is using " + mod.Name, mod.Kind == Kind.Unknown ? Theme.Warn : Theme.Good);
            }
        }
    }
}