using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Sentinel;

public sealed class PcMenu : MonoBehaviour
{

    private static readonly string[] TabNames = new string[8] { "Lobby", "Room", "Nametags", "Menu", "Music", "Stream chat", "Friends", "Controls", };

    private static readonly Color PanelColor = new(0.035f, 0.047f, 0.064f, 0.99f);

    private static readonly Color ControlColor = new(0.07f, 0.092f, 0.12f, 1f);

    private static readonly Color DividerColor = new(0.13f, 0.18f, 0.23f, 1f);

    private static readonly Color MutedTextColor = new(0.62f, 0.7f, 0.76f, 1f);

    private readonly List<(TMP_Text Text, Func<string> Value)> dynamicTexts = new();

    private readonly List<MenuItem> menuItems = new();

    private string actionStatus = "";

    private Keyboard attachedKeyboard;

    private RectTransform brightnessMarker;

    private RectTransform brightnessRect;

    private GameObject canvasRoot;

    private string capabilityFingerprint = "";

    private TMP_Text colorHexLabel;

    private ThemeColorPicker colorPicker;

    private Image colorPreview;

    private RectTransform colorWheelMarker;

    private RectTransform colorWheelRect;

    private GameObject contentRoot;

    private int currentTab;

    private bool cursorWasVisible;

    private bool customThemeOpen;

    private Font fallbackFont;

    private TMP_FontAsset fontAsset;

    private int friendsTab;

    private int hoveredItemIndex = -1;

    private bool isOpen;

    private string joinCode = "";

    private bool joinCodeFocused;

    private int keyboardSelection = -1;

    private int listPage;

    private string lobbyFingerprint = "";

    private float nextRefreshAt;

    private int openFrame = -1;

    private bool ownsFontAsset;

    private RectTransform panelRoot;

    private int persistentItemCount;

    private CursorLockMode previousCursorLock;

    private Sprite roundedSprite;

    private Texture2D roundedTexture;

    private string selectedPlayerId = "";

    private TMP_Text statusLabel;

    private bool suppressCloseSound;

    private int themeDragMode;

    public static bool IsOpen { get; private set; }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Update()
    {
        Keyboard current = Keyboard.current;
        if (!Manifest.Has("pc_ui"))
        {
            if (isOpen)
            {
                SetOpen(false);
            }
        }
        else if (!Application.isFocused || current == null || !current.tabKey.wasPressedThisFrame)
        {
            if (!isOpen)
            {
                return;
            }

            if (Application.isFocused && (current == null || !current.escapeKey.wasPressedThisFrame))
            {
                if (Time.frameCount == openFrame)
                {
                    return;
                }

                if (current != attachedKeyboard)
                {
                    AttachKeyboard(current);
                }

                Cursor.lockState = 0;
                Cursor.visible   = true;
                if (current != null && current.upArrowKey.wasPressedThisFrame)
                {
                    MoveKeyboardSelection(-1);
                }

                if (current != null && current.downArrowKey.wasPressedThisFrame)
                {
                    MoveKeyboardSelection(1);
                }

                if (current != null && joinCodeFocused && current.backspaceKey.wasPressedThisFrame)
                {
                    joinCode = RoomCode.Backspace(joinCode);
                }

                if (current != null && current.enterKey.wasPressedThisFrame && keyboardSelection >= 0)
                {
                    ActivateItem(keyboardSelection);
                }

                HandlePointerInput();
                if (Time.unscaledTime < nextRefreshAt)
                {
                    return;
                }

                nextRefreshAt = Time.unscaledTime + 0.5f;
                string text = Manifest.TierLabel + ":" + Manifest.Has("spotify") + Manifest.Has("youtube_chat") + Manifest.Has("custom_tags") + Manifest.Has("menu_rectangle");
                if (capabilityFingerprint != text)
                {
                    capabilityFingerprint = text;
                    RebuildTab();
                }

                if (currentTab == 0 || currentTab == 6)
                {
                    string text2 = BuildLobbyFingerprint();
                    if (text2 != lobbyFingerprint)
                    {
                        lobbyFingerprint = text2;
                        RebuildTab();
                    }
                }

                if (currentTab == 4 && Manifest.Has("spotify"))
                {
                    SpotifyPlayer.PollPlayback();
                }

                if (currentTab == 5 && Manifest.Has("youtube_chat"))
                {
                    YouTubeLiveChat.Poll();
                }

                RefreshValues();
            }
            else
            {
                SetOpen(false);
            }
        }
        else
        {
            SetOpen(!isOpen);
        }
    }

    private void OnDisable()
    {
        suppressCloseSound = true;
        if (isOpen)
        {
            SetOpen(false);
        }

        suppressCloseSound = false;
    }

