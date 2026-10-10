using UnityEngine;

// Finger shapes for the cinematic actors (ProtagonistRig names: Thumb/Index/Middle/Ring/Little 1-3, L/R suffix).
// Every joint bends about the axis it has in the bind pose (across the finger, towards the palm; the bind pose is a
// T-pose with the palms down), starting from its bind rotation, so a finger curls in its own plane whatever the clip
// had done to it. The thumb is aimed at a point and its two outer joints bend.
public sealed class CinematicHand
{
    public static readonly string[] Names={"Thumb","Index","Middle","Ring","Little"};
    // degrees per joint (base, middle, tip) for each finger, thumb first (its base is aimed separately)
    public static readonly float[,] Relaxed={{0,8,10},{10,16,10},{13,20,12},{16,23,13},{19,26,14}};
    public static readonly float[,] Flat={{0,4,4},{4,5,3},{4,5,3},{5,6,3},{6,7,4}};
    public static readonly float[,] Fist={{0,30,35},{86,98,58},{90,100,60},{92,100,60},{94,98,58}};
    public static readonly float[,] Point={{0,25,30},{6,8,4},{72,96,58},{80,98,58},{86,96,56}};
    // a thin plate's edge in the palm, the fingers past its far face folding over it (a book held open)
    public static readonly float[,] Edge={{0,10,15},{62,26,12},{68,28,13},{72,30,14},{76,32,15}};
    // around a bar 2-3 cm thick (a door lever): the little finger closes most
    public static readonly float[,] Bar={{0,12,22},{58,88,50},{66,90,52},{72,90,52},{78,88,50}};

    public readonly Transform hand;
    readonly Transform[,] bones=new Transform[5,3];
    readonly Quaternion[,] bind=new Quaternion[5,3];
    readonly Vector3[,] axis=new Vector3[5,3];

    public CinematicHand(Transform hand,SkinnedMeshRenderer skin,Transform character)
    {
        this.hand=hand;string side=hand.name.Substring(hand.name.Length-1);
        Matrix4x4? Bind(Transform t){int i=System.Array.IndexOf(skin.bones,t);return i<0?(Matrix4x4?)null:skin.sharedMesh.bindposes[i].inverse;}
        Vector3 palm=skin.transform.InverseTransformDirection(-character.up).normalized;
        var little=hand.Find("Little1"+side);var mh=Bind(hand);
        for(int f=0;f<5;f++)
        {
            Transform parent=hand;
            for(int k=0;k<3;k++)
            {
                var b=parent.Find(Names[f]+(k+1)+side);if(b==null)break;
                var mb=Bind(b);var mp=Bind(parent);if(mb==null || mp==null)break;
                bones[f,k]=b;bind[f,k]=Quaternion.Inverse(mp.Value.rotation)*mb.Value.rotation;
                var child=b.Find(Names[f]+(k+2)+side);var mc=child!=null?Bind(child):null;
                Vector3 dir=mc!=null?(Vector3)mc.Value.GetColumn(3)-(Vector3)mb.Value.GetColumn(3):(Vector3)mb.Value.GetColumn(3)-(Vector3)mp.Value.GetColumn(3);
                Vector3 towards=palm;
                // the thumb curls across the palm, towards the little finger's knuckle
                if(f==0 && little!=null && mh!=null){var ml=Bind(little);if(ml!=null)towards=(palm+((Vector3)ml.Value.GetColumn(3)-(Vector3)mb.Value.GetColumn(3)).normalized*.8f).normalized;}
                axis[f,k]=Quaternion.Inverse(mp.Value.rotation)*Vector3.Cross(dir.normalized,towards).normalized;
                parent=b;
            }
        }
    }

    // Bends every finger to `angles` (5 x 3, thumb first; the thumb's base only if aimThumb is false), blended from
    // the current pose by weight.
    public void Shape(float[,] angles,float weight,bool aimThumb=false)
    {
        if(weight<=0)return;
        for(int f=0;f<5;f++)for(int k=0;k<3;k++)
        {
            if(f==0 && k==0 && aimThumb)continue;
            var b=bones[f,k];if(b==null)continue;
            b.localRotation=Quaternion.Slerp(b.localRotation,Quaternion.AngleAxis(angles[f,k],axis[f,k])*bind[f,k],weight);
        }
    }
    // Turns the thumb's base so its tip lands on `target` (its outer joints keep the shape already set).
    public void AimThumb(Vector3 target,float weight)
    {
        var t1=bones[0,0];if(t1==null || weight<=0)return;
        var start=t1.localRotation;t1.localRotation=bind[0,0];
        Vector3 tip=Tip(0);
        var aimed=Quaternion.FromToRotation(tip-t1.position,target-t1.position)*t1.rotation;
        t1.rotation=aimed;var full=t1.localRotation;
        t1.localRotation=Quaternion.Slerp(start,full,weight);
    }
    // World position of a finger tip (0 thumb ... 4 little).
    public Vector3 Tip(int f)
    {
        var b3=bones[f,2];var b2=bones[f,1];if(b3==null || b2==null)return hand.position;
        // the last phalanx is about as long as 0.85 of the middle one
        return b3.position+(b3.position-b2.position)*.85f;
    }
    // World position of a finger's base joint (0 thumb ... 4 little).
    public Vector3 Knuckle(int f)=>bones[f,0]!=null?bones[f,0].position:hand.position;
    public Vector3 Middle(int f)=>bones[f,1]!=null?bones[f,1].position:hand.position;
}
