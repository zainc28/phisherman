using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class EmailSwiperManager : MonoBehaviour
{
    // ===== Tuning =====
    [Header("Game Settings")]
    public int maxCracks = 3;
    [Tooltip("Get this many correct and the minigame is won immediately — no need to sort every email.")]
    public int targetCorrectToWin = 5;

    [Tooltip("Assets/Sprites/UI/heart — used for the lives HUD instead of a drawn placeholder.")]
    public Sprite heartSprite;
    public float gameDurationSeconds = 90f;
    public int correctPoints = 100;
    public int streakBonus = 25;

    // ===== Sprites =====
    [Header("Sprites")]
    public Sprite fishNormalSprite;
    public Sprite fishPufferSprite;
    public Sprite crackSprite;
    public Sprite circleSprite;
    [Tooltip("Drag fish_1_clownfish_normal through fish_20_black here in order (20 sprites). Used for random net fish visuals.")]
    public Sprite[] fishPoolSprites; // fish_1 through fish_20

    // ===== Audio =====
    [Header("Audio")]
    public AudioClip bgMusic;
    public AudioClip sfxCardFlip;
    public AudioClip sfxCorrect;
    public AudioClip sfxWrong;
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

    // ===== Card Border (CHANGED — wired by builder for runtime theme swaps) =====
    [Header("Card Border")]
    [Tooltip("The card's RectTransform. Border stripe children are recoloured each card.")]
    public RectTransform cardBorderRT;

    // ===== Boat & Nets =====
    [Header("Boat & Nets")]
    public RectTransform boatRoot;
    public RectTransform boatHullRT;
    public RectTransform leftNetRT;
    public RectTransform rightNetRT;
    public RectTransform[] boatCrackSlots;

    public RectTransform leftNetFishContainer;
    public RectTransform rightNetFishContainer;

    // ===== Fish animation (arc from card to net) =====
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
    private int score, streak, cracks, currentIndex, fishCaughtLeft, fishCaughtRight;
    private float timeRemaining;
    private bool gameRunning, warnedLowTime;
    private bool[] answered, correctAnswers;
    private bool boatSinking;
    private int correctCount;
    private bool wonEarly;
    private System.Collections.Generic.Dictionary<string, Sprite> _fishSprites
        = new System.Collections.Generic.Dictionary<string, Sprite>();

    private struct PoolFishState
    {
        public RectTransform rt;
        public Vector2 center;
        public float phaseX, phaseY, freqX, freqY, ampX, ampY;
    }
    private List<PoolFishState> poolFishStates = new List<PoolFishState>();

    // ── CHANGED: 10 fish-themed border palettes for runtime swapping ──
    private struct BorderTheme { public Color a, b, accent; }
    private static readonly BorderTheme[] borderThemes =
    {
        new BorderTheme{a=new Color(1.00f,0.42f,0.10f), b=Color.white,                     accent=new Color(0.10f,0.10f,0.10f)}, // 0  Clownfish       orange/white/black
        new BorderTheme{a=new Color(0.10f,0.43f,1.00f), b=new Color(1.00f,0.88f,0.20f),    accent=Color.white},                  // 1  Blue Tang       blue/yellow/white
        new BorderTheme{a=new Color(0.15f,0.30f,0.70f), b=new Color(0.55f,0.78f,1.00f),    accent=new Color(0.90f,0.90f,1.00f)}, // 2  Blue Flowy      deep/light blue
        new BorderTheme{a=new Color(0.96f,0.88f,0.65f), b=new Color(0.48f,0.36f,0.22f),    accent=new Color(0.30f,0.22f,0.12f)}, // 3  Spotted         cream/brown
        new BorderTheme{a=new Color(0.22f,0.62f,0.28f), b=new Color(0.50f,0.82f,0.38f),    accent=new Color(0.12f,0.35f,0.15f)}, // 4  Green           jungle/lime
        new BorderTheme{a=new Color(0.52f,0.58f,0.65f), b=new Color(0.78f,0.82f,0.88f),    accent=new Color(0.35f,0.40f,0.50f)}, // 5  Tuna            steel/silver
        new BorderTheme{a=new Color(0.18f,0.50f,0.90f), b=new Color(1.00f,0.85f,0.18f),    accent=Color.white},                  // 6  Blue-Yellow     blue/gold
        new BorderTheme{a=new Color(0.80f,0.25f,0.15f), b=new Color(0.95f,0.75f,0.55f),    accent=new Color(0.50f,0.15f,0.08f)}, // 7  Spiky/Lionfish  red/tan
        new BorderTheme{a=new Color(0.70f,0.72f,0.74f), b=new Color(0.85f,0.28f,0.25f),    accent=new Color(0.40f,0.42f,0.45f)}, // 8  Piranha         silver/red
        new BorderTheme{a=new Color(0.90f,0.38f,0.42f), b=new Color(0.28f,0.78f,0.82f),    accent=new Color(0.18f,0.18f,0.40f)}, // 9  Rainbow         coral/aqua
    };

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
        _fishSprites.Clear();
        var catalog = PlayerProgress.FishCatalog;
        if (fishPoolSprites != null)
        {
            for (int i = 0; i < Mathf.Min(fishPoolSprites.Length, 20); i++)
            {
                if (i < catalog.Length && fishPoolSprites[i] != null)
                    _fishSprites[catalog[i].id] = fishPoolSprites[i];
            }
        }

        InitializeEmails();
        score = 0; streak = 0; cracks = 0;
        correctCount = 0; wonEarly = false;
        fishCaughtLeft = 0; fishCaughtRight = 0;
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
        if (boatCrackSlots != null)
            foreach (var c in boatCrackSlots)
                if (c != null) c.gameObject.SetActive(false);

        heartImages.Clear();
        if (heartsContainer != null)
            foreach (Transform t in heartsContainer)
            {
                var img = t.GetComponent<Image>();
                if (img != null) heartImages.Add(img);
            }
        UpdateHearts();

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
        PlayBgMusic();
        commentator?.Say("Sort each email into the nets — scam to the left, safe to the right!");
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
        if (cardSender.fontSize < 28) cardSender.fontSize = 28;
        cardEmail.text = e.senderEmail;
        if (cardEmail.fontSize < 22) cardEmail.fontSize = 22;
        cardSubject.text = e.subject;
        if (cardSubject.fontSize < 26) cardSubject.fontSize = 26;
        cardBody.text = e.body;
        if (cardBody.fontSize < 22) cardBody.fontSize = 22;
        cardAvatar.color = e.avatarColor;
        cardAvatarLetter.text = string.IsNullOrEmpty(e.senderName)
            ? "?" : e.senderName[0].ToString().ToUpper();

        if (cardCanvasGroup != null) cardCanvasGroup.alpha = 1f;
        swipeCard.ResetPosition();
        swipeCard.Unlock();
        UpdateProgress();
        ApplyRandomBorderTheme();  // ── CHANGED: random fish border each card ──
        StartCoroutine(CardSlideIn());
    }

    // =================================================================
    // CHANGED: Runtime border theme swap
    // Finds the builder-created BorderTop/Bottom/Left/Right GOs under
    // cardBorderRT and recolours their stripe children to a random
    // fish palette every time a new card loads.
    // =================================================================

    void ApplyRandomBorderTheme()
    {
        if (cardBorderRT == null) return;
        var theme = borderThemes[Random.Range(0, borderThemes.Length)];

        foreach (Transform child in cardBorderRT)
        {
            string n = child.gameObject.name;
            if (n != "BorderTop" && n != "BorderBottom" &&
                n != "BorderLeft" && n != "BorderRight") continue;

            var parentImg = child.GetComponent<Image>();
            if (parentImg != null) parentImg.color = theme.a;

            foreach (Transform stripe in child)
            {
                var img = stripe.GetComponent<Image>();
                if (img == null) continue;

                if (stripe.gameObject.name == "Acc")
                {
                    img.color = theme.accent;
                }
                else if (stripe.gameObject.name.StartsWith("S"))
                {
                    int si;
                    if (int.TryParse(stripe.gameObject.name.Substring(1), out si))
                        img.color = (si % 2 == 0) ? theme.a : theme.b;
                }
            }
        }
    }

    // =================================================================

    IEnumerator CardSlideIn()
    {
        PlaySFX(sfxCardFlip);
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
            score += gain; streak++;
            correctCount++;
            if (correctCount >= targetCorrectToWin) wonEarly = true;
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

    public void OnClickScam() { if (gameRunning && swipeCard != null) swipeCard.SimulateSwipe(-1); }
    public void OnClickSafe() { if (gameRunning && swipeCard != null) swipeCard.SimulateSwipe(+1); }

    // =================================================================
    // Swipe sequence
    // =================================================================

    IEnumerator SwipeSequence(int dir, bool correct, Email email)
    {
        yield return StartCoroutine(CardMorphToFish(dir, correct));

        RectTransform targetNet = (dir == -1) ? leftNetRT : rightNetRT;
        yield return StartCoroutine(FishArcToNet(fishAnimRT.anchoredPosition, targetNet, correct));

        if (correct)
        {
            PlaySFX(sfxCorrect);
            SpawnNetFish(dir);
            int gain = correctPoints + Mathf.Max(0, streak - 1) * streakBonus;
            StartCoroutine(ShowScorePopup(gain, targetNet));
            StartCoroutine(NetBounce(targetNet));
        }
        else
        {
            PlaySFX(sfxWrong);
            PlayerProgress.RegisterFish("fish_puffer");
            yield return StartCoroutine(PufferRocksBoat());
        }

        yield return StartCoroutine(ShowFeedback(correct, email.explanation));

        if (wonEarly || cracks >= maxCracks || currentIndex + 1 >= emails.Length)
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
            if (correct)
            {
                // CHANGED: pick a random fish sprite from the pool
                Sprite randomFish = null;
                if (fishPoolSprites != null && fishPoolSprites.Length > 0)
                    randomFish = fishPoolSprites[Random.Range(0, Mathf.Min(fishPoolSprites.Length, 20))];
                fishAnimImage.sprite = randomFish != null ? randomFish : fishNormalSprite;
                fishAnimImage.color = Color.white;
            }
            else
            {
                fishAnimImage.sprite = fishPufferSprite;
                fishAnimImage.color = Color.white;  // CHANGED: always white, no red tint
            }
        }

        fishAnimRT.gameObject.SetActive(true);
        Vector2 flyEnd = cardPos + new Vector2(dir * 200f, 40f);
        float dur = 0.28f, t = 0f;
        CanvasGroup cardCG = swipeCard.cardRoot?.GetComponent<CanvasGroup>();

        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur), ease = p * p;
            if (swipeCard.cardRoot != null)
                swipeCard.cardRoot.localScale = Vector3.one * Mathf.Lerp(1f, 0.05f, ease);
            if (cardCG != null) cardCG.alpha = 1f - p;
            fishAnimRT.localScale = Vector3.one * Mathf.Clamp01(p / 0.5f);
            fishAnimRT.anchoredPosition = Vector2.Lerp(cardPos, flyEnd, ease);
            fishAnimRT.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(0f, dir * -20f, ease));
            yield return null;
        }
    }

    IEnumerator FishArcToNet(Vector2 start, RectTransform netRT, bool correct)
    {
        if (fishAnimRT == null) yield break;

        Vector2 end = netRT != null
            ? netRT.anchoredPosition + new Vector2(0f, -40f)
            : new Vector2(0f, 350f);

        Vector2 mid = new Vector2((start.x + end.x) * 0.5f, end.y + 120f);

        float dur = 0.50f, t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            float ease = 1f - Mathf.Pow(1f - p, 2f);
            Vector2 a = Vector2.Lerp(start, mid, ease);
            Vector2 b = Vector2.Lerp(mid, end, ease);
            fishAnimRT.anchoredPosition = Vector2.Lerp(a, b, ease);
            fishAnimRT.localScale = Vector3.one * (0.6f + 0.4f * Mathf.Sin(p * Mathf.PI));
            fishAnimRT.localRotation = Quaternion.Euler(0, 0, -360f * ease);
            yield return null;
        }
        fishAnimRT.gameObject.SetActive(false);
        fishAnimRT.localRotation = Quaternion.identity;
    }

    // =================================================================
    // Net fish pool
    // =================================================================

    void SpawnNetFish(int dir)
    {
        RectTransform container = (dir == -1) ? leftNetFishContainer : rightNetFishContainer;
        if (container == null) return;

        if (dir == -1) fishCaughtLeft++;
        else fishCaughtRight++;

        string netFishId = PlayerProgress.GetRandomNetFishId();
        PlayerProgress.RegisterFish(netFishId);

        var go = new GameObject("NetFish", typeof(RectTransform));
        go.transform.SetParent(container, false);
        var rt = go.GetComponent<RectTransform>();
        float bw = 65f;
        float bh = 18f;
        Vector2 center = new Vector2(Random.Range(-bw * 0.5f, bw * 0.5f), Random.Range(-bh * 0.3f, bh * 0.3f));
        rt.anchoredPosition = center;
        rt.sizeDelta = new Vector2(58, 58);  // CHANGED: bigger fish in nets

        var img = go.AddComponent<Image>();
        img.color = Color.white;
        img.preserveAspect = true;
        img.raycastTarget = false;

        // CHANGED: pick directly from fishPoolSprites for a random fish_1..fish_20
        // This fixes the bug where all net fish showed as pufferfish because
        // the builder was setting fishNormalSprite = pufferfish_4_deflated.
        Sprite fishSpr = null;
        if (fishPoolSprites != null && fishPoolSprites.Length > 0)
        {
            // Skip null entries in the array
            int attempts = 0;
            while (fishSpr == null && attempts < 20)
            {
                fishSpr = fishPoolSprites[Random.Range(0, Mathf.Min(fishPoolSprites.Length, 20))];
                attempts++;
            }
        }
        if (fishSpr != null) img.sprite = fishSpr;
        else if (fishNormalSprite != null) img.sprite = fishNormalSprite;
        else if (circleSprite != null) img.sprite = circleSprite;

        float facing = Random.value > 0.5f ? 1f : -1f;
        rt.localScale = new Vector3(facing, 1f, 1f);

        // CHANGED: bumped amplitudes and slowed frequencies for more visible swimming
        poolFishStates.Add(new PoolFishState
        {
            rt = rt,
            center = center,
            phaseX = Random.Range(0f, Mathf.PI * 2f),
            phaseY = Random.Range(0f, Mathf.PI * 2f),
            freqX = Random.Range(0.35f, 0.85f),     // was 0.5–1.1
            freqY = Random.Range(0.80f, 1.50f),      // was 1.0–1.8
            ampX = Random.Range(38f, 80f),            // was 30–65
            ampY = Random.Range(10f, 24f),            // was 6–18
        });

        StartCoroutine(SplashIn(rt, facing));
    }

    IEnumerator SplashIn(RectTransform rt, float facing)
    {
        rt.localScale = new Vector3(facing * 1.5f, 1.5f, 1f);
        float t = 0f;
        while (t < 0.25f)
        {
            t += Time.deltaTime;
            if (rt == null) yield break;
            float s = Mathf.Lerp(1.5f, 1f, Mathf.Clamp01(t / 0.25f));
            rt.localScale = new Vector3(facing * s, s, 1f);
            yield return null;
        }
    }

    void AnimatePoolFish()
    {
        float time = Time.time;
        for (int i = poolFishStates.Count - 1; i >= 0; i--)
        {
            var s = poolFishStates[i];
            if (s.rt == null) { poolFishStates.RemoveAt(i); continue; }

            float x = s.center.x + Mathf.Sin(time * s.freqX + s.phaseX) * s.ampX;
            float y = s.center.y + Mathf.Sin(time * s.freqY + s.phaseY) * s.ampY;
            s.rt.anchoredPosition = new Vector2(x, y);

            float dx = Mathf.Cos(time * s.freqX + s.phaseX);
            if (Mathf.Abs(dx) > 0.05f)
                s.rt.localScale = new Vector3(dx > 0 ? 1f : -1f, 1f, 1f);

            poolFishStates[i] = s;
        }
    }

    // =================================================================
    // Net bounce
    // =================================================================

    IEnumerator NetBounce(RectTransform netRT)
    {
        if (netRT == null) yield break;
        Vector3 orig = netRT.localScale;
        float t = 0f;
        while (t < 0.25f)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / 0.25f);
            netRT.localScale = Vector3.one * (1f + 0.10f * Mathf.Sin(p * Mathf.PI));
            yield return null;
        }
        netRT.localScale = orig;
    }

    // =================================================================
    // Puffer rocks / damages the boat
    // =================================================================

    IEnumerator PufferRocksBoat()
    {
        int crackIdx = Mathf.Clamp(cracks - 1, 0, (boatCrackSlots?.Length ?? 1) - 1);
        if (boatCrackSlots != null && crackIdx < boatCrackSlots.Length && boatCrackSlots[crackIdx] != null)
        {
            boatCrackSlots[crackIdx].gameObject.SetActive(true);
            StartCoroutine(PunchScale(boatCrackSlots[crackIdx], 0.3f, 1.6f));
        }
        yield return StartCoroutine(RockBoat(0.55f, cracks));
        if (cracks >= maxCracks) StartCoroutine(SinkBoat());
    }

    IEnumerator RockBoat(float dur, int severity)
    {
        if (boatRoot == null) yield break;
        Vector2 origPos = boatRoot.anchoredPosition;
        float origRot = boatRoot.localEulerAngles.z;
        if (origRot > 180f) origRot -= 360f;

        float tiltMax = Mathf.Lerp(4f, 18f, (float)(severity - 1) / (maxCracks - 1));
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float d = 1f - Mathf.Clamp01(t / dur);
            float tilt = Mathf.Sin(t * 22f) * tiltMax * d;
            float shakeX = Random.Range(-6f, 6f) * d;
            float shakeY = Random.Range(-3f, 3f) * d;
            boatRoot.localEulerAngles = new Vector3(0, 0, origRot + tilt);
            boatRoot.anchoredPosition = origPos + new Vector2(shakeX, shakeY);
            yield return null;
        }

        float finalTilt = cracks >= maxCracks ? -12f : Mathf.Lerp(0f, -8f, (float)cracks / maxCracks);
        boatRoot.localEulerAngles = new Vector3(0, 0, origRot + finalTilt);
        boatRoot.anchoredPosition = origPos;
    }

    IEnumerator SinkBoat()
    {
        if (boatSinking || boatRoot == null) yield break;
        boatSinking = true;
        yield return new WaitForSeconds(0.5f);

        Vector2 startPos = boatRoot.anchoredPosition;
        Vector2 endPos = startPos + new Vector2(40f, -300f);
        float startRot = boatRoot.localEulerAngles.z;
        if (startRot > 180f) startRot -= 360f;

        float t = 0f, dur = 2.2f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            float ease = p * p;
            boatRoot.anchoredPosition = Vector2.Lerp(startPos, endPos, ease);
            boatRoot.localEulerAngles = new Vector3(0, 0, Mathf.Lerp(startRot, startRot - 45f, ease));
            yield return null;
        }
    }

    IEnumerator PunchScale(RectTransform rt, float dur, float peak)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            rt.localScale = Vector3.one * (1f + (peak - 1f) * Mathf.Sin(p * Mathf.PI));
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    // =================================================================
    // Score popup
    // =================================================================

    IEnumerator ShowScorePopup(int points, RectTransform nearRT)
    {
        if (scorePopupRT == null) yield break;
        Vector2 anchor = nearRT != null
            ? nearRT.anchoredPosition + new Vector2(0f, 50f)
            : new Vector2(0f, 300f);
        scorePopupRT.anchoredPosition = anchor;
        if (scorePopupText != null) scorePopupText.text = "+" + points;
        scorePopupCG.alpha = 1f;
        Vector2 start = anchor;
        float t = 0f;
        while (t < 0.8f)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / 0.8f);
            scorePopupRT.anchoredPosition = start + new Vector2(0f, 50f * p);
            scorePopupCG.alpha = 1f - p * p;
            yield return null;
        }
        scorePopupCG.alpha = 0f;
    }

    // =================================================================
    // HUD
    // =================================================================

    void UpdateAllUI()
    {
        UpdateScoreUI(); UpdateStreakUI(); UpdateProgress(); UpdateHearts();
    }

    void UpdateHearts()
    {
        int livesLeft = maxCracks - cracks;
        for (int i = 0; i < heartImages.Count; i++)
        {
            if (heartImages[i] == null) continue;
            heartImages[i].color = i < livesLeft ? Color.white : new Color(0.35f, 0.35f, 0.40f, 0.55f);
        }
    }

    // CHANGED: null guards to fix NullReferenceException
    void UpdateTimerUI()
    {
        int sec = Mathf.CeilToInt(timeRemaining);
        bool low = timeRemaining < 15f;
        if (timerText != null)
        {
            timerText.text = $"{sec / 60}:{sec % 60:D2}";
            timerText.color = low ? new Color(0.91f, 0.30f, 0.24f) : Color.white;
        }
        if (timerFill != null)
        {
            timerFill.fillAmount = Mathf.Clamp01(timeRemaining / gameDurationSeconds);
            timerFill.color = low ? new Color(0.91f, 0.30f, 0.24f) : new Color(0.31f, 0.80f, 0.77f);
        }
    }

    void UpdateScoreUI() { if (scoreText != null) scoreText.text = $"Score: {score}"; }
    void UpdateStreakUI() { if (streakText != null) streakText.text = streak >= 3 ? $"{streak} in a row!" : ""; }
    void UpdateProgress() { if (progressText != null) progressText.text = $"{correctCount} / {targetCorrectToWin} correct"; }

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
        if (cracks == maxCracks - 1) commentator.Say("One more hit and the boat sinks!");
        else if (cracks == maxCracks - 2) commentator.Say("Be careful — the boat's taking on water…");
        else commentator.SayRandom(new[] { "Oh no, that pufferfish hit the hull!", "Ouch! The boat's rocking…", "Don't worry, scammers are clever." });
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

        yield return new WaitForSeconds(2.0f);

        if (cg != null)
        {
            float fo = 0f;
            while (fo < 0.2f) { fo += Time.deltaTime; cg.alpha = 1f - fo / 0.2f; yield return null; }
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
        StopBgMusic();
        ShowResult();
    }

    void ShowResult()
    {
        resultPanel.SetActive(true);
        int totalPossible = emails.Length * correctPoints;
        if (resultScore != null) resultScore.text = $"{score} / {totalPossible}";
        int correct = 0;
        for (int i = 0; i < correctAnswers.Length; i++)
            if (answered[i] && correctAnswers[i]) correct++;

        float pct = wonEarly ? 1f : (emails.Length > 0 ? (float)correct / emails.Length : 0);
        PlayerProgress.QueueFromPerformance(pct);
        if (cracks == 0) PlayerProgress.RegisterFish("fish_guardian");

        string stars, msg, quip;
        if (wonEarly)
        { stars = "★ ★ ★"; msg = $"You sorted {targetCorrectToWin} correctly — mission complete!"; quip = "Excellent work! You've got a real eye for this now."; }
        else if (cracks >= maxCracks)
        { stars = "★"; msg = "The boat sank! Those pufferfish got you.\nRead carefully and try again."; quip = "Oh dear… the boat couldn't take any more."; }
        else if (timeRemaining <= 0 && pct < 0.7f)
        { stars = "★"; msg = "Time's up — you'll be quicker next time."; quip = "Time got away from us, dear."; }
        else if (pct >= 0.95f)
        { stars = "★ ★ ★"; msg = "Phish-master! The nets are full of happy fish."; quip = "Oh thank you, dear! You're wonderful."; }
        else if (pct >= 0.7f)
        { stars = "★ ★"; msg = "Solid work! Review the ones that got you."; quip = "That was a big help — thank you, dear!"; }
        else
        { stars = "★"; msg = "Scammers are tricky. Try again and read carefully."; quip = "It's a good start. We'll get them next time."; }

        if (resultStars != null) resultStars.text = stars;
        if (resultMessage != null) resultMessage.text = msg;
        commentator?.Say(quip);
    }

    public void OnPlayAgain() { SceneManager.LoadScene(SceneManager.GetActiveScene().name); }
    public void OnReturnToMap() { SceneManager.LoadScene("WorldMap"); }

    // =================================================================
    // Helpers
    // =================================================================

    public static Image MakeImage(Transform parent, string name, Color color)
    { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); var img = go.AddComponent<Image>(); img.color = color; return img; }

    public static TMP_Text MakeText(Transform parent, string name, string content, int size, Color color,
        TextAlignmentOptions align, FontStyles style = FontStyles.Normal)
    { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); var t = go.AddComponent<TextMeshProUGUI>(); t.text = content; t.fontSize = size; t.color = color; t.alignment = align; t.fontStyle = style; t.raycastTarget = false; return t; }

    // =================================================================
    // Email data
    // =================================================================

    void InitializeEmails()
    {
        emails = new Email[]
        {
            new Email { senderName="PayPal Support",    senderEmail="support@paypa1.com",               subject="URGENT: Your account has been suspended", preview="Dear Customer, your PayPal account has been suspended", body="Dear Customer,\n\nYour PayPal account has been suspended due to suspicious activity. Click below immediately to verify your information or your account will be permanently deleted within 24 hours.\n\n— PayPal Support", timestamp="10:09 AM", avatarColor=new Color(0.07f,0.45f,0.71f), isScam=true,  explanation="SCAM — The sender domain is 'paypa1.com' (number 1, not L). Real PayPal emails come from paypal.com. The 24-hour deletion threat is a classic urgency trick." },
            new Email { senderName="Spotify",           senderEmail="newsletter@spotify.com",           subject="Your June playlist is ready",               preview="Your monthly Spotify stats are in",                    body="Hi there,\n\nYour monthly Spotify stats are in. Check out your top songs from this month in the app.\n\n— The Spotify Team",                                                                                                              timestamp="9:42 AM",  avatarColor=new Color(0.12f,0.84f,0.38f), isScam=false, explanation="SAFE — Real domain (spotify.com), calm tone, no threats, no request for personal info." },
            new Email { senderName="Canada Revenue Agency", senderEmail="noreply@canada-revenue-agency-refund.com", subject="You have a $847 tax refund waiting", preview="Provide your SIN and banking details",             body="NOTICE FROM THE CRA:\n\nA refund of $847.00 is ready. To claim it, provide your SIN and banking details within 24 hours or the refund will be cancelled.\n\n— Canada Revenue Agency",                                                        timestamp="8:27 AM",  avatarColor=new Color(0.78f,0.13f,0.13f), isScam=true,  explanation="SCAM — The CRA never emails asking for your SIN. Real messages come from cra-arc.gc.ca only. The 24-hour deadline is another red flag." },
            new Email { senderName="Amazon",            senderEmail="orders@amazon.com",                subject="Your order has shipped",                    preview="Order #112-4857293 has shipped",                       body="Hello,\n\nYour order #112-4857293 has shipped. Estimated delivery June 12. Track it in the Amazon app.\n\n— Amazon",                                                                                                                         timestamp="Yesterday",avatarColor=new Color(1f,0.6f,0f),   isScam=false, explanation="SAFE — Real domain (amazon.com), specific order number, no request for personal info." },
            new Email { senderName="Microsoft Security",senderEmail="security@micros0ft-account.net",  subject="Unusual sign-in detected",                  preview="Click below immediately to secure your account",       body="Dear User,\n\nWe detected a sign-in from an unrecognized location. Click below immediately to secure your account.\n\n— Microsoft Security Team",                                                                                                timestamp="Yesterday",avatarColor=new Color(0.05f,0.45f,0.79f), isScam=true, explanation="SCAM — Domain is 'micros0ft-account.net' (zero, not O). 'Dear User' is generic — Microsoft uses your name." },
            new Email { senderName="Uber",              senderEmail="no-reply@uber.com",               subject="Your Tuesday night trip receipt",            preview="Your trip: $14.72",                                    body="Thanks for riding with Uber.\n\nYour trip came to $14.72. Payment charged to Visa ending in 4821.\n\n— Uber",                                                                                                                                  timestamp="May 4",    avatarColor=new Color(0.1f,0.1f,0.1f), isScam=false,  explanation="SAFE — Real domain, specific details, only last 4 card digits shown, no suspicious links." },
            new Email { senderName="Netflix Billing",   senderEmail="billing@netfl1x-payments.com",   subject="Payment failed — update card now",           preview="Update billing within 48 hours",                       body="Hello,\n\nYour Netflix payment could not be processed. Update your billing details within 48 hours or your account will be terminated.\n\n— Netflix Billing",                                                                                    timestamp="May 3",    avatarColor=new Color(0.90f,0.05f,0.10f), isScam=true, explanation="SCAM — Domain is 'netfl1x-payments.com' (number 1 not L). Real billing comes from netflix.com." },
            new Email { senderName="Google Calendar",   senderEmail="calendar-noreply@google.com",    subject="Reminder: Coffee with Sarah at 2pm",         preview="Event reminder for today",                             body="This is a reminder for your event:\n\nCoffee with Sarah\nToday at 2:00 PM\nThe Wired Monk Cafe\n\n— Google Calendar",                                                                                                                           timestamp="May 3",    avatarColor=new Color(0.26f,0.52f,0.96f), isScam=false, explanation="SAFE — Real Google domain, specific event, no suspicious links or requests." },
        };
    }
}