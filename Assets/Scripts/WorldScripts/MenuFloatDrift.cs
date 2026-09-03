using UnityEngine;

// ============================================================
//  MenuFloatDrift
//  Tiny sine-wave vertical drift for decorative UI elements (the
//  main menu's floating fish silhouettes). Unscaled time.
// ============================================================
public class MenuFloatDrift : MonoBehaviour
{
    public float amplitude = 0.3f;
    public float period = 3f;

    RectTransform _rt;
    Vector2 _basePos;
    float _phase;

    void Awake()
    {
        _rt = GetComponent<RectTransform>();
        _basePos = _rt.anchoredPosition;
        _phase = Random.Range(0f, Mathf.PI * 2f);
    }

    void Update()
    {
        if (_rt == null || period <= 0f) return;
        float y = Mathf.Sin((Time.unscaledTime / period) * Mathf.PI * 2f + _phase) * amplitude;
        _rt.anchoredPosition = new Vector2(_basePos.x, _basePos.y + y);
    }
}
