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
/// Builds the Email Swiper scene — Fishing Boat edition.
///
/// Layout (bottom → top of canvas):
///   0%  –  20%  Deep ocean (dark blue)
///   20% –  55%  Mid ocean
///   55% –  72%  Horizon / water-surface shimmer
///   72% –  78%  Horizon line (thin bright strip)
///   78% – 100%  Sky (lighter blue gradient)
///
/// Boat sits ON the horizon (bottom of boat = 72% of canvas height).
/// Two fishing nets hang from the sides of the boat.
/// Cards appear in the mid-ocean zone (centre of screen).
/// Fishing rod line runs from boat down to the card.
///
/// CLICK BUTTONS: SCAM (left) and SAFE (right) sit below the card,
///   The nets themselves are the click targets — no separate buttons needed.
/// </summary>
public static class EmailSwiperBuilder
{
    private const string ScenesDir = "Assets/Scenes";
    private const string ScenePath = "Assets/Scenes/EmailSwiper.unity";

    // ── Ocean palette ─────────────────────────────────────────────────
    private static readonly Color SkyTop = Hex("#6BB8D4");
    private static readonly Color SkyBottom = Hex("#A8D8EA");
    private static readonly Color HorizonLine = Hex("#E8F4F8");
    private static readonly Color OceanTop = Hex("#1A6B8A");
    private static readonly Color OceanMid = Hex("#0F4A6B");
    private static readonly Color OceanDeep = Hex("#071824");
    private static readonly Color WaveShine = new Color(0.55f, 0.85f, 1.00f, 0.18f);
    private static readonly Color WaveGlint = new Color(1.00f, 1.00f, 1.00f, 0.08f);

    // ── Boat palette ──────────────────────────────────────────────────
    private static readonly Color BoatHull = Hex("#C8341A");   // red hull
    private static readonly Color BoatDeck = Hex("#E8D5A0");   // wooden deck
    private static readonly Color BoatCabin = Hex("#FAFAFA");   // white cabin
    private static readonly Color BoatAccent = Hex("#1A3A6B");   // dark blue trim
    private static readonly Color BoatMast = Hex("#7A5230");   // wooden mast
    private static readonly Color NetColor = new Color(0.85f, 0.78f, 0.55f, 0.90f); // rope net
    private static readonly Color NetLabel = Hex("#1A3A6B");
    private static readonly Color CrackColor = Hex("#FDCB6E");

    // ── HUD / UI palette ──────────────────────────────────────────────
    private static readonly Color HudBg = new Color(0.04f, 0.08f, 0.15f, 0.92f);
    private static readonly Color HudBorder = Hex("#1E3A5A");
    private static readonly Color ScoreGold = Hex("#FFD93D");
    private static readonly Color StreakOrange = Hex("#FF9F1C");
    private static readonly Color HeartFull = new Color(0.91f, 0.30f, 0.24f);
    private static readonly Color CardWhite = Hex("#FAFBFF");
    private static readonly Color CardShadow = new Color(0, 0, 0, 0.25f);
    private static readonly Color DarkText = Hex("#1A1A2E");
    private static readonly Color MutedText = Hex("#636E72");
    private static readonly Color LightText = Hex("#DFE6E9");
    private static readonly Color DividerCol = Hex("#E8EAED");
    private static readonly Color ScamRed = Hex("#FF6B6B");
    private static readonly Color SafeTeal = Hex("#4ECDC4");
    private static readonly Color ScamBg = new Color(0.85f, 0.20f, 0.20f, 0.88f);
    private static readonly Color SafeBg = new Color(0.18f, 0.70f, 0.65f, 0.88f);
    private static readonly Color RodLine = new Color(0.90f, 0.75f, 0.40f, 0.92f);
    private static readonly Color BobCol = new Color(0.95f, 0.95f, 1.00f, 0.95f);
    private static readonly Color BobRing = new Color(0.60f, 0.80f, 1.00f, 0.70f);
    private static readonly Color Aqua = Hex("#81ECEC");

    // ── Fish border schemes ───────────────────────────────────────────
    private static readonly (Color a, Color b, Color accent, string name)[] FishSchemes =
    {
        (Hex("#FF6B1A"), Color.white,   Hex("#1A1A1A"), "Clownfish"),
        (Hex("#1A6EFF"), Hex("#FFE033"),Color.white,    "Blue Tang"),
        (Hex("#F5C518"), Hex("#6B3A1A"),Hex("#FF8C00"), "Pufferfish"),
        (Hex("#1A1A1A"), Hex("#D4AF37"),Hex("#C0C0C0"), "Angelfish"),
        (Hex("#FF8C8C"), Hex("#C8C8C8"),Hex("#FF4477"), "Salmon"),
        (Hex("#00CED1"), Hex("#FF00AA"),Hex("#AAFF00"), "Parrotfish"),
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
        Sprite spPhisherman = FindSprite("phisherman");
        LogFound("fish_normal", fishNorm);
        LogFound("fish_puffer", fishPuff);
        LogFound("phisherman", spPhisherman);

        // Camera
        var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>(); camGo.AddComponent<AudioListener>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = OceanMid;  // matches BgBase so no black shows behind canvas
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

        // Manager + 2 AudioSources
        var mgrGo = new GameObject("GameManager");
        var mgr = mgrGo.AddComponent<EmailSwiperManager>();
        var sfxSrc = mgrGo.AddComponent<AudioSource>(); sfxSrc.playOnAwake = false;
        var musSrc = mgrGo.AddComponent<AudioSource>(); musSrc.playOnAwake = false; musSrc.loop = true;
        mgr.circleSprite = circle;
        mgr.fishNormalSprite = fishNorm;
        mgr.fishPufferSprite = fishPuff;
        mgr.bgMusic = FindAudioClip("frutiger_music_fresh_waters");
        mgr.sfxCardFlip = FindAudioClip("card_flip");
        mgr.sfxCorrect = FindAudioClip("water_splash");
        mgr.sfxWrong = FindAudioClip("glass_crack");
        Debug.Log($"[EmailSwiperBuilder] bgMusic:     " + (mgr.bgMusic != null ? "✓" : "✗"));
        Debug.Log($"[EmailSwiperBuilder] sfxCardFlip: " + (mgr.sfxCardFlip != null ? "✓" : "✗"));
        Debug.Log($"[EmailSwiperBuilder] sfxCorrect:  " + (mgr.sfxCorrect != null ? "✓" : "✗"));
        Debug.Log($"[EmailSwiperBuilder] sfxWrong:    " + (mgr.sfxWrong != null ? "✓" : "✗"));

        // ── Build layers ──────────────────────────────────────────────
        BuildOceanBackground(canvasRT);
        BuildHud(canvasRT, mgr, circle);
        RectTransform phishermanHandRT = null;   // set by BuildBoatAndNets, used by BuildRod
        BuildBoatAndNets(canvasRT, mgr, circle, spPhisherman, out phishermanHandRT);

        // Game root — expanded now that separate buttons are removed
        var grImg = Img(canvasRT, "GameRoot", new Color(0, 0, 0, 0));
        var grRT = grImg.rectTransform;
        grRT.anchorMin = new Vector2(0, 0.08f); grRT.anchorMax = new Vector2(1, 0.70f);
        grRT.offsetMin = grRT.offsetMax = Vector2.zero;
        grImg.raycastTarget = false;

        // Swipe zones (invisible overlays wired to SwipeCard indicators)
        CanvasGroup scamCG, safeCG;
        BuildSwipeZones(grRT, out scamCG, out safeCG);

        // Email card — creates mgr.swipeCard
        BuildCard(grRT, mgr, scamCG, safeCG, canvas);

        // ── CRITICAL: wire every relay (including net roots built earlier) to the card ──
        // BuildCard's internal FindObjectsByType call covers relays built before it,
        // but we do it explicitly here too so the order of Build() calls doesn't matter.
        foreach (var relay in GameObject.FindObjectsByType<SwipeButtonRelay>(FindObjectsSortMode.None))
            relay.target = mgr.swipeCard;

        // Fishing rod — tip comes from phisherman's hand position
        BuildRod(canvasRT, mgr.swipeCard, canvas, circle, phishermanHandRT);

        // NOTE: No separate click buttons — nets ARE the buttons (wired in BuildNet).

        // Fish anim (arc from card up to net)
        BuildFishAnim(canvasRT, mgr, circle);

        // Score popup
        BuildScorePopup(canvasRT, mgr);

        // Commentator
        mgr.commentator = BuildCommentator(canvasRT, circle);

        // Panels
        var feedback = BuildFeedbackPanel(canvasRT, mgr);
        var tutorial = BuildTutorialPanel(canvasRT, mgr);
        var result = BuildResultPanel(canvasRT, mgr);

        mgr.gameRoot = grImg.gameObject;
        // hudPanel is set inside BuildHud() above — no extra call needed.
        mgr.feedbackPanel = feedback;
        mgr.tutorialPanel = tutorial;
        mgr.resultPanel = result;

        // Initial panel visibility
        grImg.gameObject.SetActive(false);
        feedback.SetActive(false);
        result.SetActive(false);
        tutorial.SetActive(true);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuild(ScenePath);
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log($"[EmailSwiperBuilder] Built → {ScenePath}");
    }

