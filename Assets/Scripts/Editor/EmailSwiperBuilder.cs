using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Builds the Gmail-styled Email Swiper scene from scratch.
///
/// Run via:    Phisherman > Build Email Swiper Scene
/// Output:     Assets/Scenes/EmailSwiper.unity
///
/// Re-running fully overwrites the scene. Email content is defined in
/// EmailSwiperManager.InitializeEmails() — this builder only constructs
/// the UI shell and wires references.
/// </summary>
public static class EmailSwiperBuilder
{
    private const string ScenesDir = "Assets/Scenes";
    private const string ScenePath = "Assets/Scenes/EmailSwiper.unity";

    // ===== Color palette =====
    // Gmail-ish chrome
    private static readonly Color BgColor = Hex("#F6F8FC");
    private static readonly Color TopBarBg = Color.white;
    private static readonly Color SidebarBg = Hex("#F6F8FC");
    private static readonly Color InboxBg = Color.white;
    private static readonly Color BorderColor = Hex("#E0E3E8");
    private static readonly Color GmailRed = Hex("#EA4335");
    private static readonly Color GmailBlue = Hex("#1A73E8");
    private static readonly Color InboxSelected = Hex("#FCE8E6");
    private static readonly Color SearchBg = Hex("#EAF1FB");
    private static readonly Color MutedText = Hex("#5F6368");
    private static readonly Color DarkText = Hex("#202124");

    // Game HUD (saturated, candy-crush feel)
    private static readonly Color HudBg = Hex("#2E3A59");
    private static readonly Color ScoreColor = Hex("#FFD93D");
    private static readonly Color StreakColor = Hex("#FF9F1C");
    private static readonly Color SafeGreen = Hex("#2ECC71");
    private static readonly Color ScamRed = Hex("#E74C3C");

