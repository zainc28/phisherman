using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Runtime manager for the Spot the Difference minigame.
///
/// New in this version:
///   - 60-second countdown timer
///   - Water drip effect when a difference is found
///   - Magnifying glass cursor that follows the mouse in the email area
///   - Phisherman HUD sprite holds magnifying glass when cursor is active
///   - SharkSimulation driven by the timer
/// </summary>
public class SpotDifferenceManager : MonoBehaviour
{
    [Header("Scoring")]
    public int correctPoints = 100;
    public int wrongPenalty = 25;
    public int streakBonus = 25;

    [Header("HUD")]
    public TMP_Text scoreText;
    public TMP_Text counterText;
    public TMP_Text feedbackText;
    public TMP_Text timerText;

    [Header("Timer")]
    public float totalTime = 60f;

    [Header("Result Screen")]
    public GameObject resultPanel;
    public TMP_Text resultTitle;
    public TMP_Text resultBreakdown;
    public TMP_Text resultScore;

    [Header("Markers (auto-populated by builder)")]
    public List<DifferenceMarker> markers = new List<DifferenceMarker>();

    [Header("Simulation")]
    public SharkSimulation simulation;

    [Header("Magnifying Glass")]
    public RectTransform magnifyingGlassRT;  // follows mouse in email area
    public Canvas mainCanvas;         // used for coordinate conversion
    public RectTransform mainCanvasRT;

    [Header("Phisherman HUD")]
    public GameObject phishermanWithMagGlass;  // shown when in email area
    public GameObject phishermanNoMagGlass;    // shown otherwise

    [Header("Water Effect")]
    public RectTransform effectLayer;   // top-level layer, not inside any mask
    public Sprite circleSprite;  // for drip drops

    [Header("Commentator")]
    public Commentator commentator;

    // ── Internal ──
    private int score, found, total, streak;
    private bool gameOver;
    private float timeRemaining;
    private HashSet<DifferenceMarker> foundMarkers = new HashSet<DifferenceMarker>();
    // Email area in screen-space Y (set in Start based on canvas)
    private float emailAreaMinY;
    private float emailAreaMaxY;

    // =================================================================
    // Lifecycle
    // =================================================================

    void Start()
    {
        // Fallback marker discovery
        if (markers == null || markers.Count == 0)
            markers = new List<DifferenceMarker>(
                FindObjectsByType<DifferenceMarker>(FindObjectsSortMode.None));
        foreach (var m in markers) if (m != null) m.manager = this;

        total = markers.Count;
        timeRemaining = totalTime;
        gameOver = false;

        UpdateHud();
        HideFeedback();
        if (resultPanel != null) resultPanel.SetActive(false);

        // Email area occupies roughly y = 28% to 89.5% of screen
        emailAreaMinY = Screen.height * 0.28f;
        emailAreaMaxY = Screen.height * 0.895f;

        // Magnifying glass hidden until in email area
        if (magnifyingGlassRT != null) magnifyingGlassRT.gameObject.SetActive(false);

        // Phisherman defaults to no-mag state
        phishermanWithMagGlass?.SetActive(false);
        phishermanNoMagGlass?.SetActive(true);

        // Start shark
        simulation?.StartSim();

        commentator?.Say($"Find all {total} red flags, dear. Look closely!");
    }

    void Update()
    {
        // ── Timer ──
        if (!gameOver)
        {
            timeRemaining -= Time.deltaTime;
            if (timeRemaining <= 0f) { timeRemaining = 0f; OnTimerEnd(); }
            UpdateTimerUI();
        }

        // ── Magnifying glass follows mouse ──
        UpdateMagnifyingGlass();
    }

    // =================================================================
    // Timer
    // =================================================================

    void UpdateTimerUI()
    {
        if (timerText == null) return;
        int totalSec = Mathf.CeilToInt(timeRemaining);
        int min = totalSec / 60;
        int sec = totalSec % 60;
        timerText.text = $"{min}:{sec:D2}";

        bool lowTime = timeRemaining < 10f;
        timerText.color = lowTime ? new Color(0.91f, 0.20f, 0.20f) : Color.white;

        if (lowTime && !gameOver)
            commentator?.Say("Hurry, dear — almost out of time!");
    }

    void OnTimerEnd()
    {
        if (gameOver) return;
        gameOver = true;
        simulation?.StopSim();
        // Show result with whatever was found
        Invoke(nameof(ShowResult), 0.5f);
        commentator?.Say("Time's up! Let's see what you found…");
    }

    // =================================================================
    // Magnifying glass
    // =================================================================

