using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Sentinel.Sentinel;

public static class Theme
{
    public const int Count = 7;

    public static readonly Color HamburburMain =
            new(0.1694782f, 0.1504984f, 0.3584906f);

    public static readonly Color HamburburSecondary =
            new(0.03906193f, 0.0252314f, 0.1981132f);

    public static Color Main;

    public static Color Secondary;

    public static Color Soft;

    public static Color White;

    public static Color DimText;

    public static Color Warn;

    public static Color Bad;

    public static Color Good;

    public static Color Fill;

    public static Texture2D SteamIcon;

    public static Texture2D MetaIcon;

    public static Texture2D QuestionIcon;

    public static Texture2D SettingIcon;

    public static Texture2D RoomIcon;

    public static Texture2D MenuIcon;

    public static Texture2D SpotifyIcon;

    public static Texture2D ChatIcon;

    public static bool Bouncy;

    private static Shader shader_0;

    private static TMP_FontAsset tmp_FontAsset_0;

    private static Material material_0;

    private static Material material_1;

    private static bool bool_0;

    private static AudioSource audioSource_0;

    private static AudioClip audioClip_0;

    private static AudioClip audioClip_1;

    private static AudioClip audioClip_2;

    static Theme() => Apply(0);

    public static void Apply(int theme)
    {
        switch (theme)
        {
            case 0:
                ApplyPalette(
                        HamburburMain,
                        HamburburSecondary,
                        false);

                return;

            case 1:
                ApplyAccent(new Color(0.25f, 0.78f, 1f));

                return;

            case 2:
                ApplyAccent(new Color(0.62f, 0.42f, 1f));

                return;

            case 3:
                ApplyAccent(new Color(1f, 0.45f, 0.25f));

                return;

            case 4:
                ApplyAccent(new Color(0.3f, 1f, 0.72f));

                return;

            case 5:
                ApplyAccent(new Color(1f, 0.78f, 0.22f));

                return;

            case 6:
                ApplyPalette(
                        new Color(0.05f, 0.85f, 1f),
                        new Color(0.01f, 0.08f, 0.14f),
                        true);

                return;

            default:
                ApplyPalette(
                        HamburburMain,
                        HamburburSecondary,
                        false);

                return;
        }
    }

    public static void ApplyCustom(Color color)
    {
        Color secondary = Color.Lerp(color, Color.black, 0.72f);

        ApplyPalette(color, secondary, false);
    }

    private static void ApplyAccent(Color color)
    {
        Color secondary = Color.Lerp(color, Color.black, 0.72f);

        ApplyPalette(color, secondary, false);
    }

    private static void ApplyPalette(
            Color main,
            Color secondary,
            bool  bouncy)
    {
        Main      = main;
        Secondary = secondary;

        Soft    = Color.Lerp(Main, Color.white, 0.38f);
        White   = new Color(0.94f, 0.93f, 1f);
        DimText = Color.Lerp(Secondary, White, 0.55f);

        Warn = new Color(1f,    0.78f, 0.3f);
        Bad  = new Color(1f,    0.3f,  0.35f);
        Good = new Color(0.42f, 1f,    0.62f);

        Fill = new Color(
                Secondary.r,
                Secondary.g,
                Secondary.b,
                0.82f);

        Bouncy = bouncy;

        if (material_0 != null)
        {
            ConfigureMaterial(material_0);
        }

        if (material_1 != null)
        {
            ConfigureMaterial(material_1);
        }
    }

    public static string Name(int theme) =>
            theme switch
            {
                    0     => "HAMBURBUR",
                    1     => "BLUE",
                    2     => "VIOLET",
                    3     => "EMBER",
                    4     => "MINT",
                    5     => "GOLD",
                    6     => "SENTINEL",
                    var _ => "HAMBURBUR",
            };

    public static void Load()
    {
        if (!bool_0)
        {
            bool_0       = true;
            shader_0     = Shader.Find("UI/Default");
            SteamIcon    = LoadEmbeddedTexture("steam",    true);
            MetaIcon     = LoadEmbeddedTexture("meta",     true);
            QuestionIcon = LoadEmbeddedTexture("question", true);
            SettingIcon  = LoadEmbeddedTexture("setting",  false);
            RoomIcon     = LoadEmbeddedTexture("room",     false);
            MenuIcon     = LoadEmbeddedTexture("menu",     true);
            SpotifyIcon  = LoadEmbeddedTexture("spotify",  false);
            ChatIcon     = LoadEmbeddedTexture("chat",     false);
        }
    }

