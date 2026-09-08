using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object=UnityEngine.Object;

public static class HiddenMarketTests
{
    public static IEnumerable<string> Check(VillageMarket market,Transform home)
    {
        var result=new List<string>();
        result.Add((Vector3.Distance(market.transform.position,home.position)>200?"PASS ":"FAIL ")+"Hidden market is over 200m from protagonist home");
        result.Add((market.GetComponentsInChildren<RuralTreeRoots>().Length>=15?"PASS ":"FAIL ")+"Market has dedicated forest screening");
        var spans=market.GetComponentsInChildren<RuralRoadSpan>();
        var terrain=RuralTreeRoots.TerrainSurfaces();
        bool connected=spans.Length>=8,grounded=true,walkable=true;
        var blockers=new HashSet<string>();
        var probe=new GameObject("Temporary market route walker");
        var cc=probe.AddComponent<CharacterController>();cc.height=2;cc.radius=.32f;cc.center=Vector3.up;cc.stepOffset=.3f;cc.slopeLimit=45;
        try
        {
            if(spans.Length==0)walkable=false;
            else
            {
                Vector3 start=spans[0].start;
                RuralTreeRoots.SurfaceHeight(terrain,start,out float startY);
                cc.enabled=false;probe.transform.position=new Vector3(start.x,startY+.06f,start.z);cc.enabled=true;
                for(int i=0;i<spans.Length;i++)
                {
                    var span=spans[i];
                    if(i>0 && Vector3.Distance(spans[i-1].end,span.start)>.05f)connected=false;
                    int count=Mathf.CeilToInt(Vector3.Distance(span.start,span.end)/.18f);
                    for(int n=1;n<=count;n++)
                    {
                        Vector3 p=Vector3.Lerp(span.start,span.end,n/(float)count);
                        if(!RuralTreeRoots.SurfaceHeight(terrain,p,out float y)){grounded=false;continue;}
                        Vector3 move=p-probe.transform.position;move.y=0;move=Vector3.ClampMagnitude(move,.22f);
                        cc.Move(move+Vector3.down*.08f);
                        Vector3 delta=probe.transform.position-p;delta.y=0;
                        if(delta.magnitude>1)
                        {
                            blockers.Add("segment "+i+" probe="+probe.transform.position+" target="+p+" ground="+y);
                            walkable=false;
                            foreach(var obstacle in Physics.OverlapCapsule(probe.transform.position+Vector3.up*.35f,probe.transform.position+Vector3.up*1.65f,.6f,~0,QueryTriggerInteraction.Ignore))
                                if(obstacle!=cc && !terrain.Contains(obstacle))blockers.Add(obstacle.name+" at "+obstacle.transform.position);
                            break;
                        }
                    }
                    if(!walkable)break;
                }
                result.Add((Vector3.Distance(probe.transform.position+Vector3.up,market.counter.position)<market.range?"PASS ":"FAIL ")+"Walking route reaches merchant interaction range");
            }
        }
        finally{Object.DestroyImmediate(probe);}
        result.Add((connected?"PASS ":"FAIL ")+"Dirt trail spans form a connected route");
        result.Add((grounded?"PASS ":"FAIL ")+"Trail remains on terrain");
        result.Add((walkable?"PASS ":"FAIL ")+"Standing character can walk entire market trail"+(blockers.Count>0?": "+string.Join(", ",blockers):""));
        foreach(var farm in Object.FindObjectsByType<FarmLayoutInfo>(FindObjectsSortMode.None))
        {
            var coop=farm.GetComponentsInChildren<ChickenCoopLockpick>();
            bool valid=coop.Length==1 && coop[0].door!=null && !coop[0].IsOpen
                && farm.GetComponentInChildren<FarmerSleepSystem>()!=null
                && farm.GetComponentsInChildren<InteractableChicken>().All(c=>c.coop==coop[0]);
            result.Add((valid?"PASS ":"FAIL ")+farm.identity+": farmer, closed coop and chicken ownership");
        }
        return result;
    }
}
