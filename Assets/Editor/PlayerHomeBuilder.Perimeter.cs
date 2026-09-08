using UnityEngine;
using UnityEditor;
using Object=UnityEngine.Object;

public static partial class PlayerHomeBuilder
{
    private static void ReplaceHomeReturnPoint(Transform home)
    {
        var truck=GameObject.Find("Caminhonete de Fuga");
        if(truck!=null) Object.DestroyImmediate(truck);
        var zones=Object.FindObjectsByType<ExtractionZone>(FindObjectsSortMode.None);
        if(zones.Length!=1) throw new System.InvalidOperationException("Expected one return zone, found "+zones.Length);
        var zone=zones[0];
        var renderer=zone.GetComponent<Renderer>();if(renderer!=null) Object.DestroyImmediate(renderer);
        var mesh=zone.GetComponent<MeshFilter>();if(mesh!=null) Object.DestroyImmediate(mesh);
        zone.name="Retorno ao sitio - Entrega das galinhas";
        zone.transform.SetParent(home,false);zone.transform.localPosition=new Vector3(-3,1,-7);
        zone.transform.localScale=Vector3.one;
        var box=zone.GetComponent<BoxCollider>();box.center=Vector3.zero;box.size=new Vector3(3.2f,2.5f,3.2f);box.isTrigger=true;
        zone.returnPrompt="Seu sitio: pressione E para entregar as galinhas e encerrar a noite.";
        var game=Object.FindFirstObjectByType<HeistGameManager>();
        if(game!=null) game.statusMessage="Roube galinhas para recuperar seu sitio. Volte para casa antes de ser pego.";
    }

    private static void BuildWornPerimeter(Transform home)
    {
        Vector3[] points={new Vector3(-4.7f,0,-12),new Vector3(-17,0,-12),new Vector3(-17,0,13),new Vector3(17,0,13),new Vector3(17,0,-12),new Vector3(-1.3f,0,-12)};
        int panelIndex=0;
        for(int edge=0;edge<points.Length-1;edge++)
        {
            Vector3 a=points[edge],b=points[edge+1],direction=(b-a).normalized;
            int count=Mathf.CeilToInt(Vector3.Distance(a,b)/2.8f);
            float length=Vector3.Distance(a,b)/count;
            for(int i=0;i<count;i++,panelIndex++)
            {
                Vector3 center=Vector3.Lerp(a,b,(i+.5f)/count);
                var fence=Place("Fence",center,new Vector3(4.5f,1.3f,.35f),home);
                var holder=new GameObject("Cerca antiga do quintal");holder.transform.SetParent(home,false);
                Bounds bounds=ProceduralFarmGenerator.VisualBounds(fence);
                holder.transform.position=bounds.center;fence.transform.SetParent(holder.transform,true);
                holder.transform.localScale=new Vector3((length+.018f)/bounds.size.x,1.10f/bounds.size.y,1);
                holder.transform.rotation=Quaternion.FromToRotation(Vector3.right,direction);
                bounds=ProceduralFarmGenerator.VisualBounds(holder);
                holder.transform.position+=home.TransformPoint(center)-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
                Vector3 worldA=home.TransformPoint(center-direction*length*.5f);
                foreach(var mf in fence.GetComponentsInChildren<MeshFilter>())
                {
                    var mesh=Object.Instantiate(mf.sharedMesh);mesh.name="Cerca gasta - painel "+panelIndex;
                    var vertices=mesh.vertices;
                    for(int v=0;v<vertices.Length;v++)
                    {
                        Vector3 p=mf.transform.TransformPoint(vertices[v]);
                        float u=Mathf.Clamp01(Vector3.Dot(p-worldA,direction)/length);
                        float sag=Mathf.Sin(u*Mathf.PI)*(.04f+(panelIndex%5)*.025f);
                        p.y-=sag*Mathf.Clamp01(p.y/.35f);
                        vertices[v]=mf.transform.InverseTransformPoint(p);
                    }
                    mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();mf.sharedMesh=mesh;
                    var collider=mf.GetComponent<MeshCollider>();if(collider!=null) collider.sharedMesh=mesh;
                }
                foreach(var r in fence.GetComponentsInChildren<Renderer>())
                foreach(var material in r.sharedMaterials)
                    material.SetColor("_BaseColor",new Color(.63f+(panelIndex%3)*.04f,.64f+(panelIndex%3)*.03f,.56f));
                if(panelIndex%6==2)
                {
                    var wood=WornMaterial("Remendo da cerca",new Color(.30f,.27f,.20f));
                    Beam(home,"Remendo da cerca antiga",center-direction*.55f+Vector3.up*.28f,center+direction*.55f+Vector3.up*.85f,.095f,wood);
                }
            }
        }
    }
}
