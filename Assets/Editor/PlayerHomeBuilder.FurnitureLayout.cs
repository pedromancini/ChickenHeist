using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static partial class PlayerHomeBuilder
{
    [MenuItem("Chicken Heist/Arrange Protagonist Furniture")]
    public static void ArrangeFurniture()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        var home=GameObject.Find("Casa do Protagonista - Sitio do Recomeco");
        if(home==null)throw new System.InvalidOperationException("Open the rural world scene first.");
        var scene=SceneManager.GetActiveScene();
        EditorSceneManager.SaveScene(scene,AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/BeforeFurnitureLayout.unity"),true);
        ArrangeFurnitureLayout(home.transform.Find(InteriorName));
        RuralWorldReview.PersistGeneratedAssets(scene);
        PrefabUtility.SaveAsPrefabAsset(home,"Assets/ChickenHeistGenerated/PlayerHome/ProtagonistHome.prefab");
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        CaptureInterior();
        Capture(home,Output+"/furniture-overview.png",new Vector3(-2.2f,2.85f,2.95f),new Vector3(-5.15f,1.25f,5.0f),82);
        HomeInteriorTests.ProbePassage();
        AuditFurniture(home.transform.Find(InteriorName));
        Debug.Log("FURNITURE LAYOUT SAVED: wall alignment and circulation reviewed.");
    }

    static void ArrangeFurnitureLayout(Transform root)
    {
        // Named groups keep tabletop, bedding and stove details attached when furniture moves.
        var table=root.Find("Prop_Wooden_Table_02");
        if(table==null)throw new System.InvalidOperationException("Expected separate wooden table.");
        float oldTop=LocalBounds(root,table).max.y;
        Object.DestroyImmediate(table.gameObject);
        table=Furniture("Prop_Wooden_Table_02",new Vector3(-5.80f,.63f,3.05f),new Vector3(1.6f,.78f,.82f),root,90).transform;
        Bounds desk=LocalBounds(root,table);
        Vector3 phoneDelta=new Vector3(-5.38f-root.Find("Celular antigo - tela rachada").localPosition.x,desk.max.y-oldTop,3.12f-root.Find("Celular antigo - tela rachada").localPosition.z);
        int paperIndex=0,lineIndex=0;
        foreach(Transform item in root)
        {
            if(item.name=="Conta atrasada")
            {
                item.localPosition=new Vector3(-6.02f+paperIndex*.075f,desk.max.y+.012f+paperIndex*.004f,2.98f+paperIndex*.02f);
                item.localRotation=Quaternion.Euler(0,paperIndex*5-5,0);paperIndex++;
            }
            else if(item.name=="Linha da cobranca")
            {
                item.localPosition=new Vector3(-5.89f,desk.max.y+.028f,2.92f+(lineIndex%4)*.038f);
                item.localRotation=Quaternion.identity;lineIndex++;
                item.gameObject.SetActive(lineIndex<=4);
            }
            else if(item.name=="Celular antigo - tela rachada" || item.name=="Tela" || item.name=="Vidro rachado")
            {
                item.localPosition+=phoneDelta;
            }
        }
        var shelf=root.Find("Prop_Shelf_01");
        SnapSet(root,shelf,0,-7.37f,false,new[]{"Saco de mantimentos dobrado"});
        MoveSet(root,shelf,new Vector2(LocalBounds(root,shelf).center.x,5.96f),new[]{"Saco de mantimentos dobrado"});
        var kitchen=root.Find("Prop_Cupboard_02");
        MoveSet(root,kitchen,new Vector2(-5.75f,LocalBounds(root,kitchen).center.z),new[]{"Pano de prato remendado"});
        SnapSet(root,kitchen,2,6.77f,true,new[]{"Pano de prato remendado"});
        var stove=root.Find("Fogao antigo");
        MoveSet(root,stove,new Vector2(-4.71f,6.405f),new[]{"Porta do forno","Puxador do forno","Boca do fogao","Panela da ultima refeicao","Tampa da panela","Alca da panela"});
        SnapSet(root,stove,2,6.77f,true,new[]{"Porta do forno","Puxador do forno","Boca do fogao","Panela da ultima refeicao","Tampa da panela","Alca da panela"});
        var bed=root.Find("Prop_Bed_01");
        SnapSet(root,bed,0,.90f,true,new[]{"Travesseiro antigo","Costura do cobertor"});
        SnapSet(root,bed,2,6.76f,true,new[]{"Travesseiro antigo","Costura do cobertor"});
        var wardrobe=root.Find("Prop_Cupboard_01");
        Object.DestroyImmediate(wardrobe.gameObject);
        wardrobe=Furniture("Prop_Cupboard_01",new Vector3(.6f,.63f,3.52f),new Vector3(.6f,1.4f,1.0f),root,270).transform;
        SnapSet(root,wardrobe,0,.90f,true,new string[0]);
        MoveSet(root,wardrobe,new Vector2(LocalBounds(root,wardrobe).center.x,3.52f),new string[0]);
        foreach(string name in new[]{"Prop_Wooden_Chair_01","A mesma cadeira, tantos anos depois","Assento simples da mesa"})
        {var item=root.Find(name);if(item!=null)Object.DestroyImmediate(item.gameObject);}
        var stool=Place("Stool",new Vector3(-5.80f,.63f,3.78f),new Vector3(.44f,.48f,.44f),root);
        stool.name="Assento simples da mesa";
        var bucket=root.Find("Balde sob a goteira");
        MoveSet(root,bucket,new Vector2(-7.13f,4.66f),new string[0]);
        // Remove the floating-looking stitches; the bed's original blanket already conveys wear.
        for(int i=root.childCount-1;i>=0;i--)if(root.GetChild(i).name=="Costura do cobertor")Object.DestroyImmediate(root.GetChild(i).gameObject);
    }
    static Bounds LocalBounds(Transform root,Transform item)
    {
        Bounds b=ProceduralFarmGenerator.VisualBounds(item.gameObject);
        return new Bounds(root.InverseTransformPoint(b.center),b.size);
    }
    static void MoveSet(Transform root,Transform item,Vector2 target,string[] attached)
    {
        Bounds b=LocalBounds(root,item);Vector3 delta=new Vector3(target.x-b.center.x,0,target.y-b.center.z);
        item.localPosition+=delta;
        foreach(Transform child in root)if(System.Array.IndexOf(attached,child.name)>=0)child.localPosition+=delta;
    }
    static void SnapSet(Transform root,Transform item,int axis,float wall,bool max,string[] attached)
    {
        Bounds b=LocalBounds(root,item);Vector3 p=b.center;p[axis]+=wall-(max?b.max[axis]:b.min[axis]);
        MoveSet(root,item,new Vector2(p.x,p.z),attached);
    }
    static void AuditFurniture(Transform root)
    {
        var lines=new List<string>();var items=new List<Transform>();
        foreach(string name in new[]{"Prop_Wooden_Table_02","Prop_Shelf_01","Prop_Cupboard_02","Fogao antigo","Prop_Bed_01","Prop_Cupboard_01","Assento simples da mesa"})
        {
            var item=root.Find(name);items.Add(item);Bounds b=LocalBounds(root,item);
            bool inside=b.min.x>=-7.41f && b.max.x<=.95f && b.min.z>=2.42f && b.max.z<=6.81f;
            lines.Add((inside?"PASS ":"FAIL ")+name+" inside walls | "+b);
        }
        var fridge=root.Find("Geladeira antiga");
        if(fridge!=null)items.Add(fridge);
        for(int i=0;i<items.Count;i++)for(int j=i+1;j<items.Count;j++)
        {
            Bounds a=LocalBounds(root,items[i]),b=LocalBounds(root,items[j]);a.Expand(-.015f);b.Expand(-.015f);
            if(a.Intersects(b))lines.Add("FAIL overlap: "+items[i].name+" / "+items[j].name);
        }
        Physics.SyncTransforms();int blocked=0;
        foreach(var p in new[]{new Vector3(-3.22f,.65f,3.4f),new Vector3(-3.22f,.65f,4.4f),new Vector3(-2.3f,.65f,4.3f),new Vector3(-1f,.65f,4.3f),new Vector3(-4.4f,.65f,4.8f)})
        {
            var feet=root.TransformPoint(p);
            if(Physics.CheckCapsule(feet+Vector3.up*.36f,feet+Vector3.up*1.64f,.34f,~0,QueryTriggerInteraction.Ignore))blocked++;
        }
        lines.Add((blocked==0?"PASS":"FAIL")+" room circulation: blocked samples="+blocked);
        lines.Add(root.Find("A mesma cadeira, tantos anos depois")==null && root.Find("Prop_Wooden_Chair_01")==null?"PASS old chairs removed":"FAIL old chair remains");
        File.WriteAllLines(Output+"/furniture-layout-audit.txt",lines);
    }
}
