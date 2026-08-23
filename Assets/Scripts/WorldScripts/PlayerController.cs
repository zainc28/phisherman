using UnityEngine;
using UnityEngine.EventSystems;

// ============================================================
//  PlayerController
//  WASD + click-to-move for all world map scenes (1-5).
//
//  FIX: Added IsPointerOverGameObject() guard to the mouse-click
//  handler. Without this, clicking the [M] World Map button (or
//  any other UI button) simultaneously triggered a click-to-move
//  in world space, which looked like the map wasn't opening even
//  though it was — the player just moved and the map panel
//  appeared briefly before being obscured.
// ============================================================
public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 3.5f;
    public Vector2 boundsX = new Vector2(-7.5f, 7.5f);
    public Vector2 boundsY = new Vector2(-4.5f, 4.5f);

    public float walkFrameInterval = 0.18f;

    SpriteRenderer _sr;
    Sprite _idleSprite;
    Sprite _walkSprite;

    Vector3? _clickTarget;

    Vector2[] _zoneVerts;
    bool _hasZone;

    float _animTimer;
    bool _walkFrame;

    void Start()
    {
        _sr = GetComponent<SpriteRenderer>();
        if (_sr != null) _idleSprite = _sr.sprite;
        _walkSprite = FindWalkSprite();

        var zoneGo = GameObject.Find("WalkableZone");
        if (zoneGo != null)
        {
            var pc = zoneGo.GetComponent<PolygonCollider2D>();
            if (pc != null && pc.points.Length >= 3)
            {
                Vector2 offset = zoneGo.transform.position;
                var pts = pc.points;
                _zoneVerts = new Vector2[pts.Length];
                for (int i = 0; i < pts.Length; i++)
                    _zoneVerts[i] = pts[i] + offset;
                _hasZone = true;
            }
        }
    }

    void Update()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        bool wasdActive = Mathf.Abs(h) > 0.01f || Mathf.Abs(v) > 0.01f;

        // ── Click to move ────────────────────────────────────
        // FIX: guard with IsPointerOverGameObject so clicking any
        // UI element (map button, CTA badge, close button, etc.)
        // never also triggers a world-space move.
        if (Input.GetMouseButtonDown(0) && Camera.main != null)
        {
            var es = EventSystem.current;
            bool overUI = es != null && es.IsPointerOverGameObject();
            if (!overUI)
            {
                Vector3 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                worldPos.z = transform.position.z;
                if (worldPos.x >= boundsX.x && worldPos.x <= boundsX.y &&
                    worldPos.y >= boundsY.x && worldPos.y <= boundsY.y)
                {
                    _clickTarget = worldPos;
                }
            }
        }

        if (wasdActive) _clickTarget = null;

        bool moving = false;

        if (wasdActive)
        {
            Vector2 dir = new Vector2(h, v);
            if (dir.magnitude > 1f) dir.Normalize();

            Vector3 newPos = transform.position;
            newPos.x = Mathf.Clamp(newPos.x + dir.x * moveSpeed * Time.deltaTime, boundsX.x, boundsX.y);
            newPos.y = Mathf.Clamp(newPos.y + dir.y * moveSpeed * Time.deltaTime, boundsY.x, boundsY.y);

            if (IsInsideZone(newPos))
                transform.position = newPos;

            if (_sr != null && Mathf.Abs(h) > 0.01f) _sr.flipX = h < 0;
            moving = true;
        }
        else if (_clickTarget.HasValue)
        {
            Vector3 target = _clickTarget.Value;
            Vector3 current = transform.position;
            float dist = Vector3.Distance(current, target);

            if (dist < 0.05f)
            {
                transform.position = target;
                _clickTarget = null;
            }
            else
            {
                Vector3 step = (target - current).normalized * moveSpeed * Time.deltaTime;
                if (step.magnitude > dist) step = target - current;
                Vector3 newPos = current + step;

                if (IsInsideZone(newPos))
                    transform.position = newPos;
                else
                    _clickTarget = null;

                if (_sr != null)
                {
                    float dx = target.x - current.x;
                    if (Mathf.Abs(dx) > 0.01f) _sr.flipX = dx < 0;
                }
                moving = true;
            }
        }

        // ── Walk animation ───────────────────────────────────
        if (_sr != null && _walkSprite != null && _idleSprite != null)
        {
            if (moving)
            {
                _animTimer += Time.deltaTime;
                if (_animTimer >= walkFrameInterval)
                {
                    _animTimer -= walkFrameInterval;
                    _walkFrame = !_walkFrame;
                    _sr.sprite = _walkFrame ? _walkSprite : _idleSprite;
                }
            }
            else
            {
                _animTimer = 0f;
                _walkFrame = false;
                _sr.sprite = _idleSprite;
            }
        }
    }

    bool IsInsideZone(Vector3 pos)
    {
        if (!_hasZone || _zoneVerts == null) return true;
        return PointInPolygon(pos, _zoneVerts);
    }

    static bool PointInPolygon(Vector2 p, Vector2[] verts)
    {
        int n = verts.Length;
        bool inside = false;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            float xi = verts[i].x, yi = verts[i].y;
            float xj = verts[j].x, yj = verts[j].y;
            bool intersect = ((yi > p.y) != (yj > p.y)) &&
                             (p.x < (xj - xi) * (p.y - yi) / (yj - yi) + xi);
            if (intersect) inside = !inside;
        }
        return inside;
    }

    static Sprite FindWalkSprite()
    {
        var animator = FindObjectOfType<MapWalkAnimator>();
        if (animator != null && animator.walkSprite != null)
            return animator.walkSprite;
        var allRenderers = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
        foreach (var sr in allRenderers)
            if (sr.sprite != null && sr.sprite.name.ToLower().Contains("phisherman_walking"))
                return sr.sprite;
        return null;
    }
}