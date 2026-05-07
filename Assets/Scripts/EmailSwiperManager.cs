using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Phish Patrol — Gmail-styled email sorting minigame.
///
/// Player works through an inbox of mixed scam/legit emails under time
/// and life pressure. Click an email row to open the full message, then
/// decide SCAM or SAFE.
///
/// Wiring is done by EmailSwiperBuilder (Editor) when the scene is built.
/// Email content is defined in InitializeEmails() at the bottom of this
/// file — edit there to tune the round.
/// </summary>
public class EmailSwiperManager : MonoBehaviour
{
    // ===== Game tuning =====
    [Header("Game Settings")]
    public int startingLives = 5;
    public float gameDurationSeconds = 90f;
    public int correctPoints = 100;
    public int streakBonus = 25;

    // ===== Sprites (assigned by builder at edit time) =====
    [Header("Sprites")]
    public Sprite heartSprite;     // circle sprite used for life icons

    // ===== Panel refs =====
    [Header("Panels")]
    public GameObject tutorialPanel;
    public GameObject gmailRoot;
    public GameObject hudPanel;
    public GameObject detailPanel;
    public GameObject feedbackPanel;
    public GameObject resultPanel;

    // ===== HUD =====
    [Header("HUD")]
    public Transform heartsContainer;
    public TMP_Text timerText;
    public Image timerFill;
    public TMP_Text scoreText;
    public TMP_Text streakText;

    // ===== Inbox =====
    [Header("Inbox")]
    public Transform inboxContent;
    public TMP_Text inboxCountLabel;

    // ===== Detail view =====
    [Header("Detail View")]
    public Image detailAvatarBg;
    public TMP_Text detailAvatarLetter;
    public TMP_Text detailSenderName;
    public TMP_Text detailSenderEmail;
    public TMP_Text detailTimestamp;
    public TMP_Text detailSubject;
    public TMP_Text detailBody;

    // ===== Feedback overlay =====
    [Header("Feedback")]
    public Image feedbackBg;
    public TMP_Text feedbackTitle;
    public TMP_Text feedbackBody;

    // ===== Result panel =====
    [Header("Result")]
    public TMP_Text resultStars;
    public TMP_Text resultScore;
    public TMP_Text resultMessage;

    // ===== Commentator =====
    [Header("Commentator")]
    public Commentator commentator;

    // ===== Internal state =====
    private int score;
    private int streak;
    private int lives;
    private float timeRemaining;
    private bool gameRunning;
    private bool warnedLowTime;
    private int currentEmailIndex = -1;
    private bool[] answered;
    private bool[] correctAnswers;
    private GameObject[] rowObjects;
    private List<Image> heartImages = new List<Image>();

    private static readonly Color HeartFilled = new Color(0.91f, 0.30f, 0.24f);
    private static readonly Color HeartEmpty = new Color(0.85f, 0.85f, 0.85f);

    [System.Serializable]
    public struct Email
    {
        public string senderName;
        public string senderEmail;
        public string subject;
        public string preview;
        public string body;
        public string timestamp;
        public Color avatarColor;
        public bool isScam;
        public string explanation;
    }

    private Email[] emails;

    // ===================================================================
    // Lifecycle
    // ===================================================================

    void Start()
    {
        InitializeEmails();
        score = 0;
        streak = 0;
        lives = startingLives;
        timeRemaining = gameDurationSeconds;
        answered = new bool[emails.Length];
        correctAnswers = new bool[emails.Length];
        rowObjects = new GameObject[emails.Length];

        tutorialPanel.SetActive(true);
        gmailRoot.SetActive(false);
        hudPanel.SetActive(false);
        detailPanel.SetActive(false);
        feedbackPanel.SetActive(false);
        resultPanel.SetActive(false);
    }

