using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// ============================================================
//  MainMenuManager
//
//  WireButtons() uses GetComponentsInChildren(includeInactive:true)
//  on each panel so buttons inside inactive sub-panels (Arcade,
//  Settings) are still found and wired correctly at Start().
// ============================================================
public class MainMenuManager : MonoBehaviour
{
    [Header("Root panels")]
    public GameObject mainPanel;
    public GameObject arcadePanel;
    public GameObject settingsPanel;

    [Header("Fade")]
    public CanvasGroup canvasGroup;
    public float fadeDuration = 0.35f;

    bool _resetPending;
    float _resetTimer;
    const float ResetWindow = 4f;
    const string ResetLabel = "Reset All Progress";
    TMP_Text _resetLbl;

    void Awake()
    {
        // Kill any DontDestroyOnLoad canvas left over from a previous scene
        // so it doesn't sit on top of this menu and eat its click events.
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c != null && c.gameObject.scene.name == "DontDestroyOnLoad")
                Destroy(c.gameObject);
    }

    void Start()
    {
        SetActive(mainPanel, true);
        SetActive(arcadePanel, false);
        SetActive(settingsPanel, false);

        WireButtons();

        if (canvasGroup != null) StartCoroutine(FadeIn());
        CacheResetLabel();
    }

    void Update()
    {
        if (_resetPending) { _resetTimer -= Time.unscaledDeltaTime; if (_resetTimer <= 0f) CancelReset(); }
    }

    // ── Button wiring ─────────────────────────────────────────
    void WireButtons()
    {
        BindIn(mainPanel, "StoryBtn", OnStoryModeClicked);
        BindIn(mainPanel, "ArcadeBtn", OnArcadeModeClicked);
        BindIn(mainPanel, "SettingsBtn", OnSettingsClicked);
        BindIn(mainPanel, "ExitBtn", OnExitClicked);

        // Arcade/Settings panels are INACTIVE at Start — must search includeInactive
        BindIn(arcadePanel, "EmailCard", OnPlayEmailSwiper);
        BindIn(arcadePanel, "SpotCard", OnPlaySpotDiff);
        BindIn(arcadePanel, "TowerCard", OnPlayTowerDefense);
        BindIn(arcadePanel, "ArcadeBackBtn", OnArcadeBack);

        BindIn(settingsPanel, "ResetBtn", OnResetProgress);
        BindIn(settingsPanel, "SettingsBackBtn", OnSettingsBack);
    }

    // Finds a Button by name inside a parent (including inactive children) and wires it.
    void BindIn(GameObject parent, string childName, UnityEngine.Events.UnityAction action)
    {
        if (parent == null) { Debug.LogError("[MainMenuManager] Parent is null when looking for: " + childName); return; }

        var buttons = parent.GetComponentsInChildren<Button>(includeInactive: true);
        foreach (var btn in buttons)
        {
            if (btn.gameObject.name == childName)
            {
                btn.onClick.AddListener(action);
                return;
            }
        }
        Debug.LogError("[MainMenuManager] Button not found in " + parent.name + ": " + childName);
    }

    static void SetActive(GameObject g, bool v) { if (g != null) g.SetActive(v); }

    IEnumerator FadeIn()
    {
        canvasGroup.alpha = 0f;
        float t = 0f;
        while (t < fadeDuration) { t += Time.unscaledDeltaTime; canvasGroup.alpha = Mathf.Clamp01(t / fadeDuration); yield return null; }
        canvasGroup.alpha = 1f;
    }

    IEnumerator FadeAndLoad(string scene)
    {
        if (canvasGroup != null)
        {
            float t = 0f;
            while (t < fadeDuration) { t += Time.unscaledDeltaTime; canvasGroup.alpha = 1f - Mathf.Clamp01(t / fadeDuration); yield return null; }
            canvasGroup.alpha = 0f;
        }
        SceneManager.LoadScene(scene);
    }

    void CacheResetLabel()
    {
        if (settingsPanel == null) return;
        var buttons = settingsPanel.GetComponentsInChildren<Button>(includeInactive: true);
        foreach (var btn in buttons)
        {
            if (btn.gameObject.name == "ResetBtn")
            {
                var lbl = btn.transform.Find("Label");
                if (lbl != null) _resetLbl = lbl.GetComponent<TMP_Text>();
                break;
            }
        }
    }

    // ── Main buttons ───────────────────────────────────────────
    public void OnStoryModeClicked()
    {
        // First time ever → show backstory cutscene, which leads to WorldMap.
        // Every subsequent time → go straight to WorldMap.
        string dest = PlayerPrefs.GetInt("backstory_seen", 0) == 0 ? "Backstory" : "WorldMap";
        StartCoroutine(FadeAndLoad(dest));
    }
    public void OnArcadeModeClicked() { SetActive(mainPanel, false); SetActive(arcadePanel, true); }
    public void OnSettingsClicked() { SetActive(mainPanel, false); SetActive(settingsPanel, true); }

    public void OnExitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ── Arcade panel ───────────────────────────────────────────
    public void OnPlayEmailSwiper() => StartCoroutine(FadeAndLoad("EmailSwiper"));
    public void OnPlaySpotDiff() => StartCoroutine(FadeAndLoad("SpotDifference"));
    public void OnPlayTowerDefense() => StartCoroutine(FadeAndLoad("TowerDefense"));
    public void OnArcadeBack() { SetActive(arcadePanel, false); SetActive(mainPanel, true); }

    // ── Settings panel ─────────────────────────────────────────
    public void OnSettingsBack() { CancelReset(); SetActive(settingsPanel, false); SetActive(mainPanel, true); }

    public void OnResetProgress()
    {
        if (!_resetPending)
        {
            _resetPending = true; _resetTimer = ResetWindow;
            if (_resetLbl != null) _resetLbl.text = "Tap again to confirm!";
            return;
        }
        DoReset();
    }

    void DoReset()
    {
        string[] keys =
        {
            "pp_total_xp","pp_pending_xp","pp_discovered_fish","backstory_seen",
            "completed_ApartmentInterior",   "completed_PizzaInterior",        "completed_OfficeInterior",
            "completed_FlatInterior",        "completed_PizzaW2Interior",      "completed_OfficeW2Interior",
            "completed_AuntCarolInterior",   "completed_UncleMarcusInterior",  "completed_GrandpaLouInterior",
            "completed_GrandmaIrisInterior", "completed_UncleFelixInterior",   "completed_AuntDanaInterior",
            "completed_GrandpaErnestInterior","completed_AuntPriyaInterior",   "completed_UncleDiegoInterior",
            "vol_master","vol_music","vol_sfx","acc_largetext","acc_highcontrast",
            "mg_theme_world","interior_result","interior_source"
        };
        foreach (var k in keys) PlayerPrefs.DeleteKey(k);
        PlayerPrefs.Save();

        _resetPending = false;
        if (_resetLbl != null) _resetLbl.text = "All Progress Reset!";
        StartCoroutine(RestoreLabel(2.2f));
    }

    void CancelReset() { _resetPending = false; if (_resetLbl != null) _resetLbl.text = ResetLabel; }
    IEnumerator RestoreLabel(float d) { yield return new WaitForSecondsRealtime(d); if (_resetLbl != null) _resetLbl.text = ResetLabel; }
}
