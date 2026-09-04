using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Sentinel;

internal static class DesktopMediaControls
{
    private static string cachedTrackTitle = "...";

    private static float nextTitleRefresh;

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
        get
        {
            if (Time.time < nextTitleRefresh)
            {
                return cachedTrackTitle;
            }

            nextTitleRefresh = Time.time + 2f;
            if (IsSupportedPlatform)
            {
                Process[] array = null;
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

                    cachedTrackTitle = text ?? "NOT RUNNING";
                    if (cachedTrackTitle.Length > 42)
                    {
                        cachedTrackTitle = cachedTrackTitle.Substring(0, 40) + "..";
                    }
                }
                catch
                {
                    cachedTrackTitle = "NOT RUNNING";
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

                return cachedTrackTitle;
            }

            return cachedTrackTitle = "PC ONLY";
        }
    }

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte byte_0, byte byte_1, uint uint_0, UIntPtr uintptr_0);

    private static void SendMediaKey(byte byte_0)
    {
        if (!IsSupportedPlatform)
        {
            return;
        }

        try
        {
            keybd_event(byte_0, 0, 0u, UIntPtr.Zero);
            keybd_event(byte_0, 0, 2u, UIntPtr.Zero);
        }
        catch { }
    }

    public static void NextTrack() => SendMediaKey(176);

    public static void PreviousTrack() => SendMediaKey(177);

    public static void TogglePlayPause() => SendMediaKey(179);

    public static void Play() => SendMediaKey(175);

    public static void Pause() => SendMediaKey(174);
}