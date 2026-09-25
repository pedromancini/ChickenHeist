using System;
using System.Collections.Generic;
using UnityEngine;

// Attached only by the isolated Editor review, never installed in the scene.
public class FrameTimingReview : MonoBehaviour
{
    readonly List<float> samples=new List<float>(5000);
    int collections;
    public void Begin(){samples.Clear();collections=GC.CollectionCount(0);enabled=true;}
    void Update(){samples.Add(Time.unscaledDeltaTime*1000);}
    public string Report()
    {
        enabled=false;var sorted=samples.ToArray();Array.Sort(sorted);
        if(sorted.Length==0)return "No frame samples";
        float total=0;int slow=0;foreach(float ms in sorted){total+=ms;if(ms>16.67f)slow++;}
        return "Samples: "+sorted.Length+"\nAverage FPS: "+(1000*sorted.Length/total).ToString("F1")+
            "\nMedian ms: "+sorted[sorted.Length/2].ToString("F2")+
            "\nP95 ms: "+sorted[(int)((sorted.Length-1)*.95f)].ToString("F2")+
            "\nP99 ms: "+sorted[(int)((sorted.Length-1)*.99f)].ToString("F2")+
            "\nFrames above 16.67 ms: "+slow+"\nGen0 collections: "+(GC.CollectionCount(0)-collections)+
            "\nGPU: "+SystemInfo.graphicsDeviceName+"\nCPU: "+SystemInfo.processorType+
            "\nResolution: "+Screen.width+"x"+Screen.height+"\nTarget: "+Application.targetFrameRate+" VSync: "+QualitySettings.vSyncCount+
            "\nEditor measurement, not standalone certification.";
    }
}
