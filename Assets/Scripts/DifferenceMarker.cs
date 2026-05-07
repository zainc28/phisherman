using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// An invisible click zone overlaid on a single phishing red flag in the
/// scam email. When clicked, marks itself "found", flashes a red highlight,
/// and reports back to the SpotDifferenceManager.
///
/// Created by SpotDifferenceBuilder. Each marker stores a short flagName
/// (used in the floating feedback popup) and a longer explanation
/// (shown in the result screen breakdown).
/// </summary>
[RequireComponent(typeof(Image))]
public class DifferenceMarker : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("Short label shown in the +points popup, e.g. 'Spoofed sender'")]
    public string flagName;

    [Tooltip("Long-form explanation shown in the result screen breakdown")]
    [TextArea(3, 6)]
    public string explanation;

    [HideInInspector] public bool found;
    [HideInInspector] public SpotDifferenceManager manager;

    private Image image;

    // Hidden when not found, semi-transparent red when found
    private static readonly Color HiddenColor = new Color(1f, 0f, 0f, 0f);
    private static readonly Color FoundColor = new Color(1f, 0.25f, 0.25f, 0.45f);

    void Awake()
    {
        image = GetComponent<Image>();
        image.color = HiddenColor;
        image.raycastTarget = true; // must be true to receive clicks even when invisible
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (found) return;       // already found; absorb click silently so panel doesn't penalize
        found = true;
        image.color = FoundColor;
        if (manager != null) manager.OnDifferenceFound(this);
    }
}