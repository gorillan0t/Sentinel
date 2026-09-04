using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;

namespace Sentinel.Sentinel;

public class Tags : MonoBehaviour
{

    private const float float_0 = -0.1f;

    private const float float_1 = 0.075f;

    private const float float_2 = 0.034f;

    private readonly HashSet<VRRig> activeRigs = new();

    private readonly List<VRRig> staleRigs = new();

    private readonly Dictionary<VRRig, TagVisual> tagsByRig = new();

    private float nextTextRefresh;

    private void LateUpdate()
    {
        if (Cfg.Tags.Value && PhotonNetwork.InRoom && VRRigCache.isInitialized)
        {
            if (GorillaTagger.Instance == null || GorillaTagger.Instance.mainCamera == null)
            {
                return;
            }

            bool flag;
            if (flag = Time.time >= nextTextRefresh)
            {
                nextTextRefresh = Time.time + 0.25f;
            }

            Vector3 position = GorillaTagger.Instance.mainCamera.transform.position;
            activeRigs.Clear();
            foreach (RigContainer activeRigContainer in VRRigCache.ActiveRigContainers)
            {
                VRRig rig = activeRigContainer.Rig;
                if (!(rig == null) && !rig.isLocal)
                {
                    activeRigs.Add(rig);
                    if (!tagsByRig.TryGetValue(rig, out TagVisual value))
                    {
                        value = tagsByRig[rig] = CreateTag();
                    }

                    Vector3 val = rig.headMesh != null ? rig.headMesh.transform.position : rig.transform.position;
                    value.root.transform.position = val + Vector3.up * 0.48f * rig.scaleFactor;
                    value.root.transform.rotation = Quaternion.LookRotation(value.root.transform.position - position);
                    if (flag)
                    {
                        UpdateTag(rig, value);
                    }
                }
            }

            staleRigs.Clear();
            foreach (KeyValuePair<VRRig, TagVisual> item in tagsByRig)
            {
                if (!activeRigs.Contains(item.Key))
                {
                    staleRigs.Add(item.Key);
                }
            }

            {
                foreach (VRRig staleRig in staleRigs)
                {
                    Destroy(tagsByRig[staleRig].root);
                    tagsByRig.Remove(staleRig);
                }

                return;
            }
        }

        if (tagsByRig.Count > 0)
        {
            ClearTags();
        }
    }

    private void OnDestroy() => ClearTags();

    private TagVisual CreateTag()
    {
        GameObject val = new("zx_tag");
        DontDestroyOnLoad(val);

        return new TagVisual
        {
                root         = val,
                nameText     = Theme.Label(val.transform, "name", 0.085f, Theme.White),
                fpsText      = Theme.Label(val.transform, "fps",  0.058f, Theme.White),
                platformIcon = Theme.Quad(val.transform, "plat", 0.075f, 0.075f, Theme.Holo(Color.white, 3000, Theme.QuestionIcon, 4)).GetComponent<Renderer>(),
                sentinelIcon = Theme.Quad(val.transform, "zx",   0.075f, 0.075f, Theme.Holo(Color.white, 3000, Theme.MenuIcon,     4)).GetComponent<Renderer>(),
        };
    }

    private void UpdateTag(VRRig vrrig_0, TagVisual class31_0)
    {
        NetPlayer creator = vrrig_0.Creator;
        class31_0.nameText.text  = creator != null ? creator.NickName : "?";
        class31_0.nameText.color = Detect.RigColor(vrrig_0);
        bool value = Cfg.TagFps.Value;
        class31_0.fpsText.gameObject.SetActive(value);
        float num = 0f;
        if (value)
        {
            class31_0.fpsText.text  = vrrig_0.fps + " FPS";
            class31_0.fpsText.color = Detect.FpsColor(vrrig_0.fps);
            class31_0.fpsText.ForceMeshUpdate();
            num = class31_0.fpsText.GetRenderedValues(false).x;
        }

        bool value2 = Cfg.TagPlat.Value;
        class31_0.platformIcon.gameObject.SetActive(value2);
        if (value2)
        {
            string text = Detect.PlatformOf(vrrig_0);
            if (text != class31_0.platform)
            {
                class31_0.platform                          = text;
                class31_0.platformIcon.material.mainTexture = Detect.PlatformIcon(text);
            }
        }

        bool flag = Cfg.TagMenu.Value && Net.HasRig(vrrig_0);
        class31_0.sentinelIcon.gameObject.SetActive(flag);
        if (flag)
        {
            Color white = Color.white;
            white.a                               = Net.MenuOpenRig(vrrig_0) ? 1f : 0.45f;
            class31_0.sentinelIcon.material.color = white;
        }

        int num2 = (value2 ? 1 : 0) + (value ? 1 : 0) + (flag ? 1 : 0);
        if (num2 != 0)
        {
            float num3 = (0f - ((value2 ? 0.075f : 0f) + num + (flag ? 0.075f : 0f) + 0.034f * (num2 - 1))) / 2f;
            if (value2)
            {
                class31_0.platformIcon.transform.localPosition =  new Vector3(num3 + 0.0375f, -0.1f, 0f);
                num3                                           += 0.109000005f;
            }

            if (value)
            {
                class31_0.fpsText.transform.localPosition =  new Vector3(num3 + num / 2f, -0.1f, 0f);
                num3                                      += num + 0.034f;
            }

            if (flag)
            {
                class31_0.sentinelIcon.transform.localPosition = new Vector3(num3 + 0.0375f, -0.1f, 0f);
            }
        }
    }

    private void ClearTags()
    {
        foreach (TagVisual value in tagsByRig.Values)
        {
            if (value.root != null)
            {
                Destroy(value.root);
            }
        }

        tagsByRig.Clear();
    }

    private class TagVisual
    {

        public TMP_Text fpsText;

        public TMP_Text nameText;

        public string platform;

        public Renderer   platformIcon;
        public GameObject root;

        public Renderer sentinelIcon;
    }
}