using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Phish Patrol — Reigns-style swiper with bottom bucket.
///
/// AUDIO (June 2026):
///   Assign in Inspector (auto-detected by builder if named correctly):
///     bgMusic          → frutiger_music_fresh_waters  (loops)
///     sfxCardFlip      → card_flip
///     sfxCorrect       → water_splash
///     sfxWrong         → glass_crack
/// </summary>
public class EmailSwiperManager : MonoBehaviour
{
    // ===== Tuning =====
    [Header("Game Settings")]
    public int maxCracks = 5;
    public float gameDurationSeconds = 90f;
    public int correctPoints = 100;
    public int streakBonus = 25;

    // ===== Sprites =====
    [Header("Sprites")]
    public Sprite fishNormalSprite;
    public Sprite fishPufferSprite;
    public Sprite crackSprite;
    public Sprite bucketSprite;
    public Sprite circleSprite;

    // ===== Audio =====
    [Header("Audio")]
    public AudioClip bgMusic;        // frutiger_music_fresh_waters
    public AudioClip sfxCardFlip;    // card_flip
    public AudioClip sfxCorrect;     // water_splash
    public AudioClip sfxWrong;       // glass_crack
    [Range(0f, 1f)] public float musicVolume = 0.35f;
    [Range(0f, 1f)] public float sfxVolume = 0.80f;

    private AudioSource _sfxSource;
    private AudioSource _musicSource;

    // ===== Panels =====
    [Header("Panels")]
    public GameObject tutorialPanel;
    public GameObject gameRoot;
    public GameObject hudPanel;
    public GameObject feedbackPanel;
    public GameObject resultPanel;

    // ===== HUD =====
    [Header("HUD")]
    public TMP_Text timerText;
    public Image timerFill;
    public TMP_Text scoreText;
    public TMP_Text streakText;
    public TMP_Text progressText;

    // ===== Hearts =====
    [Header("Hearts")]
    public Transform heartsContainer;
    public List<Image> heartImages = new List<Image>();

    // ===== Card =====
    [Header("Card")]
    public SwipeCard swipeCard;
    public TMP_Text cardSender;
    public TMP_Text cardEmail;
    public TMP_Text cardSubject;
    public TMP_Text cardBody;
    public Image cardAvatar;
    public TMP_Text cardAvatarLetter;
    public CanvasGroup cardCanvasGroup;

    // ===== Bucket =====
    [Header("Bucket")]
    public RectTransform bucketRoot;
    public Image bucketBodyImage;
    public Image waterFill;
    public RectTransform fishContainer;
    public Transform crackContainer;
    public TMP_Text bucketLabel;

    // ===== Fish animation =====
    [Header("Fish Anim")]
    public RectTransform fishAnimRT;
    public Image fishAnimImage;

    // ===== Score popup =====
    [Header("Score Popup")]
    public RectTransform scorePopupRT;
    public TMP_Text scorePopupText;
    public CanvasGroup scorePopupCG;

    // ===== Feedback =====
    [Header("Feedback")]
    public Image feedbackBg;
    public TMP_Text feedbackTitle;
    public TMP_Text feedbackBody;

    // ===== Result =====
    [Header("Result")]
    public TMP_Text resultStars;
    public TMP_Text resultScore;
    public TMP_Text resultMessage;

    // ===== Commentator =====
    [Header("Commentator")]
    public Commentator commentator;

    // ===== Internal =====
    private int score, streak, cracks, currentIndex, fishCaught;
    private float timeRemaining;
    private bool gameRunning, warnedLowTime;
    private bool[] answered, correctAnswers;

    [HideInInspector] public RectTransform[] crackSlots;

    private struct PoolFishState
    {
        public RectTransform rt;
        public Vector2 center;
        public float phaseX, phaseY, freqX, freqY, ampX, ampY;
    }
    private List<PoolFishState> poolFishStates = new List<PoolFishState>();