    void Update()
    {
        if (!gameRunning) return;
        timeRemaining -= Time.deltaTime;
        UpdateTimerUI();

        if (!warnedLowTime && timeRemaining < 15f && timeRemaining > 0f)
        {
            warnedLowTime = true;
            if (commentator != null)
                commentator.Say("Quick now, time's running out!");
        }

        if (timeRemaining <= 0f)
        {
            timeRemaining = 0f;
            EndGame();
        }
    }

    // ===================================================================
    // Tutorial ? Game start
    // ===================================================================

    public void OnTutorialStart()
    {
        tutorialPanel.SetActive(false);
        gmailRoot.SetActive(true);
        hudPanel.SetActive(true);
        SpawnHearts();
        UpdateScoreUI();
        UpdateStreakUI();
        UpdateHearts();
        StartCoroutine(SpawnEmailRows());
        gameRunning = true;

        if (commentator != null)
        {
            commentator.Say("Be careful, dear — some of these look very real.");
        }
    }

    // ===================================================================
    // HUD setup / updates
    // ===================================================================

    void SpawnHearts()
    {
        foreach (Transform t in heartsContainer)
        {
            Destroy(t.gameObject);
        }
        heartImages.Clear();

        for (int i = 0; i < startingLives; i++)
        {
            var go = new GameObject("Life" + i, typeof(RectTransform));
            go.transform.SetParent(heartsContainer, false);

            var img = go.AddComponent<Image>();
            if (heartSprite != null) img.sprite = heartSprite;
            img.color = HeartFilled;
            img.raycastTarget = false;
            img.preserveAspect = true;

            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 48;
            le.preferredHeight = 48;

            heartImages.Add(img);
        }
    }

    void UpdateHearts()
    {
        for (int i = 0; i < heartImages.Count; i++)
        {
            heartImages[i].color = (i < lives) ? HeartFilled : HeartEmpty;
        }
    }

    void UpdateTimerUI()
    {
        int totalSec = Mathf.CeilToInt(timeRemaining);
        int min = totalSec / 60;
        int sec = totalSec % 60;
        timerText.text = $"{min}:{sec:D2}";
        if (timerFill != null)
        {
            timerFill.fillAmount = Mathf.Clamp01(timeRemaining / gameDurationSeconds);
        }

        bool low = timeRemaining < 15f;
        timerText.color = low ? new Color(0.91f, 0.30f, 0.24f) : Color.white;
        if (timerFill != null)
        {
            timerFill.color = low
                ? new Color(0.91f, 0.30f, 0.24f)
                : new Color(0.18f, 0.74f, 0.41f);
        }
    }

    void UpdateScoreUI()
    {
        scoreText.text = $"Score: {score}";
    }

    void UpdateStreakUI()
    {
        if (streak >= 3) streakText.text = $"{streak} in a row!";
        else streakText.text = "";
    }

    void UpdateInboxCount()
    {
        int remaining = 0;
        foreach (bool a in answered) if (!a) remaining++;
        if (inboxCountLabel != null)
        {
            inboxCountLabel.text = remaining + " unread";
        }
    }

    // ===================================================================
    // Inbox row creation
    // ===================================================================

    IEnumerator SpawnEmailRows()
    {
        foreach (Transform t in inboxContent)
        {
            Destroy(t.gameObject);
        }

        for (int i = 0; i < emails.Length; i++)
        {
            int captured = i;
            var row = CreateEmailRow(emails[i], captured);
            rowObjects[i] = row;

            var cg = row.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            row.transform.localScale = new Vector3(0.96f, 0.96f, 1f);
            StartCoroutine(FadeAndScaleIn(row.transform, cg, 0.25f));

            yield return new WaitForSeconds(0.07f);
        }
        UpdateInboxCount();
    }

    IEnumerator FadeAndScaleIn(Transform t, CanvasGroup cg, float dur)
    {
        float e = 0f;
        Vector3 startScale = t.localScale;
        while (e < dur)
        {
            e += Time.deltaTime;
            float p = Mathf.Clamp01(e / dur);
            float ease = 1f - Mathf.Pow(1f - p, 3f);
            cg.alpha = ease;
            t.localScale = Vector3.Lerp(startScale, Vector3.one, ease);
            yield return null;
        }
        cg.alpha = 1f;
        t.localScale = Vector3.one;
    }

