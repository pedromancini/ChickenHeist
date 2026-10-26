using System;
using Unity.InferenceEngine;
using UnityEngine;

public sealed class PalmTracker:IDisposable
{
    const int DetectorSize=192,LandmarkSize=224;
    static readonly int[] FrameJoints={0,1,2,5,6,9,10,13,14,17,18},PalmJoints={0,5,9,13,17};
    public readonly Vector2[] Points=new Vector2[21];
    public readonly Vector3[] Joints=new Vector3[21];
    public bool Found{get;private set;}
    public float Presence{get;private set;}
    public float Handedness{get;private set;}=.5f;
    public float PixelsPerMetre{get;private set;}
    public float PalmWidth{get;private set;}
    public Vector2 PalmPoint{get;private set;}
    public Vector3 PalmJoint{get;private set;}
    public Vector3 Normal{get;private set;}
    public Vector3 Forward{get;private set;}

    struct Affine
    {
        public float a,b,c,d,x,y;
        public static Affine operator*(Affine m,Affine n)=>new Affine{a=m.a*n.a+m.b*n.c,b=m.a*n.b+m.b*n.d,c=m.c*n.a+m.d*n.c,d=m.c*n.b+m.d*n.d,x=m.a*n.x+m.b*n.y+m.x,y=m.c*n.x+m.d*n.y+m.y};
        public Vector2 Apply(float px,float py)=>new Vector2(a*px+b*py+x,c*px+d*py+y);
        public static Affine Move(float tx,float ty)=>new Affine{a=1,d=1,x=tx,y=ty};
        public static Affine Scale(float sx,float sy)=>new Affine{a=sx,d=sy};
        public static Affine Turn(float t){float cos=Mathf.Cos(t),sin=Mathf.Sin(t);return new Affine{a=cos,b=-sin,c=sin,d=cos};}
    }

    readonly Worker detector,landmarker;
    readonly Tensor<float> detectorInput,landmarkerInput;
    readonly float[] detectorData=new float[DetectorSize*DetectorSize*3],landmarkerData=new float[LandmarkSize*LandmarkSize*3];
    readonly Vector2[] anchors=new Vector2[2016];
    bool tracking;

    public PalmTracker()
    {
        var backend=SystemInfo.supportsComputeShaders?BackendType.GPUCompute:BackendType.CPU;
        detector=new Worker(ModelLoader.Load(Resources.Load<ModelAsset>("HandTracking/palm_detector")),backend);
        landmarker=new Worker(ModelLoader.Load(Resources.Load<ModelAsset>("HandTracking/hand_landmarks")),backend);
        detectorInput=new Tensor<float>(new TensorShape(1,DetectorSize,DetectorSize,3));
        landmarkerInput=new Tensor<float>(new TensorShape(1,LandmarkSize,LandmarkSize,3));
        int k=0;
        foreach(var (grid,count) in new[]{(24,2),(12,6)})
            for(int gy=0;gy<grid;gy++)for(int gx=0;gx<grid;gx++)for(int i=0;i<count;i++)anchors[k++]=new Vector2((gx+.5f)/grid,(gy+.5f)/grid);
    }

    public void Reset(){tracking=false;Found=false;}

    public bool Process(Color32[] pixels,int width,int height,bool mirror,bool flip)
    {
        Affine crop;
        if(tracking)crop=CropFromPoints();
        else if(!Detect(pixels,width,height,mirror,flip,out crop)){Found=false;return false;}
        Sample(pixels,width,height,mirror,flip,crop,landmarkerData,LandmarkSize);
        landmarkerInput.Upload(landmarkerData);
        landmarker.Schedule(landmarkerInput);
        var screen=Read(landmarker.PeekOutput("Identity"));
        Presence=Read(landmarker.PeekOutput("Identity_1"))[0];
        if(Presence<.5f)
        {
            bool wasTracking=tracking;tracking=false;Found=false;
            return wasTracking && Process(pixels,width,height,mirror,flip);
        }
        Handedness=Mathf.Lerp(Handedness,Read(landmarker.PeekOutput("Identity_2"))[0],.25f);
        var world=Read(landmarker.PeekOutput("Identity_3"));
        float length=Mathf.Sqrt(crop.a*crop.a+crop.c*crop.c);
        for(int i=0;i<21;i++)
        {
            Points[i]=crop.Apply(screen[3*i],screen[3*i+1]);
            float wx=world[3*i],wy=world[3*i+1];
            Joints[i]=new Vector3((crop.a*wx+crop.b*wy)/length,(crop.c*wx+crop.d*wy)/length,world[3*i+2]);
        }
        Measure();
        tracking=true;Found=true;return true;
    }

    void Measure()
    {
        Vector2 point=Vector2.zero;Vector3 joint=Vector3.zero;
        foreach(int i in PalmJoints){point+=Points[i];joint+=Joints[i];}
        PalmPoint=point/PalmJoints.Length;PalmJoint=joint/PalmJoints.Length;
        Vector2 meanPoint=Vector2.zero,meanJoint=Vector2.zero;
        for(int i=0;i<21;i++){meanPoint+=Points[i];meanJoint+=(Vector2)Joints[i];}
        meanPoint/=21;meanJoint/=21;
        float dot=0,square=0;
        for(int i=0;i<21;i++){var j=(Vector2)Joints[i]-meanJoint;dot+=Vector2.Dot(j,Points[i]-meanPoint);square+=j.sqrMagnitude;}
        PixelsPerMetre=square>1e-9f?dot/square:0;
        var along=Joints[9]-Joints[0];var across=Joints[5]-Joints[17];
        Normal=Vector3.Cross(along,across).normalized*(Handedness>.5f?-1:1);
        Forward=Vector3.ProjectOnPlane(along,Normal).normalized;
        PalmWidth=across.magnitude;
    }

