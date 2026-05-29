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
/// Builds the Reigns-style Email Swiper scene with bottom bucket.
///
/// Run via:  Phisherman ▸ Build Email Swiper Scene
/// Output:   Assets/Scenes/EmailSwiper.unity
/// </summary>
public static class EmailSwiperBuilder
{
    private const string ScenesDir = "Assets/Scenes";
    private const string ScenePath = "Assets/Scenes/EmailSwiper.unity";

    // =====================================================================
    // Palette — ocean / fishing, cute & vibrant
    // =====================================================================
    private static readonly Color BgDeep = Hex("#0F2027");
    private static readonly Color BgOcean = Hex("#1B3A4B");
    private static readonly Color BgWave = Hex("#274156");
    private static readonly Color HudBg = Hex("#0D1B2A");
    private static readonly Color ScoreGold = Hex("#FFD93D");
    private static readonly Color StreakOrange = Hex("#FF9F1C");
    private static readonly Color CardWhite = Hex("#FFFDF7");
    private static readonly Color CardShadow = new Color(0, 0, 0, 0.18f);
    private static readonly Color CardBorder = new Color(1, 1, 1, 0f);
    private static readonly Color ScamRed = Hex("#FF6B6B");
    private static readonly Color SafeTeal = Hex("#4ECDC4");
    private static readonly Color BucketBody = Hex("#2D3436");
    private static readonly Color BucketInner = Hex("#1A1A2E");
    private static readonly Color WaterBlue = new Color(0.27f, 0.62f, 0.83f, 0.55f);
    private static readonly Color BucketRim = Hex("#636E72");
    private static readonly Color CrackColor = Hex("#FDCB6E");
    private static readonly Color DarkText = Hex("#1A1A2E");
    private static readonly Color MutedText = Hex("#636E72");
    private static readonly Color LightText = Hex("#DFE6E9");
    private static readonly Color Coral = Hex("#FF7675");
    private static readonly Color Aqua = Hex("#81ECEC");

    // =====================================================================
    // Build
    // =====================================================================

