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
/// Builds the Tower Defense scene.
///
/// Run via:  Phisherman ▸ Build Tower Defense Scene
/// Output:   Assets/Scenes/TowerDefense.unity
///
/// Auto-detects these sprites anywhere under Assets/:
///   tower.png          → tower body
///   phisherman.png     → character on tower top
///   speargun.png       → weapon that rotates toward mouse
///   td_hanging_fish.png → fish enemy sprite
///   log.png            → log the fish holds (password text goes here)
/// </summary>
public static class TowerDefenseBuilder
{
    private const string ScenesDir = "Assets/Scenes";
    private const string ScenePath = "Assets/Scenes/TowerDefense.unity";

    // World-space layout
    private const float TowerX = 5.2f;
    private const float TowerY = -0.3f;
    private const float SpawnX = -9.0f;

    // ── Palette ──
    private static readonly Color BgDeep = Hex("#1F1530");
    private static readonly Color BgMid = Hex("#2E2050");
    private static readonly Color PathColor = Hex("#1A1230");
    private static readonly Color WaterColor = new Color(0.18f, 0.55f, 0.82f, 0.55f);
    private static readonly Color WaterDark = new Color(0.10f, 0.35f, 0.60f, 0.75f);
    private static readonly Color HudBg = new Color(0.08f, 0.07f, 0.15f, 0.88f);
    private static readonly Color ScoreGold = Hex("#FFD93D");
    private static readonly Color StreakOrange = Hex("#FF9F1C");
    private static readonly Color WaveWhite = Color.white;
    private static readonly Color SafeGreen = Hex("#2ECC71");
    private static readonly Color RetryRed = Hex("#E74C3C");
    private static readonly Color MapGrey = Hex("#7F8C8D");
    private static readonly Color DarkText = Hex("#202124");
    private static readonly Color MutedText = Hex("#5F6368");
    private static readonly Color PanelWhite = Color.white;
    private static readonly Color HeaderBlue = Hex("#1A3A6B");

    // =================================================================
    // Build
    // =================================================================

