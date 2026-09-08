using UnityEngine;

public class BackpackInventory : MonoBehaviour
{
    public int capacity = 5;
    void Start(){if(GetComponent<PlayerChickenCarry>()==null)gameObject.AddComponent<PlayerChickenCarry>();}
    public int chickensCarried { get; private set; }

    public bool IsFull => chickensCarried >= capacity;
    public void RestoreCount(int count){chickensCarried=Mathf.Clamp(count,0,capacity);if(chickensCarried==0)GetComponent<PlayerChickenCarry>()?.ClearVisual();}
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
        capacity = Mathf.Max(1, capacity + amount);
    }
}
