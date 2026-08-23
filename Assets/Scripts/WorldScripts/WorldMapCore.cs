using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// ============================================================
//  PolygonUtils
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

// ============================================================
//  MapBlocker
// ============================================================
public class MapBlocker : MonoBehaviour
{
    public Transform player; public float halfW = 1f, halfH = 1f;
    void LateUpdate()
    {
        if (!player) return;
        Vector2 c = transform.position, pos = player.position;
        float l = c.x - halfW, r = c.x + halfW, b = c.y - halfH, t = c.y + halfH;
        if (pos.x > l && pos.x < r && pos.y > b && pos.y < t)
        {
            float dL = pos.x - l, dR = r - pos.x, dB = pos.y - b, dT = t - pos.y, m = Mathf.Min(dL, dR, dB, dT);
            Vector3 pp = player.position;
            if (m == dL) pp.x = l - 0.01f;
            else if (m == dR) pp.x = r + 0.01f;
            else if (m == dB) pp.y = b - 0.01f; else pp.y = t + 0.01f;
            player.position = pp;
        }
    }
}

// ============================================================
//  MapDoorTrigger
// ============================================================
public class MapDoorTrigger : MonoBehaviour
{
    public Transform player; public string targetScene;
    public GameObject prompt; public float promptProximity = 1.8f, triggerRadius = 0.55f;
    public float bobAmount = 0.08f, bobSpeed = 2.2f;
    bool _fired; float _bobBase;
    void Start() { if (prompt) { _bobBase = prompt.transform.localPosition.y; prompt.SetActive(false); } }
    void Update()
    {
        if (_fired || !player) return;
        float dist = Vector2.Distance(transform.position, player.position);
        if (prompt) { bool show = dist < promptProximity; if (prompt.activeSelf != show) prompt.SetActive(show); if (show) { var lp = prompt.transform.localPosition; lp.y = _bobBase + Mathf.Sin(Time.time * bobSpeed) * bobAmount; prompt.transform.localPosition = lp; } }
        if (dist < triggerRadius && !string.IsNullOrEmpty(targetScene)) { _fired = true; SceneManager.LoadScene(targetScene); }
    }
}

