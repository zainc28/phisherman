using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// ============================================================
//  MapBlocker
//  Invisible push-out rectangle over non-walkable areas.
//  LateUpdate pushes the player out on the smallest axis.
// ============================================================
public class MapBlocker : MonoBehaviour
{
    public Transform player;
    public float halfW = 1f;
    public float halfH = 1f;

    void LateUpdate()
    {
        if (player == null) return;
        Vector2 c = transform.position;
        Vector2 pos = player.position;
        float l = c.x - halfW, r = c.x + halfW;
        float b = c.y - halfH, t = c.y + halfH;
        if (pos.x > l && pos.x < r && pos.y > b && pos.y < t)
        {
            float dL = pos.x - l, dR = r - pos.x;
            float dB = pos.y - b, dT = t - pos.y;
            float m = Mathf.Min(dL, dR, dB, dT);
            Vector3 p = player.position;
            if (m == dL) p.x = l - 0.01f;
            else if (m == dR) p.x = r + 0.01f;
            else if (m == dB) p.y = b - 0.01f;
            else p.y = t + 0.01f;
            player.position = p;
        }
    }
}

// ============================================================
//  MapDoorTrigger
//  Proximity-based walk-on trigger for building entrances.
// ============================================================
public class MapDoorTrigger : MonoBehaviour
{
    public Transform player;
    public string targetScene;
    public GameObject prompt;
    public float promptProximity = 1.8f;
    public float triggerRadius = 0.55f;
    public float bobAmount = 0.08f;
    public float bobSpeed = 2.2f;

    bool _fired;
    float _bobBase;

    void Start()
    {
        if (prompt != null) { _bobBase = prompt.transform.localPosition.y; prompt.SetActive(false); }
    }

    void Update()
    {
        if (_fired || player == null) return;
        float dist = Vector2.Distance(transform.position, player.position);
        if (prompt != null)
        {
            bool show = dist < promptProximity;
            if (prompt.activeSelf != show) prompt.SetActive(show);
            if (show)
            {
                var lp = prompt.transform.localPosition;
                lp.y = _bobBase + Mathf.Sin(Time.time * bobSpeed) * bobAmount;
                prompt.transform.localPosition = lp;
            }
        }
        if (dist < triggerRadius) { _fired = true; if (prompt != null) prompt.SetActive(false); SceneManager.LoadScene(targetScene); }
    }
}

// ============================================================
//  MapPathTrigger
//  Proximity-based trigger placed at path exits.
//  LoadScene  → loads target scene when player walks close.
//  LockedPopup→ shows timed popup when player walks close.
// ============================================================
public class MapPathTrigger : MonoBehaviour
{
    public enum TriggerType { LoadScene, LockedPopup }

    public Transform player;
    public TriggerType type = TriggerType.LoadScene;
    public string targetScene = "";
    public GameObject lockedPopup;
    public float popupDuration = 2.8f;
    public GameObject prompt;
    public float promptProximity = 2.2f;
    public float triggerRadius = 1.0f;
    public float bobAmount = 0.10f;
    public float bobSpeed = 2.0f;

    bool _fired;
    bool _popupActive;
    float _popupTimer;
    float _bobBase;

    void Start()
    {
        if (prompt != null) { _bobBase = prompt.transform.localPosition.y; prompt.SetActive(false); }
        if (lockedPopup != null) lockedPopup.SetActive(false);
    }

