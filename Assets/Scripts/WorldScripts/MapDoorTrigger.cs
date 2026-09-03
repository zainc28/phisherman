using UnityEngine;
using UnityEngine.SceneManagement;

// ============================================================
//  MapDoorTrigger
// ============================================================
public class MapDoorTrigger : MonoBehaviour
{
    public Transform player; public string targetScene;
    public GameObject prompt; public float promptProximity = 1.8f, triggerRadius = 0.55f;
    public float bobAmount = 0.08f, bobSpeed = 2.2f;
    bool _fired; float _bobBase;
    void Start() { if (prompt) { _bobBase = prompt.transform.localPosition.y; prompt.SetActive(false); } }
    void Update()
    {
        if (_fired || !player) return;
        float dist = Vector2.Distance(transform.position, player.position);
        if (prompt) { bool show = dist < promptProximity; if (prompt.activeSelf != show) prompt.SetActive(show); if (show) { var lp = prompt.transform.localPosition; lp.y = _bobBase + Mathf.Sin(Time.time * bobSpeed) * bobAmount; prompt.transform.localPosition = lp; } }
        if (dist < triggerRadius && !string.IsNullOrEmpty(targetScene)) { _fired = true; SceneManager.LoadScene(targetScene); }
    }
}
