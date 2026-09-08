using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using GorillaNetworking;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Sentinel;

public class RingMenu : MonoBehaviour
{

    public static RingMenu Ins;

    public static readonly string[] BtnNames = new string[4] { "X", "Y", "A", "B", };

    private static readonly Vector3 ContentDepthOffset = new(0f, 0f, -0.002f);

    private readonly bool[] controllerButtons = new bool[4];

    private readonly List<MenuItem> menuItems = new();

    private readonly HashSet<string> reportedUserIds = new();

    private int activeGridPage;

    private bool anchoredToLeftHand;

    private TMP_Text artistLabel;

    private Transform brightnessMarker;

    private int buttonBindingMode;

    private int cardSequence;

    private TMP_Text[] chatMessageLabels;

    private Renderer clientIconRenderer;

    private bool closeButtonDown;

    private float closeProgress;

    private TextMeshPro colorHexLabel;

    private ThemeColorPicker colorPicker;

    private Renderer colorPreviewRenderer;

    private Transform colorWheelMarker;

    private GameObject contentRoot;

    private Page currentPage;

    private Kind detectionKind;

    private int displayedChatVersion;

    private int draggedItemIndex = -1;

    private int friendsPage;

    private int friendsTab;

    private TMP_Text friendStatusLabel;

    private Transform handAnchor;

    private int hoveredItemIndex = -1;

    private float hoverExitDelay;

    private Transform innerBand;

    private Transform innerEdge;

    private float inputCooldownUntil;

    private bool isClosing;

    private bool isJoiningRoom;

    private bool isOpen;

    private string joinCode = "";

    private TMP_Text joinCodeLabel;

    private TMP_Text joinStatusLabel;

    private Transform laserDot;

    private float lastCloseTapTime = -10f;

    private string lastRoomCode;

    private bool lastSpotifyConnected;

    private int lastTouchedItem = -1;

    private TMP_Text liveStatusLabel;

    private TMP_Text mediaStatusLabel;

    private Transform menuFace;

    private GameObject menuRoot;

    private float menuScale = 1f;

    private bool movingToTarget;

    private float nextAutoJoinAttempt;

    private float nextDynamicRefresh;

    private float nextMainRefresh;

    private float nextMediaRefresh;

    private bool openButtonDown;

    private float openProgress;

    private Transform outerEdge;

    private int pagedItemCount;

    private int pagedSettingCount;

    private TMP_Text pageTitleLabel;

    private Renderer platformIconRenderer;

    private TMP_Text platformLabel;

    private TMP_Text playbackLabel;

    private TMP_Text playerNameLabel;

    private int playerPage;

    private LineRenderer pointerLine;

    private TMP_Text progressLabel;

    private bool rectangleLayout;

    private TMP_Text repeatLabel;

    private TMP_Text reportStatusLabel;

    private string rigSnapshot = "";

    private int roomPage;

    private bool rotatingToTarget;

    private Transform secondaryEdge;

    private int selectedReportReason = -1;

    private int selectionGeneration;

    private long selectionToken;

    private int settingsPage;

    private TMP_Text shuffleLabel;

    private TMP_Text streamTitleLabel;

    private Player targetPlayer;

    private TMP_Text tierLabel;

    private TMP_Text tooltipBodyLabel;

    private int tooltipItemIndex = -1;

    private int tooltipRevision;

    private TMP_Text tooltipTitleLabel;

    private float tooltipVisibleAt;

    private TMP_Text trackLabel;

    private float transitionProgress;

    private GameObject transitionRing;

    private bool triggerDown;

    private Vector3 worldAnchorTarget;

    public static bool IsOpen
    {
        get
        {
            if ((Object)(object)Ins != (Object)null)
            {
                return Ins.isOpen;
            }

            return false;
        }
    }

    public string DesktopJoinStatus { get; private set; } = "";

    private int PageCount => Mathf.Max(1, (pagedItemCount + 10 - 1) / 10);

    private static Color InactiveBorderColor => new(Theme.Main.r, Theme.Main.g, Theme.Main.b, 0.35f);

    private bool UseLeftHand => Cfg.SwapHands.Value;

    private void Awake() => Ins = this;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Update()
    {
        if (PhotonNetwork.InRoom && (Object)(object)NetworkSystem.Instance != (Object)null)
        {
            lastRoomCode = NetworkSystem.Instance.RoomName;
        }

        TryAutoJoin();
        HandleOpenInput();
        if (!isOpen)
        {
            return;
        }

        if ((Object)(object)menuRoot == (Object)null)
        {
            CloseMenu();
        }
        else if (rectangleLayout == Theme.RectangleMenu)
        {
            GorillaTagger instance = GorillaTagger.Instance;
            if ((Object)(object)instance == (Object)null || (Object)(object)instance.mainCamera == (Object)null)
            {
                return;
            }

            Transform transform = instance.mainCamera.transform;
            if (!isClosing)
            {
                openProgress = Mathf.MoveTowards(openProgress, 1f, Time.deltaTime / 0.45f);
                float num = Theme.Snap(openProgress);
                menuFace.localScale = Vector3.one * num * menuScale;
                if (!rectangleLayout)
                {
                    innerBand.localRotation = Quaternion.Euler(0f, 0f, (1f - num) * -55f);
                    Transform  obj           = outerEdge;
                    Quaternion localRotation = secondaryEdge.localRotation = Quaternion.Euler(0f, 0f, (1f - num) * 90f);
                    obj.localRotation       = localRotation;
                    innerEdge.localRotation = Quaternion.Euler(0f, 0f, (1f - num) * -140f);
                }

                if ((Object)(object)transitionRing != (Object)null)
                {
                    transitionProgress                  += Time.deltaTime / 0.45f;
                    transitionRing.transform.localScale =  Vector3.one    * Mathf.Lerp(0.85f, 1.5f, Theme.Ease(transitionProgress));
                    Renderer componentInChildren = transitionRing.GetComponentInChildren<Renderer>();
                    if ((Object)(object)componentInChildren != (Object)null)
                    {
                        Color color = componentInChildren.sharedMaterial.color;
                        color.a                                  = 0.5f * (1f - transitionProgress);
                        componentInChildren.sharedMaterial.color = color;
                    }

                    if (transitionProgress >= 1f)
                    {
                        Object.Destroy((Object)(object)transitionRing);
                        transitionRing = null;
                    }
                }

                if (!((Object)(object)handAnchor != (Object)null))
                {
                    UpdateWorldAnchor(transform);
                }
                else
                {
                    UpdateHandAnchor(transform);
                }

                foreach (MenuItem item in menuItems)
                {
                    float num2 = Theme.Snap((Time.time - item.createdAt - item.animationDelay) / 0.3f);
                    item.root.transform.localScale = Vector3.one * Mathf.Max(0.001f, num2 * item.scale * (!item.highlighted || item.onDrag != null ? 1f : 1.07f));
                    if (item.visible)
                    {
                        continue;
                    }

                    item.root.transform.localPosition = Vector3.LerpUnclamped(item.targetPosition * 0.25f, item.targetPosition, num2);
                    float num3 = Mathf.Clamp01((Time.time - item.createdAt - item.animationDelay) / 0.18f);
                    for (int i = 0; i < item.renderers.Count; i++)
                    {
                        if (!((Object)(object)item.renderers[i] == (Object)null))
                        {
                            Color color2 = item.renderers[i].sharedMaterial.color;
                            color2.a                               = item.rendererBaseAlpha[i] * num3;
                            item.renderers[i].sharedMaterial.color = color2;
                        }
                    }

                    foreach (TMP_Text text in item.texts)
                    {
                        if ((Object)(object)text != (Object)null)
                        {
                            text.alpha = num3;
                        }
                    }

                    if (num2 >= 1f && num3 >= 1f)
                    {
                        item.visible = true;
                    }
                }

                HandleControllerBindings();
                if (isClosing)
                {
                    return;
                }

                HandleLaserInput(instance);
                UpdateTooltip();
                RefreshDynamicContent();
                if (targetPlayer != null && currentPage != Page.Main && currentPage != Page.Settings && currentPage != Page.Room && currentPage != Page.Music && Detect.Find(targetPlayer.UserId) == null)
                {
                    Notify.Send(targetPlayer.NickName + " left", Theme.Warn);
                    ShowPage(Page.Main, null);
                }

                if (currentPage == Page.Main && Time.time > nextMainRefresh)
                {
                    nextMainRefresh = Time.time + 1f;
                    if (BuildRigSnapshot() != rigSnapshot)
                    {
                        ShowPage(Page.Main, null);
                    }
                }

                if (currentPage == Page.Friends && selectionToken != Friends.Revision)
                {
                    ShowPage(Page.Friends, null);
                }

                if (currentPage == Page.Settings && selectionGeneration != (Friends.Busy ? 2 : Friends.ShareActivity ? 1 : 0))
                {
                    ShowPage(Page.Settings, null);
                }
            }
            else
            {
                closeProgress += Time.deltaTime;
                float   num4 = Theme.Ease(closeProgress / 0.4f);
                Vector3 val2 = (Object)(object)instance.leftHandTransform != (Object)null ? instance.leftHandTransform.position : transform.position;
                menuRoot.transform.position = Vector3.Lerp(menuRoot.transform.position, val2, num4);
                menuFace.localScale         = Vector3.one * Mathf.Lerp(openProgress, 0.02f, num4) * menuScale;
                menuFace.localRotation      = Quaternion.Euler(0f, 0f, num4 * 50f);
                pointerLine.enabled         = false;
                laserDot.gameObject.SetActive(false);
                if (closeProgress >= 0.42f)
                {
                    CloseMenu();
                }
            }
        }
        else
        {
            RefreshAppearance();
        }
    }

