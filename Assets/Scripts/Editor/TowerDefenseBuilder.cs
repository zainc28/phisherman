using System.IO;
using System.Collections.Generic;
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
/// CHANGES:
///  - td_background used as full-screen background
///  - Phisherman avatar + separate speargun removed; tower sprite is the shooter
///  - Tower sprite flipped to face LEFT (toward fish), scaled 20% smaller
///  - Spear spawn point comes from tower sprite centre-left
///  - Blue water pool replaced with safe_net sprite at bottom of tower
///  - Fish non-overlap: fixed Y lanes so labels never stack
///  - Hanging fish flipped to face left (toward tower)
///  - heart + hourglass sprites wired to HUD
///  - 60-second survival timer; waves accelerate over time
/// </summary>
public static class TowerDefenseBuilder
{
    private const string ScenesDir = "Assets/Scenes";
    private const string ScenePath = "Assets/Scenes/TowerDefense.unity";

    // World-space layout
    private const float TowerX = 6.2f;  // moved right so fish have more travel room
    private const float TowerY = -0.3f;
    private const float SpawnX = -9.0f;

    // ── Palette ──
    private static readonly Color BgDeep = Hex("#1F1530");
    private static readonly Color BgMid = Hex("#2E2050");
    private static readonly Color PathColor = Hex("#1A1230");
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

        Sprite white = EnsureWhitePixel();

        // ── Sprites ──
        Sprite spBg = FindSprite("td_background");
        Sprite spTower = FindSprite("td_tower");           // the tower sprite (has built-in net)
        Sprite spLog = FindSprite("log_debris") ?? FindSprite("log");
        Sprite spNetBottom = null;   // td_tower sprite already has a net built in — no separate net needed
        Sprite spHangFish = FindSprite("td_hanging_fish");
        Sprite spHeart = FindSprite("heart");
        Sprite spHourglass = FindSprite("hourglass");

        // Fish sprites — use pool fish for normal/happy, puffer for puffed
        Sprite[] fishPool = LoadFishPoolSprites();
        Sprite spNormal = (fishPool.Length > 0 && fishPool[0] != null) ? fishPool[0] : FindSprite("fish_1_clownfish_normal");
        Sprite spHappy = FindSprite("pufferfish_4_deflated") ?? spNormal;
        Sprite spPuffed = FindSprite("pufferfish_1_default") ?? spNormal;

        AudioClip clipBgm = FindAudio("adventure_music_v1") ?? FindAudio("frutiger_music_fresh_waters");
        AudioClip clipSpear = FindAudio("spear") ?? FindAudio("water_splash");

        LogFound("td_background", spBg);
        LogFound("td_tower", spTower);
        LogFound("td_hanging_fish", spHangFish);
        LogFound("heart", spHeart);
        LogFound("hourglass", spHourglass);
        LogFound("fish pool [0]", spNormal);
        Debug.Log($"[TDBuilder] fishPool: {fishPool.Count(s => s != null)}/20 loaded");

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
        if (spBg != null)
        {
            SprRect("Background", spBg, Color.white, Vector3.zero,
                new Vector3(18f, 10f, 1f), null, -20);
        }
        else
        {
            SprRect("BgFar", white, BgDeep, Vector3.zero, new Vector3(24, 12, 1), null, -20);
            SprRect("BgMid", white, BgMid, new Vector3(0, -1, 0.5f), new Vector3(24, 8, 1), null, -15);
            SprRect("Path", white, PathColor, new Vector3(0, 0, 0.2f), new Vector3(24, 3.2f, 1), null, -5);
        }

        // ── Spawn point ──
        var spawnGo = new GameObject("SpawnPoint");
        spawnGo.transform.position = new Vector3(SpawnX, TowerY, 0);

        // ── Tower ──
        var towerRootGo = new GameObject("TowerRoot");
        towerRootGo.transform.position = new Vector3(TowerX, TowerY, 0);
        var shakeRoot = new GameObject("ShakeRoot");
        shakeRoot.transform.SetParent(towerRootGo.transform, false);

