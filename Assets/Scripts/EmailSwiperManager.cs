using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Phish Patrol — Reigns-style swiper with bottom bucket.
///
/// Swipe left = SCAM, right = SAFE.
/// Correct → card morphs into a happy fish that arcs into the bucket.
/// Wrong  → card morphs into a pufferfish that spikes the bucket,
///           causing a crack. 5 cracks = bucket breaks = game over.
///
/// Scene built by EmailSwiperBuilder. Email data in InitializeEmails().
/// </summary>
public class EmailSwiperManager : MonoBehaviour
{
    // ===== Tuning =====
    [Header("Game Settings")]
    public int maxCracks = 5;
    public float gameDurationSeconds = 90f;
    public int correctPoints = 100;
    public int streakBonus = 25;

    // ===== Sprites (assign in Inspector — see README at bottom) =====
    [Header("Sprites — assign your PNGs here")]
    public Sprite fishNormalSprite;   // happy fish (correct answer)
    public Sprite fishPufferSprite;   // pufferfish (wrong answer)
    public Sprite crackSprite;        // crack/leak mark
    public Sprite bucketSprite;       // bucket body (optional, falls back to rect)
    public Sprite circleSprite;       // UI/Skin/Knob fallback

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

    // ===== Card =====
    [Header("Card")]
    public SwipeCard swipeCard;
    public TMP_Text cardSender;
    public TMP_Text cardEmail;
    public TMP_Text cardSubject;
    public TMP_Text cardBody;
    public Image cardAvatar;
    public TMP_Text cardAvatarLetter;
    public CanvasGroup cardCanvasGroup;    // on cardRoot

    // ===== Bucket =====
    [Header("Bucket")]
    public RectTransform bucketRoot;       // the whole bucket area
    public Image bucketBodyImage;  // bucket body visual
    public Image waterFill;        // water inside bucket
    public RectTransform fishContainer;    // parent for swimming fish
    public Transform crackContainer;   // parent for crack sprites
    public TMP_Text bucketLabel;      // "X caught"

    // ===== Fish animation objects (reused each swipe) =====
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

    // Crack positions (set by builder)
    [HideInInspector] public RectTransform[] crackSlots;

    // Swimming fish tracking
    private List<RectTransform> swimmingFish = new List<RectTransform>();

    // Palette
    private static readonly Color FishHappy = new Color(0.31f, 0.80f, 0.77f);
    private static readonly Color FishAngry = new Color(0.91f, 0.30f, 0.24f);
    private static readonly Color WaterBlue = new Color(0.27f, 0.62f, 0.83f, 0.55f);
    private static readonly Color WaterLow = new Color(0.83f, 0.33f, 0.27f, 0.55f);

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
        if (scorePopupCG != null) scorePopupCG.alpha = 0f;

