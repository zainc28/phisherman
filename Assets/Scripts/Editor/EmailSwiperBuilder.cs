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
/// Builds the Email Swiper scene.
/// Run via:  Phisherman ▸ Build Email Swiper Scene
///
/// NEW (June 2026 v3):
///   1. Rod now uses 4 segments (rodLine3RT + rodLine4RT added) for smooth Bezier curve.
///   2. Reigns-style animated dot row at top of card — CardDotAnimator drives colours.
///   3. Fish-themed striped card border — 5 fish colour schemes, rotated per email.
///   4. AudioSource added to GameManager; manager fields for music + sfx clips.
/// </summary>
public static class EmailSwiperBuilder
{
    private const string ScenesDir = "Assets/Scenes";
    private const string ScenePath = "Assets/Scenes/EmailSwiper.unity";

    // ── Palette ──────────────────────────────────────────────────────────────
    private static readonly Color BgDeep = Hex("#0A1628");
    private static readonly Color BgOcean = Hex("#0F2A45");
    private static readonly Color BgMidOcean = Hex("#153552");
    private static readonly Color WaveShine = new Color(0.35f, 0.65f, 0.90f, 0.12f);
    private static readonly Color HudBg = Hex("#0A1628");
    private static readonly Color HudBorder = Hex("#1E3A5A");
    private static readonly Color ScoreGold = Hex("#FFD93D");
    private static readonly Color StreakOrange = Hex("#FF9F1C");
    private static readonly Color HeartFull = new Color(0.91f, 0.30f, 0.24f);
    private static readonly Color CardWhite = Hex("#FAFBFF");
    private static readonly Color CardShadow = new Color(0, 0, 0, 0.22f);
    private static readonly Color DarkText = Hex("#1A1A2E");
    private static readonly Color MutedText = Hex("#636E72");
    private static readonly Color LightText = Hex("#DFE6E9");
    private static readonly Color DividerCol = Hex("#E8EAED");
    private static readonly Color ScamRed = Hex("#FF6B6B");
    private static readonly Color SafeTeal = Hex("#4ECDC4");
    private static readonly Color ScamBg = new Color(1f, 0.42f, 0.42f, 0.18f);
    private static readonly Color SafeBg = new Color(0.31f, 0.80f, 0.77f, 0.18f);
    private static readonly Color BucketWood = Hex("#5C3D1E");
    private static readonly Color BucketSlat = Hex("#7A5230");
    private static readonly Color BucketHoop = Hex("#C0A060");
    private static readonly Color BucketInner = Hex("#1A1A2E");
    private static readonly Color WaterBlue = new Color(0.27f, 0.62f, 0.83f, 0.60f);
    private static readonly Color WaterShine = new Color(0.55f, 0.85f, 1.00f, 0.25f);
    private static readonly Color CrackColor = Hex("#FDCB6E");
    private static readonly Color RodLine = new Color(0.90f, 0.75f, 0.40f, 0.92f);
    private static readonly Color BobCol = new Color(0.95f, 0.95f, 1.00f, 0.95f);
    private static readonly Color BobRing = new Color(0.60f, 0.80f, 1.00f, 0.70f);
    private static readonly Color Aqua = Hex("#81ECEC");

    // ── Fish border schemes (stripeA, stripeB, accent) ───────────────────────
    // Each tuple: primary stripe, secondary stripe, thin accent line
    private static readonly (Color a, Color b, Color accent, string name)[] FishSchemes =
    {
        // Clownfish — orange + white + black accent
        (Hex("#FF6B1A"), Color.white,            Hex("#1A1A1A"), "Clownfish"),
        // Blue tang — cobalt + yellow + white accent
        (Hex("#1A6EFF"), Hex("#FFE033"),          Color.white,   "Blue Tang"),
        // Pufferfish — golden yellow + brown + orange accent
        (Hex("#F5C518"), Hex("#6B3A1A"),          Hex("#FF8C00"), "Pufferfish"),
        // Angelfish — black + gold + silver accent
        (Hex("#1A1A1A"), Hex("#D4AF37"),          Hex("#C0C0C0"), "Angelfish"),
        // Salmon — pink + silver + rose accent
        (Hex("#FF8C8C"), Hex("#C8C8C8"),          Hex("#FF4477"), "Salmon"),
        // Parrotfish — teal + magenta + lime accent
        (Hex("#00CED1"), Hex("#FF00AA"),          Hex("#AAFF00"), "Parrotfish"),
        // Lionfish — dark red + cream + gold accent
        (Hex("#8B1A1A"), Hex("#FFF8DC"),          Hex("#FFD700"), "Lionfish"),
        // Zebrafish — white + black + teal accent
        (Color.white,   Hex("#222222"),           Hex("#00CED1"), "Zebrafish"),
    };

    // =========================================================================
    // Build
    // =========================================================================