    private void OnDestroy()
    {
        CloseMenu();
        if ((Object)(object)Ins == (Object)(object)this)
        {
            Ins = null;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void DesktopJoin(string code)
    {
        if (!Manifest.Has("join_code") || string.IsNullOrWhiteSpace(code))
        {
            return;
        }

        code = code.Trim().ToUpperInvariant();
        if (code.Length > 10)
        {
            return;
        }

        string text = code;
        foreach (char c in text)
        {
            if ((c < 'A' || c > 'Z') && (c < '0' || c > '9'))
            {
                return;
            }
        }

        this.StartCoroutine(JoinRoom(code, null, true));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void DesktopHop()
    {
        if (Manifest.Has("lobby_hop"))
        {
            this.StartCoroutine(JoinRoom(null, FindPublicRoomTrigger(), true));
        }
    }

    private void UpdateHandAnchor(Transform arg1)
    {
        if (!Cfg.KeepMenuOpen.Value && !Disc.Glancing(arg1, handAnchor, anchoredToLeftHand))
        {
            BeginClose();

            return;
        }

        Vector3 val = handAnchor.position + Disc.PalmNormal(handAnchor, anchoredToLeftHand) * 0.16f;
        menuRoot.transform.position = Vector3.Lerp(menuRoot.transform.position, val, 1f - Mathf.Exp((0f - Time.deltaTime) * 16f));
        Vector3 val2 = menuRoot.transform.position - arg1.position;
        if (!(val2.sqrMagnitude < 0.0004f))
        {
            menuRoot.transform.rotation = Quaternion.Slerp(menuRoot.transform.rotation, Quaternion.LookRotation(val2, arg1.up), 1f - Mathf.Exp((0f - Time.deltaTime) * 11f));
        }
    }

    private void UpdateWorldAnchor(Transform arg1)
    {
        Vector3 val       = menuRoot.transform.position - arg1.position;
        float   magnitude = val.magnitude;
        if (!movingToTarget)
        {
            if (magnitude > 2.3f || magnitude < 0.5f || Vector3.Angle(arg1.forward, val) > 78f)
            {
                worldAnchorTarget = GameSettings.GroundPointAhead(arg1, 1.3f);
                movingToTarget    = true;
            }
        }
        else
        {
            Vector3 val2 = menuRoot.transform.position - worldAnchorTarget;
            if (val2.sqrMagnitude < 0.006f)
            {
                movingToTarget = false;
            }
        }

        menuRoot.transform.position = Vector3.Lerp(menuRoot.transform.position, worldAnchorTarget, 1f - Mathf.Exp((0f - Time.deltaTime) * 3.5f));
        Vector3 val3 = menuRoot.transform.position - arg1.position;
        val3.y = 0f;
        if (!(val3.sqrMagnitude < 0.09f))
        {
            Quaternion val4 = Quaternion.LookRotation(val3);
            float      num  = Quaternion.Angle(menuRoot.transform.rotation, val4);
            if (rotatingToTarget || !(num < 11f))
            {
                rotatingToTarget            = num > 2.5f;
                menuRoot.transform.rotation = Quaternion.RotateTowards(menuRoot.transform.rotation, val4, Mathf.Min(num * 4f, 110f) * Time.deltaTime);
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Open(Vector3 pos, Player target)
    {
        if (isOpen)
        {
            CloseMenu();
        }

        isOpen    = true;
        isClosing = false;
        ResetPointerState();
        handAnchor        = null;
        menuScale         = 1f;
        openProgress      = 0.05f;
        triggerDown       = true;
        worldAnchorTarget = pos;
        rotatingToTarget  = false;
        movingToTarget    = false;
        menuRoot          = new GameObject("zx_menu");
        Object.DontDestroyOnLoad((Object)(object)menuRoot);
        menuRoot.transform.position = pos;
        Vector3 val = pos - GorillaTagger.Instance.mainCamera.transform.position;
        val.y                       = 0f;
        menuRoot.transform.rotation = Quaternion.LookRotation(val.sqrMagnitude > 0.001f ? val : Vector3.forward);
        menuFace                    = new GameObject("face").transform;
        menuFace.SetParent(menuRoot.transform, false);
        menuFace.localScale = Vector3.one * 0.02f;
        Color val2 = default;
        val2            = new Color(Theme.Main.r, Theme.Main.g, Theme.Main.b, 0.16f);
        rectangleLayout = Theme.RectangleMenu;
        if (!rectangleLayout)
        {
            innerBand     = Theme.Ring(menuFace, "band",     0.415f, 0.55f,  val2,       2992).transform;
            outerEdge     = Theme.Ring(menuFace, "edgeOut",  0.55f,  0.558f, Theme.Main, 2993, 8f,   152f).transform;
            secondaryEdge = Theme.Ring(menuFace, "edgeOut2", 0.55f,  0.558f, Theme.Main, 2993, 188f, 152f).transform;
            innerEdge     = Theme.Ring(menuFace, "edgeIn",   0.408f, 0.414f, Theme.Soft, 2993, 250f, 220f).transform;
            Theme.Ring(menuFace, "glow", 0.4f, 0.6f, new Color(val2.r, val2.g, val2.b, 0.05f), 2990);
        }
        else
        {
            Theme.Card(menuFace, "hologram", 1.16f, 1.26f, Theme.Main, 2990);
            Theme.Quad(menuFace, "headerline", 1.06f, 0.003f, Theme.Holo(Theme.Soft, 2993)).transform.localPosition = new Vector3(0f, 0.43f, 0f);
        }

        CreateTransitionRing();
        pointerLine = new GameObject("zx_laser").AddComponent<LineRenderer>();
        Object.DontDestroyOnLoad((Object)(object)pointerLine.gameObject);
        pointerLine.startWidth = 0.004f;
        pointerLine.endWidth   = 0.0015f;
        RenderResources.Assign(pointerLine, Theme.Holo(new Color(val2.r, val2.g, val2.b, 0.6f), 3010));
        GameObject val3 = Theme.Ring(null, "zx_dot", 0f, 0.011f, Theme.Soft, 3011);
        Object.DontDestroyOnLoad((Object)(object)val3);
        laserDot                                  = val3.transform;
        tooltipTitleLabel                         = Theme.Text(menuFace, "tooltip", "", 0.022f, Theme.White);
        tooltipTitleLabel.richText                = false;
        tooltipTitleLabel.transform.localPosition = new Vector3(0f, -0.665f, -0.015f);
        ShowPage(target != null ? Page.Player : Page.Main, target);
        Theme.OpenSound();
        Theme.Haptic(true, 0.3f, 0.05f);
    }

    public void OpenOnHand(Transform palm, bool left)
    {
        Open(palm.position + Disc.PalmNormal(palm, left) * 0.16f, null);
        handAnchor         = palm;
        anchoredToLeftHand = left;
        menuScale          = 0.3f;
    }

    private void BeginClose()
    {
        if (!isClosing)
        {
            isClosing          = true;
            closeProgress      = 0f;
            inputCooldownUntil = Time.time + 0.7f;
            Theme.CloseSound();
            Theme.Haptic(false, 0.4f, 0.06f);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void CreateTransitionRing()
    {
        if ((Object)(object)transitionRing != (Object)null)
        {
            Object.Destroy((Object)(object)transitionRing);
        }

        if (!rectangleLayout)
        {
            transitionRing = new GameObject("shock");
            transitionRing.transform.SetParent(menuFace, false);
            Theme.Ring(transitionRing.transform, "ring", 0.545f, 0.565f, new Color(Theme.Soft.r, Theme.Soft.g, Theme.Soft.b, 0.5f), 2994);
            transitionProgress = 0f;
        }
    }

    private static bool IsControllerButtonPressed(ControllerInputPoller arg1, int arg2) =>
            arg2 switch
            {
                    0     => arg1.leftControllerPrimaryButton,
                    1     => arg1.leftControllerSecondaryButton,
                    2     => arg1.rightControllerPrimaryButton,
                    var _ => arg1.rightControllerSecondaryButton,
            };

    private void TryAutoJoin()
    {
        if (!Cfg.AutoJoin.Value || !Manifest.Has("lobby_hop") || isJoiningRoom || Time.time < nextAutoJoinAttempt || PhotonNetwork.InRoom || !PhotonNetwork.IsConnectedAndReady)
        {
            return;
        }

        nextAutoJoinAttempt = Time.time + 3f;
        GorillaNetworkJoinTrigger publicRoomTrigger = FindPublicRoomTrigger();
        if ((Object)(object)publicRoomTrigger != (Object)null)
        {
            StartCoroutine(JoinRoom(null, publicRoomTrigger));
        }
    }

    private void HandleOpenInput()
    {
        ControllerInputPoller instance = ControllerInputPoller.instance;
        if ((Object)(object)instance == (Object)null)
        {
            return;
        }

        bool flag;
        bool num = (flag = IsControllerButtonPressed(instance, Mathf.Clamp(Cfg.OpenBtn.Value, 0, 3))) && !openButtonDown;
        openButtonDown = flag;
        if (num && !isOpen && !(Time.time < inputCooldownUntil))
        {
            GorillaTagger instance2 = GorillaTagger.Instance;
            if (!((Object)(object)instance2 == (Object)null) && !((Object)(object)instance2.mainCamera == (Object)null))
            {
                Open(GameSettings.GroundPointAhead(instance2.mainCamera.transform, 1.3f), null);
            }
        }
    }

    private void HandleControllerBindings()
    {
        ControllerInputPoller instance = ControllerInputPoller.instance;
        if ((Object)(object)instance == (Object)null)
        {
            return;
        }

        if (buttonBindingMode != 0)
        {
            for (int i = 0; i < 4; i++)
            {
                bool flag;
                if ((flag = IsControllerButtonPressed(instance, i)) && !controllerButtons[i])
                {
                    if (buttonBindingMode == 1)
                    {
                        Cfg.CloseBtn.Value = i;
                    }
                    else
                    {
                        Cfg.OpenBtn.Value = i;
                    }

                    buttonBindingMode = 0;
                    openButtonDown    = true;
                    Theme.ClickSound();
                    Theme.Haptic(i < 2, 0.5f, 0.05f);
                    ShowPage(Page.Settings, null);
                }

                controllerButtons[i] = flag;
            }

            return;
        }

        for (int j = 0; j < 4; j++)
        {
            controllerButtons[j] = IsControllerButtonPressed(instance, j);
        }

        bool flag2;
        if ((flag2 = IsControllerButtonPressed(instance, Mathf.Clamp(Cfg.CloseBtn.Value, 0, 3))) && !closeButtonDown)
        {
            if (Time.time - lastCloseTapTime < 0.4f)
            {
                BeginClose();
            }

            lastCloseTapTime = Time.time;
        }

        closeButtonDown = flag2;
    }

    private void CloseMenu()
    {
        colorPicker?.Dispose();
        colorPicker = null;
        isClosing   = false;
        isOpen      = false;
        ResetPointerState();
        buttonBindingMode = 0;
        handAnchor        = null;
        menuScale         = 1f;
        Outline.Clear();
        menuItems.Clear();
        transitionRing = null;
        ClearPageReferences();
        if ((Object)(object)pointerLine != (Object)null)
        {
            Object.Destroy((Object)(object)pointerLine.gameObject);
        }

        if ((Object)(object)laserDot != (Object)null)
        {
            Object.Destroy((Object)(object)laserDot.gameObject);
        }

        if ((Object)(object)menuRoot != (Object)null)
        {
            Object.Destroy((Object)(object)menuRoot);
        }
    }

    private void ClearPageReferences()
    {
        tooltipBodyLabel   = friendStatusLabel = null;
        clientIconRenderer = null;
        tooltipItemIndex   = -1;
        if ((Object)(object)tooltipTitleLabel != (Object)null)
        {
            tooltipTitleLabel.text = "";
        }

        pageTitleLabel       = playerNameLabel = platformLabel = tierLabel = reportStatusLabel = mediaStatusLabel = trackLabel = artistLabel = playbackLabel = shuffleLabel = repeatLabel = progressLabel = liveStatusLabel = null;
        chatMessageLabels    = null;
        streamTitleLabel     = null;
        platformIconRenderer = null;
    }

    private void ResetPointerState()
    {
        if (hoveredItemIndex >= 0)
        {
            SetItemHighlighted(hoveredItemIndex, false);
        }

        lastTouchedItem  = -1;
        hoveredItemIndex = -1;
        draggedItemIndex = -1;
        hoverExitDelay   = 0f;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ShowPage(Page arg1, Player arg2)
    {
        colorPicker?.Dispose();
        colorPicker = null;
        currentPage = arg1;
        if (arg2 != null || arg1 == Page.Main)
        {
            targetPlayer = arg2;
        }

        ResetPointerState();
        Outline.Clear();
        menuItems.Clear();
        ClearPageReferences();
        if ((Object)(object)contentRoot != (Object)null)
        {
            Object.Destroy((Object)(object)contentRoot);
        }

        contentRoot = new GameObject("content");
        contentRoot.transform.SetParent(menuFace, false);
        if (openProgress >= 1f)
        {
            CreateTransitionRing();
        }

        switch (arg1)
        {
            case Page.Main:
                BuildMainPage();

                break;

            case Page.Player:
                BuildPlayerDetailsPage();

                break;

            case Page.ModList:
                BuildDetectionListPage();

                break;

            case Page.Report:
                BuildReportPage();

                break;

            case Page.Settings:
                BuildSettingsPage();

                break;

            default:
                BuildRoomPage();

                break;

            case Page.JoinCode:
                BuildJoinCodePage();

                break;

            case Page.Music:
                BuildDesktopMusicPage();

                break;

            case Page.Friends:
                BuildFriendsPage();

                break;

            case Page.CustomTheme:
                BuildCustomThemePage();

                break;

            case Page.Spotify:
                BuildSpotifyPage();

                break;

            case Page.Chat:
                BuildLiveChatPage();

                break;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void BuildMainPage()
    {
        rigSnapshot = BuildRigSnapshot();
        if (rectangleLayout)
        {
            BuildPlayerPage();

            return;
        }

        AddPlayerPreview((Object)(object)GorillaTagger.Instance != (Object)null ? GorillaTagger.Instance.offlineVRRig : null, PhotonNetwork.InRoom ? PhotonNetwork.LocalPlayer.NickName : "YOU");
        bool        flag       = Manifest.Has("spotify");
        bool        flag2      = Manifest.Has("youtube_chat");
        bool        isOpen     = Manifest.Has("friends");
        int         num        = 3 + (flag2 ? 1 : 0) + (isOpen ? 1 : 0);
        float       num2       = num > 4 ? 260f : 300f;
        List<VRRig> remoteRigs = GetRemoteRigs();
        int         count      = remoteRigs.Count;
        float       arg        = count <= 9 ? 1f : 0.72f;
        for (int i = 0; i < count; i++)
        {
            AddPlayerButton(remoteRigs[i], (0f - num2) / 2f + num2 * (i + 0.5f) / count, arg, i);
        }

        if (count == 0)
        {
            Theme.Text(contentRoot.transform, "empty", "NOBODY ELSE HERE", 0.032f, Theme.DimText).transform.localPosition = PolarPosition(0f, 0.5f) + ContentDepthOffset;
        }

        if (Manifest.Has("mic"))
        {
            AddCard("mic", new Vector3(-0.275f, 0.015f, 0f), "MIC", GameSettings.GetPushToTalkLabel(), delegate
                                                                                                       {
                                                                                                           GameSettings.CyclePushToTalkMode();
                                                                                                           ShowPage(Page.Main, null);
                                                                                                       }, 0.2f, 0.082f);
        }

        if (Manifest.Has("outfit"))
        {
            AddCard("fit", new Vector3(0.275f, 0.015f, 0f), "OUTFIT", GameSettings.GetOutfitLabel(), delegate
                                                                                                     {
                                                                                                         GameSettings.CycleOutfit(true);
                                                                                                         ShowPage(Page.Main, null);
                                                                                                     }, 0.2f, 0.082f);
        }

        float num3 = num > 4 ? 17f : 20f;
        float arg2 = 180f - num3 * (num - 1) / 2f;
        AddNavigationIcon("settings", ref arg2, num3, Theme.SettingIcon,               "SETTINGS", Page.Settings);
        AddNavigationIcon("music",    ref arg2, num3, flag ? Theme.SpotifyIcon : null, "MUSIC",    Page.Music);
        AddNavigationIcon("room",     ref arg2, num3, Theme.RoomIcon,                  "ROOM",     Page.Room);
        if (flag2)
        {
            AddNavigationIcon("chat", ref arg2, num3, Theme.ChatIcon, "CHAT", Page.Chat);
        }

        if (isOpen)
        {
            AddNavigationIcon("friends", ref arg2, num3, Theme.FriendIcon, "FRIENDS", Page.Friends);
        }
    }

    private void AddNavigationIcon(string arg1, ref float arg2, float arg3, Texture arg4, string arg5, Page arg6)
    {
        AddNavigationButton(arg1, arg2, arg4, arg5, delegate
                                                    {
                                                        ShowPage(arg6, null);
                                                    });

        arg2 += arg3;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void BuildPlayerPage()
    {
        AddPageTitle("SENTINEL / PLAYERS", Theme.Soft);
        List<VRRig> remoteRigs = GetRemoteRigs();
        int         index1     = Mathf.Max(1, (remoteRigs.Count + 9) / 10);
        playerPage = Mathf.Clamp(playerPage, 0, index1 - 1);
        for (int i = playerPage * 10; i < Mathf.Min(remoteRigs.Count, playerPage * 10 + 10); i++)
        {
            AddPlayerButton(remoteRigs[i], 0f, 1f, i % 10);
        }

        if (remoteRigs.Count == 0)
        {
            Theme.Text(contentRoot.transform, "empty", "NOBODY ELSE HERE", 0.032f, Theme.DimText).transform.localPosition = new Vector3(0f, 0.12f, 0f);
        }

        if (index1 > 1)
        {
            AddCard("playerspage", new Vector3(0f, -0.31f, 0f), "", "PLAYERS " + (playerPage + 1) + "/" + index1, delegate
                                                                                                                  {
                                                                                                                      playerPage = (playerPage + 1) % index1;
                                                                                                                      ShowPage(Page.Main, null);
                                                                                                                  }, 0.3f, 0.06f);
        }

        if (Manifest.Has("mic"))
        {
            AddCard("mic", new Vector3(-0.28f, -0.4f, 0f), "MIC", GameSettings.GetPushToTalkLabel(), delegate
                                                                                                     {
                                                                                                         GameSettings.CyclePushToTalkMode();
                                                                                                         ShowPage(Page.Main, null);
                                                                                                     }, 0.26f, 0.075f);
        }

        if (Manifest.Has("outfit"))
        {
            AddCard("fit", new Vector3(0.28f, -0.4f, 0f), "OUTFIT", GameSettings.GetOutfitLabel(), delegate
                                                                                                   {
                                                                                                       GameSettings.CycleOutfit(true);
                                                                                                       ShowPage(Page.Main, null);
                                                                                                   }, 0.26f, 0.075f);
        }

        cardSequence = 0;
        AddDetectionLink("settings", "SETTINGS", Page.Settings);
        AddDetectionLink("music",    "MUSIC",    Page.Music);
        AddDetectionLink("room",     "ROOM",     Page.Room);
        if (Manifest.Has("youtube_chat"))
        {
            AddDetectionLink("chat", "CHAT", Page.Chat);
        }

        if (Manifest.Has("friends"))
        {
            AddDetectionLink("friends", "FRIENDS", Page.Friends);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AddDetectionLink(string arg1, string arg2, Page arg3)
    {
        int   num  = 3 + (Manifest.Has("youtube_chat") ? 1 : 0) + (Manifest.Has("friends") ? 1 : 0);
        float num2 = (cardSequence++ - (num - 1) * 0.5f) * 0.21f;
        AddCard(arg1, new Vector3(num2, -0.53f, 0f), "", arg2, delegate
                                                               {
                                                                   ShowPage(arg3, null);
                                                               }, 0.2f, 0.064f);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void BuildPlayerDetailsPage()
    {
        VRRig val = Detect.RigOf(targetPlayer);
        AddPlayerPreview(val, targetPlayer != null ? targetPlayer.NickName : "?");
        Scan scan = Detect.Get(targetPlayer);
        pageTitleLabel = AddCard("fps", PolarPosition(-52f, 0.5f), "FPS", "...", null, 0.28f).valueText;
        string   text     = Detect.PlatformOf(val);
        MenuItem menuItem = AddCard("plat", PolarPosition(52f, 0.5f), "PLATFORM", text.ToUpper(), null, 0.28f);
        tooltipBodyLabel                           = menuItem.valueText;
        clientIconRenderer                         = Theme.Quad(menuItem.root.transform, "icon", 0.034f, 0.034f, Theme.Holo(Color.white, 2997, Detect.PlatformIcon(text))).GetComponent<Renderer>();
        clientIconRenderer.transform.localPosition = new Vector3(-0.108f, -0.014f, -0.002f);
        playerNameLabel = AddCard("mods", PolarPosition(-100f, 0.5f), "MODS", scan.Mods.Count.ToString(), delegate
                                                                                                          {
                                                                                                              detectionKind = Kind.Mod;
                                                                                                              ShowPage(Page.ModList, targetPlayer);
                                                                                                          }, 0.28f).valueText;

        playerNameLabel.color = scan.Mods.Count > 0 ? Theme.Warn : Theme.White;
        platformLabel = AddCard("cheats", PolarPosition(100f, 0.5f), "CHEATS", scan.Cheats.Count.ToString(), delegate
                                                                                                             {
                                                                                                                 detectionKind = Kind.Cheat;
                                                                                                                 ShowPage(Page.ModList, targetPlayer);
                                                                                                             }, 0.28f).valueText;

        platformLabel.color     = scan.Cheats.Count > 0 ? Theme.Bad : Theme.White;
        tierLabel               = AddCard("created", PolarPosition(-143f, 0.5f),   "CREATED",  Detect.CreatedDate(targetPlayer != null ? targetPlayer.UserId : null), null, 0.28f).valueText;
        friendStatusLabel       = AddCard("zx",      PolarPosition(-26f,  0.585f), "SENTINEL", GetTooltipText(),                                                      null, 0.27f).valueText;
        friendStatusLabel.color = Theme.Main;
        bool flag = targetPlayer != null && reportedUserIds.Contains(targetPlayer.UserId);
        AddCard("report", PolarPosition(180f, 0.5f), "REPORT", flag ? "REPORTED" : "SELECT", flag ? null : delegate
                                                                                                           {
                                                                                                               ShowPage(Page.Report, targetPlayer);
                                                                                                           }, 0.26f).valueText.color = flag ? Theme.Bad : Theme.DimText;

        bool flag2 = IsTargetMuted();
        reportStatusLabel       = AddCard("mute", PolarPosition(143f, 0.5f), "MUTE", flag2 ? "MUTED" : "OFF", ToggleTargetMute, 0.28f).valueText;
        reportStatusLabel.color = flag2 ? Theme.Bad : Theme.White;
        if (Manifest.Has("friends") && targetPlayer != null)
        {
            bool isOpen = Friends.IsFriend(targetPlayer.UserId);
            AddCard("friend", PolarPosition(26f, 0.585f), isOpen ? "UNFRIEND" : "FRIEND", Friends.Busy ? "SAVING..." : isOpen ? "REMOVE" : "REQUEST", delegate
                                                                                                                                                      {
                                                                                                                                                          if (Friends.IsFriend(targetPlayer.UserId))
                                                                                                                                                          {
                                                                                                                                                              Friends.Remove(targetPlayer.UserId);
                                                                                                                                                          }
                                                                                                                                                          else
                                                                                                                                                          {
                                                                                                                                                              Friends.Add(targetPlayer.UserId, targetPlayer.NickName);
                                                                                                                                                          }

                                                                                                                                                          ShowPage(Page.Player, targetPlayer);
                                                                                                                                                      }, 0.27f, 0.064f).valueText.color = isOpen ? Theme.Bad : Theme.Good;
        }

        AddBackButton(PolarPosition(0f, 0.5f), delegate
                                               {
                                                   ShowPage(Page.Main, null);
                                               });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void BuildDetectionListPage()
    {
        Scan        scan = Detect.Get(targetPlayer);
        List<Entry> list = detectionKind                                                                                                           == Kind.Cheat ? scan.Cheats : scan.Mods;
        AddPageTitle((detectionKind == Kind.Cheat ? "CHEATS - " : "MODS - ") + (targetPlayer != null ? targetPlayer.NickName : "?"), detectionKind == Kind.Cheat ? Theme.Bad : Theme.Warn);
        int  num  = Mathf.Min(list.Count, 14);
        bool flag = num > 7;
        for (int i = 0; i < num; i++)
        {
            float num2 = flag ? i % 2 == 0 ? -0.155f : 0.155f : 0f;
            float num3 = 0.21f - (flag ? i / 2 : i) * 0.07f;
            AddCard("e" + i, new Vector3(num2, num3, 0f), "", list[i].Name, null, flag ? 0.29f : 0.34f, 0.06f).valueText.color = list[i].Kind == Kind.Cheat ? Theme.Bad : list[i].Kind == Kind.Unknown ? Theme.Warn : Theme.Good;
        }

        if (list.Count > num)
        {
            Theme.Text(contentRoot.transform, "more", "+" + (list.Count - num) + " MORE", 0.026f, Theme.DimText).transform.localPosition = new Vector3(0f, -0.3f, 0f) + ContentDepthOffset;
        }

        if (list.Count == 0)
        {
            Theme.Text(contentRoot.transform, "none", "CLEAN", 0.04f, Theme.Good).transform.localPosition = ContentDepthOffset;
        }

        AddBackButton(PolarPosition(180f, 0.5f), delegate
                                                 {
                                                     ShowPage(Page.Player, targetPlayer);
                                                 });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void BuildReportPage()
    {
        string text = targetPlayer != null ? targetPlayer.NickName : "?";
        if (selectedReportReason < 0)
        {
            AddPageTitle("REPORT " + text, Theme.Bad);
            AddCard("cheating", new Vector3(0f, 0.1f, 0f), "", "CHEATING", delegate
                                                                           {
                                                                               selectedReportReason = 1;
                                                                               ShowPage(Page.Report, targetPlayer);
                                                                           }, 0.3f, 0.078f);

            AddCard("toxicity", Vector3.zero, "", "TOXICITY", delegate
                                                              {
                                                                  selectedReportReason = 2;
                                                                  ShowPage(Page.Report, targetPlayer);
                                                              }, 0.3f, 0.078f);

            AddCard("hate", new Vector3(0f, -0.1f, 0f), "", "HATE SPEECH", delegate
                                                                           {
                                                                               selectedReportReason = 0;
                                                                               ShowPage(Page.Report, targetPlayer);
                                                                           }, 0.3f, 0.078f);

            AddBackButton(new Vector3(0f, -0.23f, 0f), delegate
                                                       {
                                                           selectedReportReason = -1;
                                                           ShowPage(Page.Player, targetPlayer);
                                                       });
        }
        else
        {
            AddPageTitle("REPORT " + text, Theme.White);
            string text2 = selectedReportReason == 1 ? "CHEATING" : selectedReportReason == 2 ? "TOXICITY" : "HATE SPEECH";
            Theme.Text(contentRoot.transform, "sub", "FOR " + text2 + "?", 0.03f, Theme.DimText).transform.localPosition          = new Vector3(0f, 0.1f, 0f) + ContentDepthOffset;
            AddCard("confirm", new Vector3(-0.12f, -0.03f, 0f), "", "CONFIRM", SubmitPlayerReport, 0.22f, 0.078f).valueText.color = Theme.Bad;
            AddCard("cancel", new Vector3(0.12f, -0.03f, 0f), "", "CANCEL", delegate
                                                                            {
                                                                                selectedReportReason = -1;
                                                                                ShowPage(Page.Player, targetPlayer);
                                                                            }, 0.22f, 0.078f);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void BuildSettingsPage()
    {
        selectionGeneration = Friends.Busy ? 2 : Friends.ShareActivity ? 1 : 0;
        AddPageTitle("SETTINGS", Theme.Soft);
        BeginPagedGrid(settingsPage);
        AddPagedSetting("theme", "THEME", Theme.Name(Cfg.Theme.Value), delegate
                                                                       {
                                                                           Cfg.Theme.Value = (Cfg.Theme.Value + 1) % Theme.Count;
                                                                           Theme.Apply(Cfg.Theme.Value);
                                                                           Plugin.Ins.Disc.Retheme();
                                                                           ReopenSettingsAfterThemeChange();
                                                                       });

        if (Manifest.Has("notify"))
        {
            AddPagedSetting("notifs", "NOTIFS", Cfg.NotifMode.Value.ToUpper(), [MethodImpl(MethodImplOptions.NoInlining)]() =>
                                                                               {
                                                                                   Cfg.NotifMode.Value = Cfg.NotifMode.Value == "all" ? "cheats" : Cfg.NotifMode.Value == "cheats" ? "off" : "all";
                                                                                   ShowPage(Page.Settings, null);
                                                                               });
        }

        AddBooleanSetting("sounds", "SOUNDS", Cfg.Sounds);
        if (Manifest.Has("menu_sfx"))
        {
            AddPagedSetting("sfxpack", "SOUND PACK", Theme.SfxName(Cfg.MenuSfx.Value), delegate
                                                                                       {
                                                                                           Cfg.MenuSfx.Value = (Cfg.MenuSfx.Value + 1) % 4;
                                                                                           Theme.ClickSound();
                                                                                           ShowPage(Page.Settings, null);
                                                                                       });
        }

        if (Manifest.Has("menu_rectangle"))
        {
            AddPagedSetting("layout", "MENU SHAPE", Theme.RectangleMenu ? "RECTANGLE" : "DISC", delegate
                                                                                                {
                                                                                                    Cfg.MenuLayout.Value = !Theme.RectangleMenu ? 1 : 0;
                                                                                                    Plugin.Ins.Disc?.Retheme();
                                                                                                    ReopenSettingsAfterThemeChange();
                                                                                                });
        }

        AddBooleanSetting("tooltips",  "TOOLTIPS",       Cfg.Tooltips);
        AddBooleanSetting("keepopen",  "KEEP MENU OPEN", Cfg.KeepMenuOpen);
        AddBooleanSetting("playerray", "POINTER SELECT", Cfg.PlayerRay);
        AddPagedSetting("friendprivacy", "SHARE FRIEND ACTIVITY", Friends.Busy ? "SAVING..." : Friends.ShareActivity ? "ON" : "OFF", delegate
                                                                                                                                     {
                                                                                                                                         Friends.SetSharing(!Friends.ShareActivity);
                                                                                                                                         ShowPage(Page.Settings, null);
                                                                                                                                     });

        AddBooleanSetting("tags",    "NAMETAGS",       Cfg.Tags);
        AddBooleanSetting("tagfps",  "TAG FPS",        Cfg.TagFps);
        AddBooleanSetting("tagplat", "TAG PLATFORM",   Cfg.TagPlat);
        AddBooleanSetting("tagzx",   "CLIENT CHECKER", Cfg.TagMenu);
        if (Manifest.Has("custom_tags"))
        {
            AddPagedSetting("tagfont", "TAG FONT", Theme.TagFontName(Cfg.TagFont.Value), delegate
                                                                                         {
                                                                                             Cfg.TagFont.Value = (Cfg.TagFont.Value + 1) % 4;
                                                                                             ShowPage(Page.Settings, null);
                                                                                         });

            AddPagedSetting("tagsize", "TAG SIZE", Theme.TagSizeName(Cfg.TagSize.Value), delegate
                                                                                         {
                                                                                             Cfg.TagSize.Value = (Cfg.TagSize.Value + 1) % 3;
                                                                                             ShowPage(Page.Settings, null);
                                                                                         });

            AddPagedSetting("tagcolor", "TAG COLOR", Theme.TagColorName(Cfg.TagColor.Value), delegate
                                                                                             {
                                                                                                 Cfg.TagColor.Value = (Cfg.TagColor.Value + 1) % 3;
                                                                                                 ShowPage(Page.Settings, null);
                                                                                             });

            AddBooleanSetting("tagbold", "BOLD NAMES", Cfg.TagBold);
        }

        if (Manifest.Has("board_colors"))
        {
            AddBooleanSetting("board", "BOARD COLORS", Cfg.BoardColors);
        }

        if (Manifest.Has("frame"))
        {
            AddBooleanSetting("gesture", "GESTURE", Cfg.Gesture);
        }

        AddBooleanSetting("onehand", "ONE HAND",   Cfg.OneHand);
        AddBooleanSetting("touch",   "HAND TOUCH", Cfg.Touch);
        AddBooleanSetting("swap",    "SWAP HANDS", Cfg.SwapHands);
        if (Manifest.Has("net"))
        {
            AddBooleanSetting("broadcast", "BROADCAST", Cfg.Broadcast);
        }

        if (Manifest.Has("lobby_hop"))
        {
            AddBooleanSetting("autojoin", "AUTO JOIN", Cfg.AutoJoin);
        }

        if (Manifest.Has("hand_menu"))
        {
            AddBooleanSetting("handmenu", "HAND MENU", Cfg.HandMenu);
        }

        if (Manifest.Has("custom_theme"))
        {
            AddPagedSetting("customtheme", "CUSTOM COLOR", "EDIT", delegate
                                                                   {
                                                                       ShowPage(Page.CustomTheme, null);
                                                                   });
        }

        AddPagedSetting("openkey", "OPEN KEYBIND", buttonBindingMode == 2 ? "PRESS.." : BtnNames[Mathf.Clamp(Cfg.OpenBtn.Value, 0, 3)], delegate
                                                                                                                                        {
                                                                                                                                            buttonBindingMode = 2;
                                                                                                                                            ShowPage(Page.Settings, null);
                                                                                                                                        });

        AddPagedSetting("closekey", "CLOSE KEYBIND", buttonBindingMode == 1 ? "PRESS.." : BtnNames[Mathf.Clamp(Cfg.CloseBtn.Value, 0, 3)], delegate
                                                                                                                                           {
                                                                                                                                               buttonBindingMode = 1;
                                                                                                                                               ShowPage(Page.Settings, null);
                                                                                                                                           });

        AddPageControls(settingsPage, delegate
                                      {
                                          settingsPage = (settingsPage + 1) % PageCount;
                                          ShowPage(Page.Settings, null);
                                      });
    }

    private void ReopenSettingsAfterThemeChange()
    {
        RefreshAppearance();
        ShowPage(Page.Settings, null);
    }

    public void RefreshAppearance()
    {
        if (isOpen && !((Object)(object)menuRoot == (Object)null))
        {
            Page      arg      = currentPage;
            Player    arg2     = targetPlayer;
            Transform val      = handAnchor;
            bool      left     = anchoredToLeftHand;
            Vector3   position = menuRoot.transform.position;
            CloseMenu();
            if ((Object)(object)val != (Object)null)
            {
                OpenOnHand(val, left);
            }
            else
            {
                Open(position, null);
            }

            ShowPage(arg, arg2);
        }
    }

    private void BeginPagedGrid(int arg1)
    {
        pagedSettingCount = pagedItemCount = 0;
        activeGridPage    = arg1;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AddBooleanSetting(string arg1, string arg2, ConfigEntry<bool> arg3) =>
            AddPagedSetting(arg1, arg2, arg3.Value ? "ON" : "OFF", delegate
                                                                   {
                                                                       arg3.Value = !arg3.Value;
                                                                       ShowPage(currentPage, targetPlayer);
                                                                   });

    private void AddPagedSetting(string arg1, string arg2, string arg3, Action arg4)
    {
        int num = pagedSettingCount++;
        pagedItemCount = pagedSettingCount;
        if (num / 10 == activeGridPage)
        {
            int num2 = num % 10;
            AddCard(arg1, new Vector3(num2 % 2 == 0 ? -0.16f : 0.16f, 0.205f - num2 / 2 * 0.095f, 0f), arg2, arg3, arg4, 0.3f, 0.085f);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AddPageControls(int arg1, Action arg2)
    {
        if (PageCount > 1)
        {
            AddCard("page", new Vector3(-0.12f, -0.275f, 0f), "", "PAGE " + (arg1 + 1) + "/" + PageCount, arg2, 0.22f, 0.064f);
        }

        AddBackButton(new Vector3(PageCount > 1 ? 0.12f : 0f, -0.275f, 0f), delegate
                                                                            {
                                                                                ShowPage(Page.Main, null);
                                                                            });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void BuildRoomPage()
    {
        string text = "NOT IN ROOM";
        int    num  = 0;
        if (PhotonNetwork.InRoom && (Object)(object)NetworkSystem.Instance != (Object)null)
        {
            text = NetworkSystem.Instance.RoomName;
            num  = NetworkSystem.Instance.RoomPlayerCount;
        }

        AddPageTitle(text + (num > 0 ? "  (" + num + ")" : ""), Theme.Soft);
        BeginPagedGrid(roomPage);
        AddPagedSetting("leave", "SESSION", "DISCONNECT", delegate
                                                          {
                                                              if (PhotonNetwork.InRoom && (Object)(object)NetworkSystem.Instance != (Object)null)
                                                              {
                                                                  NetworkSystem.Instance.ReturnToSinglePlayer();
                                                              }

                                                              ShowPage(Page.Room, null);
                                                          });

        AddPagedSetting("rejoin", "LAST ROOM", "RECONNECT", delegate
                                                            {
                                                                this.StartCoroutine(JoinRoom(lastRoomCode, null));
                                                            });

        AddPagedSetting("hop", "PUBLIC", "LOBBY HOP", delegate
                                                      {
                                                          this.StartCoroutine(JoinRoom(null, FindPublicRoomTrigger()));
                                                      });

        if (Manifest.Has("join_code"))
        {
            AddPagedSetting("joincode", "JOIN CODE", "OPEN", delegate
                                                             {
                                                                 DesktopJoinStatus = "";
                                                                 ShowPage(Page.JoinCode, null);
                                                             });
        }

        AddPagedSetting("queue", "QUEUE", GameSettings.GetQueueLabel(), delegate
                                                                        {
                                                                            GameSettings.CycleQueue();
                                                                            ShowPage(Page.Room, null);
                                                                        });

        AddPagedSetting("mode", "GAMEMODE", GameSettings.GetGameModeLabel(), delegate
                                                                             {
                                                                                 GameSettings.CycleGameMode();
                                                                                 ShowPage(Page.Room, null);
                                                                             });

        if (Manifest.Has("time"))
        {
            AddPagedSetting("time", "TIME", GameSettings.GetTimeOfDayLabel(), delegate
                                                                              {
                                                                                  GameSettings.CycleTimeOfDay(1);
                                                                                  ShowPage(Page.Room, null);
                                                                              });

            AddPagedSetting("timeback", "TIME BACK", "REWIND", delegate
                                                               {
                                                                   GameSettings.CycleTimeOfDay(-1);
                                                                   ShowPage(Page.Room, null);
                                                               });

            AddPagedSetting("timelock", "LOCK TIME", GameSettings.IsTimeOverrideActive ? "ON" : "OFF", delegate
                                                                                                       {
                                                                                                           GameSettings.ToggleTimeLock();
                                                                                                           ShowPage(Page.Room, null);
                                                                                                       });
        }

        if (Manifest.Has("mic"))
        {
            AddPagedSetting("voice", "VOICE", GameSettings.GetVoiceChatLabel(), delegate
                                                                                {
                                                                                    GameSettings.ToggleVoiceChat();
                                                                                    ShowPage(Page.Room, null);
                                                                                });

            AddPagedSetting("ptt", "MIC MODE", GameSettings.GetPushToTalkLabel(), delegate
                                                                                  {
                                                                                      GameSettings.CyclePushToTalkMode();
                                                                                      ShowPage(Page.Room, null);
                                                                                  });
        }

        AddPageControls(roomPage, delegate
                                  {
                                      roomPage = (roomPage + 1) % PageCount;
                                      ShowPage(Page.Room, null);
                                  });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void BuildJoinCodePage()
    {
        if (!Manifest.Has("join_code"))
        {
            ShowPage(Page.Room, null);

            return;
        }

        AddPageTitle("JOIN CODE", Theme.Soft);
        joinCodeLabel                           = Theme.Text(contentRoot.transform, "code", joinCode.Length == 0 ? "ENTER CODE" : joinCode, 0.034f, Theme.White);
        joinCodeLabel.transform.localPosition   = new Vector3(0f, 0.255f, 0f) + ContentDepthOffset;
        joinStatusLabel                         = Theme.Text(contentRoot.transform, "state", DesktopJoinStatus, 0.019f, Theme.DimText);
        joinStatusLabel.transform.localPosition = new Vector3(0f, 0.215f, 0f) + ContentDepthOffset;
        for (int i = 0; i < "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".Length; i++)
        {
            char char0 = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"[i];
            int  num   = i % 6;
            int  num2  = i / 6;
            AddCard("key" + char0, new Vector3(-0.265f + num * 0.106f, 0.145f - num2 * 0.066f, 0f), "", char0.ToString(), delegate
                                                                                                                          {
                                                                                                                              joinCode          = RoomCode.Add(joinCode, char0);
                                                                                                                              DesktopJoinStatus = "";
                                                                                                                              UpdateJoinCodePage();
                                                                                                                          }, 0.092f, 0.052f);
        }

        AddCard("back", new Vector3(-0.27f, -0.275f, 0f), "", "BACK", delegate
                                                                      {
                                                                          ShowPage(Page.Room, null);
                                                                      }, 0.14f, 0.055f).valueText.color = Theme.Soft;

        AddCard("del", new Vector3(-0.09f, -0.275f, 0f), "", "DEL", delegate
                                                                    {
                                                                        joinCode          = RoomCode.Backspace(joinCode);
                                                                        DesktopJoinStatus = "";
                                                                        UpdateJoinCodePage();
                                                                    }, 0.14f, 0.055f);

        AddCard("clear", new Vector3(0.09f, -0.275f, 0f), "", "CLEAR", delegate
                                                                       {
                                                                           joinCode          = "";
                                                                           DesktopJoinStatus = "";
                                                                           UpdateJoinCodePage();
                                                                       }, 0.14f, 0.055f);

        AddCard("join", new Vector3(0.27f, -0.275f, 0f), "", "JOIN", delegate
                                                                     {
                                                                         if (RoomCode.Valid(joinCode) && !isJoiningRoom)
                                                                         {
                                                                             this.StartCoroutine(JoinRoom(joinCode, null, true));
                                                                         }
                                                                     }, 0.14f, 0.055f).valueText.color = Theme.Good;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void UpdateJoinCodePage()
    {
        if ((Object)(object)joinCodeLabel != (Object)null)
        {
            joinCodeLabel.text = joinCode.Length == 0 ? "ENTER CODE" : joinCode;
        }

        if ((Object)(object)joinStatusLabel != (Object)null)
        {
            joinStatusLabel.text = DesktopJoinStatus;
        }
    }

    private void SetJoinStatus(string arg1)
    {
        DesktopJoinStatus = arg1;
        if ((Object)(object)joinStatusLabel != (Object)null)
        {
            joinStatusLabel.text = arg1;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void BuildDesktopMusicPage()
    {
        AddPageTitle("MUSIC", Theme.Soft);
        if (Manifest.Has("spotify"))
        {
            AddCard("spotify", new Vector3(0f, 0.3f, 0f), "", "SPOTIFY CONTROLS", delegate
                                                                                  {
                                                                                      ShowPage(Page.Spotify, null);
                                                                                  }, 0.32f, 0.06f);
        }

        mediaStatusLabel                         = Theme.Text(contentRoot.transform, "track", DesktopMediaControls.TrackTitle, 0.028f, Theme.White);
        mediaStatusLabel.transform.localPosition = new Vector3(0f, 0.2f, 0f) + ContentDepthOffset;
        AddCard("prev", new Vector3(-0.23f, 0.055f, 0f), "", "PREV", delegate
                                                                     {
                                                                         DesktopMediaControls.PreviousTrack();
                                                                         nextMediaRefresh = Time.time + 0.9f;
                                                                     }, 0.2f, 0.09f);

        AddCard("play", new Vector3(0f, 0.055f, 0f), "", "PLAY/PAUSE", delegate
                                                                       {
                                                                           DesktopMediaControls.TogglePlayPause();
                                                                           nextMediaRefresh = Time.time + 0.9f;
                                                                       }, 0.22f, 0.09f);

        AddCard("next", new Vector3(0.23f, 0.055f, 0f), "", "NEXT", delegate
                                                                    {
                                                                        DesktopMediaControls.NextTrack();
                                                                        nextMediaRefresh = Time.time + 0.9f;
                                                                    }, 0.2f, 0.09f);

        AddCard("voldown", new Vector3(-0.12f, -0.065f, 0f), "", "VOL -", DesktopMediaControls.VolumeDown, 0.22f, 0.09f);
        AddCard("volup",   new Vector3(0.12f,  -0.065f, 0f), "", "VOL +", DesktopMediaControls.VolumeUp,   0.22f, 0.09f);
        AddBackButton(new Vector3(0f, -0.2f, 0f), delegate
                                                  {
                                                      ShowPage(Page.Main, null);
                                                  });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void BuildSpotifyPage()
    {
        tooltipRevision = Cfg.Revision;
        AddPageTitle("SPOTIFY", Theme.Soft);
        lastSpotifyConnected = SpotifyPlayer.OAuth.IsConnected;
        if (!lastSpotifyConnected)
        {
            mediaStatusLabel                         = Theme.Text(contentRoot.transform, "track", SpotifyPlayer.OAuth.ClientId.Length == 0 ? "SAVE YOUR CLIENT ID IN THE BEPINEX CONFIG" : SpotifyPlayer.OAuth.StatusMessage.Length > 0 ? SpotifyPlayer.OAuth.StatusMessage : "CONNECT YOUR SPOTIFY", 0.02f, Theme.DimText);
            mediaStatusLabel.transform.localPosition = new Vector3(0f, 0.12f, 0f) + ContentDepthOffset;
            if (SpotifyPlayer.OAuth.ClientId.Length > 0)
            {
                AddCard("login", new Vector3(0f, 0.01f, 0f), "", SpotifyPlayer.OAuth.LoginPending ? "WAITING..." : "LOGIN TO SPOTIFY", delegate
                                                                                                                                       {
                                                                                                                                           SpotifyPlayer.OAuth.BeginLogin();
                                                                                                                                           ShowPage(Page.Spotify, null);
                                                                                                                                       }, 0.3f, 0.08f);
            }

            AddCard("desktop", new Vector3(0f, -0.14f, 0f), "", "DESKTOP MUSIC", delegate
                                                                                 {
                                                                                     ShowPage(Page.Music, null);
                                                                                 }, 0.28f, 0.064f);

            return;
        }

        GameObject val = Theme.Quad(contentRoot.transform, "art", 0.13f, 0.13f, Theme.Holo((Object)(object)SpotifyPlayer.AlbumArt != (Object)null ? Color.white : Theme.Fill, 2997, SpotifyPlayer.AlbumArt));
        val.transform.localPosition = new Vector3(-0.27f, 0.17f, 0f) + ContentDepthOffset;
        platformIconRenderer        = val.GetComponent<Renderer>();
        mediaStatusLabel            = AddPositionedText("track",  TruncateText(SpotifyPlayer.TrackName,  24), 0.026f, Theme.White,   -0.19f, 0.205f);
        trackLabel                  = AddPositionedText("artist", TruncateText(SpotifyPlayer.ArtistName, 30), 0.02f,  Theme.DimText, -0.19f, 0.165f);
        artistLabel                 = AddPositionedText("time",   FormatPlaybackProgress(),                   0.018f, Theme.DimText, -0.19f, 0.13f);
        AddCard("prev", new Vector3(-0.23f, 0.04f, 0f), "", "PREV", SpotifyPlayer.PreviousTrack, 0.2f, 0.08f);
        playbackLabel = AddCard("play", new Vector3(0f, 0.04f, 0f), "", SpotifyPlayer.IsPlaying ? "PAUSE" : "PLAY", [MethodImpl(MethodImplOptions.NoInlining)]() =>
                                                                                                                    {
                                                                                                                        SpotifyPlayer.TogglePlayback();
                                                                                                                        playbackLabel.text = SpotifyPlayer.IsPlaying ? "PAUSE" : "PLAY";
                                                                                                                    }, 0.22f, 0.08f).valueText;

        AddCard("next", new Vector3(0.23f, 0.04f, 0f), "", "NEXT", SpotifyPlayer.NextTrack, 0.2f, 0.08f);
        AddCard("voldown", new Vector3(-0.255f, -0.055f, 0f), "", "VOL -", delegate
                                                                           {
                                                                               SpotifyPlayer.AdjustVolume(-10);
                                                                           }, 0.15f, 0.075f);

        progressLabel = AddCard("vol", new Vector3(0f, -0.055f, 0f), "VOLUME", SpotifyPlayer.VolumePercent + "%", null, 0.2f, 0.075f).valueText;
        AddCard("volup", new Vector3(0.255f, -0.055f, 0f), "", "VOL +", delegate
                                                                        {
                                                                            SpotifyPlayer.AdjustVolume(10);
                                                                        }, 0.15f, 0.075f);

        shuffleLabel = AddCard("shuf", new Vector3(-0.15f, -0.145f, 0f), "SHUFFLE", SpotifyPlayer.ShuffleEnabled ? "ON" : "OFF", [MethodImpl(MethodImplOptions.NoInlining)]() =>
                                                                                                                                 {
                                                                                                                                     SpotifyPlayer.ToggleShuffle();
                                                                                                                                     shuffleLabel.text = SpotifyPlayer.ShuffleEnabled ? "ON" : "OFF";
                                                                                                                                 }, 0.26f, 0.075f).valueText;

        repeatLabel = AddCard("rep", new Vector3(0.15f, -0.145f, 0f), "REPEAT", SpotifyPlayer.RepeatMode.ToUpper(), delegate
                                                                                                                    {
                                                                                                                        SpotifyPlayer.CycleRepeatMode();
                                                                                                                        repeatLabel.text = SpotifyPlayer.RepeatMode.ToUpper();
                                                                                                                    }, 0.26f, 0.075f).valueText;

        AddBackButton(new Vector3(-0.09f, -0.24f, 0f), delegate
                                                       {
                                                           ShowPage(Page.Music, null);
                                                       });

        AddCard("logout", new Vector3(0.12f, -0.24f, 0f), "", "LOGOUT", delegate
                                                                        {
                                                                            SpotifyPlayer.Disconnect();
                                                                            ShowPage(Page.Spotify, null);
                                                                        }, 0.16f, 0.064f).valueText.color = Theme.DimText;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void UpdateSpotifyPage()
    {
        if (tooltipRevision != Cfg.Revision)
        {
            ShowPage(Page.Spotify, null);

            return;
        }

        if (SpotifyPlayer.OAuth.IsConnected != lastSpotifyConnected)
        {
            ShowPage(Page.Spotify, null);

            return;
        }

        if (!lastSpotifyConnected)
        {
            if ((Object)(object)mediaStatusLabel != (Object)null && SpotifyPlayer.OAuth.ClientId.Length > 0 && SpotifyPlayer.OAuth.StatusMessage.Length > 0)
            {
                mediaStatusLabel.text = SpotifyPlayer.OAuth.StatusMessage;
            }

            return;
        }

        SpotifyPlayer.PollPlayback();
        if (!(Time.time < nextMediaRefresh) && !((Object)(object)mediaStatusLabel == (Object)null))
        {
            nextMediaRefresh      = Time.time + 0.5f;
            mediaStatusLabel.text = TruncateText(SpotifyPlayer.TrackName,  24);
            trackLabel.text       = TruncateText(SpotifyPlayer.ArtistName, 30);
            string text = SpotifyPlayer.StatusMessage.Length > 0 ? SpotifyPlayer.StatusMessage : SpotifyPlayer.OAuth.StatusMessage;
            artistLabel.text   = text.Length > 0 ? text : SpotifyPlayer.PremiumRequired ? "SPOTIFY PREMIUM NEEDED" : FormatPlaybackProgress();
            artistLabel.color  = SpotifyPlayer.PremiumRequired || text.Length > 0 ? Theme.Warn : Theme.DimText;
            playbackLabel.text = SpotifyPlayer.IsPlaying ? "PAUSE" : "PLAY";
            shuffleLabel.text  = SpotifyPlayer.ShuffleEnabled ? "ON" : "OFF";
            repeatLabel.text   = SpotifyPlayer.RepeatMode.ToUpper();
            progressLabel.text = SpotifyPlayer.VolumePercent + "%";
            if ((Object)(object)platformIconRenderer != (Object)null && (Object)(object)platformIconRenderer.sharedMaterial.mainTexture != (Object)(object)SpotifyPlayer.AlbumArt)
            {
                platformIconRenderer.sharedMaterial.mainTexture = SpotifyPlayer.AlbumArt;
                platformIconRenderer.sharedMaterial.color       = (Object)(object)SpotifyPlayer.AlbumArt != (Object)null ? Color.white : Theme.Fill;
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void BuildLiveChatPage()
    {
        tooltipRevision = Cfg.Revision;
        AddPageTitle("LIVE CHAT", Theme.Soft);
        if (YouTubeLiveChat.IsConfigured)
        {
            liveStatusLabel                          = Theme.Text(contentRoot.transform, "state", YouTubeLiveChat.Status.Length > 0 ? YouTubeLiveChat.Status : "LOOKING...", 0.02f, Theme.DimText);
            liveStatusLabel.transform.localPosition  = new Vector3(0f, 0.235f, 0f) + ContentDepthOffset;
            streamTitleLabel                         = Theme.Text(contentRoot.transform, "streamtitle", YouTubeLiveChat.StreamTitle, 0.019f, Theme.White);
            streamTitleLabel.richText                = false;
            streamTitleLabel.transform.localPosition = new Vector3(0f, 0.29f, 0f) + ContentDepthOffset;
            chatMessageLabels                        = new TMP_Text[8];
            for (int i = 0; i < 8; i++)
            {
                chatMessageLabels[i]          = AddPositionedText("l" + i, "", 0.019f, Theme.White, -0.36f, 0.19f - i * 0.048f);
                chatMessageLabels[i].richText = false;
            }

            displayedChatVersion = -1;
            AddBackButton(new Vector3(0f, -0.24f, 0f), delegate
                                                       {
                                                           ShowPage(Page.Main, null);
                                                       });
        }
        else
        {
            Theme.Text(contentRoot.transform, "state", "SAVE YOUR CHANNEL IN THE BEPINEX CONFIG",                   0.02f,  Theme.DimText).transform.localPosition = new Vector3(0f, 0.08f, 0f) + ContentDepthOffset;
            Theme.Text(contentRoot.transform, "hint",  "Enter @YourHandle once. New streams appear automatically.", 0.019f, Theme.DimText).transform.localPosition = new Vector3(0f, 0.02f, 0f) + ContentDepthOffset;
            AddBackButton(new Vector3(0f, -0.14f, 0f), delegate
                                                       {
                                                           ShowPage(Page.Main, null);
                                                       });
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void UpdateLiveChatPage()
    {
        if (tooltipRevision != Cfg.Revision)
        {
            ShowPage(Page.Chat, null);
        }
        else
        {
            if (!YouTubeLiveChat.IsConfigured)
            {
                return;
            }

            YouTubeLiveChat.Poll();
            if (chatMessageLabels == null)
            {
                return;
            }

            liveStatusLabel.text  = YouTubeLiveChat.Status.Length > 0 ? YouTubeLiveChat.Status : "LOOKING...";
            liveStatusLabel.color = YouTubeLiveChat.Status        == "LIVE" ? Theme.Good : Theme.DimText;
            if ((Object)(object)streamTitleLabel != (Object)null)
            {
                streamTitleLabel.text = TruncateText(YouTubeLiveChat.StreamTitle, 48);
            }

            if (displayedChatVersion == YouTubeLiveChat.MessageVersion)
            {
                return;
            }

            displayedChatVersion = YouTubeLiveChat.MessageVersion;
            string[] messages = YouTubeLiveChat.Messages;
            for (int i = 0; i < 8; i++)
            {
                if (i < messages.Length)
                {
                    int num = messages[i].IndexOf('\t');
                    if (num < 0)
                    {
                        chatMessageLabels[i].text = TruncateText(messages[i], 44);

                        continue;
                    }

                    string text = TruncateText(messages[i].Substring(0, num), 14);
                    chatMessageLabels[i].text = text + ": " + TruncateText(messages[i].Substring(num + 1), 42 - text.Length);
                }
                else
                {
                    chatMessageLabels[i].text = "";
                }
            }
        }
    }

    private TMP_Text AddPositionedText(string arg1, string arg2, float arg3, Color arg4, float arg5, float arg6)
    {
        TextMeshPro obj = Theme.Text(contentRoot.transform, arg1, arg2, arg3, arg4, true);
        ((TMP_Text)obj).transform.localPosition = new Vector3(arg5, arg6, 0f) + ContentDepthOffset;

        return obj;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string TruncateText(string arg1, int arg2)
    {
        if (arg1 == null)
        {
            return "";
        }

        if (arg1.Length > arg2)
        {
            return arg1.Substring(0, arg2 - 2) + "..";
        }

        return arg1;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string FormatPlaybackProgress()
    {
        if (SpotifyPlayer.DurationMs <= 0)
        {
            return "";
        }

        int num = SpotifyPlayer.ProgressMs;
        if (SpotifyPlayer.IsPlaying)
        {
            num += (int)((Time.time - SpotifyPlayer.LastProgressSampleTime) * 1000f);
        }

        return FormatDuration(Mathf.Min(num, SpotifyPlayer.DurationMs)) + " / " + FormatDuration(SpotifyPlayer.DurationMs);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string FormatDuration(int arg1)
    {
        int num = arg1 / 1000;

        return num / 60 + ":" + (num % 60).ToString("00");
    }

    private static Vector3 PolarPosition(float arg1, float arg2)
    {
        float num = arg1 * (MathF.PI / 180f);

        return new Vector3(Mathf.Sin(num) * arg2, Mathf.Cos(num) * arg2, 0f);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AddPageTitle(string arg1, Color arg2) => Theme.Text(contentRoot.transform, "title", arg1, 0.038f, arg2).transform.localPosition = PolarPosition(0f, 0.5f) + ContentDepthOffset;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AddPlayerPreview(VRRig arg1, string arg2)
    {
        if (rectangleLayout)
        {
            if ((Object)(object)arg1 != (Object)null)
            {
                Mirror.Spawn(contentRoot.transform, arg1, 0.18f).transform.localPosition = new Vector3(-0.45f, 0.5f, 0.1f);
            }

            AddPageTitle(arg2, Detect.RigColor(arg1));
        }
        else
        {
            if ((Object)(object)arg1 != (Object)null)
            {
                Mirror.Spawn(contentRoot.transform, arg1, 1f).transform.localPosition = new Vector3(0f, 0.1f, 0.14f);
            }

            Theme.Text(contentRoot.transform, "cname", arg2, 0.036f, Detect.RigColor(arg1)).transform.localPosition = new Vector3(0f, -0.63f, -0.02f);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AddPlayerButton(VRRig arg1, float arg2, float arg3, int arg4)
    {
        if (!rectangleLayout)
        {
            MenuItem menuItem = CreateMenuItem("fig_" + arg4, PolarPosition(arg2, 0.485f), arg4 * 0.03f);
            menuItem.rig           = arg1;
            menuItem.scale         = arg3;
            menuItem.radialHitTest = true;
            menuItem.hitWidth      = 0.085f * arg3;
            NetPlayer   creator = arg1.Creator;
            TextMeshPro val     = Theme.Text(menuItem.root.transform, "nm", creator != null ? creator.NickName : "?", 0.026f, Theme.White);
            val.transform.localPosition = new Vector3(0f, -0.115f, -0.002f);
            menuItem.valueText          = val;
            Player val2 = Detect.PlayerOf(arg1);
            if (val2 != null)
            {
                Scan scan = Detect.Get(val2);
                val.color = scan.Cheats.Count > 0 ? Theme.Bad : scan.Mods.Count > 0 ? Theme.Warn : Theme.White;
            }

            if (Net.HasRig(arg1))
            {
                Theme.Quad(menuItem.root.transform, "zx", 0.042f, 0.042f, Theme.Holo(Color.white, 2998, Theme.MenuIcon)).transform.localPosition = new Vector3(0.062f, 0.058f, -0.002f);
            }

            menuItem.onActivate = delegate
                                  {
                                      Player val3 = Detect.PlayerOf(arg1);
                                      if (val3 != null)
                                      {
                                          ShowPage(Page.Player, val3);
                                      }
                                  };

            CaptureItemVisuals(menuItem);
            Mirror.Head(menuItem.root.transform, arg1, 0.25f).transform.localPosition = new Vector3(0f, 0.012f, 0.01f);

            return;
        }

        Player player0 = Detect.PlayerOf(arg1);
        MenuItem menuItem2 = AddCard("fig_" + arg4, new Vector3(arg4 % 2 == 0 ? -0.27f : 0.27f, 0.33f - arg4 / 2 * 0.12f, 0f), "", arg1.Creator != null ? arg1.Creator.NickName : "?", delegate
                                                                                                                                                                                       {
                                                                                                                                                                                           if (player0 != null)
                                                                                                                                                                                           {
                                                                                                                                                                                               ShowPage(Page.Player, player0);
                                                                                                                                                                                           }
                                                                                                                                                                                       }, 0.48f, 0.1f);

        menuItem2.rig                                                             = arg1;
        menuItem2.valueText.transform.localPosition                               = new Vector3(0.055f, 0f, -0.002f);
        menuItem2.valueText.rectTransform.sizeDelta                               = new Vector2(0.34f, 0.08f);
        menuItem2.valueText.overflowMode                                          = (TextOverflowModes)1;
        menuItem2.valueText.richText                                              = false;
        menuItem2.valueText.fontSize                                              = 0.24f;
        Mirror.Head(menuItem2.root.transform, arg1, 0.1f).transform.localPosition = new Vector3(-0.18f, 0f, 0.01f);
        if (player0 != null)
        {
            Scan scan2 = Detect.Get(player0);
            menuItem2.valueText.color = scan2.Cheats.Count > 0 ? Theme.Bad : scan2.Mods.Count > 0 ? Theme.Warn : Theme.White;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private MenuItem AddCard(string arg1, Vector3 arg2, string arg3, string arg4, Action arg5, float arg6, float arg7 = 0.092f)
    {
        if (rectangleLayout && currentPage == Page.Player)
        {
            int count = menuItems.Count;
            arg2 = new Vector3(count % 2 == 0 ? -0.27f : 0.27f, 0.3f - count / 2 * 0.16f, 0f);
            arg6 = 0.44f;
        }

        MenuItem menuItem = CreateMenuItem(arg1, arg2, Mathf.Min(menuItems.Count, 10) * 0.028f);
        menuItem.tooltip = GetTooltipText(arg1, arg3.Length > 0 ? arg3 : arg4);
        menuItem.panel   = Theme.Card(menuItem.root.transform, "panel", arg6, arg7, InactiveBorderColor);
        bool flag;
        if (flag = !string.IsNullOrEmpty(arg3))
        {
            TextMeshPro val = Theme.Text(menuItem.root.transform, "label", arg3, 0.02f, Theme.DimText);
            val.transform.localPosition = new Vector3(0f, arg7 * 0.26f, -0.002f);
            if (currentPage == Page.Friends)
            {
                val.richText = false;
            }
        }

        menuItem.valueText = Theme.Text(menuItem.root.transform, "value", arg4, flag ? 0.027f : 0.03f, Theme.White);
        if (currentPage == Page.Friends)
        {
            menuItem.valueText.richText = false;
        }

        menuItem.valueText.transform.localPosition = new Vector3(0f, flag ? (0f - arg7) * 0.17f : 0f, -0.002f);
        if (arg5 != null)
        {
            menuItem.hitWidth   = arg6;
            menuItem.hitHeight  = arg7;
            menuItem.onActivate = arg5;
        }

        CaptureItemVisuals(menuItem);

        return menuItem;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AddBackButton(Vector3 arg1, Action arg2) => AddCard("back", arg1, "", "BACK", arg2, 0.18f, 0.064f).valueText.color = Theme.Soft;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AddNavigationButton(string arg1, float arg2, Texture arg3, string arg4, Action arg5)
    {
        MenuItem menuItem = CreateMenuItem(arg1, PolarPosition(arg2, 0.485f), 0.22f);
        menuItem.tooltip = GetTooltipText(arg1, arg4);
        menuItem.panel   = Theme.Card(menuItem.root.transform, "panel", 0.115f, 0.115f, InactiveBorderColor);
        if ((Object)(object)arg3 != (Object)null)
        {
            Theme.Quad(menuItem.root.transform, "icon", 0.062f, 0.062f, Theme.Holo(Theme.Soft, 2997, arg3)).transform.localPosition = new Vector3(0f, 0.01f, -0.002f);
        }
        else
        {
            DrawMusicNoteIcon(menuItem.root.transform);
        }

        Theme.Text(menuItem.root.transform, "lb", arg4, 0.017f, Theme.DimText).transform.localPosition = new Vector3(0f, -0.07f, -0.002f);
        menuItem.radialHitTest                                                                         = true;
        menuItem.hitWidth                                                                              = 0.062f;
        menuItem.onActivate                                                                            = arg5;
        CaptureItemVisuals(menuItem);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DrawMusicNoteIcon(Transform arg1)
    {
        Theme.Ring(arg1, "nhead", 0f, 0.016f, Theme.Soft, 2997).transform.localPosition                 = new Vector3(-0.013f, -0.014f, -0.002f);
        Theme.Quad(arg1, "nstem", 0.005f, 0.052f, Theme.Holo(Theme.Soft, 2997)).transform.localPosition = new Vector3(0.0015f, 0.012f,  -0.002f);
        Theme.Quad(arg1, "nflag", 0.021f, 0.008f, Theme.Holo(Theme.Soft, 2997)).transform.localPosition = new Vector3(0.012f,  0.034f,  -0.002f);
    }

    private MenuItem CreateMenuItem(string arg1, Vector3 arg2, float arg3)
    {
        GameObject val = new(arg1);
        val.transform.SetParent(contentRoot.transform, false);
        val.transform.localPosition = arg2        * 0.25f;
        val.transform.localScale    = Vector3.one * 0.001f;
        MenuItem menuItem = new();
        menuItem.root           = val;
        menuItem.targetPosition = arg2;
        menuItem.createdAt      = Time.time;
        menuItem.animationDelay = arg3;
        menuItems.Add(menuItem);

        return menuItem;
    }

    private void CaptureItemVisuals(MenuItem arg1)
    {
        MeshRenderer[] componentsInChildren = arg1.root.GetComponentsInChildren<MeshRenderer>(true);
        foreach (MeshRenderer val in componentsInChildren)
        {
            if (!((Object)(object)val.GetComponent<TMP_Text>() != (Object)null))
            {
                arg1.renderers.Add(val);
                arg1.rendererBaseAlpha.Add(val.sharedMaterial.color.a);
            }
        }

        TMP_Text[] componentsInChildren2 = arg1.root.GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text val2 in componentsInChildren2)
        {
            arg1.texts.Add(val2);
            val2.alpha = 0f;
        }
    }

    private int HitTest(Vector2 arg1)
    {
        for (int i = 0; i < menuItems.Count; i++)
        {
            MenuItem menuItem = menuItems[i];
            if (menuItem.onActivate == null || menuItem.hitWidth <= 0f)
            {
                continue;
            }

            float x = arg1.x - menuItem.targetPosition.x;
            float y = arg1.y - menuItem.targetPosition.y;
            bool hit = menuItem.radialHitTest
                               ? x                                 * x + y                                      * y <= menuItem.hitWidth * menuItem.hitWidth
                               : Mathf.Abs(x) <= menuItem.hitWidth * 0.5f && Mathf.Abs(y) <= menuItem.hitHeight * 0.5f;

            if (hit)
            {
                return i;
            }
        }

        return -1;
    }

    private void HandleLaserInput(GorillaTagger arg1)
    {
        Transform             val      = UseLeftHand ? arg1.leftHandTransform : arg1.rightHandTransform;
        ControllerInputPoller instance = ControllerInputPoller.instance;
        if (!((Object)(object)val == (Object)null) && !((Object)(object)instance == (Object)null))
        {
            pointerLine.enabled = true;
            Vector3 position = val.position;
            Vector3 val2     = val.rotation * Quaternion.Euler(45f, UseLeftHand ? 10f : -10f, 0f) * Vector3.forward;
            pointerLine.SetPosition(0, position);
            Vector3 val3 = menuFace.InverseTransformPoint(position);
            Vector3 val4 = menuFace.InverseTransformDirection(val2);
            Vector3 val5 = position + val2 * 3f;
            int     num  = -1;
            bool    flag = false;
            Vector2 val6 = Vector2.zero;
            if (Mathf.Abs(val4.z) > 1E-05f)
            {
                float num2 = (0f - val3.z) / val4.z;
                if (num2 > 0f)
                {
                    Vector2 val7 = default;
                    val7 = new Vector2(val3.x + val4.x * num2, val3.y + val4.y * num2);
                    val6 = val7;
                    if (val7.sqrMagnitude < 1f)
                    {
                        num = HitTest(val7);
                        if (flag = num >= 0 || (!rectangleLayout ? val7.sqrMagnitude < 0.3844f : Mathf.Abs(val7.x) <= 0.58f && Mathf.Abs(val7.y) <= 0.63f))
                        {
                            val5 = menuFace.TransformPoint(new Vector3(val7.x, val7.y, 0f));
                        }
                    }
                }
            }

            pointerLine.SetPosition(1, val5);
            laserDot.gameObject.SetActive(flag);
            if (flag)
            {
                laserDot.position = val5 - val2 * 0.005f;
                laserDot.rotation = Quaternion.LookRotation(val2);
            }

            if (Cfg.Touch.Value && !Cfg.OneHand.Value)
            {
                HandleTouchInput(arg1);
            }
            else
            {
                lastTouchedItem  = -1;
                draggedItemIndex = -1;
            }

            SetHoveredItem(draggedItemIndex >= 0 ? draggedItemIndex : num);
            bool flag2;
            if ((flag2 = (UseLeftHand ? instance.leftControllerIndexFloat : instance.rightControllerIndexFloat) > 0.6f) && num >= 0 && menuItems[num].onDrag != null)
            {
                menuItems[num].onDrag(
                        val6 - (Vector2)menuItems[num].targetPosition
                );
            }
            else if (flag2 && !triggerDown && num >= 0 && menuItems[num].onActivate != null)
            {
                ActivateItem(num, UseLeftHand);
            }

            if (!flag2 && triggerDown)
            {
                colorPicker?.SaveChanges();
            }

            triggerDown = flag2;
        }
        else
        {
            colorPicker?.SaveChanges();
            pointerLine.enabled = false;
            laserDot.gameObject.SetActive(false);
            if (hoveredItemIndex >= 0)
            {
                SetItemHighlighted(hoveredItemIndex, false);
            }

            lastTouchedItem  = -1;
            hoveredItemIndex = -1;
            draggedItemIndex = -1;
            hoverExitDelay   = 0f;
            triggerDown      = false;
        }
    }

    private void HandleTouchInput(GorillaTagger arg1)
    {
        int  num  = -1;
        bool arg2 = false;
        for (int i = 0; i < 2; i++)
        {
            Transform val = i == 0 ? arg1.leftHandTransform : arg1.rightHandTransform;
            if ((Object)(object)val == (Object)null)
            {
                continue;
            }

            Vector3 val2 = menuFace.InverseTransformPoint(val.position);
            if (Mathf.Abs(val2.z) > 0.1f)
            {
                continue;
            }

            int num2 = HitTest(new Vector2(val2.x, val2.y));
            if (num2 >= 0)
            {
                if (menuItems[num2].onDrag != null)
                {
                    menuItems[num2].onDrag(val2 - menuItems[num2].targetPosition);
                }

                num  = num2;
                arg2 = i == 0;

                break;
            }
        }

        draggedItemIndex = num;
        if (num >= 0 && num != lastTouchedItem && menuItems[num].onActivate != null && menuItems[num].onDrag == null)
        {
            ActivateItem(num, arg2);
        }

        if (num < 0 && lastTouchedItem >= 0)
        {
            colorPicker?.SaveChanges();
        }

        lastTouchedItem = num;
    }

    private void SetHoveredItem(int arg1)
    {
        if (arg1 == hoveredItemIndex)
        {
            hoverExitDelay = 0f;

            return;
        }

        if (arg1 < 0 && hoveredItemIndex >= 0)
        {
            hoverExitDelay += Time.deltaTime;
            if (!(hoverExitDelay >= 0.12f))
            {
                return;
            }
        }

        hoverExitDelay = 0f;
        SetItemHighlighted(hoveredItemIndex, false);
        hoveredItemIndex = arg1;
        SetItemHighlighted(hoveredItemIndex, true);
        if (hoveredItemIndex >= 0)
        {
            Theme.HoverSound();
            Theme.Haptic(UseLeftHand, 0.12f, 0.015f);
        }
    }

    private void ActivateItem(int arg1, bool arg2)
    {
        Theme.ClickSound();
        Theme.Haptic(arg2, 0.5f, 0.04f);
        menuItems[arg1].onActivate();
    }

    private void SetItemHighlighted(int arg1, bool arg2)
    {
        if (arg1 >= 0 && arg1 < menuItems.Count)
        {
            MenuItem menuItem = menuItems[arg1];
            menuItem.highlighted = arg2;
            if (menuItem.panel != null)
            {
                menuItem.panel.FillR.sharedMaterial.color   = arg2 ? new Color(Theme.Main.r, Theme.Main.g, Theme.Main.b, 0.3f) : Theme.Fill;
                menuItem.panel.BorderR.sharedMaterial.color = arg2 ? Theme.Soft : InactiveBorderColor;
            }

            if ((Object)(object)menuItem.rig != (Object)null)
            {
                Outline.Set(menuItem.rig, arg2);
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private string GetTooltipText()
    {
        if (targetPlayer != null)
        {
            if (!targetPlayer.IsLocal)
            {
                if (!Net.Has(targetPlayer.UserId))
                {
                    return "NOT DETECTED";
                }

                return "SENTINEL" + (Net.MenuOpen(targetPlayer.UserId) ? " / OPEN" : "");
            }

            return Manifest.TierLabel.ToUpper();
        }

        return "NOT DETECTED";
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void UpdateTooltip()
    {
        if ((Object)(object)tooltipTitleLabel == (Object)null)
        {
            return;
        }

        if (Cfg.Tooltips.Value && hoveredItemIndex >= 0 && hoveredItemIndex < menuItems.Count)
        {
            if (tooltipItemIndex != hoveredItemIndex)
            {
                tooltipItemIndex       = hoveredItemIndex;
                tooltipVisibleAt       = Time.unscaledTime + 0.45f;
                tooltipTitleLabel.text = "";
            }

            if (Time.unscaledTime >= tooltipVisibleAt)
            {
                MenuItem menuItem = menuItems[hoveredItemIndex];
                tooltipTitleLabel.text = menuItem.tooltip ?? ((Object)(object)menuItem.rig != (Object)null ? "View this player's details and client tier" : "");
            }
        }
        else
        {
            tooltipTitleLabel.text = "";
            tooltipItemIndex       = -1;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string GetTooltipText(string arg1, string arg2)
    {
        switch (arg1)
        {
            case "rep":
                return "Cycle repeat off, playlist and track";

            case "swap":
                return "Move the disc and pointer to the other hand";

            case "prev":
                return "Go to the previous track";

            case "room":
                return "Manage your room and game settings";

            case "play":
                return "Play or pause your music";

            case "back":
                return "Return to the previous page";

            case "chat":
                return "Automatically follow new live streams and chat from your saved YouTube channel";

            case "shuf":
                return "Turn Spotify shuffle on or off";

            case "next":
                return "Skip to the next track";

            case "tagzx":
                return "Show the menu icon on detected Sentinel users";

            case "volup":
                return "Raise music volume";

            case "login":
                return "Finish Spotify sign in in your desktop browser";

            case "touch":
                return "Poke buttons with your hand or use the laser";

            case "logout":
                return "Disconnect Spotify from Sentinel";

            case "sounds":
                return "Turn menu sound effects on or off";

            case "openkey":
                return "Press the controller button you want to use to open";

            case "tagplat":
                return "Show reported platform. Unknown means no reliable data";

            case "refresh":
                return "Reconnect to the live chat and clear old messages";

            case "zx":
            case "tagtier":
                return "Tier from a signed Sentinel account session; game identity is client reported";

            case "spotify":
                return "Open Spotify account controls";

            case "music":
            case "desktop":
                return "Control the active music app on your PC";

            case "voldown":
                return "Lower music volume";

            case "friends":
                return "Accept requests and view activity your friends choose to share";

            case "onehand":
                return "Place the menu in front of you and use one hand";

            case "timelock":
                return "Keep your chosen time of day. Turn off to resume the normal cycle.";

            case "handmenu":
                return "Open the menu on your palm when you grab the disc";

            case "keepopen":
                return "Keep the hand menu open when you look away; your close button still works";

            case "closekey":
                return "Choose the button you double tap to close";

            case "settings":
                return "Change menu controls, appearance and labels";

            case "tooltips":
                return "Show a short hint when you point at a control";

            case "playerray":
                return "Hold the pointer hand trigger to aim, release to inspect. Keep both grips released.. Hold right mouse to aim at a player, then left click to inspect.";

            case "colorwheel":
                return "Hold the trigger and point at a color. The center makes it lighter.";

            case "brightness":
                return "Hold the trigger and slide left for darker or right for brighter.";

            case "friendprivacy":
                return "Let accepted friends see your online status and room code. Off hides both.";

            default:
                return arg2;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void RefreshDynamicContent()
    {
        if (currentPage == Page.Music && (Object)(object)mediaStatusLabel != (Object)null && Time.time >= nextMediaRefresh)
        {
            nextMediaRefresh      = Time.time + 1f;
            mediaStatusLabel.text = DesktopMediaControls.StatusMessage.Length > 0 ? DesktopMediaControls.StatusMessage : DesktopMediaControls.TrackTitle;
        }
        else if (currentPage != Page.Spotify)
        {
            if (currentPage == Page.Chat)
            {
                UpdateLiveChatPage();
            }
        }
        else
        {
            UpdateSpotifyPage();
        }

        if (!(Time.time >= nextDynamicRefresh))
        {
            return;
        }

        nextDynamicRefresh = Time.time + 0.25f;
        if (currentPage != Page.Player || targetPlayer == null)
        {
            return;
        }

        VRRig  val  = Detect.RigOf(targetPlayer);
        string text = Detect.PlatformOf(val);
        if ((Object)(object)tooltipBodyLabel != (Object)null && tooltipBodyLabel.text != text.ToUpper())
        {
            tooltipBodyLabel.text = text.ToUpper();
            if ((Object)(object)clientIconRenderer != (Object)null)
            {
                clientIconRenderer.sharedMaterial.mainTexture = Detect.PlatformIcon(text);
            }
        }

        if ((Object)(object)friendStatusLabel != (Object)null)
        {
            friendStatusLabel.text = GetTooltipText();
        }

        Scan scan = Detect.Get(targetPlayer);
        if ((Object)(object)pageTitleLabel != (Object)null && (Object)(object)val != (Object)null)
        {
            pageTitleLabel.text  = val.fps.ToString();
            pageTitleLabel.color = Detect.FpsColor(val.fps);
        }

        if (scan.Ready)
        {
            if ((Object)(object)playerNameLabel != (Object)null)
            {
                playerNameLabel.text  = scan.Mods.Count.ToString();
                playerNameLabel.color = scan.Mods.Count > 0 ? Theme.Warn : Theme.White;
            }

            if ((Object)(object)platformLabel != (Object)null)
            {
                platformLabel.text  = scan.Cheats.Count.ToString();
                platformLabel.color = scan.Cheats.Count > 0 ? Theme.Bad : Theme.White;
            }
        }

        if ((Object)(object)tierLabel != (Object)null && tierLabel.text == "...")
        {
            tierLabel.text = Detect.CreatedDate(targetPlayer.UserId);
        }

        if ((Object)(object)reportStatusLabel != (Object)null)
        {
            bool flag = IsTargetMuted();
            reportStatusLabel.text  = flag ? "MUTED" : "OFF";
            reportStatusLabel.color = flag ? Theme.Bad : Theme.White;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void SubmitPlayerReport()
    {
        if (targetPlayer == null)
        {
            return;
        }

        try
        {
            GorillaPlayerScoreboardLine.ReportPlayer(targetPlayer.UserId, (GorillaPlayerLineButton.ButtonType)selectedReportReason, targetPlayer.NickName);
            foreach (GorillaPlayerScoreboardLine allScoreboardLine in GorillaScoreboardTotalUpdater.allScoreboardLines)
            {
                if (!((Object)(object)allScoreboardLine == (Object)null) && allScoreboardLine.linePlayer != null && !(allScoreboardLine.linePlayer.UserId != targetPlayer.UserId))
                {
                    allScoreboardLine.reportedCheating   |= selectedReportReason == 1;
                    allScoreboardLine.reportedToxicity   |= selectedReportReason == 2;
                    allScoreboardLine.reportedHateSpeech |= selectedReportReason == 0;
                    if (!((Object)(object)allScoreboardLine.reportButton == (Object)null))
                    {
                        allScoreboardLine.reportButton.isOn = true;
                        allScoreboardLine.reportButton.UpdateColor();
                    }
                }
            }
        }
        catch { }

        reportedUserIds.Add(targetPlayer.UserId);
        Notify.Send("reported " + targetPlayer.NickName, Theme.Bad);
        Theme.Haptic(false, 0.7f, 0.1f);
        selectedReportReason = -1;
        ShowPage(Page.Player, targetPlayer);
    }

    private bool IsTargetMuted()
    {
        GorillaPlayerScoreboardLine val = FindTargetScoreboardLine();
        if ((Object)(object)val != (Object)null)
        {
            return val.mute != 0;
        }

        return false;
    }

    private GorillaPlayerScoreboardLine FindTargetScoreboardLine()
    {
        if (targetPlayer == null)
        {
            return null;
        }

        foreach (GorillaPlayerScoreboardLine allScoreboardLine in GorillaScoreboardTotalUpdater.allScoreboardLines)
        {
            if ((Object)(object)allScoreboardLine != (Object)null && allScoreboardLine.linePlayer != null && allScoreboardLine.linePlayer.UserId == targetPlayer.UserId)
            {
                return allScoreboardLine;
            }
        }

        return null;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ToggleTargetMute()
    {
        GorillaPlayerScoreboardLine val = FindTargetScoreboardLine();
        if (!((Object)(object)val == (Object)null))
        {
            bool flag = !IsTargetMuted();
            val.PressButton(flag, (GorillaPlayerLineButton.ButtonType)3);
            if ((Object)(object)val.muteButton != (Object)null)
            {
                val.muteButton.isOn = flag;
                val.muteButton.UpdateColor();
            }

            if (!((Object)(object)reportStatusLabel == (Object)null))
            {
                reportStatusLabel.text  = flag ? "MUTED" : "OFF";
                reportStatusLabel.color = flag ? Theme.Bad : Theme.White;
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static GorillaNetworkJoinTrigger FindPublicRoomTrigger()
    {
        GameObject val = GameObject.Find("Environment Objects/TriggerZones_Prefab/JoinRoomTriggers_Prefab/JoinPublicRoom - Forest, Tree Exit");
        if ((Object)(object)val != (Object)null)
        {
            return val.GetComponent<GorillaNetworkJoinTrigger>();
        }

        return Object.FindFirstObjectByType<GorillaNetworkJoinTrigger>();
    }

    private IEnumerator JoinRoom(string arg1, GorillaNetworkJoinTrigger arg2, bool arg3 = false)
    {
        if (!isJoiningRoom && (!((Object)(object)arg2 == (Object)null) || !string.IsNullOrEmpty(arg1)))
        {
            isJoiningRoom = true;
            if (arg3)
            {
                SetJoinStatus("JOINING");
            }

            try
            {
                NetworkSystem instance = NetworkSystem.Instance;
                if ((Object)(object)instance == (Object)null)
                {
                    if (arg3)
                    {
                        SetJoinStatus("FAILED");
                    }

                    yield break;
                }

                if (instance.InRoom)
                {
                    instance.ReturnToSinglePlayer();
                    float num = Time.realtimeSinceStartup + 12f;
                    while ((instance.InRoom || (int)instance.netState != 2) && Time.realtimeSinceStartup < num)
                    {
                        yield return null;
                    }

                    if (instance.InRoom || (int)instance.netState != 2)
                    {
                        if (arg3)
                        {
                            SetJoinStatus("TIMED OUT");
                        }

                        yield break;
                    }
                }

                yield return new WaitForSeconds(0.5f);
                if (!((Object)(object)arg2 != (Object)null))
                {
                    PhotonNetworkController.Instance.AttemptToJoinSpecificRoom(arg1, 0);
                }
                else
                {
                    arg2.OnBoxTriggered();
                }

                float num2 = Time.realtimeSinceStartup + 3f;
                while ((int)instance.netState == 2 && !(Time.realtimeSinceStartup >= num2))
                {
                    yield return null;
                }

                if ((int)instance.netState != 2)
                {
                    float num3 = Time.realtimeSinceStartup + 20f;
                    while ((int)instance.netState != 4 && (int)instance.netState != 2 && !(Time.realtimeSinceStartup >= num3))
                    {
                        yield return null;
                    }

                    if (arg3)
                    {
                        SetJoinStatus((int)instance.netState == 4 ? "JOINED" : (int)instance.netState == 2 ? "FAILED" : "TIMED OUT");
                    }
                }
                else if (arg3)
                {
                    SetJoinStatus("FAILED");
                }

                yield break;
            }
            finally
            {
                RingMenu ringMenu = this;
                if (arg3 && ringMenu.DesktopJoinStatus == "JOINING")
                {
                    ringMenu.SetJoinStatus("FAILED");
                }

                ringMenu.isJoiningRoom = false;
            }
        }

        if (arg3)
        {
            SetJoinStatus("FAILED");
        }
    }

    private List<VRRig> GetRemoteRigs()
    {
        List<VRRig> list = new();
        if (!VRRigCache.isInitialized)
        {
            return list;
        }

        foreach (RigContainer activeRigContainer in VRRigCache.ActiveRigContainers)
        {
            if (!((Object)(object)activeRigContainer.Rig == (Object)null) && !activeRigContainer.Rig.isLocal)
            {
                list.Add(activeRigContainer.Rig);
                if (list.Count >= 19)
                {
                    break;
                }
            }
        }

        return list;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private string BuildRigSnapshot()
    {
        string text = "";
        foreach (VRRig remoteRig in GetRemoteRigs())
        {
            text = text + ((Object)remoteRig).GetInstanceID() + ",";
        }

        return text;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void BuildFriendsPage()
    {
        AddPageTitle("FRIENDS", Theme.Soft);
        selectionToken = Friends.Revision;
        FriendState  state = Friends.State;
        FriendView[] array = friendsTab == 1 ? state.Incoming : friendsTab == 2 ? state.Outgoing : state.Friends;
        int          num   = friendsTab == 1 ? 3 : friendsTab              != 0 ? 1 : 2;
        int          num2  = 3 + Math.Max(1, array.Length * num);
        if (friendsPage * 10 >= num2)
        {
            friendsPage = 0;
        }

        BeginPagedGrid(friendsPage);
        AddPagedSetting("friendview", "VIEW", friendsTab == 1 ? "INCOMING" : friendsTab == 2 ? "SENT REQUESTS" : "ACCEPTED", delegate
                                                                                                                             {
                                                                                                                                 friendsTab  = (friendsTab + 1) % 3;
                                                                                                                                 friendsPage = 0;
                                                                                                                                 ShowPage(Page.Friends, null);
                                                                                                                             });

        AddPagedSetting("friendcode",  "YOUR FRIEND CODE", state.FriendCode,                                                   null);
        AddPagedSetting("friendstate", "REQUEST STATUS",   Friends.Status.Length > 0 ? Friends.Status : "ACCEPTANCE REQUIRED", null);
        if (array.Length == 0)
        {
            AddPagedSetting("emptyfriends", "", friendsTab == 0 ? "NO ACCEPTED FRIENDS" : "NO REQUESTS", null);
        }

        FriendView[] array2 = array;
        foreach (FriendView friendView0 in array2)
        {
            AddPagedSetting("friend" + friendView0.Id, friendView0.Name, friendView0.Activity(DateTime.UtcNow, state.ReceivedAt), null);
            if (friendsTab == 0)
            {
                AddPagedSetting("remove" + friendView0.Id, friendView0.Name, "REMOVE FRIEND", delegate
                                                                                              {
                                                                                                  Friends.Remove(friendView0.Id);
                                                                                                  ShowPage(Page.Friends, null);
                                                                                              });
            }

            if (friendsTab == 1)
            {
                AddPagedSetting("accept" + friendView0.RequestId, friendView0.Name, "ACCEPT", delegate
                                                                                              {
                                                                                                  Friends.Respond(friendView0.RequestId, true);
                                                                                                  ShowPage(Page.Friends, null);
                                                                                              });

                AddPagedSetting("decline" + friendView0.RequestId, friendView0.Name, "DECLINE", delegate
                                                                                                {
                                                                                                    Friends.Respond(friendView0.RequestId, false);
                                                                                                    ShowPage(Page.Friends, null);
                                                                                                });
            }
        }

        AddPageControls(friendsPage, delegate
                                     {
                                         friendsPage = (friendsPage + 1) % PageCount;
                                         ShowPage(Page.Friends, null);
                                     });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void BuildCustomThemePage()
    {
        if (!Manifest.Has("custom_theme"))
        {
            ShowPage(Page.Settings, null);

            return;
        }

        AddPageTitle("CUSTOM COLOR", Theme.Soft);
        colorPicker                                                                                                                = new ThemeColorPicker();
        Theme.Text(contentRoot.transform, "help", "POINT AND HOLD TO PICK A COLOR", 0.019f, Theme.DimText).transform.localPosition = new Vector3(0f, 0.3f, -0.004f);
        MenuItem menuItem = CreateMenuItem("colorwheel", new Vector3(-0.09f, 0.03f, 0f), 0f);
        menuItem.hitWidth      = 0.205f;
        menuItem.radialHitTest = true;
        menuItem.onActivate    = delegate { };
        menuItem.onDrag = delegate(Vector2 arg1)
                          {
                              colorPicker.SetHueSaturation(arg1 / 0.205f);
                              UpdateCustomThemePreview();
                          };

        Theme.Quad(menuItem.root.transform, "wheel", 0.41f, 0.41f, Theme.Holo(Color.white, 3000, colorPicker.ColorWheelTexture));
        colorWheelMarker = Theme.Quad(contentRoot.transform, "selection", 0.018f, 0.018f, Theme.Holo(Color.white, 3003)).transform;
        MenuItem menuItem2 = CreateMenuItem("brightness", new Vector3(-0.09f, -0.235f, 0f), 0f);
        menuItem2.hitWidth   = 0.41f;
        menuItem2.hitHeight  = 0.038f;
        menuItem2.onActivate = delegate { };
        menuItem2.onDrag = delegate(Vector2 arg1)
                           {
                               colorPicker.SetBrightness(arg1.x / 0.41f + 0.5f);
                               UpdateCustomThemePreview();
                           };

        Theme.Quad(menuItem2.root.transform, "brightness", menuItem2.hitWidth, menuItem2.hitHeight, Theme.Holo(Color.white, 3000, colorPicker.BrightnessTexture));
        Theme.Text(contentRoot.transform, "brightnesslabel", "BRIGHTNESS", 0.018f, Theme.DimText).transform.localPosition = new Vector3(-0.09f, -0.195f, -0.004f);
        brightnessMarker                                                                                                  = Theme.Quad(contentRoot.transform, "brightnessselection", 0.01f, 0.048f, Theme.Holo(Color.white, 3003)).transform;
        colorPreviewRenderer                                                                                              = Theme.Quad(contentRoot.transform, "preview",             0.15f, 0.13f,  Theme.Holo(Theme.Main)).GetComponent<Renderer>();
        colorPreviewRenderer.transform.localPosition                                                                      = new Vector3(0.27f, 0.11f, 0f);
        colorHexLabel                                                                                                     = Theme.Text(contentRoot.transform, "colorvalue", "", 0.022f, Theme.White);
        colorHexLabel.transform.localPosition                                                                             = new Vector3(0.27f, 0.005f, -0.004f);
        AddCard("resetcolor", new Vector3(0.27f, -0.09f, 0f), "", "RESET", delegate
                                                                           {
                                                                               colorPicker.Reset();
                                                                               UpdateCustomThemePreview();
                                                                           }, 0.18f, 0.065f);

        AddCard("donecolor", new Vector3(0.27f, -0.2f, 0f), "", "DONE", delegate
                                                                        {
                                                                            colorPicker.SaveChanges();
                                                                            ReopenSettingsAfterThemeChange();
                                                                        }, 0.18f, 0.065f);

        UpdateCustomThemePreview();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void UpdateCustomThemePreview()
    {
        if (colorPicker != null)
        {
            colorWheelMarker.localPosition            = new Vector3(-0.09f  + colorPicker.WheelPosition.x            * 0.205f, 0.03f + colorPicker.WheelPosition.y * 0.205f, -0.005f);
            brightnessMarker.localPosition            = new Vector3(-0.295f + (colorPicker.Brightness - 0.1f) / 0.9f * 0.41f,  -0.235f,                                      -0.005f);
            colorPreviewRenderer.sharedMaterial.color = colorPicker.SelectedColor;
            colorHexLabel.text                        = "#" + ColorUtility.ToHtmlStringRGB(colorPicker.SelectedColor);
        }
    }

    [CompilerGenerated]
    private void CyclePushToTalkFromMain()
    {
        GameSettings.CyclePushToTalkMode();
        ShowPage(Page.Main, null);
    }

    [CompilerGenerated]
    private void CycleOutfitFromMain()
    {
        GameSettings.CycleOutfit(true);
        ShowPage(Page.Main, null);
    }

    [CompilerGenerated]
    private void OpenDetectedMods()
    {
        detectionKind = Kind.Mod;
        ShowPage(Page.ModList, targetPlayer);
    }

    [CompilerGenerated]
    private void OpenDetectedCheats()
    {
        detectionKind = Kind.Cheat;
        ShowPage(Page.ModList, targetPlayer);
    }

    [CompilerGenerated]
    private void OpenReportPage() => ShowPage(Page.Report, targetPlayer);

    [CompilerGenerated]
    private void ToggleTargetFriend()
    {
        if (Friends.IsFriend(targetPlayer.UserId))
        {
            Friends.Remove(targetPlayer.UserId);
        }
        else
        {
            Friends.Add(targetPlayer.UserId, targetPlayer.NickName);
        }

        ShowPage(Page.Player, targetPlayer);
    }

    [CompilerGenerated]
    private void ReturnToMain() => ShowPage(Page.Main, null);

    [CompilerGenerated]
    private void ReturnToPlayer() => ShowPage(Page.Player, targetPlayer);

    [CompilerGenerated]
    private void SelectCheatingReport()
    {
        selectedReportReason = 1;
        ShowPage(Page.Report, targetPlayer);
    }

    [CompilerGenerated]
    private void SelectToxicityReport()
    {
        selectedReportReason = 2;
        ShowPage(Page.Report, targetPlayer);
    }

    [CompilerGenerated]
    private void SelectHateSpeechReport()
    {
        selectedReportReason = 0;
        ShowPage(Page.Report, targetPlayer);
    }

    [CompilerGenerated]
    private void CancelReport()
    {
        selectedReportReason = -1;
        ShowPage(Page.Player, targetPlayer);
    }

    [CompilerGenerated]
    private void CancelReportConfirmation()
    {
        selectedReportReason = -1;
        ShowPage(Page.Player, targetPlayer);
    }

    [CompilerGenerated]
    private void CycleTheme()
    {
        Cfg.Theme.Value = (Cfg.Theme.Value + 1) % Theme.Count;
        Theme.Apply(Cfg.Theme.Value);
        Plugin.Ins.Disc.Retheme();
        ReopenSettingsAfterThemeChange();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [CompilerGenerated]
    private void CycleNotificationMode()
    {
        Cfg.NotifMode.Value = Cfg.NotifMode.Value == "all" ? "cheats" : Cfg.NotifMode.Value == "cheats" ? "off" : "all";
        ShowPage(Page.Settings, null);
    }

    [CompilerGenerated]
    private void CycleSfxPack()
    {
        Cfg.MenuSfx.Value = (Cfg.MenuSfx.Value + 1) % 4;
        Theme.ClickSound();
        ShowPage(Page.Settings, null);
    }

    [CompilerGenerated]
    private void ToggleMenuLayout()
    {
        Cfg.MenuLayout.Value = !Theme.RectangleMenu ? 1 : 0;
        Plugin.Ins.Disc?.Retheme();
        ReopenSettingsAfterThemeChange();
    }

    [CompilerGenerated]
    private void ToggleFriendSharing()
    {
        Friends.SetSharing(!Friends.ShareActivity);
        ShowPage(Page.Settings, null);
    }

    [CompilerGenerated]
    private void CycleTagFont()
    {
        Cfg.TagFont.Value = (Cfg.TagFont.Value + 1) % 4;
        ShowPage(Page.Settings, null);
    }

    [CompilerGenerated]
    private void CycleTagSize()
    {
        Cfg.TagSize.Value = (Cfg.TagSize.Value + 1) % 3;
        ShowPage(Page.Settings, null);
    }

    [CompilerGenerated]
    private void CycleTagColor()
    {
        Cfg.TagColor.Value = (Cfg.TagColor.Value + 1) % 3;
        ShowPage(Page.Settings, null);
    }

    [CompilerGenerated]
    private void OpenCustomTheme() => ShowPage(Page.CustomTheme, null);

    [CompilerGenerated]
    private void BindOpenButton()
    {
        buttonBindingMode = 2;
        ShowPage(Page.Settings, null);
    }

    [CompilerGenerated]
    private void BindCloseButton()
    {
        buttonBindingMode = 1;
        ShowPage(Page.Settings, null);
    }

    [CompilerGenerated]
    private void NextSettingsPage()
    {
        settingsPage = (settingsPage + 1) % PageCount;
        ShowPage(Page.Settings, null);
    }

    [CompilerGenerated]
    private void CloseSettings() => ShowPage(Page.Main, null);

    [CompilerGenerated]
    private void DisconnectRoom()
    {
        if (PhotonNetwork.InRoom && (Object)(object)NetworkSystem.Instance != (Object)null)
        {
            NetworkSystem.Instance.ReturnToSinglePlayer();
        }

        ShowPage(Page.Room, null);
    }

    [CompilerGenerated]
    private void ReconnectLastRoom() => this.StartCoroutine(JoinRoom(lastRoomCode, null));

    [CompilerGenerated]
    private void JoinPublicRoom() => this.StartCoroutine(JoinRoom(null, FindPublicRoomTrigger()));

    [CompilerGenerated]
    private void OpenJoinCode()
    {
        DesktopJoinStatus = "";
        ShowPage(Page.JoinCode, null);
    }

    [CompilerGenerated]
    private void CycleRoomQueue()
    {
        GameSettings.CycleQueue();
        ShowPage(Page.Room, null);
    }

    [CompilerGenerated]
    private void CycleRoomGameMode()
    {
        GameSettings.CycleGameMode();
        ShowPage(Page.Room, null);
    }

    [CompilerGenerated]
    private void AdvanceTimeOfDay()
    {
        GameSettings.CycleTimeOfDay(1);
        ShowPage(Page.Room, null);
    }

    [CompilerGenerated]
    private void RewindTimeOfDay()
    {
        GameSettings.CycleTimeOfDay(-1);
        ShowPage(Page.Room, null);
    }

    [CompilerGenerated]
    private void ToggleTimeLock()
    {
        GameSettings.ToggleTimeLock();
        ShowPage(Page.Room, null);
    }

    [CompilerGenerated]
    private void ToggleRoomVoiceChat()
    {
        GameSettings.ToggleVoiceChat();
        ShowPage(Page.Room, null);
    }

    [CompilerGenerated]
    private void CycleRoomPushToTalk()
    {
        GameSettings.CyclePushToTalkMode();
        ShowPage(Page.Room, null);
    }

    [CompilerGenerated]
    private void NextRoomPage()
    {
        roomPage = (roomPage + 1) % PageCount;
        ShowPage(Page.Room, null);
    }

    [CompilerGenerated]
    private void BackFromJoinCode() => ShowPage(Page.Room, null);

    [CompilerGenerated]
    private void DeleteJoinCodeCharacter()
    {
        joinCode          = RoomCode.Backspace(joinCode);
        DesktopJoinStatus = "";
        UpdateJoinCodePage();
    }

    [CompilerGenerated]
    private void ClearJoinCode()
    {
        joinCode          = "";
        DesktopJoinStatus = "";
        UpdateJoinCodePage();
    }

    [CompilerGenerated]
    private void SubmitJoinCode()
    {
        if (RoomCode.Valid(joinCode) && !isJoiningRoom)
        {
            this.StartCoroutine(JoinRoom(joinCode, null, true));
        }
    }

    [CompilerGenerated]
    private void OpenSpotify() => ShowPage(Page.Spotify, null);

    [CompilerGenerated]
    private void PreviousDesktopTrack()
    {
        DesktopMediaControls.PreviousTrack();
        nextMediaRefresh = Time.time + 0.9f;
    }

    [CompilerGenerated]
    private void ToggleDesktopPlayback()
    {
        DesktopMediaControls.TogglePlayPause();
        nextMediaRefresh = Time.time + 0.9f;
    }

    [CompilerGenerated]
    private void NextDesktopTrack()
    {
        DesktopMediaControls.NextTrack();
        nextMediaRefresh = Time.time + 0.9f;
    }

    [CompilerGenerated]
    private void CloseDesktopMusic() => ShowPage(Page.Main, null);

    [CompilerGenerated]
    private void BeginSpotifyLogin()
    {
        SpotifyPlayer.OAuth.BeginLogin();
        ShowPage(Page.Spotify, null);
    }

    [CompilerGenerated]
    private void OpenDesktopMusic() => ShowPage(Page.Music, null);

    [MethodImpl(MethodImplOptions.NoInlining)]
    [CompilerGenerated]
    private void ToggleSpotifyPlayback()
    {
        SpotifyPlayer.TogglePlayback();
        playbackLabel.text = SpotifyPlayer.IsPlaying ? "PAUSE" : "PLAY";
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [CompilerGenerated]
    private void ToggleSpotifyShuffle()
    {
        SpotifyPlayer.ToggleShuffle();
        shuffleLabel.text = SpotifyPlayer.ShuffleEnabled ? "ON" : "OFF";
    }

    [CompilerGenerated]
    private void CycleSpotifyRepeat()
    {
        SpotifyPlayer.CycleRepeatMode();
        repeatLabel.text = SpotifyPlayer.RepeatMode.ToUpper();
    }

    [CompilerGenerated]
    private void CloseSpotifyPlayer() => ShowPage(Page.Music, null);

    [CompilerGenerated]
    private void DisconnectSpotify()
    {
        SpotifyPlayer.Disconnect();
        ShowPage(Page.Spotify, null);
    }

    [CompilerGenerated]
    private void CloseLiveChatSetup() => ShowPage(Page.Main, null);

    [CompilerGenerated]
    private void CloseLiveChat() => ShowPage(Page.Main, null);

    [CompilerGenerated]
    private void CycleFriendsTab()
    {
        friendsTab  = (friendsTab + 1) % 3;
        friendsPage = 0;
        ShowPage(Page.Friends, null);
    }

    [CompilerGenerated]
    private void NextFriendsPage()
    {
        friendsPage = (friendsPage + 1) % PageCount;
        ShowPage(Page.Friends, null);
    }

    [CompilerGenerated]
    private void PickThemeColor(Vector2 arg1)
    {
        colorPicker.SetHueSaturation(arg1 / 0.205f);
        UpdateCustomThemePreview();
    }

    [CompilerGenerated]
    private void PickThemeBrightness(Vector2 arg1)
    {
        colorPicker.SetBrightness(arg1.x / 0.41f + 0.5f);
        UpdateCustomThemePreview();
    }

    [CompilerGenerated]
    private void ResetCustomTheme()
    {
        colorPicker.Reset();
        UpdateCustomThemePreview();
    }

    [CompilerGenerated]
    private void SaveCustomTheme()
    {
        colorPicker.SaveChanges();
        ReopenSettingsAfterThemeChange();
    }

    private enum Page
    {
        Main,
        Player,
        ModList,
        Report,
        Settings,
        Room,
        JoinCode,
        Music,
        Friends,
        CustomTheme,
        Spotify,
        Chat,
    }

    private class MenuItem
    {

        public readonly List<float> rendererBaseAlpha = new();

        public readonly List<Renderer> renderers = new();

        public readonly List<TMP_Text> texts = new();

        public float animationDelay;

        public float createdAt;

        public bool highlighted;

        public float hitHeight;

        public float hitWidth;

        public Action onActivate;

        public Action<Vector2> onDrag;

        public Theme.Panel panel;

        public bool radialHitTest;

        public VRRig      rig;
        public GameObject root;

        public float scale = 1f;

        public Vector3 targetPosition;

        public string tooltip;

        public TMP_Text valueText;

        public bool visible;
    }
}