using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// ============================================================
//  MapBlocker  (legacy — not needed with WalkableZone polygon)
// ============================================================
public class MapBlocker : MonoBehaviour
{
    public Transform player;
    public float halfW = 1f;
    public float halfH = 1f;

    void LateUpdate()
    {
        if (player == null) return;
        Vector2 c = transform.position; Vector2 pos = player.position;
        float l = c.x - halfW, r = c.x + halfW, b = c.y - halfH, t = c.y + halfH;
        if (pos.x > l && pos.x < r && pos.y > b && pos.y < t)
        {
            float dL = pos.x - l, dR = r - pos.x, dB = pos.y - b, dT = t - pos.y;
            float m = Mathf.Min(dL, dR, dB, dT); Vector3 p = player.position;
            if (m == dL) p.x = l - 0.01f;
            else if (m == dR) p.x = r + 0.01f;
            else if (m == dB) p.y = b - 0.01f; else p.y = t + 0.01f;
            player.position = p;
        }
    }
}

// ============================================================
//  MapDoorTrigger
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

    void Start() { if (prompt != null) { _bobBase = prompt.transform.localPosition.y; prompt.SetActive(false); } }

    void Update()
    {
        if (_fired || player == null) return;
        float dist = Vector2.Distance(transform.position, player.position);
        if (prompt != null)
        {
            bool show = dist < promptProximity;
            if (prompt.activeSelf != show) prompt.SetActive(show);
            if (show) { var lp = prompt.transform.localPosition; lp.y = _bobBase + Mathf.Sin(Time.time * bobSpeed) * bobAmount; prompt.transform.localPosition = lp; }
        }
        if (dist < triggerRadius) { _fired = true; if (prompt != null) prompt.SetActive(false); SceneManager.LoadScene(targetScene); }
    }
}

// ============================================================
//  MapPathTrigger
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
    public float bobAmount = 0.10f, bobSpeed = 2.0f;

    bool _fired, _popupActive;
    float _popupTimer, _bobBase;

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
        if (_popupActive) { _popupTimer -= Time.deltaTime; if (_popupTimer <= 0f) { _popupActive = false; if (lockedPopup != null) lockedPopup.SetActive(false); } }
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
    public int zonePixels = 200;

    bool _fired, _popupActive;
    float _popupTimer;

    void Start() { if (lockedPopup != null) lockedPopup.SetActive(false); }

    void Update()
    {
        if (player == null || _fired || Camera.main == null) return;
        Vector2 sp = Camera.main.WorldToScreenPoint(player.position);
        bool inZone = corner == Corner.BottomLeft ? sp.x < zonePixels && sp.y < zonePixels : sp.x > Screen.width - zonePixels && sp.y < zonePixels;
        if (_popupActive) { _popupTimer -= Time.deltaTime; if (_popupTimer <= 0f) { _popupActive = false; if (lockedPopup != null) lockedPopup.SetActive(false); } }
        if (inZone)
        {
            if (action == ZoneAction.LoadScene && !string.IsNullOrEmpty(targetScene)) { _fired = true; SceneManager.LoadScene(targetScene); }
            else if (action == ZoneAction.LockedPopup && !_popupActive) { _popupActive = true; _popupTimer = popupDuration; if (lockedPopup != null) lockedPopup.SetActive(true); }
        }
    }
}

// ============================================================
//  PolygonUtils
//  Pure math point-in-polygon test (ray casting algorithm).
//  Works with NO Physics2D components at all — no Rigidbody2D
//  needed on the WalkableZone GO.
// ============================================================
public static class PolygonUtils
{
    /// Returns true if point p is inside the polygon defined by verts.
    /// verts should be in world space (already offset by transform.position).
    public static bool PointInPolygon(Vector2 p, Vector2[] verts)
    {
        int n = verts.Length;
        bool inside = false;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            float xi = verts[i].x, yi = verts[i].y;
            float xj = verts[j].x, yj = verts[j].y;
            bool intersect = ((yi > p.y) != (yj > p.y))
                          && (p.x < (xj - xi) * (p.y - yi) / (yj - yi) + xi);
            if (intersect) inside = !inside;
        }
        return inside;
    }

    /// Returns the closest point on any edge of the polygon to point p.
    public static Vector2 NearestEdgePoint(Vector2 p, Vector2[] verts)
    {
        int n = verts.Length;
        Vector2 best = verts[0];
        float bestD = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            Vector2 a = verts[i], b = verts[(i + 1) % n];
            Vector2 closest = ClosestOnSegment(p, a, b);
            float d = Vector2.Distance(p, closest);
            if (d < bestD) { bestD = d; best = closest; }
        }
        return best;
    }

    static Vector2 ClosestOnSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float d = ab.sqrMagnitude;
        if (d < 0.0001f) return a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / d);
        return a + t * ab;
    }
}

