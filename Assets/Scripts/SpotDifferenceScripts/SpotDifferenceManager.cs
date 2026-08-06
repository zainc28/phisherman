using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// =============================================================================
// SpotDifferenceManager.cs
// DifferenceMarker + PanelClickReceiver + SpotDifferenceManager
// =============================================================================

// ─────────────────────────────────────────────────────────────────────────────
// DifferenceMarker
// FIX: switched from IPointerClickHandler to IPointerDownHandler. Inside a
// ScrollRect, any tiny mouse movement during a click gets treated as the
// start of a drag, which cancels OnPointerClick entirely — that's why some
// flags registered and others (the "6th" one) intermittently didn't.
// OnPointerDown fires immediately on press and isn't affected by this.
// ─────────────────────────────────────────────────────────────────────────────

[RequireComponent(typeof(Image))]
public class DifferenceMarker : MonoBehaviour, IPointerDownHandler
{
    [Tooltip("Short label shown in the found popup")]
    public string flagName;

    [Tooltip("Full explanation for result screen")]
    [TextArea(3, 6)]
    public string explanation;

    [HideInInspector] public bool found;
    [HideInInspector] public SpotDifferenceManager manager;
    [HideInInspector] public Image penCircleImage;
    [HideInInspector] public GameObject stickyNote;

    private Image _hitZone;

    // FIX: track the frame a marker was clicked so PanelClickReceiver
    // can reliably ignore the same pointer event.
    public static int LastMarkerClickFrame { get; private set; } = -1;

    private static readonly Color HiddenColor = new Color(1f, 0f, 0f, 0f);
    private static readonly Color FoundWaterColor = new Color(0.15f, 0.50f, 0.90f, 0.22f);

