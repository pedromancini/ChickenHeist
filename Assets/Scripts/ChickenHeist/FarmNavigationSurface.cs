using UnityEngine;
using UnityEngine.AI;

public class FarmNavigationSurface : MonoBehaviour
{
    public NavMeshData data;
    NavMeshDataInstance instance;
    void OnEnable(){if(data!=null)instance=NavMesh.AddNavMeshData(data);}
    void OnDisable(){if(instance.valid)instance.Remove();}
}
