using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Sentinel;

[DefaultExecutionOrder(10000)]
public class Mirror : MonoBehaviour
{

    private const int MaxRenderers = 96;

    private static readonly FrameBudget rendererSyncBudget = new(4, () => Time.realtimeSinceStartupAsDouble, 0.001);

    private static readonly FrameBudget discoveryBudget = new(1);

    private static readonly BatchedFrameBudget batchedRendererSync = new(rendererSyncBudget);

    private readonly HashSet<Renderer> discoveredRenderers = new();

    private readonly List<Material> materialBuffer = new();

    private readonly List<Renderer> rendererBuffer = new();

    private readonly List<RendererClone> rendererClones = new();

    private Renderer bodyRenderer;

    private GameObject cloneRoot;

    private Renderer faceRenderer;

    private bool headOnly;

    private bool includeFace;

    private float nextDiscoveryAt;

    private MaterialPropertyBlock propertyBlock;

    private VRRig sourceRig;

    private int syncIndex = -1;

    private VisibilityThrottle visibilityThrottle;

    private void LateUpdate()
    {
        if ((Object)(object)sourceRig == (Object)null)
        {
            Cleanup();
            this.enabled = false;
        }
        else if (visibilityThrottle != null)
        {
            UpdateRenderers();
            if (!headOnly)
            {
                SynchronizePose();
            }
        }
    }

    private void OnDestroy() => Cleanup();

    public static Mirror Spawn(Transform parent, VRRig source, float scale) => CreateMirror(parent, source, scale, false);

    public static Mirror Head(Transform parent, VRRig source, float scale) => CreateMirror(parent, source, scale, true);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Mirror CreateMirror(Transform arg1, VRRig arg2, float arg3, bool arg4)
    {
        GameObject val = new("zx_mirror");
        val.transform.SetParent(arg1, false);
        val.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        val.transform.localScale    = Vector3.one * arg3;
        Mirror mirror = val.AddComponent<Mirror>();
        mirror.sourceRig          = arg2;
        mirror.headOnly           = arg4;
        mirror.visibilityThrottle = new VisibilityThrottle(arg4);
        mirror.propertyBlock      = new MaterialPropertyBlock();
        mirror.cloneRoot          = new GameObject("rig_snapshot");
        mirror.cloneRoot.transform.SetParent(val.transform, false);
        mirror.cloneRoot.SetActive(false);

        return mirror;
    }

    private void UpdateRenderers()
    {
        float unscaledTime = Time.unscaledTime;
        if (syncIndex < 0)
        {
            if (!visibilityThrottle.ShouldUpdate(unscaledTime))
            {
                return;
            }

            if (unscaledTime >= nextDiscoveryAt)
            {
                if (!discoveryBudget.TryConsume(Time.frameCount))
                {
                    return;
                }

                DiscoverRenderers();
                nextDiscoveryAt = unscaledTime + 1f;
            }

            if (rendererClones.Count == 0)
            {
                return;
            }

            syncIndex = 0;
        }

        if (!headOnly)
        {
            while (syncIndex < rendererClones.Count)
            {
                if (rendererSyncBudget.TryConsume(Time.frameCount))
                {
                    SynchronizeRenderer(rendererClones[syncIndex++]);

                    continue;
                }

                return;
            }
        }
        else if (!batchedRendererSync.RunBatch(Time.frameCount, rendererClones.Count, delegate(int arg1)
                                                                                      {
                                                                                          SynchronizeRenderer(rendererClones[arg1]);
                                                                                      }))
        {
            return;
        }

        syncIndex = -1;
        bool flag  = false;
        bool flag2 = false;
        bool flag3 = !includeFace;
        foreach (RendererClone item in rendererClones)
        {
            if (!((Object)(object)item.cloneRenderer == (Object)null) && item.cloneRenderer.enabled)
            {
                flag = true;
                if ((Object)(object)item.sourceRenderer == (Object)(object)bodyRenderer)
                {
                    flag2 = true;
                }

                if ((Object)(object)item.sourceRenderer == (Object)(object)faceRenderer)
                {
                    flag3 = true;
                }
            }
        }

        if (headOnly && flag)
        {
            FinalizeFrame();
        }

        cloneRoot.SetActive(flag);
        visibilityThrottle.MarkVisible(unscaledTime, flag && (!headOnly || flag2 && flag3));
        if (headOnly && flag && flag2 && flag3)
        {
            this.enabled = false;
        }
    }

