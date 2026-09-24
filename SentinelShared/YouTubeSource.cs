using System;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace SentinelShared;

public static class YouTubeSource
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool IsVideoId(string arg1) => Regex.IsMatch(arg1 ?? "", "^[A-Za-z0-9_-]{11}$");

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool IsChannelId(string arg1) => Regex.IsMatch(arg1 ?? "", "^UC[A-Za-z0-9_-]{22}$");

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool IsHandle(string arg1)
    {
        if (Regex.IsMatch(arg1 ?? "", "^[\\p{L}\\p{M}\\p{N}_.-]{1,30}$"))
        {
            return Regex.IsMatch(arg1 ?? "", "[\\p{L}\\p{N}]");
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string Normalize(string source)
    {
        source = (source ?? "").Trim();
        if (source.Length != 0 && source.Length <= 2048)
        {
            string text = source;
            int    num  = 0;
            while (true)
            {
                if (num < text.Length)
                {
                    if (char.IsControl(text[num]))
                    {
                        break;
                    }

                    num++;

                    continue;
                }

                if (IsChannelId(source))
                {
                    return "https://www.youtube.com/channel/" + source + "/live";
                }

                if (source.StartsWith("@") && IsHandle(source.Substring(1)))
                {
                    return BuildHandleLiveUrl(source.Substring(1));
                }

                if (source.StartsWith("youtube.com/", StringComparison.OrdinalIgnoreCase) || source.StartsWith("www.youtube.com/", StringComparison.OrdinalIgnoreCase) || source.StartsWith("youtu.be/", StringComparison.OrdinalIgnoreCase))
                {
                    source = "https://" + source;
                }

                if (Uri.TryCreate(source, UriKind.Absolute, out Uri result))
                {
                    if ((!(result.Scheme != "https") || !(result.Scheme != "http")) && result.IsDefaultPort && result.UserInfo.Length <= 0)
                    {
                        string   text2 = result.Host.ToLowerInvariant();
                        string[] array = Uri.UnescapeDataString(result.AbsolutePath).Trim('/').Split('/');
                        if (!(text2 == "youtu.be"))
                        {
                            if (text2 != "youtube.com" && text2 != "www.youtube.com" && text2 != "m.youtube.com" && text2 != "music.youtube.com")
                            {
                                return "";
                            }

                            if (array.Length == 1 && array[0] == "watch")
                            {
                                string[] array2 = result.Query.TrimStart('?').Split('&');
                                foreach (string text3 in array2)
                                {
                                    if (text3.StartsWith("v=", StringComparison.Ordinal))
                                    {
                                        string arg = Uri.UnescapeDataString(text3.Substring(2));
                                        if (!IsVideoId(arg))
                                        {
                                            return "";
                                        }

                                        return BuildWatchUrl(arg);
                                    }
                                }
                            }

                            if (array.Length != 2 || !(array[0] == "live") && !(array[0] == "shorts") && !(array[0] == "embed") || !IsVideoId(array[1]))
                            {
                                if ((array.Length == 1 || array.Length == 2 && IsChannelTab(array[1])) && array[0].StartsWith("@") && IsHandle(array[0].Substring(1)))
                                {
                                    return BuildHandleLiveUrl(array[0].Substring(1));
                                }

                                if (array.Length == 2 || array.Length == 3 && IsChannelTab(array[2]))
                                {
                                    if (array[0] == "channel" && IsChannelId(array[1]))
                                    {
                                        return "https://www.youtube.com/channel/" + array[1] + "/live";
                                    }

                                    if ((array[0] == "c" || array[0] == "user") && IsHandle(array[1]))
                                    {
                                        return "https://www.youtube.com/" + array[0] + "/" + Uri.EscapeDataString(array[1]) + "/live";
                                    }
                                }

                                return "";
                            }

                            return BuildWatchUrl(array[1]);
                        }

                        if (array.Length == 1 && IsVideoId(array[0]))
                        {
                            return BuildWatchUrl(array[0]);
                        }

                        return "";
                    }

                    return "";
                }

                if (!IsHandle(source))
                {
                    return "";
                }

                return BuildHandleLiveUrl(source);
            }

            return "";
        }

        return "";
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool IsChannelTab(string arg1)
    {
        switch (arg1)
        {
            default:
                return arg1 == "featured";

            case "live":
            case "streams":
            case "videos":
                return true;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string BuildHandleLiveUrl(string arg1) => "https://www.youtube.com/@" + Uri.EscapeDataString(arg1) + "/live";

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string BuildWatchUrl(string arg1) => "https://www.youtube.com/watch?v=" + arg1;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string NormalizeInput(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return "";
        }

        string text = Normalize(source);
        if (text.Length == 0)
        {
            throw new ArgumentException("Enter your @YouTubeHandle or YouTube channel link. A display name with spaces will not identify your channel.");
        }

        return text;
    }
}