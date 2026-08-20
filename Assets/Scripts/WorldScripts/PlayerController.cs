using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 3.5f;
    public Vector2 boundsX = new Vector2(-7.5f, 7.5f);
    public Vector2 boundsY = new Vector2(-4.5f, 4.5f);

    // Walk animation
    public float walkFrameInterval = 0.18f; // seconds between sprite swaps

    SpriteRenderer _sr;
    Sprite _idleSprite;
    Sprite _walkSprite;

    // Click-to-move
    Vector3? _clickTarget;

    // Polygon walkable zone
    Vector2[] _zoneVerts;
    bool _hasZone;

    // Animation
    float _animTimer;
    bool _walkFrame;
    bool _wasMoving;

    void Start()
    {
        _sr = GetComponent<SpriteRenderer>();
        if (_sr != null) _idleSprite = _sr.sprite;

        // Find phisherman_walking sprite from the project
        _walkSprite = FindWalkSprite();

        // Read WalkableZone polygon
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
        if (Input.GetMouseButtonDown(0) && Camera.main != null)
        {
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            worldPos.z = transform.position.z;
            // Only set target if click is within bounds
            if (worldPos.x >= boundsX.x && worldPos.x <= boundsX.y &&
                worldPos.y >= boundsY.x && worldPos.y <= boundsY.y)
            {
                _clickTarget = worldPos;
            }
        }

        // ── WASD cancels click target ────────────────────────
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
                    _clickTarget = null; // hit zone boundary, stop

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
                // Reset to idle when stopped
                _animTimer = 0f;
                _walkFrame = false;
                _sr.sprite = _idleSprite;
            }
        }

        _wasMoving = moving;
    }

    // Returns true if pos is inside the walkable polygon, or if no polygon exists
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

    // Finds phisherman_walking sprite without requiring a project reference
    static Sprite FindWalkSprite()
    {
        // Search all loaded SpriteRenderers in the scene for a cached reference
        // Fall back to searching resources if not found
        var allRenderers = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
        foreach (var sr in allRenderers)
        {
            if (sr.sprite != null &&
                sr.sprite.name.ToLower().Contains("phisherman_walking"))
                return sr.sprite;
        }

        // Try MapWalkAnimator on this object — it already has the walk sprite
        var animator = FindObjectOfType<MapWalkAnimator>();
        if (animator != null && animator.walkSprite != null)
            return animator.walkSprite;

        return null;
    }
}