    private void SynchronizePose()
    {
        Matrix4x4 worldToLocalMatrix = sourceRig.transform.worldToLocalMatrix;
        foreach (RendererClone item in rendererClones)
        {
            if (!((Object)(object)item.sourceRenderer == (Object)null) && !((Object)(object)item.cloneRenderer == (Object)null))
            {
                SkinnedMeshClone.ApplyTransform(worldToLocalMatrix, item.sourceRenderer.transform, item.cloneRenderer.transform);
                item.skinnedMeshClone?.Synchronize(worldToLocalMatrix);
            }
        }
    }

    private void DiscoverRenderers()
    {
        discoveredRenderers.Clear();
        bodyRenderer = (Object)(object)sourceRig.bodyRenderer != (Object)null ? sourceRig.bodyRenderer.ActiveBody : null;
        if ((Object)(object)bodyRenderer == (Object)null)
        {
            bodyRenderer = sourceRig.mainSkin;
        }

        RegisterRenderer(bodyRenderer, true);
        includeFace  = (Object)(object)sourceRig.bodyRenderer == (Object)null || sourceRig.bodyRenderer.renderFace;
        faceRenderer = (Object)(object)sourceRig.bodyRenderer != (Object)null ? sourceRig.bodyRenderer.faceRenderer : null;
        if ((Object)(object)faceRenderer == (Object)null)
        {
            faceRenderer = sourceRig.faceSkin;
        }

        if (includeFace)
        {
            RegisterRenderer(faceRenderer, true);
        }

        if (!headOnly)
        {
            CollectRenderers(sourceRig.gameObject);
            CollectCosmetics(sourceRig.activeCosmetics);
            try
            {
                CollectCosmetics(sourceRig.cosmetics);
                CollectCosmetics(sourceRig.overrideCosmetics);
            }
            catch (KeyNotFoundException) { }
        }

        for (int num = rendererClones.Count - 1; num >= 0; num--)
        {
            RendererClone rendererClone = rendererClones[num];
            if (!((Object)(object)rendererClone.sourceRenderer != (Object)null) || !discoveredRenderers.Contains(rendererClone.sourceRenderer))
            {
                DisposeRendererClone(rendererClone);
                rendererClones.RemoveAt(num);
            }
        }

        foreach (Renderer item in discoveredRenderers)
        {
            bool flag = false;
            for (int i = 0; i < rendererClones.Count; i++)
            {
                if ((Object)(object)rendererClones[i].sourceRenderer == (Object)(object)item)
                {
                    flag = true;

                    break;
                }
            }

            if (!flag)
            {
                rendererClones.Add(new RendererClone
                {
                        sourceRenderer = item,
                });
            }
        }
    }

    private void CollectCosmetics(List<GameObject> arg1)
    {
        if (arg1 == null)
        {
            return;
        }

        foreach (GameObject item in arg1)
        {
            if (!((Object)(object)item == (Object)null) && item.activeInHierarchy && !item.transform.IsChildOf(sourceRig.transform))
            {
                CollectRenderers(item);
            }
        }
    }

    private void CollectRenderers(GameObject arg1)
    {
        if ((Object)(object)arg1 == (Object)null || discoveredRenderers.Count >= 96)
        {
            return;
        }

        rendererBuffer.Clear();
        arg1.GetComponentsInChildren(false, rendererBuffer);
        foreach (Renderer item in rendererBuffer)
        {
            RegisterRenderer(item, false);
        }
    }

