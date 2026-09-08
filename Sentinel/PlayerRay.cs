using System.Collections.Generic;
using System.Runtime.CompilerServices;
using GorillaLocomotion;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace Sentinel;

[DefaultExecutionOrder(-20)]
public sealed class PlayerRay : MonoBehaviour
{
    public const string VrHint = "Hold the pointer hand trigger to aim, release to inspect. Keep both grips released.";

    public const string PcHint = "Hold right mouse to aim at a player, then left click to inspect.";

    private const float MaxDistance = 40f;

    private readonly PlayerRayInput desktopInput = new();

    private readonly Collider[] overlapColliders = new Collider[32];

    private readonly RaycastHit[] raycastHits = new RaycastHit[64];

    private readonly PlayerRayInput vrInput = new();

    private bool cursorOverridden;

    private Camera desktopCamera;

    private float nextCameraRefreshAt;

    private LineRenderer pointerLine;

    private GameObject pointerRoot;

    private CursorLockMode previousCursorLockMode;

    private bool previousCursorVisible;

    private Transform reticle;

    private Renderer reticleRenderer;

    private bool triggerHeld;

    public static bool IsAiming { get; private set; }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Update()
    {
        GorillaTagger         instance  = GorillaTagger.Instance;
        Mouse                 current   = Mouse.current;
        Keyboard              current2  = Keyboard.current;
        ControllerInputPoller instance2 = ControllerInputPoller.instance;
        bool                  flag      = current != null && current.rightButton.isPressed;
        bool                  value     = Cfg.SwapHands.Value;
        float                 num       = instance2 == null ? 0f : value ? instance2.leftControllerIndexFloat : instance2.rightControllerIndexFloat;
        if (num < 0.65f)
        {
            if (num <= 0.2f)
            {
                triggerHeld = false;
            }
        }
        else
        {
            triggerHeld = true;
        }

        bool            flag2 = (current2 == null || !current2.escapeKey.wasPressedThisFrame && !current2.tabKey.wasPressedThisFrame) && Manifest.Has("ring_menu") && Cfg.PlayerRay.Value && Application.isFocused && PhotonNetwork.InRoom && VRRigCache.isInitialized && instance != null && instance.mainCamera != null && !RingMenu.IsOpen && !PcMenu.IsOpen && (Plugin.Ins == null || Plugin.Ins.Disc == null || !Plugin.Ins.Disc.InputBusy);
        bool            flag3 = flag || desktopInput.Aiming;
        Ray             arg   = default;
        PlayerRayAction playerRayAction;
        if (!flag3)
        {
            RestoreCursor();
            desktopInput.Step(flag2 && current                          != null, false, false, false);
            Transform val   = instance                                  == null ? null : value ? instance.leftHandTransform : instance.rightHandTransform;
            bool      flag4 = flag2 && XRSettings.isDeviceActive && val != null && instance2 != null && !instance2.leftGrab && !instance2.rightGrab;
            playerRayAction = vrInput.Step(flag4, triggerHeld, false, true);
            arg             = flag4 ? new Ray(val.position, val.rotation * Quaternion.Euler(45f, value ? 10f : -10f, 0f) * Vector3.forward) : default(Ray);
        }
        else
        {
            vrInput.Step(false, triggerHeld, false, true);
            playerRayAction = desktopInput.Step(flag2 && Manifest.Has("pc_ui"), flag, current != null && current.leftButton.wasPressedThisFrame, false);
            if (playerRayAction != PlayerRayAction.None)
            {
                UnlockCursor();
                if (!TryGetDesktopRay(instance, current, out arg))
                {
                    desktopInput.Step(false, flag, false, false);
                    playerRayAction = PlayerRayAction.None;
                }
            }
        }

        IsAiming = playerRayAction == PlayerRayAction.Aim;
        if (playerRayAction == PlayerRayAction.None)
        {
            HidePointer();

            return;
        }

        Player  arg2;
        Vector3 arg3;
        bool    flag5 = TrySelectPlayer(arg, instance, out arg2, out arg3);
        if (playerRayAction != PlayerRayAction.Select)
        {
            UpdatePointer(arg, arg3, flag5);

            return;
        }

        HidePointer();
        if (flag5 && arg2 != null && !arg2.IsInactive && PhotonNetwork.InRoom)
        {
            Theme.ClickSound();
            if (flag3)
            {
                PcMenu.OpenPlayer(arg2);
            }
            else if (RingMenu.Ins != null)
            {
                Theme.Haptic(value, 0.35f, 0.05f);
                RingMenu.Ins.Open(GameSettings.GroundPointAhead(instance.mainCamera.transform, 1.2f), arg2);
            }
        }
    }

