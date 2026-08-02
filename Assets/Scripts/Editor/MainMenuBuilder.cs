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
/// Builds the MainMenu scene.
/// Run via: Phisherman > Build Main Menu Scene
///
/// Layout:
///   - main_menu_bg fills the screen
///   - Title "PHISHERMAN" top-centre
///   - Three buttons centre-right: Story Mode / Arcade Mode / Settings
///   - Arcade panel (hidden by default): 3 minigame cards + back
///   - Settings panel (hidden by default): placeholder sliders + back
///
/// Button style matches the CTA border style from the world map:
///   dark fill + coloured border + bold white text.
/// </summary>
public static class MainMenuBuilder
{
    private const string ScenesDir = "Assets/Scenes";
    private const string ScenePath = "Assets/Scenes/MainMenu.unity";

    // Palette
    static readonly Color StoryCol = Hex("#FF9F1C");   // orange  — story mode
    static readonly Color ArcadeCol = Hex("#4ECDC4");   // teal    — arcade mode
    static readonly Color SettingsCol = Hex("#A29BFE");   // purple  — settings
    static readonly Color BackCol = Hex("#636E72");   // grey    — back buttons
    static readonly Color PanelBg = new Color(0.05f, 0.07f, 0.14f, 0.96f);
    static readonly Color DarkFill = new Color(0.06f, 0.08f, 0.14f, 0.92f);

    // Minigame card colours
    static readonly Color EmailCol = Hex("#FF6B6B");
    static readonly Color SpotCol = Hex("#FFD93D");
    static readonly Color TowerCol = Hex("#2ECC71");

