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
/// Sprite progression across lines mirrors the narrative:
///   backfacing  — phisherman walking away / at the dock
///   threequarter— mid-conversation / turning
///   frontfacing — phisherman facing camera, making his vow
/// </summary>
public class BackstoryManager : MonoBehaviour
{
    [System.Serializable]
    public struct CutsceneLine
    {
        [TextArea(2, 5)] public string text;
        public string speaker;     // "" = caption / narrator
        public Sprite phishermanSprite;  // which backstory pose to show
        public Color bgTint;      // subtle tint shift per beat
    }

    [Header("Lines")]
    public CutsceneLine[] lines;

    [Header("UI refs — wired by builder")]
    public Image phishermanImage;   // the large character portrait
    public Image backgroundImage;   // full-screen bg (can be tinted)
    public TMP_Text speakerText;
    public TMP_Text bodyText;
    public TMP_Text continueHint;
    public CanvasGroup canvasGroup;       // root canvas — fade in/out
    public Button advanceButton;     // invisible full-screen click catcher
    public Button skipButton;        // top-right skip

    [Header("Timing")]
    public float fadeDuration = 0.5f;
    public float portraitFade = 0.3f;    // crossfade when sprite changes
    public string nextScene = "WorldMap";

    int _idx = -1;
    bool _transitioning;

    void Start()
    {
        if (canvasGroup != null) { canvasGroup.alpha = 0f; StartCoroutine(FadeIn()); }
        else AdvanceLine();
    }

    IEnumerator FadeIn()
    {
        float t = 0f;
        while (t < fadeDuration) { t += Time.deltaTime; canvasGroup.alpha = Mathf.Clamp01(t / fadeDuration); yield return null; }
        canvasGroup.alpha = 1f;
        AdvanceLine();
    }

    public void AdvanceLine()
    {
        if (_transitioning) return;
        _idx++;
        if (lines == null || _idx >= lines.Length) { StartCoroutine(Finish()); return; }
        StartCoroutine(ShowLine(lines[_idx]));
    }

    public void SkipAll() => StartCoroutine(Finish());

    IEnumerator ShowLine(CutsceneLine line)
    {
        _transitioning = true;

        // Crossfade portrait to new sprite if different
        if (phishermanImage != null && line.phishermanSprite != null &&
            phishermanImage.sprite != line.phishermanSprite)
        {
            float t = 0f;
            Color start = phishermanImage.color;
            Color mid = new Color(start.r, start.g, start.b, 0f);
            while (t < portraitFade)
            { t += Time.deltaTime; phishermanImage.color = Color.Lerp(start, mid, t / portraitFade); yield return null; }
            phishermanImage.sprite = line.phishermanSprite;
            t = 0f;
            while (t < portraitFade)
            { t += Time.deltaTime; phishermanImage.color = Color.Lerp(mid, Color.white, t / portraitFade); yield return null; }
            phishermanImage.color = Color.white;
        }

        // Tint background
        if (backgroundImage != null && line.bgTint != default)
        {
            backgroundImage.color = line.bgTint;
        }

        // Update text
        if (speakerText != null) speakerText.text = string.IsNullOrEmpty(line.speaker) ? "" : line.speaker;
        if (bodyText != null) bodyText.text = line.text;

        bool isLast = _idx >= lines.Length - 1;
        if (continueHint != null)
            continueHint.text = isLast ? "[ tap to begin your journey ]" : "Tap to continue...";

        _transitioning = false;
    }

    IEnumerator Finish()
    {
        _transitioning = true;

        // Mark backstory as seen — won't show again
        PlayerPrefs.SetInt("backstory_seen", 1);
        PlayerPrefs.Save();

        // Fade out
        if (canvasGroup != null)
        {
            float t = 0f;
            while (t < fadeDuration)
            { t += Time.deltaTime; canvasGroup.alpha = 1f - Mathf.Clamp01(t / fadeDuration); yield return null; }
            canvasGroup.alpha = 0f;
        }

        SceneManager.LoadScene(nextScene);
    }
}