using UnityEngine;

public class BackpackInventory : MonoBehaviour
{
    public int capacity = 1;
    void Awake(){capacity=1;}
    void Start(){if(GetComponent<PlayerChickenCarry>()==null)gameObject.AddComponent<PlayerChickenCarry>();}
    public int chickensCarried { get; private set; }

    public bool IsFull => chickensCarried >= 1;
    public void RestoreCount(int count){capacity=1;chickensCarried=Mathf.Clamp(count,0,1);if(chickensCarried==0)GetComponent<PlayerChickenCarry>()?.ClearVisual();}
    public bool RemoveChickens(int quantity)
    {
        if(quantity<1 || quantity>chickensCarried)return false;
        chickensCarried-=quantity;
        if(chickensCarried==0)GetComponent<PlayerChickenCarry>()?.ClearVisual();
        return true;
    }

    public bool TryAddChicken()
    {
        if (IsFull)
            return false;

        chickensCarried++;
        return true;
    }

    public void UpgradeCapacity(int amount)
    {
        capacity = 1;
    }
}
