using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  MapNavAgent  (A* pathfinding on WalkableZone polygon)
// ============================================================
public class MapNavAgent : MonoBehaviour
{
    [Header("Grid")] public int gridCols = 80, gridRows = 45;
    [Header("Movement")] public float moveSpeed = 3.5f;

    PolygonCollider2D _walkZone;
    Vector2[] _worldVerts;
    bool[,] _walkable;
    Bounds _gridBounds;
    float _cellW, _cellH;
    List<Vector2> _waypoints = new List<Vector2>();
    int _waypointIdx;
    bool _navigating;
    public bool IsNavigating => _navigating;

    void Start() { FindWalkZone(); BuildGrid(); }

    void FindWalkZone()
    {
        var go = GameObject.Find("WalkableZone"); if (go) _walkZone = go.GetComponent<PolygonCollider2D>();
        if (!_walkZone) { Debug.LogWarning("[MapNavAgent] No WalkableZone."); return; }
        Vector2 off = _walkZone.transform.position; var loc = _walkZone.points;
        _worldVerts = new Vector2[loc.Length]; for (int i = 0; i < loc.Length; i++) _worldVerts[i] = loc[i] + off;
    }

    void BuildGrid()
    {
        _gridBounds = _walkZone != null ? _walkZone.bounds : new Bounds(Vector3.zero, new Vector3(16f, 9f, 0));
        _gridBounds.Expand(0.3f); _cellW = _gridBounds.size.x / gridCols; _cellH = _gridBounds.size.y / gridRows;
        _walkable = new bool[gridCols, gridRows];
        for (int c = 0; c < gridCols; c++) for (int r = 0; r < gridRows; r++) _walkable[c, r] = _worldVerts == null || PolygonUtils.PointInPolygon(GridToWorld(c, r), _worldVerts);
    }

    void Update()
    {
        if (!_navigating || _waypoints.Count == 0) return;
        Vector2 target = _waypoints[_waypointIdx];
        Vector2 next = Vector2.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
        transform.position = new Vector3(next.x, next.y, transform.position.z);
        if (Vector2.Distance(next, target) < 0.05f) { _waypointIdx++; if (_waypointIdx >= _waypoints.Count) { _navigating = false; _waypoints.Clear(); } }
    }

    public void ClampToWalkZone()
    {
        if (_worldVerts == null) return;
        Vector2 pos = transform.position;
        if (!PolygonUtils.PointInPolygon(pos, _worldVerts)) { Vector2 c = PolygonUtils.NearestEdgePoint(pos, _worldVerts); transform.position = new Vector3(c.x, c.y, transform.position.z); }
    }

    public void RequestPath(Vector3 destination)
    {
        Vector2 dest = destination;
        if (_worldVerts != null && !PolygonUtils.PointInPolygon(dest, _worldVerts)) dest = PolygonUtils.NearestEdgePoint(dest, _worldVerts);
        int sc = Mathf.Clamp(Col(transform.position.x), 0, gridCols - 1), sr = Mathf.Clamp(Row(transform.position.y), 0, gridRows - 1);
        int gc = Mathf.Clamp(Col(dest.x), 0, gridCols - 1), gr = Mathf.Clamp(Row(dest.y), 0, gridRows - 1);
        if (!_walkable[gc, gr]) (gc, gr) = NearestWalkable(gc, gr);
        var path = AStar(sc, sr, gc, gr);
        _waypoints.Clear();
        if (path == null || path.Count == 0) _waypoints.Add(dest);
        else { foreach (var n in path) _waypoints.Add(GridToWorld(n.x, n.y)); if (_worldVerts == null || PolygonUtils.PointInPolygon(dest, _worldVerts)) _waypoints[_waypoints.Count - 1] = dest; }
        _waypointIdx = 0; _navigating = true;
    }

    public void CancelPath() { _navigating = false; _waypoints.Clear(); }

    struct Node { public int c, r, pc, pr; public float g, f; }
    List<Vector2Int> AStar(int sc, int sr, int gc, int gr)
    {
        if (sc == gc && sr == gr) return new List<Vector2Int> { new Vector2Int(gc, gr) };
        var open = new List<Node>(); var closed = new HashSet<int>(); var came = new Dictionary<int, int>();
        open.Add(new Node { c = sc, r = sr, pc = -1, pr = -1, g = 0, f = H(sc, sr, gc, gr) });
        int[] dc = { 0, 0, 1, -1, 1, 1, -1, -1 }, dr = { 1, -1, 0, 0, 1, -1, 1, -1 };
        float[] co = { 1, 1, 1, 1, 1.414f, 1.414f, 1.414f, 1.414f };
        int iters = 0;
        while (open.Count > 0 && iters++ < 8000)
        {
            int bi = 0; for (int i = 1; i < open.Count; i++) if (open[i].f < open[bi].f) bi = i;
            var cur = open[bi]; open.RemoveAt(bi); int ck = Key(cur.c, cur.r);
            if (closed.Contains(ck)) continue; closed.Add(ck);
            if (cur.pc >= 0) came[ck] = Key(cur.pc, cur.pr);
            if (cur.c == gc && cur.r == gr) { var path = new List<Vector2Int>(); int k = ck; while (came.ContainsKey(k)) { path.Add(new Vector2Int(k % gridCols, k / gridCols)); k = came[k]; } path.Add(new Vector2Int(sc, sr)); path.Reverse(); return path; }
            for (int d = 0; d < 8; d++) { int nc = cur.c + dc[d], nr = cur.r + dr[d]; if (nc < 0 || nc >= gridCols || nr < 0 || nr >= gridRows || !_walkable[nc, nr]) continue; int nk = Key(nc, nr); if (closed.Contains(nk)) continue; float ng = cur.g + co[d]; open.Add(new Node { c = nc, r = nr, pc = cur.c, pr = cur.r, g = ng, f = ng + H(nc, nr, gc, gr) }); }
        }
        return null;
    }
    float H(int c, int r, int gc, int gr) => Mathf.Sqrt((c - gc) * (c - gc) + (r - gr) * (r - gr));
    int Key(int c, int r) => r * gridCols + c;
    (int, int) NearestWalkable(int gc, int gr) { for (int rad = 1; rad < Mathf.Max(gridCols, gridRows); rad++) for (int c = gc - rad; c <= gc + rad; c++) for (int r = gr - rad; r <= gr + rad; r++) { if (c < 0 || c >= gridCols || r < 0 || r >= gridRows) continue; if (_walkable[c, r]) return (c, r); } return (gc, gr); }
    int Col(float x) => Mathf.FloorToInt((x - _gridBounds.min.x) / _cellW);
    int Row(float y) => Mathf.FloorToInt((y - _gridBounds.min.y) / _cellH);
    Vector2 GridToWorld(int c, int r) => new Vector2(_gridBounds.min.x + (c + 0.5f) * _cellW, _gridBounds.min.y + (r + 0.5f) * _cellH);
}