    private void RegisterRenderer(Renderer arg1, bool arg2)
    {
        if (!((Object)(object)arg1 == (Object)null) && discoveredRenderers.Count < 96 && (arg1 is MeshRenderer || arg1 is SkinnedMeshRenderer) && (arg2 || arg1.enabled && arg1.gameObject.activeInHierarchy) && !((Object)(object)arg1.GetComponentInParent<Mirror>() != (Object)null) && (!((Object)(object)sourceRig.playerText1 != (Object)null) || !arg1.transform.IsChildOf(sourceRig.playerText1.transform)))
        {
            discoveredRenderers.Add(arg1);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void SynchronizeRenderer(RendererClone arg1)
    {
        Renderer renderer = arg1.sourceRenderer;
        if ((Object)(object)renderer == (Object)null)
        {
            if ((Object)(object)arg1.cloneRenderer != (Object)null)
            {
                arg1.cloneRenderer.enabled = false;
            }

            return;
        }

        if ((Object)(object)arg1.cloneRenderer == (Object)null)
        {
            GameObject val = new("renderer_snapshot");
            val.transform.SetParent(cloneRoot.transform, false);
            SkinnedMeshRenderer val2 = renderer as SkinnedMeshRenderer;
            if (!headOnly && (Object)(object)val2 != (Object)null)
            {
                arg1.skinnedMeshClone = new SkinnedMeshClone(val2, (SkinnedMeshRenderer)(arg1.cloneRenderer = val.AddComponent<SkinnedMeshRenderer>()), cloneRoot.transform, sourceRig.transform);
            }
            else
            {
                arg1.cloneMeshFilter = val.AddComponent<MeshFilter>();
                arg1.cloneRenderer   = val.AddComponent<MeshRenderer>();
            }

            arg1.cloneRenderer.enabled = false;
            Quiet(arg1.cloneRenderer);
            arg1.cloneRenderer.lightProbeUsage      = 0;
            arg1.cloneRenderer.reflectionProbeUsage = 0;
        }

        try
        {
            if (arg1.skinnedMeshClone != null)
            {
                if (Time.unscaledTime >= arg1.lastBlendShapeSyncAt)
                {
                    arg1.skinnedMeshClone.RebuildBindings();
                    arg1.lastBlendShapeSyncAt = Time.unscaledTime + 1f;
                }
            }
            else
            {
                SkinnedMeshRenderer val3 = (SkinnedMeshRenderer)(renderer is SkinnedMeshRenderer ? renderer : null);
                if (val3 != null)
                {
                    if ((Object)(object)val3.sharedMesh == (Object)null)
                    {
                        arg1.cloneRenderer.enabled = false;

                        return;
                    }

                    if ((Object)(object)arg1.sourceMesh == (Object)null)
                    {
                        arg1.sourceMesh = new Mesh
                        {
                                name = "zx_preview_mesh",
                        };

                        arg1.sourceMesh.MarkDynamic();
                    }

                    val3.BakeMesh(arg1.sourceMesh, false);
                    if (headOnly && (Object)(object)renderer == (Object)(object)bodyRenderer)
                    {
                        SynchronizeSkinnedMesh(arg1, val3);
                    }

                    arg1.cloneMeshFilter.sharedMesh = arg1.sourceMesh;
                }
                else
                {
                    MeshFilter component = renderer.GetComponent<MeshFilter>();
                    arg1.cloneMeshFilter.sharedMesh = (Object)(object)component != (Object)null ? component.sharedMesh : null;
                }
            }

            SkinnedMeshClone.ApplyTransform(sourceRig.transform.worldToLocalMatrix, renderer.transform, arg1.cloneRenderer.transform);
            renderer.GetSharedMaterials(materialBuffer);
            bool flag = arg1.sourceMaterials == null || arg1.sourceMaterials.Length != materialBuffer.Count;
            int  num  = 0;
            while (!flag && num < materialBuffer.Count)
            {
                flag = (Object)(object)arg1.sourceMaterials[num] != (Object)(object)materialBuffer[num];
                num++;
            }

            if (flag)
            {
                arg1.sourceMaterials               = materialBuffer.ToArray();
                arg1.cloneRenderer.sharedMaterials = arg1.sourceMaterials;
            }

            renderer.GetPropertyBlock(propertyBlock);
            arg1.cloneRenderer.SetPropertyBlock(propertyBlock);
            for (int i = 0; i < materialBuffer.Count; i++)
            {
                renderer.GetPropertyBlock(propertyBlock, i);
                arg1.cloneRenderer.SetPropertyBlock(propertyBlock, i);
            }

            Renderer            renderer2 = arg1.cloneRenderer;
            Renderer            renderer3 = arg1.cloneRenderer;
            SkinnedMeshRenderer val4      = (SkinnedMeshRenderer)(renderer3 is SkinnedMeshRenderer ? renderer3 : null);
            renderer2.enabled = val4 != null ? (Object)(object)val4.sharedMesh != (Object)null : (Object)(object)arg1.cloneMeshFilter.sharedMesh != (Object)null;
        }
        catch (MissingReferenceException)
        {
            arg1.cloneRenderer.enabled = false;
        }
        catch (UnityException)
        {
            arg1.cloneRenderer.enabled = false;
        }
    }

    private void SynchronizeSkinnedMesh(RendererClone arg1, SkinnedMeshRenderer arg2)
    {
        Mesh sharedMesh = arg2.sharedMesh;
        if ((Object)(object)sharedMesh != (Object)(object)arg1.cloneMesh)
        {
            arg1.cloneMesh        = sharedMesh;
            arg1.submeshTriangles = null;
            Transform val = sourceRig.head?.rigTarget;
            if ((Object)(object)val == (Object)null || (Object)(object)val == (Object)(object)sourceRig.transform || !sharedMesh.isReadable)
            {
                return;
            }

            Transform[] bones = arg2.bones;
            bool[]      array = new bool[bones.Length];
            bool        flag  = false;
            for (int i = 0; i < bones.Length; i++)
            {
                array[i] =  (Object)(object)bones[i] != (Object)null && ((Object)(object)bones[i] == (Object)(object)val || bones[i].IsChildOf(val));
                flag     |= array[i];
            }

            if (!flag)
            {
                return;
            }

            BoneWeight[] boneWeights = sharedMesh.boneWeights;
            if (boneWeights.Length != arg1.sourceMesh.vertexCount)
            {
                return;
            }

            float[] array2 = new float[boneWeights.Length];
            for (int j = 0; j < boneWeights.Length; j++)
            {
                BoneWeight val2 = boneWeights[j];
                array2[j] = CopyBlendShapes(array, val2.boneIndex0, val2.weight0) + CopyBlendShapes(array, val2.boneIndex1, val2.weight1) + CopyBlendShapes(array, val2.boneIndex2, val2.weight2) + CopyBlendShapes(array, val2.boneIndex3, val2.weight3);
            }

            int[][] array3 = new int[sharedMesh.subMeshCount][];
            for (int k = 0; k < array3.Length; k++)
            {
                if ((int)sharedMesh.GetTopology(k) == 0)
                {
                    array3[k] = sharedMesh.GetTriangles(k);

                    continue;
                }

                return;
            }

            arg1.submeshTriangles = MeshSubmeshFilter.FilterSubmeshes(array2, array3);
        }

        if (arg1.submeshTriangles == null)
        {
            return;
        }

        Vector3[] vertices = arg1.sourceMesh.vertices;
        Bounds    bounds   = default;
        bool      flag2    = false;
        for (int l = 0; l < arg1.submeshTriangles.Length; l++)
        {
            arg1.sourceMesh.SetTriangles(arg1.submeshTriangles[l], l, false);
            int[] array4 = arg1.submeshTriangles[l];
            foreach (int num in array4)
            {
                if (!flag2)
                {
                    bounds = new Bounds(vertices[num], Vector3.zero);
                    flag2  = true;
                }
                else
                {
                    bounds.Encapsulate(vertices[num]);
                }
            }
        }

        if (flag2)
        {
            arg1.sourceMesh.bounds = bounds;
        }
    }

    private static float CopyBlendShapes(bool[] arg1, int arg2, float arg3)
    {
        if ((uint)arg2 < arg1.Length && arg1[arg2])
        {
            return arg3;
        }

        return 0f;
    }

    private void FinalizeFrame()
    {
        Bounds val  = default;
        bool   flag = false;
        foreach (RendererClone item in rendererClones)
        {
            if ((Object)(object)item.cloneRenderer == (Object)null || !item.cloneRenderer.enabled || (Object)(object)item.cloneMeshFilter.sharedMesh == (Object)null)
            {
                continue;
            }

            Bounds    bounds = item.cloneMeshFilter.sharedMesh.bounds;
            Matrix4x4 val2   = Matrix4x4.TRS(item.cloneRenderer.transform.localPosition, item.cloneRenderer.transform.localRotation, item.cloneRenderer.transform.localScale);
            for (int i = -1; i <= 1; i += 2)
            {
                for (int j = -1; j <= 1; j += 2)
                {
                    for (int k = -1; k <= 1; k += 2)
                    {
                        Vector3 val3 = val2.MultiplyPoint3x4(bounds.center + Vector3.Scale(bounds.extents, new Vector3(i, j, k)));
                        if (!flag)
                        {
                            val  = new Bounds(val3, Vector3.zero);
                            flag = true;
                        }
                        else
                        {
                            val.Encapsulate(val3);
                        }
                    }
                }
            }
        }

        if (flag)
        {
            float num  = Mathf.Max(val.size.x, Mathf.Max(val.size.y, val.size.z));
            float num2 = 0.65f / Mathf.Max(num, 0.01f);
            cloneRoot.transform.localScale    = Vector3.one * num2;
            cloneRoot.transform.localPosition = -val.center * num2;
        }
    }

    private static void DisposeRendererClone(RendererClone arg1)
    {
        arg1.skinnedMeshClone?.Dispose();
        if ((Object)(object)arg1.cloneRenderer != (Object)null)
        {
            Object.Destroy((Object)(object)arg1.cloneRenderer.gameObject);
        }

        if ((Object)(object)arg1.sourceMesh != (Object)null)
        {
            Object.Destroy((Object)(object)arg1.sourceMesh);
        }
    }

    private void Cleanup()
    {
        foreach (RendererClone item in rendererClones)
        {
            DisposeRendererClone(item);
        }

        rendererClones.Clear();
        discoveredRenderers.Clear();
        rendererBuffer.Clear();
        if ((Object)(object)cloneRoot != (Object)null)
        {
            Object.Destroy((Object)(object)cloneRoot);
        }

        cloneRoot = null;
    }

    public static void Quiet(Renderer r)
    {
        r.shadowCastingMode = 0;
        r.receiveShadows    = false;
    }

    [CompilerGenerated]
    private void SetLayerRecursively(int arg1) => SynchronizeRenderer(rendererClones[arg1]);

    private sealed class RendererClone
    {

        internal Mesh cloneMesh;

        internal MeshFilter cloneMeshFilter;

        internal Renderer cloneRenderer;

        internal float lastBlendShapeSyncAt;

        internal SkinnedMeshClone skinnedMeshClone;

        internal Material[] sourceMaterials;

        internal Mesh     sourceMesh;
        internal Renderer sourceRenderer;

        internal int[][] submeshTriangles;
    }

    [Serializable]
    [CompilerGenerated]
    private sealed class Clock
    {
        public static readonly Clock Instance = new();

        internal double Invoke0() => Time.realtimeSinceStartupAsDouble;
    }
}