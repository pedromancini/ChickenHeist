using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[Serializable]
public class GamePreferences
{
    public float sensitivity=100,volume=.8f,fov=65;
    public int quality=1,width=1920,height=1080,frameLimit=60;
    public bool fullscreen=true,vsync=true,invertY;
    const string Key="ChickenHeist.Settings.v1";
    static UniversalRenderPipelineAsset runtimePipeline;
    public static GamePreferences Defaults()=>new GamePreferences{width=Screen.width,height=Screen.height,fullscreen=Screen.fullScreen};
    public static GamePreferences Load()
    {
        var defaults=Defaults();
        if(HouseholdEconomy.ReviewSession)return defaults;
        try{return PlayerPrefs.HasKey(Key)?JsonUtility.FromJson<GamePreferences>(PlayerPrefs.GetString(Key))??defaults:defaults;}
        catch{return defaults;}
    }
    public void Apply(bool display)
    {
        sensitivity=Mathf.Clamp(sensitivity,20,300);volume=Mathf.Clamp01(volume);fov=Mathf.Clamp(fov,50,90);
        quality=Mathf.Clamp(quality,0,2);
        QualitySettings.vSyncCount=vsync?1:0;
        QualitySettings.globalTextureMipmapLimit=quality==0?1:0;
        QualitySettings.lodBias=new[]{.7f,1f,1.4f}[quality];
        if(runtimePipeline==null)
        {
            var source=(QualitySettings.renderPipeline??GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
            if(source!=null){runtimePipeline=UnityEngine.Object.Instantiate(source);runtimePipeline.name="ChickenHeist runtime video settings";runtimePipeline.hideFlags=HideFlags.DontSave;}
        }
        if(runtimePipeline!=null)
        {
            runtimePipeline.renderScale=new[]{.75f,.9f,1f}[quality];
            runtimePipeline.msaaSampleCount=new[]{1,2,4}[quality];
            runtimePipeline.shadowDistance=new[]{25f,50f,85f}[quality];
            runtimePipeline.mainLightShadowmapResolution=new[]{512,1024,2048}[quality];
            QualitySettings.renderPipeline=runtimePipeline;
        }
        Application.targetFrameRate=frameLimit<=0?-1:Mathf.Clamp(frameLimit,30,240);
        AudioListener.volume=volume;
        foreach(var look in UnityEngine.Object.FindObjectsByType<PlayerLook>())
        {look.sensibilidade=sensitivity;look.invertY=invertY;}
        if(Camera.main!=null)Camera.main.fieldOfView=fov;
        if(display)Screen.SetResolution(Mathf.Clamp(width,800,7680),Mathf.Clamp(height,600,4320),fullscreen?FullScreenMode.FullScreenWindow:FullScreenMode.Windowed);
    }
    public void Persist(){if(HouseholdEconomy.ReviewSession)return;PlayerPrefs.SetString(Key,JsonUtility.ToJson(this));PlayerPrefs.Save();}
    public GamePreferences Copy()=>JsonUtility.FromJson<GamePreferences>(JsonUtility.ToJson(this));
}
