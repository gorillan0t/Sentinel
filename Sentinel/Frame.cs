using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace Sentinel;

public class Frame : MonoBehaviour
{

    private float cooldownUntil;
    private float gestureHoldTime;

    private Vector3 lastLeftHandPosition;

    private Vector3 lastRightHandPosition;

    private void Update()
    {
        GorillaTagger instance = GorillaTagger.Instance;
        if (instance == null || instance.mainCamera == null || instance.leftHandTransform == null)
        {
            return;
        }

        Transform transform          = instance.mainCamera.transform;
        Transform leftHandTransform  = instance.leftHandTransform;
        Transform rightHandTransform = instance.rightHandTransform;
        if (Cfg.Gesture.Value && !RingMenu.IsOpen && !PcMenu.IsOpen && !PlayerRay.IsAiming && !(Time.time < cooldownUntil) && PhotonNetwork.InRoom)
        {
            if (IsFrameGesture(transform, leftHandTransform, rightHandTransform))
            {
                gestureHoldTime += Time.deltaTime;
                if (gestureHoldTime > 0.12f && gestureHoldTime - Time.deltaTime <= 0.12f)
                {
                    Theme.Haptic(true, 0.1f, 0.02f);
                }

                if (gestureHoldTime >= 0.45f)
                {
                    gestureHoldTime = 0f;
                    cooldownUntil   = Time.time + 3f;
                    SelectFramedPlayer(transform, leftHandTransform, rightHandTransform);
                }
            }
            else
            {
                gestureHoldTime = 0f;
            }

            lastLeftHandPosition  = leftHandTransform.position;
            lastRightHandPosition = rightHandTransform.position;
        }
        else
        {
            gestureHoldTime = 0f;
        }
    }

    private bool IsFrameGesture(Transform arg1, Transform arg2, Transform arg3)
    {
        Vector3 position  = arg2.position;
        Vector3 position2 = arg3.position;
        float   num       = Vector3.Distance(position, position2);
        if (num < 0.06f || num > 0.6f)
        {
            return false;
        }

        Vector3 val  = position  - arg1.position;
        Vector3 val2 = position2 - arg1.position;
        if (!(val.magnitude < 0.12f) && !(val2.magnitude < 0.12f) && Mathf.Abs(val.magnitude - val2.magnitude) <= 0.25f)
        {
            if (Vector3.Dot(arg1.forward, val.normalized) < 0.45f || Vector3.Dot(arg1.forward, val2.normalized) < 0.45f)
            {
                return false;
            }

            float   num2 = Mathf.Max(Time.deltaTime, 0.001f);
            Vector3 val3 = position - lastLeftHandPosition;
            if (val3.magnitude / num2 >= 0.6f)
            {
                return false;
            }

            val3 = position2 - lastRightHandPosition;

            return val3.magnitude / num2 < 0.6f;
        }

        return false;
    }

    private void SelectFramedPlayer(Transform arg1, Transform arg2, Transform arg3)
    {
        Vector3      val        = (arg2.position + arg3.position) / 2f - arg1.position;
        Vector3      normalized = val.normalized;
        float        num        = Vector3.Distance(arg2.position, arg3.position);
        float        num2       = float.MaxValue;
        VRRig        val2       = null;
        RaycastHit[] array      = Physics.SphereCastAll(arg1.position, Mathf.Max(num * 0.4f, 0.12f), normalized, 40f);
        for (int i = 0; i < array.Length; i++)
        {
            RaycastHit val3              = array[i];
            VRRig      componentInParent = val3.collider.GetComponentInParent<VRRig>();
            if (!(componentInParent == null) && !componentInParent.isLocal && !(val3.distance >= num2))
            {
                num2 = val3.distance;
                val2 = componentInParent;
            }
        }

        if (!(val2 == null))
        {
            Player val4 = Detect.PlayerOf(val2);
            if (val4 != null)
            {
                Theme.Haptic(true,  0.5f, 0.06f);
                Theme.Haptic(false, 0.5f, 0.06f);
                RingMenu.Ins.Open(GameSettings.GroundPointAhead(arg1, 1.2f), val4);
            }
        }
    }
}