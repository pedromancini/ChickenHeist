using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class CutsceneTwoReview
{
    const string Key="CutsceneTwoReview",Folder="output/cutscene-two";
    static int phase;static double next;static bool failed;static string farm;
    static Color ambient;static float fogDensity;static Rect viewport;
    static CutsceneTwoReview(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Changed;}
    public static void Review()
    {
        Directory.CreateDirectory(Folder+"/story");File.WriteAllText(Folder+"/checks.txt","");
        EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        var economy=Object.FindAnyObjectByType<HouseholdEconomy>();economy.editorTestSavePath=Path.GetFullPath("Temp/cut2-"+Guid.NewGuid().ToString("N")+".json");
        HouseholdEconomy.SaveAccount(economy.editorTestSavePath,new HouseholdAccount{introSeen=true});
        SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){phase=0;failed=false;next=EditorApplication.timeSinceStartup+5;}
        if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(Key,false);EditorApplication.Exit(failed?1:0);}
    }
    static void Check(bool ok,string message){failed|=!ok;File.AppendAllText(Folder+"/checks.txt",(ok?"PASS ":"FAIL ")+message+"\n");}
    static void SetLine(int line,float time)
    {
        var director=StoryDirector.Instance;var type=typeof(StoryDirector);
        type.GetField("line",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(director,line);
        type.GetField("lineClock",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(director,time);
        type.GetMethod("StartLine",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(director,null);
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || EditorApplication.timeSinceStartup<next)return;
        try
        {
            if(phase==0){GameMenu.Instance.Resume();phase=1;next=EditorApplication.timeSinceStartup+1;return;}
            if(phase==1)
            {
                var farms=Object.FindObjectsByType<FarmLayoutInfo>(FindObjectsSortMode.None).OrderBy(f=>f.layoutIndex).ToArray();farm=farms[Mathf.Min(2,farms.Length-1)].identity;
                HouseholdEconomy.Instance.Commit(a=>{a.news=new List<FarmRaidNews>{new FarmRaidNews{farm=farm,chickens=5,day=a.day}};a.pendingVision=true;a.regionalSecurity=true;return true;},"Review raid");
                ambient=RenderSettings.ambientLight;fogDensity=RenderSettings.fogDensity;viewport=Camera.main.rect;
                Check(StoryDirector.Instance.Begin(true),"Cutscene 2 starts after a raid");
                phase=2;next=EditorApplication.timeSinceStartup+.5;return;
            }
            if(phase==2)
            {
                var stage=Object.FindAnyObjectByType<DeclineCinematicStage>();var camera=Camera.main;var director=StoryDirector.Instance;
                Check(stage!=null,"The stage is built");
                Check(stage.Farm==farm,"It plays at the farm that was raided: "+stage.Farm);
                File.WriteAllText(Folder+"/staging.txt",stage.Staging+"\n"+stage.BedStaging+" tablet "+HouseholdEconomy.Instance.home.InverseTransformPoint(stage.Tablet.position).ToString("0.00"));
                var lot=Object.FindObjectsByType<FarmLayoutInfo>(FindObjectsSortMode.None).First(f=>f.identity==farm).lot;
                Check(stage.Coop!=null && lot.Contains(new Vector2(stage.Coop.transform.position.x,stage.Coop.transform.position.z)),"Its coop is that farm's coop");
                Check(director.LineText(4)=="Cinco.","Osvaldo counts what was taken: \""+director.LineText(4)+"\"");
                Check(stage.Coop!=null && stage.Coop.IsOpen,"The robbed coop stands open");Check(Object.FindObjectsByType<TrapSystem>(FindObjectsSortMode.None).All(t=>t.GetComponentsInChildren<Renderer>().All(x=>!x.enabled)) && Object.FindObjectsByType<SecurityCamera>(FindObjectsSortMode.None).All(c=>c.GetComponentsInChildren<Renderer>().All(x=>!x.enabled)),"No wires or cameras before the neighbours put them up");
                {
                    var home=HouseholdEconomy.Instance.home;string Field(string n){var v=(Vector3)typeof(DeclineCinematicStage).GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(stage);return n+"="+v.ToString("0.00")+" home "+home.InverseTransformPoint(v).ToString("0.00");}
                    var rs=stage.Coop.GetComponentsInChildren<Renderer>();var bb=rs[0].bounds;foreach(var r in rs)bb.Encapsulate(r.bounds);
                    File.AppendAllText(Folder+"/geometry.txt",string.Join("\n",new[]{"latch","outward","across","gate","inward","bedTop","window","toWindow"}.Select(Field))+"\ncoop "+stage.Coop.name+" at "+stage.Coop.transform.position.ToString("0.00")+" bounds "+bb.center.ToString("0.00")+" size "+bb.size.ToString("0.00")+" door "+(stage.Coop.door!=null?stage.Coop.door.position.ToString("0.00"):"-")+"\ntablet "+stage.Tablet.position.ToString("0.00")+" home "+home.InverseTransformPoint(stage.Tablet.position).ToString("0.00")+"\nhome "+home.position.ToString("0.00")+" rot "+home.eulerAngles.ToString("0")+"\n");
                }
                float total=0;int framed=0,spoken=0,moving=0,quiet=0;var trouble=new List<string>();
                for(int line=0;line<director.LineCount;line++)
                {
                    float d=StoryDialogue.declineDurations[line];string who=StoryDialogue.declineSpeakers[line];
                    bool talker=who=="Joana" || who=="Osvaldo" || who=="Bento" || who=="Elias";float jaw=0,others=0;
                    foreach(var (k,f) in new[]{("a",.1f),("b",.5f),("c",.9f)})
                    {
                        SetLine(line,d*f);stage.Evaluate(camera,line,d*f,d,total+d*f);Capture(camera,"story/line-"+line.ToString("00")+"-"+k);
                        if(talker && k=="b")
                        {
                            var head=stage.HeadOf(who);var v=Viewport(camera,head);
                            bool inFrame=v.z>0 && v.x>.08f && v.x<.92f && v.y>.08f && v.y<.95f;
                            bool clear=!Physics.Linecast(camera.transform.position,head,out var block,~0,QueryTriggerInteraction.Ignore);
                            if(inFrame && clear)framed++;else trouble.Add(line+" "+who+(inFrame?" hidden by "+block.collider.name+" at "+block.point.ToString("0.00")+" cam "+camera.transform.position.ToString("0.00")+" head "+head.ToString("0.00"):" out of frame ("+v.x.ToString("0.00")+", "+v.y.ToString("0.00")+")"));
                        }
                    }
                    if(talker)
                    {
                        spoken++;
                        for(float t=.1f;t<d-.1f;t+=.05f)
                        {
                            SetLine(line,t);stage.Evaluate(camera,line,t,d,total+t);
                            jaw=Mathf.Max(jaw,stage.JawOf(who));
                            foreach(var other in new[]{"Joana","Osvaldo","Bento","Elias"}.Where(o=>o!=who))if(t>.4f)others=Mathf.Max(others,stage.JawOf(other));
                        }
                        if(jaw>=20)moving++;else trouble.Add(line+" "+who+" mouth "+jaw.ToString("0"));
                        if(others<=3)quiet++;else trouble.Add(line+" listener mouth "+others.ToString("0"));
                    }
                    total+=d;
                }
                Check(framed==spoken,"Whoever speaks is in the picture and nothing hides them: "+framed+"/"+spoken+(trouble.Count>0?" ("+string.Join("; ",trouble)+")":""));
                Check(moving==spoken,"The speaker's mouth moves through the line: "+moving+"/"+spoken);
                Check(quiet==spoken,"The listeners' mouths stay shut: "+quiet+"/"+spoken);
                {
                    SetLine(2,1);stage.Evaluate(camera,2,1,3.6f,10);var latchAt=stage.Coop.InteractionPoint;
                    var keep=camera.transform.position;var keepRot=camera.transform.rotation;float keepFov=camera.fieldOfView;
                    camera.transform.SetPositionAndRotation(latchAt+Vector3.up*16,Quaternion.LookRotation(Vector3.down,Vector3.forward));camera.fieldOfView=60;
                    Capture(camera,"overhead-scene1");
                    var gateAt=Object.FindObjectsByType<FarmLayoutInfo>(FindObjectsSortMode.None).First(f=>f.identity==farm).entrance;
                    SetLine(10,2);stage.Evaluate(camera,10,2,5.2f,40);
                    camera.transform.SetPositionAndRotation(gateAt+Vector3.up*16,Quaternion.LookRotation(Vector3.down,Vector3.forward));camera.fieldOfView=60;
                    Capture(camera,"overhead-scene2");
                    camera.transform.SetPositionAndRotation(keep,keepRot);camera.fieldOfView=keepFov;
                }
                SetLine(2,1);stage.Evaluate(camera,2,1,3.6f,10);Check(stage.Scene==1 && stage.Lantern.enabled && stage.Lantern.intensity>1,"Before dawn the lantern lights the scene");
                Check(RenderSettings.fog && RenderSettings.fogDensity>.03f,"Before dawn the mist lies thick: "+RenderSettings.fogDensity.ToString("0.000"));
                SetLine(10,2);stage.Evaluate(camera,10,2,5.2f,40);Check(stage.Scene==2 && !stage.Lantern.enabled && RenderSettings.ambientLight.grayscale>.3f,"The morning is lit by the sun: ambient "+RenderSettings.ambientLight.grayscale.ToString("0.00"));
                SetLine(13,2);stage.Evaluate(camera,13,2,4.2f,60);Check(stage.Scene==3 && stage.Tablet!=null && Vector3.Dot(stage.Tablet.up,Vector3.up)>.9f,"In Elias' room the tablet lies lit, screen up");
                SetLine(15,3.2f);stage.Evaluate(camera,15,3.2f,6.5f,70);Check(stage.Headlights.intensity>3 && Vector3.Dot(stage.Tablet.up,Vector3.up)<-.9f,"He has turned it over, and a car's lights go past: "+stage.Headlights.intensity.ToString("0.0"));
                director.Complete();
                phase=3;next=EditorApplication.timeSinceStartup+.5;return;
            }
            if(phase==3)
            {
                Check(Object.FindAnyObjectByType<DeclineCinematicStage>()==null,"The stage is removed afterwards");Check(!FarmSecurityProgression.HiddenForStory && Object.FindObjectsByType<TrapSystem>(FindObjectsSortMode.None).Any(t=>t.GetComponentsInChildren<Renderer>().Any(x=>x.enabled)),"The wires are back in play afterwards");
                Check(RenderSettings.ambientLight==ambient && Mathf.Abs(RenderSettings.fogDensity-fogDensity)<1e-5f && Camera.main.rect==viewport,"Light, mist and the view are put back");
                Check(!HouseholdEconomy.Instance.Account.pendingVision && HouseholdEconomy.Instance.Account.declineSeen,"It is seen once");
                var farmInfo=Object.FindObjectsByType<FarmLayoutInfo>(FindObjectsSortMode.None).First(f=>f.identity==farm);
                var coop=Object.FindObjectsByType<ChickenCoopLockpick>(FindObjectsSortMode.None).OrderBy(c=>(new Vector2(c.transform.position.x,c.transform.position.z)-farmInfo.lot.center).sqrMagnitude).First();
                Check(!coop.IsOpen,"The robbed coop is shut again for play");
            }
        }
        catch(Exception e){Check(false,"Review error: "+e.Message);Debug.LogException(e);}
        EditorApplication.isPlaying=false;
    }
    static Vector3 Viewport(Camera camera,Vector3 at){float aspect=camera.aspect;camera.aspect=1280f/(720*camera.rect.height);var v=camera.WorldToViewportPoint(at);camera.aspect=aspect;return v;}
    static void Capture(Camera camera,string name)
    {
        var old=camera.targetTexture;var active=RenderTexture.active;float aspect=camera.aspect;
        var target=new RenderTexture(1280,720,24);camera.targetTexture=target;camera.aspect=1280f/(720*camera.rect.height);camera.Render();RenderTexture.active=target;
        var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();
        File.WriteAllBytes(Folder+"/"+name+".png",texture.EncodeToPNG());camera.targetTexture=old;camera.aspect=aspect;RenderTexture.active=active;
        Object.DestroyImmediate(target);Object.DestroyImmediate(texture);
    }
}
