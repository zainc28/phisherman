#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

public class EmailSwiperBuilder : EditorWindow
{
    // Candy Crush palette
    static readonly Color BG = Hex("#3D1A6E");   // deep purple
    static readonly Color HEADER_YEL = Hex("#FFD700");   // gold header
    static readonly Color HEADER_CYAN = Hex("#00C8E0");   // cyan header
    static readonly Color CARD_WHITE = Hex("#FFFFFF");
    static readonly Color CARD_LIGHT = Hex("#F4F0FF");
    static readonly Color ACCENT_PINK = Hex("#FF4D8B");
    static readonly Color TEXT_DARK = Hex("#2A1040");
    static readonly Color TEXT_MID = Hex("#5A3A7A");
    static readonly Color BTN_RED = Hex("#E8233A");
    static readonly Color BTN_GREEN = Hex("#1DB954");
    static readonly Color BTN_BLUE = Hex("#1E7BF0");
    static readonly Color BTN_GREY = Hex("#7A7A9A");
    static readonly Color ROW_BG = Hex("#FFFFFF");

    static Color Hex(string h)
    {
        ColorUtility.TryParseHtmlString(h, out Color c);
        return c;
    }

    [MenuItem("Phisherman/Build Email Swiper UI")]
    public static void BuildUI()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) { Debug.LogError("No Canvas found in scene!"); return; }

        // --- Setup canvas ---
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
        }
        if (Camera.main != null)
            Camera.main.backgroundColor = BG;

        // --- Delete everything old ---
        string[] toDelete = { "TutorialPanel", "InboxPanel", "DetailPanel", "FeedbackOverlay", "ResultPanel", "GameManager" };
        foreach (string n in toDelete)
        {
            Transform t = canvas.transform.Find(n);
            if (t != null) DestroyImmediate(t.gameObject);
            GameObject g = GameObject.Find(n);
            if (g != null) DestroyImmediate(g);
        }

        // ============================
        // TUTORIAL PANEL
        // ============================
        GameObject tutPanel = MakePanel(canvas.transform, "TutorialPanel", BG);

        // Yellow top bar
        GameObject tutTop = MakePanel(tutPanel.transform, "TopBar", HEADER_YEL);
        Stretch(tutTop, 0, 0, 0, 0);
        AnchorTopStrip(tutTop, 0.82f);

        // Title in bar
        var tutTitle = MakeText(tutPanel.transform, "TutorialTitle", "Welcome!", 50, FontStyles.Bold, TEXT_DARK);
        SetRect(tutTitle, 0, 215, 860, 80);
        TMP(tutTitle).alignment = TextAlignmentOptions.Center;

        // White card
        GameObject tutCard = MakePanel(tutPanel.transform, "TutCard", CARD_WHITE);
        SetRect(tutCard, 0, -20, 860, 360);

        // Body inside card
        var tutBody = MakeText(tutCard.transform, "TutorialBody", "", 26, FontStyles.Normal, TEXT_DARK);
        FillParent(tutBody, 30, 30, 25, 25);
        TMP(tutBody).alignment = TextAlignmentOptions.TopLeft;
        TMP(tutBody).textWrappingMode = TextWrappingModes.Normal;

        // Step label
        var stepText = MakeText(tutPanel.transform, "TutorialStepText", "1 of 4", 22, FontStyles.Normal, new Color(1, 1, 1, 0.7f));
        SetRect(stepText, 0, -228, 200, 38);

        // Next button
        GameObject tutBtn = MakeButton(tutPanel.transform, "TutorialNextButton", "Next", BTN_BLUE, 36);
        SetRect(tutBtn, 0, -280, 320, 80);
        BoldBtn(tutBtn);

        // ============================
        // INBOX PANEL
        // ============================
        GameObject inboxPanel = MakePanel(canvas.transform, "InboxPanel", BG);
        inboxPanel.SetActive(false);

        // Yellow header
        GameObject inboxHeader = MakePanel(inboxPanel.transform, "InboxHeader", HEADER_YEL);
        Stretch(inboxHeader, 0, 0, 0, 0);
        AnchorTopStrip(inboxHeader, 0.86f);

        var inboxTitle = MakeText(inboxHeader.transform, "InboxTitle", "Inbox", 46, FontStyles.Bold, TEXT_DARK);
        SetRect(inboxTitle, -220, 0, 280, 70);
        TMP(inboxTitle).alignment = TextAlignmentOptions.Left;

        var inboxCount = MakeText(inboxHeader.transform, "InboxCountText", "6 unread", 22, FontStyles.Normal, TEXT_MID);
        SetRect(inboxCount, -220, -35, 280, 34);
        TMP(inboxCount).alignment = TextAlignmentOptions.Left;

        var scoreText = MakeText(inboxHeader.transform, "ScoreText", "Score: 0", 28, FontStyles.Bold, TEXT_DARK);
        SetRect(scoreText, 260, 0, 260, 60);
        TMP(scoreText).alignment = TextAlignmentOptions.Right;

        var streakText = MakeText(inboxHeader.transform, "StreakText", "", 20, FontStyles.Bold, BTN_RED);
        SetRect(streakText, 260, -35, 260, 32);
        TMP(streakText).alignment = TextAlignmentOptions.Right;

        // Scroll view
        GameObject sv = new GameObject("EmailScrollView");
        sv.transform.SetParent(inboxPanel.transform, false);
        sv.AddComponent<Image>().color = new Color(0, 0, 0, 0);
        ScrollRect sr = sv.AddComponent<ScrollRect>();
        RectTransform svRT = sv.GetComponent<RectTransform>();
        svRT.anchorMin = new Vector2(0, 0);
        svRT.anchorMax = new Vector2(1, 0.86f);
        svRT.offsetMin = new Vector2(20, 10);
        svRT.offsetMax = new Vector2(-20, -10);

        GameObject vp = new GameObject("Viewport");
        vp.transform.SetParent(sv.transform, false);
        RectTransform vpRT = vp.AddComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero; vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = vpRT.offsetMax = Vector2.zero;
        vp.AddComponent<Image>().color = new Color(0, 0, 0, 0);
        vp.AddComponent<Mask>().showMaskGraphic = false;

        GameObject content = new GameObject("Content");
        content.transform.SetParent(vp.transform, false);
        RectTransform cRT = content.AddComponent<RectTransform>();
        cRT.anchorMin = new Vector2(0, 1); cRT.anchorMax = new Vector2(1, 1);
        cRT.pivot = new Vector2(0.5f, 1f);
        cRT.offsetMin = cRT.offsetMax = Vector2.zero;

        VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 10f;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.padding = new RectOffset(0, 0, 10, 10);

        ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        sr.viewport = vpRT;
        sr.content = cRT;
        sr.horizontal = false;
        sr.vertical = true;

        // ============================
        // EMAIL ROW PREFAB
        // ============================
        GameObject rowObj = new GameObject("EmailRowPrefab");
        rowObj.transform.SetParent(canvas.transform, false);

        Image rowImg = rowObj.AddComponent<Image>();
        rowImg.color = ROW_BG;
        Button rowBtn = rowObj.AddComponent<Button>();
        ColorBlock rcb = rowBtn.colors;
        rcb.highlightedColor = Hex("#EDE8FF");
        rcb.pressedColor = Hex("#D4CCF5");
        rowBtn.colors = rcb;
        rowBtn.targetGraphic = rowImg;
        rowObj.GetComponent<RectTransform>().sizeDelta = new Vector2(820f, 90f);
        LayoutElement rle = rowObj.AddComponent<LayoutElement>();
        rle.preferredHeight = 90f;

        // Pink left accent
        GameObject acc = new GameObject("AccentBar");
        acc.transform.SetParent(rowObj.transform, false);
        acc.AddComponent<Image>().color = ACCENT_PINK;
        RectTransform accRT = acc.GetComponent<RectTransform>();
        accRT.anchorMin = new Vector2(0, 0); accRT.anchorMax = new Vector2(0, 1);
        accRT.offsetMin = Vector2.zero; accRT.offsetMax = new Vector2(7, 0);

        var rSender = MakeText(rowObj.transform, "SenderText", "sender@example.com", 21, FontStyles.Bold, TEXT_DARK);
        RectTransform rsRT = rSender.GetComponent<RectTransform>();
        rsRT.anchorMin = new Vector2(0, 0.5f); rsRT.anchorMax = new Vector2(1, 1f);
        rsRT.offsetMin = new Vector2(18, 2); rsRT.offsetMax = new Vector2(-12, -2);
        TMP(rSender).alignment = TextAlignmentOptions.BottomLeft;

        var rSubject = MakeText(rowObj.transform, "SubjectText", "Subject", 19, FontStyles.Normal, TEXT_MID);
        RectTransform rsubRT = rSubject.GetComponent<RectTransform>();
        rsubRT.anchorMin = new Vector2(0, 0); rsubRT.anchorMax = new Vector2(1, 0.5f);
        rsubRT.offsetMin = new Vector2(18, 2); rsubRT.offsetMax = new Vector2(-12, -2);
        TMP(rSubject).alignment = TextAlignmentOptions.TopLeft;

        string prefabPath = "Assets/Prefabs/EmailRowPrefab.prefab";
        PrefabUtility.SaveAsPrefabAsset(rowObj, prefabPath);
        DestroyImmediate(rowObj);
        GameObject savedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        // ============================
        // DETAIL PANEL
        // ============================
        GameObject detailPanel = MakePanel(canvas.transform, "DetailPanel", BG);
        detailPanel.SetActive(false);

        // Cyan header
        GameObject detHead = MakePanel(detailPanel.transform, "DetailHeader", HEADER_CYAN);
        AnchorTopStrip(detHead, 0.86f);

        GameObject backBtn = MakeButton(detHead.transform, "BackButton", "Back", BTN_GREY, 26);
        SetRect(backBtn, -360, 0, 120, 55);

        var detHeadTitle = MakeText(detHead.transform, "DetailHeaderLabel", "Email", 38, FontStyles.Bold, TEXT_DARK);
        SetRect(detHeadTitle, 30, 0, 350, 70);
        TMP(detHeadTitle).alignment = TextAlignmentOptions.Left;

        // White email card (this is detailCard)
        GameObject emailCard = MakePanel(detailPanel.transform, "DetailCard", CARD_WHITE);
        SetRect(emailCard, 0, 40, 880, 450);

        // Shadow panel behind card
        GameObject shadow = MakePanel(detailPanel.transform, "CardShadow", new Color(0.1f, 0.05f, 0.2f, 0.4f));
        SetRect(shadow, 5, 35, 880, 450);
        emailCard.transform.SetAsLastSibling();

        // Sender inside card
        var dSender = MakeText(emailCard.transform, "DetailSender", "From:", 22, FontStyles.Bold, ACCENT_PINK);
        RectTransform dsRT = dSender.GetComponent<RectTransform>();
        dsRT.anchorMin = new Vector2(0, 1); dsRT.anchorMax = new Vector2(1, 1);
        dsRT.offsetMin = new Vector2(20, -52); dsRT.offsetMax = new Vector2(-20, -10);
        TMP(dSender).alignment = TextAlignmentOptions.Left;

        // Divider
        GameObject div = new GameObject("Divider");
        div.transform.SetParent(emailCard.transform, false);
        div.AddComponent<Image>().color = Hex("#E0DAEE");
        RectTransform divRT = div.GetComponent<RectTransform>();
        divRT.anchorMin = new Vector2(0, 1); divRT.anchorMax = new Vector2(1, 1);
        divRT.offsetMin = new Vector2(15, -60); divRT.offsetMax = new Vector2(-15, -58);

        // Subject inside card
        var dSubject = MakeText(emailCard.transform, "DetailSubject", "Subject:", 24, FontStyles.Bold, TEXT_DARK);
        RectTransform dsubRT = dSubject.GetComponent<RectTransform>();
        dsubRT.anchorMin = new Vector2(0, 1); dsubRT.anchorMax = new Vector2(1, 1);
        dsubRT.offsetMin = new Vector2(20, -108); dsubRT.offsetMax = new Vector2(-20, -68);
        TMP(dSubject).alignment = TextAlignmentOptions.Left;

        // Body inside card
        var dBody = MakeText(emailCard.transform, "DetailBody", "Email body...", 20, FontStyles.Normal, TEXT_DARK);
        RectTransform dbRT = dBody.GetComponent<RectTransform>();
        dbRT.anchorMin = Vector2.zero; dbRT.anchorMax = Vector2.one;
        dbRT.offsetMin = new Vector2(20, 14); dbRT.offsetMax = new Vector2(-20, -118);
        TMP(dBody).alignment = TextAlignmentOptions.TopLeft;
        TMP(dBody).textWrappingMode = TextWrappingModes.Normal;

        // Scam button
        GameObject scamBtn = MakeButton(detailPanel.transform, "ScamButton", "Scam", BTN_RED, 38);
        SetRect(scamBtn, -195, -240, 290, 90);
        BoldBtn(scamBtn);

        // Safe button
        GameObject safeBtn = MakeButton(detailPanel.transform, "SafeButton", "Safe", BTN_GREEN, 38);
        SetRect(safeBtn, 195, -240, 290, 90);
        BoldBtn(safeBtn);

        // ============================
        // FEEDBACK OVERLAY
        // ============================
        GameObject feedbackOverlay = MakePanel(canvas.transform, "FeedbackOverlay", new Color(0.13f, 0.74f, 0.35f, 0.97f));
        feedbackOverlay.SetActive(false);
        feedbackOverlay.transform.SetAsLastSibling();

        var feedbackText = MakeText(feedbackOverlay.transform, "FeedbackText", "", 28, FontStyles.Normal, Color.white);
        FillParent(feedbackText, 60, 60, 80, 80);
        TMP(feedbackText).alignment = TextAlignmentOptions.Center;
        TMP(feedbackText).textWrappingMode = TextWrappingModes.Normal;

        // ============================
        // RESULT PANEL
        // ============================
        GameObject resultPanel = MakePanel(canvas.transform, "ResultPanel", BG);
        resultPanel.SetActive(false);

        // Gold banner
        GameObject resBanner = MakePanel(resultPanel.transform, "ResultBanner", HEADER_YEL);
        AnchorTopStrip(resBanner, 0.75f);

        var resTitle = MakeText(resBanner.transform, "ResultTitle", "Round Complete!", 52, FontStyles.Bold, TEXT_DARK);
        SetRect(resTitle, 0, 0, 700, 80);

        // White score card
        GameObject scoreCard = MakePanel(resultPanel.transform, "ScoreCard", CARD_WHITE);
        SetRect(scoreCard, 0, 40, 600, 290);

        var starsText = MakeText(scoreCard.transform, "ResultStarsText", "* * *", 52, FontStyles.Bold, HEADER_YEL);
        SetRect(starsText, 0, 80, 500, 80);

        var finalScore = MakeText(scoreCard.transform, "FinalScoreText", "60 / 60", 58, FontStyles.Bold, TEXT_DARK);
        SetRect(finalScore, 0, -5, 500, 80);

        var resMsg = MakeText(scoreCard.transform, "ResultMessageText", "", 24, FontStyles.Normal, TEXT_MID);
        SetRect(resMsg, 0, -80, 520, 60);
        TMP(resMsg).textWrappingMode = TextWrappingModes.Normal;

        GameObject playAgainBtn = MakeButton(resultPanel.transform, "PlayAgainButton", "Play Again", BTN_GREEN, 34);
        SetRect(playAgainBtn, 0, -210, 320, 80);
        BoldBtn(playAgainBtn);

        GameObject returnBtn = MakeButton(resultPanel.transform, "ReturnButton", "Return to Map", BTN_GREY, 28);
        SetRect(returnBtn, 0, -310, 320, 70);

        // ============================
        // GAME MANAGER + WIRING
        // ============================
        GameObject gmGO = new GameObject("GameManager");
        EmailSwiperManager mgr = gmGO.AddComponent<EmailSwiperManager>();

        mgr.tutorialPanel = tutPanel;
        mgr.inboxPanel = inboxPanel;
        mgr.detailPanel = detailPanel;
        mgr.feedbackOverlay = feedbackOverlay;
        mgr.resultPanel = resultPanel;

        mgr.tutorialTitleText = TMP(tutTitle);
        mgr.tutorialBodyText = TMP(tutBody);
        mgr.tutorialStepText = TMP(stepText);
        mgr.tutorialNextButton = tutBtn.GetComponent<Button>();
        mgr.tutorialNextButtonText = TMP(tutBtn.transform.Find("Text (TMP)").gameObject);

        mgr.emailListContainer = content.transform;
        mgr.emailRowPrefab = savedPrefab;
        mgr.scoreText = TMP(scoreText);
        mgr.inboxCountText = TMP(inboxCount);
        mgr.streakText = TMP(streakText);

        mgr.detailSender = TMP(dSender);
        mgr.detailSubject = TMP(dSubject);
        mgr.detailBody = TMP(dBody);
        mgr.detailCard = emailCard;
        mgr.scamButton = scamBtn.GetComponent<Button>();
        mgr.safeButton = safeBtn.GetComponent<Button>();

        mgr.feedbackText = TMP(feedbackText);
        mgr.feedbackImage = feedbackOverlay.GetComponent<Image>();

        mgr.finalScoreText = TMP(finalScore);
        mgr.resultMessageText = TMP(resMsg);
        mgr.resultStarsText = TMP(starsText);

        Wire(tutBtn, mgr, "OnTutorialNext");
        Wire(scamBtn, mgr, "OnScamClicked");
        Wire(safeBtn, mgr, "OnSafeClicked");
        Wire(backBtn, mgr, "OnBackClicked");
        Wire(playAgainBtn, mgr, "OnPlayAgain");
        Wire(returnBtn, mgr, "OnReturnToMap");

        EditorUtility.SetDirty(gmGO);
        AssetDatabase.SaveAssets();
        Debug.Log("Done! Hit Play to test.");
    }

    // ============================
    // HELPERS
    // ============================
    static void Wire(GameObject btn, EmailSwiperManager target, string method)
    {
        Button b = btn.GetComponent<Button>();
        var m = typeof(EmailSwiperManager).GetMethod(method);
        if (b == null || m == null) { Debug.LogError("Wire failed: " + method); return; }
        var del = System.Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction), target, m)
                  as UnityEngine.Events.UnityAction;
        UnityEditor.Events.UnityEventTools.AddPersistentListener(b.onClick, del);
    }

    static GameObject MakePanel(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<Image>().color = color;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return go;
    }

    static GameObject MakeText(Transform parent, string name, string text, int size, FontStyles style, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.fontStyle = style; t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.Normal;
        return go;
    }

    static GameObject MakeButton(Transform parent, string name, string label, Color color, int fontSize)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.color = color;
        Button btn = go.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.highlightedColor = new Color(Mathf.Min(color.r + 0.12f, 1), Mathf.Min(color.g + 0.12f, 1), Mathf.Min(color.b + 0.12f, 1));
        cb.pressedColor = new Color(Mathf.Max(color.r - 0.12f, 0), Mathf.Max(color.g - 0.12f, 0), Mathf.Max(color.b - 0.12f, 0));
        btn.colors = cb; btn.targetGraphic = img;

        GameObject tgo = new GameObject("Text (TMP)");
        tgo.transform.SetParent(go.transform, false);
        TextMeshProUGUI tmp = tgo.AddComponent<TextMeshProUGUI>();
        tmp.text = label; tmp.fontSize = fontSize; tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        RectTransform tRT = tgo.GetComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
        tRT.offsetMin = tRT.offsetMax = Vector2.zero;
        return go;
    }

    static void SetRect(GameObject go, float x, float y, float w, float h)
    {
        RectTransform rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    static void Stretch(GameObject go, float l, float r, float t, float b)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
    }

    static void AnchorTopStrip(GameObject go, float yMin)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, yMin); rt.anchorMax = new Vector2(1, 1);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void FillParent(GameObject go, float l, float r, float t, float b)
    {
        RectTransform rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
    }

    static void BoldBtn(GameObject btn)
    {
        var t = btn.transform.Find("Text (TMP)");
        if (t != null) t.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
    }

    static TextMeshProUGUI TMP(GameObject go) => go.GetComponent<TextMeshProUGUI>();
}
#endif