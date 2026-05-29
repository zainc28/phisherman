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
/// Builds the Tower Defense scene from scratch.
///
/// Run via:    Phisherman > Build Tower Defense Scene
/// Output:     Assets/Scenes/TowerDefense.unity
///
/// Tower layout (world-space):
///   TowerRoot
///     ShakeRoot               ← shakes on impact
///       [computer sprite OR rectangle tower]
///       ScreenFlash           ← transparent overlay for red flash
///       CrackContainer        ← crack sprites added here on damage
///       Phisherman            ← placeholder character on top
///         Body / Hat / Rod
///
/// Sprites are loaded automatically if present in Assets/Sprites/:
///   computer.*              → tower body
///   Free Fish Icons/*.png   → attached to each enemy
///   Tower/*.png (or Kenney) → rocket / projectile
///
/// Re-running fully overwrites the scene.
/// </summary>
public static class TowerDefenseBuilder
{
    private const string ScenesDir = "Assets/Scenes";
    private const string ScenePath = "Assets/Scenes/TowerDefense.unity";

    private const float TowerX = 5.0f;
    private const float TowerY = -0.5f;
    private const float SpawnX = -8.0f;

    // ── Palette ──────────────────────────────────────────────────────────────
    private static readonly Color BgColor = Hex("#3D2E5C");
    private static readonly Color MonitorCase = Hex("#1A1D24");
    private static readonly Color MonitorBezel = Hex("#0F1218");
    private static readonly Color ScreenColor = Hex("#0A2D52");
    private static readonly Color ScreenAccent = Hex("#22C2DD");
    private static readonly Color StandColor = Hex("#2A2D34");

    private static readonly Color HudBg = Hex("#1A1D2E");
    private static readonly Color ScoreColor = Hex("#FFD93D");
    private static readonly Color WaveColor = Color.white;
    private static readonly Color ComboColor = Hex("#FF9F1C");

    private static readonly Color PanelBg = Color.white;
    private static readonly Color HeaderBlue = Hex("#1A73E8");
    private static readonly Color SafeGreen = Hex("#2ECC71");
    private static readonly Color RetryRed = Hex("#E74C3C");
    private static readonly Color MapGrey = Hex("#7F8C8D");
    private static readonly Color DarkText = Hex("#202124");
    private static readonly Color MutedText = Hex("#5F6368");

    // Phisherman placeholder colours
    private static readonly Color FishermanSkin = Hex("#F5C89A");
    private static readonly Color FishermanJacket = Hex("#2A4FA8");
    private static readonly Color FishermanHat = Hex("#8B5E1E");
    private static readonly Color FishermanRod = Hex("#7A4E1A");

    // =========================================================================
    // Entry point
    // =========================================================================

