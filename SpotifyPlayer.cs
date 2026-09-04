using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Sentinel.Sentinel;
using UnityEngine;

namespace Sentinel;

internal static class SpotifyPlayer
{

    private const string string_0 = "";

    private const string string_1 = "https://api.spotify.com/v1/me/player";

    public static readonly OAuthSession OAuth = new("spotify.json", "https://accounts.spotify.com/authorize", "https://accounts.spotify.com/api/token", "user-read-playback-state user-modify-playback-state", 27381, "/callback", "");

    public static bool IsPlaying;

    public static bool ShuffleEnabled;

    public static bool PremiumRequired;

    public static string TrackName = "";

    public static string ArtistName = "";

    public static string RepeatMode = "off";

    public static int VolumePercent = 50;

    public static int ProgressMs;

    public static int DurationMs;

    public static Texture2D AlbumArt;

    public static float LastProgressSampleTime;

    private static string albumArtUrl;

    private static float nextPollTime;

    private static bool pollInProgress;

    public static void Initialize()
    {
        string text = Cfg.SpotifyId.Value.Trim();
        OAuth.ClientId = text.Length > 0 ? text : "";
        OAuth.LoadSession();
    }

    public static void Disconnect()
    {
        OAuth.ClearSession();
        TrackName       = ArtistName = "";
        DurationMs      = 0;
        PremiumRequired = false;
    }

    public static void PollPlayback()
    {
        if (!OAuth.IsConnected || pollInProgress || !(Time.time >= nextPollTime))
        {
            return;
        }

        pollInProgress = true;
        nextPollTime   = Time.time + 2f;
        float sampleTime = Time.time;
        Task.Run(delegate
                 {
                     try
                     {
                         if (!OAuth.EnsureAccessToken())
                         {
                             return;
                         }

                         using HttpResponseMessage httpResponseMessage = OAuthSession.HttpClient.SendAsync(OAuth.CreateAuthorizedRequest(HttpMethod.Get, "https://api.spotify.com/v1/me/player")).Result;
                         if (httpResponseMessage.StatusCode == HttpStatusCode.NoContent)
                         {
                             TrackName  = "NOTHING PLAYING";
                             ArtistName = "";
                             IsPlaying  = false;
                             DurationMs = 0;
                         }
                         else
                         {
                             if (httpResponseMessage.StatusCode == HttpStatusCode.Unauthorized)
                             {
                                 OAuth.ExpireAccessToken();
                             }

                             if (httpResponseMessage.IsSuccessStatusCode)
                             {
                                 JObject obj = JObject.Parse(httpResponseMessage.Content.ReadAsStringAsync().Result);
                                 IsPlaying              = (bool?)obj["is_playing"]    == true;
                                 ShuffleEnabled         = (bool?)obj["shuffle_state"] == true;
                                 RepeatMode             = (string)obj["repeat_state"] ?? "off";
                                 ProgressMs             = ((int?)obj["progress_ms"]).GetValueOrDefault();
                                 LastProgressSampleTime = sampleTime;
                                 JToken  obj2 = obj["device"];
                                 JObject val  = (JObject)(obj2 is JObject ? obj2 : null);
                                 if (val != null)
                                 {
                                     VolumePercent = (int?)val["volume_percent"] ?? VolumePercent;
                                 }

                                 JToken  obj3 = obj["item"];
                                 JObject val2 = (JObject)(obj3 is JObject ? obj3 : null);
                                 if (val2 != null)
                                 {
                                     TrackName  = (string)val2["name"] ?? "";
                                     DurationMs = ((int?)val2["duration_ms"]).GetValueOrDefault();
                                     JToken obj4 = val2["artists"];
                                     JArray val3 = (JArray)(obj4 is JArray ? obj4 : null);
                                     ArtistName = val3 != null && val3.Count > 0 ? (string)val3[0]["name"] ?? "" : "";
                                     JToken  obj5 = val2["album"];
                                     JObject val4 = (JObject)(obj5 is JObject ? obj5 : null);
                                     object  obj6;
                                     if (val4 == null)
                                     {
                                         obj6 = null;
                                     }
                                     else
                                     {
                                         JToken obj7 = val4["images"];
                                         obj6 = obj7 is JArray ? obj7 : null;
                                     }

                                     JArray val5 = (JArray)obj6;
                                     string url  = val5 == null || val5.Count <= 0 ? null : (string)val5[val5.Count > 1 ? 1 : 0]["url"];
                                     if (!(url == albumArtUrl))
                                     {
                                         albumArtUrl = url;
                                         if (url != null)
                                         {
                                             byte[] imageBytes = OAuthSession.HttpClient.GetByteArrayAsync(url).Result;
                                             MainThreadDispatch.Enqueue(delegate
                                                                        {
                                                                            if (!(url != albumArtUrl))
                                                                            {
                                                                                Texture2D val6 = new(2, 2, (TextureFormat)4, false);
                                                                                if (val6.LoadImage(imageBytes))
                                                                                {
                                                                                    val6.filterMode = (FilterMode)1;
                                                                                    if (AlbumArt != null)
                                                                                    {
                                                                                        Object.Destroy(AlbumArt);
                                                                                    }

                                                                                    AlbumArt = val6;
                                                                                }
                                                                                else
                                                                                {
                                                                                    Object.Destroy(val6);
                                                                                }
                                                                            }
                                                                        });
                                         }
                                     }
                                 }
                             }
                         }
                     }
                     catch { }
                     finally
                     {
                         pollInProgress = false;
                     }
                 });
    }

