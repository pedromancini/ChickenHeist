using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class FarmProgressionTests
{
    public static List<string> Run()
    {
        var results=new List<string>();
        void Check(bool value,string name)=>results.Add((value?"PASS ":"FAIL ")+"PROGRESSION "+name);
        var game=HeistGameManager.Instance;var economy=HouseholdEconomy.Instance;
        var checkpoint=GameMenu.Instance.GetComponent<GameCheckpoint>();var baseline=checkpoint.Capture();
        try
        {
            var account=new HouseholdAccount();
            Check(!account.regionalSecurity && !account.newsUnread,"new region has no defenses or robbery news");
            account.RestUntilMorning();
            Check(account.day==2 && !account.regionalSecurity && account.news.Count==0,"peaceful sleep does not install defenses");
            account.RegisterRaid("Fazenda Teste",2,false);
            Check(!account.regionalSecurity && account.news.Count==0 && account.pendingRaids.Count==1,"robbery remains pending until sleep");
            account=JsonUtility.FromJson<HouseholdAccount>(JsonUtility.ToJson(account));
            account.RestUntilMorning();
            Check(account.regionalSecurity && account.newsUnread && account.news.Count==1 && account.news[0].chickens==2,"sleep publishes persisted robbery and protects entire region");
            Check(!account.news[0].Story.Contains("todas as galinhas"),"partial robbery never claims an empty coop");
            account.RestUntilMorning();Check(account.news.Count==1,"later peaceful sleep cannot duplicate news");
            account.RegisterRaid("Outro Sitio",3,true);account.RestUntilMorning();
            Check(account.news[0].Story.Contains("todas as galinhas"),"empty coop uses all-chickens headline detail");

            economy.RestoreAccount(new HouseholdAccount());game.EndActiveMission();game.backpack.RestoreCount(0);
            var cameras=Object.FindObjectsByType<SecurityCamera>();var traps=Object.FindObjectsByType<TrapSystem>();
            Check(cameras.Length>0 && traps.Length>0,"world contains authored upgrade placements");
            Check(cameras.All(c=>!c.GetComponent<Renderer>().enabled && !c.GetComponent<Collider>().enabled),"all cameras physically absent in untouched region");
            Check(traps.All(c=>!c.GetComponent<Renderer>().enabled && !c.GetComponent<Collider>().enabled),"all traps physically absent in untouched region");
            Check(cameras.All(c=>!c.CanPaintNow()),"absent cameras cannot consume equipment");
            Check(game.StartMission(0),"first unprotected farm can be selected");
            game.backpack.RestoreCount(2);game.CompleteMission();
            Check(!economy.Account.regionalSecurity && economy.Account.pendingRaids.Count==1,"real home delivery records robbery without early defenses");
            Check(game.PrepareNextNight() && economy.Account.regionalSecurity && economy.Account.newsUnread,"real rest path installs defenses and queues phone notification");
            Check(!game.PrepareNextNight(),"same rest cannot advance the day twice");
            Check(cameras.All(c=>c.GetComponent<Renderer>().enabled && c.GetComponent<Collider>().enabled) && traps.All(c=>c.GetComponent<Renderer>().enabled && c.GetComponent<Collider>().enabled),"all regional farms receive physical protection after rest");
            var saved=checkpoint.Capture();economy.RestoreAccount(new HouseholdAccount());
            Check(checkpoint.Restore(saved,out _) && economy.Account.regionalSecurity && economy.Account.newsUnread,"checkpoint restores security and unread newspaper together");
            ProtagonistPhone.Instance.SetOpen(true);
            Check(!economy.Account.newsUnread && economy.Account.news.Count==1,"opening unread news acknowledges without removing article");
            ProtagonistPhone.Instance.SetOpen(false);
            var carry=game.player.GetComponent<PlayerChickenCarry>();
            Check(carry!=null,"player has physical chicken carry presentation");
            var bird=Object.FindObjectsByType<InteractableChicken>().First();
            carry.Lift(bird);
            Check(carry.HasVisual && carry.IsLifting,"pickup creates visible bird and begins lift animation");
            Check(carry.GetComponentsInChildren<InteractableChicken>().Length==0,"carried visual cannot duplicate stealable birds");
            carry.ClearVisual();
        }
        finally {ProtagonistPhone.Instance.SetOpen(false);checkpoint.Restore(baseline,out _);}
        return results;
    }
}
