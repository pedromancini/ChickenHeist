using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class HouseholdDebt
{
    public string label;
    public int amount;
    public int dueDay;
    public HouseholdDebt(string label, int amount, int dueDay)
    { this.label=label; this.amount=amount; this.dueDay=dueDay; }
}

[Serializable]
public class HouseholdAccount
{
    public int version=1, day=1, balance=95, feed=0, boards=0, flock=0, meals=0, repairs=0;
    public bool backpackUpgrade;
    public bool professionalLockpick, professionalEquipped;
    public int paintUses;
    public int truckCages, truckChickens;
    public const int TruckLimit=8;
    public int TruckCapacity=>truckCages*2;
    public bool regionalSecurity, newsUnread;
    public List<FarmRaidNews> pendingRaids = new List<FarmRaidNews>();
    public List<FarmRaidNews> news = new List<FarmRaidNews>();
    public void RegisterRaid(string farm, int chickens, bool emptied)
    {
        if(string.IsNullOrEmpty(farm) || chickens < 1)return;
        if(pendingRaids==null)pendingRaids=new List<FarmRaidNews>();
        var report=pendingRaids.Find(r=>r.farm==farm);
        if(report==null){report=new FarmRaidNews{farm=farm};pendingRaids.Add(report);}
        report.chickens+=chickens;report.emptied|=emptied;
    }
    public void RestUntilMorning()
    {
        day++;
        if(pendingRaids==null)pendingRaids=new List<FarmRaidNews>();
        if(news==null)news=new List<FarmRaidNews>();
        foreach(var report in pendingRaids){report.day=day;news.Insert(0,report);}
        if(pendingRaids.Count>0){regionalSecurity=true;newsUnread=true;}
        pendingRaids.Clear();
        if(news.Count>20)news.RemoveRange(20,news.Count-20);
        Record("Descansou ate a manha");
    }
    public const int PaintUsesPerCan=3;
    public const int ProductCount=6;
    public static string ProductName(int id)=>id==0?"Racao - 5 porcoes":id==1?"Kit de tabuas e pregos":id==2?"Mochila reforcada +3":id==3?"Lockpick profissional":id==4?"Tinta spray - 3 usos":id==5?"Gaiola para caminhonete - 2 galinhas":"Produto invalido";
    public static int ProductPrice(int id)=>id==0?25:id==1?35:id==2?90:id==3?180:id==4?30:id==5?100:0;
    public bool OwnsUniqueProduct(int id)=>(id==2 && backpackUpgrade) || (id==3 && professionalLockpick);
    public bool CanBuy(int id)=>ProductPrice(id)>0 && balance>=ProductPrice(id) && !OwnsUniqueProduct(id)
        && (id!=4 || paintUses<=int.MaxValue-PaintUsesPerCan) && (id!=5 || truckCages<4);
    public List<HouseholdDebt> debts=new List<HouseholdDebt> {
        new HouseholdDebt("Energia atrasada",160,1),
        new HouseholdDebt("Cooperativa - racao",230,3),
        new HouseholdDebt("Parcela do sitio",480,7) };
    public List<string> ledger=new List<string>{"Dia 1 | Saldo restante: R$ 95"};

    public bool IsValid() => version==1 && day>0 && balance>=0 && feed>=0 && boards>=0 && flock>=0 && meals>=0 && repairs>=0
        && truckCages>=0 && truckCages<=4 && truckChickens>=0 && truckChickens<=TruckCapacity
        && paintUses>=0 && (!professionalEquipped || professionalLockpick)
        && debts!=null && ledger!=null && debts.TrueForAll(d=>d!=null && d.amount>=0 && d.dueDay>0);
    public void Record(string message) { ledger.Insert(0,"Dia "+day+" | "+message); if(ledger.Count>40)ledger.RemoveAt(40); }
    public bool Buy(int product)
    {
        int price=ProductPrice(product);
        if(!CanBuy(product))return false;
        balance-=price;
        if(product==0)feed++; else if(product==1)boards++; else if(product==2)backpackUpgrade=true;
        else if(product==3)professionalLockpick=true;else if(product==4)paintUses+=PaintUsesPerCan;else truckCages++;
        Record("- R$ "+price+" | "+ProductName(product)); return true;
    }
    public bool EquipLockpick(bool professional)
    {
        if(professional && !professionalLockpick)return false;
        professionalEquipped=professional;return true;
    }
    public bool UsePaint()
    {
        if(paintUses<1)return false;
        paintUses--;Record("Tinta aplicada na camera | Restam "+paintUses+" usos");return true;
    }
    public bool Pay(int index)
    {
        if(index<0 || index>=debts.Count)return false;
        var debt=debts[index]; if(debt.amount==0 || balance<debt.amount)return false;
        balance-=debt.amount; Record("- R$ "+debt.amount+" | "+debt.label); debt.amount=0; return true;
    }
    public bool Feed()
    {
        if(feed<1 || flock<1 || meals>=flock)return false;
        feed--; meals+=5; Record("Comedouro abastecido: +5 porcoes"); return true;
    }
    public bool Repair()
    {
        if(boards<1 || repairs>=3)return false;
        boards--;repairs++;Record("Galinheiro remendado: etapa "+repairs+"/3");return true;
    }
    public bool Sell()
    {
        return Sell(1);
    }
    public bool Sell(int quantity)
    {
        if(quantity<1 || quantity>flock || quantity>(int.MaxValue-balance)/45)return false;
        flock-=quantity;balance+=45*quantity;Record("+ R$ "+45*quantity+" | Venda de "+quantity+" galinha(s)");return true;
    }
    public void ReturnFromHeist(int chickens)
    {
        int fed=Mathf.Min(meals,flock); meals-=fed;
        if(fed>0){balance+=fed*6;Record("+ R$ "+fed*6+" | Ovos vendidos na cooperativa");}
        flock+=Mathf.Max(0,chickens);Record("Retorno ao sitio: +"+chickens+" galinhas");
    }
}