// ============================================================
//  MapNavAgent
//
//  Uses the WalkableZone PolygonCollider2D you drew in the
//  editor. Containment is tested with pure ray-casting math
//  so NO Rigidbody2D is needed on the WalkableZone GO.
//
//  The A* grid is built by testing each cell centre against
//  the polygon, so the grid perfectly mirrors your drawing.
// ============================================================
public class MapNavAgent : MonoBehaviour
{
    [Header("Grid (auto-sized from WalkableZone bounds)")]
    public int gridCols = 80;
    public int gridRows = 45;

    [Header("Movement")]
    public float moveSpeed = 3.5f;

    PolygonCollider2D _walkZone;
    Vector2[] _worldVerts;   // polygon verts in world space (cached)

    bool[,] _walkable;
    Bounds _gridBounds;
    float _cellW, _cellH;

    List<Vector2> _waypoints = new List<Vector2>();
    int _waypointIdx;
    bool _navigating;

    public bool IsNavigating => _navigating;

    // ── Lifecycle ──────────────────────────────────────────────
    void Start()
    {
        FindWalkZone();
        BuildGrid();
    }

    void FindWalkZone()
    {
        var go = GameObject.Find("WalkableZone");
        if (go != null) _walkZone = go.GetComponent<PolygonCollider2D>();

        if (_walkZone == null)
        {
            Debug.LogWarning("[MapNavAgent] 'WalkableZone' not found — player will walk anywhere. " +
                             "Make sure the GO is named exactly 'WalkableZone'.");
            return;
        }

        // Cache verts in world space once (polygon assumed static)
        CacheWorldVerts();
        Debug.Log($"[MapNavAgent] WalkableZone found with {_worldVerts.Length} verts. Polygon constraint active.");
    }

    void CacheWorldVerts()
    {
        if (_walkZone == null) return;
        Vector2 offset = _walkZone.transform.position;
        var local = _walkZone.points;
        _worldVerts = new Vector2[local.Length];
        for (int i = 0; i < local.Length; i++)
            _worldVerts[i] = local[i] + offset;
    }

    bool InWalkZone(Vector2 p) =>
        _worldVerts != null && PolygonUtils.PointInPolygon(p, _worldVerts);

    Vector2 NearestInZone(Vector2 p) =>
        _worldVerts != null ? PolygonUtils.NearestEdgePoint(p, _worldVerts) : p;

    void BuildGrid()
    {
        if (_walkZone != null)
            _gridBounds = _walkZone.bounds;
        else
            _gridBounds = new Bounds(Vector3.zero, new Vector3(16f, 9f, 0f));

        _gridBounds.Expand(0.3f);
        _cellW = _gridBounds.size.x / gridCols;
        _cellH = _gridBounds.size.y / gridRows;
        _walkable = new bool[gridCols, gridRows];

        int walkCount = 0;
        for (int c = 0; c < gridCols; c++)
            for (int r = 0; r < gridRows; r++)
            {
                bool w = _worldVerts == null || PolygonUtils.PointInPolygon(GridToWorld(c, r), _worldVerts);
                _walkable[c, r] = w;
                if (w) walkCount++;
            }

        Debug.Log($"[MapNavAgent] Grid built: {walkCount}/{gridCols * gridRows} cells walkable.");
    }

    // ── Per-frame ──────────────────────────────────────────────
    void Update()
    {
        if (Input.GetMouseButtonDown(0) && Camera.main != null)
        {
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;
            Vector3 wp = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            wp.z = transform.position.z;
            RequestPath(wp);
        }

        if (_navigating && _waypoints.Count > 0)
        {
            Vector2 target = _waypoints[_waypointIdx];
            Vector2 next = Vector2.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
            transform.position = new Vector3(next.x, next.y, transform.position.z);
            if (Vector2.Distance(next, target) < 0.05f)
            {
                _waypointIdx++;
                if (_waypointIdx >= _waypoints.Count) { _navigating = false; _waypoints.Clear(); }
            }
        }
    }

