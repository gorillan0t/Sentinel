using System;
using TMPro;
using UnityEngine;

namespace Sentinel;

/// <summary>A local, camera-facing chat card. No chat messages are transmitted by this component.</summary>
public sealed class FloatingChat : MonoBehaviour
{
    private Transform  cameraTransform;
    private Color      displayedColor;
    private int        displayedMode    = -1;
    private int        displayedTheme   = -1;
    private int        displayedVersion = -1;
    private TMP_Text[] messageTexts;
    private float      nextRefresh;

    private       GameObject   root;
    private       TMP_Text     statusText;
    private       TMP_Text     titleText;
    public static FloatingChat Ins { get; private set; }

    private void Awake() => Ins = this;

    private void Update() => Refresh(false);

    private void OnDestroy()
    {
        DestroyCard();
        if (Ins == this) Ins = null;
    }

    public static string ModeName() => Cfg.ChatHud != null && Cfg.ChatHud.Value > 0 ? "ON" : "OFF";

    public static void Cycle()
    {
        if (Cfg.ChatHud == null) return;
        Cfg.ChatHud.Value = Cfg.ChatHud.Value > 0 ? 0 : 1;
        Ins?.Refresh(true);
    }

    public static void Recenter() => Ins?.Refresh(true);

    private void Refresh(bool force)
    {
        int mode = Cfg.ChatHud == null ? 0 : Mathf.Clamp(Cfg.ChatHud.Value, 0, 4);
        if (!Manifest.Has("youtube_chat") || mode == 0)
        {
            DestroyCard();
            displayedMode = mode;

            return;
        }

        GorillaTagger tagger = GorillaTagger.Instance;
        Transform     camera = tagger != null && tagger.mainCamera != null ? tagger.mainCamera.transform : null;
        if (camera == null)
        {
            if (root != null) root.SetActive(false);

            return;
        }

        if (force || root == null || cameraTransform != camera || displayedTheme != Cfg.Theme.Value || displayedColor != Theme.Main)
            BuildCard(camera, mode);
        else if (displayedMode != mode)
            PositionCard(mode);

        root.SetActive(true);

        if (Time.unscaledTime < nextRefresh && displayedVersion == YouTubeLiveChat.MessageVersion) return;
        nextRefresh = Time.unscaledTime + 0.25f;
        YouTubeLiveChat.Poll();
        UpdateMessages();
    }

    private void BuildCard(Transform camera, int mode)
    {
        DestroyCard();
        cameraTransform = camera;
        displayedTheme  = Cfg.Theme.Value;
        displayedColor  = Theme.Main;
        root            = new GameObject("zx_floating_chat");
        root.transform.SetParent(transform, false);
        root.transform.localScale = Vector3.one;
        Theme.Card(root.transform, "panel", 0.64f, 0.44f, Theme.Main, 3090);
        Theme.Quad(root.transform, "rule", 0.58f, 0.0025f, Theme.Holo(Theme.Main, 3093)).transform.localPosition = new Vector3(0f, 0.115f, -0.003f);
        AddText("heading", "LIVE CHAT", 0.021f, Theme.Soft, -0.285f, 0.177f).fontStyle                           = FontStyles.Bold;
        statusText                                                                                               = AddText("state", "", 0.016f, Theme.DimText, 0.105f,  0.177f);
        titleText                                                                                                = AddText("title", "", 0.017f, Theme.White,   -0.285f, 0.137f);
        messageTexts                                                                                             = new TMP_Text[6];
        for (int i = 0; i < messageTexts.Length; i++)
            messageTexts[i] = AddText("message" + i, "", 0.017f, Theme.White, -0.285f, 0.08f - i * 0.046f);

        PositionCard(mode);
        UpdateMessages();
    }

    private TMP_Text AddText(string name, string value, float size, Color color, float x, float y)
    {
        TextMeshPro label = Theme.Text(root.transform, name, value, size, color, true);
        label.richText                = false;
        label.transform.localPosition = new Vector3(x, y, -0.004f);

        return label;
    }

    private void PositionCard(int mode)
    {
        displayedMode = mode;

        if (root == null || cameraTransform == null) return;
        Vector3 heading                           = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        if (heading.sqrMagnitude < 0.01f) heading = cameraTransform.forward;
        root.transform.position = cameraTransform.position + heading * 1.15f + Vector3.down * 0.08f;
        root.transform.rotation = Quaternion.LookRotation(heading, Vector3.up);
    }

    private void UpdateMessages()
    {
        if (statusText == null || titleText == null || messageTexts == null) return;
        statusText.text  = YouTubeLiveChat.Status.Length > 0 ? Truncate(YouTubeLiveChat.Status, 25) : YouTubeLiveChat.IsConfigured ? "LOOKING..." : "NO CHANNEL";
        statusText.color = YouTubeLiveChat.Status        == "LIVE" ? Theme.Good : Theme.DimText;
        titleText.text   = YouTubeLiveChat.IsConfigured ? Truncate(YouTubeLiveChat.StreamTitle, 50) : "SAVE YOUR CHANNEL IN CONFIG";
        string[] messages = YouTubeLiveChat.Messages ?? Array.Empty<string>();
        int      first    = Mathf.Max(0, messages.Length - messageTexts.Length);
        for (int i = 0; i < messageTexts.Length; i++)
        {
            int    index   = first + i;
            string message = index < messages.Length ? messages[index] ?? "" : "";
            int    split   = message.IndexOf('\t');
            messageTexts[i].text = split < 0 ? Truncate(message, 52) : Truncate(message.Substring(0, split), 15) + ": " + Truncate(message.Substring(split + 1), 49);
        }

        displayedVersion = YouTubeLiveChat.MessageVersion;
    }

    private static string Truncate(string value, int length)
    {
        value = (value ?? "").Replace('\r', ' ').Replace('\n', ' ');

        return value.Length > length ? value.Substring(0, Mathf.Max(0, length - 1)) + "…" : value;
    }

    private void DestroyCard()
    {
        if (root != null) Destroy(root);
        root             = null;
        cameraTransform  = null;
        statusText       = titleText = null;
        messageTexts     = null;
        displayedVersion = -1;
    }
}