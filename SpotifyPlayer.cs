using System;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sentinel;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class SpotifyPlayer
{

    private const string PlayerEndpoint = "https://api.spotify.com/v1/me/player";

    public static readonly OAuthSession OAuth = new("spotify.json", "https://accounts.spotify.com/authorize", "https://accounts.spotify.com/api/token", "user-read-playback-state user-modify-playback-state", 27381, "/callback", "");

    public static bool IsPlaying;

    public static bool ShuffleEnabled;

    public static bool PremiumRequired;

    public static string TrackName = "";

    public static string ArtistName = "";

    public static string RepeatMode = "off";

    public static string StatusMessage = "";

    public static int VolumePercent = 50;

    public static int ProgressMs;

    public static int DurationMs;

    public static Texture2D AlbumArt;

    public static float LastProgressSampleTime;

    private static string albumArtUrl;

    private static AlbumArtRequestKey pendingAlbumArtRequest;

    private static DateTime nextPollAt;

    private static DateTime lastCommandAt;

    private static bool pollInProgress;

    private static int requestGeneration;

    private static readonly SemaphoreSlim requestLock = new(1, 1);

    public static void Initialize()
    {
        string text = (Cfg.SpotifyId.Value ?? "").Trim();
        if (OAuth.ClientId != text)
        {
            OAuth.ClientId = text;
            ResetState();
        }

        OAuth.LoadSession();
    }

    public static void Disconnect()
    {
        OAuth.ClearSession();
        ResetState();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ResetState()
    {
        TrackName              = ArtistName = StatusMessage = "";
        RepeatMode             = "off";
        PremiumRequired        = false;
        ShuffleEnabled         = false;
        IsPlaying              = false;
        VolumePercent          = 50;
        DurationMs             = 0;
        ProgressMs             = 0;
        nextPollAt             = lastCommandAt = DateTime.MinValue;
        albumArtUrl            = null;
        pendingAlbumArtRequest = null;
        if ((Object)(object)AlbumArt != (Object)null)
        {
            Object.Destroy((Object)(object)AlbumArt);
        }

        AlbumArt = null;
    }

    private static Task RunDelayed(int arg1, Action arg2)
    {
        TaskCompletionSource<bool> taskCompletionSource2 = new(TaskCreationOptions.RunContinuationsAsynchronously);
        MainThreadDispatch.Enqueue(delegate
                                   {
                                       try
                                       {
                                           if (arg1 == OAuth.Generation)
                                           {
                                               arg2();
                                           }

                                           taskCompletionSource2.TrySetResult(true);
                                       }
                                       catch (Exception exception)
                                       {
                                           taskCompletionSource2.TrySetException(exception);
                                       }
                                   });

        return taskCompletionSource2.Task;
    }

    public static void PollPlayback()
    {
        if (OAuth.IsConnected && !pollInProgress && !(DateTime.UtcNow < nextPollAt) && !(DateTime.UtcNow < lastCommandAt))
        {
            pollInProgress = true;
            nextPollAt     = DateTime.UtcNow.AddSeconds(2.0);
            SendPlayerCommand(null, true, OAuth.Generation);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void QueueCommand(Func<PlayerCommand> arg1)
    {
        if (!OAuth.IsConnected)
        {
            StatusMessage = "CONNECT SPOTIFY OR USE DESKTOP MUSIC";
        }
        else if (!(DateTime.UtcNow < lastCommandAt))
        {
            if (Interlocked.Increment(ref requestGeneration) > 16)
            {
                Interlocked.Decrement(ref requestGeneration);
                StatusMessage = "WAIT FOR CURRENT COMMANDS";
            }
            else
            {
                SendPlayerCommand(arg1, false, OAuth.Generation);
            }
        }
        else
        {
            StatusMessage = "RATE LIMITED: WAIT BEFORE RETRYING";
        }
    }

    private async static Task SendPlayerCommand(Func<PlayerCommand> arg1, bool arg2, int arg3)
    {
        AlbumArtRequestKey albumArtRequest = null;
        await requestLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (arg3 != OAuth.Generation)
            {
                return;
            }

            PlayerCommand command    = null;
            bool          authorized = false;
            await RunDelayed(arg3, delegate
                                   {
                                       authorized = OAuth.IsConnected && DateTime.UtcNow >= lastCommandAt;
                                       if (authorized && !arg2)
                                       {
                                           command = arg1();
                                       }
                                   }).ConfigureAwait(false);

            if (!authorized || arg3 != OAuth.Generation || !await OAuth.EnsureAccessToken().ConfigureAwait(false))
            {
                return;
            }

            using HttpRequestMessage request  = OAuth.CreateAuthorizedRequest(arg2 ? HttpMethod.Get : command.method, "https://api.spotify.com/v1/me/player" + (arg2 ? "" : "/" + command.path), arg3);
            HttpResponseMessage      response = await OAuthSession.HttpClient.SendAsync(request).ConfigureAwait(false);
            try
            {
                if (response.IsSuccessStatusCode)
                {
                    if (arg2)
                    {
                        if (response.StatusCode == HttpStatusCode.NoContent)
                        {
                            await RunDelayed(arg3, [MethodImpl(MethodImplOptions.NoInlining)]() =>
                                                   {
                                                       ResetPlayback();
                                                       TrackName       = "NOTHING PLAYING";
                                                       StatusMessage   = "";
                                                       PremiumRequired = false;
                                                   }).ConfigureAwait(false);

                            return;
                        }

                        PlaybackState playbackState = ParsePlaybackState(JObject.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false)));
                        string        albumArtUrl   = playbackState.albumArtUrl;
                        await RunDelayed(arg3, delegate
                                               {
                                                   ApplyPlaybackState(playbackState);
                                                   StatusMessage   = "";
                                                   PremiumRequired = false;
                                                   if (albumArtUrl != albumArtUrl)
                                                   {
                                                       albumArtUrl            = albumArtUrl;
                                                       pendingAlbumArtRequest = null;
                                                       if ((Object)(object)AlbumArt != (Object)null)
                                                       {
                                                           Object.Destroy((Object)(object)AlbumArt);
                                                       }

                                                       AlbumArt = null;
                                                   }

                                                   if (albumArtUrl != null && !((Object)(object)AlbumArt != (Object)null) && (pendingAlbumArtRequest == null || pendingAlbumArtRequest.generation != arg3))
                                                   {
                                                       pendingAlbumArtRequest = albumArtRequest = new AlbumArtRequestKey(albumArtUrl, arg3);
                                                   }
                                               }).ConfigureAwait(false);

                        return;
                    }

                    await RunDelayed(arg3, delegate
                                           {
                                               command.onSuccess?.Invoke();
                                               PremiumRequired        = false;
                                               StatusMessage          = "";
                                               LastProgressSampleTime = Time.time;
                                               nextPollAt             = DateTime.MinValue;
                                           }).ConfigureAwait(false);

                    return;
                }

                string text9 = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                await RunDelayed(arg3, delegate
                                       {
                                           ApplyApiError(response, text9);
                                       }).ConfigureAwait(false);
            }
            finally
            {
                if (response != null)
                {
                    ((IDisposable)response).Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            bool flag5 = ex is JsonException || ex is FormatException || ex is InvalidCastException;
            await RunDelayed(arg3, [MethodImpl(MethodImplOptions.NoInlining)]() =>
                                   {
                                       StatusMessage = flag5 ? "SPOTIFY RESPONSE ERROR: TRY AGAIN" : "SPOTIFY NETWORK ERROR: TRY AGAIN";
                                       nextPollAt    = DateTime.UtcNow.AddSeconds(10.0);
                                   }).ConfigureAwait(false);
        }
        finally
        {
            if (arg2)
            {
                MainThreadDispatch.Enqueue(delegate
                                           {
                                               pollInProgress = false;
                                           });
            }
            else
            {
                Interlocked.Decrement(ref requestGeneration);
            }

            requestLock.Release();
            if (albumArtRequest != null)
            {
                UpdateAlbumArt(albumArtRequest);
            }
        }
    }

    private async static Task UpdateAlbumArt(AlbumArtRequestKey arg1)
    {
        try
        {
            byte[] array = await OAuthSession.HttpClient.GetByteArrayAsync(arg1.value).ConfigureAwait(false);
            await RunDelayed(arg1.generation, delegate
                                              {
                                                  if (arg1 == pendingAlbumArtRequest && !(arg1.value != albumArtUrl))
                                                  {
                                                      Texture2D val = new(2, 2, (TextureFormat)4, false);
                                                      if (val.LoadImage(array))
                                                      {
                                                          val.filterMode = (FilterMode)1;
                                                          if ((Object)(object)AlbumArt != (Object)null)
                                                          {
                                                              Object.Destroy((Object)(object)AlbumArt);
                                                          }

                                                          AlbumArt = val;
                                                      }
                                                      else
                                                      {
                                                          Object.Destroy((Object)(object)val);
                                                      }
                                                  }
                                              }).ConfigureAwait(false);
        }
        catch { }
        finally
        {
            await RunDelayed(arg1.generation, delegate
                                              {
                                                  if (arg1 == pendingAlbumArtRequest)
                                                  {
                                                      pendingAlbumArtRequest = null;
                                                  }
                                              }).ConfigureAwait(false);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ApplyApiError(HttpResponseMessage arg1, string arg2)
    {
        int statusCode = (int)arg1.StatusCode;
        switch (statusCode)
        {
            case 401:
                OAuth.ExpireAccessToken();
                StatusMessage = "LOGIN EXPIRED: RETRY OR LOG IN AGAIN";

                break;

            case 403:
                PremiumRequired = arg2.IndexOf("PREMIUM", StringComparison.OrdinalIgnoreCase) >= 0;
                StatusMessage   = PremiumRequired ? "SPOTIFY PREMIUM NEEDED" : "SPOTIFY ACCESS DENIED: CHECK APP ACCESS";

                break;

            case 429:
                lastCommandAt = DateTime.UtcNow.AddSeconds(OAuthSession.GetResponseStatusCode(arg1));
                StatusMessage = "RATE LIMITED: WAIT BEFORE RETRYING";

                break;

            default:
                StatusMessage = "SPOTIFY REQUEST FAILED (" + statusCode + "): TRY AGAIN";

                break;

            case 404:
                StatusMessage = "OPEN SPOTIFY AND START A TRACK";

                break;
        }

        nextPollAt = DateTime.UtcNow.AddSeconds(5.0);
    }

    private static void ResetPlayback()
    {
        TrackName              = ArtistName = "";
        IsPlaying              = false;
        ProgressMs             = 0;
        DurationMs             = 0;
        LastProgressSampleTime = Time.time;
        albumArtUrl            = null;
        pendingAlbumArtRequest = null;
        if ((Object)(object)AlbumArt != (Object)null)
        {
            Object.Destroy((Object)(object)AlbumArt);
        }

        AlbumArt = null;
    }

    private static JToken UnwrapPlaybackItem(JToken arg1)
    {
        JArray val = (JArray)(arg1 is JArray ? arg1 : null);
        if (val != null && val.Count > 0)
        {
            return val[0];
        }

        return null;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static PlaybackState ParsePlaybackState(JObject arg1)
    {
        JToken  obj = arg1["item"];
        JObject val = (JObject)(obj is JObject ? obj : null);
        PlaybackState obj2 = new()
        {
                isPlaying      = (bool?)arg1["is_playing"]    == true,
                shuffleEnabled = (bool?)arg1["shuffle_state"] == true,
                repeatMode     = (string)arg1["repeat_state"] ?? "off",
                progressMs     = ((int?)arg1["progress_ms"]).GetValueOrDefault(),
        };

        JToken obj3 = arg1["device"];
        obj2.volumePercent = (int?)(obj3 != null ? obj3["volume_percent"] : null);
        obj2.trackName     = val == null ? "NOTHING PLAYING" : (string)val["name"] ?? "";
        obj2.durationMs    = ((int?)val?["duration_ms"]).GetValueOrDefault();
        JToken obj4 = UnwrapPlaybackItem(val?["artists"]);
        object obj5 = (string)(obj4 != null ? obj4["name"] : null);
        if (obj5 == null)
        {
            object obj6;
            if (val == null)
            {
                obj6 = null;
            }
            else
            {
                JToken obj7 = val["show"];
                obj6 = obj7 != null ? obj7["name"] : null;
            }

            obj5 = (string)(JToken)obj6 ?? "";
        }

        obj2.artistName = (string)obj5;
        JToken images = val?["album"]?["images"] ?? val?["images"];
        JToken obj10  = UnwrapPlaybackItem(images);
        obj2.albumArtUrl = (string)(obj10 != null ? obj10["url"] : null);
        PlaybackState playbackState = obj2;
        if (val == null)
        {
            playbackState.isPlaying  = false;
            playbackState.progressMs = 0;
        }

        return playbackState;
    }

    private static void ApplyPlaybackState(PlaybackState arg1)
    {
        IsPlaying              = arg1.isPlaying;
        ShuffleEnabled         = arg1.shuffleEnabled;
        RepeatMode             = arg1.repeatMode;
        ProgressMs             = arg1.progressMs;
        DurationMs             = arg1.durationMs;
        LastProgressSampleTime = Time.time;
        VolumePercent          = arg1.volumePercent ?? VolumePercent;
        TrackName              = arg1.trackName;
        ArtistName             = arg1.artistName;
    }

    public static void TogglePlayback() =>
            QueueCommand([MethodImpl(MethodImplOptions.NoInlining)]() =>
                         {
                             ShuffleResult shuffleResult = new();
                             shuffleResult.result = !IsPlaying;

                             return new PlayerCommand(HttpMethod.Put, shuffleResult.result ? "play" : "pause", shuffleResult.Invoke0);
                         });

    public static void NextTrack() => QueueCommand([MethodImpl(MethodImplOptions.NoInlining)]() => new PlayerCommand(HttpMethod.Post, "next"));

    public static void PreviousTrack() => QueueCommand([MethodImpl(MethodImplOptions.NoInlining)]() => new PlayerCommand(HttpMethod.Post, "previous"));

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static PlayerCommand BuildVolumeCommand(int arg1)
    {
        int volumePercent = Mathf.Clamp(arg1, 0, 100);

        return new PlayerCommand(HttpMethod.Put, "volume?volume_percent=" + volumePercent, delegate
                                                                                           {
                                                                                               VolumePercent = volumePercent;
                                                                                           });
    }

    public static void SetVolume(int arg1) => QueueCommand(() => BuildVolumeCommand(arg1));

    public static void AdjustVolume(int arg1) => QueueCommand(() => BuildVolumeCommand(VolumePercent + arg1));

    public static void ToggleShuffle() =>
            QueueCommand([MethodImpl(MethodImplOptions.NoInlining)]() =>
                         {
                             ToggleResult toggleResult = new();
                             toggleResult.result = !ShuffleEnabled;

                             return new PlayerCommand(HttpMethod.Put, "shuffle?state=" + (toggleResult.result ? "true" : "false"), toggleResult.Invoke0);
                         });

    public static void CycleRepeatMode() =>
            QueueCommand([MethodImpl(MethodImplOptions.NoInlining)]() =>
                         {
                             RepeatModeResult repeatModeResult = new();
                             repeatModeResult.value = RepeatMode == "off" ? "context" : RepeatMode == "context" ? "track" : "off";

                             return new PlayerCommand(HttpMethod.Put, "repeat?state=" + repeatModeResult.value, repeatModeResult.Invoke0);
                         });

    private sealed class AlbumArtRequestKey
    {

        internal readonly int    generation;
        internal readonly string value;

        internal AlbumArtRequestKey(string arg1, int arg2)
        {
            value      = arg1;
            generation = arg2;
        }
    }

    private sealed class PlayerCommand
    {
        public readonly HttpMethod method;

        public readonly Action onSuccess;

        public readonly string path;

        public PlayerCommand(HttpMethod arg1, string arg2, Action arg3 = null)
        {
            method    = arg1;
            path      = arg2;
            onSuccess = arg3;
        }
    }

    private sealed class PlaybackState
    {

        internal string albumArtUrl;

        internal string artistName;

        internal int  durationMs;
        internal bool isPlaying;

        internal int progressMs;

        internal string repeatMode;

        internal bool shuffleEnabled;

        internal string trackName;

        internal int? volumePercent;
    }

    [CompilerGenerated]
    private sealed class ShuffleResult
    {
        public bool result;

        internal void Invoke0() => IsPlaying = result;
    }

    [CompilerGenerated]
    private sealed class ToggleResult
    {
        public bool result;

        internal void Invoke0() => ShuffleEnabled = result;
    }

    [CompilerGenerated]
    private sealed class RepeatModeResult
    {
        public string value;

        internal void Invoke0() => RepeatMode = value;
    }
}