        Transform crackContainer, speargunPivot, spearSpawnPoint;
        BuildTower(spTower, white,
                   shakeRoot.transform,
                   out crackContainer,
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

        mgr.fishNormalSprite = spNormal;
        mgr.fishHappySprite = spHappy;
        mgr.fishPuffedSprite = spPuffed;
        mgr.logSprite = spLog;
        mgr.towerSprite = spTower;
        mgr.phishermanSprite = null;    // no separate phisherman — tower IS the shooter
        mgr.speargunSprite = null;
        mgr.whiteSprite = white;
        mgr.hangingFishSprite = spHangFish;
        mgr.heartSprite = spHeart;
        mgr.hourglassSprite = spHourglass;
        mgr.fishPoolSprites = fishPool;

        mgr.bgmClip = clipBgm;
        mgr.spearClip = clipSpear;

        mgr.spawnPoint = spawnGo.transform;
        mgr.towerRoot = towerRootGo.transform;
        mgr.towerShakeRoot = shakeRoot.transform;
        mgr.crackContainer = crackContainer;
        mgr.towerWaterContainer = null;  // td_tower has a built-in net — no separate fish container needed
        mgr.speargunPivot = speargunPivot;
        mgr.spearSpawnPoint = spearSpawnPoint;
        mgr.surviveDuration = 60f;                // 1-minute survival

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
    // Tower — no phisherman/speargun, just the tower sprite + net base
    // Spear fires from the tower's left-centre (toward the fish)
    // =================================================================

    static void BuildTower(
        Sprite spTower, Sprite white,
        Transform shakeRoot,
        out Transform crackContainer,
        out Transform speargunPivot,
        out Transform spearSpawnPoint)
    {
        // td_tower sprite — uniform scale preserves aspect ratio.
        // Flip X (negative) so the tower faces LEFT toward incoming fish.
        // Height 3.6 world units. The speargun on td_tower is at roughly
        // bottom-left of the sprite, which after X-flip becomes bottom-right
        // visually (toward the fish). Offset pivot there.
        float towerH = 3.6f;
        float towerCentreY = 0.5f;

        if (spTower != null)
        {
            var tGo = new GameObject("TowerBody");
            tGo.transform.SetParent(shakeRoot, false);
            tGo.transform.localPosition = new Vector3(0, towerCentreY, 0);
            var tSR = tGo.AddComponent<SpriteRenderer>();
            tSR.sprite = spTower;
            tSR.color = Color.white;
            tSR.sortingOrder = 2;
            // Uniform scale keeps aspect ratio; negative X flips left
            tGo.transform.localScale = new Vector3(-towerH, towerH, 1f);
        }
        else
        {
            SprRect("TowerBody", white, Hex("#2D2040"),
                new Vector3(0, towerCentreY, 0), new Vector3(towerH * 0.6f, towerH, 1),
                shakeRoot, sortingOrder: 2);
        }

        // ── Crack container ──
        var ccGo = new GameObject("CrackContainer");
        ccGo.transform.SetParent(shakeRoot, false);
        ccGo.transform.localPosition = new Vector3(0, towerCentreY, -0.3f);
        crackContainer = ccGo.transform;

        // ── Spear pivot — placed at the speargun position on td_tower.
        // CHANGED: re-estimated from the actual in-game screenshot (the harpoon
        // basket sits low on the sub, close to horizontal centre rather than
        // far left). If it's still off after rebuilding, nudge these two
        // multipliers — x closer to 0 moves the pivot toward the sub's centre,
        // a more negative y moves it further down the sub.
        var pivotGo = new GameObject("SpeargunPivot");
        pivotGo.transform.SetParent(shakeRoot, false);
        pivotGo.transform.localPosition = new Vector3(
            -towerH * 0.07f,                // near horizontal centre of the sub
            towerCentreY - towerH * 0.43f,  // low on the sub, near the harpoon basket
            -0.5f);
        speargunPivot = pivotGo.transform;

        // Spear spawn point — tip of the gun, further left
        var sspGo = new GameObject("SpearSpawnPoint");
        sspGo.transform.SetParent(pivotGo.transform, false);
        sspGo.transform.localPosition = new Vector3(-0.4f, 0, 0);
        spearSpawnPoint = sspGo.transform;
    }

    // =================================================================
    // HUD — hearts left, score centre, wave right, timer top-right
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
        var waveTxt = UTxt(hud.rectTransform, "WaveText", "Wave 1",
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
        mgr.scoreText = scoreTxt;
        mgr.waveText = waveTxt;
        mgr.comboText = comboTxt;
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

        var stepLbl = UTxt(crt, "StepLabel", "1 of 2", 18, MutedText, TextAlignmentOptions.Center);
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
    // Fish pool sprite loader (same pattern as EmailSwiperBuilder)
    // =================================================================

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

    // =================================================================
    // Sprite / audio helpers
    // =================================================================

    static Sprite FindSprite(string name)
    {
        foreach (var g in AssetDatabase.FindAssets(name + " t:Sprite"))
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            if (Path.GetFileNameWithoutExtension(p).ToLower() == name.ToLower())
            { var s = AssetDatabase.LoadAssetAtPath<Sprite>(p); if (s != null) return s; }
        }
        foreach (var g in AssetDatabase.FindAssets(name + " t:Texture2D"))
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            if (Path.GetFileNameWithoutExtension(p).ToLower().Contains(name.ToLower()))
            { var s = AssetDatabase.LoadAssetAtPath<Sprite>(p); if (s != null) return s; }
        }
        return null;
    }

    static AudioClip FindAudio(string name)
    {
        foreach (var g in AssetDatabase.FindAssets(name + " t:AudioClip"))
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            if (Path.GetFileNameWithoutExtension(p).ToLower() == name.ToLower())
                return AssetDatabase.LoadAssetAtPath<AudioClip>(p);
        }
        return null;
    }

    static void LogFound(string n, Sprite s)
        => Debug.Log($"[TDBuilder] {n}: " + (s != null ? "✓" : "✗ not found"));

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
            var px = new Color[64];
            for (int i = 0; i < 64; i++) px[i] = Color.white;
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