    GameObject CreateEmailRow(Email email, int index)
    {
        var row = new GameObject("EmailRow_" + index, typeof(RectTransform));
        row.transform.SetParent(inboxContent, false);

        var le = row.AddComponent<LayoutElement>();
        le.preferredHeight = 64;
        le.flexibleWidth = 1;

        var bg = row.AddComponent<Image>();
        bg.color = Color.white;
        bg.raycastTarget = true;

        var btn = row.AddComponent<Button>();
        btn.targetGraphic = bg;
        var cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.96f, 0.97f, 0.98f);
        cb.pressedColor = new Color(0.91f, 0.93f, 0.96f);
        cb.disabledColor = new Color(0.94f, 0.94f, 0.96f);
        btn.colors = cb;
        btn.onClick.AddListener(() => OnRowClicked(index));

        // Bottom border line
        var border = AddImage(row.transform, "Border", new Color(0.88f, 0.88f, 0.90f));
        var brt = border.rectTransform;
        brt.anchorMin = new Vector2(0, 0);
        brt.anchorMax = new Vector2(1, 0);
        brt.pivot = new Vector2(0.5f, 0);
        brt.sizeDelta = new Vector2(0, 1);
        brt.anchoredPosition = Vector2.zero;
        border.raycastTarget = false;

        // Sender (bold)
        var sender = AddText(row.transform, "Sender", email.senderName, 22,
            new Color(0.13f, 0.13f, 0.13f), TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        var sdrt = sender.rectTransform;
        sdrt.anchorMin = new Vector2(0, 0); sdrt.anchorMax = new Vector2(0, 1);
        sdrt.pivot = new Vector2(0, 0.5f);
        sdrt.sizeDelta = new Vector2(220, 0);
        sdrt.anchoredPosition = new Vector2(30, 0);

        // Subject + preview combined
        string subjPreview = $"<b>{email.subject}</b>  <color=#5F6368>- {email.preview}</color>";
        var subj = AddText(row.transform, "SubjectPreview", subjPreview, 20,
            Color.black, TextAlignmentOptions.MidlineLeft);
        var subrt = subj.rectTransform;
        subrt.anchorMin = new Vector2(0, 0); subrt.anchorMax = new Vector2(1, 1);
        subrt.pivot = new Vector2(0, 0.5f);
        subrt.offsetMin = new Vector2(260, 0);
        subrt.offsetMax = new Vector2(-110, 0);
        subj.textWrappingMode = TextWrappingModes.NoWrap;
        subj.overflowMode = TextOverflowModes.Ellipsis;

        // Timestamp
        var ts = AddText(row.transform, "Timestamp", email.timestamp, 18,
            new Color(0.4f, 0.4f, 0.4f), TextAlignmentOptions.MidlineRight);
        var trt = ts.rectTransform;
        trt.anchorMin = new Vector2(1, 0); trt.anchorMax = new Vector2(1, 1);
        trt.pivot = new Vector2(1, 0.5f);
        trt.sizeDelta = new Vector2(90, 0);
        trt.anchoredPosition = new Vector2(-20, 0);

        return row;
    }

    // ===================================================================
    // Detail view
    // ===================================================================

    void OnRowClicked(int index)
    {
        if (answered[index]) return;
        currentEmailIndex = index;
        OpenDetail(emails[index]);
    }

    void OpenDetail(Email email)
    {
        detailPanel.SetActive(true);

        detailAvatarBg.color = email.avatarColor;
        detailAvatarLetter.text = string.IsNullOrEmpty(email.senderName)
            ? "?" : email.senderName.Substring(0, 1).ToUpper();
        detailSenderName.text = email.senderName;
        detailSenderEmail.text = "<" + email.senderEmail + ">";
        detailTimestamp.text = email.timestamp;
        detailSubject.text = email.subject;
        detailBody.text = email.body;

        StartCoroutine(BounceInPanel(detailPanel.transform));
    }

