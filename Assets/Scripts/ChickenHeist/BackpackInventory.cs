using UnityEngine;

public class BackpackInventory : MonoBehaviour
{
    public int capacity = 5;
    public int chickensCarried { get; private set; }

    public bool IsFull => chickensCarried >= capacity;

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
