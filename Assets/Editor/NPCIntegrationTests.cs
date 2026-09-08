using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object=UnityEngine.Object;

public static class NPCIntegrationTests
{
    static readonly Dictionary<RoadsideWalker,float> starts=new Dictionary<RoadsideWalker,float>();
    public static IEnumerable<string> Begin()
    {
        starts.Clear();var result=new List<string>();
        var walkers=Object.FindObjectsByType<RoadsideWalker>(FindObjectsSortMode.None);
        result.Add((walkers.Length==10?"PASS ":"FAIL ")+"Ten street residents spawned");
        foreach(var walker in walkers)
        {
            starts[walker]=walker.DistanceWalked;
            var driver=walker.GetComponentInChildren<RuralCharacterAnimator>();
            bool rig=driver!=null && driver.walker==walker && driver.gestureBone!=null
                && driver.GetComponentsInChildren<SkinnedMeshRenderer>().Length>0;
            result.Add((rig?"PASS ":"FAIL ")+walker.name+": imported rig and walking driver");
            if(driver==null)continue;
            foreach(string state in new[]{"Idle","Walk","Run","Trade","Sleep"})
                result.Add((driver.clips[state]!=null?"PASS ":"FAIL ")+walker.name+": "+state+" clip");
            bool textured=driver.GetComponentsInChildren<Renderer>().All(r=>r.sharedMaterials.All(m=>m!=null && m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap")!=null));
            result.Add((textured?"PASS ":"FAIL ")+walker.name+": original texture atlas assigned");
        }
        foreach(var farmer in Object.FindObjectsByType<FarmerSleepSystem>(FindObjectsSortMode.None))
        {
            var visual=farmer.transform.Find("Visual Villager NPC");
            bool valid=visual!=null && visual.GetComponent<RuralCharacterAnimator>().farmer==farmer
                && visual.GetComponentsInChildren<SkinnedMeshRenderer>().Length>0;
            result.Add((valid?"PASS ":"FAIL ")+farmer.name+": Villager NPC model preserves farmer AI");
        }
        return result;
    }
    public static IEnumerable<string> Finish()
    {
        var result=new List<string>();
        foreach(var pair in starts)
        {
            result.Add((pair.Key.DistanceWalked-pair.Value>.15f?"PASS ":"FAIL ")+pair.Key.name+": moved on street during gameplay");
            var driver=pair.Key.GetComponentInChildren<RuralCharacterAnimator>();
            var feet=driver.GetComponent<NPCFootContact>();
            result.Add((feet!=null && Mathf.Abs(feet.LowestSole-(pair.Key.transform.position.y-.04f))<.12f?"PASS ":"FAIL ")+pair.Key.name+": animated feet remain grounded");
            result.Add((driver.clips.IsPlaying("Walk") || driver.clips.IsPlaying("Idle")?"PASS ":"FAIL ")+pair.Key.name+": active locomotion animation");
            var bone=driver.gestureBone;Quaternion original=bone.localRotation;
            driver.clips["Walk"].clip.SampleAnimation(driver.gameObject,.15f);Quaternion a=bone.localRotation;
            driver.clips["Walk"].clip.SampleAnimation(driver.gameObject,.6f);Quaternion b=bone.localRotation;
            result.Add((Quaternion.Angle(a,b)>5?"PASS ":"FAIL ")+pair.Key.name+": imported skeleton is animated");
            bone.localRotation=original;
        }
        PlayerHomeBuilder.CaptureVillagerPopulation();
        return result;
    }
}
