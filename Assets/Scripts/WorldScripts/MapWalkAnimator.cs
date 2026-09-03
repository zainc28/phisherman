using UnityEngine;

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