    private static void SendPlayerCommand(HttpMethod method, string command)
    {
        if (!OAuth.IsConnected)
        {
            return;
        }

        Task.Run(delegate
                 {
                     try
                     {
                         if (!OAuth.EnsureAccessToken())
                         {
                             return;
                         }

                         using HttpResponseMessage httpResponseMessage = OAuthSession.HttpClient.SendAsync(OAuth.CreateAuthorizedRequest(method, "https://api.spotify.com/v1/me/player/" + command)).Result;
                         switch ((int)httpResponseMessage.StatusCode)
                         {
                             case 403:
                                 PremiumRequired = httpResponseMessage.Content.ReadAsStringAsync().Result.Contains("PREMIUM");

                                 break;

                             case 404:
                                 TrackName = "OPEN SPOTIFY FIRST";

                                 break;

                             default:
                                 if (httpResponseMessage.IsSuccessStatusCode)
                                 {
                                     PremiumRequired = false;
                                 }

                                 break;

                             case 401:
                                 OAuth.ExpireAccessToken();

                                 break;
                         }

                         nextPollTime = 0f;
                     }
                     catch { }
                 });
    }

    public static void TogglePlayback()
    {
        SendPlayerCommand(HttpMethod.Put, IsPlaying ? "pause" : "play");
        IsPlaying = !IsPlaying;
    }

    public static void NextTrack() => SendPlayerCommand(HttpMethod.Post, "next");

    public static void PreviousTrack() => SendPlayerCommand(HttpMethod.Post, "previous");

    public static void SetVolume(int int_3)
    {
        VolumePercent = Mathf.Clamp(int_3, 0, 100);
        SendPlayerCommand(HttpMethod.Put, "volume?volume_percent=" + VolumePercent);
    }

    public static void ToggleShuffle()
    {
        ShuffleEnabled = !ShuffleEnabled;
        SendPlayerCommand(HttpMethod.Put, "shuffle?state=" + (ShuffleEnabled ? "true" : "false"));
    }

    public static void CycleRepeatMode()
    {
        RepeatMode = RepeatMode == "off" ? "context" : RepeatMode == "context" ? "track" : "off";
        SendPlayerCommand(HttpMethod.Put, "repeat?state=" + RepeatMode);
    }

    private sealed class PlaybackPollJob
    {
        public float sampleTime;

