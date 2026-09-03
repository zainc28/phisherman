using UnityEngine;
using UnityEngine.SceneManagement;

// ============================================================
//  MapCornerZone
// ============================================================
public class MapCornerZone : MonoBehaviour
{
    public enum Corner { BottomLeft, BottomRight }
    public enum ZoneAction { LoadScene, LockedPopup }
    public Transform player; public Corner corner = Corner.BottomLeft;
    public ZoneAction action = ZoneAction.LoadScene; public string targetScene = "";
    public GameObject lockedPopup; public float popupDuration = 2.8f;
    public int zonePixels = 200, requiresWorldComplete = 0;
    bool _fired, _popupActive; float _popupTimer;
    void Start() { if (lockedPopup) lockedPopup.SetActive(false); }
    void Update()
    {
        if (!player || _fired || !Camera.main) return;
        Vector2 sp = Camera.main.WorldToScreenPoint(player.position);
        bool inZone = corner == Corner.BottomLeft ? sp.x < zonePixels && sp.y < zonePixels : sp.x > Screen.width - zonePixels && sp.y < zonePixels;
        if (_popupActive) { _popupTimer -= Time.deltaTime; if (_popupTimer <= 0f) { _popupActive = false; if (lockedPopup) lockedPopup.SetActive(false); } }
        if (inZone)
        {
            if (action == ZoneAction.LoadScene && !string.IsNullOrEmpty(targetScene))
            {
                if (requiresWorldComplete > 0 && !WorldProgress.IsWorldComplete(requiresWorldComplete))
                { if (!_popupActive && lockedPopup) { _popupActive = true; _popupTimer = popupDuration; lockedPopup.SetActive(true); } return; }
                _fired = true; SceneManager.LoadScene(targetScene);
            }
            else if (action == ZoneAction.LockedPopup && !_popupActive) { _popupActive = true; _popupTimer = popupDuration; if (lockedPopup) lockedPopup.SetActive(true); }
        }
    }
}
