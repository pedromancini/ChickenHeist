using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RegionalProgressionUpgrade
{
    public static void RunBatch()
    {
        EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        var steel=AssetDatabase.LoadAssetAtPath<Material>("Assets/ChickenHeistGenerated/Padlock/Steel.mat");
        var dark=AssetDatabase.LoadAssetAtPath<Material>("Assets/ChickenHeistGenerated/Padlock/Keyway.mat");
        Physics.SyncTransforms();
        foreach(var farm in Object.FindObjectsByType<FarmLayoutInfo>())
        {
            if(farm.GetComponentsInChildren<TrapSystem>(true).Length>0)continue;
            var coop=farm.GetComponentInChildren<ChickenCoopLockpick>();if(coop==null)continue;
            for(int i=0;i<2;i++)
            {
                Vector3 p=coop.InteractionPoint-coop.transform.forward*(1.3f+i*.65f)+coop.transform.right*(i==0?-.75f:.75f);
                if(!Physics.Raycast(p,Vector3.down,out var hit,20,~0,QueryTriggerInteraction.Ignore))continue;
                var plate=GameObject.CreatePrimitive(PrimitiveType.Cylinder);plate.name="Armadilha sonora - protecao regional "+(i+1);
                plate.transform.SetParent(farm.transform);plate.transform.position=hit.point+Vector3.up*.045f;
                plate.transform.localScale=new Vector3(.65f,.035f,.65f);
                plate.GetComponent<Renderer>().sharedMaterial=steel;plate.GetComponent<Collider>().isTrigger=true;
                plate.AddComponent<TrapSystem>();
                for(int bar=0;bar<3;bar++)
                {
                    var groove=GameObject.CreatePrimitive(PrimitiveType.Cube);groove.name="Ranhura da placa";
                    groove.transform.SetParent(plate.transform,false);groove.transform.localPosition=new Vector3((bar-1)*.18f,1.02f,0);
                    groove.transform.localScale=new Vector3(.04f,.03f,.55f);
                    groove.GetComponent<Renderer>().sharedMaterial=dark;Object.DestroyImmediate(groove.GetComponent<Collider>());
                }
            }
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        SessionState.SetBool("Protagonist.Batch",true);PlayableVillageTests.Run();
    }
}
