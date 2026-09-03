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
    static readonly Color ArcadeC = Hex("#FFD93D");
    static readonly Color SettingC = Hex("#A29BFE");
    static readonly Color ExitC = Hex("#636E72");
    static readonly Color ResetC = Hex("#E74C3C");
    static readonly Color EmailC = Hex("#FF6B6B");
    static readonly Color SpotC = Hex("#FFD93D");
    static readonly Color TowerC = Hex("#2ECC71");

    // ── Underwater-cyberpunk main menu background (Task 2 redesign) ──
    static readonly Color BgDeep = Hex("#080f1e");
    static readonly Color AccentCyan = Hex("#00E5FF");      // same as StoryC — title / button border / glow
    static readonly Color MenuBtnFill = Hex("#0d2137");     // main button fill + scanline grid lines
    static readonly Color ExitDark = Hex("#3a0f0f");

    [MenuItem("Phisherman/Build Main Menu Scene")]
    public static void Build()
    {
        if (File.Exists(ScenePath)) { AssetDatabase.DeleteAsset(ScenePath); AssetDatabase.Refresh(); }
        if (!Directory.Exists(ScenesDir)) Directory.CreateDirectory(ScenesDir);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

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

        // ── Background — fully code-built underwater-cyberpunk scene, no sprites ──
        var bgImg = MkImg(cRT, "Background", BgDeep);
        Stretch(bgImg.rectTransform); bgImg.raycastTarget = false;

        Sprite circleSpr = GetCircle();
        Sprite triSpr = GetTriangleSprite();

        // Layer 3: subtle digital scanline grid, 40px apart at reference resolution
        BuildScanlineGrid(cRT, new Color(MenuBtnFill.r, MenuBtnFill.g, MenuBtnFill.b, 0.30f));

        // Layer 2: decorative fish silhouettes tucked into the corners
        Color fishCol = new Color(AccentCyan.r, AccentCyan.g, AccentCyan.b, 0.06f);
        var fishLayerGo = new GameObject("FishLayer", typeof(RectTransform));
        fishLayerGo.transform.SetParent(cRT, false);
        var fishRT = fishLayerGo.GetComponent<RectTransform>();
        Stretch(fishRT);
        var fishCG = fishLayerGo.AddComponent<CanvasGroup>(); fishCG.blocksRaycasts = false; fishCG.interactable = false;
        BuildFishSilhouette(fishRT, circleSpr, triSpr, new Vector2(0.07f, 0.87f), 220f, 78f, 8f, false, fishCol);
        BuildFishSilhouette(fishRT, circleSpr, triSpr, new Vector2(0.93f, 0.15f), 260f, 92f, -6f, true, fishCol);
        BuildFishSilhouette(fishRT, circleSpr, triSpr, new Vector2(0.90f, 0.82f), 170f, 60f, 4f, true, fishCol);

        // Layer 1: bubbles drifting upward forever (runtime coroutine-driven component)
        var bubbleLayerGo = new GameObject("BubbleField", typeof(RectTransform));
        bubbleLayerGo.transform.SetParent(cRT, false);
        Stretch(bubbleLayerGo.GetComponent<RectTransform>());
        var bubbleCG = bubbleLayerGo.AddComponent<CanvasGroup>(); bubbleCG.blocksRaycasts = false; bubbleCG.interactable = false;
        var bubbleField = bubbleLayerGo.AddComponent<MenuBubbleField>();
        bubbleField.bubbleSprite = circleSpr;
        bubbleField.tint = Color.white;

        // Manager
        var mgr = new GameObject("MainMenuManager").AddComponent<MainMenuManager>();
        mgr.canvasGroup = rootCG;

        // ── Main panel ────────────────────────────────────────
        var mainPanel = new GameObject("MainPanel", typeof(RectTransform));
        mainPanel.transform.SetParent(cRT, false);
        Stretch(mainPanel.GetComponent<RectTransform>());

        // Title, with a slightly larger low-opacity copy behind it for a soft glow feel
        var titleGlow = MkTxt(mainPanel.transform, "TitleGlow", "PHISHERMAN", 80, new Color(AccentCyan.r, AccentCyan.g, AccentCyan.b, 0.35f), TextAlignmentOptions.Center, FontStyles.Bold);
        titleGlow.textWrappingMode = TextWrappingModes.NoWrap;
        SetAnch(titleGlow.rectTransform, 0.17f, 0.822f, 0.83f, 0.978f);

        var titleTxt = MkTxt(mainPanel.transform, "TitleText", "PHISHERMAN", 72, AccentCyan, TextAlignmentOptions.Center, FontStyles.Bold);
        titleTxt.textWrappingMode = TextWrappingModes.NoWrap;
        SetAnch(titleTxt.rectTransform, 0.20f, 0.83f, 0.80f, 0.97f);

        var tagTxt = MkTxt(mainPanel.transform, "Tagline", "An Educational Anti-Phishing Adventure", 18, new Color(1, 1, 1, 0.70f), TextAlignmentOptions.Center);
        SetAnch(tagTxt.rectTransform, 0.15f, 0.78f, 0.85f, 0.83f);

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

        MkMainBtn(mainPanel.transform, "StoryBtn", "STORY MODE", bx0, y0Min, bx1, y0Max);
        MkMainBtn(mainPanel.transform, "ArcadeBtn", "ARCADE MODE", bx0, y1Min, bx1, y1Max);
        MkMainBtn(mainPanel.transform, "SettingsBtn", "SETTINGS", bx0, y2Min, bx1, y2Max);

        MkExitBtn(mainPanel.transform, "ExitBtn", "EXIT GAME");

        var verTxt = MkTxt(mainPanel.transform, "Ver", "v0.1 - Research Prototype", 13, new Color(0.62f, 0.62f, 0.68f, 1f), TextAlignmentOptions.Left);
        SetAnch(verTxt.rectTransform, 0.01f, 0.008f, 0.35f, 0.045f, ox: 12);

        // ── Arcade panel ──────────────────────────────────────
        var arcadePanel = MkPanel(cRT, "ArcadePanel", 0.07f, 0.06f, 0.93f, 0.94f);
        {
            var pRT = arcadePanel.GetComponent<RectTransform>();
            TopBar(pRT, ArcadeC);
            MkTxt(arcadePanel.transform, "Title", "ARCADE MODE", 48, ArcadeC, TextAlignmentOptions.Center, FontStyles.Bold)
                .rectTransform.With(r => SetAnch(r, 0f, 0.88f, 1f, 1f));
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
            TopBar(pRT, SettingC);
            MkTxt(settingsPanel.transform, "Title", "SETTINGS", 48, SettingC, TextAlignmentOptions.Center, FontStyles.Bold)
                .rectTransform.With(r => SetAnch(r, 0f, 0.88f, 1f, 1f));
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

    // ── Main menu button — fixed 440x70, uniform 2px cyan border, centered label ──
    static void MkMainBtn(Transform parent, string name, string label, float xMin, float yMin, float xMax, float yMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(xMin, yMin); rt.anchorMax = new Vector2(xMax, yMax);
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        // Border layer (full rect, cyan) + fill layer inset 2px = crisp 2px border
        var border = go.AddComponent<Image>(); border.color = AccentCyan;

        var fillGo = new GameObject("Fill", typeof(RectTransform)); fillGo.transform.SetParent(go.transform, false);
        var fillRT = fillGo.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero; fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = new Vector2(2, 2); fillRT.offsetMax = new Vector2(-2, -2);
        var fillImg = fillGo.AddComponent<Image>(); fillImg.color = new Color(MenuBtnFill.r, MenuBtnFill.g, MenuBtnFill.b, 0.92f); fillImg.raycastTarget = false;

        var lbl = MkTxt(go.transform, "Label", label, 22, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(lbl.rectTransform); lbl.raycastTarget = false;

        var btn = go.AddComponent<Button>(); btn.targetGraphic = border;
        var cb = btn.colors;
        cb.normalColor = AccentCyan;
        cb.highlightedColor = new Color(Mathf.Min(1f, AccentCyan.r * 1.3f), Mathf.Min(1f, AccentCyan.g * 1.3f), Mathf.Min(1f, AccentCyan.b * 1.3f), 1f);
        cb.pressedColor = new Color(AccentCyan.r * 0.65f, AccentCyan.g * 0.65f, AccentCyan.b * 0.65f, 1f);
        cb.colorMultiplier = 1f; btn.colors = cb;

        MkHover(go, 1.03f, 0.975f);
    }

    // ── Exit button — fixed 160x44, pinned bottom-right ──
    static void MkExitBtn(Transform parent, string name, string label)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.sizeDelta = new Vector2(160, 44);
        rt.anchoredPosition = new Vector2(-24, 24);

        var img = go.AddComponent<Image>(); img.color = ExitDark;
        var lbl = MkTxt(go.transform, "Label", label, 16, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(lbl.rectTransform); lbl.raycastTarget = false;

        var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        var cb = btn.colors;
        cb.normalColor = ExitDark;
        cb.highlightedColor = new Color(Mathf.Min(1f, ExitDark.r * 1.8f), Mathf.Min(1f, ExitDark.g * 1.8f), Mathf.Min(1f, ExitDark.b * 1.8f), 1f);
        cb.pressedColor = new Color(ExitDark.r * 0.6f, ExitDark.g * 0.6f, ExitDark.b * 0.6f, 1f);
        cb.colorMultiplier = 1f; btn.colors = cb;
    }

    // ── Scanline grid (Layer 3) — thin horizontal lines, 40px apart at 1920x1080 ──
    static void BuildScanlineGrid(RectTransform canvasRT, Color lineCol)
    {
        var gridGo = new GameObject("ScanlineGrid", typeof(RectTransform));
        gridGo.transform.SetParent(canvasRT, false);
        var grt = gridGo.GetComponent<RectTransform>();
        Stretch(grt);
        var cg = gridGo.AddComponent<CanvasGroup>(); cg.blocksRaycasts = false; cg.interactable = false;

        const float refH = 1080f, spacing = 40f;
        int lines = Mathf.FloorToInt(refH / spacing);
        for (int i = 0; i <= lines; i++)
        {
            float frac = (i * spacing) / refH;
            var line = MkImg(grt, "Line_" + i, lineCol);
            line.raycastTarget = false;
            var lrt = line.rectTransform;
            lrt.anchorMin = new Vector2(0f, frac);
            lrt.anchorMax = new Vector2(1f, frac);
            lrt.pivot = new Vector2(0.5f, 0.5f);
            lrt.sizeDelta = new Vector2(0f, 1.5f);
        }
    }

    // ── Fish silhouette (Layer 2) — ellipse body + triangle tail, purely decorative ──
    static void BuildFishSilhouette(RectTransform canvasRT, Sprite bodySpr, Sprite tailSpr,
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

    static void TopBar(RectTransform p, Color col)
    {
        MkImg(p, "TopBar", col).With(i => { i.raycastTarget = false; i.rectTransform.anchorMin = new Vector2(0, 1); i.rectTransform.anchorMax = new Vector2(1, 1); i.rectTransform.pivot = new Vector2(0.5f, 1); i.rectTransform.sizeDelta = new Vector2(0, 5); });
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