    IEnumerator BounceInPanel(Transform t)
    {
        Vector3 startScale = Vector3.one * 0.92f;
        Vector3 endScale = Vector3.one;
        t.localScale = startScale;
        float e = 0f, dur = 0.22f;
        while (e < dur)
        {
            e += Time.deltaTime;
            float p = 1f - Mathf.Pow(1f - Mathf.Clamp01(e / dur), 3f);
            t.localScale = Vector3.Lerp(startScale, endScale, p);
            yield return null;
        }
        t.localScale = endScale;
    }

    public void OnBackPressed()
    {
        detailPanel.SetActive(false);
    }

    public void OnScamPressed() { Decide(true); }
    public void OnSafePressed() { Decide(false); }

    void Decide(bool playerSaidScam)
    {
        if (currentEmailIndex < 0) return;
        if (answered[currentEmailIndex]) return;

        var email = emails[currentEmailIndex];
        bool correct = (playerSaidScam == email.isScam);
        answered[currentEmailIndex] = true;
        correctAnswers[currentEmailIndex] = correct;

        if (correct)
        {
            int gain = correctPoints + Mathf.Max(0, streak) * streakBonus;
            score += gain;
            streak++;
            ReactToCorrect();
        }
        else
        {
            streak = 0;
            lives = Mathf.Max(0, lives - 1);
            UpdateHearts();
            ReactToWrong();
        }

        UpdateScoreUI();
        UpdateStreakUI();
        StartCoroutine(ShowFeedback(correct, email.explanation));
    }

    void ReactToCorrect()
    {
        if (commentator == null) return;
        if (streak == 3)
            commentator.Say("Oh my, you're on a roll!");
        else if (streak == 6)
            commentator.Say("Goodness, what a sharp eye!");
        else
            commentator.SayRandom(new[] {
                "Good catch!",
                "Very nice, dear.",
                "You're so smart.",
                "That's the way!"
            });
    }

    void ReactToWrong()
    {
        if (commentator == null) return;
        if (lives == 0) return; // game-over reaction handles it
        if (lives == 1)
            commentator.Say("One life left! Take your time, dear.");
        else if (lives == 2)
            commentator.Say("Be careful now — two lives left.");
        else
            commentator.SayRandom(new[] {
                "Oh no, that one tricked me too...",
                "Don't worry, scammers are clever.",
                "It happens to the best of us, dear."
            });
    }

    // ===================================================================
    // Feedback overlay
    // ===================================================================

    IEnumerator ShowFeedback(bool correct, string explanation)
    {
        detailPanel.SetActive(false);
        feedbackPanel.SetActive(true);

        feedbackBg.color = correct
            ? new Color(0.18f, 0.74f, 0.41f, 0.97f)
            : new Color(0.91f, 0.30f, 0.24f, 0.97f);
        feedbackTitle.text = correct ? "Correct!" : "Not quite.";
        feedbackBody.text = explanation;

        feedbackPanel.transform.localScale = Vector3.one * 0.85f;
        float e = 0f, dur = 0.22f;
        while (e < dur)
        {
            e += Time.deltaTime;
            float p = 1f - Mathf.Pow(1f - Mathf.Clamp01(e / dur), 3f);
            feedbackPanel.transform.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, p);
            yield return null;
        }

        yield return new WaitForSeconds(2.6f);

        feedbackPanel.SetActive(false);
        feedbackPanel.transform.localScale = Vector3.one;

        // Grey out the answered row
        if (rowObjects[currentEmailIndex] != null)
        {
            var img = rowObjects[currentEmailIndex].GetComponent<Image>();
            if (img != null) img.color = new Color(0.94f, 0.94f, 0.96f);
            var btn = rowObjects[currentEmailIndex].GetComponent<Button>();
            if (btn != null) btn.interactable = false;
        }
        UpdateInboxCount();

