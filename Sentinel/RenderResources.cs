using UnityEngine;

namespace Sentinel;

public sealed class RenderResources : MonoBehaviour
{
    private Material ownedMaterial;

    private Mesh ownedMesh;

    private void OnDestroy()
    {
        if (ownedMaterial != null)
        {
            Destroy(ownedMaterial);
        }

        if (ownedMesh != null)
        {
            Destroy(ownedMesh);
        }

        ownedMaterial = null;
        ownedMesh     = null;
    }

    public static void Assign(Renderer target, Material ownedMaterial, Mesh ownedMesh = null)
    {
        target.sharedMaterial = ownedMaterial;
        RenderResources renderResources = target.gameObject.AddComponent<RenderResources>();
        renderResources.ownedMaterial = ownedMaterial;
        renderResources.ownedMesh     = ownedMesh;
    }
}