    // Called by MapWasdZoneClamp in LateUpdate after WASD movement
    public void ClampToWalkZone()
    {
        if (_worldVerts == null) return;
        Vector2 pos = transform.position;
        if (!InWalkZone(pos))
        {
            Vector2 clamped = NearestInZone(pos);
            transform.position = new Vector3(clamped.x, clamped.y, transform.position.z);
        }
    }

    void RequestPath(Vector3 destination)
    {
        Vector2 dest = destination;
        if (!InWalkZone(dest)) dest = NearestInZone(dest);

        int sc = Mathf.Clamp(WorldToCol(transform.position.x), 0, gridCols - 1);
        int sr = Mathf.Clamp(WorldToRow(transform.position.y), 0, gridRows - 1);
        int gc = Mathf.Clamp(WorldToCol(dest.x), 0, gridCols - 1);
        int gr = Mathf.Clamp(WorldToRow(dest.y), 0, gridRows - 1);

        if (!_walkable[gc, gr]) (gc, gr) = FindNearestWalkableCell(gc, gr);

        var path = AStar(sc, sr, gc, gr);
        _waypoints.Clear();
        if (path == null || path.Count == 0)
            _waypoints.Add(dest);
        else
        {
            foreach (var n in path) _waypoints.Add(GridToWorld(n.x, n.y));
            if (InWalkZone(dest)) _waypoints[_waypoints.Count - 1] = dest;
        }
        _waypointIdx = 0;
        _navigating = true;
    }

    // ── A* ────────────────────────────────────────────────────
    struct Node { public int c, r, pc, pr; public float g, f; }

    List<Vector2Int> AStar(int sc, int sr, int gc, int gr)
    {
        if (sc == gc && sr == gr) return new List<Vector2Int> { new Vector2Int(gc, gr) };
        var open = new List<Node>(); var closed = new HashSet<int>(); var came = new Dictionary<int, int>();
        open.Add(new Node { c = sc, r = sr, pc = -1, pr = -1, g = 0, f = H(sc, sr, gc, gr) });
        int[] dc = { 0, 0, 1, -1, 1, 1, -1, -1 }; int[] dr = { 1, -1, 0, 0, 1, -1, 1, -1 }; float[] co = { 1, 1, 1, 1, 1.414f, 1.414f, 1.414f, 1.414f };
        int iters = 0;
        while (open.Count > 0 && iters++ < 8000)
        {
            int bi = 0; for (int i = 1; i < open.Count; i++) if (open[i].f < open[bi].f) bi = i;
            var cur = open[bi]; open.RemoveAt(bi);
            int ck = Key(cur.c, cur.r); if (closed.Contains(ck)) continue; closed.Add(ck);
            if (cur.pc >= 0) came[ck] = Key(cur.pc, cur.pr);
            if (cur.c == gc && cur.r == gr)
            {
                var path = new List<Vector2Int>(); int k = ck;
                while (came.ContainsKey(k)) { path.Add(new Vector2Int(k % gridCols, k / gridCols)); k = came[k]; }
                path.Add(new Vector2Int(sc, sr)); path.Reverse(); return path;
            }
            for (int d = 0; d < 8; d++)
            {
                int nc = cur.c + dc[d], nr = cur.r + dr[d];
                if (nc < 0 || nc >= gridCols || nr < 0 || nr >= gridRows) continue;
                if (!_walkable[nc, nr]) continue;
                int nk = Key(nc, nr); if (closed.Contains(nk)) continue;
                float ng = cur.g + co[d];
                open.Add(new Node { c = nc, r = nr, pc = cur.c, pr = cur.r, g = ng, f = ng + H(nc, nr, gc, gr) });
            }
        }
        return null;
    }

    float H(int c, int r, int gc, int gr) => Mathf.Sqrt((c - gc) * (c - gc) + (r - gr) * (r - gr));
    int Key(int c, int r) => r * gridCols + c;

    (int c, int r) FindNearestWalkableCell(int gc, int gr)
    {
        for (int rad = 1; rad < Mathf.Max(gridCols, gridRows); rad++)
            for (int c = gc - rad; c <= gc + rad; c++)
                for (int r = gr - rad; r <= gr + rad; r++)
                { if (c < 0 || c >= gridCols || r < 0 || r >= gridRows) continue; if (_walkable[c, r]) return (c, r); }
        return (gc, gr);
    }

