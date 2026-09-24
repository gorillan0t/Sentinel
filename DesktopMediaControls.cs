using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Sentinel;
using UnityEngine;

internal static class DesktopMediaControls
{
    private static string cachedTrackTitle = "...";

    private static float nextTitleRefresh;

    private static bool refreshPending;

    public static string StatusMessage = "";

    private static bool IsSupportedPlatform
    {
        get
        {
            if ((int)Application.platform != 2)
            {
                return (int)Application.platform == 7;
            }

            return true;
        }
    }

    public static string TrackTitle
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        get
        {
            if (!refreshPending && Time.time >= nextTitleRefresh)
            {
                nextTitleRefresh = Time.time + 2f;
                if (!IsSupportedPlatform)
                {
                    return cachedTrackTitle = "PC ONLY";
                }

                refreshPending = true;
                Task.Run((Action)RefreshTrackTitle);

                return cachedTrackTitle;
            }

            return cachedTrackTitle;
        }
    }

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte arg1, byte arg2, uint arg3, UIntPtr arg4);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void SendMediaKey(byte arg1)
    {
        if (!IsSupportedPlatform)
        {
            StatusMessage = "DESKTOP MUSIC REQUIRES WINDOWS";

            return;
        }

        try
        {
            keybd_event(arg1, 0, 0u, UIntPtr.Zero);
            keybd_event(arg1, 0, 2u, UIntPtr.Zero);
            StatusMessage    = "";
            nextTitleRefresh = 0f;
        }
        catch
        {
            StatusMessage = "DESKTOP MEDIA KEY FAILED";
        }
    }

    public static void NextTrack() => SendMediaKey(176);

    public static void PreviousTrack() => SendMediaKey(177);

    public static void TogglePlayPause() => SendMediaKey(179);

    public static void VolumeUp() => SendMediaKey(175);

    public static void VolumeDown() => SendMediaKey(174);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RefreshTrackTitle()
    {
        Process[] array = null;
        string    text2;
        try
        {
            array = Process.GetProcessesByName("Spotify");
            string    text   = null;
            Process[] array2 = array;
            for (int i = 0; i < array2.Length; i++)
            {
                string mainWindowTitle = array2[i].MainWindowTitle;
                if (!string.IsNullOrEmpty(mainWindowTitle))
                {
                    if (mainWindowTitle.IndexOf('-') > 0)
                    {
                        text = mainWindowTitle;

                        break;
                    }

                    text = "PAUSED";
                }
            }

            text2 = text ?? "NOT RUNNING";
            if (text2.Length > 42)
            {
                text2 = text2.Substring(0, 40) + "..";
            }
        }
        catch
        {
            text2 = "TRACK UNAVAILABLE";
        }
        finally
        {
            if (array != null)
            {
                Process[] array2 = array;
                for (int i = 0; i < array2.Length; i++)
                {
                    array2[i].Dispose();
                }
            }
        }

        MainThreadDispatch.Enqueue(delegate
                                   {
                                       cachedTrackTitle = text2;
                                       refreshPending   = false;
                                   });
    }
}