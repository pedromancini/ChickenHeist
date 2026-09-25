using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public static class BackpackEquipmentTests
{
    public static List<string> Run()
    {
        var results=new List<string>();
        void Check(bool value,string name)=>results.Add((value?"PASS ":"FAIL ")+"EQUIPMENT "+name);
        var economy=HouseholdEconomy.Instance;var game=HeistGameManager.Instance;
        var checkpoint=GameMenu.Instance.GetComponent<GameCheckpoint>();var baseline=checkpoint.Capture();
        var movement=game.player.GetComponent<PlayerMovement>();bool movingEnabled=movement.enabled;
        var panel=BackpackPanel.Instance;
        GameObject blocker=null;Transform scare=null;ChickenCoopLockpick coop=null;
        try
        {
            var account=new HouseholdAccount();
            Check(!account.professionalEquipped && !account.professionalLockpick && account.paintUses==0,"new player has basic lockpick and no free paint");
            Check(!account.EquipLockpick(true) && !account.UsePaint(),"unowned equipment and empty spray rejected");
            var legacy=JsonUtility.FromJson<HouseholdAccount>("{\"version\":1,\"day\":1,\"balance\":95,\"debts\":[],\"ledger\":[]}");
            Check(legacy.IsValid() && !legacy.professionalEquipped && legacy.paintUses==0,"old account remains compatible");
            account.balance=500;
            Check(account.Buy(3) && account.balance==320 && account.professionalLockpick && !account.professionalEquipped,"professional purchase costs 180 and waits for equipment choice");
            Check(!account.Buy(3) && account.balance==320,"duplicate permanent purchase rejected");
            Check(account.Buy(4) && account.paintUses==3 && account.balance==290,"one paint can grants exactly three uses for 30");
            Check(account.Buy(4) && account.paintUses==6 && account.balance==260,"paint cans stack without replacing remaining uses");
            Check(account.UsePaint() && account.paintUses==5,"one application consumes one use");
            Check(account.EquipLockpick(true),"professional can be equipped after purchase");
            Check(!new HouseholdAccount{balance=0}.Buy(4),"no purchase without funds");
            Check(!new HouseholdAccount{professionalEquipped=true}.IsValid() && !new HouseholdAccount{paintUses=-1}.IsValid(),"invalid equipment state rejected");
            Check(!new HouseholdAccount{paintUses=int.MaxValue}.Buy(4),"paint count overflow rejected");
            account.regionalSecurity=true;
            economy.RestoreAccount(account);
            Check(economy.Account.professionalEquipped && economy.Account.paintUses==5,"equipment persists in account");
            game.EndActiveMission();game.backpack.RestoreCount(0);
            var camera=Object.FindObjectsByType<SecurityCamera>().First();
            int farm=System.Array.IndexOf(ProtagonistPhone.Instance.farmNames,camera.GetComponentInParent<FarmLayoutInfo>().identity);
            Check(game.StartMission(farm),"camera test mission starts");
            coop=Object.FindObjectsByType<ChickenCoopLockpick>().First(c=>game.IsMissionTarget(c));
            scare=coop.scareChicken;coop.scareChicken=null;
            economy.EquipLockpick(false);float basic=coop.EffectiveSweetSpotWidth;
            coop.RestoreOpen(false);coop.SendMessage("BeginChallenge");
            CoopPressureTests.SolvePiece(coop,basic*1.35f);
            Check(!coop.IsOpen && coop.PinsSet==0,"basic tool cannot slide outside pressure tolerance");
            coop.RestoreOpen(false);coop.SendMessage("BeginChallenge");
            economy.EquipLockpick(true);
            Check(coop.EffectiveSweetSpotWidth>basic,"professional widens pressure tolerance");
            for(int pin=0;pin<3;pin++)
            {
                CoopPressureTests.SolvePiece(coop,basic*1.35f);
                Check(coop.IsOpen==(pin==2),"professional tool still requires all three pins: "+pin);
            }
            // Place only the test player near a real camera, above surrounding fence geometry.
            var cc=game.player.GetComponent<CharacterController>();cc.enabled=false;
            game.player.position=camera.transform.position+Vector3.up*.8f+Vector3.back*1.7f-Vector3.up*1.65f;
            movement.RestorePosture(false);Camera.main.transform.LookAt(camera.transform.position);cc.enabled=true;Physics.SyncTransforms();
            camera.RestorePaint(0);int before=economy.Account.paintUses;
            Check(camera.CanPaintNow(),"visible nearby camera is a valid target");
            if(!camera.CanPaintNow())
            {
                var delta=camera.transform.position-Camera.main.transform.position;
                results.Add("DIAGNOSTIC target "+camera.name+" distance "+delta.magnitude+" aim "+Vector3.Dot(Camera.main.transform.forward,delta.normalized));
                foreach(var hit in Physics.RaycastAll(Camera.main.transform.position,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance))
                    results.Add("DIAGNOSTIC blocker "+hit.transform.name+" distance "+hit.distance+" own "+hit.transform.IsChildOf(camera.transform));
            }
            blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.transform.position=Vector3.Lerp(Camera.main.transform.position,camera.transform.position,.5f);
            blocker.transform.localScale=Vector3.one*.5f;Physics.SyncTransforms();
            Check(!camera.TryPaint() && economy.Account.paintUses==before,"walls prevent painting and do not consume paint");
            Object.DestroyImmediate(blocker);blocker=null;Physics.SyncTransforms();
            Check(BackpackPanel.Spray() && economy.Account.paintUses==before-1 && camera.PaintSecondsRemaining>59,"spray action disables one camera and consumes exactly one use");
            Check(!camera.TryPaint() && economy.Account.paintUses==before-1,"already coated camera cannot waste another use");
            var snapshot=checkpoint.Capture();camera.RestorePaint(0);economy.EquipLockpick(false);
            bool restored=checkpoint.Restore(snapshot,out var restoreMessage);
            Check(restored && economy.Account.professionalEquipped && economy.Account.paintUses==before-1 && camera.PaintSecondsRemaining>59,"checkpoint restores equipped tool, charges and coated cameras together");
            if(!restored)results.Add("DIAGNOSTIC equipment restore: "+restoreMessage);
            camera.RestorePaint(0);economy.Commit(a=>{a.paintUses=0;return true;},"Test");
            Check(!camera.TryPaint() && camera.PaintSecondsRemaining==0,"empty spray cannot disable camera");
            snapshot.cameras.Clear();Check(checkpoint.Restore(snapshot,out _) && camera.PaintSecondsRemaining==0,"legacy checkpoint without camera states loads cleanly");
            Check(panel.Open() && BackpackPanel.IsOpen && GameMenu.BlocksInput && Time.timeScale==0,"B inventory panel pauses and owns gameplay input");
            Check(!DeveloperConsole.Instance.Open(),"developer chat cannot steal inventory focus");
            Check(panel.Equip(false) && !economy.Account.professionalEquipped && panel.Equip(true) && economy.Account.professionalEquipped,"inventory switches both owned lockpicks");
            panel.Close();Check(!BackpackPanel.IsOpen && Time.timeScale==1 && BackpackPanel.BlocksInput,"closing inventory restores time and protects same frame");
        }
        finally
        {
            if(blocker!=null)Object.DestroyImmediate(blocker);
            if(coop!=null)coop.scareChicken=scare;
            panel.Close();checkpoint.Restore(baseline,out _);movement.enabled=movingEnabled;
        }
        return results;
    }
}