    [MenuItem("Phisherman/Build Email Swiper Scene")]
    public static void Build()
    {
        if (!Directory.Exists(ScenesDir)) Directory.CreateDirectory(ScenesDir);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Sprite circle = GetCircle();
        Sprite fishNorm = FindSprite("fish_normal") ?? FindSprite("td_hanging_fish");
        Sprite fishPuff = FindSprite("fish_puffer") ?? FindSprite("fishpuffer");
        LogFound("fish_normal", fishNorm);
        LogFound("fish_puffer", fishPuff);

        // Camera
        var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>(); camGo.AddComponent<AudioListener>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = BgDeep;
        cam.orthographic = true;

        // EventSystem
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>(); es.AddComponent<StandaloneInputModule>();

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

        // Manager + two AudioSources (one for music loop, one for sfx)
        var mgrGo = new GameObject("GameManager");
        var mgr = mgrGo.AddComponent<EmailSwiperManager>();
        var sfxSource = mgrGo.AddComponent<AudioSource>();   // sfx (short one-shots)
        var musicSource = mgrGo.AddComponent<AudioSource>();   // music (looping)
        sfxSource.playOnAwake = false;
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        mgr.circleSprite = circle;
        mgr.fishNormalSprite = fishNorm;
        mgr.fishPufferSprite = fishPuff;

        // FIX 3: Auto-detect and wire audio clips so Inspector drag isn't needed
        mgr.bgMusic = FindAudioClip("frutiger_music_fresh_waters");
        mgr.sfxCardFlip = FindAudioClip("card_flip");
        mgr.sfxCorrect = FindAudioClip("water_splash");
        mgr.sfxWrong = FindAudioClip("glass_crack");
        LogFound("bgMusic", mgr.bgMusic != null ? GetCircle() : null);  // reuse log
        Debug.Log($"[EmailSwiperBuilder] bgMusic: " + (mgr.bgMusic != null ? "✓" : "✗ not found — drag frutiger_music_fresh_waters into Inspector"));
        Debug.Log($"[EmailSwiperBuilder] sfxCardFlip: " + (mgr.sfxCardFlip != null ? "✓" : "✗ not found"));
        Debug.Log($"[EmailSwiperBuilder] sfxCorrect: " + (mgr.sfxCorrect != null ? "✓" : "✗ not found"));
        Debug.Log($"[EmailSwiperBuilder] sfxWrong: " + (mgr.sfxWrong != null ? "✓" : "✗ not found"));

        BuildBackground(canvasRT);
        var hud = BuildHud(canvasRT, mgr, circle);

        var gameRootImg = Img(canvasRT, "GameRoot", new Color(0, 0, 0, 0));
        var grrt = gameRootImg.rectTransform;
        grrt.anchorMin = Vector2.zero; grrt.anchorMax = Vector2.one;
        grrt.offsetMin = new Vector2(0, 220); grrt.offsetMax = new Vector2(0, -90);
        gameRootImg.raycastTarget = false;

        CanvasGroup scamCG, safeCG;
        BuildSwipeZones(grrt, out scamCG, out safeCG);
        BuildCard(grrt, mgr, scamCG, safeCG, canvas);
        BuildRod(canvasRT, mgr.swipeCard, canvas, circle);
        BuildBucket(canvasRT, mgr, circle);
        BuildFishAnim(canvasRT, mgr, circle);
        BuildScorePopup(canvasRT, mgr);
        mgr.commentator = BuildCommentator(canvasRT, circle);

        var feedback = BuildFeedbackPanel(canvasRT, mgr);
        var tutorial = BuildTutorialPanel(canvasRT, mgr);
        var result = BuildResultPanel(canvasRT, mgr);

        mgr.gameRoot = gameRootImg.gameObject;
        mgr.hudPanel = hud;
        mgr.feedbackPanel = feedback;
        mgr.tutorialPanel = tutorial;
        mgr.resultPanel = result;

        gameRootImg.gameObject.SetActive(false);
        hud.SetActive(false); feedback.SetActive(false);
        result.SetActive(false); tutorial.SetActive(true);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuild(ScenePath);
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log($"[EmailSwiperBuilder] Built → {ScenePath}");
    }

    // =========================================================================
    // Background
    // =========================================================================

    static void BuildBackground(RectTransform canvasRT)
    {
        var bgBase = Img(canvasRT, "BgBase", BgDeep); Stretch(bgBase.rectTransform); bgBase.raycastTarget = false;
        var bgMid = Img(canvasRT, "BgMid", BgOcean);
        var mr = bgMid.rectTransform; mr.anchorMin = new Vector2(0, 0.10f); mr.anchorMax = Vector2.one;
        mr.offsetMin = mr.offsetMax = Vector2.zero; bgMid.raycastTarget = false;
        var bgUp = Img(canvasRT, "BgUpper", BgMidOcean);
        var ur = bgUp.rectTransform; ur.anchorMin = new Vector2(0, 0.45f); ur.anchorMax = Vector2.one;
        ur.offsetMin = ur.offsetMax = Vector2.zero; bgUp.raycastTarget = false;
        float[] wY = { 0.18f, 0.34f, 0.52f }, wH = { 0.08f, 0.06f, 0.05f };
        for (int w = 0; w < 3; w++)
        {
            var wave = Img(canvasRT, "Wave_" + w, WaveShine);
            var wr = wave.rectTransform;
            wr.anchorMin = new Vector2(0, wY[w]); wr.anchorMax = new Vector2(1, wY[w] + wH[w]);
            wr.offsetMin = wr.offsetMax = Vector2.zero; wave.raycastTarget = false;
        }
    }

    // =========================================================================
    // HUD with hearts
    // =========================================================================