    [MenuItem("Phisherman/Build Email Swiper Scene")]
    public static void Build()
    {
        if (!Directory.Exists(ScenesDir)) Directory.CreateDirectory(ScenesDir);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Camera
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        camGo.AddComponent<AudioListener>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = BgDeep;
        cam.orthographic = true;

        // EventSystem
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();

        // Canvas
        var canvasGo = new GameObject("Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        var canvasRT = canvasGo.GetComponent<RectTransform>();

        // Manager
        var mgrGo = new GameObject("GameManager");
        var mgr = mgrGo.AddComponent<EmailSwiperManager>();
        mgr.circleSprite = Circle();

        // ── Background layers ──
        var bgB = Img(canvasRT, "BgBottom", BgDeep); Stretch(bgB.rectTransform); bgB.raycastTarget = false;
        var bgM = Img(canvasRT, "BgMid", new Color(BgOcean.r, BgOcean.g, BgOcean.b, 0.7f));
        var mrt = bgM.rectTransform; mrt.anchorMin = new Vector2(0, 0.15f); mrt.anchorMax = Vector2.one;
        mrt.offsetMin = mrt.offsetMax = Vector2.zero; bgM.raycastTarget = false;
        var bgW = Img(canvasRT, "BgWave", new Color(BgWave.r, BgWave.g, BgWave.b, 0.35f));
        var wrt = bgW.rectTransform; wrt.anchorMin = new Vector2(0, 0.55f); wrt.anchorMax = Vector2.one;
        wrt.offsetMin = wrt.offsetMax = Vector2.zero; bgW.raycastTarget = false;

        // ── HUD ──
        var hud = BuildHud(canvasRT, mgr);

        // ── Game root (below HUD, above bucket) ──
        var gameRootImg = Img(canvasRT, "GameRoot", new Color(0, 0, 0, 0));
        var grrt = gameRootImg.rectTransform;
        grrt.anchorMin = Vector2.zero; grrt.anchorMax = Vector2.one;
        grrt.offsetMin = new Vector2(0, 220); // leave room for bucket
        grrt.offsetMax = new Vector2(0, -90); // below HUD
        gameRootImg.raycastTarget = false;

        // ── Swipe hints ──
        CanvasGroup scamHint, safeHint;
        BuildSwipeHints(grrt, out scamHint, out safeHint);

        // ── Email card + drag overlay ──
        BuildCard(grrt, mgr, scamHint, safeHint);

        // ── Bottom bucket ──
        BuildBucket(canvasRT, mgr);

        // ── Fish animation sprite ──
        BuildFishAnim(canvasRT, mgr);

        // ── Score popup ──
        BuildScorePopup(canvasRT, mgr);

        // ── Commentator ──
        mgr.commentator = BuildCommentator(canvasRT);

        // ── Overlays ──
        var feedback = BuildFeedbackPanel(canvasRT, mgr);
        var tutorial = BuildTutorialPanel(canvasRT, mgr);
        var result = BuildResultPanel(canvasRT, mgr);

        mgr.gameRoot = gameRootImg.gameObject;
        mgr.hudPanel = hud;
        mgr.feedbackPanel = feedback;
        mgr.tutorialPanel = tutorial;
        mgr.resultPanel = result;

        gameRootImg.gameObject.SetActive(false);
        hud.SetActive(false);
        feedback.SetActive(false);
        result.SetActive(false);
        tutorial.SetActive(true);

        // Save
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuild(ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[EmailSwiperBuilder] Built → {ScenePath}");
    }

    // =====================================================================
    // HUD
    // =====================================================================

    static GameObject BuildHud(RectTransform parent, EmailSwiperManager mgr)
    {
        var hud = Img(parent, "HUD", HudBg);
        TopStretch(hud.rectTransform, 90);
        hud.raycastTarget = true;

        // Timer (centre)
        var tH = RT("TimerHolder", hud.rectTransform);
        var thr = tH.GetComponent<RectTransform>();
        thr.anchorMin = new Vector2(0.5f, 0); thr.anchorMax = new Vector2(0.5f, 1);
        thr.pivot = new Vector2(0.5f, 0.5f); thr.sizeDelta = new Vector2(340, 0);

        var timerTxt = Txt(thr, "TimerText", "1:30", 42, Color.white,
            TextAlignmentOptions.Center, FontStyles.Bold);
        var ttrt = timerTxt.rectTransform;
        ttrt.anchorMin = new Vector2(0, 0.5f); ttrt.anchorMax = Vector2.one;
        ttrt.offsetMin = ttrt.offsetMax = Vector2.zero;

        var barBg = Img(thr, "TimerBarBg", new Color(1, 1, 1, 0.15f));
        var bbrt = barBg.rectTransform;
        bbrt.anchorMin = new Vector2(0, 0.08f); bbrt.anchorMax = new Vector2(1, 0.40f);
        bbrt.offsetMin = new Vector2(16, 0); bbrt.offsetMax = new Vector2(-16, 0);
        barBg.raycastTarget = false;

        var fill = Img(bbrt, "TimerFill", SafeTeal);
        Stretch(fill.rectTransform);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0; fill.fillAmount = 1f; fill.raycastTarget = false;

        var progTxt = Txt(thr, "Progress", "1 / 8", 20, LightText,
            TextAlignmentOptions.Center);
        var prt = progTxt.rectTransform;
        prt.anchorMin = Vector2.zero; prt.anchorMax = new Vector2(1, 0.22f);
        prt.offsetMin = prt.offsetMax = Vector2.zero;

        // Score (right)
        var sH = RT("ScoreHolder", hud.rectTransform);
        var shr = sH.GetComponent<RectTransform>();
        shr.anchorMin = new Vector2(1, 0); shr.anchorMax = new Vector2(1, 1);
        shr.pivot = new Vector2(1, 0.5f);
        shr.sizeDelta = new Vector2(380, 0); shr.anchoredPosition = new Vector2(-30, 0);

        var scoreTxt = Txt(shr, "ScoreText", "Score: 0", 38, ScoreGold,
            TextAlignmentOptions.MidlineRight, FontStyles.Bold);
        var srrt = scoreTxt.rectTransform;
        srrt.anchorMin = new Vector2(0, 0.4f); srrt.anchorMax = Vector2.one;
        srrt.offsetMin = srrt.offsetMax = Vector2.zero;

        var streakTxt = Txt(shr, "StreakText", "", 24, StreakOrange,
            TextAlignmentOptions.MidlineRight, FontStyles.Bold);
        var skrt = streakTxt.rectTransform;
        skrt.anchorMin = Vector2.zero; skrt.anchorMax = new Vector2(1, 0.4f);
        skrt.offsetMin = skrt.offsetMax = Vector2.zero;

        mgr.timerText = timerTxt;
        mgr.timerFill = fill;
        mgr.scoreText = scoreTxt;
        mgr.streakText = streakTxt;
        mgr.progressText = progTxt;

        return hud.gameObject;
    }

    // =====================================================================
    // Swipe hints (SCAM left, SAFE right)
    // =====================================================================

    static void BuildSwipeHints(RectTransform parent,
        out CanvasGroup scamCG, out CanvasGroup safeCG)
    {
        scamCG = MakeHintLabel(parent, "ScamHint", "◀  SCAM", ScamRed, 0.12f);
        safeCG = MakeHintLabel(parent, "SafeHint", "SAFE  ▶", SafeTeal, 0.88f);
    }

    static CanvasGroup MakeHintLabel(RectTransform parent, string name,
        string text, Color color, float xAnch)
    {
        var go = RT(name, parent);
        var rrt = go.GetComponent<RectTransform>();
        rrt.anchorMin = new Vector2(xAnch, 0.42f);
        rrt.anchorMax = new Vector2(xAnch, 0.58f);
        rrt.pivot = new Vector2(0.5f, 0.5f);
        rrt.sizeDelta = new Vector2(240, 72);

        var img = go.AddComponent<Image>();
        img.color = new Color(color.r, color.g, color.b, 0.25f);
        img.raycastTarget = false;

        var lbl = Txt(rrt, "Label", text, 32, color,
            TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(lbl.rectTransform);

        var cg = go.AddComponent<CanvasGroup>();
        cg.alpha = 0f; cg.blocksRaycasts = false;
        return cg;
    }

    // =====================================================================
    // Email card + transparent drag overlay
    // =====================================================================

    static void BuildCard(RectTransform parent, EmailSwiperManager mgr,
        CanvasGroup scamHint, CanvasGroup safeHint)
    {
        // -- Shadow --
        var shadow = Img(parent, "CardShadow", CardShadow);
        var shrt = shadow.rectTransform;
        shrt.anchorMin = shrt.anchorMax = new Vector2(0.5f, 0.52f);
        shrt.pivot = new Vector2(0.5f, 0.5f);
        shrt.sizeDelta = new Vector2(620, 700);
        shrt.anchoredPosition = new Vector2(6, -6);
        shadow.raycastTarget = false;

        // -- Border / glow --
        var border = Img(parent, "CardBorder", CardBorder);
        var brrt = border.rectTransform;
        brrt.anchorMin = brrt.anchorMax = new Vector2(0.5f, 0.52f);
        brrt.pivot = new Vector2(0.5f, 0.5f);
        brrt.sizeDelta = new Vector2(626, 706);
        border.raycastTarget = false;

        // -- Card visual (cardRoot) --
        var card = Img(parent, "EmailCard", CardWhite);
        var crt = card.rectTransform;
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.52f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(600, 680);
        card.raycastTarget = false;          // overlay handles input

        var cardCG = card.gameObject.AddComponent<CanvasGroup>();

        // Avatar
        var avatar = Img(crt, "Avatar", Hex("#1A73E8"));
        avatar.sprite = Circle();
        var avrt = avatar.rectTransform;
        avrt.anchorMin = avrt.anchorMax = new Vector2(0, 1);
        avrt.pivot = new Vector2(0, 1);
        avrt.sizeDelta = new Vector2(60, 60);
        avrt.anchoredPosition = new Vector2(28, -24);
        avatar.raycastTarget = false;

        var avatarLetter = Txt(avrt, "Letter", "P", 34, Color.white,
            TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(avatarLetter.rectTransform);

        // Sender name
        var senderName = Txt(crt, "SenderName", "Sender", 24, DarkText,
            TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        var snrt = senderName.rectTransform;
        snrt.anchorMin = new Vector2(0, 1); snrt.anchorMax = new Vector2(1, 1);
        snrt.pivot = new Vector2(0, 1);
        snrt.sizeDelta = new Vector2(-130, 32);
        snrt.anchoredPosition = new Vector2(100, -24);

        // Sender email
        var senderEmail = Txt(crt, "SenderEmail", "sender@example.com", 19, MutedText,
            TextAlignmentOptions.MidlineLeft);
        var sert = senderEmail.rectTransform;
        sert.anchorMin = new Vector2(0, 1); sert.anchorMax = new Vector2(1, 1);
        sert.pivot = new Vector2(0, 1);
        sert.sizeDelta = new Vector2(-130, 26);
        sert.anchoredPosition = new Vector2(100, -58);

        // Divider
        var div = Img(crt, "Divider", Hex("#E0E3E8"));
        var dvrt = div.rectTransform;
        dvrt.anchorMin = new Vector2(0, 1); dvrt.anchorMax = new Vector2(1, 1);
        dvrt.pivot = new Vector2(0.5f, 1);
        dvrt.sizeDelta = new Vector2(-50, 2);
        dvrt.anchoredPosition = new Vector2(0, -100);
        div.raycastTarget = false;

        // Subject
        var subject = Txt(crt, "Subject", "Subject", 26, DarkText,
            TextAlignmentOptions.TopLeft, FontStyles.Bold);
        var sjrt = subject.rectTransform;
        sjrt.anchorMin = new Vector2(0, 1); sjrt.anchorMax = new Vector2(1, 1);
        sjrt.pivot = new Vector2(0, 1);
        sjrt.sizeDelta = new Vector2(-50, 60);
        sjrt.anchoredPosition = new Vector2(25, -115);

        // Body — NO ScrollRect! Just plain text that clips.
        var body = Txt(crt, "Body", "Body text…", 21, DarkText,
            TextAlignmentOptions.TopLeft);
        body.textWrappingMode = TextWrappingModes.Normal;
        body.overflowMode = TextOverflowModes.Ellipsis;
        var brt = body.rectTransform;
        brt.anchorMin = new Vector2(0, 0); brt.anchorMax = new Vector2(1, 1);
        brt.offsetMin = new Vector2(25, 25);
        brt.offsetMax = new Vector2(-25, -185);

        // == DRAG OVERLAY (sits ON TOP of the card, catches all input) ==
        var overlay = Img(parent, "DragOverlay", new Color(0, 0, 0, 0));
        var olrt = overlay.rectTransform;
        olrt.anchorMin = olrt.anchorMax = new Vector2(0.5f, 0.52f);
        olrt.pivot = new Vector2(0.5f, 0.5f);
        olrt.sizeDelta = new Vector2(620, 700);
        overlay.raycastTarget = true;

        // SwipeCard on the overlay
        var swipe = overlay.gameObject.AddComponent<SwipeCard>();
        swipe.cardRoot = crt;
        swipe.scamIndicator = scamHint;
        swipe.safeIndicator = safeHint;
        swipe.cardBorder = border;

        // Wire to manager
        mgr.swipeCard = swipe;
        mgr.cardSender = senderName;
        mgr.cardEmail = senderEmail;
        mgr.cardSubject = subject;
        mgr.cardBody = body;
        mgr.cardAvatar = avatar;
        mgr.cardAvatarLetter = avatarLetter;
        mgr.cardCanvasGroup = cardCG;
    }

    // =====================================================================
    // Bottom bucket
    // =====================================================================

    static void BuildBucket(RectTransform parent, EmailSwiperManager mgr)
    {
        // Bucket root — bottom 220px
        var root = Img(parent, "BucketRoot", new Color(0, 0, 0, 0));
        var rrt = root.rectTransform;
        rrt.anchorMin = new Vector2(0, 0); rrt.anchorMax = new Vector2(1, 0);
        rrt.pivot = new Vector2(0.5f, 0);
        rrt.sizeDelta = new Vector2(0, 220);
        root.raycastTarget = false;

        // Bucket body (dark)
        var body = Img(rrt, "BucketBody", BucketBody);
        var brt = body.rectTransform;
        brt.anchorMin = new Vector2(0.1f, 0); brt.anchorMax = new Vector2(0.9f, 0.75f);
        brt.offsetMin = brt.offsetMax = Vector2.zero;
        body.raycastTarget = false;

        // Inner (slightly lighter)
        var inner = Img(brt, "Inner", BucketInner);
        var irt = inner.rectTransform;
        irt.anchorMin = new Vector2(0.02f, 0.05f); irt.anchorMax = new Vector2(0.98f, 0.95f);
        irt.offsetMin = irt.offsetMax = Vector2.zero;
        inner.raycastTarget = false;

        // Water fill (rises with caught fish, changes colour with damage)
        var water = Img(inner.rectTransform, "WaterFill", WaterBlue);
        var wrt2 = water.rectTransform;
        wrt2.anchorMin = Vector2.zero; wrt2.anchorMax = new Vector2(1, 0.6f);
        wrt2.offsetMin = wrt2.offsetMax = Vector2.zero;
        water.raycastTarget = false;

        // Fish container (swimming fish go here)
        var fishCont = RT("FishContainer", inner.rectTransform);
        Stretch(fishCont.GetComponent<RectTransform>());

        // Bucket rim (top edge)
        var rim = Img(rrt, "Rim", BucketRim);
        var rmrt = rim.rectTransform;
        rmrt.anchorMin = new Vector2(0.08f, 0.72f); rmrt.anchorMax = new Vector2(0.92f, 0.82f);
        rmrt.offsetMin = rmrt.offsetMax = Vector2.zero;
        rim.raycastTarget = false;

        // Bucket label
        var label = Txt(rrt, "BucketLabel", "0 caught  ·  0/5 cracks", 22, LightText,
            TextAlignmentOptions.Center);
        var lrt = label.rectTransform;
        lrt.anchorMin = new Vector2(0, 0.82f); lrt.anchorMax = new Vector2(1, 1);
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;

        // Crack container + 5 crack slots
        var crackCont = RT("CrackContainer", brt);
        Stretch(crackCont.GetComponent<RectTransform>());
        var slots = new RectTransform[5];
        float[] xPositions = { 0.10f, 0.30f, 0.50f, 0.70f, 0.90f };
        float[] yPositions = { 0.30f, 0.60f, 0.40f, 0.70f, 0.25f };
        for (int i = 0; i < 5; i++)
        {
            var crack = Img(crackCont.transform, "Crack_" + i, CrackColor);
            crack.sprite = Circle(); // placeholder — swap with crackSprite PNG
            var ccrt = crack.rectTransform;
            ccrt.anchorMin = ccrt.anchorMax = new Vector2(xPositions[i], yPositions[i]);
            ccrt.pivot = new Vector2(0.5f, 0.5f);
            ccrt.sizeDelta = new Vector2(40, 40);
            crack.raycastTarget = false;
            crack.gameObject.SetActive(false); // hidden until damage
            slots[i] = ccrt;
        }

        mgr.bucketRoot = rrt;
        mgr.bucketBodyImage = body;
        mgr.waterFill = water;
        mgr.fishContainer = fishCont.GetComponent<RectTransform>();
        mgr.crackContainer = crackCont.transform;
        mgr.crackSlots = slots;
        mgr.bucketLabel = label;
    }

    // =====================================================================
    // Fish animation sprite
    // =====================================================================

    static void BuildFishAnim(RectTransform parent, EmailSwiperManager mgr)
    {
        var go = RT("FishAnim", parent);
        var rrt = go.GetComponent<RectTransform>();
        rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 0.5f);
        rrt.pivot = new Vector2(0.5f, 0.5f);
        rrt.sizeDelta = new Vector2(72, 72);

        var img = go.AddComponent<Image>();
        img.sprite = Circle();
        img.preserveAspect = true;
        img.color = Coral;
        img.raycastTarget = false;

        // Simple tail
        var tail = Img(rrt, "Tail", Coral);
        var trt = tail.rectTransform;
        trt.anchorMin = trt.anchorMax = new Vector2(1, 0.5f);
        trt.pivot = new Vector2(0, 0.5f);
        trt.sizeDelta = new Vector2(26, 34);
        tail.raycastTarget = false;

        // Eye
        var eye = Img(rrt, "Eye", Color.white);
        eye.sprite = Circle();
        var ert = eye.rectTransform;
        ert.anchorMin = ert.anchorMax = new Vector2(0.28f, 0.62f);
        ert.pivot = new Vector2(0.5f, 0.5f);
        ert.sizeDelta = new Vector2(14, 14);
        eye.raycastTarget = false;

        var pupil = Img(ert, "Pupil", DarkText);
        pupil.sprite = Circle();
        var purt = pupil.rectTransform;
        purt.anchorMin = purt.anchorMax = new Vector2(0.5f, 0.5f);
        purt.pivot = new Vector2(0.5f, 0.5f);
        purt.sizeDelta = new Vector2(7, 7);
        pupil.raycastTarget = false;

        go.SetActive(false);
        mgr.fishAnimRT = rrt;
        mgr.fishAnimImage = img;
    }

    // =====================================================================
    // Score popup
    // =====================================================================

    static void BuildScorePopup(RectTransform parent, EmailSwiperManager mgr)
    {
        var go = RT("ScorePopup", parent);
        var rrt = go.GetComponent<RectTransform>();
        rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 0.5f);
        rrt.pivot = new Vector2(0.5f, 0.5f);
        rrt.sizeDelta = new Vector2(200, 60);

        var cg = go.AddComponent<CanvasGroup>();
        cg.alpha = 0f; cg.blocksRaycasts = false;

        var txt = Txt(rrt, "Text", "+100", 36, ScoreGold,
            TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(txt.rectTransform);

        mgr.scorePopupRT = rrt;
        mgr.scorePopupText = txt;
        mgr.scorePopupCG = cg;
    }

    // =====================================================================
    // Commentator
    // =====================================================================

    static Commentator BuildCommentator(RectTransform parent)
    {
        var root = RT("Commentator", parent);
        var rrt = root.GetComponent<RectTransform>();
        rrt.anchorMin = rrt.anchorMax = Vector2.zero;
        rrt.pivot = Vector2.zero;
        rrt.sizeDelta = new Vector2(540, 120);
        rrt.anchoredPosition = new Vector2(30, 230); // above bucket

        var portrait = Img(rrt, "Portrait", new Color(1f, 0.72f, 0.78f));
        portrait.sprite = Circle(); portrait.preserveAspect = true;
        var prt2 = portrait.rectTransform;
        prt2.anchorMin = new Vector2(0, 0); prt2.anchorMax = new Vector2(0, 1);
        prt2.pivot = new Vector2(0, 0.5f);
        prt2.sizeDelta = new Vector2(100, 0);

        var letter = Txt(prt2, "Letter", "G", 52,
            new Color(0.20f, 0.10f, 0.18f), TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(letter.rectTransform);

        var bubble = Img(rrt, "Bubble", new Color(1, 1, 1, 0.92f));
        var bbrt = bubble.rectTransform;
        bbrt.anchorMin = Vector2.zero; bbrt.anchorMax = Vector2.one;
        bbrt.offsetMin = new Vector2(115, 0); bbrt.offsetMax = Vector2.zero;

        var speaker = Txt(bbrt, "Speaker", "Grandma", 15,
            new Color(0.78f, 0.30f, 0.50f), TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        var slrt = speaker.rectTransform;
        slrt.anchorMin = new Vector2(0, 1); slrt.anchorMax = new Vector2(1, 1);
        slrt.pivot = new Vector2(0, 1);
        slrt.sizeDelta = new Vector2(0, 22); slrt.anchoredPosition = new Vector2(14, -5);

        var bText = Txt(bbrt, "BubbleText", "…", 19,
            new Color(0.13f, 0.13f, 0.13f), TextAlignmentOptions.MidlineLeft);
        bText.textWrappingMode = TextWrappingModes.Normal;
        var btrt = bText.rectTransform;
        btrt.anchorMin = Vector2.zero; btrt.anchorMax = Vector2.one;
        btrt.offsetMin = new Vector2(14, 5); btrt.offsetMax = new Vector2(-14, -25);

        var comm = root.AddComponent<Commentator>();
        comm.root = root; comm.portrait = portrait; comm.portraitLetter = letter;
        comm.bubbleBg = bubble; comm.bubbleText = bText; comm.speakerLabel = speaker;
        comm.portraitSprite = Circle();
        comm.speakerName = "Grandma"; comm.portraitInitial = "G";
        comm.portraitColor = new Color(1f, 0.72f, 0.78f);
        return comm;
    }

    // =====================================================================
    // Tutorial
    // =====================================================================

    static GameObject BuildTutorialPanel(RectTransform parent, EmailSwiperManager mgr)
    {
        var ov = Img(parent, "TutorialOverlay", new Color(0, 0, 0, 0.72f));
        Stretch(ov.rectTransform); ov.raycastTarget = true;

        var card = Img(ov.rectTransform, "Card", CardWhite);
        var crt = card.rectTransform;
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(820, 560);

        var hdr = Img(crt, "Header", Hex("#1B3A4B"));
        TopStretch(hdr.rectTransform, 95);
        var hLbl = Txt(hdr.rectTransform, "Title", "Phish Patrol", 44, Aqua,
            TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(hLbl.rectTransform); hLbl.raycastTarget = false;

        var body = Txt(crt, "Body",
            "Swipe each email to sort it.\n\n" +
            "◀  Swipe <b>LEFT</b> → <color=#FF6B6B><b>SCAM</b></color>\n" +
            "Swipe <b>RIGHT</b> → <color=#4ECDC4><b>SAFE</b></color>  ▶\n\n" +
            "Correct → fish joins the bucket!\n" +
            "Wrong → pufferfish spikes it.\n" +
            "<b>5 cracks</b> and the bucket breaks.",
            24, DarkText, TextAlignmentOptions.Center);
        var brt = body.rectTransform;
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
        brt.offsetMin = new Vector2(45, 110); brt.offsetMax = new Vector2(-45, -110);

        var btn = MakeButton(crt, "StartBtn", "Start Game", 28, SafeTeal, Color.white);
        var sbrt = btn.GetComponent<RectTransform>();
        sbrt.anchorMin = sbrt.anchorMax = new Vector2(0.5f, 0);
        sbrt.pivot = new Vector2(0.5f, 0);
        sbrt.sizeDelta = new Vector2(280, 64); sbrt.anchoredPosition = new Vector2(0, 28);
        UnityEventTools.AddPersistentListener(btn.GetComponent<Button>().onClick, mgr.OnTutorialStart);

        return ov.gameObject;
    }

    // =====================================================================
    // Feedback
    // =====================================================================

    static GameObject BuildFeedbackPanel(RectTransform parent, EmailSwiperManager mgr)
    {
        var ov = Img(parent, "FeedbackOverlay", new Color(0, 0, 0, 0.45f));
        Stretch(ov.rectTransform); ov.raycastTarget = true;
        ov.gameObject.AddComponent<CanvasGroup>();

        var card = Img(ov.rectTransform, "Card", Hex("#2ECC71"));
        var crt = card.rectTransform;
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.55f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(960, 380);

        var title = Txt(crt, "Title", "Correct!", 52, Color.white,
            TextAlignmentOptions.Center, FontStyles.Bold);
        var trt = title.rectTransform;
        trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1);
        trt.pivot = new Vector2(0.5f, 1);
        trt.sizeDelta = new Vector2(-40, 80); trt.anchoredPosition = new Vector2(0, -24);

        var body = Txt(crt, "Body", "Explanation…", 23, Color.white,
            TextAlignmentOptions.Center);
        body.textWrappingMode = TextWrappingModes.Normal;
        var brt = body.rectTransform;
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
        brt.offsetMin = new Vector2(40, 24); brt.offsetMax = new Vector2(-40, -110);

        mgr.feedbackBg = card; mgr.feedbackTitle = title; mgr.feedbackBody = body;
        return ov.gameObject;
    }

    // =====================================================================
    // Result
    // =====================================================================

    static GameObject BuildResultPanel(RectTransform parent, EmailSwiperManager mgr)
    {
        var ov = Img(parent, "ResultOverlay", new Color(0, 0, 0, 0.75f));
        Stretch(ov.rectTransform); ov.raycastTarget = true;

        var card = Img(ov.rectTransform, "Card", CardWhite);
        var crt = card.rectTransform;
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(820, 620);

        var hdr = Img(crt, "Header", Hex("#1B3A4B"));
        TopStretch(hdr.rectTransform, 95);
        var hLbl = Txt(hdr.rectTransform, "Label", "Round Complete!", 40, Aqua,
            TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(hLbl.rectTransform); hLbl.raycastTarget = false;

        var stars = Txt(crt, "Stars", "★ ★ ★", 62, ScoreGold,
            TextAlignmentOptions.Center, FontStyles.Bold);
        var srt = stars.rectTransform;
        srt.anchorMin = new Vector2(0, 1); srt.anchorMax = new Vector2(1, 1);
        srt.pivot = new Vector2(0.5f, 1);
        srt.sizeDelta = new Vector2(0, 90); srt.anchoredPosition = new Vector2(0, -120);

        var score = Txt(crt, "Score", "0 / 800", 42, DarkText,
            TextAlignmentOptions.Center, FontStyles.Bold);
        var scrt = score.rectTransform;
        scrt.anchorMin = new Vector2(0, 1); scrt.anchorMax = new Vector2(1, 1);
        scrt.pivot = new Vector2(0.5f, 1);
        scrt.sizeDelta = new Vector2(0, 60); scrt.anchoredPosition = new Vector2(0, -220);

        var msg = Txt(crt, "Message", "Result", 21, MutedText,
            TextAlignmentOptions.Center);
        msg.textWrappingMode = TextWrappingModes.Normal;
        var mrt = msg.rectTransform;
        mrt.anchorMin = Vector2.zero; mrt.anchorMax = Vector2.one;
        mrt.offsetMin = new Vector2(45, 120); mrt.offsetMax = new Vector2(-45, -300);

        var play = MakeButton(crt, "PlayAgain", "Play Again", 25, SafeTeal, Color.white);
        var prrt = play.GetComponent<RectTransform>();
        prrt.anchorMin = prrt.anchorMax = new Vector2(0.5f, 0);
        prrt.pivot = new Vector2(1, 0);
        prrt.sizeDelta = new Vector2(240, 60); prrt.anchoredPosition = new Vector2(-12, 36);
        UnityEventTools.AddPersistentListener(play.GetComponent<Button>().onClick, mgr.OnPlayAgain);

        var back = MakeButton(crt, "BackToMap", "Back to Map", 25, Hex("#636E72"), Color.white);
        var bart = back.GetComponent<RectTransform>();
        bart.anchorMin = bart.anchorMax = new Vector2(0.5f, 0);
        bart.pivot = new Vector2(0, 0);
        bart.sizeDelta = new Vector2(240, 60); bart.anchoredPosition = new Vector2(12, 36);
        UnityEventTools.AddPersistentListener(back.GetComponent<Button>().onClick, mgr.OnReturnToMap);

        mgr.resultStars = stars; mgr.resultScore = score; mgr.resultMessage = msg;
        return ov.gameObject;
    }

    // =====================================================================
    // Low-level helpers
    // =====================================================================

    static Image Img(Transform p, string n, Color c)
    {
        var go = new GameObject(n, typeof(RectTransform));
        go.transform.SetParent(p, false);
        var img = go.AddComponent<Image>(); img.color = c; return img;
    }

    static TMP_Text Txt(Transform p, string n, string c, int s, Color col,
        TextAlignmentOptions a, FontStyles st = FontStyles.Normal)
    {
        var go = new GameObject(n, typeof(RectTransform));
        go.transform.SetParent(p, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = c; t.fontSize = s; t.color = col;
        t.alignment = a; t.fontStyle = st; t.raycastTarget = false;
        return t;
    }

    static GameObject MakeButton(Transform p, string n, string lbl,
        int fs, Color bg, Color tc)
    {
        var go = new GameObject(n, typeof(RectTransform));
        go.transform.SetParent(p, false);
        var img = go.AddComponent<Image>(); img.color = bg;
        var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        var t = Txt(go.transform, "Label", lbl, fs, tc,
            TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(t.rectTransform);
        return go;
    }

    static GameObject RT(string n, Transform p)
    {
        var go = new GameObject(n, typeof(RectTransform));
        go.transform.SetParent(p, false); return go;
    }

    static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    static void TopStretch(RectTransform r, float h, float inset = 0)
    {
        r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1);
        r.pivot = new Vector2(0.5f, 1); r.sizeDelta = new Vector2(0, h);
        r.anchoredPosition = new Vector2(0, -inset);
    }

    static Color Hex(string h) =>
        ColorUtility.TryParseHtmlString(h, out var c) ? c : Color.magenta;

    static Sprite Circle()
    {
        try
        {
            var s = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            if (s != null) return s;
        }
        catch { }
        return null;
    }

    static void AddToBuild(string path)
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(s => s.path == path))
        {
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}