    [MenuItem("Phisherman/Build Main Menu Scene")]
    public static void Build()
    {
        if (!Directory.Exists(ScenesDir)) Directory.CreateDirectory(ScenesDir);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── Camera ───────────────────────────────────────────────────
        var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>(); camGo.AddComponent<AudioListener>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Hex("#0D1B2A");
        cam.orthographic = true;
        camGo.transform.position = new Vector3(0, 0, -10);

        // ── EventSystem ───────────────────────────────────────────────
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>(); es.AddComponent<StandaloneInputModule>();

        // ── Canvas ────────────────────────────────────────────────────
        var canvasGo = new GameObject("Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        var canvasRT = canvasGo.GetComponent<RectTransform>();

        // Canvas group for fade in/out
        var rootCG = canvasGo.AddComponent<CanvasGroup>();

        // ── Background ────────────────────────────────────────────────
        Sprite bgSpr = FindSprite("main_menu_bg");
        var bgImg = Img(canvasRT, "Background", bgSpr != null ? Color.white : Hex("#0D1B2A"));
        if (bgSpr != null) { bgImg.sprite = bgSpr; bgImg.preserveAspect = false; }
        Stretch(bgImg.rectTransform);
        bgImg.raycastTarget = false;
        if (bgSpr == null) Debug.LogWarning("[MainMenuBuilder] 'main_menu_bg' not found — import it as Sprite (2D and UI).");

        // No vignette — background art already has atmosphere built in

        // ── Manager GO ────────────────────────────────────────────────
        var mgrGo = new GameObject("MainMenuManager");
        var mgr = mgrGo.AddComponent<MainMenuManager>();
        mgr.canvasGroup = rootCG;

        // ── Main panel ────────────────────────────────────────────────
        // The background image already has Story Mode / Arcade Mode /
        // Settings buttons baked in. We place invisible transparent
        // click-catcher buttons exactly over them.
        // Positions eyeballed from the screenshot — tweak anchorMin/Max
        // in the Scene view if they don't line up perfectly.
        var mainPanel = new GameObject("MainPanel", typeof(RectTransform));
        mainPanel.transform.SetParent(canvasRT, false);
        Stretch(mainPanel.GetComponent<RectTransform>());

        // Transparent hit-boxes over the baked-in button art
        // Story Mode  — centre band, upper third of screen
        var storyBtn = MakeInvisibleButton(mainPanel.GetComponent<RectTransform>(),
            "StoryBtn", new Vector2(0.34f, 0.595f), new Vector2(0.72f, 0.685f));
        // Arcade Mode — centre band, middle
        var arcadeBtn = MakeInvisibleButton(mainPanel.GetComponent<RectTransform>(),
            "ArcadeBtn", new Vector2(0.34f, 0.455f), new Vector2(0.72f, 0.545f));
        // Settings    — centre band, lower
        var settingsBtn = MakeInvisibleButton(mainPanel.GetComponent<RectTransform>(),
            "SettingsBtn", new Vector2(0.34f, 0.315f), new Vector2(0.72f, 0.405f));
        // Exit Game   — bottom right corner
        var exitBtn = MakeInvisibleButton(mainPanel.GetComponent<RectTransform>(),
            "ExitBtn", new Vector2(0.75f, 0.045f), new Vector2(0.95f, 0.115f));

        // ── ARCADE PANEL ──────────────────────────────────────────────
        var arcadePanel = BuildArcadePanel(canvasRT, mgr);

        // ── SETTINGS PANEL ────────────────────────────────────────────
        var settingsPanel = BuildSettingsPanel(canvasRT, mgr);

        // ── Wire manager ──────────────────────────────────────────────
        mgr.mainPanel = mainPanel;
        mgr.arcadePanel = arcadePanel;
        mgr.settingsPanel = settingsPanel;

        UnityEventTools.AddPersistentListener(storyBtn.GetComponent<Button>().onClick, mgr.OnStoryModeClicked);
        UnityEventTools.AddPersistentListener(arcadeBtn.GetComponent<Button>().onClick, mgr.OnArcadeModeClicked);
        UnityEventTools.AddPersistentListener(settingsBtn.GetComponent<Button>().onClick, mgr.OnSettingsClicked);
        UnityEventTools.AddPersistentListener(exitBtn.GetComponent<Button>().onClick, mgr.OnExitClicked);

        // Initial visibility
        arcadePanel.SetActive(false);
        settingsPanel.SetActive(false);

        // ── Save ──────────────────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuild(ScenePath);
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log($"[MainMenuBuilder] Built → {ScenePath}");
    }

    // =========================================================================
    // Arcade panel — 3 minigame cards
    // =========================================================================
    static GameObject BuildArcadePanel(RectTransform canvasRT, MainMenuManager mgr)
    {
        var panel = new GameObject("ArcadePanel", typeof(RectTransform));
        panel.transform.SetParent(canvasRT, false);
        var pRT = panel.GetComponent<RectTransform>();
        pRT.anchorMin = new Vector2(0.1f, 0.08f);
        pRT.anchorMax = new Vector2(0.9f, 0.92f);
        pRT.offsetMin = pRT.offsetMax = Vector2.zero;

        // Background
        var bg = panel.AddComponent<Image>(); bg.color = PanelBg;

        // Top border accent
        var bord = Img(pRT, "TopBorder", ArcadeCol);
        var brt = bord.rectTransform;
        brt.anchorMin = new Vector2(0, 1); brt.anchorMax = new Vector2(1, 1);
        brt.pivot = new Vector2(0.5f, 1); brt.sizeDelta = new Vector2(0, 5);
        bord.raycastTarget = false;

        // Title
        var t = Txt(pRT, "Title", "ARCADE MODE — Choose a Minigame", 40,
            ArcadeCol, TextAlignmentOptions.Center, FontStyles.Bold);
        var trt = t.rectTransform;
        trt.anchorMin = new Vector2(0, 0.82f); trt.anchorMax = new Vector2(1, 1f);
        trt.offsetMin = new Vector2(20, 0); trt.offsetMax = new Vector2(-20, 0);

        // Three minigame cards side by side
        BuildMinigameCard(pRT, "EmailSwiperCard",
            "Email Swiper",
            "Sort phishing emails.\nSwipe SCAM or SAFE!",
            EmailCol,
            "fish",
            new Vector2(0.02f, 0.12f), new Vector2(0.32f, 0.80f),
            mgr.OnPlayEmailSwiper, mgr);

        BuildMinigameCard(pRT, "SpotDiffCard",
            "Spot the Difference",
            "Find the red flags\nhidden in real emails.",
            SpotCol,
            "magnifying_glass",
            new Vector2(0.35f, 0.12f), new Vector2(0.65f, 0.80f),
            mgr.OnPlaySpotDiff, mgr);

        BuildMinigameCard(pRT, "TowerDefenseCard",
            "Tower Defense",
            "Defend your inbox from\nwaves of phishing attacks!",
            TowerCol,
            "tower",
            new Vector2(0.68f, 0.12f), new Vector2(0.98f, 0.80f),
            mgr.OnPlayTowerDefense, mgr);

        // Back button
        var back = MakeMenuButton(pRT, "BackBtn", "← Back", BackCol,
            new Vector2(0.35f, 0.01f), new Vector2(0.65f, 0.10f));
        UnityEventTools.AddPersistentListener(back.GetComponent<Button>().onClick, mgr.OnArcadeBack);

        return panel;
    }

    static void BuildMinigameCard(RectTransform parent, string name,
        string title, string desc, Color col, string iconSpriteName,
        Vector2 anchorMin, Vector2 anchorMax,
        UnityEngine.Events.UnityAction action, MainMenuManager mgr)
    {
        var card = new GameObject(name, typeof(RectTransform));
        card.transform.SetParent(parent, false);
        var cRT = card.GetComponent<RectTransform>();
        cRT.anchorMin = anchorMin; cRT.anchorMax = anchorMax;
        cRT.offsetMin = new Vector2(8, 0); cRT.offsetMax = new Vector2(-8, 0);

        // Card dark fill
        var fill = card.AddComponent<Image>(); fill.color = DarkFill;

        // Coloured border
        var border = Img(cRT, "Border", col);
        var brt = border.rectTransform;
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
        brt.offsetMin = new Vector2(-3, -3); brt.offsetMax = new Vector2(3, 3);
        border.raycastTarget = false;

        // Overlay fill on top of border so border is just an outline
        var fill2 = Img(cRT, "Fill", DarkFill); Stretch(fill2.rectTransform); fill2.raycastTarget = false;

        // Icon (top half)
        Sprite iconSpr = FindSprite(iconSpriteName);
        if (iconSpr != null)
        {
            var icon = Img(cRT, "Icon", Color.white);
            icon.sprite = iconSpr; icon.preserveAspect = true; icon.raycastTarget = false;
            var irt = icon.rectTransform;
            irt.anchorMin = new Vector2(0.2f, 0.48f); irt.anchorMax = new Vector2(0.8f, 0.90f);
            irt.offsetMin = irt.offsetMax = Vector2.zero;
        }
        else
        {
            // Coloured circle fallback
            var dot = Img(cRT, "Icon", col); dot.sprite = GetCircle(); dot.preserveAspect = true; dot.raycastTarget = false;
            var irt = dot.rectTransform;
            irt.anchorMin = new Vector2(0.3f, 0.52f); irt.anchorMax = new Vector2(0.7f, 0.88f);
            irt.offsetMin = irt.offsetMax = Vector2.zero;
        }

        // Title
        var titleTxt = Txt(cRT, "Title", title, 28, col, TextAlignmentOptions.Center, FontStyles.Bold);
        var ttrt = titleTxt.rectTransform;
        ttrt.anchorMin = new Vector2(0, 0.30f); ttrt.anchorMax = new Vector2(1, 0.50f);
        ttrt.offsetMin = new Vector2(8, 0); ttrt.offsetMax = new Vector2(-8, 0);

        // Desc
        var descTxt = Txt(cRT, "Desc", desc, 19, new Color(1, 1, 1, 0.70f), TextAlignmentOptions.Center);
        descTxt.textWrappingMode = TextWrappingModes.Normal;
        var dtrt = descTxt.rectTransform;
        dtrt.anchorMin = new Vector2(0, 0.10f); dtrt.anchorMax = new Vector2(1, 0.32f);
        dtrt.offsetMin = new Vector2(10, 0); dtrt.offsetMax = new Vector2(-10, 0);

        // Invisible button over the whole card
        var btn = card.AddComponent<Button>(); btn.targetGraphic = fill;
        var cb = btn.colors;
        cb.normalColor = DarkFill;
        cb.highlightedColor = new Color(col.r * 0.25f, col.g * 0.25f, col.b * 0.25f, 0.95f);
        cb.pressedColor = new Color(col.r * 0.40f, col.g * 0.40f, col.b * 0.40f, 1f);
        btn.colors = cb;
        UnityEventTools.AddPersistentListener(btn.onClick, action);
    }

    // =========================================================================
    // Settings panel — placeholder
    // =========================================================================
    static GameObject BuildSettingsPanel(RectTransform canvasRT, MainMenuManager mgr)
    {
        var panel = new GameObject("SettingsPanel", typeof(RectTransform));
        panel.transform.SetParent(canvasRT, false);
        var pRT = panel.GetComponent<RectTransform>();
        pRT.anchorMin = new Vector2(0.15f, 0.08f);
        pRT.anchorMax = new Vector2(0.85f, 0.92f);
        pRT.offsetMin = pRT.offsetMax = Vector2.zero;

        var bg = panel.AddComponent<Image>(); bg.color = PanelBg;

        // Top border
        var bord = Img(pRT, "TopBorder", SettingsCol);
        var brt = bord.rectTransform;
        brt.anchorMin = new Vector2(0, 1); brt.anchorMax = new Vector2(1, 1);
        brt.pivot = new Vector2(0.5f, 1); brt.sizeDelta = new Vector2(0, 5);
        bord.raycastTarget = false;

        // Title
        var t = Txt(pRT, "Title", "SETTINGS", 46, SettingsCol, TextAlignmentOptions.Center, FontStyles.Bold);
        var trt = t.rectTransform; trt.anchorMin = new Vector2(0, 0.84f); trt.anchorMax = new Vector2(1, 1f); trt.offsetMin = trt.offsetMax = Vector2.zero;

        // Placeholder rows
        BuildSettingRow(pRT, "Master Volume", SettingsCol, new Vector2(0, 0.68f), new Vector2(1, 0.80f));
        BuildSettingRow(pRT, "Music Volume", SettingsCol, new Vector2(0, 0.54f), new Vector2(1, 0.66f));
        BuildSettingRow(pRT, "SFX Volume", SettingsCol, new Vector2(0, 0.40f), new Vector2(1, 0.52f));

        // Accessibility label
        var accLbl = Txt(pRT, "AccLabel", "Accessibility", 30, new Color(1, 1, 1, 0.55f), TextAlignmentOptions.Left, FontStyles.Bold);
        var alrt = accLbl.rectTransform; alrt.anchorMin = new Vector2(0.05f, 0.28f); alrt.anchorMax = new Vector2(0.5f, 0.37f); alrt.offsetMin = alrt.offsetMax = Vector2.zero;

        // Coming soon note
        var note = Txt(pRT, "Note", "More settings coming soon…", 22, new Color(1, 1, 1, 0.35f), TextAlignmentOptions.Center, FontStyles.Italic);
        var nrt = note.rectTransform; nrt.anchorMin = new Vector2(0, 0.10f); nrt.anchorMax = new Vector2(1, 0.26f); nrt.offsetMin = nrt.offsetMax = Vector2.zero;

        // Back
        var back = MakeMenuButton(pRT, "BackBtn", "← Back", BackCol, new Vector2(0.35f, 0.01f), new Vector2(0.65f, 0.10f));
        UnityEventTools.AddPersistentListener(back.GetComponent<Button>().onClick, mgr.OnSettingsBack);

        // Wire SettingsManager (placeholder — fields null for now)
        var sm = panel.AddComponent<SettingsManager>();

        return panel;
    }

    static void BuildSettingRow(RectTransform parent, string label, Color col, Vector2 anchorMin, Vector2 anchorMax)
    {
        var row = new GameObject(label.Replace(" ", "_") + "_Row", typeof(RectTransform));
        row.transform.SetParent(parent, false);
        var rRT = row.GetComponent<RectTransform>();
        rRT.anchorMin = anchorMin; rRT.anchorMax = anchorMax;
        rRT.offsetMin = new Vector2(40, 4); rRT.offsetMax = new Vector2(-40, -4);

        var lbl = Txt(rRT, "Label", label, 26, Color.white, TextAlignmentOptions.MidlineLeft);
        var lrt = lbl.rectTransform; lrt.anchorMin = new Vector2(0, 0); lrt.anchorMax = new Vector2(0.35f, 1); lrt.offsetMin = lrt.offsetMax = Vector2.zero;

        // Slider track (visual only — functional slider wired by SettingsManager at runtime)
        var trackBg = Img(rRT, "TrackBg", new Color(1, 1, 1, 0.10f));
        var tRT = trackBg.rectTransform; tRT.anchorMin = new Vector2(0.37f, 0.25f); tRT.anchorMax = new Vector2(0.95f, 0.75f); tRT.offsetMin = tRT.offsetMax = Vector2.zero;

        var fill = Img(tRT, "Fill", col); fill.raycastTarget = false;
        var fRT = fill.rectTransform; fRT.anchorMin = Vector2.zero; fRT.anchorMax = new Vector2(0.8f, 1); fRT.offsetMin = fRT.offsetMax = Vector2.zero;
    }

    // =========================================================================
    // UI helpers
    // =========================================================================

    /// Invisible transparent button — sits over baked-in art, no visual of its own.
    static GameObject MakeInvisibleButton(RectTransform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>(); img.color = new Color(0, 0, 0, 0);
        var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        var cb = btn.colors;
        cb.normalColor = new Color(0, 0, 0, 0);
        cb.highlightedColor = new Color(1, 1, 1, 0.08f);
        cb.pressedColor = new Color(1, 1, 1, 0.15f);
        btn.colors = cb;
        return go;
    }

    /// Creates a styled CTA-style button (dark fill + coloured border + bold text).
    static GameObject MakeMenuButton(RectTransform parent, string name, string label,
        Color col, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = new Vector2(0, 4); rt.offsetMax = new Vector2(0, -4);

        // Border (coloured, slightly larger)
        var borderGo = new GameObject("Border", typeof(RectTransform));
        borderGo.transform.SetParent(go.transform, false);
        var borderImg = borderGo.AddComponent<Image>(); borderImg.color = col; borderImg.raycastTarget = false;
        var brt = borderGo.GetComponent<RectTransform>();
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
        brt.offsetMin = new Vector2(-3, -3); brt.offsetMax = new Vector2(3, 3);

        // Dark fill
        var fillGo = new GameObject("Fill", typeof(RectTransform));
        fillGo.transform.SetParent(go.transform, false);
        var fillImg = fillGo.AddComponent<Image>(); fillImg.color = DarkFill; fillImg.raycastTarget = false;
        var frt = fillGo.GetComponent<RectTransform>();
        frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = frt.offsetMax = Vector2.zero;

        // Label
        var txt = Txt(go.transform, "Label", label, 36, col,
            TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(txt.rectTransform);

        // Button component on root
        var img2 = go.AddComponent<Image>(); img2.color = new Color(0, 0, 0, 0);
        var btn = go.AddComponent<Button>(); btn.targetGraphic = img2;
        var cb = btn.colors;
        cb.normalColor = new Color(0, 0, 0, 0);
        cb.highlightedColor = new Color(col.r, col.g, col.b, 0.18f);
        cb.pressedColor = new Color(col.r, col.g, col.b, 0.35f);
        btn.colors = cb;

        return go;
    }

    // ── Sprite / asset helpers ────────────────────────────────────────
    static Sprite FindSprite(string name)
    {
        foreach (var g in AssetDatabase.FindAssets(name + " t:Sprite"))
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            if (Path.GetFileNameWithoutExtension(p).ToLower() == name.ToLower())
            { var s = AssetDatabase.LoadAssetAtPath<Sprite>(p); if (s != null) return s; }
        }
        return null;
    }

    static Sprite GetCircle()
    { try { return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"); } catch { return null; } }

    static Image Img(Transform p, string n, Color c)
    {
        var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false);
        var img = go.AddComponent<Image>(); img.color = c; return img;
    }

    static TMP_Text Txt(Transform p, string n, string text, int size, Color col,
        TextAlignmentOptions align, FontStyles style = FontStyles.Normal)
    {
        var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.color = col;
        t.alignment = align; t.fontStyle = style; t.raycastTarget = false; return t;
    }

    static void Stretch(RectTransform r)
    { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }

    static Color Hex(string h) =>
        ColorUtility.TryParseHtmlString(h, out var c) ? c : Color.magenta;

    static void AddToBuild(string path)
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(s => s.path == path))
        { scenes.Add(new EditorBuildSettingsScene(path, true)); EditorBuildSettings.scenes = scenes.ToArray(); }
    }
}