    static GameObject BuildHud(RectTransform parent, EmailSwiperManager mgr, Sprite circle)
    {
        var hud = Img(parent, "HUD", HudBg); TopStretch(hud.rectTransform, 90); hud.raycastTarget = true;
        var bord = Img(hud.rectTransform, "HudBorder", HudBorder);
        var brrt = bord.rectTransform; brrt.anchorMin = Vector2.zero; brrt.anchorMax = new Vector2(1, 0);
        brrt.pivot = new Vector2(0.5f, 0); brrt.sizeDelta = new Vector2(0, 2); bord.raycastTarget = false;

        // Hearts (left)
        var hh = RT("HeartsHolder", hud.rectTransform);
        var hhrt = hh.GetComponent<RectTransform>();
        hhrt.anchorMin = new Vector2(0, 0); hhrt.anchorMax = new Vector2(0, 1);
        hhrt.pivot = new Vector2(0, 0.5f); hhrt.sizeDelta = new Vector2(240, 0);
        hhrt.anchoredPosition = new Vector2(24, 0);
        var hlg = hh.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleLeft; hlg.spacing = 6;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;
        hlg.childControlWidth = hlg.childControlHeight = false;
        hlg.padding = new RectOffset(0, 0, 14, 14);
        for (int i = 0; i < 5; i++)
        {
            var hgo = new GameObject("Heart_" + i, typeof(RectTransform));
            hgo.transform.SetParent(hh.transform, false);
            var hi = hgo.AddComponent<Image>();
            hi.sprite = circle; hi.color = HeartFull; hi.preserveAspect = true; hi.raycastTarget = false;
            var le = hgo.AddComponent<LayoutElement>(); le.preferredWidth = le.preferredHeight = 34;
        }
        mgr.heartsContainer = hh.transform;

        // Timer (centre)
        var tH = RT("TimerHolder", hud.rectTransform);
        var thr = tH.GetComponent<RectTransform>();
        thr.anchorMin = new Vector2(0.5f, 0); thr.anchorMax = new Vector2(0.5f, 1);
        thr.pivot = new Vector2(0.5f, 0.5f); thr.sizeDelta = new Vector2(340, 0);
        var timerTxt = Txt(thr, "TimerText", "1:30", 42, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        var ttrt = timerTxt.rectTransform; ttrt.anchorMin = new Vector2(0, 0.45f); ttrt.anchorMax = Vector2.one; ttrt.offsetMin = ttrt.offsetMax = Vector2.zero;
        var barBg = Img(thr, "TimerBarBg", new Color(1, 1, 1, 0.12f));
        var bbrt = barBg.rectTransform; bbrt.anchorMin = new Vector2(0, 0.08f); bbrt.anchorMax = new Vector2(1, 0.38f); bbrt.offsetMin = new Vector2(12, 0); bbrt.offsetMax = new Vector2(-12, 0); barBg.raycastTarget = false;
        var fill = Img(bbrt, "TimerFill", SafeTeal); Stretch(fill.rectTransform);
        fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillOrigin = 0; fill.fillAmount = 1f; fill.raycastTarget = false;
        var progTxt = Txt(thr, "Progress", "1 / 8", 18, LightText, TextAlignmentOptions.Center);
        var prt = progTxt.rectTransform; prt.anchorMin = Vector2.zero; prt.anchorMax = new Vector2(1, 0.22f); prt.offsetMin = prt.offsetMax = Vector2.zero;

        // Score (right)
        var sH = RT("ScoreHolder", hud.rectTransform);
        var shr = sH.GetComponent<RectTransform>();
        shr.anchorMin = new Vector2(1, 0); shr.anchorMax = new Vector2(1, 1); shr.pivot = new Vector2(1, 0.5f);
        shr.sizeDelta = new Vector2(340, 0); shr.anchoredPosition = new Vector2(-24, 0);
        var scoreTxt = Txt(shr, "ScoreText", "Score: 0", 36, ScoreGold, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
        var srrt = scoreTxt.rectTransform; srrt.anchorMin = new Vector2(0, 0.38f); srrt.anchorMax = Vector2.one; srrt.offsetMin = srrt.offsetMax = Vector2.zero;
        var streakTxt = Txt(shr, "StreakText", "", 22, StreakOrange, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
        var skrt = streakTxt.rectTransform; skrt.anchorMin = Vector2.zero; skrt.anchorMax = new Vector2(1, 0.38f); skrt.offsetMin = skrt.offsetMax = Vector2.zero;

        mgr.timerText = timerTxt; mgr.timerFill = fill; mgr.scoreText = scoreTxt; mgr.streakText = streakTxt; mgr.progressText = progTxt;
        return hud.gameObject;
    }

    // =========================================================================
    // Swipe zones (merged hint + button)
    // =========================================================================

    static void BuildSwipeZones(RectTransform parent, out CanvasGroup scamCG, out CanvasGroup safeCG)
    {
        scamCG = BuildSwipeZone(parent, "ScamZone", "◀  SCAM", ScamRed, ScamBg, 0.10f, -1);
        safeCG = BuildSwipeZone(parent, "SafeZone", "SAFE  ▶", SafeTeal, SafeBg, 0.90f, +1);
    }

    static CanvasGroup BuildSwipeZone(RectTransform parent, string name,
        string label, Color textCol, Color bgCol, float xAnchor, int dir)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(xAnchor, 0.20f); rt.anchorMax = new Vector2(xAnchor, 0.85f);
        rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = new Vector2(180, 0);
        var bg = go.AddComponent<Image>(); bg.color = bgCol; bg.raycastTarget = true;
        var arrow = Txt(rt, "Arrow", dir < 0 ? "◀" : "▶", 48, textCol, TextAlignmentOptions.Center, FontStyles.Bold);
        var arrt = arrow.rectTransform; arrt.anchorMin = new Vector2(0, 0.55f); arrt.anchorMax = new Vector2(1, 0.85f); arrt.offsetMin = arrt.offsetMax = Vector2.zero;
        var lbl = Txt(rt, "Label", label, 30, textCol, TextAlignmentOptions.Center, FontStyles.Bold);
        var lrt = lbl.rectTransform; lrt.anchorMin = new Vector2(0, 0.30f); lrt.anchorMax = new Vector2(1, 0.55f); lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        var cg = go.AddComponent<CanvasGroup>(); cg.alpha = 0f; cg.blocksRaycasts = true;
        var relay = go.AddComponent<SwipeButtonRelay>(); relay.direction = dir;
        var btn = go.AddComponent<Button>(); btn.targetGraphic = bg; btn.onClick.AddListener(relay.Fire);
        return cg;
    }

    // =========================================================================
    // NEW: Email card with Reigns dots + fish-stripe border
    // =========================================================================

    static void BuildCard(RectTransform parent, EmailSwiperManager mgr,
        CanvasGroup scamHint, CanvasGroup safeHint, Canvas rootCanvas)
    {
        var scheme = FishSchemes[0];  // clownfish default; swap per email at runtime via index

        // ── Shadow (behind everything) ──
        var shadow = Img(parent, "CardShadow", CardShadow);
        var shrt = shadow.rectTransform;
        shrt.anchorMin = shrt.anchorMax = new Vector2(0.5f, 0.52f);
        shrt.pivot = new Vector2(0.5f, 0.5f); shrt.sizeDelta = new Vector2(628, 708);
        shrt.anchoredPosition = new Vector2(8, -8); shadow.raycastTarget = false;

        // ── Card face root (transparent container — holds all card children) ──
        var card = Img(parent, "EmailCard", new Color(0, 0, 0, 0));
        var crt = card.rectTransform;
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.52f);
        crt.pivot = new Vector2(0.5f, 0.5f); crt.sizeDelta = new Vector2(620, 710);
        card.raycastTarget = false;
        var cardCG = card.gameObject.AddComponent<CanvasGroup>();

        // ── Solid white background — first child so it's behind everything ──
        var cardBgImg = Img(crt, "CardBg", CardWhite);
        Stretch(cardBgImg.rectTransform);
        cardBgImg.raycastTarget = false;

        // ── FIX 1: Fish-stripe border — 4 edge strips INSIDE the card ──
        // Each strip is a child of crt so it moves/rotates with the card.
        float borderW = 14f;   // thickness of each border strip in pixels

        // Top strip — striped horizontally (alternating colours)
        BuildStripedEdge(crt, "BorderTop", scheme,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
            new Vector2(0, borderW), Vector2.zero, horizontal: true);

        // Bottom strip
        BuildStripedEdge(crt, "BorderBottom", scheme,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0),
            new Vector2(0, borderW), Vector2.zero, horizontal: true);

        // Left strip — striped vertically
        BuildStripedEdge(crt, "BorderLeft", scheme,
            new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f),
            new Vector2(borderW, 0), Vector2.zero, horizontal: false);