    // =========================================================================
    // Ocean background — sky + ocean layers + horizon
    // =========================================================================

    static void BuildOceanBackground(RectTransform canvasRT)
    {
        // Base fills the entire canvas with mid-ocean blue — no black gaps anywhere
        var bg = Img(canvasRT, "BgBase", OceanMid); Stretch(bg.rectTransform); bg.raycastTarget = false;

        // Sky (top 30%)
        var sky = Img(canvasRT, "Sky", SkyBottom);
        var sr = sky.rectTransform;
        sr.anchorMin = new Vector2(0, 0.70f); sr.anchorMax = Vector2.one;
        sr.offsetMin = sr.offsetMax = Vector2.zero; sky.raycastTarget = false;

        var skyTop = Img(canvasRT, "SkyTop", SkyTop);
        var str = skyTop.rectTransform;
        str.anchorMin = new Vector2(0, 0.85f); str.anchorMax = Vector2.one;
        str.offsetMin = str.offsetMax = Vector2.zero; skyTop.raycastTarget = false;

        // Ocean surface (lighter blue just below horizon)
        var ocTop = Img(canvasRT, "OceanTop", OceanTop);
        var otr = ocTop.rectTransform;
        otr.anchorMin = new Vector2(0, 0.55f); otr.anchorMax = new Vector2(1, 0.72f);
        otr.offsetMin = otr.offsetMax = Vector2.zero; ocTop.raycastTarget = false;

        // Subtle deep-water tint at bottom — semi-transparent so BgBase blue shows through
        var ocDeep = Img(canvasRT, "OceanDepthTint", new Color(0.03f, 0.08f, 0.15f, 0.55f));
        var odr = ocDeep.rectTransform;
        odr.anchorMin = Vector2.zero; odr.anchorMax = new Vector2(1, 0.25f);
        odr.offsetMin = odr.offsetMax = Vector2.zero; ocDeep.raycastTarget = false;

        // Horizon bright line
        var hLine = Img(canvasRT, "HorizonLine", HorizonLine);
        var hlr = hLine.rectTransform;
        hlr.anchorMin = new Vector2(0, 0.718f); hlr.anchorMax = new Vector2(1, 0.724f);
        hlr.offsetMin = hlr.offsetMax = Vector2.zero; hLine.raycastTarget = false;

        // Wave shimmer strips
        float[] wY = { 0.60f, 0.50f, 0.42f, 0.35f, 0.25f, 0.14f };
        float[] wH = { 0.018f, 0.012f, 0.010f, 0.008f, 0.007f, 0.006f };
        for (int i = 0; i < wY.Length; i++)
        {
            var wave = Img(canvasRT, "Wave_" + i, i % 2 == 0 ? WaveShine : WaveGlint);
            var wr = wave.rectTransform;
            wr.anchorMin = new Vector2(0, wY[i]);
            wr.anchorMax = new Vector2(1, wY[i] + wH[i]);
            wr.offsetMin = wr.offsetMax = Vector2.zero;
            wave.raycastTarget = false;
        }
    }

    // =========================================================================
    // HUD (top bar)
    // =========================================================================

    // Returns the GameObject (stored in mgr.hudPanel)
    static GameObject BuildHudGO(RectTransform parent, EmailSwiperManager mgr, Sprite circle)
    {
        // This is a lightweight wrapper — the actual HUD was built in BuildHud()
        // We just return the already-built HUD GO
        var existing = parent.Find("HUD");
        return existing != null ? existing.gameObject : new GameObject("HUD_ref");
    }

