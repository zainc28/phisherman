using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// MagnifierZoom.cs
///
/// Refactored to use the Camera + RenderTexture technique. This is completely
/// bulletproof for zooming UI without duplicating massive layout groups.
/// 
/// The SpotDifferenceBuilder sets the Main Canvas to ScreenSpaceCamera.
/// This script creates a secondary camera, points it where the cursor is, 
/// narrows the orthographic field of view to zoom in, and renders the 
/// result into the circular RawImage mask.
/// </summary>
public class MagnifierZoom : MonoBehaviour
{
    [Header("References — wired by builder")]
    public RectTransform mainCanvasRT;   // root canvas RectTransform
    public RectTransform magnifierRT;    // the magnifier GO's own RT (follows cursor)
    public GameObject maskGo;            // the GameObject containing the Mask

    [Header("Settings")]
    public float zoomScale = 2.2f;

    private Camera _magCam;
    private RenderTexture _rt;

    void Start()
    {
        if (Camera.main == null || maskGo == null) return;

        // 1. Create the render texture for the lens
        _rt = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32);
        _rt.Create();

        // 2. Create the secondary zoomed camera
        var camObj = new GameObject("MagCamera");
        camObj.transform.SetParent(transform);
        _magCam = camObj.AddComponent<Camera>();

        // Inherit everything from Main Camera
        _magCam.CopyFrom(Camera.main);
        _magCam.clearFlags = CameraClearFlags.SolidColor;
        _magCam.backgroundColor = Camera.main.backgroundColor;
        _magCam.targetTexture = _rt;

        // Zoom in by reducing orthographic size
        _magCam.orthographicSize = Camera.main.orthographicSize / zoomScale;

        // 3. Apply the texture to a RawImage inside the mask
        var rawImg = maskGo.AddComponent<RawImage>();
        rawImg.texture = _rt;
        rawImg.raycastTarget = false;

        // Ensure raw image fills the mask perfectly
        rawImg.rectTransform.anchorMin = Vector2.zero;
        rawImg.rectTransform.anchorMax = Vector2.one;
        rawImg.rectTransform.offsetMin = Vector2.zero;
        rawImg.rectTransform.offsetMax = Vector2.zero;
    }

    void LateUpdate()
    {
        if (_magCam == null || Camera.main == null) return;

        // Match camera position to world position of magnifier center
        Vector3 worldPos = magnifierRT.position;
        _magCam.transform.position = new Vector3(worldPos.x, worldPos.y, Camera.main.transform.position.z);
    }

    void OnDestroy()
    {
        if (_rt != null) _rt.Release();
    }
}