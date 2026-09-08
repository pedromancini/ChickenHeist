using UnityEngine;

// Root contacts are baked from the source mesh, so player builds need no readable mesh copies.
public class RuralTreeRoots : MonoBehaviour
{
    public Vector3[] contacts;
    public bool materialsCalibrated;

    public bool Ground(Collider[] terrain, out float movement)
    {
        movement=0;
        if(contacts==null || contacts.Length==0)return false;
        // A few edge trees have roots beyond the map. Bring their root footprint inside it.
        foreach(var surface in terrain)
        {
            if(surface.name!="Terreno Ondulado Low Poly")continue;
            Bounds footprint=new Bounds(transform.TransformPoint(contacts[0]),Vector3.zero);
            foreach(var contact in contacts)footprint.Encapsulate(transform.TransformPoint(contact));
            Bounds area=surface.bounds;Vector3 shift=Vector3.zero;
            if(footprint.min.x<area.min.x+.1f)shift.x=area.min.x+.1f-footprint.min.x;
            else if(footprint.max.x>area.max.x-.1f)shift.x=area.max.x-.1f-footprint.max.x;
            if(footprint.min.z<area.min.z+.1f)shift.z=area.min.z+.1f-footprint.min.z;
            else if(footprint.max.z>area.max.z-.1f)shift.z=area.max.z-.1f-footprint.max.z;
            transform.position+=shift;break;
        }
        float delta=float.PositiveInfinity;
        foreach(var local in contacts)
        {
            Vector3 p=transform.TransformPoint(local);
            if(!SurfaceHeight(terrain,p,out float y))return false;
            delta=Mathf.Min(delta,y-p.y-.025f);
        }
        transform.position+=Vector3.up*delta;
        movement=delta;
        return true;
    }

    public static Collider[] TerrainSurfaces()
    {
        var surfaces=new System.Collections.Generic.List<Collider>();
        foreach(var collider in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
            if(collider.name=="Terreno Ondulado Low Poly" || collider.name=="Vale Rural Noturno" || collider.name.StartsWith("Morro Rural"))surfaces.Add(collider);
        return surfaces.ToArray();
    }

    public static bool SurfaceHeight(Collider[] terrain,Vector3 p,out float height)
    {
        height=float.NegativeInfinity;
        foreach(var surface in terrain)
        {
            Bounds b=surface.bounds;
            if(p.x<b.min.x || p.x>b.max.x || p.z<b.min.z || p.z>b.max.z)continue;
            var ray=new Ray(new Vector3(p.x,b.max.y+2,p.z),Vector3.down);
            if(surface.Raycast(ray,out var hit,b.size.y+4))height=Mathf.Max(height,hit.point.y);
        }
        return !float.IsNegativeInfinity(height);
    }

    public static void GroundAll()
    {
        Physics.SyncTransforms();
        var terrain=TerrainSurfaces();
        foreach(var tree in Object.FindObjectsByType<RuralTreeRoots>(FindObjectsSortMode.None))
            if(!tree.Ground(terrain,out _))Debug.LogWarning("No terrain under tree: "+tree.name,tree);
        Physics.SyncTransforms();
    }
}
