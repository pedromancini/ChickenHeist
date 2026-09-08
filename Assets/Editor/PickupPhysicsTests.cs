using System.Collections.Generic;
using UnityEngine;

public static class PickupPhysicsTests
{
    public static List<string> Run(OldPickupTruck truck)
    {
        var result=new List<string>();void Check(bool ok,string message)=>result.Add((ok?"PASS ":"FAIL ")+"VEHICLE PHYSICS "+message);
        var mode=Physics.simulationMode;var vehicle=truck.vehicle;var body=vehicle.Body;
        var start=body.position;var rotation=body.rotation;
        var objects=new List<GameObject>();
        void Step(float gas,float steer,bool brake,int steps)
        {
            truck.Drive(gas,steer,brake,.02f);
            for(int i=0;i<steps;i++){vehicle.PhysicsStep(.02f);Physics.Simulate(.02f);}
        }
        try
        {
            Physics.simulationMode=SimulationMode.Script;
            Step(0,0,true,100);
            Check(vehicle.GroundedWheels==4,"four tires support the parked truck");
            var departure=body.position;Step(1,0,false,130);
            Check(truck.Speed>2 && Vector3.Dot(body.position-departure,rotation*Vector3.forward)>4.5f,"rear wheel torque drives through home opening");
            float before=body.linearVelocity.magnitude;Step(0,0,true,100);
            Check(before>2 && body.linearVelocity.magnitude<.4f,"wheel brakes stop an accelerating truck");
            // A private flat pad avoids relying on a particular procedural road
            // for steering, reverse, collision and uneven-ground regression tests.
            var pad=GameObject.CreatePrimitive(PrimitiveType.Cube);objects.Add(pad);pad.transform.position=new Vector3(0,199.5f,0);pad.transform.localScale=new Vector3(100,1,100);
            vehicle.ResetPose(new Vector3(0,200.1f,0),Quaternion.identity);Step(0,0,true,100);
            Step(-1,0,false,90);Check(truck.Speed<-.5f,"reverse applies actual negative wheel torque");
            Step(0,0,true,90);vehicle.ResetPose(new Vector3(0,200.1f,0),Quaternion.identity);Step(0,0,true,80);
            Step(1,.6f,false,130);
            Check(Mathf.Abs(Mathf.DeltaAngle(0,body.rotation.eulerAngles.y))>8 && Vector3.Dot(body.rotation*Vector3.up,Vector3.up)>.9f,"tires steer a stable arc without rotating the chassis directly");
            Check(vehicle.axles[1].steerAngle>vehicle.axles[0].steerAngle,"inside front tire uses tighter Ackermann steering angle");
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);objects.Add(wall);wall.transform.position=new Vector3(0,201,8);wall.transform.localScale=new Vector3(15,2,1);
            vehicle.ResetPose(new Vector3(0,200.1f,0),Quaternion.identity);Step(0,0,true,80);Step(1,0,false,230);
            Check(body.position.z<6.1f && body.linearVelocity.magnitude<1.5f,"compound body collision stops front bumper at wall");
            var uneven=GameObject.CreatePrimitive(PrimitiveType.Cube);objects.Add(uneven);uneven.transform.position=new Vector3(-.84f,200.065f,1.4f);uneven.transform.localScale=new Vector3(.62f,.13f,1.1f);
            vehicle.ResetPose(new Vector3(0,200.35f,0),Quaternion.identity);Step(0,0,true,160);
            Check(vehicle.GroundedWheels>=3 && Vector3.Dot(body.rotation*Vector3.up,Vector3.up)>.95f,"independent springs settle on uneven ground without rolling over");
            vehicle.axles[0].GetWorldPose(out var frontLeft,out _);vehicle.axles[1].GetWorldPose(out var frontRight,out _);
            Check(Mathf.Abs(frontLeft.y-frontRight.y)>.05f,"individual wheel poses follow different ground heights");
            result.Add("DIAGNOSTIC suspension wheel heights "+frontLeft.y.ToString("F3")+" / "+frontRight.y.ToString("F3"));
        }
        finally
        {
            foreach(var o in objects)Object.DestroyImmediate(o);
            vehicle.ResetPose(start,rotation);Physics.simulationMode=mode;
        }
        return result;
    }
}