    [MenuItem("Phisherman/Build Email Swiper Scene")]
    public static void Build()
    {
        if (!Directory.Exists(ScenesDir)) Directory.CreateDirectory(ScenesDir);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // === Camera ===
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        camGo.AddComponent<AudioListener>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = BgColor;
        cam.orthographic = true;

        // === EventSystem ===
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();

        // === Canvas ===
        var canvasGo = new GameObject("Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        var canvasRT = canvasGo.GetComponent<RectTransform>();

        // === Manager ===
        var managerGo = new GameObject("GameManager");
        var manager = managerGo.AddComponent<EmailSwiperManager>();
        manager.heartSprite = TryGetCircleSprite();

        // === Background (full canvas) ===
        var bg = AddImage(canvasRT, "Background", BgColor);
        Stretch(bg.rectTransform);
        bg.raycastTarget = false;

        // === Build sections ===
        var gmailRoot = BuildGmailSection(canvasRT, manager);
        var hudPanel = BuildHud(canvasRT, manager);
        var detailPanel = BuildDetailPanel(canvasRT, manager);
        var feedbackPanel = BuildFeedbackPanel(canvasRT, manager);
        var tutorialPanel = BuildTutorialPanel(canvasRT, manager);
        var resultPanel = BuildResultPanel(canvasRT, manager);

        // Commentator (grandma's reactive speech bubble in bottom-left)
        manager.commentator = BuildCommentator(canvasRT);

        manager.gmailRoot = gmailRoot;
        manager.hudPanel = hudPanel;
        manager.detailPanel = detailPanel;
        manager.feedbackPanel = feedbackPanel;
        manager.tutorialPanel = tutorialPanel;
        manager.resultPanel = resultPanel;

        gmailRoot.SetActive(false);
        hudPanel.SetActive(false);
        detailPanel.SetActive(false);
        feedbackPanel.SetActive(false);
        resultPanel.SetActive(false);
        tutorialPanel.SetActive(true);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings(ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[EmailSwiperBuilder] Built scene at {ScenePath}");
    }

    // =====================================================================
    // HUD (game UI - hearts, timer, score, streak)
    // =====================================================================

    private static GameObject BuildHud(RectTransform parent, EmailSwiperManager manager)
    {
        var hud = AddImage(parent, "HUD", HudBg);
        AnchorTopStretch(hud.rectTransform, height: 90);
        hud.raycastTarget = true;

        // === Hearts container (left) ===
        var heartsHolder = new GameObject("HeartsHolder", typeof(RectTransform));
        heartsHolder.transform.SetParent(hud.rectTransform, false);
        var hhrt = heartsHolder.GetComponent<RectTransform>();
        hhrt.anchorMin = new Vector2(0, 0);
        hhrt.anchorMax = new Vector2(0, 1);
        hhrt.pivot = new Vector2(0, 0.5f);
        hhrt.sizeDelta = new Vector2(360, 0);
        hhrt.anchoredPosition = new Vector2(40, 0);

        var hlg = heartsHolder.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.spacing = 8;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        // === Timer (center) ===
        var timerHolder = new GameObject("TimerHolder", typeof(RectTransform));
        timerHolder.transform.SetParent(hud.rectTransform, false);
        var thrt = timerHolder.GetComponent<RectTransform>();
        thrt.anchorMin = new Vector2(0.5f, 0);
        thrt.anchorMax = new Vector2(0.5f, 1);
        thrt.pivot = new Vector2(0.5f, 0.5f);
        thrt.sizeDelta = new Vector2(360, 0);
        thrt.anchoredPosition = Vector2.zero;

        var timerText = AddText(thrt, "TimerText", "1:30",
            44, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        var ttrt = timerText.rectTransform;
        ttrt.anchorMin = new Vector2(0, 0.5f);
        ttrt.anchorMax = new Vector2(1, 1);
        ttrt.offsetMin = Vector2.zero;
        ttrt.offsetMax = Vector2.zero;

        var barBg = AddImage(thrt, "TimerBarBg", new Color(1, 1, 1, 0.18f));
        var bbrt = barBg.rectTransform;
        bbrt.anchorMin = new Vector2(0, 0.05f);
        bbrt.anchorMax = new Vector2(1, 0.42f);
        bbrt.offsetMin = new Vector2(20, 0);
        bbrt.offsetMax = new Vector2(-20, 0);
        barBg.raycastTarget = false;

        var barFill = AddImage(barBg.rectTransform, "TimerFill", SafeGreen);
        Stretch(barFill.rectTransform);
        barFill.type = Image.Type.Filled;
        barFill.fillMethod = Image.FillMethod.Horizontal;
        barFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        barFill.fillAmount = 1f;
        barFill.raycastTarget = false;

        // === Score + streak (right) ===
        var scoreHolder = new GameObject("ScoreHolder", typeof(RectTransform));
        scoreHolder.transform.SetParent(hud.rectTransform, false);
        var shrt = scoreHolder.GetComponent<RectTransform>();
        shrt.anchorMin = new Vector2(1, 0);
        shrt.anchorMax = new Vector2(1, 1);
        shrt.pivot = new Vector2(1, 0.5f);
        shrt.sizeDelta = new Vector2(420, 0);
        shrt.anchoredPosition = new Vector2(-40, 0);

        var scoreText = AddText(shrt, "ScoreText", "Score: 0",
            42, ScoreColor, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
        var srrt = scoreText.rectTransform;
        srrt.anchorMin = new Vector2(0, 0.4f);
        srrt.anchorMax = new Vector2(1, 1);
        srrt.offsetMin = Vector2.zero;
        srrt.offsetMax = Vector2.zero;

        var streakText = AddText(shrt, "StreakText", "",
            26, StreakColor, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
        var skrt = streakText.rectTransform;
        skrt.anchorMin = new Vector2(0, 0);
        skrt.anchorMax = new Vector2(1, 0.4f);
        skrt.offsetMin = Vector2.zero;
        skrt.offsetMax = Vector2.zero;

        manager.heartsContainer = heartsHolder.transform;
        manager.timerText = timerText;
        manager.timerFill = barFill;
        manager.scoreText = scoreText;
        manager.streakText = streakText;

        return hud.gameObject;
    }

    // =====================================================================
    // Gmail section (top bar + sidebar + inbox)
    // =====================================================================

    private static GameObject BuildGmailSection(RectTransform parent, EmailSwiperManager manager)
    {
        var root = AddImage(parent, "GmailRoot", BgColor);
        var rrt = root.rectTransform;
        rrt.anchorMin = new Vector2(0, 0);
        rrt.anchorMax = new Vector2(1, 1);
        rrt.offsetMin = Vector2.zero;
        rrt.offsetMax = new Vector2(0, -90);
        root.raycastTarget = false;

        // === Top bar ===
        var topBar = AddImage(rrt, "TopBar", TopBarBg);
        AnchorTopStretch(topBar.rectTransform, height: 64);
        var topBarBorder = AddImage(topBar.rectTransform, "Border", BorderColor);
        var tbrt = topBarBorder.rectTransform;
        tbrt.anchorMin = new Vector2(0, 0);
        tbrt.anchorMax = new Vector2(1, 0);
        tbrt.pivot = new Vector2(0.5f, 0);
        tbrt.sizeDelta = new Vector2(0, 1);

        // Gmail logo (text)
        var logo = AddText(topBar.rectTransform, "Logo", "Gmail",
            34, GmailRed, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        var lrt = logo.rectTransform;
        lrt.anchorMin = new Vector2(0, 0);
        lrt.anchorMax = new Vector2(0, 1);
        lrt.pivot = new Vector2(0, 0.5f);
        lrt.sizeDelta = new Vector2(160, 0);
        lrt.anchoredPosition = new Vector2(40, 0);

        // Search bar (decorative, rounded)
        var search = AddImage(topBar.rectTransform, "SearchBar", SearchBg);
        var srt = search.rectTransform;
        srt.anchorMin = new Vector2(0.20f, 0.5f);
        srt.anchorMax = new Vector2(0.65f, 0.5f);
        srt.pivot = new Vector2(0.5f, 0.5f);
        srt.sizeDelta = new Vector2(0, 44);
        srt.anchoredPosition = Vector2.zero;
        var searchPlaceholder = AddText(search.rectTransform, "Placeholder",
            "    Search mail", 20, MutedText, TextAlignmentOptions.MidlineLeft);
        Stretch(searchPlaceholder.rectTransform);
        searchPlaceholder.rectTransform.offsetMin = new Vector2(20, 0);
        searchPlaceholder.raycastTarget = false;

        // Sidebar
        var sidebar = AddImage(rrt, "Sidebar", SidebarBg);
        var sbrt = sidebar.rectTransform;
        sbrt.anchorMin = new Vector2(0, 0);
        sbrt.anchorMax = new Vector2(0, 1);
        sbrt.pivot = new Vector2(0, 0.5f);
        sbrt.sizeDelta = new Vector2(220, 0);
        sbrt.offsetMax = new Vector2(220, -64);
        sbrt.offsetMin = new Vector2(0, 0);
        sidebar.raycastTarget = false;
        BuildSidebarContents(sidebar.rectTransform);

        // Inbox area
        var inboxArea = AddImage(rrt, "InboxArea", InboxBg);
        var iart = inboxArea.rectTransform;
        iart.anchorMin = new Vector2(0, 0);
        iart.anchorMax = new Vector2(1, 1);
        iart.offsetMin = new Vector2(220, 0);
        iart.offsetMax = new Vector2(0, -64);
        inboxArea.raycastTarget = false;

        // Inbox toolbar (count + tab indicator)
        var toolbar = AddImage(iart, "InboxToolbar", InboxBg);
        AnchorTopStretch(toolbar.rectTransform, height: 50);
        var toolbarBorder = AddImage(toolbar.rectTransform, "Border", BorderColor);
        var tbrt2 = toolbarBorder.rectTransform;
        tbrt2.anchorMin = new Vector2(0, 0);
        tbrt2.anchorMax = new Vector2(1, 0);
        tbrt2.pivot = new Vector2(0.5f, 0);
        tbrt2.sizeDelta = new Vector2(0, 1);
        toolbarBorder.raycastTarget = false;

        var countLabel = AddText(toolbar.rectTransform, "CountLabel", "8 unread",
            22, MutedText, TextAlignmentOptions.MidlineLeft);
        var clrt = countLabel.rectTransform;
        clrt.anchorMin = new Vector2(0, 0);
        clrt.anchorMax = new Vector2(0, 1);
        clrt.pivot = new Vector2(0, 0.5f);
        clrt.sizeDelta = new Vector2(300, 0);
        clrt.anchoredPosition = new Vector2(40, 0);
        countLabel.raycastTarget = false;

        var primaryTab = AddText(toolbar.rectTransform, "PrimaryTab", "Primary",
            22, GmailBlue, TextAlignmentOptions.Center, FontStyles.Bold);
        var ptrt = primaryTab.rectTransform;
        ptrt.anchorMin = new Vector2(0.5f, 0);
        ptrt.anchorMax = new Vector2(0.5f, 1);
        ptrt.pivot = new Vector2(0.5f, 0.5f);
        ptrt.sizeDelta = new Vector2(160, 0);
        ptrt.anchoredPosition = Vector2.zero;
        primaryTab.raycastTarget = false;

        var tabUnderline = AddImage(primaryTab.rectTransform, "Underline", GmailBlue);
        var tut = tabUnderline.rectTransform;
        tut.anchorMin = new Vector2(0, 0);
        tut.anchorMax = new Vector2(1, 0);
        tut.pivot = new Vector2(0.5f, 0);
        tut.sizeDelta = new Vector2(0, 3);
        tabUnderline.raycastTarget = false;

        // Inbox scroll view
        var scrollGo = new GameObject("InboxScroll", typeof(RectTransform));
        scrollGo.transform.SetParent(iart, false);
        var scrollRT = scrollGo.GetComponent<RectTransform>();
        scrollRT.anchorMin = new Vector2(0, 0);
        scrollRT.anchorMax = new Vector2(1, 1);
        scrollRT.offsetMin = Vector2.zero;
        scrollRT.offsetMax = new Vector2(0, -50);

        var scrollImg = scrollGo.AddComponent<Image>();
        scrollImg.color = InboxBg;
        scrollImg.raycastTarget = true;

        var scrollRect = scrollGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 30;

        var viewport = new GameObject("Viewport", typeof(RectTransform));
        viewport.transform.SetParent(scrollRT, false);
        var vrt = viewport.GetComponent<RectTransform>();
        Stretch(vrt);
        var vimg = viewport.AddComponent<Image>();
        vimg.color = InboxBg;
        vimg.raycastTarget = false;
        var vmask = viewport.AddComponent<Mask>();
        vmask.showMaskGraphic = false;

        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(vrt, false);
        var crt = content.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0, 1);
        crt.anchorMax = new Vector2(1, 1);
        crt.pivot = new Vector2(0.5f, 1);
        crt.sizeDelta = new Vector2(0, 0);
        crt.anchoredPosition = Vector2.zero;

        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        vlg.spacing = 0;
        vlg.padding = new RectOffset(0, 0, 0, 0);

        var fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = vrt;
        scrollRect.content = crt;

        manager.inboxContent = content.transform;
        manager.inboxCountLabel = countLabel;

        return root.gameObject;
    }

    private static void BuildSidebarContents(RectTransform sidebar)
    {
        // Compose button (decorative)
        var compose = AddImage(sidebar, "ComposeBtn", Hex("#C2E7FF"));
        var crt = compose.rectTransform;
        crt.anchorMin = new Vector2(0, 1);
        crt.anchorMax = new Vector2(1, 1);
        crt.pivot = new Vector2(0.5f, 1);
        crt.sizeDelta = new Vector2(-32, 56);
        crt.anchoredPosition = new Vector2(0, -16);
        var composeText = AddText(compose.rectTransform, "Label",
            "Compose", 22, DarkText, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(composeText.rectTransform);
        composeText.raycastTarget = false;

        // Folder list
        string[] folders = { "Inbox", "Starred", "Snoozed", "Sent", "Drafts", "More" };
        float startY = 100f;
        float rowHeight = 44f;
        for (int i = 0; i < folders.Length; i++)
        {
            bool selected = (i == 0);
            var row = AddImage(sidebar, "Folder_" + folders[i],
                selected ? InboxSelected : new Color(1, 1, 1, 0));
            var rrt = row.rectTransform;
            rrt.anchorMin = new Vector2(0, 1);
            rrt.anchorMax = new Vector2(1, 1);
            rrt.pivot = new Vector2(0.5f, 1);
            rrt.sizeDelta = new Vector2(-12, rowHeight);
            rrt.anchoredPosition = new Vector2(0, -(startY + i * rowHeight));
            row.raycastTarget = false;

            var lbl = AddText(row.rectTransform, "Label",
                folders[i], 20,
                selected ? GmailRed : DarkText,
                TextAlignmentOptions.MidlineLeft,
                selected ? FontStyles.Bold : FontStyles.Normal);
            Stretch(lbl.rectTransform);
            lbl.rectTransform.offsetMin = new Vector2(24, 0);
            lbl.raycastTarget = false;
        }
    }

    // =====================================================================
    // Detail panel (the opened email + decision buttons)
    // =====================================================================

    private static GameObject BuildDetailPanel(RectTransform parent, EmailSwiperManager manager)
    {
        var overlay = AddImage(parent, "DetailOverlay", new Color(0, 0, 0, 0.5f));
        var ort = overlay.rectTransform;
        ort.anchorMin = new Vector2(0, 0);
        ort.anchorMax = new Vector2(1, 1);
        ort.offsetMin = Vector2.zero;
        ort.offsetMax = new Vector2(0, -90);
        overlay.raycastTarget = true;

        var card = AddImage(ort, "Card", Color.white);
        var crt = card.rectTransform;
        crt.anchorMin = new Vector2(0.5f, 0.5f);
        crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(1500, 820);

        // === Top bar with Back button ===
        var topBar = AddImage(crt, "TopBar", Hex("#F8F9FA"));
        AnchorTopStretch(topBar.rectTransform, height: 60);
        var tbBorder = AddImage(topBar.rectTransform, "Border", BorderColor);
        var tbrt = tbBorder.rectTransform;
        tbrt.anchorMin = new Vector2(0, 0);
        tbrt.anchorMax = new Vector2(1, 0);
        tbrt.pivot = new Vector2(0.5f, 0);
        tbrt.sizeDelta = new Vector2(0, 1);
        tbBorder.raycastTarget = false;

        var back = AddButton(topBar.rectTransform, "BackBtn", "Back", 22,
            new Color(1, 1, 1, 0), DarkText);
        var brrt = back.GetComponent<RectTransform>();
        brrt.anchorMin = new Vector2(0, 0);
        brrt.anchorMax = new Vector2(0, 1);
        brrt.pivot = new Vector2(0, 0.5f);
        brrt.sizeDelta = new Vector2(120, 40);
        brrt.anchoredPosition = new Vector2(20, 0);
        UnityEventTools.AddPersistentListener(back.GetComponent<Button>().onClick, manager.OnBackPressed);

        // === Subject (big bold) ===
        var subject = AddText(crt, "Subject",
            "Subject goes here", 36, DarkText, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        var srt = subject.rectTransform;
        srt.anchorMin = new Vector2(0, 1);
        srt.anchorMax = new Vector2(1, 1);
        srt.pivot = new Vector2(0.5f, 1);
        srt.sizeDelta = new Vector2(-80, 60);
        srt.anchoredPosition = new Vector2(0, -90);

        // === Sender row (avatar + name + email + timestamp) ===
        var senderRow = new GameObject("SenderRow", typeof(RectTransform));
        senderRow.transform.SetParent(crt, false);
        var senrrt = senderRow.GetComponent<RectTransform>();
        senrrt.anchorMin = new Vector2(0, 1);
        senrrt.anchorMax = new Vector2(1, 1);
        senrrt.pivot = new Vector2(0.5f, 1);
        senrrt.sizeDelta = new Vector2(-80, 64);
        senrrt.anchoredPosition = new Vector2(0, -160);

        var avatar = AddImage(senrrt, "Avatar", Hex("#1A73E8"));
        avatar.sprite = TryGetCircleSprite();
        var avrt = avatar.rectTransform;
        avrt.anchorMin = new Vector2(0, 0.5f);
        avrt.anchorMax = new Vector2(0, 0.5f);
        avrt.pivot = new Vector2(0, 0.5f);
        avrt.sizeDelta = new Vector2(56, 56);
        avrt.anchoredPosition = Vector2.zero;
        avatar.raycastTarget = false;

        var avatarLetter = AddText(avatar.rectTransform, "Letter", "A",
            32, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(avatarLetter.rectTransform);
        avatarLetter.raycastTarget = false;

        var senderName = AddText(senrrt, "SenderName", "Sender Name",
            24, DarkText, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        var snrt = senderName.rectTransform;
        snrt.anchorMin = new Vector2(0, 0.5f);
        snrt.anchorMax = new Vector2(0, 1);
        snrt.pivot = new Vector2(0, 0.5f);
        snrt.sizeDelta = new Vector2(900, 0);
        snrt.anchoredPosition = new Vector2(80, -8);

        var senderEmail = AddText(senrrt, "SenderEmail", "<sender@example.com>",
            18, MutedText, TextAlignmentOptions.MidlineLeft);
        var sert = senderEmail.rectTransform;
        sert.anchorMin = new Vector2(0, 0);
        sert.anchorMax = new Vector2(0, 0.5f);
        sert.pivot = new Vector2(0, 0.5f);
        sert.sizeDelta = new Vector2(900, 0);
        sert.anchoredPosition = new Vector2(80, 8);

        var timestamp = AddText(senrrt, "Timestamp", "10:09 AM",
            18, MutedText, TextAlignmentOptions.MidlineRight);
        var trt = timestamp.rectTransform;
        trt.anchorMin = new Vector2(1, 0);
        trt.anchorMax = new Vector2(1, 1);
        trt.pivot = new Vector2(1, 0.5f);
        trt.sizeDelta = new Vector2(200, 0);
        trt.anchoredPosition = Vector2.zero;

        // === Body (scrollable area) ===
        var bodyScroll = new GameObject("BodyScroll", typeof(RectTransform));
        bodyScroll.transform.SetParent(crt, false);
        var bsrt = bodyScroll.GetComponent<RectTransform>();
        bsrt.anchorMin = new Vector2(0, 0);
        bsrt.anchorMax = new Vector2(1, 1);
        bsrt.offsetMin = new Vector2(40, 160);
        bsrt.offsetMax = new Vector2(-40, -250);

        var bodyImg = bodyScroll.AddComponent<Image>();
        bodyImg.color = Hex("#FAFBFC");
        var bodySr = bodyScroll.AddComponent<ScrollRect>();
        bodySr.horizontal = false;
        bodySr.vertical = true;

        var bodyVp = new GameObject("Viewport", typeof(RectTransform));
        bodyVp.transform.SetParent(bsrt, false);
        var bvrt = bodyVp.GetComponent<RectTransform>();
        Stretch(bvrt);
        var bvi = bodyVp.AddComponent<Image>();
        bvi.color = Hex("#FAFBFC");
        bvi.raycastTarget = false;
        var bvm = bodyVp.AddComponent<Mask>();
        bvm.showMaskGraphic = false;

        var bodyContent = new GameObject("Content", typeof(RectTransform));
        bodyContent.transform.SetParent(bvrt, false);
        var bcrt = bodyContent.GetComponent<RectTransform>();
        bcrt.anchorMin = new Vector2(0, 1);
        bcrt.anchorMax = new Vector2(1, 1);
        bcrt.pivot = new Vector2(0.5f, 1);
        bcrt.sizeDelta = new Vector2(0, 0);

        var body = AddText(bcrt, "Body",
            "Body text here.", 22, DarkText, TextAlignmentOptions.TopLeft);
        var brt2 = body.rectTransform;
        brt2.anchorMin = new Vector2(0, 1);
        brt2.anchorMax = new Vector2(1, 1);
        brt2.pivot = new Vector2(0.5f, 1);
        brt2.sizeDelta = new Vector2(-40, 600);
        brt2.anchoredPosition = new Vector2(0, -20);

        var bodyFitter = bodyContent.AddComponent<ContentSizeFitter>();
        bodyFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var bodyVlg = bodyContent.AddComponent<VerticalLayoutGroup>();
        bodyVlg.padding = new RectOffset(20, 20, 20, 20);
        bodyVlg.childForceExpandWidth = true;
        bodyVlg.childControlWidth = true;
        bodyVlg.childControlHeight = true;

        bodySr.viewport = bvrt;
        bodySr.content = bcrt;

        // === Decision bar (bottom) ===
        var decisionBar = new GameObject("DecisionBar", typeof(RectTransform));
        decisionBar.transform.SetParent(crt, false);
        var dbrt = decisionBar.GetComponent<RectTransform>();
        dbrt.anchorMin = new Vector2(0, 0);
        dbrt.anchorMax = new Vector2(1, 0);
        dbrt.pivot = new Vector2(0.5f, 0);
        dbrt.sizeDelta = new Vector2(0, 100);
        dbrt.anchoredPosition = new Vector2(0, 0);

        var scamBtn = AddButton(dbrt, "ScamBtn", "SCAM!", 36, ScamRed, Color.white);
        var sbrt = scamBtn.GetComponent<RectTransform>();
        sbrt.anchorMin = new Vector2(0.5f, 0.5f);
        sbrt.anchorMax = new Vector2(0.5f, 0.5f);
        sbrt.pivot = new Vector2(1, 0.5f);
        sbrt.sizeDelta = new Vector2(360, 80);
        sbrt.anchoredPosition = new Vector2(-20, 0);
        UnityEventTools.AddPersistentListener(scamBtn.GetComponent<Button>().onClick, manager.OnScamPressed);

        var safeBtn = AddButton(dbrt, "SafeBtn", "SAFE", 36, SafeGreen, Color.white);
        var sfrt = safeBtn.GetComponent<RectTransform>();
        sfrt.anchorMin = new Vector2(0.5f, 0.5f);
        sfrt.anchorMax = new Vector2(0.5f, 0.5f);
        sfrt.pivot = new Vector2(0, 0.5f);
        sfrt.sizeDelta = new Vector2(360, 80);
        sfrt.anchoredPosition = new Vector2(20, 0);
        UnityEventTools.AddPersistentListener(safeBtn.GetComponent<Button>().onClick, manager.OnSafePressed);

        manager.detailAvatarBg = avatar;
        manager.detailAvatarLetter = avatarLetter;
        manager.detailSenderName = senderName;
        manager.detailSenderEmail = senderEmail;
        manager.detailTimestamp = timestamp;
        manager.detailSubject = subject;
        manager.detailBody = body;

        return overlay.gameObject;
    }

    // =====================================================================
    // Tutorial panel
    // =====================================================================

    private static GameObject BuildTutorialPanel(RectTransform parent, EmailSwiperManager manager)
    {
        var overlay = AddImage(parent, "TutorialOverlay", new Color(0, 0, 0, 0.65f));
        Stretch(overlay.rectTransform);
        overlay.raycastTarget = true;

        var card = AddImage(overlay.rectTransform, "Card", Color.white);
        var crt = card.rectTransform;
        crt.anchorMin = new Vector2(0.5f, 0.5f);
        crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(900, 600);

        var header = AddImage(crt, "Header", GmailBlue);
        AnchorTopStretch(header.rectTransform, height: 100);
        var headerLabel = AddText(header.rectTransform, "HeaderLabel",
            "Phish Patrol", 48, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(headerLabel.rectTransform);
        headerLabel.raycastTarget = false;

        var body = AddText(crt, "Body",
            "Sort the scam emails from the real ones.\n\n" +
            "Click each email to open it, then decide: <b>SCAM</b> or <b>SAFE</b>.\n\n" +
            "<b>5 lives</b> - wrong answers cost a life\n" +
            "<b>90 seconds</b> on the clock\n" +
            "<b>Streak bonus</b> for 3+ correct in a row",
            28, DarkText, TextAlignmentOptions.Center);
        var brt = body.rectTransform;
        brt.anchorMin = new Vector2(0, 0);
        brt.anchorMax = new Vector2(1, 1);
        brt.offsetMin = new Vector2(60, 140);
        brt.offsetMax = new Vector2(-60, -120);

        var start = AddButton(crt, "StartBtn", "Start Game", 32, SafeGreen, Color.white);
        var srt = start.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0.5f, 0);
        srt.anchorMax = new Vector2(0.5f, 0);
        srt.pivot = new Vector2(0.5f, 0);
        srt.sizeDelta = new Vector2(320, 80);
        srt.anchoredPosition = new Vector2(0, 40);
        UnityEventTools.AddPersistentListener(start.GetComponent<Button>().onClick, manager.OnTutorialStart);

        return overlay.gameObject;
    }

    // =====================================================================
    // Feedback overlay
    // =====================================================================

    private static GameObject BuildFeedbackPanel(RectTransform parent, EmailSwiperManager manager)
    {
        var overlay = AddImage(parent, "FeedbackOverlay", new Color(0, 0, 0, 0.5f));
        Stretch(overlay.rectTransform);
        overlay.raycastTarget = true;

        var card = AddImage(overlay.rectTransform, "Card", SafeGreen);
        var crt = card.rectTransform;
        crt.anchorMin = new Vector2(0.5f, 0.5f);
        crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(1100, 480);

        var title = AddText(crt, "Title", "Correct!",
            64, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        var trt = title.rectTransform;
        trt.anchorMin = new Vector2(0, 1);
        trt.anchorMax = new Vector2(1, 1);
        trt.pivot = new Vector2(0.5f, 1);
        trt.sizeDelta = new Vector2(-60, 100);
        trt.anchoredPosition = new Vector2(0, -50);

        var body = AddText(crt, "Body",
            "Explanation goes here", 28, Color.white, TextAlignmentOptions.Center);
        var brt = body.rectTransform;
        brt.anchorMin = new Vector2(0, 0);
        brt.anchorMax = new Vector2(1, 1);
        brt.offsetMin = new Vector2(60, 50);
        brt.offsetMax = new Vector2(-60, -160);

        manager.feedbackBg = card;
        manager.feedbackTitle = title;
        manager.feedbackBody = body;

        return overlay.gameObject;
    }

    // =====================================================================
    // Result panel (end of game)
    // =====================================================================

    private static GameObject BuildResultPanel(RectTransform parent, EmailSwiperManager manager)
    {
        var overlay = AddImage(parent, "ResultOverlay", new Color(0, 0, 0, 0.7f));
        Stretch(overlay.rectTransform);
        overlay.raycastTarget = true;

        var card = AddImage(overlay.rectTransform, "Card", Color.white);
        var crt = card.rectTransform;
        crt.anchorMin = new Vector2(0.5f, 0.5f);
        crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(900, 700);

        var header = AddImage(crt, "Header", GmailBlue);
        AnchorTopStretch(header.rectTransform, height: 100);
        var hLabel = AddText(header.rectTransform, "HLabel",
            "Round Complete!", 44, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(hLabel.rectTransform);
        hLabel.raycastTarget = false;

        var stars = AddText(crt, "Stars", "* * *",
            72, ScoreColor, TextAlignmentOptions.Center, FontStyles.Bold);
        var srt = stars.rectTransform;
        srt.anchorMin = new Vector2(0, 1);
        srt.anchorMax = new Vector2(1, 1);
        srt.pivot = new Vector2(0.5f, 1);
        srt.sizeDelta = new Vector2(0, 100);
        srt.anchoredPosition = new Vector2(0, -140);

        var score = AddText(crt, "ScoreText", "0 / 800",
            48, DarkText, TextAlignmentOptions.Center, FontStyles.Bold);
        var scrt = score.rectTransform;
        scrt.anchorMin = new Vector2(0, 1);
        scrt.anchorMax = new Vector2(1, 1);
        scrt.pivot = new Vector2(0.5f, 1);
        scrt.sizeDelta = new Vector2(0, 80);
        scrt.anchoredPosition = new Vector2(0, -260);

        var msg = AddText(crt, "Message",
            "Result message", 24, MutedText, TextAlignmentOptions.Center);
        var mrt = msg.rectTransform;
        mrt.anchorMin = new Vector2(0, 0);
        mrt.anchorMax = new Vector2(1, 1);
        mrt.offsetMin = new Vector2(60, 160);
        mrt.offsetMax = new Vector2(-60, -360);

        var playAgain = AddButton(crt, "PlayAgainBtn",
            "Play Again", 28, SafeGreen, Color.white);
        var part = playAgain.GetComponent<RectTransform>();
        part.anchorMin = new Vector2(0.5f, 0);
        part.anchorMax = new Vector2(0.5f, 0);
        part.pivot = new Vector2(1, 0);
        part.sizeDelta = new Vector2(280, 70);
        part.anchoredPosition = new Vector2(-20, 50);
        UnityEventTools.AddPersistentListener(playAgain.GetComponent<Button>().onClick, manager.OnPlayAgain);

        var backToMap = AddButton(crt, "BackBtn",
            "Back to Map", 28, Hex("#7F8C8D"), Color.white);
        var bart = backToMap.GetComponent<RectTransform>();
        bart.anchorMin = new Vector2(0.5f, 0);
        bart.anchorMax = new Vector2(0.5f, 0);
        bart.pivot = new Vector2(0, 0);
        bart.sizeDelta = new Vector2(280, 70);
        bart.anchoredPosition = new Vector2(20, 50);
        UnityEventTools.AddPersistentListener(backToMap.GetComponent<Button>().onClick, manager.OnReturnToMap);

        manager.resultStars = stars;
        manager.resultScore = score;
        manager.resultMessage = msg;

        return overlay.gameObject;
    }

    // =====================================================================
    // Commentator (reactive speech bubble — bottom-left)
    // =====================================================================

    private static Commentator BuildCommentator(RectTransform parent)
    {
        var root = new GameObject("CommentatorPanel", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        var rrt = root.GetComponent<RectTransform>();
        rrt.anchorMin = new Vector2(0, 0);
        rrt.anchorMax = new Vector2(0, 0);
        rrt.pivot = new Vector2(0, 0);
        rrt.sizeDelta = new Vector2(620, 140);
        rrt.anchoredPosition = new Vector2(30, 30);

        // Portrait (pink circle with "G")
        var portrait = AddImage(rrt, "Portrait", new Color(1f, 0.72f, 0.78f));
        portrait.sprite = TryGetCircleSprite();
        portrait.preserveAspect = true;
        var prt = portrait.rectTransform;
        prt.anchorMin = new Vector2(0, 0);
        prt.anchorMax = new Vector2(0, 1);
        prt.pivot = new Vector2(0, 0.5f);
        prt.sizeDelta = new Vector2(120, 0);
        prt.anchoredPosition = Vector2.zero;

        var letter = AddText(portrait.rectTransform, "Letter", "G",
            64, new Color(0.20f, 0.10f, 0.18f),
            TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(letter.rectTransform);

        // Bubble background
        var bubble = AddImage(rrt, "Bubble", Color.white);
        var brt = bubble.rectTransform;
        brt.anchorMin = new Vector2(0, 0);
        brt.anchorMax = new Vector2(1, 1);
        brt.pivot = new Vector2(0, 0.5f);
        brt.offsetMin = new Vector2(140, 0);
        brt.offsetMax = new Vector2(0, 0);

        // Speaker label
        var speakerLabel = AddText(bubble.rectTransform, "SpeakerLabel",
            "Grandma", 18,
            new Color(0.78f, 0.30f, 0.50f),
            TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        var slrt = speakerLabel.rectTransform;
        slrt.anchorMin = new Vector2(0, 1);
        slrt.anchorMax = new Vector2(1, 1);
        slrt.pivot = new Vector2(0, 1);
        slrt.sizeDelta = new Vector2(0, 26);
        slrt.anchoredPosition = new Vector2(20, -8);

        // Bubble text
        var bubbleText = AddText(bubble.rectTransform, "BubbleText",
            "...", 22,
            new Color(0.13f, 0.13f, 0.13f),
            TextAlignmentOptions.MidlineLeft);
        bubbleText.textWrappingMode = TextWrappingModes.Normal;
        var btrt = bubbleText.rectTransform;
        btrt.anchorMin = new Vector2(0, 0);
        btrt.anchorMax = new Vector2(1, 1);
        btrt.offsetMin = new Vector2(20, 8);
        btrt.offsetMax = new Vector2(-20, -32);

        // Wire Commentator component
        var comm = root.AddComponent<Commentator>();
        comm.root = root;
        comm.portrait = portrait;
        comm.portraitLetter = letter;
        comm.bubbleBg = bubble;
        comm.bubbleText = bubbleText;
        comm.speakerLabel = speakerLabel;
        comm.portraitSprite = TryGetCircleSprite();
        comm.speakerName = "Grandma";
        comm.portraitInitial = "G";
        comm.portraitColor = new Color(1f, 0.72f, 0.78f);

        return comm;
    }

    // =====================================================================
    // Build settings
    // =====================================================================

    private static void AddSceneToBuildSettings(string path)
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(s => s.path == path))
        {
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }

    // =====================================================================
    // Helpers
    // =====================================================================

    private static Image AddImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    private static TMP_Text AddText(Transform parent, string name, string content,
        int fontSize, Color color, TextAlignmentOptions align,
        FontStyles style = FontStyles.Normal)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = content;
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = align;
        t.fontStyle = style;
        t.raycastTarget = false;
        return t;
    }

    private static GameObject AddButton(Transform parent, string name, string label,
        int fontSize, Color bg, Color textColor)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = bg;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var t = AddText(go.transform, "Label", label, fontSize, textColor,
            TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(t.rectTransform);
        return go;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void AnchorTopStretch(RectTransform rt, float height, float topInset = 0)
    {
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.sizeDelta = new Vector2(0, height);
        rt.anchoredPosition = new Vector2(0, -topInset);
    }

    private static Color Hex(string hex)
    {
        return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
    }

    private static Sprite TryGetCircleSprite()
    {
        try
        {
            var s = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            if (s != null) return s;
        }
        catch { }
        return null;
    }
}