// ============================================================
//  MapPathTrigger
// ============================================================
public class MapPathTrigger : MonoBehaviour
{
    public enum TriggerType { LoadScene, LockedPopup }
    public Transform player; public TriggerType type = TriggerType.LoadScene;
    public string targetScene = ""; public GameObject lockedPopup;
    public float popupDuration = 2.8f, promptProximity = 2.2f, triggerRadius = 1.0f;
    public float bobAmount = 0.10f, bobSpeed = 2.0f; public GameObject prompt;
    public int requiresWorldComplete = 0;
    bool _fired, _popupActive; float _popupTimer, _bobBase;
    void Start() { if (prompt) { _bobBase = prompt.transform.localPosition.y; prompt.SetActive(false); } if (lockedPopup) lockedPopup.SetActive(false); }
    void Update()
    {
        if (!player || _fired) return;
        float dist = Vector2.Distance(transform.position, player.position);
        if (prompt) { bool show = dist < promptProximity; if (prompt.activeSelf != show) prompt.SetActive(show); if (show) { var lp = prompt.transform.localPosition; lp.y = _bobBase + Mathf.Sin(Time.time * bobSpeed) * bobAmount; prompt.transform.localPosition = lp; } }
        if (_popupActive) { _popupTimer -= Time.deltaTime; if (_popupTimer <= 0f) { _popupActive = false; if (lockedPopup) lockedPopup.SetActive(false); } }
        if (dist < triggerRadius)
        {
            if (type == TriggerType.LoadScene && !string.IsNullOrEmpty(targetScene))
            {
                if (requiresWorldComplete > 0 && !WorldProgress.IsWorldComplete(requiresWorldComplete))
                { if (!_popupActive && lockedPopup) { _popupActive = true; _popupTimer = popupDuration; lockedPopup.SetActive(true); } return; }
                _fired = true; if (prompt) prompt.SetActive(false); SceneManager.LoadScene(targetScene);
            }
            else if (type == TriggerType.LockedPopup && !_popupActive) { _popupActive = true; _popupTimer = popupDuration; if (lockedPopup) lockedPopup.SetActive(true); }
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
    public Transform player; public Corner corner = Corner.BottomLeft;
    public ZoneAction action = ZoneAction.LoadScene; public string targetScene = "";
    public GameObject lockedPopup; public float popupDuration = 2.8f;
    public int zonePixels = 200, requiresWorldComplete = 0;
    bool _fired, _popupActive; float _popupTimer;
    void Start() { if (lockedPopup) lockedPopup.SetActive(false); }
    void Update()
    {
        if (!player || _fired || !Camera.main) return;
        Vector2 sp = Camera.main.WorldToScreenPoint(player.position);
        bool inZone = corner == Corner.BottomLeft ? sp.x < zonePixels && sp.y < zonePixels : sp.x > Screen.width - zonePixels && sp.y < zonePixels;
        if (_popupActive) { _popupTimer -= Time.deltaTime; if (_popupTimer <= 0f) { _popupActive = false; if (lockedPopup) lockedPopup.SetActive(false); } }
        if (inZone)
        {
            if (action == ZoneAction.LoadScene && !string.IsNullOrEmpty(targetScene))
            {
                if (requiresWorldComplete > 0 && !WorldProgress.IsWorldComplete(requiresWorldComplete))
                { if (!_popupActive && lockedPopup) { _popupActive = true; _popupTimer = popupDuration; lockedPopup.SetActive(true); } return; }
                _fired = true; SceneManager.LoadScene(targetScene);
            }
            else if (action == ZoneAction.LockedPopup && !_popupActive) { _popupActive = true; _popupTimer = popupDuration; if (lockedPopup) lockedPopup.SetActive(true); }
        }
    }
}

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

// ============================================================
//  MapWasdZoneClamp
// ============================================================
public class MapWasdZoneClamp : MonoBehaviour
{
    MapNavAgent _nav; Vector2[] _worldVerts; bool _ready;
    void Start()
    {
        _nav = GetComponent<MapNavAgent>();
        var zoneGo = GameObject.Find("WalkableZone"); if (!zoneGo) return;
        var pc = zoneGo.GetComponent<PolygonCollider2D>(); if (!pc) return;
        var pts = pc.points; Vector2 off = zoneGo.transform.position;
        _worldVerts = new Vector2[pts.Length]; for (int i = 0; i < pts.Length; i++) _worldVerts[i] = pts[i] + off;
        _ready = true;
    }
    void LateUpdate()
    {
        if (!_ready || _worldVerts == null || _worldVerts.Length < 3) return;
        Vector2 pos = transform.position;
        if (!PolygonUtils.PointInPolygon(pos, _worldVerts)) { Vector2 cl = PolygonUtils.NearestEdgePoint(pos, _worldVerts); transform.position = new Vector3(cl.x, cl.y, transform.position.z); if (_nav != null && _nav.IsNavigating) _nav.CancelPath(); }
    }
}

// ============================================================
//  MapWalkAnimator
// ============================================================
public class MapWalkAnimator : MonoBehaviour
{
    public Sprite idleSprite, walkSprite; public float fps = 2f;
    SpriteRenderer _sr; MapNavAgent _nav; float _timer; bool _walkFrame; Vector3 _lastPos;
    void Start() { _sr = GetComponent<SpriteRenderer>(); _nav = GetComponent<MapNavAgent>(); _lastPos = transform.position; }
    void Update()
    {
        bool moving = Vector3.Distance(transform.position, _lastPos) > 0.001f || (_nav != null && _nav.IsNavigating) || Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.1f || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.1f;
        if (!moving) { if (_sr && idleSprite) _sr.sprite = idleSprite; _timer = 0; _walkFrame = false; _lastPos = transform.position; return; }
        _timer += Time.deltaTime;
        if (_timer >= 1f / fps) { _timer -= 1f / fps; _walkFrame = !_walkFrame; if (_sr) _sr.sprite = _walkFrame ? walkSprite : idleSprite; }
        _lastPos = transform.position;
    }
}

// ============================================================
//  MapDoorCTA
// ============================================================
public class MapDoorCTA : MonoBehaviour
{
    public string targetScene; public Transform playerTransform, doorPosition;
    public float bobAmount = 0.16f, bobSpeed = 2.8f, clickRadius = 1.2f, proximityRadius = 0.55f;
    public TMPro.TextMeshProUGUI label;
    public string completionKey = ""; public Color activeColor = Color.white, completedColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
    public GameObject completedLockPopup; public string completedLockMessage = "Already helped here! Try another building.";
    float _baseY; bool _completed, _walkingToDoor, _proxFired;

    void Start()
    {
        _baseY = transform.localPosition.y;
        _completed = !string.IsNullOrEmpty(completionKey) && PlayerPrefs.GetInt(completionKey, 0) == 1;
        RefreshColor();
    }

    void Update()
    {
        if (!string.IsNullOrEmpty(completionKey))
        {
            bool now = PlayerPrefs.GetInt(completionKey, 0) == 1;
            if (now != _completed) { _completed = now; RefreshColor(); _proxFired = false; }
        }
        if (!_completed) { var lp = transform.localPosition; lp.y = _baseY + Mathf.Sin(Time.time * bobSpeed) * bobAmount; transform.localPosition = lp; transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * bobSpeed * 1.4f + 0.6f) * 0.06f); }
        else { var lp = transform.localPosition; lp.y = _baseY; transform.localPosition = lp; transform.localScale = Vector3.one; }

        if (playerTransform && doorPosition)
        {
            float dist = Vector2.Distance(playerTransform.position, doorPosition.position);
            if (_proxFired && dist > proximityRadius * 2f) _proxFired = false;
            if (!_proxFired && dist < proximityRadius) { _proxFired = true; OnClick(); }
        }
        if (Input.GetMouseButtonDown(0) && Camera.main)
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null || !es.IsPointerOverGameObject())
            { Vector2 mw = Camera.main.ScreenToWorldPoint(Input.mousePosition); if (Vector2.Distance(mw, (Vector2)transform.position) < clickRadius) OnClick(); }
        }
        if (_walkingToDoor && playerTransform && doorPosition && Vector2.Distance(playerTransform.position, doorPosition.position) < proximityRadius)
        { _walkingToDoor = false; if (!string.IsNullOrEmpty(targetScene)) SceneManager.LoadScene(targetScene); }
    }

    void RefreshColor() { if (label) label.color = _completed ? completedColor : activeColor; }

    public void OnClick()
    {
        if (_completed) { if (completedLockPopup) { var c = completedLockPopup.GetComponent<LockedPopupController>(); if (c) c.Show(completedLockMessage); else completedLockPopup.SetActive(true); } return; }
        if (!playerTransform || !doorPosition) return;
        var nav = playerTransform.GetComponent<MapNavAgent>();
        if (nav) { nav.RequestPath(doorPosition.position); _walkingToDoor = true; }
        else if (!string.IsNullOrEmpty(targetScene)) SceneManager.LoadScene(targetScene);
    }
}

