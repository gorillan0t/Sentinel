using HarmonyLib;
using Photon.Realtime;

namespace Sentinel.Sentinel;

[HarmonyPatch(typeof(GorillaPlayerScoreboardLine), "UpdatePlayerText")]
public static class Board
{
    private static void Postfix(GorillaPlayerScoreboardLine __instance)
    {
        if (!Manifest.Has("board_colors") || !Cfg.BoardColors.Value || __instance.linePlayer == null || __instance.playerNameVisible != null && __instance.playerNameVisible.StartsWith("<color"))
        {
            return;
        }

        Player val = Detect.Find(__instance.linePlayer.UserId);

        if (val == null)
            return;

        Scan scan = Detect.Get(val);
        if (scan.Cheats.Count > 0)
            __instance.playerNameVisible = "<color=#FF4B42>" + __instance.playerNameVisible + "</color>";
        else if (scan.Mods.Count > 0)
            __instance.playerNameVisible = "<color=#FFD24A>" + __instance.playerNameVisible + "</color>";
    }
}