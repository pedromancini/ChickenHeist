using System.Collections.Generic;
using UnityEngine;

public class FarmAnimalBoundary : MonoBehaviour
{
    public Rect area;
    public Rect[] obstacles = new Rect[0];
    public float radius = 0.6f;
    private static readonly List<FarmAnimalBoundary> animals = new List<FarmAnimalBoundary>();

    private void OnEnable() { animals.Add(this); }

    // Cows keep their distance by a circle around their pivot, which is not at the middle of the body: use the
    // farthest point of the model from the pivot (head or tail), so two cows never walk into each other.
    private void Start()
    {
        if (GetComponent<SimpleAnimalWander>() == null) return;
        float reach = 0f;
        foreach (var filter in GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null) continue;
            var b = filter.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var local = transform.InverseTransformPoint(filter.transform.TransformPoint(corner));
                reach = Mathf.Max(reach, new Vector2(local.x, local.z).magnitude * transform.lossyScale.x);
            }
        }
        if (reach > 0f) radius = Mathf.Max(radius, reach * .92f);
    }
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
