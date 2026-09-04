using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx.Configuration;
using GorillaNetworking;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;

namespace Sentinel.Sentinel;

public class RingMenu : MonoBehaviour
{

    private const float float_0 = 0.5f;

    private const int int_0 = 10;

    public static RingMenu Ins;

    public static readonly string[] BtnNames;

    private static readonly Vector3 contentOffset;

    private readonly bool[] bool_10 = new bool[4];

    private readonly List<MenuItem> menuItems = new();

    private readonly HashSet<string> reportedUserIds = new();

    private int activeGridPage;

    private bool anchoredToLeftHand;

    private int buttonBindingMode;

    private bool closeButtonDown;

    private float closeProgress;

    private GameObject contentRoot;

    private Page currentPage;

    private Kind detectionKind;

    private int displayedChatVersion;

    private Transform handAnchor;

    private int hoveredItemIndex = -1;

    private float hoverExitDelay;

    private Transform innerBand;

    private Transform innerEdge;

    private float inputCooldownUntil;

    private bool isClosing;

    private bool isJoiningRoom;

    private bool isOpen;

    private Transform laserDot;

    private float lastCloseTapTime = -10f;

    private string lastRoomCode;

    private bool lastSpotifyConnected;

    private int lastTouchedItem = -1;

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

    private LineRenderer pointerLine;

    private Renderer renderer_0;

    private string rigSnapshot = "";

    private int roomPage;

    private bool rotatingToTarget;

    private Transform secondaryEdge;

    private int selectedReportReason = -1;

    private int settingsPage;

    private Player targetPlayer;

    private TMP_Text tmp_Text_0;

    private TMP_Text tmp_Text_1;

    private TMP_Text tmp_Text_10;

    private TMP_Text[] tmp_Text_11;

    private TMP_Text tmp_Text_2;

    private TMP_Text tmp_Text_3;

    private TMP_Text tmp_Text_4;

    private TMP_Text tmp_Text_5;

    private TMP_Text tmp_Text_6;

    private TMP_Text tmp_Text_7;

    private TMP_Text tmp_Text_8;

    private TMP_Text tmp_Text_9;

    private float transitionProgress;

    private GameObject transitionRing;

    private bool triggerDown;

    private Vector3 worldAnchorTarget;

    static RingMenu()
    {
        BtnNames      = new string[4] { "X", "Y", "A", "B", };
        contentOffset = new Vector3(0f, 0f, -0.002f);
    }

    public static bool IsOpen
    {
        get
        {
            if (Ins != null)
            {
                return Ins.isOpen;
            }

            return false;
        }
    }

    private int PageCount => Mathf.Max(1, (pagedItemCount + 10 - 1) / 10);

    private static Color InactiveBorderColor => new(Theme.Main.r, Theme.Main.g, Theme.Main.b, 0.35f);

    private bool UseLeftHand => Cfg.SwapHands.Value;

    private void Awake() => Ins = this;

    private void Update()
    {
        if (PhotonNetwork.InRoom && NetworkSystem.Instance != null)
        {
            lastRoomCode = NetworkSystem.Instance.RoomName;
        }

        TryAutoJoin();
        HandleOpenButton();
        if (!isOpen)
        {
            return;
        }

        if (!(menuRoot == null))
        {
            GorillaTagger instance = GorillaTagger.Instance;
            if (instance == null || instance.mainCamera == null)
            {
                return;
            }

            Transform transform = instance.mainCamera.transform;
            if (isClosing)
            {
                closeProgress += Time.deltaTime;
                float   num = Theme.Ease(closeProgress / 0.4f);
                Vector3 val = instance.leftHandTransform != null ? instance.leftHandTransform.position : transform.position;
                menuRoot.transform.position = Vector3.Lerp(menuRoot.transform.position, val, num);
                menuFace.localScale         = Vector3.one * Mathf.Lerp(openProgress, 0.02f, num) * menuScale;
                menuFace.localRotation      = Quaternion.Euler(0f, 0f, num * 50f);
                pointerLine.enabled         = false;
                laserDot.gameObject.SetActive(false);
                if (closeProgress >= 0.42f)
                {
                    CloseImmediately();
                }

                return;
            }

            openProgress = Mathf.MoveTowards(openProgress, 1f, Time.deltaTime / 0.45f);
            float num2 = Theme.Snap(openProgress);
            menuFace.localScale     = Vector3.one * num2 * menuScale;
            innerBand.localRotation = Quaternion.Euler(0f, 0f, (1f - num2) * -55f);
            Transform  obj           = outerEdge;
            Quaternion localRotation = secondaryEdge.localRotation = Quaternion.Euler(0f, 0f, (1f - num2) * 90f);
            obj.localRotation       = localRotation;
            innerEdge.localRotation = Quaternion.Euler(0f, 0f, (1f - num2) * -140f);
            if (transitionRing != null)
            {
                transitionProgress                  += Time.deltaTime / 0.45f;
                transitionRing.transform.localScale =  Vector3.one    * Mathf.Lerp(0.85f, 1.5f, Theme.Ease(transitionProgress));
                Renderer componentInChildren = transitionRing.GetComponentInChildren<Renderer>();
                if (componentInChildren != null)
                {
                    Color color = componentInChildren.material.color;
                    color.a                            = 0.5f * (1f - transitionProgress);
                    componentInChildren.material.color = color;
                }

                if (transitionProgress >= 1f)
                {
                    Destroy(transitionRing);
                    transitionRing = null;
                }
            }

            if (handAnchor != null)
            {
                UpdateHandAnchor(transform);
            }
            else
            {
                UpdateWorldAnchor(transform);
            }

            foreach (MenuItem menuItem in menuItems)
            {
                float num3 = Theme.Snap((Time.time - menuItem.createdAt - menuItem.animationDelay) / 0.3f);
                menuItem.root.transform.localScale = Vector3.one * Mathf.Max(0.001f, num3 * menuItem.scale * (menuItem.highlighted ? 1.07f : 1f));
                if (menuItem.visible)
                {
                    continue;
                }

                menuItem.root.transform.localPosition = Vector3.LerpUnclamped(menuItem.targetPosition * 0.25f, menuItem.targetPosition, num3);
                float num4 = Mathf.Clamp01((Time.time - menuItem.createdAt - menuItem.animationDelay) / 0.18f);
                for (int i = 0; i < menuItem.renderers.Count; i++)
                {
                    if (!(menuItem.renderers[i] == null))
                    {
                        Color color2 = menuItem.renderers[i].material.color;
                        color2.a                             = menuItem.rendererBaseAlpha[i] * num4;
                        menuItem.renderers[i].material.color = color2;
                    }
                }

                foreach (TMP_Text text in menuItem.texts)
                {
                    if (text != null)
                    {
                        text.alpha = num4;
                    }
                }

                if (num3 >= 1f && num4 >= 1f)
                {
                    menuItem.visible = true;
                }
            }

            HandleButtonBinding();
            if (isClosing)
            {
                return;
            }

            HandleLaserInput(instance);
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
        }
        else
        {
            CloseImmediately();
        }
    }

    private void UpdateHandAnchor(Transform transform_7)
    {
        if (!Disc.Glancing(transform_7, handAnchor, anchoredToLeftHand))
        {
            BeginClose();

            return;
        }

        Vector3 val = handAnchor.position + Disc.PalmNormal(handAnchor, anchoredToLeftHand) * 0.16f;
        menuRoot.transform.position = Vector3.Lerp(menuRoot.transform.position, val, 1f - Mathf.Exp((0f - Time.deltaTime) * 16f));
        Vector3 val2 = menuRoot.transform.position - transform_7.position;
        if (val2.sqrMagnitude >= 0.0004f)
        {
            menuRoot.transform.rotation = Quaternion.Slerp(menuRoot.transform.rotation, Quaternion.LookRotation(val2, transform_7.up), 1f - Mathf.Exp((0f - Time.deltaTime) * 11f));
        }
    }

