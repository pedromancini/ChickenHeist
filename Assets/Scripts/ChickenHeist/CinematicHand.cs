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
    // a thin plate's edge in the palm, the fingers past its far face folding onto it: about 95 degrees in all, so the
    // tips rest on the far face (more and they curl back round through the plate)
    public static readonly float[,] Edge={{0,10,15},{48,32,14},{50,32,14},{46,28,12},{38,22,10}};
    // around a bar 2-3 cm thick (a door lever): the little finger closes most
    public static readonly float[,] Bar={{0,12,22},{58,88,50},{66,90,52},{72,90,52},{78,88,50}};

    public readonly Transform hand;
    readonly Transform[,] bones=new Transform[5,3];
    readonly Quaternion[,] bind=new Quaternion[5,3];
    readonly Vector3[,] axis=new Vector3[5,3];
    // the skin of each phalanx (vertices that follow mostly that bone), in the bone's own space: Wrap closes the fingers
    // onto a surface until this skin touches it
    readonly Vector3[,][] surface=new Vector3[5,3][];
    Vector3[] palmSurface;
    // the palm's skin in the hand bone's space (null when the mesh cannot be read)
    public Vector3[] PalmSurface=>palmSurface;

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
        var mesh=skin.sharedMesh;
        if(mesh!=null && mesh.isReadable)
        {
            var vertices=mesh.vertices;var weights=mesh.boneWeights;var poses=mesh.bindposes;
            var lists=new System.Collections.Generic.List<Vector3>[5,3];
            for(int f=0;f<5;f++)for(int k=0;k<3;k++)if(bones[f,k]!=null)lists[f,k]=new System.Collections.Generic.List<Vector3>();
            var palmSkin=new System.Collections.Generic.List<Vector3>();
            for(int v=0;v<vertices.Length;v++)
            {
                var w=weights[v];if(w.weight0<.5f)continue;var bone=skin.bones[w.boneIndex0];
                if(bone==hand){palmSkin.Add(poses[w.boneIndex0].MultiplyPoint3x4(vertices[v]));continue;}
                for(int f=0;f<5;f++)for(int k=0;k<3;k++)if(bones[f,k]==bone)lists[f,k].Add(poses[w.boneIndex0].MultiplyPoint3x4(vertices[v]));
            }
            // at most 120 points a phalanx and 600 for the palm, spread over them
            for(int f=0;f<5;f++)for(int k=0;k<3;k++)if(lists[f,k]!=null)
            {var l=lists[f,k];int step=Mathf.Max(1,l.Count/120);var keep=new System.Collections.Generic.List<Vector3>();for(int i=0;i<l.Count;i+=step)keep.Add(l[i]);surface[f,k]=keep.ToArray();}
            {int step=Mathf.Max(1,palmSkin.Count/600);var keep=new System.Collections.Generic.List<Vector3>();for(int i=0;i<palmSkin.Count;i+=step)keep.Add(palmSkin[i]);palmSurface=keep.ToArray();}
        }
    }
    // Signed distance (metres, negative inside) to a box: `box` maps world space to the box's own, `half` its half size.
    public static float BoxDistance(Matrix4x4 box,Vector3 half,Vector3 world)
    {
        var p=box.MultiplyPoint3x4(world);var q=new Vector3(Mathf.Abs(p.x)-half.x,Mathf.Abs(p.y)-half.y,Mathf.Abs(p.z)-half.z);
        return Vector3.Max(q,Vector3.zero).magnitude+Mathf.Min(Mathf.Max(q.x,Mathf.Max(q.y,q.z)),0);
    }
    // Closes each finger onto a solid (`distance`: signed distance to it), joint by joint from the knuckle out, as far
    // as its skin stays `clearance` off the surface (up to `limit` degrees a joint): the fingers come to rest on the
    // solid instead of curling through it. The thumb's base keeps its aim when skipThumbBase.
    // How far the palm's skin is from a solid (negative: that deep inside it).
    // `deepest`: the palm's point nearest the solid (or furthest inside it).
    public float PalmGap(System.Func<Vector3,float> distance)=>PalmGap(distance,out _);
    public float PalmGap(System.Func<Vector3,float> distance,out Vector3 deepest)
    {
        deepest=hand.position;if(palmSurface==null)return float.PositiveInfinity;var m=hand.localToWorldMatrix;float gap=float.PositiveInfinity;
        foreach(var v in palmSurface){var p=m.MultiplyPoint3x4(v);float d=distance(p);if(d<gap){gap=d;deepest=p;}}
        return gap;
    }
    float FingerGap(int f,int from,System.Func<Vector3,float> distance)
    {
        float gap=float.PositiveInfinity;
        for(int j=from;j<3;j++){var bj=bones[f,j];if(bj==null || surface[f,j]==null)continue;var m=bj.localToWorldMatrix;foreach(var v in surface[f,j])gap=Mathf.Min(gap,distance(m.MultiplyPoint3x4(v)));}
        return gap;
    }
    public void Wrap(System.Func<Vector3,float> distance,float[,] limit,float weight,bool skipThumbBase,float clearance=.003f)
    {
        if(weight<=0)return;
        var start=new Quaternion[5,3];for(int f=0;f<5;f++)for(int k=0;k<3;k++)if(bones[f,k]!=null)start[f,k]=bones[f,k].localRotation;
        for(int f=0;f<5;f++)
        {
            int first=f==0 && skipThumbBase?1:0;
            for(int k=first;k<3;k++)if(bones[f,k]!=null)bones[f,k].localRotation=bind[f,k];
            for(int k=first;k<3;k++)
            {
                var b=bones[f,k];if(b==null || surface[f,k]==null)continue;
                float Gap(float a){b.localRotation=Quaternion.AngleAxis(a,axis[f,k])*bind[f,k];return FingerGap(f,k,distance);}
                // the largest bend that stays clear; a finger that starts inside opens (or closes) the least that frees it,
                // or failing that goes where it is least deep
                float best=0,bestGap=Gap(0);
                if(bestGap>=clearance){for(float a=3;a<=limit[f,k];a+=3){if(Gap(a)<clearance)break;best=a;}}
                else
                {
                    for(float step=3;step<=45;step+=3)
                    {
                        float done=float.NaN;
                        foreach(float a in new[]{-step,step}){if(a>limit[f,k])continue;float g=Gap(a);if(g>=clearance){done=a;break;}if(g>bestGap){bestGap=g;best=a;}}
                        if(!float.IsNaN(done)){best=done;break;}
                    }
                }
                b.localRotation=Quaternion.AngleAxis(best,axis[f,k])*bind[f,k];
            }
            // an aimed thumb that still lies in the solid swings its base out of it, away from the palm
            if(first==1 && bones[f,0]!=null && FingerGap(f,0,distance)<clearance)
            {
                var b0=bones[f,0];var aimed=b0.localRotation;float bestGap=FingerGap(f,0,distance);var bestRotation=aimed;
                for(float a=3;a<=40;a+=3)
                {
                    b0.localRotation=Quaternion.AngleAxis(-a,axis[f,0])*aimed;float g=FingerGap(f,0,distance);
                    if(g>bestGap){bestGap=g;bestRotation=b0.localRotation;}
                    if(g>=clearance)break;
                }
                b0.localRotation=bestRotation;
            }
        }
        if(weight<1)for(int f=0;f<5;f++)for(int k=0;k<3;k++)if(bones[f,k]!=null)bones[f,k].localRotation=Quaternion.Slerp(start[f,k],bones[f,k].localRotation,weight);
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