    private static readonly Color WaterBlue = new Color(0.27f, 0.62f, 0.83f, 0.55f);
    private static readonly Color WaterLow = new Color(0.83f, 0.33f, 0.27f, 0.55f);
    private static readonly Color HeartFull = new Color(0.91f, 0.30f, 0.24f);
    private static readonly Color HeartEmpty = new Color(0.35f, 0.35f, 0.40f);

    [System.Serializable]
    public struct Email
    {
        public string senderName, senderEmail, subject, preview, body, timestamp, explanation;
        public Color avatarColor;
        public bool isScam;
    }
    private Email[] emails;

    // =================================================================
    // Lifecycle
    // =================================================================

    void Start()
    {
        InitializeEmails();
        score = 0; streak = 0; cracks = 0; fishCaught = 0;
        currentIndex = 0;
        timeRemaining = gameDurationSeconds;
        answered = new bool[emails.Length];
        correctAnswers = new bool[emails.Length];

        if (swipeCard != null) swipeCard.onSwipeCommit = OnSwipeCommit;

        tutorialPanel.SetActive(true);
        gameRoot.SetActive(false);
        hudPanel.SetActive(false);
        feedbackPanel.SetActive(false);
        resultPanel.SetActive(false);

        if (fishAnimRT != null) fishAnimRT.gameObject.SetActive(false);
        if (fishAnimImage != null) { fishAnimImage.sprite = null; fishAnimImage.color = Color.white; }
        if (scorePopupCG != null) scorePopupCG.alpha = 0f;
        if (crackSlots != null) foreach (var c in crackSlots) if (c != null) c.gameObject.SetActive(false);

        // Hearts
        heartImages.Clear();
        if (heartsContainer != null)
            foreach (Transform t in heartsContainer)
            {
                var img = t.GetComponent<Image>();
                if (img != null) heartImages.Add(img);
            }
        UpdateHearts();

        // Audio setup — two AudioSources on this GameObject (added by builder)
        // First = sfx (short one-shots), Second = music (looping background)
        var sources = GetComponents<AudioSource>();
        _sfxSource = sources.Length > 0 ? sources[0] : gameObject.AddComponent<AudioSource>();
        _musicSource = sources.Length > 1 ? sources[1] : gameObject.AddComponent<AudioSource>();
        _sfxSource.playOnAwake = false; _sfxSource.loop = false;
        _musicSource.playOnAwake = false; _musicSource.loop = true;
    }

    // =================================================================
    // Audio helpers
    // =================================================================

    void PlayBgMusic()
    {
        if (_musicSource == null || bgMusic == null) return;
        _musicSource.clip = bgMusic;
        _musicSource.volume = musicVolume;
        _musicSource.loop = true;
        _musicSource.Play();
    }

    void StopBgMusic()
    {
        if (_musicSource != null && _musicSource.isPlaying) _musicSource.Stop();
    }

    void PlaySFX(AudioClip clip)
    {
        if (_sfxSource == null || clip == null) return;
        _sfxSource.PlayOneShot(clip, sfxVolume);
    }

    // =================================================================
    // Update
    // =================================================================

    void Update()
    {
        if (!gameRunning) return;
        timeRemaining -= Time.deltaTime;
        UpdateTimerUI();
        if (!warnedLowTime && timeRemaining < 15f && timeRemaining > 0f)
        {
            warnedLowTime = true;
            commentator?.Say("Quick now, time's running out!");
        }
        if (timeRemaining <= 0f) { timeRemaining = 0f; EndGame(); }
        AnimatePoolFish();
    }

    // =================================================================
    // Tutorial → Game
    // =================================================================

    public void OnTutorialStart()
    {
        tutorialPanel.SetActive(false);
        gameRoot.SetActive(true);
        hudPanel.SetActive(true);
        UpdateAllUI();
        LoadCard(0);
        gameRunning = true;
        PlayBgMusic();   // ← music starts when game starts
        commentator?.Say("Be careful, dear — some of these look very real.");
    }

