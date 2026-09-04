using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Sentinel.Sentinel;

namespace Sentinel;

internal static class YouTubeLiveChat
{

    private static readonly HttpClient HttpClient = CreateHttpClient();

    public static string[] Messages = new string[0];

    public static string Status = "";

    public static int MessageVersion;

    private static readonly List<string> messageBuffer = new();

    private static string channelId = "";

    private static string apiKey;

    private static string clientVersion;

    private static string continuation;

    private static DateTime nextPollAt;

    private static bool pollInProgress;

    public static bool IsConfigured => channelId.Length > 0;

    private static HttpClient CreateHttpClient() =>
            new(new HttpClientHandler
            {
                    UseCookies = false,
            })
            {
                    Timeout = TimeSpan.FromSeconds(10.0),
                    DefaultRequestHeaders =
                    {
                            { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36" },
                            { "Accept-Language", "en-US,en;q=0.9" },
                            { "Cookie", "SOCS=CAI; CONSENT=YES+1" },
                    },
            };

    public static void Initialize() => channelId = Cfg.YtChannel.Value.Trim();

    public static void Reset()
    {
        continuation = null;
        nextPollAt   = DateTime.MinValue;
    }

    private static string BuildLiveChannelUrl()
    {
        if (channelId.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            if (!channelId.Contains("watch?v=") && !channelId.Contains("youtu.be/") && !channelId.Contains("/live/"))
            {
                return channelId.TrimEnd('/') + "/live";
            }

            return channelId;
        }

        if (channelId.StartsWith("@"))
        {
            return "https://www.youtube.com/" + channelId + "/live";
        }

        if (channelId.StartsWith("UC") && channelId.Length == 24)
        {
            return "https://www.youtube.com/channel/" + channelId + "/live";
        }

        return "https://www.youtube.com/watch?v=" + channelId;
    }

    private static string ReadRegexGroup(string string_6, string string_7)
    {
        Match match = Regex.Match(string_6, string_7, RegexOptions.Singleline);
        if (!match.Success)
        {
            return null;
        }

        return match.Groups[1].Value;
    }

    private static bool ResolveLiveStream()
    {
        string result = HttpClient.GetStringAsync(BuildLiveChannelUrl()).Result;
        string text   = ReadRegexGroup(result, "<link rel=\"canonical\" href=\"https://www\\.youtube\\.com/watch\\?v=([^\"]+)\">");
        if (text != null && !Regex.IsMatch(result, "['\"]isReplay['\"]:\\s*true"))
        {
            string result2 = HttpClient.GetStringAsync("https://www.youtube.com/live_chat?v=" + text).Result;
            apiKey        = ReadRegexGroup(result2, "\"INNERTUBE_API_KEY\":\"([^\"]+)\"")        ?? "AIzaSyAO_FJ2SlqU8Q4STEHLGCilw_Y9_11qcW8";
            clientVersion = ReadRegexGroup(result2, "\"INNERTUBE_CLIENT_VERSION\":\"([^\"]+)\"") ?? "2.20240101.00.00";
            string text2 = ReadRegexGroup(result2, "ytInitialData(?:\"\\]|'\\])?\\s*=\\s*(\\{.*?\\});\\s*</script>");
            object obj;
            if (text2 == null)
            {
                obj = null;
            }
            else
            {
                JToken obj2 = JObject.Parse(text2)["contents"];
                object obj3;
                if (obj2 == null)
                {
                    obj3 = null;
                }
                else
                {
                    JToken obj4 = obj2["liveChatRenderer"];
                    obj3 = obj4 != null ? obj4["continuations"] : null;
                }

                obj = obj3 is JArray ? obj3 : null;
            }

            JArray val = (JArray)obj;
            continuation = val == null || val.Count <= 0 ? null : ReadContinuation((JObject)val[0], out int _);
            if (continuation != null)
            {
                Status = "LIVE";

                return true;
            }

            Status = "CHAT OFF";

            return false;
        }

        Status = "NOT LIVE";

        return false;
    }

    private static string ReadContinuation(JObject jobject_0, out int int_1)
    {
        int_1 = 5000;
        string text = null;
        foreach (JProperty item in jobject_0.Properties())
        {
            JToken  value = item.Value;
            JObject val   = (JObject)(value is JObject ? value : null);
            if (val != null)
            {
                text  = (string)val["continuation"] ?? text;
                int_1 = (int?)val["timeoutMs"]      ?? int_1;
            }
        }

        return text;
    }

    public static void Poll()
    {
        if (!IsConfigured || pollInProgress || DateTime.UtcNow < nextPollAt)
        {
            return;
        }

        pollInProgress = true;
        nextPollAt     = DateTime.UtcNow.AddSeconds(5.0);
        Task.Run(delegate
                 {
                     try
                     {
                         if (continuation == null && !ResolveLiveStream())
                         {
                             nextPollAt = DateTime.UtcNow.AddSeconds(20.0);

                             return;
                         }

                         StringContent             content             = new("{\"context\":{\"client\":{\"clientName\":\"WEB\",\"clientVersion\":\""             + clientVersion + "\"}},\"continuation\":\"" + continuation + "\"}", Encoding.UTF8, "application/json");
                         using HttpResponseMessage httpResponseMessage = HttpClient.PostAsync("https://www.youtube.com/youtubei/v1/live_chat/get_live_chat?key=" + apiKey        + "&prettyPrint=false", content).Result;
                         string                    result              = httpResponseMessage.Content.ReadAsStringAsync().Result;
                         if (httpResponseMessage.IsSuccessStatusCode)
                         {
                             JToken  obj  = JObject.Parse(result)["continuationContents"];
                             JToken  obj2 = obj == null ? null : obj["liveChatContinuation"];
                             JObject val  = (JObject)(obj2 is JObject ? obj2 : null);
                             if (val != null)
                             {
                                 JToken obj3 = val["continuations"];
                                 JArray val2 = (JArray)(obj3 is JArray ? obj3 : null);
                                 int    int_ = 5000;
                                 continuation = val2 == null || val2.Count <= 0 ? null : ReadContinuation((JObject)val2[0], out int_);
                                 nextPollAt   = DateTime.UtcNow.AddMilliseconds(Math.Max(Math.Min(int_, 8000), 1500));
                                 JToken obj4 = val["actions"];
                                 JArray val3 = (JArray)(obj4 is JArray ? obj4 : null);
                                 if (val3 != null)
                                 {
                                     foreach (JToken item in val3)
                                     {
                                         JToken  obj5 = item["addChatItemAction"];
                                         JToken  obj6 = obj5 != null ? obj5["item"] : null;
                                         JObject val4 = (JObject)(obj6 is JObject ? obj6 : null);
                                         if (val4 != null)
                                         {
                                             JToken obj7 = val4["liveChatTextMessageRenderer"];
                                             object obj8 = obj7 is JObject ? obj7 : null;
                                             if (obj8 == null)
                                             {
                                                 JToken obj9 = val4["liveChatPaidMessageRenderer"];
                                                 obj8 = obj9 is JObject ? obj9 : null;
                                             }

                                             JObject val5 = (JObject)obj8;
                                             if (val5 != null)
                                             {
                                                 JToken obj10 = val5["message"];
                                                 string text  = RenderMessageText((JObject)(obj10 is JObject ? obj10 : null));
                                                 JToken obj11 = val5["purchaseAmountText"];
                                                 string text2 = (string)(obj11 != null ? obj11["simpleText"] : null);
                                                 if (text2 != null)
                                                 {
                                                     text = "[" + text2 + "] " + text;
                                                 }

                                                 if (text.Length != 0)
                                                 {
                                                     List<string> list  = messageBuffer;
                                                     JToken       obj12 = val5["authorName"];
                                                     list.Add(((string)(obj12 != null ? obj12["simpleText"] : null) ?? "?") + "\t" + text);
                                                 }
                                             }
                                         }
                                     }

                                     while (messageBuffer.Count > 8)
                                     {
                                         messageBuffer.RemoveAt(0);
                                     }

                                     Messages = messageBuffer.ToArray();
                                     MessageVersion++;
                                 }
                             }
                             else
                             {
                                 continuation = null;
                                 Status       = "NOT LIVE";
                                 nextPollAt   = DateTime.UtcNow.AddSeconds(15.0);
                             }
                         }
                         else
                         {
                             continuation = null;
                             nextPollAt   = DateTime.UtcNow.AddSeconds(10.0);
                         }
                     }
                     catch
                     {
                         continuation = null;
                         nextPollAt   = DateTime.UtcNow.AddSeconds(15.0);
                     }
                     finally
                     {
                         pollInProgress = false;
                     }
                 });
    }

    private static string RenderMessageText(JObject jobject_0)
    {
        JToken obj = jobject_0?["runs"];
        JArray val = (JArray)(obj is JArray ? obj : null);
        if (val == null)
        {
            return "";
        }

        StringBuilder stringBuilder = new();
        foreach (JToken item in val)
        {
            string text = (string)item["text"];
            if (text != null)
            {
                stringBuilder.Append(text);

                continue;
            }

            JToken obj2 = item["emoji"];
            JToken obj3 = obj2 != null ? obj2["shortcuts"] : null;
            JArray val2 = (JArray)(obj3 is JArray ? obj3 : null);
            if (val2 != null && val2.Count > 0)
            {
                stringBuilder.Append((string)val2[0]);
            }
        }

        return stringBuilder.ToString();
    }

    [Serializable]
    private sealed class PollJob
    {
        public static readonly PollJob Field0 = new();

        public static Action Field1;

        internal void PollWorker()
        {
            try
            {
                if (continuation == null && !ResolveLiveStream())
                {
                    nextPollAt = DateTime.UtcNow.AddSeconds(20.0);

                    return;
                }

                StringContent             content             = new("{\"context\":{\"client\":{\"clientName\":\"WEB\",\"clientVersion\":\""             + clientVersion + "\"}},\"continuation\":\"" + continuation + "\"}", Encoding.UTF8, "application/json");
                using HttpResponseMessage httpResponseMessage = HttpClient.PostAsync("https://www.youtube.com/youtubei/v1/live_chat/get_live_chat?key=" + apiKey        + "&prettyPrint=false", content).Result;
                string                    result              = httpResponseMessage.Content.ReadAsStringAsync().Result;
                if (httpResponseMessage.IsSuccessStatusCode)
                {
                    JToken  obj  = JObject.Parse(result)["continuationContents"];
                    JToken  obj2 = obj == null ? null : obj["liveChatContinuation"];
                    JObject val  = (JObject)(obj2 is JObject ? obj2 : null);
                    if (val != null)
                    {
                        JToken obj3 = val["continuations"];
                        JArray val2 = (JArray)(obj3 is JArray ? obj3 : null);
                        int    int_ = 5000;
                        continuation = val2 == null || val2.Count <= 0 ? null : ReadContinuation((JObject)val2[0], out int_);
                        nextPollAt   = DateTime.UtcNow.AddMilliseconds(Math.Max(Math.Min(int_, 8000), 1500));
                        JToken obj4 = val["actions"];
                        JArray val3 = (JArray)(obj4 is JArray ? obj4 : null);
                        if (val3 == null)
                        {
                            return;
                        }

                        foreach (JToken item in val3)
                        {
                            JToken  obj5 = item["addChatItemAction"];
                            JToken  obj6 = obj5 != null ? obj5["item"] : null;
                            JObject val4 = (JObject)(obj6 is JObject ? obj6 : null);
                            if (val4 == null)
                            {
                                continue;
                            }

                            JToken obj7 = val4["liveChatTextMessageRenderer"];
                            object obj8 = obj7 is JObject ? obj7 : null;
                            if (obj8 == null)
                            {
                                JToken obj9 = val4["liveChatPaidMessageRenderer"];
                                obj8 = obj9 is JObject ? obj9 : null;
                            }

                            JObject val5 = (JObject)obj8;
                            if (val5 != null)
                            {
                                JToken obj10 = val5["message"];
                                string text  = RenderMessageText((JObject)(obj10 is JObject ? obj10 : null));
                                JToken obj11 = val5["purchaseAmountText"];
                                string text2 = (string)(obj11 != null ? obj11["simpleText"] : null);
                                if (text2 != null)
                                {
                                    text = "[" + text2 + "] " + text;
                                }

                                if (text.Length != 0)
                                {
                                    List<string> messageBuffer = YouTubeLiveChat.messageBuffer;
                                    JToken       obj12         = val5["authorName"];
                                    messageBuffer.Add(((string)(obj12 != null ? obj12["simpleText"] : null) ?? "?") + "\t" + text);
                                }
                            }
                        }

                        while (messageBuffer.Count > 8)
                        {
                            messageBuffer.RemoveAt(0);
                        }

                        Messages = messageBuffer.ToArray();
                        MessageVersion++;
                    }
                    else
                    {
                        continuation = null;
                        Status       = "NOT LIVE";
                        nextPollAt   = DateTime.UtcNow.AddSeconds(15.0);
                    }
                }
                else
                {
                    continuation = null;
                    nextPollAt   = DateTime.UtcNow.AddSeconds(10.0);
                }
            }
            catch
            {
                continuation = null;
                nextPollAt   = DateTime.UtcNow.AddSeconds(15.0);
            }
            finally
            {
                pollInProgress = false;
            }
        }
    }
}