    [MenuItem("Phisherman/Build Tower Defense Scene")]
    public static void Build()
    {
        if (!Directory.Exists(ScenesDir)) Directory.CreateDirectory(ScenesDir);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── White pixel (fallback for everything) ──
        Sprite white = EnsureWhitePixel();

        // ── Auto-detect sprites ──
        Sprite spTower = FindSprite("tower");
        Sprite spPhisherman = FindSprite("phisherman");
        Sprite spSpeargun = FindSprite("speargun");
        Sprite spFish = FindSprite("td_hanging_fish");
        Sprite spLog = FindSprite("log");

        LogFound("tower", spTower);
        LogFound("phisherman", spPhisherman);
        LogFound("speargun", spSpeargun);
        LogFound("td_hanging_fish", spFish);
        LogFound("log", spLog);

        // ── Tag setup ──
        EnsureTag("Spear");

        // ── Camera ──
        var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>(); camGo.AddComponent<AudioListener>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = BgDeep;
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        camGo.transform.position = new Vector3(0, 0, -10);

        // ── EventSystem ──
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>(); es.AddComponent<StandaloneInputModule>();

        // ── Background ──
        SprRect("BgFar", white, BgDeep, Vector3.zero, new Vector3(24, 12, 1), null, -20);
        SprRect("BgMid", white, BgMid, new Vector3(0, -1, 0.5f), new Vector3(24, 8, 1), null, -15);
        SprRect("Path", white, PathColor, new Vector3(0, 0, 0.2f), new Vector3(24, 3.2f, 1), null, -5);

        // ── Spawn point ──
        var spawnGo = new GameObject("SpawnPoint");
        spawnGo.transform.position = new Vector3(SpawnX, TowerY, 0);

        // ── Tower ──
        var towerRootGo = new GameObject("TowerRoot");
        towerRootGo.transform.position = new Vector3(TowerX, TowerY, 0);
        var shakeRoot = new GameObject("ShakeRoot");
        shakeRoot.transform.SetParent(towerRootGo.transform, false);

        Transform crackContainer, towerFishCanvasRT, speargunPivot, spearSpawnPoint;
        BuildTower(spTower, spPhisherman, spSpeargun, white,
                   shakeRoot.transform,
                   out crackContainer, out towerFishCanvasRT,
                   out speargunPivot, out spearSpawnPoint);

        // ── Canvas ──
        var canvasGo = new GameObject("Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        var canvasRT = canvasGo.GetComponent<RectTransform>();

        // ── Manager ──
        var mgrGo = new GameObject("GameManager");
        var mgr = mgrGo.AddComponent<TowerDefenseManager>();
        mgr.fishSprite = spFish;
        mgr.logSprite = spLog;
        mgr.towerSprite = spTower;
        mgr.phishermanSprite = spPhisherman;
        mgr.speargunSprite = spSpeargun;
        mgr.whiteSprite = white;
        mgr.spawnPoint = spawnGo.transform;
        mgr.towerRoot = towerRootGo.transform;
        mgr.towerShakeRoot = shakeRoot.transform;
        mgr.crackContainer = crackContainer;
        mgr.towerWaterContainer = towerFishCanvasRT;
        mgr.speargunPivot = speargunPivot;
        mgr.spearSpawnPoint = spearSpawnPoint;

        // ── UI panels ──
        var hud = BuildHud(canvasRT, mgr);
        var tutorial = BuildTutorialPanel(canvasRT, mgr);
        var gameOver = BuildGameOverPanel(canvasRT, mgr);
        var win = BuildWinPanel(canvasRT, mgr);
        mgr.hudPanel = hud;
        mgr.tutorialPanel = tutorial;
        mgr.gameOverPanel = gameOver;
        mgr.winPanel = win;
        mgr.commentator = BuildCommentator(canvasRT);

        hud.SetActive(false); gameOver.SetActive(false);
        win.SetActive(false); tutorial.SetActive(true);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuild(ScenePath);
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log($"[TowerDefenseBuilder] Built → {ScenePath}");
    }

    // =================================================================
    // Tower + Phisherman + Speargun
    // =================================================================

    static void BuildTower(
        Sprite spTower, Sprite spPhisherman, Sprite spSpeargun, Sprite white,
        Transform shakeRoot,
        out Transform crackContainer,
        out Transform towerFishRT,
        out Transform speargunPivot,
        out Transform spearSpawnPoint)
    {
        // ── Tower body ──
        if (spTower != null)
        {
            SprRect("TowerBody", spTower, Color.white,
                new Vector3(0, 0.3f, 0), new Vector3(2.8f, 5.5f, 1),
                shakeRoot, sortingOrder: 2);
        }
        else
        {
            // Fallback: rectangle tower
            SprRect("TowerBody", white, Hex("#2D2040"),
                new Vector3(0, 0.3f, 0), new Vector3(2.8f, 5.5f, 1),
                shakeRoot, sortingOrder: 2);
            SprRect("TowerScreen", white, Hex("#0A2D52"),
                new Vector3(0, 0.8f, 0), new Vector3(2.2f, 2.5f, 1),
                shakeRoot, sortingOrder: 3);
        }

        // ── Water section (bottom of tower) ──
        var waterBg = SprRect("TowerWaterBg", white, WaterDark,
            new Vector3(0, -2.0f, 0), new Vector3(2.6f, 1.2f, 1),
            shakeRoot, sortingOrder: 3);

        SprRect("TowerWaterFill", white, WaterColor,
            new Vector3(0, -2.0f, -0.1f), new Vector3(2.6f, 1.2f, 1),
            shakeRoot, sortingOrder: 4);

        // WorldSpace Canvas inside water for swimming fish UI
        var waterCanvasGo = new GameObject("WaterCanvas");
        waterCanvasGo.transform.SetParent(shakeRoot, false);
        waterCanvasGo.transform.localPosition = new Vector3(0, -2.0f, -0.2f);
        const float k = 0.01f;
        waterCanvasGo.transform.localScale = new Vector3(k, k, 1f);
        var wc = waterCanvasGo.AddComponent<Canvas>();
        wc.renderMode = RenderMode.WorldSpace;
        wc.sortingOrder = 6;
        var wcRT = waterCanvasGo.GetComponent<RectTransform>();
        wcRT.sizeDelta = new Vector2(260, 120);

        // Fish container inside canvas
        var fishContGo = new GameObject("FishContainer", typeof(RectTransform));
        fishContGo.transform.SetParent(waterCanvasGo.transform, false);
        var fcRT = fishContGo.GetComponent<RectTransform>();
        fcRT.anchorMin = Vector2.zero; fcRT.anchorMax = Vector2.one;
        fcRT.offsetMin = fcRT.offsetMax = Vector2.zero;
        towerFishRT = fcRT;

        // ── Crack container (over tower body) ──
        var ccGo = new GameObject("CrackContainer");
        ccGo.transform.SetParent(shakeRoot, false);
        ccGo.transform.localPosition = new Vector3(0, 0.3f, -0.3f);
        crackContainer = ccGo.transform;

        // ── Phisherman on top of tower ──
        float towerTop = spTower != null ? 3.2f : 2.8f;
        var phGo = new GameObject("Phisherman");
        phGo.transform.SetParent(shakeRoot, false);
        phGo.transform.localPosition = new Vector3(0f, towerTop, -0.5f);

        if (spPhisherman != null)
        {
            SprRect("PhishermanSprite", spPhisherman, Color.white,
                new Vector3(0, 0, 0), new Vector3(1.4f, 1.8f, 1),
                phGo.transform, sortingOrder: 8);
        }
        else
        {
            // Placeholder body
            SprRect("Body", white, Hex("#F5C89A"), Vector3.zero, new Vector3(0.5f, 0.7f, 1), phGo.transform, 8);
            SprRect("Jacket", white, Hex("#2A4FA8"), new Vector3(0, -0.22f, 0), new Vector3(0.5f, 0.36f, 1), phGo.transform, 9);
            SprRect("HatBrim", white, Hex("#8B5E1E"), new Vector3(0, 0.40f, 0), new Vector3(0.70f, 0.10f, 1), phGo.transform, 10);
            SprRect("HatCrown", white, Hex("#8B5E1E"), new Vector3(0, 0.56f, 0), new Vector3(0.42f, 0.30f, 1), phGo.transform, 10);
        }

        // ── Speargun pivot (rotates to face mouse each frame) ──
        // Pivot is at phisherman's hand position (left side, mid-height)
        var pivotGo = new GameObject("SpeargunPivot");
        pivotGo.transform.SetParent(phGo.transform, false);
        pivotGo.transform.localPosition = new Vector3(-0.25f, 0.05f, -0.1f);
        speargunPivot = pivotGo.transform;

        // Speargun sprite — extends in +X from pivot
        // (at 180° rotation it points LEFT toward fish)
        if (spSpeargun != null)
        {
            SprRect("SpeargunSprite", spSpeargun, Color.white,
                new Vector3(0.30f, 0, 0), new Vector3(1.1f, 0.45f, 1),
                pivotGo.transform, sortingOrder: 9);
        }
        else
        {
            // Fallback speargun rect
            SprRect("SpeargunBarrel", white, Hex("#4A3020"),
                new Vector3(0.32f, 0, 0), new Vector3(0.9f, 0.14f, 1),
                pivotGo.transform, sortingOrder: 9);
            SprRect("SpeargunHandle", white, Hex("#6B4528"),
                new Vector3(0.05f, -0.10f, 0), new Vector3(0.18f, 0.30f, 1),
                pivotGo.transform, sortingOrder: 9);
            SprRect("SpeargunTip", white, Hex("#C0A060"),
                new Vector3(0.80f, 0, 0), new Vector3(0.12f, 0.12f, 1),
                pivotGo.transform, sortingOrder: 10);
        }

        // Spear spawn point — tip of the speargun (+X direction from pivot)
        var sspGo = new GameObject("SpearSpawnPoint");
        sspGo.transform.SetParent(pivotGo.transform, false);
        sspGo.transform.localPosition = new Vector3(0.85f, 0, 0);
        spearSpawnPoint = sspGo.transform;
    }

    // =================================================================
    // HUD
    // =================================================================

    static GameObject BuildHud(RectTransform parent, TowerDefenseManager mgr)
    {
        var hud = UImg(parent, "HUD", HudBg);
        TopStretch(hud.rectTransform, 82); hud.raycastTarget = true;

        // Hearts (left)
        var hh = RT("HeartsHolder", hud.rectTransform);
        var hhrt = hh.GetComponent<RectTransform>();
        hhrt.anchorMin = new Vector2(0, 0); hhrt.anchorMax = new Vector2(0, 1);
        hhrt.pivot = new Vector2(0, 0.5f);
        hhrt.sizeDelta = new Vector2(280, 0); hhrt.anchoredPosition = new Vector2(30, 0);
        var hlg = hh.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.spacing = 6;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;
        hlg.childControlWidth = hlg.childControlHeight = false;

        // Score (centre)
        var scoreTxt = UTxt(hud.rectTransform, "ScoreText", "Score: 0",
            40, ScoreGold, TextAlignmentOptions.Center, FontStyles.Bold);
        var srt = scoreTxt.rectTransform;
        srt.anchorMin = new Vector2(0.5f, 0); srt.anchorMax = new Vector2(0.5f, 1);
        srt.pivot = new Vector2(0.5f, 0.5f);
        srt.sizeDelta = new Vector2(360, 0);

        // Wave (right)
        var waveTxt = UTxt(hud.rectTransform, "WaveText", "Wave 1 / 4",
            30, WaveWhite, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
        var wrt = waveTxt.rectTransform;
        wrt.anchorMin = new Vector2(1, 0); wrt.anchorMax = new Vector2(1, 1);
        wrt.pivot = new Vector2(1, 0.5f);
        wrt.sizeDelta = new Vector2(260, 0); wrt.anchoredPosition = new Vector2(-30, 0);

        // Combo (world overlay below hud)
        var comboTxt = UTxt(parent, "ComboText", "",
            44, StreakOrange, TextAlignmentOptions.Center, FontStyles.Bold);
        var crt = comboTxt.rectTransform;
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(500, 70); crt.anchoredPosition = new Vector2(0, -280);
        comboTxt.raycastTarget = false;

        mgr.heartsContainer = hh.transform;
        mgr.scoreText = scoreTxt; mgr.waveText = waveTxt; mgr.comboText = comboTxt;
        return hud.gameObject;
    }

    // =================================================================
    // Tutorial
    // =================================================================

    static GameObject BuildTutorialPanel(RectTransform parent, TowerDefenseManager mgr)
    {
        var ov = UImg(parent, "TutorialOverlay", new Color(0, 0, 0, 0.72f));
        Stretch(ov.rectTransform); ov.raycastTarget = true;

        var card = UImg(ov.rectTransform, "Card", PanelWhite);
        var crt = card.rectTransform;
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f); crt.sizeDelta = new Vector2(960, 620);

        var hdr = UImg(crt, "Header", HeaderBlue);
        TopStretch(hdr.rectTransform, 90);
        var hLbl = UTxt(hdr.rectTransform, "Title", "Phish Patrol", 40,
            Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(hLbl.rectTransform); hLbl.raycastTarget = false;

        var body = UTxt(crt, "Body", "…", 25, DarkText, TextAlignmentOptions.Center);
        body.textWrappingMode = TextWrappingModes.Normal;
        var brt = body.rectTransform;
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
        brt.offsetMin = new Vector2(48, 155); brt.offsetMax = new Vector2(-48, -110);

        var stepLbl = UTxt(crt, "StepLabel", "1 of 2", 18, MutedText,
            TextAlignmentOptions.Center);
        var slrt = stepLbl.rectTransform;
        slrt.anchorMin = new Vector2(0, 0); slrt.anchorMax = new Vector2(1, 0);
        slrt.pivot = new Vector2(0.5f, 0);
        slrt.sizeDelta = new Vector2(0, 28); slrt.anchoredPosition = new Vector2(0, 118);

        var btn = UBtn(crt, "NextBtn", "Next", 30, SafeGreen, Color.white);
        var nbrt = btn.GetComponent<RectTransform>();
        nbrt.anchorMin = nbrt.anchorMax = new Vector2(0.5f, 0);
        nbrt.pivot = new Vector2(0.5f, 0);
        nbrt.sizeDelta = new Vector2(260, 72); nbrt.anchoredPosition = new Vector2(0, 28);
        UnityEventTools.AddPersistentListener(btn.GetComponent<Button>().onClick, mgr.OnTutorialNext);

        mgr.tutorialTitleText = hLbl;
        mgr.tutorialBodyText = body;
        mgr.tutorialStepText = stepLbl;
        mgr.tutorialNextButton = btn.GetComponent<Button>();
        mgr.tutorialNextButtonText = btn.transform.Find("Label").GetComponent<TMP_Text>();

        return ov.gameObject;
    }

    // =================================================================
    // Game Over
    // =================================================================

    static GameObject BuildGameOverPanel(RectTransform parent, TowerDefenseManager mgr)
    {
        var ov = UImg(parent, "GameOverOverlay", new Color(0, 0, 0, 0.78f));
        Stretch(ov.rectTransform); ov.raycastTarget = true;

        var card = UImg(ov.rectTransform, "Card", PanelWhite);
        var crt = card.rectTransform;
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f); crt.sizeDelta = new Vector2(860, 560);

        var hdr = UImg(crt, "Header", RetryRed);
        TopStretch(hdr.rectTransform, 95);
        var hLbl = UTxt(hdr.rectTransform, "HLabel", "Tower Cracked!", 44,
            Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(hLbl.rectTransform); hLbl.raycastTarget = false;

        var sTxt = UTxt(crt, "ScoreText", "Score: 0", 42, DarkText,
            TextAlignmentOptions.Center, FontStyles.Bold);
        var srt = sTxt.rectTransform;
        srt.anchorMin = new Vector2(0, 1); srt.anchorMax = new Vector2(1, 1);
        srt.pivot = new Vector2(0.5f, 1);
        srt.sizeDelta = new Vector2(0, 72); srt.anchoredPosition = new Vector2(0, -120);

        var msg = UTxt(crt, "Message", "", 21, MutedText, TextAlignmentOptions.Center);
        msg.textWrappingMode = TextWrappingModes.Normal;
        var mrt = msg.rectTransform;
        mrt.anchorMin = Vector2.zero; mrt.anchorMax = Vector2.one;
        mrt.offsetMin = new Vector2(55, 145); mrt.offsetMax = new Vector2(-55, -230);

        var retry = UBtn(crt, "RetryBtn", "Retry", 26, SafeGreen, Color.white);
        SetBtnPos(retry, new Vector2(-14, 40), new Vector2(240, 60), new Vector2(1, 0));
        UnityEventTools.AddPersistentListener(retry.GetComponent<Button>().onClick, mgr.OnRetry);

        var back = UBtn(crt, "BackBtn", "Back to Map", 26, MapGrey, Color.white);
        SetBtnPos(back, new Vector2(14, 40), new Vector2(240, 60), new Vector2(0, 0));
        UnityEventTools.AddPersistentListener(back.GetComponent<Button>().onClick, mgr.OnReturnToMap);

        mgr.gameOverScoreText = sTxt;
        mgr.gameOverMessageText = msg;
        return ov.gameObject;
    }

    // =================================================================
    // Win
    // =================================================================

    static GameObject BuildWinPanel(RectTransform parent, TowerDefenseManager mgr)
    {
        var ov = UImg(parent, "WinOverlay", new Color(0, 0, 0, 0.72f));
        Stretch(ov.rectTransform); ov.raycastTarget = true;

        var card = UImg(ov.rectTransform, "Card", PanelWhite);
        var crt = card.rectTransform;
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f); crt.sizeDelta = new Vector2(860, 560);

        var hdr = UImg(crt, "Header", SafeGreen);
        TopStretch(hdr.rectTransform, 95);
        var hLbl = UTxt(hdr.rectTransform, "HLabel", "Tower Defended!", 44,
            Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(hLbl.rectTransform); hLbl.raycastTarget = false;

        var stars = UTxt(crt, "Stars", "★ ★ ★", 60, ScoreGold,
            TextAlignmentOptions.Center, FontStyles.Bold);
        var stt = stars.rectTransform;
        stt.anchorMin = new Vector2(0, 1); stt.anchorMax = new Vector2(1, 1);
        stt.pivot = new Vector2(0.5f, 1);
        stt.sizeDelta = new Vector2(0, 88); stt.anchoredPosition = new Vector2(0, -120);

        var sTxt = UTxt(crt, "ScoreText", "0 / 100", 40, DarkText,
            TextAlignmentOptions.Center, FontStyles.Bold);
        var scrt = sTxt.rectTransform;
        scrt.anchorMin = new Vector2(0, 1); scrt.anchorMax = new Vector2(1, 1);
        scrt.pivot = new Vector2(0.5f, 1);
        scrt.sizeDelta = new Vector2(0, 66); scrt.anchoredPosition = new Vector2(0, -218);

        var retry = UBtn(crt, "PlayAgainBtn", "Play Again", 26, SafeGreen, Color.white);
        SetBtnPos(retry, new Vector2(-14, 40), new Vector2(240, 60), new Vector2(1, 0));
        UnityEventTools.AddPersistentListener(retry.GetComponent<Button>().onClick, mgr.OnRetry);

        var back = UBtn(crt, "BackBtn", "Back to Map", 26, MapGrey, Color.white);
        SetBtnPos(back, new Vector2(14, 40), new Vector2(240, 60), new Vector2(0, 0));
        UnityEventTools.AddPersistentListener(back.GetComponent<Button>().onClick, mgr.OnReturnToMap);

        mgr.winScoreText = sTxt;
        mgr.winStarsText = stars;
        return ov.gameObject;
    }

    // =================================================================
    // Commentator
    // =================================================================

    static Commentator BuildCommentator(RectTransform parent)
    {
        var root = RT("Commentator", parent);
        var rrt = root.GetComponent<RectTransform>();
        rrt.anchorMin = rrt.anchorMax = Vector2.zero;
        rrt.pivot = Vector2.zero;
        rrt.sizeDelta = new Vector2(560, 128);
        rrt.anchoredPosition = new Vector2(30, 30);

        var portrait = UImg(rrt, "Portrait", new Color(1f, 0.72f, 0.78f));
        portrait.sprite = Circle(); portrait.preserveAspect = true;
        var pr = portrait.rectTransform;
        pr.anchorMin = new Vector2(0, 0); pr.anchorMax = new Vector2(0, 1);
        pr.pivot = new Vector2(0, 0.5f); pr.sizeDelta = new Vector2(108, 0);

        var letter = UTxt(pr, "Letter", "G", 54,
            new Color(0.2f, 0.1f, 0.18f), TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(letter.rectTransform);

        var bubble = UImg(rrt, "Bubble", new Color(1, 1, 1, 0.92f));
        var bbrt = bubble.rectTransform;
        bbrt.anchorMin = Vector2.zero; bbrt.anchorMax = Vector2.one;
        bbrt.offsetMin = new Vector2(120, 0); bbrt.offsetMax = Vector2.zero;

        var speaker = UTxt(bbrt, "Speaker", "Grandma", 16,
            new Color(0.78f, 0.3f, 0.5f), TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        var slrt = speaker.rectTransform;
        slrt.anchorMin = new Vector2(0, 1); slrt.anchorMax = new Vector2(1, 1);
        slrt.pivot = new Vector2(0, 1);
        slrt.sizeDelta = new Vector2(0, 22); slrt.anchoredPosition = new Vector2(14, -5);

        var bText = UTxt(bbrt, "BubbleText", "…", 20,
            new Color(0.13f, 0.13f, 0.13f), TextAlignmentOptions.MidlineLeft);
        bText.textWrappingMode = TextWrappingModes.Normal;
        var btrt = bText.rectTransform;
        btrt.anchorMin = Vector2.zero; btrt.anchorMax = Vector2.one;
        btrt.offsetMin = new Vector2(14, 5); btrt.offsetMax = new Vector2(-14, -26);

        var comm = root.AddComponent<Commentator>();
        comm.root = root; comm.portrait = portrait; comm.portraitLetter = letter;
        comm.bubbleBg = bubble; comm.bubbleText = bText; comm.speakerLabel = speaker;
        comm.portraitSprite = Circle();
        comm.speakerName = "Grandma"; comm.portraitInitial = "G";
        comm.portraitColor = new Color(1f, 0.72f, 0.78f);
        return comm;
    }

    // =================================================================
    // Sprite auto-detection
    // =================================================================

    static Sprite FindSprite(string name)
    {
        // Try exact name match first
        string[] guids = AssetDatabase.FindAssets(name + " t:Sprite");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path)
                    .ToLower() == name.ToLower())
            {
                var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (s != null) return s;
            }
        }
        // Fallback: any sprite whose path contains the name
        string[] texGuids = AssetDatabase.FindAssets(name + " t:Texture2D");
        foreach (string guid in texGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path)
                    .ToLower().Contains(name.ToLower()))
            {
                var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (s != null) return s;
            }
        }
        return null;
    }

    static void LogFound(string name, Sprite s)
        => Debug.Log($"[TDBuilder] {name}: " + (s != null ? "✓ found" : "✗ not found (using fallback)"));

    // =================================================================
    // Tag helper
    // =================================================================

    static void EnsureTag(string tag)
    {
        var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (asset.Length == 0) return;
        var so = new SerializedObject(asset[0]);
        var tags = so.FindProperty("tags");
        for (int i = 0; i < tags.arraySize; i++)
            if (tags.GetArrayElementAtIndex(i).stringValue == tag) return;
        tags.arraySize++;
        tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
        so.ApplyModifiedProperties();
    }

    // =================================================================
    // White pixel helper
    // =================================================================

    static Sprite EnsureWhitePixel()
    {
        const string dir = "Assets/Sprites";
        const string path = "Assets/Sprites/td_white_pixel.png";
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        if (!File.Exists(path))
        {
            var tex = new Texture2D(8, 8);
            var px = new Color[64]; for (int i = 0; i < 64; i++) px[i] = Color.white;
            tex.SetPixels(px); tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null)
        {
            bool changed = false;
            if (imp.textureType != TextureImporterType.Sprite) { imp.textureType = TextureImporterType.Sprite; changed = true; }
            if (Mathf.Abs(imp.spritePixelsPerUnit - 8f) > 0.01f) { imp.spritePixelsPerUnit = 8; changed = true; }
            if (imp.filterMode != FilterMode.Point) { imp.filterMode = FilterMode.Point; changed = true; }
            if (changed) imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // =================================================================
    // Low-level helpers
    // =================================================================

    static SpriteRenderer SprRect(string n, Sprite spr, Color col,
        Vector3 localPos, Vector3 scale, Transform parent = null, int sortingOrder = 0)
    {
        var go = new GameObject(n);
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = spr; sr.color = col; sr.sortingOrder = sortingOrder;
        return sr;
    }

    static Image UImg(Transform p, string n, Color c)
    {
        var go = new GameObject(n, typeof(RectTransform));
        go.transform.SetParent(p, false);
        var img = go.AddComponent<Image>(); img.color = c; return img;
    }

    static TMP_Text UTxt(Transform p, string n, string c, int s, Color col,
        TextAlignmentOptions a, FontStyles st = FontStyles.Normal)
    {
        var go = new GameObject(n, typeof(RectTransform));
        go.transform.SetParent(p, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = c; t.fontSize = s; t.color = col;
        t.alignment = a; t.fontStyle = st; t.raycastTarget = false; return t;
    }

    static GameObject UBtn(Transform p, string n, string l, int fs, Color bg, Color tc)
    {
        var go = new GameObject(n, typeof(RectTransform));
        go.transform.SetParent(p, false);
        var img = go.AddComponent<Image>(); img.color = bg;
        var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        var t = UTxt(go.transform, "Label", l, fs, tc,
            TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(t.rectTransform); return go;
    }

    static GameObject RT(string n, Transform p)
    {
        var go = new GameObject(n, typeof(RectTransform));
        go.transform.SetParent(p, false); return go;
    }

    static void Stretch(RectTransform r)
    { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }

    static void TopStretch(RectTransform r, float h)
    {
        r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1);
        r.pivot = new Vector2(0.5f, 1); r.sizeDelta = new Vector2(0, h);
        r.anchoredPosition = Vector2.zero;
    }

    static void SetBtnPos(GameObject btn, Vector2 ap, Vector2 sd, Vector2 pivot)
    {
        var rt = btn.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0);
        rt.pivot = pivot; rt.sizeDelta = sd; rt.anchoredPosition = ap;
    }

    static Color Hex(string h) => ColorUtility.TryParseHtmlString(h, out var c) ? c : Color.magenta;

    static Sprite Circle()
    {
        try { return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"); }
        catch { return null; }
    }

    static void AddToBuild(string path)
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(s => s.path == path))
        { scenes.Add(new EditorBuildSettingsScene(path, true)); EditorBuildSettings.scenes = scenes.ToArray(); }
    }
}