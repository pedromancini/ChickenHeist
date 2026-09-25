using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class InstallPersistentWorldIds
{
    public static void Run()
    {
        EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        int count=0;
        foreach(var item in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            if(!(item is InteractableChicken || item is ChickenCoopLockpick || item is FarmerSleepSystem || item is RuralGate || item is HomeDoor || item is SecurityCamera || item is TrapSystem))continue;
            var id=item.GetComponent<PersistentWorldId>();
            if(id!=null && !string.IsNullOrEmpty(id.value))continue;
            if(id==null)id=item.gameObject.AddComponent<PersistentWorldId>();
            id.value=Guid.NewGuid().ToString("N");string path="";
            for(var t=item.transform;t!=null;t=t.parent)path="/"+t.GetSiblingIndex()+":"+t.name+path;
            id.legacyPath=path;EditorUtility.SetDirty(id);count++;
        }
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("Persistent IDs installed: "+count);
    }
}
