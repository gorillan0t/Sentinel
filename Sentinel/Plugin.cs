using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace Sentinel;

[BepInPlugin(Constants.Guid, Constants.Name, Constants.Version)]
public class Plugin : BaseUnityPlugin
{
    public static Plugin Ins;

    public Disc Disc;

    private float nextFriendRefreshAt;

    private void Awake()
    {
        Ins = this;

        Cfg.Load(Config);
        Theme.Apply(Cfg.Theme.Value);

        gameObject.AddComponent<MainThreadDispatch>();

        new Harmony(Constants.Guid).PatchAll();

        StartCoroutine(HamburburData.RefreshLoop());

        if (Manifest.Has("custom_theme"))
        {
            float hue = PlayerPrefs.GetFloat("zx_theme_h", -1f);

            if (hue >= 0f)
            {
                float saturation = PlayerPrefs.GetFloat("zx_theme_s", 0.8f);
                float brightness = PlayerPrefs.GetFloat("zx_theme_b", 0.9f);

                Theme.ApplyCustom(Color.HSVToRGB(hue, saturation, brightness));
            }
        }

        if (Manifest.Has("spotify"))
        {
            SpotifyPlayer.Initialize();
        }

        if (Manifest.Has("youtube_chat"))
        {
            YouTubeLiveChat.Initialize();
        }

        if (Manifest.Has("disc"))
        {
            Disc = gameObject.AddComponent<Disc>();
        }

        if (Manifest.Has("ring_menu"))
        {
            gameObject.AddComponent<RingMenu>();
            gameObject.AddComponent<PlayerRay>();
        }

        if (Manifest.Has("pc_ui"))
        {
            gameObject.AddComponent<PcMenu>();
        }

        if (Manifest.Has("frame"))
        {
            gameObject.AddComponent<Frame>();
        }

        if (Manifest.Has("tags"))
        {
            gameObject.AddComponent<Tags>();
        }

        if (Manifest.Has("notify"))
        {
            gameObject.AddComponent<Notify>();
        }

        if (Manifest.Has("watcher"))
        {
            gameObject.AddComponent<Watcher>();
        }

        if (Manifest.Has("net"))
        {
            gameObject.AddComponent<Net>();
        }

        if (Manifest.Has("friends"))
        {
            gameObject.AddComponent<Friends>();
        }
    }

    private void Update()
    {
        if (Manifest.Has("time"))
        {
            GameSettings.UpdateTimeLock();
        }
        else
        {
            GameSettings.DisableTimeLock();
        }

        if (Time.unscaledTime < nextFriendRefreshAt)
        {
            return;
        }

        nextFriendRefreshAt = Time.unscaledTime + 1f;
        Friends.CaptureRoom();
    }

    private void OnDestroy()
    {
        if (Ins != this)
        {
            return;
        }

        GameSettings.DisableTimeLock();
        Theme.ReleaseCustomization();
        Ins = null;
    }
}