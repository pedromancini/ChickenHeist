using System.Collections.Generic;
using UnityEngine;

// Recorded sound effects replace the synthesized placeholders one by one: a file at Assets/Resources/Sfx/<key>.wav
// (or .ogg/.mp3) wins over the sound the code makes for that key. The list of keys, with what each one is and how long,
// is in SONS_PARA_GRAVAR.md at the project root.
public static class SoundLibrary
{
    static readonly HashSet<AudioClip> files=new HashSet<AudioClip>();
    // The recorded clip for `key`, or null.
    public static AudioClip Load(string key)
    {
        var clip=Resources.Load<AudioClip>("Sfx/"+key);
        if(clip==null)return null;
        files.Add(clip);return clip;
    }
    // The recorded clip for `key` if there is one, else the synthesized placeholder.
    public static AudioClip Or(string key,System.Func<AudioClip> synthesize)=>Load(key)??synthesize();
    // Synthesized clips are freed with their owner; recorded ones belong to the project and stay.
    public static void Release(AudioClip clip){if(clip!=null && !files.Contains(clip))Object.Destroy(clip);}
}
