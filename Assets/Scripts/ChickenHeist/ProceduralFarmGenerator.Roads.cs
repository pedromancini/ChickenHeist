using System.Collections.Generic;
using UnityEngine;

public partial class ProceduralFarmGenerator
{
    public Texture2D dirtAlbedo;
    public Texture2D dirtNormal;
    private readonly List<RuralRoadSpan> roadSpans = new List<RuralRoadSpan>();

    private void BuildDirtRoad(string name, Vector3 a, Vector3 b, float width, Transform parent, bool gravel)
    {
        a.y=b.y=0f;
        if (Vector3.Distance(a,b)<0.1f) return;
        var road=new GameObject(name);
        road.transform.SetParent(parent,false);
        var span=road.AddComponent<RuralRoadSpan>();
        span.start=a; span.end=b; span.width=width;
        roadSpans.Add(span);
        if (!gravel) return;
        Vector3 side=Vector3.Cross(Vector3.up,(b-a).normalized);
        int count=Mathf.FloorToInt(Vector3.Distance(a,b)/9f);
        for (int i=0;i<count;i++)
        {
            var prefab=ChooseAsset(rockPrefabs,null,i+name.Length);
            if (prefab==null) continue;
            Vector3 p=Vector3.Lerp(a,b,(i+0.5f)/count)+side*(i%2==0 ? -1f:1f)*width*0.49f;
            bool inside=false;
            for (int row=0;row<farmRows;row++)
            for (int col=0;col<farmColumns;col++)
                if (ExpandRect(GetLotRect(col,row),0.3f).Contains(new Vector2(p.x,p.z))) inside=true;
            if (inside) continue;
            var stone=Instantiate(prefab,road.transform);
            stone.name="Cascalho do acostamento";
            float size=0.12f+Hash01(i*17+name.Length)*0.20f;
            FitAsset(stone,p,new Vector3(size,size*0.45f,size));
            ApplyNatureMaterials(stone,"Pedra de estrada");
            foreach (var collider in stone.GetComponentsInChildren<Collider>()) DestroySafe(collider);
        }
    }
}
