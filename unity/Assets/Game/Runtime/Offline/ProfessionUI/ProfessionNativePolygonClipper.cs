using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Offline.ProfessionUI
{
    // Independent ear triangulation partitions a simple polygon into disjoint
    // convex pieces. Existing half-plane clipping preserves source UVs/winding.
    public sealed class ProfessionNativePolygonClipper
    {
        private readonly List<ProfessionNativeConvexClipper> pieces = new List<ProfessionNativeConvexClipper>();

        public ProfessionNativePolygonClipper(Vector2[] boundary, bool allowConcave = false)
        {
            try { pieces.Add(new ProfessionNativeConvexClipper(boundary)); return; }
            catch (NotSupportedException) when (allowConcave) { }
            var polygon = new List<Vector2>(boundary);
            for (int i = polygon.Count - 1; i >= 0 && polygon.Count > 3; i--)
            {
                int before = (i + polygon.Count - 1) % polygon.Count, after = (i + 1) % polygon.Count;
                if (polygon[i].Equals(polygon[before]) || Cross(polygon[before], polygon[i], polygon[after]) == 0
                    && Vector2.Dot(polygon[i] - polygon[before], polygon[i] - polygon[after]) <= 0)
                    polygon.RemoveAt(i);
            }
            for (int i = 0; i < polygon.Count; i++)
                for (int j = i + 1; j < polygon.Count; j++)
                {
                    int nextI = (i + 1) % polygon.Count, nextJ = (j + 1) % polygon.Count;
                    if (nextI == j || nextJ == i) continue;
                    if (Intersects(polygon[i], polygon[nextI], polygon[j], polygon[nextJ]))
                        throw new NotSupportedException("Restore original self-intersecting clipping boundary.");
                }
            double area = 0;
            for (int i = 0; i < polygon.Count; i++)
                area += (double)polygon[i].x * polygon[(i + 1) % polygon.Count].y
                    - (double)polygon[(i + 1) % polygon.Count].x * polygon[i].y;
            int sign = Math.Sign(area);
            if (sign == 0) return;
            while (polygon.Count > 3)
            {
                bool found = false;
                for (int i = 0; i < polygon.Count; i++)
                {
                    int before = (i + polygon.Count - 1) % polygon.Count, after = (i + 1) % polygon.Count;
                    Vector2 a = polygon[before], b = polygon[i], c = polygon[after];
                    if (sign * Cross(a, b, c) <= 0) continue;
                    bool blocked = false;
                    for (int j = 0; j < polygon.Count; j++)
                        if (j != before && j != i && j != after && Inside(polygon[j], a, b, c, sign))
                        { blocked = true; break; }
                    if (blocked) continue;
                    pieces.Add(new ProfessionNativeConvexClipper(new[] { a, b, c }));
                    polygon.RemoveAt(i); found = true; break;
                }
                if (!found) throw new NotSupportedException("Original clipping polygon has no simple ear partition.");
            }
            pieces.Add(new ProfessionNativeConvexClipper(polygon.ToArray()));
        }

        public void AppendTriangle(Vector2 a, Vector2 b, Vector2 c, Vector2 uvA, Vector2 uvB, Vector2 uvC,
            List<Vector3> vertices, List<Vector2> uvs, List<int> indices)
        {
            foreach (var piece in pieces)
                piece.AppendTriangle(a, b, c, uvA, uvB, uvC, vertices, uvs, indices);
        }

        private static bool Inside(Vector2 p, Vector2 a, Vector2 b, Vector2 c, int sign)
            => sign * Cross(a, b, p) >= 0 && sign * Cross(b, c, p) >= 0 && sign * Cross(c, a, p) >= 0;
        private static bool Intersects(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            double ac = Cross(a, b, c), ad = Cross(a, b, d), ca = Cross(c, d, a), cb = Cross(c, d, b);
            if (Math.Sign(ac) * Math.Sign(ad) < 0 && Math.Sign(ca) * Math.Sign(cb) < 0) return true;
            return ac == 0 && OnSegment(c, a, b) || ad == 0 && OnSegment(d, a, b)
                || ca == 0 && OnSegment(a, c, d) || cb == 0 && OnSegment(b, c, d);
        }
        private static bool OnSegment(Vector2 p, Vector2 a, Vector2 b)
            => p.x >= Math.Min(a.x, b.x) && p.x <= Math.Max(a.x, b.x)
                && p.y >= Math.Min(a.y, b.y) && p.y <= Math.Max(a.y, b.y);
        private static double Cross(Vector2 a, Vector2 b, Vector2 c)
            => ((double)b.x - a.x) * ((double)c.y - a.y) - ((double)b.y - a.y) * ((double)c.x - a.x);
    }
}
