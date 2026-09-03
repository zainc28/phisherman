using UnityEngine;

// ============================================================
//  PolygonUtils
//
//  All the MonoBehaviour classes that used to live in this file
//  (WorldMapManager, MapDoorTrigger, MapPathTrigger, MapCornerZone,
//  MapNavAgent, MapWasdZoneClamp, MapWalkAnimator, MapDoorCTA,
//  CTABorderFader, NPCMarker, MapBlocker) have each been split into
//  their own file in this same folder.
//
//  Why: a script's identity in an already-saved scene is the GUID of
//  its .cs file plus a per-class local ID computed from the class
//  name. When several classes shared this one file, any scene built
//  against an earlier version of it kept pointing at that old
//  combination — so after this file's own content (and therefore its
//  effective identity) changed over time, those components turned
//  into "Missing Script" on every GameObject that used them, project
//  wide (WorldMap.unity through WorldMap5.unity). Splitting each
//  class into its own single-class file gives it a fixed identity
//  (guid of that file + the standard primary-class local ID) that
//  no longer moves out from under already-built scenes.
//
//  PolygonUtils has no MonoBehaviour instances of its own — it's a
//  static helper used by MapNavAgent and MapWasdZoneClamp — so it
//  isn't part of that bug and stays here.
// ============================================================
public static class PolygonUtils
{
    public static bool PointInPolygon(Vector2 p, Vector2[] verts)
    {
        int n = verts.Length; bool inside = false;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            float xi = verts[i].x, yi = verts[i].y, xj = verts[j].x, yj = verts[j].y;
            bool hit = ((yi > p.y) != (yj > p.y)) && (p.x < (xj - xi) * (p.y - yi) / (yj - yi) + xi);
            if (hit) inside = !inside;
        }
        return inside;
    }

    public static Vector2 NearestEdgePoint(Vector2 p, Vector2[] verts)
    {
        int n = verts.Length; Vector2 best = verts[0]; float bestD = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            Vector2 a = verts[i], b = verts[(i + 1) % n], ab = b - a;
            float d = ab.sqrMagnitude;
            Vector2 closest = d < 0.0001f ? a : a + Mathf.Clamp01(Vector2.Dot(p - a, ab) / d) * ab;
            float dist = Vector2.Distance(p, closest);
            if (dist < bestD) { bestD = dist; best = closest; }
        }
        return best;
    }
}
