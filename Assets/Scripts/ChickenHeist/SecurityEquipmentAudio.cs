using UnityEngine;

// Original synthesized placeholders, no third-party recording or license.
// Replace/tune during the plan's final audio and listening pass.
public class SecurityEquipmentAudio : MonoBehaviour
{
    AudioSource source;AudioClip alert,rattle;
    void Awake()
    {
        source=gameObject.AddComponent<AudioSource>();source.spatialBlend=1;source.minDistance=2;source.maxDistance=24;
        source.rolloffMode=AudioRolloffMode.Linear;source.volume=.35f;source.playOnAwake=false;
        alert=Make(false);rattle=Make(true);
    }
    void OnEnable(){NoiseEmitter.NoiseEmitted+=Heard;}
    void OnDisable(){NoiseEmitter.NoiseEmitted-=Heard;if(source!=null)source.Stop();}
    void Heard(NoiseSource kind,Vector3 position,float multiplier)
    {
        if(multiplier<=0 || (position-transform.position).sqrMagnitude>.1f && kind==NoiseSource.TrapTriggered)return;
        if(kind==NoiseSource.TrapTriggered && GetComponent<TrapSystem>()!=null)source.PlayOneShot(rattle);
    }
    public void Alert(){if(source!=null && !source.isPlaying)source.PlayOneShot(alert);}
    static AudioClip Make(bool metal)
    {
        const int rate=22050;int count=(int)(rate*(metal?.7f:.3f));var samples=new float[count];
        for(int i=0;i<count;i++)
        {
            float t=(float)i/rate;
            if(metal)
            {
                float value=0;
                for(int strike=0;strike<3;strike++)
                {float dt=t-strike*.11f;if(dt>=0)value+=(Mathf.Sin(dt*1381*2*Mathf.PI)+Mathf.Sin(dt*2177*2*Mathf.PI)*.5f)*Mathf.Exp(-dt*18)*.22f;}
                samples[i]=value;
            }
            else samples[i]=Mathf.Sin(t*880*2*Mathf.PI)*.35f*Mathf.Sin(Mathf.PI*t/.3f)*(t% .15f<.10f?1:0);
        }
        var clip=AudioClip.Create(metal?"Provisional metal wire rattle":"Provisional camera alert",count,1,rate,false);clip.SetData(samples,0);return clip;
    }
    void OnDestroy(){if(alert!=null)Destroy(alert);if(rattle!=null)Destroy(rattle);}
}
