using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;

internal sealed class SkinnedMeshClone : IDisposable
{

    private readonly List<BoneBinding> bindings = new();

    private readonly Dictionary<Transform, Transform> boneMap = new();

    private readonly SkinnedMeshRenderer cloneRenderer;

    private readonly Transform cloneRoot;

    private readonly SkinnedMeshRenderer sourceRenderer;

    private readonly Transform sourceRoot;

    private int failureCount;

    private Transform[] sourceBones = Array.Empty<Transform>();

    private Transform sourceRootBone;

    internal SkinnedMeshClone(SkinnedMeshRenderer arg1, SkinnedMeshRenderer arg2, Transform arg3, Transform arg4)
    {
        sourceRenderer           = arg1;
        cloneRenderer            = arg2;
        sourceRoot               = arg3;
        cloneRoot                = arg4;
        arg2.updateWhenOffscreen = true;
    }

    void IDisposable.Dispose() => Dispose();

    internal void RebuildBindings()
    {
        cloneRenderer.sharedMesh  = sourceRenderer.sharedMesh;
        cloneRenderer.quality     = sourceRenderer.quality;
        cloneRenderer.localBounds = sourceRenderer.localBounds;
        failureCount              = cloneRenderer.sharedMesh != (Object)null ? cloneRenderer.sharedMesh.blendShapeCount : 0;
        Transform[] bones = sourceRenderer.bones;
        bool        flag  = bones.Length != sourceBones.Length || sourceRootBone != (object)sourceRenderer.rootBone;
        int         num   = 0;
        while (!flag && num < bones.Length)
        {
            flag = bones[num] != (object)sourceBones[num];
            num++;
        }

        int num2 = 0;
        while (!flag && num2 < bindings.Count)
        {
            flag = bindings[num2].source == (Object)null || bindings[num2].source.parent != (object)bindings[num2].parent;
            num2++;
        }

        if (flag)
        {
            Dispose();
            sourceBones = bones;
            Transform[] array = new Transform[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                array[i] = FindMappedBone(bones[i]);
            }

            sourceRootBone         = sourceRenderer.rootBone;
            cloneRenderer.bones    = array;
            cloneRenderer.rootBone = FindMappedBone(sourceRootBone);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private Transform FindMappedBone(Transform arg1)
    {
        if (arg1 == (Object)null)
        {
            return null;
        }

        if (arg1 == (object)cloneRoot)
        {
            return sourceRoot;
        }

        if (!boneMap.TryGetValue(arg1, out Transform value))
        {
            Transform val       = FindMappedBone(arg1.parent);
            Transform transform = new GameObject("preview_bone").transform;
            transform.SetParent(val != (Object)null ? val : sourceRoot, false);
            boneMap.Add(arg1, transform);
            bindings.Add(new BoneBinding
            {
                    source    = arg1,
                    clone     = transform,
                    parent    = arg1.parent,
                    copyScale = val == (Object)null,
            });

            return transform;
        }

        return value;
    }

    internal void Synchronize(Matrix4x4 arg1)
    {
        foreach (BoneBinding binding in bindings)
        {
            if (!(binding.source == (Object)null) && !(binding.clone == (Object)null))
            {
                if (!binding.copyScale)
                {
                    binding.clone.localPosition = binding.source.localPosition;
                    binding.clone.localRotation = binding.source.localRotation;
                    binding.clone.localScale    = binding.source.localScale;
                }
                else
                {
                    ApplyTransform(arg1, binding.source, binding.clone);
                }
            }
        }

        if (sourceRenderer.sharedMesh == (object)cloneRenderer.sharedMesh)
        {
            for (int i = 0; i < failureCount; i++)
            {
                cloneRenderer.SetBlendShapeWeight(i, sourceRenderer.GetBlendShapeWeight(i));
            }
        }
    }

    internal static void ApplyTransform(Matrix4x4 arg1, Transform arg2, Transform arg3)
    {
        Matrix4x4 val = arg1 * arg2.localToWorldMatrix;
        arg3.localPosition = val.GetColumn(3);
        arg3.localRotation = val.rotation;
        arg3.localScale    = val.lossyScale;
    }

    public void Dispose()
    {
        foreach (BoneBinding binding in bindings)
        {
            if (binding.clone != null && binding.clone.parent == sourceRoot)
            {
                Object.Destroy(binding.clone.gameObject);
            }
        }

        boneMap.Clear();
        bindings.Clear();
        sourceBones    = Array.Empty<Transform>();
        sourceRootBone = null;
    }

    private sealed class BoneBinding
    {

        internal Transform clone;

        internal bool copyScale;

        internal Transform parent;
        internal Transform source;
    }
}