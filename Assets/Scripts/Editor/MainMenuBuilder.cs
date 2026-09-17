using System.Collections;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// ============================================================
//  MainMenuBuilder  (Editor-only)
//  Run via: Phisherman > Build Main Menu Scene
//
//  All button wiring happens at runtime in MainMenuManager.Start().
//  Buttons are found via Transform.Find() on their parent panel
//  with includeInactive:true so inactive panels are still searchable.
// ============================================================
public static class MainMenuBuilder
{
    const string ScenesDir = "Assets/Scenes";
    const string ScenePath = "Assets/Scenes/MainMenu.unity";

    static readonly Color BgDark = new Color(0.03f, 0.06f, 0.12f, 1f);
    static readonly Color PanelBg = new Color(0.04f, 0.06f, 0.13f, 0.98f);
    static readonly Color CardBg = new Color(0.05f, 0.08f, 0.17f, 0.97f);
    static readonly Color StoryC = Hex("#00E5FF");
    static readonly Color ArcadeC = Hex("#CC44FF");
    static readonly Color SettingC = Hex("#FF8C00");
    static readonly Color ExitC = Hex("#636E72");
    static readonly Color ResetC = Hex("#E74C3C");
    static readonly Color EmailC = Hex("#FF6B6B");
    static readonly Color SpotC = Hex("#FFD93D");
    static readonly Color TowerC = Hex("#2ECC71");

    // ── Underwater-cyberpunk main menu background (Task 3 redesign) ──
    static readonly Color BgDeep = Hex("#060d1a");
    static readonly Color AccentCyan = Hex("#00E5FF");      // same as StoryC — title / button border / glow
    static readonly Color MenuBtnFill = Hex("#0d2137");     // main button fill + scanline grid lines
    static readonly Color ExitDark = Hex("#1a0808");
    static readonly Color ExitBorder = Hex("#cc2222");
    static readonly Color PurpleAccent = Hex("#8b00ff");
    static readonly Color BlueAccent = Hex("#0066ff");
    static readonly Color GoldAccent = Hex("#ffd700");