    private void OnDisable() => Cancel();

    private void OnDestroy()
    {
        IsAiming = false;
        RestoreCursor();
        if (pointerRoot != null)
        {
            Destroy(pointerRoot);
        }
    }

    public static void CancelActive()
    {
        PlayerRay playerRay = Plugin.Ins != null ? Plugin.Ins.GetComponent<PlayerRay>() : null;
        if (playerRay != null)
        {
            playerRay.Cancel();
        }
    }

    private bool TryGetDesktopRay(GorillaTagger arg1, Mouse arg2, out Ray arg3)
    {
        arg3 = default(Ray);
        if (!(arg1 == null) && arg2 != null)
        {
            if (!IsUsableCamera(desktopCamera) || Time.unscaledTime >= nextCameraRefreshAt)
            {
                nextCameraRefreshAt = Time.unscaledTime + 0.5f;
                desktopCamera       = arg1.thirdPersonCamera != null ? arg1.thirdPersonCamera.GetComponentInChildren<Camera>(true) : null;
                if (!IsUsableCamera(desktopCamera))
                {
                    desktopCamera = Camera.main;
                }

                if (!IsUsableCamera(desktopCamera))
                {
                    desktopCamera = arg1.mainCamera != null ? arg1.mainCamera.GetComponent<Camera>() : null;
                }
            }

            if (!IsUsableCamera(desktopCamera))
            {
                return false;
            }

            Vector2 val;
            Rect    pixelRect;
            if ((int)Cursor.lockState != 1)
            {
                val = arg2.position.ReadValue();
            }
            else
            {
                pixelRect = desktopCamera.pixelRect;
                val       = pixelRect.center;
            }

            Vector2 val2 = val;
            pixelRect = desktopCamera.pixelRect;
            if (pixelRect.Contains(val2))
            {
                arg3 = desktopCamera.ScreenPointToRay(val2);

                return true;
            }

            return false;
        }

        return false;
    }

    private static bool IsUsableCamera(Camera arg1)
    {
        if (arg1 != null && arg1.isActiveAndEnabled && (int)arg1.cameraType == 1 && arg1.targetTexture == null)
        {
            return arg1.targetDisplay == 0;
        }

        return false;
    }

    private bool TrySelectPlayer(Ray arg1, GorillaTagger arg2, out Player arg3, out Vector3 arg4)
    {
        arg3 = null;
        arg4 = arg1.GetPoint(40f);
        int num = Physics.OverlapSphereNonAlloc(arg1.origin, 0.01f, overlapColliders, -5, (QueryTriggerInteraction)2);
        if (num != overlapColliders.Length)
        {
            for (int i = 0; i < num; i++)
            {
                Collider val = overlapColliders[i];
                if (!(val == null))
                {
                    VRRig componentInParent = val.GetComponentInParent<VRRig>();
                    if (!IsIgnoredHit(val, componentInParent, arg2) && (!val.isTrigger || GetPlayer(componentInParent) != null))
                    {
                        arg4 = arg1.origin;

                        return false;
                    }
                }
            }

            int                num2               = Physics.RaycastNonAlloc(arg1, raycastHits, 40f, -5, (QueryTriggerInteraction)2);
            PlayerRaySelection playerRaySelection = new(40f);
            for (int j = 0; j < num2; j++)
            {
                Collider collider = raycastHits[j].collider;
                if (!(collider == null))
                {
                    VRRig componentInParent2 = collider.GetComponentInParent<VRRig>();
                    if (!IsIgnoredHit(collider, componentInParent2, arg2))
                    {
                        bool flag = GetPlayer(componentInParent2) != null;
                        playerRaySelection.Consider(j, raycastHits[j].distance, flag, !flag && !collider.isTrigger);
                    }
                }
            }

            int num3 = playerRaySelection.Finish(num2 == raycastHits.Length);
            arg4 = arg1.GetPoint(playerRaySelection.BlockerDistance);
            if (num3 < 0)
            {
                return false;
            }

            arg3 = GetPlayer(raycastHits[num3].collider.GetComponentInParent<VRRig>());
            arg4 = raycastHits[num3].point;

            return arg3 != null;
        }

        arg4 = arg1.origin;

        return false;
    }

