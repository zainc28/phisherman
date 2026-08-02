using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
//  MainMenuManager
//  Original OnStoryModeClicked + OnArcadeModeClicked kept.
//  Added: arcade panel, settings panel, fade in/out.
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

    void Start()
    {
        if (mainPanel != null) mainPanel.SetActive(true);
        if (arcadePanel != null) arcadePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (canvasGroup != null) StartCoroutine(FadeIn());
    }

    IEnumerator FadeIn()
    {
        canvasGroup.alpha = 0f;
        float t = 0f;
        while (t < fadeDuration) { t += Time.deltaTime; canvasGroup.alpha = Mathf.Clamp01(t / fadeDuration); yield return null; }
        canvasGroup.alpha = 1f;
    }

    // ── Main buttons ───────────────────────────────────────────
    public void OnStoryModeClicked()
    {
        // First time ever → show backstory cutscene, which leads to WorldMap.
        // Every subsequent time → go straight to WorldMap.
        string dest = PlayerPrefs.GetInt("backstory_seen", 0) == 0 ? "Backstory" : "WorldMap";
        StartCoroutine(FadeAndLoad(dest));
    }
    public void OnArcadeModeClicked() { if (mainPanel != null) mainPanel.SetActive(false); if (arcadePanel != null) arcadePanel.SetActive(true); }
    public void OnSettingsClicked() { if (mainPanel != null) mainPanel.SetActive(false); if (settingsPanel != null) settingsPanel.SetActive(true); }

    // ── Arcade panel ───────────────────────────────────────────
    public void OnPlayEmailSwiper() => StartCoroutine(FadeAndLoad("EmailSwiper"));
    public void OnPlaySpotDiff() => StartCoroutine(FadeAndLoad("SpotDifference"));
    public void OnPlayTowerDefense() => StartCoroutine(FadeAndLoad("TowerDefense"));
    public void OnArcadeBack() { if (arcadePanel != null) arcadePanel.SetActive(false); if (mainPanel != null) mainPanel.SetActive(true); }

    // ── Settings panel ─────────────────────────────────────────
    public void OnSettingsBack() { if (settingsPanel != null) settingsPanel.SetActive(false); if (mainPanel != null) mainPanel.SetActive(true); }

    public void OnExitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    IEnumerator FadeAndLoad(string scene)
    {
        if (canvasGroup != null)
        {
            float t = 0f;
            while (t < fadeDuration) { t += Time.deltaTime; canvasGroup.alpha = 1f - Mathf.Clamp01(t / fadeDuration); yield return null; }
            canvasGroup.alpha = 0f;
        }
        SceneManager.LoadScene(scene);
    }
}

// ============================================================
//  SettingsManager  (placeholder — wire sliders/toggles later)
// ============================================================
public class SettingsManager : MonoBehaviour
{
    [Header("Volume sliders (assign in Inspector)")]
    public Slider masterVolume;
    public Slider musicVolume;
    public Slider sfxVolume;

    [Header("Accessibility toggles (assign in Inspector)")]
    public Toggle largeTextToggle;
    public Toggle highContrastToggle;

    const string KeyMaster = "vol_master";
    const string KeyMusic = "vol_music";
    const string KeySfx = "vol_sfx";
    const string KeyLargeText = "acc_largetext";
    const string KeyHighContrast = "acc_highcontrast";

    void Start()
    {
        if (masterVolume != null) { masterVolume.value = PlayerPrefs.GetFloat(KeyMaster, 1f); masterVolume.onValueChanged.AddListener(v => { AudioListener.volume = v; PlayerPrefs.SetFloat(KeyMaster, v); }); }
        if (musicVolume != null) { musicVolume.value = PlayerPrefs.GetFloat(KeyMusic, 0.8f); musicVolume.onValueChanged.AddListener(v => PlayerPrefs.SetFloat(KeyMusic, v)); }
        if (sfxVolume != null) { sfxVolume.value = PlayerPrefs.GetFloat(KeySfx, 1f); sfxVolume.onValueChanged.AddListener(v => PlayerPrefs.SetFloat(KeySfx, v)); }
        if (largeTextToggle != null) { largeTextToggle.isOn = PlayerPrefs.GetInt(KeyLargeText, 0) == 1; largeTextToggle.onValueChanged.AddListener(v => PlayerPrefs.SetInt(KeyLargeText, v ? 1 : 0)); }
        if (highContrastToggle != null) { highContrastToggle.isOn = PlayerPrefs.GetInt(KeyHighContrast, 0) == 1; highContrastToggle.onValueChanged.AddListener(v => PlayerPrefs.SetInt(KeyHighContrast, v ? 1 : 0)); }
        AudioListener.volume = PlayerPrefs.GetFloat(KeyMaster, 1f);
    }

    public void ResetToDefaults()
    {
        if (masterVolume != null) masterVolume.value = 1f;
        if (musicVolume != null) musicVolume.value = 0.8f;
        if (sfxVolume != null) sfxVolume.value = 1f;
        if (largeTextToggle != null) largeTextToggle.isOn = false;
        if (highContrastToggle != null) highContrastToggle.isOn = false;
        PlayerPrefs.DeleteKey(KeyMaster); PlayerPrefs.DeleteKey(KeyMusic); PlayerPrefs.DeleteKey(KeySfx);
        PlayerPrefs.DeleteKey(KeyLargeText); PlayerPrefs.DeleteKey(KeyHighContrast);
    }
}