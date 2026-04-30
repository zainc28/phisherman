using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;

public class EmailSwiperManager : MonoBehaviour
{
    [Header("Panels")]
    public GameObject tutorialPanel;
    public GameObject inboxPanel;
    public GameObject detailPanel;
    public GameObject feedbackOverlay;
    public GameObject resultPanel;

    [Header("Tutorial UI")]
    public TextMeshProUGUI tutorialTitleText;
    public TextMeshProUGUI tutorialBodyText;
    public TextMeshProUGUI tutorialStepText;
    public Button tutorialNextButton;
    public TextMeshProUGUI tutorialNextButtonText;

    [Header("Inbox UI")]
    public Transform emailListContainer;
    public GameObject emailRowPrefab;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI inboxCountText;
    public TextMeshProUGUI streakText;

    [Header("Detail UI")]
    public TextMeshProUGUI detailSender;
    public TextMeshProUGUI detailSubject;
    public TextMeshProUGUI detailBody;
    public Button scamButton;
    public Button safeButton;
    public GameObject detailCard;

    [Header("Feedback UI")]
    public TextMeshProUGUI feedbackText;
    public Image feedbackImage;

    [Header("Result UI")]
    public TextMeshProUGUI finalScoreText;
    public TextMeshProUGUI resultMessageText;
    public TextMeshProUGUI resultStarsText;

    private int score = 0;
    private int streak = 0;
    private int selectedIndex = 0;
    private int tutorialStep = 0;
    private bool[] answered;
    private GameObject[] spawnedRows;

    struct Email
    {
        public string sender, subject, body, explanation;
        public bool isScam;
        public Email(string s, string sub, string b, bool scam, string exp)
        { sender = s; subject = sub; body = b; isScam = scam; explanation = exp; }
    }

    Email[] emails = new Email[]
    {
        new Email("support@paypa1.com",
            "URGENT: Your account has been suspended",
            "Dear Customer,\n\nYour PayPal account has been suspended due to suspicious activity. Click below immediately to verify your information or your account will be permanently deleted within 24 hours.\n\n- PayPal Support",
            true,
            "SCAM. The sender says 'paypa1.com' — that is the number 1, not the letter l. Real companies never threaten to delete your account by email."),

        new Email("newsletter@spotify.com",
            "Your June playlist is ready",
            "Hi there,\n\nYour monthly Spotify stats are in. Check out your top songs from this month in the app.\n\n- The Spotify Team",
            false,
            "Safe. Real domain, calm tone, no threats, no request for personal information."),

        new Email("noreply@canada-revenue-agency-refund.com",
            "You have a $847 tax refund waiting",
            "NOTICE FROM THE CRA:\n\nA refund of $847.00 is ready. To claim it provide your SIN and banking details within 24 hours or the refund will be cancelled.\n\n- Canada Revenue Agency",
            true,
            "SCAM. The CRA never emails asking for your SIN or banking info. Real CRA emails come from cra-arc.gc.ca only."),

        new Email("orders@amazon.com",
            "Your order has shipped",
            "Hello,\n\nYour order #112-4857293 has shipped. Estimated delivery June 12. Track it in the Amazon app.\n\n- Amazon",
            false,
            "Safe. Real domain, specific order number, no request for personal info."),

        new Email("security@micros0ft-account.net",
            "Unusual sign-in detected",
            "Dear User,\n\nWe detected a sign-in from an unrecognized location. Click below immediately to secure your account.\n\n- Microsoft Security Team",
            true,
            "SCAM. The domain is 'micros0ft-account.net' — a zero not the letter o. Microsoft only sends alerts from microsoft.com."),

        new Email("no-reply@uber.com",
            "Your Tuesday night trip receipt",
            "Thanks for riding with Uber.\n\nYour trip came to $14.72. Payment charged to Visa ending in 4821.\n\n- Uber",
            false,
            "Safe. Real domain, specific trip details, only last 4 card digits shown, no suspicious links."),
    };

