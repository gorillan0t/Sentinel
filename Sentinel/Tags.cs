using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Photon.Pun;
using TMPro;
using UnityEngine;

namespace Sentinel;

public class Tags : MonoBehaviour
{

    private readonly HashSet<VRRig> activeRigs = new();

    private readonly List<VRRig> staleRigs = new();

    private readonly Dictionary<VRRig, TagVisual> tagsByRig = new();

    private float nextRefreshAt;

    private void LateUpdate()
    {
        if (Cfg.Tags.Value && PhotonNetwork.InRoom && VRRigCache.isInitialized)
        {
            if (GorillaTagger.Instance == null || GorillaTagger.Instance.mainCamera == null)
            {
                return;
            }

            bool flag;
            if (flag = Time.time >= nextRefreshAt)
            {
                nextRefreshAt = Time.time + 0.25f;
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
                foreach (VRRig item2 in staleRigs)
                {
                    Destroy(tagsByRig[item2].root);
                    tagsByRig.Remove(item2);
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    private TagVisual CreateTag()
    {
        GameObject val = new("zx_tag");
        DontDestroyOnLoad(val);
        TagVisual obj = new()
        {
                root     = val,
                nameText = Theme.Label(val.transform, "name", 0.085f, Theme.White),
        };

        obj.nameText.richText                = false;
        obj.fpsText                          = Theme.Label(val.transform, "fps",  0.058f, Theme.White);
        obj.tierText                         = Theme.Label(val.transform, "tier", 0.044f, Theme.Soft);
        obj.tierText.transform.localPosition = new Vector3(0f, -0.19f, 0f);
        obj.platformRenderer                 = Theme.Quad(val.transform, "plat", 0.075f, 0.075f, Theme.Holo(Color.white, 3000, Theme.QuestionIcon, 4)).GetComponent<Renderer>();
        obj.sentinelRenderer                 = Theme.Quad(val.transform, "zx",   0.075f, 0.075f, Theme.Holo(Color.white, 3000, Theme.MenuIcon,     4)).GetComponent<Renderer>();

        return obj;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void UpdateTag(VRRig arg1, TagVisual arg2)
    {
        NetPlayer creator = arg1.Creator;
        arg2.nameText.text = creator != null ? creator.NickName : "?";
        bool flag;
        int  num = (flag = Manifest.Has("custom_tags")) ? Mathf.Clamp(Cfg.TagFont.Value, 0, 3) : 0;
        if (arg2.cachedFont != num)
        {
            arg2.cachedFont = num;
            Theme.SetTagFont(arg2.nameText, num);
            Theme.SetTagFont(arg2.fpsText,  num);
            Theme.SetTagFont(arg2.tierText, num);
            arg2.cachedFps = int.MinValue;
        }

        int num2 = !flag ? 1 : Cfg.TagSize.Value;
        arg2.root.transform.localScale = Vector3.one * num2 switch
                                                       {
                                                               2     => 1.25f,
                                                               0     => 0.8f,
                                                               var _ => 1f,
                                                       };

        arg2.nameText.fontStyle = (FontStyles)(flag && Cfg.TagBold.Value ? 1 : 0);
        arg2.nameText.color     = flag && Cfg.TagColor.Value == 1 ? Theme.Main : !flag || Cfg.TagColor.Value != 2 ? Detect.RigColor(arg1) : Theme.White;
        bool value = Cfg.TagFps.Value;
        arg2.fpsText.gameObject.SetActive(value);
        float num3 = 0f;
        if (value)
        {
            if (arg1.fps != arg2.cachedFps)
            {
                arg2.cachedFps     = arg1.fps;
                arg2.fpsText.text  = arg1.fps + " FPS";
                arg2.fpsText.color = Detect.FpsColor(arg1.fps);
                arg2.fpsText.ForceMeshUpdate();
                arg2.fpsTextWidth = arg2.fpsText.GetRenderedValues(false).x;
            }

            num3 = arg2.fpsTextWidth;
        }

        bool value2 = Cfg.TagPlat.Value;
        arg2.platformRenderer.gameObject.SetActive(value2);
        if (value2)
        {
            string text = Detect.PlatformOf(arg1);
            if (text != arg2.platformName)
            {
                arg2.platformName                                = text;
                arg2.platformRenderer.sharedMaterial.mainTexture = Detect.PlatformIcon(text);
            }
        }

        bool flag2 = Cfg.TagMenu.Value && Net.HasRig(arg1);
        arg2.tierText.gameObject.SetActive(false);
        arg2.sentinelRenderer.gameObject.SetActive(flag2);
        if (flag2)
        {
            Color white = Color.white;
            white.a                                    = Net.MenuOpenRig(arg1) ? 1f : 0.45f;
            arg2.sentinelRenderer.sharedMaterial.color = white;
        }

        int num4 = (value2 ? 1 : 0) + (value ? 1 : 0) + (flag2 ? 1 : 0);
        if (num4 != 0)
        {
            float num5 = (0f - ((value2 ? 0.075f : 0f) + num3 + (flag2 ? 0.075f : 0f) + 0.034f * (num4 - 1))) / 2f;
            if (value2)
            {
                arg2.platformRenderer.transform.localPosition =  new Vector3(num5 + 0.0375f, -0.1f, 0f);
                num5                                          += 0.109000005f;
            }

            if (value)
            {
                arg2.fpsText.transform.localPosition =  new Vector3(num5 + num3 / 2f, -0.1f, 0f);
                num5                                 += num3 + 0.034f;
            }

            if (flag2)
            {
                arg2.sentinelRenderer.transform.localPosition = new Vector3(num5 + 0.0375f, -0.1f, 0f);
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

        public int cachedFont = -1;

        public int cachedFps = int.MinValue;

        public TMP_Text fpsText;

        public float fpsTextWidth;

        public TMP_Text nameText;

        public string platformName;

        public Renderer   platformRenderer;
        public GameObject root;

        public Renderer sentinelRenderer;

        public TMP_Text tierText;
    }
}