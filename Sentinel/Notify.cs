using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Sentinel.Sentinel;

public class Notify : MonoBehaviour
{

    private static Notify instance;

    private readonly List<NotificationCard> notifications = new();

    private Transform hudRoot;

    private void Awake() => instance = this;

    private void LateUpdate()
    {
        if (hudRoot == null || GorillaTagger.Instance == null)
        {
            return;
        }

        GameObject mainCamera = GorillaTagger.Instance.mainCamera;
        if (mainCamera == null)
        {
            return;
        }

        Transform transform = mainCamera.transform;
        hudRoot.position = transform.position + transform.forward * 1.35f + transform.up * -0.42f + transform.right * -0.28f;
        hudRoot.rotation = transform.rotation;
        bool flag = false;
        for (int num = notifications.Count - 1; num >= 0; num--)
        {
            if (!(notifications[num].root != null) || !(Time.time <= notifications[num].expiresAt + 0.35f))
            {
                if (notifications[num].root != null)
                {
                    Destroy(notifications[num].root);
                }

                notifications.RemoveAt(num);
                flag = true;
            }
        }

        if (flag)
        {
            ReflowNotifications();
        }

        float num2 = 1f - Mathf.Exp((0f - Time.deltaTime) * 13f);
        foreach (NotificationCard notification in notifications)
        {
            float   num3          = (Time.time - notification.createdAt) / 0.4f;
            float   num4          = Mathf.Clamp01((Time.time - notification.expiresAt) / 0.35f);
            Vector3 localPosition = notification.root.transform.localPosition;
            localPosition.x                           = num4 > 0f ? Mathf.Lerp(0f, (0f - notification.width) * 1.4f, num4 * num4) : Mathf.LerpUnclamped((0f - notification.width) * 1.2f, 0f, Theme.Snap(num3));
            localPosition.y                           = Mathf.Lerp(localPosition.y, notification.targetY, num2);
            notification.root.transform.localPosition = localPosition;
            float num5 = Mathf.Min(Mathf.Clamp01(num3 * 3f), 1f - num4);
            SetRendererAlpha(notification.fillRenderer,   Theme.Fill.a * num5);
            SetRendererAlpha(notification.borderRenderer, 0.3f         * num5);
            SetRendererAlpha(notification.accentRenderer, num5);
            notification.messageText.alpha = num5;
        }
    }

    public static void Send(string msg, Color accent)
    {
        if (instance != null)
        {
            instance.AddNotification(msg, accent);
        }
    }

    private void AddNotification(string string_0, Color color_0)
    {
        if (hudRoot == null)
        {
            if (GorillaTagger.Instance == null || GorillaTagger.Instance.mainCamera == null)
            {
                return;
            }

            GameObject val = new("zx_hud");
            DontDestroyOnLoad(val);
            hudRoot = val.transform;
        }

        GameObject val2 = new("card");
        val2.transform.SetParent(hudRoot, false);
        NotificationCard notificationCard = new();
        notificationCard.root                                = val2;
        notificationCard.createdAt                           = Time.time;
        notificationCard.expiresAt                           = Time.time + 4.5f;
        notificationCard.messageText                         = Theme.Text(val2.transform, "msg", string_0, 0.028f, Theme.White, true);
        notificationCard.messageText.transform.localPosition = new Vector3(0.058f, 0f, -0.001f);
        notificationCard.messageText.ForceMeshUpdate();
        notificationCard.width = 0.096f + notificationCard.messageText.GetRenderedValues(false).x;
        Theme.Panel panel = Theme.Card(val2.transform, "panel", notificationCard.width, 0.062f, new Color(color_0.r, color_0.g, color_0.b, 0.3f), 2990);
        panel.Go.transform.localPosition = new Vector3(notificationCard.width / 2f - 0.028f, 0f, 0.001f);
        notificationCard.fillRenderer    = panel.FillR;
        notificationCard.borderRenderer  = panel.BorderR;
        GameObject val3 = Theme.Quad(val2.transform, "diamond", 0.034f, 0.034f, Theme.Holo(color_0, 2992));
        val3.transform.localPosition    = new Vector3(0.011f, 0f, 0f);
        val3.transform.localRotation    = Quaternion.Euler(0f, 0f, 45f);
        notificationCard.accentRenderer = val3.GetComponent<Renderer>();
        notifications.Add(notificationCard);
        while (notifications.Count > 5)
        {
            Destroy(notifications[0].root);
            notifications.RemoveAt(0);
        }

        ReflowNotifications();
        val2.transform.localPosition = new Vector3(0f - notificationCard.width, notificationCard.targetY, 0f);
    }

    private void ReflowNotifications()
    {
        int num  = notifications.Count - 1;
        int num2 = 0;
        while (num >= 0)
        {
            notifications[num].targetY = num2 * 0.076f;
            num--;
            num2++;
        }
    }

    private static void SetRendererAlpha(Renderer renderer_0, float float_0)
    {
        Color color = renderer_0.material.color;
        color.a                   = float_0;
        renderer_0.material.color = color;
    }

    private class NotificationCard
    {

        public Renderer accentRenderer;

        public Renderer borderRenderer;

        public float createdAt;

        public float expiresAt;

        public Renderer fillRenderer;

        public TMP_Text   messageText;
        public GameObject root;

        public float targetY;

        public float width;
    }
}