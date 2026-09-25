using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
public static class DialogueVoiceReview
{
    public static void Run()
    {
        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var names=StoryDialogue.openingFiles.Concat(StoryDialogue.declineFiles).ToArray();
            if(names.Length!=24)throw new Exception("Expected 24 dialogue cues");
            var report=names.Select(name=>{
                var clip=Resources.Load<AudioClip>("Dialogue/"+name);
                if(clip==null || clip.length<.1f || clip.channels!=1)throw new Exception("Invalid clip: "+name);
                return "PASS "+name+" duration="+clip.length+" channels="+clip.channels;
            }).ToArray();
            Directory.CreateDirectory("output/dialogue-voice-review");
            File.WriteAllLines("output/dialogue-voice-review/checks.txt",report);
            Debug.Log("DIALOGUE_VOICE_PASS: all 24 dialogue clips load successfully");
            EditorApplication.Exit(0);
        }
        catch(Exception ex){Debug.LogException(ex);EditorApplication.Exit(1);}
    }
}

