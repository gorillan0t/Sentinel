using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Sentinel;

public static class Theme
{

    public const int Count = 7;

    public const int SfxCount = 4;

    public const int TagFontCount = 4;

    public static Color Main = new(0.25f, 0.78f, 1f);

    public static Color Secondary = new(0.01f, 0.08f, 0.14f);

    public static readonly Color HamburburMain = new(0.1694782f, 0.1504984f, 0.3584906f);

    public static readonly Color HamburburSecondary = new(0.03906193f, 0.0252314f, 0.1981132f);

    public static Color Soft = new(0.55f, 0.87f, 1f);

    public static Color White = new(0.92f, 0.97f, 1f);

    public static Color DimText = new(0.55f, 0.72f, 0.82f);

    public static Color Warn = new(1f, 0.82f, 0.29f);

    public static Color Bad = new(1f, 0.3f, 0.26f);

    public static Color Good = new(0.35f, 1f, 0.55f);

    public static Color Fill = new(0.016f, 0.055f, 0.1f, 0.6f);

    public static Texture2D SteamIcon;

    public static Texture2D MetaIcon;

    public static Texture2D PcIcon;

    public static Texture2D QuestionIcon;

    public static Texture2D SettingIcon;

    public static Texture2D RoomIcon;

    public static Texture2D MenuIcon;

    public static Texture2D SpotifyIcon;

    public static Texture2D ChatIcon;

    public static Texture2D FriendIcon;

    public static bool Bouncy;

    private static Shader uiShader;

    private static TMP_FontAsset defaultFontAsset;

    private static Material textMaterial;

    private static Material panelMaterial;

    private static bool resourcesLoaded;

    private static AudioSource sfxSource;

    private static readonly AudioClip[,] sfxClips = new AudioClip[4, 5];

    private static readonly TMP_FontAsset[] tagFonts = new TMP_FontAsset[4];

    private static readonly Font[] ownedFonts = new Font[4];

    private static readonly Material[] tagMaterials = new Material[4];

    private static readonly bool[] tagFontLoaded = new bool[4];

    private static readonly Dictionary<(float, float), Mesh> roundedRectMeshes = new();

    private static readonly Dictionary<(float, float, float), Mesh> filledArcMeshes = new();

    private static readonly Dictionary<(float, float, float), Mesh> ringMeshes = new();

    private static readonly Dictionary<(float, float, float, float), Mesh> borderMeshes = new();

    private static readonly HashSet<(float, float, float)> prebuiltMeshSizes = new()
    {
            (0.2f, 0.082f, 0.018f),
            (0.28f, 0.092f, 0.018f),
            (0.27f, 0.092f, 0.018f),
            (0.26f, 0.092f, 0.018f),
            (0.29f, 0.06f, 0.018f),
            (0.34f, 0.06f, 0.018f),
            (0.3f, 0.078f, 0.018f),
            (0.22f, 0.078f, 0.018f),
            (0.3f, 0.085f, 0.018f),
            (0.22f, 0.064f, 0.018f),
            (0.2f, 0.09f, 0.018f),
            (0.22f, 0.09f, 0.018f),
            (0.3f, 0.08f, 0.018f),
            (0.2f, 0.08f, 0.018f),
            (0.22f, 0.08f, 0.018f),
            (0.15f, 0.075f, 0.018f),
            (0.2f, 0.075f, 0.018f),
            (0.26f, 0.075f, 0.018f),
            (0.16f, 0.064f, 0.018f),
            (0.18f, 0.064f, 0.018f),
            (0.115f, 0.115f, 0.018f),
            (0.32f, 0.065f, 0.018f),
            (0.21f, 0.065f, 0.018f),
            (0.068f, 0.065f, 0.018f),
    };

