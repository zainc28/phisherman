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
///
/// Asset swaps vs original:
///  - Boat      → swiper_boat   (single sprite)
///  - SCAM net  → scam_net      (single sprite)
///  - SAFE net  → safe_net      (single sprite)
///  - Caught fish in net → pufferfish_4_deflated
///  - Wrong-guess puffer → pufferfish_1_default (flies to boat, damages it)
///  - Boat damage states → swiper_boat_damaged_1 … _4
///  - Phisherman on deck → phisherman_chibi (first frame of sheet)
///  - Fishing rod → fishing_rod sprite attached to phisherman's hand
///  - Rod line   → 4 segments drawn from rod tip to card bob
///  - HUD        → 3 heart sprites top-left only, no bar / timer
///  - Background → swiper_background (full-screen, no procedural ocean)
/// </summary>
public static class EmailSwiperBuilder
{
    private const string ScenesDir = "Assets/Scenes";
    private const string ScenePath = "Assets/Scenes/EmailSwiper.unity";

    // Colours used in procedural fallbacks and UI
    static readonly Color OceanMid = Hex("#0F4A6B");
    static readonly Color OceanTop = Hex("#1A6B8A");
    static readonly Color OceanDeep = new Color(0.07f, 0.09f, 0.15f, 0.55f);
    static readonly Color HorizonCol = Hex("#E8F4F8");
    static readonly Color SkyTop = Hex("#6BB8D4");
    static readonly Color SkyBot = Hex("#A8D8EA");
    static readonly Color WaveShine = new Color(0.55f, 0.85f, 1.00f, 0.18f);
    static readonly Color WaveGlint = new Color(1.00f, 1.00f, 1.00f, 0.08f);
    static readonly Color RodLine = new Color(0.90f, 0.75f, 0.40f, 0.92f);
    static readonly Color BobCol = new Color(0.95f, 0.95f, 1.00f, 0.95f);
    static readonly Color BobRing = new Color(0.60f, 0.80f, 1.00f, 0.70f);
    static readonly Color ScamRed = Hex("#FF6B6B");
    static readonly Color SafeTeal = Hex("#4ECDC4");
    static readonly Color DarkText = Hex("#1A1A2E");
    static readonly Color MutedText = Hex("#636E72");
    static readonly Color LightText = Hex("#DFE6E9");
    static readonly Color DividerCol = Hex("#E8EAED");
    static readonly Color CardWhite = Hex("#FAFBFF");
    static readonly Color CardShadow = new Color(0, 0, 0, 0.25f);
    static readonly Color ScoreGold = Hex("#FFD93D");
    static readonly Color StreakOrange = Hex("#FF9F1C");
    static readonly Color HeartFull = new Color(0.91f, 0.30f, 0.24f);
    static readonly Color Aqua = Hex("#81ECEC");
    static readonly Color HudBg = new Color(0.04f, 0.08f, 0.15f, 0.92f);
    static readonly Color HudBorder = Hex("#1E3A5A");
    static readonly Color BoatMast = Hex("#7A5230");
    static readonly Color CrackColor = Hex("#FDCB6E");