        // Hide all crack slots
        if (crackSlots != null)
            foreach (var c in crackSlots)
                if (c != null) c.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!gameRunning) return;
        timeRemaining -= Time.deltaTime;
        UpdateTimerUI();

        if (!warnedLowTime && timeRemaining < 15f && timeRemaining > 0f)
        {
            warnedLowTime = true;
            if (commentator != null) commentator.Say("Quick now, time's running out!");
        }
        if (timeRemaining <= 0f) { timeRemaining = 0f; EndGame(); }

        AnimateSwimmingFish();
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

        if (commentator != null)
            commentator.Say("Be careful, dear — some of these look very real.");
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
            score += gain;
            streak++;
            fishCaught++;
            ReactToCorrect();
        }
        else
        {
            streak = 0;
            cracks++;
            ReactToWrong();
        }

        UpdateAllUI();
        StartCoroutine(SwipeSequence(dir, correct, email));
    }

    // =================================================================
    // UPDATED: SwipeSequence — card morphs into fish, then arcs to bucket
    // =================================================================

    IEnumerator SwipeSequence(int dir, bool correct, Email email)
    {
        // 1) Card transforms into fish mid-swipe
        yield return StartCoroutine(CardMorphToFish(dir, correct));

        // 2) Fish arcs from where morph ended down into bucket
        Vector2 spawnPos = fishAnimRT != null
            ? fishAnimRT.anchoredPosition
            : swipeCard.GetCardPosition();
        yield return StartCoroutine(FishArcToBucket(spawnPos, correct));

        // 3) Landing effects
        if (correct)
        {
            SpawnSwimmingFish();
            int gain = correctPoints + Mathf.Max(0, streak - 1) * streakBonus;
            StartCoroutine(ShowScorePopup(gain));
            StartCoroutine(BucketBounce());
        }
        else
        {
            yield return StartCoroutine(PufferSpikesBucket());
        }

        UpdateBucketLabel();

        // 4) Brief feedback panel
        yield return StartCoroutine(ShowFeedback(correct, email.explanation));

        // 5) Next card or end
        if (cracks >= maxCracks || currentIndex + 1 >= emails.Length)
            EndGame();
        else
            LoadCard(currentIndex + 1);
    }

    // =================================================================
    // UPDATED: Card shrinks away while fish pops in on top of it,
    //          then drifts slightly in the swipe direction.
    // =================================================================

    IEnumerator CardMorphToFish(int dir, bool correct)
    {
        if (fishAnimRT == null) yield break;

        // Place fish exactly on top of the card
        Vector2 cardPos = swipeCard.cardRoot != null
            ? swipeCard.cardRoot.anchoredPosition
            : Vector2.zero;
        fishAnimRT.anchoredPosition = cardPos;
        fishAnimRT.localScale = Vector3.zero;
        fishAnimRT.localRotation = Quaternion.identity;
        fishAnimRT.gameObject.SetActive(true);

        // Set sprite BEFORE animation starts — this is what was missing before
        if (fishAnimImage != null)
        {
            Sprite target = correct ? fishNormalSprite : fishPufferSprite;
            fishAnimImage.sprite = target != null ? target : circleSprite;
            fishAnimImage.color = Color.white; // let the sprite show its real colours
        }

        Vector2 flyEnd = cardPos + new Vector2(dir * 240f, 30f);
        float dur = 0.30f;
        float t = 0f;

        CanvasGroup cardCG = swipeCard.cardRoot != null
            ? swipeCard.cardRoot.GetComponent<CanvasGroup>() : null;

        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            float ease = p * p;

            // Card shrinks and fades out
            if (swipeCard.cardRoot != null)
                swipeCard.cardRoot.localScale = Vector3.one * Mathf.Lerp(1f, 0.05f, ease);
            if (cardCG != null)
                cardCG.alpha = 1f - p;

            // Fish pops in during first half, then holds size
            float fishScale = Mathf.Clamp01(p / 0.5f);
            fishAnimRT.localScale = Vector3.one * fishScale;
            fishAnimRT.anchoredPosition = Vector2.Lerp(cardPos, flyEnd, ease);
            fishAnimRT.localRotation = Quaternion.Euler(0, 0,
                Mathf.Lerp(0f, dir * -25f, ease));

            yield return null;
        }
    }

    // =================================================================
    // UPDATED: Arc fish from morph end-position into the bucket.
    //          Sprite is already set by CardMorphToFish — don't touch it.
    // =================================================================

    IEnumerator FishArcToBucket(Vector2 start, bool correct)
    {
        if (fishAnimRT == null) yield break;

        Vector2 end = bucketRoot != null
            ? bucketRoot.anchoredPosition + new Vector2(0, 80f)
            : new Vector2(0, -300f);
        Vector2 mid = (start + end) * 0.5f + new Vector2(0, 180f);

        float dur = 0.38f, t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            float ease = 1f - Mathf.Pow(1f - p, 2f);

            Vector2 a = Vector2.Lerp(start, mid, ease);
            Vector2 b = Vector2.Lerp(mid, end, ease);
            fishAnimRT.anchoredPosition = Vector2.Lerp(a, b, ease);

            // Slight size pulse so it feels alive
            float s = 0.6f + 0.4f * Mathf.Sin(p * Mathf.PI);
            fishAnimRT.localScale = Vector3.one * s;

            fishAnimRT.localRotation = Quaternion.Euler(0, 0, -360f * ease);
            yield return null;
        }

        fishAnimRT.gameObject.SetActive(false);
        fishAnimRT.localRotation = Quaternion.identity;
    }

    // =================================================================
    // Correct: fish lands in bucket, swims around
    // =================================================================

    void SpawnSwimmingFish()
    {
        if (fishContainer == null) return;

        var go = new GameObject("SwimFish_" + fishCaught, typeof(RectTransform));
        go.transform.SetParent(fishContainer, false);
        var rt = go.GetComponent<RectTransform>();

        float bw = fishContainer.rect.width * 0.4f;
        float bh = fishContainer.rect.height * 0.3f;
        rt.anchoredPosition = new Vector2(
            Random.Range(-bw, bw),
            Random.Range(-bh, bh));
        rt.sizeDelta = new Vector2(48, 48);

        var img = go.AddComponent<Image>();
        if (fishNormalSprite != null) img.sprite = fishNormalSprite;
        else if (circleSprite != null) img.sprite = circleSprite;
        img.color = Color.white; // show real sprite colours
        img.preserveAspect = true;
        img.raycastTarget = false;

        if (Random.value > 0.5f) rt.localScale = new Vector3(-1, 1, 1);

        swimmingFish.Add(rt);
        StartCoroutine(SplashIn(rt));
    }

    IEnumerator SplashIn(RectTransform rt)
    {
        rt.localScale = Vector3.one * 1.5f;
        float t = 0f;
        while (t < 0.25f)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / 0.25f);
            float s = Mathf.Lerp(1.5f, 1f, p);
            rt.localScale = new Vector3(rt.localScale.x > 0 ? s : -s, s, 1f);
            yield return null;
        }
    }

    void AnimateSwimmingFish()
    {
        float time = Time.time;
        for (int i = swimmingFish.Count - 1; i >= 0; i--)
        {
            if (swimmingFish[i] == null) { swimmingFish.RemoveAt(i); continue; }
            var rt = swimmingFish[i];
            var pos = rt.anchoredPosition;
            pos.x += Mathf.Sin(time * 0.8f + i * 1.7f) * 0.4f;
            pos.y += Mathf.Cos(time * 1.2f + i * 2.3f) * 0.15f;
            rt.anchoredPosition = pos;
        }
    }

    // =================================================================
    // Wrong: pufferfish inflates + spikes bucket → crack
    // =================================================================

    IEnumerator PufferSpikesBucket()
    {
        int crackIdx = Mathf.Clamp(cracks - 1, 0, maxCracks - 1);
        if (crackSlots != null && crackIdx < crackSlots.Length && crackSlots[crackIdx] != null)
        {
            var slot = crackSlots[crackIdx];
            slot.gameObject.SetActive(true);
            StartCoroutine(PunchScale(slot, 0.3f, 1.6f));
        }

        yield return StartCoroutine(ShakeBucket(0.3f, 12f));
        UpdateWaterColor();
    }

    IEnumerator ShakeBucket(float dur, float intensity)
    {
        if (bucketRoot == null) yield break;
        Vector2 orig = bucketRoot.anchoredPosition;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float x = Random.Range(-intensity, intensity) * (1f - t / dur);
            float y = Random.Range(-intensity * 0.3f, intensity * 0.3f) * (1f - t / dur);
            bucketRoot.anchoredPosition = orig + new Vector2(x, y);
            yield return null;
        }
        bucketRoot.anchoredPosition = orig;
    }

    void UpdateWaterColor()
    {
        if (waterFill == null) return;
        float dmg = (float)cracks / maxCracks;
        waterFill.color = Color.Lerp(WaterBlue, WaterLow, dmg);
        var rt = waterFill.rectTransform;
        float targetHeight = Mathf.Lerp(1f, 0.3f, dmg);
        rt.anchorMax = new Vector2(1, targetHeight);
    }

    IEnumerator BucketBounce()
    {
        if (bucketRoot == null) yield break;
        Vector3 orig = bucketRoot.localScale;
        float t = 0f;
        while (t < 0.22f)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / 0.22f);
            float s = 1f + 0.08f * Mathf.Sin(p * Mathf.PI);
            bucketRoot.localScale = Vector3.one * s;
            yield return null;
        }
        bucketRoot.localScale = orig;
    }

    IEnumerator PunchScale(RectTransform rt, float dur, float peak)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            float s = 1f + (peak - 1f) * Mathf.Sin(p * Mathf.PI);
            rt.localScale = Vector3.one * s;
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    // =================================================================
    // Score popup
    // =================================================================

    IEnumerator ShowScorePopup(int points)
    {
        if (scorePopupRT == null) yield break;

        scorePopupRT.anchoredPosition = bucketRoot != null
            ? bucketRoot.anchoredPosition + new Vector2(0, 100f)
            : new Vector2(0, -200f);
        scorePopupText.text = "+" + points;
        scorePopupCG.alpha = 1f;

        Vector2 start = scorePopupRT.anchoredPosition;
        float t = 0f;
        while (t < 0.8f)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / 0.8f);
            scorePopupRT.anchoredPosition = start + new Vector2(0, 50f * p);
            scorePopupCG.alpha = 1f - p * p;
            yield return null;
        }
        scorePopupCG.alpha = 0f;
    }

    // =================================================================
    // HUD updates
    // =================================================================

    void UpdateAllUI()
    {
        UpdateScoreUI();
        UpdateStreakUI();
        UpdateProgress();
        UpdateBucketLabel();
    }

    void UpdateTimerUI()
    {
        int sec = Mathf.CeilToInt(timeRemaining);
        timerText.text = $"{sec / 60}:{sec % 60:D2}";
        if (timerFill != null)
            timerFill.fillAmount = Mathf.Clamp01(timeRemaining / gameDurationSeconds);

        bool low = timeRemaining < 15f;
        timerText.color = low ? new Color(0.91f, 0.30f, 0.24f) : Color.white;
        if (timerFill != null)
            timerFill.color = low
                ? new Color(0.91f, 0.30f, 0.24f)
                : new Color(0.31f, 0.80f, 0.77f);
    }

    void UpdateScoreUI() { scoreText.text = $"Score: {score}"; }
    void UpdateStreakUI() { streakText.text = streak >= 3 ? $"{streak} in a row!" : ""; }
    void UpdateProgress()
    {
        if (progressText != null)
            progressText.text = $"{currentIndex + 1} / {emails.Length}";
    }
    void UpdateBucketLabel()
    {
        if (bucketLabel != null)
            bucketLabel.text = $"{fishCaught} caught  ·  {cracks}/{maxCracks} cracks";
    }

    // =================================================================
    // Commentator
    // =================================================================

    void ReactToCorrect()
    {
        if (commentator == null) return;
        if (streak == 3) commentator.Say("Oh my, you're on a roll!");
        else if (streak == 6) commentator.Say("Goodness, what a sharp eye!");
        else commentator.SayRandom(new[] {
            "Good catch, dear!", "Very nice!",
            "You're so smart.", "That's the way!"
        });
    }

    void ReactToWrong()
    {
        if (commentator == null) return;
        if (cracks >= maxCracks) return;
        if (cracks == maxCracks - 1)
            commentator.Say("One more crack and the bucket breaks!");
        else if (cracks == maxCracks - 2)
            commentator.Say("Be careful — the bucket's getting fragile…");
        else
            commentator.SayRandom(new[] {
                "Oh no, that pufferfish spiked us!",
                "Ouch! The bucket sprung a leak…",
                "Don't worry, scammers are clever."
            });
    }

    // =================================================================
    // Feedback
    // =================================================================

    IEnumerator ShowFeedback(bool correct, string explanation)
    {
        feedbackPanel.SetActive(true);
        var cg = feedbackPanel.GetComponent<CanvasGroup>();
        if (cg != null) cg.alpha = 1f;

        feedbackBg.color = correct
            ? new Color(0.18f, 0.74f, 0.41f, 0.97f)
            : new Color(0.91f, 0.30f, 0.24f, 0.97f);
        feedbackTitle.text = correct ? "Correct!" : "Not quite…";
        feedbackBody.text = explanation;

        feedbackPanel.transform.localScale = Vector3.one * 0.85f;
        float t = 0f;
        while (t < 0.2f)
        {
            t += Time.deltaTime;
            float p = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.2f), 3f);
            feedbackPanel.transform.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, p);
            yield return null;
        }

        yield return new WaitForSeconds(2.2f);

        if (cg != null)
        {
            float fo = 0f;
            while (fo < 0.2f)
            {
                fo += Time.deltaTime;
                cg.alpha = 1f - fo / 0.2f;
                yield return null;
            }
            cg.alpha = 1f;
        }

        feedbackPanel.SetActive(false);
        feedbackPanel.transform.localScale = Vector3.one;
    }

    // =================================================================
    // End game
    // =================================================================

    void EndGame()
    {
        gameRunning = false;
        swipeCard.Lock();
        ShowResult();
    }

    void ShowResult()
    {
        resultPanel.SetActive(true);
        int totalPossible = emails.Length * correctPoints;
        resultScore.text = $"{score} / {totalPossible}";

        int correct = 0;
        for (int i = 0; i < correctAnswers.Length; i++)
            if (answered[i] && correctAnswers[i]) correct++;
        float pct = emails.Length > 0 ? (float)correct / emails.Length : 0;

        if (cracks >= maxCracks)
        {
            resultStars.text = "★";
            resultMessage.text = "The bucket broke! Those pufferfish got you.\nRead carefully and try again.";
            if (commentator != null) commentator.Say("Oh dear… the bucket couldn't take any more.");
        }
        else if (timeRemaining <= 0 && pct < 0.7f)
        {
            resultStars.text = "★";
            resultMessage.text = "Time's up — you'll be quicker next time.";
            if (commentator != null) commentator.Say("Time got away from us, dear.");
        }
        else if (pct >= 0.95f)
        {
            resultStars.text = "★ ★ ★";
            resultMessage.text = "Phish-master! The bucket's full of happy fish.";
            if (commentator != null) commentator.Say("Oh thank you, dear! You're wonderful.");
        }
        else if (pct >= 0.7f)
        {
            resultStars.text = "★ ★";
            resultMessage.text = "Solid work! Review the ones that got you.";
            if (commentator != null) commentator.Say("That was a big help — thank you, dear!");
        }
        else
        {
            resultStars.text = "★";
            resultMessage.text = "Scammers are tricky. Try again and read carefully.";
            if (commentator != null) commentator.Say("It's a good start. We'll get them next time.");
        }
    }

    public void OnPlayAgain() { SceneManager.LoadScene(SceneManager.GetActiveScene().name); }
    public void OnReturnToMap() { SceneManager.LoadScene("WorldMap"); }

    // =================================================================
    // Helpers
    // =================================================================

    public static Image MakeImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    public static TMP_Text MakeText(Transform parent, string name, string content,
        int size, Color color, TextAlignmentOptions align,
        FontStyles style = FontStyles.Normal)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = content; t.fontSize = size; t.color = color;
        t.alignment = align; t.fontStyle = style; t.raycastTarget = false;
        return t;
    }

    // =================================================================
    // Email data — edit here
    // =================================================================

    void InitializeEmails()
    {
        emails = new Email[]
        {
            new Email {
                senderName  = "PayPal Support",
                senderEmail = "support@paypa1.com",
                subject     = "URGENT: Your account has been suspended",
                preview     = "Dear Customer, your PayPal account has been suspended",
                body        = "Dear Customer,\n\nYour PayPal account has been suspended due to suspicious activity. Click below immediately to verify your information or your account will be permanently deleted within 24 hours.\n\n— PayPal Support",
                timestamp   = "10:09 AM",
                avatarColor = new Color(0.07f, 0.45f, 0.71f),
                isScam      = true,
                explanation = "SCAM — The sender domain is 'paypa1.com' (number 1, not lowercase L). Real PayPal emails come from paypal.com. The 24-hour deletion threat is a classic urgency trick."
            },
            new Email {
                senderName  = "Spotify",
                senderEmail = "newsletter@spotify.com",
                subject     = "Your June playlist is ready",
                preview     = "Your monthly Spotify stats are in",
                body        = "Hi there,\n\nYour monthly Spotify stats are in. Check out your top songs from this month in the app.\n\n— The Spotify Team",
                timestamp   = "9:42 AM",
                avatarColor = new Color(0.12f, 0.84f, 0.38f),
                isScam      = false,
                explanation = "SAFE — Real domain (spotify.com), calm tone, no threats, no request for personal info."
            },
            new Email {
                senderName  = "Canada Revenue Agency",
                senderEmail = "noreply@canada-revenue-agency-refund.com",
                subject     = "You have a $847 tax refund waiting",
                preview     = "Provide your SIN and banking details",
                body        = "NOTICE FROM THE CRA:\n\nA refund of $847.00 is ready. To claim it, provide your SIN and banking details within 24 hours or the refund will be cancelled.\n\n— Canada Revenue Agency",
                timestamp   = "8:27 AM",
                avatarColor = new Color(0.78f, 0.13f, 0.13f),
                isScam      = true,
                explanation = "SCAM — The CRA never emails asking for your SIN. Real messages come from cra-arc.gc.ca only. The 24-hour deadline is another red flag."
            },
            new Email {
                senderName  = "Amazon",
                senderEmail = "orders@amazon.com",
                subject     = "Your order has shipped",
                preview     = "Order #112-4857293 has shipped",
                body        = "Hello,\n\nYour order #112-4857293 has shipped. Estimated delivery June 12. Track it in the Amazon app.\n\n— Amazon",
                timestamp   = "Yesterday",
                avatarColor = new Color(1f, 0.6f, 0f),
                isScam      = false,
                explanation = "SAFE — Real domain (amazon.com), specific order number, no request for personal info."
            },
            new Email {
                senderName  = "Microsoft Security",
                senderEmail = "security@micros0ft-account.net",
                subject     = "Unusual sign-in detected",
                preview     = "Click below immediately to secure your account",
                body        = "Dear User,\n\nWe detected a sign-in from an unrecognized location. Click below immediately to secure your account.\n\n— Microsoft Security Team",
                timestamp   = "Yesterday",
                avatarColor = new Color(0.05f, 0.45f, 0.79f),
                isScam      = true,
                explanation = "SCAM — Domain is 'micros0ft-account.net' (zero, not O). 'Dear User' is generic — Microsoft uses your name."
            },
            new Email {
                senderName  = "Uber",
                senderEmail = "no-reply@uber.com",
                subject     = "Your Tuesday night trip receipt",
                preview     = "Your trip: $14.72",
                body        = "Thanks for riding with Uber.\n\nYour trip came to $14.72. Payment charged to Visa ending in 4821.\n\n— Uber",
                timestamp   = "May 4",
                avatarColor = new Color(0.1f, 0.1f, 0.1f),
                isScam      = false,
                explanation = "SAFE — Real domain, specific details, only last 4 card digits shown, no suspicious links."
            },
            new Email {
                senderName  = "Netflix Billing",
                senderEmail = "billing@netfl1x-payments.com",
                subject     = "Payment failed — update card now",
                preview     = "Update billing within 48 hours",
                body        = "Hello,\n\nYour Netflix payment could not be processed. Update your billing details within 48 hours or your account will be terminated.\n\n— Netflix Billing",
                timestamp   = "May 3",
                avatarColor = new Color(0.90f, 0.05f, 0.10f),
                isScam      = true,
                explanation = "SCAM — Domain is 'netfl1x-payments.com' (number 1 not L). Real billing comes from netflix.com."
            },
            new Email {
                senderName  = "Google Calendar",
                senderEmail = "calendar-noreply@google.com",
                subject     = "Reminder: Coffee with Sarah at 2pm",
                preview     = "Event reminder for today",
                body        = "This is a reminder for your event:\n\nCoffee with Sarah\nToday at 2:00 PM\nThe Wired Monk Cafe\n\n— Google Calendar",
                timestamp   = "May 3",
                avatarColor = new Color(0.26f, 0.52f, 0.96f),
                isScam      = false,
                explanation = "SAFE — Real Google domain, specific event, no suspicious links or requests."
            }
        };
    }
}