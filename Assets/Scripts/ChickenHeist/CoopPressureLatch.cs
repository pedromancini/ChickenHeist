using UnityEngine;

// Deterministic mechanics: failure always follows sustained force, never a random roll.
public sealed class CoopPressureLatch
{
    readonly int[] order=new int[3];
    readonly float[] offsets=new float[3];
    public float Pressure {get;private set;}=.5f;
    public float Stress {get;private set;}
    public int Completed {get;private set;}
    public int Selected {get;private set;}
    public int NextPiece=>Completed<3?order[Completed]:-1;
    public float Travel(int piece)=>offsets[Mathf.Clamp(piece,0,2)];
    public float RequiredPressure=>Completed>=3?0:.27f+.17f*NextPiece+.035f*Completed+.12f*offsets[NextPiece];
    public bool HasSlack=>Selected==NextPiece;
    public float Tolerance(bool professional)=>professional?.095f:.062f;
    public bool CanSlide(bool professional)=>HasSlack && Mathf.Abs(Pressure-RequiredPressure)<=Tolerance(professional);
    public CoopPressureLatch(int seed)
    {
        var random=new System.Random(seed);for(int i=0;i<3;i++)order[i]=i;
        for(int i=2;i>0;i--){int j=random.Next(i+1);int hold=order[i];order[i]=order[j];order[j]=hold;}
    }
    // Returns -1 for a noisy slip, +1 when a piece releases, zero otherwise.
    public int Step(float dt,int selected,float axis,bool pull,bool professional)
    {
        if(!float.IsFinite(dt) || !float.IsFinite(axis) || dt<=0 || Completed==3)return 0;
        dt=Mathf.Min(dt,.05f);Selected=Mathf.Clamp(selected,0,2);
        Pressure=Mathf.Clamp01(Pressure+(Mathf.Clamp(axis,-1,1)*.38f-(Mathf.Abs(axis)<.01f?.035f:0))*dt);
        if(!pull){Stress=Mathf.MoveTowards(Stress,0,dt*.65f);return 0;}
        if(CanSlide(professional))
        {
            Stress=Mathf.MoveTowards(Stress,0,dt*.4f);offsets[Selected]=Mathf.Min(1,offsets[Selected]+dt*.48f);
            if(offsets[Selected]>=1){Completed++;Pressure=Mathf.Max(0,Pressure-.09f);return 1;}
        }
        else
        {
            Stress+=dt*(HasSlack?.85f:1.25f);
            if(Stress>=1){Completed=0;Stress=0;Pressure=.5f;System.Array.Clear(offsets,0,3);return -1;}
        }
        return 0;
    }
}