// ============================================================
//  CTABorderFader
// ============================================================
public class CTABorderFader : MonoBehaviour
{
    public Image borderImage; public string completionKey = "";
    public Color activeColor = Color.white, completedColor = new Color(0.3f, 0.3f, 0.3f, 0.4f);
    bool _last;
    void Start() => Refresh(); void Update() => Refresh();
    void Refresh() { if (!borderImage || string.IsNullOrEmpty(completionKey)) return; bool done = PlayerPrefs.GetInt(completionKey, 0) == 1; if (done == _last) return; _last = done; borderImage.color = done ? completedColor : activeColor; }
}

// ============================================================
//  NPCMarker
// ============================================================
public class NPCMarker : MonoBehaviour { public int npcIndex; }

// ============================================================
//  WorldMapManager
//
//  FIX (map toggle bug):
//  The previous version called ToggleMap() and then immediately
//  checked `if (mapOpen && ...)` in the same frame. After ToggleMap()
//  sets mapOpen = true, the second condition was also true and called
//  CloseMap() — so the map opened and closed in the exact same frame,
//  appearing to never open.
//
//  Fix: capture the key state ONCE at the top of Update() into a bool
//  (`pressedM`), use it for the toggle, and never check mapOpen again
//  in the same expression that set it.
//
//  FIX (works in all 5 worlds):
//  WorldMapManager is a single shared class. Dropping this file in
//  fixes Worlds 1-5 simultaneously. Each world builder assigns
//  worldMapPanel at build time via WireManager / the World1 builder —
//  that wiring is unchanged.
// ============================================================
public class WorldMapManager : MonoBehaviour
{
    [System.Serializable]
    public struct NPCDialogue
    {
        public string speakerName, npcDescription;
        [TextArea(2, 5)] public string[] lines;
        public string acceptText, declineText, sceneToLoad, declineResponse;
    }

