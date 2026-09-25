using System;
using UnityEngine;
using UnityEngine.AI;

public enum FarmerActivity { Sleeping,Waking,Leaving,Investigating,Chasing,Aiming,Reloading,Returning }

[Serializable]
public class FarmerCombatSave
{
    public FarmerActivity activity;
    public int exitStep,ammo;
    public Vector3 lastKnown;
    public float actionRemaining,memoryRemaining;
    public bool doorOpen;
    public bool Valid=>Enum.IsDefined(typeof(FarmerActivity),activity) && exitStep>=0 && exitStep<=2 && ammo>=0 && ammo<=2 &&
        float.IsFinite(lastKnown.x) && float.IsFinite(lastKnown.y) && float.IsFinite(lastKnown.z) &&
        float.IsFinite(actionRemaining) && actionRemaining>=0 && actionRemaining<=10 && float.IsFinite(memoryRemaining) && memoryRemaining>=0 && memoryRemaining<=25;
}

[RequireComponent(typeof(CharacterController))]
public class FarmerStateMachine : MonoBehaviour
{
    public FarmerSleepSystem sleepSystem;
    public Transform player;
    public Transform[] patrolPoints;
    public float patrolSpeed=1.8f,chaseSpeed=3.6f,catchDistance=1.8f;
    public FarmerActivity Activity {get;private set;}
    public bool HasVisualContact {get;private set;}
    public int Shells {get;private set;}=2;
    public Vector3 LastKnownPosition=>lastKnown;
    public float AimProgress=>Activity==FarmerActivity.Aiming?Mathf.Clamp01(1-(actionUntil-Time.time)/1.35f):0;
    public bool Moving=>controller!=null && new Vector2(controller.velocity.x,controller.velocity.z).sqrMagnitude>.04f;
    CharacterController controller;FarmerResidence home;FarmerShotgun gun;
    NavMeshPath path;int corner,exitStep;
    Vector3 lastKnown,previousGoal,vertical;
    float actionUntil,memoryUntil,nextPath;
    readonly RaycastHit[] hits=new RaycastHit[64];
    void Awake()
    {
        controller=GetComponent<CharacterController>();home=GetComponent<FarmerResidence>();path=new NavMeshPath();
        gun=GetComponent<FarmerShotgun>();if(gun==null)gun=gameObject.AddComponent<FarmerShotgun>();
    }
    public void Hear(Vector3 position){lastKnown=position;memoryUntil=Time.time+20;}
    public void ResetForNewMission(){HasVisualContact=false;nextPath=0;}
    public FarmerCombatSave Capture()=>new FarmerCombatSave{activity=Activity,exitStep=exitStep,ammo=Shells,lastKnown=lastKnown,
        actionRemaining=Mathf.Clamp(actionUntil-Time.time,0,10),memoryRemaining=Mathf.Clamp(memoryUntil-Time.time,0,25),doorOpen=home!=null && home.DoorOpen};
    public void Restore(FarmerCombatSave state)
    {
        if(home==null)home=GetComponent<FarmerResidence>();
        HasVisualContact=false;vertical=Vector3.zero;nextPath=0;path.ClearCorners();corner=0;
        if(state==null)
        {Activity=home!=null && home.AtBed?FarmerActivity.Sleeping:FarmerActivity.Returning;Shells=2;exitStep=0;actionUntil=memoryUntil=0;return;}
        Activity=state.activity;exitStep=state.exitStep;Shells=state.ammo;lastKnown=state.lastKnown;
        actionUntil=Time.time+state.actionRemaining;memoryUntil=Time.time+state.memoryRemaining;home?.SetDoor(state.doorOpen,true);
    }
    void Update()
    {
        var game=HeistGameManager.Instance;
        if(GameMenu.BlocksInput || game==null || game.missionEnded || sleepSystem==null || player==null)return;
        if(home==null || home.bedPosition==null)return;
        if(!game.IsMissionFarmer(sleepSystem))
        {
            HasVisualContact=false;
            if(home.AtBed){Activity=FarmerActivity.Sleeping;home.SetDoor(false);Shells=2;return;}
            Activity=FarmerActivity.Returning;ReturnHome();return;
        }
        if(Activity==FarmerActivity.Sleeping)
        {
            if(sleepSystem.CurrentSleep<FarmerSleepSystem.WakeThreshold)return;
            Activity=FarmerActivity.Waking;actionUntil=Time.time+2.6f;memoryUntil=Mathf.Max(memoryUntil,Time.time+20);return;
        }
        if(Activity==FarmerActivity.Waking)
        {
            if(Time.time>=actionUntil){Activity=FarmerActivity.Leaving;exitStep=0;home.SetDoor(true);}
            return;
        }
        if(Activity==FarmerActivity.Leaving)
        {
            var target=exitStep==0?home.insideDoor:home.outsideDoor;
            if(exitStep==1 && !home.DoorReady)return;
            if(MoveToward(target.position,patrolSpeed)){exitStep++;if(exitStep>=2){Activity=FarmerActivity.Investigating;nextPath=0;}}
            return;
        }
        HasVisualContact=CanSeePlayer();sleepSystem.SetVisualContact(HasVisualContact);
        if(HasVisualContact){lastKnown=PlayerTarget;memoryUntil=Time.time+18;}
        if(Activity==FarmerActivity.Reloading)
        {
            if(Time.time>=actionUntil){Shells=2;Activity=FarmerActivity.Investigating;}
            return;
        }
        if(HasVisualContact)
        {
            Face(lastKnown);
            if(Vector3.Distance(transform.position,player.position)>25){Activity=FarmerActivity.Chasing;MoveToward(lastKnown,chaseSpeed);return;}
            if(Activity!=FarmerActivity.Aiming)
            {Activity=FarmerActivity.Aiming;actionUntil=Time.time+1.35f;game.ShowMessage("O fazendeiro esta mirando. Procure cobertura!",1.3f);}
            if(Time.time>=actionUntil)
            {
                gun.Fire(lastKnown);Shells--;Activity=Shells==0?FarmerActivity.Reloading:FarmerActivity.Aiming;
                actionUntil=Time.time+(Shells==0?3.2f:1.7f);
            }
            Gravity();return;
        }
        // Losing sight cancels the shot; search uses only the last observed/heard point.
        if(Time.time<memoryUntil){Activity=FarmerActivity.Investigating;MoveToward(lastKnown,patrolSpeed);}
        else{Activity=FarmerActivity.Returning;ReturnHome();}
    }
    Vector3 PlayerTarget=>player.position+Vector3.up*(player.GetComponent<PlayerMovement>()?.estaAgachado==true?.65f:1.35f);
    public bool CanSeePlayer()
    {
        if(player==null)return false;
        var delta=PlayerTarget-(transform.position+Vector3.up*1.55f);
        if(delta.magnitude>32 || Vector3.Angle(transform.forward,new Vector3(delta.x,0,delta.z))>75)return false;
        return ClearSight(transform.position+Vector3.up*1.55f,PlayerTarget);
    }
    public bool ClearSight(Vector3 origin,Vector3 target)
    {
        var delta=target-origin;int count=Physics.RaycastNonAlloc(origin,delta.normalized,hits,delta.magnitude,~0,QueryTriggerInteraction.Ignore);
        if(count==hits.Length)return false;
        for(int i=0;i<count;i++)if(!hits[i].transform.IsChildOf(transform) && !hits[i].transform.IsChildOf(player))return false;
        return true;
    }
    void ReturnHome()
    {
        home.SetDoor(true);
        if(!home.DoorReady)return;
        if(MoveToward(home.bedPosition.position,patrolSpeed))
        {Activity=FarmerActivity.Sleeping;Shells=2;home.SetDoor(false);sleepSystem.RestoreSleep(sleepSystem.minimumSleep);}
    }
    bool MoveToward(Vector3 goal,float speed)
    {
        Vector3 flat=goal-transform.position;flat.y=0;
        if(flat.magnitude<.20f){Gravity();return true;}
        if(Time.time>=nextPath || Vector3.Distance(previousGoal,goal)>1)
        {
            nextPath=Time.time+.7f;previousGoal=goal;corner=1;path.ClearCorners();
            if(NavMesh.SamplePosition(transform.position,out var start,1.2f,NavMesh.AllAreas) && NavMesh.SamplePosition(goal,out var end,2f,NavMesh.AllAreas))
                NavMesh.CalculatePath(start.position,end.position,NavMesh.AllAreas,path);
        }
        var corners=path.corners;
        if(corners.Length<2){Gravity();return false;}
        while(corner<corners.Length-1 && Vector3.Distance(transform.position,corners[corner])<.4f)corner++;
        Vector3 direction=corners[Mathf.Min(corner,corners.Length-1)]-transform.position;direction.y=0;
        Face(transform.position+direction);controller.Move(direction.normalized*Mathf.Min(speed*Time.deltaTime,direction.magnitude));Gravity();return false;
    }
    void Face(Vector3 point){var d=point-transform.position;d.y=0;if(d.sqrMagnitude>.001f)transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(d),240*Time.deltaTime);}
    void Gravity(){if(!controller.enabled)return;if(controller.isGrounded && vertical.y<0)vertical.y=-1;vertical.y+=Physics.gravity.y*Time.deltaTime;controller.Move(vertical*Time.deltaTime);}
}