    public static bool RectangleMenu
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        get
        {
            if (Manifest.Has("menu_rectangle"))
            {
                return Cfg.MenuLayout.Value == 1;
            }

            return false;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string SfxName(int value) =>
            value switch
            {
                    1     => "SOFT",
                    2     => "ARCADE",
                    3     => "CRYSTAL",
                    var _ => "CLASSIC",
            };

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string TagFontName(int value) =>
            value switch
            {
                    1     => "SANS",
                    2     => "SERIF",
                    3     => "MONO",
                    var _ => "DEFAULT",
            };

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string TagSizeName(int value) =>
            value switch
            {
                    2     => "LARGE",
                    0     => "SMALL",
                    var _ => "NORMAL",
            };

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string TagColorName(int value) =>
            value switch
            {
                    2     => "WHITE",
                    1     => "THEME",
                    var _ => "PLAYER",
            };

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void SetTagFont(TMP_Text text, int value)
    {
        int num = Manifest.Has("custom_tags") ? Mathf.Clamp(value, 0, 3) : 0;
        if (!tagFontLoaded[num])
        {
            tagFontLoaded[num] = true;
            tagFonts[num]      = CreateFont();
            if (num != 0)
            {
                Font val = Font.CreateDynamicFontFromOSFont(num switch
                                                            {
                                                                    2     => "Georgia",
                                                                    1     => "Arial",
                                                                    var _ => "Consolas",
                                                            }, 48);

                if ((Object)(object)val != (Object)null)
                {
                    TMP_FontAsset val2 = TMP_FontAsset.CreateFontAsset(val);
                    if ((Object)(object)val2 != (Object)null)
                    {
                        tagFonts[num]   = val2;
                        ownedFonts[num] = val;
                    }
                    else
                    {
                        Object.Destroy((Object)(object)val);
                    }
                }
            }

            if ((Object)(object)tagFonts[num] != (Object)null)
            {
                tagMaterials[num] = new Material(tagFonts[num].material);
                ConfigureMaterial(tagMaterials[num]);
            }
        }

        if (!((Object)(object)tagFonts[num] == (Object)null))
        {
            text.font               = tagFonts[num];
            text.fontSharedMaterial = tagMaterials[num];
        }
    }

    public static void ReleaseCustomization()
    {
        for (int i = 0; i < 4; i++)
        {
            if ((Object)(object)tagMaterials[i] != (Object)null)
            {
                Object.Destroy((Object)(object)tagMaterials[i]);
            }

            if ((Object)(object)ownedFonts[i] != (Object)null)
            {
                if ((Object)(object)tagFonts[i] != (Object)null)
                {
                    Texture2D[] atlasTextures = tagFonts[i].atlasTextures;
                    foreach (Texture2D val in atlasTextures)
                    {
                        if ((Object)(object)val != (Object)null)
                        {
                            Object.Destroy((Object)(object)val);
                        }
                    }

                    Object.Destroy((Object)(object)tagFonts[i].material);
                    Object.Destroy((Object)(object)tagFonts[i]);
                }

                Object.Destroy((Object)(object)ownedFonts[i]);
            }

            tagFonts[i]      = null;
            ownedFonts[i]    = null;
            tagMaterials[i]  = null;
            tagFontLoaded[i] = false;
        }

        for (int k = 0; k < 4; k++)
        {
            for (int l = 0; l < 5; l++)
            {
                if ((Object)(object)sfxClips[k, l] != (Object)null)
                {
                    Object.Destroy((Object)(object)sfxClips[k, l]);
                }

                sfxClips[k, l] = null;
            }
        }

        if ((Object)(object)sfxSource != (Object)null)
        {
            Object.Destroy((Object)(object)sfxSource.gameObject);
        }

        sfxSource = null;
    }

    public static void Apply(int n)
    {
        if (n == 0)
        {
            ApplyPalette(HamburburMain, HamburburSecondary, false);

            return;
        }

        Color accent = n switch
                       {
                               2     => new Color(0.62f, 0.42f, 1f),
                               3     => new Color(1f,    0.45f, 0.25f),
                               4     => new Color(0.3f,  1f,    0.72f),
                               5     => new Color(1f,    0.78f, 0.22f),
                               6     => new Color(0.05f, 0.85f, 1f),
                               var _ => new Color(0.25f, 0.78f, 1f),
                       };

        ApplyPalette(accent, Color.Lerp(accent, Color.black, 0.72f), n == 6);
    }

    public static void ApplyCustom(Color color) => ApplyPalette(color, Color.Lerp(color, Color.black, 0.72f), false);

    private static void ApplyPalette(Color main, Color secondary, bool bouncy)
    {
        Main      = main;
        Secondary = secondary;
        Soft      = Color.Lerp(Main, Color.white, 0.38f);
        White     = new Color(0.94f, 0.93f, 1f);
        DimText   = Color.Lerp(Secondary, White, 0.55f);
        Warn      = new Color(1f,          0.78f,       0.3f);
        Bad       = new Color(1f,          0.3f,        0.35f);
        Good      = new Color(0.42f,       1f,          0.62f);
        Fill      = new Color(Secondary.r, Secondary.g, Secondary.b, 0.82f);
        Bouncy    = bouncy;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string Name(int n) =>
            n switch
            {
                    1     => "BLUE",
                    2     => "VIOLET",
                    3     => "EMBER",
                    4     => "MINT",
                    5     => "GOLD",
                    6     => "SENTINEL",
                    var _ => "HAMBURBUR",
            };

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Load()
    {
        if (!resourcesLoaded)
        {
            resourcesLoaded = true;
            uiShader        = Shader.Find("UI/Default");
            SteamIcon       = LoadEmbeddedTexture("steam",    true);
            MetaIcon        = LoadEmbeddedTexture("meta",     true);
            PcIcon          = LoadEmbeddedTexture("pc",       true);
            QuestionIcon    = LoadEmbeddedTexture("question", true);
            SettingIcon     = LoadEmbeddedTexture("setting",  false);
            RoomIcon        = LoadEmbeddedTexture("room",     false);
            MenuIcon        = LoadEmbeddedTexture("menu",     true);
            SpotifyIcon     = LoadEmbeddedTexture("spotify",  false);
            ChatIcon        = LoadEmbeddedTexture("chat",     false);
            FriendIcon      = LoadEmbeddedTexture("friend",   false);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Texture2D LoadEmbeddedTexture(string arg1, bool arg2)
    {
        Stream manifestResourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Sentinel.icons." + arg1 + ".png");
        if (manifestResourceStream == null)
        {
            return Texture2D.whiteTexture;
        }

        Texture2D val = new(2, 2, (TextureFormat)4, false);
        val.LoadImage(new BinaryReader(manifestResourceStream).ReadBytes((int)manifestResourceStream.Length));
        if (!arg2)
        {
            Color32[] pixels = val.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                ref Color32 reference  = ref pixels[i];
                ref Color32 reference2 = ref pixels[i];
                pixels[i].b  = byte.MaxValue;
                reference2.g = byte.MaxValue;
                reference.r  = byte.MaxValue;
            }

            val.SetPixels32(pixels);
            val.Apply();
        }

        val.filterMode = (FilterMode)1;

        return val;
    }

    private static TMP_FontAsset CreateFont()
    {
        if ((Object)(object)defaultFontAsset != (Object)null)
        {
            return defaultFontAsset;
        }

        defaultFontAsset = TMP_Settings.defaultFontAsset;
        if ((Object)(object)defaultFontAsset == (Object)null)
        {
            TMP_FontAsset[] array = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            if (array.Length != 0)
            {
                defaultFontAsset = array[0];
            }
        }

        return defaultFontAsset;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Material CreatePanelMaterial(bool arg1)
    {
        TMP_FontAsset val = CreateFont();
        if (!((Object)(object)val == (Object)null))
        {
            if (!arg1)
            {
                if ((Object)(object)panelMaterial == (Object)null)
                {
                    panelMaterial = new Material(val.material);
                    ConfigureMaterial(panelMaterial);
                }

                return panelMaterial;
            }

            if ((Object)(object)textMaterial == (Object)null)
            {
                textMaterial = new Material(val.material);
                Shader val2 = Shader.Find("TextMeshPro/Distance Field Overlay");
                if ((Object)(object)val2 != (Object)null)
                {
                    textMaterial.shader = val2;
                }

                textMaterial.renderQueue = 3005;
                ConfigureMaterial(textMaterial);
            }

            return textMaterial;
        }

        return null;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ConfigureMaterial(Material arg1)
    {
        if (arg1.HasProperty("_FaceDilate"))
        {
            arg1.SetFloat("_FaceDilate", 0.14f);
        }

        if (arg1.HasProperty("_Sharpness"))
        {
            arg1.SetFloat("_Sharpness", 0.35f);
        }

        if (arg1.HasProperty("_OutlineWidth"))
        {
            arg1.SetFloat("_OutlineWidth",    0.12f);
            arg1.SetFloat("_OutlineSoftness", 0f);
            arg1.SetColor("_OutlineColor", new Color(0f, 0.02f, 0.05f, 0.9f));
            arg1.EnableKeyword("OUTLINE_ON");
        }

        ShaderUtilities.UpdateShaderRatios(arg1);
    }

    public static TextMeshPro Text(Transform parent, string name, string str, float size, Color c, bool left = false, bool overlay = true)
    {
        GameObject val = new(name);
        val.transform.SetParent(parent, false);
        TextMeshPro val2 = val.AddComponent<TextMeshPro>();
        Material    val3 = CreatePanelMaterial(overlay);
        if ((Object)(object)val3 != (Object)null)
        {
            val2.font               = CreateFont();
            val2.fontSharedMaterial = val3;
        }

        val2.rectTransform.sizeDelta = Vector2.zero;
        val2.text                    = str;
        val2.fontSize                = size * 10f;
        val2.color                   = c;
        val2.alignment               = (TextAlignmentOptions)(left ? 513 : 514);
        val2.textWrappingMode        = 0;

        return val2;
    }

    public static TextMeshPro Label(Transform parent, string name, float size, Color c) => Text(parent, name, "", size, c, false, false);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Material Holo(Color c, int queue = 3000, Texture tex = null, int ztest = 8)
    {
        Load();
        Material val = new(uiShader)
        {
                mainTexture = (Texture)((Object)(object)tex != (Object)null ? (object)tex : (object)Texture2D.whiteTexture),
                color       = c,
        };

        val.SetInt("unity_GUIZTestMode", ztest);
        val.renderQueue = queue;

        return val;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Material Rig(Color c)
    {
        Material val = new(Shader.Find("GorillaTag/UberShader"));
        val.SetInt("_SrcBlend", 5);
        val.SetInt("_DstBlend", 1);
        val.SetInt("_ZWrite",   0);
        val.renderQueue = 3050;
        val.color       = c;

        return val;
    }

    private static GameObject CreateMeshObject(Transform arg1, string arg2, Mesh arg3, Material arg4, bool arg5 = false)
    {
        GameObject val = new(arg2);
        val.transform.SetParent(arg1, false);
        val.AddComponent<MeshFilter>().sharedMesh = arg3;
        RenderResources.Assign(val.AddComponent<MeshRenderer>(), arg4, arg5 ? arg3 : null);

        return val;
    }

    private static Mesh BuildRoundedRectMesh(float arg1, float arg2)
    {
        if (roundedRectMeshes.TryGetValue((arg1, arg2), out Mesh value))
        {
            return value;
        }

        float num  = arg1 / 2f;
        float num2 = arg2 / 2f;
        value = new Mesh();
        value.vertices = new Vector3[4]
        {
                new(0f      - num, 0f - num2),
                new(num, 0f - num2),
                new(0f      - num, num2),
                new(num, num2),
        };

        value.uv = new Vector2[4]
        {
                new(0f, 0f),
                new(1f, 0f),
                new(0f, 1f),
                new(1f, 1f),
        };

        value.triangles = new int[6] { 0, 2, 1, 2, 3, 1, };
        value.RecalculateNormals();
        roundedRectMeshes[(arg1, arg2)] = value;

        return value;
    }

    public static GameObject Quad(Transform parent, string name, float w, float h, Material mat) => CreateMeshObject(parent, name, BuildRoundedRectMesh(w, h), mat);

    private static List<Vector2> BuildArcPoints(float arg1, float arg2, float arg3)
    {
        List<Vector2> list = new();
        float         num  = arg1 / 2f - arg3;
        float         num2 = arg2 / 2f - arg3;
        Vector2[] array = new Vector2[4]
        {
                new(num, num2),
                new(0f      - num, num2),
                new(0f      - num, 0f - num2),
                new(num, 0f - num2),
        };

        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j <= 5; j++)
            {
                float num3 = (90f * i + 18f * j) * (MathF.PI / 180f);
                list.Add(array[i] + new Vector2(Mathf.Cos(num3), Mathf.Sin(num3)) * arg3);
            }
        }

        return list;
    }

    private static Mesh BuildFilledArcMesh(List<Vector2> arg1)
    {
        Mesh      val   = new();
        Vector3[] array = new Vector3[arg1.Count + 1];
        for (int i = 0; i < arg1.Count; i++)
        {
            array[i + 1] = arg1[i];
        }

        int[] array2 = new int[arg1.Count * 3];
        for (int j = 0; j < arg1.Count; j++)
        {
            array2[j * 3 + 1] = 1 + (j + 1) % arg1.Count;
            array2[j * 3 + 2] = 1 + j;
        }

        val.vertices  = array;
        val.triangles = array2;
        val.RecalculateNormals();

        return val;
    }

    private static Mesh BuildRingMesh(List<Vector2> arg1, List<Vector2> arg2)
    {
        Mesh      val   = new();
        int       count = arg1.Count;
        Vector3[] array = new Vector3[count * 2];
        for (int i = 0; i < count; i++)
        {
            array[i * 2]     = arg1[i];
            array[i * 2 + 1] = arg2[i];
        }

        int[] array2 = new int[count * 6];
        for (int j = 0; j < count; j++)
        {
            int num  = j               * 2;
            int num2 = (j + 1) % count * 2;
            int num3 = j               * 6;
            array2[num3]     = num;
            array2[num3 + 1] = num2;
            array2[num3 + 2] = num + 1;
            array2[num3 + 3] = num2;
            array2[num3 + 4] = num2 + 1;
            array2[num3 + 5] = num  + 1;
        }

        val.vertices  = array;
        val.triangles = array2;
        val.RecalculateNormals();

        return val;
    }

    private static Mesh BuildBorderMesh(float arg1, float arg2, float arg3)
    {
        if (!GetCachedPanelMesh(arg1, arg2, arg3))
        {
            return BuildFilledArcMesh(BuildArcPoints(arg1, arg2, arg3));
        }

        if (!filledArcMeshes.TryGetValue((arg1, arg2, arg3), out Mesh value))
        {
            value                               = BuildFilledArcMesh(BuildArcPoints(arg1, arg2, arg3));
            filledArcMeshes[(arg1, arg2, arg3)] = value;

            return value;
        }

        return value;
    }

    private static Mesh BuildPanelMesh(float arg1, float arg2, float arg3)
    {
        if (!GetCachedPanelMesh(arg1, arg2, arg3))
        {
            return BuildRingMesh(BuildArcPoints(arg1, arg2, arg3), BuildArcPoints(arg1 + 0.0044f, arg2 + 0.0044f, arg3 + 0.0022f));
        }

        if (!ringMeshes.TryGetValue((arg1, arg2, arg3), out Mesh value))
        {
            value                          = BuildRingMesh(BuildArcPoints(arg1, arg2, arg3), BuildArcPoints(arg1 + 0.0044f, arg2 + 0.0044f, arg3 + 0.0022f));
            ringMeshes[(arg1, arg2, arg3)] = value;

            return value;
        }

        return value;
    }

    private static bool GetCachedPanelMesh(float arg1, float arg2, float arg3) => prebuiltMeshSizes.Contains((arg1, arg2, arg3));

    private static Mesh CreatePanelMesh(float arg1, float arg2, float arg3, float arg4)
    {
        if (borderMeshes.TryGetValue((arg1, arg2, arg3, arg4), out Mesh value))
        {
            return value;
        }

        value = new Mesh();
        Vector3[] array  = new Vector3[130];
        int[]     array2 = new int[384];
        for (int i = 0; i <= 64; i++)
        {
            float num  = (90f - arg3 - arg4 * i / 64f) * (MathF.PI / 180f);
            float num2 = Mathf.Cos(num);
            float num3 = Mathf.Sin(num);
            array[i * 2]     = new Vector3(num2 * arg1, num3 * arg1, 0f);
            array[i * 2 + 1] = new Vector3(num2 * arg2, num3 * arg2, 0f);
        }

        for (int j = 0; j < 64; j++)
        {
            int num4 = j * 2;
            int num5 = j * 6;
            array2[num5]     = num4;
            array2[num5 + 1] = num4 + 2;
            array2[num5 + 2] = num4 + 1;
            array2[num5 + 3] = num4 + 2;
            array2[num5 + 4] = num4 + 3;
            array2[num5 + 5] = num4 + 1;
        }

        value.vertices  = array;
        value.triangles = array2;
        value.RecalculateNormals();
        borderMeshes[(arg1, arg2, arg3, arg4)] = value;

        return value;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Panel Card(Transform parent, string name, float w, float h, Color border, int queue = 2995)
    {
        float arg = Mathf.Min(0.018f, h * 0.3f);
        Panel obj = new()
        {
                Go = new GameObject(name),
        };

        obj.Go.transform.SetParent(parent, false);
        bool arg2 = !GetCachedPanelMesh(w, h, arg);
        obj.FillR   = CreateMeshObject(obj.Go.transform, "fill",   BuildBorderMesh(w, h, arg), Holo(Fill,   queue),     arg2).GetComponent<Renderer>();
        obj.BorderR = CreateMeshObject(obj.Go.transform, "border", BuildPanelMesh(w, h, arg),  Holo(border, queue + 1), arg2).GetComponent<Renderer>();

        return obj;
    }

    public static GameObject Ring(Transform parent, string name, float inner, float outer, Color c, int queue = 3001, float startDeg = 0f, float lenDeg = 360f) => CreateMeshObject(parent, name, CreatePanelMesh(inner, outer, startDeg, lenDeg), Holo(c, queue));

    public static float Snap(float t)
    {
        t = Mathf.Clamp01(t) - 1f;

        return t * t * (2.35f * t + 1.35f) + 1f;
    }

    public static float Ease(float t) => 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);

    public static void Haptic(bool left, float amp, float dur)
    {
        if ((Object)(object)GorillaTagger.Instance != (Object)null)
        {
            GorillaTagger.Instance.StartVibration(left, amp, dur);
        }
    }

    private static AudioClip CreateTone(string arg1, float arg2, float arg3, float arg4, float arg5)
    {
        int     num   = (int)(44100f * arg4);
        float[] array = new float[num];
        float   num2  = 0f;
        for (int i = 0; i < num; i++)
        {
            float num3 = i / (float)num;
            num2 += MathF.PI * 2f * Mathf.Lerp(arg2, arg3, num3) / 44100f;
            float num4 = Mathf.Min(1f, num3 * 30f);
            array[i] = (Mathf.Sin(num2) * 0.8f + Mathf.Sin(num2 * 2f) * 0.2f) * num4 * Mathf.Exp((0f - num3) * 7f) * arg5;
        }

        AudioClip obj = AudioClip.Create(arg1, num, 1, 44100, false);
        obj.SetData(array, 0);

        return obj;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void PlayClip(AudioClip arg1)
    {
        if (Cfg.Sounds.Value && !((Object)(object)arg1 == (Object)null))
        {
            if ((Object)(object)sfxSource == (Object)null)
            {
                GameObject val = new("zx_audio");
                Object.DontDestroyOnLoad((Object)val);
                sfxSource              = val.AddComponent<AudioSource>();
                sfxSource.spatialBlend = 0f;
                sfxSource.playOnAwake  = false;
            }

            sfxSource.PlayOneShot(arg1);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void PlaySfx(int arg1)
    {
        if (!Cfg.Sounds.Value)
        {
            return;
        }

        int num = Manifest.Has("menu_sfx") ? Mathf.Clamp(Cfg.MenuSfx.Value, 0, 3) : 0;
        if ((Object)(object)sfxClips[num, arg1] == (Object)null)
        {
            float num2;
            float num3;
            float num4;
            float num5;
            switch (arg1)
            {
                default:
                    num2 = 640f;
                    num3 = 920f;
                    num4 = 0.065f;
                    num5 = 0.32f;

                    break;

                case 1:
                    num2 = 1100f;
                    num3 = 1250f;
                    num4 = 0.025f;
                    num5 = 0.08f;

                    break;

                case 2:
                    num2 = 700f;
                    num3 = 400f;
                    num4 = 0.11f;
                    num5 = 0.42f;

                    break;

                case 3:
                    num2 = 330f;
                    num3 = 880f;
                    num4 = 0.18f;
                    num5 = 0.35f;

                    break;

                case 4:
                    num2 = 660f;
                    num3 = 220f;
                    num4 = 0.16f;
                    num5 = 0.28f;

                    break;
            }

            switch (num)
            {
                case 2:
                    num2 *= 1.5f;
                    num3 *= 0.8f;
                    num4 *= 0.7f;

                    break;

                case 3:
                    num2 *= 2.4f;
                    num3 *= 2.8f;
                    num4 *= 1.8f;
                    num5 *= 0.65f;

                    break;

                case 1:
                    num2 *= 0.55f;
                    num3 *= 0.55f;
                    num4 *= 1.4f;
                    num5 *= 0.6f;

                    break;
            }

            sfxClips[num, arg1] = CreateTone("zx_sfx_" + num + "_" + arg1, num2, num3, num4, num5);
        }

        PlayClip(sfxClips[num, arg1]);
    }

    public static void ClickSound() => PlaySfx(0);

    public static void HoverSound() => PlaySfx(1);

    public static void BackSound() => PlaySfx(2);

    public static void OpenSound() => PlaySfx(3);

    public static void CloseSound() => PlaySfx(4);

    public class Panel
    {

        public Renderer BorderR;

        public Renderer   FillR;
        public GameObject Go;
    }
}