        // Right strip
        BuildStripedEdge(crt, "BorderRight", scheme,
            new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f),
            new Vector2(borderW, 0), Vector2.zero, horizontal: false);

        // Store the top border image so SwipeCard can tint it on drag
        // (we'll use the card itself as the border target for the glow since
        //  the border is now inside the card and moves with it)
        var cardBorderImg = card;   // SwipeCard tints this on drag

        // ── FIX 2: Dots row at BOTTOM of card, small ──
        BuildDotRow(crt, 10);

        // ── Sender header accent strip ──
        var headerStrip = Img(crt, "HeaderStrip", Hex("#1A73E8"));
        var hsrt = headerStrip.rectTransform;
        hsrt.anchorMin = new Vector2(0, 1); hsrt.anchorMax = new Vector2(1, 1);
        hsrt.pivot = new Vector2(0.5f, 1); hsrt.sizeDelta = new Vector2(0, 6);
        headerStrip.raycastTarget = false;

        // ── Avatar ──
        var avatar = Img(crt, "Avatar", Hex("#1A73E8"));
        avatar.sprite = GetCircle(); avatar.preserveAspect = true;
        var avrt = avatar.rectTransform;
        avrt.anchorMin = avrt.anchorMax = new Vector2(0, 1); avrt.pivot = new Vector2(0, 1);
        avrt.sizeDelta = new Vector2(58, 58); avrt.anchoredPosition = new Vector2(26, -48);
        avatar.raycastTarget = false;
        var avatarLetter = Txt(avrt, "Letter", "P", 32, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(avatarLetter.rectTransform);

        // ── Sender name ──
        var senderName = Txt(crt, "SenderName", "Sender", 24, DarkText, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        var snrt = senderName.rectTransform;
        snrt.anchorMin = new Vector2(0, 1); snrt.anchorMax = new Vector2(1, 1); snrt.pivot = new Vector2(0, 1);
        snrt.sizeDelta = new Vector2(-130, 32); snrt.anchoredPosition = new Vector2(100, -50);

        // ── Sender email ──
        var senderEmail = Txt(crt, "SenderEmail", "sender@example.com", 18, MutedText, TextAlignmentOptions.MidlineLeft);
        var sert = senderEmail.rectTransform;
        sert.anchorMin = new Vector2(0, 1); sert.anchorMax = new Vector2(1, 1); sert.pivot = new Vector2(0, 1);
        sert.sizeDelta = new Vector2(-130, 24); sert.anchoredPosition = new Vector2(100, -84);

        // ── Domain badge ──
        var domainBadge = Img(crt, "DomainBadge", new Color(1f, 0.94f, 0.94f));
        var dbrt = domainBadge.rectTransform;
        dbrt.anchorMin = new Vector2(0, 1); dbrt.anchorMax = new Vector2(0, 1); dbrt.pivot = new Vector2(0, 1);
        dbrt.sizeDelta = new Vector2(280, 24); dbrt.anchoredPosition = new Vector2(100, -84);
        domainBadge.raycastTarget = false;

        // ── Divider ──
        var div = Img(crt, "Divider", DividerCol);
        var dvrt = div.rectTransform;
        dvrt.anchorMin = new Vector2(0, 1); dvrt.anchorMax = new Vector2(1, 1); dvrt.pivot = new Vector2(0.5f, 1);
        dvrt.sizeDelta = new Vector2(-40, 1); dvrt.anchoredPosition = new Vector2(0, -124);
        div.raycastTarget = false;

        // ── Subject ──
        var subject = Txt(crt, "Subject", "Subject", 26, DarkText, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        var sjrt = subject.rectTransform;
        sjrt.anchorMin = new Vector2(0, 1); sjrt.anchorMax = new Vector2(1, 1); sjrt.pivot = new Vector2(0, 1);
        sjrt.sizeDelta = new Vector2(-48, 64); sjrt.anchoredPosition = new Vector2(24, -138);

        // ── Body ──
        var body = Txt(crt, "Body", "Body text…", 20, DarkText, TextAlignmentOptions.TopLeft);
        body.textWrappingMode = TextWrappingModes.Normal; body.overflowMode = TextOverflowModes.Ellipsis;
        var brt = body.rectTransform;
        brt.anchorMin = new Vector2(0, 0); brt.anchorMax = new Vector2(1, 1);
        brt.offsetMin = new Vector2(24, 28); brt.offsetMax = new Vector2(-24, -215);

        // ── Drag overlay ──
        var overlay = Img(parent, "DragOverlay", new Color(0, 0, 0, 0));
        var olrt = overlay.rectTransform;
        olrt.anchorMin = olrt.anchorMax = new Vector2(0.5f, 0.52f); olrt.pivot = new Vector2(0.5f, 0.5f);
        olrt.sizeDelta = new Vector2(640, 720); overlay.raycastTarget = true;

        var swipe = overlay.gameObject.AddComponent<SwipeCard>();
        swipe.cardRoot = crt;
        swipe.scamIndicator = scamHint;
        swipe.safeIndicator = safeHint;
        swipe.cardBorder = cardBorderImg;
        swipe.rootCanvas = rootCanvas;

        foreach (var relay in GameObject.FindObjectsByType<SwipeButtonRelay>(FindObjectsSortMode.None))
            relay.target = swipe;

        mgr.swipeCard = swipe;
        mgr.cardSender = senderName;
        mgr.cardEmail = senderEmail;
        mgr.cardSubject = subject;
        mgr.cardBody = body;
        mgr.cardAvatar = avatar;
        mgr.cardAvatarLetter = avatarLetter;
        mgr.cardCanvasGroup = cardCG;
    }

    // =========================================================================
    // NEW: Reigns-style dot row
    // =========================================================================

    static void BuildDotRow(RectTransform cardRT, int dotCount)
    {
        // FIX 2: anchored to BOTTOM of card, small dots, inside card bounds
        var rowGo = new GameObject("DotRow", typeof(RectTransform));
        rowGo.transform.SetParent(cardRT, false);
        var rrt = rowGo.GetComponent<RectTransform>();
        rrt.anchorMin = new Vector2(0, 0); rrt.anchorMax = new Vector2(1, 0);
        rrt.pivot = new Vector2(0.5f, 0);
        rrt.sizeDelta = new Vector2(0, 24); rrt.anchoredPosition = new Vector2(0, 8);

        var hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 5;   // FIX 2: tighter spacing for small dots
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;
        hlg.childControlWidth = hlg.childControlHeight = false;

        var dotAnimator = rowGo.AddComponent<CardDotAnimator>();
        var dots = new Image[dotCount];

        for (int i = 0; i < dotCount; i++)
        {
            var dGo = new GameObject("Dot_" + i, typeof(RectTransform));
            dGo.transform.SetParent(rowGo.transform, false);
            // Set sizeDelta directly — this is what actually controls size
            // when childControlWidth/Height = false
            var dRT = dGo.GetComponent<RectTransform>();
            dRT.sizeDelta = new Vector2(12, 12);
            var dImg = dGo.AddComponent<Image>();
            dImg.sprite = GetCircle();
            dImg.color = Color.white;
            dImg.preserveAspect = true;
            dImg.raycastTarget = false;
            dots[i] = dImg;
        }

        dotAnimator.dots = dots;
    }

    // =========================================================================
    // FIX: Rod — 4 segments for smooth Bezier
    // =========================================================================

    static void BuildRod(RectTransform canvasRT, SwipeCard swipe, Canvas canvas, Sprite circle)
    {
        if (swipe == null) return;

        // Rod tip anchor
        var tipGo = new GameObject("RodTip", typeof(RectTransform)); tipGo.transform.SetParent(canvasRT, false);
        var tipRT = tipGo.GetComponent<RectTransform>();
        tipRT.anchorMin = tipRT.anchorMax = new Vector2(0.5f, 1f); tipRT.pivot = new Vector2(0.5f, 1f);
        tipRT.sizeDelta = new Vector2(4, 4); tipRT.anchoredPosition = new Vector2(0, -90f);

        // 4 line segments
        RectTransform MakeSeg(string n)
        {
            var go = Img(canvasRT, n, RodLine); var rt = go.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(200, 4f); go.raycastTarget = false; go.gameObject.SetActive(false);
            return rt;
        }

        var seg1 = MakeSeg("RodLine1");
        var seg2 = MakeSeg("RodLine2");
        var seg3 = MakeSeg("RodLine3");
        var seg4 = MakeSeg("RodLine4");

        // Bob orb
        var bobGo = new GameObject("RodBob", typeof(RectTransform)); bobGo.transform.SetParent(canvasRT, false);
        var bobRT = bobGo.GetComponent<RectTransform>();
        bobRT.anchorMin = bobRT.anchorMax = new Vector2(0.5f, 0.5f); bobRT.pivot = new Vector2(0.5f, 0.5f);
        bobRT.sizeDelta = new Vector2(18, 18);
        var bobImg = bobGo.AddComponent<Image>(); bobImg.sprite = circle; bobImg.color = BobCol;
        bobImg.preserveAspect = true; bobImg.raycastTarget = false;
        var ringGo = new GameObject("BobRing", typeof(RectTransform)); ringGo.transform.SetParent(bobGo.transform, false);
        var ringImg = ringGo.AddComponent<Image>(); ringImg.sprite = circle; ringImg.color = BobRing;
        ringImg.preserveAspect = true; ringImg.raycastTarget = false;
        var ringRT = ringGo.GetComponent<RectTransform>();
        ringRT.anchorMin = ringRT.anchorMax = new Vector2(0.5f, 0.5f); ringRT.pivot = new Vector2(0.5f, 0.5f);
        ringRT.sizeDelta = new Vector2(26, 26);
        bobGo.SetActive(false);

        swipe.rodTipRT = tipRT;
        swipe.rodLineRT = seg1;
        swipe.rodLine2RT = seg2;
        swipe.rodLine3RT = seg3;
        swipe.rodLine4RT = seg4;
        swipe.rodBobRT = bobRT;
        swipe.rootCanvas = canvas;
    }

    // =========================================================================
    // Bucket (wooden barrel)
    // =========================================================================

    static void BuildBucket(RectTransform parent, EmailSwiperManager mgr, Sprite circle)
    {
        var root = Img(parent, "BucketRoot", new Color(0, 0, 0, 0)); var rrt = root.rectTransform;
        rrt.anchorMin = new Vector2(0, 0); rrt.anchorMax = new Vector2(1, 0); rrt.pivot = new Vector2(0.5f, 0);
        rrt.sizeDelta = new Vector2(0, 220); root.raycastTarget = false;
        var body = Img(rrt, "BucketBody", BucketWood); var brt = body.rectTransform;
        brt.anchorMin = new Vector2(0.08f, 0); brt.anchorMax = new Vector2(0.92f, 0.76f); brt.offsetMin = brt.offsetMax = Vector2.zero; body.raycastTarget = false;
        float[] slatX = { 0.18f, 0.32f, 0.50f, 0.68f, 0.82f };
        foreach (var sx in slatX) { var sl = Img(brt, "Slat", BucketSlat); var sr = sl.rectTransform; sr.anchorMin = new Vector2(sx - 0.02f, 0.02f); sr.anchorMax = new Vector2(sx + 0.02f, 0.98f); sr.offsetMin = sr.offsetMax = Vector2.zero; sl.raycastTarget = false; }
        foreach (float hy in new[] { 0.82f, 0.18f }) { var hp = Img(brt, "Hoop_" + hy, BucketHoop); var hr = hp.rectTransform; hr.anchorMin = new Vector2(0, hy - 0.04f); hr.anchorMax = new Vector2(1, hy + 0.04f); hr.offsetMin = hr.offsetMax = Vector2.zero; hp.raycastTarget = false; }
        var inner = Img(brt, "Inner", BucketInner); var irt = inner.rectTransform; irt.anchorMin = new Vector2(0.03f, 0.28f); irt.anchorMax = new Vector2(0.97f, 0.80f); irt.offsetMin = irt.offsetMax = Vector2.zero; inner.raycastTarget = false;
        var water = Img(inner.rectTransform, "WaterFill", WaterBlue); var wrt2 = water.rectTransform; wrt2.anchorMin = Vector2.zero; wrt2.anchorMax = new Vector2(1, 0.55f); wrt2.offsetMin = wrt2.offsetMax = Vector2.zero; water.raycastTarget = false;
        var shine = Img(inner.rectTransform, "WaterShine", WaterShine); var shrt = shine.rectTransform; shrt.anchorMin = new Vector2(0.05f, 0.52f); shrt.anchorMax = new Vector2(0.95f, 0.58f); shrt.offsetMin = shrt.offsetMax = Vector2.zero; shine.raycastTarget = false;
        var fishCont = RT("FishContainer", inner.rectTransform); Stretch(fishCont.GetComponent<RectTransform>());
        var rim = Img(rrt, "Rim", BucketHoop); var rmrt = rim.rectTransform; rmrt.anchorMin = new Vector2(0.06f, 0.73f); rmrt.anchorMax = new Vector2(0.94f, 0.82f); rmrt.offsetMin = rmrt.offsetMax = Vector2.zero; rim.raycastTarget = false;
        var label = Txt(rrt, "BucketLabel", "0 caught  ·  0/5 cracks", 20, LightText, TextAlignmentOptions.Center); var lrt = label.rectTransform; lrt.anchorMin = new Vector2(0, 0.82f); lrt.anchorMax = new Vector2(1, 1); lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        var crackCont = RT("CrackContainer", brt); Stretch(crackCont.GetComponent<RectTransform>());
        var slots = new RectTransform[5]; float[] cx = { 0.12f, 0.30f, 0.50f, 0.70f, 0.88f }; float[] cy = { 0.35f, 0.62f, 0.42f, 0.68f, 0.28f };
        for (int i = 0; i < 5; i++) { var cr = Img(crackCont.transform, "Crack_" + i, CrackColor); cr.sprite = circle; var ccrt = cr.rectTransform; ccrt.anchorMin = ccrt.anchorMax = new Vector2(cx[i], cy[i]); ccrt.pivot = new Vector2(0.5f, 0.5f); ccrt.sizeDelta = new Vector2(38, 38); cr.raycastTarget = false; cr.gameObject.SetActive(false); slots[i] = ccrt; }
        mgr.bucketRoot = rrt; mgr.bucketBodyImage = body; mgr.waterFill = water; mgr.fishContainer = fishCont.GetComponent<RectTransform>(); mgr.crackContainer = crackCont.transform; mgr.crackSlots = slots; mgr.bucketLabel = label;
    }

    // =========================================================================
    // Fish anim, score popup, commentator, tutorial, feedback, result
    // (identical to previous version — condensed for brevity)
    // =========================================================================

    static void BuildFishAnim(RectTransform parent, EmailSwiperManager mgr, Sprite circle)
    {
        var go = RT("FishAnim", parent); var rrt = go.GetComponent<RectTransform>();
        rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 0.5f); rrt.pivot = new Vector2(0.5f, 0.5f); rrt.sizeDelta = new Vector2(72, 72);
        var img = go.AddComponent<Image>(); img.color = Color.white; img.sprite = null; img.preserveAspect = true; img.raycastTarget = false;
        var tail = Img(rrt, "Tail", SafeTeal); var trt = tail.rectTransform; trt.anchorMin = trt.anchorMax = new Vector2(1, 0.5f); trt.pivot = new Vector2(0, 0.5f); trt.sizeDelta = new Vector2(24, 32); tail.raycastTarget = false;
        var eye = Img(rrt, "Eye", Color.white); eye.sprite = circle; var ert = eye.rectTransform; ert.anchorMin = ert.anchorMax = new Vector2(0.28f, 0.62f); ert.pivot = new Vector2(0.5f, 0.5f); ert.sizeDelta = new Vector2(14, 14); eye.raycastTarget = false;
        var pupil = Img(ert, "Pupil", DarkText); pupil.sprite = circle; var purt = pupil.rectTransform; purt.anchorMin = purt.anchorMax = new Vector2(0.5f, 0.5f); purt.pivot = new Vector2(0.5f, 0.5f); purt.sizeDelta = new Vector2(7, 7); pupil.raycastTarget = false;
        go.SetActive(false); mgr.fishAnimRT = rrt; mgr.fishAnimImage = img;
    }

    static void BuildScorePopup(RectTransform parent, EmailSwiperManager mgr)
    {
        var go = RT("ScorePopup", parent); var rrt = go.GetComponent<RectTransform>();
        rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 0.5f); rrt.pivot = new Vector2(0.5f, 0.5f); rrt.sizeDelta = new Vector2(200, 60);
        var cg = go.AddComponent<CanvasGroup>(); cg.alpha = 0f; cg.blocksRaycasts = false;
        var txt = Txt(rrt, "Text", "+100", 36, ScoreGold, TextAlignmentOptions.Center, FontStyles.Bold); Stretch(txt.rectTransform);
        mgr.scorePopupRT = rrt; mgr.scorePopupText = txt; mgr.scorePopupCG = cg;
    }

    static Commentator BuildCommentator(RectTransform parent, Sprite circle)
    {
        var root = RT("Commentator", parent); var rrt = root.GetComponent<RectTransform>();
        rrt.anchorMin = rrt.anchorMax = Vector2.zero; rrt.pivot = Vector2.zero; rrt.sizeDelta = new Vector2(540, 120); rrt.anchoredPosition = new Vector2(30, 230);
        var portrait = Img(rrt, "Portrait", new Color(1f, 0.72f, 0.78f)); portrait.sprite = circle; portrait.preserveAspect = true;
        var prt = portrait.rectTransform; prt.anchorMin = new Vector2(0, 0); prt.anchorMax = new Vector2(0, 1); prt.pivot = new Vector2(0, 0.5f); prt.sizeDelta = new Vector2(100, 0);
        var letter = Txt(prt, "Letter", "G", 52, new Color(0.20f, 0.10f, 0.18f), TextAlignmentOptions.Center, FontStyles.Bold); Stretch(letter.rectTransform);
        var bubble = Img(rrt, "Bubble", new Color(1, 1, 1, 0.92f)); var bbrt = bubble.rectTransform; bbrt.anchorMin = Vector2.zero; bbrt.anchorMax = Vector2.one; bbrt.offsetMin = new Vector2(115, 0); bbrt.offsetMax = Vector2.zero;
        var speaker = Txt(bbrt, "Speaker", "Grandma", 15, new Color(0.78f, 0.30f, 0.50f), TextAlignmentOptions.MidlineLeft, FontStyles.Bold); var slrt = speaker.rectTransform; slrt.anchorMin = new Vector2(0, 1); slrt.anchorMax = new Vector2(1, 1); slrt.pivot = new Vector2(0, 1); slrt.sizeDelta = new Vector2(0, 22); slrt.anchoredPosition = new Vector2(14, -5);
        var bText = Txt(bbrt, "BubbleText", "…", 19, new Color(0.13f, 0.13f, 0.13f), TextAlignmentOptions.MidlineLeft); bText.textWrappingMode = TextWrappingModes.Normal;
        var btrt = bText.rectTransform; btrt.anchorMin = Vector2.zero; btrt.anchorMax = Vector2.one; btrt.offsetMin = new Vector2(14, 5); btrt.offsetMax = new Vector2(-14, -25);
        var comm = root.AddComponent<Commentator>(); comm.root = root; comm.portrait = portrait; comm.portraitLetter = letter; comm.bubbleBg = bubble; comm.bubbleText = bText; comm.speakerLabel = speaker; comm.portraitSprite = circle; comm.speakerName = "Grandma"; comm.portraitInitial = "G"; comm.portraitColor = new Color(1f, 0.72f, 0.78f);
        return comm;
    }

    static GameObject BuildTutorialPanel(RectTransform parent, EmailSwiperManager mgr)
    {
        var ov = Img(parent, "TutorialOverlay", new Color(0, 0, 0, 0.75f)); Stretch(ov.rectTransform); ov.raycastTarget = true;
        var card = Img(ov.rectTransform, "Card", CardWhite); var crt = card.rectTransform; crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f); crt.pivot = new Vector2(0.5f, 0.5f); crt.sizeDelta = new Vector2(820, 580);
        var hdr = Img(crt, "Header", BgOcean); TopStretch(hdr.rectTransform, 95);
        var hLbl = Txt(hdr.rectTransform, "Title", "Phish Patrol — Email Swiper", 38, Aqua, TextAlignmentOptions.Center, FontStyles.Bold); Stretch(hLbl.rectTransform); hLbl.raycastTarget = false;
        var body = Txt(crt, "Body", "Sort each email — SCAM or SAFE.\n\n◀  Drag or tap <b>LEFT</b> → <color=#FF6B6B><b>SCAM</b></color>\nDrag or tap <b>RIGHT</b> → <color=#4ECDC4><b>SAFE</b></color>  ▶\n\n✓ Correct → fish joins the bucket!\n✗ Wrong → pufferfish cracks it.\n<b>5 cracks</b> and the bucket breaks!", 24, DarkText, TextAlignmentOptions.Center);
        body.textWrappingMode = TextWrappingModes.Normal; var brt = body.rectTransform; brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = new Vector2(50, 110); brt.offsetMax = new Vector2(-50, -110);
        var btn = MakeButton(crt, "StartBtn", "Start!", 30, SafeTeal, Color.white); var sbrt = btn.GetComponent<RectTransform>(); sbrt.anchorMin = sbrt.anchorMax = new Vector2(0.5f, 0); sbrt.pivot = new Vector2(0.5f, 0); sbrt.sizeDelta = new Vector2(260, 66); sbrt.anchoredPosition = new Vector2(0, 28);
        UnityEventTools.AddPersistentListener(btn.GetComponent<Button>().onClick, mgr.OnTutorialStart);
        return ov.gameObject;
    }

    static GameObject BuildFeedbackPanel(RectTransform parent, EmailSwiperManager mgr)
    {
        var ov = Img(parent, "FeedbackOverlay", new Color(0, 0, 0, 0.45f)); Stretch(ov.rectTransform); ov.raycastTarget = true; ov.gameObject.AddComponent<CanvasGroup>();
        var card = Img(ov.rectTransform, "Card", Hex("#2ECC71")); var crt = card.rectTransform; crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.55f); crt.pivot = new Vector2(0.5f, 0.5f); crt.sizeDelta = new Vector2(960, 380);
        var title = Txt(crt, "Title", "Correct!", 52, Color.white, TextAlignmentOptions.Center, FontStyles.Bold); var trt = title.rectTransform; trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1); trt.pivot = new Vector2(0.5f, 1); trt.sizeDelta = new Vector2(-40, 80); trt.anchoredPosition = new Vector2(0, -24);
        var body2 = Txt(crt, "Body", "Explanation…", 23, Color.white, TextAlignmentOptions.Center); body2.textWrappingMode = TextWrappingModes.Normal; var brt2 = body2.rectTransform; brt2.anchorMin = Vector2.zero; brt2.anchorMax = Vector2.one; brt2.offsetMin = new Vector2(40, 24); brt2.offsetMax = new Vector2(-40, -110);
        mgr.feedbackBg = card; mgr.feedbackTitle = title; mgr.feedbackBody = body2;
        return ov.gameObject;
    }

    static GameObject BuildResultPanel(RectTransform parent, EmailSwiperManager mgr)
    {
        var ov = Img(parent, "ResultOverlay", new Color(0, 0, 0, 0.78f)); Stretch(ov.rectTransform); ov.raycastTarget = true;
        var card = Img(ov.rectTransform, "Card", CardWhite); var crt = card.rectTransform; crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f); crt.pivot = new Vector2(0.5f, 0.5f); crt.sizeDelta = new Vector2(820, 620);
        var hdr = Img(crt, "Header", BgOcean); TopStretch(hdr.rectTransform, 95);
        var hLbl = Txt(hdr.rectTransform, "Label", "Round Complete!", 40, Aqua, TextAlignmentOptions.Center, FontStyles.Bold); Stretch(hLbl.rectTransform); hLbl.raycastTarget = false;
        var stars = Txt(crt, "Stars", "★ ★ ★", 62, ScoreGold, TextAlignmentOptions.Center, FontStyles.Bold); var srt = stars.rectTransform; srt.anchorMin = new Vector2(0, 1); srt.anchorMax = new Vector2(1, 1); srt.pivot = new Vector2(0.5f, 1); srt.sizeDelta = new Vector2(0, 90); srt.anchoredPosition = new Vector2(0, -120);
        var score = Txt(crt, "Score", "0 / 800", 42, DarkText, TextAlignmentOptions.Center, FontStyles.Bold); var scrt = score.rectTransform; scrt.anchorMin = new Vector2(0, 1); scrt.anchorMax = new Vector2(1, 1); scrt.pivot = new Vector2(0.5f, 1); scrt.sizeDelta = new Vector2(0, 60); scrt.anchoredPosition = new Vector2(0, -220);
        var msg = Txt(crt, "Message", "Result", 21, MutedText, TextAlignmentOptions.Center); msg.textWrappingMode = TextWrappingModes.Normal; var mrt2 = msg.rectTransform; mrt2.anchorMin = Vector2.zero; mrt2.anchorMax = Vector2.one; mrt2.offsetMin = new Vector2(45, 120); mrt2.offsetMax = new Vector2(-45, -300);
        var play = MakeButton(crt, "PlayAgain", "Play Again", 25, SafeTeal, Color.white); var prrt = play.GetComponent<RectTransform>(); prrt.anchorMin = prrt.anchorMax = new Vector2(0.5f, 0); prrt.pivot = new Vector2(1, 0); prrt.sizeDelta = new Vector2(240, 60); prrt.anchoredPosition = new Vector2(-12, 36);
        UnityEventTools.AddPersistentListener(play.GetComponent<Button>().onClick, mgr.OnPlayAgain);
        var back = MakeButton(crt, "BackToMap", "Back to Map", 25, Hex("#636E72"), Color.white); var bart = back.GetComponent<RectTransform>(); bart.anchorMin = bart.anchorMax = new Vector2(0.5f, 0); bart.pivot = new Vector2(0, 0); bart.sizeDelta = new Vector2(240, 60); bart.anchoredPosition = new Vector2(12, 36);
        UnityEventTools.AddPersistentListener(back.GetComponent<Button>().onClick, mgr.OnReturnToMap);
        mgr.resultStars = stars; mgr.resultScore = score; mgr.resultMessage = msg;
        return ov.gameObject;
    }

    // =========================================================================
    // FIX 1: Striped border edge — child of card, moves with it
    // =========================================================================

    // Builds one border edge strip as a child of the card.
    // anchorMin/Max pin the strip to one edge; sizeDelta.x or .y sets its thickness.
    static void BuildStripedEdge(RectTransform cardRT, string name,
        (Color a, Color b, Color accent, string label) scheme,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 sizeDelta, Vector2 anchoredPos, bool horizontal)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(cardRT, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.sizeDelta = sizeDelta;
        rt.anchoredPosition = anchoredPos;

        // Base fill
        var bg = go.AddComponent<Image>(); bg.color = scheme.a; bg.raycastTarget = false;

        // 6 alternating stripes filling this strip
        int count = 6;
        for (int i = 0; i < count; i++)
        {
            float t0 = (float)i / count;
            float t1 = (float)(i + 1) / count;
            var stripe = Img(rt, "S" + i, i % 2 == 0 ? scheme.a : scheme.b);
            var srt = stripe.rectTransform;
            // Stripes run along the long axis of the edge
            srt.anchorMin = horizontal ? new Vector2(t0, 0) : new Vector2(0, t0);
            srt.anchorMax = horizontal ? new Vector2(t1, 1) : new Vector2(1, t1);
            srt.offsetMin = srt.offsetMax = Vector2.zero;
            stripe.raycastTarget = false;
        }

        // Accent line on the INNER edge of the strip (2px)
        var acc = Img(rt, "Acc", scheme.accent);
        var accRT = acc.rectTransform;
        // inner = far side from the card edge
        if (horizontal)
        {
            bool top = pivot.y >= 1f;
            accRT.anchorMin = top ? new Vector2(0, 0) : new Vector2(0, 0.86f);
            accRT.anchorMax = top ? new Vector2(1, 0.14f) : new Vector2(1, 1);
        }
        else
        {
            bool left = pivot.x <= 0f;
            accRT.anchorMin = left ? new Vector2(0.86f, 0) : new Vector2(0, 0);
            accRT.anchorMax = left ? new Vector2(1, 1) : new Vector2(0.14f, 1);
        }
        accRT.offsetMin = accRT.offsetMax = Vector2.zero;
        acc.raycastTarget = false;
    }

    // =========================================================================
    // FIX 3: Audio clip finder
    // =========================================================================

    static AudioClip FindAudioClip(string name)
    {
        foreach (var g in AssetDatabase.FindAssets(name + " t:AudioClip"))
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            if (System.IO.Path.GetFileNameWithoutExtension(path).ToLower() == name.ToLower())
            {
                var c = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (c != null) return c;
            }
        }
        // Fallback: partial match (covers renamed or slightly different filenames)
        foreach (var g in AssetDatabase.FindAssets(name))
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            if (System.IO.Path.GetFileNameWithoutExtension(path).ToLower().Contains(name.ToLower()))
            {
                var c = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (c != null) return c;
            }
        }
        return null;
    }

    static Sprite FindSprite(string name)
    {
        foreach (var g in AssetDatabase.FindAssets(name + " t:Sprite"))
        { var path = AssetDatabase.GUIDToAssetPath(g); if (System.IO.Path.GetFileNameWithoutExtension(path).ToLower() == name.ToLower()) { var s = AssetDatabase.LoadAssetAtPath<Sprite>(path); if (s != null) return s; } }
        foreach (var g in AssetDatabase.FindAssets(name + " t:Texture2D"))
        { var path = AssetDatabase.GUIDToAssetPath(g); if (System.IO.Path.GetFileNameWithoutExtension(path).ToLower().Contains(name.ToLower())) { var s = AssetDatabase.LoadAssetAtPath<Sprite>(path); if (s != null) return s; } }
        return null;
    }
    static void LogFound(string n, Sprite s) => Debug.Log($"[EmailSwiperBuilder] {n}: " + (s != null ? "✓" : "✗ not found"));
    static Sprite GetCircle() { try { var s = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"); if (s != null) return s; } catch { } return null; }
    static Image Img(Transform p, string n, Color c) { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); var img = go.AddComponent<Image>(); img.color = c; return img; }
    static TMP_Text Txt(Transform p, string n, string c, int s, Color col, TextAlignmentOptions a, FontStyles st = FontStyles.Normal) { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); var t = go.AddComponent<TextMeshProUGUI>(); t.text = c; t.fontSize = s; t.color = col; t.alignment = a; t.fontStyle = st; t.raycastTarget = false; return t; }
    static GameObject MakeButton(Transform p, string n, string lbl, int fs, Color bg, Color tc) { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); var img = go.AddComponent<Image>(); img.color = bg; var btn = go.AddComponent<Button>(); btn.targetGraphic = img; var t = Txt(go.transform, "Label", lbl, fs, tc, TextAlignmentOptions.Center, FontStyles.Bold); Stretch(t.rectTransform); return go; }
    static GameObject RT(string n, Transform p) { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); return go; }
    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
    static void TopStretch(RectTransform r, float h, float inset = 0) { r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1); r.pivot = new Vector2(0.5f, 1); r.sizeDelta = new Vector2(0, h); r.anchoredPosition = new Vector2(0, -inset); }
    static Color Hex(string h) => ColorUtility.TryParseHtmlString(h, out var c) ? c : Color.magenta;
    static void AddToBuild(string path) { var scenes = EditorBuildSettings.scenes.ToList(); if (!scenes.Any(s => s.path == path)) { scenes.Add(new EditorBuildSettingsScene(path, true)); EditorBuildSettings.scenes = scenes.ToArray(); } }
}