    private void UpdateWorldAnchor(Transform transform_7)
    {
        Vector3 val       = menuRoot.transform.position - transform_7.position;
        float   magnitude = val.magnitude;
        if (movingToTarget)
        {
            Vector3 val2 = menuRoot.transform.position - worldAnchorTarget;
            if (val2.sqrMagnitude < 0.006f)
            {
                movingToTarget = false;
            }
        }
        else if (magnitude > 2.3f || magnitude < 0.5f || Vector3.Angle(transform_7.forward, val) > 78f)
        {
            worldAnchorTarget = GameSettings.GroundPointAhead(transform_7, 1.3f);
            movingToTarget    = true;
        }

        menuRoot.transform.position = Vector3.Lerp(menuRoot.transform.position, worldAnchorTarget, 1f - Mathf.Exp((0f - Time.deltaTime) * 3.5f));
        Vector3 val3 = menuRoot.transform.position - transform_7.position;
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

    public void Open(Vector3 pos, Player target)
    {
        if (isOpen)
        {
            CloseImmediately();
        }

        isOpen            = true;
        isClosing         = false;
        handAnchor        = null;
        menuScale         = 1f;
        openProgress      = 0.05f;
        triggerDown       = true;
        worldAnchorTarget = pos;
        rotatingToTarget  = false;
        movingToTarget    = false;
        menuRoot          = new GameObject("zx_menu");
        DontDestroyOnLoad(menuRoot);
        menuRoot.transform.position = pos;
        Vector3 val = pos - GorillaTagger.Instance.mainCamera.transform.position;
        val.y                       = 0f;
        menuRoot.transform.rotation = Quaternion.LookRotation(val.sqrMagnitude > 0.001f ? val : Vector3.forward);
        menuFace                    = new GameObject("face").transform;
        menuFace.SetParent(menuRoot.transform, false);
        menuFace.localScale = Vector3.one * 0.02f;
        Color val2 = new(Theme.Main.r, Theme.Main.g, Theme.Main.b, 0.16f);
        innerBand     = Theme.Ring(menuFace, "band",     0.415f, 0.55f,  val2,       2992).transform;
        outerEdge     = Theme.Ring(menuFace, "edgeOut",  0.55f,  0.558f, Theme.Main, 2993, 8f,   152f).transform;
        secondaryEdge = Theme.Ring(menuFace, "edgeOut2", 0.55f,  0.558f, Theme.Main, 2993, 188f, 152f).transform;
        innerEdge     = Theme.Ring(menuFace, "edgeIn",   0.408f, 0.414f, Theme.Soft, 2993, 250f, 220f).transform;
        Theme.Ring(menuFace, "glow", 0.4f, 0.6f, new Color(val2.r, val2.g, val2.b, 0.05f), 2990);
        CreateTransitionRing();
        pointerLine = new GameObject("zx_laser").AddComponent<LineRenderer>();
        DontDestroyOnLoad(pointerLine.gameObject);
        pointerLine.startWidth = 0.004f;
        pointerLine.endWidth   = 0.0015f;
        pointerLine.material   = Theme.Holo(new Color(val2.r, val2.g, val2.b, 0.6f), 3010);
        GameObject val3 = Theme.Ring(null, "zx_dot", 0f, 0.011f, Theme.Soft, 3011);
        DontDestroyOnLoad(val3);
        laserDot = val3.transform;
        ShowPage(target != null ? Page.Player : Page.Main, target);
        Theme.ClickSound();
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
            Theme.BackSound();
            Theme.Haptic(false, 0.4f, 0.06f);
        }
    }

    private void CreateTransitionRing()
    {
        if (transitionRing != null)
        {
            Destroy(transitionRing);
        }

        transitionRing = new GameObject("shock");
        transitionRing.transform.SetParent(menuFace, false);
        Theme.Ring(transitionRing.transform, "ring", 0.545f, 0.565f, new Color(Theme.Soft.r, Theme.Soft.g, Theme.Soft.b, 0.5f), 2994);
        transitionProgress = 0f;
    }

    private static bool IsControllerButtonPressed(ControllerInputPoller controllerInputPoller_0, int int_11) =>
            int_11 switch
            {
                    0     => controllerInputPoller_0.leftControllerPrimaryButton,
                    2     => controllerInputPoller_0.rightControllerPrimaryButton,
                    1     => controllerInputPoller_0.leftControllerSecondaryButton,
                    var _ => controllerInputPoller_0.rightControllerSecondaryButton,
            };

    private void TryAutoJoin()
    {
        if (Manifest.Has("auto_join") && Cfg.AutoJoin.Value && !isJoiningRoom && Time.time >= nextAutoJoinAttempt && !PhotonNetwork.InRoom && PhotonNetwork.IsConnectedAndReady)
        {
            nextAutoJoinAttempt = Time.time + 3f;
            GorillaNetworkJoinTrigger val = FindPublicRoomTrigger();
            if (val != null)
            {
                StartCoroutine(JoinRoom(null, val));
            }
        }
    }

    private void HandleOpenButton()
    {
        ControllerInputPoller instance = ControllerInputPoller.instance;
        if (instance == null)
        {
            return;
        }

        bool flag;
        bool num = (flag = IsControllerButtonPressed(instance, Mathf.Clamp(Cfg.OpenBtn.Value, 0, 3))) && !openButtonDown;
        openButtonDown = flag;
        if (num && !isOpen && Time.time >= inputCooldownUntil)
        {
            GorillaTagger instance2 = GorillaTagger.Instance;
            if (!(instance2 == null) && !(instance2.mainCamera == null))
            {
                Open(GameSettings.GroundPointAhead(instance2.mainCamera.transform, 1.3f), null);
            }
        }
    }

    private void HandleButtonBinding()
    {
        ControllerInputPoller instance = ControllerInputPoller.instance;
        if (instance == null)
        {
            return;
        }

        if (buttonBindingMode == 0)
        {
            for (int i = 0; i < 4; i++)
            {
                bool_10[i] = IsControllerButtonPressed(instance, i);
            }

            bool flag;
            if ((flag = IsControllerButtonPressed(instance, Mathf.Clamp(Cfg.CloseBtn.Value, 0, 3))) && !closeButtonDown)
            {
                if (Time.time - lastCloseTapTime < 0.4f)
                {
                    BeginClose();
                }

                lastCloseTapTime = Time.time;
            }

            closeButtonDown = flag;

            return;
        }

        for (int j = 0; j < 4; j++)
        {
            bool flag2;
            if ((flag2 = IsControllerButtonPressed(instance, j)) && !bool_10[j])
            {
                if (buttonBindingMode == 1)
                {
                    Cfg.CloseBtn.Value = j;
                }
                else
                {
                    Cfg.OpenBtn.Value = j;
                }

                buttonBindingMode = 0;
                openButtonDown    = true;
                Theme.ClickSound();
                Theme.Haptic(j < 2, 0.5f, 0.05f);
                ShowPage(Page.Settings, null);
            }

            bool_10[j] = flag2;
        }
    }

    private void CloseImmediately()
    {
        isClosing         = false;
        isOpen            = false;
        lastTouchedItem   = -1;
        hoveredItemIndex  = -1;
        buttonBindingMode = 0;
        handAnchor        = null;
        menuScale         = 1f;
        Outline.Clear();
        menuItems.Clear();
        transitionRing = null;
        ClearUiReferences();
        if (pointerLine != null)
        {
            Destroy(pointerLine.gameObject);
        }

        if (laserDot != null)
        {
            Destroy(laserDot.gameObject);
        }

        if (menuRoot != null)
        {
            Destroy(menuRoot);
        }
    }

    private void ClearUiReferences()
    {
        tmp_Text_0  = tmp_Text_1 = tmp_Text_2 = tmp_Text_3 = tmp_Text_4 = tmp_Text_5 = tmp_Text_6 = tmp_Text_7 = tmp_Text_8 = tmp_Text_9 = tmp_Text_10 = null;
        tmp_Text_11 = null;
        renderer_0  = null;
    }