    private static Texture2D LoadEmbeddedTexture(string name, bool smthidk)
    {
        Stream manifestResourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Sentinel.Icons." + name + ".png");
        if (manifestResourceStream == null)
        {
            return Texture2D.whiteTexture;
        }

        Texture2D val = new(2, 2, (TextureFormat)4, false);
        val.LoadImage(new BinaryReader(manifestResourceStream).ReadBytes((int)manifestResourceStream.Length));
        if (!smthidk)
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
        if (tmp_FontAsset_0 != null)
        {
            return tmp_FontAsset_0;
        }

        tmp_FontAsset_0 = TMP_Settings.defaultFontAsset;
        if (tmp_FontAsset_0 == null)
        {
            TMP_FontAsset[] array = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            if (array.Length != 0)
            {
                tmp_FontAsset_0 = array[0];
            }
        }

        return tmp_FontAsset_0;
    }

    private static Material CreatePanelMaterial(bool bool_1)
    {
        TMP_FontAsset val = CreateFont();
        if (val == null)
        {
            return null;
        }

        if (bool_1)
        {
            if (material_0 == null)
            {
                material_0 = new Material(val.material);
                Shader val2 = Shader.Find("TextMeshPro/Distance Field Overlay");
                if (val2 != null)
                {
                    material_0.shader = val2;
                }

                material_0.renderQueue = 3005;
                ConfigureMaterial(material_0);
            }

            return material_0;
        }

        if (material_1 == null)
        {
            material_1 = new Material(val.material);
            ConfigureMaterial(material_1);
        }

        return material_1;
    }

    private static void ConfigureMaterial(Material material_2)
    {
        if (material_2.HasProperty("_FaceDilate"))
        {
            material_2.SetFloat("_FaceDilate", 0.14f);
        }

        if (material_2.HasProperty("_Sharpness"))
        {
            material_2.SetFloat("_Sharpness", 0.35f);
        }

        if (material_2.HasProperty("_OutlineWidth"))
        {
            material_2.SetFloat("_OutlineWidth",    0.12f);
            material_2.SetFloat("_OutlineSoftness", 0f);
            material_2.SetColor("_OutlineColor", new Color(Secondary.r, Secondary.g, Secondary.b, 0.95f));
            material_2.EnableKeyword("OUTLINE_ON");
        }

        ShaderUtilities.UpdateShaderRatios(material_2);
    }

    public static TextMeshPro Text(Transform parent, string name, string str, float size, Color c, bool left = false, bool overlay = true)
    {
        GameObject val = new(name);
        val.transform.SetParent(parent, false);
        TextMeshPro val2 = val.AddComponent<TextMeshPro>();
        Material    val3 = CreatePanelMaterial(overlay);
        if (val3 != null)
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

    public static Material Holo(Color c, int queue = 3000, Texture tex = null, int ztest = 8)
    {
        Load();
        Material val = new(shader_0)
        {
                mainTexture = (Texture)(tex != null ? (object)tex : (object)Texture2D.whiteTexture),
                color       = c,
        };

        val.SetInt("unity_GUIZTestMode", ztest);
        val.renderQueue = queue;

        return val;
    }

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

    private static GameObject CreateMeshObject(Transform transform_0, string string_0, Mesh mesh_0, Material material_2)
    {
        GameObject val = new(string_0);
        val.transform.SetParent(transform_0, false);
        val.AddComponent<MeshFilter>().mesh       = mesh_0;
        val.AddComponent<MeshRenderer>().material = material_2;

        return val;
    }

    public static GameObject Quad(Transform parent, string name, float w, float h, Material mat)
    {
        float num  = w / 2f;
        float num2 = h / 2f;
        Mesh  val  = new();
        val.vertices =
        [
                new Vector3(0f - num, 0f - num2),
                new Vector3(num,      0f - num2),
                new Vector3(0f           - num, num2),
                new Vector3(num,                num2),
        ];

        val.uv =
        [
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
        ];

        val.triangles = [0, 2, 1, 2, 3, 1,];
        val.RecalculateNormals();

        return CreateMeshObject(parent, name, val, mat);
    }

    private static List<Vector2> BuildArcPoints(float float_0, float float_1, float float_2)
    {
        List<Vector2> list = new();
        float         num  = float_0 / 2f - float_2;
        float         num2 = float_1 / 2f - float_2;
        Vector2[] array =
        [
                new(num, num2),
                new(0f      - num, num2),
                new(0f      - num, 0f - num2),
                new(num, 0f - num2),
        ];

        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j <= 5; j++)
            {
                float num3 = (90f * i + 18f * j) * (MathF.PI / 180f);
                list.Add(array[i] + new Vector2(Mathf.Cos(num3), Mathf.Sin(num3)) * float_2);
            }
        }

