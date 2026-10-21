using UnityEngine;

// Spray paint going on a camera lens: a jet of fine black droplets from the can to the lens, then a little dark mist
// settling over it. The particle material lives in Resources/Effects (made by EffectAssets), so its shader is in builds.
public static class SprayEffect
{
    static Material material;
    static Material Material()
    {
        if(material!=null)return material;
        material=Resources.Load<Material>("Effects/SprayMist");
        if(material==null){var shader=Shader.Find("Universal Render Pipeline/Particles/Unlit");if(shader!=null)material=new Material(shader){name="Spray mist (fallback)"};}
        return material;
    }
    public static GameObject Play(Vector3 from,Vector3 to)
    {
        var root=new GameObject("Jato de tinta");root.transform.position=from;
        var dir=to-from;float distance=Mathf.Max(.2f,dir.magnitude);root.transform.rotation=Quaternion.LookRotation(dir.normalized);
        // the jet: droplets crossing the gap in about 0.35 s
        var jet=System(root.transform,"Jato",.7f,300);
        var main=jet.main;main.startLifetime=new ParticleSystem.MinMaxCurve(.3f,.4f);main.startSpeed=new ParticleSystem.MinMaxCurve(distance/.36f*.9f,distance/.36f*1.1f);
        main.startSize=new ParticleSystem.MinMaxCurve(.03f,.07f);main.gravityModifier=.04f;
        main.startColor=new ParticleSystem.MinMaxGradient(new Color(.02f,.02f,.02f,.9f),new Color(.10f,.09f,.08f,.75f));
        var emission=jet.emission;emission.rateOverTime=260;
        var shape=jet.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=4.5f;shape.radius=.008f;
        var size=jet.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.6f,1,2.4f));
        Fade(jet,.9f);
        // the mist on the lens
        var mist=System(root.transform,"Nevoa na lente",.6f,60);mist.transform.position=to;
        var m=mist.main;m.startDelay=.3f;m.startLifetime=new ParticleSystem.MinMaxCurve(.8f,1.4f);m.startSpeed=new ParticleSystem.MinMaxCurve(.1f,.45f);
        m.startSize=new ParticleSystem.MinMaxCurve(.10f,.24f);m.gravityModifier=-.02f;
        m.startColor=new ParticleSystem.MinMaxGradient(new Color(.03f,.03f,.03f,.7f),new Color(.12f,.11f,.10f,.5f));
        var e=mist.emission;e.rateOverTime=0;e.SetBursts(new[]{new ParticleSystem.Burst(0,14),new ParticleSystem.Burst(.2f,10)});
        var sh=mist.shape;sh.shapeType=ParticleSystemShapeType.Sphere;sh.radius=.08f;
        Fade(mist,.75f);
        jet.Play();mist.Play();
        Object.Destroy(root,2.5f);
        return root;
    }
    static ParticleSystem System(Transform parent,string name,float duration,int max)
    {
        var go=new GameObject(name);go.transform.SetParent(parent,false);
        var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=ps.main;main.duration=duration;main.loop=false;main.playOnAwake=false;main.simulationSpace=ParticleSystemSimulationSpace.World;main.maxParticles=max;
        var r=go.GetComponent<ParticleSystemRenderer>();r.renderMode=ParticleSystemRenderMode.Billboard;r.sharedMaterial=Material();
        r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;
        return ps;
    }
    static void Fade(ParticleSystem ps,float alpha)
    {
        var col=ps.colorOverLifetime;col.enabled=true;var g=new Gradient();
        g.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(alpha,0),new GradientAlphaKey(alpha*.8f,.5f),new GradientAlphaKey(0,1)});
        col.color=g;
    }
}
