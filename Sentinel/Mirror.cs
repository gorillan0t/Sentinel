using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Sentinel.Sentinel;

public class Mirror : MonoBehaviour
{

    private const float float_0 = 0.001f;
    private static readonly Type[] componentAllowList =
    [
            typeof(Transform),
            typeof(MeshFilter),
            typeof(MeshRenderer),
            typeof(SkinnedMeshRenderer),
            typeof(ParticleSystem),
            typeof(ParticleSystemRenderer),
            typeof(TrailRenderer),
            typeof(LineRenderer),
    ];

    private static readonly Dictionary<Type, Type[]> copyableTypesCache = new();

    private static int mirrorLayer = -1;

    private static int instanceCounter;

    private bool built;

    private Transform[] cloneBones;

    private int cloneLayer = -1;

    private GameObject cloneRoot;

    private int failedSyncs;

    private float nextSyncAt;

    private bool quiet;

    private float scale;

    private Transform[] sourceBones;

    private int sourceLayer = -1;

    private VRRig sourceRig;

    private void LateUpdate()
    {
        if (built)
        {
            if (sourceRig == null || !CanCreateMirror())
            {
                return;
            }

            built = false;
            BuildClone();
        }

        if (sourceRig == null)
        {
            if (cloneRoot != null)
            {
                Destroy(cloneRoot);
            }

            cloneRoot = null;
        }
        else
        {
            if (sourceBones == null)
            {
                return;
            }

            if (Time.time > nextSyncAt)
            {
                nextSyncAt = Time.time + 4f + Random.value;
                if (sourceRig.GetComponentsInChildren<Transform>(true).Length != sourceBones.Length)
                {
                    BuildClone();

                    return;
                }
            }

            SynchronizePose();
            if (quiet && sourceLayer >= 0 && cloneRoot != null)
            {
                Transform transform = cloneRoot.transform;
                transform.position -= cloneBones[sourceLayer].position - this.transform.position;
            }
        }
    }

    private void OnDestroy()
    {
        if (cloneRoot != null)
        {
            Destroy(cloneRoot);
        }
    }

    public static Mirror Spawn(Transform parent, VRRig source, float scale) => CreateMirror(parent, source, scale, false);

    public static Mirror Head(Transform parent, VRRig source, float scale) => CreateMirror(parent, source, scale, true);

    private static Mirror CreateMirror(Transform transform_2, VRRig vrrig_1, float float_3, bool bool_2)
    {
        GameObject val = new("zx_mirror");
        val.transform.SetParent(transform_2, false);
        val.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        Mirror mirror = val.AddComponent<Mirror>();
        mirror.sourceRig         = vrrig_1;
        mirror.scale             = float_3;
        mirror.quiet             = bool_2;
        val.transform.localScale = Vector3.one * float_3;
        if (bool_2)
        {
            mirror.built = true;
        }
        else
        {
            mirror.BuildClone();
        }

        return mirror;
    }

    private static bool CanCreateMirror()
    {
        if (Time.frameCount != mirrorLayer)
        {
            mirrorLayer     = Time.frameCount;
            instanceCounter = 2;
        }

        if (instanceCounter > 0)
        {
            instanceCounter--;

            return true;
        }

        return false;
    }

    private void BuildClone()
    {
        if (cloneRoot != null)
        {
            Destroy(cloneRoot);
        }

        cloneRoot   = null;
        sourceBones = cloneBones = null;
        if (sourceRig == null)
        {
            return;
        }

        gameObject.SetActive(false);
        cloneRoot      = Instantiate(sourceRig.gameObject, transform, false);
        cloneRoot.name = "rig";
        ConfigureCloneObject(cloneRoot);
        Collider[] componentsInChildren = cloneRoot.GetComponentsInChildren<Collider>(true);
        foreach (Collider val in componentsInChildren)
        {
            if (val != null)
            {
                DestroyImmediate(val);
            }
        }

        cloneRoot.transform.localPosition = Vector3.zero;
        cloneRoot.transform.localRotation = Quaternion.identity;
        transform.localScale              = Vector3.one * scale;
        sourceBones                       = sourceRig.GetComponentsInChildren<Transform>(true);
        cloneBones                        = cloneRoot.GetComponentsInChildren<Transform>(true);
        if (sourceBones.Length != cloneBones.Length)
        {
            sourceBones = cloneBones = null;
            gameObject.SetActive(true);

            return;
        }

        int i = -1;
        cloneLayer  = -1;
        sourceLayer = -1;
        failedSyncs = 0;
        for (int j = 0; j < sourceBones.Length; j++)
        {
            if (cloneLayer >= 0)
            {
                break;
            }

            if (!(sourceBones[j] == null) && !(sourceBones[j].name != "body"))
            {
                Transform val2 = sourceBones[j].Find("head");
                if (!(val2 == null))
                {
                    cloneLayer  = j;
                    sourceLayer = Array.IndexOf(sourceBones, val2);
                }
            }
        }

        if (sourceLayer < 0)
        {
            for (int k = 0; k < sourceBones.Length; k++)
            {
                if (sourceBones[k] != null && sourceBones[k].name == "head")
                {
                    sourceLayer = k;

                    break;
                }
            }
        }

        Transform[] array = cloneBones;
        for (i = 0; i < array.Length; i++)
        {
            array[i].gameObject.layer = 0;
        }

        Renderer[] componentsInChildren2 = cloneRoot.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer obj in componentsInChildren2)
        {
            Quiet(obj);
            SkinnedMeshRenderer val3 = (SkinnedMeshRenderer)(obj is SkinnedMeshRenderer ? obj : null);
            if (val3 != null && quiet)
            {
                val3.updateWhenOffscreen = true;
            }
        }