    private void OnDestroy()
    {
        colorPicker?.Dispose();
        colorPicker        = null;
        suppressCloseSound = true;
        if (isOpen)
        {
            SetOpen(false);
        }

        AttachKeyboard(null);
        if (canvasRoot != null)
        {
            Destroy(canvasRoot);
        }

        if (roundedSprite != null)
        {
            Destroy(roundedSprite);
        }

        if (roundedTexture != null)
        {
            Destroy(roundedTexture);
        }

        if (ownsFontAsset && fontAsset != null)
        {
            Texture2D[] atlasTextures = fontAsset.atlasTextures;
            foreach (Texture2D val in atlasTextures)
            {
                if (val != null)
                {
                    Destroy(val);
                }
            }

            if (fontAsset.material != null)
            {
                Destroy(fontAsset.material);
            }

            Destroy(fontAsset);
        }

        if (fallbackFont != null)
        {
            Destroy(fallbackFont);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void OpenPlayer(Player player)
    {
        if (!Manifest.Has("pc_ui") || !PhotonNetwork.InRoom || player == null || player.IsLocal || Plugin.Ins == null)
        {
            return;
        }

        Player val = Detect.Find(player.UserId);
        if (val == null || val.IsLocal)
        {
            return;
        }

        PcMenu component = Plugin.Ins.GetComponent<PcMenu>();
        if (!(component == null))
        {
            component.currentTab       = 0;
            component.listPage         = 0;
            component.selectedPlayerId = val.UserId;
            component.actionStatus     = "";
            component.openFrame        = Time.frameCount;
            if (component.isOpen)
            {
                component.RebuildTab();
            }
            else
            {
                component.SetOpen(true);
            }
        }
    }

    private void SetOpen(bool arg1)
    {
        if (isOpen == arg1)
        {
            return;
        }

        if (arg1)
        {
            PlayerRay.CancelActive();
            if (canvasRoot == null)
            {
                BuildUi();
            }

            previousCursorLock = Cursor.lockState;
            cursorWasVisible   = Cursor.visible;
            IsOpen             = true;
            isOpen             = true;
            openFrame          = Time.frameCount;
            canvasRoot.SetActive(true);
            AttachKeyboard(Keyboard.current);
            Cursor.lockState = 0;
            Cursor.visible   = true;
            RebuildTab();
            Theme.OpenSound();
        }
        else
        {
            colorPicker?.SaveChanges();
            themeDragMode = 0;
            IsOpen        = false;
            isOpen        = false;
            if (canvasRoot != null)
            {
                canvasRoot.SetActive(false);
            }

            AttachKeyboard(null);
            Cursor.lockState = previousCursorLock;
            Cursor.visible   = cursorWasVisible;
            if (!suppressCloseSound)
            {
                Theme.CloseSound();
            }
        }
    }

    private void AttachKeyboard(Keyboard arg1)
    {
        if (attachedKeyboard != null)
        {
            attachedKeyboard.onTextInput -= HandleTextInput;
        }

        attachedKeyboard = arg1;
        if (attachedKeyboard != null)
        {
            attachedKeyboard.onTextInput += HandleTextInput;
        }
    }

    private void HandleTextInput(char arg1)
    {
        if (isOpen && joinCodeFocused && (arg1 >= 'a' && arg1 <= 'z' || arg1 >= 'A' && arg1 <= 'Z' || arg1 >= '0' && arg1 <= '9'))
        {
            joinCode = RoomCode.Add(joinCode, arg1);
            RefreshValues();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void BuildUi()
    {
        Theme.Load();
        try
        {
            fallbackFont = Font.CreateDynamicFontFromOSFont(new string[2] { "Segoe UI", "Arial", }, 32);
            if (fallbackFont != null)
            {
                fontAsset     = TMP_FontAsset.CreateFontAsset(fallbackFont);
                ownsFontAsset = fontAsset != null;
            }
        }
        catch { }

        if (fontAsset == null)
        {
            fontAsset = TMP_Settings.defaultFontAsset;
        }

        if (fontAsset == null)
        {
            TMP_FontAsset[] array = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            if (array.Length != 0)
            {
                fontAsset = array[0];
            }
        }

        roundedTexture = new Texture2D(32, 32, (TextureFormat)4, false)
        {
                name = "Sentinel desktop corners",
        };

        Color[] array2 = new Color[1024];
        for (int i = 0; i < 32; i++)
        {
            for (int j = 0; j < 32; j++)
            {
                float num  = Mathf.Max(7.5f - j, j - 23.5f, 0f);
                float num2 = Mathf.Max(7.5f - i, i - 23.5f, 0f);
                array2[i * 32 + j] = new Color(1f, 1f, 1f, Mathf.Clamp01(8f - Mathf.Sqrt(num * num + num2 * num2)));
            }
        }

        roundedTexture.SetPixels(array2);
        roundedTexture.Apply();
        roundedSprite = Sprite.Create(roundedTexture, new Rect(0f, 0f, 32f, 32f), new Vector2(0.5f, 0.5f), 100f, 0u, 0, new Vector4(9f, 9f, 9f, 9f));
        canvasRoot    = new GameObject("Sentinel desktop", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasRoot.transform.SetParent(transform, false);
        Canvas component = canvasRoot.GetComponent<Canvas>();
        component.renderMode   = 0;
        component.sortingOrder = 32000;
        CanvasScaler component2 = canvasRoot.GetComponent<CanvasScaler>();
        component2.uiScaleMode         = (CanvasScaler.ScaleMode)1;
        component2.referenceResolution = new Vector2(1120f, 720f);
        component2.screenMatchMode     = (CanvasScaler.ScreenMatchMode)1;
        RectTransform rectTransform = AddPanelImage(canvasRoot.transform, "Shade", 0f, 0f, 1120f, 720f, new Color(0f, 0f, 0f, 0.4f)).rectTransform;
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        Vector2 offsetMin = rectTransform.offsetMax = Vector2.zero;
        rectTransform.offsetMin = offsetMin;
        panelRoot               = AddPanelImage(canvasRoot.transform, "Panel", 0f, 0f, 1064f, 644f, PanelColor).rectTransform;
        RectTransform obj2 = panelRoot;
        RectTransform obj3 = panelRoot;
        RectTransform obj4 = panelRoot;
        Vector2       val  = default;
        val                        = new Vector2(0.5f, 0.5f);
        obj4.pivot                 = val;
        offsetMin                  = obj3.anchorMax = val;
        obj2.anchorMin             = offsetMin;
        panelRoot.anchoredPosition = Vector2.zero;
        GameObject val3 = new("Logo", typeof(RectTransform), typeof(RawImage));
        val3.transform.SetParent(panelRoot, false);
        SetRect((RectTransform)val3.transform, 24f, 24f, 36f, 36f);
        val3.GetComponent<RawImage>().texture       = Theme.MenuIcon;
        val3.GetComponent<RawImage>().raycastTarget = false;
        AddText(panelRoot, "Sentinel",                          72f,  21f, 165f, 29f, 25f, Color.white);
        AddText(panelRoot, "DESKTOP",                           73f,  52f, 145f, 17f, 10f, Theme.Main);
        AddText(panelRoot, "Tab to toggle  /  Escape to close", 620f, 31f, 315f, 25f, 12f, MutedTextColor);
        AddButton(panelRoot, "Close", 951f, 24f, 89f, delegate
                                                      {
                                                          SetOpen(false);
                                                      });

        AddPanelImage(panelRoot, "Divider", 24f, 85f, 1016f, 1f, DividerColor);
        for (int num3 = 0; num3 < TabNames.Length; num3++)
        {
            int index0 = num3;
            AddButton(panelRoot, TabNames[num3], 24f, 111 + num3 * 55, 171f, delegate
                                                                             {
                                                                                 currentTab       = index0;
                                                                                 listPage         = 0;
                                                                                 selectedPlayerId = "";
                                                                                 actionStatus     = "";
                                                                                 RebuildTab();
                                                                             });
        }

        AddText(panelRoot, "YOUR PLAN", 30f, 550f, 165f, 20f, 10f, MutedTextColor);
        TMP_Text item = AddText(panelRoot, "", 30f, 573f, 165f, 26f, 19f, Theme.Main);
        dynamicTexts.Add((item, [MethodImpl(MethodImplOptions.NoInlining)]() => Manifest.TierLabel));
        persistentItemCount = menuItems.Count;
        contentRoot         = new GameObject("Content", typeof(RectTransform));
        contentRoot.transform.SetParent(panelRoot, false);
        SetRect((RectTransform)contentRoot.transform, 223f, 111f, 817f, 506f);
        canvasRoot.SetActive(false);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void RebuildTab()
    {
        if (contentRoot == null)
        {
            return;
        }

        colorPicker?.Dispose();
        colorPicker   = null;
        themeDragMode = 0;
        for (int num = contentRoot.transform.childCount - 1; num >= 0; num--)
        {
            GameObject gameObject = contentRoot.transform.GetChild(num).gameObject;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        if (menuItems.Count > persistentItemCount)
        {
            menuItems.RemoveRange(persistentItemCount, menuItems.Count - persistentItemCount);
        }

        dynamicTexts.RemoveAll(tuple => tuple.Text == null || tuple.Text.transform.IsChildOf(contentRoot.transform));
        int num2 = -1;
        hoveredItemIndex  = -1;
        keyboardSelection = -1;
        joinCodeFocused   = false;
        string arg = currentTab != 0 || selectedPlayerId.Length <= 0 ? TabNames[currentTab] : "Player details";
        AddText(contentRoot.transform, arg, 0f, 0f, 790f, 40f, 30f, Color.white);
        statusLabel = AddText(contentRoot.transform, "", 0f, 46f, 790f, 30f, 13f, MutedTextColor);
        switch (currentTab)
        {
            case 0:
                BuildLobbyTab();

                break;

            case 1:
                BuildRoomTab();

                break;

            case 2:
                BuildNametagsTab();

                break;

            case 3:
                if (customThemeOpen && Manifest.Has("custom_theme"))
                {
                    BuildCustomThemeTab();
                }
                else
                {
                    BuildAppearanceTab();
                }

                break;

            case 4:
                BuildMusicTab();

                break;

            case 5:
                BuildStreamChatTab();

                break;

            case 6:
                BuildFriendsTab();

                break;

            case 7:
                BuildControlsTab();

                break;
        }

        RefreshValues();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void BuildLobbyTab()
    {
        if (selectedPlayerId.Length <= 0)
        {
            statusLabel.text = "Select a player to view detections and available actions.";
            Player[] playerListOthers = PhotonNetwork.PlayerListOthers;
            int      num              = listPage * 7;
            if (num >= playerListOthers.Length)
            {
                listPage = 0;
                num      = 0;
            }

            for (int i = num; i < playerListOthers.Length && i < num + 7; i++)
            {
                string text0 = playerListOthers[i].UserId;
                AddButton(contentRoot.transform, SanitizeText(playerListOthers[i].NickName), 0f, 93 + (i - num) * 47, 792f, delegate
                                                                                                                            {
                                                                                                                                selectedPlayerId = text0 ?? "";
                                                                                                                                RebuildTab();
                                                                                                                            });
            }

            if (playerListOthers.Length == 0)
            {
                AddText(contentRoot.transform, "Join a lobby to see players here.", 0f, 137f, 780f, 55f, 18f, MutedTextColor);
            }

            if (playerListOthers.Length > 7)
            {
                AddButton(contentRoot.transform, "Next page", 624f, 452f, 168f, delegate
                                                                                {
                                                                                    listPage = (listPage + 1) % ((playerListOthers.Length + 6) / 7);
                                                                                    RebuildTab();
                                                                                });
            }
        }
        else
        {
            BuildPlayerDetails();
        }
    }

    private Player FindSelectedPlayer()
    {
        if (PhotonNetwork.InRoom && selectedPlayerId.Length != 0)
        {
            Player[] playerListOthers = PhotonNetwork.PlayerListOthers;
            int      num              = 0;
            Player   val;
            while (true)
            {
                if (num < playerListOthers.Length)
                {
                    val = playerListOthers[num];
                    if (val.UserId == selectedPlayerId)
                    {
                        break;
                    }

                    num++;

                    continue;
                }

                return null;
            }

            return val;
        }

        return null;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void BuildPlayerDetails()
    {
        Player val = FindSelectedPlayer();
        if (val == null)
        {
            selectedPlayerId = "";
            BuildLobbyTab();

            return;
        }

        statusLabel.text = SanitizeText(val.NickName);
        AddDynamicText([MethodImpl(MethodImplOptions.NoInlining)]() => FindSelectedPlayer() != null ? "Platform: " + Detect.PlatformOf(FindRigByUserId(selectedPlayerId)) : "Player left the lobby", 0f,   93f);
        AddDynamicText([MethodImpl(MethodImplOptions.NoInlining)]() => Net.Has(selectedPlayerId) ? "Sentinel: Detected" : "Sentinel: Not detected",                                                  400f, 93f);
        AddDynamicText(delegate
                       {
                           Player val2 = FindSelectedPlayer();

                           return val2 == null ? "" : FormatScan(Detect.Get(val2));
                       }, 0f, 150f, 790f, 173f);

        AddButton(contentRoot.transform, "Request friendship", 0f, 348f, 246f, delegate
                                                                               {
                                                                                   Player val2 = FindSelectedPlayer();
                                                                                   if (val2 != null)
                                                                                   {
                                                                                       if (!Friends.IsFriend(val2.UserId))
                                                                                       {
                                                                                           Friends.Add(val2.UserId, val2.NickName);
                                                                                       }
                                                                                       else
                                                                                       {
                                                                                           Friends.Remove(val2.UserId);
                                                                                       }
                                                                                   }
                                                                               }, [MethodImpl(MethodImplOptions.NoInlining)]() => Manifest.Has("friends") && FindSelectedPlayer() != null && !Friends.Busy).valueProvider = [MethodImpl(MethodImplOptions.NoInlining)]() => !Friends.Busy ? Friends.IsFriend(selectedPlayerId) ? "Remove friend" : "Request friendship" : "Saving...";

        AddButton(contentRoot.transform, "Mute / unmute", 265f, 348f, 246f, ToggleSelectedPlayerMute, () => FindSelectedPlayer() != null);
        AddButton(contentRoot.transform, "Report",        530f, 348f, 262f, BuildReportControls,      [MethodImpl(MethodImplOptions.NoInlining)]() => Manifest.Has("quick_report") && FindSelectedPlayer() != null);
        AddButton(contentRoot.transform, "Back to lobby", 0f, 418f, 246f, delegate
                                                                          {
                                                                              selectedPlayerId = "";
                                                                              RebuildTab();
                                                                          });

        AddDynamicText(() => Friends.Status, 0f, 468f, 790f, 29f);
        AddText(contentRoot.transform, "Sentinel presence is detected locally. Game identities are player reported.", 0f, 395f, 790f, 19f, 11f, MutedTextColor);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string FormatScan(Scan arg1)
    {
        if (!arg1.Ready)
        {
            return "Checking detected client properties...";
        }

        List<string> list = new()
                { "Mods: " + arg1.Mods.Count + "    Cheats: " + arg1.Cheats.Count, };

        foreach (Entry cheat in arg1.Cheats)
        {
            if (list.Count < 7)
            {
                list.Add("Cheat: " + SanitizeText(cheat.Name));
            }
        }

        foreach (Entry mod in arg1.Mods)
        {
            if (list.Count < 7)
            {
                list.Add("Mod: " + SanitizeText(mod.Name));
            }
        }

        if (list.Count == 1)
        {
            list.Add("No known signatures detected. This does not prove a player is clean.");
        }

        return string.Join("\n", list);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ToggleSelectedPlayerMute()
    {
        if (FindSelectedPlayer() == null)
        {
            return;
        }

        foreach (GorillaPlayerScoreboardLine allScoreboardLine in GorillaScoreboardTotalUpdater.allScoreboardLines)
        {
            if (!(allScoreboardLine == null) && allScoreboardLine.linePlayer != null && !(allScoreboardLine.linePlayer.UserId != selectedPlayerId))
            {
                bool flag = allScoreboardLine.mute == 0;
                allScoreboardLine.PressButton(flag, (GorillaPlayerLineButton.ButtonType)3);
                if (allScoreboardLine.muteButton != null)
                {
                    allScoreboardLine.muteButton.isOn = flag;
                    allScoreboardLine.muteButton.UpdateColor();
                }

                actionStatus = flag ? "Player muted" : "Player unmuted";

                return;
            }
        }

        actionStatus = "Player voice controls are not ready";
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void BuildReportControls()
    {
        RebuildTab();
        statusLabel.text = "Choose a reason. Selecting it submits your report.";
        AddButton(contentRoot.transform, "Cheating", 0f, 468f, 246f, delegate
                                                                     {
                                                                         SubmitReport(1);
                                                                     });

        AddButton(contentRoot.transform, "Toxicity", 265f, 468f, 246f, delegate
                                                                       {
                                                                           SubmitReport(2);
                                                                       });

        AddButton(contentRoot.transform, "Hate speech", 530f, 468f, 262f, delegate
                                                                          {
                                                                              SubmitReport(0);
                                                                          });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void SubmitReport(int arg1)
    {
        Player val = FindSelectedPlayer();
        if (val != null && Manifest.Has("quick_report"))
        {
            GorillaPlayerScoreboardLine.ReportPlayer(val.UserId, (GorillaPlayerLineButton.ButtonType)arg1, val.NickName);
            RebuildTab();
            actionStatus = "Report submitted";
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void BuildRoomTab()
    {
        dynamicTexts.Add((statusLabel, [MethodImpl(MethodImplOptions.NoInlining)]() => !PhotonNetwork.InRoom ? "You are not in a room" : "Current room: " + SanitizeText(PhotonNetwork.CurrentRoom.Name)));
        AddSetting(0, "Queue",     GameSettings.GetQueueLabel,    GameSettings.CycleQueue);
        AddSetting(1, "Game mode", GameSettings.GetGameModeLabel, GameSettings.CycleGameMode);
        AddSetting(2, "Time of day", GameSettings.GetTimeOfDayLabel, delegate
                                                                     {
                                                                         GameSettings.CycleTimeOfDay(1);
                                                                     }, "time");

        AddSetting(3, "Microphone", GameSettings.GetPushToTalkLabel,                                                                   GameSettings.CyclePushToTalkMode, "mic");
        AddSetting(4, "Lock time",  [MethodImpl(MethodImplOptions.NoInlining)]() => !GameSettings.IsTimeOverrideActive ? "Off" : "On", GameSettings.ToggleTimeLock,      "time");
        AddButton(contentRoot.transform, "Leave room", 0f, 350f, 246f, delegate
                                                                       {
                                                                           if (NetworkSystem.Instance != null)
                                                                           {
                                                                               NetworkSystem.Instance.ReturnToSinglePlayer();
                                                                           }
                                                                       }, () => PhotonNetwork.InRoom);

        AddButton(contentRoot.transform, "Lobby hop", 265f, 350f, 246f, delegate
                                                                        {
                                                                            RingMenu.Ins?.DesktopHop();
                                                                        }, [MethodImpl(MethodImplOptions.NoInlining)]() => Manifest.Has("lobby_hop"));

        AddButton(contentRoot.transform, "", 0f, 413f, 510f, delegate
                                                             {
                                                                 joinCodeFocused = true;
                                                             }, [MethodImpl(MethodImplOptions.NoInlining)]() => Manifest.Has("join_code")).valueProvider = [MethodImpl(MethodImplOptions.NoInlining)]() =>
                                                                                                                                                           {
                                                                                                                                                               if (joinCode.Length != 0)
                                                                                                                                                               {
                                                                                                                                                                   return joinCode + (joinCodeFocused ? " |" : "");
                                                                                                                                                               }

                                                                                                                                                               return Manifest.Has("join_code") ? "Click and type a room code" : "Room codes require Premium";
                                                                                                                                                           };

        AddButton(contentRoot.transform, "Join code", 530f, 413f, 262f, delegate
                                                                        {
                                                                            joinCodeFocused = false;
                                                                            RingMenu.Ins?.DesktopJoin(joinCode);
                                                                        }, [MethodImpl(MethodImplOptions.NoInlining)]() => Manifest.Has("join_code") && RoomCode.Valid(joinCode));

        AddDynamicText(() => RingMenu.Ins?.DesktopJoinStatus ?? "", 0f, 466f, 790f, 25f);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void BuildNametagsTab()
    {
        statusLabel.text = "Changes apply to the nametags you see in your game.";
        AddBooleanSetting(0, "Nametags",      Cfg.Tags);
        AddBooleanSetting(1, "Show FPS",      Cfg.TagFps);
        AddBooleanSetting(2, "Show platform", Cfg.TagPlat);
        AddSetting(3, "Font", () => Theme.TagFontName(Cfg.TagFont.Value), delegate
                                                                          {
                                                                              Cfg.TagFont.Value = (Cfg.TagFont.Value + 1) % 4;
                                                                          }, "custom_tags");

        AddSetting(4, "Size", () => Theme.TagSizeName(Cfg.TagSize.Value), delegate
                                                                          {
                                                                              Cfg.TagSize.Value = (Cfg.TagSize.Value + 1) % 3;
                                                                          }, "custom_tags");

        AddSetting(5, "Color", () => Theme.TagColorName(Cfg.TagColor.Value), delegate
                                                                             {
                                                                                 Cfg.TagColor.Value = (Cfg.TagColor.Value + 1) % 3;
                                                                             }, "custom_tags");

        AddBooleanSetting(6, "Bold names", Cfg.TagBold, "custom_tags");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void BuildAppearanceTab()
    {
        statusLabel.text = "Shared preferences for your desktop and VR menus.";
        AddSetting(0, "Theme", () => Theme.Name(Cfg.Theme.Value), delegate
                                                                  {
                                                                      Cfg.Theme.Value = (Cfg.Theme.Value + 1) % Theme.Count;
                                                                      Theme.Apply(Cfg.Theme.Value);
                                                                      RingMenu.Ins?.RefreshAppearance();
                                                                      Plugin.Ins?.Disc?.Retheme();
                                                                  });

        AddSetting(1, "VR layout", [MethodImpl(MethodImplOptions.NoInlining)]() => !Theme.RectangleMenu ? "Disc" : "Hologram rectangle", delegate
                                                                                                                                         {
                                                                                                                                             Cfg.MenuLayout.Value = Cfg.MenuLayout.Value == 0 ? 1 : 0;
                                                                                                                                         }, "menu_rectangle");

        AddBooleanSetting(2, "Menu sounds", Cfg.Sounds);
        AddSetting(3, "Sound pack", () => Theme.SfxName(Cfg.MenuSfx.Value), delegate
                                                                            {
                                                                                Cfg.MenuSfx.Value = (Cfg.MenuSfx.Value + 1) % 4;
                                                                                Theme.ClickSound();
                                                                            }, "menu_sfx");

        AddBooleanSetting(4, "Tooltips", Cfg.Tooltips);
        AddButton(contentRoot.transform, "Choose custom color", 0f, 353f, 300f, delegate
                                                                                {
                                                                                    customThemeOpen = true;
                                                                                    RebuildTab();
                                                                                }, [MethodImpl(MethodImplOptions.NoInlining)]() => Manifest.Has("custom_theme"));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void BuildCustomThemeTab()
    {
        statusLabel.text = "Drag around the wheel to pick a color. Use the slider to change brightness.";
        colorPicker      = new ThemeColorPicker();
        colorWheelRect   = AddTexture("Color wheel", colorPicker.ColorWheelTexture, 20f, 96f,  280f, 280f);
        brightnessRect   = AddTexture("Brightness",  colorPicker.BrightnessTexture, 20f, 425f, 280f, 24f);
        AddText(contentRoot.transform, "Brightness", 20f, 385f, 280f, 32f, 16f, Color.white);
        colorWheelMarker = AddPanelImage(contentRoot.transform, "Color selection",      0f, 0f, 14f, 14f, Color.white).rectTransform;
        brightnessMarker = AddPanelImage(contentRoot.transform, "Brightness selection", 0f, 0f, 6f,  34f, Color.white).rectTransform;
        AddText(contentRoot.transform, "Preview", 390f, 110f, 280f, 32f, 18f, Color.white);
        colorPreview  = AddPanelImage(contentRoot.transform, "Color preview", 390f, 161f, 230f, 105f, colorPicker.SelectedColor);
        colorHexLabel = AddText(contentRoot.transform, "", 390f, 280f, 280f, 32f, 18f, Color.white);
        AddButton(contentRoot.transform, "Reset color", 390f, 345f, 230f, delegate
                                                                          {
                                                                              colorPicker.Reset();
                                                                              UpdateThemePreview();
                                                                          });

        AddButton(contentRoot.transform, "Done", 390f, 410f, 230f, delegate
                                                                   {
                                                                       colorPicker.SaveChanges();
                                                                       customThemeOpen = false;
                                                                       RingMenu.Ins?.RefreshAppearance();
                                                                       RebuildTab();
                                                                   });

        UpdateThemePreview();
    }

    private RectTransform AddTexture(string arg1, Texture arg2, float arg3, float arg4, float arg5, float arg6)
    {
        GameObject val = new(arg1, typeof(RectTransform), typeof(RawImage));
        val.transform.SetParent(contentRoot.transform, false);
        RectTransform val2 = (RectTransform)val.transform;
        SetRect(val2, arg3, arg4, arg5, arg6);
        RawImage component = val.GetComponent<RawImage>();
        component.texture       = arg2;
        component.raycastTarget = false;

        return val2;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void UpdateThemePreview()
    {
        SetRect(colorWheelMarker, 153f + colorPicker.WheelPosition.x            * 140f, 229f - colorPicker.WheelPosition.y * 140f, 14f, 14f);
        SetRect(brightnessMarker, 17f  + (colorPicker.Brightness - 0.1f) / 0.9f * 280f, 420f,                                      6f,  34f);
        colorPreview.color = colorPicker.SelectedColor;
        colorHexLabel.text = "#" + ColorUtility.ToHtmlStringRGB(colorPicker.SelectedColor);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void HandleThemePointer(Mouse arg1, Vector2 arg2)
    {
        if (colorPicker == null || !Manifest.Has("custom_theme"))
        {
            return;
        }

        if (arg1.leftButton.wasPressedThisFrame)
        {
            Vector2 val = default;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(colorWheelRect, arg2, (Camera)null, out val);
            Vector2 val2 = new(val.x - 140f, val.y + 140f);
            if (val2.sqrMagnitude > 19600f)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(brightnessRect, arg2))
                {
                    themeDragMode = 2;
                }
            }
            else
            {
                themeDragMode = 1;
            }
        }

        if (arg1.leftButton.isPressed && themeDragMode > 0)
        {
            Vector2 val3 = default;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(themeDragMode == 1 ? colorWheelRect : brightnessRect, arg2, (Camera)null, out val3);
            if (themeDragMode != 1)
            {
                colorPicker.SetBrightness(val3.x / 280f);
            }
            else
            {
                colorPicker.SetHueSaturation(new Vector2((val3.x - 140f) / 140f, (val3.y + 140f) / 140f));
            }

            UpdateThemePreview();
        }

        if (arg1.leftButton.wasReleasedThisFrame)
        {
            colorPicker.SaveChanges();
            themeDragMode = 0;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void BuildControlsTab()
    {
        statusLabel.text = "Tab opens and closes this panel. Arrow keys move focus; Enter selects.";
        AddBooleanSetting(0, "Keep VR menu open", Cfg.KeepMenuOpen);
        AddBooleanSetting(1, "Pointer select",    Cfg.PlayerRay);
        AddBooleanSetting(2, "Hand menu",         Cfg.HandMenu, "hand_menu");
        AddBooleanSetting(3, "One hand mode",     Cfg.OneHand);
        AddBooleanSetting(4, "Swap hands",        Cfg.SwapHands, "swap_hands");
        AddBooleanSetting(5, "Touch selection",   Cfg.Touch);
        AddSetting(6, "Share activity with friends", [MethodImpl(MethodImplOptions.NoInlining)]() => !Friends.Busy ? Friends.ShareActivity ? "On" : "Off" : "Saving...", delegate
                                                                                                                                                                         {
                                                                                                                                                                             Friends.SetSharing(!Friends.ShareActivity);
                                                                                                                                                                         });

        AddText(contentRoot.transform, "Hold the pointer hand trigger to aim, release to inspect. Keep both grips released.\nHold right mouse to aim at a player, then left click to inspect.\nClose the menu before aiming at a player.", 2f, 435f, 790f, 62f, 12f, MutedTextColor);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void BuildMusicTab()
    {
        statusLabel.text = "Control your desktop player or connected Spotify account.";
        AddDynamicText(() => DesktopMediaControls.TrackTitle, 0f, 93f, 790f, 45f);
        AddButton(contentRoot.transform, "Previous",     0f,   152f, 246f, DesktopMediaControls.PreviousTrack);
        AddButton(contentRoot.transform, "Play / pause", 265f, 152f, 246f, DesktopMediaControls.TogglePlayPause);
        AddButton(contentRoot.transform, "Next",         530f, 152f, 262f, DesktopMediaControls.NextTrack);
        if (Manifest.Has("spotify"))
        {
            AddDynamicText([MethodImpl(MethodImplOptions.NoInlining)]() => !SpotifyPlayer.OAuth.IsConnected ? "Spotify is not connected. Set your client ID in the BepInEx config." : SanitizeText(SpotifyPlayer.TrackName) + "\n" + SanitizeText(SpotifyPlayer.ArtistName), 0f, 231f, 790f, 73f);
            AddButton(contentRoot.transform, "Connect Spotify", 0f, 323f, 246f, delegate
                                                                                {
                                                                                    SpotifyPlayer.OAuth.BeginLogin();
                                                                                }, [MethodImpl(MethodImplOptions.NoInlining)]() => Manifest.Has("spotify") && !SpotifyPlayer.OAuth.LoginPending && !SpotifyPlayer.OAuth.IsConnected && SpotifyPlayer.OAuth.ClientId.Length > 0);

            AddButton(contentRoot.transform, "Spotify play / pause", 265f, 323f, 246f, SpotifyPlayer.TogglePlayback, [MethodImpl(MethodImplOptions.NoInlining)]() => Manifest.Has("spotify") && SpotifyPlayer.OAuth.IsConnected);
            AddButton(contentRoot.transform, "Spotify next",         530f, 323f, 262f, SpotifyPlayer.NextTrack,      [MethodImpl(MethodImplOptions.NoInlining)]() => Manifest.Has("spotify") && SpotifyPlayer.OAuth.IsConnected);
            AddButton(contentRoot.transform, "Volume down", 0f, 380f, 246f, delegate
                                                                            {
                                                                                SpotifyPlayer.AdjustVolume(-10);
                                                                            }, [MethodImpl(MethodImplOptions.NoInlining)]() => Manifest.Has("spotify") && SpotifyPlayer.OAuth.IsConnected);

            AddButton(contentRoot.transform, "Volume up", 265f, 380f, 246f, delegate
                                                                            {
                                                                                SpotifyPlayer.AdjustVolume(10);
                                                                            }, [MethodImpl(MethodImplOptions.NoInlining)]() => Manifest.Has("spotify") && SpotifyPlayer.OAuth.IsConnected);

            AddButton(contentRoot.transform, "Shuffle", 530f, 380f, 262f, SpotifyPlayer.ToggleShuffle, [MethodImpl(MethodImplOptions.NoInlining)]() => Manifest.Has("spotify") && SpotifyPlayer.OAuth.IsConnected);
            AddDynamicText(() => SpotifyPlayer.StatusMessage.Length <= 0 ? DesktopMediaControls.StatusMessage : SpotifyPlayer.StatusMessage, 0f, 449f, 790f);
        }
        else
        {
            AddText(contentRoot.transform, "Spotify controls are disabled.", 0f, 245f, 790f, 42f, 17f, MutedTextColor);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void BuildStreamChatTab()
    {
        statusLabel.text = Manifest.Has("youtube_chat") ? "Following your YouTube channel automatically" : "Stream chat is disabled.";
        if (!Manifest.Has("youtube_chat"))
        {
            return;
        }

        AddDynamicText(() => YouTubeLiveChat.Status,      0f, 88f,  790f, 35f);
        AddDynamicText(() => YouTubeLiveChat.StreamTitle, 0f, 125f, 790f, 30f);
        AddDynamicText([MethodImpl(MethodImplOptions.NoInlining)]() =>
                       {
                           string[]     messages = YouTubeLiveChat.Messages;
                           int          num      = Mathf.Max(0, messages.Length - 10);
                           List<string> list     = new();
                           for (int i = num; i < messages.Length; i++)
                           {
                               list.Add(SanitizeText(messages[i]));
                           }

                           if (list.Count != 0)
                           {
                               return string.Join("\n", list);
                           }

                           return !YouTubeLiveChat.IsConfigured ? "Save your @YouTubeHandle in the BepInEx config." : "Messages will appear here automatically when live chat is available.";
                       }, 0f, 165f, 790f, 265f);

        AddText(contentRoot.transform, "New streams and messages update automatically.", 0f, 453f, 790f, 32f, 14f, MutedTextColor);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void BuildFriendsTab()
    {
        FriendState state = Friends.State;
        statusLabel.text = "Requests need acceptance. Activity is visible only when your friend shares it.";
        string[] array = new string[3] { "Friends", "Incoming", "Sent requests", };
        for (int i = 0; i < array.Length; i++)
        {
            int index0 = i;
            AddButton(contentRoot.transform, array[i], i * 265, 83f, 246f, delegate
                                                                           {
                                                                               friendsTab = index0;
                                                                               listPage   = 0;
                                                                               RebuildTab();
                                                                           });
        }

        FriendView[] array2 = friendsTab == 1 ? state.Incoming : friendsTab == 2 ? state.Outgoing : state.Friends;
        if (listPage * 4 >= array2.Length)
        {
            listPage = 0;
        }

        int num = listPage * 4;
        for (int num2 = num; num2 < array2.Length && num2 < num + 4; num2++)
        {
            FriendView friendView0 = array2[num2];
            float      num3        = 144 + (num2 - num) * 69;
            AddText(contentRoot.transform, SanitizeText(friendView0.Name),                          0f, num3,       475f, 28f, 17f, Color.white);
            AddText(contentRoot.transform, friendView0.Activity(DateTime.UtcNow, state.ReceivedAt), 0f, num3 + 29f, 475f, 26f, 14f, MutedTextColor);
            if (friendsTab == 0)
            {
                AddButton(contentRoot.transform, "Remove", 624f, num3 + 3f, 168f, delegate
                                                                                  {
                                                                                      Friends.Remove(friendView0.Id);
                                                                                  }, () => !Friends.Busy);
            }

            if (friendsTab == 1)
            {
                AddButton(contentRoot.transform, "Accept", 494f, num3 + 3f, 140f, delegate
                                                                                  {
                                                                                      Friends.Respond(friendView0.RequestId, true);
                                                                                  }, () => !Friends.Busy);

                AddButton(contentRoot.transform, "Decline", 648f, num3 + 3f, 144f, delegate
                                                                                   {
                                                                                       Friends.Respond(friendView0.RequestId, false);
                                                                                   }, () => !Friends.Busy);
            }
        }

        if (array2.Length == 0)
        {
            AddText(contentRoot.transform, friendsTab == 0 ? "No accepted friends yet. Select a lobby player to send a request." : "No requests here.", 0f, 153f, 790f, 70f, 17f, MutedTextColor);
        }

        AddDynamicText(() => Friends.Status, 0f, 421f, 790f, 29f);
        AddText(contentRoot.transform, "Your friend code: " + state.FriendCode, 0f, 461f, 595f, 28f, 14f, MutedTextColor);
        if (array2.Length > 4)
        {
            AddButton(contentRoot.transform, "Next page", 624f, 452f, 168f, delegate
                                                                            {
                                                                                listPage = (listPage + 1) % ((array2.Length + 3) / 4);
                                                                                RebuildTab();
                                                                            });
        }
    }

    private void AddBooleanSetting(int arg1, string arg2, ConfigEntry<bool> arg3, string arg4 = null) =>
            AddSetting(arg1, arg2, [MethodImpl(MethodImplOptions.NoInlining)]() => !arg3.Value ? "Off" : "On", delegate
                                                                                                               {
                                                                                                                   arg3.Value = !arg3.Value;
                                                                                                               }, arg4);

    private void AddSetting(int arg1, string arg2, Func<string> arg3, Action arg4, string arg5 = null)
    {
        float num = 88                               + arg1 * 49;
        AddText(contentRoot.transform, arg2, 2f, num + 4f, 377f, 32f, 16f, Color.white);
        AddButton(contentRoot.transform, "", 396f, num, 396f, arg4, () => arg5 == null || Manifest.Has(arg5)).valueProvider = [MethodImpl(MethodImplOptions.NoInlining)]() => arg5 != null && !Manifest.Has(arg5) ? "Unavailable" : arg3();
    }

    private void AddDynamicText(Func<string> arg1, float arg2, float arg3, float arg4 = 390f, float arg5 = 42f)
    {
        TMP_Text item = AddText(contentRoot.transform, "", arg2, arg3, arg4, arg5, 16f, MutedTextColor);
        dynamicTexts.Add((item, arg1));
    }

    private Image AddPanelImage(Transform arg1, string arg2, float arg3, float arg4, float arg5, float arg6, Color arg7)
    {
        GameObject val = new(arg2, typeof(RectTransform), typeof(Image));
        val.transform.SetParent(arg1, false);
        SetRect((RectTransform)val.transform, arg3, arg4, arg5, arg6);
        Image component = val.GetComponent<Image>();
        component.color         = arg7;
        component.sprite        = roundedSprite;
        component.type          = (Image.Type)1;
        component.raycastTarget = false;

        return component;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private TMP_Text AddText(Transform arg1, string arg2, float arg3, float arg4, float arg5, float arg6, float arg7, Color arg8)
    {
        GameObject val = new("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        val.transform.SetParent(arg1, false);
        SetRect((RectTransform)val.transform, arg3, arg4, arg5, arg6);
        TextMeshProUGUI component = val.GetComponent<TextMeshProUGUI>();
        if (fontAsset != null)
        {
            component.font = fontAsset;
        }

        component.text             = arg2;
        component.fontSize         = arg7;
        component.color            = arg8;
        component.richText         = false;
        component.alignment        = (TextAlignmentOptions)4097;
        component.textWrappingMode = (TextWrappingModes)1;
        component.overflowMode     = (TextOverflowModes)1;
        component.raycastTarget    = false;

        return component;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private MenuItem AddButton(Transform arg1, string arg2, float arg3, float arg4, float arg5, Action arg6, Func<bool> arg7 = null)
    {
        Image    val  = AddPanelImage(arg1, "Control", arg3, arg4, arg5, 39f, ControlColor);
        TMP_Text val2 = AddText(val.transform, arg2, 15f, 1f, arg5 - 30f, 37f, 14f, Color.white);
        val2.textWrappingMode = 0;
        MenuItem menuItem = new()
        {
                root       = val.rectTransform,
                background = val,
                label      = val2,
                onClick    = arg6,
                isEnabled  = arg7,
        };

        menuItems.Add(menuItem);

        return menuItem;
    }

    private static void SetRect(RectTransform arg1, float arg2, float arg3, float arg4, float arg5)
    {
        Vector2 val = default;
        val        = new Vector2(0f, 1f);
        arg1.pivot = val;
        Vector2 anchorMin = arg1.anchorMax = val;
        arg1.anchorMin        = anchorMin;
        arg1.anchoredPosition = new Vector2(arg2, 0f - arg3);
        arg1.sizeDelta        = new Vector2(arg4, arg5);
    }

    private void HandlePointerInput()
    {
        Mouse current = Mouse.current;
        int   num     = -1;
        if (current != null)
        {
            Vector2 val = current.position.ReadValue();
            HandleThemePointer(current, val);
            int num2 = menuItems.Count - 1;
            while (num2 >= 0)
            {
                if (!IsItemEnabled(num2) || !RectTransformUtility.RectangleContainsScreenPoint(menuItems[num2].root, val))
                {
                    num2--;

                    continue;
                }

                num = num2;

                break;
            }

            if (num != hoveredItemIndex && num >= 0)
            {
                Theme.HoverSound();
            }

            hoveredItemIndex = num;
            if (current.leftButton.wasPressedThisFrame)
            {
                joinCodeFocused   = false;
                keyboardSelection = num;
                if (num >= 0)
                {
                    ActivateItem(num);
                }
            }
        }

        for (int i = 0; i < menuItems.Count; i++)
        {
            MenuItem menuItem = menuItems[i];
            bool     flag     = IsItemEnabled(i);
            menuItem.background.color = !flag ? PanelColor : i == hoveredItemIndex || i == keyboardSelection ? Color.Lerp(ControlColor, Theme.Main, 0.3f) : i == currentTab + 1 ? Color.Lerp(ControlColor, Theme.Main, 0.18f) : ControlColor;
            menuItem.label.color      = flag ? Color.white : MutedTextColor * 0.7f;
        }
    }

    private bool IsItemEnabled(int arg1)
    {
        if (arg1 >= 0 && arg1 < menuItems.Count)
        {
            if (menuItems[arg1].isEnabled != null)
            {
                return menuItems[arg1].isEnabled();
            }

            return true;
        }

        return false;
    }

    private void MoveKeyboardSelection(int arg1)
    {
        joinCodeFocused = false;
        for (int i = 0; i < menuItems.Count; i++)
        {
            keyboardSelection = (keyboardSelection + arg1 + menuItems.Count) % menuItems.Count;
            if (IsItemEnabled(keyboardSelection))
            {
                break;
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ActivateItem(int arg1)
    {
        if (!IsItemEnabled(arg1))
        {
            return;
        }

        try
        {
            menuItems[arg1].onClick?.Invoke();
            Theme.ClickSound();
            RefreshValues();
        }
        catch (Exception)
        {
            actionStatus = "That action could not finish. Try again.";
            RefreshValues();
        }
    }

    private void RefreshValues()
    {
        foreach ((TMP_Text Text, Func<string> Value) item in dynamicTexts)
        {
            if (item.Text != null)
            {
                item.Text.text = item.Value() ?? "";
            }
        }

        foreach (MenuItem item2 in menuItems)
        {
            if (item2.valueProvider != null)
            {
                item2.label.text = item2.valueProvider() ?? "";
            }
        }

        if (actionStatus.Length > 0 && statusLabel != null)
        {
            statusLabel.text = actionStatus;
        }
    }

    private static string SanitizeText(string arg1) => (arg1 ?? "").Replace('\r', ' ').Replace('\n', ' ');

    private static VRRig FindRigByUserId(string arg1)
    {
        if (!VRRigCache.isInitialized)
        {
            return null;
        }

        foreach (RigContainer activeRigContainer in VRRigCache.ActiveRigContainers)
        {
            if (activeRigContainer.Rig != null && activeRigContainer.Rig.Creator != null && activeRigContainer.Rig.Creator.UserId == arg1)
            {
                return activeRigContainer.Rig;
            }
        }

        return null;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string BuildLobbyFingerprint()
    {
        List<string> list             = new();
        Player[]     playerListOthers = PhotonNetwork.PlayerListOthers;
        foreach (Player val in playerListOthers)
        {
            list.Add(val.UserId + ":" + val.NickName);
        }

        list.Add(Friends.Revision.ToString());

        return string.Join("|", list);
    }

    [CompilerGenerated]
    private void CloseFromButton() => SetOpen(false);

    [CompilerGenerated]
    private bool IsDestroyedDynamicText((TMP_Text Text, Func<string> Value) arg1)
    {
        if (!(arg1.Text == null))
        {
            return arg1.Text.transform.IsChildOf(contentRoot.transform);
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [CompilerGenerated]
    private string GetSelectedPlatformText()
    {
        if (FindSelectedPlayer() != null)
        {
            return "Platform: " + Detect.PlatformOf(FindRigByUserId(selectedPlayerId));
        }

        return "Player left the lobby";
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [CompilerGenerated]
    private string GetSelectedTierText() => Net.Has(selectedPlayerId) ? "Sentinel: Detected" : "Sentinel: Not detected";

    [CompilerGenerated]
    private string GetSelectedScanText()
    {
        Player val = FindSelectedPlayer();
        if (val == null)
        {
            return "";
        }

        return FormatScan(Detect.Get(val));
    }

    [CompilerGenerated]
    private void ToggleSelectedFriend()
    {
        Player val = FindSelectedPlayer();
        if (val != null)
        {
            if (!Friends.IsFriend(val.UserId))
            {
                Friends.Add(val.UserId, val.NickName);
            }
            else
            {
                Friends.Remove(val.UserId);
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [CompilerGenerated]
    private bool CanChangeFriendship()
    {
        if (Manifest.Has("friends") && FindSelectedPlayer() != null)
        {
            return !Friends.Busy;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [CompilerGenerated]
    private string GetFriendActionLabel()
    {
        if (!Friends.Busy)
        {
            if (Friends.IsFriend(selectedPlayerId))
            {
                return "Remove friend";
            }

            return "Request friendship";
        }

        return "Saving...";
    }

    [CompilerGenerated]
    private bool HasSelectedPlayer() => FindSelectedPlayer() != null;

    [MethodImpl(MethodImplOptions.NoInlining)]
    [CompilerGenerated]
    private bool CanReportSelectedPlayer()
    {
        if (Manifest.Has("quick_report"))
        {
            return FindSelectedPlayer() != null;
        }

        return false;
    }

    [CompilerGenerated]
    private void BackToLobby()
    {
        selectedPlayerId = "";
        RebuildTab();
    }

    [CompilerGenerated]
    private void ReportCheating() => SubmitReport(1);

    [CompilerGenerated]
    private void ReportToxicity() => SubmitReport(2);

    [CompilerGenerated]
    private void ReportHateSpeech() => SubmitReport(0);

    [CompilerGenerated]
    private void FocusJoinCode() => joinCodeFocused = true;

    [MethodImpl(MethodImplOptions.NoInlining)]
    [CompilerGenerated]
    private string GetJoinCodeLabel()
    {
        if (joinCode.Length != 0)
        {
            return joinCode + (joinCodeFocused ? " |" : "");
        }

        if (Manifest.Has("join_code"))
        {
            return "Click and type a room code";
        }

        return "Room codes require Premium";
    }

    [CompilerGenerated]
    private void JoinEnteredCode()
    {
        joinCodeFocused = false;
        RingMenu.Ins?.DesktopJoin(joinCode);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [CompilerGenerated]
    private bool CanJoinCode()
    {
        if (Manifest.Has("join_code"))
        {
            return RoomCode.Valid(joinCode);
        }

        return false;
    }

    [CompilerGenerated]
    private void OpenCustomTheme()
    {
        customThemeOpen = true;
        RebuildTab();
    }

    [CompilerGenerated]
    private void ResetCustomTheme()
    {
        colorPicker.Reset();
        UpdateThemePreview();
    }

    [CompilerGenerated]
    private void SaveCustomTheme()
    {
        colorPicker.SaveChanges();
        customThemeOpen = false;
        RingMenu.Ins?.RefreshAppearance();
        RebuildTab();
    }

    private sealed class MenuItem
    {

        public Image background;

        public Func<bool> isEnabled;

        public TMP_Text label;

        public Action        onClick;
        public RectTransform root;

        public Func<string> valueProvider;
    }
}