    private void ShowPage(Page enum1_1, Player player_1)
    {
        currentPage = enum1_1;
        if (player_1 != null || enum1_1 == Page.Main)
        {
            targetPlayer = player_1;
        }

        hoveredItemIndex = -1;
        Outline.Clear();
        menuItems.Clear();
        ClearUiReferences();
        if (contentRoot != null)
        {
            Destroy(contentRoot);
        }

        contentRoot = new GameObject("content");
        contentRoot.transform.SetParent(menuFace, false);
        if (openProgress >= 1f)
        {
            CreateTransitionRing();
        }

        switch (enum1_1)
        {
            case Page.Main:
                BuildMainPage();

                break;

            case Page.Player:
                BuildPlayerPage();

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

    private void BuildMainPage()
    {
        rigSnapshot = BuildRigSnapshot();
        AddPlayerPreview(GorillaTagger.Instance != null ? GorillaTagger.Instance.offlineVRRig : null, PhotonNetwork.InRoom ? PhotonNetwork.LocalPlayer.NickName : "YOU");
        bool        plus       = Manifest.Plus;
        bool        flag       = Manifest.Has("friends");
        int         num        = 3 + (plus ? 2 : 0) + (flag ? 1 : 0);
        float       num2       = num > 4 ? 260f : 300f;
        List<VRRig> remoteRigs = GetRemoteRigs();
        int         count      = remoteRigs.Count;
        float       float_     = count <= 9 ? 1f : 0.72f;
        for (int i = 0; i < count; i++)
        {
            AddPlayerButton(remoteRigs[i], (0f - num2) / 2f + num2 * (i + 0.5f) / count, float_, i);
        }

        if (count == 0)
        {
            Theme.Text(contentRoot.transform, "empty", "NOBODY ELSE HERE", 0.032f, Theme.DimText).transform.localPosition = PolarPosition(0f, 0.5f) + contentOffset;
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

        float num3    = num > 4 ? 17f : 20f;
        float float_2 = 180f - num3 * (num - 1) / 2f;
        AddNavigationIcon("settings", ref float_2, num3, Theme.SettingIcon, "SETTINGS", Page.Settings);
        AddNavigationIcon("music",    ref float_2, num3, null,              "MUSIC",    Page.Music);
        AddNavigationIcon("room",     ref float_2, num3, Theme.RoomIcon,    "ROOM",     Page.Room);
        if (plus)
        {
            AddNavigationIcon("spotify", ref float_2, num3, Theme.SpotifyIcon, "SPOTIFY", Page.Spotify);
            AddNavigationIcon("chat",    ref float_2, num3, Theme.ChatIcon,    "CHAT",    Page.Chat);
        }

        if (flag)
        {
            AddNavigationIcon("friends", ref float_2, num3, null, "FRIENDS", Page.Friends);
        }
    }

    private void AddNavigationIcon(string string_2, ref float float_12, float float_13, Texture texture_0, string string_3, Page enum1_1)
    {
        AddNavigationButton(string_2, float_12, texture_0, string_3, delegate
                                                                     {
                                                                         ShowPage(enum1_1, null);
                                                                     });

        float_12 += float_13;
    }

    private void BuildPlayerPage()
    {
        VRRig val = Detect.RigOf(targetPlayer);
        AddPlayerPreview(val, targetPlayer != null ? targetPlayer.NickName : "?");
        Scan scan = Detect.Get(targetPlayer);
        tmp_Text_0 = AddCard("fps", PolarPosition(-52f, 0.5f), "FPS", "...", null, 0.28f).valueText;
        string text = Detect.PlatformOf(val);
        Theme.Quad(AddCard("plat", PolarPosition(52f, 0.5f), "PLATFORM", text.ToUpper(), null, 0.28f).root.transform, "icon", 0.034f, 0.034f, Theme.Holo(Color.white, 2997, Detect.PlatformIcon(text))).transform.localPosition = new Vector3(-0.108f, -0.014f, -0.002f);
        AddCard("mods", PolarPosition(-100f, 0.5f), "MODS", scan.Mods.Count.ToString(), delegate
                                                                                        {
                                                                                            detectionKind = Kind.Mod;
                                                                                            ShowPage(Page.ModList, targetPlayer);
                                                                                        }, 0.28f).valueText.color = scan.Mods.Count > 0 ? Theme.Warn : Theme.White;

        AddCard("cheats", PolarPosition(100f, 0.5f), "CHEATS", scan.Cheats.Count.ToString(), delegate
                                                                                             {
                                                                                                 detectionKind = Kind.Cheat;
                                                                                                 ShowPage(Page.ModList, targetPlayer);
                                                                                             }, 0.28f).valueText.color = scan.Cheats.Count > 0 ? Theme.Bad : Theme.White;

        tmp_Text_1 = AddCard("created", PolarPosition(-143f, 0.5f), "CREATED", Detect.CreatedDate(targetPlayer != null ? targetPlayer.UserId : null), null, 0.28f).valueText;
        if (targetPlayer != null && Net.Has(targetPlayer.UserId))
        {
            AddCard("zx", PolarPosition(-26f, 0.585f), "SENTINEL", Net.MenuOpen(targetPlayer.UserId) ? "MENU OPEN" : "INSTALLED", null, 0.27f).valueText.color = Theme.Main;
        }

        bool flag = targetPlayer != null && reportedUserIds.Contains(targetPlayer.UserId);
        AddCard("report", PolarPosition(180f, 0.5f), "REPORT", flag ? "REPORTED" : "SELECT", flag ? null : delegate
                                                                                                           {
                                                                                                               ShowPage(Page.Report, targetPlayer);
                                                                                                           }, 0.26f).valueText.color = flag ? Theme.Bad : Theme.DimText;

        bool flag2 = IsTargetMuted();
        tmp_Text_2       = AddCard("mute", PolarPosition(143f, 0.5f), "MUTE", flag2 ? "MUTED" : "OFF", ToggleTargetMute, 0.28f).valueText;
        tmp_Text_2.color = flag2 ? Theme.Bad : Theme.White;
        if (Manifest.Has("friends") && targetPlayer != null)
        {
            bool flag3 = Friends.IsFriend(targetPlayer.UserId);
            AddCard("friend", PolarPosition(26f, 0.585f), flag3 ? "UNFRIEND" : "FRIEND", flag3 ? "REMOVE" : "ADD", delegate
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
                                                                                                                   }, 0.27f, 0.064f).valueText.color = flag3 ? Theme.Bad : Theme.Good;
        }

        AddBackButton(PolarPosition(0f, 0.5f), delegate
                                               {
                                                   ShowPage(Page.Main, null);
                                               });
    }

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
            Theme.Text(contentRoot.transform, "more", "+" + (list.Count - num) + " MORE", 0.026f, Theme.DimText).transform.localPosition = new Vector3(0f, -0.3f, 0f) + contentOffset;
        }

        if (list.Count == 0)
        {
            Theme.Text(contentRoot.transform, "none", "CLEAN", 0.04f, Theme.Good).transform.localPosition = contentOffset;
        }

        AddBackButton(PolarPosition(180f, 0.5f), delegate
                                                 {
                                                     ShowPage(Page.Player, targetPlayer);
                                                 });
    }