    [MenuItem("Phisherman/Build Main Menu Scene")]
    public static void Build()
    {
        var permanentColliders = PermanentColliderGuard.Capture(ScenePath);
        if (File.Exists(ScenePath)) { AssetDatabase.DeleteAsset(ScenePath); AssetDatabase.Refresh(); }
        if (!Directory.Exists(ScenesDir)) Directory.CreateDirectory(ScenesDir);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        PermanentColliderGuard.Restore(permanentColliders);

        // Camera
        var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>(); camGo.AddComponent<AudioListener>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = BgDark;
        cam.orthographic = true; camGo.transform.position = new Vector3(0, 0, -10);

        // EventSystem
        var esGo = new GameObject("EventSystem");
        esGo.AddComponent<EventSystem>(); esGo.AddComponent<StandaloneInputModule>();

        // Canvas
        var cGo = new GameObject("Canvas");
        var cv = cGo.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 200;
        var scaler = cGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
        cGo.AddComponent<GraphicRaycaster>();
        var cRT = cGo.GetComponent<RectTransform>();
        var rootCG = cGo.AddComponent<CanvasGroup>();

        // ── Background — main_menu_bg sprite (title is baked into the art) ──
        // FIX: FindSprite()'s AssetDatabase.FindAssets(name + " t:Sprite") search
        // matches against Unity's search index, which for this file was still
        // keyed on its original import name ("ChatGPT Image Sep 16..."), not the
        // "main_menu_bg" filename — so the name search silently found nothing.
        // Load directly by the known asset path instead, which doesn't depend on
        // the search index at all; fall back to the name search only if the file
        // ever moves.
        Sprite mainMenuBgSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Backgrounds/main_menu_bg.png");
        if (mainMenuBgSpr == null) mainMenuBgSpr = FindSprite("main_menu_bg");
        var bgImg = MkImg(cRT, "Background", Color.white);
        bgImg.sprite = mainMenuBgSpr;
        Stretch(bgImg.rectTransform); bgImg.raycastTarget = false;
        if (mainMenuBgSpr == null) Debug.LogWarning("[MainMenuBuilder] 'main_menu_bg' sprite not found at Assets/Sprites/Backgrounds/main_menu_bg.png — Background left blank.");

        Sprite circleSpr = GetCircle();
        Sprite triSpr = GetTriangleSprite();

        // Layer 2: animated diagonal gradient sweep, loops left-to-right every 8s
        var sweepGo = new GameObject("GradientSweep", typeof(RectTransform));
        sweepGo.transform.SetParent(cRT, false);
        var sweepImg = sweepGo.AddComponent<Image>(); sweepImg.color = new Color(MenuBtnFill.r * 1.3f, MenuBtnFill.g * 1.3f, MenuBtnFill.b * 1.3f, 0.55f); sweepImg.raycastTarget = false;
        var sweepRT = sweepGo.GetComponent<RectTransform>();
        sweepRT.anchorMin = sweepRT.anchorMax = new Vector2(0.5f, 0.5f);
        sweepRT.sizeDelta = new Vector2(500f, 2400f);
        sweepRT.localEulerAngles = new Vector3(0, 0, -20f);
        sweepGo.AddComponent<MenuGradientSweep>();

        // Layer 3: subtle digital scanline grid — horizontal + vertical, 60px apart
        BuildScanlineGrid(cRT, new Color(MenuBtnFill.r, MenuBtnFill.g, MenuBtnFill.b, 0.40f));

        // Layer 4: glowing orbs, very low opacity
        BuildGlowOrbs(cRT, circleSpr);

        // Layer 2b: decorative fish silhouettes tucked into the corners — 2 of the
        // 4 get a gentle floating drift.
        Color fishTeal = new Color(AccentCyan.r, AccentCyan.g, AccentCyan.b, 0.08f);
        Color fishPurple = new Color(PurpleAccent.r, PurpleAccent.g, PurpleAccent.b, 0.06f);
        var fishLayerGo = new GameObject("FishLayer", typeof(RectTransform));
        fishLayerGo.transform.SetParent(cRT, false);
        var fishRT = fishLayerGo.GetComponent<RectTransform>();
        Stretch(fishRT);
        var fishCG = fishLayerGo.AddComponent<CanvasGroup>(); fishCG.blocksRaycasts = false; fishCG.interactable = false;
        BuildFishSilhouette(fishRT, circleSpr, triSpr, new Vector2(0.06f, 0.88f), 160f, 56f, 8f, false, fishTeal);
        var fishB = BuildFishSilhouette(fishRT, circleSpr, triSpr, new Vector2(0.93f, 0.14f), 200f, 70f, -6f, true, fishPurple);
        BuildFishSilhouette(fishRT, circleSpr, triSpr, new Vector2(0.90f, 0.85f), 140f, 50f, 4f, true, fishTeal);
        var fishD = BuildFishSilhouette(fishRT, circleSpr, triSpr, new Vector2(0.07f, 0.12f), 180f, 63f, -4f, false, fishPurple);
        var driftB = fishB.gameObject.AddComponent<MenuFloatDrift>(); driftB.amplitude = 0.3f; driftB.period = 3f;
        var driftD = fishD.gameObject.AddComponent<MenuFloatDrift>(); driftD.amplitude = 0.3f; driftD.period = 3f;

        // Layer 1: bubbles drifting upward forever (runtime coroutine-driven component)
        var bubbleLayerGo = new GameObject("BubbleField", typeof(RectTransform));
        bubbleLayerGo.transform.SetParent(cRT, false);
        Stretch(bubbleLayerGo.GetComponent<RectTransform>());
        var bubbleCG = bubbleLayerGo.AddComponent<CanvasGroup>(); bubbleCG.blocksRaycasts = false; bubbleCG.interactable = false;
        var bubbleField = bubbleLayerGo.AddComponent<MenuBubbleField>();
        bubbleField.bubbleSprite = circleSpr;
        bubbleField.tint = Color.white;
        bubbleField.bubbleCount = 12;
        bubbleField.sizeRange = new Vector2(8f, 20f);

        // Manager
        var mgr = new GameObject("MainMenuManager").AddComponent<MainMenuManager>();
        mgr.canvasGroup = rootCG;

        // ── Main panel ────────────────────────────────────────
        var mainPanel = new GameObject("MainPanel", typeof(RectTransform));
        mainPanel.transform.SetParent(cRT, false);
        Stretch(mainPanel.GetComponent<RectTransform>());

        // Fishing hook — built before the title so it renders behind it
        BuildFishingHook(mainPanel.transform);

        // Title text removed — main_menu_bg sprite has the title baked into the art.

        // Divider — gradient transparent -> teal -> transparent
        var divider = MkImg(mainPanel.transform, "TitleDivider", AccentCyan);
        divider.sprite = GetHorizontalFadeSprite(); divider.raycastTarget = false;
        var divRT = divider.rectTransform; divRT.anchorMin = new Vector2(0.20f, 0.825f); divRT.anchorMax = new Vector2(0.80f, 0.825f); divRT.pivot = new Vector2(0.5f, 0.5f); divRT.sizeDelta = new Vector2(0, 3);

        // Tagline text removed (was "An Educational Anti-Phishing Adventure" under the title).

        // Three main buttons — fixed 440x70 px, 18px gap, centered as a block
        const float RefW = 1920f, RefH = 1080f, BtnW = 440f, BtnH = 70f, BtnGap = 18f;
        float bx0 = 0.5f - (BtnW / RefW) * 0.5f;
        float bx1 = 0.5f + (BtnW / RefW) * 0.5f;
        float blockH = BtnH * 3f + BtnGap * 2f;
        float blockTop = RefH * 0.46f + blockH * 0.5f;
        float y0Max = blockTop / RefH;
        float y0Min = (blockTop - BtnH) / RefH;
        float y1Max = (blockTop - BtnH - BtnGap) / RefH;
        float y1Min = (blockTop - 2f * BtnH - BtnGap) / RefH;
        float y2Max = (blockTop - 2f * BtnH - 2f * BtnGap) / RefH;
        float y2Min = (blockTop - 3f * BtnH - 2f * BtnGap) / RefH;

        MkMainBtn(mainPanel.transform, "StoryBtn", "STORY MODE", StoryC, bx0, y0Min, bx1, y0Max);
        MkMainBtn(mainPanel.transform, "ArcadeBtn", "ARCADE MODE", ArcadeC, bx0, y1Min, bx1, y1Max);
        MkMainBtn(mainPanel.transform, "SettingsBtn", "SETTINGS", SettingC, bx0, y2Min, bx1, y2Max);

        MkExitBtn(mainPanel.transform, "ExitBtn", "EXIT");

        var verTxt = MkTxt(mainPanel.transform, "Ver", "v0.1 - Research Prototype", 12, Hex("#445566"), TextAlignmentOptions.Left);
        SetAnch(verTxt.rectTransform, 0.01f, 0.008f, 0.35f, 0.045f, ox: 12);

        // ── Arcade panel ──────────────────────────────────────
        var arcadePanel = MkPanel(cRT, "ArcadePanel", 0.07f, 0.06f, 0.93f, 0.94f);
        {
            var pRT = arcadePanel.GetComponent<RectTransform>();
            BuildPanelHeader(pRT, "ARCADE MODE");
            MkTxt(arcadePanel.transform, "Sub", "Choose a minigame — no story progress required.", 21, new Color(1, 1, 1, 0.46f), TextAlignmentOptions.Center)
                .rectTransform.With(r => SetAnch(r, 0.05f, 0.82f, 0.95f, 0.89f));
            MkCard(pRT, "EmailCard", "Email Swiper", "Sort real vs phishing emails.\nSwipe SCAM or SAFE.", EmailC, "fish", 0.02f, 0.13f, 0.33f, 0.80f);
            MkCard(pRT, "SpotCard", "Spot the Difference", "Find every red flag the\nfake email hides.", SpotC, "magnifying_glass", 0.36f, 0.13f, 0.64f, 0.80f);
            MkCard(pRT, "TowerCard", "Tower Defense", "Block waves of phishing\nattacks before they land.", TowerC, "tower", 0.67f, 0.13f, 0.98f, 0.80f);
            MkBtn(arcadePanel.transform, "ArcadeBackBtn", "Back", ExitC, 0.36f, 0.01f, 0.64f, 0.10f, 27);
        }
        arcadePanel.SetActive(false);

        // ── Settings panel ────────────────────────────────────
        var settingsPanel = MkPanel(cRT, "SettingsPanel", 0.15f, 0.06f, 0.85f, 0.94f);
        {
            var pRT = settingsPanel.GetComponent<RectTransform>();
            BuildPanelHeader(pRT, "SETTINGS");
            MkVolRow(pRT, "MasterVolumeRow", "Master Volume", SettingC, 0.73f, 0.82f);
            MkVolRow(pRT, "MusicVolumeRow", "Music Volume", SettingC, 0.60f, 0.69f);
            MkVolRow(pRT, "SFXVolumeRow", "SFX Volume", SettingC, 0.47f, 0.56f);

            var div = MkImg(pRT, "Div", new Color(1, 1, 1, 0.07f));
            div.rectTransform.anchorMin = new Vector2(0.04f, 0.44f); div.rectTransform.anchorMax = new Vector2(0.96f, 0.44f);
            div.rectTransform.sizeDelta = new Vector2(0, 1); div.raycastTarget = false;

            MkTxt(settingsPanel.transform, "DLbl", "DANGER ZONE", 18, new Color(ResetC.r, ResetC.g, ResetC.b, 0.50f), TextAlignmentOptions.Left, FontStyles.Bold)
                .rectTransform.With(r => SetAnch(r, 0.06f, 0.37f, 0.50f, 0.44f));

            MkBtn(settingsPanel.transform, "ResetBtn", "Reset All Progress", ResetC, 0.08f, 0.24f, 0.92f, 0.36f, 29);

            MkTxt(settingsPanel.transform, "ResetWarn",
                "Permanently erases XP, levels, fish stickers, and all world / house progress.",
                15, new Color(1f, 0.45f, 0.45f, 0.60f), TextAlignmentOptions.Center)
                .With(t => { t.textWrappingMode = TextWrappingModes.Normal; SetAnch(t.rectTransform, 0.05f, 0.15f, 0.95f, 0.24f); });

            MkBtn(settingsPanel.transform, "SettingsBackBtn", "Back", ExitC, 0.36f, 0.01f, 0.64f, 0.10f, 27);
            settingsPanel.AddComponent<SettingsManager>();
        }
        settingsPanel.SetActive(false);

        // Give manager the panel references
        mgr.mainPanel = mainPanel;
        mgr.arcadePanel = arcadePanel;
        mgr.settingsPanel = settingsPanel;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuild(ScenePath);
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log("[MainMenuBuilder] Done -> " + ScenePath);
    }