    // CHANGED: expanded from 2 to 10 fish border schemes
    static readonly (Color a, Color b, Color accent, string name)[] FishSchemes =
    {
        (Hex("#FF6B1A"), Color.white,              Hex("#1A1A1A"), "Clownfish"),           // 0
        (Hex("#1A6EFF"), Hex("#FFE033"),            Color.white,   "Blue Tang"),            // 1
        (Hex("#1A3878"), Hex("#6EB8F0"),            Hex("#E8E8FF"), "Blue Flowy"),           // 2
        (Hex("#F0DCA0"), Hex("#7A5A38"),            Hex("#4A3418"), "Spotted"),              // 3
        (Hex("#38A048"), Hex("#80D460"),            Hex("#1A5828"), "Green"),                // 4
        (Hex("#5070A0"), Hex("#C0CCD8"),            Hex("#384858"), "Tuna"),                 // 5
        (Hex("#CC3818"), Hex("#F0C088"),            Hex("#801008"), "Spiky"),                // 6
        (Hex("#B0B8C0"), Hex("#D83838"),            Hex("#606870"), "Piranha"),              // 7
        (Hex("#E85868"), Hex("#48C8D0"),            Hex("#282850"), "Rainbow"),              // 8
        (Hex("#2060D8"), Hex("#F8D028"),            Hex("#080808"), "Dory"),                 // 9
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

        // ── Sprite loading ────────────────────────────────────────────
        Sprite bgSpr = FindSprite("swiper_background");

        Sprite boatSpr = FindSprite("swiper_boat");
        Sprite scamNetSpr = FindSprite("scam_net");
        Sprite safeNetSpr = FindSprite("safe_net");

        var chibiList = LoadSpritesInOrder("phisherman_chibi");
        Sprite phishermanSpr = chibiList.Count > 0 ? chibiList[0] : FindSprite("phisherman");

        Sprite fishingRodSpr = FindSprite("fishing_rod");

        Sprite puffDeflated = FindSprite("pufferfish_4_deflated");
        Sprite puffMad = FindSprite("pufferfish_1_default");
        if (puffMad == null) puffMad = FindSprite("pufferfish_2_smile");

        Sprite[] boatDamaged = new Sprite[4];
        for (int i = 1; i <= 4; i++)
            boatDamaged[i - 1] = FindSprite("swiper_boat_damaged_" + i);

        Sprite heartSpr = FindSprite("heart");

        // CHANGED: load fish_1 through fish_20 sprites for the pool array
        Sprite[] fishPoolArr = LoadFishPoolSprites();

        // ── Log ──────────────────────────────────────────────────────
        LogFound("swiper_background", bgSpr);
        LogFound("swiper_boat", boatSpr);
        LogFound("scam_net", scamNetSpr);
        LogFound("safe_net", safeNetSpr);
        LogFound("phisherman_chibi[0]", phishermanSpr);
        LogFound("fishing_rod", fishingRodSpr);
        LogFound("pufferfish_4_deflated", puffDeflated);
        LogFound("pufferfish_1_default", puffMad);
        LogFound("heart", heartSpr);
        Debug.Log($"[EmailSwiperBuilder] fishPoolSprites: {fishPoolArr.Count(s => s != null)}/20 loaded");

        // ── Camera ───────────────────────────────────────────────────
        var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>(); camGo.AddComponent<AudioListener>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = OceanMid;
        cam.orthographic = true;

        // ── EventSystem ───────────────────────────────────────────────
        var es = new GameObject("EventSystem"); es.AddComponent<EventSystem>(); es.AddComponent<StandaloneInputModule>();

        // ── Canvas ────────────────────────────────────────────────────
        var canvasGo = new GameObject("Canvas"); var canvas = canvasGo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>(); var canvasRT = canvasGo.GetComponent<RectTransform>();

        // ── Manager ───────────────────────────────────────────────────
        var mgrGo = new GameObject("GameManager"); var mgr = mgrGo.AddComponent<EmailSwiperManager>();
        var sfxSrc = mgrGo.AddComponent<AudioSource>(); sfxSrc.playOnAwake = false;
        var musSrc = mgrGo.AddComponent<AudioSource>(); musSrc.playOnAwake = false; musSrc.loop = true;
        mgr.circleSprite = circle;

        // CHANGED: fishNormalSprite = first fish from pool (was pufferfish_4_deflated!)
        // fishPufferSprite = pufferfish_1_default (the angry one that hits the boat)
        mgr.fishNormalSprite = (fishPoolArr.Length > 0 && fishPoolArr[0] != null) ? fishPoolArr[0] : (puffDeflated ?? FindSprite("fish_normal"));
        mgr.fishPufferSprite = puffMad ?? FindSprite("fish_puffer");

        // CHANGED: assign the loaded fish pool sprites so net fish are random
        mgr.fishPoolSprites = fishPoolArr;

        // ── Background ────────────────────────────────────────────────
        BuildBackground(canvasRT, bgSpr);

        // ── HUD — 3 hearts top-left, nothing else ────────────────────
        BuildHeartHud(canvasRT, mgr, circle, heartSpr);

        // ── Score (top-right, minimal) ────────────────────────────────
        BuildScoreDisplay(canvasRT, mgr);

        // ── Boat + nets ───────────────────────────────────────────────
        RectTransform rodTipRT = null;
        BuildBoat(canvasRT, mgr, circle, phishermanSpr, fishingRodSpr, boatSpr, out rodTipRT);
        BuildNet(canvasRT, mgr, isLeft: true, circle, scamNetSpr);
        BuildNet(canvasRT, mgr, isLeft: false, circle, safeNetSpr);

        // ── Game root (card area) ─────────────────────────────────────
        var grImg = Img(canvasRT, "GameRoot", new Color(0, 0, 0, 0)); var grRT = grImg.rectTransform;
        grRT.anchorMin = new Vector2(0, 0.08f); grRT.anchorMax = new Vector2(1, 0.70f); grRT.offsetMin = grRT.offsetMax = Vector2.zero; grImg.raycastTarget = false;

        CanvasGroup scamCG, safeCG;
        BuildSwipeZones(grRT, out scamCG, out safeCG);
        BuildCard(grRT, mgr, scamCG, safeCG, canvas);

        foreach (var relay in UnityEngine.Object.FindObjectsByType<SwipeButtonRelay>(FindObjectsSortMode.None))
            relay.target = mgr.swipeCard;

        BuildRod(canvasRT, mgr.swipeCard, canvas, circle, rodTipRT);

        BuildFishAnim(canvasRT, mgr, circle);
        BuildScorePopup(canvasRT, mgr);
        mgr.commentator = BuildCommentator(canvasRT, circle);
        var feedback = BuildFeedbackPanel(canvasRT, mgr);
        var tutorial = BuildTutorialPanel(canvasRT, mgr);
        var result = BuildResultPanel(canvasRT, mgr);

        mgr.gameRoot = grImg.gameObject;
        mgr.feedbackPanel = feedback;
        mgr.tutorialPanel = tutorial;
        mgr.resultPanel = result;

        grImg.gameObject.SetActive(false);
        feedback.SetActive(false); result.SetActive(false); tutorial.SetActive(true);

        // CHANGED: move nets above game root in sibling order so both are
        // clickable (the DragOverlay inside GameRoot was blocking the safe net)
        mgr.leftNetRT.transform.SetAsLastSibling();
        mgr.rightNetRT.transform.SetAsLastSibling();
        // Keep overlay panels on top of everything
        feedback.transform.SetAsLastSibling();
        tutorial.transform.SetAsLastSibling();
        result.transform.SetAsLastSibling();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuild(ScenePath);
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log($"[EmailSwiperBuilder] Built → {ScenePath}");
    }

    // =========================================================================
    // CHANGED: Load fish_1 through fish_20 sprites by prefix matching
    // =========================================================================
    static Sprite[] LoadFishPoolSprites()
    {
        var pool = new Sprite[20];
        var allGuids = AssetDatabase.FindAssets("fish_ t:Sprite");
        foreach (var g in allGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var fn = Path.GetFileNameWithoutExtension(path).ToLower();
            for (int i = 1; i <= 20; i++)
            {
                string prefix = "fish_" + i + "_";
                if (fn.StartsWith(prefix) && pool[i - 1] == null)
                {
                    pool[i - 1] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    break;
                }
            }
        }
        return pool;
    }

    // =========================================================================
    // Background
    // =========================================================================
    static void BuildBackground(RectTransform canvasRT, Sprite bgSpr)
    {
        if (bgSpr != null)
        {
            var bg = Img(canvasRT, "Background", Color.white); bg.sprite = bgSpr; bg.preserveAspect = false; Stretch(bg.rectTransform); bg.raycastTarget = false; return;
        }
        var bgBase = Img(canvasRT, "BgBase", OceanMid); Stretch(bgBase.rectTransform); bgBase.raycastTarget = false;
        var sky = Img(canvasRT, "Sky", SkyBot); sky.rectTransform.anchorMin = new Vector2(0, 0.70f); sky.rectTransform.anchorMax = Vector2.one; sky.rectTransform.offsetMin = sky.rectTransform.offsetMax = Vector2.zero; sky.raycastTarget = false;
        var skyT = Img(canvasRT, "SkyTop", SkyTop); skyT.rectTransform.anchorMin = new Vector2(0, 0.85f); skyT.rectTransform.anchorMax = Vector2.one; skyT.rectTransform.offsetMin = skyT.rectTransform.offsetMax = Vector2.zero; skyT.raycastTarget = false;
        var hLine = Img(canvasRT, "HorizonLine", HorizonCol); var hlr = hLine.rectTransform; hlr.anchorMin = new Vector2(0, 0.718f); hlr.anchorMax = new Vector2(1, 0.724f); hlr.offsetMin = hlr.offsetMax = Vector2.zero; hLine.raycastTarget = false;
        float[] wY = { 0.60f, 0.50f, 0.42f, 0.35f, 0.25f, 0.14f }; float[] wH = { 0.018f, 0.012f, 0.010f, 0.008f, 0.007f, 0.006f };
        for (int i = 0; i < wY.Length; i++) { var w = Img(canvasRT, "Wave_" + i, i % 2 == 0 ? WaveShine : WaveGlint); var wr = w.rectTransform; wr.anchorMin = new Vector2(0, wY[i]); wr.anchorMax = new Vector2(1, wY[i] + wH[i]); wr.offsetMin = wr.offsetMax = Vector2.zero; w.raycastTarget = false; }
    }

