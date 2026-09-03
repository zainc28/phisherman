using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Drives the one-time backstory cutscene shown before WorldMap
/// on the player's first Story Mode launch.
///
/// On the final line it sets PlayerPrefs "backstory_seen" = 1 so
/// it never shows again, then loads WorldMap.
///
/// Act 2 shows a second portrait (dadImage / showDad) alongside the
/// Phisherman's for the father-son conversation beats.
/// </summary>
public class BackstoryManager : MonoBehaviour
{
    [System.Serializable]
    public struct CutsceneLine
    {
        [TextArea(2, 5)] public string text;
        public string speaker;
        public Sprite phishermanSprite;
        public bool showDad;
        public Color bgTint;
    }

    [Header("Portraits")]
    public Image phishermanImage;
    public Image dadImage;

    [Header("Background")]
    public Image backgroundImage;

    [Header("Dialogue UI")]
    public TMP_Text speakerText;
    public TMP_Text bodyText;
    public TMP_Text continueHint;

    [Header("Navigation")]
    public Button advanceButton;
    public Button skipButton;

    [Header("Timing")]
    public CanvasGroup canvasGroup;
    public float fadeDuration = 0.5f;
    public float portraitFade = 0.28f;

    [Header("Scene flow")]
    public string nextScene = "WorldMap";

    [Header("Lines")]
    public CutsceneLine[] lines;

    int _lineIndex = 0;
    bool _busy = false;

    static readonly Color NarratorCol = new Color(0.85f, 0.85f, 1.00f, 0.90f);
    static readonly Color SpeakerCol = new Color(1.00f, 0.85f, 0.25f, 1.00f);

    void Start()
    {
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        if (dadImage != null) dadImage.gameObject.SetActive(false);
        StartCoroutine(BeginSequence());
    }

    IEnumerator BeginSequence()
    {
        if (canvasGroup != null)
        { float t = 0f; while (t < fadeDuration) { t += Time.deltaTime; canvasGroup.alpha = Mathf.Clamp01(t / fadeDuration); yield return null; } canvasGroup.alpha = 1f; }
        ShowLine(_lineIndex);
    }

    public void AdvanceLine()
    {
        if (_busy) return;
        _lineIndex++;
        if (_lineIndex >= lines.Length) { StartCoroutine(FadeAndLoad(nextScene)); return; }
        StartCoroutine(CrossfadeToLine(_lineIndex));
    }

    public void SkipAll()
    { if (_busy) return; StartCoroutine(FadeAndLoad(nextScene)); }

    void ShowLine(int idx)
    {
        if (lines == null || idx < 0 || idx >= lines.Length) return;
        var line = lines[idx];
        if (bodyText != null) bodyText.text = line.text;
        if (continueHint != null) continueHint.text = idx < lines.Length - 1 ? "Tap to continue..." : "Tap to finish";
        bool hasSpeaker = !string.IsNullOrEmpty(line.speaker);
        if (speakerText != null) { speakerText.text = line.speaker; speakerText.color = hasSpeaker ? SpeakerCol : NarratorCol; }
        if (bodyText != null) bodyText.color = hasSpeaker ? Color.white : NarratorCol;
        if (phishermanImage != null && line.phishermanSprite != null) phishermanImage.sprite = line.phishermanSprite;
        if (dadImage != null) dadImage.gameObject.SetActive(line.showDad);
        if (Camera.main != null && line.bgTint != default) Camera.main.backgroundColor = line.bgTint;
    }

    IEnumerator CrossfadeToLine(int idx)
    {
        _busy = true;
        yield return StartCoroutine(FadePortraits(1f, 0f));
        ShowLine(idx);
        yield return StartCoroutine(FadePortraits(0f, 1f));
        _busy = false;
    }

    IEnumerator FadePortraits(float from, float to)
    {
        float t = 0f;
        while (t < portraitFade)
        { t += Time.deltaTime; float a = Mathf.Lerp(from, to, Mathf.Clamp01(t / portraitFade)); SetAlpha(phishermanImage, a); if (dadImage != null && dadImage.gameObject.activeSelf) SetAlpha(dadImage, a); yield return null; }
        SetAlpha(phishermanImage, to);
        if (dadImage != null && dadImage.gameObject.activeSelf) SetAlpha(dadImage, to);
    }

    static void SetAlpha(Image img, float a) { if (img == null) return; var c = img.color; c.a = a; img.color = c; }

    IEnumerator FadeAndLoad(string sceneName)
    {
        _busy = true;
        PlayerPrefs.SetInt("backstory_seen", 1); PlayerPrefs.Save();
        if (canvasGroup != null)
        { float t = 0f; while (t < fadeDuration) { t += Time.deltaTime; canvasGroup.alpha = 1f - Mathf.Clamp01(t / fadeDuration); yield return null; } canvasGroup.alpha = 0f; }
        SceneManager.LoadScene(sceneName);
    }
}