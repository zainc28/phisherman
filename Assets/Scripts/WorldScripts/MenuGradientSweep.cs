using UnityEngine;

// ============================================================
//  MenuGradientSweep
//  Slides a large diagonal band left-to-right across its parent,
//  looping every `period` seconds. Used for the main menu's
//  animated background gradient sweep. Unscaled time so it keeps
//  animating even if the menu is ever shown while paused.
// ============================================================
public class MenuGradientSweep : MonoBehaviour
{
    public float period = 8f;
    public float travelDistance = 2600f; // px, wide enough to clear a 1920-wide reference canvas

    RectTransform _rt;
    float _startX;

    void Awake()
    {
        _rt = GetComponent<RectTransform>();
        _startX = _rt.anchoredPosition.x;
    }

    void Update()
    {
        if (_rt == null || period <= 0f) return;
        float t = (Time.unscaledTime % period) / period;
        var pos = _rt.anchoredPosition;
        pos.x = _startX - travelDistance * 0.5f + travelDistance * t;
        _rt.anchoredPosition = pos;
    }
}
