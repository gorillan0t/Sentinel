using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sentinel;
using SentinelShared;

internal static class YouTubeLiveChat
{

    private static readonly HttpClient HttpClient = CreateHttpClient();

    public static string[] Messages = new string[0];

    public static string Status = "";

    public static string StreamTitle = "";

    private static readonly SemaphoreSlim requestLock = new(1, 1);

    public static int MessageVersion;

    private static string configurationKey = "";

    private static ChatSession session = new("");

    public static bool IsConfigured => configurationKey.Length > 0;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static HttpClient CreateHttpClient() =>
            new(new HttpClientHandler
            {
                    UseCookies = false,
            })
            {
                    Timeout = TimeSpan.FromSeconds(10.0),
                    DefaultRequestHeaders =
                    {
                            { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/130.0.0.0 Safari/537.36" },
                            { "Accept-Language", "en-US,en;q=0.9" },
                    },
            };

    public static void Initialize()
    {
        string text = (Cfg.YtChannel.Value ?? "").Trim();
        if (!(configurationKey == text))
        {
            configurationKey = text;
            Reset();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Reset()
    {
        session.cancellation.Cancel();
        session     = new ChatSession(BuildLiveChannelUrl());
        StreamTitle = "";
        Messages    = new string[0];
        MessageVersion++;
        Status = !IsConfigured ? "" : session.sourceUrl.Length == 0 ? "ENTER A YOUTUBE VIDEO OR CHANNEL" : "LOOKING...";
    }

    private static string BuildLiveChannelUrl() => YouTubeSource.Normalize(configurationKey);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool IsValidConfiguration(string arg1) => Regex.IsMatch(arg1 ?? "", "^[A-Za-z0-9_-]{11}$");

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string ReadRegexGroup(string arg1, string arg2)
    {
        Match match = Regex.Match(arg1, "\"" + Regex.Escape(arg2) + "\"\\s*:\\s*(\"(?:\\\\.|[^\"\\\\])*\")");
        if (match.Success)
        {
            return JsonConvert.DeserializeObject<string>(match.Groups[1].Value);
        }

        return null;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static JObject ParseObject(string arg1) => FindObject(arg1, "ytInitialData");

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static JObject FindObject(string arg1, string arg2)
    {
        Match match = Regex.Match(arg1, "(?:" + Regex.Escape(arg2) + "|window\\s*\\[\\s*['\"]" + Regex.Escape(arg2) + "['\"]\\s*\\])\\s*=\\s*(?=\\{)");
        if (match.Success)
        {
            using (StringReader stringReader = new(arg1.Substring(match.Index + match.Length)))
            {
                JsonTextReader val = new(stringReader);
                try
                {
                    return JObject.Load(val);
                }
                finally
                {
                    ((IDisposable)val)?.Dispose();
                }
            }
        }

        return null;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string ReadContinuation(JObject arg1, out int arg2)
    {
        arg2 = 5000;
        if (arg1 == null)
        {
            return null;
        }

        foreach (JProperty item in arg1.Properties())
        {
            if (item.Name.IndexOf("Replay", StringComparison.OrdinalIgnoreCase) < 0)
            {
                JToken  value = item.Value;
                JObject val   = (JObject)(value is JObject ? value : null);
                if (val != null && val["continuation"] != null)
                {
                    arg2 = Math.Max(1500, (int?)val["timeoutMs"] ?? 5000);

                    return (string)val["continuation"];
                }
            }
        }

        return null;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string ReadContinuationFromToken(JToken arg1, out int arg2)
    {
        arg2 = 5000;
        JToken obj = arg1 != null ? arg1["continuations"] : null;
        JArray val = (JArray)(obj is JArray ? obj : null);
        if (val == null)
        {
            return null;
        }

        foreach (JToken item in val)
        {
            string text = ReadContinuation((JObject)(item is JObject ? item : null), out arg2);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }
        }

        return null;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string NormalizeText(string arg1)
    {
        foreach (Match item in Regex.Matches(arg1, "<link\\b[^>]*>", RegexOptions.IgnoreCase))
        {
            if (Regex.IsMatch(item.Value, "\\brel\\s*=\\s*['\"]canonical['\"]", RegexOptions.IgnoreCase))
            {
                string text = YouTubeSource.Normalize(WebUtility.HtmlDecode(Regex.Match(item.Value, "\\bhref\\s*=\\s*['\"]([^'\"]+)['\"]", RegexOptions.IgnoreCase).Groups[1].Value));
                if (text.StartsWith("https://www.youtube.com/watch?v="))
                {
                    return text.Substring(text.IndexOf("v=", StringComparison.Ordinal) + 2);
                }
            }
        }

        return null;
    }

    private async static Task<string> ReadResponseBody(HttpResponseMessage arg1)
    {
        if (arg1.StatusCode != HttpStatusCode.TooManyRequests)
        {
            if (arg1.StatusCode != HttpStatusCode.Forbidden)
            {
                if (!arg1.IsSuccessStatusCode)
                {
                    throw new RetryAfterException("CHAT REQUEST FAILED (" + (int)arg1.StatusCode + ")");
                }

                return await arg1.Content.ReadAsStringAsync().ConfigureAwait(false);
            }

            throw new RetryAfterException("YOUTUBE BLOCKED CHAT ACCESS", 30);
        }

        throw new RetryAfterException("CHAT RATE LIMITED: WAITING", OAuthSession.GetResponseStatusCode(arg1));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Poll()
    {
        ChatSession chatSession = session;
        DateTime    utcNow      = DateTime.UtcNow;
        bool        flag        = chatSession.lastPollAt != default(DateTime) && utcNow - chatSession.lastPollAt > TimeSpan.FromSeconds(15.0);
        chatSession.lastPollAt = utcNow;
        if (flag)
        {
            chatSession.lastStreamLookupAt = DateTime.MinValue;
            chatSession.generation++;
            chatSession.messages.Clear();
            Messages    = new string[0];
            StreamTitle = "";
            MessageVersion++;
            if (chatSession.continuation != null)
            {
                Status = "CHECKING LIVE STREAM...";
            }
        }

        if (chatSession.sourceUrl.Length != 0 && !chatSession.pollInProgress && (!(utcNow < chatSession.nextPollAt) || flag && chatSession.continuation != null))
        {
            chatSession.pollInProgress = true;
            int generation = chatSession.generation;
            Task.Run(() => PollWorker(chatSession, generation));
        }
    }

    private async static Task PollWorker(ChatSession arg1, int arg2)
    {
        string            apiKey             = arg1.apiKey;
        string            clientVersion      = arg1.clientVersion;
        string            continuation       = arg1.continuation;
        string            videoId            = arg1.videoId;
        string            streamTitle        = arg1.streamTitle;
        string            channelUrl         = arg1.channelUrl;
        DateTime          lastStreamLookupAt = arg1.lastStreamLookupAt;
        List<JObject>     chatActions        = new();
        int               pollDelayMs        = 5000;
        string            status             = "LIVE";
        bool              resetMessages      = false;
        bool              lockAcquired       = false;
        CancellationToken token              = arg1.cancellation.Token;
        try
        {
            await requestLock.WaitAsync(token).ConfigureAwait(false);
            lockAcquired = true;
            token.ThrowIfCancellationRequested();
            if (continuation == null || DateTime.UtcNow - lastStreamLookupAt > TimeSpan.FromSeconds(30.0))
            {
                using HttpResponseMessage arg3 = await HttpClient.GetAsync(channelUrl, token).ConfigureAwait(false);
                JObject                   val  = FindObject(await ReadResponseBody(arg3).ConfigureAwait(false), "ytInitialPlayerResponse");
                object                    obj;
                if (val == null)
                {
                    obj = null;
                }
                else
                {
                    JToken obj2 = val["videoDetails"];
                    obj = obj2 != null ? obj2["channelId"] : null;
                }

                string detectedChannelId = (string)(JToken)obj;
                if (Regex.IsMatch(detectedChannelId ?? "", "^UC[A-Za-z0-9_-]{22}$"))
                {
                    channelUrl = "https://www.youtube.com/channel/" + detectedChannelId + "/live";
                }

                object obj3;
                if (val == null)
                {
                    obj3 = null;
                }
                else
                {
                    JToken obj4 = val["microformat"];
                    if (obj4 == null)
                    {
                        obj3 = null;
                    }
                    else
                    {
                        JToken obj5 = obj4["playerMicroformatRenderer"];
                        obj3 = obj5 != null ? obj5["liveBroadcastDetails"] : null;
                    }
                }

                JToken val2 = (JToken)obj3;
                object obj6;
                if (val == null)
                {
                    obj6 = null;
                }
                else
                {
                    JToken obj7 = val["playabilityStatus"];
                    obj6 = obj7 != null ? obj7["status"] : null;
                }

                if (!((string)(JToken)obj6 == "OK") || (bool?)(val2 != null ? val2["isLiveNow"] : null) != true || (val2 != null ? val2["endTimestamp"] : null) != null)
                {
                    throw new RetryAfterException("NO PUBLIC LIVE STREAM: CHECKING AUTOMATICALLY");
                }

                object obj8;
                if (val == null)
                {
                    obj8 = null;
                }
                else
                {
                    JToken obj9 = val["videoDetails"];
                    obj8 = obj9 != null ? obj9["isUpcoming"] : null;
                }

                if ((bool?)(JToken)obj8 == true)
                {
                    throw new RetryAfterException("NO PUBLIC LIVE STREAM: CHECKING AUTOMATICALLY");
                }

                object obj10;
                if (val == null)
                {
                    obj10 = null;
                }
                else
                {
                    JToken obj11 = val["videoDetails"];
                    obj10 = obj11 != null ? obj11["videoId"] : null;
                }

                string detectedVideoId = (string)(JToken)obj10;
                if (!IsValidConfiguration(detectedVideoId))
                {
                    throw new RetryAfterException("NO PUBLIC LIVE STREAM FOUND");
                }

                if (detectedVideoId != videoId)
                {
                    resetMessages = true;
                    continuation  = null;
                }

                videoId = detectedVideoId;
                object obj12;
                if (val == null)
                {
                    obj12 = null;
                }
                else
                {
                    JToken obj13 = val["videoDetails"];
                    obj12 = obj13 != null ? obj13["title"] : null;
                }

                streamTitle        = (string)(JToken)obj12 ?? "";
                lastStreamLookupAt = DateTime.UtcNow;
            }

            if (continuation == null)
            {
                using HttpResponseMessage arg3 = await HttpClient.GetAsync("https://www.youtube.com/live_chat?v=" + videoId, token).ConfigureAwait(false);
                string                    arg4 = await ReadResponseBody(arg3).ConfigureAwait(false);
                apiKey        = ReadRegexGroup(arg4, "INNERTUBE_API_KEY");
                clientVersion = ReadRegexGroup(arg4, "INNERTUBE_CLIENT_VERSION");
                JObject obj14 = ParseObject(arg4);
                JToken  val3  = obj14 != null ? obj14.SelectToken("$..liveChatRenderer") : null;
                if ((bool?)(val3 != null ? val3["isReplay"] : null) == true)
                {
                    throw new RetryAfterException("STREAM ENDED: CHECKING AUTOMATICALLY");
                }

                continuation = ReadContinuationFromToken(val3, out pollDelayMs);
                if (continuation == null)
                {
                    throw new RetryAfterException("PUBLIC CHAT UNAVAILABLE OR OFF");
                }

                if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(clientVersion))
                {
                    throw new RetryAfterException("YOUTUBE CHAT PAGE CHANGED");
                }

                CollectChatActions(val3, chatActions);
            }

            token.ThrowIfCancellationRequested();
            JObject val4 = new()
            {
                    ["context"] = new JObject
                    {
                            ["client"] = new JObject
                            {
                                    ["clientName"]    = JToken.Parse("WEB"),
                                    ["clientVersion"] = JToken.Parse(clientVersion),
                            },
                    },
                    ["continuation"] = JToken.Parse(continuation),
            };

            using StringContent       content = new(val4.ToString(0), Encoding.UTF8, "application/json");
            using HttpResponseMessage arg5    = await HttpClient.PostAsync("https://www.youtube.com/youtubei/v1/live_chat/get_live_chat?key=" + Uri.EscapeDataString(apiKey) + "&prettyPrint=false", content, token).ConfigureAwait(false);
            JToken                    obj15   = JObject.Parse(await ReadResponseBody(arg5).ConfigureAwait(false))["continuationContents"];
            JToken                    val5    = obj15 != null ? obj15["liveChatContinuation"] : null;
            if (val5 == null || (bool?)val5["isReplay"] == true)
            {
                throw new RetryAfterException("STREAM ENDED OR CHAT OFF: CHECKING AUTOMATICALLY");
            }

            continuation = ReadContinuationFromToken(val5, out pollDelayMs);
            if (continuation == null)
            {
                throw new RetryAfterException("STREAM ENDED OR CHAT OFF: CHECKING AUTOMATICALLY");
            }

            CollectChatActions(val5, chatActions);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return;
        }
        catch (RetryAfterException ex2)
        {
            status        = ex2.Message;
            continuation  = null;
            pollDelayMs   = (int)Math.Min(2147483647L, ex2.delaySeconds * 1000L);
            resetMessages = true;
        }
        catch
        {
            status        = "CHAT NETWORK OR PAGE ERROR: RETRYING";
            continuation  = null;
            pollDelayMs   = 15000;
            resetMessages = true;
        }
        finally
        {
            if (lockAcquired)
            {
                requestLock.Release();
            }
        }

        MainThreadDispatch.Enqueue([MethodImpl(MethodImplOptions.NoInlining)]() =>
                                   {
                                       if (arg1 == session)
                                       {
                                           if (arg2 != arg1.generation)
                                           {
                                               arg1.pollInProgress = false;
                                               arg1.continuation   = null;
                                               arg1.nextPollAt     = status == "LIVE" ? DateTime.MinValue : DateTime.UtcNow.AddMilliseconds(pollDelayMs);
                                               if (status != "LIVE")
                                               {
                                                   Status = status;
                                               }
                                           }
                                           else
                                           {
                                               if (resetMessages)
                                               {
                                                   arg1.messages.Clear();
                                                   arg1.seenMessageIds.Clear();
                                                   arg1.messageIdOrder.Clear();
                                               }

                                               if (continuation == null)
                                               {
                                                   chatActions.Clear();
                                                   streamTitle        = "";
                                                   videoId            = null;
                                                   lastStreamLookupAt = DateTime.MinValue;
                                               }

                                               arg1.apiKey             = apiKey;
                                               arg1.clientVersion      = clientVersion;
                                               arg1.continuation       = continuation;
                                               arg1.videoId            = videoId;
                                               arg1.streamTitle        = streamTitle;
                                               arg1.channelUrl         = channelUrl;
                                               arg1.lastStreamLookupAt = lastStreamLookupAt;
                                               arg1.nextPollAt         = DateTime.UtcNow.AddMilliseconds(pollDelayMs);
                                               arg1.pollInProgress     = false;
                                               Status                  = status;
                                               StreamTitle             = streamTitle ?? "";
                                               foreach (JObject item in chatActions)
                                               {
                                                   ApplyPollResult(arg1, item);
                                               }

                                               Messages = arg1.messages.ToArray();
                                               MessageVersion++;
                                           }
                                       }
                                   });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CollectChatActions(JToken arg1, List<JObject> arg2)
    {
        JContainer val = (JContainer)(arg1 is JContainer ? arg1 : null);
        if (val == null)
        {
            return;
        }

        foreach (JProperty item in val.Descendants().OfType<JProperty>())
        {
            if (item.Name == "liveChatTextMessageRenderer" || item.Name == "liveChatPaidMessageRenderer")
            {
                JToken  value = item.Value;
                JObject val2  = (JObject)(value is JObject ? value : null);
                if (val2 != null)
                {
                    arg2.Add(val2);
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ApplyPollResult(ChatSession arg1, JObject arg2)
    {
        JToken obj           = arg2["message"];
        string text          = RenderMessageText((JObject)(obj is JObject ? obj : null));
        JToken obj2          = arg2["purchaseAmountText"];
        string clientVersion = RenderMessageText((JObject)(obj2 is JObject ? obj2 : null));
        if (clientVersion.Length > 0)
        {
            text = "[" + clientVersion + "] " + text;
        }

        if (text.Length == 0)
        {
            return;
        }

        JToken obj3         = arg2["authorName"];
        string continuation = RenderMessageText((JObject)(obj3 is JObject ? obj3 : null));
        string videoId      = (string)arg2["id"];
        if (string.IsNullOrEmpty(videoId))
        {
            videoId = (string)arg2["timestampUsec"] + "\t" + continuation + "\t" + text;
        }

        if (arg1.seenMessageIds.Add(videoId))
        {
            arg1.messageIdOrder.Enqueue(videoId);
            while (arg1.messageIdOrder.Count > 512)
            {
                arg1.seenMessageIds.Remove(arg1.messageIdOrder.Dequeue());
            }

            arg1.messages.Add((continuation.Length == 0 ? "?" : continuation.Replace('\t', ' ').Replace('\n', ' ')) + "\t" + text.Replace('\t', ' ').Replace('\n', ' '));
            while (arg1.messages.Count > 8)
            {
                arg1.messages.RemoveAt(0);
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string RenderMessageText(JObject arg1)
    {
        if (arg1 == null)
        {
            return "";
        }

        if (arg1["simpleText"] == null)
        {
            JToken obj = arg1["runs"];
            JArray val = (JArray)(obj is JArray ? obj : null);
            if (val == null)
            {
                return "";
            }

            StringBuilder stringBuilder = new();
            foreach (JToken item in val)
            {
                string text = (string)item["text"];
                if (text == null)
                {
                    JToken obj2 = item["emoji"];
                    JToken obj3 = obj2 != null ? obj2["shortcuts"] : null;
                    JArray val2 = (JArray)(obj3 is JArray ? obj3 : null);
                    if (val2 == null || val2.Count <= 0)
                    {
                        JToken obj4 = item["emoji"];
                        stringBuilder.Append((string)(obj4 != null ? obj4["emojiId"] : null) ?? "");
                    }
                    else
                    {
                        stringBuilder.Append((string)val2[0]);
                    }
                }
                else
                {
                    stringBuilder.Append(text);
                }
            }

            return stringBuilder.ToString();
        }

        return (string)arg1["simpleText"];
    }

    private sealed class ChatSession
    {

        public readonly CancellationTokenSource cancellation = new();

        public readonly Queue<string> messageIdOrder = new();

        public readonly List<string> messages = new();

        public readonly HashSet<string> seenMessageIds = new();
        public readonly string          sourceUrl;

        public string apiKey;

        public string channelUrl;

        public string clientVersion;

        public string continuation;

        public int generation;

        public DateTime lastPollAt;

        public DateTime lastStreamLookupAt;

        public DateTime nextPollAt;

        public bool pollInProgress;

        public string streamTitle;

        public string videoId;

        public ChatSession(string arg1)
        {
            sourceUrl  = arg1;
            channelUrl = arg1;
        }
    }

    private sealed class RetryAfterException : Exception
    {
        public readonly int delaySeconds;

        public RetryAfterException(string arg1, int arg2 = 20)
                : base(arg1) =>
                delaySeconds = arg2;
    }
}