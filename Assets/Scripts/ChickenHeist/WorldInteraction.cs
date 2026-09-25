using UnityEngine;

// Choose once per press so independent Update methods cannot use the same E.
public static class WorldInteraction
{
    static int frame=-1;
    static Component selected;
    public static bool Pressed(Component owner)
    {
        if(!Input.GetKeyDown(KeyCode.E))return false;
        if(frame!=Time.frameCount){frame=Time.frameCount;selected=Choose();}
        return selected==owner;
    }
    public static Component Focus => Time.frameCount==frame?selected:RefreshFocus();
    static Component RefreshFocus(){frame=Time.frameCount;selected=Choose();return selected;}
    static MonoBehaviour[] candidates;static float refreshAt;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset(){frame=-1;selected=null;candidates=null;refreshAt=0;}
    static Component Choose()
    {
        var game=HeistGameManager.Instance;var eye=Camera.main;
        if(game?.player==null || eye==null)return null;
        Component result=null;float best=float.PositiveInfinity;
        if(candidates==null || Time.unscaledTime>=refreshAt){candidates=Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);refreshAt=Time.unscaledTime+2;}
        foreach(var item in candidates)
        {
            if(item==null || !item.isActiveAndEnabled)continue;
            Vector3 point=item.transform.position;float range=0;
            if(item is InteractableChicken bird){if(!game.IsMissionTarget(bird) || bird.coop!=null && !bird.coop.IsOpen)continue;range=bird.interactionDistance;point+=Vector3.up*.2f;}
            else if(item is ChickenCoopLockpick coop){point=coop.InteractionPoint;range=coop.interactionDistance;}
            else if(item is RuralGate gate){point=gate.InteractionPoint;range=3.2f;}
            else if(item is HomeDoor){range=2.6f;point+=Vector3.up;}
            else if(item is HomeNextNight rest){range=rest.distance;}
            else if(item is ReceivedTabletDock tablet){if(!tablet.Available)continue;point=tablet.InteractionPoint;range=2;}
            else if(item is HomePhoneDock){if(HouseholdEconomy.Instance?.Account.introSeen==true)continue;range=2;}
            else if(item is OldPickupTruck truck){point=truck.cargoPoint.position;range=2.5f;if(game.backpack.chickensCarried==0)continue;}
            else if(item is VillageMarket market){if(market.counter==null)continue;point=market.counter.position;range=market.range;}
            else if(item is ExtractionZone zone){if(!zone.PlayerInside || !game.CanDeliverHere)continue;point=game.player.position+Vector3.up;range=3;}
            else continue;
            if(Vector3.Distance(game.player.position+Vector3.up*.5f,point)>range+.5f)continue;
            var delta=point-eye.transform.position;float dot=Vector3.Dot(eye.transform.forward,delta.normalized);
            if(dot<.25f)continue;
            bool blocked=false;
            foreach(var hit in Physics.RaycastAll(eye.transform.position,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore))
                if(!hit.transform.IsChildOf(game.player) && !hit.transform.IsChildOf(item.transform)){blocked=true;break;}
            if(blocked)continue;
            float score=delta.magnitude+(1-dot)*5;
            if(score<best){best=score;result=item;}
        }
        return result;
    }
}
