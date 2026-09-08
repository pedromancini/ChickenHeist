using UnityEngine;

public static class CoopPadlockModel
{
    public static void Apply(Transform anchor)
    {
        if(anchor==null)return;
        var prefab=Resources.Load<GameObject>("CoopPadlock");
        if(prefab==null)return;
        var filter=anchor.GetComponent<MeshFilter>();var renderer=anchor.GetComponent<MeshRenderer>();
        if(filter!=null && renderer!=null)
        {
            filter.sharedMesh=prefab.GetComponent<MeshFilter>().sharedMesh;
            renderer.sharedMaterials=prefab.GetComponent<MeshRenderer>().sharedMaterials;
        }
    }
}
