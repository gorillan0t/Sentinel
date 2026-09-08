using System;
using System.Collections.Generic;
using GorillaGameModes;
using GorillaLocomotion;
using GorillaNetworking;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class GameSettings
{
    private static readonly string[] QueueNames = new string[3] { "DEFAULT", "MINIGAMES", "COMPETITIVE", };

    private static readonly string[] VoiceModes = new string[3] { "OPEN MIC", "PUSH TO TALK", "PUSH TO MUTE", };

    private static readonly GameModeType[] GameModes;

    private static BetterDayNightManager timeManager;

    private static int lockedTimeIndex;

    static GameSettings()
    {
        GameModes       = (GameModeType[])Enum.GetValues(typeof(GameModeType));
        lockedTimeIndex = -1;
    }

    public static bool IsTimeOverrideActive => lockedTimeIndex >= 0;

    public static Vector3 GroundPointAhead(Transform arg1, float arg2)
    {
        Vector3 forward = arg1.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }
        else
        {
            forward.Normalize();
        }

        Vector3 val  = arg1.position + forward * arg2 - Vector3.up * 0.1f;
        int     num  = (Object)(object)GTPlayer.Instance != (Object)null ? GTPlayer.Instance.locomotionEnabledLayers.value : -1;
        float   num2 = Physics.Raycast(val + Vector3.up * 2.5f, Vector3.down, out RaycastHit val2, 14f, num, (QueryTriggerInteraction)1) ? val2.point.y : val.y - 0.95f;
        val.y = Mathf.Max(val.y, num2                                                                                                                           + 0.95f);

        return val;
    }

    public static string GetTimeOfDayLabel()
    {
        BetterDayNightManager instance = BetterDayNightManager.instance;
        if ((Object)(object)instance == (Object)null)
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

    public static string GetQueueLabel()
    {
        GorillaComputer instance = GorillaComputer.instance;
        if ((Object)(object)instance == (Object)null || string.IsNullOrEmpty(instance.currentQueue))
        {
            return "N/A";
        }

        return instance.currentQueue;
    }

    public static void CycleQueue()
    {
        GorillaComputer instance = GorillaComputer.instance;
        if ((Object)(object)instance == (Object)null)
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
        if ((Object)(object)instance == (Object)null)
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

        for (int j = 1; j <= GameModes.Length; j++)
        {
            GameModeType item = GameModes[(num + j) % GameModes.Length];
            if (hashSet == null || hashSet.Contains(item))
            {
                instance.OnModeSelectButtonPress(item.ToString(), instance.leftHanded);

                break;
            }
        }
    }

    public static string GetPushToTalkLabel()
    {
        GorillaComputer instance = GorillaComputer.instance;
        if ((Object)(object)instance == (Object)null)
        {
            return "N/A";
        }

        if (instance.pttType == "PUSH TO TALK")
        {
            return "PTT";
        }

        if (instance.pttType == "PUSH TO MUTE")
        {
            return "PTM";
        }

        return "OPEN";
    }

    public static void CyclePushToTalkMode()
    {
        GorillaComputer instance = GorillaComputer.instance;
        if (!((Object)(object)instance == (Object)null))
        {
            instance.pttType = VoiceModes[(Array.IndexOf(VoiceModes, instance.pttType) + 1 + VoiceModes.Length) % VoiceModes.Length];
            PlayerPrefs.SetString("pttType", instance.pttType);
            PlayerPrefs.Save();
        }
    }

    public static string GetVoiceChatLabel()
    {
        GorillaComputer instance = GorillaComputer.instance;
        if (!((Object)(object)instance == (Object)null))
        {
            if (instance.voiceChatOn == "TRUE")
            {
                return "HUMAN";
            }

            if (instance.voiceChatOn == "FALSE")
            {
                return "MONKE";
            }

            return "OFF";
        }

        return "N/A";
    }

    public static void ToggleVoiceChat()
    {
        GorillaComputer instance = GorillaComputer.instance;
        if (!((Object)(object)instance == (Object)null))
        {
            instance.voiceChatOn = instance.voiceChatOn == "TRUE" ? "FALSE" : "TRUE";
            PlayerPrefs.SetString("voiceChatOn", instance.voiceChatOn);
            PlayerPrefs.Save();
            RigContainer.RefreshAllRigVoices();
        }
    }

    public static string GetOutfitLabel()
    {
        if ((Object)(object)CosmeticsController.instance == (Object)null)
        {
            return "N/A";
        }

        if (CosmeticsController.CanScrollOutfits())
        {
            return "SET " + (CosmeticsController.SelectedOutfit + 1);
        }

        return "WAIT";
    }

    public static void CycleOutfit(bool arg1)
    {
        CosmeticsController instance = CosmeticsController.instance;
        if ((Object)(object)instance != (Object)null && CosmeticsController.CanScrollOutfits())
        {
            instance.PressWardrobeScrollOutfit(arg1);
        }
    }

    public static void ToggleTimeLock()
    {
        if (IsTimeOverrideActive)
        {
            DisableTimeLock();

            return;
        }

        BetterDayNightManager instance = BetterDayNightManager.instance;
        if (IsTimeManagerReady(instance))
        {
            lockedTimeIndex = Math.Max(0, Math.Min(instance.currentTimeIndex, instance.timeOfDayRange.Length - 1));
            ApplyLockedTime(instance);
        }
    }

    public static void UpdateTimeLock()
    {
        if (!IsTimeOverrideActive)
        {
            return;
        }

        BetterDayNightManager instance = BetterDayNightManager.instance;
        if (IsTimeManagerReady(instance))
        {
            lockedTimeIndex = Math.Min(lockedTimeIndex, instance.timeOfDayRange.Length - 1);
            if ((Object)(object)timeManager != (Object)(object)instance || (int)instance.currentSetting != 0 || instance.currentTimeIndex != lockedTimeIndex)
            {
                ApplyLockedTime(instance);
            }
        }
    }

    public static void DisableTimeLock()
    {
        BetterDayNightManager val = timeManager;
        timeManager     = null;
        lockedTimeIndex = -1;
        if ((Object)(object)val != (Object)null)
        {
            val.ClearTimeOfDay(true);
        }
    }

    public static void CycleTimeOfDay(int arg1)
    {
        BetterDayNightManager instance = BetterDayNightManager.instance;
        if (!IsTimeManagerReady(instance))
        {
            return;
        }

        int num = instance.timeOfDayRange.Length;
        if (!IsTimeOverrideActive)
        {
            int num2 = (int)instance.currentSetting != 1 ? instance.currentTimeIndex + arg1 : arg1 <= 0 ? num - 1 : 0;
            if (num2 >= 0 && num2 < num)
            {
                instance.SetTimeOfDay(num2, true);
                instance.SetOverrideIndex(num2);
            }
            else
            {
                instance.ClearTimeOfDay(true);
            }
        }
        else
        {
            lockedTimeIndex = ((lockedTimeIndex + arg1) % num + num) % num;
            ApplyLockedTime(instance);
        }
    }

    private static bool IsTimeManagerReady(BetterDayNightManager arg1)
    {
        if ((Object)(object)arg1 != (Object)null && arg1.timeOfDayRange != null)
        {
            return arg1.timeOfDayRange.Length != 0;
        }

        return false;
    }

    private static void ApplyLockedTime(BetterDayNightManager arg1)
    {
        if ((Object)(object)timeManager != (Object)null && (Object)(object)timeManager != (Object)(object)arg1)
        {
            timeManager.ClearTimeOfDay(true);
        }

        timeManager = arg1;
        arg1.SetTimeOfDay(lockedTimeIndex, true);
        arg1.SetOverrideIndex(lockedTimeIndex);
    }
}