    [MenuItem("Phisherman/Build Tower Defense Scene")]
    public static void Build()
    {
        if (!Directory.Exists(ScenesDir)) Directory.CreateDirectory(ScenesDir);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // White pixel sprite (used for primitives, cracks, fallback rocket)
        var whiteSprite = EnsureWhitePixelSprite();

        // ── Try to load user-imported game sprites ────────────────────────────
        Sprite computerSprite = FindFirstSprite("computer");
        Sprite fishSprite = FindFirstSprite("", "Assets/Sprites/Free Fish Icons")
                             ?? FindFirstSprite("fish");
        Sprite rocketSprite = FindFirstSprite("", "Assets/Sprites/Tower")
                             ?? FindFirstSprite("rocket")
                             ?? FindFirstSprite("", "Assets/Sprites/kenney_tower-defense-top-down");

        Debug.Log($"[TowerDefenseBuilder] Loaded sprites — " +
                  $"computer:{computerSprite != null} " +
                  $"fish:{fishSprite != null} " +
                  $"rocket:{rocketSprite != null}");

        // ── Camera ────────────────────────────────────────────────────────────
        var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>(); camGo.AddComponent<AudioListener>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Hex("#1F1530");
        cam.orthographic = true;
        cam.orthographicSize = 4.5f;
        camGo.transform.position = new Vector3(0, 0, -10);

        // ── EventSystem ───────────────────────────────────────────────────────
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>(); es.AddComponent<StandaloneInputModule>();

        // ── Path background ───────────────────────────────────────────────────
        CreateSpriteRect("PathBackground", whiteSprite, BgColor,
            new Vector3(0, 0, 1), new Vector3(18f, 6f, 1f), sortingOrder: -10);

        // ── Spawn point ───────────────────────────────────────────────────────
        var spawnGo = new GameObject("SpawnPoint", typeof(Transform));
        spawnGo.transform.position = new Vector3(SpawnX, 0, 0);

        // ── Tower ─────────────────────────────────────────────────────────────
        var towerRoot = new GameObject("Tower", typeof(Transform));
        towerRoot.transform.position = new Vector3(TowerX, TowerY, 0);
        var shakeRoot = new GameObject("ShakeRoot", typeof(Transform));
        shakeRoot.transform.SetParent(towerRoot.transform, false);

        SpriteRenderer screenSr;
        Transform crackContainerT;
        Transform fishermanT;

        if (computerSprite != null)
        {
            screenSr = BuildTowerWithSprite(computerSprite, whiteSprite, shakeRoot.transform,
                                                 out crackContainerT, out fishermanT);
        }
        else
        {
            screenSr = BuildTowerRectangles(whiteSprite, shakeRoot.transform,
                                                 out crackContainerT, out fishermanT);
        }

        // ── Canvas ────────────────────────────────────────────────────────────
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

        // ── Manager ───────────────────────────────────────────────────────────
        var managerGo = new GameObject("GameManager");
        var manager = managerGo.AddComponent<TowerDefenseManager>();
        manager.heartSprite = TryGetCircleSprite();
        manager.whiteSprite = whiteSprite;
        manager.fishSprite = fishSprite;
        manager.rocketSprite = rocketSprite;
        manager.spawnPoint = spawnGo.transform;
        manager.towerTransform = towerRoot.transform;
        manager.towerShakeRoot = shakeRoot.transform;
        manager.towerScreenSr = screenSr;
        manager.crackContainer = crackContainerT;
        manager.fishermanTransform = fishermanT;

        // ── UI panels ─────────────────────────────────────────────────────────
        var hudPanel = BuildHud(canvasRT, manager);
        var tutorialPanel = BuildTutorialPanel(canvasRT, manager);
        var gameOverPanel = BuildGameOverPanel(canvasRT, manager);
        var winPanel = BuildWinPanel(canvasRT, manager);

        manager.hudPanel = hudPanel;
        manager.tutorialPanel = tutorialPanel;
        manager.gameOverPanel = gameOverPanel;
        manager.winPanel = winPanel;
        manager.commentator = BuildCommentator(canvasRT);

        hudPanel.SetActive(false);
        gameOverPanel.SetActive(false);
        winPanel.SetActive(false);
        tutorialPanel.SetActive(true);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings(ScenePath);
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log($"[TowerDefenseBuilder] Scene built → {ScenePath}");
    }

    // =========================================================================
    // Tower — uses the imported computer sprite as the body
    // =========================================================================

    private static SpriteRenderer BuildTowerWithSprite(
        Sprite computerSprite, Sprite white, Transform shakeRoot,
        out Transform crackContainer, out Transform fisherman)
    {
        // Computer image (fills the tower slot)
        CreateSpriteRect("TowerComputer", computerSprite, Color.white,
            new Vector3(0f, 0.6f, 0f), new Vector3(3.6f, 3.2f, 1f),
            parent: shakeRoot, sortingOrder: 2);

        // Transparent overlay used for the screen-hit red flash
        var flashSr = CreateSpriteRect("ScreenFlash", white,
            new Color(1f, 1f, 1f, 0f),
            new Vector3(0f, 0.8f, 0f), new Vector3(2.5f, 1.7f, 1f),
            parent: shakeRoot, sortingOrder: 4);

        // Screen text labels
        CreateScreenLabel(shakeRoot, "ScreenLabel", "PHISHERMAN OS",
            ScreenAccent, new Vector3(0f, 1.3f, -0.05f), 0.95f, 0.25f);
        CreateScreenLabel(shakeRoot, "ScreenStatus", "[ ALIVE ]",
            new Color(0.45f, 0.95f, 0.55f), new Vector3(0f, 0.3f, -0.05f), 0.7f, 0.18f);

        // Crack container (children spawned here on damage, positioned over screen area)
        var cc = new GameObject("CrackContainer", typeof(Transform));
        cc.transform.SetParent(flashSr.transform, false);
        cc.transform.localPosition = new Vector3(0, 0, -0.1f);
        crackContainer = cc.transform;

        // Phisherman on top of the computer
        fisherman = BuildPhisherman(white, shakeRoot, topY: 2.8f);
        return flashSr;
    }

