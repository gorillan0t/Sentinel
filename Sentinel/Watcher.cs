using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace Sentinel;

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
        foreach (KeyValuePair<string, float> item in pendingPlayers)
        {
            if (Time.time >= item.Value)
            {
                readyPlayerIds.Add(item.Key);
            }
        }

        foreach (string item2 in readyPlayerIds)
        {
            pendingPlayers.Remove(item2);
            Player val2 = Detect.Find(item2);
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
        if (p != null && !p.IsLocal)
        {
            Detect.Invalidate(p.UserId);
            pendingPlayers[p.UserId] = Time.time + 0.5f;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void InspectPlayer(Player arg1)
    {
        string value = Cfg.NotifMode.Value;
        if (value == "off")
        {
            return;
        }

        Scan scan = Detect.Get(arg1);
        if (scan.Ready)
        {
            foreach (Entry cheat in scan.Cheats)
            {
                if (announcedDetections.Add(arg1.UserId + "|" + cheat.Name))
                {
                    Notify.Send(arg1.NickName + " is using " + cheat.Name, Theme.Bad);
                }
            }

            if (value == "cheats")
            {
                return;
            }

            {
                foreach (Entry mod in scan.Mods)
                {
                    if (announcedDetections.Add(arg1.UserId + "|" + mod.Name))
                    {
                        Notify.Send(arg1.NickName + " is using " + mod.Name, mod.Kind == Kind.Unknown ? Theme.Warn : Theme.Good);
                    }
                }

                return;
            }
        }

        pendingPlayers[arg1.UserId] = Time.time + 2f;
    }
}