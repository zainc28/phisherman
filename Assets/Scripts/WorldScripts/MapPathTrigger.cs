using UnityEngine;
using UnityEngine.SceneManagement;

// ============================================================
//  MapPathTrigger
// ============================================================
public class MapPathTrigger : MonoBehaviour
{
    public enum TriggerType { LoadScene, LockedPopup }
    public Transform player; public TriggerType type = TriggerType.LoadScene;
    public string targetScene = ""; public GameObject lockedPopup;
    public float popupDuration = 2.8f, promptProximity = 2.2f, triggerRadius = 1.0f;
    public float bobAmount = 0.10f, bobSpeed = 2.0f; public GameObject prompt;
    public int requiresWorldComplete = 0;
    bool _fired, _popupActive; float _popupTimer, _bobBase;
    void Start() { if (prompt) { _bobBase = prompt.transform.localPosition.y; prompt.SetActive(false); } if (lockedPopup) lockedPopup.SetActive(false); }
    void Update()
    {
        if (!player || _fired) return;
        float dist = Vector2.Distance(transform.position, player.position);
        if (prompt) { bool show = dist < promptProximity; if (prompt.activeSelf != show) prompt.SetActive(show); if (show) { var lp = prompt.transform.localPosition; lp.y = _bobBase + Mathf.Sin(Time.time * bobSpeed) * bobAmount; prompt.transform.localPosition = lp; } }
        if (_popupActive) { _popupTimer -= Time.deltaTime; if (_popupTimer <= 0f) { _popupActive = false; if (lockedPopup) lockedPopup.SetActive(false); } }
        if (dist < triggerRadius)
        {
            if (type == TriggerType.LoadScene && !string.IsNullOrEmpty(targetScene))
            {
                if (requiresWorldComplete > 0 && !WorldProgress.IsWorldComplete(requiresWorldComplete))
                { if (!_popupActive && lockedPopup) { _popupActive = true; _popupTimer = popupDuration; lockedPopup.SetActive(true); } return; }
                _fired = true; if (prompt) prompt.SetActive(false); SceneManager.LoadScene(targetScene);
            }
            else if (type == TriggerType.LockedPopup && !_popupActive) { _popupActive = true; _popupTimer = popupDuration; if (lockedPopup) lockedPopup.SetActive(true); }
        }
    }
}