    private void BuildReportPage()
    {
        string text = targetPlayer == null ? "?" : targetPlayer.NickName;
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
            Theme.Text(contentRoot.transform, "sub", "FOR " + text2 + "?", 0.03f, Theme.DimText).transform.localPosition          = new Vector3(0f, 0.1f, 0f) + contentOffset;
            AddCard("confirm", new Vector3(-0.12f, -0.03f, 0f), "", "CONFIRM", SubmitPlayerReport, 0.22f, 0.078f).valueText.color = Theme.Bad;
            AddCard("cancel", new Vector3(0.12f, -0.03f, 0f), "", "CANCEL", delegate
                                                                            {
                                                                                selectedReportReason = -1;
                                                                                ShowPage(Page.Player, targetPlayer);
                                                                            }, 0.22f, 0.078f);
        }
    }

    private void BuildSettingsPage()
    {
        AddPageTitle("SETTINGS", Theme.Soft);
        BeginPagedGrid(settingsPage);
        AddPagedSetting("theme", "THEME", Theme.Name(Cfg.Theme.Value), delegate
                                                                       {
                                                                           Cfg.Theme.Value = (Cfg.Theme.Value + 1) % Theme.Count;
                                                                           Theme.Apply(Cfg.Theme.Value);
                                                                           Plugin.Ins.Disc.Retheme();
                                                                           ReopenSettingsAfterThemeChange();
                                                                       });

        AddPagedSetting("notifs", "NOTIFS", Cfg.NotifMode.Value.ToUpper(), delegate
                                                                           {
                                                                               Cfg.NotifMode.Value = Cfg.NotifMode.Value switch
                                                                                                     {
                                                                                                             "all"    => "cheats",
                                                                                                             "cheats" => "off",
                                                                                                             var _    => "all",
                                                                                                     };

                                                                               ShowPage(Page.Settings, null);
                                                                           });

        AddBooleanSetting("sounds",  "SOUNDS",         Cfg.Sounds);
        AddBooleanSetting("tags",    "NAMETAGS",       Cfg.Tags);
        AddBooleanSetting("tagfps",  "TAG FPS",        Cfg.TagFps);
        AddBooleanSetting("tagplat", "TAG PLATFORM",   Cfg.TagPlat);
        AddBooleanSetting("tagzx",   "CLIENT CHECKER", Cfg.TagMenu);
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
        if (Manifest.Has("hand_menu"))
        {
            AddBooleanSetting("handmenu", "HAND MENU", Cfg.HandMenu);
        }

        if (Manifest.Has("net"))
        {
            AddBooleanSetting("beacon", "BROADCAST", Cfg.Broadcast);
        }

        if (Manifest.Has("auto_join"))
        {
            AddBooleanSetting("autojoin", "AUTO JOIN", Cfg.AutoJoin);
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
        Transform val      = handAnchor;
        bool      left     = anchoredToLeftHand;
        Vector3   position = menuRoot.transform.position;
        CloseImmediately();
        if (!(val != null))
        {
            Open(position, null);
        }
        else
        {
            OpenOnHand(val, left);
        }

        ShowPage(Page.Settings, null);
    }

    private void BeginPagedGrid(int int_11)
    {
        pagedItemCount    = 0;
        pagedSettingCount = 0;
        activeGridPage    = int_11;
    }

    private void AddBooleanSetting(string string_2, string string_3, ConfigEntry<bool> configEntry_0) =>
            AddPagedSetting(string_2, string_3, configEntry_0.Value ? "ON" : "OFF", delegate
                                                                                    {
                                                                                        configEntry_0.Value = !configEntry_0.Value;
                                                                                        ShowPage(currentPage, targetPlayer);
                                                                                    });

    private void AddPagedSetting(string string_2, string string_3, string string_4, Action action_0)
    {
        int num = pagedSettingCount++;
        pagedItemCount = pagedSettingCount;
        if (num / 10 == activeGridPage)
        {
            int num2 = num % 10;
            AddCard(string_2, new Vector3(num2 % 2 == 0 ? -0.16f : 0.16f, 0.205f - num2 / 2 * 0.095f, 0f), string_3, string_4, action_0, 0.3f, 0.085f);
        }
    }

    private void AddPageControls(int int_11, Action action_0)
    {
        if (PageCount > 1)
        {
            AddCard("page", new Vector3(-0.12f, -0.275f, 0f), "", "PAGE " + (int_11 + 1) + "/" + PageCount, action_0, 0.22f, 0.064f);
        }

        AddBackButton(new Vector3(PageCount > 1 ? 0.12f : 0f, -0.275f, 0f), delegate
                                                                            {
                                                                                ShowPage(Page.Main, null);
                                                                            });
    }

    private void BuildRoomPage()
    {
        string text = "NOT IN ROOM";
        int    num  = 0;
        if (PhotonNetwork.InRoom && NetworkSystem.Instance != null)
        {
            text = NetworkSystem.Instance.RoomName;
            num  = NetworkSystem.Instance.RoomPlayerCount;
        }

        AddPageTitle(text + (num > 0 ? "  (" + num + ")" : ""), Theme.Soft);
        BeginPagedGrid(roomPage);
        AddPagedSetting("leave", "SESSION", "DISCONNECT", delegate
                                                          {
                                                              if (PhotonNetwork.InRoom && NetworkSystem.Instance != null)
                                                              {
                                                                  NetworkSystem.Instance.ReturnToSinglePlayer();
                                                              }

                                                              ShowPage(Page.Room, null);
                                                          });

        AddPagedSetting("rejoin", "LAST ROOM", "RECONNECT", delegate
                                                            {
                                                                StartCoroutine(JoinRoom(lastRoomCode, null));
                                                            });

        AddPagedSetting("hop", "PUBLIC", "LOBBY HOP", delegate
                                                      {
                                                          StartCoroutine(JoinRoom(null, FindPublicRoomTrigger()));
                                                      });

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

    private void BuildDesktopMusicPage()
    {
        AddPageTitle("MUSIC", Theme.Soft);
        tmp_Text_3                         = Theme.Text(contentRoot.transform, "track", DesktopMediaControls.TrackTitle, 0.028f, Theme.White);
        tmp_Text_3.transform.localPosition = new Vector3(0f, 0.2f, 0f) + contentOffset;
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

        AddCard("voldown", new Vector3(-0.12f, -0.065f, 0f), "", "VOL -", DesktopMediaControls.Pause, 0.22f, 0.09f);
        AddCard("volup",   new Vector3(0.12f,  -0.065f, 0f), "", "VOL +", DesktopMediaControls.Play,  0.22f, 0.09f);
        AddBackButton(new Vector3(0f, -0.2f, 0f), delegate
                                                  {
                                                      ShowPage(Page.Main, null);
                                                  });
    }

    private void BuildSpotifyPage()
    {
        AddPageTitle("SPOTIFY", Theme.Soft);
        lastSpotifyConnected = SpotifyPlayer.OAuth.IsConnected;
        if (lastSpotifyConnected)
        {
            GameObject val = Theme.Quad(contentRoot.transform, "art", 0.13f, 0.13f, Theme.Holo(!(SpotifyPlayer.AlbumArt != null) ? Theme.Fill : Color.white, 2997, SpotifyPlayer.AlbumArt));
            val.transform.localPosition = new Vector3(-0.27f, 0.17f, 0f) + contentOffset;
            renderer_0                  = val.GetComponent<Renderer>();
            tmp_Text_3                  = AddPositionedText("track",  TruncateText(SpotifyPlayer.TrackName,  24), 0.026f, Theme.White,   -0.19f, 0.205f);
            tmp_Text_4                  = AddPositionedText("artist", TruncateText(SpotifyPlayer.ArtistName, 30), 0.02f,  Theme.DimText, -0.19f, 0.165f);
            tmp_Text_5                  = AddPositionedText("time",   FormatPlaybackProgress(),                   0.018f, Theme.DimText, -0.19f, 0.13f);
            AddCard("prev", new Vector3(-0.23f, 0.04f, 0f), "", "PREV", SpotifyPlayer.PreviousTrack, 0.2f, 0.08f);
            tmp_Text_6 = AddCard("play", new Vector3(0f, 0.04f, 0f), "", SpotifyPlayer.IsPlaying ? "PAUSE" : "PLAY", delegate
                                                                                                                     {
                                                                                                                         SpotifyPlayer.TogglePlayback();
                                                                                                                         tmp_Text_6.text = SpotifyPlayer.IsPlaying ? "PAUSE" : "PLAY";
                                                                                                                     }, 0.22f, 0.08f).valueText;

            AddCard("next", new Vector3(0.23f, 0.04f, 0f), "", "NEXT", SpotifyPlayer.NextTrack, 0.2f, 0.08f);
            AddCard("voldown", new Vector3(-0.255f, -0.055f, 0f), "", "VOL -", delegate
                                                                               {
                                                                                   SpotifyPlayer.SetVolume(SpotifyPlayer.VolumePercent - 10);
                                                                                   tmp_Text_9.text = SpotifyPlayer.VolumePercent + "%";
                                                                               }, 0.15f, 0.075f);

            tmp_Text_9 = AddCard("vol", new Vector3(0f, -0.055f, 0f), "VOLUME", SpotifyPlayer.VolumePercent + "%", null, 0.2f, 0.075f).valueText;
            AddCard("volup", new Vector3(0.255f, -0.055f, 0f), "", "VOL +", delegate
                                                                            {
                                                                                SpotifyPlayer.SetVolume(SpotifyPlayer.VolumePercent + 10);
                                                                                tmp_Text_9.text = SpotifyPlayer.VolumePercent + "%";
                                                                            }, 0.15f, 0.075f);

            tmp_Text_7 = AddCard("shuf", new Vector3(-0.15f, -0.145f, 0f), "SHUFFLE", SpotifyPlayer.ShuffleEnabled ? "ON" : "OFF", delegate
                                                                                                                                   {
                                                                                                                                       SpotifyPlayer.ToggleShuffle();
                                                                                                                                       tmp_Text_7.text = SpotifyPlayer.ShuffleEnabled ? "ON" : "OFF";
                                                                                                                                   }, 0.26f, 0.075f).valueText;

            tmp_Text_8 = AddCard("rep", new Vector3(0.15f, -0.145f, 0f), "REPEAT", SpotifyPlayer.RepeatMode.ToUpper(), delegate
                                                                                                                       {
                                                                                                                           SpotifyPlayer.CycleRepeatMode();
                                                                                                                           tmp_Text_8.text = SpotifyPlayer.RepeatMode.ToUpper();
                                                                                                                       }, 0.26f, 0.075f).valueText;

            AddBackButton(new Vector3(-0.09f, -0.24f, 0f), delegate
                                                           {
                                                               ShowPage(Page.Main, null);
                                                           });

            AddCard("logout", new Vector3(0.12f, -0.24f, 0f), "", "LOGOUT", delegate
                                                                            {
                                                                                SpotifyPlayer.Disconnect();
                                                                                ShowPage(Page.Spotify, null);
                                                                            }, 0.16f, 0.064f).valueText.color = Theme.DimText;

            return;
        }

        tmp_Text_3                         = Theme.Text(contentRoot.transform, "track", SpotifyPlayer.OAuth.ClientId.Length == 0 ? "PUT YOUR CLIENT ID IN CONFIG.CFG" : SpotifyPlayer.OAuth.StatusMessage.Length > 0 ? SpotifyPlayer.OAuth.StatusMessage : "CONNECT YOUR SPOTIFY", 0.024f, Theme.DimText);
        tmp_Text_3.transform.localPosition = new Vector3(0f, 0.12f, 0f) + contentOffset;
        if (SpotifyPlayer.OAuth.ClientId.Length > 0)
        {
            AddCard("login", new Vector3(0f, 0.01f, 0f), "", SpotifyPlayer.OAuth.LoginPending ? "WAITING..." : "LOGIN TO SPOTIFY", delegate
                                                                                                                                   {
                                                                                                                                       SpotifyPlayer.OAuth.BeginLogin();
                                                                                                                                       ShowPage(Page.Spotify, null);
                                                                                                                                   }, 0.3f, 0.08f);
        }

        AddBackButton(new Vector3(0f, -0.14f, 0f), delegate
                                                   {
                                                       ShowPage(Page.Main, null);
                                                   });
    }

    private void UpdateSpotifyPage()
    {
        if (SpotifyPlayer.OAuth.IsConnected != lastSpotifyConnected)
        {
            ShowPage(Page.Spotify, null);
        }
        else if (lastSpotifyConnected)
        {
            SpotifyPlayer.PollPlayback();
            if (!(Time.time < nextMediaRefresh) && !(tmp_Text_3 == null))
            {
                nextMediaRefresh = Time.time + 0.5f;
                tmp_Text_3.text  = TruncateText(SpotifyPlayer.TrackName,  24);
                tmp_Text_4.text  = TruncateText(SpotifyPlayer.ArtistName, 30);
                tmp_Text_5.text  = SpotifyPlayer.PremiumRequired ? "SPOTIFY PREMIUM NEEDED" : FormatPlaybackProgress();
                tmp_Text_5.color = SpotifyPlayer.PremiumRequired ? Theme.Warn : Theme.DimText;
                tmp_Text_6.text  = SpotifyPlayer.IsPlaying ? "PAUSE" : "PLAY";
                tmp_Text_7.text  = SpotifyPlayer.ShuffleEnabled ? "ON" : "OFF";
                tmp_Text_8.text  = SpotifyPlayer.RepeatMode.ToUpper();
                tmp_Text_9.text  = SpotifyPlayer.VolumePercent + "%";
                if (renderer_0 != null && SpotifyPlayer.AlbumArt != null && renderer_0.material.mainTexture != SpotifyPlayer.AlbumArt)
                {
                    renderer_0.material.mainTexture = SpotifyPlayer.AlbumArt;
                    renderer_0.material.color       = Color.white;
                }
            }
        }
        else if (tmp_Text_3 != null && SpotifyPlayer.OAuth.ClientId.Length > 0 && SpotifyPlayer.OAuth.StatusMessage.Length > 0)
        {
            tmp_Text_3.text = SpotifyPlayer.OAuth.StatusMessage;
        }
    }

    private void BuildLiveChatPage()
    {
        AddPageTitle("LIVE CHAT", Theme.Soft);
        if (YouTubeLiveChat.IsConfigured)
        {
            tmp_Text_10                         = Theme.Text(contentRoot.transform, "state", YouTubeLiveChat.Status.Length <= 0 ? "LOOKING..." : YouTubeLiveChat.Status, 0.02f, Theme.DimText);
            tmp_Text_10.transform.localPosition = new Vector3(0f, 0.235f, 0f) + contentOffset;
            tmp_Text_11                         = new TMP_Text[8];
            for (int i = 0; i < 8; i++)
            {
                tmp_Text_11[i] = AddPositionedText("l" + i, "", 0.019f, Theme.White, -0.36f, 0.19f - i * 0.048f);
            }

            displayedChatVersion = -1;
            AddBackButton(new Vector3(-0.09f, -0.24f, 0f), delegate
                                                           {
                                                               ShowPage(Page.Main, null);
                                                           });

            AddCard("refresh", new Vector3(0.12f, -0.24f, 0f), "", "REFRESH", delegate
                                                                              {
                                                                                  YouTubeLiveChat.Reset();
                                                                                  ShowPage(Page.Chat, null);
                                                                              }, 0.16f, 0.064f).valueText.color = Theme.DimText;
        }
        else
        {
            Theme.Text(contentRoot.transform, "state", "PUT YOUR CHANNEL IN CONFIG.CFG",  0.024f, Theme.DimText).transform.localPosition = new Vector3(0f, 0.08f, 0f) + contentOffset;
            Theme.Text(contentRoot.transform, "hint",  "[youtube] channel = @yourhandle", 0.02f,  Theme.DimText).transform.localPosition = new Vector3(0f, 0.02f, 0f) + contentOffset;
            AddBackButton(new Vector3(0f, -0.14f, 0f), delegate
                                                       {
                                                           ShowPage(Page.Main, null);
                                                       });
        }
    }

    private void UpdateLiveChatPage()
    {
        if (!YouTubeLiveChat.IsConfigured)
        {
            return;
        }

        YouTubeLiveChat.Poll();
        if (tmp_Text_11 == null)
        {
            return;
        }

        tmp_Text_10.text  = YouTubeLiveChat.Status.Length > 0 ? YouTubeLiveChat.Status : "LOOKING...";
        tmp_Text_10.color = YouTubeLiveChat.Status        == "LIVE" ? Theme.Good : Theme.DimText;
        if (displayedChatVersion == YouTubeLiveChat.MessageVersion)
        {
            return;
        }

        displayedChatVersion = YouTubeLiveChat.MessageVersion;
        string[] messages = YouTubeLiveChat.Messages;
        string   text     = ColorUtility.ToHtmlStringRGB(Theme.Soft);
        for (int i = 0; i < 8; i++)
        {
            if (i >= messages.Length)
            {
                tmp_Text_11[i].text = "";

                continue;
            }

            int    num   = messages[i].IndexOf('\t');
            string text2 = TruncateText(messages[i].Substring(0, num), 14);
            tmp_Text_11[i].text = "<color=#" + text + "><noparse>" + text2 + "</noparse></color> <noparse>" + TruncateText(messages[i].Substring(num + 1), 44 - text2.Length) + "</noparse>";
        }
    }

    private TMP_Text AddPositionedText(string string_2, string string_3, float float_12, Color color_0, float float_13, float float_14)
    {
        TextMeshPro obj = Theme.Text(contentRoot.transform, string_2, string_3, float_12, color_0, true);
        ((TMP_Text)obj).transform.localPosition = new Vector3(float_13, float_14, 0f) + contentOffset;

        return obj;
    }

    private static string TruncateText(string string_2, int int_11)
    {
        if (string_2 == null)
        {
            return "";
        }

        if (string_2.Length <= int_11)
        {
            return string_2;
        }

        return string_2.Substring(0, int_11 - 2) + "..";
    }

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

    private static string FormatDuration(int int_11)
    {
        int num = int_11 / 1000;

        return num / 60 + ":" + (num % 60).ToString("00");
    }

    private static Vector3 PolarPosition(float float_12, float float_13)
    {
        float num = float_12 * (MathF.PI / 180f);

        return new Vector3(Mathf.Sin(num) * float_13, Mathf.Cos(num) * float_13, 0f);
    }

    private void AddPageTitle(string string_2, Color color_0) => Theme.Text(contentRoot.transform, "title", string_2, 0.038f, color_0).transform.localPosition = PolarPosition(0f, 0.5f) + contentOffset;

    private void AddPlayerPreview(VRRig vrrig_0, string string_2)
    {
        if (vrrig_0 != null)
        {
            Mirror.Spawn(contentRoot.transform, vrrig_0, 1f).transform.localPosition = new Vector3(0f, 0.1f, 0.14f);
        }

        Theme.Text(contentRoot.transform, "cname", string_2, 0.036f, Detect.RigColor(vrrig_0)).transform.localPosition = new Vector3(0f, -0.63f, -0.02f);
    }

    private void AddPlayerButton(VRRig vrrig_0, float float_12, float float_13, int int_11)
    {
        MenuItem menuItem = CreateMenuItem("fig_" + int_11, PolarPosition(float_12, 0.485f), int_11 * 0.03f);
        menuItem.rig           = vrrig_0;
        menuItem.scale         = float_13;
        menuItem.radialHitTest = true;
        menuItem.hitWidth      = 0.085f * float_13;
        NetPlayer   creator = vrrig_0.Creator;
        TextMeshPro val     = Theme.Text(menuItem.root.transform, "nm", creator != null ? creator.NickName : "?", 0.026f, Theme.White);
        val.transform.localPosition = new Vector3(0f, -0.115f, -0.002f);
        menuItem.valueText          = val;
        Player val2 = Detect.PlayerOf(vrrig_0);
        if (val2 != null)
        {
            Scan scan = Detect.Get(val2);
            val.color = scan.Cheats.Count > 0 ? Theme.Bad : scan.Mods.Count > 0 ? Theme.Warn : Theme.White;
        }

        if (Net.HasRig(vrrig_0))
        {
            Theme.Quad(menuItem.root.transform, "zx", 0.042f, 0.042f, Theme.Holo(Color.white, 2998, Theme.MenuIcon)).transform.localPosition = new Vector3(0.062f, 0.058f, -0.002f);
        }

        menuItem.onActivate = delegate
                              {
                                  Player val3 = Detect.PlayerOf(vrrig_0);
                                  if (val3 != null)
                                  {
                                      ShowPage(Page.Player, val3);
                                  }
                              };

        CaptureItemVisuals(menuItem);
        Mirror.Head(menuItem.root.transform, vrrig_0, 0.36f).transform.localPosition = new Vector3(0f, 0.012f, 0.01f);
    }

    private MenuItem AddCard(string string_2, Vector3 vector3_2, string string_3, string string_4, Action action_0, float float_12, float float_13 = 0.092f)
    {
        MenuItem menuItem = CreateMenuItem(string_2, vector3_2, menuItems.Count * 0.028f);
        menuItem.panel = Theme.Card(menuItem.root.transform, "panel", float_12, float_13, InactiveBorderColor);
        bool flag;
        if (flag = !string.IsNullOrEmpty(string_3))
        {
            Theme.Text(menuItem.root.transform, "label", string_3, 0.02f, Theme.DimText).transform.localPosition = new Vector3(0f, float_13 * 0.26f, -0.002f);
        }

        menuItem.valueText                         = Theme.Text(menuItem.root.transform, "value", string_4, flag ? 0.027f : 0.03f, Theme.White);
        menuItem.valueText.transform.localPosition = new Vector3(0f, flag ? (0f - float_13) * 0.17f : 0f, -0.002f);
        if (action_0 != null)
        {
            menuItem.hitWidth   = float_12;
            menuItem.hitHeight  = float_13;
            menuItem.onActivate = action_0;
        }

        CaptureItemVisuals(menuItem);

        return menuItem;
    }

    private void AddBackButton(Vector3 vector3_2, Action action_0) => AddCard("back", vector3_2, "", "BACK", action_0, 0.18f, 0.064f).valueText.color = Theme.Soft;

    private void AddNavigationButton(string string_2, float float_12, Texture texture_0, string string_3, Action action_0)
    {
        MenuItem menuItem = CreateMenuItem(string_2, PolarPosition(float_12, 0.485f), 0.22f);
        menuItem.panel = Theme.Card(menuItem.root.transform, "panel", 0.115f, 0.115f, InactiveBorderColor);
        if (texture_0 != null)
        {
            Theme.Quad(menuItem.root.transform, "icon", 0.062f, 0.062f, Theme.Holo(Theme.Soft, 2997, texture_0)).transform.localPosition = new Vector3(0f, 0.01f, -0.002f);
        }
        else
        {
            DrawMusicNoteIcon(menuItem.root.transform);
        }

        Theme.Text(menuItem.root.transform, "lb", string_3, 0.017f, Theme.DimText).transform.localPosition = new Vector3(0f, -0.07f, -0.002f);
        menuItem.radialHitTest                                                                             = true;
        menuItem.hitWidth                                                                                  = 0.062f;
        menuItem.onActivate                                                                                = action_0;
        CaptureItemVisuals(menuItem);
    }

    private static void DrawMusicNoteIcon(Transform transform_7)
    {
        Theme.Ring(transform_7, "nhead", 0f, 0.016f, Theme.Soft, 2997).transform.localPosition                 = new Vector3(-0.013f, -0.014f, -0.002f);
        Theme.Quad(transform_7, "nstem", 0.005f, 0.052f, Theme.Holo(Theme.Soft, 2997)).transform.localPosition = new Vector3(0.0015f, 0.012f,  -0.002f);
        Theme.Quad(transform_7, "nflag", 0.021f, 0.008f, Theme.Holo(Theme.Soft, 2997)).transform.localPosition = new Vector3(0.012f,  0.034f,  -0.002f);
    }

    private MenuItem CreateMenuItem(string string_2, Vector3 vector3_2, float float_12)
    {
        GameObject val = new(string_2);
        val.transform.SetParent(contentRoot.transform, false);
        val.transform.localPosition = vector3_2   * 0.25f;
        val.transform.localScale    = Vector3.one * 0.001f;
        MenuItem menuItem = new();
        menuItem.root           = val;
        menuItem.targetPosition = vector3_2;
        menuItem.createdAt      = Time.time;
        menuItem.animationDelay = float_12;
        menuItems.Add(menuItem);

        return menuItem;
    }

    private void CaptureItemVisuals(MenuItem class24_0)
    {
        MeshRenderer[] componentsInChildren = class24_0.root.GetComponentsInChildren<MeshRenderer>(true);
        foreach (MeshRenderer val in componentsInChildren)
        {
            if (!(val.GetComponent<TMP_Text>() != null))
            {
                class24_0.renderers.Add(val);
                class24_0.rendererBaseAlpha.Add(val.material.color.a);
            }
        }

        TMP_Text[] componentsInChildren2 = class24_0.root.GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text val2 in componentsInChildren2)
        {
            class24_0.texts.Add(val2);
            val2.alpha = 0f;
        }
    }

    private int HitTest(Vector2 vector2_0)
    {
        for (int i = 0; i < menuItems.Count; i++)
        {
            MenuItem menuItem = menuItems[i];
            if (menuItem.onActivate == null || menuItem.hitWidth <= 0f)
            {
                continue;
            }

            float num  = vector2_0.x - menuItem.targetPosition.x;
            float num2 = vector2_0.y - menuItem.targetPosition.y;
            bool  num3;
            if (!menuItem.radialHitTest)
            {
                if (!(Mathf.Abs(num) <= menuItem.hitWidth * 0.5f))
                {
                    continue;
                }

                num3 = Mathf.Abs(num2) <= menuItem.hitHeight * 0.5f;
            }
            else
            {
                num3 = num * num + num2 * num2 <= menuItem.hitWidth * menuItem.hitWidth;
            }

            if (num3)
            {
                return i;
            }
        }

        return -1;
    }

    private void HandleLaserInput(GorillaTagger gorillaTaggerInstance)
    {
        if (Cfg.Touch.Value && !Cfg.OneHand.Value)
        {
            HandleTouchInput(gorillaTaggerInstance);

            return;
        }

        Transform             val      = UseLeftHand ? gorillaTaggerInstance.leftHandTransform : gorillaTaggerInstance.rightHandTransform;
        ControllerInputPoller instance = ControllerInputPoller.instance;
        if (val == null || instance == null)
        {
            return;
        }

        pointerLine.enabled = true;
        Vector3 position = val.position;
        Vector3 val2     = val.rotation * Quaternion.Euler(45f, UseLeftHand ? 10f : -10f, 0f) * Vector3.forward;
        pointerLine.SetPosition(0, position);
        Vector3 val3 = menuFace.InverseTransformPoint(position);
        Vector3 val4 = menuFace.InverseTransformDirection(val2);
        Vector3 val5 = position + val2 * 3f;
        int     num  = -1;
        bool    flag = false;
        if (Mathf.Abs(val4.z) > 1E-05f)
        {
            float num2 = (0f - val3.z) / val4.z;
            if (num2 > 0f)
            {
                Vector2 val6 = new(val3.x + val4.x * num2, val3.y + val4.y * num2);
                if (val6.sqrMagnitude < 1f)
                {
                    num = HitTest(val6);
                    if (flag = num >= 0 || val6.sqrMagnitude < 0.3844f)
                    {
                        val5 = menuFace.TransformPoint(new Vector3(val6.x, val6.y, 0f));
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

        SetHoveredItem(num);
        bool flag2;
        if ((flag2 = (UseLeftHand ? instance.leftControllerIndexFloat : instance.rightControllerIndexFloat) > 0.6f) && !triggerDown && hoveredItemIndex >= 0 && menuItems[hoveredItemIndex].onActivate != null)
        {
            ActivateItem(hoveredItemIndex, UseLeftHand);
        }

        triggerDown = flag2;
    }

    private void HandleTouchInput(GorillaTagger gorillaTagger_0)
    {
        pointerLine.enabled = false;
        laserDot.gameObject.SetActive(false);
        int  num   = -1;
        bool bool_ = false;
        for (int i = 0; i < 2; i++)
        {
            Transform val = i == 0 ? gorillaTagger_0.leftHandTransform : gorillaTagger_0.rightHandTransform;
            if (val == null)
            {
                continue;
            }

            Vector3 val2 = menuFace.InverseTransformPoint(val.position);
            if (!(Mathf.Abs(val2.z) > 0.1f))
            {
                int num2 = HitTest(new Vector2(val2.x, val2.y));
                if (num2 >= 0)
                {
                    num   = num2;
                    bool_ = i == 0;

                    break;
                }
            }
        }

        SetHoveredItem(num);
        if (num >= 0 && num != lastTouchedItem && menuItems[num].onActivate != null)
        {
            ActivateItem(num, bool_);
        }

        lastTouchedItem = num;
    }

    private void SetHoveredItem(int int_11)
    {
        if (int_11 == hoveredItemIndex)
        {
            hoverExitDelay = 0f;

            return;
        }

        if (int_11 < 0 && hoveredItemIndex >= 0)
        {
            hoverExitDelay += Time.deltaTime;
            if (!(hoverExitDelay >= 0.12f))
            {
                return;
            }
        }

        hoverExitDelay = 0f;
        SetItemHighlighted(hoveredItemIndex, false);
        hoveredItemIndex = int_11;
        SetItemHighlighted(hoveredItemIndex, true);
        if (hoveredItemIndex >= 0)
        {
            Theme.HoverSound();
            Theme.Haptic(UseLeftHand, 0.12f, 0.015f);
        }
    }

    private void ActivateItem(int int_11, bool bool_11)
    {
        Theme.ClickSound();
        Theme.Haptic(bool_11, 0.5f, 0.04f);
        menuItems[int_11].onActivate();
    }

    private void SetItemHighlighted(int int_11, bool bool_11)
    {
        if (int_11 >= 0 && int_11 < menuItems.Count)
        {
            MenuItem menuItem = menuItems[int_11];
            menuItem.highlighted = bool_11;
            if (menuItem.panel != null)
            {
                menuItem.panel.FillR.material.color   = bool_11 ? new Color(Theme.Main.r, Theme.Main.g, Theme.Main.b, 0.3f) : Theme.Fill;
                menuItem.panel.BorderR.material.color = bool_11 ? Theme.Soft : InactiveBorderColor;
            }

            if (menuItem.rig != null)
            {
                Outline.Set(menuItem.rig, bool_11);
            }
        }
    }

    private void RefreshDynamicContent()
    {
        if (currentPage == Page.Music && tmp_Text_3 != null && Time.time >= nextMediaRefresh)
        {
            nextMediaRefresh = Time.time + 1f;
            tmp_Text_3.text  = DesktopMediaControls.TrackTitle;
        }
        else if (currentPage == Page.Spotify)
        {
            UpdateSpotifyPage();
        }
        else if (currentPage == Page.Chat)
        {
            UpdateLiveChatPage();
        }

        if (!(Time.time >= nextDynamicRefresh))
        {
            return;
        }

        nextDynamicRefresh = Time.time + 0.25f;
        if (currentPage == Page.Player && targetPlayer != null)
        {
            VRRig val = Detect.RigOf(targetPlayer);
            if (tmp_Text_0 != null && val != null)
            {
                tmp_Text_0.text  = val.fps.ToString();
                tmp_Text_0.color = Detect.FpsColor(val.fps);
            }

            if (tmp_Text_1 != null && tmp_Text_1.text == "...")
            {
                tmp_Text_1.text = Detect.CreatedDate(targetPlayer.UserId);
            }

            if (tmp_Text_2 != null)
            {
                bool flag = IsTargetMuted();
                tmp_Text_2.text  = flag ? "MUTED" : "OFF";
                tmp_Text_2.color = flag ? Theme.Bad : Theme.White;
            }
        }
    }

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
                if (!(allScoreboardLine == null) && allScoreboardLine.linePlayer != null && allScoreboardLine.linePlayer.UserId == targetPlayer.UserId)
                {
                    allScoreboardLine.reportedCheating   |= selectedReportReason == 1;
                    allScoreboardLine.reportedToxicity   |= selectedReportReason == 2;
                    allScoreboardLine.reportedHateSpeech |= selectedReportReason == 0;
                    if (!(allScoreboardLine.reportButton == null))
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
        if (val != null)
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
            if (allScoreboardLine != null && allScoreboardLine.linePlayer != null && allScoreboardLine.linePlayer.UserId == targetPlayer.UserId)
            {
                return allScoreboardLine;
            }
        }

        return null;
    }

    private void ToggleTargetMute()
    {
        GorillaPlayerScoreboardLine val = FindTargetScoreboardLine();
        if (!(val == null))
        {
            bool isMuted = !IsTargetMuted();
            val.PressButton(isMuted, GorillaPlayerLineButton.ButtonType.Mute);
            if (val.muteButton != null)
            {
                val.muteButton.isOn = isMuted;
                val.muteButton.UpdateColor();
            }

            if (!(tmp_Text_2 == null))
            {
                tmp_Text_2.text  = isMuted ? "MUTED" : "OFF";
                tmp_Text_2.color = isMuted ? Theme.Bad : Theme.White;
            }
        }
    }

    private static GorillaNetworkJoinTrigger FindPublicRoomTrigger()
    {
        GameObject val = GameObject.Find("Environment Objects/TriggerZones_Prefab/JoinRoomTriggers_Prefab/JoinPublicRoom - Forest, Tree Exit");
        if (!(val != null))
        {
            return FindFirstObjectByType<GorillaNetworkJoinTrigger>();
        }

        return val.GetComponent<GorillaNetworkJoinTrigger>();
    }

    private IEnumerator JoinRoom(string string_2, GorillaNetworkJoinTrigger gorillaNetworkJoinTrigger_0)
    {
        if (isJoiningRoom || gorillaNetworkJoinTrigger_0 == null && string.IsNullOrEmpty(string_2))
        {
            yield break;
        }

        isJoiningRoom = true;
        if (PhotonNetwork.InRoom)
        {
            if (NetworkSystem.Instance != null)
            {
                NetworkSystem.Instance.ReturnToSinglePlayer();
            }

            float time = Time.time;
            while (!(Time.time - time >= 10f) && (!(NetworkSystem.Instance != null) || (int)NetworkSystem.Instance.netState != 2 || PhotonNetwork.InRoom))
            {
                yield return null;
            }
        }

        if (gorillaNetworkJoinTrigger_0 != null)
        {
            PhotonNetworkController.Instance.AttemptToJoinPublicRoom(gorillaNetworkJoinTrigger_0);
        }
        else
        {
            PhotonNetworkController.Instance.AttemptToJoinSpecificRoom(string_2, 0);
        }

        isJoiningRoom = false;
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
            if (!(activeRigContainer.Rig == null) && !activeRigContainer.Rig.isLocal)
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

    private string BuildRigSnapshot()
    {
        string text = "";
        foreach (VRRig remoteRig in GetRemoteRigs())
        {
            text = text + remoteRig.GetInstanceID() + ",";
        }

        return text;
    }

    private void BuildFriendsPage()
    {
        AddPageTitle("FRIENDS", Theme.Soft);
        if (Friends.All.Count == 0)
        {
            Theme.Text(contentRoot.transform, "empty", "NO FRIENDS YET", 0.032f, Theme.DimText).transform.localPosition = contentOffset;
            AddBackButton(new Vector3(0f, -0.27f, 0f), delegate
                                                       {
                                                           ShowPage(Page.Main, null);
                                                       });

            return;
        }

        Dictionary<string, string> dictionary = new();
        if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null)
        {
            foreach (KeyValuePair<int, Player> player in PhotonNetwork.CurrentRoom.Players)
            {
                if (player.Value.UserId != null)
                {
                    dictionary[player.Value.UserId] = player.Value.NickName;
                }
            }
        }

        int num = 0;
        for (int num2 = 0; num2 < 2; num2++)
        {
            foreach (string item in Friends.All)
            {
                bool flag;
                if ((flag = dictionary.ContainsKey(item)) == (num2 == 0) && num < 7)
                {
                    AddCard("f" + num, new Vector3(0f, 0.215f - num * 0.075f, 0f), flag ? "IN ROOM" : "", flag ? dictionary[item] : Friends.NameOf(item), null, 0.32f, 0.065f).valueText.color = flag ? Theme.Good : Theme.DimText;
                    num++;
                }
            }
        }

        AddBackButton(new Vector3(0f, -0.27f, 0f), delegate
                                                   {
                                                       ShowPage(Page.Main, null);
                                                   });
    }

    private void BuildCustomThemePage()
    {
        AddPageTitle("CUSTOM COLOR", Theme.Soft);
        string[] array  = new string[3] { "HUE", "SAT", "BRIGHT", };
        string[] array2 = new string[3] { "h", "s", "b", };
        float[]  array3 = new float[3] { 0.6f, 0.8f, 0.9f, };
        for (int i = 0; i < 3; i++)
        {
            float num   = PlayerPrefs.GetFloat("zx_theme_" + array2[i], array3[i]);
            float num2  = 0.195f - i * 0.085f;
            int   int_0 = i;
            AddCard(array2[i] + "lbl", new Vector3(0f, num2, 0f), array[i], i == 0 ? Mathf.RoundToInt(num * 360f) + "D" : Mathf.RoundToInt(num * 100f) + "%", null, 0.21f, 0.065f);
            AddCard(array2[i] + "dec", new Vector3(-0.175f, num2, 0f), "", array2[i].ToUpper() + "-", delegate
                                                                                                      {
                                                                                                          AdjustThemeComponent(int_0, -1);
                                                                                                      }, 0.068f, 0.065f);

            AddCard(array2[i] + "inc", new Vector3(0.175f, num2, 0f), "", array2[i].ToUpper() + "+", delegate
                                                                                                     {
                                                                                                         AdjustThemeComponent(int_0, 1);
                                                                                                     }, 0.068f, 0.065f);
        }

        AddBackButton(new Vector3(0f, -0.22f, 0f), delegate
                                                   {
                                                       ShowPage(Page.Settings, null);
                                                   });
    }

    private void AdjustThemeComponent(int int_11, int int_12)
    {
        float num  = PlayerPrefs.GetFloat("zx_theme_h", 0.6f);
        float num2 = PlayerPrefs.GetFloat("zx_theme_s", 0.8f);
        float num3 = PlayerPrefs.GetFloat("zx_theme_b", 0.9f);
        switch (int_11)
        {
            case 0:
                num = Mathf.Repeat(num + int_12 * 0.028f, 1f);

                break;

            default:
                num3 = Mathf.Clamp01(num3 + int_12 * 0.05f);

                break;

            case 1:
                num2 = Mathf.Clamp01(num2 + int_12 * 0.05f);

                break;
        }

        PlayerPrefs.SetFloat("zx_theme_h", num);
        PlayerPrefs.SetFloat("zx_theme_s", num2);
        PlayerPrefs.SetFloat("zx_theme_b", num3);
        PlayerPrefs.Save();
        Theme.ApplyCustom(Color.HSVToRGB(num, num2, num3));
        if (Plugin.Ins?.Disc != null)
        {
            Plugin.Ins.Disc.Retheme();
        }

        ShowPage(Page.CustomTheme, null);
    }

    private void CyclePushToTalkFromMain()
    {
        GameSettings.CyclePushToTalkMode();
        ShowPage(Page.Main, null);
    }

    private void CycleOutfitFromMain()
    {
        GameSettings.CycleOutfit(true);
        ShowPage(Page.Main, null);
    }

    private void OpenDetectedMods()
    {
        detectionKind = Kind.Mod;
        ShowPage(Page.ModList, targetPlayer);
    }

    private void OpenDetectedCheats()
    {
        detectionKind = Kind.Cheat;
        ShowPage(Page.ModList, targetPlayer);
    }

    private void OpenReportPage() => ShowPage(Page.Report, targetPlayer);

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

    private void ReturnToMain() => ShowPage(Page.Main, null);

    private void ReturnToPlayer() => ShowPage(Page.Player, targetPlayer);

    private void SelectCheatingReport()
    {
        selectedReportReason = 1;
        ShowPage(Page.Report, targetPlayer);
    }

    private void SelectToxicityReport()
    {
        selectedReportReason = 2;
        ShowPage(Page.Report, targetPlayer);
    }

    private void SelectHateSpeechReport()
    {
        selectedReportReason = 0;
        ShowPage(Page.Report, targetPlayer);
    }

    private void CancelReport()
    {
        selectedReportReason = -1;
        ShowPage(Page.Player, targetPlayer);
    }

    private void CancelReportConfirmation()
    {
        selectedReportReason = -1;
        ShowPage(Page.Player, targetPlayer);
    }

    private void CycleTheme()
    {
        Cfg.Theme.Value = (Cfg.Theme.Value + 1) % Theme.Count;
        Theme.Apply(Cfg.Theme.Value);
        Plugin.Ins.Disc.Retheme();
        ReopenSettingsAfterThemeChange();
    }

    private void CycleNotificationMode()
    {
        Cfg.NotifMode.Value = Cfg.NotifMode.Value == "all" ? "cheats" : Cfg.NotifMode.Value == "cheats" ? "off" : "all";
        ShowPage(Page.Settings, null);
    }

    private void OpenCustomTheme() => ShowPage(Page.CustomTheme, null);

    private void BindOpenButton()
    {
        buttonBindingMode = 2;
        ShowPage(Page.Settings, null);
    }

    private void BindCloseButton()
    {
        buttonBindingMode = 1;
        ShowPage(Page.Settings, null);
    }

    private void NextSettingsPage()
    {
        settingsPage = (settingsPage + 1) % PageCount;
        ShowPage(Page.Settings, null);
    }

    private void CloseSettings() => ShowPage(Page.Main, null);

    private void DisconnectRoom()
    {
        if (PhotonNetwork.InRoom && NetworkSystem.Instance != null)
        {
            NetworkSystem.Instance.ReturnToSinglePlayer();
        }

        ShowPage(Page.Room, null);
    }

    private void ReconnectLastRoom() => StartCoroutine(JoinRoom(lastRoomCode, null));

    private void JoinPublicRoom() => StartCoroutine(JoinRoom(null, FindPublicRoomTrigger()));

    private void CycleRoomQueue()
    {
        GameSettings.CycleQueue();
        ShowPage(Page.Room, null);
    }

    private void CycleRoomGameMode()
    {
        GameSettings.CycleGameMode();
        ShowPage(Page.Room, null);
    }

    private void AdvanceTimeOfDay()
    {
        GameSettings.CycleTimeOfDay(1);
        ShowPage(Page.Room, null);
    }

    private void RewindTimeOfDay()
    {
        GameSettings.CycleTimeOfDay(-1);
        ShowPage(Page.Room, null);
    }

    private void ToggleRoomVoiceChat()
    {
        GameSettings.ToggleVoiceChat();
        ShowPage(Page.Room, null);
    }

    private void CycleRoomPushToTalk()
    {
        GameSettings.CyclePushToTalkMode();
        ShowPage(Page.Room, null);
    }

    private void NextRoomPage()
    {
        roomPage = (roomPage + 1) % PageCount;
        ShowPage(Page.Room, null);
    }

    private void PreviousDesktopTrack()
    {
        DesktopMediaControls.PreviousTrack();
        nextMediaRefresh = Time.time + 0.9f;
    }

    private void ToggleDesktopPlayback()
    {
        DesktopMediaControls.TogglePlayPause();
        nextMediaRefresh = Time.time + 0.9f;
    }

    private void NextDesktopTrack()
    {
        DesktopMediaControls.NextTrack();
        nextMediaRefresh = Time.time + 0.9f;
    }

    private void CloseDesktopMusic() => ShowPage(Page.Main, null);

    private void BeginSpotifyLogin()
    {
        SpotifyPlayer.OAuth.BeginLogin();
        ShowPage(Page.Spotify, null);
    }

    private void CloseSpotifyLogin() => ShowPage(Page.Main, null);

    private void ToggleSpotifyPlayback()
    {
        SpotifyPlayer.TogglePlayback();
        tmp_Text_6.text = SpotifyPlayer.IsPlaying ? "PAUSE" : "PLAY";
    }

    private void DecreaseSpotifyVolume()
    {
        SpotifyPlayer.SetVolume(SpotifyPlayer.VolumePercent - 10);
        tmp_Text_9.text = SpotifyPlayer.VolumePercent + "%";
    }

    private void IncreaseSpotifyVolume()
    {
        SpotifyPlayer.SetVolume(SpotifyPlayer.VolumePercent + 10);
        tmp_Text_9.text = SpotifyPlayer.VolumePercent + "%";
    }

    private void ToggleSpotifyShuffle()
    {
        SpotifyPlayer.ToggleShuffle();
        tmp_Text_7.text = SpotifyPlayer.ShuffleEnabled ? "ON" : "OFF";
    }

    private void CycleSpotifyRepeat()
    {
        SpotifyPlayer.CycleRepeatMode();
        tmp_Text_8.text = SpotifyPlayer.RepeatMode.ToUpper();
    }

    private void CloseSpotifyPlayer() => ShowPage(Page.Main, null);

    private void DisconnectSpotify()
    {
        SpotifyPlayer.Disconnect();
        ShowPage(Page.Spotify, null);
    }

    private void CloseSpotifyDisconnected() => ShowPage(Page.Main, null);

    private void CloseLiveChatSetup() => ShowPage(Page.Main, null);

    private void ResetLiveChat()
    {
        YouTubeLiveChat.Reset();
        ShowPage(Page.Chat, null);
    }

    private void CloseLiveChat() => ShowPage(Page.Main, null);

    private void CloseEmptyLiveChat() => ShowPage(Page.Main, null);

    private void ReturnToSettings() => ShowPage(Page.Settings, null);

    private enum Page
    {
        Main,
        Player,
        ModList,
        Report,
        Settings,
        Room,
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

        public Theme.Panel panel;

        public bool radialHitTest;

        public VRRig      rig;
        public GameObject root;

        public float scale = 1f;

        public Vector3 targetPosition;

        public TMP_Text valueText;

        public bool visible;
    }

    private static class CachedCallbacks
    {
        public static Action action_0;

        public static Action action_1;

        public static Action action_2;

        public static Action action_3;
    }

    private sealed class PlayerSelectionCallback
    {

        public RingMenu ringMenu_0;
        public VRRig    vrrig_0;

        internal void OpenSelectedPlayer()
        {
            Player val = Detect.PlayerOf(vrrig_0);
            if (val != null)
            {
                ringMenu_0.ShowPage(Page.Player, val);
            }
        }
    }

    private sealed class ThemeAdjustmentCallback
    {
        public int int_0;

        public RingMenu ringMenu_0;

        internal void Decrease() => ringMenu_0.AdjustThemeComponent(int_0, -1);

        internal void Increase() => ringMenu_0.AdjustThemeComponent(int_0, 1);
    }

    private sealed class PageNavigationCallback
    {

        public Page     enum1_0;
        public RingMenu ringMenu_0;

        internal void Navigate() => ringMenu_0.ShowPage(enum1_0, null);
    }

    private sealed class ConfigToggleCallback
    {
        public ConfigEntry<bool> configEntry_0;

        public RingMenu ringMenu_0;

        internal void Toggle()
        {
            configEntry_0.Value = !configEntry_0.Value;
            ringMenu_0.ShowPage(ringMenu_0.currentPage, ringMenu_0.targetPlayer);
        }
    }
}