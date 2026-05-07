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
/// Constructs a 2D scene with a computer-shaped tower on the right,
/// spawn point on the left, and Screen Space Overlay UI for HUD/panels.
///
/// Re-running fully overwrites the scene.
/// </summary>
public static class TowerDefenseBuilder
{
    private const string ScenesDir = "Assets/Scenes";
    private const string ScenePath = "Assets/Scenes/TowerDefense.unity";

    // World-space layout
    private const float TowerX = 5.0f;
    private const float TowerY = -0.5f;
    private const float SpawnX = -8.0f;

    // Colors
    private static readonly Color BgColor = Hex("#3D2E5C"); // path background purple
    private static readonly Color MonitorCase = Hex("#1A1D24");
    private static readonly Color MonitorBezel = Hex("#0F1218");
    private static readonly Color ScreenColor = Hex("#0A2D52");
    private static readonly Color ScreenAccent = Hex("#22C2DD");
    private static readonly Color StandColor = Hex("#2A2D34");

    private static readonly Color HudBg = Hex("#1A1D2E");
    private static readonly Color ScoreColor = Hex("#FFD93D");
    private static readonly Color WaveColor = Hex("#FFFFFF");
    private static readonly Color ComboColor = Hex("#FF9F1C");

    private static readonly Color PanelBg = Color.white;
    private static readonly Color HeaderBlue = Hex("#1A73E8");
    private static readonly Color SafeGreen = Hex("#2ECC71");
    private static readonly Color RetryRed = Hex("#E74C3C");
    private static readonly Color MapGrey = Hex("#7F8C8D");
    private static readonly Color DarkText = Hex("#202124");
    private static readonly Color MutedText = Hex("#5F6368");

