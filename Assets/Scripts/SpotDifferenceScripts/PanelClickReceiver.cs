using UnityEngine;
using UnityEngine.EventSystems;

// ─────────────────────────────────────────────────────────────────────────────
// PanelClickReceiver
// ─────────────────────────────────────────────────────────────────────────────

public class PanelClickReceiver : MonoBehaviour, IPointerDownHandler
{
    public SpotDifferenceManager manager;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (DifferenceMarker.LastMarkerClickFrame == Time.frameCount) return;
        manager?.OnWrongClick();
    }
}