    private static bool IsIgnoredHit(Collider arg1, VRRig arg2, GorillaTagger arg3)
    {
        if (arg2 != null && (arg2.isLocal || arg2.isOfflineVRRig || arg2.Creator != null && arg2.Creator.IsLocal))
        {
            return true;
        }

        if (!(arg3 != null) || !(arg1 == arg3.headCollider) && !(arg1 == arg3.bodyCollider))
        {
            if (!(GTPlayer.Instance != null))
            {
                return false;
            }

            return arg1.transform.IsChildOf(GTPlayer.Instance.transform);
        }

        return true;
    }

    private static Player GetPlayer(VRRig arg1)
    {
        if (!(arg1 == null) && arg1.Creator != null && !string.IsNullOrEmpty(arg1.Creator.UserId) && PhotonNetwork.CurrentRoom != null)
        {
            foreach (KeyValuePair<int, Player> player in PhotonNetwork.CurrentRoom.Players)
            {
                Player value = player.Value;
                if (value != null && !value.IsLocal && !value.IsInactive && value.UserId == arg1.Creator.UserId)
                {
                    return value;
                }
            }

            return null;
        }

        return null;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void UpdatePointer(Ray arg1, Vector3 arg2, bool arg3)
    {
        if (pointerRoot == null)
        {
            pointerRoot = new GameObject("zx_player_ray");
            pointerRoot.transform.SetParent(transform, false);
            pointerLine               = pointerRoot.AddComponent<LineRenderer>();
            pointerLine.useWorldSpace = true;
            pointerLine.positionCount = 2;
            pointerLine.startWidth    = 0.003f;
            pointerLine.endWidth      = 0.0015f;
            RenderResources.Assign(pointerLine, Theme.Holo(Theme.Soft, 3000, null, 4));
            reticle         = Theme.Ring(pointerRoot.transform, "reticle", 0.008f, 0.012f, Theme.Soft).transform;
            reticleRenderer = reticle.GetComponent<Renderer>();
            reticleRenderer.sharedMaterial.SetInt("unity_GUIZTestMode", 4);
        }

        pointerRoot.SetActive(true);
        Color color = arg3 ? Theme.Good : Theme.Soft;
        pointerLine.sharedMaterial.color = color;
        pointerLine.SetPosition(0, arg1.origin);
        pointerLine.SetPosition(1, arg2);
        reticle.position                     = arg2 - arg1.direction * 0.005f;
        reticle.rotation                     = Quaternion.LookRotation(arg1.direction);
        reticleRenderer.sharedMaterial.color = color;
    }

    private void HidePointer()
    {
        IsAiming = false;
        RestoreCursor();
        if (pointerRoot != null)
        {
            pointerRoot.SetActive(false);
        }
    }

    private void UnlockCursor()
    {
        if (!cursorOverridden)
        {
            cursorOverridden       = true;
            previousCursorVisible  = Cursor.visible;
            previousCursorLockMode = Cursor.lockState;
        }

        Cursor.visible   = true;
        Cursor.lockState = 0;
    }

    private void RestoreCursor()
    {
        if (cursorOverridden)
        {
            Cursor.visible   = previousCursorVisible;
            Cursor.lockState = previousCursorLockMode;
            cursorOverridden = false;
        }
    }

    private void Cancel()
    {
        vrInput.Step(false, false, false, true);
        desktopInput.Step(false, false, false, false);
        HidePointer();
    }
}