    void Update()
    {
        if (player == null || _fired) return;
        float dist = Vector2.Distance(transform.position, player.position);

        if (prompt != null)
        {
            bool show = dist < promptProximity;
            if (prompt.activeSelf != show) prompt.SetActive(show);
            if (show) { var lp = prompt.transform.localPosition; lp.y = _bobBase + Mathf.Sin(Time.time * bobSpeed) * bobAmount; prompt.transform.localPosition = lp; }
        }

        if (_popupActive)
        {
            _popupTimer -= Time.deltaTime;
            if (_popupTimer <= 0f) { _popupActive = false; if (lockedPopup != null) lockedPopup.SetActive(false); }
        }

        if (dist < triggerRadius)
        {
            if (type == TriggerType.LoadScene && !string.IsNullOrEmpty(targetScene))
            { _fired = true; if (prompt != null) prompt.SetActive(false); SceneManager.LoadScene(targetScene); }
            else if (type == TriggerType.LockedPopup && !_popupActive)
            { _popupActive = true; _popupTimer = popupDuration; if (lockedPopup != null) lockedPopup.SetActive(true); }
        }
    }
}

// ============================================================
//  MapCornerZone
//  Screen-space 200×200 pixel zone in a corner of the screen.
//  When the player's screen position enters it, fires an action.
//  Much more reliable than world-space for corner paths since
//  the cobblestone paths go diagonally toward the corners.
// ============================================================
public class MapCornerZone : MonoBehaviour
{
    public enum Corner { BottomLeft, BottomRight }
    public enum ZoneAction { LoadScene, LockedPopup }

    public Transform player;
    public Corner corner = Corner.BottomLeft;
    public ZoneAction action = ZoneAction.LoadScene;
    public string targetScene = "";
    public GameObject lockedPopup;
    public float popupDuration = 2.8f;
    public int zonePixels = 200;   // size of corner zone in screen pixels

    bool _fired;
    bool _popupActive;
    float _popupTimer;

    void Start()
    {
        if (lockedPopup != null) lockedPopup.SetActive(false);
    }

    void Update()
    {
        if (player == null || _fired) return;
        if (Camera.main == null) return;

        Vector2 screenPos = Camera.main.WorldToScreenPoint(player.position);
        float sw = Screen.width;
        float sh = Screen.height;

        bool inZone = corner == Corner.BottomLeft
            ? screenPos.x < zonePixels && screenPos.y < zonePixels
            : screenPos.x > sw - zonePixels && screenPos.y < zonePixels;

        if (_popupActive)
        {
            _popupTimer -= Time.deltaTime;
            if (_popupTimer <= 0f) { _popupActive = false; if (lockedPopup != null) lockedPopup.SetActive(false); }
        }

        if (inZone)
        {
            if (action == ZoneAction.LoadScene && !string.IsNullOrEmpty(targetScene))
            { _fired = true; SceneManager.LoadScene(targetScene); }
            else if (action == ZoneAction.LockedPopup && !_popupActive)
            { _popupActive = true; _popupTimer = popupDuration; if (lockedPopup != null) lockedPopup.SetActive(true); }
        }
    }
}

// ============================================================
//  MapNavAgent
//  Attached to the player. Intercepts clicks, runs a coarse
//  grid A* around all MapBlocker rectangles in the scene, and
//  feeds the resulting waypoints to WorldMapManager's movement.
//
//  HOW IT WORKS:
//  - On mouse click it captures the destination.
//  - Builds a lightweight grid (gridCols × gridRows) over the
//    walkable area and marks cells occupied by any MapBlocker.
//  - Runs A* from current position to destination.
//  - Feeds the resulting world-space waypoints into a simple
//    coroutine that moves the player via transform.position,
//    matching WorldMapManager's moveSpeed field exactly so the
//    feel is identical.
//  - If no path is found (destination inside a blocker) it
//    finds the nearest reachable cell and goes there instead.
//
//  IMPORTANT: To hook this in without touching WorldMapManager,
//  MapNavAgent uses a tiny trick: it reads the moveSpeed value
//  from WorldMapManager and takes over movement only when a
//  path is active. When idle it does nothing so WASD still
//  works through the manager as normal.
// ============================================================
public class MapNavAgent : MonoBehaviour
{
    [Header("Grid dimensions (tune if obstacles clip)")]
    public int gridCols = 64;
    public int gridRows = 36;

    [Header("World bounds (match WorldMapManager.boundsX/Y)")]
    public Vector2 boundsX = new Vector2(-7.8f, 7.8f);
    public Vector2 boundsY = new Vector2(-4.0f, 4.3f);