    // =========================================================================
    // HUD
    // =========================================================================
    static void BuildHeartHud(RectTransform canvasRT, EmailSwiperManager mgr, Sprite circle, Sprite heartSpr)
    {
        var hh = new GameObject("HeartsHolder", typeof(RectTransform)); hh.transform.SetParent(canvasRT, false);
        var hhrt = hh.GetComponent<RectTransform>();
        hhrt.anchorMin = new Vector2(0, 1); hhrt.anchorMax = new Vector2(0, 1); hhrt.pivot = new Vector2(0, 1); hhrt.sizeDelta = new Vector2(160, 60); hhrt.anchoredPosition = new Vector2(24, -24);
        var hlg = hh.AddComponent<HorizontalLayoutGroup>(); hlg.childAlignment = TextAnchor.MiddleLeft; hlg.spacing = 8; hlg.childForceExpandWidth = hlg.childForceExpandHeight = false; hlg.childControlWidth = hlg.childControlHeight = false; hlg.padding = new RectOffset(0, 0, 8, 8);
        for (int i = 0; i < 3; i++)
        {
            var hgo = new GameObject("Heart_" + i, typeof(RectTransform)); hgo.transform.SetParent(hh.transform, false);
            var hi = hgo.AddComponent<Image>();
            if (heartSpr != null) { hi.sprite = heartSpr; hi.color = Color.white; } else { hi.sprite = circle; hi.color = HeartFull; }
            hi.preserveAspect = true; hi.raycastTarget = false;
            var le = hgo.AddComponent<LayoutElement>(); le.preferredWidth = le.preferredHeight = 44;
        }
        mgr.heartsContainer = hh.transform;
        mgr.hudPanel = hh;
    }

    // =========================================================================
    // Score display
    // =========================================================================
    static void BuildScoreDisplay(RectTransform canvasRT, EmailSwiperManager mgr)
    {
        var sH = new GameObject("ScoreHolder", typeof(RectTransform)); sH.transform.SetParent(canvasRT, false);
        var srt = sH.GetComponent<RectTransform>(); srt.anchorMin = new Vector2(1, 1); srt.anchorMax = new Vector2(1, 1); srt.pivot = new Vector2(1, 1); srt.sizeDelta = new Vector2(280, 52); srt.anchoredPosition = new Vector2(-24, -24);
        var scoreTxt = Txt(srt, "ScoreText", "Score: 0", 32, ScoreGold, TextAlignmentOptions.MidlineRight, FontStyles.Bold); var str2 = scoreTxt.rectTransform; str2.anchorMin = Vector2.zero; str2.anchorMax = new Vector2(1, 1); str2.offsetMin = str2.offsetMax = Vector2.zero;
        var streakTxt = Txt(canvasRT, "StreakText", "", 22, StreakOrange, TextAlignmentOptions.Center, FontStyles.Bold); var skrt = streakTxt.rectTransform; skrt.anchorMin = new Vector2(0.35f, 0.92f); skrt.anchorMax = new Vector2(0.65f, 1f); skrt.offsetMin = skrt.offsetMax = Vector2.zero;
        var progTxt = Txt(canvasRT, "ProgressText", "1 / 8", 18, LightText, TextAlignmentOptions.Center); var prt = progTxt.rectTransform; prt.anchorMin = new Vector2(0.35f, 0.87f); prt.anchorMax = new Vector2(0.65f, 0.93f); prt.offsetMin = prt.offsetMax = Vector2.zero;
        mgr.scoreText = scoreTxt; mgr.streakText = streakTxt; mgr.progressText = progTxt;
    }