    bool Detect(Color32[] pixels,int width,int height,bool mirror,bool flip,out Affine crop)
    {
        float size=Mathf.Max(width,height),scale=size/DetectorSize;
        var frame=Affine.Move(.5f*(width-size),.5f*(height+size))*Affine.Scale(scale,-scale);
        Sample(pixels,width,height,mirror,flip,frame,detectorData,DetectorSize);
        detectorInput.Upload(detectorData);
        detector.Schedule(detectorInput);
        var boxes=Read(detector.PeekOutput("Identity"));var scores=Read(detector.PeekOutput("Identity_1"));
        int best=0;for(int i=1;i<scores.Length;i++)if(scores[i]>scores[best])best=i;
        crop=default;
        if(1/(1+Mathf.Exp(-Mathf.Clamp(scores[best],-100,100)))<.5f)return false;
        int o=best*18;var anchor=anchors[best]*DetectorSize;
        var centre=anchor+new Vector2(boxes[o],boxes[o+1]);float box=Mathf.Max(boxes[o+2],boxes[o+3]);
        var reach=new Vector2(boxes[o+8]-boxes[o+4],boxes[o+9]-boxes[o+5]);
        float turn=.5f*Mathf.PI-Mathf.Atan2(reach.y,reach.x);
        centre+=.5f*box*reach.normalized;box*=2.6f;
        float k=box/LandmarkSize;
        crop=frame*Affine.Move(centre.x,centre.y)*Affine.Scale(k,-k)*Affine.Turn(turn)*Affine.Move(-.5f*LandmarkSize,-.5f*LandmarkSize);
        return true;
    }

    Affine CropFromPoints()
    {
        var reach=Points[9]-Points[0];
        float turn=.5f*Mathf.PI-Mathf.Atan2(-reach.y,reach.x);
        var axis=new Vector2(Mathf.Cos(turn),Mathf.Sin(turn));var down=new Vector2(-axis.y,axis.x);
        float uMin=float.MaxValue,uMax=float.MinValue,vMin=float.MaxValue,vMax=float.MinValue;
        foreach(int i in FrameJoints)
        {
            float u=Vector2.Dot(Points[i],axis),v=Vector2.Dot(Points[i],down);
            uMin=Mathf.Min(uMin,u);uMax=Mathf.Max(uMax,u);vMin=Mathf.Min(vMin,v);vMax=Mathf.Max(vMax,v);
        }
        float side=Mathf.Max(uMax-uMin,vMax-vMin);
        var centre=(uMin+uMax)*.5f*axis+(vMin+vMax)*.5f*down-.1f*side*down;
        float k=2*side/LandmarkSize;
        return Affine.Move(centre.x,centre.y)*Affine.Scale(k,k)*Affine.Turn(turn)*Affine.Move(-.5f*LandmarkSize,-.5f*LandmarkSize);
    }

    static void Sample(Color32[] pixels,int width,int height,bool mirror,bool flip,Affine m,float[] target,int size)
    {
        int i=0;
        for(int oy=0;oy<size;oy++)for(int ox=0;ox<size;ox++)
        {
            float fx=m.a*ox+m.b*oy+m.x-.5f,fy=m.c*ox+m.d*oy+m.y-.5f;
            if(fx<-.5f || fy<-.5f || fx>width-.5f || fy>height-.5f){target[i++]=0;target[i++]=0;target[i++]=0;continue;}
            int x0=Mathf.FloorToInt(fx),y0=Mathf.FloorToInt(fy);float u=Mathf.Clamp01(fx-x0),v=Mathf.Clamp01(fy-y0);
            int xa=Mathf.Clamp(x0,0,width-1),xb=Mathf.Min(xa+1,width-1),ya=Mathf.Clamp(y0,0,height-1),yb=Mathf.Min(ya+1,height-1);
            if(x0<0)xb=xa;if(y0<0)yb=ya;
            if(mirror){xa=width-1-xa;xb=width-1-xb;}
            if(flip){ya=height-1-ya;yb=height-1-yb;}
            Color32 p00=pixels[ya*width+xa],p10=pixels[ya*width+xb],p01=pixels[yb*width+xa],p11=pixels[yb*width+xb];
            float k00=(1-u)*(1-v)/255f,k10=u*(1-v)/255f,k01=(1-u)*v/255f,k11=u*v/255f;
            target[i++]=p00.r*k00+p10.r*k10+p01.r*k01+p11.r*k11;
            target[i++]=p00.g*k00+p10.g*k10+p01.g*k01+p11.g*k11;
            target[i++]=p00.b*k00+p10.b*k10+p01.b*k01+p11.b*k11;
        }
    }

    static float[] Read(Tensor tensor)=>((Tensor<float>)tensor).DownloadToArray();

    public void Dispose()
    {
        detector?.Dispose();landmarker?.Dispose();detectorInput?.Dispose();landmarkerInput?.Dispose();
    }
}