    string[] tutorialTitles = {
        "Welcome to Phish Patrol!",
        "How phishing works",
        "What to look for",
        "You are ready"
    };

    string[] tutorialBodies = {
        "Scammers send fake emails pretending to be real companies to trick you into giving up your password or money.\n\nYour job is to catch them.",
        "Phishing emails usually:\n\n- Use a sender address that looks almost right (paypa1.com not paypal.com)\n- Create urgency: act within 24 hours\n- Ask for your SIN, password, or banking info\n- Greet you as Dear Customer not your name",
        "Safe emails tend to:\n\n- Come from real correctly spelled domains\n- Reference specific details like order numbers\n- Never ask for sensitive info over email\n- Direct you to official apps not suspicious links",
        "Emails will arrive in your inbox. Click any email to open it and read the full message.\n\nThen decide: Scam or Safe?\n\nTake your time. Read carefully."
    };

    void Start()
    {
        answered = new bool[emails.Length];
        spawnedRows = new GameObject[emails.Length];

        tutorialPanel.SetActive(true);
        inboxPanel.SetActive(false);
        detailPanel.SetActive(false);
        feedbackOverlay.SetActive(false);
        resultPanel.SetActive(false);

        ShowTutorialStep(0);
    }

    void ShowTutorialStep(int step)
    {
        tutorialTitleText.text = tutorialTitles[step];
        tutorialBodyText.text = tutorialBodies[step];
        tutorialStepText.text = (step + 1) + " of " + tutorialTitles.Length;
        tutorialNextButtonText.text = step == tutorialTitles.Length - 1 ? "Start" : "Next";
    }

    public void OnTutorialNext()
    {
        tutorialStep++;
        if (tutorialStep >= tutorialTitles.Length)
        {
            tutorialPanel.SetActive(false);
            inboxPanel.SetActive(true);
            StartCoroutine(SpawnEmailsAnimated());
        }
        else
        {
            ShowTutorialStep(tutorialStep);
        }
    }

    IEnumerator SpawnEmailsAnimated()
    {
        foreach (Transform child in emailListContainer)
            Destroy(child.gameObject);

        UpdateHUD();

        for (int i = 0; i < emails.Length; i++)
        {
            int index = i;
            GameObject row = Instantiate(emailRowPrefab, emailListContainer);
            spawnedRows[index] = row;

            row.transform.Find("SenderText").GetComponent<TextMeshProUGUI>().text = emails[i].sender;
            row.transform.Find("SubjectText").GetComponent<TextMeshProUGUI>().text = emails[i].subject;

            if (answered[index])
                row.GetComponent<Image>().color = new Color(0.82f, 0.82f, 0.88f);

            row.GetComponent<Button>().onClick.AddListener(() => OpenEmail(index));

            RectTransform rt = row.GetComponent<RectTransform>();
            Vector2 endPos = rt.anchoredPosition;
            rt.anchoredPosition = endPos + new Vector2(0, 100f);
            CanvasGroup cg = row.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            StartCoroutine(AnimateRowIn(rt, cg, endPos));
            yield return new WaitForSeconds(0.1f);
        }
    }

    IEnumerator AnimateRowIn(RectTransform rt, CanvasGroup cg, Vector2 target)
    {
        float t = 0f;
        Vector2 start = rt.anchoredPosition;
        while (t < 1f)
        {
            t += Time.deltaTime * 5f;
            float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
            rt.anchoredPosition = Vector2.Lerp(start, target, e);
            cg.alpha = Mathf.Clamp01(t * 2f);
            yield return null;
        }
        rt.anchoredPosition = target;
        cg.alpha = 1f;
    }

    void UpdateHUD()
    {
        int remaining = 0;
        foreach (bool a in answered) if (!a) remaining++;
        if (inboxCountText != null) inboxCountText.text = remaining + " unread";
        if (scoreText != null) scoreText.text = "Score: " + score;
        if (streakText != null) streakText.text = streak >= 3 ? streak + " in a row!" : "";
    }

