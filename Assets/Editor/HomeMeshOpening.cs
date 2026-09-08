using System.Collections.Generic;
using UnityEngine;

public static class HomeMeshOpening
{
    struct Vertex
    {
        public Vector3 position,normal,home;
        public Vector2 uv;
        public static Vertex Lerp(Vertex a,Vertex b,float t)=>new Vertex {
            position=Vector3.Lerp(a.position,b.position,t),normal=Vector3.Lerp(a.normal,b.normal,t).normalized,
            home=Vector3.Lerp(a.home,b.home,t),uv=Vector2.Lerp(a.uv,b.uv,t)};
    }
    // Subtract a box by clipping polygons, retaining the source UVs and submeshes.
    public static Mesh Subtract(MeshFilter filter,Transform home,Bounds opening)
    {
        Mesh source=filter.sharedMesh;
        var positions=source.vertices;var normals=source.normals;var uv=source.uv;
        var vertices=new List<Vector3>();var outputNormals=new List<Vector3>();var outputUV=new List<Vector2>();
        var submeshes=new List<int[]>();
        for(int sub=0;sub<source.subMeshCount;sub++)
        {
            var indices=new List<int>();var triangles=source.GetTriangles(sub);
            for(int i=0;i<triangles.Length;i+=3)
            {
                var inside=new List<Vertex>();
                for(int j=0;j<3;j++)
                {
                    int k=triangles[i+j];inside.Add(new Vertex {position=positions[k],normal=normals[k],uv=uv.Length>k?uv[k]:Vector2.zero,
                        home=home.InverseTransformPoint(filter.transform.TransformPoint(positions[k]))});
                }
                for(int plane=0;plane<6 && inside.Count>=3;plane++)
                {
                    int axis=plane/2;float sign=plane%2==0?1:-1;
                    float value=plane%2==0?opening.min[axis]:opening.max[axis];
                    var outside=Clip(inside,axis,value,sign,false);
                    for(int j=1;j<outside.Count-1;j++)
                    {
                        if(Vector3.Cross(outside[j].position-outside[0].position,outside[j+1].position-outside[0].position).sqrMagnitude<1e-14f)continue;
                        foreach(var v in new[]{outside[0],outside[j],outside[j+1]})
                        {indices.Add(vertices.Count);vertices.Add(v.position);outputNormals.Add(v.normal);outputUV.Add(v.uv);}
                    }
                    inside=Clip(inside,axis,value,sign,true);
                }
            }
            submeshes.Add(indices.ToArray());
        }
        var mesh=new Mesh {name="Casa original - abertura transitavel",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
        mesh.SetVertices(vertices);mesh.SetNormals(outputNormals);mesh.SetUVs(0,outputUV);mesh.subMeshCount=submeshes.Count;
        for(int i=0;i<submeshes.Count;i++)mesh.SetTriangles(submeshes[i],i);
        mesh.RecalculateBounds();return mesh;
    }
    static List<Vertex> Clip(List<Vertex> polygon,int axis,float value,float sign,bool positive)
    {
        var result=new List<Vertex>();if(polygon.Count==0)return result;
        Vertex a=polygon[polygon.Count-1];float da=(a.home[axis]-value)*sign*(positive?1:-1);
        foreach(var b in polygon)
        {
            float db=(b.home[axis]-value)*sign*(positive?1:-1);
            if((da>=0)!=(db>=0))result.Add(Vertex.Lerp(a,b,da/(da-db)));
            if(db>=0)result.Add(b);a=b;da=db;
        }
        return result;
    }
}
