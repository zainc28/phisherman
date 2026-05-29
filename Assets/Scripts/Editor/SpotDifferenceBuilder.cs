using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Builds the Spot the Difference scene.
///
/// Layout (bottom to top):
///   0%  – 27%  Shark simulation panel
///   28% – 89%  Email panels (real vs phishing)
///   89% – 100% HUD (title, timer, phisherman, score, counter)
///
/// Auto-detects sprites: phisherman, magnifying_glass, fish_normal /
///   td_hanging_fish, fish_puffer.
/// </summary>
public static class SpotDifferenceBuilder
{
    private const string ScenesDir = "Assets/Scenes";
    private const string ScenePath = "Assets/Scenes/SpotDifference.unity";

    // ── Palette ──
    private static readonly Color BgColor = Hex("#F6F8FC");
    private static readonly Color HudColor = Hex("#2E3A59");
    private static readonly Color HudText = Color.white;
    private static readonly Color OverlayColor = new Color(0f, 0f, 0f, 0.65f);
    private static readonly Color PanelBg = Color.white;
    private static readonly Color LabelRealBg = Hex("#E8F5E9");
    private static readonly Color LabelScamBg = Hex("#FCE8E6");
    private static readonly Color LabelRealText = Hex("#0D652D");
    private static readonly Color LabelScamText = Hex("#C5221F");
    private static readonly Color DividerColor = Hex("#E0E0E0");
    private static readonly Color BodyText = Hex("#202124");
    private static readonly Color MutedText = Hex("#5F6368");
    private static readonly Color AmazonOrange = Hex("#E37400");
    private static readonly Color CtaReal = Hex("#0061D5");
    private static readonly Color CtaScam = Hex("#C5221F");
    // Simulation
    private static readonly Color OceanDeep = Hex("#071824");
    private static readonly Color OceanMid = Hex("#0A2840");
    private static readonly Color BarSteel = Hex("#8A9BB0");
    private static readonly Color BarBolt = Hex("#4A5A6A");
    private static readonly Color SafeZoneBg = Hex("#0D3050");
    private static readonly Color EggColor = Hex("#F5E8B0");
    private static readonly Color CrackYellow = Hex("#FDCB6E");
    private static readonly Color SharkColor = new Color(0.28f, 0.35f, 0.42f);
    private static readonly Color SafeFishColor = new Color(0.30f, 0.85f, 0.65f);

    // =================================================================
    // Build
    // =================================================================

    [MenuItem("Phisherman/Build Spot the Difference Scene")]
    public static void Build()
    {
        if (!Directory.Exists(ScenesDir)) Directory.CreateDirectory(ScenesDir);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── Sprites ──
        Sprite spCircle = TryGetCircleSprite();
        Sprite spPhisherman = FindSprite("phisherman");
        Sprite spMagGlass = FindSprite("magnifying_glass");
        Sprite spFish = FindSprite("fish_normal") ?? FindSprite("td_hanging_fish");
        LogFound("phisherman", spPhisherman);
        LogFound("magnifying_glass", spMagGlass);
        LogFound("fish", spFish);

        // ── Camera ──
        var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>(); camGo.AddComponent<AudioListener>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = BgColor;
        cam.orthographic = true;

        // ── EventSystem ──
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>(); es.AddComponent<StandaloneInputModule>();

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
        var manager = mgrGo.AddComponent<SpotDifferenceManager>();
        manager.mainCanvas = canvas;
        manager.mainCanvasRT = canvasRT;
        manager.circleSprite = spCircle;

        // ── Background ──
        var bg = AddImage(canvasRT, "Background", BgColor);
        Stretch(bg.rectTransform); bg.raycastTarget = false;

        // ── HUD (top) ──
        BuildHud(canvasRT, manager, spPhisherman, spMagGlass, spCircle);

        // ── Feedback popup (just below HUD) ──
        var feedback = AddText(canvasRT, "Feedback", string.Empty,
            46, Color.green, TextAlignmentOptions.Center, FontStyles.Bold);
        var fbRT = feedback.rectTransform;
        fbRT.anchorMin = new Vector2(0.5f, 1f); fbRT.anchorMax = new Vector2(0.5f, 1f);
        fbRT.pivot = new Vector2(0.5f, 1f);
        fbRT.sizeDelta = new Vector2(1100, 64);
        fbRT.anchoredPosition = new Vector2(0, -124);
        feedback.raycastTarget = false;

        // ── Email panels  (y = 0.285 → 0.895) ──
        BuildEmailPanel(canvasRT, manager, isScam: false,
            new Vector2(0.025f, 0.285f), new Vector2(0.487f, 0.895f));
        BuildEmailPanel(canvasRT, manager, isScam: true,
            new Vector2(0.513f, 0.285f), new Vector2(0.975f, 0.895f));

        // ── Simulation (y = 0 → 0.270) ──
        BuildSimulation(canvasRT, manager, spFish, spCircle);

        // ── Effect layer (top-most, no masks, for water drips) ──
        var effectLayerGo = new GameObject("EffectLayer", typeof(RectTransform));
        effectLayerGo.transform.SetParent(canvasRT, false);
        Stretch(effectLayerGo.GetComponent<RectTransform>());
        effectLayerGo.transform.SetAsLastSibling();
        var eImg = effectLayerGo.AddComponent<Image>();
        eImg.color = new Color(0, 0, 0, 0); eImg.raycastTarget = false;
        manager.effectLayer = effectLayerGo.GetComponent<RectTransform>();

        // ── Magnifying glass cursor (also top-most) ──
        BuildMagnifyingGlass(canvasRT, manager, spMagGlass, spCircle);

        // ── Result panel ──
        var resultPanel = BuildResultPanel(canvasRT, manager);

        // ── Commentator ──
        manager.commentator = BuildCommentator(canvasRT);

        // ── Wire remaining manager refs ──
        manager.feedbackText = feedback;
        manager.resultPanel = resultPanel;

        // ── Save ──
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings(ScenePath);
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log($"[SpotDifferenceBuilder] Built scene → {ScenePath}");
    }

