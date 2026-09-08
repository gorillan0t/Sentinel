using System.Runtime.CompilerServices;
using UnityEngine;

namespace Sentinel;

public class Disc : MonoBehaviour
{

    private readonly Vector3[] positionSamples = new Vector3[8];

    private readonly float[] sampleTimes = new float[8];

    private float deployProgress;

    private Quaternion deployStartRotation;

    private GameObject discObject;

    private Transform discTransform;

    private Vector3 flightControl;

    private float flightProgress;

    private Vector3 flightStart;

    private Vector3 flightTarget;

    private float grabProgress;

    private float lastGlanceAt;

    private float palmHoldTime;

    private Vector3 palmVelocity;

    private float palmVisibility;

    private float[] rendererBaseAlpha;

    private Renderer[] renderers;

    private int sampleIndex;

    private DiscState state;

    private TrailRenderer trail;

    private bool usesRectangleStyle;

    public bool InputBusy => state != DiscState.Hidden;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Start()
    {
        discObject = new GameObject("zx_disc");
        DontDestroyOnLoad(discObject);
        discTransform = discObject.transform;
        Color val = default;
        val                = new Color(Theme.Main.r, Theme.Main.g, Theme.Main.b, 0.42f);
        usesRectangleStyle = Theme.RectangleMenu;
        if (!usesRectangleStyle)
        {
            Theme.Ring(discTransform, "band",  0.02f,  0.04f,   val);
            Theme.Ring(discTransform, "edge",  0.04f,  0.0435f, Theme.Soft,                           3002, 12f,  130f);
            Theme.Ring(discTransform, "edge2", 0.04f,  0.0435f, Theme.Soft,                           3002, 192f, 130f);
            Theme.Ring(discTransform, "core",  0.016f, 0.019f,  Theme.Soft,                           3002, 260f, 200f);
            Theme.Ring(discTransform, "glow",  0.012f, 0.055f,  new Color(val.r, val.g, val.b, 0.1f), 2989);
        }
        else
        {
            Theme.Card(discTransform, "hologram", 0.09f, 0.064f, Theme.Soft, 3000);
            Theme.Quad(discTransform, "header", 0.072f, 0.003f, Theme.Holo(Theme.Main, 3002)).transform.localPosition = new Vector3(0f, 0.018f, 0f);
            for (int i = 0; i < 3; i++)
            {
                Theme.Quad(discTransform, "row" + i, 0.06f, 0.002f, Theme.Holo(val, 3002)).transform.localPosition = new Vector3(0f, 0.005f - i * 0.01f, 0f);
            }
        }

        renderers         = discObject.GetComponentsInChildren<Renderer>();
        rendererBaseAlpha = new float[renderers.Length];
        for (int j = 0; j < renderers.Length; j++)
        {
            rendererBaseAlpha[j] = usesRectangleStyle ? renderers[j].sharedMaterial.color.a : renderers[j].name == "band" ? 0.42f : renderers[j].name == "glow" ? 0.1f : 0.95f;
        }

        trail            = discObject.AddComponent<TrailRenderer>();
        trail.time       = 0.4f;
        trail.startWidth = 0.008f;
        trail.endWidth   = 0f;
        RenderResources.Assign(trail, Theme.Holo(new Color(val.r, val.g, val.b, 0.55f), 2988));
        trail.emitting = false;
        discObject.SetActive(false);
    }