    [Header("Player")]
    public Transform playerTransform; public SpriteRenderer playerRenderer;
    public float playerSpeed = 3.5f;
    public Vector2 boundsX = new Vector2(-8f, 8f), boundsY = new Vector2(-3.8f, 1.2f);
    MapNavAgent _playerNav;

    [Header("NPCs")]
    public Transform[] npcTransforms; public GameObject[] exclamationMarks;
    public float exclamationProximity = 3.5f, interactRadius = 1.2f;

    [Header("Dialogue UI")]
    public GameObject dialoguePanel; public Button advanceButton;
    public TMP_Text speakerNameText, dialogueText, continueHint;
    public GameObject choicePanel; public Button acceptButton, declineButton;
    public TMP_Text acceptButtonText, declineButtonText;

    [Header("World Map Overlay")]
    public GameObject worldMapPanel;

    NPCDialogue[] dialogues;
    bool inDialogue, mapOpen;
    int currentNpcIndex = -1, currentLineIndex;
    bool waitingForChoice;
    Vector3? moveTarget;
    bool[] npcTriggered;

    void Start()
    {
        InitializeDialogues();
        npcTriggered = new bool[dialogues.Length];
        if (dialoguePanel) dialoguePanel.SetActive(false);
        if (choicePanel) choicePanel.SetActive(false);
        if (worldMapPanel) worldMapPanel.SetActive(false);
        // Ensure the full-screen advance button never blocks the map hint click at start
        if (advanceButton) advanceButton.gameObject.SetActive(false);
        if (exclamationMarks != null) foreach (var em in exclamationMarks) if (em) em.SetActive(false);

        if (playerTransform) _playerNav = playerTransform.GetComponent<MapNavAgent>();
        if (!_playerNav)
        {
            var pg = GameObject.FindWithTag("Player");
            if (pg) { _playerNav = pg.GetComponent<MapNavAgent>(); if (!playerTransform) playerTransform = pg.transform; if (!playerRenderer) playerRenderer = pg.GetComponent<SpriteRenderer>(); }
        }
    }

    void Update()
    {
        // ── Capture input once — never re-read after acting on it ────────
        bool pressedM = Input.GetKeyDown(KeyCode.M);
        bool pressedEsc = Input.GetKeyDown(KeyCode.Escape);

        // M toggles the map regardless of dialogue state
        if (pressedM)
            ToggleMap();

        // Escape closes the map (if open) — checked separately so a single
        // M press doesn't both open and then immediately close via this block
        if (pressedEsc && mapOpen)
            CloseMap();

        // Gameplay blocked while map is open or during dialogue
        if (inDialogue || mapOpen) return;
        if (!playerTransform) return;

        UpdatePlayer();
        UpdateExclamationMarks();
        CheckProximityTriggers();
        DetectClick();
    }

    // ── Map ───────────────────────────────────────────────────
    public void ToggleMap()
    {
        mapOpen = !mapOpen;
        if (worldMapPanel) worldMapPanel.SetActive(mapOpen);
    }

    public void CloseMap()
    {
        mapOpen = false;
        if (worldMapPanel) worldMapPanel.SetActive(false);
    }