    // =================================================================
    // HUD
    // =================================================================

    static void BuildHud(RectTransform parent, SpotDifferenceManager manager,
        Sprite spPhisherman, Sprite spMagGlass, Sprite spCircle)
    {
        var hud = AddImage(parent, "HUD", HudColor);
        AnchorTopStretch(hud.rectTransform, height: 110);
        hud.raycastTarget = false;

        // ── Phisherman sprite (left) ──
        float phX = 30f;
        if (spPhisherman != null)
        {
            // Container holding both states
            var phHolder = new GameObject("PhishermanHolder", typeof(RectTransform));
            phHolder.transform.SetParent(hud.rectTransform, false);
            var phHRT = phHolder.GetComponent<RectTransform>();
            phHRT.anchorMin = new Vector2(0, 0); phHRT.anchorMax = new Vector2(0, 1);
            phHRT.pivot = new Vector2(0, 0.5f);
            phHRT.sizeDelta = new Vector2(80, 0); phHRT.anchoredPosition = new Vector2(16, 0);

            // No-magGlass version
            var noMag = new GameObject("NoMagGlass", typeof(RectTransform));
            noMag.transform.SetParent(phHolder.transform, false);
            Stretch(noMag.GetComponent<RectTransform>());
            var nmImg = noMag.AddComponent<Image>();
            nmImg.sprite = spPhisherman; nmImg.preserveAspect = true;
            nmImg.raycastTarget = false;

            // With-magGlass version (phisherman + small magnifying glass icon)
            var withMag = new GameObject("WithMagGlass", typeof(RectTransform));
            withMag.transform.SetParent(phHolder.transform, false);
            Stretch(withMag.GetComponent<RectTransform>());
            var wmImg = withMag.AddComponent<Image>();
            wmImg.sprite = spPhisherman; wmImg.preserveAspect = true;
            wmImg.raycastTarget = false;

            if (spMagGlass != null)
            {
                var mgIcon = new GameObject("MagGlassIcon", typeof(RectTransform));
                mgIcon.transform.SetParent(withMag.transform, false);
                var mgRT = mgIcon.GetComponent<RectTransform>();
                mgRT.anchorMin = mgRT.anchorMax = new Vector2(1, 0);
                mgRT.pivot = new Vector2(1, 0);
                mgRT.sizeDelta = new Vector2(32, 32);
                mgRT.anchoredPosition = new Vector2(-4, 4);
                var mgImg = mgIcon.AddComponent<Image>();
                mgImg.sprite = spMagGlass; mgImg.preserveAspect = true;
                mgImg.raycastTarget = false;
            }

            withMag.SetActive(false); // default: no magnifying glass

            manager.phishermanNoMagGlass = noMag;
            manager.phishermanWithMagGlass = withMag;
            phX = 110f;
        }

        // ── Title ──
        var title = AddText(hud.rectTransform, "Title",
            "Spot the Phishing Red Flags",
            38, HudText, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        AnchorRect(title.rectTransform,
            new Vector2(0, 0), new Vector2(0.38f, 1),
            new Vector2(phX, 0), Vector2.zero);

        // ── Timer (centre) ──
        var timerTxt = AddText(hud.rectTransform, "Timer", "1:00",
            44, HudText, TextAlignmentOptions.Center, FontStyles.Bold);
        AnchorRect(timerTxt.rectTransform,
            new Vector2(0.38f, 0), new Vector2(0.62f, 1),
            Vector2.zero, Vector2.zero);

        // ── Counter ──
        var counter = AddText(hud.rectTransform, "Counter", "Found: 0 / 5",
            32, HudText, TextAlignmentOptions.Midline, FontStyles.Bold);
        AnchorRect(counter.rectTransform,
            new Vector2(0.62f, 0), new Vector2(0.80f, 1),
            Vector2.zero, Vector2.zero);

        // ── Score ──
        var score = AddText(hud.rectTransform, "Score", "Score: 0",
            32, HudText, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
        AnchorRect(score.rectTransform,
            new Vector2(0.80f, 0), new Vector2(1, 1),
            Vector2.zero, new Vector2(-28, 0));

        manager.timerText = timerTxt;
        manager.counterText = counter;
        manager.scoreText = score;
    }

    // =================================================================
    // Magnifying glass cursor
    // =================================================================

    static void BuildMagnifyingGlass(RectTransform parent, SpotDifferenceManager manager,
        Sprite spMagGlass, Sprite spCircle)
    {
        var go = new GameObject("MagnifyingGlassCursor", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(72, 72);
        rt.anchoredPosition = Vector2.zero;

        var img = go.AddComponent<Image>();
        img.sprite = spMagGlass != null ? spMagGlass : spCircle;
        img.preserveAspect = true;
        img.color = new Color(1, 1, 1, 0.88f);
        img.raycastTarget = false;

        go.SetActive(false); // hidden until mouse in email area
        manager.magnifyingGlassRT = rt;
    }

    // =================================================================
    // Simulation panel (bottom 27%)
    // =================================================================

    static void BuildSimulation(RectTransform parent, SpotDifferenceManager manager,
        Sprite spFish, Sprite spCircle)
    {
        // Root
        var root = AddImage(parent, "SimulationRoot", OceanDeep);
        var rrt = root.rectTransform;
        rrt.anchorMin = new Vector2(0, 0); rrt.anchorMax = new Vector2(1, 0.270f);
        rrt.offsetMin = rrt.offsetMax = Vector2.zero;
        root.raycastTarget = false;

        // Ocean gradient layers
        var midLayer = AddImage(rrt, "OceanMid", OceanMid);
        var mlrt = midLayer.rectTransform;
        mlrt.anchorMin = new Vector2(0, 0.25f); mlrt.anchorMax = new Vector2(1, 1);
        mlrt.offsetMin = mlrt.offsetMax = Vector2.zero;
        midLayer.raycastTarget = false;

        // Ocean floor
        var floor = AddImage(rrt, "OceanFloor", Hex("#050F18"));
        var flrt = floor.rectTransform;
        flrt.anchorMin = Vector2.zero; flrt.anchorMax = new Vector2(1, 0.14f);
        flrt.offsetMin = flrt.offsetMax = Vector2.zero;
        floor.raycastTarget = false;

        // Simulation hint text (above simulation panel, below emails)
        var simHint = AddImage(parent, "SimHintBg", new Color(0.18f, 0.28f, 0.40f, 0.85f));
        var shrt = simHint.rectTransform;
        shrt.anchorMin = new Vector2(0, 0.265f); shrt.anchorMax = new Vector2(1, 0.285f);
        shrt.offsetMin = shrt.offsetMax = Vector2.zero;
        simHint.raycastTarget = false;
        var simHintTxt = AddText(simHint.rectTransform, "SimHintText",
            "Find all 5 red flags before the shark breaks through!",
            20, new Color(0.90f, 0.90f, 1f), TextAlignmentOptions.Center, FontStyles.Italic);
        Stretch(simHintTxt.rectTransform); simHintTxt.raycastTarget = false;

        // ── Build simulation elements (all use anchor (0.5, 0.5) pivot = panel centre) ──
        // Panel size in reference resolution: 1920 x (0.27 * 1080) = 1920 x 291
        // Using anchor (0.5, 0.5) + anchoredPosition from panel centre:
        //   panel centre = (0, 0)
        //   left edge  = -960, right edge = +960
        //   bottom = -146, top = +146

        // ── Safe zone background (right 38%) ──
        var safeZoneBg = AddImage(rrt, "SafeZoneBg", SafeZoneBg);
        var szrt = safeZoneBg.rectTransform;
        szrt.anchorMin = new Vector2(0.60f, 0); szrt.anchorMax = Vector2.one;
        szrt.offsetMin = szrt.offsetMax = Vector2.zero;
        safeZoneBg.raycastTarget = false;

        // ── Steel bars (centre) ──
        var barsRoot = new GameObject("BarsRoot", typeof(RectTransform));
        barsRoot.transform.SetParent(rrt, false);
        var brrt = barsRoot.GetComponent<RectTransform>();
        brrt.anchorMin = new Vector2(0.38f, 0); brrt.anchorMax = new Vector2(0.62f, 1);
        brrt.offsetMin = brrt.offsetMax = Vector2.zero;

        // Top + bottom crossbeams
        var topBeam = AddImage(brrt, "TopBeam", BarBolt);
        AnchorHorizStretch(topBeam.rectTransform, 0.85f, 0.96f);
        topBeam.raycastTarget = false;
        var botBeam = AddImage(brrt, "BotBeam", BarBolt);
        AnchorHorizStretch(botBeam.rectTransform, 0.04f, 0.15f);
        botBeam.raycastTarget = false;

        // 6 vertical bars
        var barImages = new Image[6];
        var barRTs = new RectTransform[6];
        for (int i = 0; i < 6; i++)
        {
            float xAnchor = Mathf.Lerp(0.08f, 0.92f, i / 5f);
            var bar = AddImage(brrt, "Bar_" + i, BarSteel);
            var brt = bar.rectTransform;
            brt.anchorMin = new Vector2(xAnchor - 0.05f, 0.05f);
            brt.anchorMax = new Vector2(xAnchor + 0.05f, 0.95f);
            brt.offsetMin = brt.offsetMax = Vector2.zero;
            bar.raycastTarget = false;
            barImages[i] = bar;
            barRTs[i] = brt;

            // Bolt circles at top and bottom
            foreach (float yBolt in new[] { 0.88f, 0.12f })
            {
                var bolt = AddImage(brt, "Bolt", BarBolt);
                bolt.sprite = spCircle;
                var bolrt = bolt.rectTransform;
                bolrt.anchorMin = bolrt.anchorMax = new Vector2(0.5f, yBolt);
                bolrt.pivot = new Vector2(0.5f, 0.5f);
                bolrt.sizeDelta = new Vector2(12, 12);
                bolt.raycastTarget = false;
            }
        }

        // ── Crack stages (initially hidden) ──
        var crackStages = new GameObject[3];
        // Crack positions: random-ish across bars
        Vector2[][] crackDefs = {
            new[] { new Vector2(0.20f, 0.60f), new Vector2(0.75f, 0.40f) },
            new[] { new Vector2(0.40f, 0.75f), new Vector2(0.55f, 0.30f), new Vector2(0.85f, 0.55f) },
            new[] { new Vector2(0.15f, 0.35f), new Vector2(0.50f, 0.55f),
                    new Vector2(0.70f, 0.20f), new Vector2(0.90f, 0.70f) }
        };
        for (int s = 0; s < 3; s++)
        {
            var stage = new GameObject("CrackStage_" + s, typeof(RectTransform));
            stage.transform.SetParent(brrt, false);
            Stretch(stage.GetComponent<RectTransform>());
            foreach (var cd in crackDefs[s])
                SpawnCrackOnBars(stage.transform, cd, spCircle);
            stage.SetActive(false);
            crackStages[s] = stage;
        }

        // ── Safe fish + eggs (right zone, anchored in panel) ──
        // Using panel-relative anchors for fish + eggs
        var fishImg = AddImage(rrt, "SafeFish", SafeFishColor);
        if (spFish != null) { fishImg.sprite = spFish; fishImg.preserveAspect = true; }
        else fishImg.sprite = spCircle;
        var fishRT = fishImg.rectTransform;
        fishRT.anchorMin = fishRT.anchorMax = new Vector2(0.78f, 0.50f);
        fishRT.pivot = new Vector2(0.5f, 0.5f);
        fishRT.sizeDelta = new Vector2(70, 70);
        fishImg.raycastTarget = false;

        var eggRTs = new RectTransform[3];
        Vector2[] eggOffsets = { new Vector2(90f, 20f), new Vector2(110f, -5f), new Vector2(80f, -25f) };
        for (int e = 0; e < 3; e++)
        {
            var egg = AddImage(rrt, "Egg_" + e, EggColor);
            egg.sprite = spCircle;
            var ert = egg.rectTransform;
            ert.anchorMin = ert.anchorMax = new Vector2(0.78f, 0.50f);
            ert.pivot = new Vector2(0.5f, 0.5f);
            ert.sizeDelta = new Vector2(22, 26);
            ert.anchoredPosition = eggOffsets[e];
            egg.raycastTarget = false;
            eggRTs[e] = ert;
        }

        // ── Shark (left zone) ──
        var sharkImg = AddImage(rrt, "Shark", SharkColor);
        if (spFish != null) { sharkImg.sprite = spFish; sharkImg.preserveAspect = true; }
        else sharkImg.sprite = spCircle;
        var sharkRT = sharkImg.rectTransform;
        sharkRT.anchorMin = sharkRT.anchorMax = new Vector2(0.5f, 0.5f);
        sharkRT.pivot = new Vector2(0.5f, 0.5f);
        sharkRT.sizeDelta = new Vector2(130, 90);
        sharkRT.anchoredPosition = new Vector2(-660f, 0f); // starts far left

        // ── Danger label ──
        var dangerLbl = AddText(rrt, "DangerLabel",
            "DANGER ZONE", 18, new Color(0.9f, 0.2f, 0.2f, 0.7f),
            TextAlignmentOptions.Center, FontStyles.Bold);
        var dlrt = dangerLbl.rectTransform;
        dlrt.anchorMin = new Vector2(0, 0.82f); dlrt.anchorMax = new Vector2(0.38f, 1f);
        dlrt.offsetMin = dlrt.offsetMax = Vector2.zero;
        dangerLbl.raycastTarget = false;

        // ── SharkSimulation component ──
        var simComp = root.gameObject.AddComponent<SharkSimulation>();
        simComp.sharkRT = sharkRT;
        simComp.sharkImage = sharkImg;
        simComp.fishRT = fishRT;
        simComp.eggRTs = eggRTs;
        simComp.barImages = barImages;
        simComp.crackStages = crackStages;
        simComp.totalTime = 60f;

        manager.simulation = simComp;
    }

    // ── Utility: add crack lines on bars ──
    static void SpawnCrackOnBars(Transform parent, Vector2 normPos, Sprite spCircle)
    {
        float[] angles = { Random.Range(-25f, 25f), Random.Range(50f, 130f) };
        float[] lengths = { Random.Range(0.18f, 0.32f), Random.Range(0.12f, 0.22f) };

        for (int ci = 0; ci < 2; ci++)
        {
            var line = new GameObject("CrackLine", typeof(RectTransform));
            line.transform.SetParent(parent, false);
            var lrt = line.GetComponent<RectTransform>();
            lrt.anchorMin = lrt.anchorMax = normPos;
            lrt.pivot = new Vector2(0.5f, 0.5f);
            lrt.sizeDelta = new Vector2(lengths[ci] * 200, 5);
            lrt.localRotation = Quaternion.Euler(0, 0, angles[ci]);
            var img = line.AddComponent<Image>();
            img.color = CrackYellow; img.raycastTarget = false;
        }
    }

    // ── Anchor helper for horizontal bar stretches ──
    static void AnchorHorizStretch(RectTransform rt, float yMin, float yMax)
    {
        rt.anchorMin = new Vector2(0, yMin); rt.anchorMax = new Vector2(1, yMax);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // =================================================================
    // Email panel (unchanged structure, new anchor Y range)
    // =================================================================

    static void BuildEmailPanel(
        RectTransform parent, SpotDifferenceManager manager, bool isScam,
        Vector2 anchorMin, Vector2 anchorMax)
    {
        var panel = AddImage(parent, isScam ? "ScamEmail" : "RealEmail", PanelBg);
        var rt = panel.rectTransform;
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        panel.raycastTarget = false;

        // Label strip
        var labelBar = AddImage(rt, "LabelBar", isScam ? LabelScamBg : LabelRealBg);
        AnchorTopStretch(labelBar.rectTransform, 42); labelBar.raycastTarget = false;
        var labelText = AddText(labelBar.rectTransform, "LabelText",
            isScam ? "PHISHING EXAMPLE — find 5 red flags"
                   : "LEGITIMATE EMAIL — for comparison",
            20, isScam ? LabelScamText : LabelRealText,
            TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(labelText.rectTransform); labelText.raycastTarget = false;

        // Subject area
        var subjectArea = AddImage(rt, "SubjectArea", PanelBg);
        var sart = subjectArea.rectTransform;
        sart.anchorMin = new Vector2(0, 1); sart.anchorMax = new Vector2(1, 1);
        sart.pivot = new Vector2(0.5f, 1);
        sart.sizeDelta = new Vector2(0, 70); sart.anchoredPosition = new Vector2(0, -42);
        subjectArea.raycastTarget = false;

        string subjectStr = isScam
            ? "URGENT: Account suspended in 24 hours!"
            : "Your package will arrive Friday";
        var subjectTmp = AddText(subjectArea.rectTransform, "SubjectText",
            subjectStr, 30, BodyText, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        var strt = subjectTmp.rectTransform;
        strt.anchorMin = Vector2.zero; strt.anchorMax = Vector2.one;
        strt.offsetMin = new Vector2(24, 0); strt.offsetMax = Vector2.zero;
        subjectTmp.raycastTarget = false;
        if (isScam)
            AddMarker(subjectArea.gameObject, manager,
                "Urgency and threat language",
                "'URGENT', 'suspended', and '24 hours' are designed to create panic. " +
                "Real companies describe what the email is about calmly.");

        // Sender row
        var senderRow = AddImage(rt, "SenderRow", PanelBg);
        var srrt = senderRow.rectTransform;
        srrt.anchorMin = new Vector2(0, 1); srrt.anchorMax = new Vector2(1, 1);
        srrt.pivot = new Vector2(0.5f, 1); srrt.sizeDelta = new Vector2(0, 68);
        srrt.anchoredPosition = new Vector2(0, -112);
        senderRow.raycastTarget = false;

        var avatar = AddImage(senderRow.rectTransform, "Avatar", AmazonOrange);
        avatar.sprite = TryGetCircleSprite(); avatar.preserveAspect = true;
        var avrt = avatar.rectTransform;
        avrt.anchorMin = avrt.anchorMax = new Vector2(0, 0.5f);
        avrt.pivot = new Vector2(0, 0.5f);
        avrt.sizeDelta = new Vector2(50, 50); avrt.anchoredPosition = new Vector2(22, 0);
        avatar.raycastTarget = false;
        var avLetter = AddText(avatar.rectTransform, "Letter", "A",
            28, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(avLetter.rectTransform); avLetter.raycastTarget = false;

        string senderName = isScam ? "Amaz0n Security" : "Amazon";
        string senderEmail = isScam ? "<security@amaz0n-shipping.net>" : "<ship-confirm@amazon.com>";
        var nameT = AddText(senderRow.rectTransform, "SenderName", senderName,
            22, BodyText, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        var ntrt = nameT.rectTransform;
        ntrt.anchorMin = new Vector2(0, 0.5f); ntrt.anchorMax = new Vector2(0, 1);
        ntrt.pivot = new Vector2(0, 0.5f);
        ntrt.sizeDelta = new Vector2(360, 0); ntrt.anchoredPosition = new Vector2(86, -4);
        nameT.raycastTarget = false;

        var emailT = AddText(senderRow.rectTransform, "SenderEmail", senderEmail,
            17, MutedText, TextAlignmentOptions.MidlineLeft);
        var etrt = emailT.rectTransform;
        etrt.anchorMin = new Vector2(0, 0); etrt.anchorMax = new Vector2(0, 0.5f);
        etrt.pivot = new Vector2(0, 0.5f);
        etrt.sizeDelta = new Vector2(420, 0); etrt.anchoredPosition = new Vector2(86, 4);
        emailT.raycastTarget = false;

        var tsT = AddText(senderRow.rectTransform, "Timestamp", "May 5, 4:03 PM",
            17, MutedText, TextAlignmentOptions.MidlineRight);
        var tsrt = tsT.rectTransform;
        tsrt.anchorMin = new Vector2(1, 0); tsrt.anchorMax = new Vector2(1, 1);
        tsrt.pivot = new Vector2(1, 0.5f);
        tsrt.sizeDelta = new Vector2(200, 0); tsrt.anchoredPosition = new Vector2(-22, 0);
        tsT.raycastTarget = false;
        if (isScam)
            AddMarker(senderRow.gameObject, manager,
                "Spoofed sender domain",
                "The address ends in 'amaz0n-shipping.net' — note the zero instead of 'o'. " +
                "Real Amazon emails come from @amazon.com.");

        // Divider
        var div = AddImage(rt, "HeaderDivider", DividerColor);
        var drt = div.rectTransform;
        drt.anchorMin = new Vector2(0, 1); drt.anchorMax = new Vector2(1, 1);
        drt.pivot = new Vector2(0.5f, 1); drt.sizeDelta = new Vector2(0, 1);
        drt.anchoredPosition = new Vector2(0, -180);
        div.raycastTarget = false;

        // Scrollable body
        float headerH = 181f;
        var scrollGo = new GameObject("BodyScroll", typeof(RectTransform));
        scrollGo.transform.SetParent(rt, false);
        var scrollRT = scrollGo.GetComponent<RectTransform>();
        scrollRT.anchorMin = Vector2.zero; scrollRT.anchorMax = Vector2.one;
        scrollRT.offsetMin = Vector2.zero; scrollRT.offsetMax = new Vector2(0, -headerH);
        var scrollImg = scrollGo.AddComponent<Image>();
        scrollImg.color = PanelBg; scrollImg.raycastTarget = false;
        var scrollRect = scrollGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = false; scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 35;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        var vpGo = new GameObject("Viewport", typeof(RectTransform));
        vpGo.transform.SetParent(scrollRT, false);
        Stretch(vpGo.GetComponent<RectTransform>());
        var vpImg = vpGo.AddComponent<Image>();
        vpImg.color = PanelBg; vpImg.raycastTarget = isScam;
        if (isScam) { var recv = vpGo.AddComponent<PanelClickReceiver>(); recv.manager = manager; }
        vpGo.AddComponent<Mask>().showMaskGraphic = false;

        var contentGo = new GameObject("BodyContent", typeof(RectTransform));
        contentGo.transform.SetParent(vpGo.transform, false);
        var contentRT = contentGo.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1); contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(0.5f, 1); contentRT.sizeDelta = Vector2.zero;
        contentRT.anchoredPosition = Vector2.zero;
        var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.spacing = 6; vlg.padding = new RectOffset(28, 28, 22, 32);
        contentGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.viewport = vpGo.GetComponent<RectTransform>();
        scrollRect.content = contentRT;

        BuildEmailBody(contentGo.transform, manager, isScam);
    }

    // =================================================================
    // Email body (unchanged from original)
    // =================================================================

    static void BuildEmailBody(Transform content, SpotDifferenceManager manager, bool isScam)
    {
        string greeting = isScam ? "Dear Valued Customer," : "Hi John,";
        var greetingRow = BodyRow(content, "Greeting", greeting, 26, BodyText, FontStyles.Normal);
        if (isScam)
            AddMarker(greetingRow, manager, "Generic greeting",
                "'Dear Valued Customer' shows the sender doesn't actually know who you are. " +
                "Real companies use your real name.");
        Spacer(content, 8);

        if (!isScam)
        {
            BodyRow(content, "OpenPara",
                "Great news! Your recent order has shipped and is on its way to you. " +
                "We wanted to give you a quick update on your delivery.",
                24, BodyText, FontStyles.Normal);
        }
        else
        {
            var body1 = BodyRow(content, "OpenPara",
                "We have detected suspecious activity on your account and have temporarily " +
                "suspended your access. You must verify your identity immediately.",
                24, BodyText, FontStyles.Normal);
            AddMarker(body1, manager, "Spelling error",
                "'Suspecious' is a misspelling of 'suspicious'. Real corporate communications " +
                "are professionally written and proofread.");
        }
        Spacer(content, 10);

        BodyRow(content, "OrderLabel", "Order Summary:", 21, MutedText, FontStyles.Bold);
        Spacer(content, 2);
        BodyRow(content, "OrderItem", "    Echo Dot (5th Gen) — Charcoal x 1", 22, BodyText, FontStyles.Normal);
        BodyRow(content, "OrderNum", "    Order #: 113-4567890", 22, MutedText, FontStyles.Italic);
        if (!isScam)
        {
            BodyRow(content, "OrderDate", "    Estimated Delivery: Friday, May 10, 2024", 22, MutedText, FontStyles.Italic);
            BodyRow(content, "OrderAddr", "    Shipping to: 142 Maple Street, Edmonton, AB", 22, MutedText, FontStyles.Italic);
        }
        Spacer(content, 12);

        if (!isScam)
            BodyRow(content, "Para2",
                "You can track your package in real time using the button below. " +
                "Our delivery partner will send you a separate notification when your order is out for delivery.",
                24, BodyText, FontStyles.Normal);
        else
            BodyRow(content, "Para2",
                "To restore your account, click the button below and complete identity verification. " +
                "Failure to respond within 24 hours will result in permanent account deletion.",
                24, BodyText, FontStyles.Normal);
        Spacer(content, 14);

        // CTA
        var ctaHolder = new GameObject("CtaHolder", typeof(RectTransform));
        ctaHolder.transform.SetParent(content, false);
        var ctaHolderImg = ctaHolder.AddComponent<Image>();
        ctaHolderImg.color = new Color(0, 0, 0, 0); ctaHolderImg.raycastTarget = false;
        ctaHolder.AddComponent<LayoutElement>().preferredHeight = 72;

        var ctaBtn = AddImage(ctaHolder.GetComponent<RectTransform>(), "CtaBtn",
            isScam ? CtaScam : CtaReal);
        var cbrt = ctaBtn.rectTransform;
        cbrt.anchorMin = cbrt.anchorMax = new Vector2(0.5f, 0.5f);
        cbrt.pivot = new Vector2(0.5f, 0.5f); cbrt.sizeDelta = new Vector2(340, 56);
        ctaBtn.raycastTarget = false;
        var ctaLabel = AddText(ctaBtn.rectTransform, "Label",
            isScam ? "VERIFY ACCOUNT NOW" : "View Order Details",
            23, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(ctaLabel.rectTransform); ctaLabel.raycastTarget = false;
        if (isScam)
            AddMarker(ctaHolder, manager, "Suspicious call to action",
                "'VERIFY ACCOUNT NOW' uses vague urgent language. Legitimate email buttons " +
                "describe specific tasks like 'View Order Details'.");
        Spacer(content, 14);

        if (!isScam)
        {
            BodyRow(content, "Para3", "If you have any questions, our customer support team is available 24/7 through the Amazon Help Center.", 24, BodyText, FontStyles.Normal);
            Spacer(content, 10);
            BodyRow(content, "Para4", "As an Amazon Prime member, your package is eligible for our A-to-Z Guarantee.", 24, BodyText, FontStyles.Normal);
            Spacer(content, 10);
            BodyRow(content, "Para5", "You have 30 days from the delivery date to initiate a return.", 24, BodyText, FontStyles.Normal);
            Spacer(content, 16);
            BodyRow(content, "Sig", "Thank you for shopping with Amazon!", 24, BodyText, FontStyles.Normal);
            BodyRow(content, "SigName", "— The Amazon Team", 22, MutedText, FontStyles.Italic);
        }
        else
        {
            BodyRow(content, "Para3",
                "For verification you will be required to confirm your password and provide " +
                "full billing information including credit card details. This step cannot be skipped.",
                24, BodyText, FontStyles.Normal);
            Spacer(content, 10);
            BodyRow(content, "Para4",
                "This is the only notice you will receive. We cannot process recovery requests " +
                "for accounts not verified within the required time window.",
                24, BodyText, FontStyles.Normal);
            Spacer(content, 10);
            BodyRow(content, "Para5",
                "Do not share this email or verification link with anyone. This link is unique " +
                "to your account and expires in exactly 24 hours.",
                24, BodyText, FontStyles.Normal);
            Spacer(content, 16);
            BodyRow(content, "Sig", "— Amaz0n Security Team", 22, MutedText, FontStyles.Italic);
            BodyRow(content, "SigDept", "Account Protection Division", 20, MutedText, FontStyles.Normal);
        }

        Spacer(content, 16);
        var footerDiv = AddImage(content, "FooterDiv", DividerColor);
        (footerDiv.GetComponent<LayoutElement>() ?? footerDiv.gameObject.AddComponent<LayoutElement>())
            .preferredHeight = 1;
        footerDiv.raycastTarget = false;
        Spacer(content, 10);
        string footer = isScam
            ? "Questions? Contact us at: support@amaz0n-security.tk"
            : "Need help? Visit our Help Center   |   Manage your account";
        BodyRow(content, "Footer", footer, 18, MutedText, FontStyles.Normal);
    }

    // =================================================================
    // Result panel (unchanged)
    // =================================================================

    static GameObject BuildResultPanel(RectTransform parent, SpotDifferenceManager manager)
    {
        var overlay = AddImage(parent, "ResultOverlay", OverlayColor);
        Stretch(overlay.rectTransform); overlay.raycastTarget = true;

        var panel = AddImage(overlay.rectTransform, "ResultPanel", Color.white);
        var prt = panel.rectTransform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot = new Vector2(0.5f, 0.5f); prt.sizeDelta = new Vector2(1100, 800);

        var header = AddImage(prt, "Header", HudColor);
        AnchorTopStretch(header.rectTransform, 90);
        var hLabel = AddText(header.rectTransform, "Title",
            "You spotted all the red flags!", 44, HudText, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(hLabel.rectTransform); hLabel.raycastTarget = false;

        var breakdown = AddText(prt, "Breakdown", string.Empty,
            22, Hex("#333333"), TextAlignmentOptions.TopLeft);
        var brt = breakdown.rectTransform;
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
        brt.offsetMin = new Vector2(60, 190); brt.offsetMax = new Vector2(-60, -130);

        var scoreText = AddText(prt, "Score", "Final Score: 0",
            36, Hex("#222222"), TextAlignmentOptions.Center, FontStyles.Bold);
        var srt = scoreText.rectTransform;
        srt.anchorMin = new Vector2(0, 0); srt.anchorMax = new Vector2(1, 0);
        srt.pivot = new Vector2(0.5f, 0); srt.sizeDelta = new Vector2(-80, 56);
        srt.anchoredPosition = new Vector2(0, 120);

        var playAgain = AddButton(prt, "PlayAgainBtn", "Play Again", 28, Hex("#2ECC71"), Color.white);
        var part = playAgain.GetComponent<RectTransform>();
        part.anchorMin = new Vector2(0.5f, 0); part.anchorMax = new Vector2(0.5f, 0);
        part.pivot = new Vector2(1f, 0); part.sizeDelta = new Vector2(260, 70);
        part.anchoredPosition = new Vector2(-20, 32);
        playAgain.GetComponent<Button>().onClick.AddListener(manager.PlayAgain);

        var menuBtn = AddButton(prt, "MenuBtn", "Back to Map", 28, Hex("#7F8C8D"), Color.white);
        var mrt = menuBtn.GetComponent<RectTransform>();
        mrt.anchorMin = new Vector2(0.5f, 0); mrt.anchorMax = new Vector2(0.5f, 0);
        mrt.pivot = new Vector2(0f, 0); mrt.sizeDelta = new Vector2(260, 70);
        mrt.anchoredPosition = new Vector2(20, 32);
        menuBtn.GetComponent<Button>().onClick.AddListener(manager.BackToWorldMap);

        manager.resultTitle = hLabel;
        manager.resultBreakdown = breakdown;
        manager.resultScore = scoreText;

        overlay.gameObject.SetActive(false);
        return overlay.gameObject;
    }

    // =================================================================
    // Commentator (unchanged)
    // =================================================================

    static Commentator BuildCommentator(RectTransform parent)
    {
        var root = new GameObject("CommentatorPanel", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        var rrt = root.GetComponent<RectTransform>();
        rrt.anchorMin = rrt.anchorMax = Vector2.zero;
        rrt.pivot = Vector2.zero; rrt.sizeDelta = new Vector2(580, 130);
        rrt.anchoredPosition = new Vector2(30, 30);

        var portrait = AddImage(rrt, "Portrait", new Color(1f, 0.72f, 0.78f));
        portrait.sprite = TryGetCircleSprite(); portrait.preserveAspect = true;
        var prt = portrait.rectTransform;
        prt.anchorMin = new Vector2(0, 0); prt.anchorMax = new Vector2(0, 1);
        prt.pivot = new Vector2(0, 0.5f); prt.sizeDelta = new Vector2(110, 0);

        var letter = AddText(portrait.rectTransform, "Letter", "G",
            60, new Color(0.2f, 0.1f, 0.18f), TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(letter.rectTransform);

        var bubble = AddImage(rrt, "Bubble", Color.white);
        var brt = bubble.rectTransform;
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
        brt.offsetMin = new Vector2(125, 0); brt.offsetMax = Vector2.zero;

        var speakerLabel = AddText(bubble.rectTransform, "SpeakerLabel", "Grandma", 17,
            new Color(0.78f, 0.30f, 0.50f), TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        var slrt = speakerLabel.rectTransform;
        slrt.anchorMin = new Vector2(0, 1); slrt.anchorMax = new Vector2(1, 1);
        slrt.pivot = new Vector2(0, 1); slrt.sizeDelta = new Vector2(0, 24);
        slrt.anchoredPosition = new Vector2(18, -6);

        var bubbleText = AddText(bubble.rectTransform, "BubbleText", "...", 21,
            new Color(0.13f, 0.13f, 0.13f), TextAlignmentOptions.MidlineLeft);
        bubbleText.textWrappingMode = TextWrappingModes.Normal;
        var btrt = bubbleText.rectTransform;
        btrt.anchorMin = Vector2.zero; btrt.anchorMax = Vector2.one;
        btrt.offsetMin = new Vector2(18, 6); btrt.offsetMax = new Vector2(-18, -28);

        var comm = root.AddComponent<Commentator>();
        comm.root = root; comm.portrait = portrait; comm.portraitLetter = letter;
        comm.bubbleBg = bubble; comm.bubbleText = bubbleText; comm.speakerLabel = speakerLabel;
        comm.portraitSprite = TryGetCircleSprite();
        comm.speakerName = "Grandma"; comm.portraitInitial = "G";
        comm.portraitColor = new Color(1f, 0.72f, 0.78f);
        return comm;
    }

    // =================================================================
    // Marker helper
    // =================================================================

    static void AddMarker(GameObject parent, SpotDifferenceManager manager,
        string flagName, string explanation)
    {
        var go = new GameObject("DiffMarker_" + Sanitize(flagName), typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        Stretch(go.GetComponent<RectTransform>());
        var img = go.AddComponent<Image>();
        img.color = new Color(1, 0, 0, 0); img.raycastTarget = true;
        var marker = go.AddComponent<DifferenceMarker>();
        marker.flagName = flagName;
        marker.explanation = explanation;
        marker.manager = manager;
        manager.markers.Add(marker);
    }

    // =================================================================
    // Build settings
    // =================================================================

    static void AddSceneToBuildSettings(string path)
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(s => s.path == path))
        {
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }

    // =================================================================
    // Sprite finder
    // =================================================================

    static Sprite FindSprite(string name)
    {
        string[] guids = AssetDatabase.FindAssets(name + " t:Sprite");
        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            if (System.IO.Path.GetFileNameWithoutExtension(path).ToLower() == name.ToLower())
            {
                var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (s != null) return s;
            }
        }
        return null;
    }

    static void LogFound(string n, Sprite s) =>
        Debug.Log($"[SpotDiffBuilder] {n}: " + (s != null ? "found" : "not found (fallback)"));

    // =================================================================
    // Body row helpers
    // =================================================================

    static GameObject BodyRow(Transform parent, string name, string text,
        int fontSize, Color color, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = fontSize; t.color = color;
        t.fontStyle = style; t.alignment = TextAlignmentOptions.TopLeft;
        t.textWrappingMode = TextWrappingModes.Normal; t.raycastTarget = false;
        go.AddComponent<LayoutElement>().flexibleWidth = 1;
        return go;
    }

    static void Spacer(Transform parent, float height)
    {
        var go = new GameObject("Spacer", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height; le.flexibleWidth = 1;
    }

    static void AddMarker(Transform parent, SpotDifferenceManager manager,
        string flagName, string explanation)
        => AddMarker(parent.gameObject, manager, flagName, explanation);

    // =================================================================
    // Low-level UI helpers
    // =================================================================

    static Image AddImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>(); img.color = color; return img;
    }

    static TMP_Text AddText(Transform parent, string name, string content,
        int fontSize, Color color, TextAlignmentOptions align,
        FontStyles style = FontStyles.Normal)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = content; tmp.fontSize = fontSize; tmp.color = color;
        tmp.alignment = align; tmp.fontStyle = style; tmp.raycastTarget = false;
        return tmp;
    }

    static GameObject AddButton(Transform parent, string name, string label,
        int fontSize, Color bg, Color textColor)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>(); img.color = bg;
        var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        var t = AddText(go.transform, "Label", label, fontSize, textColor,
            TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(t.rectTransform); return go;
    }

    static void Stretch(RectTransform rt)
    { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }

    static void AnchorTopStretch(RectTransform rt, float height, float topInset = 0)
    {
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1); rt.sizeDelta = new Vector2(0, height);
        rt.anchoredPosition = new Vector2(0, -topInset);
    }

    static void AnchorRect(RectTransform rt, Vector2 aMin, Vector2 aMax,
        Vector2 oMin, Vector2 oMax)
    { rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = oMin; rt.offsetMax = oMax; }

    static Color Hex(string hex) =>
        ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

    static string Sanitize(string s) =>
        new string(s.Where(c => char.IsLetterOrDigit(c)).ToArray());

    static Sprite TryGetCircleSprite()
    {
        try { return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"); }
        catch { return null; }
    }
}