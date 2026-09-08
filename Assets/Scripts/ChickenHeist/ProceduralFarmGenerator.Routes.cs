using System.Collections.Generic;
using UnityEngine;

public partial class ProceduralFarmGenerator
{
    private void RouteAroundFarms(Vector2 start, Vector2 end)
    {
        var obstacles = new List<Rect>();
        for (int row = 0; row < farmRows; row++)
        for (int col = 0; col < farmColumns; col++) obstacles.Add(ExpandRect(GetLotRect(col, row), pathWidth * 0.55f));
        start = PushOutside(start, obstacles);
        end = PushOutside(end, obstacles);
        var nodes = new List<Vector2> { start, end };
        foreach (Rect obstacle in obstacles)
        {
            Rect r = ExpandRect(obstacle, 0.2f);
            nodes.Add(new Vector2(r.xMin,r.yMin)); nodes.Add(new Vector2(r.xMax,r.yMin));
            nodes.Add(new Vector2(r.xMin,r.yMax)); nodes.Add(new Vector2(r.xMax,r.yMax));
        }
        float[] cost = new float[nodes.Count];
        int[] previous = new int[nodes.Count];
        bool[] visited = new bool[nodes.Count];
        for (int i = 0; i < nodes.Count; i++) { cost[i] = float.PositiveInfinity; previous[i] = -1; }
        cost[0] = 0;
        for (int step = 0; step < nodes.Count; step++)
        {
            int current = -1;
            for (int i = 0; i < nodes.Count; i++)
                if (!visited[i] && (current < 0 || cost[i] < cost[current])) current = i;
            if (current < 0 || float.IsInfinity(cost[current])) break;
            if (current == 1) break;
            visited[current] = true;
            for (int j = 0; j < nodes.Count; j++)
            {
                if (visited[j] || j == current) continue;
                float nextCost = cost[current] + Vector2.Distance(nodes[current], nodes[j]);
                if (nextCost >= cost[j]) continue;
                bool blocked = false;
                foreach (Rect obstacle in obstacles)
                    if (SegmentIntersectsRect(nodes[current], nodes[j], obstacle)) { blocked = true; break; }
                if (!blocked) { cost[j] = nextCost; previous[j] = current; }
            }
        }
        if (previous[1] < 0) { Debug.LogWarning("Rural route could not connect endpoints."); return; }
        var route = new List<int>();
        for (int p = 1; p >= 0; p = previous[p]) route.Add(p);
        for (int i = route.Count - 1; i > 0; i--)
        {
            Vector2 a = nodes[route[i]], b = nodes[route[i-1]];
            dirtPathSegments.Add(new Vector4(a.x,a.y,b.x,b.y));
        }
    }

    private static Vector2 PushOutside(Vector2 p, List<Rect> obstacles)
    {
        foreach (Rect r in obstacles)
        {
            if (!r.Contains(p)) continue;
            float[] distances = {p.x-r.xMin,r.xMax-p.x,p.y-r.yMin,r.yMax-p.y};
            int side = 0;
            for (int i = 1; i < 4; i++) if (distances[i] < distances[side]) side = i;
            if (side == 0) p.x = r.xMin - 0.2f;
            else if (side == 1) p.x = r.xMax + 0.2f;
            else if (side == 2) p.y = r.yMin - 0.2f;
            else p.y = r.yMax + 0.2f;
        }
        return p;
    }

    public static bool SegmentIntersectsRect(Vector2 a, Vector2 b, Rect rect)
    {
        float lo = 0f, hi = 1f;
        Vector2 d = b-a;
        for (int axis = 0; axis < 2; axis++)
        {
            float origin = axis == 0 ? a.x : a.y, delta = axis == 0 ? d.x : d.y;
            float min = axis == 0 ? rect.xMin : rect.yMin, max = axis == 0 ? rect.xMax : rect.yMax;
            if (Mathf.Abs(delta) < 0.00001f) { if (origin < min || origin > max) return false; }
            else
            {
                float t0 = (min-origin)/delta, t1 = (max-origin)/delta;
                lo = Mathf.Max(lo, Mathf.Min(t0,t1)); hi = Mathf.Min(hi, Mathf.Max(t0,t1));
                if (lo > hi) return false;
            }
        }
        return true;
    }
}