    void UpdateMagnifyingGlass()
    {
        if (magnifyingGlassRT == null || mainCanvasRT == null) return;

        Vector2 mousePos = Input.mousePosition;
        bool inEmailArea = mousePos.y > emailAreaMinY && mousePos.y < emailAreaMaxY;

        magnifyingGlassRT.gameObject.SetActive(inEmailArea);
        phishermanWithMagGlass?.SetActive(inEmailArea);
        phishermanNoMagGlass?.SetActive(!inEmailArea);

        if (!inEmailArea) return;

        // Convert screen point to canvas local space
        Camera cam = mainCanvas != null && mainCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null : Camera.main;
        Vector2 canvasPos;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                mainCanvasRT, mousePos, cam, out canvasPos))
        {
            magnifyingGlassRT.anchoredPosition = canvasPos;
        }
    }

    // =================================================================
    // Difference found / wrong click
    // =================================================================

    public void OnDifferenceFound(DifferenceMarker marker)
    {
        if (gameOver) return;

        found++;
        streak++;
        foundMarkers.Add(marker);
        int gain = correctPoints + Mathf.Max(0, streak - 1) * streakBonus;
        score += gain;

        ShowFeedback($"+{gain}   {marker.flagName}", new Color(0.18f, 0.62f, 0.20f));
        UpdateHud();

        // Water drip effect at marker
        var rt = marker.GetComponent<RectTransform>();
        if (rt != null) StartCoroutine(WaterEffect(marker.GetComponent<Image>(), rt));

        // Commentator
        int remaining = total - found;
        if (remaining == 0)
            commentator?.Say("That's all of them — wonderful!");
        else if (remaining == 1)
            commentator?.Say("Just one more, dear!");
        else if (streak >= 3)
            commentator?.Say("You're catching them so quickly!");
        else
            commentator?.SayRandom(new[] {
                "Yes! That's a red flag.",
                "Good eye, dear.",
                "I see it now too!"
            });

        if (found >= total)
        {
            gameOver = true;
            simulation?.StopSim();
            Invoke(nameof(ShowResult), 0.7f);
        }
    }

    public void OnWrongClick()
    {
        if (gameOver) return;
        score = Mathf.Max(0, score - wrongPenalty);
        streak = 0;
        ShowFeedback($"-{wrongPenalty}   Not a red flag", new Color(0.78f, 0.20f, 0.20f));
        UpdateHud();
        commentator?.SayRandom(new[] {
            "Hmm, look more carefully there.",
            "Not quite — keep searching.",
            "That part looks normal to me."
        });
    }

    // =================================================================
    // Water drip effect
    // =================================================================

    IEnumerator WaterEffect(Image markerImg, RectTransform markerRT)
    {
        // ── Fade marker background to watery blue ──
        Color targetBlue = new Color(0.15f, 0.55f, 0.95f, 0.38f);
        float t = 0f;
        while (t < 0.28f)
        {
            t += Time.deltaTime;
            if (markerImg != null)
                markerImg.color = Color.Lerp(new Color(1, 0, 0, 0), targetBlue,
                    Mathf.Clamp01(t / 0.28f));
            yield return null;
        }
        if (markerImg != null) markerImg.color = targetBlue;

        // ── Spawn 4 drips on the effect layer ──
        // Convert marker's canvas-space position for the effect layer
        Canvas cv = mainCanvas;
        Camera cam = cv != null && cv.renderMode == RenderMode.ScreenSpaceOverlay
            ? null : Camera.main;

        Vector2 markerScreenPos = RectTransformUtility.WorldToScreenPoint(cam, markerRT.position);

        for (int i = 0; i < 4; i++)
        {
            float xOff = Random.Range(-markerRT.rect.width * 0.28f,
                                       markerRT.rect.width * 0.28f);
            StartCoroutine(SpawnDrip(markerScreenPos, xOff, cam));
        }
    }

    IEnumerator SpawnDrip(Vector2 originScreenPos, float xOffset, Camera cam)
    {
        if (effectLayer == null) yield break;

        // Convert origin to effect-layer local space
        Vector2 localPos;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                effectLayer, originScreenPos, cam, out localPos))
            yield break;

        localPos.x += xOffset;

        // Build drip GameObject
        var go = new GameObject("Drip", typeof(RectTransform));
        go.transform.SetParent(effectLayer, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(9, 18);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = localPos;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);

        var img = go.AddComponent<Image>();
        if (circleSprite != null) img.sprite = circleSprite;
        img.color = new Color(0.10f, 0.42f, 0.92f, 0.90f);
        img.preserveAspect = true;
        img.raycastTarget = false;

        // Animate: fall and fade
        Vector2 start = localPos;
        Vector2 end = localPos + new Vector2(0f, -Random.Range(55f, 105f));
        float dur = Random.Range(0.55f, 1.15f);
        float t = 0f;

        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            rt.anchoredPosition = Vector2.Lerp(start, end, p * p);
            img.color = new Color(0.10f, 0.42f, 0.92f, 0.90f * (1f - p));
            yield return null;
        }

        Destroy(go);
    }

    // =================================================================
    // HUD helpers
    // =================================================================

    void UpdateHud()
    {
        if (scoreText != null) scoreText.text = $"Score: {score}";
        if (counterText != null) counterText.text = $"Found: {found} / {total}";
    }

    void ShowFeedback(string text, Color color)
    {
        if (feedbackText == null) return;
        feedbackText.text = text;
        feedbackText.color = color;
        CancelInvoke(nameof(HideFeedback));
        Invoke(nameof(HideFeedback), 1.1f);
    }

    void HideFeedback()
    {
        if (feedbackText != null) feedbackText.text = string.Empty;
    }

    // =================================================================
    // Result screen
    // =================================================================

    void ShowResult()
    {
        if (resultPanel == null) return;
        resultPanel.SetActive(true);

        if (resultTitle != null)
        {
            resultTitle.text = found >= total
                ? $"You spotted all {total} red flags!"
                : $"Time's up! You found {found} / {total} red flags.";
        }

        if (resultScore != null) resultScore.text = $"Final Score: {score}";

        if (resultBreakdown != null)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < markers.Count; i++)
            {
                var m = markers[i];
                if (m == null) continue;
                bool wasFound = foundMarkers.Contains(m);
                string tick = wasFound ? "[FOUND]" : "[MISSED]";
                sb.Append("<b>").Append(i + 1).Append(". ")
                  .Append(tick).Append(" ").Append(m.flagName).AppendLine("</b>");
                sb.Append("<size=80%>").Append(m.explanation).AppendLine("</size>");
                sb.AppendLine();
            }
            resultBreakdown.text = sb.ToString();
        }
    }

    // =================================================================
    // Buttons
    // =================================================================

    public void PlayAgain() { SceneManager.LoadScene(SceneManager.GetActiveScene().name); }
    public void BackToWorldMap() { SceneManager.LoadScene("WorldMap"); }
}