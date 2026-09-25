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
    AudioSource engine;float nextScan,nextStep;bool wasIgniting,wasRunning;int paintUses=-1;
    public int ActiveEffects {get{int n=0;foreach(var s in pool)if(s.isPlaying)n++;return n;}}
    void Awake(){Instance=this;NoiseEmitter.NoiseEmitted+=Noise;}
    void Start()
    {
        for(int i=0;i<12;i++)pool.Add(Source("Efeito espacial "+i));engine=Source("Motor continuo");engine.loop=true;engine.clip=Clip("engine");engine.volume=0;
        doorNodes=FindObjectsByType<HomeDoor>(FindObjectsSortMode.None);farmerNodes=FindObjectsByType<FarmerStateMachine>(FindObjectsSortMode.None);
        foreach(var d in doorNodes)doors[d]=d.opened;
        foreach(var f in farmerNodes){farmers[f]=f.Activity;steps[f]=0;}
        foreach(var key in new[]{"engine","starter","start","stall","step","wood","door","sleep","wake","chicken","cow","spray"})Clip(key);
    }
    AudioSource Source(string label)
    {
        var node=new GameObject(label);node.transform.SetParent(transform,false);node.AddComponent<AudioBusGain>();var s=node.AddComponent<AudioSource>();s.playOnAwake=false;s.spatialBlend=1;s.minDistance=1.5f;s.maxDistance=35;s.rolloffMode=AudioRolloffMode.Linear;return s;
    }
    public void Play(string sound,Vector3 position,float volume=.35f)
    {
        if(GameMenu.IsOpen || ActiveEffects>=12)return;
        if(Camera.main!=null && (Camera.main.transform.position-position).sqrMagnitude>35*35)return;
        foreach(var source in pool)if(!source.isPlaying){source.GetComponent<AudioBusGain>().ambient=sound=="chicken" || sound=="cow";source.transform.position=position;source.clip=Clip(sound);source.volume=volume;source.pitch=Random.Range(.95f,1.05f);source.Play();return;}
    }
    void Noise(NoiseSource noise,Vector3 position,float strength)
    {
        if(noise==NoiseSource.ChickenCluck)Play("chicken",position,.15f);
        else if(noise==NoiseSource.CowMoo)Play("cow",position,.2f);
        else if(noise==NoiseSource.FloorCreak)Play("wood",position,.17f);
    }
    void Update()
    {
        if(Time.unscaledTime>=nextScan)
        {
            nextScan=Time.unscaledTime+1;
            foreach(var source in FindObjectsByType<AudioSource>())if(source.GetComponent<AudioBusGain>()==null && source.GetComponents<AudioSource>().Length==1 && source.GetComponent<AudioListener>()==null)
            {var gain=source.gameObject.AddComponent<AudioBusGain>();gain.narration=source.GetComponent<StoryDirector>()!=null;gain.ambient=source.GetComponentInParent<InteractableChicken>()!=null || source.GetComponentInParent<SimpleAnimalWander>()!=null;}
        }
        if(GameMenu.BlocksInput)return;
        var game=HeistGameManager.Instance;if(game?.player==null)return;
        var movement=game.player.GetComponent<PlayerMovement>();
        if(movement.enabled && movement.estaMovendo && Time.time>=nextStep)
        {
            nextStep=Time.time+Mathf.Clamp(.7f/Mathf.Max(.4f,movement.CurrentMoveSpeed),.23f,.8f);
            bool wood=Physics.Raycast(game.player.position+Vector3.up*.3f,Vector3.down,out var hit,1) && (hit.transform.name.Contains("Assoalho") || hit.transform.name.Contains("Tabua") || hit.transform.name.Contains("Piso"));
            Play(wood?"wood":"step",game.player.position,movement.estaAgachado?.08f:.18f);
        }
        foreach(var door in doorNodes)if(door!=null && doors[door]!=door.opened){Play("door",door.transform.position,.3f);doors[door]=door.opened;}
        foreach(var farmer in farmerNodes)
        {
            if(farmer==null)continue;
            if(farmer.Activity!=farmers[farmer]){if(farmer.Activity==FarmerActivity.Waking)Play("wake",farmer.transform.position,.25f);farmers[farmer]=farmer.Activity;}
            if(Time.time>=steps[farmer] && (farmer.Moving || farmer.Activity==FarmerActivity.Sleeping))
            {steps[farmer]=Time.time+(farmer.Moving?.45f:4);Play(farmer.Moving?"step":"sleep",farmer.transform.position,farmer.Moving?.16f:.06f);}
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
        const int rate=22050;float duration=kind=="engine"?2:kind=="spray"?1.2f:kind=="starter"?1.5f:kind=="cow"?1.4f:kind=="sleep"?1:.4f;
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
            data[i]=Mathf.Clamp(sound*envelope,-.8f,.8f);
        }
        var clip=AudioClip.Create("Original synthesized "+kind,data.Length,1,rate,false);clip.SetData(data,0);clips[kind]=clip;return clip;
    }
    void OnDestroy(){NoiseEmitter.NoiseEmitted-=Noise;foreach(var clip in clips.Values)Destroy(clip);if(Instance==this)Instance=null;}
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
