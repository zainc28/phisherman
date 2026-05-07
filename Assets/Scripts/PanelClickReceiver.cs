using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Sits on the scam email's body area. Catches any click that didn't hit
/// a DifferenceMarker (which sit on top in the hierarchy and consume their
/// own clicks). Fires the wrong-click penalty on the manager.
///
/// Why on the body and not on the whole panel: we don't want the
/// "PHISHING EXAMPLE" header strip or the white margin to count as wrong
/// clicks — only clicks on the actual email content should penalize.
/// </summary>
public class PanelClickReceiver : MonoBehaviour, IPointerClickHandler
{
    public SpotDifferenceManager manager;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (manager != null) manager.OnWrongClick();
    }
}