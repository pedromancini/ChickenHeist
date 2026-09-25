using System.Collections.Generic;
using UnityEngine;

public class FarmerShotgun : MonoBehaviour
{
    public Transform Muzzle {get;private set;}
    public int ShotsFired {get;private set;}
    public float LastShotDamage {get;private set;}
    Transform weapon,body;FarmerStateMachine ai;RuralCharacterAnimator animator;NPCFootContact feet;
    Transform[] arms=new Transform[6];
    readonly List<Material> materials=new List<Material>();
    AudioSource audioSource;AudioClip shot,reload;
    Light flash;float flashUntil,awakeBlend=1;FarmerActivity previous;
    Vector3 bodyPosition;Quaternion bodyRotation;
    Material Mat(string name,Color color){var m=SecurityEquipmentVisual.Material(name,color);materials.Add(m);return m;}
    void Start()
    {
        ai=GetComponent<FarmerStateMachine>();
        var activeVisual=transform.Find("Visual Villager NPC");
        animator=activeVisual!=null?activeVisual.GetComponent<RuralCharacterAnimator>():GetComponentInChildren<RuralCharacterAnimator>();
        if(animator!=null)
        {
            body=animator.transform;bodyPosition=body.localPosition;bodyRotation=body.localRotation;
            feet=body.GetComponent<NPCFootContact>();
            var names=new[]{"ArmL|upper_arm.L|Upperarm_L","ForearmL|forearm.L|Lowerarm_L","HandL|hand.L|Hand_L","ArmR|upper_arm.R|Upperarm_R","ForearmR|forearm.R|Lowerarm_R","HandR|hand.R|Hand_R"};
            foreach(var bone in body.GetComponentsInChildren<Transform>())for(int i=0;i<6;i++)if(System.Array.IndexOf(names[i].Split('|'),bone.name)>=0)arms[i]=bone;
        }
        weapon=new GameObject("Escopeta de dois canos").transform;weapon.SetParent(transform,false);
        var steel=Mat("Escopeta - aco azulado",new Color(.075f,.09f,.10f));var wood=Mat("Escopeta - coronha de nogueira",new Color(.24f,.105f,.045f));
        var brass=Mat("Escopeta - latão",new Color(.57f,.39f,.10f));var dark=Mat("Escopeta - boca dos canos",new Color(.005f,.008f,.009f));
        SecurityEquipmentVisual.Part(weapon,"Coronha arredondada",PrimitiveType.Capsule,new Vector3(0,-.045f,-.23f),new Vector3(.12f,.19f,.085f),wood,Quaternion.Euler(78,0,0));
        SecurityEquipmentVisual.Part(weapon,"Caixa da culatra",PrimitiveType.Cube,new Vector3(0,0,-.015f),new Vector3(.105f,.09f,.22f),steel);
        SecurityEquipmentVisual.Part(weapon,"Telha de madeira",PrimitiveType.Capsule,new Vector3(0,-.025f,.21f),new Vector3(.095f,.17f,.055f),wood,Quaternion.Euler(90,0,0));
        for(int i=-1;i<=1;i+=2)
        {
            SecurityEquipmentVisual.Rod(weapon,"Cano calibre 12",new Vector3(i*.027f,.045f,.06f),new Vector3(i*.027f,.045f,.68f),.045f,steel);
            SecurityEquipmentVisual.Part(weapon,"Boca escura do cano",PrimitiveType.Cylinder,new Vector3(i*.027f,.045f,.683f),new Vector3(.034f,.002f,.034f),dark,Quaternion.Euler(90,0,0));
        }
        SecurityEquipmentVisual.Part(weapon,"Massa de mira",PrimitiveType.Sphere,new Vector3(0,.075f,.64f),Vector3.one*.015f,brass);
        SecurityEquipmentVisual.Rod(weapon,"Guarda do gatilho",new Vector3(0,-.105f,-.11f),new Vector3(0,-.105f,-.015f),.012f,steel);
        Muzzle=new GameObject("Saida dos canos").transform;Muzzle.SetParent(weapon,false);Muzzle.localPosition=new Vector3(0,.045f,.70f);
        flash=Muzzle.gameObject.AddComponent<Light>();flash.color=new Color(1,.61f,.2f);flash.range=5;flash.intensity=0;
        audioSource=gameObject.AddComponent<AudioSource>();audioSource.spatialBlend=1;audioSource.minDistance=3;audioSource.maxDistance=65;audioSource.rolloffMode=AudioRolloffMode.Linear;audioSource.playOnAwake=false;
        shot=Sound(true);reload=Sound(false);awakeBlend=ai.Activity==FarmerActivity.Sleeping?0:1;
    }
    public void Fire(Vector3 target)
    {
        if(Muzzle==null || GameMenu.BlocksInput || HeistGameManager.Instance?.missionEnded!=false)return;
        ShotsFired++;LastShotDamage=0;flashUntil=Time.time+.08f;audioSource.PlayOneShot(shot,.55f);
        Vector3 direction=(target-Muzzle.position).normalized;
        var player=HeistGameManager.Instance.player;
        var movement=player.GetComponent<PlayerMovement>();float distance=Vector3.Distance(player.position,transform.position);
        float spread=4.5f+(movement!=null && movement.estaMovendo?2.5f:0)+(OldPickupTruck.IsDriving?3:0)+distance*.08f;
        Quaternion aim=Quaternion.LookRotation(direction);
        for(int pellet=0;pellet<6;pellet++)
        {
            Vector2 scatter=Random.insideUnitCircle*Mathf.Tan(spread*Mathf.Deg2Rad);
            Vector3 ray=aim*new Vector3(scatter.x,scatter.y,1).normalized;
            var hits=Physics.RaycastAll(Muzzle.position,ray,32,~0,QueryTriggerInteraction.Collide);System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            foreach(var hit in hits)
            {
                if(hit.transform.IsChildOf(transform))continue;
                var health=hit.transform.GetComponentInParent<PlayerHealth>();
                if(hit.collider.isTrigger && health==null)continue;
                if(health!=null)LastShotDamage+=Mathf.Lerp(8,3,Mathf.Clamp01(hit.distance/32));
                break;
            }
        }
        if(LastShotDamage>0)player.GetComponent<PlayerHealth>()?.TakeDamage(LastShotDamage);
        NoiseEmitter.EmitGlobal(NoiseSource.DoorSlam,transform.position,2);
    }
    void LateUpdate()
    {
        if(ai==null || weapon==null || GameMenu.BlocksInput)return;
        bool asleep=ai.Activity==FarmerActivity.Sleeping;
        awakeBlend=Mathf.MoveTowards(awakeBlend,asleep?0:1,Time.deltaTime/2.6f);
        if(body!=null)
        {
            if(feet!=null)feet.enabled=awakeBlend>=1;
            body.localPosition=bodyPosition+new Vector3(0,.80f,-.45f)*(1-awakeBlend);
            body.localRotation=bodyRotation*Quaternion.Euler(-90*(1-awakeBlend),0,0);
        }
        weapon.gameObject.SetActive(!asleep && awakeBlend>.95f);
        if(ai.Activity==FarmerActivity.Reloading && previous!=FarmerActivity.Reloading)audioSource.PlayOneShot(reload,.3f);
        previous=ai.Activity;
        float pitch=ai.Activity==FarmerActivity.Aiming?0:ai.Activity==FarmerActivity.Reloading?35:20;
        weapon.localPosition=new Vector3(.05f,1.28f,.18f)-Vector3.forward*(Time.time<flashUntil?.05f:0);
        if(arms[0]!=null && arms[3]!=null)
            weapon.position=(arms[0].position+arms[3].position)*.5f-Vector3.up*.22f+transform.forward*.12f+transform.right*.05f-transform.forward*(Time.time<flashUntil?.05f:0);
        weapon.localRotation=Quaternion.Euler(pitch,0,0);flash.intensity=Time.time<flashUntil?3:0;
        if(!weapon.gameObject.activeSelf)return;
        Reach(0,weapon.TransformPoint(new Vector3(-.07f,-.03f,.18f)),-1);
        Reach(3,weapon.TransformPoint(new Vector3(.06f,-.07f,-.10f)),1);
    }
    void Reach(int index,Vector3 target,float side)
    {
        var upper=arms[index];var lower=arms[index+1];var hand=arms[index+2];if(upper==null || lower==null || hand==null)return;
        float a=Vector3.Distance(upper.position,lower.position),b=Vector3.Distance(lower.position,hand.position);
        Vector3 axis=target-upper.position;float length=Mathf.Clamp(axis.magnitude,.02f,a+b-.005f);axis.Normalize();
        Vector3 bend=Vector3.ProjectOnPlane(transform.right*side-Vector3.up*.6f,axis).normalized;
        float along=(a*a-b*b+length*length)/(2*length);
        Vector3 elbow=upper.position+axis*along+bend*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
        upper.rotation=Quaternion.FromToRotation(lower.position-upper.position,elbow-upper.position)*upper.rotation;
        lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,target-lower.position)*lower.rotation;
    }
    // Original synthesized placeholders, pending the plan's listening/mix pass.
    static AudioClip Sound(bool bang)
    {
        const int rate=22050;int length=(int)(rate*(bang?.65f:1.1f));var data=new float[length];var random=new System.Random(123);
        for(int i=0;i<length;i++)
        {
            float t=(float)i/rate;
            if(bang)data[i]=((float)random.NextDouble()*2-1)*Mathf.Exp(-t*18)*.65f+Mathf.Sin(t*91*2*Mathf.PI)*Mathf.Exp(-t*12)*.25f;
            else{float dt=t% .35f;data[i]=Mathf.Sin(dt*1620*2*Mathf.PI)*Mathf.Exp(-dt*90)*.25f;}
        }
        var clip=AudioClip.Create(bang?"Provisional shotgun blast":"Provisional shotgun reload",length,1,rate,false);clip.SetData(data,0);return clip;
    }
    void OnDestroy(){foreach(var m in materials)if(m!=null)Destroy(m);if(shot!=null)Destroy(shot);if(reload!=null)Destroy(reload);}
}