    void Awake()
    {
        _hitZone = GetComponent<Image>();
        _hitZone.color = HiddenColor;
        _hitZone.raycastTarget = true;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // Always stamp the frame — even for already-found markers — so
        // PanelClickReceiver knows a marker absorbed this click.
        LastMarkerClickFrame = Time.frameCount;

        if (found) return;
        found = true;
        _hitZone.color = FoundWaterColor;
        if (penCircleImage != null) penCircleImage.gameObject.SetActive(true);
        if (stickyNote != null) stickyNote.SetActive(true);
        manager?.OnDifferenceFound(this);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// PanelClickReceiver
// FIX: switched to IPointerDownHandler for the same reason as DifferenceMarker
// above — OnPointerClick was being silently swallowed by drag-threshold
// detection inside the ScrollRect, which is why wrong clicks on the spoof
// page frequently failed to register.
// ─────────────────────────────────────────────────────────────────────────────

public class PanelClickReceiver : MonoBehaviour, IPointerDownHandler
{
    public SpotDifferenceManager manager;

    public void OnPointerDown(PointerEventData eventData)
    {
        // If any DifferenceMarker handled a click this frame, skip.
        if (DifferenceMarker.LastMarkerClickFrame == Time.frameCount) return;

        manager?.OnWrongClick();
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// SpotDifferenceManager
// ─────────────────────────────────────────────────────────────────────────────

public class SpotDifferenceManager : MonoBehaviour
{
    [Header("Scoring")]
    public int correctPoints = 120;
    public int wrongPenalty = 30;
    public int streakBonus = 20;

    [Header("Lives")]
    public int maxLives = 3;

    [Header("HUD")]
    public TMP_Text scoreText;
    public TMP_Text counterText;
    public TMP_Text feedbackText;
    public TMP_Text timerText;
    public TMP_Text evidenceLogText;

    [Header("Hearts")]
    public Image[] heartImages;
    [Tooltip("Assets/Sprites/UI/heart — used for the lives HUD instead of a drawn placeholder.")]
    public Sprite heartSprite;

    [Header("Magnifying Glass")]
    public RectTransform magnifyingGlassRT;
    public Canvas mainCanvas;
    public RectTransform mainCanvasRT;
    public Button magToggleButton;
    public TMP_Text magToggleLabel;

    [Header("Phisherman sprite")]
    public GameObject phishermanWithMagGlass;
    public GameObject phishermanNoMagGlass;

    [Header("Timer")]
    public float totalTime = 60f;

    [Header("Result Screen")]
    public GameObject resultPanel;
    public TMP_Text resultTitle;
    public TMP_Text resultBreakdown;
    public TMP_Text resultScore;

    [Header("Markers")]
    public List<DifferenceMarker> markers = new List<DifferenceMarker>();

    [Header("Simulation")]
    public LureSimulation lureSimulation;

    [Header("Water Drip Effect")]
    public RectTransform effectLayer;
    public Sprite circleSprite;

    [Header("Audio")]
    public AudioSource sfxSource;
    public AudioSource bgMusicSource;
    public AudioClip sfxSplash;
    public AudioClip sfxWrong;
    public AudioClip sfxImpact;
    public AudioClip sfxRodWinding;

    [Header("Commentator")]
    public Commentator commentator;

    private int _score, _found, _total, _streak, _lives;
    private bool _gameOver, _magActive;
    private float _timeRemaining;

    private HashSet<DifferenceMarker> _foundMarkers = new HashSet<DifferenceMarker>();
    private List<string> _evidenceItems = new List<string>();
    private MinigameLivesHUD livesHUD;

    private bool _xpQueuedForClear;

    // FIX: frame-level guard — second safety net so a correct find and
    // a wrong-click penalty can never both fire on the same pointer event.
    private int _lastFoundFrame = -1;

    private static readonly string[] ProgressiveHints =
    {
        "Look at every part of that email carefully, dear.",
        "Pay attention to the exact words they chose.",
        "Check who it's actually from — look at the address.",
        "There's still something in how they're speaking to you.",
        "One more — look at what they're asking you to click."
    };

    // =================================================================
    // Lifecycle
    // =================================================================

    void Start()
    {
        if (markers == null || markers.Count == 0)
            markers = new List<DifferenceMarker>(
                FindObjectsByType<DifferenceMarker>(FindObjectsSortMode.None));
        foreach (var m in markers) if (m != null) m.manager = this;

        _total = markers.Count;
        _lives = maxLives;
        _timeRemaining = totalTime;
        _gameOver = false;
        _magActive = false;

        if (heartImages != null)
            foreach (var img in heartImages) if (img != null) img.gameObject.SetActive(false);
        livesHUD = gameObject.AddComponent<MinigameLivesHUD>();
        livesHUD.Initialize(maxLives, heartSprite);

        UpdateHud(); UpdateEvidenceLog(); HideFeedback();
        if (resultPanel != null) resultPanel.SetActive(false);
        if (magnifyingGlassRT != null) magnifyingGlassRT.gameObject.SetActive(false);
        UpdateMagButton();

        // FIX: these two were left unassigned by the scene builder (the
        // magnifier feature isn't wired up). After a scene save/reload an
        // unassigned Unity object reference becomes a "missing object"
        // placeholder rather than a true C# null, and `?.` does NOT catch
        // that — it throws UnassignedReferenceException. That exception
        // was aborting the rest of Start(), which meant
        // lureSimulation.StartSim(...) below never ran at all — which is
        // why the shark/phisherman never moved off-center and never
        // animated. Explicit null checks avoid the exception entirely.
        if (phishermanWithMagGlass != null) phishermanWithMagGlass.SetActive(false);
        if (phishermanNoMagGlass != null) phishermanNoMagGlass.SetActive(true);

        lureSimulation?.StartSim(maxLives, _total);
        commentator?.Say($"Find all {_total} red flags, dear — look closely at every detail!");

        var sources = GetComponents<AudioSource>();
        sfxSource = sources.Length > 0 ? sources[0] : gameObject.AddComponent<AudioSource>();
        bgMusicSource = sources.Length > 1 ? sources[1] : gameObject.AddComponent<AudioSource>();
        if (sfxSource != null) { sfxSource.playOnAwake = false; sfxSource.loop = false; }
        if (bgMusicSource != null && bgMusicSource.clip != null) bgMusicSource.Play();
    }

    void Update()
    {
        if (!_gameOver)
        {
            _timeRemaining -= Time.deltaTime;
            if (_timeRemaining <= 0f) { _timeRemaining = 0f; OnTimerEnd(); }
            UpdateTimerUI();
        }
        if (_magActive) TrackMagnifier();
    }

    // =================================================================
    // Timer
    // =================================================================

    void UpdateTimerUI()
    {
        if (timerText == null) return;
        int s = Mathf.CeilToInt(_timeRemaining);
        timerText.text = $"{s / 60}:{s % 60:D2}";
        timerText.color = _timeRemaining < 15f ? new Color(0.91f, 0.20f, 0.20f) : Color.white;
        if (_timeRemaining < 15f && !_gameOver)
            commentator?.Say("Hurry, dear — almost out of time!");
    }

    void OnTimerEnd()
    {
        if (_gameOver) return;
        commentator?.Say("Time's up — the shark got the rod!");
        lureSimulation?.TriggerBreach();
        EndGame(false);
    }

    // =================================================================
    // Magnifier toggle
    // =================================================================

    public void ToggleMagnifier()
    {
        _magActive = !_magActive;
        if (magnifyingGlassRT != null) magnifyingGlassRT.gameObject.SetActive(_magActive);
        // FIX: same UnassignedReferenceException issue as in Start() — use
        // explicit null checks instead of `?.` for these two fields.
        if (phishermanWithMagGlass != null) phishermanWithMagGlass.SetActive(_magActive);
        if (phishermanNoMagGlass != null) phishermanNoMagGlass.SetActive(!_magActive);
        UpdateMagButton();
    }

    void UpdateMagButton()
    {
        if (magToggleLabel != null)
            magToggleLabel.text = _magActive ? "🔍 ON" : "🔍 Magnifier";
    }

    void TrackMagnifier()
    {
        if (magnifyingGlassRT == null || mainCanvasRT == null) return;
        Camera cam = mainCanvas != null &&
                     mainCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Camera.main;
        Vector2 p;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                mainCanvasRT, Input.mousePosition, cam, out p))
            magnifyingGlassRT.anchoredPosition = p;
    }

    // =================================================================
    // Correct find
    // =================================================================

    public void OnDifferenceFound(DifferenceMarker marker)
    {
        if (_gameOver) return;

        _lastFoundFrame = Time.frameCount;

        _found++; _streak++;
        _foundMarkers.Add(marker);
        int gain = correctPoints + Mathf.Max(0, _streak - 1) * streakBonus;
        _score += gain;

        _evidenceItems.Add(marker.flagName);
        UpdateEvidenceLog();
        ShowFeedback($"+{gain}   {marker.flagName}", new Color(0.18f, 0.62f, 0.20f));
        UpdateHud();

        PlayerProgress.RegisterFish("fish_detective");

        PlaySFX(sfxRodWinding);
        int findIdx = markers.IndexOf(marker);
        lureSimulation?.OnCorrectFind(findIdx);

        PlaySFX(sfxSplash);
        var rt = marker.GetComponent<RectTransform>();
        if (rt != null) StartCoroutine(WaterDrips(rt));

        int remaining = _total - _found;
        if (remaining == 0) commentator?.Say("That's all of them! The hook is free!");
        else if (remaining == 1) commentator?.Say("Just one more, dear!");
        else if (_streak >= 3) commentator?.Say("You're catching them so quickly!");
        else
        {
            int hi = Mathf.Clamp(_found, 0, ProgressiveHints.Length - 1);
            commentator?.Say(ProgressiveHints[hi]);
        }

        if (_found >= _total)
        {
            _gameOver = true;
            lureSimulation?.TriggerSuccess();
            Invoke(nameof(ShowResult), 0.9f);
        }
    }

    // =================================================================
    // Wrong click
    // =================================================================

    public void OnWrongClick()
    {
        if (_gameOver) return;

        // Second safety net: skip if a difference was found this frame.
        if (Time.frameCount == _lastFoundFrame) return;

        _lives = Mathf.Max(0, _lives - 1);
        _score = Mathf.Max(0, _score - wrongPenalty);
        _streak = 0;

        livesHUD?.LoseLife(); UpdateHud();
        PlaySFX(sfxWrong);
        lureSimulation?.OnWrongClick();

        ShowFeedback($"-{wrongPenalty}   Not a red flag  (−1 life)",
            new Color(0.78f, 0.20f, 0.20f));
        commentator?.SayRandom(new[] {
            "Careful, dear — that's not one of them.",
            "Hmm, that part looks normal. Keep searching.",
            "Not quite — look more carefully."
        });

        if (_lives <= 0)
        {
            lureSimulation?.TriggerBreach();
            commentator?.Say("Oh no — the shark got the rod!");
            EndGame(false);
        }
    }

    // =================================================================
    // Audio helper
    // =================================================================

    void PlaySFX(AudioClip clip)
    {
        if (sfxSource != null && clip != null) sfxSource.PlayOneShot(clip, 0.85f);
    }

    // =================================================================
    // Evidence log
    // =================================================================

    void UpdateEvidenceLog()
    {
        if (evidenceLogText == null) return;
        if (_evidenceItems.Count == 0)
        { evidenceLogText.text = "<color=#7a6040>— no flags marked yet —</color>"; return; }
        var sb = new StringBuilder();
        foreach (var item in _evidenceItems) sb.Append("✓ ").AppendLine(item);
        evidenceLogText.text = sb.ToString();
    }

    // =================================================================
    // Water drips
    // =================================================================

    IEnumerator WaterDrips(RectTransform markerRT)
    {
        Camera cam = mainCanvas != null &&
                     mainCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Camera.main;
        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(cam, markerRT.position);
        for (int i = 0; i < 5; i++)
        {
            float xOff = Random.Range(-markerRT.rect.width * 0.3f, markerRT.rect.width * 0.3f);
            StartCoroutine(SpawnDrip(screenPos, xOff, cam));
        }
        yield break;
    }

    IEnumerator SpawnDrip(Vector2 origin, float xOffset, Camera cam)
    {
        if (effectLayer == null) yield break;
        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                effectLayer, origin, cam, out local)) yield break;
        local.x += xOffset;

        var go = new GameObject("Drip", typeof(RectTransform));
        go.transform.SetParent(effectLayer, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(9, 18); rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = local; rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);

        var img = go.AddComponent<Image>();
        if (circleSprite != null) img.sprite = circleSprite;
        img.color = new Color(0.10f, 0.42f, 0.92f, 0.90f);
        img.preserveAspect = true; img.raycastTarget = false;

        Vector2 start = local, end = local + new Vector2(0f, -Random.Range(55f, 105f));
        float dur = Random.Range(0.55f, 1.15f), t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime; float p = Mathf.Clamp01(t / dur);
            rt.anchoredPosition = Vector2.Lerp(start, end, p * p);
            img.color = new Color(0.10f, 0.42f, 0.92f, 0.90f * (1f - p));
            yield return null;
        }
        Destroy(go);
    }

    // =================================================================
    // End game
    // =================================================================

    void EndGame(bool win)
    {
        if (_gameOver) return;
        _gameOver = true;
        lureSimulation?.StopSim();

        float accuracy = _total > 0 ? (float)_found / _total : 0f;
        PlayerProgress.QueueFromPerformance(accuracy);
        _xpQueuedForClear = true;

        Invoke(nameof(ShowResult), 0.8f);
    }

    // =================================================================
    // HUD helpers
    // =================================================================

    void UpdateHud()
    {
        if (scoreText != null) scoreText.text = $"Score: {_score}";
        if (counterText != null) counterText.text = $"Found: {_found} / {_total}";
    }

    void ShowFeedback(string text, Color color)
    {
        if (feedbackText == null) return;
        feedbackText.text = text; feedbackText.color = color;
        CancelInvoke(nameof(HideFeedback));
        Invoke(nameof(HideFeedback), 1.4f);
    }

    void HideFeedback() { if (feedbackText != null) feedbackText.text = string.Empty; }

    // =================================================================
    // Result
    // =================================================================

    void ShowResult()
    {
        if (resultPanel == null) return;

        if (!_xpQueuedForClear)
        {
            _xpQueuedForClear = true;
            float accuracy = _total > 0 ? (float)_found / _total : 0f;
            PlayerProgress.QueueFromPerformance(accuracy);
        }

        resultPanel.SetActive(true);
        if (resultTitle != null)
            resultTitle.text = _found >= _total
                ? $"Case closed — all {_total} red flags identified!"
                : $"Investigation incomplete — {_found} of {_total} flags found.";
        if (resultScore != null) resultScore.text = $"Final score: {_score}";
        if (resultBreakdown != null)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < markers.Count; i++)
            {
                var m = markers[i]; if (m == null) continue;
                bool f = _foundMarkers.Contains(m);
                sb.Append("<b>").Append(i + 1).Append(". ")
                  .Append(f ? "[FOUND]" : "[MISSED]").Append(" ")
                  .Append(m.flagName).AppendLine("</b>");
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