    private void Update()
    {
        if (discObject == null)
        {
            state = DiscState.Hidden;
            Start();

            return;
        }

        if (usesRectangleStyle != Theme.RectangleMenu)
        {
            Retheme();

            return;
        }

        GorillaTagger instance = GorillaTagger.Instance;
        if (instance == null || instance.mainCamera == null)
        {
            return;
        }

        Transform             transform          = instance.mainCamera.transform;
        Transform             leftHandTransform  = instance.leftHandTransform;
        Transform             rightHandTransform = instance.rightHandTransform;
        bool                  flag;
        Transform             val       = (flag = Cfg.OneHand.Value ? Cfg.SwapHands.Value : !Cfg.SwapHands.Value) ? leftHandTransform : rightHandTransform;
        Transform             val2      = flag ? rightHandTransform : leftHandTransform;
        ControllerInputPoller instance2 = ControllerInputPoller.instance;
        if (leftHandTransform == null || rightHandTransform == null || instance2 == null)
        {
            return;
        }

        bool flag2 = flag ? instance2.rightGrab : instance2.leftGrab;
        bool flag3 = (flag ? instance2.rightControllerIndexFloat : instance2.leftControllerIndexFloat) > 0.55f;
        switch (state)
        {
            case DiscState.Hidden:
                if (Cfg.Palm.Value && !RingMenu.IsOpen && !PcMenu.IsOpen && !PlayerRay.IsAiming && Glancing(transform, val, flag))
                {
                    state = DiscState.Palm;
                    float num3 = 0f;
                    palmHoldTime           = 0f;
                    palmVisibility         = num3;
                    palmVelocity           = Vector3.zero;
                    discTransform.position = val.position + PalmNormal(val, flag) * 0.02f;
                    discObject.SetActive(true);
                    trail.emitting = false;
                }

                break;

            case DiscState.Palm:
            {
                bool flag4;
                if (flag4 = Glancing(transform, val, flag))
                {
                    lastGlanceAt = Time.time;
                }

                if (!RingMenu.IsOpen && !PcMenu.IsOpen && !PlayerRay.IsAiming && (flag4 || Time.time - lastGlanceAt <= 0.35f))
                {
                    palmHoldTime   += Time.deltaTime;
                    palmVisibility =  Mathf.MoveTowards(palmVisibility, flag4 ? 1f : 0.4f, Time.deltaTime * 5f);
                    Vector3 val4 = val.position + PalmNormal(val, flag)                                   * 0.02f;
                    if (!Theme.Bouncy)
                    {
                        discTransform.position = Vector3.Lerp(discTransform.position, val4, 1f - Mathf.Exp((0f - Time.deltaTime) * 26f));
                    }
                    else
                    {
                        Vector3 val5 = val4 - discTransform.position;
                        palmVelocity += val5 * 220f * Time.deltaTime;
                        palmVelocity *= Mathf.Exp(-7f * Time.deltaTime);
                        Transform obj = discTransform;
                        obj.position += palmVelocity * Time.deltaTime;
                        if (!(val5.magnitude <= 0.28f))
                        {
                            HideDisc();

                            break;
                        }
                    }

                    discTransform.rotation   = GetPalmRotation(val, flag) * Quaternion.Euler(0f, 0f, usesRectangleStyle ? 0f : Time.time * 30f);
                    discTransform.localScale = Vector3.one                * (Theme.Snap(palmHoldTime / 0.3f) * (0.94f + 0.06f            * Mathf.Sin(Time.time * 2.2f)));
                    SetOpacity(palmVisibility);
                    if (Cfg.OneHand.Value)
                    {
                        if ((flag ? instance2.leftControllerIndexFloat : instance2.rightControllerIndexFloat) > 0.6f && palmHoldTime > 0.25f)
                        {
                            Theme.Haptic(flag, 0.55f, 0.06f);
                            HideDisc();
                            RingMenu.Ins.Open(GameSettings.GroundPointAhead(transform, 1.3f), null);
                        }
                    }
                    else
                    {
                        if (!flag2 || !flag3 || Vector3.Distance(val2.position, discTransform.position) >= 0.16f)
                        {
                            break;
                        }

                        if (!Cfg.HandMenu.Value)
                        {
                            state        = DiscState.Held;
                            grabProgress = 0f;
                            for (int i = 0; i < positionSamples.Length; i++)
                            {
                                positionSamples[i] = val2.position;
                                sampleTimes[i]     = Time.time;
                            }

                            Theme.Haptic(!flag, 0.6f, 0.06f);
                            Theme.Haptic(flag,  0.3f, 0.04f);
                        }
                        else
                        {
                            Theme.Haptic(flag, 0.5f, 0.06f);
                            HideDisc();
                            RingMenu.Ins.OpenOnHand(val, flag);
                        }
                    }
                }
                else
                {
                    HideDisc();
                }

                break;
            }

            case DiscState.Held:
                grabProgress             += Time.deltaTime * 5f;
                discTransform.position   =  val2.position + PalmNormal(val2, !flag) * 0.025f;
                discTransform.rotation   =  GetPalmRotation(val2, !flag) * Quaternion.Euler(0f, 0f, usesRectangleStyle ? 0f : Time.time * 25f);
                discTransform.localScale =  Vector3.one                  * Mathf.LerpUnclamped(1f, 2.1f, Theme.Snap(grabProgress));
                SetOpacity(1f);
                positionSamples[sampleIndex] = val2.position;
                sampleTimes[sampleIndex]     = Time.time;
                sampleIndex                  = (sampleIndex + 1) % positionSamples.Length;
                if (!(flag2 && flag3))
                {
                    Vector3 val6 = EstimateThrowVelocity();
                    if (val6.magnitude <= 1.2f)
                    {
                        state        = DiscState.Palm;
                        lastGlanceAt = Time.time;
                        palmHoldTime = 1f;

                        break;
                    }

                    state          = DiscState.Flying;
                    flightProgress = 0f;
                    flightStart    = discTransform.position;
                    Vector3 forward = transform.forward;
                    forward.y = 0f;
                    forward.Normalize();
                    flightTarget  = GameSettings.GroundPointAhead(transform, 1.35f);
                    flightControl = (flightStart + flightTarget) / 2f + Vector3.up * 0.3f + forward * 0.15f;
                    trail.Clear();
                    trail.emitting = true;
                    Theme.Haptic(!flag, 0.4f, 0.05f);
                }

                break;

            case DiscState.Flying:
            {
                flightProgress += Time.deltaTime / 0.5f;
                float num  = Mathf.Clamp01(flightProgress);
                float num2 = Theme.Ease(num);
                discTransform.position = Vector3.Lerp(Vector3.Lerp(flightStart, flightControl, num2), Vector3.Lerp(flightControl, flightTarget, num2), num2);
                Transform obj2 = discTransform;
                obj2.rotation *= Quaternion.Euler(0f, 0f, (1f - num * 0.6f) * 900f * Time.deltaTime);
                if (!(num < 1f))
                {
                    state               = DiscState.Deploying;
                    deployProgress      = 0f;
                    deployStartRotation = discTransform.rotation;
                    trail.emitting      = false;
                }

                break;
            }

            case DiscState.Deploying:
            {
                deployProgress += Time.deltaTime;
                Vector3 val3 = flightTarget - transform.position;
                val3.y                 = 0f;
                discTransform.rotation = Quaternion.Slerp(deployStartRotation, Quaternion.LookRotation(val3.sqrMagnitude > 0.001f ? val3 : Vector3.forward), Theme.Snap(deployProgress / 0.3f));
                if (!(deployProgress < 0.32f))
                {
                    HideDisc();
                    RingMenu.Ins.Open(flightTarget, null);
                }

                break;
            }
        }
    }

