using System.Collections.Generic;
using UnityEngine;

namespace Sentinel.Sentinel;

public static class Outline
{
    private static readonly Dictionary<VRRig, GameObject> outlinesByRig = new();

    public static void Set(VRRig rig, bool on)
    {
        if (rig == null)
        {
            return;
        }

        GameObject value;
        if (on)
        {
            if (!outlinesByRig.ContainsKey(rig) && !(rig.mainSkin == null))
            {
                GameObject val = new("zx_glow");
                val.transform.SetParent(rig.mainSkin.transform.parent, false);
                SkinnedMeshRenderer obj = val.AddComponent<SkinnedMeshRenderer>();
                obj.sharedMesh     = rig.mainSkin.sharedMesh;
                obj.bones          = rig.mainSkin.bones;
                obj.rootBone       = rig.mainSkin.rootBone;
                obj.material       = Theme.Rig(Theme.Main * 0.5f);
                outlinesByRig[rig] = val;
            }
        }
        else if (outlinesByRig.TryGetValue(rig, out value))
        {
            if (value != null)
            {
                Object.Destroy(value);
            }

            outlinesByRig.Remove(rig);
        }
    }

    public static void Clear()
    {
        foreach (GameObject value in outlinesByRig.Values)
        {
            if (value != null)
            {
                Object.Destroy(value);
            }
        }

        outlinesByRig.Clear();
    }
}