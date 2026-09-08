using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Object=UnityEngine.Object;

public static class HomeExperienceTests
{
    static readonly List<string> results=new List<string>();
    static Camera camera;static Transform player;static Vector3 oldPosition;static Quaternion oldRotation,oldLook;
    static CrackedHomeMirror mirror;static HandheldPhone phone;
    static void Check(bool value,string message){results.Add((value?"PASS ":"FAIL ")+message);}
    public static void Begin()
    {
        results.Clear();camera=Camera.main;player=HeistGameManager.Instance.player;oldPosition=player.position;oldRotation=player.rotation;oldLook=camera.transform.localRotation;
        var birds=Object.FindObjectsByType<InteractableChicken>(FindObjectsSortMode.None);
        foreach(var bird in birds)Check(bird.GetComponentsInChildren<MeshRenderer>().Count(r=>r.name.StartsWith("alt"))==1,bird.name+": one visible animal variant");
        phone=player.GetComponentInChildren<HandheldPhone>();Check(phone!=null && phone.eyes==camera.transform,"Handheld phone bound to player camera");
        mirror=Object.FindAnyObjectByType<CrackedHomeMirror>();Check(mirror!=null,"Cracked mirror installed inside home");
        var controller=player.GetComponent<CharacterController>();controller.enabled=false;
        player.position=mirror.transform.position+mirror.transform.forward*1.8f-Vector3.up*1.31f;
        player.rotation=Quaternion.LookRotation(-mirror.transform.forward);camera.transform.localRotation=Quaternion.identity;controller.enabled=true;
        var body=player.GetComponentInChildren<RuralCharacterAnimator>();body.movement.estaMovendo=false;body.movement.estaSprinting=false;
        var liner=body.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="Forro fechado da camisa");
        Check(liner!=null && liner.GetComponent<Renderer>().enabled,"Closed shirt lining exists");
        ProtagonistPhone.Instance.SetOpen(true);Check(!phone.ScreenReady,"Phone UI waits for drawing motion");
    }
    public static void ClosePhone()
    {
        Check(phone.ScreenReady && phone.handset.gameObject.activeInHierarchy,"Phone drawing reaches held pose");
        float distance=Vector3.Distance(phone.hand.position,phone.GripPoint);
        Check(distance<.16f,"Hand reaches the phone: "+distance.ToString("F3")+"m");
        Capture("phone-held");
        var rect=phone.ScreenRect;
        Check(rect.x>0 && rect.y>0 && rect.xMax<Screen.width && rect.yMax<Screen.height,"Phone and interface stay inside the viewport");
        Check(rect.height>Screen.height*.65f && rect.height<Screen.height*.9f,"Readable phone screen without filling entire view");
        ScreenCapture.CaptureScreenshot("output/home-experience/phone-interface.png");
    }
    public static void StowPhone(){ProtagonistPhone.Instance.SetOpen(false);}
    public static IEnumerable<string> Finish()
    {
        try
        {
            Check(!phone.handset.gameObject.activeSelf,"Phone is stowed after closing");
            Check(phone.hand.position.y<player.position.y+1.2f,"Arm returns below chest after stowing phone");
            var animator=player.GetComponentInChildren<RuralCharacterAnimator>();animator.clips["Idle"].clip.SampleAnimation(animator.gameObject,0);
            Capture("mirror-in-home");Check(mirror.RenderCount>0 && mirror.Reflection!=null,"Mirror renders live reflection");
            if(mirror.Reflection!=null)Save(mirror.Reflection,"mirror-reflection");
            camera.transform.localRotation=Quaternion.Euler(85,0,0);Capture("body-looking-down");
            if(birdsForCapture()!=null)
            {
                var bird=birdsForCapture();camera.transform.position=bird.transform.position+new Vector3(1.1f,.7f,1.2f);
                camera.transform.LookAt(bird.transform.position+Vector3.up*.2f);Capture("single-chicken");
            }
        }
        finally
        {
            ProtagonistPhone.Instance.SetOpen(false);
            var c=player.GetComponent<CharacterController>();c.enabled=false;player.position=oldPosition;player.rotation=oldRotation;c.enabled=true;
            camera.transform.localPosition=new Vector3(0,1.65f,0);camera.transform.localRotation=oldLook;
            File.WriteAllLines("output/home-experience/tests.txt",results);
        }
        return results;
    }
    static InteractableChicken birdsForCapture()=>Object.FindAnyObjectByType<InteractableChicken>();
    static void Capture(string name)
    {
        var texture=new RenderTexture(960,720,24);texture.Create();var previous=camera.targetTexture;
        try{camera.targetTexture=texture;camera.Render();Save(texture,name);}
        finally{camera.targetTexture=previous;texture.Release();Object.DestroyImmediate(texture);}
    }
    static void Save(RenderTexture texture,string name)
    {
        var previous=RenderTexture.active;RenderTexture.active=texture;var image=new Texture2D(texture.width,texture.height,TextureFormat.RGB24,false);
        try{image.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);image.Apply();File.WriteAllBytes("output/home-experience/"+name+".png",image.EncodeToPNG());}
        finally{RenderTexture.active=previous;Object.DestroyImmediate(image);}
    }
}