        internal void PollPlaybackWorker()
        {
            try
            {
                if (!OAuth.EnsureAccessToken())
                {
                    return;
                }

                using HttpResponseMessage httpResponseMessage = OAuthSession.HttpClient.SendAsync(OAuth.CreateAuthorizedRequest(HttpMethod.Get, "https://api.spotify.com/v1/me/player")).Result;
                if (httpResponseMessage.StatusCode == HttpStatusCode.NoContent)
                {
                    TrackName  = "NOTHING PLAYING";
                    ArtistName = "";
                    IsPlaying  = false;
                    DurationMs = 0;

                    return;
                }

                if (httpResponseMessage.StatusCode == HttpStatusCode.Unauthorized)
                {
                    OAuth.ExpireAccessToken();
                }

                if (!httpResponseMessage.IsSuccessStatusCode)
                {
                    return;
                }

                JObject obj = JObject.Parse(httpResponseMessage.Content.ReadAsStringAsync().Result);
                IsPlaying              = (bool?)obj["is_playing"]    == true;
                ShuffleEnabled         = (bool?)obj["shuffle_state"] == true;
                RepeatMode             = (string)obj["repeat_state"] ?? "off";
                ProgressMs             = ((int?)obj["progress_ms"]).GetValueOrDefault();
                LastProgressSampleTime = sampleTime;
                JToken  obj2 = obj["device"];
                JObject val  = (JObject)(obj2 is JObject ? obj2 : null);
                if (val != null)
                {
                    VolumePercent = (int?)val["volume_percent"] ?? VolumePercent;
                }

                JToken  obj3 = obj["item"];
                JObject val2 = (JObject)(obj3 is JObject ? obj3 : null);
                if (val2 == null)
                {
                    return;
                }

                TrackName  = (string)val2["name"] ?? "";
                DurationMs = ((int?)val2["duration_ms"]).GetValueOrDefault();
                JToken obj4 = val2["artists"];
                JArray val3 = (JArray)(obj4 is JArray ? obj4 : null);
                ArtistName = val3 != null && val3.Count > 0 ? (string)val3[0]["name"] ?? "" : "";
                JToken  obj5 = val2["album"];
                JObject val4 = (JObject)(obj5 is JObject ? obj5 : null);
                object  obj6;
                if (val4 == null)
                {
                    obj6 = null;
                }
                else
                {
                    JToken obj7 = val4["images"];
                    obj6 = obj7 is JArray ? obj7 : null;
                }

                JArray val5 = (JArray)obj6;
                string url  = val5 == null || val5.Count <= 0 ? null : (string)val5[val5.Count > 1 ? 1 : 0]["url"];
                if (url == albumArtUrl)
                {
                    return;
                }

                albumArtUrl = url;
                if (url == null)
                {
                    return;
                }

                byte[] imageBytes = OAuthSession.HttpClient.GetByteArrayAsync(url).Result;
                MainThreadDispatch.Enqueue(delegate
                                           {
                                               if (!(url != albumArtUrl))
                                               {
                                                   Texture2D val6 = new(2, 2, (TextureFormat)4, false);
                                                   if (val6.LoadImage(imageBytes))
                                                   {
                                                       val6.filterMode = (FilterMode)1;
                                                       if (AlbumArt != null)
                                                       {
                                                           Object.Destroy(AlbumArt);
                                                       }

                                                       AlbumArt = val6;
                                                   }
                                                   else
                                                   {
                                                       Object.Destroy(val6);
                                                   }
                                               }
                                           });
            }
            catch { }
            finally
            {
                pollInProgress = false;
            }
        }
    }

    private sealed class AlbumArtUpdate
    {

        public byte[] imageBytes;
        public string url;

        internal void ApplyAlbumArt()
        {
            if (url != albumArtUrl)
            {
                return;
            }

            Texture2D val = new(2, 2, (TextureFormat)4, false);
            if (val.LoadImage(imageBytes))
            {
                val.filterMode = (FilterMode)1;
                if (AlbumArt != null)
                {
                    Object.Destroy(AlbumArt);
                }

                AlbumArt = val;
            }
            else
            {
                Object.Destroy(val);
            }
        }
    }

    private sealed class PlayerCommandJob
    {

        public string     command;
        public HttpMethod method;

        internal void SendCommandWorker()
        {
            try
            {
                if (!OAuth.EnsureAccessToken())
                {
                    return;
                }

                using HttpResponseMessage httpResponseMessage = OAuthSession.HttpClient.SendAsync(OAuth.CreateAuthorizedRequest(method, "https://api.spotify.com/v1/me/player/" + command)).Result;
                switch ((int)httpResponseMessage.StatusCode)
                {
                    case 403:
                        PremiumRequired = httpResponseMessage.Content.ReadAsStringAsync().Result.Contains("PREMIUM");

                        break;

                    case 404:
                        TrackName = "OPEN SPOTIFY FIRST";

                        break;

                    default:
                        if (httpResponseMessage.IsSuccessStatusCode)
                        {
                            PremiumRequired = false;
                        }

                        break;

                    case 401:
                        OAuth.ExpireAccessToken();

                        break;
                }

                nextPollTime = 0f;
            }
            catch { }
        }
    }
}