    // ── Card ──────────────────────────────────────────────────
    static void MkCard(RectTransform parent, string name, string title, string desc,
        Color col, string iconName, float xMin, float yMin, float xMax, float yMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(xMin, yMin); rt.anchorMax = new Vector2(xMax, yMax);
        rt.offsetMin = new Vector2(6, 0); rt.offsetMax = new Vector2(-6, 0);

        var bg = go.AddComponent<Image>(); bg.color = CardBg;

        var tb = MkImg(rt, "TB", col);
        tb.rectTransform.anchorMin = new Vector2(0, 1); tb.rectTransform.anchorMax = new Vector2(1, 1);
        tb.rectTransform.pivot = new Vector2(0.5f, 1); tb.rectTransform.sizeDelta = new Vector2(0, 4);
        tb.raycastTarget = false;

        Sprite spr = FindSprite(iconName);
        var icon = MkImg(rt, "Icon", spr != null ? Color.white : new Color(col.r, col.g, col.b, 0.12f));
        if (spr != null) { icon.sprite = spr; icon.preserveAspect = true; }
        icon.rectTransform.anchorMin = new Vector2(0.12f, 0.46f); icon.rectTransform.anchorMax = new Vector2(0.88f, 0.88f);
        icon.rectTransform.offsetMin = icon.rectTransform.offsetMax = Vector2.zero; icon.raycastTarget = false;

        MkTxt(rt, "CardTitle", title, 26, col, TextAlignmentOptions.Center, FontStyles.Bold)
            .rectTransform.With(r => SetAnch(r, 0f, 0.28f, 1f, 0.46f, ox: 8, ox2: -8));
        MkTxt(rt, "CardDesc", desc, 17, new Color(1, 1, 1, 0.58f), TextAlignmentOptions.Center)
            .With(t => { t.textWrappingMode = TextWrappingModes.Normal; SetAnch(t.rectTransform, 0f, 0.04f, 1f, 0.28f, ox: 10, ox2: -10); });

        var btn = go.AddComponent<Button>(); btn.targetGraphic = bg;
        var cb = btn.colors;
        cb.normalColor = CardBg;
        cb.highlightedColor = new Color(col.r * 0.22f, col.g * 0.22f, col.b * 0.22f, 0.97f);
        cb.pressedColor = new Color(col.r * 0.36f, col.g * 0.36f, col.b * 0.36f, 1f);
        btn.colors = cb;
        MkHover(go, 1.030f, 0.975f);
    }

