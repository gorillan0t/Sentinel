using System;
using System.Runtime.CompilerServices;
using Sentinel;
using UnityEngine;
using Object = UnityEngine.Object;

internal sealed class ThemeColorPicker : IDisposable
{

    private readonly Color[] pixelBuffer = new Color[256];

    private bool disposed;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public ThemeColorPicker()
    {
        RegenerateTextures();
        ColorWheelTexture = new Texture2D(256, 256, (TextureFormat)4, false)
        {
                name     = "Sentinel color wheel",
                wrapMode = (TextureWrapMode)1,
        };

        Color[] array = new Color[65536];
        Vector2 val   = default;
        for (int i = 0; i < 256; i++)
        {
            for (int j = 0; j < 256; j++)
            {
                val = new Vector2((j - 127.5f) / 127f, (i - 127.5f) / 127f);
                float magnitude = val.magnitude;
                Color val2      = Color.HSVToRGB(Mathf.Repeat(Mathf.Atan2(val.y, val.x) / (MathF.PI * 2f), 1f), Mathf.Clamp01(magnitude), 1f);
                val2.a             = Mathf.Clamp01((1f - magnitude) * 127f);
                array[i * 256 + j] = val2;
            }
        }

        ColorWheelTexture.SetPixels(array);
        ColorWheelTexture.Apply(false, true);
        BrightnessTexture = new Texture2D(256, 1, (TextureFormat)4, false)
        {
                name     = "Sentinel brightness",
                wrapMode = (TextureWrapMode)1,
        };

        UpdateBrightnessTexture();
    }

    [field: CompilerGenerated]
    public Texture2D ColorWheelTexture { [CompilerGenerated] get; }

    [field: CompilerGenerated]
    public Texture2D BrightnessTexture { [CompilerGenerated] get; }

    [field: CompilerGenerated]
    public float Hue { [CompilerGenerated] get; [CompilerGenerated] private set; }

    [field: CompilerGenerated]
    public float Saturation { [CompilerGenerated] get; [CompilerGenerated] private set; }

    [field: CompilerGenerated]
    public float Brightness { [CompilerGenerated] get; [CompilerGenerated] private set; }

    public Color SelectedColor => Color.HSVToRGB(Hue, Saturation, Brightness);

    public Vector2 WheelPosition => new Vector2(Mathf.Cos(Hue * MathF.PI * 2f), Mathf.Sin(Hue * MathF.PI * 2f)) * Saturation;

    void IDisposable.Dispose() => Dispose();

    private void RegenerateTextures()
    {
        float arg  = default;
        float arg2 = default;
        float num  = default;
        Color.RGBToHSV(Theme.Main, out arg, out arg2, out num);
        Hue        = arg;
        Saturation = arg2;
        Brightness = Mathf.Max(0.1f, num);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void SetHueSaturation(Vector2 arg1)
    {
        if (Manifest.Has("custom_theme"))
        {
            Hue        = Mathf.Repeat(Mathf.Atan2(arg1.y, arg1.x) / (MathF.PI * 2f), 1f);
            Saturation = Mathf.Clamp01(arg1.magnitude);
            UpdateBrightnessTexture();
            UpdateWheelTexture();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void SetBrightness(float arg1)
    {
        if (Manifest.Has("custom_theme"))
        {
            Brightness = Mathf.Lerp(0.1f, 1f, Mathf.Clamp01(arg1));
            UpdateWheelTexture();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Reset()
    {
        if (Manifest.Has("custom_theme"))
        {
            Theme.Apply(Cfg.Theme.Value);
            RegenerateTextures();
            UpdateBrightnessTexture();
            UpdateWheelTexture();
            SaveChanges();
        }
    }

    private void UpdateWheelTexture()
    {
        Theme.ApplyCustom(SelectedColor);
        disposed = true;
    }

    private void UpdateBrightnessTexture()
    {
        for (int i = 0; i < pixelBuffer.Length; i++)
        {
            pixelBuffer[i] = Color.HSVToRGB(Hue, Saturation, Mathf.Lerp(0.1f, 1f, i / 255f));
        }

        BrightnessTexture.SetPixels(pixelBuffer);
        BrightnessTexture.Apply(false, false);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void SaveChanges()
    {
        if (disposed)
        {
            disposed = false;
            if (Manifest.Has("custom_theme"))
            {
                PlayerPrefs.SetFloat("zx_theme_h", Hue);
                PlayerPrefs.SetFloat("zx_theme_s", Saturation);
                PlayerPrefs.SetFloat("zx_theme_b", Brightness);
                PlayerPrefs.Save();
                Plugin.Ins?.Disc?.Retheme();
            }
        }
    }

    public void Dispose()
    {
        SaveChanges();
        Object.Destroy(ColorWheelTexture);
        Object.Destroy(BrightnessTexture);
    }
}