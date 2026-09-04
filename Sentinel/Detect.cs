using System;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

namespace Sentinel.Sentinel;

public static class Detect
{
    private const float ScanCacheTime = 0.5f;

    private static readonly Dictionary<string, Scan> scansByUserId = new();

    private static readonly Dictionary<string, DateTime> createdDatesByUserId = new();

    private static readonly HashSet<string> accountLookupsInProgress = new();

    public static Scan Get(Player player)
    {
        if (player == null || string.IsNullOrEmpty(player.UserId))
        {
            return new Scan
            {
                    Time = Time.time,
            };
        }

        if (scansByUserId.TryGetValue(player.UserId, out Scan existingScan)
         && Time.time - existingScan.Time < ScanCacheTime)
        {
            return existingScan;
        }

        Scan scan = ScanPlayer(player);

        scansByUserId[player.UserId] = scan;

        return scan;
    }

    public static void Invalidate(Player player)
    {
        if (player == null || string.IsNullOrEmpty(player.UserId))
        {
            return;
        }

        scansByUserId.Remove(player.UserId);
    }

    public static void ClearScans() => scansByUserId.Clear();

    private static Scan ScanPlayer(Player player)
    {
        Scan scan = new()
        {
                Time = Time.time,
        };

        HashSet<string> foundMods = new(StringComparer.Ordinal);

        HashSet<string> foundCheats = new(StringComparer.Ordinal);

        foreach (object keyObject in player.CustomProperties.Keys)
        {
            string key = keyObject?.ToString();

            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            if (HamburburData.KnownCheats.TryGetValue(key, out string cheatName))
            {
                if (foundCheats.Add(cheatName))
                {
                    scan.Cheats.Add(new Entry
                    {
                            Name = cheatName,
                            Kind = Kind.Cheat,
                    });
                }

                continue;
            }

            if (!HamburburData.KnownMods.TryGetValue(key, out string modName))
            {
                continue;
            }

            if (!foundMods.Add(modName))
            {
                continue;
            }

            scan.Mods.Add(new Entry
            {
                    Name = modName,
                    Kind = Kind.Mod,
            });
        }

        return scan;
    }

    public static string PlatformOf(VRRig rig)
    {
        if (rig == null)
        {
            return "unknown";
        }

        if (rig.IsItemAllowed("S. FIRST LOGIN"))
        {
            return "steam";
        }

        if (rig.currentRankedSubTierQuest <= 0 && rig.currentRankedSubTierPC <= 0)
        {
            return "unknown";
        }

        return "meta";
    }

    public static Texture2D PlatformIcon(string platform)
    {
        if (platform == "steam")
        {
            return Theme.SteamIcon;
        }

        if (platform == "meta")
        {
            return Theme.MetaIcon;
        }

        return Theme.QuestionIcon;
    }

    public static Color RigColor(VRRig rig)
    {
        if (rig == null)
        {
            return Theme.White;
        }

        if (rig.mainSkin != null)
        {
            Material material = rig.mainSkin.sharedMaterial;

            if (material != null && material.HasProperty("_Color"))
            {
                Color color = material.color;

                if (color.maxColorComponent > 0.08f)
                {
                    return new Color(color.r, color.g, color.b, 1f);
                }
            }
        }

        return rig.playerColor;
    }

    public static Color FpsColor(int fps)
    {
        if (fps >= 60)
        {
            return Theme.White;
        }

        if (fps >= 35)
        {
            return Theme.Warn;
        }

        return Theme.Bad;
    }

    public static string CreatedDate(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return "...";
        }

        if (createdDatesByUserId.TryGetValue(id, out DateTime created))
        {
            if (created == DateTime.MinValue)
            {
                return "unknown";
            }

            return created.ToString("MMM d, yyyy");
        }

        if (accountLookupsInProgress.Add(id))
        {
            PlayFabClientAPI.GetAccountInfo(
                    new GetAccountInfoRequest
                    {
                            PlayFabId = id,
                    },
                    OnAccountInfo,
                    OnAccountInfoError,
                    id);
        }

        return "...";
    }

    private static void OnAccountInfo(GetAccountInfoResult result)
    {
        if (result?.AccountInfo == null)
        {
            return;
        }

        string id = result.AccountInfo.PlayFabId;

        createdDatesByUserId[id] = result.AccountInfo.Created;
        accountLookupsInProgress.Remove(id);
    }

    private static void OnAccountInfoError(PlayFabError error)
    {
        if (error?.CustomData == null)
        {
            return;
        }

        string id = error.CustomData.ToString();

        createdDatesByUserId[id] = DateTime.MinValue;
        accountLookupsInProgress.Remove(id);
    }

    public static VRRig RigOf(Player player)
    {
        if (player == null || !VRRigCache.isInitialized)
        {
            return null;
        }

        foreach (RigContainer rigContainer in VRRigCache.ActiveRigContainers)
        {
            if (rigContainer.Creator == null)
            {
                continue;
            }

            if (rigContainer.Creator.UserId == player.UserId)
            {
                return rigContainer.Rig;
            }
        }

        return null;
    }

    public static Player PlayerOf(VRRig rig)
    {
        if (rig == null || rig.Creator == null)
        {
            return null;
        }

        return Find(rig.Creator.UserId);
    }

    public static Player Find(string id)
    {
        if (!PhotonNetwork.InRoom || string.IsNullOrEmpty(id))
        {
            return null;
        }

        return PhotonNetwork.PlayerList.FirstOrDefault(player => player.UserId == id);

    }
}