    // =========================================================================
    // Tower — original rectangle-based construction (fallback, no sprite)
    // =========================================================================

    private static SpriteRenderer BuildTowerRectangles(
        Sprite white, Transform shakeRoot,
        out Transform crackContainer, out Transform fisherman)
    {
        // Monitor body
        CreateSpriteRect("MonitorBody", white, MonitorCase,
            new Vector3(0f, 1f, 0f), new Vector3(3.5f, 2.5f, 1f),
            parent: shakeRoot, sortingOrder: 1);
        CreateSpriteRect("MonitorBezel", white, MonitorBezel,
            new Vector3(0f, 1f, 0f), new Vector3(3.2f, 2.2f, 1f),
            parent: shakeRoot, sortingOrder: 2);

        var screen = CreateSpriteRect("Screen", white, ScreenColor,
            new Vector3(0f, 1f, 0f), new Vector3(3.0f, 2.0f, 1f),
            parent: shakeRoot, sortingOrder: 3);

        CreateScreenLabel(shakeRoot, "ScreenLabel", "PHISHERMAN OS",
            ScreenAccent, new Vector3(0f, 1.78f, -0.05f), 0.95f, 0.25f);
        CreateScreenLabel(shakeRoot, "ScreenStatus", "[ ALIVE ]",
            new Color(0.45f, 0.95f, 0.55f), new Vector3(0f, 0.3f, -0.05f), 0.7f, 0.18f);

        // Crack container
        var cc = new GameObject("CrackContainer", typeof(Transform));
        cc.transform.SetParent(screen.transform, false);
        cc.transform.localPosition = new Vector3(0, 0, -0.1f);
        crackContainer = cc.transform;

        // Stand
        CreateSpriteRect("StandNeck", white, StandColor,
            new Vector3(0f, -0.6f, 0f), new Vector3(0.55f, 0.6f, 1f),
            parent: shakeRoot, sortingOrder: 1);
        CreateSpriteRect("StandBase", white, StandColor,
            new Vector3(0f, -1.0f, 0f), new Vector3(1.7f, 0.22f, 1f),
            parent: shakeRoot, sortingOrder: 1);

        // Phisherman on top of monitor
        fisherman = BuildPhisherman(white, shakeRoot, topY: 2.5f);
        return screen;
    }

    // =========================================================================
    // Phisherman placeholder
    //   A simple "body + hat + fishing rod" figure that sits on top of the
    //   tower. Replace these SpriteRenderers with a real sprite asset later.
    // =========================================================================

    private static Transform BuildPhisherman(Sprite white, Transform parent, float topY)
    {
        var root = new GameObject("Phisherman");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = new Vector3(0f, topY, 0f);

        // Body (skin-tone oval)
        CreateSpriteRect("Body", white, FishermanSkin,
            new Vector3(0f, 0f, 0f), new Vector3(0.52f, 0.72f, 1f),
            parent: root.transform, sortingOrder: 5);

        // Jacket / overalls (lower half)
        CreateSpriteRect("Jacket", white, FishermanJacket,
            new Vector3(0f, -0.22f, 0f), new Vector3(0.52f, 0.38f, 1f),
            parent: root.transform, sortingOrder: 6);

        // Hat brim
        CreateSpriteRect("HatBrim", white, FishermanHat,
            new Vector3(0f, 0.40f, 0f), new Vector3(0.74f, 0.11f, 1f),
            parent: root.transform, sortingOrder: 7);

        // Hat crown
        CreateSpriteRect("HatCrown", white, FishermanHat,
            new Vector3(0f, 0.58f, 0f), new Vector3(0.44f, 0.32f, 1f),
            parent: root.transform, sortingOrder: 7);

        // Fishing rod — extends LEFT toward enemies
        var rodSr = CreateSpriteRect("FishingRod", white, FishermanRod,
            new Vector3(-0.60f, 0.18f, 0f), new Vector3(1.15f, 0.065f, 1f),
            parent: root.transform, sortingOrder: 6);
        rodSr.transform.localRotation = Quaternion.Euler(0, 0, 12f); // slight upward angle

        // Rod tip (small circle)
        CreateSpriteRect("RodTip", white, new Color(0.85f, 0.65f, 0.15f),
            new Vector3(-1.12f, 0.42f, 0f), new Vector3(0.12f, 0.12f, 1f),
            parent: root.transform, sortingOrder: 7);

        return root.transform;
    }