    private void OnDestroy()
    {
        if (discObject != null)
        {
            Destroy(discObject);
        }
    }

    public void Retheme()
    {
        if (!(discObject == null))
        {
            Destroy(discObject);
            state = DiscState.Hidden;
            Start();
        }
    }

    public static Vector3 PalmNormal(Transform hand, bool left)
    {
        if (!left)
        {
            return -hand.right;
        }

        return hand.right;
    }

    private static Quaternion GetPalmRotation(Transform arg1, bool arg2) => Quaternion.LookRotation(PalmNormal(arg1, arg2), arg1.rotation * Quaternion.Euler(45f, 0f, 0f) * Vector3.forward);

    private void HideDisc()
    {
        state = DiscState.Hidden;
        if (discObject != null)
        {
            discObject.SetActive(false);
        }
    }

    private Vector3 EstimateThrowVelocity()
    {
        int   num  = (sampleIndex + positionSamples.Length - 1) % positionSamples.Length;
        float num2 = sampleTimes[num] - sampleTimes[sampleIndex];
        if (!(num2 < 0.01f))
        {
            return (positionSamples[num] - positionSamples[sampleIndex]) / num2;
        }

        return Vector3.zero;
    }

    public static bool Glancing(Transform head, Transform hand, bool left)
    {
        Vector3 val = hand.position - head.position;
        if (!(val.magnitude > 0.85f) && Vector3.Angle(head.forward, val) <= 32f)
        {
            return Mathf.Abs(Vector3.Dot(PalmNormal(hand, left), -val.normalized)) > 0.15f;
        }

        return false;
    }

    private void SetOpacity(float arg1)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer obj   = renderers[i];
            float    num   = rendererBaseAlpha[i];
            Color    color = obj.sharedMaterial.color;
            obj.sharedMaterial.color = new Color(color.r, color.g, color.b, num * arg1);
        }
    }

    private enum DiscState
    {
        Hidden,
        Palm,
        Held,
        Flying,
        Deploying,
    }
}