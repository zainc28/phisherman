using UnityEngine;
using UnityEngine.SceneManagement;

// ============================================================
//  DoorTrigger
//  Walk-on door trigger for WorldMap hub and interiors.
//  UNCHANGED from original.
// ============================================================
public class DoorTrigger : MonoBehaviour
{
    public Transform player;
    public string targetScene;
    public float triggerRadius = 0.6f;
    public GameObject prompt;
    public float promptProximity = 2.3f;
    public float bobAmount = 0.10f;
    public float bobSpeed = 4f;
    bool _fired;
    Vector3 _promptBaseLocal;
    float _bobTime;
    void Start()
    {
        if (prompt != null) { _promptBaseLocal = prompt.transform.localPosition; prompt.SetActive(false); }
    }
    void Update()
    {
        if (player == null || _fired) return;
        float dist = Vector2.Distance(player.position, transform.position);
        if (prompt != null)
        {
            bool near = dist <= promptProximity;
            if (prompt.activeSelf != near) prompt.SetActive(near);
            if (near) { _bobTime += Time.deltaTime; prompt.transform.localPosition = _promptBaseLocal + new Vector3(0f, Mathf.Sin(_bobTime * bobSpeed) * bobAmount, 0f); }
        }
        if (dist <= triggerRadius && !string.IsNullOrEmpty(targetScene)) { _fired = true; SceneManager.LoadScene(targetScene); }
    }
}

// ============================================================
//  SceneLoader
//  Tiny helper so a UI Button can load a scene via persistent listener.
//  UNCHANGED from original.
// ============================================================
public class SceneLoader : MonoBehaviour
{
    public string sceneName;
    public void Load() { if (!string.IsNullOrEmpty(sceneName)) SceneManager.LoadScene(sceneName); }
}

// ============================================================
//  MainMenuManager and SettingsManager now live in their own files:
//  MainMenuManager.cs and SettingsManager.cs (same folder). Splitting
//  them out gives each script a stable identity, which is what the
//  MainMenu.unity scene's components resolve against.
// ============================================================