        // Game-over check
        bool allDone = true;
        foreach (bool a in answered) if (!a) { allDone = false; break; }
        if (lives <= 0 || allDone) EndGame();
    }

    // ===================================================================
    // End game
    // ===================================================================

    void EndGame()
    {
        gameRunning = false;
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

        if (lives <= 0)
        {
            resultStars.text = "*";
            resultMessage.text = "Out of lives! The scammers got you. Read carefully and try again.";
            if (commentator != null) commentator.Say("Oh dear... those scammers are cleverer than I thought.");
        }
        else if (timeRemaining <= 0 && pct < 0.7f)
        {
            resultStars.text = "*";
            resultMessage.text = "Time's up - you'll be quicker next time.";
            if (commentator != null) commentator.Say("Time got away from us, dear.");
        }
        else if (pct >= 0.95f)
        {
            resultStars.text = "* * *";
            resultMessage.text = "Phish-master! You spotted them all.";
            if (commentator != null) commentator.Say("Oh thank you so much, dear! You're wonderful.");
        }
        else if (pct >= 0.7f)
        {
            resultStars.text = "* *";
            resultMessage.text = "Solid work! Review the ones you missed.";
            if (commentator != null) commentator.Say("That was a big help — thank you, dear!");
        }
        else
        {
            resultStars.text = "*";
            resultMessage.text = "Scammers are tricky. Try again and read carefully.";
            if (commentator != null) commentator.Say("It's a good start. We'll get them next time.");
        }
    }

    public void OnPlayAgain()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void OnReturnToMap()
    {
        SceneManager.LoadScene("WorldMap");
    }

    // ===================================================================
    // Helpers
    // ===================================================================

    private static Image AddImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    private static TMP_Text AddText(Transform parent, string name, string content,
        int size, Color color, TextAlignmentOptions align,
        FontStyles style = FontStyles.Normal)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = content;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.fontStyle = style;
        t.raycastTarget = false;
        return t;
    }

    // ===================================================================
    // Email content (edit here to change a round)
    // ===================================================================

    void InitializeEmails()
    {
        emails = new Email[]
        {
            new Email {
                senderName  = "PayPal Support",
                senderEmail = "support@paypa1.com",
                subject     = "URGENT: Your account has been suspended",
                preview     = "Dear Customer, your PayPal account has been suspended due to suspicious activity",
                body        = "Dear Customer,\n\nYour PayPal account has been suspended due to suspicious activity. Click below immediately to verify your information or your account will be permanently deleted within 24 hours.\n\n- PayPal Support",
                timestamp   = "10:09 AM",
                avatarColor = new Color(0.07f, 0.45f, 0.71f),
                isScam      = true,
                explanation = "SCAM. The sender domain is 'paypa1.com' - that's the number 1, not a lowercase L. Real PayPal emails come from paypal.com. The 24-hour deletion threat is a classic urgency trick."
            },
            new Email {
                senderName  = "Spotify",
                senderEmail = "newsletter@spotify.com",
                subject     = "Your June playlist is ready",
                preview     = "Hi there, your monthly Spotify stats are in. Check out your top songs",
                body        = "Hi there,\n\nYour monthly Spotify stats are in. Check out your top songs from this month in the app.\n\n- The Spotify Team",
                timestamp   = "9:42 AM",
                avatarColor = new Color(0.12f, 0.84f, 0.38f),
                isScam      = false,
                explanation = "SAFE. Real domain (spotify.com), calm tone, no threats, no request for personal information."
            },
            new Email {
                senderName  = "Canada Revenue Agency",
                senderEmail = "noreply@canada-revenue-agency-refund.com",
                subject     = "You have a $847 tax refund waiting",
                preview     = "A refund of $847.00 is ready. To claim it provide your SIN and banking details",
                body        = "NOTICE FROM THE CRA:\n\nA refund of $847.00 is ready. To claim it, provide your SIN and banking details within 24 hours or the refund will be cancelled.\n\n- Canada Revenue Agency",
                timestamp   = "8:27 AM",
                avatarColor = new Color(0.78f, 0.13f, 0.13f),
                isScam      = true,
                explanation = "SCAM. The CRA never emails asking for your SIN or banking info. Real CRA messages come from cra-arc.gc.ca only - never a hyphenated domain like this. The 'within 24 hours' deadline is another red flag."
            },
            new Email {
                senderName  = "Amazon",
                senderEmail = "orders@amazon.com",
                subject     = "Your order has shipped",
                preview     = "Hello, your order #112-4857293 has shipped. Estimated delivery June 12",
                body        = "Hello,\n\nYour order #112-4857293 has shipped. Estimated delivery June 12. Track it in the Amazon app.\n\n- Amazon",
                timestamp   = "Yesterday",
                avatarColor = new Color(1f, 0.6f, 0f),
                isScam      = false,
                explanation = "SAFE. Real domain (amazon.com), specific order number, no request for personal info, directs to the official app."
            },
            new Email {
                senderName  = "Microsoft Security",
                senderEmail = "security@micros0ft-account.net",
                subject     = "Unusual sign-in detected",
                preview     = "Dear User, we detected a sign-in from an unrecognized location. Click below immediately",
                body        = "Dear User,\n\nWe detected a sign-in from an unrecognized location. Click below immediately to secure your account.\n\n- Microsoft Security Team",
                timestamp   = "Yesterday",
                avatarColor = new Color(0.05f, 0.45f, 0.79f),
                isScam      = true,
                explanation = "SCAM. The domain is 'micros0ft-account.net' - that's a zero, not a letter o. Real Microsoft alerts come from microsoft.com. 'Dear User' is also a generic greeting - Microsoft would normally use your name."
            },
            new Email {
                senderName  = "Uber",
                senderEmail = "no-reply@uber.com",
                subject     = "Your Tuesday night trip receipt",
                preview     = "Thanks for riding with Uber. Your trip came to $14.72. Payment charged to Visa ending in 4821",
                body        = "Thanks for riding with Uber.\n\nYour trip came to $14.72. Payment charged to Visa ending in 4821.\n\n- Uber",
                timestamp   = "May 4",
                avatarColor = new Color(0.1f, 0.1f, 0.1f),
                isScam      = false,
                explanation = "SAFE. Real domain, specific trip details, only the last 4 digits of your card are shown (a security best practice), and no suspicious links."
            },
            new Email {
                senderName  = "Netflix Billing",
                senderEmail = "billing@netfl1x-payments.com",
                subject     = "Payment failed - update card now",
                preview     = "Your Netflix payment could not be processed. Update billing within 48 hours",
                body        = "Hello,\n\nYour Netflix payment could not be processed. Update your billing details within 48 hours by clicking the link below or your account will be terminated.\n\n- Netflix Billing",
                timestamp   = "May 3",
                avatarColor = new Color(0.90f, 0.05f, 0.10f),
                isScam      = true,
                explanation = "SCAM. The domain is 'netfl1x-payments.com' - that's a number 1 instead of an L, and Netflix doesn't use that subdomain. Real Netflix billing comes from netflix.com only."
            },
            new Email {
                senderName  = "Google Calendar",
                senderEmail = "calendar-noreply@google.com",
                subject     = "Reminder: Coffee with Sarah at 2pm",
                preview     = "This is a reminder for your event Coffee with Sarah today at 2pm at The Wired Monk Cafe",
                body        = "This is a reminder for your event:\n\nCoffee with Sarah\nToday at 2:00 PM\nThe Wired Monk Cafe\n\n- Google Calendar",
                timestamp   = "May 3",
                avatarColor = new Color(0.26f, 0.52f, 0.96f),
                isScam      = false,
                explanation = "SAFE. Real Google domain, references a specific event you'd recognize, no suspicious links or requests for info."
            }
        };
    }
}