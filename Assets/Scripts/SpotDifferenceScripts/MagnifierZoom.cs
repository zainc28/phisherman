using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// MagnifierZoom.cs
///
/// Attached to the MagnifyingGlass GameObject.
/// Every LateUpdate it takes a snapshot-style zoom approach:
/// scales the entire canvas and offsets it so that the area under
/// the magnifier appears enlarged inside the circular lens mask.
///
/// How it works:
///   - The ZoomLayer is a child of the circular mask.
///   - We set its scale to zoomScale and its anchoredPosition so that
///     the point under the magnifier centre maps to the lens centre.
///   - The circular Mask clips everything outside the lens.
///
/// This is the standard Unity UI zoom trick — no RenderTexture needed.
/// </summary>
public class MagnifierZoom : MonoBehaviour
{
    [Header("References — wired by builder")]
    public RectTransform zoomLayerRT;    // child of MagMask, will be scaled
    public RectTransform mainCanvasRT;   // root canvas RectTransform
    public RectTransform magnifierRT;    // the magnifier GO's own RT (follows cursor)

    [Header("Settings")]
    public float zoomScale = 2.2f;

    void LateUpdate()
    {
        if (zoomLayerRT == null || mainCanvasRT == null || magnifierRT == null) return;
        if (!gameObject.activeSelf) return;

        // Position of magnifier centre in canvas local space
        Vector2 magCentre = magnifierRT.anchoredPosition;

        // The ZoomLayer mirrors the full canvas.
        // We need: canvas_point_under_mag_centre → maps to lens centre.
        // If ZoomLayer scale = S, offset = -magCentre * (S - 1)
        // This means the point at magCentre in canvas space ends up at (0,0)
        // in the mask's local space (i.e. the lens centre).

        zoomLayerRT.localScale = new Vector3(zoomScale, zoomScale, 1f);
        zoomLayerRT.anchoredPosition = -magCentre * (zoomScale - 1f);

        // The ZoomLayer needs to span the full canvas to show any area
        zoomLayerRT.sizeDelta = mainCanvasRT.sizeDelta == Vector2.zero
            ? new Vector2(1920f, 1080f)
            : mainCanvasRT.sizeDelta;

        // Anchor to centre of mask so the position math works
        zoomLayerRT.anchorMin = new Vector2(0.5f, 0.5f);
        zoomLayerRT.anchorMax = new Vector2(0.5f, 0.5f);
        zoomLayerRT.pivot = new Vector2(0.5f, 0.5f);
    }
}