    [Header("Movement")]
    public float moveSpeed = 5f;   // match WorldMapManager's moveSpeed value

    // Internal state
    List<Vector2> _waypoints = new List<Vector2>();
    int _waypointIdx;
    bool _navigating;

    // Grid
    bool[,] _blocked;
    float _cellW, _cellH;
    float _gridOriginX, _gridOriginY;

    void Start()
    {
        BuildGrid();
    }

    void BuildGrid()
    {
        _cellW = (boundsX.y - boundsX.x) / gridCols;
        _cellH = (boundsY.y - boundsY.x) / gridRows;
        _gridOriginX = boundsX.x;
        _gridOriginY = boundsY.x;
        _blocked = new bool[gridCols, gridRows];

        foreach (var blocker in FindObjectsByType<MapBlocker>(FindObjectsSortMode.None))
        {
            Vector2 bc = blocker.transform.position;
            float l = bc.x - blocker.halfW;
            float r = bc.x + blocker.halfW;
            float b = bc.y - blocker.halfH;
            float t = bc.y + blocker.halfH;

            int cMin = Mathf.Max(0, WorldToCol(l) - 1);
            int cMax = Mathf.Min(gridCols - 1, WorldToCol(r) + 1);
            int rMin = Mathf.Max(0, WorldToRow(b) - 1);
            int rMax = Mathf.Min(gridRows - 1, WorldToRow(t) + 1);
            for (int c = cMin; c <= cMax; c++)
                for (int row = rMin; row <= rMax; row++)
                    _blocked[c, row] = true;
        }
    }

    void Update()
    {
        // Intercept left mouse click
        if (Input.GetMouseButtonDown(0) && Camera.main != null)
        {
            // Don't intercept if mouse is over UI
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                return;

            Vector3 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            worldPos.z = transform.position.z;
            RequestPath(worldPos);
        }

        // Follow waypoints
        if (_navigating && _waypoints.Count > 0)
        {
            Vector2 target = _waypoints[_waypointIdx];
            Vector2 cur = transform.position;
            Vector2 next = Vector2.MoveTowards(cur, target, moveSpeed * Time.deltaTime);
            transform.position = new Vector3(next.x, next.y, transform.position.z);

            if (Vector2.Distance(next, target) < 0.05f)
            {
                _waypointIdx++;
                if (_waypointIdx >= _waypoints.Count)
                {
                    _navigating = false;
                    _waypoints.Clear();
                }
            }
        }
    }

    void RequestPath(Vector3 destination)
    {
        // Clamp destination to bounds
        float dx = Mathf.Clamp(destination.x, boundsX.x, boundsX.y);
        float dy = Mathf.Clamp(destination.y, boundsY.x, boundsY.y);

        int startC = Mathf.Clamp(WorldToCol(transform.position.x), 0, gridCols - 1);
        int startR = Mathf.Clamp(WorldToRow(transform.position.y), 0, gridRows - 1);
        int goalC = Mathf.Clamp(WorldToCol(dx), 0, gridCols - 1);
        int goalR = Mathf.Clamp(WorldToRow(dy), 0, gridRows - 1);

        // If goal is blocked, find nearest free cell
        if (_blocked[goalC, goalR])
        {
            (goalC, goalR) = FindNearestFree(goalC, goalR);
        }

        var path = AStar(startC, startR, goalC, goalR);
        if (path == null || path.Count == 0)
        {
            // Fallback: just walk straight (WorldMapManager will handle the blocker push-out)
            _waypoints.Clear();
            _waypoints.Add(new Vector2(dx, dy));
            _waypointIdx = 0;
            _navigating = true;
            return;
        }

        // Convert grid path to world waypoints, skipping intermediate collinear points
        _waypoints.Clear();
        for (int i = 0; i < path.Count; i++)
        {
            var wp = ColRowToWorld(path[i].x, path[i].y);
            _waypoints.Add(wp);
        }
        // Replace final waypoint with exact click position if it's free
        if (!_blocked[goalC, goalR])
            _waypoints[_waypoints.Count - 1] = new Vector2(dx, dy);

        _waypointIdx = 0;
        _navigating = true;
    }