    [MenuItem("Phisherman/Build Tower Defense Scene")]
    public static void Build()
    {
        if (!Directory.Exists(ScenesDir)) Directory.CreateDirectory(ScenesDir);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Ensure white sprite exists on disk (for tower parts, rockets, cracks)
        var whiteSprite = EnsureWhitePixelSprite();

        // === Camera ===
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        camGo.AddComponent<AudioListener>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Hex("#1F1530");
        cam.orthographic = true;
        cam.orthographicSize = 4.5f;
        camGo.transform.position = new Vector3(0, 0, -10);

        // === EventSystem ===
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();

        // === Path background ===
        var pathBg = CreateSpriteRect("PathBackground", whiteSprite, BgColor,
            new Vector3(0, 0, 1), new Vector3(18f, 6f, 1f), sortingOrder: -10);

        // === SpawnPoint marker ===
        var spawnGo = new GameObject("SpawnPoint", typeof(Transform));
        spawnGo.transform.position = new Vector3(SpawnX, 0, 0);

        // === Computer tower ===
        var towerRoot = new GameObject("Tower", typeof(Transform));
        towerRoot.transform.position = new Vector3(TowerX, TowerY, 0);

        var shakeRoot = new GameObject("ShakeRoot", typeof(Transform));
        shakeRoot.transform.SetParent(towerRoot.transform, false);

        // Monitor body (large dark rect)
        CreateSpriteRect("MonitorBody", whiteSprite, MonitorCase,
            new Vector3(0, 1f, 0), new Vector3(3.5f, 2.5f, 1f),
            parent: shakeRoot.transform, sortingOrder: 1);

        // Bezel (slightly inset, darker)
        CreateSpriteRect("MonitorBezel", whiteSprite, MonitorBezel,
            new Vector3(0, 1f, 0), new Vector3(3.2f, 2.2f, 1f),
            parent: shakeRoot.transform, sortingOrder: 2);

        // Screen (this gets the color flash + cracks underneath)
        var screen = CreateSpriteRect("Screen", whiteSprite, ScreenColor,
            new Vector3(0, 1f, 0), new Vector3(3.0f, 2.0f, 1f),
            parent: shakeRoot.transform, sortingOrder: 3);

        // Screen label (world-space canvas)
        CreateScreenLabel(screen.transform, "ScreenLabel", "PHISHERMAN OS", ScreenAccent,
            new Vector3(0, 0.78f, -0.05f), 0.95f, 0.25f);
        CreateScreenLabel(screen.transform, "ScreenStatus", "[ ALIVE ]",
            new Color(0.45f, 0.95f, 0.55f),
            new Vector3(0, -0.7f, -0.05f), 0.7f, 0.18f);

        // Crack container (children spawn here)
        var crackContainer = new GameObject("CrackContainer", typeof(Transform));
        crackContainer.transform.SetParent(screen.transform, false);
        crackContainer.transform.localPosition = new Vector3(0, 0, -0.1f);

        // Stand neck
        CreateSpriteRect("StandNeck", whiteSprite, StandColor,
            new Vector3(0, -0.6f, 0), new Vector3(0.55f, 0.6f, 1f),
            parent: shakeRoot.transform, sortingOrder: 1);

        // Stand base
        CreateSpriteRect("StandBase", whiteSprite, StandColor,
            new Vector3(0, -1.0f, 0), new Vector3(1.7f, 0.22f, 1f),
            parent: shakeRoot.transform, sortingOrder: 1);

        // === Canvas (overlay) ===
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
        var manager = managerGo.AddComponent<TowerDefenseManager>();
        manager.heartSprite = TryGetCircleSprite();
        manager.whiteSprite = whiteSprite;
        manager.spawnPoint = spawnGo.transform;
        manager.towerTransform = towerRoot.transform;
        manager.towerShakeRoot = shakeRoot.transform;
        manager.towerScreenSr = screen;
        manager.crackContainer = crackContainer.transform;

        // === Build UI sections ===
        var hudPanel = BuildHud(canvasRT, manager);
        var tutorialPanel = BuildTutorialPanel(canvasRT, manager);
        var gameOverPanel = BuildGameOverPanel(canvasRT, manager);
        var winPanel = BuildWinPanel(canvasRT, manager);

        manager.hudPanel = hudPanel;
        manager.tutorialPanel = tutorialPanel;
        manager.gameOverPanel = gameOverPanel;
        manager.winPanel = winPanel;

        // Commentator (grandma's reactive speech bubble in bottom-left)
        manager.commentator = BuildCommentator(canvasRT);

        hudPanel.SetActive(false);
        gameOverPanel.SetActive(false);
        winPanel.SetActive(false);
        tutorialPanel.SetActive(true);

        // === Save ===
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings(ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[TowerDefenseBuilder] Built scene at {ScenePath}");
    }

    // =====================================================================
    // HUD
    // =====================================================================

    private static GameObject BuildHud(RectTransform parent, TowerDefenseManager manager)
    {
        var hud = AddImage(parent, "HUD", new Color(0.10f, 0.11f, 0.18f, 0.85f));
        AnchorTopStretch(hud.rectTransform, height: 80);

        // Hearts holder (left)
        var heartsHolder = new GameObject("HeartsHolder", typeof(RectTransform));
        heartsHolder.transform.SetParent(hud.rectTransform, false);
        var hhrt = heartsHolder.GetComponent<RectTransform>();
        hhrt.anchorMin = new Vector2(0, 0);
        hhrt.anchorMax = new Vector2(0, 1);
        hhrt.pivot = new Vector2(0, 0.5f);
        hhrt.sizeDelta = new Vector2(280, 0);
        hhrt.anchoredPosition = new Vector2(40, 0);

        var hlg = heartsHolder.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.spacing = 6;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        // Score (center)
        var scoreText = AddText(hud.rectTransform, "ScoreText", "Score: 0",
            42, ScoreColor, TextAlignmentOptions.Center, FontStyles.Bold);
        var srt = scoreText.rectTransform;
        srt.anchorMin = new Vector2(0.5f, 0); srt.anchorMax = new Vector2(0.5f, 1);
        srt.pivot = new Vector2(0.5f, 0.5f);
        srt.sizeDelta = new Vector2(360, 0);
        srt.anchoredPosition = Vector2.zero;

        // Wave (right)
        var waveText = AddText(hud.rectTransform, "WaveText", "Wave 1 / 3",
            32, WaveColor, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
        var wrt = waveText.rectTransform;
        wrt.anchorMin = new Vector2(1, 0); wrt.anchorMax = new Vector2(1, 1);
        wrt.pivot = new Vector2(1, 0.5f);
        wrt.sizeDelta = new Vector2(280, 0);
        wrt.anchoredPosition = new Vector2(-40, 0);

        // Combo (overlay below HUD, big)
        var comboText = AddText(parent, "ComboText", "",
            48, ComboColor, TextAlignmentOptions.Center, FontStyles.Bold);
        var crt = comboText.rectTransform;
        crt.anchorMin = new Vector2(0.5f, 0.5f);
        crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(600, 80);
        crt.anchoredPosition = new Vector2(0, -300);
        comboText.raycastTarget = false;

        manager.heartsContainer = heartsHolder.transform;
        manager.scoreText = scoreText;
        manager.waveText = waveText;
        manager.comboText = comboText;

        return hud.gameObject;
    }

    // =====================================================================
    // Tutorial panel
    // =====================================================================

    private static GameObject BuildTutorialPanel(RectTransform parent, TowerDefenseManager manager)
    {
        var overlay = AddImage(parent, "TutorialOverlay", new Color(0, 0, 0, 0.7f));
        Stretch(overlay.rectTransform);
        overlay.raycastTarget = true;

        var card = AddImage(overlay.rectTransform, "Card", PanelBg);
        var crt = card.rectTransform;
        crt.anchorMin = new Vector2(0.5f, 0.5f);
        crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(960, 640);

        var header = AddImage(crt, "Header", HeaderBlue);
        AnchorTopStretch(header.rectTransform, height: 90);

        var title = AddText(header.rectTransform, "Title", "Phish Patrol: Password Edition",
            36, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(title.rectTransform);
        title.raycastTarget = false;

        var body = AddText(crt, "Body",
            "Tutorial body", 26, DarkText, TextAlignmentOptions.Center);
        var brt = body.rectTransform;
        brt.anchorMin = new Vector2(0, 0);
        brt.anchorMax = new Vector2(1, 1);
        brt.offsetMin = new Vector2(50, 160);
        brt.offsetMax = new Vector2(-50, -110);

        var stepLabel = AddText(crt, "StepLabel", "1 of 2",
            20, MutedText, TextAlignmentOptions.Center);
        var serrt = stepLabel.rectTransform;
        serrt.anchorMin = new Vector2(0, 0); serrt.anchorMax = new Vector2(1, 0);
        serrt.pivot = new Vector2(0.5f, 0);
        serrt.sizeDelta = new Vector2(0, 30);
        serrt.anchoredPosition = new Vector2(0, 130);

        var nextBtn = AddButton(crt, "NextBtn", "Next", 32, SafeGreen, Color.white);
        var nbrt = nextBtn.GetComponent<RectTransform>();
        nbrt.anchorMin = new Vector2(0.5f, 0); nbrt.anchorMax = new Vector2(0.5f, 0);
        nbrt.pivot = new Vector2(0.5f, 0);
        nbrt.sizeDelta = new Vector2(280, 80);
        nbrt.anchoredPosition = new Vector2(0, 30);

        UnityEventTools.AddPersistentListener(nextBtn.GetComponent<Button>().onClick,
            manager.OnTutorialNext);

        manager.tutorialTitleText = title;
        manager.tutorialBodyText = body;
        manager.tutorialStepText = stepLabel;
        manager.tutorialNextButton = nextBtn.GetComponent<Button>();
        manager.tutorialNextButtonText = nextBtn.transform.Find("Label")
            .GetComponent<TMP_Text>();

        return overlay.gameObject;
    }

    // =====================================================================
    // Game Over panel
    // =====================================================================

    private static GameObject BuildGameOverPanel(RectTransform parent, TowerDefenseManager manager)
    {
        var overlay = AddImage(parent, "GameOverOverlay", new Color(0, 0, 0, 0.78f));
        Stretch(overlay.rectTransform);
        overlay.raycastTarget = true;

        var card = AddImage(overlay.rectTransform, "Card", PanelBg);
        var crt = card.rectTransform;
        crt.anchorMin = new Vector2(0.5f, 0.5f); crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(900, 600);

        var header = AddImage(crt, "Header", RetryRed);
        AnchorTopStretch(header.rectTransform, height: 100);
        var hLabel = AddText(header.rectTransform, "HLabel",
            "Tower Cracked!", 48, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(hLabel.rectTransform);
        hLabel.raycastTarget = false;

        var scoreText = AddText(crt, "ScoreText", "Score: 0",
            44, DarkText, TextAlignmentOptions.Center, FontStyles.Bold);
        var srt = scoreText.rectTransform;
        srt.anchorMin = new Vector2(0, 1); srt.anchorMax = new Vector2(1, 1);
        srt.pivot = new Vector2(0.5f, 1);
        srt.sizeDelta = new Vector2(0, 80);
        srt.anchoredPosition = new Vector2(0, -130);

        var msg = AddText(crt, "Message", "",
            22, MutedText, TextAlignmentOptions.Center);
        var mrt = msg.rectTransform;
        mrt.anchorMin = new Vector2(0, 0); mrt.anchorMax = new Vector2(1, 1);
        mrt.offsetMin = new Vector2(60, 160);
        mrt.offsetMax = new Vector2(-60, -240);

        var retry = AddButton(crt, "RetryBtn", "Retry", 28, SafeGreen, Color.white);
        var rrt = retry.GetComponent<RectTransform>();
        rrt.anchorMin = new Vector2(0.5f, 0); rrt.anchorMax = new Vector2(0.5f, 0);
        rrt.pivot = new Vector2(1, 0);
        rrt.sizeDelta = new Vector2(260, 70);
        rrt.anchoredPosition = new Vector2(-20, 50);
        UnityEventTools.AddPersistentListener(retry.GetComponent<Button>().onClick,
            manager.OnRetry);

        var back = AddButton(crt, "BackBtn", "Back to Map", 28, MapGrey, Color.white);
        var bart = back.GetComponent<RectTransform>();
        bart.anchorMin = new Vector2(0.5f, 0); bart.anchorMax = new Vector2(0.5f, 0);
        bart.pivot = new Vector2(0, 0);
        bart.sizeDelta = new Vector2(260, 70);
        bart.anchoredPosition = new Vector2(20, 50);
        UnityEventTools.AddPersistentListener(back.GetComponent<Button>().onClick,
            manager.OnReturnToMap);

        manager.gameOverScoreText = scoreText;
        manager.gameOverMessageText = msg;

        return overlay.gameObject;
    }

    // =====================================================================
    // Win panel
    // =====================================================================

    private static GameObject BuildWinPanel(RectTransform parent, TowerDefenseManager manager)
    {
        var overlay = AddImage(parent, "WinOverlay", new Color(0, 0, 0, 0.7f));
        Stretch(overlay.rectTransform);
        overlay.raycastTarget = true;

        var card = AddImage(overlay.rectTransform, "Card", PanelBg);
        var crt = card.rectTransform;
        crt.anchorMin = new Vector2(0.5f, 0.5f); crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(900, 600);

        var header = AddImage(crt, "Header", SafeGreen);
        AnchorTopStretch(header.rectTransform, height: 100);
        var hLabel = AddText(header.rectTransform, "HLabel",
            "Tower Defended!", 48, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(hLabel.rectTransform);
        hLabel.raycastTarget = false;

        var stars = AddText(crt, "Stars", "* * *",
            72, ScoreColor, TextAlignmentOptions.Center, FontStyles.Bold);
        var stt = stars.rectTransform;
        stt.anchorMin = new Vector2(0, 1); stt.anchorMax = new Vector2(1, 1);
        stt.pivot = new Vector2(0.5f, 1);
        stt.sizeDelta = new Vector2(0, 100);
        stt.anchoredPosition = new Vector2(0, -130);

        var scoreText = AddText(crt, "ScoreText", "0 / 100",
            44, DarkText, TextAlignmentOptions.Center, FontStyles.Bold);
        var srt = scoreText.rectTransform;
        srt.anchorMin = new Vector2(0, 1); srt.anchorMax = new Vector2(1, 1);
        srt.pivot = new Vector2(0.5f, 1);
        srt.sizeDelta = new Vector2(0, 80);
        srt.anchoredPosition = new Vector2(0, -270);

        var retry = AddButton(crt, "PlayAgainBtn", "Play Again", 28, SafeGreen, Color.white);
        var rrt = retry.GetComponent<RectTransform>();
        rrt.anchorMin = new Vector2(0.5f, 0); rrt.anchorMax = new Vector2(0.5f, 0);
        rrt.pivot = new Vector2(1, 0);
        rrt.sizeDelta = new Vector2(260, 70);
        rrt.anchoredPosition = new Vector2(-20, 50);
        UnityEventTools.AddPersistentListener(retry.GetComponent<Button>().onClick,
            manager.OnRetry);

        var back = AddButton(crt, "BackBtn", "Back to Map", 28, MapGrey, Color.white);
        var bart = back.GetComponent<RectTransform>();
        bart.anchorMin = new Vector2(0.5f, 0); bart.anchorMax = new Vector2(0.5f, 0);
        bart.pivot = new Vector2(0, 0);
        bart.sizeDelta = new Vector2(260, 70);
        bart.anchoredPosition = new Vector2(20, 50);
        UnityEventTools.AddPersistentListener(back.GetComponent<Button>().onClick,
            manager.OnReturnToMap);

        manager.winScoreText = scoreText;
        manager.winStarsText = stars;

        return overlay.gameObject;
    }

    // =====================================================================
    // Helpers - world-space
    // =====================================================================

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
        Color color, Vector3 localPos, float width, float height)
    {
        var canvasGo = new GameObject(name);
        canvasGo.transform.SetParent(parent, false);
        canvasGo.transform.localPosition = localPos;
        canvasGo.transform.localScale = new Vector3(0.008f, 0.008f, 1f);

        var c = canvasGo.AddComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace;
        c.sortingOrder = 4;

        var rt = canvasGo.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(width / 0.008f, height / 0.008f);

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(canvasGo.transform, false);
        var tmp = labelGo.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 28;
        tmp.color = color;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        var lrt = labelGo.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
    }

    // =====================================================================
    // Helpers - UI
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

    private static void AnchorTopStretch(RectTransform rt, float height)
    {
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.sizeDelta = new Vector2(0, height);
        rt.anchoredPosition = Vector2.zero;
    }

    private static Color Hex(string hex)
    {
        return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
    }

    private static Sprite TryGetCircleSprite()
    {
        try { return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"); }
        catch { return null; }
    }

    /// <summary>
    /// Creates (or reuses) a small white sprite saved as a PNG asset so it
    /// persists through scene saves. Used as the source sprite for tower
    /// parts, rockets, cracks, and enemies.
    /// </summary>
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
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        // Always enforce importer settings — see WorldMapBuilder for the
        // rationale (the file might have been imported previously with
        // Unity's default PPU=100, making sprites render tiny).
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            bool changed = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }
            if (Mathf.Abs(importer.spritePixelsPerUnit - 8f) > 0.01f)
            {
                importer.spritePixelsPerUnit = 8;
                changed = true;
            }
            if (importer.filterMode != FilterMode.Point)
            {
                importer.filterMode = FilterMode.Point;
                changed = true;
            }
            if (changed) importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
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

        var bubble = AddImage(rrt, "Bubble", Color.white);
        var brt = bubble.rectTransform;
        brt.anchorMin = new Vector2(0, 0);
        brt.anchorMax = new Vector2(1, 1);
        brt.pivot = new Vector2(0, 0.5f);
        brt.offsetMin = new Vector2(140, 0);
        brt.offsetMax = new Vector2(0, 0);

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