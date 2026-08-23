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
// Commentator removed. Sounds wired: splash on find, shark_music BGM, impact on all lives lost.
// =============================================================================

// ─────────────────────────────────────────────────────────────────────────────
// DifferenceMarker
// ─────────────────────────────────────────────────────────────────────────────

[RequireComponent(typeof(Image))]
public class DifferenceMarker : MonoBehaviour, IPointerDownHandler
{
    [Tooltip("Short label shown in the found popup")]
    public string flagName;

    [TextArea(3, 6)]
    public string explanation;

    [HideInInspector] public bool found;
    [HideInInspector] public SpotDifferenceManager manager;
    [HideInInspector] public Image penCircleImage;
    [HideInInspector] public GameObject stickyNote;

    private Image _hitZone;

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
// ─────────────────────────────────────────────────────────────────────────────

public class PanelClickReceiver : MonoBehaviour, IPointerDownHandler
{
    public SpotDifferenceManager manager;

    public void OnPointerDown(PointerEventData eventData)
    {
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
    public AudioClip bgMusicClip;   // assign shark_music_v1 in builder

    // ── Internal ──
    private int _score, _found, _total, _streak, _lives;
    private bool _gameOver, _magActive;
    private float _timeRemaining;

    private HashSet<DifferenceMarker> _foundMarkers = new HashSet<DifferenceMarker>();
    private List<string> _evidenceItems = new List<string>();
    private MinigameLivesHUD livesHUD;

    private bool _xpQueuedForClear;
    private int _lastFoundFrame = -1;

    // ── World theme ──
    private int _worldTheme = 1;

    // =================================================================
    //  Lifecycle
    // =================================================================

    void Start()
    {
        _worldTheme = MinigameTheme.Get();

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
            foreach (var img in heartImages)
                if (img != null) img.gameObject.SetActive(false);
        livesHUD = gameObject.AddComponent<MinigameLivesHUD>();
        livesHUD.Initialize(maxLives, heartSprite);

        UpdateHud(); UpdateEvidenceLog(); HideFeedback();
        if (resultPanel != null) resultPanel.SetActive(false);
        if (magnifyingGlassRT != null) magnifyingGlassRT.gameObject.SetActive(false);
        UpdateMagButton();

        if (phishermanWithMagGlass != null) phishermanWithMagGlass.SetActive(false);
        if (phishermanNoMagGlass != null) phishermanNoMagGlass.SetActive(true);

        lureSimulation?.StartSim(maxLives, _total);

        // ── Audio setup ──
        var sources = GetComponents<AudioSource>();
        sfxSource = sources.Length > 0 ? sources[0] : gameObject.AddComponent<AudioSource>();
        bgMusicSource = sources.Length > 1 ? sources[1] : gameObject.AddComponent<AudioSource>();

        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.volume = 0.85f;
        bgMusicSource.playOnAwake = false;
        bgMusicSource.loop = true;
        bgMusicSource.volume = 0.35f;

        // Start shark_music BGM for the entire minigame
        if (bgMusicClip != null)
        {
            bgMusicSource.clip = bgMusicClip;
            bgMusicSource.Play();
        }
        else if (bgMusicSource.clip != null)
        {
            bgMusicSource.Play();
        }
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
    //  Audio helpers
    // =================================================================

    void PlaySFX(AudioClip clip)
    {
        if (sfxSource == null || clip == null) return;
        sfxSource.PlayOneShot(clip, sfxSource.volume);
    }

    void StopBGM()
    {
        if (bgMusicSource != null && bgMusicSource.isPlaying)
            bgMusicSource.Stop();
    }

    // =================================================================
    //  Timer
    // =================================================================

    void UpdateTimerUI()
    {
        if (timerText == null) return;
        int s = Mathf.CeilToInt(_timeRemaining);
        timerText.text = $"{s / 60}:{s % 60:D2}";
        timerText.color = _timeRemaining < 15f ? new Color(0.91f, 0.20f, 0.20f) : Color.white;
    }

    void OnTimerEnd()
    {
        if (_gameOver) return;
        lureSimulation?.TriggerBreach();
        EndGame(false);
    }

    // =================================================================
    //  Magnifier
    // =================================================================

    public void ToggleMagnifier()
    {
        _magActive = !_magActive;
        if (magnifyingGlassRT != null) magnifyingGlassRT.gameObject.SetActive(_magActive);
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
    //  Correct find
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

        // Play splash SFX every time a flag is found
        PlaySFX(sfxSplash);
        PlaySFX(sfxRodWinding);

        int findIdx = markers.IndexOf(marker);
        lureSimulation?.OnCorrectFind(findIdx);

        var rt = marker.GetComponent<RectTransform>();
        if (rt != null) StartCoroutine(WaterDrips(rt));

        if (_found >= _total)
        {
            _gameOver = true;
            lureSimulation?.TriggerSuccess();
            Invoke(nameof(ShowResult), 0.9f);
        }
    }

    // =================================================================
    //  Wrong click
    // =================================================================

    public void OnWrongClick()
    {
        if (_gameOver) return;
        if (Time.frameCount == _lastFoundFrame) return;

        _lives = Mathf.Max(0, _lives - 1);
        _score = Mathf.Max(0, _score - wrongPenalty);
        _streak = 0;

        livesHUD?.LoseLife(); UpdateHud();
        // Play wrong sfx on wrong click
        PlaySFX(sfxWrong);
        lureSimulation?.OnWrongClick();

        ShowFeedback($"-{wrongPenalty}   Not a red flag  (−1 life)",
            new Color(0.78f, 0.20f, 0.20f));

        if (_lives <= 0)
        {
            // Play impact when all lives are lost
            PlaySFX(sfxImpact);
            lureSimulation?.TriggerBreach();
            EndGame(false);
        }
    }

    // =================================================================
    //  Evidence log
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
    //  Water drips
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
    //  End game
    // =================================================================

    void EndGame(bool win)
    {
        if (_gameOver) return;
        _gameOver = true;
        lureSimulation?.StopSim();
        StopBGM();

        float accuracy = _total > 0 ? (float)_found / _total : 0f;
        PlayerProgress.QueueFromPerformance(accuracy);
        _xpQueuedForClear = true;

        bool isWin = win || _found >= _total;
        PlayerPrefs.SetString("interior_result", isWin ? "win" : "lose");
        PlayerPrefs.Save();

        Invoke(nameof(ShowResult), 0.8f);
    }

    // =================================================================
    //  HUD helpers
    // =================================================================

    void UpdateHud()
    {
        if (scoreText != null) scoreText.text = $"Score: {_score}";
        if (counterText != null) counterText.text = $"Found: {_found} / {_total}";
    }

    void ShowFeedback(string text, Color color)
    {
        if (feedbackText == null) return;
        feedbackText.text = text;
        feedbackText.color = color;
        CancelInvoke(nameof(HideFeedback));
        Invoke(nameof(HideFeedback), 1.4f);
    }

    void HideFeedback() { if (feedbackText != null) feedbackText.text = string.Empty; }

    // =================================================================
    //  Result — theme-aware title
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
        {
            string winTitle = _worldTheme switch
            {
                2 => $"Login page cracked — all {_total} red flags identified!",
                3 => $"Smish caught — all {_total} red flags identified!",
                4 => $"Fake account busted — all {_total} red flags identified!",
                5 => $"Final case closed — all {_total} red flags identified!",
                _ => $"Case closed — all {_total} red flags identified!"
            };
            resultTitle.text = _found >= _total
                ? winTitle
                : $"Investigation incomplete — {_found} of {_total} flags found.";
        }

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
    //  Buttons
    // =================================================================

    public void PlayAgain() { SceneManager.LoadScene(SceneManager.GetActiveScene().name); }

    public void BackToWorldMap()
    {
        string src = PlayerPrefs.GetString("interior_source", "WorldMap");
        if (string.IsNullOrEmpty(src)) src = "WorldMap";
        SceneManager.LoadScene(src);
    }
}