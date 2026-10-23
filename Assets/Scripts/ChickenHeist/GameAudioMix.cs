using System.Collections.Generic;
using UnityEngine;

public class GameAudioMix : MonoBehaviour
{
    public static float Effects=1,Ambience=.7f,Voice=1;
    public static GameAudioMix Instance {get;private set;}
    readonly List<AudioSource> pool=new List<AudioSource>();readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();
    readonly Dictionary<HomeDoor,bool> doors=new Dictionary<HomeDoor,bool>();readonly Dictionary<FarmerStateMachine,FarmerActivity> farmers=new Dictionary<FarmerStateMachine,FarmerActivity>();
    readonly Dictionary<FarmerStateMachine,float> steps=new Dictionary<FarmerStateMachine,float>();
    HomeDoor[] doorNodes;FarmerStateMachine[] farmerNodes;
    AudioSource engine;float nextScan;bool wasIgniting,wasRunning;int paintUses=-1;
    public int ActiveEffects {get{int n=0;foreach(var s in pool)if(s.isPlaying)n++;return n;}}
    void Awake(){Instance=this;NoiseEmitter.NoiseEmitted+=Noise;}
    void Start()
    {
        for(int i=0;i<12;i++)pool.Add(Effect("Efeito espacial "+i));engine=Source("Motor continuo");engine.loop=true;engine.clip=Clip("engine");engine.volume=0;
        doorNodes=FindObjectsByType<HomeDoor>(FindObjectsSortMode.None);farmerNodes=FindObjectsByType<FarmerStateMachine>(FindObjectsSortMode.None);
        foreach(var d in doorNodes)doors[d]=d.opened;
        foreach(var f in farmerNodes){farmers[f]=f.Activity;steps[f]=0;}
        foreach(var key in new[]{"engine","starter","start","stall","wood","door","sleep","wake","chicken","cow","spray"})Clip(key);
        // the night all round: a breeze through the grass and trees, quiet, on the ambience volume (NightLife adds the
        // crickets, frogs and the owl, here and there, with silences between)
        var night=SoundLibrary.Load("night_ambience");
        if(night!=null){var bed=Source("Noite no vale");bed.spatialBlend=0;bed.loop=true;bed.clip=night;bed.volume=.22f;bed.GetComponent<AudioBusGain>().ambient=true;bed.time=Random.Range(0,night.length);bed.Play();}
        // a new day: the rooster, a moment after waking up
        if(HomeNextNight.JustWoke){HomeNextNight.JustWoke=false;var home=HouseholdEconomy.Instance?.home;if(home!=null)StartCoroutine(Rooster(home.position+home.right*14+home.forward*9));}
    }
    System.Collections.IEnumerator Rooster(Vector3 at){yield return new WaitForSeconds(1.2f);PlayRecorded("rooster_morning",at,.5f);}
    // A recorded effect only: silent while the file is missing (no synthesized stand-in for these).
    public void PlayRecorded(string key,Vector3 position,float volume=.35f){if(SoundLibrary.Load(key)!=null)Play(key,position,volume);}
    // One of the pool's slots: no bus filter on it (a filter on a source without a clip turns it into a generator that
    // Unity reports as playing forever: the slots all looked busy and no effect ever played); Play scales its volume.
    AudioSource Effect(string label)
    {
        var node=new GameObject(label);node.transform.SetParent(transform,false);var s=node.AddComponent<AudioSource>();s.playOnAwake=false;s.spatialBlend=1;s.minDistance=1.5f;s.maxDistance=35;s.rolloffMode=AudioRolloffMode.Linear;return s;
    }
    AudioSource Source(string label)
    {
        var node=new GameObject(label);node.transform.SetParent(transform,false);node.AddComponent<AudioBusGain>();var s=node.AddComponent<AudioSource>();s.playOnAwake=false;s.spatialBlend=1;s.minDistance=1.5f;s.maxDistance=35;s.rolloffMode=AudioRolloffMode.Linear;return s;
    }
    public void Play(string sound,Vector3 position,float volume=.35f)
    {
        if(GameMenu.IsOpen || ActiveEffects>=12)return;
        if(Camera.main!=null && (Camera.main.transform.position-position).sqrMagnitude>35*35)return;
        foreach(var source in pool)if(!source.isPlaying){bool ambient=sound=="chicken" || sound=="cow";source.transform.position=position;source.clip=Clip(sound);source.volume=volume*(ambient?Ambience:Effects);source.pitch=Random.Range(.95f,1.05f);source.Play();return;}
    }
    void Noise(NoiseSource noise,Vector3 position,float strength)
    {
        if(noise==NoiseSource.ChickenCluck)Play("chicken",position,.15f);
        else if(noise==NoiseSource.CowMoo)Play("cow",position,.2f);
        else if(noise==NoiseSource.FloorCreak)Play("wood",position,.17f);
    }
    // Something alive in the dark every so often, never on a beat: a cricket's bout close by, frogs or an owl far off,
    // from a direction of its own, with silences of 6-24 s between (a constant chorus read as a buzzing machine).
    readonly AudioSource[] nightLife=new AudioSource[2];float nextLife=4;
    static readonly string[] Life={"night_cricket_1","night_cricket_2","night_cricket_3","night_cricket_1","night_cricket_2","night_frogs","night_owl"};
    void NightLife(Vector3 around)
    {
        if(Time.time<nextLife)return;nextLife=Time.time+Random.Range(6f,24f);
        string key=Life[Random.Range(0,Life.Length)];var clip=SoundLibrary.Load(key);if(clip==null)return;
        var source=nightLife[0]!=null && nightLife[0].isPlaying?nightLife[1]:nightLife[0];
        if(source==null){source=Effect("Vida noturna");source.minDistance=4;source.maxDistance=70;nightLife[nightLife[0]==null?0:1]=source;}
        if(source.isPlaying)return;
        bool far=key=="night_owl" || key=="night_frogs";float angle=Random.Range(0,2*Mathf.PI),distance=far?Random.Range(28f,45f):Random.Range(7f,18f);
        source.transform.position=around+new Vector3(Mathf.Cos(angle),far?3:.2f,Mathf.Sin(angle))*distance;
        source.clip=clip;source.pitch=Random.Range(.94f,1.06f);source.volume=(far?.55f:.35f)*Ambience;source.Play();
    }
    void Update()
    {
        if(Time.unscaledTime>=nextScan)
        {
            nextScan=Time.unscaledTime+1;
            foreach(var source in FindObjectsByType<AudioSource>())if(source.transform.parent!=transform && source.GetComponent<AudioBusGain>()==null && source.GetComponents<AudioSource>().Length==1 && source.GetComponent<AudioListener>()==null)
            {var gain=source.gameObject.AddComponent<AudioBusGain>();gain.narration=source.GetComponent<StoryDirector>()!=null;gain.ambient=source.GetComponentInParent<InteractableChicken>()!=null || source.GetComponentInParent<SimpleAnimalWander>()!=null;}
        }
        if(GameMenu.BlocksInput)return;
        var game=HeistGameManager.Instance;if(game?.player==null)return;
        NightLife(game.player.position);
        foreach(var door in doorNodes)if(door!=null && doors[door]!=door.opened){Play("door",door.transform.position,.3f);doors[door]=door.opened;}
        foreach(var farmer in farmerNodes)
        {
            if(farmer==null)continue;
            if(farmer.Activity!=farmers[farmer]){if(farmer.Activity==FarmerActivity.Waking)Play("wake",farmer.transform.position,.25f);farmers[farmer]=farmer.Activity;}
            if(Time.time>=steps[farmer] && farmer.Activity==FarmerActivity.Sleeping)
            {steps[farmer]=Time.time+4;Play("sleep",farmer.transform.position,.06f);}
        }
        var truck=OldPickupTruck.Instance;
        if(truck!=null)
        {
            engine.transform.position=truck.transform.position;bool running=truck.ignition.EngineRunning,igniting=truck.ignition.Active;
            if(igniting && !wasIgniting)Play("starter",truck.transform.position,.3f);
            if(wasIgniting && !igniting)Play(running?"start":"stall",truck.transform.position,.32f);
            if(running && !engine.isPlaying)engine.Play();engine.volume=Mathf.MoveTowards(engine.volume,running?.18f+Mathf.Abs(truck.Speed)*.008f:0,Time.deltaTime*.5f);engine.volume=Mathf.Min(engine.volume,.4f);
            engine.pitch=Mathf.MoveTowards(engine.pitch,.8f+Mathf.Min(1.4f,Mathf.Abs(truck.Speed)*.065f),Time.deltaTime);if(!running && engine.volume<=0)engine.Stop();wasIgniting=igniting;wasRunning=running;
        }
        int uses=HouseholdEconomy.Instance.Account.paintUses;if(paintUses>=0 && uses<paintUses)Play("spray",game.player.position+game.player.forward,.2f);paintUses=uses;
    }
    public AudioClip Clip(string kind)
    {
        if(clips.TryGetValue(kind,out var existing))return existing;
        var recorded=SoundLibrary.Load(kind);if(recorded!=null){clips[kind]=recorded;return recorded;}
        const int rate=22050;float duration=kind=="engine"?2:kind=="spray"?1.2f:kind=="starter"?1.5f:kind=="cow"?1.4f:kind=="sleep"?1:kind=="bell"?1.15f:kind=="creak"?.75f:.4f;
        var data=new float[(int)(duration*rate)];var random=new System.Random(72);float phase=0,previous=0;
        for(int i=0;i<data.Length;i++)
        {
            float t=(float)i/rate,noise=(float)random.NextDouble()*2-1;previous=Mathf.Lerp(previous,noise,.3f);
            float envelope=kind=="engine"?1:Mathf.Min(1,t*40)*Mathf.Pow(Mathf.Max(0,1-t/duration),2);
            float freq=kind=="cow"?110+25*Mathf.Sin(t*4):kind=="chicken"?650+180*Mathf.Sin(t*24):kind=="engine"?45:kind=="starter"?35:kind=="sleep"?65:kind=="wake"?130:220;
            phase+=2*Mathf.PI*freq/rate;
            float sound=(Mathf.Sin(phase)+Mathf.Sin(phase*2)*.35f)*.25f;
            if(kind=="spray" || kind=="step" || kind=="wood" || kind=="door")sound=previous*(kind=="spray"?.6f:.8f)+Mathf.Sin(phase)*.12f;
            if(kind=="engine")sound+=previous*.12f;
            if(kind=="starter")sound=(sound+previous*.2f)*(.25f+.75f*Mathf.Max(0,Mathf.Sin(t*2*Mathf.PI*7)));
            if(kind=="start")sound=Mathf.Sin(2*Mathf.PI*(40*t+90*t*t))*.4f+previous*.12f;
            if(kind=="stall")sound=(Mathf.Sin(2*Mathf.PI*(95*t-80*t*t))*.35f+previous*.18f)*Mathf.Max(0,Mathf.Sin(t*40));
            // Old doorbell: two-tone buzzer struck by its hammer, cutting out on tired wiring.
            if(kind=="bell"){float hammer=Mathf.Sin(2*Mathf.PI*31*t)>0?1:.35f;float cut=(t%.41f)<.31f?1:.12f;
                sound=(Mathf.Sin(2*Mathf.PI*880*t)*.42f+Mathf.Sin(2*Mathf.PI*1176*t)*.28f+previous*.18f)*hammer*cut;envelope=Mathf.Min(1,t*60)*Mathf.Clamp01((duration-t)*8);}
            // Gate hinge: stick-slip squeal sliding in pitch.
            if(kind=="creak"){float f=210+130*Mathf.Sin(t*6.5f);float slip=Mathf.Max(0,Mathf.Sin(2*Mathf.PI*19*t));
                sound=(Mathf.Sin(2*Mathf.PI*f*t)*.38f*slip+previous*.22f)*(.6f+.4f*Mathf.Sin(t*11));envelope=Mathf.Min(1,t*25)*Mathf.Clamp01((duration-t)*5);}
            data[i]=Mathf.Clamp(sound*envelope,-.8f,.8f);
        }
        var clip=AudioClip.Create("Original synthesized "+kind,data.Length,1,rate,false);clip.SetData(data,0);clips[kind]=clip;return clip;
    }
    void OnDestroy(){NoiseEmitter.NoiseEmitted-=Noise;foreach(var clip in clips.Values)SoundLibrary.Release(clip);if(Instance==this)Instance=null;}
}

public class AudioBusGain : MonoBehaviour
{
    public bool narration,ambient;
    void OnAudioFilterRead(float[] data,int channels)
    {
        float gain=narration?GameAudioMix.Voice:ambient?GameAudioMix.Ambience:GameAudioMix.Effects;
        for(int i=0;i<data.Length;i++)data[i]=Mathf.Clamp(data[i]*gain,-.95f,.95f);
    }
}
