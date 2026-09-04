using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace Sentinel.Sentinel;

[BepInPlugin(Constants.Guid, Constants.Name, Constants.Version)]
public class Plugin : BaseUnityPlugin
{
    public static Plugin Ins;

    public Disc Disc;

    private void Awake()
    {
        Ins = this;

        Cfg.Load(Config);
        Theme.Apply(Cfg.Theme.Value);

        gameObject.AddComponent<MainThreadDispatch>();

        new Harmony(Constants.Guid).PatchAll();

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

        StartCoroutine(HamburburData.RefreshLoop());

        SpotifyPlayer.Initialize();
        YouTubeLiveChat.Initialize();

        Disc = gameObject.AddComponent<Disc>();

        gameObject.AddComponent<RingMenu>();
        gameObject.AddComponent<Frame>();
        gameObject.AddComponent<Tags>();
        gameObject.AddComponent<Notify>();
        gameObject.AddComponent<Watcher>();
        gameObject.AddComponent<Net>();
        gameObject.AddComponent<Friends>();
    }
}