        SynchronizePose();
        if (quiet)
        {
            BuildRenderers();
        }

        gameObject.SetActive(true);
    }

    private void BuildRenderers()
    {
        if (sourceLayer >= 0 && cloneLayer >= 0)
        {
            Transform  val                  = cloneBones[sourceLayer];
            Transform  val2                 = cloneBones[cloneLayer];
            Transform  transform            = cloneRoot.transform;
            Vector3    val3                 = transform.InverseTransformPoint(val.position);
            Renderer[] componentsInChildren = cloneRoot.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer val4 in componentsInChildren)
            {
                if (!(val4 is SkinnedMeshRenderer))
                {
                    Vector3 val5 = transform.InverseTransformPoint(val4.transform.position) - val3;
                    if (val5.magnitude > 0.34f)
                    {
                        val4.enabled = false;
                    }
                }
            }

            Vector3 val6 = val.localScale;
            if (val6.sqrMagnitude < 0.01f)
            {
                val6 = Vector3.one;
            }

            val2.localRotation = Quaternion.identity;
            val.localRotation  = Quaternion.identity;
            val2.localScale    = Vector3.one * 0.001f;
            val.localScale     = val6        / 0.001f;
            failedSyncs        = val.GetComponentsInChildren<Transform>(true).Length;
        }
        else
        {
            quiet = false;
        }
    }

    private static Type[] GetCopyableComponentTypes(Type type_1)
    {
        if (copyableTypesCache.TryGetValue(type_1, out Type[] value))
        {
            return value;
        }

        List<Type> list             = new();
        object[]   customAttributes = type_1.GetCustomAttributes(typeof(RequireComponent), true);
        for (int i = 0; i < customAttributes.Length; i++)
        {
            RequireComponent val = (RequireComponent)customAttributes[i];
            if (val.m_Type0 != null)
            {
                list.Add(val.m_Type0);
            }

            if (val.m_Type1 != null)
            {
                list.Add(val.m_Type1);
            }

            if (val.m_Type2 != null)
            {
                list.Add(val.m_Type2);
            }
        }

        return copyableTypesCache[type_1] = list.ToArray();
    }

    private static bool ShouldCopyComponent(Component component_0)
    {
        Type[] array = componentAllowList;
        for (int i = 0; i < array.Length; i++)
        {
            if (array[i].IsAssignableFrom(component_0.GetType()))
            {
                return true;
            }
        }

        return false;
    }

    private static void ConfigureCloneObject(GameObject gameObject_1)
    {
        for (int i = 0; i < 12; i++)
        {
            int         num                  = 0;
            int         num2                 = 0;
            Component[] componentsInChildren = gameObject_1.GetComponentsInChildren<Component>(true);
            foreach (Component val in componentsInChildren)
            {
                if (val == null || ShouldCopyComponent(val))
                {
                    continue;
                }

                num2++;
                Type        type       = val.GetType();
                bool        flag       = false;
                Component[] components = val.gameObject.GetComponents<Component>();
                foreach (Component val2 in components)
                {
                    if (val2 == null || val2 == val)
                    {
                        continue;
                    }

                    Type[] copyableComponentTypes = GetCopyableComponentTypes(val2.GetType());
                    for (int l = 0; l < copyableComponentTypes.Length; l++)
                    {
                        if (copyableComponentTypes[l].IsAssignableFrom(type))
                        {
                            flag = true;

                            break;
                        }
                    }

                    if (flag)
                    {
                        break;
                    }
                }

                if (!flag)
                {
                    DestroyImmediate(val);
                    num++;
                }
            }

            if (num2 == num || num == 0)
            {
                break;
            }
        }
    }

    private void SynchronizePose()
    {
        cloneBones[0].localScale = sourceBones[0].localScale;
        int num  = 1;
        int num2 = sourceBones.Length;
        if (failedSyncs > 0)
        {
            num  = sourceLayer + 1;
            num2 = sourceLayer + failedSyncs;
        }

        for (int i = num; i < num2; i++)
        {
            Transform val  = sourceBones[i];
            Transform val2 = cloneBones[i];
            Vector3   localScale2;
            if (!(val == null) && !(val2 == null))
            {
                if (quiet && (i == sourceLayer || i == cloneLayer))
                {
                    continue;
                }

                val2.localPosition = val.localPosition;
                val2.localRotation = val.localRotation;
                if (i == sourceLayer)
                {
                    Vector3 localScale = val.localScale;
                    if (!(localScale.sqrMagnitude >= 0.01f))
                    {
                        localScale2 = Vector3.one;

                        goto IL_00e1;
                    }
                }

                localScale2 = val.localScale;

                goto IL_00e1;
            }

            BuildClone();

            break;
            IL_00e1:
            val2.localScale = localScale2;
            if (val2.gameObject.activeSelf != val.gameObject.activeSelf)
            {
                val2.gameObject.SetActive(val.gameObject.activeSelf);
            }
        }
    }

    public static void Quiet(Renderer r)
    {
        r.shadowCastingMode = 0;
        r.receiveShadows    = false;
    }
}