    int WorldToCol(float x) => Mathf.FloorToInt((x - _gridBounds.min.x) / _cellW);
    int WorldToRow(float y) => Mathf.FloorToInt((y - _gridBounds.min.y) / _cellH);
    Vector2 GridToWorld(int c, int r) => new Vector2(_gridBounds.min.x + (c + 0.5f) * _cellW, _gridBounds.min.y + (r + 0.5f) * _cellH);
}

// ============================================================
//  MapWasdZoneClamp
//  LateUpdate: clamps player back inside WalkableZone after
//  WorldMapManager moves them via WASD. No changes to WMM needed.
// ============================================================
public class MapWasdZoneClamp : MonoBehaviour
{
    MapNavAgent _nav;
    void Start() => _nav = GetComponent<MapNavAgent>();
    void LateUpdate() { if (_nav != null) _nav.ClampToWalkZone(); }
}

// ============================================================
//  MapWalkAnimator
//  2fps walk cycle. Detects movement from WASD + click-nav.
//  Persistent across scenes via DontDestroyOnLoad on the player.
// ============================================================
public class MapWalkAnimator : MonoBehaviour
{
    [Header("Sprites")]
    public Sprite idleSprite;
    public Sprite walkSprite;

    [Header("Animation")]
    public float fps = 2f;

    SpriteRenderer _sr;
    MapNavAgent _nav;
    float _timer;
    bool _walkFrame;
    Vector3 _lastPos;

    void Start()
    {
        _sr = GetComponent<SpriteRenderer>();
        _nav = GetComponent<MapNavAgent>();
        _lastPos = transform.position;
    }

    void Update()
    {
        bool moving = IsMoving();
        if (!moving)
        {
            if (_sr != null && idleSprite != null) _sr.sprite = idleSprite;
            _timer = 0f; _walkFrame = false; _lastPos = transform.position; return;
        }
        _timer += Time.deltaTime;
        if (_timer >= 1f / fps)
        {
            _timer -= 1f / fps; _walkFrame = !_walkFrame;
            if (_sr != null) _sr.sprite = _walkFrame ? walkSprite : idleSprite;
        }
        _lastPos = transform.position;
    }

    bool IsMoving() =>
        Vector3.Distance(transform.position, _lastPos) > 0.001f ||
        (_nav != null && _nav.IsNavigating) ||
        Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.1f ||
        Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.1f;
}

// ============================================================
//  PersistentPlayer
//  Attach to the Phisherman GO. Calls DontDestroyOnLoad so the
//  player (with MapNavAgent, MapWalkAnimator, MapWasdZoneClamp)
//  survives every scene transition automatically.
//
//  Each scene's WorldMapManager finds the player by tag at Start.
//  The builder sets tag = "Player" and positions a SpawnPoint GO
//  instead of re-creating the player every rebuild.
//
//  On scene load, PersistentPlayer repositions itself at the
//  SpawnPoint GO (named "PlayerSpawn") in the new scene.
// ============================================================
public class PersistentPlayer : MonoBehaviour
{
    static PersistentPlayer _instance;

    void Awake()
    {
        // Singleton — destroy duplicates that arrive from a new scene build
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (_instance == this) _instance = null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Move to the spawn point defined in the new scene (if any)
        var spawn = GameObject.Find("PlayerSpawn");
        if (spawn != null)
            transform.position = spawn.transform.position;

        // Re-wire any MapDoorTrigger / MapPathTrigger / MapCornerZone
        // that reference the player (they might be new scene GOs)
        foreach (var d in FindObjectsByType<MapDoorTrigger>(FindObjectsSortMode.None))
            if (d.player == null) d.player = transform;
        foreach (var t in FindObjectsByType<MapPathTrigger>(FindObjectsSortMode.None))
            if (t.player == null) t.player = transform;
        foreach (var z in FindObjectsByType<MapCornerZone>(FindObjectsSortMode.None))
            if (z.player == null) z.player = transform;
        foreach (var b in FindObjectsByType<MapBlocker>(FindObjectsSortMode.None))
            if (b.player == null) b.player = transform;

        // Re-wire WorldMapManager
        var mgr = FindFirstObjectByType<WorldMapManager>();
        if (mgr != null)
        {
            mgr.playerTransform = transform;
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null) mgr.playerRenderer = sr;
        }

        // Rebuild the A* grid for the new scene's WalkableZone
        var nav = GetComponent<MapNavAgent>();
        if (nav != null) nav.SendMessage("Start", SendMessageOptions.DontRequireReceiver);
    }
}