    // ── Button ────────────────────────────────────────────────
    static void MkBtn(Transform parent, string name, string label,
        Color col, float xMin, float yMin, float xMax, float yMax, int fontSize = 33)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(xMin, yMin); rt.anchorMax = new Vector2(xMax, yMax);
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var bg = go.AddComponent<Image>(); bg.color = new Color(0.04f, 0.06f, 0.13f, 0.95f);

        var accGo = new GameObject("Acc", typeof(RectTransform)); accGo.transform.SetParent(go.transform, false);
        var accImg = accGo.AddComponent<Image>(); accImg.color = col; accImg.raycastTarget = false;
        accGo.GetComponent<RectTransform>().With(r => { r.anchorMin = Vector2.zero; r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 0.5f); r.sizeDelta = new Vector2(5, 0); });

        var brdGo = new GameObject("Brd", typeof(RectTransform)); brdGo.transform.SetParent(go.transform, false);
        var brdImg = brdGo.AddComponent<Image>(); brdImg.color = new Color(col.r, col.g, col.b, 0.28f); brdImg.raycastTarget = false;
        brdGo.GetComponent<RectTransform>().With(r => { r.anchorMin = Vector2.zero; r.anchorMax = new Vector2(1, 0); r.pivot = new Vector2(0.5f, 0); r.sizeDelta = new Vector2(0, 2); });

        MkTxt(go.transform, "Label", label, fontSize, col, TextAlignmentOptions.MidlineLeft, FontStyles.Bold)
            .rectTransform.With(r => { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(20, 0); r.offsetMax = new Vector2(-12, 0); });

        var btn = go.AddComponent<Button>(); btn.targetGraphic = bg;
        var cb = btn.colors;
        cb.normalColor = new Color(0.04f, 0.06f, 0.13f, 0.95f);
        cb.highlightedColor = new Color(col.r * 0.18f, col.g * 0.18f, col.b * 0.18f, 0.97f);
        cb.pressedColor = new Color(col.r * 0.30f, col.g * 0.30f, col.b * 0.30f, 1.00f);
        cb.colorMultiplier = 1f; btn.colors = cb;
        MkHover(go, 1.025f, 0.975f);
    }

    // ── Main menu button — fixed 440x70, three layers (glow / fill+accent bar / border) + chevron ──
    static void MkMainBtn(Transform parent, string name, string label, Color accentCol, float xMin, float yMin, float xMax, float yMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(xMin, yMin); rt.anchorMax = new Vector2(xMax, yMax);
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        // Layer 1: outer glow, +6px each side, behind everything
        var glowGo = new GameObject("Glow", typeof(RectTransform)); glowGo.transform.SetParent(go.transform, false);
        var glowImg = glowGo.AddComponent<Image>(); glowImg.color = new Color(accentCol.r, accentCol.g, accentCol.b, 0.15f); glowImg.raycastTarget = false;
        var glowRT = glowGo.GetComponent<RectTransform>(); glowRT.anchorMin = Vector2.zero; glowRT.anchorMax = Vector2.one; glowRT.offsetMin = new Vector2(-6, -6); glowRT.offsetMax = new Vector2(6, 6);

        // Layer 3 (border): full-rect accent colour behind an inset fill — a crisp 1.5px border.
        // This IS the button's own Image/Button target.
        var border = go.AddComponent<Image>(); border.color = accentCol;

        // Layer 2 (background): dark fill inset 1.5px
        var fillGo = new GameObject("Fill", typeof(RectTransform)); fillGo.transform.SetParent(go.transform, false);
        var fillImg = fillGo.AddComponent<Image>(); fillImg.color = MenuBtnFill; fillImg.raycastTarget = false;
        var fillRT = fillGo.GetComponent<RectTransform>(); fillRT.anchorMin = Vector2.zero; fillRT.anchorMax = Vector2.one; fillRT.offsetMin = new Vector2(1.5f, 1.5f); fillRT.offsetMax = new Vector2(-1.5f, -1.5f);

        // Left accent bar, 6px wide, full height
        var accGo = new GameObject("AccentBar", typeof(RectTransform)); accGo.transform.SetParent(go.transform, false);
        var accImg = accGo.AddComponent<Image>(); accImg.color = accentCol; accImg.raycastTarget = false;
        var accRT = accGo.GetComponent<RectTransform>(); accRT.anchorMin = Vector2.zero; accRT.anchorMax = new Vector2(0, 1); accRT.pivot = new Vector2(0, 0.5f); accRT.sizeDelta = new Vector2(6, 0);

        // Label — left aligned, 20px left padding to clear the accent bar
        var lbl = MkTxt(go.transform, "Label", label, 24, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        var lblRT = lbl.rectTransform; lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = Vector2.one; lblRT.offsetMin = new Vector2(20, 0); lblRT.offsetMax = new Vector2(-40, 0);
        lbl.raycastTarget = false;

        BuildChevron(go.transform, accentCol);

        var btn = go.AddComponent<Button>(); btn.targetGraphic = border;
        var cb = btn.colors;
        cb.normalColor = accentCol;
        cb.highlightedColor = new Color(Mathf.Min(1f, accentCol.r * 1.3f), Mathf.Min(1f, accentCol.g * 1.3f), Mathf.Min(1f, accentCol.b * 1.3f), 1f);
        cb.pressedColor = new Color(accentCol.r * 0.65f, accentCol.g * 0.65f, accentCol.b * 0.65f, 1f);
        cb.colorMultiplier = 1f; btn.colors = cb;

        MkHover(go, 1.03f, 0.975f);
    }

    // ── Chevron ">" — two thin rectangles forming an arrow, right side of a button ──
    static void BuildChevron(Transform parent, Color col)
    {
        var root = new GameObject("Chevron", typeof(RectTransform)); root.transform.SetParent(parent, false);
        var rrt = root.GetComponent<RectTransform>();
        rrt.anchorMin = rrt.anchorMax = new Vector2(1f, 0.5f); rrt.pivot = new Vector2(1f, 0.5f);
        rrt.sizeDelta = new Vector2(20, 20); rrt.anchoredPosition = new Vector2(-20, 0);

        var top = MkImg(rrt, "Top", col); top.raycastTarget = false;
        var trt = top.rectTransform; trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
        trt.sizeDelta = new Vector2(3, 13); trt.anchoredPosition = new Vector2(-3, 3.2f); trt.localEulerAngles = new Vector3(0, 0, 45f);

        var bot = MkImg(rrt, "Bottom", col); bot.raycastTarget = false;
        var brt2 = bot.rectTransform; brt2.anchorMin = brt2.anchorMax = new Vector2(0.5f, 0.5f);
        brt2.sizeDelta = new Vector2(3, 13); brt2.anchoredPosition = new Vector2(-3, -3.2f); brt2.localEulerAngles = new Vector3(0, 0, -45f);
    }

    // ── Exit button — fixed 140x40, pinned bottom-right, dark bg + distinct red border ──
    static void MkExitBtn(Transform parent, string name, string label)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.sizeDelta = new Vector2(140, 40);
        rt.anchoredPosition = new Vector2(-24, 24);

        var border = go.AddComponent<Image>(); border.color = ExitBorder;

        var fillGo = new GameObject("Fill", typeof(RectTransform)); fillGo.transform.SetParent(go.transform, false);
        var fillImg = fillGo.AddComponent<Image>(); fillImg.color = ExitDark; fillImg.raycastTarget = false;
        var fillRT = fillGo.GetComponent<RectTransform>(); fillRT.anchorMin = Vector2.zero; fillRT.anchorMax = Vector2.one; fillRT.offsetMin = new Vector2(1.5f, 1.5f); fillRT.offsetMax = new Vector2(-1.5f, -1.5f);

        var lbl = MkTxt(go.transform, "Label", label, 16, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(lbl.rectTransform); lbl.raycastTarget = false;

        var btn = go.AddComponent<Button>(); btn.targetGraphic = border;
        var cb = btn.colors;
        cb.normalColor = ExitBorder;
        cb.highlightedColor = new Color(Mathf.Min(1f, ExitBorder.r * 1.3f), Mathf.Min(1f, ExitBorder.g * 1.3f), Mathf.Min(1f, ExitBorder.b * 1.3f), 1f);
        cb.pressedColor = new Color(ExitBorder.r * 0.65f, ExitBorder.g * 0.65f, ExitBorder.b * 0.65f, 1f);
        cb.colorMultiplier = 1f; btn.colors = cb;
    }

    // ── Shared panel header — dark bar + glow-style title, "same style as main title" but smaller/teal ──
    static void BuildPanelHeader(RectTransform pRT, string title)
    {
        var header = MkImg(pRT, "HeaderBar", BgDeep);
        var hrt = header.rectTransform; hrt.anchorMin = new Vector2(0, 1); hrt.anchorMax = new Vector2(1, 1); hrt.pivot = new Vector2(0.5f, 1); hrt.sizeDelta = new Vector2(0, 90);
        header.raycastTarget = false;

        var glowT = MkTxt(header.rectTransform, "TitleGlow", title, 34, new Color(AccentCyan.r, AccentCyan.g, AccentCyan.b, 0.30f), TextAlignmentOptions.Center, FontStyles.Bold);
        var glrt = glowT.rectTransform; glrt.anchorMin = Vector2.zero; glrt.anchorMax = Vector2.one; glrt.offsetMin = new Vector2(1, -1); glrt.offsetMax = new Vector2(1, -1);

        var frontT = MkTxt(header.rectTransform, "Title", title, 32, AccentCyan, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(frontT.rectTransform);
    }

    // ── Scanline grid (Layer 3) — horizontal + vertical lines, 60px apart at 1920x1080 ──
    static void BuildScanlineGrid(RectTransform canvasRT, Color lineCol)
    {
        var gridGo = new GameObject("ScanlineGrid", typeof(RectTransform));
        gridGo.transform.SetParent(canvasRT, false);
        var grt = gridGo.GetComponent<RectTransform>();
        Stretch(grt);
        var cg = gridGo.AddComponent<CanvasGroup>(); cg.blocksRaycasts = false; cg.interactable = false;

        const float refW = 1920f, refH = 1080f, spacing = 60f;
        int hLines = Mathf.FloorToInt(refH / spacing);
        for (int i = 0; i <= hLines; i++)
        {
            float frac = (i * spacing) / refH;
            var line = MkImg(grt, "HLine_" + i, lineCol);
            line.raycastTarget = false;
            var lrt = line.rectTransform;
            lrt.anchorMin = new Vector2(0f, frac);
            lrt.anchorMax = new Vector2(1f, frac);
            lrt.pivot = new Vector2(0.5f, 0.5f);
            lrt.sizeDelta = new Vector2(0f, 1.5f);
        }
        int vLines = Mathf.FloorToInt(refW / spacing);
        for (int i = 0; i <= vLines; i++)
        {
            float frac = (i * spacing) / refW;
            var line = MkImg(grt, "VLine_" + i, lineCol);
            line.raycastTarget = false;
            var lrt = line.rectTransform;
            lrt.anchorMin = new Vector2(frac, 0f);
            lrt.anchorMax = new Vector2(frac, 1f);
            lrt.pivot = new Vector2(0.5f, 0.5f);
            lrt.sizeDelta = new Vector2(1.5f, 0f);
        }
    }

    // ── Glowing orbs (Layer 4) — large low-opacity circles at fixed decorative positions ──
    static void BuildGlowOrbs(RectTransform canvasRT, Sprite circleSpr)
    {
        var orbsGo = new GameObject("GlowOrbs", typeof(RectTransform));
        orbsGo.transform.SetParent(canvasRT, false);
        Stretch(orbsGo.GetComponent<RectTransform>());
        var cg = orbsGo.AddComponent<CanvasGroup>(); cg.blocksRaycasts = false; cg.interactable = false;

        (Vector2 pos, float size, Color col, float alpha)[] orbs =
        {
            (new Vector2(0.12f, 0.75f), 260f, AccentCyan, 0.08f),
            (new Vector2(0.85f, 0.65f), 340f, PurpleAccent, 0.06f),
            (new Vector2(0.30f, 0.20f), 220f, BlueAccent, 0.10f),
            (new Vector2(0.70f, 0.90f), 300f, AccentCyan, 0.07f),
            (new Vector2(0.92f, 0.30f), 380f, PurpleAccent, 0.09f),
        };
        for (int i = 0; i < orbs.Length; i++)
        {
            var img = MkImg(orbsGo.transform, "Orb" + i, new Color(orbs[i].col.r, orbs[i].col.g, orbs[i].col.b, orbs[i].alpha));
            img.sprite = circleSpr; img.raycastTarget = false;
            var rt = img.rectTransform; rt.anchorMin = rt.anchorMax = orbs[i].pos; rt.sizeDelta = new Vector2(orbs[i].size, orbs[i].size);
        }
    }

    // ── Fishing hook — vertical line from the top + a J-curve, sits behind the title ──
    static void BuildFishingHook(Transform parent)
    {
        Color hookCol = new Color(GoldAccent.r, GoldAccent.g, GoldAccent.b, 0.9f);

        var line = MkImg(parent, "HookLine", hookCol); line.raycastTarget = false;
        var lrt = line.rectTransform; lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 1f); lrt.pivot = new Vector2(0.5f, 1f);
        lrt.sizeDelta = new Vector2(3, 80); lrt.anchoredPosition = Vector2.zero;

        var curve1 = MkImg(parent, "HookCurve1", hookCol); curve1.raycastTarget = false;
        var c1rt = curve1.rectTransform; c1rt.anchorMin = c1rt.anchorMax = new Vector2(0.5f, 1f); c1rt.pivot = new Vector2(0.5f, 0.5f);
        c1rt.sizeDelta = new Vector2(28, 5); c1rt.anchoredPosition = new Vector2(6, -84); c1rt.localEulerAngles = new Vector3(0, 0, -55f);

        var curve2 = MkImg(parent, "HookCurve2", hookCol); curve2.raycastTarget = false;
        var c2rt = curve2.rectTransform; c2rt.anchorMin = c2rt.anchorMax = new Vector2(0.5f, 1f); c2rt.pivot = new Vector2(0.5f, 0.5f);
        c2rt.sizeDelta = new Vector2(24, 5); c2rt.anchoredPosition = new Vector2(14, -98); c2rt.localEulerAngles = new Vector3(0, 0, -110f);
    }

    // ── Fish silhouette (Layer 2) — ellipse body + triangle tail, purely decorative ──
    // Returns the group's Transform so callers can optionally attach MenuFloatDrift.
    static Transform BuildFishSilhouette(RectTransform canvasRT, Sprite bodySpr, Sprite tailSpr,
        Vector2 anchorPos, float bodyW, float bodyH, float rotation, bool flipX, Color col)
    {
        var group = new GameObject("FishSilhouette", typeof(RectTransform));
        group.transform.SetParent(canvasRT, false);
        var grt = group.GetComponent<RectTransform>();
        grt.anchorMin = grt.anchorMax = anchorPos;
        grt.pivot = new Vector2(0.5f, 0.5f);
        grt.sizeDelta = Vector2.zero;
        grt.localEulerAngles = new Vector3(0, 0, rotation);
        grt.localScale = new Vector3(flipX ? -1f : 1f, 1f, 1f);

        var body = MkImg(grt, "Body", col);
        body.sprite = bodySpr; body.preserveAspect = false; body.raycastTarget = false;
        var brt = body.rectTransform;
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
        brt.sizeDelta = new Vector2(bodyW, bodyH);
        brt.anchoredPosition = Vector2.zero;

        float tailW = bodyH * 1.1f, tailH = bodyH * 1.6f;
        var tail = MkImg(grt, "Tail", col);
        tail.sprite = tailSpr; tail.raycastTarget = false;
        var trt = tail.rectTransform;
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
        trt.sizeDelta = new Vector2(tailW, tailH);
        trt.anchoredPosition = new Vector2(-(bodyW * 0.5f + tailW * 0.35f), 0f);

        return group.transform;
    }

    // ── Volume row ────────────────────────────────────────────
    static void MkVolRow(RectTransform parent, string rowName, string label, Color col, float yMin, float yMax)
    {
        var row = new GameObject(rowName, typeof(RectTransform)); row.transform.SetParent(parent, false);
        row.GetComponent<RectTransform>().With(r => { r.anchorMin = new Vector2(0.04f, yMin); r.anchorMax = new Vector2(0.96f, yMax); r.offsetMin = new Vector2(0, 3); r.offsetMax = new Vector2(0, -3); });
        MkTxt(row.transform, "Lbl", label, 25, Color.white, TextAlignmentOptions.MidlineLeft)
            .rectTransform.With(r => { r.anchorMin = Vector2.zero; r.anchorMax = new Vector2(0.32f, 1); r.offsetMin = new Vector2(8, 0); r.offsetMax = Vector2.zero; });
        var track = MkImg(row.transform, "Track", new Color(1, 1, 1, 0.09f));
        track.rectTransform.With(r => { r.anchorMin = new Vector2(0.34f, 0.20f); r.anchorMax = new Vector2(0.93f, 0.80f); r.offsetMin = r.offsetMax = Vector2.zero; });
        MkImg(track.rectTransform, "Fill", col).With(i => { i.raycastTarget = false; i.rectTransform.anchorMin = Vector2.zero; i.rectTransform.anchorMax = new Vector2(0.80f, 1); i.rectTransform.offsetMin = new Vector2(2, 2); i.rectTransform.offsetMax = new Vector2(-2, -2); });
    }

    // ── Helpers ───────────────────────────────────────────────
    static GameObject MkPanel(RectTransform cRT, string name, float xMin, float yMin, float xMax, float yMax)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(cRT, false);
        go.GetComponent<RectTransform>().With(r => { r.anchorMin = new Vector2(xMin, yMin); r.anchorMax = new Vector2(xMax, yMax); r.offsetMin = r.offsetMax = Vector2.zero; });
        go.AddComponent<Image>().color = PanelBg; return go;
    }

    static void MkHover(GameObject go, float h, float p, float spd = 10f)
    { var hv = go.AddComponent<MenuHoverButton>(); hv.normalScale = Vector3.one; hv.hoverScale = new Vector3(h, h, 1f); hv.pressScale = new Vector3(p, p, 1f); hv.animSpeed = spd; }

    static void SetAnch(RectTransform rt, float xMin, float yMin, float xMax, float yMax, float ox = 0, float oy = 0, float ox2 = 0, float oy2 = 0)
    { rt.anchorMin = new Vector2(xMin, yMin); rt.anchorMax = new Vector2(xMax, yMax); rt.offsetMin = new Vector2(ox, oy); rt.offsetMax = new Vector2(ox2, oy2); }

    static Sprite FindSprite(string n) { foreach (var g in AssetDatabase.FindAssets(n + " t:Sprite")) { var p = AssetDatabase.GUIDToAssetPath(g); if (Path.GetFileNameWithoutExtension(p).ToLower() == n.ToLower()) { var s = AssetDatabase.LoadAssetAtPath<Sprite>(p); if (s != null) return s; } } return null; }
    static Image MkImg(Transform p, string n, Color c) { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); var i = go.AddComponent<Image>(); i.color = c; return i; }
    static Image MkImg(RectTransform p, string n, Color c) => MkImg((Transform)p, n, c);
    static TMP_Text MkTxt(Transform p, string n, string txt, int sz, Color col, TextAlignmentOptions al, FontStyles fs = FontStyles.Normal)
    { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); var t = go.AddComponent<TextMeshProUGUI>(); t.text = txt; t.fontSize = sz; t.color = col; t.alignment = al; t.fontStyle = fs; t.raycastTarget = false; return t; }
    static TMP_Text MkTxt(RectTransform p, string n, string txt, int sz, Color col, TextAlignmentOptions al, FontStyles fs = FontStyles.Normal) => MkTxt((Transform)p, n, txt, sz, col, al, fs);
    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
    static Color Hex(string h) => ColorUtility.TryParseHtmlString(h, out var c) ? c : Color.magenta;
    static void AddToBuild(string path) { var s = EditorBuildSettings.scenes.ToList(); if (!s.Any(x => x.path == path)) { s.Add(new EditorBuildSettingsScene(path, true)); EditorBuildSettings.scenes = s.ToArray(); } }

    static Sprite GetCircle() { try { return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"); } catch { return null; } }

    // Procedurally builds a small filled right-pointing triangle sprite (apex on the
    // right, base on the left) so the fish-silhouette tail needs no external art.
    static Sprite _triangleSprite;
    static Sprite GetTriangleSprite()
    {
        if (_triangleSprite != null) return _triangleSprite;
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            float fy = y / (float)(size - 1);
            for (int x = 0; x < size; x++)
            {
                float fx = x / (float)(size - 1);
                float halfHeightAtX = (1f - fx) * 0.5f; // widest at the left base, tapers to a point on the right
                bool inside = Mathf.Abs(fy - 0.5f) <= halfHeightAtX;
                px[y * size + x] = inside ? Color.white : new Color(1f, 1f, 1f, 0f);
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        _triangleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return _triangleSprite;
    }

    // Procedurally builds a 1px-tall horizontal alpha ramp (0 -> 1 -> 0) so the
    // title divider can fade transparent -> teal -> transparent without art.
    static Sprite _fadeSprite;
    static Sprite GetHorizontalFadeSprite()
    {
        if (_fadeSprite != null) return _fadeSprite;
        const int size = 64;
        var tex = new Texture2D(size, 1, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color[size];
        for (int x = 0; x < size; x++)
        {
            float fx = x / (float)(size - 1);
            float a = Mathf.Clamp01(1f - Mathf.Abs(fx - 0.5f) * 2f);
            px[x] = new Color(1f, 1f, 1f, a);
        }
        tex.SetPixels(px);
        tex.Apply();
        _fadeSprite = Sprite.Create(tex, new Rect(0, 0, size, 1), new Vector2(0.5f, 0.5f));
        return _fadeSprite;
    }
}

// ============================================================
//  With() extension — lets us configure a component inline
//  without a separate variable. Only used inside the builder.
// ============================================================
public static class MMBExtensions
{
    public static T With<T>(this T obj, System.Action<T> fn) { fn(obj); return obj; }
}

// ============================================================
//  MainMenuManager lives in Assets/Scripts/WorldScripts/MainMenuManager.cs
//  SettingsManager lives in Assets/Scripts/WorldScripts/SettingsManager.cs
//  DoorTrigger / SceneLoader live in Assets/Scripts/WorldScripts/GameUtilities.cs
//
//  None of these may be redefined here: this file is under an Editor/
//  folder and compiles into the Editor-only assembly, which already
//  references the runtime assembly. A duplicate class of the same
//  name in both is a CS0433 "type exists in both assemblies" compile
//  error, breaking every custom Editor menu in the project — this
//  exact bug is what caused the Story/Arcade/Settings buttons to stop
//  working, so keep these classes split into their own runtime files.
// ============================================================