    // =================================================================
    // Card loading
    // =================================================================

    void LoadCard(int idx)
    {
        if (idx >= emails.Length) { EndGame(); return; }
        currentIndex = idx;
        var e = emails[idx];

        cardSender.text = e.senderName;
        cardEmail.text = e.senderEmail;
        cardSubject.text = e.subject;
        cardBody.text = e.body;
        cardAvatar.color = e.avatarColor;
        cardAvatarLetter.text = string.IsNullOrEmpty(e.senderName)
            ? "?" : e.senderName[0].ToString().ToUpper();

        if (cardCanvasGroup != null) cardCanvasGroup.alpha = 1f;
        swipeCard.ResetPosition();
        swipeCard.Unlock();
        UpdateProgress();
        StartCoroutine(CardSlideIn());
    }

    IEnumerator CardSlideIn()
    {
        PlaySFX(sfxCardFlip);   // ← card flip sound on each new card

        var rt = swipeCard.cardRoot;
        if (rt == null) yield break;
        Vector2 target = rt.anchoredPosition;
        rt.anchoredPosition = target + new Vector2(0, -160f);
        if (cardCanvasGroup != null) cardCanvasGroup.alpha = 0f;
        rt.localScale = Vector3.one * 0.90f;

        float t = 0f;
        while (t < 0.28f)
        {
            t += Time.deltaTime;
            float p = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.28f), 3f);
            rt.anchoredPosition = Vector2.Lerp(target + new Vector2(0, -160f), target, p);
            if (cardCanvasGroup != null) cardCanvasGroup.alpha = p;
            rt.localScale = Vector3.Lerp(Vector3.one * 0.90f, Vector3.one, p);
            yield return null;
        }
        rt.anchoredPosition = target;
        if (cardCanvasGroup != null) cardCanvasGroup.alpha = 1f;
        rt.localScale = Vector3.one;
    }

    // =================================================================
    // Swipe commit
    // =================================================================

    void OnSwipeCommit(int dir)
    {
        bool playerSaidScam = (dir == -1);
        var email = emails[currentIndex];
        bool correct = (playerSaidScam == email.isScam);

        answered[currentIndex] = true;
        correctAnswers[currentIndex] = correct;

        if (correct)
        {
            int gain = correctPoints + Mathf.Max(0, streak) * streakBonus;
            score += gain; streak++; fishCaught++;
            ReactToCorrect();
        }
        else
        {
            streak = 0; cracks++;
            UpdateHearts();
            ReactToWrong();
        }

        UpdateAllUI();
        StartCoroutine(SwipeSequence(dir, correct, email));
    }

    // =================================================================
    // Swipe sequence
    // =================================================================

    IEnumerator SwipeSequence(int dir, bool correct, Email email)
    {
        yield return StartCoroutine(CardMorphToFish(dir, correct));

        Vector2 spawnPos = fishAnimRT != null
            ? fishAnimRT.anchoredPosition : swipeCard.GetCardPosition();
        yield return StartCoroutine(FishArcToBucket(spawnPos, correct));

        if (correct)
        {
            SpawnPoolFish();
            PlaySFX(sfxCorrect);   // ← splash when fish lands in bucket
            int gain = correctPoints + Mathf.Max(0, streak - 1) * streakBonus;
            StartCoroutine(ShowScorePopup(gain));
            StartCoroutine(BucketBounce());
        }
        else
        {
            PlaySFX(sfxWrong);     // ← crack sound when puffer spikes bucket
            yield return StartCoroutine(PufferSpikesBucket());
        }

        UpdateBucketLabel();
        yield return StartCoroutine(ShowFeedback(correct, email.explanation));

        if (cracks >= maxCracks || currentIndex + 1 >= emails.Length)
            EndGame();
        else
            LoadCard(currentIndex + 1);
    }

    // =================================================================
    // Card morph to fish
    // =================================================================

    IEnumerator CardMorphToFish(int dir, bool correct)
    {
        if (fishAnimRT == null) yield break;

        Vector2 cardPos = swipeCard.cardRoot != null
            ? swipeCard.cardRoot.anchoredPosition : Vector2.zero;

        fishAnimRT.anchoredPosition = cardPos;
        fishAnimRT.localScale = Vector3.zero;
        fishAnimRT.localRotation = Quaternion.identity;

        if (fishAnimImage != null)
        {
            Sprite target = correct ? fishNormalSprite : fishPufferSprite;
            if (target != null) { fishAnimImage.sprite = target; fishAnimImage.color = Color.white; }
            else { fishAnimImage.sprite = circleSprite; fishAnimImage.color = correct ? new Color(0.31f, 0.80f, 0.77f) : new Color(0.91f, 0.30f, 0.24f); }
        }

        fishAnimRT.gameObject.SetActive(true);
        Vector2 flyEnd = cardPos + new Vector2(dir * 240f, 30f);
        float dur = 0.30f, t = 0f;
        CanvasGroup cardCG = swipeCard.cardRoot?.GetComponent<CanvasGroup>();

        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur), ease = p * p;
            if (swipeCard.cardRoot != null) swipeCard.cardRoot.localScale = Vector3.one * Mathf.Lerp(1f, 0.05f, ease);
            if (cardCG != null) cardCG.alpha = 1f - p;
            fishAnimRT.localScale = Vector3.one * Mathf.Clamp01(p / 0.5f);
            fishAnimRT.anchoredPosition = Vector2.Lerp(cardPos, flyEnd, ease);
            fishAnimRT.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(0f, dir * -25f, ease));
            yield return null;
        }
    }

    IEnumerator FishArcToBucket(Vector2 start, bool correct)
    {
        if (fishAnimRT == null) yield break;
        Vector2 end = bucketRoot != null ? bucketRoot.anchoredPosition + new Vector2(0, 80f) : new Vector2(0, -300f);
        Vector2 mid = (start + end) * 0.5f + new Vector2(0, 180f);
        float dur = 0.38f, t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur), ease = 1f - Mathf.Pow(1f - p, 2f);
            Vector2 a = Vector2.Lerp(start, mid, ease), b = Vector2.Lerp(mid, end, ease);
            fishAnimRT.anchoredPosition = Vector2.Lerp(a, b, ease);
            fishAnimRT.localScale = Vector3.one * (0.6f + 0.4f * Mathf.Sin(p * Mathf.PI));
            fishAnimRT.localRotation = Quaternion.Euler(0, 0, -360f * ease);
            yield return null;
        }
        fishAnimRT.gameObject.SetActive(false);
        fishAnimRT.localRotation = Quaternion.identity;
    }

    // =================================================================
    // Pool fish
    // =================================================================

    void SpawnPoolFish()
    {
        if (fishContainer == null) return;
        var go = new GameObject("PoolFish_" + fishCaught, typeof(RectTransform));
        go.transform.SetParent(fishContainer, false);
        var rt = go.GetComponent<RectTransform>();
        float bw = Mathf.Max(fishContainer.rect.width * 0.38f, 60f);
        float bh = Mathf.Max(fishContainer.rect.height * 0.28f, 20f);
        Vector2 center = new Vector2(Random.Range(-bw, bw), Random.Range(-bh, bh));
        rt.anchoredPosition = center; rt.sizeDelta = new Vector2(48, 48);
        var img = go.AddComponent<Image>(); img.color = Color.white; img.preserveAspect = true; img.raycastTarget = false;
        if (fishNormalSprite != null) img.sprite = fishNormalSprite;
        else if (circleSprite != null) img.sprite = circleSprite;
        float facing = Random.value > 0.5f ? 1f : -1f;
        rt.localScale = new Vector3(facing, 1f, 1f);
        poolFishStates.Add(new PoolFishState
        {
            rt = rt,
            center = center,
            phaseX = Random.Range(0f, Mathf.PI * 2f),
            phaseY = Random.Range(0f, Mathf.PI * 2f),
            freqX = Random.Range(0.45f, 1.0f),
            freqY = Random.Range(0.9f, 1.8f),
            ampX = Random.Range(Mathf.Min(bw * 0.85f, 70f), Mathf.Min(bw, 90f)),
            ampY = Random.Range(Mathf.Min(bh * 0.7f, 10f), Mathf.Min(bh, 20f)),
        });
        StartCoroutine(SplashIn(rt, facing));
    }

    IEnumerator SplashIn(RectTransform rt, float facing)
    {
        rt.localScale = new Vector3(facing * 1.5f, 1.5f, 1f);
        float t = 0f;
        while (t < 0.25f)
        {
            t += Time.deltaTime; if (rt == null) yield break;
            float s = Mathf.Lerp(1.5f, 1f, Mathf.Clamp01(t / 0.25f));
            rt.localScale = new Vector3(facing * s, s, 1f);
            yield return null;
        }
    }

    void AnimatePoolFish()
    {
        float maxX = Mathf.Max(fishContainer != null ? fishContainer.rect.width * 0.45f : 80f, 40f);
        float maxY = Mathf.Max(fishContainer != null ? fishContainer.rect.height * 0.40f : 20f, 10f);
        float time = Time.time;
        for (int i = poolFishStates.Count - 1; i >= 0; i--)
        {
            var s = poolFishStates[i];
            if (s.rt == null) { poolFishStates.RemoveAt(i); continue; }
            float x = Mathf.Clamp(s.center.x + Mathf.Sin(time * s.freqX + s.phaseX) * s.ampX, -maxX, maxX);
            float y = Mathf.Clamp(s.center.y + Mathf.Sin(time * s.freqY + s.phaseY) * s.ampY, -maxY, maxY);
            s.rt.anchoredPosition = new Vector2(x, y);
            float dx = Mathf.Cos(time * s.freqX + s.phaseX);
            if (Mathf.Abs(dx) > 0.05f) s.rt.localScale = new Vector3(dx > 0 ? 1f : -1f, 1f, 1f);
            poolFishStates[i] = s;
        }
    }

    // =================================================================
    // Bucket damage
    // =================================================================

    IEnumerator PufferSpikesBucket()
    {
        int crackIdx = Mathf.Clamp(cracks - 1, 0, maxCracks - 1);
        if (crackSlots != null && crackIdx < crackSlots.Length && crackSlots[crackIdx] != null)
        { crackSlots[crackIdx].gameObject.SetActive(true); StartCoroutine(PunchScale(crackSlots[crackIdx], 0.3f, 1.6f)); }
        yield return StartCoroutine(ShakeBucket(0.3f, 12f));
        UpdateWaterColor();
    }

    IEnumerator ShakeBucket(float dur, float intensity)
    {
        if (bucketRoot == null) yield break;
        Vector2 orig = bucketRoot.anchoredPosition; float t = 0f;
        while (t < dur) { t += Time.deltaTime; float x = Random.Range(-intensity, intensity) * (1f - t / dur); float y = Random.Range(-intensity * 0.3f, intensity * 0.3f) * (1f - t / dur); bucketRoot.anchoredPosition = orig + new Vector2(x, y); yield return null; }
        bucketRoot.anchoredPosition = orig;
    }

    void UpdateWaterColor()
    {
        if (waterFill == null) return;
        float dmg = (float)cracks / maxCracks;
        waterFill.color = Color.Lerp(WaterBlue, WaterLow, dmg);
        waterFill.rectTransform.anchorMax = new Vector2(1, Mathf.Lerp(1f, 0.3f, dmg));
    }

    IEnumerator BucketBounce()
    {
        if (bucketRoot == null) yield break;
        Vector3 orig = bucketRoot.localScale; float t = 0f;
        while (t < 0.22f) { t += Time.deltaTime; float p = Mathf.Clamp01(t / 0.22f); bucketRoot.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(p * Mathf.PI)); yield return null; }
        bucketRoot.localScale = orig;
    }

    IEnumerator PunchScale(RectTransform rt, float dur, float peak)
    {
        float t = 0f;
        while (t < dur) { t += Time.deltaTime; float p = Mathf.Clamp01(t / dur); rt.localScale = Vector3.one * (1f + (peak - 1f) * Mathf.Sin(p * Mathf.PI)); yield return null; }
        rt.localScale = Vector3.one;
    }

    // =================================================================
    // Hearts
    // =================================================================

    void UpdateHearts()
    {
        int livesLeft = maxCracks - cracks;
        for (int i = 0; i < heartImages.Count; i++)
            heartImages[i].color = i < livesLeft ? HeartFull : HeartEmpty;
    }

    // =================================================================
    // Score popup
    // =================================================================

    IEnumerator ShowScorePopup(int points)
    {
        if (scorePopupRT == null) yield break;
        scorePopupRT.anchoredPosition = bucketRoot != null ? bucketRoot.anchoredPosition + new Vector2(0, 100f) : new Vector2(0, -200f);
        scorePopupText.text = "+" + points; scorePopupCG.alpha = 1f;
        Vector2 start = scorePopupRT.anchoredPosition; float t = 0f;
        while (t < 0.8f) { t += Time.deltaTime; float p = Mathf.Clamp01(t / 0.8f); scorePopupRT.anchoredPosition = start + new Vector2(0, 50f * p); scorePopupCG.alpha = 1f - p * p; yield return null; }
        scorePopupCG.alpha = 0f;
    }

    // =================================================================
    // HUD
    // =================================================================

    void UpdateAllUI() { UpdateScoreUI(); UpdateStreakUI(); UpdateProgress(); UpdateBucketLabel(); UpdateHearts(); }

    void UpdateTimerUI()
    {
        int sec = Mathf.CeilToInt(timeRemaining);
        timerText.text = $"{sec / 60}:{sec % 60:D2}";
        if (timerFill != null) timerFill.fillAmount = Mathf.Clamp01(timeRemaining / gameDurationSeconds);
        bool low = timeRemaining < 15f;
        timerText.color = low ? new Color(0.91f, 0.30f, 0.24f) : Color.white;
        if (timerFill != null) timerFill.color = low ? new Color(0.91f, 0.30f, 0.24f) : new Color(0.31f, 0.80f, 0.77f);
    }

    void UpdateScoreUI() { scoreText.text = $"Score: {score}"; }
    void UpdateStreakUI() { streakText.text = streak >= 3 ? $"{streak} in a row!" : ""; }
    void UpdateProgress() { if (progressText != null) progressText.text = $"{currentIndex + 1} / {emails.Length}"; }
    void UpdateBucketLabel() { if (bucketLabel != null) bucketLabel.text = $"{fishCaught} caught  ·  {cracks}/{maxCracks} cracks"; }

    // =================================================================
    // Commentator
    // =================================================================

    void ReactToCorrect()
    {
        if (commentator == null) return;
        if (streak == 3) commentator.Say("Oh my, you're on a roll!");
        else if (streak == 6) commentator.Say("Goodness, what a sharp eye!");
        else commentator.SayRandom(new[] { "Good catch, dear!", "Very nice!", "You're so smart.", "That's the way!" });
    }

    void ReactToWrong()
    {
        if (commentator == null) return;
        if (cracks >= maxCracks) return;
        if (cracks == maxCracks - 1) commentator.Say("One more crack and the bucket breaks!");
        else if (cracks == maxCracks - 2) commentator.Say("Be careful — the bucket's getting fragile…");
        else commentator.SayRandom(new[] { "Oh no, that pufferfish spiked us!", "Ouch! The bucket sprung a leak…", "Don't worry, scammers are clever." });
    }

    // =================================================================
    // Feedback
    // =================================================================

    IEnumerator ShowFeedback(bool correct, string explanation)
    {
        feedbackPanel.SetActive(true);
        var cg = feedbackPanel.GetComponent<CanvasGroup>(); if (cg != null) cg.alpha = 1f;
        feedbackBg.color = correct ? new Color(0.18f, 0.74f, 0.41f, 0.97f) : new Color(0.91f, 0.30f, 0.24f, 0.97f);
        feedbackTitle.text = correct ? "Correct!" : "Not quite…";
        feedbackBody.text = explanation;
        feedbackPanel.transform.localScale = Vector3.one * 0.85f;
        float t = 0f;
        while (t < 0.2f) { t += Time.deltaTime; float p = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.2f), 3f); feedbackPanel.transform.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, p); yield return null; }
        yield return new WaitForSeconds(2.2f);
        if (cg != null) { float fo = 0f; while (fo < 0.2f) { fo += Time.deltaTime; cg.alpha = 1f - fo / 0.2f; yield return null; } cg.alpha = 1f; }
        feedbackPanel.SetActive(false); feedbackPanel.transform.localScale = Vector3.one;
    }

    // =================================================================
    // End game
    // =================================================================

    void EndGame()
    {
        gameRunning = false;
        swipeCard.Lock();
        StopBgMusic();   // ← music stops when game ends
        ShowResult();
    }

    void ShowResult()
    {
        resultPanel.SetActive(true);
        int totalPossible = emails.Length * correctPoints;
        resultScore.text = $"{score} / {totalPossible}";
        int correct = 0;
        for (int i = 0; i < correctAnswers.Length; i++) if (answered[i] && correctAnswers[i]) correct++;
        float pct = emails.Length > 0 ? (float)correct / emails.Length : 0;
        if (cracks >= maxCracks) { resultStars.text = "★"; resultMessage.text = "The bucket broke! Those pufferfish got you.\nRead carefully and try again."; commentator?.Say("Oh dear… the bucket couldn't take any more."); }
        else if (timeRemaining <= 0 && pct < 0.7f) { resultStars.text = "★"; resultMessage.text = "Time's up — you'll be quicker next time."; commentator?.Say("Time got away from us, dear."); }
        else if (pct >= 0.95f) { resultStars.text = "★ ★ ★"; resultMessage.text = "Phish-master! The bucket's full of happy fish."; commentator?.Say("Oh thank you, dear! You're wonderful."); }
        else if (pct >= 0.7f) { resultStars.text = "★ ★"; resultMessage.text = "Solid work! Review the ones that got you."; commentator?.Say("That was a big help — thank you, dear!"); }
        else { resultStars.text = "★"; resultMessage.text = "Scammers are tricky. Try again and read carefully."; commentator?.Say("It's a good start. We'll get them next time."); }
    }

    public void OnPlayAgain() { SceneManager.LoadScene(SceneManager.GetActiveScene().name); }
    public void OnReturnToMap() { SceneManager.LoadScene("WorldMap"); }

    // =================================================================
    // Helpers
    // =================================================================

    public static Image MakeImage(Transform parent, string name, Color color)
    { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); var img = go.AddComponent<Image>(); img.color = color; return img; }

    public static TMP_Text MakeText(Transform parent, string name, string content, int size, Color color, TextAlignmentOptions align, FontStyles style = FontStyles.Normal)
    { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); var t = go.AddComponent<TextMeshProUGUI>(); t.text = content; t.fontSize = size; t.color = color; t.alignment = align; t.fontStyle = style; t.raycastTarget = false; return t; }

    // =================================================================
    // Email data
    // =================================================================

    void InitializeEmails()
    {
        emails = new Email[]
        {
            new Email { senderName="PayPal Support", senderEmail="support@paypa1.com", subject="URGENT: Your account has been suspended", preview="Dear Customer, your PayPal account has been suspended", body="Dear Customer,\n\nYour PayPal account has been suspended due to suspicious activity. Click below immediately to verify your information or your account will be permanently deleted within 24 hours.\n\n— PayPal Support", timestamp="10:09 AM", avatarColor=new Color(0.07f,0.45f,0.71f), isScam=true, explanation="SCAM — The sender domain is 'paypa1.com' (number 1, not lowercase L). Real PayPal emails come from paypal.com. The 24-hour deletion threat is a classic urgency trick." },
            new Email { senderName="Spotify", senderEmail="newsletter@spotify.com", subject="Your June playlist is ready", preview="Your monthly Spotify stats are in", body="Hi there,\n\nYour monthly Spotify stats are in. Check out your top songs from this month in the app.\n\n— The Spotify Team", timestamp="9:42 AM", avatarColor=new Color(0.12f,0.84f,0.38f), isScam=false, explanation="SAFE — Real domain (spotify.com), calm tone, no threats, no request for personal info." },
            new Email { senderName="Canada Revenue Agency", senderEmail="noreply@canada-revenue-agency-refund.com", subject="You have a $847 tax refund waiting", preview="Provide your SIN and banking details", body="NOTICE FROM THE CRA:\n\nA refund of $847.00 is ready. To claim it, provide your SIN and banking details within 24 hours or the refund will be cancelled.\n\n— Canada Revenue Agency", timestamp="8:27 AM", avatarColor=new Color(0.78f,0.13f,0.13f), isScam=true, explanation="SCAM — The CRA never emails asking for your SIN. Real messages come from cra-arc.gc.ca only. The 24-hour deadline is another red flag." },
            new Email { senderName="Amazon", senderEmail="orders@amazon.com", subject="Your order has shipped", preview="Order #112-4857293 has shipped", body="Hello,\n\nYour order #112-4857293 has shipped. Estimated delivery June 12. Track it in the Amazon app.\n\n— Amazon", timestamp="Yesterday", avatarColor=new Color(1f,0.6f,0f), isScam=false, explanation="SAFE — Real domain (amazon.com), specific order number, no request for personal info." },
            new Email { senderName="Microsoft Security", senderEmail="security@micros0ft-account.net", subject="Unusual sign-in detected", preview="Click below immediately to secure your account", body="Dear User,\n\nWe detected a sign-in from an unrecognized location. Click below immediately to secure your account.\n\n— Microsoft Security Team", timestamp="Yesterday", avatarColor=new Color(0.05f,0.45f,0.79f), isScam=true, explanation="SCAM — Domain is 'micros0ft-account.net' (zero, not O). 'Dear User' is generic — Microsoft uses your name." },
            new Email { senderName="Uber", senderEmail="no-reply@uber.com", subject="Your Tuesday night trip receipt", preview="Your trip: $14.72", body="Thanks for riding with Uber.\n\nYour trip came to $14.72. Payment charged to Visa ending in 4821.\n\n— Uber", timestamp="May 4", avatarColor=new Color(0.1f,0.1f,0.1f), isScam=false, explanation="SAFE — Real domain, specific details, only last 4 card digits shown, no suspicious links." },
            new Email { senderName="Netflix Billing", senderEmail="billing@netfl1x-payments.com", subject="Payment failed — update card now", preview="Update billing within 48 hours", body="Hello,\n\nYour Netflix payment could not be processed. Update your billing details within 48 hours or your account will be terminated.\n\n— Netflix Billing", timestamp="May 3", avatarColor=new Color(0.90f,0.05f,0.10f), isScam=true, explanation="SCAM — Domain is 'netfl1x-payments.com' (number 1 not L). Real billing comes from netflix.com." },
            new Email { senderName="Google Calendar", senderEmail="calendar-noreply@google.com", subject="Reminder: Coffee with Sarah at 2pm", preview="Event reminder for today", body="This is a reminder for your event:\n\nCoffee with Sarah\nToday at 2:00 PM\nThe Wired Monk Cafe\n\n— Google Calendar", timestamp="May 3", avatarColor=new Color(0.26f,0.52f,0.96f), isScam=false, explanation="SAFE — Real Google domain, specific event, no suspicious links or requests." },
        };
    }
}