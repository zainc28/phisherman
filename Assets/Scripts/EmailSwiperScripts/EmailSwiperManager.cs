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
    public int targetCorrectToWin = 5;
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
    public Sprite[] fishPoolSprites;

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

    [Header("Card Border")]
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
    public RectTransform feedbackCardRT;
    public TMP_Text feedbackIcon;

    // ===== Objective =====
    [Header("Objective")]
    public GameObject objectivePanel;
    public TMP_Text objectiveBodyText;

    // ===== Result =====
    [Header("Result")]
    public TMP_Text resultStars;
    public TMP_Text resultScore;
    public TMP_Text resultMessage;

    // ===== Internal =====
    private int score, streak, cracks, currentIndex, fishCaughtLeft, fishCaughtRight;
    private float timeRemaining;
    private bool gameRunning, warnedLowTime;
    private bool[] answered, correctAnswers;
    private bool boatSinking;
    private int correctCount;
    private bool wonEarly;
    private Dictionary<string, Sprite> _fishSprites = new Dictionary<string, Sprite>();

    private struct PoolFishState
    {
        public RectTransform rt;
        public Vector2 center;
        public float phaseX, phaseY, freqX, freqY, ampX, ampY;
    }
    private List<PoolFishState> poolFishStates = new List<PoolFishState>();

    public enum CardLayout { Email, Website, SMS, Social }

    private struct BorderTheme { public Color a, b, accent; }
    private static readonly BorderTheme[] borderThemes =
    {
        new BorderTheme{a=new Color(1.00f,0.42f,0.10f), b=Color.white,                     accent=new Color(0.10f,0.10f,0.10f)},
        new BorderTheme{a=new Color(0.10f,0.43f,1.00f), b=new Color(1.00f,0.88f,0.20f),    accent=Color.white},
        new BorderTheme{a=new Color(0.15f,0.30f,0.70f), b=new Color(0.55f,0.78f,1.00f),    accent=new Color(0.90f,0.90f,1.00f)},
        new BorderTheme{a=new Color(0.96f,0.88f,0.65f), b=new Color(0.48f,0.36f,0.22f),    accent=new Color(0.30f,0.22f,0.12f)},
        new BorderTheme{a=new Color(0.22f,0.62f,0.28f), b=new Color(0.50f,0.82f,0.38f),    accent=new Color(0.12f,0.35f,0.15f)},
        new BorderTheme{a=new Color(0.52f,0.58f,0.65f), b=new Color(0.78f,0.82f,0.88f),    accent=new Color(0.35f,0.40f,0.50f)},
        new BorderTheme{a=new Color(0.18f,0.50f,0.90f), b=new Color(1.00f,0.85f,0.18f),    accent=Color.white},
        new BorderTheme{a=new Color(0.80f,0.25f,0.15f), b=new Color(0.95f,0.75f,0.55f),    accent=new Color(0.50f,0.15f,0.08f)},
        new BorderTheme{a=new Color(0.70f,0.72f,0.74f), b=new Color(0.85f,0.28f,0.25f),    accent=new Color(0.40f,0.42f,0.45f)},
        new BorderTheme{a=new Color(0.90f,0.38f,0.42f), b=new Color(0.28f,0.78f,0.82f),    accent=new Color(0.18f,0.18f,0.40f)},
    };

    [System.Serializable]
    public struct Email
    {
        public string senderName;
        public string senderEmail;
        public string subject;
        public string preview;
        public string body;
        public string timestamp;
        public string explanation;
        public Color avatarColor;
        public bool isScam;
        public CardLayout layout;
    }

    private Email[] emails;
    private int _worldTheme = 1;

    // iMessage colors
    static readonly Color iMsgBubbleIn = new Color(0.90f, 0.90f, 0.92f);
    static readonly Color iMsgBubbleOut = new Color(0.00f, 0.48f, 1.00f);
    static readonly Color iMsgBg = new Color(0.97f, 0.97f, 0.97f);
    static readonly Color iMsgHeader = new Color(0.95f, 0.95f, 0.95f);

    // Facebook colors
    static readonly Color FBBlue = new Color(0.11f, 0.46f, 0.95f);
    static readonly Color FBDark = new Color(0.11f, 0.13f, 0.13f);
    static readonly Color FBCard = new Color(0.98f, 0.98f, 1.00f);
    static readonly Color FBMuted = new Color(0.40f, 0.40f, 0.45f);

    // Website colors
    static readonly Color WebBg = new Color(0.97f, 0.97f, 1.00f);

    // SMS / Social legacy
    static readonly Color SMSBg = new Color(0.96f, 0.96f, 0.98f);
    static readonly Color SocialBg = new Color(0.24f, 0.40f, 0.70f);
    static readonly Color SocialCard = new Color(0.98f, 0.98f, 1.00f);

    // ===================================================================
    //  Lifecycle
    // ===================================================================
    void Start()
    {
        _worldTheme = MinigameTheme.Get();

        _fishSprites.Clear();
        var catalog = PlayerProgress.FishCatalog;
        if (fishPoolSprites != null)
            for (int i = 0; i < Mathf.Min(fishPoolSprites.Length, 20); i++)
                if (i < catalog.Length && fishPoolSprites[i] != null)
                    _fishSprites[catalog[i].id] = fishPoolSprites[i];

        switch (_worldTheme)
        {
            case 2: InitializeWebsiteContent(); break;
            case 3: InitializeSMSContent(); break;
            case 4: InitializeSocialContent(); break;
            case 5: InitializeCombinedContent(); break;
            default: InitializeEmails(); break;
        }

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
            { var img = t.GetComponent<Image>(); if (img != null) heartImages.Add(img); }
        UpdateHearts();

        // Setup audio sources — use builder-assigned sources, then ensure clips are wired
        var sources = GetComponents<AudioSource>();
        _sfxSource = sources.Length > 0 ? sources[0] : gameObject.AddComponent<AudioSource>();
        _musicSource = sources.Length > 1 ? sources[1] : gameObject.AddComponent<AudioSource>();
        _sfxSource.playOnAwake = false; _sfxSource.loop = false; _sfxSource.volume = sfxVolume;
        _musicSource.playOnAwake = false; _musicSource.loop = true; _musicSource.volume = musicVolume;
        // Clips are assigned in the Inspector/builder as serialized fields — wire them to the sources now
        if (bgMusic != null && _musicSource.clip == null) _musicSource.clip = bgMusic;
        // sfxCardFlip, sfxCorrect, sfxWrong are played via PlayOneShot so no clip assignment needed
    }

    // ===================================================================
    //  Audio
    // ===================================================================
    void PlayBgMusic()
    {
        if (_musicSource == null || bgMusic == null) return;
        _musicSource.clip = bgMusic;
        _musicSource.volume = musicVolume;
        _musicSource.loop = true;
        _musicSource.Play();
    }
    void StopBgMusic() { if (_musicSource != null && _musicSource.isPlaying) _musicSource.Stop(); }
    void PlaySFX(AudioClip clip)
    {
        if (_sfxSource == null || clip == null) return;
        _sfxSource.PlayOneShot(clip, sfxVolume);
    }

    // ===================================================================
    //  Update
    // ===================================================================
    void Update()
    {
        if (!gameRunning) return;
        timeRemaining -= Time.deltaTime;
        UpdateTimerUI();
        if (timeRemaining <= 0f) { timeRemaining = 0f; EndGame(); }
        AnimatePoolFish();
    }

    // ===================================================================
    //  Tutorial start
    // ===================================================================
    public void OnTutorialStart()
    {
        tutorialPanel.SetActive(false);
        gameRoot.SetActive(true);
        hudPanel.SetActive(true);
        UpdateAllUI();
        StartCoroutine(ShowObjectiveThenBegin());
    }

    // ===================================================================
    //  Objective panel — brief reminder shown before the first card
    // ===================================================================
    IEnumerator ShowObjectiveThenBegin()
    {
        if (objectivePanel != null)
        {
            if (objectiveBodyText != null)
                objectiveBodyText.text = $"Identify {targetCorrectToWin} scams correctly to win!\n\nSwipe LEFT for scam, RIGHT for safe.";
            objectivePanel.SetActive(true);
            var cg = objectivePanel.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 0f;
                float fi = 0f;
                while (fi < 0.2f) { fi += Time.deltaTime; cg.alpha = Mathf.Clamp01(fi / 0.2f); yield return null; }
                cg.alpha = 1f;
            }

            yield return new WaitForSeconds(2.5f);

            if (cg != null)
            {
                float fo = 0f;
                while (fo < 0.25f) { fo += Time.deltaTime; cg.alpha = 1f - Mathf.Clamp01(fo / 0.25f); yield return null; }
            }
            objectivePanel.SetActive(false);
        }

        LoadCard(0);
        gameRunning = true;
        PlayBgMusic();
    }

    // ===================================================================
    //  Card loading
    // ===================================================================
    void LoadCard(int idx)
    {
        if (idx >= emails.Length) { EndGame(); return; }
        currentIndex = idx;
        var e = emails[idx];
        ApplyCardLayout(e);
        ApplyRandomBorderTheme();
        if (cardCanvasGroup != null) cardCanvasGroup.alpha = 1f;
        swipeCard.ResetPosition();
        swipeCard.Unlock();
        UpdateProgress();
        StartCoroutine(CardSlideIn());
    }

    void ApplyCardLayout(Email e)
    {
        switch (e.layout)
        {
            case CardLayout.SMS: ApplySMSLayout(e); break;
            case CardLayout.Social: ApplySocialLayout(e); break;
            case CardLayout.Website: ApplyWebsiteLayout(e); break;
            default: ApplyEmailLayout(e); break;
        }
    }

    void ApplyEmailLayout(Email e)
    {
        if (cardSender != null) { cardSender.text = e.senderName; cardSender.fontSize = 22; cardSender.color = new Color(0.10f, 0.10f, 0.20f); }
        if (cardEmail != null) { cardEmail.text = e.senderEmail; cardEmail.fontSize = 16; cardEmail.color = new Color(0.40f, 0.40f, 0.50f); cardEmail.gameObject.SetActive(true); }
        if (cardSubject != null) { cardSubject.text = e.subject; cardSubject.fontSize = 24; cardSubject.color = new Color(0.10f, 0.10f, 0.20f); cardSubject.fontStyle = FontStyles.Bold; cardSubject.gameObject.SetActive(true); }
        if (cardBody != null) { cardBody.text = e.body; cardBody.fontSize = 19; cardBody.color = new Color(0.10f, 0.10f, 0.20f); cardBody.alignment = TextAlignmentOptions.TopLeft; }
        if (cardAvatar != null) { cardAvatar.color = e.avatarColor; cardAvatar.gameObject.SetActive(true); }
        if (cardAvatarLetter != null) cardAvatarLetter.text = string.IsNullOrEmpty(e.senderName) ? "?" : e.senderName[0].ToString().ToUpper();
        SetCardBg(Color.white);
    }

    void ApplyWebsiteLayout(Email e)
    {
        // World 2 – website / forum look
        if (cardSender != null) { cardSender.text = e.senderName; cardSender.fontSize = 20; cardSender.color = new Color(0.10f, 0.10f, 0.20f); }
        if (cardEmail != null) { cardEmail.text = "🔒 " + e.senderEmail; cardEmail.fontSize = 13; cardEmail.color = new Color(0.18f, 0.55f, 0.18f); cardEmail.gameObject.SetActive(true); }
        if (cardSubject != null) { cardSubject.text = e.subject; cardSubject.fontSize = 21; cardSubject.color = new Color(0.08f, 0.08f, 0.18f); cardSubject.fontStyle = FontStyles.Bold; cardSubject.gameObject.SetActive(true); }
        if (cardBody != null)
        {
            // Wrap body in a styled "web page" look
            string styled = $"<size=11><color=#888888>──────────────────────────</color></size>\n{e.body}";
            cardBody.text = styled; cardBody.fontSize = 17; cardBody.color = new Color(0.12f, 0.12f, 0.20f); cardBody.alignment = TextAlignmentOptions.TopLeft;
        }
        if (cardAvatar != null) { cardAvatar.color = e.avatarColor; cardAvatar.gameObject.SetActive(true); }
        if (cardAvatarLetter != null) cardAvatarLetter.text = string.IsNullOrEmpty(e.senderName) ? "?" : e.senderName[0].ToString().ToUpper();
        SetCardBg(WebBg);
    }

    // ---------------------------------------------------------------
    //  iMessage-style SMS layout
    // ---------------------------------------------------------------
    void ApplySMSLayout(Email e)
    {
        // Header: contact name centred like iMessage
        if (cardSender != null)
        {
            cardSender.text = e.senderName;
            cardSender.fontSize = 17;
            cardSender.color = new Color(0.08f, 0.08f, 0.10f);
            cardSender.fontStyle = FontStyles.Bold;
            cardSender.alignment = TextAlignmentOptions.Center;
        }
        if (cardEmail != null) cardEmail.gameObject.SetActive(false);
        if (cardSubject != null) cardSubject.gameObject.SetActive(false);

        // Avatar becomes the contact icon circle at top-centre
        if (cardAvatar != null)
        {
            cardAvatar.color = e.avatarColor;
            cardAvatar.gameObject.SetActive(true);
        }
        if (cardAvatarLetter != null)
            cardAvatarLetter.text = string.IsNullOrEmpty(e.senderName) ? "?" : e.senderName[0].ToString().ToUpper();

        // Build iMessage bubble layout in body
        if (cardBody != null)
        {
            var lines = e.body.Split('\n');
            var sb = new System.Text.StringBuilder();
            foreach (var line in lines)
            {
                if (line.StartsWith("THEM: "))
                {
                    // Incoming: left-aligned, grey bubble style
                    sb.AppendLine($"<align=left><color=#1C1C1E><size=90%>{line.Substring(6)}</size></color></align>");
                    sb.AppendLine();
                }
                else if (line.StartsWith("YOU: "))
                {
                    // Outgoing: right-aligned, blue bubble style
                    sb.AppendLine($"<align=right><color=#0A84FF><size=90%>{line.Substring(5)}</size></color></align>");
                    sb.AppendLine();
                }
                else if (!string.IsNullOrWhiteSpace(line))
                {
                    // Timestamp / system message
                    sb.AppendLine($"<align=center><color=#8E8E93><size=75%>{line}</size></color></align>");
                    sb.AppendLine();
                }
            }
            cardBody.text = sb.ToString();
            cardBody.fontSize = 16;
            cardBody.alignment = TextAlignmentOptions.TopLeft;
            cardBody.color = Color.black;
        }
        SetCardBg(iMsgBg);
    }

    // ---------------------------------------------------------------
    //  Facebook-style Social layout
    // ---------------------------------------------------------------
    void ApplySocialLayout(Email e)
    {
        // Header: Facebook blue bar look
        if (cardSender != null)
        {
            cardSender.text = e.senderName;
            cardSender.fontSize = 18;
            cardSender.color = new Color(0.06f, 0.06f, 0.09f);
            cardSender.fontStyle = FontStyles.Bold;
        }
        if (cardEmail != null)
        {
            // Show handle/page name in muted colour
            cardEmail.text = e.senderEmail;
            cardEmail.fontSize = 13;
            cardEmail.color = FBMuted;
            cardEmail.gameObject.SetActive(true);
        }
        if (cardSubject != null)
        {
            cardSubject.text = e.subject;
            cardSubject.fontSize = 19;
            cardSubject.color = new Color(0.06f, 0.06f, 0.09f);
            cardSubject.fontStyle = FontStyles.Bold;
            cardSubject.gameObject.SetActive(true);
        }
        if (cardBody != null)
        {
            // Facebook post body: add "Like · Comment · Share" footer
            string postBody = e.body + "\n\n<size=75%><color=#0866FF>👍 Like   💬 Comment   ↗ Share</color></size>";
            cardBody.text = postBody;
            cardBody.fontSize = 16;
            cardBody.color = new Color(0.08f, 0.08f, 0.12f);
            cardBody.alignment = TextAlignmentOptions.TopLeft;
        }
        if (cardAvatar != null) { cardAvatar.color = e.avatarColor; cardAvatar.gameObject.SetActive(true); }
        if (cardAvatarLetter != null)
            cardAvatarLetter.text = string.IsNullOrEmpty(e.senderName) ? "?" : e.senderName[0].ToString().ToUpper();
        SetCardBg(FBCard);
    }

    void SetCardBg(Color col)
    {
        if (swipeCard == null || swipeCard.cardRoot == null) return;
        var bgImg = swipeCard.cardRoot.Find("CardBg")?.GetComponent<Image>();
        if (bgImg != null) bgImg.color = col;
    }

    void ApplyRandomBorderTheme()
    {
        if (cardBorderRT == null) return;
        var theme = borderThemes[Random.Range(0, borderThemes.Length)];
        foreach (Transform child in cardBorderRT)
        {
            string n = child.gameObject.name;
            if (n != "BorderTop" && n != "BorderBottom" && n != "BorderLeft" && n != "BorderRight") continue;
            var parentImg = child.GetComponent<Image>();
            if (parentImg != null) parentImg.color = theme.a;
            foreach (Transform stripe in child)
            {
                var img = stripe.GetComponent<Image>(); if (img == null) continue;
                if (stripe.gameObject.name == "Acc") img.color = theme.accent;
                else if (stripe.gameObject.name.StartsWith("S"))
                { int si; if (int.TryParse(stripe.gameObject.name.Substring(1), out si)) img.color = (si % 2 == 0) ? theme.a : theme.b; }
            }
        }
    }

    IEnumerator CardSlideIn()
    {
        PlaySFX(sfxCardFlip);
        var rt = swipeCard.cardRoot; if (rt == null) yield break;
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

    // ===================================================================
    //  Swipe commit
    // ===================================================================
    void OnSwipeCommit(int dir)
    {
        bool playerSaidScam = (dir == -1);
        var email = emails[currentIndex];
        bool correct = (playerSaidScam == email.isScam);
        answered[currentIndex] = true; correctAnswers[currentIndex] = correct;

        // Play card-flip SFX on every swipe
        PlaySFX(sfxCardFlip);

        if (correct)
        {
            int gain = correctPoints + Mathf.Max(0, streak) * streakBonus;
            score += gain; streak++; correctCount++;
            if (correctCount >= targetCorrectToWin) wonEarly = true;
        }
        else
        {
            streak = 0; cracks++;
            UpdateHearts();
        }
        UpdateAllUI();
        StartCoroutine(SwipeSequence(dir, correct, email));
    }

    public void OnClickScam() { if (gameRunning && swipeCard != null) swipeCard.SimulateSwipe(-1); }
    public void OnClickSafe() { if (gameRunning && swipeCard != null) swipeCard.SimulateSwipe(+1); }

    IEnumerator SwipeSequence(int dir, bool correct, Email email)
    {
        yield return StartCoroutine(CardMorphToFish(dir, correct));
        RectTransform targetNet = (dir == -1) ? leftNetRT : rightNetRT;
        yield return StartCoroutine(FishArcToNet(fishAnimRT.anchoredPosition, targetNet, correct));

        if (correct)
        {
            PlaySFX(sfxCorrect); // water_splash on correct
            SpawnNetFish(dir);
            int gain = correctPoints + Mathf.Max(0, streak - 1) * streakBonus;
            StartCoroutine(ShowScorePopup(gain, targetNet));
            StartCoroutine(NetBounce(targetNet));
        }
        else
        {
            PlaySFX(sfxWrong); // impact on wrong
            PlayerProgress.RegisterFish("fish_puffer");
            yield return StartCoroutine(PufferRocksBoat());
        }

        yield return StartCoroutine(ShowFeedback(correct, email.explanation));
        if (wonEarly || cracks >= maxCracks || currentIndex + 1 >= emails.Length) EndGame();
        else LoadCard(currentIndex + 1);
    }

    IEnumerator CardMorphToFish(int dir, bool correct)
    {
        if (fishAnimRT == null) yield break;
        Vector2 cardPos = swipeCard.cardRoot != null ? swipeCard.cardRoot.anchoredPosition : Vector2.zero;
        fishAnimRT.anchoredPosition = cardPos; fishAnimRT.localScale = Vector3.zero; fishAnimRT.localRotation = Quaternion.identity;
        if (fishAnimImage != null)
        {
            if (correct) { Sprite r = null; if (fishPoolSprites != null && fishPoolSprites.Length > 0) r = fishPoolSprites[Random.Range(0, Mathf.Min(fishPoolSprites.Length, 20))]; fishAnimImage.sprite = r != null ? r : fishNormalSprite; fishAnimImage.color = Color.white; }
            else { fishAnimImage.sprite = fishPufferSprite; fishAnimImage.color = Color.white; }
        }
        fishAnimRT.gameObject.SetActive(true);
        Vector2 flyEnd = cardPos + new Vector2(dir * 200f, 40f); float dur = 0.28f, t = 0f;
        CanvasGroup cardCG = swipeCard.cardRoot?.GetComponent<CanvasGroup>();
        while (t < dur)
        {
            t += Time.deltaTime; float p = Mathf.Clamp01(t / dur), ease = p * p;
            if (swipeCard.cardRoot != null) swipeCard.cardRoot.localScale = Vector3.one * Mathf.Lerp(1f, 0.05f, ease);
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
        Vector2 end = netRT != null ? netRT.anchoredPosition + new Vector2(0f, -40f) : new Vector2(0f, 350f);
        Vector2 mid = new Vector2((start.x + end.x) * 0.5f, end.y + 120f);
        float dur = 0.50f, t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime; float p = Mathf.Clamp01(t / dur), ease = 1f - Mathf.Pow(1f - p, 2f);
            Vector2 a = Vector2.Lerp(start, mid, ease), b = Vector2.Lerp(mid, end, ease);
            fishAnimRT.anchoredPosition = Vector2.Lerp(a, b, ease);
            fishAnimRT.localScale = Vector3.one * (0.6f + 0.4f * Mathf.Sin(p * Mathf.PI));
            fishAnimRT.localRotation = Quaternion.Euler(0, 0, -360f * ease); yield return null;
        }
        fishAnimRT.gameObject.SetActive(false); fishAnimRT.localRotation = Quaternion.identity;
    }

    void SpawnNetFish(int dir)
    {
        RectTransform container = (dir == -1) ? leftNetFishContainer : rightNetFishContainer;
        if (container == null) return;
        if (dir == -1) fishCaughtLeft++; else fishCaughtRight++;
        string netFishId = PlayerProgress.GetRandomNetFishId(); PlayerProgress.RegisterFish(netFishId);
        var go = new GameObject("NetFish", typeof(RectTransform)); go.transform.SetParent(container, false);
        var rt = go.GetComponent<RectTransform>();
        Vector2 center = new Vector2(Random.Range(-32f, 32f), Random.Range(-9f, 9f));
        rt.anchoredPosition = center; rt.sizeDelta = new Vector2(58, 58);
        var img = go.AddComponent<Image>(); img.color = Color.white; img.preserveAspect = true; img.raycastTarget = false;
        Sprite fishSpr = null;
        if (fishPoolSprites != null && fishPoolSprites.Length > 0)
        { int att = 0; while (fishSpr == null && att < 20) { fishSpr = fishPoolSprites[Random.Range(0, Mathf.Min(fishPoolSprites.Length, 20))]; att++; } }
        if (fishSpr != null) img.sprite = fishSpr; else if (fishNormalSprite != null) img.sprite = fishNormalSprite;
        float facing = Random.value > 0.5f ? 1f : -1f; rt.localScale = new Vector3(facing, 1f, 1f);
        poolFishStates.Add(new PoolFishState { rt = rt, center = center, phaseX = Random.Range(0f, Mathf.PI * 2f), phaseY = Random.Range(0f, Mathf.PI * 2f), freqX = Random.Range(0.35f, 0.85f), freqY = Random.Range(0.80f, 1.50f), ampX = Random.Range(38f, 80f), ampY = Random.Range(10f, 24f) });
        StartCoroutine(SplashIn(rt, facing));
    }
    IEnumerator SplashIn(RectTransform rt, float facing) { rt.localScale = new Vector3(facing * 1.5f, 1.5f, 1f); float t = 0f; while (t < 0.25f) { t += Time.deltaTime; if (rt == null) yield break; float s = Mathf.Lerp(1.5f, 1f, Mathf.Clamp01(t / 0.25f)); rt.localScale = new Vector3(facing * s, s, 1f); yield return null; } }
    void AnimatePoolFish()
    {
        float time = Time.time;
        for (int i = poolFishStates.Count - 1; i >= 0; i--)
        {
            var s = poolFishStates[i]; if (s.rt == null) { poolFishStates.RemoveAt(i); continue; }
            float x = s.center.x + Mathf.Sin(time * s.freqX + s.phaseX) * s.ampX;
            float y = s.center.y + Mathf.Sin(time * s.freqY + s.phaseY) * s.ampY;
            s.rt.anchoredPosition = new Vector2(x, y);
            float dx = Mathf.Cos(time * s.freqX + s.phaseX);
            if (Mathf.Abs(dx) > 0.05f) s.rt.localScale = new Vector3(dx > 0 ? 1f : -1f, 1f, 1f);
            poolFishStates[i] = s;
        }
    }
    IEnumerator NetBounce(RectTransform netRT) { if (netRT == null) yield break; Vector3 orig = netRT.localScale; float t = 0f; while (t < 0.25f) { t += Time.deltaTime; netRT.localScale = Vector3.one * (1f + 0.10f * Mathf.Sin(Mathf.Clamp01(t / 0.25f) * Mathf.PI)); yield return null; } netRT.localScale = orig; }
    IEnumerator PufferRocksBoat()
    {
        int crackIdx = Mathf.Clamp(cracks - 1, 0, (boatCrackSlots?.Length ?? 1) - 1);
        if (boatCrackSlots != null && crackIdx < boatCrackSlots.Length && boatCrackSlots[crackIdx] != null) { boatCrackSlots[crackIdx].gameObject.SetActive(true); StartCoroutine(PunchScale(boatCrackSlots[crackIdx], 0.3f, 1.6f)); }
        yield return StartCoroutine(RockBoat(0.55f, cracks));
        if (cracks >= maxCracks) StartCoroutine(SinkBoat());
    }
    IEnumerator RockBoat(float dur, int severity)
    {
        if (boatRoot == null) yield break;
        Vector2 origPos = boatRoot.anchoredPosition; float origRot = boatRoot.localEulerAngles.z; if (origRot > 180f) origRot -= 360f;
        float tiltMax = Mathf.Lerp(4f, 18f, (float)(severity - 1) / (maxCracks - 1)); float t = 0f;
        while (t < dur) { t += Time.deltaTime; float d = 1f - Mathf.Clamp01(t / dur); boatRoot.localEulerAngles = new Vector3(0, 0, origRot + Mathf.Sin(t * 22f) * tiltMax * d); boatRoot.anchoredPosition = origPos + new Vector2(Random.Range(-6f, 6f) * d, Random.Range(-3f, 3f) * d); yield return null; }
        boatRoot.localEulerAngles = new Vector3(0, 0, origRot + (cracks >= maxCracks ? -12f : Mathf.Lerp(0f, -8f, (float)cracks / maxCracks))); boatRoot.anchoredPosition = origPos;
    }
    IEnumerator SinkBoat()
    {
        if (boatSinking || boatRoot == null) yield break; boatSinking = true; yield return new WaitForSeconds(0.5f);
        Vector2 startPos = boatRoot.anchoredPosition, endPos = startPos + new Vector2(40f, -300f);
        float startRot = boatRoot.localEulerAngles.z; if (startRot > 180f) startRot -= 360f;
        float t = 0f, dur = 2.2f;
        while (t < dur) { t += Time.deltaTime; float ease = Mathf.Clamp01(t / dur); ease = ease * ease; boatRoot.anchoredPosition = Vector2.Lerp(startPos, endPos, ease); boatRoot.localEulerAngles = new Vector3(0, 0, Mathf.Lerp(startRot, startRot - 45f, ease)); yield return null; }
    }
    IEnumerator PunchScale(RectTransform rt, float dur, float peak) { float t = 0f; while (t < dur) { t += Time.deltaTime; rt.localScale = Vector3.one * (1f + (peak - 1f) * Mathf.Sin(Mathf.Clamp01(t / dur) * Mathf.PI)); yield return null; } rt.localScale = Vector3.one; }
    IEnumerator ShowScorePopup(int points, RectTransform nearRT)
    {
        if (scorePopupRT == null) yield break;
        Vector2 anchor = nearRT != null ? nearRT.anchoredPosition + new Vector2(0f, 50f) : new Vector2(0f, 300f);
        scorePopupRT.anchoredPosition = anchor; if (scorePopupText != null) scorePopupText.text = "+" + points; scorePopupCG.alpha = 1f;
        Vector2 start = anchor; float t = 0f;
        while (t < 0.8f) { t += Time.deltaTime; float p = Mathf.Clamp01(t / 0.8f); scorePopupRT.anchoredPosition = start + new Vector2(0f, 50f * p); scorePopupCG.alpha = 1f - p * p; yield return null; }
        scorePopupCG.alpha = 0f;
    }

    // ===================================================================
    //  HUD
    // ===================================================================
    void UpdateAllUI() { UpdateScoreUI(); UpdateStreakUI(); UpdateProgress(); UpdateHearts(); }
    void UpdateHearts() { int livesLeft = maxCracks - cracks; for (int i = 0; i < heartImages.Count; i++) { if (heartImages[i] == null) continue; heartImages[i].color = i < livesLeft ? Color.white : new Color(0.35f, 0.35f, 0.40f, 0.55f); } }
    void UpdateTimerUI()
    {
        int sec = Mathf.CeilToInt(timeRemaining); bool low = timeRemaining < 15f;
        if (timerText != null) { timerText.text = $"{sec / 60}:{sec % 60:D2}"; timerText.color = low ? new Color(0.91f, 0.30f, 0.24f) : Color.white; }
        if (timerFill != null) { timerFill.fillAmount = Mathf.Clamp01(timeRemaining / gameDurationSeconds); timerFill.color = low ? new Color(0.91f, 0.30f, 0.24f) : new Color(0.31f, 0.80f, 0.77f); }
    }
    void UpdateScoreUI() { if (scoreText != null) scoreText.text = $"Score: {score}"; }
    void UpdateStreakUI() { if (streakText != null) streakText.text = streak >= 3 ? $"{streak} in a row!" : ""; }
    void UpdateProgress() { if (progressText != null) progressText.text = $"{correctCount} / {targetCorrectToWin} correct"; }

    // ===================================================================
    //  Feedback
    // ===================================================================
    IEnumerator ShowFeedback(bool correct, string explanation)
    {
        feedbackPanel.SetActive(true);
        var cg = feedbackPanel.GetComponent<CanvasGroup>();
        if (cg != null) cg.alpha = 0f;

        Color mainColor = correct ? new Color(0.13f, 0.68f, 0.38f, 1f) : new Color(0.86f, 0.20f, 0.20f, 1f);
        feedbackBg.color = mainColor;
        feedbackTitle.text = correct ? "Correct!" : "Not quite…";
        feedbackBody.text = explanation;
        if (feedbackIcon != null) { feedbackIcon.text = correct ? "✓" : "✗"; feedbackIcon.color = mainColor; }

        // Punch-scale just the card while the backdrop fades in behind it
        var cardT = feedbackCardRT != null ? feedbackCardRT : feedbackPanel.transform;
        cardT.localScale = Vector3.one * 0.6f;
        float t = 0f; const float dur = 0.32f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            cardT.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, EaseOutBack(p));
            if (cg != null) cg.alpha = Mathf.Clamp01(p / 0.6f);
            yield return null;
        }
        cardT.localScale = Vector3.one;
        if (cg != null) cg.alpha = 1f;

        yield return new WaitForSeconds(2.0f);

        if (cg != null) { float fo = 0f; while (fo < 0.2f) { fo += Time.deltaTime; cg.alpha = 1f - fo / 0.2f; yield return null; } }
        feedbackPanel.SetActive(false);
        cardT.localScale = Vector3.one;
    }

    // Overshoots past 1 then settles — gives the feedback card a "punch" pop on appear.
    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float xm1 = x - 1f;
        return 1f + c3 * xm1 * xm1 * xm1 + c1 * xm1 * xm1;
    }

    // ===================================================================
    //  End game
    // ===================================================================
    void EndGame()
    {
        gameRunning = false;
        swipeCard.Lock();
        StopBgMusic();

        bool sank = cracks >= maxCracks;
        int correct = 0;
        for (int i = 0; i < correctAnswers.Length; i++) if (answered[i] && correctAnswers[i]) correct++;
        float pct = emails.Length > 0 ? (float)correct / emails.Length : 0f;
        bool isWin = wonEarly || (!sank && pct >= 0.7f);

        PlayerPrefs.SetString("interior_result", isWin ? "win" : "lose");
        PlayerPrefs.Save();

        ShowResult();
    }

    void ShowResult()
    {
        resultPanel.SetActive(true);
        int totalPossible = emails.Length * correctPoints;
        if (resultScore != null) resultScore.text = $"{score} / {totalPossible}";
        int correct = 0; for (int i = 0; i < correctAnswers.Length; i++) if (answered[i] && correctAnswers[i]) correct++;
        float pct = wonEarly ? 1f : (emails.Length > 0 ? (float)correct / emails.Length : 0);
        PlayerProgress.QueueFromPerformance(pct);
        if (cracks == 0) PlayerProgress.RegisterFish("fish_guardian");
        string stars, msg;
        if (wonEarly) { stars = "★ ★ ★"; msg = $"You sorted {targetCorrectToWin} correctly — mission complete!"; }
        else if (cracks >= maxCracks) { stars = "★"; msg = "The boat sank! Those pufferfish got you.\nRead carefully and try again."; }
        else if (timeRemaining <= 0 && pct < 0.7f) { stars = "★"; msg = "Time's up — you'll be quicker next time."; }
        else if (pct >= 0.95f) { stars = "★ ★ ★"; msg = "Phish-master! The nets are full of happy fish."; }
        else if (pct >= 0.7f) { stars = "★ ★"; msg = "Solid work! Review the ones that got you."; }
        else { stars = "★"; msg = "Scammers are tricky. Try again and read carefully."; }
        if (resultStars != null) resultStars.text = stars;
        if (resultMessage != null) resultMessage.text = msg;
    }

    public void OnPlayAgain() { SceneManager.LoadScene(SceneManager.GetActiveScene().name); }

    public void OnReturnToMap()
    {
        string src = PlayerPrefs.GetString("interior_source", "WorldMap");
        if (string.IsNullOrEmpty(src)) src = "WorldMap";
        SceneManager.LoadScene(src);
    }

    public static Image MakeImage(Transform parent, string name, Color color) { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); var img = go.AddComponent<Image>(); img.color = color; return img; }
    public static TMP_Text MakeText(Transform parent, string name, string content, int size, Color color, TextAlignmentOptions align, FontStyles style = FontStyles.Normal) { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); var t = go.AddComponent<TextMeshProUGUI>(); t.text = content; t.fontSize = size; t.color = color; t.alignment = align; t.fontStyle = style; t.raycastTarget = false; return t; }

    // ===================================================================
    //  Content
    // ===================================================================
    void InitializeEmails()
    {
        emails = new Email[]
        {
            new Email { senderName="PayPal Support",    senderEmail="support@paypa1.com",             subject="URGENT: Your account has been suspended", body="Dear Customer,\n\nYour PayPal account has been suspended due to suspicious activity. Click below immediately to verify your information or your account will be permanently deleted within 24 hours.\n\n— PayPal Support", timestamp="10:09 AM", avatarColor=new Color(0.07f,0.45f,0.71f), isScam=true,  layout=CardLayout.Email, explanation="SCAM — The sender domain is 'paypa1.com' (number 1, not L). Real PayPal emails come from paypal.com. The 24-hour deletion threat is a classic urgency trick." },
            new Email { senderName="Spotify",           senderEmail="newsletter@spotify.com",         subject="Your June playlist is ready",              body="Hi there,\n\nYour monthly Spotify stats are in. Check out your top songs from this month in the app.\n\n— The Spotify Team", timestamp="9:42 AM", avatarColor=new Color(0.12f,0.84f,0.38f), isScam=false, layout=CardLayout.Email, explanation="SAFE — Real domain (spotify.com), calm tone, no threats, no request for personal info." },
            new Email { senderName="Canada Revenue Agency", senderEmail="noreply@canada-revenue-agency-refund.com", subject="You have a $847 tax refund waiting", body="NOTICE FROM THE CRA:\n\nA refund of $847.00 is ready. To claim it, provide your SIN and banking details within 24 hours or the refund will be cancelled.\n\n— Canada Revenue Agency", timestamp="8:27 AM", avatarColor=new Color(0.78f,0.13f,0.13f), isScam=true, layout=CardLayout.Email, explanation="SCAM — The CRA never emails asking for your SIN. Real messages come from cra-arc.gc.ca only. The 24-hour deadline is another red flag." },
            new Email { senderName="Amazon",            senderEmail="orders@amazon.com",              subject="Your order has shipped",                   body="Hello,\n\nYour order #112-4857293 has shipped. Estimated delivery June 12. Track it in the Amazon app.\n\n— Amazon", timestamp="Yesterday", avatarColor=new Color(1f,0.6f,0f), isScam=false, layout=CardLayout.Email, explanation="SAFE — Real domain (amazon.com), specific order number, no request for personal info." },
            new Email { senderName="Microsoft Security",senderEmail="security@micros0ft-account.net", subject="Unusual sign-in detected",                 body="Dear User,\n\nWe detected a sign-in from an unrecognized location. Click below immediately to secure your account.\n\n— Microsoft Security Team", timestamp="Yesterday", avatarColor=new Color(0.05f,0.45f,0.79f), isScam=true, layout=CardLayout.Email, explanation="SCAM — Domain is 'micros0ft-account.net' (zero, not O). 'Dear User' is generic — Microsoft uses your name." },
            new Email { senderName="Uber",              senderEmail="no-reply@uber.com",              subject="Your Tuesday night trip receipt",           body="Thanks for riding with Uber.\n\nYour trip came to $14.72. Payment charged to Visa ending in 4821.\n\n— Uber", timestamp="May 4", avatarColor=new Color(0.1f,0.1f,0.1f), isScam=false, layout=CardLayout.Email, explanation="SAFE — Real domain, specific details, only last 4 card digits shown, no suspicious links." },
            new Email { senderName="Netflix Billing",   senderEmail="billing@netfl1x-payments.com",  subject="Payment failed — update card now",          body="Hello,\n\nYour Netflix payment could not be processed. Update your billing details within 48 hours or your account will be terminated.\n\n— Netflix Billing", timestamp="May 3", avatarColor=new Color(0.90f,0.05f,0.10f), isScam=true, layout=CardLayout.Email, explanation="SCAM — Domain is 'netfl1x-payments.com' (number 1 not L). Real billing comes from netflix.com." },
            new Email { senderName="Google Calendar",   senderEmail="calendar-noreply@google.com",   subject="Reminder: Coffee with Sarah at 2pm",        body="This is a reminder for your event:\n\nCoffee with Sarah\nToday at 2:00 PM\nThe Wired Monk Cafe\n\n— Google Calendar", timestamp="May 3", avatarColor=new Color(0.26f,0.52f,0.96f), isScam=false, layout=CardLayout.Email, explanation="SAFE — Real Google domain, specific event, no suspicious links or requests." },
        };
    }

    void InitializeWebsiteContent()
    {
        emails = new Email[]
        {
            new Email { senderName="Community Forums", senderEmail="community-forums-rewards.net/claim", subject="🎉 You've been selected — claim your $500 gift card!", body="Hi Member,\n\nCongratulations! You were randomly selected from our forum members to receive a $500 gift card.\n\nClick the link below to claim your prize — offer expires in 2 hours!\n\n► CLAIM NOW: community-forums-rewards.net/claim?id=4829\n\nDo not share this link. It is unique to you.", timestamp="11:02 AM", avatarColor=new Color(0.85f,0.55f,0.10f), isScam=true,  layout=CardLayout.Website, explanation="SCAM — The URL 'community-forums-rewards.net' is not a real forum. Legitimate prizes are never announced through random posts with 2-hour deadlines." },
            new Email { senderName="Reddit",           senderEmail="noreply@reddit.com",               subject="Someone replied to your comment in r/personalfinance", body="Hi u/JohnDoe,\n\nSomeone replied to your comment in r/personalfinance:\n\n\"Great point about the emergency fund — I completely agree!\"\n\nView the thread: reddit.com/r/personalfinance/comments/abc123\n\n— The Reddit Team", timestamp="10:15 AM", avatarColor=new Color(1.00f,0.27f,0.00f), isScam=false, layout=CardLayout.Website, explanation="SAFE — Real domain (reddit.com), refers to your username and a specific thread, no request for personal information or money." },
            new Email { senderName="Forum Admin",      senderEmail="admin@forumhub-security.tk/verify", subject="⚠️ Your account will be DELETED in 24 hours", body="NOTICE: Your forum account has been flagged for suspicious activity.\n\nTo prevent deletion, you must verify your identity immediately by providing:\n• Full name\n• Date of birth\n• Current password\n• Recovery email\n\nFailure to respond within 24 hours will result in permanent account deletion.\n\nVerify here: forumhub-security.tk/verify", timestamp="9:30 AM", avatarColor=new Color(0.78f,0.10f,0.10f), isScam=true, layout=CardLayout.Website, explanation="SCAM — The domain ends in '.tk', a free throwaway domain used by scammers. No legitimate forum ever asks for your password via a post or message." },
            new Email { senderName="Stack Overflow",   senderEmail="notifications@stackoverflow.com",  subject="Your question received 3 new answers",      body="Hi John,\n\nYour question 'How do I centre a div in CSS?' received 3 new answers.\n\nThe top-voted answer suggests using flexbox with justify-content: center.\n\nView answers: stackoverflow.com/questions/12345678\n\n— Stack Overflow", timestamp="Yesterday", avatarColor=new Color(0.96f,0.48f,0.00f), isScam=false, layout=CardLayout.Website, explanation="SAFE — Real Stack Overflow domain, refers to your specific question, links go to stackoverflow.com, no personal information requested." },
            new Email { senderName="TechHelp Community", senderEmail="support@techhelp-fix.com/remote", subject="Re: Your computer is infected — remote fix available", body="We noticed your device is showing signs of malware infection based on your recent forum posts.\n\nOur certified technicians can fix this remotely in 15 minutes.\n\nCall us now: 1-800-555-0199\nOr click: techhelp-fix.com/remote-access\n\nDo NOT ignore this — your banking data may already be at risk.", timestamp="2 days ago", avatarColor=new Color(0.25f,0.55f,0.85f), isScam=true, layout=CardLayout.Website, explanation="SCAM — No website can detect malware from your forum posts. Unsolicited remote access offers are almost always scams designed to steal data or charge for fake repairs." },
            new Email { senderName="GitHub",           senderEmail="notifications@github.com",         subject="Pull request merged: fix login timeout bug",  body="Hi johndoe,\n\nYour pull request #142 'fix login timeout bug' was merged into main by @teamlead.\n\nView the changes: github.com/myorg/myrepo/pull/142\n\n— GitHub", timestamp="3 days ago", avatarColor=new Color(0.10f,0.10f,0.10f), isScam=false, layout=CardLayout.Website, explanation="SAFE — Real GitHub domain, refers to a specific PR number and repository, links to github.com, no personal information or payment requested." },
            new Email { senderName="Survey Rewards Hub", senderEmail="rewards@survey-hub-canada.net/survey", subject="Complete a 2-minute survey — earn $75 instantly", body="Hello valued member,\n\nYou have been selected to complete a short 2-minute survey about your online shopping habits.\n\nAs a thank-you, you will receive $75 deposited directly to your PayPal account upon completion.\n\nStart survey: survey-hub-canada.net/survey?ref=98234\n\nThis offer is only available for the next 30 minutes.", timestamp="4 days ago", avatarColor=new Color(0.20f,0.70f,0.30f), isScam=true, layout=CardLayout.Website, explanation="SCAM — No company pays $75 for a 2-minute survey. The 30-minute deadline is fake urgency. These sites collect your PayPal login to steal your account." },
            new Email { senderName="Discord",          senderEmail="noreply@discord.com",              subject="You have 4 unread messages in Gaming Pals",  body="Hi John,\n\nYou have 4 unread messages in the Gaming Pals server.\n\nOpen Discord to catch up: discord.com/channels/123456789\n\n— The Discord Team", timestamp="5 days ago", avatarColor=new Color(0.35f,0.40f,0.86f), isScam=false, layout=CardLayout.Website, explanation="SAFE — Real Discord domain, refers to a specific server you're in, links go to discord.com, no personal data or payment requested." },
        };
    }

    void InitializeSMSContent()
    {
        emails = new Email[]
        {
            new Email { senderName="+1-888-555-0147", senderEmail="Unknown number", subject="URGENT: Suspicious activity on your account", body="THEM: URGENT: Suspicious activity detected on your RBC account. Your card has been TEMPORARILY LOCKED.\nTHEM: To restore access click here immediately: rbc-secure-verify.net/unlock\nYOU: Oh no, is this real?\nTHEM: Yes this is RBC Security. You must verify within 15 mins or card stays locked.\nYOU: Okay clicking the link now...", timestamp="10:03 AM", avatarColor=new Color(0.78f,0.10f,0.10f), isScam=true, layout=CardLayout.SMS, explanation="SCAM — Real banks never send links via text to unlock your card. 'rbc-secure-verify.net' is not RBC's domain. The fake 15-minute deadline creates panic so you don't think before clicking." },
            new Email { senderName="Canada Post", senderEmail="Short code 272727", subject="Your parcel is out for delivery", body="THEM: Canada Post: Your parcel (tracking #1234567890) is out for delivery today. Expected by 5 PM. Track: canadapost.ca/track\nYOU: Great, thanks!\nTHEM: No reply needed. Reply STOP to opt out.", timestamp="9:15 AM", avatarColor=new Color(0.88f,0.08f,0.18f), isScam=false, layout=CardLayout.SMS, explanation="SAFE — Canada Post texts from a registered short code, not a random number. The link goes to canadapost.ca (the real domain), and there's no request for personal info or payment." },
            new Email { senderName="CRA Tax Dept", senderEmail="+1-647-555-0193", subject="Tax refund of $648 ready to deposit", body="THEM: CRA: You have an unclaimed tax refund of $648.00. To receive your deposit pls confirm bank details here: cra-etransfer-canada.com\nYOU: How do I confirm?\nTHEM: Just enter your banking info on the site and we process within 24hrs. Act fast refunds expire!\nYOU: Seems off... the URL doesn't look right\nTHEM: This is official CRA system. All URLs are secure. Please proceed.", timestamp="Yesterday", avatarColor=new Color(0.20f,0.40f,0.70f), isScam=true, layout=CardLayout.SMS, explanation="SCAM — The CRA never contacts you by text to offer refunds. 'cra-etransfer-canada.com' is not the CRA's domain (canada.ca). The scammer also pressures you when you hesitate — a major red flag." },
            new Email { senderName="Google", senderEmail="Short code 22000", subject="Your Google verification code", body="THEM: G-748392 is your Google verification code. Do not share this code with anyone.\nYOU: (entering code to log in)\nTHEM: This code expires in 10 minutes.", timestamp="Tuesday", avatarColor=new Color(0.26f,0.52f,0.96f), isScam=false, layout=CardLayout.SMS, explanation="SAFE — This is a legitimate 2FA code from Google. It was sent because you requested it while logging in. Crucially it says 'Do not share this code' — a real provider never asks you to read it back to them." },
            new Email { senderName="+1-905-555-0182", senderEmail="Unknown number", subject="You've won a $1,000 Walmart gift card!", body="THEM: Congrats! Ur number was selected as our weekly WINNER for a $1000 Walmart giftcard!! Claim b4 it expires: walmart-winner-ca.net/claim\nYOU: I don't remember entering a contest\nTHEM: U were auto-entered when u shopped at Walmart last month. Hurry link expires in 1 hour!!\nYOU: What information do I need to provide?\nTHEM: Just ur name address and credit card for $1.99 shipping fee to send the card", timestamp="Monday", avatarColor=new Color(0.00f,0.45f,0.20f), isScam=true, layout=CardLayout.SMS, explanation="SCAM — Multiple red flags: unknown number, poor spelling ('ur', 'b4'), fake domain, you 'don't remember entering', and a '$1.99 shipping fee' is how scammers steal your credit card number." },
            new Email { senderName="Sunnybrook Clinic", senderEmail="Short code 89898", subject="Appointment reminder for tomorrow", body="THEM: Sunnybrook Clinic: Reminder — you have an appointment with Dr. Patel tomorrow Aug 18 at 2:30 PM. Reply YES to confirm or call 416-555-0100 to reschedule.\nYOU: YES\nTHEM: Confirmed! See you tomorrow. Please arrive 10 mins early.", timestamp="Sunday", avatarColor=new Color(0.10f,0.60f,0.80f), isScam=false, layout=CardLayout.SMS, explanation="SAFE — A legitimate appointment reminder from a registered short code. It includes specific details (doctor name, date, time), provides a real phone number, and only asks you to reply YES or call — no links, no personal data." },
            new Email { senderName="+1-416-555-0174", senderEmail="Unknown number", subject="Your Netflix acount has been suspended", body="THEM: Netlfix: Your acount has been supended due to a billing issue. Update your payment informaton here or loose access: netflix-billing-update.net\nYOU: This looks weird, there are typos\nTHEM: Sorry for the typos our system is updating. Please still verify ur account is important.\nYOU: Netflix wouldn't text me from a random number would they?\nTHEM: We use multiple numbers for security reasons. Please click link now.", timestamp="Last week", avatarColor=new Color(0.90f,0.05f,0.10f), isScam=true, layout=CardLayout.SMS, explanation="SCAM — 'Netlfix', 'acount', 'supended', 'informaton', 'loose' — multiple spelling mistakes are a key smishing red flag. Real companies proofread their messages. The domain 'netflix-billing-update.net' is also fake." },
            new Email { senderName="TD Bank", senderEmail="Short code 39733", subject="Transaction alert on your account", body="THEM: TD: A purchase of $47.82 at Tim Hortons was made on your TD Visa ending 4821 on Aug 17. Not you? Call 1-800-983-8472 or visit td.com/security\nYOU: That was me, all good!\nTHEM: Great. No further action needed.", timestamp="Today", avatarColor=new Color(0.00f,0.35f,0.65f), isScam=false, layout=CardLayout.SMS, explanation="SAFE — A legitimate bank transaction alert from a registered short code. It shows the real purchase amount, merchant, and last 4 digits of your card. It gives you a real phone number and domain (td.com) — no link to click." },
        };
    }

    void InitializeSocialContent()
    {
        emails = new Email[]
        {
            new Email { senderName="@instagram.support.team", senderEmail="via Instagram DM", subject="⚠️ Your account is scheduled for deletion", body="Your Instagram account has been reported for violating our community guidelines.\n\nYour account will be permanently deleted within 24 hours unless you verify your identity through our official appeal form.\n\n► Appeal here: instagram-appeals-center.com/verify\n\nThis is your only chance to save your account.\n\n— Instagram Trust & Safety", timestamp="2 hours ago", avatarColor=new Color(0.75f,0.25f,0.75f), isScam=true, layout=CardLayout.Social, explanation="SCAM — Instagram never sends deletion warnings via DM. '@instagram.support.team' is a fake account — real Instagram support handles are @instagram or @creators. The domain 'instagram-appeals-center.com' is not owned by Meta." },
            new Email { senderName="YouTube", senderEmail="via youtube.com", subject="MrBeast uploaded a new video", body="MrBeast just posted:\n\n\"I Spent 7 Days In A Cave!\"\n\nWatch now on YouTube: youtube.com/watch?v=abc123\n\n— The YouTube Team\n\nManage your notification settings in YouTube Studio.", timestamp="3 hours ago", avatarColor=new Color(1.00f,0.00f,0.00f), isScam=false, layout=CardLayout.Social, explanation="SAFE — A real YouTube channel subscription notification. It links to youtube.com, references a real creator you subscribed to, and includes a way to manage settings. No personal info or payment requested." },
            new Email { senderName="Facebook Rewards Program", senderEmail="@FacebookRewards2024", subject="🎁 You've been chosen for our loyalty reward!", body="Congratulations! Your Facebook account was selected as one of 500 loyalty reward winners this month.\n\nYour prize: $750 gift card\n\nTo claim, you must:\n1. Like and share this post\n2. Send us a DM with your full name and phone number\n3. Pay a $4.99 processing fee\n\nOffer expires in 48 hours. Only 12 prizes remaining!", timestamp="5 hours ago", avatarColor=new Color(0.23f,0.35f,0.60f), isScam=true, layout=CardLayout.Social, explanation="SCAM — Facebook has no official 'Rewards Program'. Requiring a 'processing fee' for a prize is always a scam. Asking for your phone number via DM and using countdown pressure ('12 prizes remaining') are classic manipulation tactics." },
            new Email { senderName="X (Twitter)", senderEmail="via twitter.com", subject="New login to your X account", body="We noticed a new login to your X account from:\n\nDevice: Chrome on Windows\nLocation: Toronto, ON\nTime: Aug 17 at 9:41 AM\n\nIf this was you, no action is needed.\n\nIf this wasn't you, secure your account at: twitter.com/settings/security\n\n— X Security Team", timestamp="9:41 AM", avatarColor=new Color(0.10f,0.10f,0.10f), isScam=false, layout=CardLayout.Social, explanation="SAFE — A real security login notification from X. It gives specific details (device, location, time), doesn't ask you to click a link in the message, and directs you to twitter.com/settings — the real site." },
            new Email { senderName="@Elon.Musk.Official2", senderEmail="via Twitter/X DM", subject="🚀 CRYPTO GIVEAWAY — Send 0.1 BTC get 1 BTC back!", body="For a limited time I'm giving back to my followers!\n\nSend any amount of Bitcoin to the address below and I'll send back DOUBLE within 30 minutes:\n\nBTC: 1A2B3C4D5EFake6Address789\n\nI'm doing this to celebrate Tesla's record quarter. Already sent $2.4M to 1,200 people today!\n\nMinimum: 0.05 BTC   Maximum: 2 BTC\n\nDon't miss this — closing in 2 hours!", timestamp="Yesterday", avatarColor=new Color(0.10f,0.10f,0.10f), isScam=true, layout=CardLayout.Social, explanation="SCAM — Crypto doubling scams impersonating celebrities are extremely common on social media. Nobody ever doubles your cryptocurrency. The '2 hours' deadline, the inflated claims ('sent $2.4M today'), and an unverified account are all red flags." },
            new Email { senderName="LinkedIn", senderEmail="via linkedin.com", subject="Sarah Chen accepted your connection request", body="Great news — Sarah Chen accepted your connection request!\n\nSarah Chen\nProduct Manager at Shopify | Toronto\n\nYou now have 312 connections.\n\nView Sarah's profile: linkedin.com/in/sarah-chen-pm\n\n— LinkedIn", timestamp="Tuesday", avatarColor=new Color(0.00f,0.46f,0.71f), isScam=false, layout=CardLayout.Social, explanation="SAFE — A real LinkedIn connection notification. It references a specific person and action you initiated, links to linkedin.com, and doesn't ask for any information or payment." },
            new Email { senderName="@Meta.Verified.Support", senderEmail="via Facebook DM", subject="Get your blue verification badge today!", body="Hi! We noticed your page has strong engagement and you may qualify for a Meta Verified blue badge ✓\n\nVerification gives you:\n• Blue checkmark badge\n• Priority support\n• More reach and visibility\n\nTo apply, DM us:\n1. Your full legal name\n2. Date of birth\n3. A photo of your government ID\n4. Your Facebook login email and password\n\nFee: $49.99 paid via gift card\n\nOffer valid for 72 hours only.", timestamp="Monday", avatarColor=new Color(0.23f,0.35f,0.60f), isScam=true, layout=CardLayout.Social, explanation="SCAM — Meta never asks for your password or government ID via DM. Requiring gift cards as payment is a universal scam signal. Real Meta Verified is applied for through the app settings, not through unsolicited DMs." },
            new Email { senderName="Instagram", senderEmail="via instagram.com", subject="@janedoe and 47 others liked your photo", body="@janedoe, @mike.photos, and 47 others liked your photo.\n\n\"Sunset at Kensington Market 🌅\"\n\nView your post: instagram.com/p/abc123def456\n\n— Instagram\n\nYou're receiving this because you have post notifications turned on.", timestamp="Sunday", avatarColor=new Color(0.75f,0.25f,0.75f), isScam=false, layout=CardLayout.Social, explanation="SAFE — A real Instagram engagement notification. It names specific users, references your actual post caption, links to instagram.com, and explains why you're receiving it. No personal info or action required." },
        };
    }

    void InitializeCombinedContent()
    {
        var pool = new List<Email>();
        pool.Add(new Email { senderName = "PayPal Support", senderEmail = "support@paypa1.com", subject = "URGENT: Account suspended", body = "Dear Customer,\n\nYour PayPal account has been suspended. Verify immediately or your account will be permanently deleted within 24 hours.\n\n— PayPal Support", timestamp = "Today", avatarColor = new Color(0.07f, 0.45f, 0.71f), isScam = true, layout = CardLayout.Email, explanation = "SCAM — 'paypa1.com' uses the number 1 instead of L. Real PayPal emails come from paypal.com. 24-hour threats are classic phishing pressure." });
        pool.Add(new Email { senderName = "Amazon", senderEmail = "orders@amazon.com", subject = "Your order has shipped", body = "Hello,\n\nYour order #112-4857293 has shipped. Estimated delivery June 12.\n\n— Amazon", timestamp = "Today", avatarColor = new Color(1f, 0.6f, 0f), isScam = false, layout = CardLayout.Email, explanation = "SAFE — Real amazon.com domain, specific order number, calm tone, no personal info requested." });
        pool.Add(new Email { senderName = "Survey Rewards Hub", senderEmail = "rewards@survey-hub-canada.net", subject = "Earn $75 in 2 minutes!", body = "You've been selected for a short survey. Receive $75 in your PayPal account on completion.\n\nStart: survey-hub-canada.net/survey\n\nOffer expires in 30 minutes.", timestamp = "Today", avatarColor = new Color(0.20f, 0.70f, 0.30f), isScam = true, layout = CardLayout.Website, explanation = "SCAM — No company pays $75 for a 2-minute survey. The 30-minute deadline is fake urgency. These sites steal your PayPal login." });
        pool.Add(new Email { senderName = "GitHub", senderEmail = "notifications@github.com", subject = "PR merged: fix login bug", body = "Hi johndoe,\n\nYour pull request #142 was merged into main by @teamlead.\n\nView: github.com/myorg/myrepo/pull/142\n\n— GitHub", timestamp = "Today", avatarColor = new Color(0.10f, 0.10f, 0.10f), isScam = false, layout = CardLayout.Website, explanation = "SAFE — Real GitHub domain, specific PR number, links to github.com, no payment or personal info requested." });
        pool.Add(new Email { senderName = "+1-888-555-0147", senderEmail = "Unknown number", subject = "RBC account locked", body = "THEM: URGENT: Suspicious activity on your RBC account. Card LOCKED. Restore here: rbc-secure-verify.net/unlock\nYOU: Is this real?\nTHEM: Yes. RBC Security. Verify within 15 mins.", timestamp = "Today", avatarColor = new Color(0.78f, 0.10f, 0.10f), isScam = true, layout = CardLayout.SMS, explanation = "SCAM — Real banks never send links to unlock cards via text. 'rbc-secure-verify.net' is not RBC's domain. The 15-minute fake deadline creates panic." });
        pool.Add(new Email { senderName = "TD Bank", senderEmail = "Short code 39733", subject = "Transaction alert", body = "THEM: TD: Purchase of $47.82 at Tim Hortons on your Visa ending 4821. Not you? Call 1-800-983-8472 or visit td.com/security\nYOU: That was me, all good!\nTHEM: Great, no action needed.", timestamp = "Today", avatarColor = new Color(0.00f, 0.35f, 0.65f), isScam = false, layout = CardLayout.SMS, explanation = "SAFE — Legitimate bank alert from a registered short code. Shows real purchase details, last 4 digits, and directs to td.com — no link to click." });
        pool.Add(new Email { senderName = "@Meta.Verified.Support", senderEmail = "via Facebook DM", subject = "Get your blue badge today!", body = "You may qualify for a Meta Verified blue badge ✓\n\nTo apply DM us:\n• Full legal name\n• Date of birth  \n• Government ID photo\n• Facebook password\n\nFee: $49.99 via gift card. Offer valid 72 hours.", timestamp = "Today", avatarColor = new Color(0.23f, 0.35f, 0.60f), isScam = true, layout = CardLayout.Social, explanation = "SCAM — Meta never asks for your password or ID via DM. Gift card payment is a universal scam signal. Real Meta Verified is applied for in the app settings." });
        pool.Add(new Email { senderName = "LinkedIn", senderEmail = "via linkedin.com", subject = "Sarah Chen accepted your request", body = "Sarah Chen accepted your connection request!\n\nSarah Chen — Product Manager at Shopify\n\nView her profile: linkedin.com/in/sarah-chen-pm\n\n— LinkedIn", timestamp = "Today", avatarColor = new Color(0.00f, 0.46f, 0.71f), isScam = false, layout = CardLayout.Social, explanation = "SAFE — Real LinkedIn notification for an action you initiated. Links to linkedin.com, names a specific person, no personal info or payment requested." });
        for (int i = pool.Count - 1; i > 0; i--)
        { int j = Random.Range(0, i + 1); var tmp = pool[i]; pool[i] = pool[j]; pool[j] = tmp; }
        emails = pool.ToArray();
    }
}