    // =========================================================================
    // Boat
    // =========================================================================
    static void BuildBoat(RectTransform canvasRT, EmailSwiperManager mgr, Sprite circle, Sprite phishermanSpr, Sprite fishingRodSpr, Sprite boatSpr, out RectTransform rodTipRT)
    {
        rodTipRT = null;
        var boatRoot = new GameObject("BoatRoot", typeof(RectTransform)); boatRoot.transform.SetParent(canvasRT, false);
        var brRT = boatRoot.GetComponent<RectTransform>(); brRT.anchorMin = new Vector2(0.275f, 0.68f); brRT.anchorMax = new Vector2(0.725f, 0.99f); brRT.offsetMin = brRT.offsetMax = Vector2.zero; mgr.boatRoot = brRT;
        if (boatSpr != null) { var boatImg = Img(brRT, "Boat", Color.white); boatImg.sprite = boatSpr; boatImg.preserveAspect = true; boatImg.raycastTarget = false; Stretch(boatImg.rectTransform); mgr.boatHullRT = boatImg.rectTransform; }
        else { var hull = Img(brRT, "Hull", Hex("#C8341A")); var hrt = hull.rectTransform; hrt.anchorMin = new Vector2(0.05f, 0); hrt.anchorMax = new Vector2(0.95f, 0.35f); hrt.offsetMin = hrt.offsetMax = Vector2.zero; hull.raycastTarget = false; mgr.boatHullRT = hrt; var deck = Img(brRT, "Deck", Hex("#E8D5A0")); var drt = deck.rectTransform; drt.anchorMin = new Vector2(0.03f, 0.32f); drt.anchorMax = new Vector2(0.97f, 0.44f); drt.offsetMin = drt.offsetMax = Vector2.zero; deck.raycastTarget = false; var cabin = Img(brRT, "Cabin", Hex("#FAFAFA")); var cabRT = cabin.rectTransform; cabRT.anchorMin = new Vector2(0.30f, 0.42f); cabRT.anchorMax = new Vector2(0.70f, 0.82f); cabRT.offsetMin = cabRT.offsetMax = Vector2.zero; cabin.raycastTarget = false; var mast = Img(brRT, "Mast", Hex("#7A5230")); var mrt = mast.rectTransform; mrt.anchorMin = new Vector2(0.47f, 0.42f); mrt.anchorMax = new Vector2(0.53f, 1.00f); mrt.offsetMin = mrt.offsetMax = Vector2.zero; mast.raycastTarget = false; }
        var crackSlots = new RectTransform[5]; float[] crX = { 0.15f, 0.30f, 0.50f, 0.70f, 0.85f }; float[] crY = { 0.15f, 0.22f, 0.12f, 0.20f, 0.14f };
        for (int i = 0; i < 5; i++) { var cr = Img(brRT, "Crack_" + i, CrackColor); cr.sprite = circle; var ccRT = cr.rectTransform; ccRT.anchorMin = ccRT.anchorMax = new Vector2(crX[i], crY[i]); ccRT.pivot = new Vector2(0.5f, 0.5f); ccRT.sizeDelta = new Vector2(32, 32); cr.raycastTarget = false; cr.gameObject.SetActive(false); crackSlots[i] = ccRT; }
        mgr.boatCrackSlots = crackSlots;
        // CHANGED: pufferfish decoration on the boat deck (replaces the yellow dot)
        Sprite deckPuff = FindSprite("pufferfish_1_default") ?? FindSprite("pufferfish_2_smile");
        if (deckPuff != null) { var puffGo = Img(brRT, "DeckPuffer", Color.white); puffGo.sprite = deckPuff; puffGo.preserveAspect = true; puffGo.raycastTarget = false; var ppRT = puffGo.rectTransform; ppRT.anchorMin = ppRT.anchorMax = new Vector2(0.72f, 0.42f); ppRT.pivot = new Vector2(0.5f, 0.5f); ppRT.sizeDelta = new Vector2(52, 52); }

        var phGo = new GameObject("Phisherman", typeof(RectTransform)); phGo.transform.SetParent(brRT, false); var phRT = phGo.GetComponent<RectTransform>(); phRT.anchorMin = new Vector2(0.30f, 0.35f); phRT.anchorMax = new Vector2(0.52f, 0.90f); phRT.offsetMin = phRT.offsetMax = Vector2.zero;
        var phImg = phGo.AddComponent<Image>(); phImg.color = Color.white; phImg.preserveAspect = true; phImg.raycastTarget = false; if (phishermanSpr != null) phImg.sprite = phishermanSpr;
        // CHANGED: rod tip moved to top of fishing rod sprite so line starts from rod tip
        if (fishingRodSpr != null) { var rodGo = new GameObject("FishingRod", typeof(RectTransform)); rodGo.transform.SetParent(phRT, false); var rodRT = rodGo.GetComponent<RectTransform>(); rodRT.anchorMin = new Vector2(-0.2f, 0.4f); rodRT.anchorMax = new Vector2(0.6f, 1.1f); rodRT.offsetMin = rodRT.offsetMax = Vector2.zero; var rodImg = rodGo.AddComponent<Image>(); rodImg.sprite = fishingRodSpr; rodImg.color = Color.white; rodImg.preserveAspect = true; rodImg.raycastTarget = false; var tipGo = new GameObject("RodTip", typeof(RectTransform)); tipGo.transform.SetParent(brRT, false); var trt2 = tipGo.GetComponent<RectTransform>(); trt2.anchorMin = trt2.anchorMax = new Vector2(0.20f, 0.98f); trt2.pivot = new Vector2(0.5f, 0.5f); trt2.sizeDelta = new Vector2(4, 4); rodTipRT = trt2; }
        else { var tipGo = new GameObject("RodTip", typeof(RectTransform)); tipGo.transform.SetParent(brRT, false); var trt2 = tipGo.GetComponent<RectTransform>(); trt2.anchorMin = trt2.anchorMax = new Vector2(0.42f, 0.91f); trt2.pivot = new Vector2(0.5f, 0.5f); trt2.sizeDelta = new Vector2(4, 4); rodTipRT = trt2; }
    }

    // =========================================================================
    // Nets
    // =========================================================================
    static void BuildNet(RectTransform canvasRT, EmailSwiperManager mgr, bool isLeft, Sprite circle, Sprite netSpr)
    {
        string side = isLeft ? "Left" : "Right"; Color col = isLeft ? ScamRed : SafeTeal; string label = isLeft ? "SCAM" : "SAFE"; int swipeDir = isLeft ? -1 : 1;
        var netRoot = new GameObject(side + "NetRoot", typeof(RectTransform)); netRoot.transform.SetParent(canvasRT, false); var nrRT = netRoot.GetComponent<RectTransform>();
        float xMin = isLeft ? 0.01f : 0.74f; float xMax = isLeft ? 0.26f : 0.99f;
        nrRT.anchorMin = new Vector2(xMin, 0.48f); nrRT.anchorMax = new Vector2(xMax, 0.76f); nrRT.offsetMin = nrRT.offsetMax = Vector2.zero;
        var bgImg = netRoot.AddComponent<Image>(); bgImg.color = new Color(col.r, col.g, col.b, 0.08f); bgImg.raycastTarget = true;
        var netBtn = netRoot.AddComponent<Button>(); netBtn.targetGraphic = bgImg; var cb = netBtn.colors; cb.normalColor = new Color(col.r, col.g, col.b, 0.08f); cb.highlightedColor = new Color(col.r, col.g, col.b, 0.25f); cb.pressedColor = new Color(col.r, col.g, col.b, 0.42f); cb.selectedColor = cb.normalColor; netBtn.colors = cb;
        var relay = netRoot.AddComponent<SwipeButtonRelay>(); relay.direction = swipeDir; UnityEventTools.AddPersistentListener(netBtn.onClick, relay.Fire);
        Graphic glowMesh; RectTransform bagRT;
        if (netSpr != null) { var netImg = Img(nrRT, "NetSprite", Color.white); netImg.sprite = netSpr; netImg.preserveAspect = true; netImg.raycastTarget = false; var niRT = netImg.rectTransform; niRT.anchorMin = new Vector2(0.04f, 0.06f); niRT.anchorMax = new Vector2(0.96f, 0.98f); niRT.offsetMin = niRT.offsetMax = Vector2.zero; glowMesh = netImg; bagRT = niRT; }
        else { var netBag = Img(nrRT, "NetBag", new Color(col.r, col.g, col.b, 0.20f)); var nbRT = netBag.rectTransform; nbRT.anchorMin = new Vector2(0.08f, 0.14f); nbRT.anchorMax = new Vector2(0.92f, 0.92f); nbRT.offsetMin = nbRT.offsetMax = Vector2.zero; netBag.raycastTarget = false; var netLbl = Txt(nrRT, "NetLabel", label, 32, col, TextAlignmentOptions.Center, FontStyles.Bold); var nlRT = netLbl.rectTransform; nlRT.anchorMin = new Vector2(0.02f, 0); nlRT.anchorMax = new Vector2(0.98f, 0.16f); nlRT.offsetMin = nlRT.offsetMax = Vector2.zero; netLbl.raycastTarget = false; glowMesh = netBag; bagRT = nbRT; }
        var fishCont = RT(side + "FishCont", bagRT); Stretch(fishCont.GetComponent<RectTransform>()); var fishContRT = fishCont.GetComponent<RectTransform>();
        var netHover = netRoot.AddComponent<NetHoverEffect>(); netHover.lift = 14f; netHover.swayAngle = 2.5f; netHover.swaySpeed = 2.4f; netHover.lerpSpeed = 12f; netHover.glowBoost = 0.16f; netHover.glowGraphics = new Graphic[] { glowMesh };
        if (isLeft) { mgr.leftNetRT = nrRT; mgr.leftNetFishContainer = fishContRT; } else { mgr.rightNetRT = nrRT; mgr.rightNetFishContainer = fishContRT; }
    }

