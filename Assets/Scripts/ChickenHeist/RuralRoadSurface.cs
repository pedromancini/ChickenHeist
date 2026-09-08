using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public static class RuralRoadSurface
{
    public const string RootName = "Estradas de Terra - Superficie Unificada";
    private const float Step = 0.65f;
    private const int ChunkCells = 48;
    private static Collider[] terrain;
    private static readonly Dictionary<Vector2,float> heights=new Dictionary<Vector2,float>();

    private struct Sample
    {
        public Vector2 p;
        public float distance;
        public float wear;
    }

    private sealed class Chunk
    {
        public readonly List<Vector3> vertices = new List<Vector3>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public readonly List<Color> colors = new List<Color>();
        public readonly List<int> triangles = new List<int>();
    }

    public static GameObject Build(IList<RuralRoadSpan> spans, Texture2D texture)
    {
        terrain=RuralTreeRoots.TerrainSurfaces();heights.Clear();
        var root = new GameObject(RootName);
        Shader shader = Shader.Find("ChickenHeist/RuralDirt");
        if (shader == null) throw new System.InvalidOperationException("Rural dirt shader is missing.");
        var material = new Material(shader) { name = "Terra batida - continua e fosca" };
        material.SetTexture("_BaseMap", texture);
        material.enableInstancing = true;
        var cells = new Dictionary<Vector2Int, List<RuralRoadSpan>>();
        foreach (var span in spans)
        {
            Vector2 a = new Vector2(span.start.x,span.start.z), b = new Vector2(span.end.x,span.end.z);
            float padding = span.width*0.5f + Step*2f;
            int minX = Mathf.FloorToInt((Mathf.Min(a.x,b.x)-padding)/Step);
            int maxX = Mathf.CeilToInt((Mathf.Max(a.x,b.x)+padding)/Step);
            int minZ = Mathf.FloorToInt((Mathf.Min(a.y,b.y)-padding)/Step);
            int maxZ = Mathf.CeilToInt((Mathf.Max(a.y,b.y)+padding)/Step);
            for (int z=minZ; z<=maxZ; z++)
            for (int x=minX; x<=maxX; x++)
            {
                var key = new Vector2Int(x,z);
                Vector2 p = new Vector2((x+0.5f)*Step,(z+0.5f)*Step);
                if (Distance(p,a,b,out _) > padding) continue;
                if (!cells.TryGetValue(key,out var list)) cells.Add(key,list=new List<RuralRoadSpan>());
                list.Add(span);
            }
        }
        var samples = new Dictionary<Vector2Int,Sample>();
        var chunks = new Dictionary<Vector2Int,Chunk>();
        foreach (var pair in cells)
        {
            Vector2Int key = pair.Key;
            Sample a = At(key,cells,samples);
            Sample b = At(key+Vector2Int.right,cells,samples);
            Sample c = At(key+Vector2Int.up,cells,samples);
            Sample d = At(key+Vector2Int.one,cells,samples);
            if (a.distance>0 && b.distance>0 && c.distance>0 && d.distance>0) continue;
            var chunkKey = new Vector2Int(Mathf.FloorToInt(key.x/(float)ChunkCells),Mathf.FloorToInt(key.y/(float)ChunkCells));
            if (!chunks.TryGetValue(chunkKey,out Chunk chunk)) chunks.Add(chunkKey,chunk=new Chunk());
            // Every cell is triangulated once. Union happens in the distance field, not overlapping strips.
            Clip(a,c,b,chunk);
            Clip(b,c,d,chunk);
        }
        foreach (var pair in chunks)
        {
            Chunk c = pair.Value;
            if (c.triangles.Count == 0) continue;
            var mesh = new Mesh { name = "Solo continuo " + pair.Key, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(c.vertices); mesh.SetUVs(0,c.uv); mesh.SetColors(c.colors);
            mesh.SetTriangles(c.triangles,0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var go = new GameObject(mesh.name);
            go.transform.SetParent(root.transform,false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
        return root;
    }

    private static Sample At(Vector2Int key, Dictionary<Vector2Int,List<RuralRoadSpan>> cells, Dictionary<Vector2Int,Sample> cache)
    {
        if (cache.TryGetValue(key,out Sample value)) return value;
        Vector2 p = new Vector2(key.x*Step,key.y*Step);
        float best = float.PositiveInfinity, wear = 0f;
        int interiorCount = 0;
        var seen = new HashSet<RuralRoadSpan>();
        for (int z=-1; z<=0; z++)
        for (int x=-1; x<=0; x++)
        {
            if (!cells.TryGetValue(key+new Vector2Int(x,z),out var spans)) continue;
            foreach (var span in spans)
            {
                if (!seen.Add(span)) continue;
                Vector2 a = new Vector2(span.start.x,span.start.z), b = new Vector2(span.end.x,span.end.z);
                float distance = Distance(p,a,b,out float t);
                float radius = span.width*0.5f;
                float noise = (Mathf.PerlinNoise(p.x*0.38f+72f,p.y*0.38f+35f)-0.5f)*0.34f;
                best = Mathf.Min(best,distance-radius+noise);
                if (distance < radius*0.85f) interiorCount++;
                float rut = Mathf.Exp(-Mathf.Pow((distance-radius*0.43f)/0.30f,2f));
                float endFade = Mathf.SmoothStep(0f,1f,Mathf.Min(t,1f-t)*Vector2.Distance(a,b)/3f);
                wear = Mathf.Max(wear,rut*endFade);
            }
        }
        value = new Sample { p=p, distance=best, wear=interiorCount>1 ? wear*0.2f : wear };
        cache.Add(key,value);
        return value;
    }

    private static float Distance(Vector2 p, Vector2 a, Vector2 b, out float t)
    {
        Vector2 ab=b-a;
        t=Mathf.Clamp01(Vector2.Dot(p-a,ab)/Mathf.Max(0.0001f,ab.sqrMagnitude));
        return Vector2.Distance(p,a+ab*t);
    }

    private static void Clip(Sample a, Sample b, Sample c, Chunk chunk)
    {
        Sample[] input={a,b,c};
        var polygon=new List<Sample>(4);
        for (int i=0;i<3;i++)
        {
            Sample from=input[i],to=input[(i+1)%3];
            bool inside=from.distance<=0, nextInside=to.distance<=0;
            if (inside) polygon.Add(from);
            if (inside!=nextInside)
            {
                float t=from.distance/(from.distance-to.distance);
                polygon.Add(new Sample {p=Vector2.Lerp(from.p,to.p,t),distance=0,wear=Mathf.Lerp(from.wear,to.wear,t)});
            }
        }
        for (int i=1;i<polygon.Count-1;i++)
        {
            Vector2 ab=polygon[i].p-polygon[0].p, ac=polygon[i+1].p-polygon[0].p;
            float area=ab.x*ac.y-ab.y*ac.x;
            if (area*area<0.000000001f) continue;
            Add(polygon[0],chunk); Add(polygon[i],chunk); Add(polygon[i+1],chunk);
        }
    }

    private static void Add(Sample s, Chunk chunk)
    {
        chunk.triangles.Add(chunk.vertices.Count);
        if(!heights.TryGetValue(s.p,out float y))
        {
            if(!RuralTreeRoots.SurfaceHeight(terrain,new Vector3(s.p.x,0,s.p.y),out y))y=0;
            heights[s.p]=y;
        }
        chunk.vertices.Add(new Vector3(s.p.x,y+0.045f,s.p.y));
        chunk.uv.Add(s.p/3f);
        float variation=Mathf.PerlinNoise(s.p.x*0.7f+9f,s.p.y*0.7f+31f);
        float tone=0.94f+variation*0.12f-s.wear*0.075f;
        chunk.colors.Add(new Color(tone,tone,tone,1f));
    }
}
