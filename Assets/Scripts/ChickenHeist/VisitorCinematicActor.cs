using System.Linq;
using UnityEngine;

public sealed class VisitorCinematicActor
{
    public readonly Transform root,head,spine,leftHand,rightHand;
    readonly Animation animation;readonly AnimationClip idle,walk,walkBack,talk;
    readonly Transform[] bones;readonly Quaternion[] rest,walkPose,talkPose;
    float talkWeight;
    const float TrunkSway=.22f;
    readonly int[] trunk;readonly Quaternion[] trunkCalm;readonly Transform hips;readonly Vector3 hipsCalm;
    readonly struct Gait{public readonly float stride;public readonly Vector3 lift;public Gait(float stride,Vector3 lift){this.stride=stride;this.lift=lift;}}
    readonly Gait walkGait,backGait;Vector3 walkHips;
    public static float ArmSway=.12f;
    readonly int[] armBones;readonly Quaternion[] armCalm;
    readonly CinematicArm[] arms=new CinematicArm[2];readonly CinematicHand[] hands=new CinematicHand[2];
    readonly HandGripPose[] grips=new HandGripPose[2];readonly Vector3[] knuckles=new Vector3[2];
    readonly Vector3[][] knuckleEach=new Vector3[2][];
    readonly ArmTwist[] twists=new ArmTwist[2];
    readonly System.Collections.Generic.List<(SkinnedMeshRenderer skin,int index)> jaws=new System.Collections.Generic.List<(SkinnedMeshRenderer,int)>();
    float mouth;
    public float ContactError {get;private set;}
    public string Trace="";
    public System.Func<Vector3,float> Plate;
    static readonly float[,] WrapLimit={{0,40,50},{95,100,75},{95,100,75},{95,100,75},{95,100,75}};
    public float WristLoad {get;private set;}
    public CinematicArm Arm(bool right)=>arms[right?1:0];
    public CinematicHand Hand(bool right)=>hands[right?1:0];
    public Vector3 Palm(bool right){int i=right?1:0;return hands[i].hand.TransformDirection(grips[i].PalmLocal).normalized;}
    public Vector3 Fingers(bool right){int i=right?1:0;return hands[i].hand.TransformDirection(grips[i].FingersLocal).normalized;}
    public VisitorCinematicActor(Transform actor)
    {
        root=actor;animation=actor.GetComponentInChildren<Animation>();
        foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>())skin.forceMatrixRecalculationPerRender=true;
        if(animation==null)throw new System.InvalidOperationException("Cinematic actor requires Animation: "+actor.name);
        idle=Clip("Breathe","Idle");walk=animation.GetClip("Walk");walkBack=Clip("WalkBackward","Walk");talk=Clip("Talk");animation.enabled=false;
        if(idle==null || walk==null)throw new System.InvalidOperationException("Missing cinematic Idle/Walk clips");
        head=Bone("Head");spine=Bone("Chest");leftHand=Bone("HandL");rightHand=Bone("HandR");
        bones=root.GetComponentsInChildren<Transform>(true);rest=bones.Select(b=>b.localRotation).ToArray();walkPose=new Quaternion[bones.Length];talkPose=new Quaternion[bones.Length];
        hips=Bone("Hips");var toeL=Bone("ToeL");var toeR=Bone("ToeR");
        idle.SampleAnimation(animation.gameObject,0);float standing=Mathf.Min(root.InverseTransformPoint(toeL.position).y,root.InverseTransformPoint(toeR.position).y);
        walkGait=Measure(walk,toeL,toeR,standing);backGait=walkBack!=null?Measure(walkBack,toeL,toeR,standing):walkGait;
        idle.SampleAnimation(animation.gameObject,0);hipsCalm=hips.localPosition;
        trunk=Enumerable.Range(0,bones.Length).Where(i=>new[]{"Hips","Spine","Spine2","Chest","Neck"}.Contains(bones[i].name)).ToArray();
        trunkCalm=trunk.Select(i=>bones[i].localRotation).ToArray();
        var armNames=new[]{"Shoulder","UpperArm","Forearm","ForearmTwist","Hand"}.SelectMany(n=>new[]{n+"L",n+"R"}).ToArray();
        armBones=Enumerable.Range(0,bones.Length).Where(i=>armNames.Contains(bones[i].name)).ToArray();
        armCalm=armBones.Select(i=>bones[i].localRotation).ToArray();
        for(int i=0;i<bones.Length;i++)bones[i].localRotation=rest[i];
        var body=actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(s=>System.Array.IndexOf(s.bones,leftHand)>=0);
        foreach(var s in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true)){int k=s.sharedMesh!=null?s.sharedMesh.GetBlendShapeIndex("Fala"):-1;if(k>=0)jaws.Add((s,k));}
        for(int i=0;i<2;i++)
        {
            string side=i==0?"L":"R";var hand=i==0?leftHand:rightHand;
            arms[i]=new CinematicArm(root,spine,Bone("UpperArm"+side),Bone("Forearm"+side),hand,body,i==1);
            hands[i]=new CinematicHand(hand,body,root);
            grips[i]=new HandGripPose(hand,root,palmDown:true,fingersFromKnuckles:true);
            twists[i]=ArmTwist.Find(hand,body);
            var sum=Vector3.zero;int n=0;var each=new System.Collections.Generic.List<Vector3>();
            foreach(var f in new[]{"Index1","Middle1","Ring1","Little1"}){var b=hand.Find(f+side);if(b!=null){sum+=b.localPosition;each.Add(b.localPosition);n++;}}
            knuckleEach[i]=each.ToArray();
            knuckles[i]=n>0?sum/n:grips[i].FingersLocal*.08f;
        }
    }
    Gait Measure(AnimationClip clip,Transform toeL,Transform toeR,float standing)
    {
        var (stride,planted)=Stride(clip,animation.gameObject,root,toeL,toeR);
        return new Gait(stride,hips.parent.InverseTransformVector(root.up*(standing-planted)));
    }
    public static (float stride,float planted) Stride(AnimationClip clip,GameObject animated,Transform frame,Transform toeL,Transform toeR)
    {
        const int n=24;var toes=new Vector3[2,n+1];
        for(int i=0;i<=n;i++)
        {
            clip.SampleAnimation(animated,clip.length*i/n);
            toes[0,i]=frame.InverseTransformPoint(toeL.position);toes[1,i]=frame.InverseTransformPoint(toeR.position);
        }
        var steps=new System.Collections.Generic.List<(Vector3 move,float height)>();
        for(int s=0;s<2;s++)
        {
            float floor=float.PositiveInfinity;for(int i=0;i<=n;i++)floor=Mathf.Min(floor,toes[s,i].y);
            for(int i=0;i<n;i++)if(toes[s,i].y<floor+.03f && toes[s,i+1].y<floor+.03f){var d=toes[s,i+1]-toes[s,i];d.y=0;steps.Add((d,(toes[s,i].y+toes[s,i+1].y)*.5f));}
        }
        var way=steps.Aggregate(Vector3.zero,(sum,x)=>sum+x.move.normalized);
        var down=steps.Where(x=>Vector3.Dot(x.move,way)>0).ToList();
        if(down.Count==0)return (1,0);
        return (Mathf.Max(.3f,down.Average(x=>x.move.magnitude)*n),down.Select(x=>x.height).OrderBy(h=>h).ElementAt(down.Count/2));
    }
    AnimationClip Clip(params string[] names){foreach(var n in names){var c=animation.GetClip(n);if(c!=null)return c;}return null;}
    Transform Bone(params string[] names)=>root.GetComponentsInChildren<Transform>(true).First(t=>names.Contains(t.name));
    public void Pose(Vector3 position,float yaw,float time,bool moving,bool speaking,Vector3 gaze,float lean=0,float distance=0,float walkWeight=1,bool backward=false)
    {
        for(int i=0;i<bones.Length;i++)bones[i].localRotation=rest[i];
        if(moving)
        {
            var steps=backward?walkBack:walk;
            var gait=backward?backGait:walkGait;
            steps.SampleAnimation(animation.gameObject,Mathf.Repeat(distance/gait.stride,1)*steps.length);
            for(int i=0;i<bones.Length;i++)walkPose[i]=bones[i].localRotation;walkHips=hips.localPosition+gait.lift;
        }
        talkWeight=Mathf.MoveTowards(talkWeight,speaking && !moving && talk!=null?1:0,Time.deltaTime*2.2f);
        if(talkWeight>0)
        {
            talk.SampleAnimation(animation.gameObject,Mathf.Repeat(time,talk.length));
            for(int i=0;i<bones.Length;i++)talkPose[i]=bones[i].localRotation;
        }
        idle.SampleAnimation(animation.gameObject,Mathf.Repeat(time,Mathf.Max(.1f,idle.length)));
        if(talkWeight>0){float w=Mathf.SmoothStep(0,1,talkWeight);for(int i=0;i<bones.Length;i++)bones[i].localRotation=Quaternion.Slerp(bones[i].localRotation,talkPose[i],w);}
        for(int k=0;k<trunk.Length;k++)bones[trunk[k]].localRotation=Quaternion.Slerp(trunkCalm[k],bones[trunk[k]].localRotation,TrunkSway);
        hips.localPosition=Vector3.Lerp(hipsCalm,hips.localPosition,TrunkSway);
        for(int k=0;k<armBones.Length;k++)bones[armBones[k]].localRotation=Quaternion.Slerp(armCalm[k],bones[armBones[k]].localRotation,ArmSway);
        if(moving){for(int i=0;i<bones.Length;i++)bones[i].localRotation=Quaternion.Slerp(bones[i].localRotation,walkPose[i],walkWeight);hips.localPosition=Vector3.Lerp(hips.localPosition,walkHips,walkWeight);}
        ContactError=0;WristLoad=0;Trace="";
        root.localPosition=position;root.localRotation=Quaternion.Euler(0,yaw,0);
        spine.rotation=Quaternion.AngleAxis(lean,root.right)*spine.rotation;
        Vector3 delta=root.InverseTransformDirection(gaze-head.position);
        float turn=Mathf.Clamp(Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg,-28,28);
        float pitch=Mathf.Clamp(-Mathf.Atan2(delta.y,new Vector2(delta.x,delta.z).magnitude)*Mathf.Rad2Deg,-16,12);
        head.rotation=Quaternion.AngleAxis(turn*.7f,root.up)*Quaternion.AngleAxis(pitch*.65f,root.right)*head.rotation;
    }

    public void Flat(bool right,Vector3 point,Vector3 into,Vector3 along,float weight)
    {
        int i=right?1:0;if(weight<=0)return;
        hands[i].Shape(CinematicHand.Flat,weight);
        Place(i,grips[i].Rotation(into,along),grips[i].ContactLocal,point,weight);
    }
    public void Edge(bool right,Vector3 edge,Vector3 inward,Vector3 near,float thickness,float weight)
    {
        int i=right?1:0;if(weight<=0)return;var hand=hands[i].hand;
        hands[i].Shape(CinematicHand.Edge,weight,aimThumb:true);
        inward=inward.normalized;near=Vector3.ProjectOnPlane(near,inward).normalized;
        float reach=Vector3.Dot(knuckles[i]-grips[i].ContactLocal,grips[i].FingersLocal)*hand.lossyScale.x;
        Vector3 contact=edge-inward*.004f+near*(reach-thickness*.5f-.02f),local=grips[i].ContactLocal;
        var palm=hands[i].PalmSurface;
        float Knuckles(CinematicArm.Plan plan)
        {
            float cost=0;
            foreach(var k in knuckleEach[i])
            {
                float depth=Vector3.Dot(contact+plan.hand*Vector3.Scale(k-local,hand.lossyScale)-edge,-near);
                cost+=Mathf.Pow(Mathf.Max(0,thickness*.5f+.01f-depth)/.002f,2);
            }
            if(Plate!=null && palm!=null)
                for(int p=0;p<palm.Length;p+=3)
                    cost+=Mathf.Pow(Mathf.Max(0,.003f-Plate(contact+plan.hand*Vector3.Scale(palm[p]-local,hand.lossyScale)))/.002f,2);
            return cost;
        }
        Place(i,grips[i].Rotation(inward,-near),local,contact,weight,inward,-50,50,Knuckles);
        if(Plate!=null)
        {
            var placed=contact;float placedGap=hands[i].PalmGap(Plate,out var deep);
            Trace+=" || "+(right?"R":"L")+" gap "+(placedGap*1000).ToString("0.0");
            for(int pass=0;pass<3 && placedGap<.0025f;pass++)
            {
                const float e=.001f;
                var outward=new Vector3(Plate(deep+Vector3.right*e)-Plate(deep-Vector3.right*e),Plate(deep+Vector3.up*e)-Plate(deep-Vector3.up*e),Plate(deep+Vector3.forward*e)-Plate(deep-Vector3.forward*e)).normalized;
                var before=hand.position;
                contact=placed+outward*(.0035f-placedGap);Place(i,grips[i].Rotation(inward,-near),local,contact,weight,inward,-50,50,Knuckles);
                float g=hands[i].PalmGap(Plate,out var d2);
                Trace+=" | pass "+pass+" out "+outward.ToString("F2")+" push "+((.002f-placedGap)*1000).ToString("0.0")+" moved "+((hand.position-before).magnitude*1000).ToString("0.0")+" err "+(ContactError*1000).ToString("0.0")+" gap "+(g*1000).ToString("0.0");
                if(g<=placedGap){contact=placed;Place(i,grips[i].Rotation(inward,-near),local,contact,weight,inward,-50,50,Knuckles);break;}
                placed=contact;placedGap=g;deep=d2;
            }
        }
        hands[i].AimThumb(edge+inward*.035f+near*(thickness*.5f+.008f),weight);
        if(Plate!=null)hands[i].Wrap(Plate,WrapLimit,weight,skipThumbBase:true);
    }
    public void Knock(bool right,Vector3 point,Vector3 into,float weight)
    {
        int i=right?1:0;if(weight<=0)return;var hand=hands[i].hand;
        hands[i].Shape(CinematicHand.Fist,weight,aimThumb:true);
        var front=knuckles[i]+grips[i].FingersLocal*(.012f/Mathf.Max(1e-4f,hand.lossyScale.x));
        Place(i,grips[i].Rotation(Vector3.ProjectOnPlane(-root.up,into),into),front,point,weight);
        var palm=hand.TransformDirection(grips[i].PalmLocal).normalized;
        hands[i].AimThumb((hands[i].Middle(1)+hands[i].Middle(2))*.5f+palm*.012f,weight);
    }
    public void Point(bool right,Vector3 point,Vector3 along,float weight)
    {
        int i=right?1:0;if(weight<=0)return;var hand=hands[i].hand;
        along=(along.normalized+(point-arms[i].upper.position).normalized).normalized;
        hands[i].Shape(CinematicHand.Point,weight,aimThumb:true);
        var palm=hand.TransformDirection(grips[i].PalmLocal).normalized;
        hands[i].AimThumb(hands[i].Middle(2)+palm*.012f,weight);
        var tip=hand.InverseTransformPoint(hands[i].Tip(1));
        Place(i,grips[i].Rotation(Vector3.ProjectOnPlane(-root.up,along),along),tip,point,weight);
    }
    public void Bar(bool right,Vector3 centre,Vector3 axis,Vector3 palmFacing,float radius,float weight)
    {
        int i=right?1:0;if(weight<=0)return;
        hands[i].Shape(CinematicHand.Bar,weight,aimThumb:true);
        Vector3 fingers=Vector3.Cross(axis,palmFacing);if(Vector3.Dot(fingers,centre-root.position)<0)fingers=-fingers;
        Place(i,grips[i].Rotation(palmFacing,fingers),PowerGrip.Anchor(grips[i],radius),centre,weight);
        var palm=hands[i].hand.TransformDirection(grips[i].PalmLocal).normalized;
        hands[i].AimThumb(centre+palm*(radius+.012f)-fingers*.01f,weight);
    }
    public void Speak(float open,bool snap=false)
    {
        mouth=Mathf.Lerp(mouth,Mathf.Clamp01(open),1-Mathf.Exp(-Time.deltaTime*22));
        if(snap || Time.deltaTime<=0)mouth=Mathf.Clamp01(open);
        foreach(var (skin,index) in jaws)skin.SetBlendShapeWeight(index,mouth*100);
    }
    public bool HasJaw=>jaws.Count>0;
    public float JawWeight=>jaws.Count>0?jaws[0].skin.GetBlendShapeWeight(jaws[0].index):0;
    public static float Spoken(string text,float time,float duration)
    {
        var director=StoryDirector.Instance;float voice=director!=null?director.VoiceOpen(time):-1;
        return voice>=0?voice:Mouth(text,time,duration);
    }
    public static float Mouth(string text,float time,float duration)
    {
        if(string.IsNullOrEmpty(text))return 0;
        float start=.15f,end=Mathf.Max(start+.5f,duration-.5f),u=(time-start)/(end-start);
        if(u<=0 || u>=1)return 0;
        float at=u*text.Length;int here=Mathf.Clamp((int)at,0,text.Length-1);
        if(!char.IsLetter(text[here]))return 0;
        float best=0;
        for(int k=-2;k<=2;k++)
        {
            int i=here+k;if(i<0 || i>=text.Length)continue;
            bool sameWord=true;for(int j=Mathf.Min(i,here);j<=Mathf.Max(i,here);j++)if(!char.IsLetter(text[j])){sameWord=false;break;}
            if(!sameWord)continue;
            float open=Openness(text[i]);if(open<.4f)continue;
            float distance=Mathf.Abs(at-(i+.5f));best=Mathf.Max(best,open*Mathf.Clamp01(1-distance/1.4f));
        }
        return best;
    }
    static float Openness(char c)
    {
        switch(char.ToLowerInvariant(c))
        {
            case 'a':case 'á':case 'â':case 'ã':case 'à':return 1;
            case 'o':case 'ó':case 'ô':case 'õ':return .8f;
            case 'e':case 'é':case 'ê':return .7f;
            case 'u':case 'ú':return .5f;
            case 'i':case 'í':return .45f;
            case 'm':case 'b':case 'p':return .05f;
        }
        return char.IsLetter(c)?.1f:0;
    }
    public void Twist(){foreach(var t in twists)t?.Apply();}
    public void Reach(bool right,Vector3 target,float weight)
    {
        int i=right?1:0;if(weight<=0)return;
        arms[i].Apply(arms[i].Solve(target,null),weight);
    }
    void Place(int i,Quaternion rotation,Vector3 local,Vector3 point,float weight,Vector3 free=default,float from=0,float to=0,System.Func<CinematicArm.Plan,float> extra=null)
    {
        var arm=arms[i];Vector3 scaled=Vector3.Scale(local,arm.hand.lossyScale);
        CinematicArm.Plan Reach(Quaternion r)
        {
            var p=arm.Solve(point-r*scaled,r);
            for(int it=0;it<2;it++)p=arm.Solve(point-p.hand*scaled,r);
            return p;
        }
        CinematicArm.Plan plan;
        if(free==Vector3.zero)plan=Reach(rotation);
        else
        {
            float best=from;plan=default;plan.cost=float.PositiveInfinity;
            void Try(float a){a=Mathf.Clamp(a,from,to);var p=Reach(Quaternion.AngleAxis(a,free)*rotation);if(extra!=null)p.cost+=extra(p);if(p.cost<plan.cost){plan=p;best=a;}}
            for(int k=0;k<=6;k++)Try(Mathf.Lerp(from,to,k/6f));
            float step=(to-from)/12;Try(best-step);Try(best+step);
        }
        arm.Apply(plan,weight);
        ContactError=Mathf.Max(ContactError,Vector3.Distance(arm.hand.TransformPoint(local),point)*weight);
        float load=Mathf.Sqrt(Mathf.Pow(arm.Flexion/(arm.Flexion>=0?70:55),2)+Mathf.Pow(arm.Deviation/(arm.Deviation>=0?20:30),2));
        WristLoad=Mathf.Max(WristLoad,load);
    }
}
