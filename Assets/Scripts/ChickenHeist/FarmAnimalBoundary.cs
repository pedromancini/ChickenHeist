using System.Collections.Generic;
using UnityEngine;

public class FarmAnimalBoundary : MonoBehaviour
{
    public Rect area;
    public Rect[] obstacles = new Rect[0];
    public float radius = 0.6f;
    private static readonly List<FarmAnimalBoundary> animals = new List<FarmAnimalBoundary>();

    private void OnEnable() { animals.Add(this); }
    private void OnDisable() { animals.Remove(this); }

    public bool Allows(Vector3 position)
    {
        Vector2 p = new Vector2(position.x, position.z);
        if (!area.Contains(p)) return false;
        foreach (Rect obstacle in obstacles) if (obstacle.Contains(p)) return false;
        foreach (var other in animals)
        {
            if (other == null || other == this) continue;
            Vector3 delta = other.transform.position - position;
            delta.y = 0;
            if (delta.sqrMagnitude < (radius + other.radius) * (radius + other.radius)) return false;
        }
        return true;
    }

    public Vector3 PickTarget(Vector3 from, float distance)
    {
        for (int i = 0; i < 25; i++)
        {
            Vector2 offset = Random.insideUnitCircle * distance;
            Vector3 candidate = from + new Vector3(offset.x, 0, offset.y);
            if (Allows(candidate)) return candidate;
        }
        return from;
    }
}
