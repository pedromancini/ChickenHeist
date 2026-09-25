using System.Linq;
using UnityEngine;

public class CoopUpgradeVisibility : MonoBehaviour
{
    GameObject[] oldRoof;int level=-1;
    void Awake(){oldRoof=GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Chapa ondulada rasgada")).Select(t=>t.gameObject).ToArray();}
    void LateUpdate(){int current=HouseholdEconomy.Instance?.Account.CoopLevel??0;if(current==level)return;level=current;foreach(var sheet in oldRoof)if(sheet!=null)sheet.SetActive(level<2);}
}