    static void BuildHud(RectTransform parent, EmailSwiperManager mgr, Sprite circle)
    {
        var hud = Img(parent, "HUD", HudBg);
        TopStretch(hud.rectTransform, 90); hud.raycastTarget = true;

        var bord = Img(hud.rectTransform, "Border", HudBorder);
        var brrt = bord.rectTransform;
        brrt.anchorMin = Vector2.zero; brrt.anchorMax = new Vector2(1, 0);
        brrt.pivot = new Vector2(0.5f, 0); brrt.sizeDelta = new Vector2(0, 2);
        bord.raycastTarget = false;

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
        var ttrt = timerTxt.rectTransform;
        ttrt.anchorMin = new Vector2(0, 0.45f); ttrt.anchorMax = Vector2.one; ttrt.offsetMin = ttrt.offsetMax = Vector2.zero;

        var barBg = Img(thr, "TimerBarBg", new Color(1, 1, 1, 0.12f));
        var bbrt = barBg.rectTransform;
        bbrt.anchorMin = new Vector2(0, 0.08f); bbrt.anchorMax = new Vector2(1, 0.38f);
        bbrt.offsetMin = new Vector2(12, 0); bbrt.offsetMax = new Vector2(-12, 0); barBg.raycastTarget = false;

        var fill = Img(bbrt, "TimerFill", SafeTeal); Stretch(fill.rectTransform);
        fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0; fill.fillAmount = 1f; fill.raycastTarget = false;

        var progTxt = Txt(thr, "Progress", "1 / 8", 18, LightText, TextAlignmentOptions.Center);
        var prt = progTxt.rectTransform;
        prt.anchorMin = Vector2.zero; prt.anchorMax = new Vector2(1, 0.22f); prt.offsetMin = prt.offsetMax = Vector2.zero;

        // Score (right)
        var sH = RT("ScoreHolder", hud.rectTransform);
        var shr = sH.GetComponent<RectTransform>();
        shr.anchorMin = new Vector2(1, 0); shr.anchorMax = new Vector2(1, 1);
        shr.pivot = new Vector2(1, 0.5f); shr.sizeDelta = new Vector2(340, 0);
        shr.anchoredPosition = new Vector2(-24, 0);

        var scoreTxt = Txt(shr, "ScoreText", "Score: 0", 36, ScoreGold, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
        var srrt = scoreTxt.rectTransform;
        srrt.anchorMin = new Vector2(0, 0.38f); srrt.anchorMax = Vector2.one; srrt.offsetMin = srrt.offsetMax = Vector2.zero;

        var streakTxt = Txt(shr, "StreakText", "", 22, StreakOrange, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
        var skrt = streakTxt.rectTransform;
        skrt.anchorMin = Vector2.zero; skrt.anchorMax = new Vector2(1, 0.38f); skrt.offsetMin = skrt.offsetMax = Vector2.zero;

        mgr.timerText = timerTxt; mgr.timerFill = fill;
        mgr.scoreText = scoreTxt; mgr.streakText = streakTxt;
        mgr.progressText = progTxt;
        // Store HUD ref directly in mgr.hudPanel
        mgr.hudPanel = hud.gameObject;
        hud.gameObject.SetActive(false);
    }

    // =========================================================================
    // Boat + two fishing nets  (sits at the horizon, top 28% of canvas)
    // =========================================================================

    static void BuildBoatAndNets(RectTransform canvasRT, EmailSwiperManager mgr,
        Sprite circle, Sprite spPhisherman, out RectTransform phishermanHandRT)
    {
        // Root group pinned to the horizon line
        var boatRoot = new GameObject("BoatRoot", typeof(RectTransform));
        boatRoot.transform.SetParent(canvasRT, false);
        var brRT = boatRoot.GetComponent<RectTransform>();
        // Boat sits with its bottom at 72% of canvas, spanning ~45% width centred
        brRT.anchorMin = new Vector2(0.275f, 0.68f);
        brRT.anchorMax = new Vector2(0.725f, 0.99f);
        brRT.offsetMin = brRT.offsetMax = Vector2.zero;
        mgr.boatRoot = brRT;

        // ── Hull (red) ──
        var hull = Img(brRT, "Hull", BoatHull);
        var hrt = hull.rectTransform;
        hrt.anchorMin = new Vector2(0.05f, 0.00f); hrt.anchorMax = new Vector2(0.95f, 0.35f);
        hrt.offsetMin = hrt.offsetMax = Vector2.zero; hull.raycastTarget = false;
        mgr.boatHullRT = hrt;

        // Hull accent stripe
        var stripe = Img(hrt, "HullStripe", BoatAccent);
        var strt = stripe.rectTransform;
        strt.anchorMin = new Vector2(0, 0.70f); strt.anchorMax = new Vector2(1, 0.90f);
        strt.offsetMin = strt.offsetMax = Vector2.zero; stripe.raycastTarget = false;

        // ── Deck ──
        var deck = Img(brRT, "Deck", BoatDeck);
        var drt = deck.rectTransform;
        drt.anchorMin = new Vector2(0.03f, 0.32f); drt.anchorMax = new Vector2(0.97f, 0.44f);
        drt.offsetMin = drt.offsetMax = Vector2.zero; deck.raycastTarget = false;

        // ── Cabin ──
        var cabin = Img(brRT, "Cabin", BoatCabin);
        var cabRT = cabin.rectTransform;
        cabRT.anchorMin = new Vector2(0.30f, 0.42f); cabRT.anchorMax = new Vector2(0.70f, 0.82f);
        cabRT.offsetMin = cabRT.offsetMax = Vector2.zero; cabin.raycastTarget = false;

        // Cabin windows
        foreach (float wx in new[] { 0.33f, 0.67f })
        {
            var win = Img(cabRT, "Window", BoatAccent);
            win.sprite = circle; win.preserveAspect = true;
            var wrt = win.rectTransform;
            wrt.anchorMin = wrt.anchorMax = new Vector2(wx, 0.5f); wrt.pivot = new Vector2(0.5f, 0.5f);
            wrt.sizeDelta = new Vector2(22, 22); win.raycastTarget = false;
        }

        // Cabin accent trim
        var trim = Img(cabRT, "CabinTrim", BoatAccent);
        var trimRT = trim.rectTransform;
        trimRT.anchorMin = new Vector2(0, 0); trimRT.anchorMax = new Vector2(1, 0.08f);
        trimRT.offsetMin = trimRT.offsetMax = Vector2.zero; trim.raycastTarget = false;

        // ── Mast (centre, tallest element) ──
        var mast = Img(brRT, "Mast", BoatMast);
        var mrt2 = mast.rectTransform;
        mrt2.anchorMin = new Vector2(0.47f, 0.42f); mrt2.anchorMax = new Vector2(0.53f, 1.00f);
        mrt2.offsetMin = mrt2.offsetMax = Vector2.zero; mast.raycastTarget = false;

        // ── Flag on mast ──
        var flag = Img(brRT, "Flag", ScamRed);
        var flagRT = flag.rectTransform;
        flagRT.anchorMin = new Vector2(0.53f, 0.86f); flagRT.anchorMax = new Vector2(0.65f, 0.98f);
        flagRT.offsetMin = flagRT.offsetMax = Vector2.zero; flag.raycastTarget = false;

        // ── Phisherman standing at the base of the mast on the deck ──
        // Anchored inside brRT: x centred on mast (0.35–0.65), y from deck (0.38) to cabin top (~0.82)
        var phGo = new GameObject("Phisherman", typeof(RectTransform));
        phGo.transform.SetParent(brRT, false);
        var phRT = phGo.GetComponent<RectTransform>();
        phRT.anchorMin = new Vector2(0.32f, 0.38f);
        phRT.anchorMax = new Vector2(0.52f, 0.84f);
        phRT.offsetMin = phRT.offsetMax = Vector2.zero;
        var phImg = phGo.AddComponent<Image>();
        phImg.color = Color.white;
        phImg.preserveAspect = true;
        phImg.raycastTarget = false;
        if (spPhisherman != null) phImg.sprite = spPhisherman;
        else { phImg.color = new Color(1f, 0.85f, 0.65f); }  // skin-tone fallback

        // ── Hand anchor — the rod tip comes from here ──
        // Positioned at his left hand: upper-left of the phisherman rect
        // (roughly 20% from left, 75% up — where a fishing rod hand would be)
        var handGo = new GameObject("PhishermanHand", typeof(RectTransform));
        handGo.transform.SetParent(phRT, false);
        var handRT = handGo.GetComponent<RectTransform>();
        handRT.anchorMin = handRT.anchorMax = new Vector2(0.20f, 0.75f);
        handRT.pivot = new Vector2(0.5f, 0.5f);
        handRT.sizeDelta = new Vector2(4, 4);
        phishermanHandRT = handRT;   // returned to Build() for BuildRod

        // ── Crack slots on hull ──
        var crackSlots = new RectTransform[5];
        float[] crackX = { 0.15f, 0.30f, 0.50f, 0.70f, 0.85f };
        float[] crackY = { 0.15f, 0.22f, 0.12f, 0.20f, 0.14f };
        for (int i = 0; i < 5; i++)
        {
            var cr = Img(brRT, "Crack_" + i, CrackColor);
            cr.sprite = circle;
            var ccRT = cr.rectTransform;
            ccRT.anchorMin = ccRT.anchorMax = new Vector2(crackX[i], crackY[i]);
            ccRT.pivot = new Vector2(0.5f, 0.5f); ccRT.sizeDelta = new Vector2(32, 32);
            cr.raycastTarget = false; cr.gameObject.SetActive(false);
            crackSlots[i] = ccRT;
        }
        mgr.boatCrackSlots = crackSlots;

        // ── LEFT NET (SCAM side) ──
        BuildNet(canvasRT, brRT, mgr, isLeft: true, circle);

        // ── RIGHT NET (SAFE side) ──
        BuildNet(canvasRT, brRT, mgr, isLeft: false, circle);
    }

    /// <summary>
    /// Builds one fishing net hanging from the side of the boat.
    /// The net post extends outward, net hangs below in world space.
    /// </summary>
    static void BuildNet(RectTransform canvasRT, RectTransform boatRT,
        EmailSwiperManager mgr, bool isLeft, Sprite circle)
    {
        string side = isLeft ? "Left" : "Right";
        Color col = isLeft ? ScamRed : SafeTeal;
        Color colDim = new Color(col.r, col.g, col.b, 0.55f);
        string label = isLeft ? "SCAM" : "SAFE";
        int swipeDir = isLeft ? -1 : 1;

        // ── Net root — compact, flanking the boat just like the original design.
        //    Sits beside the boat in the horizon zone, not a full-height column.
        var netRoot = new GameObject(side + "NetRoot", typeof(RectTransform));
        netRoot.transform.SetParent(canvasRT, false);
        var nrRT = netRoot.GetComponent<RectTransform>();

        // Left net: x 0–26%.  Right net: x 74–100%.
        // Vertically: just below the boat top (74%) down to ~48% — compact, beside boat.
        float xMin = isLeft ? 0.01f : 0.74f;
        float xMax = isLeft ? 0.26f : 0.99f;
        nrRT.anchorMin = new Vector2(xMin, 0.48f);
        nrRT.anchorMax = new Vector2(xMax, 0.76f);
        nrRT.offsetMin = nrRT.offsetMax = Vector2.zero;

        // ── Translucent background — this is what the Button targets ──
        var bgImg = netRoot.AddComponent<Image>();
        bgImg.color = new Color(col.r, col.g, col.b, 0.08f);
        bgImg.raycastTarget = true;

        // ── Button on the net root so clicking anywhere on the net fires the swipe ──
        var netBtn = netRoot.AddComponent<Button>();
        netBtn.targetGraphic = bgImg;
        var cb = netBtn.colors;
        cb.normalColor = new Color(col.r, col.g, col.b, 0.08f);
        cb.highlightedColor = new Color(col.r, col.g, col.b, 0.25f);
        cb.pressedColor = new Color(col.r, col.g, col.b, 0.42f);
        cb.selectedColor = cb.normalColor;
        netBtn.colors = cb;

        // ── Relay — target wired explicitly after SwipeCard is built ──
        var relay = netRoot.AddComponent<SwipeButtonRelay>();
        relay.direction = swipeDir;
        netBtn.onClick.AddListener(relay.Fire);

        // ── Horizontal arm from boat side to net ──
        var arm = Img(nrRT, "Arm", BoatMast);
        var armRT = arm.rectTransform;
        armRT.anchorMin = isLeft ? new Vector2(0.60f, 0.90f) : new Vector2(0.00f, 0.90f);
        armRT.anchorMax = isLeft ? new Vector2(1.00f, 0.96f) : new Vector2(0.40f, 0.96f);
        armRT.offsetMin = armRT.offsetMax = Vector2.zero;
        arm.raycastTarget = false;

        // ── Vertical rope lines ──
        foreach (float rx in new[] { 0.10f, 0.90f })
        {
            var rope = Img(nrRT, "Rope_" + rx, NetColor);
            var rRT = rope.rectTransform;
            rRT.anchorMin = new Vector2(rx - 0.025f, 0.14f);
            rRT.anchorMax = new Vector2(rx + 0.025f, 0.92f);
            rRT.offsetMin = rRT.offsetMax = Vector2.zero;
            rope.raycastTarget = false;
        }

        // ── Net bag ──
        var netBag = Img(nrRT, "NetBag", new Color(col.r, col.g, col.b, 0.20f));
        var nbRT = netBag.rectTransform;
        nbRT.anchorMin = new Vector2(0.08f, 0.14f);
        nbRT.anchorMax = new Vector2(0.92f, 0.92f);
        nbRT.offsetMin = nbRT.offsetMax = Vector2.zero;
        netBag.raycastTarget = false;

        // Horizontal grid lines
        for (int row = 1; row < 4; row++)
        {
            float fy = (float)row / 4f;
            var gridH = Img(nbRT, "GridH_" + row, colDim);
            var ghRT = gridH.rectTransform;
            ghRT.anchorMin = new Vector2(0, fy - 0.018f);
            ghRT.anchorMax = new Vector2(1, fy + 0.018f);
            ghRT.offsetMin = ghRT.offsetMax = Vector2.zero;
            gridH.raycastTarget = false;
        }

        // Vertical grid lines
        for (int col2 = 1; col2 < 4; col2++)
        {
            float fx = (float)col2 / 4f;
            var gridV = Img(nbRT, "GridV_" + col2, colDim);
            var gvRT = gridV.rectTransform;
            gvRT.anchorMin = new Vector2(fx - 0.015f, 0);
            gvRT.anchorMax = new Vector2(fx + 0.015f, 1);
            gvRT.offsetMin = gvRT.offsetMax = Vector2.zero;
            gridV.raycastTarget = false;
        }

        // ── Simple SCAM / SAFE label below the net (just the word, no arrows) ──
        var netLbl = Txt(nrRT, "NetLabel", label, 32, col,
            TextAlignmentOptions.Center, FontStyles.Bold);
        var nlRT = netLbl.rectTransform;
        nlRT.anchorMin = new Vector2(0.02f, 0.00f);
        nlRT.anchorMax = new Vector2(0.98f, 0.16f);
        nlRT.offsetMin = nlRT.offsetMax = Vector2.zero;
        netLbl.raycastTarget = false;

        // ── Fish container inside net bag ──
        var fishCont = RT(side + "FishCont", nbRT);
        Stretch(fishCont.GetComponent<RectTransform>());
        var fishContRT = fishCont.GetComponent<RectTransform>();

        // ── Hover animation — makes the net read as a clickable button ──
        // Lifts + sways the net and brightens its mesh/label while the pointer
        // is over it. This pairs with the Button click already wired above so
        // players can either swipe the card OR click a net. It animates only
        // position + rotation (never scale), so it doesn't fight the catch
        // bounce in EmailSwiperManager.NetBounce().
        var netHover = netRoot.AddComponent<NetHoverEffect>();
        netHover.lift = 14f;
        netHover.swayAngle = 2.5f;
        netHover.swaySpeed = 2.4f;
        netHover.lerpSpeed = 12f;
        netHover.glowBoost = 0.16f;
        netHover.glowGraphics = new Graphic[] { netBag, netLbl };

        if (isLeft)
        {
            mgr.leftNetRT = nrRT;
            mgr.leftNetFishContainer = fishContRT;
        }
        else
        {
            mgr.rightNetRT = nrRT;
            mgr.rightNetFishContainer = fishContRT;
        }
    }

    // =========================================================================
    // Swipe zones (invisible overlays wired to SwipeCard indicators)
    // =========================================================================

    static void BuildSwipeZones(RectTransform parent,
        out CanvasGroup scamCG, out CanvasGroup safeCG)
    {
        scamCG = MakeZoneCG(parent, "ScamZone", 0.08f, -1);
        safeCG = MakeZoneCG(parent, "SafeZone", 0.92f, +1);
    }

    static CanvasGroup MakeZoneCG(RectTransform parent, string name, float xAnchor, int dir)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(xAnchor - 0.08f, 0.15f);
        rt.anchorMax = new Vector2(xAnchor + 0.08f, 0.90f);
        // NOTE: these zones used to be edge click-buttons, but they sat ON TOP
        // of the fishing nets and stole their hover/click events (worst on the
        // SCAM side, where the zone covered the whole label). The nets are now
        // the click targets, so the zones are kept ONLY as no-op indicator
        // holders for SwipeCard and must NOT receive any raycasts.
        var bg = go.AddComponent<Image>(); bg.color = new Color(0, 0, 0, 0); bg.raycastTarget = false;
        var cg = go.AddComponent<CanvasGroup>(); cg.alpha = 0f; cg.blocksRaycasts = false; cg.interactable = false;
        return cg;
    }

    // =========================================================================
    // Email card
    // =========================================================================

    static void BuildCard(RectTransform parent, EmailSwiperManager mgr,
        CanvasGroup scamHint, CanvasGroup safeHint, Canvas rootCanvas)
    {
        var scheme = FishSchemes[0];

        // Shadow
        var shadow = Img(parent, "CardShadow", CardShadow);
        var shrt = shadow.rectTransform;
        shrt.anchorMin = shrt.anchorMax = new Vector2(0.5f, 0.52f);
        shrt.pivot = new Vector2(0.5f, 0.5f); shrt.sizeDelta = new Vector2(628, 668);
        shrt.anchoredPosition = new Vector2(8, -8); shadow.raycastTarget = false;

        // Card face
        var card = Img(parent, "EmailCard", new Color(0, 0, 0, 0));
        var crt = card.rectTransform;
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.52f);
        crt.pivot = new Vector2(0.5f, 0.5f); crt.sizeDelta = new Vector2(620, 660);
        card.raycastTarget = false;
        var cardCG = card.gameObject.AddComponent<CanvasGroup>();

        // White background
        var cardBg = Img(crt, "CardBg", CardWhite); Stretch(cardBg.rectTransform); cardBg.raycastTarget = false;

        // Fish-stripe border
        float borderW = 12f;
        BuildStripedEdge(crt, "BorderTop", scheme, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, borderW), Vector2.zero, true);
        BuildStripedEdge(crt, "BorderBottom", scheme, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, borderW), Vector2.zero, true);
        BuildStripedEdge(crt, "BorderLeft", scheme, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(borderW, 0), Vector2.zero, false);
        BuildStripedEdge(crt, "BorderRight", scheme, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(borderW, 0), Vector2.zero, false);

        // Dot row at bottom
        BuildDotRow(crt, 10);

        // Sender header strip
        var headerStrip = Img(crt, "HeaderStrip", Hex("#1A73E8"));
        var hsrt = headerStrip.rectTransform;
        hsrt.anchorMin = new Vector2(0, 1); hsrt.anchorMax = new Vector2(1, 1);
        hsrt.pivot = new Vector2(0.5f, 1); hsrt.sizeDelta = new Vector2(0, 5);
        headerStrip.raycastTarget = false;

        // Avatar
        var avatar = Img(crt, "Avatar", Hex("#1A73E8"));
        avatar.sprite = GetCircle(); avatar.preserveAspect = true;
        var avrt = avatar.rectTransform;
        avrt.anchorMin = avrt.anchorMax = new Vector2(0, 1); avrt.pivot = new Vector2(0, 1);
        avrt.sizeDelta = new Vector2(54, 54); avrt.anchoredPosition = new Vector2(24, -44);
        avatar.raycastTarget = false;

        var avatarLetter = Txt(avrt, "Letter", "P", 30, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(avatarLetter.rectTransform);

        // Sender name
        var senderName = Txt(crt, "SenderName", "Sender", 22, DarkText, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        var snrt = senderName.rectTransform;
        snrt.anchorMin = new Vector2(0, 1); snrt.anchorMax = new Vector2(1, 1); snrt.pivot = new Vector2(0, 1);
        snrt.sizeDelta = new Vector2(-130, 30); snrt.anchoredPosition = new Vector2(94, -46);

        // Sender email
        var senderEmail = Txt(crt, "SenderEmail", "sender@example.com", 16, MutedText, TextAlignmentOptions.MidlineLeft);
        var sert = senderEmail.rectTransform;
        sert.anchorMin = new Vector2(0, 1); sert.anchorMax = new Vector2(1, 1); sert.pivot = new Vector2(0, 1);
        sert.sizeDelta = new Vector2(-130, 22); sert.anchoredPosition = new Vector2(94, -78);

        // Divider
        var div = Img(crt, "Divider", DividerCol);
        var dvrt = div.rectTransform;
        dvrt.anchorMin = new Vector2(0, 1); dvrt.anchorMax = new Vector2(1, 1); dvrt.pivot = new Vector2(0.5f, 1);
        dvrt.sizeDelta = new Vector2(-36, 1); dvrt.anchoredPosition = new Vector2(0, -110);
        div.raycastTarget = false;

        // Subject
        var subject = Txt(crt, "Subject", "Subject", 24, DarkText, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        var sjrt = subject.rectTransform;
        sjrt.anchorMin = new Vector2(0, 1); sjrt.anchorMax = new Vector2(1, 1); sjrt.pivot = new Vector2(0, 1);
        sjrt.sizeDelta = new Vector2(-44, 60); sjrt.anchoredPosition = new Vector2(22, -124);

        // Body — nudged down a little (offsetMax.y reduced to push top edge down)
        var body = Txt(crt, "Body", "Body text…", 19, DarkText, TextAlignmentOptions.TopLeft);
        body.textWrappingMode = TextWrappingModes.Normal; body.overflowMode = TextOverflowModes.Ellipsis;
        var brt = body.rectTransform;
        brt.anchorMin = new Vector2(0, 0); brt.anchorMax = new Vector2(1, 1);
        brt.offsetMin = new Vector2(22, 26); brt.offsetMax = new Vector2(-22, -228);

        // Drag overlay (SwipeCard lives here)
        var overlay = Img(parent, "DragOverlay", new Color(0, 0, 0, 0));
        var olrt = overlay.rectTransform;
        olrt.anchorMin = olrt.anchorMax = new Vector2(0.5f, 0.52f); olrt.pivot = new Vector2(0.5f, 0.5f);
        olrt.sizeDelta = new Vector2(640, 680); overlay.raycastTarget = true;

        var swipe = overlay.gameObject.AddComponent<SwipeCard>();
        swipe.cardRoot = crt;
        swipe.scamIndicator = scamHint;
        swipe.safeIndicator = safeHint;
        swipe.cardBorder = card;
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
    // Dot row (bottom of card)
    // =========================================================================

    static void BuildDotRow(RectTransform cardRT, int dotCount)
    {
        var rowGo = new GameObject("DotRow", typeof(RectTransform));
        rowGo.transform.SetParent(cardRT, false);
        var rrt = rowGo.GetComponent<RectTransform>();
        rrt.anchorMin = new Vector2(0, 0); rrt.anchorMax = new Vector2(1, 0);
        rrt.pivot = new Vector2(0.5f, 0); rrt.sizeDelta = new Vector2(0, 22);
        rrt.anchoredPosition = new Vector2(0, 6);

        var hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter; hlg.spacing = 5;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;
        hlg.childControlWidth = hlg.childControlHeight = false;

        var dotAnimator = rowGo.AddComponent<CardDotAnimator>();
        var dots = new Image[dotCount];
        for (int i = 0; i < dotCount; i++)
        {
            var dGo = new GameObject("Dot_" + i, typeof(RectTransform)); dGo.transform.SetParent(rowGo.transform, false);
            var dRT = dGo.GetComponent<RectTransform>(); dRT.sizeDelta = new Vector2(12, 12);
            var dImg = dGo.AddComponent<Image>(); dImg.sprite = GetCircle();
            dImg.color = Color.white; dImg.preserveAspect = true; dImg.raycastTarget = false;
            dots[i] = dImg;
        }
        dotAnimator.dots = dots;
    }

    // =========================================================================
    // Fishing rod  (4-segment Bezier, tip at phisherman's hand, bob on card)
    // =========================================================================

    static void BuildRod(RectTransform canvasRT, SwipeCard swipe, Canvas canvas,
        Sprite circle, RectTransform handAnchorRT = null)
    {
        if (swipe == null) return;

        // Rod tip — if we have the phisherman hand RT, use it directly as the tip.
        // Otherwise fall back to a fixed canvas anchor near the mast.
        RectTransform tipRT;
        if (handAnchorRT != null)
        {
            // Re-parent the hand GO to the canvas so SwipeCard.ToCanvasSpace() can read it.
            // We keep its world position by re-parenting with worldPositionStays = true.
            handAnchorRT.SetParent(canvasRT, true);
            tipRT = handAnchorRT;
        }
        else
        {
            var tipGo = new GameObject("RodTip", typeof(RectTransform));
            tipGo.transform.SetParent(canvasRT, false);
            tipRT = tipGo.GetComponent<RectTransform>();
            tipRT.anchorMin = tipRT.anchorMax = new Vector2(0.42f, 0.91f);
            tipRT.pivot = new Vector2(0.5f, 0.5f);
            tipRT.sizeDelta = new Vector2(4, 4);
            tipRT.anchoredPosition = Vector2.zero;
        }

        RectTransform MakeSeg(string n)
        {
            var go = Img(canvasRT, n, RodLine); var rt = go.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(200, 3.5f); go.raycastTarget = false;
            go.gameObject.SetActive(false); return rt;
        }

        var seg1 = MakeSeg("RodLine1");
        var seg2 = MakeSeg("RodLine2");
        var seg3 = MakeSeg("RodLine3");
        var seg4 = MakeSeg("RodLine4");

        // Bob orb
        var bobGo = new GameObject("RodBob", typeof(RectTransform)); bobGo.transform.SetParent(canvasRT, false);
        var bobRT = bobGo.GetComponent<RectTransform>();
        bobRT.anchorMin = bobRT.anchorMax = new Vector2(0.5f, 0.5f); bobRT.pivot = new Vector2(0.5f, 0.5f);
        bobRT.sizeDelta = new Vector2(16, 16);
        var bobImg = bobGo.AddComponent<Image>(); bobImg.sprite = circle; bobImg.color = BobCol;
        bobImg.preserveAspect = true; bobImg.raycastTarget = false;

        var ringGo = new GameObject("BobRing", typeof(RectTransform)); ringGo.transform.SetParent(bobGo.transform, false);
        var ringImg = ringGo.AddComponent<Image>(); ringImg.sprite = circle; ringImg.color = BobRing;
        ringImg.preserveAspect = true; ringImg.raycastTarget = false;
        var ringRT = ringGo.GetComponent<RectTransform>();
        ringRT.anchorMin = ringRT.anchorMax = new Vector2(0.5f, 0.5f); ringRT.pivot = new Vector2(0.5f, 0.5f);
        ringRT.sizeDelta = new Vector2(24, 24);
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
    // Fish anim, score popup, commentator, panels (condensed from original)
    // =========================================================================

    static void BuildFishAnim(RectTransform parent, EmailSwiperManager mgr, Sprite circle)
    {
        var go = RT("FishAnim", parent); var rrt = go.GetComponent<RectTransform>();
        rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 0.5f); rrt.pivot = new Vector2(0.5f, 0.5f);
        rrt.sizeDelta = new Vector2(68, 68);
        var img = go.AddComponent<Image>(); img.color = Color.white; img.preserveAspect = true; img.raycastTarget = false;
        go.SetActive(false); mgr.fishAnimRT = rrt; mgr.fishAnimImage = img;
    }

    static void BuildScorePopup(RectTransform parent, EmailSwiperManager mgr)
    {
        var go = RT("ScorePopup", parent); var rrt = go.GetComponent<RectTransform>();
        rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 0.5f); rrt.pivot = new Vector2(0.5f, 0.5f);
        rrt.sizeDelta = new Vector2(200, 60);
        var cg = go.AddComponent<CanvasGroup>(); cg.alpha = 0f; cg.blocksRaycasts = false;
        var txt = Txt(rrt, "Text", "+100", 36, ScoreGold, TextAlignmentOptions.Center, FontStyles.Bold); Stretch(txt.rectTransform);
        mgr.scorePopupRT = rrt; mgr.scorePopupText = txt; mgr.scorePopupCG = cg;
    }

    static Commentator BuildCommentator(RectTransform parent, Sprite circle)
    {
        var root = RT("Commentator", parent); var rrt = root.GetComponent<RectTransform>();
        rrt.anchorMin = rrt.anchorMax = Vector2.zero; rrt.pivot = Vector2.zero;
        rrt.sizeDelta = new Vector2(520, 112); rrt.anchoredPosition = new Vector2(28, 210);
        var portrait = Img(rrt, "Portrait", new Color(1f, 0.72f, 0.78f)); portrait.sprite = circle; portrait.preserveAspect = true;
        var pr = portrait.rectTransform; pr.anchorMin = new Vector2(0, 0); pr.anchorMax = new Vector2(0, 1); pr.pivot = new Vector2(0, 0.5f); pr.sizeDelta = new Vector2(96, 0);
        var letter = Txt(pr, "Letter", "G", 48, new Color(0.20f, 0.10f, 0.18f), TextAlignmentOptions.Center, FontStyles.Bold); Stretch(letter.rectTransform);
        var bubble = Img(rrt, "Bubble", new Color(1, 1, 1, 0.92f)); var bbrt = bubble.rectTransform; bbrt.anchorMin = Vector2.zero; bbrt.anchorMax = Vector2.one; bbrt.offsetMin = new Vector2(110, 0); bbrt.offsetMax = Vector2.zero;
        var speaker = Txt(bbrt, "Speaker", "Grandma", 14, new Color(0.78f, 0.30f, 0.50f), TextAlignmentOptions.MidlineLeft, FontStyles.Bold); var slrt = speaker.rectTransform; slrt.anchorMin = new Vector2(0, 1); slrt.anchorMax = new Vector2(1, 1); slrt.pivot = new Vector2(0, 1); slrt.sizeDelta = new Vector2(0, 20); slrt.anchoredPosition = new Vector2(12, -4);
        var bText = Txt(bbrt, "BubbleText", "…", 18, new Color(0.13f, 0.13f, 0.13f), TextAlignmentOptions.MidlineLeft); bText.textWrappingMode = TextWrappingModes.Normal;
        var btrt = bText.rectTransform; btrt.anchorMin = Vector2.zero; btrt.anchorMax = Vector2.one; btrt.offsetMin = new Vector2(12, 4); btrt.offsetMax = new Vector2(-12, -22);
        var comm = root.AddComponent<Commentator>(); comm.root = root; comm.portrait = portrait; comm.portraitLetter = letter; comm.bubbleBg = bubble; comm.bubbleText = bText; comm.speakerLabel = speaker; comm.portraitSprite = circle; comm.speakerName = "Grandma"; comm.portraitInitial = "G"; comm.portraitColor = new Color(1f, 0.72f, 0.78f);
        return comm;
    }

    static GameObject BuildTutorialPanel(RectTransform parent, EmailSwiperManager mgr)
    {
        var ov = Img(parent, "TutorialOverlay", new Color(0, 0, 0, 0.75f)); Stretch(ov.rectTransform); ov.raycastTarget = true;
        var card = Img(ov.rectTransform, "Card", CardWhite); var crt = card.rectTransform; crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f); crt.pivot = new Vector2(0.5f, 0.5f); crt.sizeDelta = new Vector2(820, 560);
        var hdr = Img(crt, "Header", Hex("#0F4A6B")); TopStretch(hdr.rectTransform, 90);
        var hLbl = Txt(hdr.rectTransform, "Title", "Phish Patrol — Email Swiper", 36, Aqua, TextAlignmentOptions.Center, FontStyles.Bold); Stretch(hLbl.rectTransform); hLbl.raycastTarget = false;
        var body = Txt(crt, "Body", "Sort each email — SCAM or SAFE.\n\n◀ Drag LEFT or tap <b>SCAM</b> → <color=#FF6B6B><b>Left Net</b></color>\nDrag RIGHT or tap <b>SAFE</b> → <color=#4ECDC4><b>Right Net</b></color>  ▶\n\n✓ Correct → fish lands in the net!\n✗ Wrong → pufferfish hits the boat.\n<b>5 hits</b> and the boat sinks!", 24, DarkText, TextAlignmentOptions.Center);
        body.textWrappingMode = TextWrappingModes.Normal; var brt = body.rectTransform; brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = new Vector2(46, 100); brt.offsetMax = new Vector2(-46, -96);
        var btn = MakeButton(crt, "StartBtn", "Start!", 30, SafeTeal, Color.white); var sbrt = btn.GetComponent<RectTransform>(); sbrt.anchorMin = sbrt.anchorMax = new Vector2(0.5f, 0); sbrt.pivot = new Vector2(0.5f, 0); sbrt.sizeDelta = new Vector2(240, 62); sbrt.anchoredPosition = new Vector2(0, 24);
        UnityEventTools.AddPersistentListener(btn.GetComponent<Button>().onClick, mgr.OnTutorialStart);
        return ov.gameObject;
    }

    static GameObject BuildFeedbackPanel(RectTransform parent, EmailSwiperManager mgr)
    {
        var ov = Img(parent, "FeedbackOverlay", new Color(0, 0, 0, 0.42f)); Stretch(ov.rectTransform); ov.raycastTarget = true; ov.gameObject.AddComponent<CanvasGroup>();
        var card = Img(ov.rectTransform, "Card", Hex("#2ECC71")); var crt = card.rectTransform; crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.55f); crt.pivot = new Vector2(0.5f, 0.5f); crt.sizeDelta = new Vector2(920, 360);
        var title = Txt(crt, "Title", "Correct!", 50, Color.white, TextAlignmentOptions.Center, FontStyles.Bold); var trt = title.rectTransform; trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1); trt.pivot = new Vector2(0.5f, 1); trt.sizeDelta = new Vector2(-36, 76); trt.anchoredPosition = new Vector2(0, -20);
        var body2 = Txt(crt, "Body", "Explanation…", 22, Color.white, TextAlignmentOptions.Center); body2.textWrappingMode = TextWrappingModes.Normal; var brt2 = body2.rectTransform; brt2.anchorMin = Vector2.zero; brt2.anchorMax = Vector2.one; brt2.offsetMin = new Vector2(36, 20); brt2.offsetMax = new Vector2(-36, -105);
        mgr.feedbackBg = card; mgr.feedbackTitle = title; mgr.feedbackBody = body2;
        return ov.gameObject;
    }

    static GameObject BuildResultPanel(RectTransform parent, EmailSwiperManager mgr)
    {
        var ov = Img(parent, "ResultOverlay", new Color(0, 0, 0, 0.78f)); Stretch(ov.rectTransform); ov.raycastTarget = true;
        var card = Img(ov.rectTransform, "Card", CardWhite); var crt = card.rectTransform; crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f); crt.pivot = new Vector2(0.5f, 0.5f); crt.sizeDelta = new Vector2(800, 600);
        var hdr = Img(crt, "Header", Hex("#0F4A6B")); TopStretch(hdr.rectTransform, 90);
        var hLbl = Txt(hdr.rectTransform, "Label", "Round Complete!", 38, Aqua, TextAlignmentOptions.Center, FontStyles.Bold); Stretch(hLbl.rectTransform); hLbl.raycastTarget = false;
        var stars = Txt(crt, "Stars", "★ ★ ★", 58, ScoreGold, TextAlignmentOptions.Center, FontStyles.Bold); var srt = stars.rectTransform; srt.anchorMin = new Vector2(0, 1); srt.anchorMax = new Vector2(1, 1); srt.pivot = new Vector2(0.5f, 1); srt.sizeDelta = new Vector2(0, 84); srt.anchoredPosition = new Vector2(0, -110);
        var score = Txt(crt, "Score", "0 / 800", 40, DarkText, TextAlignmentOptions.Center, FontStyles.Bold); var scrt = score.rectTransform; scrt.anchorMin = new Vector2(0, 1); scrt.anchorMax = new Vector2(1, 1); scrt.pivot = new Vector2(0.5f, 1); scrt.sizeDelta = new Vector2(0, 56); scrt.anchoredPosition = new Vector2(0, -204);
        var msg = Txt(crt, "Message", "Result", 20, MutedText, TextAlignmentOptions.Center); msg.textWrappingMode = TextWrappingModes.Normal; var mrt2 = msg.rectTransform; mrt2.anchorMin = Vector2.zero; mrt2.anchorMax = Vector2.one; mrt2.offsetMin = new Vector2(40, 115); mrt2.offsetMax = new Vector2(-40, -285);
        var play = MakeButton(crt, "PlayAgain", "Play Again", 24, SafeTeal, Color.white); var prrt = play.GetComponent<RectTransform>(); prrt.anchorMin = prrt.anchorMax = new Vector2(0.5f, 0); prrt.pivot = new Vector2(1, 0); prrt.sizeDelta = new Vector2(228, 58); prrt.anchoredPosition = new Vector2(-10, 32);
        UnityEventTools.AddPersistentListener(play.GetComponent<Button>().onClick, mgr.OnPlayAgain);
        var back = MakeButton(crt, "BackToMap", "Back to Map", 24, Hex("#636E72"), Color.white); var bart = back.GetComponent<RectTransform>(); bart.anchorMin = bart.anchorMax = new Vector2(0.5f, 0); bart.pivot = new Vector2(0, 0); bart.sizeDelta = new Vector2(228, 58); bart.anchoredPosition = new Vector2(10, 32);
        UnityEventTools.AddPersistentListener(back.GetComponent<Button>().onClick, mgr.OnReturnToMap);
        mgr.resultStars = stars; mgr.resultScore = score; mgr.resultMessage = msg;
        return ov.gameObject;
    }

    // =========================================================================
    // Striped border edge
    // =========================================================================

    static void BuildStripedEdge(RectTransform cardRT, string name,
        (Color a, Color b, Color accent, string label) scheme,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 sizeDelta, Vector2 anchoredPos, bool horizontal)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(cardRT, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
        rt.sizeDelta = sizeDelta; rt.anchoredPosition = anchoredPos;
        var bg = go.AddComponent<Image>(); bg.color = scheme.a; bg.raycastTarget = false;
        int count = 6;
        for (int i = 0; i < count; i++)
        {
            float t0 = (float)i / count, t1 = (float)(i + 1) / count;
            var stripe = Img(rt, "S" + i, i % 2 == 0 ? scheme.a : scheme.b);
            var srt = stripe.rectTransform;
            srt.anchorMin = horizontal ? new Vector2(t0, 0) : new Vector2(0, t0);
            srt.anchorMax = horizontal ? new Vector2(t1, 1) : new Vector2(1, t1);
            srt.offsetMin = srt.offsetMax = Vector2.zero; stripe.raycastTarget = false;
        }
        var acc = Img(rt, "Acc", scheme.accent); var accRT = acc.rectTransform;
        if (horizontal) { bool top = pivot.y >= 1f; accRT.anchorMin = top ? new Vector2(0, 0) : new Vector2(0, 0.86f); accRT.anchorMax = top ? new Vector2(1, 0.14f) : new Vector2(1, 1); }
        else { bool left = pivot.x <= 0f; accRT.anchorMin = left ? new Vector2(0.86f, 0) : new Vector2(0, 0); accRT.anchorMax = left ? new Vector2(1, 1) : new Vector2(0.14f, 1); }
        accRT.offsetMin = accRT.offsetMax = Vector2.zero; acc.raycastTarget = false;
    }

    // =========================================================================
    // Utilities
    // =========================================================================

    static AudioClip FindAudioClip(string name)
    {
        foreach (var g in AssetDatabase.FindAssets(name + " t:AudioClip"))
        { var p = AssetDatabase.GUIDToAssetPath(g); if (System.IO.Path.GetFileNameWithoutExtension(p).ToLower() == name.ToLower()) { var c = AssetDatabase.LoadAssetAtPath<AudioClip>(p); if (c != null) return c; } }
        foreach (var g in AssetDatabase.FindAssets(name))
        { var p = AssetDatabase.GUIDToAssetPath(g); if (System.IO.Path.GetFileNameWithoutExtension(p).ToLower().Contains(name.ToLower())) { var c = AssetDatabase.LoadAssetAtPath<AudioClip>(p); if (c != null) return c; } }
        return null;
    }

    static Sprite FindSprite(string name)
    {
        foreach (var g in AssetDatabase.FindAssets(name + " t:Sprite"))
        { var p = AssetDatabase.GUIDToAssetPath(g); if (System.IO.Path.GetFileNameWithoutExtension(p).ToLower() == name.ToLower()) { var s = AssetDatabase.LoadAssetAtPath<Sprite>(p); if (s != null) return s; } }
        return null;
    }

    static void LogFound(string n, Sprite s) => Debug.Log($"[EmailSwiperBuilder] {n}: " + (s != null ? "✓" : "✗ not found"));
    static Sprite GetCircle() { try { return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"); } catch { return null; } }
    static Image Img(Transform p, string n, Color c) { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); var img = go.AddComponent<Image>(); img.color = c; return img; }
    static TMP_Text Txt(Transform p, string n, string c, int s, Color col, TextAlignmentOptions a, FontStyles st = FontStyles.Normal) { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); var t = go.AddComponent<TextMeshProUGUI>(); t.text = c; t.fontSize = s; t.color = col; t.alignment = a; t.fontStyle = st; t.raycastTarget = false; return t; }
    static GameObject MakeButton(Transform p, string n, string lbl, int fs, Color bg, Color tc) { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); var img = go.AddComponent<Image>(); img.color = bg; var btn = go.AddComponent<Button>(); btn.targetGraphic = img; var t = Txt(go.transform, "Label", lbl, fs, tc, TextAlignmentOptions.Center, FontStyles.Bold); Stretch(t.rectTransform); return go; }
    static GameObject RT(string n, Transform p) { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); return go; }
    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
    static void TopStretch(RectTransform r, float h, float inset = 0) { r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1); r.pivot = new Vector2(0.5f, 1); r.sizeDelta = new Vector2(0, h); r.anchoredPosition = new Vector2(0, -inset); }
    static Color Hex(string h) => ColorUtility.TryParseHtmlString(h, out var c) ? c : Color.magenta;
    static void AddToBuild(string path) { var scenes = EditorBuildSettings.scenes.ToList(); if (!scenes.Any(s => s.path == path)) { scenes.Add(new EditorBuildSettingsScene(path, true)); EditorBuildSettings.scenes = scenes.ToArray(); } }
}