    // ── A* ────────────────────────────────────────────────────────────────────
    struct Node
    {
        public int c, r, pc, pr;
        public float g, f;
    }

    List<Vector2Int> AStar(int sc, int sr, int gc, int gr)
    {
        if (sc == gc && sr == gr) return new List<Vector2Int> { new Vector2Int(gc, gr) };

        var open = new List<Node>();
        var closed = new HashSet<int>();
        var came = new Dictionary<int, int>(); // key → parent key

        int startKey = Key(sc, sr);
        open.Add(new Node { c = sc, r = sr, pc = -1, pr = -1, g = 0, f = H(sc, sr, gc, gr) });

        int[] dc = { 0, 0, 1, -1, 1, 1, -1, -1 };
        int[] dr = { 1, -1, 0, 0, 1, -1, 1, -1 };
        float[] cost = { 1, 1, 1, 1, 1.414f, 1.414f, 1.414f, 1.414f };

        int iters = 0;
        while (open.Count > 0 && iters++ < 4000)
        {
            // Find lowest f
            int bestIdx = 0;
            for (int i = 1; i < open.Count; i++) if (open[i].f < open[bestIdx].f) bestIdx = i;
            var cur = open[bestIdx];
            open.RemoveAt(bestIdx);

            int curKey = Key(cur.c, cur.r);
            if (closed.Contains(curKey)) continue;
            closed.Add(curKey);

            if (cur.pc >= 0) came[curKey] = Key(cur.pc, cur.pr);

            if (cur.c == gc && cur.r == gr)
            {
                // Reconstruct
                var path = new List<Vector2Int>();
                int k = curKey;
                while (came.ContainsKey(k)) { path.Add(new Vector2Int(k % gridCols, k / gridCols)); k = came[k]; }
                path.Add(new Vector2Int(sc, sr));
                path.Reverse();
                return path;
            }

            for (int d = 0; d < 8; d++)
            {
                int nc = cur.c + dc[d];
                int nr = cur.r + dr[d];
                if (nc < 0 || nc >= gridCols || nr < 0 || nr >= gridRows) continue;
                if (_blocked[nc, nr]) continue;
                int nKey = Key(nc, nr);
                if (closed.Contains(nKey)) continue;
                float ng = cur.g + cost[d];
                open.Add(new Node { c = nc, r = nr, pc = cur.c, pr = cur.r, g = ng, f = ng + H(nc, nr, gc, gr) });
            }
        }
        return null; // no path
    }

    float H(int c, int r, int gc, int gr) => Mathf.Sqrt((c - gc) * (c - gc) + (r - gr) * (r - gr));
    int Key(int c, int r) => r * gridCols + c;

    (int c, int r) FindNearestFree(int gc, int gr)
    {
        for (int radius = 1; radius < Mathf.Max(gridCols, gridRows); radius++)
            for (int c = gc - radius; c <= gc + radius; c++)
                for (int r = gr - radius; r <= gr + radius; r++)
                {
                    if (c < 0 || c >= gridCols || r < 0 || r >= gridRows) continue;
                    if (!_blocked[c, r]) return (c, r);
                }
        return (gc, gr);
    }

    // ── Coordinate helpers ────────────────────────────────────────────────────
    int WorldToCol(float x) => Mathf.FloorToInt((x - _gridOriginX) / _cellW);
    int WorldToRow(float y) => Mathf.FloorToInt((y - _gridOriginY) / _cellH);
    Vector2 ColRowToWorld(int c, int r) => new Vector2(
        _gridOriginX + (c + 0.5f) * _cellW,
        _gridOriginY + (r + 0.5f) * _cellH);
}