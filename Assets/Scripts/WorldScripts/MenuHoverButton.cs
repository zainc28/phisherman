using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Attach to any UI element to give it a smooth scale-up on hover
/// and scale-down on pointer exit. Works alongside Button's color block.
///
/// Used by MainMenuBuilder for the invisible hit-zone buttons placed
/// over the baked-in main menu art.
/// </summary>
public class MenuHoverButton : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Scale targets")]
    public Vector3 normalScale = Vector3.one;
    public Vector3 hoverScale = new Vector3(1.04f, 1.04f, 1f);
    public Vector3 pressScale = new Vector3(0.97f, 0.97f, 1f);

    [Header("Animation")]
    [Tooltip("Higher = faster lerp. 8 feels snappy but not jarring.")]
    public float animSpeed = 8f;

    Vector3 _target;
    RectTransform _rt;

    void Awake()
    {
        _rt = GetComponent<RectTransform>();
        _target = normalScale;
    }

    void Update()
    {
        if (_rt == null) return;
        _rt.localScale = Vector3.Lerp(_rt.localScale, _target, Time.unscaledDeltaTime * animSpeed);
    }

    public void OnPointerEnter(PointerEventData _) => _target = hoverScale;
    public void OnPointerExit(PointerEventData _) => _target = normalScale;
    public void OnPointerDown(PointerEventData _) => _target = pressScale;
    public void OnPointerUp(PointerEventData _) => _target = hoverScale;   // back to hover, not normal (still over)
}