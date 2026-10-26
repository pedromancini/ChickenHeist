using System.Collections.Generic;
using UnityEngine;

public static class SoundLibrary
{
    static readonly HashSet<AudioClip> files=new HashSet<AudioClip>();
    public static AudioClip Load(string key)
    {
        var clip=Resources.Load<AudioClip>("Sfx/"+key);
        if(clip==null)return null;
        files.Add(clip);return clip;
    }
    public static AudioClip Or(string key,System.Func<AudioClip> synthesize)=>Load(key)??synthesize();
    public static void Release(AudioClip clip){if(clip!=null && !files.Contains(clip))Object.Destroy(clip);}
}