    void RefreshInbox()
    {
        for (int i = 0; i < spawnedRows.Length; i++)
        {
            if (spawnedRows[i] == null) continue;
            if (answered[i])
                spawnedRows[i].GetComponent<Image>().color = new Color(0.82f, 0.82f, 0.88f);
        }
        UpdateHUD();
    }

    void OpenEmail(int index)
    {
        selectedIndex = index;
        inboxPanel.SetActive(false);
        detailPanel.SetActive(true);

        detailSender.text = "From:  " + emails[index].sender;
        detailSubject.text = emails[index].subject;
        detailBody.text = emails[index].body;
        scamButton.interactable = !answered[index];
        safeButton.interactable = !answered[index];

        if (detailCard != null)
            StartCoroutine(BounceIn(detailCard.GetComponent<RectTransform>()));
    }

    IEnumerator BounceIn(RectTransform rt)
    {
        Vector2 target = rt.anchoredPosition;
        rt.anchoredPosition = target + new Vector2(0, -50f);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 7f;
            float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
            rt.anchoredPosition = Vector2.Lerp(target + new Vector2(0, -50f), target, e);
            yield return null;
        }
        rt.anchoredPosition = target;
    }

    public void OnBackClicked()
    {
        detailPanel.SetActive(false);
        inboxPanel.SetActive(true);
    }

    public void OnScamClicked() { if (!answered[selectedIndex]) CheckAnswer(true); }
    public void OnSafeClicked() { if (!answered[selectedIndex]) CheckAnswer(false); }

    void CheckAnswer(bool playerSaidScam)
    {
        answered[selectedIndex] = true;
        bool correct = playerSaidScam == emails[selectedIndex].isScam;
        if (correct) { score += 10; streak++; if (streak >= 3) score += 5; }
        else streak = 0;
        StartCoroutine(ShowFeedback(correct, emails[selectedIndex].explanation));
    }

    IEnumerator ShowFeedback(bool correct, string explanation)
    {
        detailPanel.SetActive(false);
        feedbackOverlay.SetActive(true);

        feedbackImage.color = correct
            ? new Color(0.13f, 0.74f, 0.35f, 0.97f)
            : new Color(0.9f, 0.23f, 0.23f, 0.97f);

        feedbackText.text = (correct ? "Correct!\n\n" : "Not quite.\n\n") + explanation;

        feedbackOverlay.transform.localScale = Vector3.one * 0.85f;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 8f;
            float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
            feedbackOverlay.transform.localScale = Vector3.Lerp(Vector3.one * 0.85f, Vector3.one, e);
            yield return null;
        }

        yield return new WaitForSeconds(2.8f);
        feedbackOverlay.SetActive(false);
        feedbackOverlay.transform.localScale = Vector3.one;

        bool allDone = true;
        foreach (bool a in answered) if (!a) { allDone = false; break; }

        if (allDone) ShowResult();
        else { inboxPanel.SetActive(true); RefreshInbox(); }
    }

    void ShowResult()
    {
        resultPanel.SetActive(true);
        int max = emails.Length * 10;
        finalScoreText.text = score + " / " + max;
        float pct = (float)score / max;
        if (pct >= 1f) { resultStarsText.text = "* * *"; resultMessageText.text = "Perfect. You caught every single one."; }
        else if (pct >= 0.6f) { resultStarsText.text = "* *"; resultMessageText.text = "Good work. Review the ones you missed."; }
        else { resultStarsText.text = "*"; resultMessageText.text = "Scammers are tricky. Try again and read carefully."; }
    }

    public void OnPlayAgain() { SceneManager.LoadScene("EmailSwiper"); }
    public void OnReturnToMap() { SceneManager.LoadScene("WorldMap"); }
}