    // =========================================================================
    // HUD
    // =========================================================================

    private static GameObject BuildHud(RectTransform parent, TowerDefenseManager manager)
    {
        var hud = AddImage(parent, "HUD", new Color(0.10f, 0.11f, 0.18f, 0.85f));
        AnchorTopStretch(hud.rectTransform, height: 80);

        // Hearts (left)
        var heartsHolder = new GameObject("HeartsHolder", typeof(RectTransform));
        heartsHolder.transform.SetParent(hud.rectTransform, false);
        var hhrt = heartsHolder.GetComponent<RectTransform>();
        hhrt.anchorMin = new Vector2(0, 0); hhrt.anchorMax = new Vector2(0, 1);
        hhrt.pivot = new Vector2(0, 0.5f);
        hhrt.sizeDelta = new Vector2(280, 0);
        hhrt.anchoredPosition = new Vector2(40, 0);
        var hlg = heartsHolder.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.spacing = 6;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;
        hlg.childControlWidth = hlg.childControlHeight = false;

        // Score (centre)
        var scoreText = AddText(hud.rectTransform, "ScoreText", "Score: 0",
            42, ScoreColor, TextAlignmentOptions.Center, FontStyles.Bold);
        var srt = scoreText.rectTransform;
        srt.anchorMin = new Vector2(0.5f, 0); srt.anchorMax = new Vector2(0.5f, 1);
        srt.pivot = new Vector2(0.5f, 0.5f);
        srt.sizeDelta = new Vector2(360, 0); srt.anchoredPosition = Vector2.zero;

        // Wave (right)
        var waveText = AddText(hud.rectTransform, "WaveText", "Wave 1 / 4",
            32, WaveColor, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
        var wrt = waveText.rectTransform;
        wrt.anchorMin = new Vector2(1, 0); wrt.anchorMax = new Vector2(1, 1);
        wrt.pivot = new Vector2(1, 0.5f);
        wrt.sizeDelta = new Vector2(280, 0); wrt.anchoredPosition = new Vector2(-40, 0);

        // Combo (overlay, below HUD)
        var comboText = AddText(parent, "ComboText", "",
            48, ComboColor, TextAlignmentOptions.Center, FontStyles.Bold);
        var crt = comboText.rectTransform;
        crt.anchorMin = new Vector2(0.5f, 0.5f); crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(600, 80); crt.anchoredPosition = new Vector2(0, -300);
        comboText.raycastTarget = false;

        manager.heartsContainer = heartsHolder.transform;
        manager.scoreText = scoreText;
        manager.waveText = waveText;
        manager.comboText = comboText;

        return hud.gameObject;
    }

    // =========================================================================
    // Tutorial panel
    // =========================================================================

    private static GameObject BuildTutorialPanel(RectTransform parent, TowerDefenseManager manager)
    {
        var overlay = AddImage(parent, "TutorialOverlay", new Color(0, 0, 0, 0.7f));
        Stretch(overlay.rectTransform); overlay.raycastTarget = true;

        var card = AddImage(overlay.rectTransform, "Card", PanelBg);
        var crt = card.rectTransform;
        crt.anchorMin = new Vector2(0.5f, 0.5f); crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f); crt.sizeDelta = new Vector2(960, 640);

        var header = AddImage(crt, "Header", HeaderBlue);
        AnchorTopStretch(header.rectTransform, height: 90);
        var title = AddText(header.rectTransform, "Title",
            "Phish Patrol: Password Edition",
            36, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(title.rectTransform); title.raycastTarget = false;

        var body = AddText(crt, "Body", "Tutorial body",
            26, DarkText, TextAlignmentOptions.Center);
        var brt = body.rectTransform;
        brt.anchorMin = new Vector2(0, 0); brt.anchorMax = new Vector2(1, 1);
        brt.offsetMin = new Vector2(50, 160); brt.offsetMax = new Vector2(-50, -110);

        var stepLabel = AddText(crt, "StepLabel", "1 of 2",
            20, MutedText, TextAlignmentOptions.Center);
        var slrt = stepLabel.rectTransform;
        slrt.anchorMin = new Vector2(0, 0); slrt.anchorMax = new Vector2(1, 0);
        slrt.pivot = new Vector2(0.5f, 0);
        slrt.sizeDelta = new Vector2(0, 30); slrt.anchoredPosition = new Vector2(0, 130);

        var nextBtn = AddButton(crt, "NextBtn", "Next", 32, SafeGreen, Color.white);
        var nbrt = nextBtn.GetComponent<RectTransform>();
        nbrt.anchorMin = new Vector2(0.5f, 0); nbrt.anchorMax = new Vector2(0.5f, 0);
        nbrt.pivot = new Vector2(0.5f, 0);
        nbrt.sizeDelta = new Vector2(280, 80); nbrt.anchoredPosition = new Vector2(0, 30);
        UnityEventTools.AddPersistentListener(nextBtn.GetComponent<Button>().onClick,
            manager.OnTutorialNext);

        manager.tutorialTitleText = title;
        manager.tutorialBodyText = body;
        manager.tutorialStepText = stepLabel;
        manager.tutorialNextButton = nextBtn.GetComponent<Button>();
        manager.tutorialNextButtonText = nextBtn.transform.Find("Label").GetComponent<TMP_Text>();

        return overlay.gameObject;
    }

    // =========================================================================
    // Game Over panel
    // =========================================================================

    private static GameObject BuildGameOverPanel(RectTransform parent, TowerDefenseManager manager)
    {
        var overlay = AddImage(parent, "GameOverOverlay", new Color(0, 0, 0, 0.78f));
        Stretch(overlay.rectTransform); overlay.raycastTarget = true;

        var card = AddImage(overlay.rectTransform, "Card", PanelBg);
        var crt = card.rectTransform;
        crt.anchorMin = new Vector2(0.5f, 0.5f); crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f); crt.sizeDelta = new Vector2(900, 600);

        var header = AddImage(crt, "Header", RetryRed);
        AnchorTopStretch(header.rectTransform, height: 100);
        var hLabel = AddText(header.rectTransform, "HLabel",
            "Tower Cracked!", 48, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(hLabel.rectTransform); hLabel.raycastTarget = false;

        var scoreText = AddText(crt, "ScoreText", "Score: 0",
            44, DarkText, TextAlignmentOptions.Center, FontStyles.Bold);
        var srt = scoreText.rectTransform;
        srt.anchorMin = new Vector2(0, 1); srt.anchorMax = new Vector2(1, 1);
        srt.pivot = new Vector2(0.5f, 1);
        srt.sizeDelta = new Vector2(0, 80); srt.anchoredPosition = new Vector2(0, -130);

        var msg = AddText(crt, "Message", "",
            22, MutedText, TextAlignmentOptions.Center);
        var mrt = msg.rectTransform;
        mrt.anchorMin = new Vector2(0, 0); mrt.anchorMax = new Vector2(1, 1);
        mrt.offsetMin = new Vector2(60, 160); mrt.offsetMax = new Vector2(-60, -240);

        var retry = AddButton(crt, "RetryBtn", "Retry", 28, SafeGreen, Color.white);
        var rrt = retry.GetComponent<RectTransform>();
        rrt.anchorMin = new Vector2(0.5f, 0); rrt.anchorMax = new Vector2(0.5f, 0);
        rrt.pivot = new Vector2(1, 0);
        rrt.sizeDelta = new Vector2(260, 70); rrt.anchoredPosition = new Vector2(-20, 50);
        UnityEventTools.AddPersistentListener(retry.GetComponent<Button>().onClick, manager.OnRetry);

        var back = AddButton(crt, "BackBtn", "Back to Map", 28, MapGrey, Color.white);
        var bart = back.GetComponent<RectTransform>();
        bart.anchorMin = new Vector2(0.5f, 0); bart.anchorMax = new Vector2(0.5f, 0);
        bart.pivot = new Vector2(0, 0);
        bart.sizeDelta = new Vector2(260, 70); bart.anchoredPosition = new Vector2(20, 50);
        UnityEventTools.AddPersistentListener(back.GetComponent<Button>().onClick, manager.OnReturnToMap);

        manager.gameOverScoreText = scoreText;
        manager.gameOverMessageText = msg;

        return overlay.gameObject;
    }

    // =========================================================================
    // Win panel
    // =========================================================================

    private static GameObject BuildWinPanel(RectTransform parent, TowerDefenseManager manager)
    {
        var overlay = AddImage(parent, "WinOverlay", new Color(0, 0, 0, 0.7f));
        Stretch(overlay.rectTransform); overlay.raycastTarget = true;

        var card = AddImage(overlay.rectTransform, "Card", PanelBg);
        var crt = card.rectTransform;
        crt.anchorMin = new Vector2(0.5f, 0.5f); crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f); crt.sizeDelta = new Vector2(900, 600);

        var header = AddImage(crt, "Header", SafeGreen);
        AnchorTopStretch(header.rectTransform, height: 100);
        var hLabel = AddText(header.rectTransform, "HLabel",
            "Tower Defended!", 48, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(hLabel.rectTransform); hLabel.raycastTarget = false;

        var stars = AddText(crt, "Stars", "* * *",
            72, ScoreColor, TextAlignmentOptions.Center, FontStyles.Bold);
        var stt = stars.rectTransform;
        stt.anchorMin = new Vector2(0, 1); stt.anchorMax = new Vector2(1, 1);
        stt.pivot = new Vector2(0.5f, 1);
        stt.sizeDelta = new Vector2(0, 100); stt.anchoredPosition = new Vector2(0, -130);

        var scoreText = AddText(crt, "ScoreText", "0 / 100",
            44, DarkText, TextAlignmentOptions.Center, FontStyles.Bold);
        var srt = scoreText.rectTransform;
        srt.anchorMin = new Vector2(0, 1); srt.anchorMax = new Vector2(1, 1);
        srt.pivot = new Vector2(0.5f, 1);
        srt.sizeDelta = new Vector2(0, 80); srt.anchoredPosition = new Vector2(0, -270);

        var retry = AddButton(crt, "PlayAgainBtn", "Play Again", 28, SafeGreen, Color.white);
        var rrt = retry.GetComponent<RectTransform>();
        rrt.anchorMin = new Vector2(0.5f, 0); rrt.anchorMax = new Vector2(0.5f, 0);
        rrt.pivot = new Vector2(1, 0);
        rrt.sizeDelta = new Vector2(260, 70); rrt.anchoredPosition = new Vector2(-20, 50);
        UnityEventTools.AddPersistentListener(retry.GetComponent<Button>().onClick, manager.OnRetry);

        var back = AddButton(crt, "BackBtn", "Back to Map", 28, MapGrey, Color.white);
        var bart = back.GetComponent<RectTransform>();
        bart.anchorMin = new Vector2(0.5f, 0); bart.anchorMax = new Vector2(0.5f, 0);
        bart.pivot = new Vector2(0, 0);
        bart.sizeDelta = new Vector2(260, 70); bart.anchoredPosition = new Vector2(20, 50);
        UnityEventTools.AddPersistentListener(back.GetComponent<Button>().onClick, manager.OnReturnToMap);

        manager.winScoreText = scoreText;
        manager.winStarsText = stars;

        return overlay.gameObject;
    }

    // =========================================================================
    // Commentator
    // =========================================================================

    private static Commentator BuildCommentator(RectTransform parent)
    {
        var root = new GameObject("CommentatorPanel", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        var rrt = root.GetComponent<RectTransform>();
        rrt.anchorMin = new Vector2(0, 0); rrt.anchorMax = new Vector2(0, 0);
        rrt.pivot = new Vector2(0, 0);
        rrt.sizeDelta = new Vector2(620, 140); rrt.anchoredPosition = new Vector2(30, 30);

        var portrait = AddImage(rrt, "Portrait", new Color(1f, 0.72f, 0.78f));
        portrait.sprite = TryGetCircleSprite();
        portrait.preserveAspect = true;
        var prt = portrait.rectTransform;
        prt.anchorMin = new Vector2(0, 0); prt.anchorMax = new Vector2(0, 1);
        prt.pivot = new Vector2(0, 0.5f);
        prt.sizeDelta = new Vector2(120, 0); prt.anchoredPosition = Vector2.zero;

        var letter = AddText(portrait.rectTransform, "Letter", "G",
            64, new Color(0.20f, 0.10f, 0.18f), TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(letter.rectTransform);

        var bubble = AddImage(rrt, "Bubble", Color.white);
        var brt = bubble.rectTransform;
        brt.anchorMin = new Vector2(0, 0); brt.anchorMax = new Vector2(1, 1);
        brt.offsetMin = new Vector2(140, 0); brt.offsetMax = Vector2.zero;

        var speakerLabel = AddText(bubble.rectTransform, "SpeakerLabel", "Grandma",
            18, new Color(0.78f, 0.30f, 0.50f), TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        var slrt = speakerLabel.rectTransform;
        slrt.anchorMin = new Vector2(0, 1); slrt.anchorMax = new Vector2(1, 1);
        slrt.pivot = new Vector2(0, 1);
        slrt.sizeDelta = new Vector2(0, 26); slrt.anchoredPosition = new Vector2(20, -8);

        var bubbleText = AddText(bubble.rectTransform, "BubbleText", "...",
            22, new Color(0.13f, 0.13f, 0.13f), TextAlignmentOptions.MidlineLeft);
        bubbleText.textWrappingMode = TextWrappingModes.Normal;
        var btrt = bubbleText.rectTransform;
        btrt.anchorMin = new Vector2(0, 0); btrt.anchorMax = new Vector2(1, 1);
        btrt.offsetMin = new Vector2(20, 8); btrt.offsetMax = new Vector2(-20, -32);

        var comm = root.AddComponent<Commentator>();
        comm.root = root; comm.portrait = portrait;
        comm.portraitLetter = letter; comm.bubbleBg = bubble;
        comm.bubbleText = bubbleText; comm.speakerLabel = speakerLabel;
        comm.portraitSprite = TryGetCircleSprite();
        comm.speakerName = "Grandma";
        comm.portraitInitial = "G";
        comm.portraitColor = new Color(1f, 0.72f, 0.78f);

        return comm;
    }

    // =========================================================================
    // Helpers — world-space
    // =========================================================================

    private static SpriteRenderer CreateSpriteRect(string name, Sprite sprite, Color color,
        Vector3 localPos, Vector3 scale,
        Transform parent = null, int sortingOrder = 0)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = sortingOrder;
        return sr;
    }

    private static void CreateScreenLabel(Transform parent, string name, string text,
        Color color, Vector3 localPos, float worldWidth, float worldHeight)
    {
        const float k = 0.008f;
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = new Vector3(k, k, 1f);
        var c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace;
        c.sortingOrder = 4;
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(worldWidth / k, worldHeight / k);

        var label = new GameObject("Label");
        label.transform.SetParent(go.transform, false);
        var tmp = label.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = 28; tmp.color = color;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        var lrt = label.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
    }

    // =========================================================================
    // Helpers — UI
    // =========================================================================

    private static Image AddImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>(); img.color = color;
        return img;
    }

    private static TMP_Text AddText(Transform parent, string name, string content,
        int fontSize, Color color, TextAlignmentOptions align,
        FontStyles style = FontStyles.Normal)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = content; t.fontSize = fontSize; t.color = color;
        t.alignment = align; t.fontStyle = style; t.raycastTarget = false;
        return t;
    }

    private static GameObject AddButton(Transform parent, string name, string label,
        int fontSize, Color bg, Color textColor)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>(); img.color = bg;
        var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        var t = AddText(go.transform, "Label", label, fontSize, textColor,
                          TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(t.rectTransform);
        return go;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = rt.offsetMin = Vector2.zero;
        rt.anchorMax = Vector2.one; rt.offsetMax = Vector2.zero;
    }

    private static void AnchorTopStretch(RectTransform rt, float height)
    {
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.sizeDelta = new Vector2(0, height);
        rt.anchoredPosition = Vector2.zero;
    }

    private static Color Hex(string hex)
        => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

    private static Sprite TryGetCircleSprite()
    {
        try { return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"); }
        catch { return null; }
    }

    /// <summary>
    /// Searches Assets/Sprites (and sub-folders) for the first sprite whose
    /// asset name contains <paramref name="nameHint"/>. If nameHint is empty
    /// all sprites in the folder are eligible (returns the first found).
    /// </summary>
    private static Sprite FindFirstSprite(string nameHint,
        string searchFolder = "Assets/Sprites")
    {
        string query = string.IsNullOrEmpty(nameHint) ? "t:Sprite" : $"t:Sprite {nameHint}";
        var guids = AssetDatabase.FindAssets(query, new[] { searchFolder });
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) return sprite;
        }
        return null;
    }

    // =========================================================================
    // White-pixel sprite asset
    // =========================================================================

    private static Sprite EnsureWhitePixelSprite()
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

    private static void AddSceneToBuildSettings(string path)
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(s => s.path == path))
        {
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}