    // ── Player ────────────────────────────────────────────────
    void UpdatePlayer()
    {
        float h = Input.GetAxisRaw("Horizontal"), v = Input.GetAxisRaw("Vertical");
        Vector2 input = new Vector2(h, v);
        if (input.sqrMagnitude > 0.01f)
        {
            moveTarget = null; if (_playerNav != null && _playerNav.IsNavigating) _playerNav.CancelPath();
            if (input.magnitude > 1f) input.Normalize();
            Vector3 np = playerTransform.position + (Vector3)input * playerSpeed * Time.deltaTime;
            np.x = Mathf.Clamp(np.x, boundsX.x, boundsX.y); np.y = Mathf.Clamp(np.y, boundsY.x, boundsY.y);
            playerTransform.position = np;
            if (playerRenderer && Mathf.Abs(input.x) > 0.01f) playerRenderer.flipX = input.x < 0;
            return;
        }
        if (moveTarget.HasValue)
        {
            if (_playerNav != null) { moveTarget = null; return; }
            Vector3 to = moveTarget.Value - playerTransform.position; float dist = to.magnitude;
            if (dist < 0.05f) { playerTransform.position = moveTarget.Value; moveTarget = null; return; }
            Vector3 step = to.normalized * playerSpeed * Time.deltaTime; if (step.magnitude > dist) step = to;
            playerTransform.position += step;
            if (playerRenderer && Mathf.Abs(to.x) > 0.01f) playerRenderer.flipX = to.x < 0;
        }
    }

    void UpdateExclamationMarks()
    {
        if (npcTransforms == null || exclamationMarks == null || !playerTransform) return;
        for (int i = 0; i < npcTransforms.Length; i++)
        {
            if (i >= exclamationMarks.Length || !exclamationMarks[i] || !npcTransforms[i]) continue;
            float dist = Vector3.Distance(playerTransform.position, npcTransforms[i].position);
            bool show = dist < exclamationProximity; exclamationMarks[i].SetActive(show);
            if (show) { float bob = Mathf.Sin(Time.time * 4.5f + i * 1.1f) * 0.12f; exclamationMarks[i].transform.position = new Vector3(npcTransforms[i].position.x, npcTransforms[i].position.y + 1.2f + bob, 0f); }
        }
    }

    void CheckProximityTriggers()
    {
        if (npcTransforms == null || !playerTransform) return;
        for (int i = 0; i < npcTransforms.Length; i++)
        {
            if (!npcTransforms[i]) continue;
            float dist = Vector3.Distance(playerTransform.position, npcTransforms[i].position);
            if (dist < interactRadius && !npcTriggered[i]) { npcTriggered[i] = true; StartDialogue(i); return; }
            if (dist > interactRadius + 0.5f) npcTriggered[i] = false;
        }
    }