        return list;
    }

    private static Mesh BuildFilledArcMesh(List<Vector2> list_0)
    {
        Mesh      val   = new();
        Vector3[] array = new Vector3[list_0.Count + 1];
        for (int i = 0; i < list_0.Count; i++)
        {
            array[i + 1] = list_0[i];
        }

        int[] array2 = new int[list_0.Count * 3];
        for (int j = 0; j < list_0.Count; j++)
        {
            array2[j * 3 + 1] = 1 + (j + 1) % list_0.Count;
            array2[j * 3 + 2] = 1 + j;
        }

        val.vertices  = array;
        val.triangles = array2;
        val.RecalculateNormals();

        return val;
    }

    private static Mesh BuildRingMesh(List<Vector2> list_0, List<Vector2> list_1)
    {
        Mesh      val   = new();
        int       count = list_0.Count;
        Vector3[] array = new Vector3[count * 2];
        for (int i = 0; i < count; i++)
        {
            array[i * 2]     = list_0[i];
            array[i * 2 + 1] = list_1[i];
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

    public static Panel Card(Transform parent, string name, float w, float h, Color border, int queue = 2995)
    {
        float num = Mathf.Min(0.018f, h * 0.3f);
        Panel obj = new()
        {
                Go = new GameObject(name),
        };

        obj.Go.transform.SetParent(parent, false);
        obj.FillR   = CreateMeshObject(obj.Go.transform, "fill",   BuildFilledArcMesh(BuildArcPoints(w, h, num)),                                                          Holo(Fill,   queue)).GetComponent<Renderer>();
        obj.BorderR = CreateMeshObject(obj.Go.transform, "border", BuildRingMesh(BuildArcPoints(w,      h, num), BuildArcPoints(w + 0.0044f, h + 0.0044f, num + 0.0022f)), Holo(border, queue + 1)).GetComponent<Renderer>();

        return obj;
    }

    public static GameObject Ring(Transform parent, string name, float inner, float outer, Color c, int queue = 3001, float startDeg = 0f, float lenDeg = 360f)
    {
        Mesh      val    = new();
        Vector3[] array  = new Vector3[130];
        int[]     array2 = new int[384];
        for (int i = 0; i <= 64; i++)
        {
            float num  = (90f - startDeg - lenDeg * i / 64f) * (MathF.PI / 180f);
            float num2 = Mathf.Cos(num);
            float num3 = Mathf.Sin(num);
            array[i * 2]     = new Vector3(num2 * inner, num3 * inner, 0f);
            array[i * 2 + 1] = new Vector3(num2 * outer, num3 * outer, 0f);
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

        val.vertices  = array;
        val.triangles = array2;
        val.RecalculateNormals();

        return CreateMeshObject(parent, name, val, Holo(c, queue));
    }

    public static float Snap(float t)
    {
        t = Mathf.Clamp01(t) - 1f;

        return t * t * (2.35f * t + 1.35f) + 1f;
    }

    public static float Ease(float t) => 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);

    public static void Haptic(bool left, float amp, float dur)
    {
        if (GorillaTagger.Instance != null)
        {
            GorillaTagger.Instance.StartVibration(left, amp, dur);
        }
    }

    private static AudioClip CreateTone(string string_0, float float_0, float float_1, float float_2, float float_3)
    {
        int     num   = (int)(44100f * float_2);
        float[] array = new float[num];
        float   num2  = 0f;
        for (int i = 0; i < num; i++)
        {
            float num3 = i / (float)num;
            num2     += MathF.PI * 2f * Mathf.Lerp(float_0, float_1, num3) / 44100f;
            array[i] =  Mathf.Sin(num2)                                    * Mathf.Exp((0f - num3) * 6f) * float_3;
        }

        AudioClip obj = AudioClip.Create(string_0, num, 1, 44100, false);
        obj.SetData(array, 0);

        return obj;
    }

    private static void PlayClip(AudioClip audioClip_3)
    {
        if (Cfg.Sounds.Value && !(audioClip_3 == null))
        {
            if (audioSource_0 == null)
            {
                GameObject val = new("zx_audio");
                Object.DontDestroyOnLoad(val);
                audioSource_0              = val.AddComponent<AudioSource>();
                audioSource_0.spatialBlend = 0f;
                audioSource_0.playOnAwake  = false;
            }

            audioSource_0.PlayOneShot(audioClip_3);
        }
    }

    public static void ClickSound()
    {
        if (audioClip_0 == null)
        {
            audioClip_0 = CreateTone("zx_click", 880f, 1450f, 0.09f, 0.5f);
        }

        PlayClip(audioClip_0);
    }

    public static void HoverSound()
    {
        if (audioClip_1 == null)
        {
            audioClip_1 = CreateTone("zx_hover", 1600f, 1750f, 0.035f, 0.16f);
        }

        PlayClip(audioClip_1);
    }

    public static void BackSound()
    {
        if (audioClip_2 == null)
        {
            audioClip_2 = CreateTone("zx_back", 700f, 400f, 0.11f, 0.42f);
        }

        PlayClip(audioClip_2);
    }

    public class Panel
    {

        public Renderer BorderR;

        public Renderer   FillR;
        public GameObject Go;
    }
}