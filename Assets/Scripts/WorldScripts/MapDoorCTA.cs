using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// ============================================================
//  MapDoorCTA
// ============================================================
public class MapDoorCTA : MonoBehaviour
{
    public string targetScene; public Transform playerTransform, doorPosition;
    public float bobAmount = 0.16f, bobSpeed = 2.8f, clickRadius = 1.2f, proximityRadius = 0.55f;
    public TMPro.TextMeshProUGUI label;
    public string completionKey = ""; public Color activeColor = Color.white, completedColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
    public GameObject completedLockPopup; public string completedLockMessage = "Already helped here! Try another building.";
    float _baseY; bool _completed, _walkingToDoor, _proxFired;

    void Start()
    {
        _baseY = transform.localPosition.y;
        _completed = !string.IsNullOrEmpty(completionKey) && PlayerPrefs.GetInt(completionKey, 0) == 1;
        RefreshColor();
    }

    void Update()
    {
        if (!string.IsNullOrEmpty(completionKey))
        {
            bool now = PlayerPrefs.GetInt(completionKey, 0) == 1;
            if (now != _completed) { _completed = now; RefreshColor(); _proxFired = false; }
        }
        if (!_completed) { var lp = transform.localPosition; lp.y = _baseY + Mathf.Sin(Time.time * bobSpeed) * bobAmount; transform.localPosition = lp; transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * bobSpeed * 1.4f + 0.6f) * 0.06f); }
        else { var lp = transform.localPosition; lp.y = _baseY; transform.localPosition = lp; transform.localScale = Vector3.one; }

        if (playerTransform && doorPosition)
        {
            float dist = Vector2.Distance(playerTransform.position, doorPosition.position);
            if (_proxFired && dist > proximityRadius * 2f) _proxFired = false;
            if (!_proxFired && dist < proximityRadius) { _proxFired = true; OnClick(); }
        }
        if (Input.GetMouseButtonDown(0) && Camera.main)
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null || !es.IsPointerOverGameObject())
            { Vector2 mw = Camera.main.ScreenToWorldPoint(Input.mousePosition); if (Vector2.Distance(mw, (Vector2)transform.position) < clickRadius) OnClick(); }
        }
        if (_walkingToDoor && playerTransform && doorPosition && Vector2.Distance(playerTransform.position, doorPosition.position) < proximityRadius)
        { _walkingToDoor = false; if (!string.IsNullOrEmpty(targetScene)) SceneManager.LoadScene(targetScene); }
    }

    void RefreshColor() { if (label) label.color = _completed ? completedColor : activeColor; }

    public void OnClick()
    {
        if (_completed) { if (completedLockPopup) { var c = completedLockPopup.GetComponent<LockedPopupController>(); if (c) c.Show(completedLockMessage); else completedLockPopup.SetActive(true); } return; }
        if (!playerTransform || !doorPosition) return;
        var nav = playerTransform.GetComponent<MapNavAgent>();
        if (nav) { nav.RequestPath(doorPosition.position); _walkingToDoor = true; }
        else if (!string.IsNullOrEmpty(targetScene)) SceneManager.LoadScene(targetScene);
    }
}
