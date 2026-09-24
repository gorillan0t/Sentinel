using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;

namespace Sentinel;

public sealed class FriendState
{
    public FriendView[] Friends { get; private set; } = Array.Empty<FriendView>();

    public FriendView[] Incoming { get; private set; } = Array.Empty<FriendView>();

    public FriendView[] Outgoing { get; private set; } = Array.Empty<FriendView>();

    public bool ShareActivity { get; private set; }

    public string FriendCode { get; private set; } = "";

    public DateTime ReceivedAt { get; private set; }

    internal static FriendState CreateLocal(FriendView[] friends, bool shareActivity, DateTime receivedAt) =>
            new()
            {
                    Friends       = friends ?? Array.Empty<FriendView>(),
                    Incoming      = Array.Empty<FriendView>(),
                    Outgoing      = Array.Empty<FriendView>(),
                    ShareActivity = shareActivity,
                    FriendCode    = "",
                    ReceivedAt    = receivedAt,
            };

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static FriendState Parse(JObject data, DateTime now) =>
            new()
            {
                    ShareActivity = data.Value<bool?>("share_activity") == true,
                    FriendCode    = GetString(data, "friend_code", 32),
                    ReceivedAt    = ParseSnapshotTime(data, now),
                    Friends       = ParseViews(data["friends"] as JArray,  false),
                    Incoming      = ParseViews(data["incoming"] as JArray, true),
                    Outgoing      = ParseViews(data["outgoing"] as JArray, true),
            };

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static DateTime ParseSnapshotTime(JObject arg1, DateTime arg2)
    {
        JToken val = arg1["snapshot_at"];
        if (val == null || (int)val.Type != 12)
        {
            if (DateTime.TryParse(GetString(arg1, "snapshot_at", 40), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime result))
            {
                if (!(result < arg2))
                {
                    return arg2;
                }

                return result;
            }

            return DateTime.MinValue;
        }

        DateTime dateTime = val.Value<DateTime>().ToUniversalTime();
        if (dateTime < arg2)
        {
            return dateTime;
        }

        return arg2;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static FriendView[] ParseViews(JArray arg1, bool arg2)
    {
        List<FriendView> list    = new();
        HashSet<string>  hashSet = new(StringComparer.Ordinal);
        if (arg1 != null)
        {
            foreach (JToken item in arg1)
            {
                JObject val = (JObject)(item is JObject ? item : null);
                if (val == null)
                {
                    continue;
                }

                JObject val2 = arg2 ? val["user"] as JObject : val;
                if (val2 == null)
                {
                    continue;
                }

                string text  = GetString(val2, "id", 24);
                string text2 = arg2 ? GetString(val, "id", 24) : "";
                if (IsValidId(text) && (!arg2 || IsValidId(text2)) && hashSet.Add(arg2 ? text2 : text))
                {
                    string text3 = arg2 ? "pending" : GetString(val2, "status", 16);
                    if (text3 != "online" && text3 != "offline" && text3 != "hidden" && text3 != "pending")
                    {
                        text3 = "unknown";
                    }

                    string text4 = text3 == "online" ? GetString(val2, "room_code", 10) : "";
                    if (!ValidRoom(text4))
                    {
                        text4 = "";
                    }

                    string text5 = GetString(val2, "display_name", 64);
                    list.Add(new FriendView(text, GetString(val2, "game_id", 128), text5.Length == 0 ? "Sentinel user" : text5, text3, text4, text2));
                    if (list.Count >= 1000)
                    {
                        break;
                    }
                }
            }

            return list.ToArray();
        }

        return list.ToArray();
    }

    public static bool ValidRoom(string room)
    {
        if (!string.IsNullOrEmpty(room) && room.Length <= 10)
        {
            int num = 0;
            while (true)
            {
                if (num < room.Length)
                {
                    char c = room[num];
                    if ((c < 'A' || c > 'Z') && (c < '0' || c > '9'))
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

        return false;
    }

    private static bool IsValidId(string arg1)
    {
        if (arg1.Length != 24)
        {
            return false;
        }

        int num = 0;
        while (true)
        {
            if (num < arg1.Length)
            {
                char c = arg1[num];
                if ((c < 'a' || c > 'f') && (c < '0' || c > '9'))
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

    private static string GetString(JObject arg1, string arg2, int arg3)
    {
        JToken obj = arg1[arg2];
        if (obj == null || (int)obj.Type != 8)
        {
            return "";
        }

        string text = (string)arg1[arg2];
        if (text.Length > arg3)
        {
            return "";
        }

        string text2 = text;
        for (int i = 0; i < text2.Length; i++)
        {
            if (char.IsControl(text2[i]))
            {
                return "";
            }
        }

        return text;
    }
}