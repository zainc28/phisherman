using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// ─────────────────────────────────────────────────────────────────────────────
// DifferenceMarker
// ─────────────────────────────────────────────────────────────────────────────

[RequireComponent(typeof(Image))]
public class DifferenceMarker : MonoBehaviour, IPointerDownHandler
{
    [Tooltip("Short label shown in the found popup")]
    public string flagName;

    [TextArea(3, 6)]
    public string explanation;

    [HideInInspector] public bool found;
    [HideInInspector] public SpotDifferenceManager manager;
    [HideInInspector] public Image penCircleImage;
    [HideInInspector] public GameObject stickyNote;

    private Image _hitZone;

    public static int LastMarkerClickFrame { get; private set; } = -1;

    private static readonly Color HiddenColor = new Color(1f, 0f, 0f, 0f);
    private static readonly Color FoundWaterColor = new Color(0.15f, 0.50f, 0.90f, 0.22f);

    void Awake()
    {
        _hitZone = GetComponent<Image>();
        _hitZone.color = HiddenColor;
        _hitZone.raycastTarget = true;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        LastMarkerClickFrame = Time.frameCount;
        if (found) return;
        found = true;
        _hitZone.color = FoundWaterColor;
        if (penCircleImage != null) penCircleImage.gameObject.SetActive(true);
        if (stickyNote != null) stickyNote.SetActive(true);
        manager?.OnDifferenceFound(this);
    }
}
