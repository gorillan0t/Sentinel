using System;
using System.Collections.Generic;
using GorillaGameModes;
using GorillaLocomotion;
using GorillaNetworking;
using UnityEngine;

namespace Sentinel;

internal static class GameSettings
{
    private static readonly string[] QueueNames = ["DEFAULT", "MINIGAMES", "COMPETITIVE",];

    private static readonly string[] VoiceModes = ["OPEN MIC", "PUSH TO TALK", "PUSH TO MUTE",];

    private static readonly GameModeType[] GameModes;

    static GameSettings() =>
            GameModes =
            [
                    (GameModeType)1,
                    (GameModeType)11,
                    0,
                    (GameModeType)12,
                    (GameModeType)2,
                    (GameModeType)3,
                    (GameModeType)4,
                    (GameModeType)5,
                    (GameModeType)6,
                    (GameModeType)8,
                    (GameModeType)9,
            ];

    public static Vector3 GroundPointAhead(Transform transform_0, float float_0)
    {
        Vector3 forward = transform_0.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }
        else
        {
            forward.Normalize();
        }

        Vector3 val  = transform_0.position + forward * float_0 - Vector3.up * 0.1f;
        int     num  = GTPlayer.Instance != null ? GTPlayer.Instance.locomotionEnabledLayers.value : -1;
        float   num2 = Physics.Raycast(val + Vector3.up * 2.5f, Vector3.down, out RaycastHit raycastHit, 14f, num, (QueryTriggerInteraction)1) ? raycastHit.point.y : val.y - 0.95f;
        val.y = Mathf.Max(val.y, num2                                                                                                                                       + 0.95f);

        return val;
    }

    public static string GetTimeOfDayLabel()
    {
        BetterDayNightManager instance = BetterDayNightManager.instance;
        if (instance == null)
        {
            return "N/A";
        }

        try
        {
            return instance.GetTimeOfDayString().ToUpper();
        }
        catch
        {
            return "?";
        }
    }

    public static void CycleTimeOfDay(int int_0)
    {
        BetterDayNightManager instance = BetterDayNightManager.instance;
        if (!(instance == null) && instance.timeOfDayRange != null && instance.timeOfDayRange.Length != 0)
        {
            int num  = instance.timeOfDayRange.Length;
            int num2 = (int)instance.currentSetting != 1 ? instance.currentTimeIndex + int_0 : int_0 <= 0 ? num - 1 : 0;
            if (num2 >= 0 && num2 < num)
            {
                instance.SetTimeOfDay(num2);
                instance.SetOverrideIndex(num2);
            }
            else
            {
                instance.ClearTimeOfDay(true);
            }
        }
    }

    public static string GetQueueLabel()
    {
        GorillaComputer instance = GorillaComputer.instance;
        if (!(instance == null) && !string.IsNullOrEmpty(instance.currentQueue))
        {
            return instance.currentQueue;
        }

        return "N/A";
    }

    public static void CycleQueue()
    {
        GorillaComputer instance = GorillaComputer.instance;
        if (instance == null)
        {
            return;
        }

        int num = Array.IndexOf(QueueNames, instance.currentQueue);
        for (int i = 0; i < QueueNames.Length; i++)
        {
            num = (num + 1) % QueueNames.Length;
            if (QueueNames[num] != "COMPETITIVE" || instance.allowedInCompetitive)
            {
                break;
            }
        }

        instance.currentQueue     = QueueNames[num];
        instance.troopQueueActive = false;
        PlayerPrefs.SetString("currentQueue", instance.currentQueue);
        PlayerPrefs.SetInt("troopQueueActive", 0);
        PlayerPrefs.Save();
    }

    public static string GetGameModeLabel()
    {
        string text = PlayerPrefs.GetString("currentGameModePostSI", "");
        if (!string.IsNullOrEmpty(text))
        {
            return text.ToUpper();
        }

        return "N/A";
    }

    public static void CycleGameMode()
    {
        GorillaComputer instance = GorillaComputer.instance;
        if (instance == null)
        {
            return;
        }

        HashSet<GameModeType> hashSet = null;
        try
        {
            hashSet = GameMode.GameModeZoneMapping.AllModes;
        }
        catch { }

        string text = PlayerPrefs.GetString("currentGameModePostSI", "");
        int    num  = 0;
        for (int i = 0; i < GameModes.Length; i++)
        {
            if (GameModes[i].ToString() == text)
            {
                num = i;

                break;
            }
        }

        int          num2 = 1;
        GameModeType item;
        while (true)
        {
            if (num2 <= GameModes.Length)
            {
                item = GameModes[(num + num2) % GameModes.Length];
                if (hashSet == null || hashSet.Contains(item))
                {
                    break;
                }

                num2++;

                continue;
            }

            return;
        }

        instance.OnModeSelectButtonPress(item.ToString(), instance.leftHanded);
    }

    public static string GetPushToTalkLabel()
    {
        GorillaComputer instance = GorillaComputer.instance;
        if (instance == null)
        {
            return "N/A";
        }

        if (instance.pttType == "PUSH TO TALK")
        {
            return "PTT";
        }

        if (!(instance.pttType == "PUSH TO MUTE"))
        {
            return "OPEN";
        }

        return "PTM";
    }

    public static void CyclePushToTalkMode()
    {
        GorillaComputer instance = GorillaComputer.instance;
        if (!(instance == null))
        {
            instance.pttType = VoiceModes[(Array.IndexOf(VoiceModes, instance.pttType) + 1 + VoiceModes.Length) % VoiceModes.Length];
            PlayerPrefs.SetString("pttType", instance.pttType);
            PlayerPrefs.Save();
        }
    }

    public static string GetVoiceChatLabel()
    {
        GorillaComputer instance = GorillaComputer.instance;
        if (instance == null)
        {
            return "N/A";
        }

        if (!(instance.voiceChatOn == "TRUE"))
        {
            if (!(instance.voiceChatOn == "FALSE"))
            {
                return "OFF";
            }

            return "MONKE";
        }

        return "HUMAN";
    }

    public static void ToggleVoiceChat()
    {
        GorillaComputer instance = GorillaComputer.instance;
        if (!(instance == null))
        {
            instance.voiceChatOn = instance.voiceChatOn == "TRUE" ? "FALSE" : "TRUE";
            PlayerPrefs.SetString("voiceChatOn", instance.voiceChatOn);
            PlayerPrefs.Save();
            RigContainer.RefreshAllRigVoices();
        }
    }

    public static string GetOutfitLabel()
    {
        if (CosmeticsController.instance == null)
        {
            return "N/A";
        }

        if (!CosmeticsController.CanScrollOutfits())
        {
            return "WAIT";
        }

        return "SET " + (CosmeticsController.SelectedOutfit + 1);
    }

    public static void CycleOutfit(bool bool_0)
    {
        CosmeticsController instance = CosmeticsController.instance;
        if (instance != null && CosmeticsController.CanScrollOutfits())
        {
            instance.PressWardrobeScrollOutfit(bool_0);
        }
    }
}