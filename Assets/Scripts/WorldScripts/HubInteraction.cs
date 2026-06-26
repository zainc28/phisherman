using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Walk-on door trigger for the WorldMap hub and interiors.
///
/// When the assigned player transform comes within <c>triggerRadius</c> of
/// this object, it loads <c>targetScene</c>. A floating "ENTER" prompt
/// (optional) appears and bobs while the player is within promptProximity.
///
/// This only READS the player's position each frame — it never moves the
/// player, so the existing WorldMapManager movement is completely untouched.
/// </summary>
public class DoorTrigger : MonoBehaviour
{
    public Transform player;
    public string targetScene;
    public float triggerRadius = 0.6f;

    [Header("Prompt (optional)")]
    public GameObject prompt;          // floating badge shown when near
    public float promptProximity = 2.3f;
    public float bobAmount = 0.10f;
    public float bobSpeed = 4f;

    private bool fired;
    private Vector3 promptBaseLocal;
    private float bobTime;

    void Start()
    {
        if (prompt != null)
        {
            promptBaseLocal = prompt.transform.localPosition;
            prompt.SetActive(false);
        }
    }

    void Update()
    {
        if (player == null || fired) return;

        float dist = Vector2.Distance(player.position, transform.position);

        // Proximity prompt
        if (prompt != null)
        {
            bool near = dist <= promptProximity;
            if (prompt.activeSelf != near) prompt.SetActive(near);
            if (near)
            {
                bobTime += Time.deltaTime;
                prompt.transform.localPosition =
                    promptBaseLocal + new Vector3(0f, Mathf.Sin(bobTime * bobSpeed) * bobAmount, 0f);
            }
        }

        // Walk-on enter
        if (dist <= triggerRadius && !string.IsNullOrEmpty(targetScene))
        {
            fired = true;
            SceneManager.LoadScene(targetScene);
        }
    }
}

/// <summary>
/// Tiny helper so a UI Button can load a named scene via a persistent
/// (serialized) onClick listener. Used by interior "Back to Town" buttons.
/// </summary>
public class SceneLoader : MonoBehaviour
{
    public string sceneName;

    public void Load()
    {
        if (!string.IsNullOrEmpty(sceneName))
            SceneManager.LoadScene(sceneName);
    }
}