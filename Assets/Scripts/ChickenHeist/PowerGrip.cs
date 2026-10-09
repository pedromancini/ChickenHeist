using System.Collections.Generic;
using UnityEngine;

// A closed hand around a bar (the steering wheel rim): where the bar runs through the hand, and the finger pose.
// The bar lies across the palm just past the knuckle line, the fingers close around it in a cascade (the little
// finger closes most) and the thumb wraps around the near side of the bar, above the index finger.
public static class PowerGrip
{
    static readonly Dictionary<Transform,Vector3> anchors=new Dictionary<Transform,Vector3>();
    static readonly string[] Fingers={"Index","Middle","Ring","Little"};
    // Flexion per joint (knuckle, middle, tip) for a bar about 2-3 cm thick.
    static readonly float[,] Flexion={{58,88,50},{66,90,52},{72,90,52},{78,88,50}};

    // Hand-local point that sits on the bar's centre line: the knuckle line, moved to the palm surface and then out
    // by the bar radius, a little towards the wrist (the bar sits at the base of the fingers).
    public static Vector3 Anchor(HandGripPose grip,float barRadius)
    {
        var hand=grip.Hand;
        if(!anchors.TryGetValue(hand,out var knuckles))
        {
            string side=hand.name.Substring(hand.name.Length-1);Vector3 sum=Vector3.zero;int n=0;
            foreach(var f in Fingers){var b=hand.Find(f+"1"+side);if(b!=null){sum+=b.localPosition;n++;}}
            knuckles=n>0?sum/n:grip.FingersLocal*.08f;anchors[hand]=knuckles;
        }
        float scale=Mathf.Max(1e-4f,hand.lossyScale.x);
        return knuckles+grip.PalmLocal*(grip.SurfaceLevel-Vector3.Dot(knuckles,grip.PalmLocal)+barRadius/scale)-grip.FingersLocal*(.008f/scale);
    }
    // Wrist position that puts the anchor on the bar centre for the given hand rotation.
    public static Vector3 Wrist(HandGripPose grip,Quaternion rotation,Vector3 barCentre,float barRadius)
        =>barCentre-rotation*Vector3.Scale(Anchor(grip,barRadius),grip.Hand.lossyScale);
    public static Vector3 AnchorWorld(HandGripPose grip,float barRadius)=>grip.Hand.TransformPoint(Anchor(grip,barRadius));

    // Closes the fingers of a hand whose finger bones are at their rest pose. barAxis: the bar's direction;
    // thumbSide: the bar direction the thumb points along; nearSide: unit vector from the bar towards the wrist side
    // where the thumb rests.
    public static void Close(HandGripPose grip,Vector3 barCentre,Vector3 barAxis,float barRadius,Vector3 nearSide)
    {
        var hand=grip.Hand;string side=hand.name.Substring(hand.name.Length-1);
        Vector3 palm=hand.TransformDirection(grip.PalmLocal).normalized;
        for(int f=0;f<Fingers.Length;f++)
        {
            var b1=hand.Find(Fingers[f]+"1"+side);if(b1==null)continue;
            var b2=b1.Find(Fingers[f]+"2"+side);var b3=b2!=null?b2.Find(Fingers[f]+"3"+side):null;
            var chain=new[]{b1,b2,b3};
            for(int k=0;k<3;k++)
            {
                var bone=chain[k];if(bone==null)break;
                Vector3 along=k<2 && chain[k+1]!=null?chain[k+1].position-bone.position:bone.position-chain[k-1].position;
                Vector3 axis=Vector3.Cross(along,palm);if(axis.sqrMagnitude<1e-8f)continue;
                bone.rotation=Quaternion.AngleAxis(Flexion[f,k],axis.normalized)*bone.rotation;
            }
        }
        // Thumb: wrapped around the near side of the bar just above the index finger, tip towards the fingers.
        var t1=hand.Find("Thumb1"+side);var t2=t1!=null?t1.Find("Thumb2"+side):null;var t3=t2!=null?t2.Find("Thumb3"+side):null;
        if(t1==null || t2==null || t3==null)return;
        Vector3 tip=t3.position+(t3.position-t2.position)*.9f;
        Vector3 axis2=barAxis.normalized;var index=hand.Find("Index1"+side);
        float level=index!=null?Vector3.Dot(index.position-barCentre,axis2)+.012f:Vector3.Dot(t1.position-barCentre,axis2);
        Vector3 target=barCentre+axis2*level+nearSide.normalized*(barRadius+.008f)+palm*(barRadius*.6f);
        t1.rotation=Quaternion.FromToRotation(tip-t1.position,target-t1.position)*t1.rotation;
        foreach(var (bone,angle) in new[]{(t2,12f),(t3,22f)})
        {
            Vector3 along=(bone==t2?t3.position:t3.position+(t3.position-t2.position))-bone.position;
            Vector3 axis=Vector3.Cross(along,nearSide*-1f);
            if(axis.sqrMagnitude>1e-8f)bone.rotation=Quaternion.AngleAxis(angle,axis.normalized)*bone.rotation;
        }
    }
}
