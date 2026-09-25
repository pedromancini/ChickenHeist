using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
public static class EliasVoiceReview
{
    public static void Run()
    {
        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var names=StoryDialogue.openingFiles.Concat(StoryDialogue.declineFiles).Where(x=>x.EndsWith("_elias")).ToArray();
            if(names.Length!=9)throw new Exception("Expected nine Elias cues");
            var report=names.Select(name=>{
                var clip=Resources.Load<AudioClip>("Dialogue/"+name);
                if(clip==null || clip.length<.1f || clip.channels!=1)throw new Exception("Invalid clip: "+name);
                return "PASS "+name+" duration="+clip.length+" channels="+clip.channels;
            }).ToArray();
            Directory.CreateDirectory("output/elias-voice-review");
            File.WriteAllLines("output/elias-voice-review/checks.txt",report);
            Debug.Log("ELIAS_VOICE_PASS: nine dialogue clips load successfully");
            EditorApplication.Exit(0);
        }
        catch(Exception ex){Debug.LogException(ex);EditorApplication.Exit(1);}
    }
}