    // =========================================================================
    // Swipe zones
    // =========================================================================
    static void BuildSwipeZones(RectTransform parent, out CanvasGroup scamCG, out CanvasGroup safeCG)
    { scamCG = MakeZoneCG(parent, "ScamZone", 0.08f); safeCG = MakeZoneCG(parent, "SafeZone", 0.92f); }
    static CanvasGroup MakeZoneCG(RectTransform parent, string name, float xAnchor)
    { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); var rt = go.GetComponent<RectTransform>(); rt.anchorMin = new Vector2(xAnchor - 0.08f, 0.15f); rt.anchorMax = new Vector2(xAnchor + 0.08f, 0.90f); var bg = go.AddComponent<Image>(); bg.color = new Color(0, 0, 0, 0); bg.raycastTarget = false; var cg = go.AddComponent<CanvasGroup>(); cg.alpha = 0f; cg.blocksRaycasts = false; cg.interactable = false; return cg; }

    // =========================================================================
    // Card
    // =========================================================================
    static void BuildCard(RectTransform parent, EmailSwiperManager mgr, CanvasGroup scamHint, CanvasGroup safeHint, Canvas rootCanvas)
    {
        var shadow = Img(parent, "CardShadow", CardShadow); var shrt = shadow.rectTransform; shrt.anchorMin = shrt.anchorMax = new Vector2(0.5f, 0.52f); shrt.pivot = new Vector2(0.5f, 0.5f); shrt.sizeDelta = new Vector2(628, 668); shrt.anchoredPosition = new Vector2(8, -8); shadow.raycastTarget = false;
        var card = Img(parent, "EmailCard", new Color(0, 0, 0, 0)); var crt = card.rectTransform; crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.52f); crt.pivot = new Vector2(0.5f, 0.5f); crt.sizeDelta = new Vector2(620, 660); card.raycastTarget = false;
        var cardCG = card.gameObject.AddComponent<CanvasGroup>();
        var cardBg = Img(crt, "CardBg", CardWhite); Stretch(cardBg.rectTransform); cardBg.raycastTarget = false;

        // Build initial border stripes with a random scheme (manager swaps at runtime)
        var scheme = FishSchemes[Random.Range(0, FishSchemes.Length)];
        BuildStripedEdge(crt, "BorderTop", scheme, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, 12), Vector2.zero, true);
        BuildStripedEdge(crt, "BorderBottom", scheme, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 12), Vector2.zero, true);
        BuildStripedEdge(crt, "BorderLeft", scheme, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(12, 0), Vector2.zero, false);
        BuildStripedEdge(crt, "BorderRight", scheme, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(12, 0), Vector2.zero, false);

        BuildDotRow(crt, 10);
        var headerStrip = Img(crt, "HeaderStrip", Hex("#1A73E8")); var hsrt = headerStrip.rectTransform; hsrt.anchorMin = new Vector2(0, 1); hsrt.anchorMax = new Vector2(1, 1); hsrt.pivot = new Vector2(0.5f, 1); hsrt.sizeDelta = new Vector2(0, 5); headerStrip.raycastTarget = false;
        var avatar = Img(crt, "Avatar", Hex("#1A73E8")); avatar.sprite = GetCircle(); avatar.preserveAspect = true; var avrt = avatar.rectTransform; avrt.anchorMin = avrt.anchorMax = new Vector2(0, 1); avrt.pivot = new Vector2(0, 1); avrt.sizeDelta = new Vector2(54, 54); avrt.anchoredPosition = new Vector2(24, -44); avatar.raycastTarget = false;
        var avatarLetter = Txt(avrt, "Letter", "P", 30, Color.white, TextAlignmentOptions.Center, FontStyles.Bold); Stretch(avatarLetter.rectTransform);
        var senderName = Txt(crt, "SenderName", "Sender", 22, DarkText, TextAlignmentOptions.MidlineLeft, FontStyles.Bold); var snrt = senderName.rectTransform; snrt.anchorMin = new Vector2(0, 1); snrt.anchorMax = new Vector2(1, 1); snrt.pivot = new Vector2(0, 1); snrt.sizeDelta = new Vector2(-130, 30); snrt.anchoredPosition = new Vector2(94, -46);
        var senderEmail = Txt(crt, "SenderEmail", "sender@example.com", 16, MutedText, TextAlignmentOptions.MidlineLeft); var sert = senderEmail.rectTransform; sert.anchorMin = new Vector2(0, 1); sert.anchorMax = new Vector2(1, 1); sert.pivot = new Vector2(0, 1); sert.sizeDelta = new Vector2(-130, 22); sert.anchoredPosition = new Vector2(94, -78);
        var div = Img(crt, "Divider", DividerCol); var dvrt = div.rectTransform; dvrt.anchorMin = new Vector2(0, 1); dvrt.anchorMax = new Vector2(1, 1); dvrt.pivot = new Vector2(0.5f, 1); dvrt.sizeDelta = new Vector2(-36, 1); dvrt.anchoredPosition = new Vector2(0, -110); div.raycastTarget = false;
        var subject = Txt(crt, "Subject", "Subject", 24, DarkText, TextAlignmentOptions.TopLeft, FontStyles.Bold); var sjrt = subject.rectTransform; sjrt.anchorMin = new Vector2(0, 1); sjrt.anchorMax = new Vector2(1, 1); sjrt.pivot = new Vector2(0, 1); sjrt.sizeDelta = new Vector2(-44, 60); sjrt.anchoredPosition = new Vector2(22, -124);
        var body = Txt(crt, "Body", "Body text...", 19, DarkText, TextAlignmentOptions.TopLeft); body.textWrappingMode = TextWrappingModes.Normal; body.overflowMode = TextOverflowModes.Ellipsis; var brt = body.rectTransform; brt.anchorMin = new Vector2(0, 0); brt.anchorMax = new Vector2(1, 1); brt.offsetMin = new Vector2(22, 26); brt.offsetMax = new Vector2(-22, -228);
        var overlay = Img(parent, "DragOverlay", new Color(0, 0, 0, 0)); var olrt = overlay.rectTransform; olrt.anchorMin = olrt.anchorMax = new Vector2(0.5f, 0.52f); olrt.pivot = new Vector2(0.5f, 0.5f); olrt.sizeDelta = new Vector2(640, 680); overlay.raycastTarget = true;
        var swipe = overlay.gameObject.AddComponent<SwipeCard>(); swipe.cardRoot = crt; swipe.scamIndicator = scamHint; swipe.safeIndicator = safeHint; swipe.cardBorder = card; swipe.rootCanvas = rootCanvas; swipe.cardCanvasGroup = cardCG;
        foreach (var relay in UnityEngine.Object.FindObjectsByType<SwipeButtonRelay>(FindObjectsSortMode.None)) relay.target = swipe;
        mgr.swipeCard = swipe; mgr.cardSender = senderName; mgr.cardEmail = senderEmail; mgr.cardSubject = subject; mgr.cardBody = body; mgr.cardAvatar = avatar; mgr.cardAvatarLetter = avatarLetter; mgr.cardCanvasGroup = cardCG;

        // CHANGED: wire cardBorderRT so manager can recolour border stripes at runtime
        mgr.cardBorderRT = crt;
    }

    static void BuildDotRow(RectTransform cardRT, int dotCount)
    {
        var rowGo = new GameObject("DotRow", typeof(RectTransform)); rowGo.transform.SetParent(cardRT, false);
        var rrt = rowGo.GetComponent<RectTransform>(); rrt.anchorMin = new Vector2(0, 0); rrt.anchorMax = new Vector2(1, 0); rrt.pivot = new Vector2(0.5f, 0); rrt.sizeDelta = new Vector2(0, 22); rrt.anchoredPosition = new Vector2(0, 6);
        var hlg = rowGo.AddComponent<HorizontalLayoutGroup>(); hlg.childAlignment = TextAnchor.MiddleCenter; hlg.spacing = 5; hlg.childForceExpandWidth = hlg.childForceExpandHeight = false; hlg.childControlWidth = hlg.childControlHeight = false;
        var dotAnimator = rowGo.AddComponent<CardDotAnimator>(); var dots = new Image[dotCount];
        for (int i = 0; i < dotCount; i++) { var dGo = new GameObject("Dot_" + i, typeof(RectTransform)); dGo.transform.SetParent(rowGo.transform, false); var dRT = dGo.GetComponent<RectTransform>(); dRT.sizeDelta = new Vector2(12, 12); var dImg = dGo.AddComponent<Image>(); dImg.sprite = GetCircle(); dImg.color = Color.white; dImg.preserveAspect = true; dImg.raycastTarget = false; dots[i] = dImg; }
        dotAnimator.dots = dots;
    }

    // =========================================================================
    // Rod
    // =========================================================================
    static void BuildRod(RectTransform canvasRT, SwipeCard swipe, Canvas canvas, Sprite circle, RectTransform handAnchorRT)
    {
        if (swipe == null) return;
        RectTransform tipRT;
        if (handAnchorRT != null) { handAnchorRT.SetParent(canvasRT, true); tipRT = handAnchorRT; }
        else { var tipGo = new GameObject("RodTip", typeof(RectTransform)); tipGo.transform.SetParent(canvasRT, false); tipRT = tipGo.GetComponent<RectTransform>(); tipRT.anchorMin = tipRT.anchorMax = new Vector2(0.42f, 0.91f); tipRT.pivot = new Vector2(0.5f, 0.5f); tipRT.sizeDelta = new Vector2(4, 4); }
        RectTransform MakeSeg(string n) { var go = Img(canvasRT, n, RodLine); var rt = go.rectTransform; rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = new Vector2(200, 3.5f); go.raycastTarget = false; go.gameObject.SetActive(false); return rt; }
        var seg1 = MakeSeg("RodLine1"); var seg2 = MakeSeg("RodLine2"); var seg3 = MakeSeg("RodLine3"); var seg4 = MakeSeg("RodLine4");
        var bobGo = new GameObject("RodBob", typeof(RectTransform)); bobGo.transform.SetParent(canvasRT, false); var bobRT = bobGo.GetComponent<RectTransform>(); bobRT.anchorMin = bobRT.anchorMax = new Vector2(0.5f, 0.5f); bobRT.pivot = new Vector2(0.5f, 0.5f); bobRT.sizeDelta = new Vector2(16, 16);
        var bobImg = bobGo.AddComponent<Image>(); bobImg.sprite = circle; bobImg.color = BobCol; bobImg.preserveAspect = true; bobImg.raycastTarget = false;
        var ringGo = new GameObject("BobRing", typeof(RectTransform)); ringGo.transform.SetParent(bobGo.transform, false); var ringImg = ringGo.AddComponent<Image>(); ringImg.sprite = circle; ringImg.color = BobRing; ringImg.preserveAspect = true; ringImg.raycastTarget = false; var ringRT = ringGo.GetComponent<RectTransform>(); ringRT.anchorMin = ringRT.anchorMax = new Vector2(0.5f, 0.5f); ringRT.pivot = new Vector2(0.5f, 0.5f); ringRT.sizeDelta = new Vector2(24, 24);
        bobGo.SetActive(false);
        swipe.rodTipRT = tipRT; swipe.rodLineRT = seg1; swipe.rodLine2RT = seg2; swipe.rodLine3RT = seg3; swipe.rodLine4RT = seg4; swipe.rodBobRT = bobRT; swipe.rootCanvas = canvas;
    }

    // =========================================================================
    // Fish anim, score popup, commentator, panels
    // =========================================================================
    static void BuildFishAnim(RectTransform parent, EmailSwiperManager mgr, Sprite circle)
    { var go = RT("FishAnim", parent); var rrt = go.GetComponent<RectTransform>(); rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 0.5f); rrt.pivot = new Vector2(0.5f, 0.5f); rrt.sizeDelta = new Vector2(68, 68); var img = go.AddComponent<Image>(); img.color = Color.white; img.preserveAspect = true; img.raycastTarget = false; go.SetActive(false); mgr.fishAnimRT = rrt; mgr.fishAnimImage = img; }

    static void BuildScorePopup(RectTransform parent, EmailSwiperManager mgr)
    { var go = RT("ScorePopup", parent); var rrt = go.GetComponent<RectTransform>(); rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 0.5f); rrt.pivot = new Vector2(0.5f, 0.5f); rrt.sizeDelta = new Vector2(200, 60); var cg = go.AddComponent<CanvasGroup>(); cg.alpha = 0f; cg.blocksRaycasts = false; var txt = Txt(rrt, "Text", "+100", 36, ScoreGold, TextAlignmentOptions.Center, FontStyles.Bold); Stretch(txt.rectTransform); mgr.scorePopupRT = rrt; mgr.scorePopupText = txt; mgr.scorePopupCG = cg; }

    static Commentator BuildCommentator(RectTransform parent, Sprite circle)
    {
        var root = RT("Commentator", parent); var rrt = root.GetComponent<RectTransform>(); rrt.anchorMin = rrt.anchorMax = Vector2.zero; rrt.pivot = Vector2.zero; rrt.sizeDelta = new Vector2(520, 112); rrt.anchoredPosition = new Vector2(28, 210);
        var portrait = Img(rrt, "Portrait", new Color(1f, 0.72f, 0.78f)); portrait.sprite = circle; portrait.preserveAspect = true; var pr = portrait.rectTransform; pr.anchorMin = new Vector2(0, 0); pr.anchorMax = new Vector2(0, 1); pr.pivot = new Vector2(0, 0.5f); pr.sizeDelta = new Vector2(96, 0);
        var letter = Txt(pr, "Letter", "G", 48, new Color(0.20f, 0.10f, 0.18f), TextAlignmentOptions.Center, FontStyles.Bold); Stretch(letter.rectTransform);
        var bubble = Img(rrt, "Bubble", new Color(1, 1, 1, 0.92f)); var bbrt = bubble.rectTransform; bbrt.anchorMin = Vector2.zero; bbrt.anchorMax = Vector2.one; bbrt.offsetMin = new Vector2(110, 0); bbrt.offsetMax = Vector2.zero;
        var speaker = Txt(bbrt, "Speaker", "Grandma", 14, new Color(0.78f, 0.30f, 0.50f), TextAlignmentOptions.MidlineLeft, FontStyles.Bold); var slrt = speaker.rectTransform; slrt.anchorMin = new Vector2(0, 1); slrt.anchorMax = new Vector2(1, 1); slrt.pivot = new Vector2(0, 1); slrt.sizeDelta = new Vector2(0, 20); slrt.anchoredPosition = new Vector2(12, -4);
        var bText = Txt(bbrt, "BubbleText", "...", 18, new Color(0.13f, 0.13f, 0.13f), TextAlignmentOptions.MidlineLeft); bText.textWrappingMode = TextWrappingModes.Normal; var btrt = bText.rectTransform; btrt.anchorMin = Vector2.zero; btrt.anchorMax = Vector2.one; btrt.offsetMin = new Vector2(12, 4); btrt.offsetMax = new Vector2(-12, -22);
        var comm = root.AddComponent<Commentator>(); comm.root = root; comm.portrait = portrait; comm.portraitLetter = letter; comm.bubbleBg = bubble; comm.bubbleText = bText; comm.speakerLabel = speaker; comm.portraitSprite = circle; comm.speakerName = "Grandma"; comm.portraitInitial = "G"; comm.portraitColor = new Color(1f, 0.72f, 0.78f);
        return comm;
    }

    static GameObject BuildTutorialPanel(RectTransform parent, EmailSwiperManager mgr)
    {
        var ov = Img(parent, "TutorialOverlay", new Color(0, 0, 0, 0.75f)); Stretch(ov.rectTransform); ov.raycastTarget = true;
        var card = Img(ov.rectTransform, "Card", CardWhite); var crt = card.rectTransform; crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f); crt.pivot = new Vector2(0.5f, 0.5f); crt.sizeDelta = new Vector2(820, 560);
        var hdr = Img(crt, "Header", Hex("#0F4A6B")); TopStretch(hdr.rectTransform, 90);
        var hLbl = Txt(hdr.rectTransform, "Title", "Phish Patrol - Email Swiper", 36, Aqua, TextAlignmentOptions.Center, FontStyles.Bold); Stretch(hLbl.rectTransform); hLbl.raycastTarget = false;
        var body = Txt(crt, "Body", "Sort each email - SCAM or SAFE.\n\nDrag LEFT  -  SCAM Net\nDrag RIGHT -  SAFE Net\n\nCorrect catch lands in the net!\nWrong guess hits the boat - 3 hits and it sinks!", 24, DarkText, TextAlignmentOptions.Center); body.textWrappingMode = TextWrappingModes.Normal; var brt = body.rectTransform; brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = new Vector2(46, 100); brt.offsetMax = new Vector2(-46, -96);
        var btn = MakeButton(crt, "StartBtn", "Start!", 30, SafeTeal, Color.white); var sbrt = btn.GetComponent<RectTransform>(); sbrt.anchorMin = sbrt.anchorMax = new Vector2(0.5f, 0); sbrt.pivot = new Vector2(0.5f, 0); sbrt.sizeDelta = new Vector2(240, 62); sbrt.anchoredPosition = new Vector2(0, 24);
        UnityEventTools.AddPersistentListener(btn.GetComponent<Button>().onClick, mgr.OnTutorialStart);
        return ov.gameObject;
    }

    static GameObject BuildFeedbackPanel(RectTransform parent, EmailSwiperManager mgr)
    {
        var ov = Img(parent, "FeedbackOverlay", new Color(0, 0, 0, 0.42f)); Stretch(ov.rectTransform); ov.raycastTarget = true; ov.gameObject.AddComponent<CanvasGroup>();
        var card = Img(ov.rectTransform, "Card", Hex("#2ECC71")); var crt = card.rectTransform; crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.55f); crt.pivot = new Vector2(0.5f, 0.5f); crt.sizeDelta = new Vector2(920, 360);
        var title = Txt(crt, "Title", "Correct!", 50, Color.white, TextAlignmentOptions.Center, FontStyles.Bold); var trt = title.rectTransform; trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1); trt.pivot = new Vector2(0.5f, 1); trt.sizeDelta = new Vector2(-36, 76); trt.anchoredPosition = new Vector2(0, -20);
        var body2 = Txt(crt, "Body", "Explanation...", 22, Color.white, TextAlignmentOptions.Center); body2.textWrappingMode = TextWrappingModes.Normal; var brt2 = body2.rectTransform; brt2.anchorMin = Vector2.zero; brt2.anchorMax = Vector2.one; brt2.offsetMin = new Vector2(36, 20); brt2.offsetMax = new Vector2(-36, -105);
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
    // Striped card border
    // =========================================================================
    static void BuildStripedEdge(RectTransform cardRT, string name, (Color a, Color b, Color accent, string label) scheme,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta, Vector2 anchoredPos, bool horizontal)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(cardRT, false);
        var rt = go.GetComponent<RectTransform>(); rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot; rt.sizeDelta = sizeDelta; rt.anchoredPosition = anchoredPos;
        var bg = go.AddComponent<Image>(); bg.color = scheme.a; bg.raycastTarget = false;
        int count = 6;
        for (int i = 0; i < count; i++) { float t0 = (float)i / count, t1 = (float)(i + 1) / count; var stripe = Img(rt, "S" + i, i % 2 == 0 ? scheme.a : scheme.b); var srt = stripe.rectTransform; srt.anchorMin = horizontal ? new Vector2(t0, 0) : new Vector2(0, t0); srt.anchorMax = horizontal ? new Vector2(t1, 1) : new Vector2(1, t1); srt.offsetMin = srt.offsetMax = Vector2.zero; stripe.raycastTarget = false; }
        var acc = Img(rt, "Acc", scheme.accent); var accRT = acc.rectTransform;
        if (horizontal) { bool top = pivot.y >= 1f; accRT.anchorMin = top ? new Vector2(0, 0) : new Vector2(0, 0.86f); accRT.anchorMax = top ? new Vector2(1, 0.14f) : new Vector2(1, 1); }
        else { bool left = pivot.x <= 0f; accRT.anchorMin = left ? new Vector2(0.86f, 0) : new Vector2(0, 0); accRT.anchorMax = left ? new Vector2(1, 1) : new Vector2(0.14f, 1); }
        accRT.offsetMin = accRT.offsetMax = Vector2.zero; acc.raycastTarget = false;
    }

    // =========================================================================
    // Sprite loading helpers
    // =========================================================================
    static List<Sprite> LoadSpritesInOrder(string baseName)
    { var list = new List<Sprite>(); var path = FindTexturePath(baseName); if (path == null) return list; var reps = AssetDatabase.LoadAllAssetRepresentationsAtPath(path).OfType<Sprite>().ToList(); if (reps.Count == 0) { var s = AssetDatabase.LoadAssetAtPath<Sprite>(path); if (s != null) list.Add(s); return list; } float avgH = reps.Average(s => s.rect.height); if (avgH < 1f) avgH = 1f; list = reps.OrderByDescending(s => Mathf.RoundToInt(s.rect.y / (avgH * 0.8f))).ThenBy(s => s.rect.x).ToList(); return list; }
    static string FindTexturePath(string baseName) { foreach (var g in AssetDatabase.FindAssets(baseName + " t:Texture2D")) { var p = AssetDatabase.GUIDToAssetPath(g); if (Path.GetFileNameWithoutExtension(p).ToLower() == baseName.ToLower()) return p; } return null; }
    static Sprite FindSprite(string name) { foreach (var g in AssetDatabase.FindAssets(name + " t:Sprite")) { var p = AssetDatabase.GUIDToAssetPath(g); if (Path.GetFileNameWithoutExtension(p).ToLower() == name.ToLower()) { var s = AssetDatabase.LoadAssetAtPath<Sprite>(p); if (s != null) return s; } } return null; }
    static void LogFound(string n, Sprite s) => Debug.Log($"[EmailSwiperBuilder] {n}: " + (s != null ? "✓" : "✗ not found"));

    // =========================================================================
    // Generic UI helpers
    // =========================================================================
    static Sprite GetCircle() { try { return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"); } catch { return null; } }
    static Image Img(Transform p, string n, Color c) { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); var img = go.AddComponent<Image>(); img.color = c; return img; }
    static TMP_Text Txt(Transform p, string n, string text, int size, Color col, TextAlignmentOptions align, FontStyles style = FontStyles.Normal) { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); var t = go.AddComponent<TextMeshProUGUI>(); t.text = text; t.fontSize = size; t.color = col; t.alignment = align; t.fontStyle = style; t.raycastTarget = false; return t; }
    static GameObject MakeButton(Transform p, string n, string lbl, int fs, Color bg, Color tc) { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); var img = go.AddComponent<Image>(); img.color = bg; var btn = go.AddComponent<Button>(); btn.targetGraphic = img; var t = Txt(go.transform, "Label", lbl, fs, tc, TextAlignmentOptions.Center, FontStyles.Bold); Stretch(t.rectTransform); return go; }
    static GameObject RT(string n, Transform p) { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); return go; }
    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
    static void TopStretch(RectTransform r, float h, float inset = 0) { r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1); r.pivot = new Vector2(0.5f, 1); r.sizeDelta = new Vector2(0, h); r.anchoredPosition = new Vector2(0, -inset); }
    static Color Hex(string h) => ColorUtility.TryParseHtmlString(h, out var c) ? c : Color.magenta;
    static void AddToBuild(string path) { var scenes = EditorBuildSettings.scenes.ToList(); if (!scenes.Any(s => s.path == path)) { scenes.Add(new EditorBuildSettingsScene(path, true)); EditorBuildSettings.scenes = scenes.ToArray(); } }
}