    void DetectClick()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es != null && es.IsPointerOverGameObject()) return;
        if (!Camera.main) return;
        Vector2 wp = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Collider2D hit = Physics2D.OverlapPoint(wp);
        if (hit) { var m = hit.GetComponent<NPCMarker>(); if (m) { moveTarget = null; StartDialogue(m.npcIndex); return; } }
        if (_playerNav != null) { _playerNav.RequestPath(new Vector3(wp.x, wp.y, playerTransform.position.z)); moveTarget = null; }
        else moveTarget = new Vector3(Mathf.Clamp(wp.x, boundsX.x, boundsX.y), Mathf.Clamp(wp.y, boundsY.x, boundsY.y), 0f);
    }

    // ── Dialogue ──────────────────────────────────────────────
    public void StartDialogue(int idx)
    {
        if (dialogues == null || idx < 0 || idx >= dialogues.Length) return;
        inDialogue = true; currentNpcIndex = idx; currentLineIndex = 0; waitingForChoice = false;
        if (dialoguePanel) dialoguePanel.SetActive(true);
        if (choicePanel) choicePanel.SetActive(false);
        if (advanceButton) advanceButton.gameObject.SetActive(true);
        if (exclamationMarks != null && idx < exclamationMarks.Length && exclamationMarks[idx]) exclamationMarks[idx].SetActive(false);
        ShowCurrentLine();
    }

    void ShowCurrentLine()
    {
        var dlg = dialogues[currentNpcIndex];
        if (speakerNameText) speakerNameText.text = dlg.speakerName;
        if (dialogueText) dialogueText.text = dlg.lines[currentLineIndex];
        if (continueHint) continueHint.text = "Click to continue...";
    }

    public void AdvanceDialogue()
    {
        if (!inDialogue || waitingForChoice) return;
        currentLineIndex++;
        var dlg = dialogues[currentNpcIndex];
        if (currentLineIndex >= dlg.lines.Length) ShowChoice(); else ShowCurrentLine();
    }

    void ShowChoice()
    {
        var dlg = dialogues[currentNpcIndex]; waitingForChoice = true;
        if (choicePanel) choicePanel.SetActive(true);
        if (acceptButtonText) acceptButtonText.text = dlg.acceptText;
        if (declineButtonText) declineButtonText.text = dlg.declineText;
        if (continueHint) continueHint.text = "";
    }

    public void OnAcceptHelp()
    {
        if (!inDialogue) return;
        var dlg = dialogues[currentNpcIndex];
        if (!string.IsNullOrEmpty(dlg.sceneToLoad)) SceneManager.LoadScene(dlg.sceneToLoad);
    }

    public void OnDeclineHelp()
    {
        if (!inDialogue) return;
        var dlg = dialogues[currentNpcIndex];
        if (choicePanel) choicePanel.SetActive(false); waitingForChoice = false;
        if (!string.IsNullOrEmpty(dlg.declineResponse)) { if (dialogueText) dialogueText.text = dlg.declineResponse; StartCoroutine(EndAfterDelay(2f)); }
        else EndDialogue();
    }

    IEnumerator EndAfterDelay(float d) { yield return new WaitForSeconds(d); EndDialogue(); }

    void EndDialogue()
    {
        if (dialoguePanel) dialoguePanel.SetActive(false);
        if (choicePanel) choicePanel.SetActive(false);
        if (advanceButton) advanceButton.gameObject.SetActive(false);
        inDialogue = false; currentNpcIndex = -1;
    }

    void InitializeDialogues()
    {
        dialogues = new NPCDialogue[]
        {
            new NPCDialogue { speakerName="Grandma Rose", lines=new[]{"Oh, hello dear! I'm so glad you stopped by.","I've been getting frightening emails saying my bank account is frozen!","Could you help me figure out which emails are real and which are scams?"}, acceptText="Let's sort those emails!", declineText="Maybe later", sceneToLoad="ApartmentInterior", declineResponse="Oh alright dear. But please come back soon!" },
            new NPCDialogue { speakerName="Uncle Tony",   lines=new[]{"Hey, come in! I was just about to call you.","The shop computer keeps getting pop-ups saying it's infected with a virus.","They want remote access to 'fix' it. Seems fishy to me."}, acceptText="Let's check those emails!", declineText="Can't right now", sceneToLoad="PizzaInterior", declineResponse="Okay, but hurry back!" },
            new NPCDialogue { speakerName="Grandpa Sal",  lines=new[]{"Ah, just the person I wanted to see.","I got an email that looks exactly like it's from my bank.","Can you spot the differences between this and a real one?"}, acceptText="I'll find those differences!", declineText="Not right now", sceneToLoad="SpotDifference", declineResponse="Very well. But don't wait too long." },
            new NPCDialogue { speakerName="Mrs. Patel",   lines=new[]{"Oh good, you're here! I order packages online every day.","Some delivery emails look like Amazon or FedEx but the links go somewhere strange.","Help me defend my account — these fake emails are coming fast!"}, acceptText="Let's defend that tower!", declineText="Maybe another time", sceneToLoad="TowerDefense", declineResponse="Alright, but those phishers won't wait!" },
            new NPCDialogue { speakerName="Uncle Rajan",  lines=new[]{"Ah, perfect timing! I got a message saying it was from my nephew.","He said he was stranded abroad and needed money urgently.","Can you look carefully and spot what gives them away?"}, acceptText="I'll spot those red flags!", declineText="Not today", sceneToLoad="SpotDifference", declineResponse="Okay. Share this with your family." },
        };
    }
}