using UnityEngine;

// A tired bulb on old wiring: mostly steady, with short dips and the occasional double flicker.
public class HomeLightFlicker : MonoBehaviour
{
    public float baseIntensity=-1;
    Light lamp;float nextEvent,eventEnd;
    void Awake(){lamp=GetComponent<Light>();if(lamp!=null && baseIntensity<0)baseIntensity=lamp.intensity;nextEvent=Time.time+Random.Range(3f,9f);}
    void Update()
    {
        if(lamp==null)return;
        float hum=1+(Mathf.PerlinNoise(Time.time*3.1f,.7f)-.5f)*.08f;
        if(Time.time>=nextEvent){eventEnd=Time.time+Random.Range(.08f,.32f);nextEvent=Time.time+Random.Range(4f,14f);}
        float dip=Time.time<eventEnd?(Mathf.PerlinNoise(Time.time*40,.2f)>.45f?.25f:.7f):1;
        lamp.intensity=baseIntensity*hum*dip;
    }
}
