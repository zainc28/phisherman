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
/// Builds the Spot the Difference scene — Detective Dossier theme.
///
/// Visual language: manila folder, dark brown HUD, "CLASSIFIED" stamp,
/// dashed inner borders, monospace case number, red pen circles on found flags.
///
/// Layout (bottom → top):
///   0%   – 27%  Cage + diver simulation panel
///   27%  – 29%  Sim hint strip
///   29%  – 89%  Email panels (real vs phishing) inside folder
///   89%  – 93%  Evidence log strip
///   93%  – 100% HUD bar
///
/// Sim revamp (cage edition):
///   • Phisherman diver swims idly on the RIGHT side of the sim panel.
///   • A shark sits in the CENTRE-LEFT behind cage bars.
///   • At Start, a cage drops from above and traps the shark.
///   • Each wrong click lifts the cage one stage (3 stages = fully free).
///   • The shark periodically bangs the cage bars to pressure the player.
///   • Success: diver cheers, cage stays locked.
///   • Breach (3 wrong / time out): cage rises, shark charges the diver.
/// </summary>
public static class SpotDifferenceBuilder
{
    private const string ScenesDir = "Assets/Scenes";
    private const string ScenePath = "Assets/Scenes/SpotDifference.unity";

    // ── Detective Dossier palette ──────────────────────────────────────
    private static readonly Color HudDark = Hex("#1A140E");
    private static readonly Color HudBorder = Hex("#8B6914");
    private static readonly Color FolderOuter = Hex("#C8A96E");
    private static readonly Color FolderInner = Hex("#E8D5A3");
    private static readonly Color FolderDash = Hex("#A07840");
    private static readonly Color HudGold = Hex("#D4A843");
    private static readonly Color HudMuted = Hex("#8B7355");
    private static readonly Color CaseMono = Hex("#6B4F2A");
    private static readonly Color ClassifiedRed = Hex("#C0392B");
    private static readonly Color PanelBg = Hex("#FAF6EE");
    private static readonly Color PanelBorder = Hex("#C8A870");
    private static readonly Color LabelRealBg = Hex("#D4EDDA");
    private static readonly Color LabelScamBg = Hex("#FDE8E8");
    private static readonly Color LabelRealText = Hex("#1A5C2A");
    private static readonly Color LabelScamText = Hex("#7A1515");
    private static readonly Color DividerColor = Hex("#DDC9A0");
    private static readonly Color BodyText = Hex("#1A0F00");
    private static readonly Color MutedText = Hex("#7A6040");
    private static readonly Color AmazonOrange = Hex("#E37400");
    private static readonly Color CtaReal = Hex("#1565C0");
    private static readonly Color CtaScam = Hex("#B71C1C");
    private static readonly Color EvidenceBg = Hex("#2B1F0E");
    private static readonly Color EvidenceGold = Hex("#C8A060");
    // Sim palette — deep ocean
    private static readonly Color SimDeep = Hex("#071824");
    private static readonly Color SimMid = Hex("#0A2840");
    private static readonly Color SimHintBg = new Color(0.18f, 0.28f, 0.40f, 0.85f);
    // Cage palette
    private static readonly Color CageBarColor = Hex("#C8A060");          // gold bars
    private static readonly Color CageBaseColor = new Color(0.55f, 0.35f, 0.10f, 0.95f); // dark wood base
    private static readonly Color CageShadowC = new Color(0f, 0f, 0f, 0.30f);
    // Other sim colours
    private static readonly Color RodLineColor = new Color(0.85f, 0.70f, 0.40f, 0.90f);
    private static readonly Color HookColor = Hex("#C8A060");
    private static readonly Color HeartFull = new Color(0.75f, 0.18f, 0.18f);
    private static readonly Color PenRed = Hex("#C0392B");
    private static readonly Color OverlayColor = new Color(0f, 0f, 0f, 0.72f);

    // =================================================================
    // Build entry-point
    // =================================================================

    [MenuItem("Phisherman/Build Spot the Difference Scene")]
    public static void Build()
    {
        if (!Directory.Exists(ScenesDir)) Directory.CreateDirectory(ScenesDir);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Sprite spCircle = TryGetCircleSprite();
        Sprite spPhisherman = FindSprite("phisherman");
        Sprite spMagGlass = FindSprite("magnifying_glass");
        Sprite spFish = FindSprite("fish_normal") ?? FindSprite("td_hanging_fish");
        Sprite spShark = FindSprite("shark");
        LogFound("phisherman", spPhisherman);
        LogFound("magnifying_glass", spMagGlass);
        LogFound("fish", spFish);
        LogFound("shark", spShark);

        // Camera
        var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>(); camGo.AddComponent<AudioListener>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Hex("#1A140E");
        cam.orthographic = true;

        // EventSystem
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>(); es.AddComponent<StandaloneInputModule>();

        // Canvas
        var canvasGo = new GameObject("Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 100;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        var canvasRT = canvasGo.GetComponent<RectTransform>();

        // Manager
        var mgrGo = new GameObject("GameManager");
        var manager = mgrGo.AddComponent<SpotDifferenceManager>();
        var sfxSrc = mgrGo.AddComponent<AudioSource>(); sfxSrc.playOnAwake = false;
        var musicSrc = mgrGo.AddComponent<AudioSource>();
        musicSrc.playOnAwake = false; musicSrc.loop = true; musicSrc.volume = 0.35f;
        var sharkMusic = FindAudioClip("shark_music_v1");
        if (sharkMusic != null) musicSrc.clip = sharkMusic;
        manager.mainCanvas = canvas;
        manager.mainCanvasRT = canvasRT;
        manager.circleSprite = spCircle;
        manager.sfxSource = sfxSrc;
        manager.sfxSplash = FindAudioClip("water_splash");
        manager.sfxWrong = FindAudioClip("glass_crack");
        manager.sfxImpact = FindAudioClip("impact");
        manager.sfxRodWinding = FindAudioClip("fishing_rod_winding");

        // Background
        var bgImg = AddImage(canvasRT, "Background", HudDark);
        Stretch(bgImg.rectTransform); bgImg.raycastTarget = false;

        // HUD
        BuildHud(canvasRT, manager, spPhisherman, spMagGlass, spCircle);

        // Evidence log strip
        BuildEvidenceStrip(canvasRT, manager);

        // Folder + email panels
        BuildFolder(canvasRT, manager);

        // Sim hint strip
        var simHintBg = AddImage(canvasRT, "SimHintBg", SimHintBg);
        var shrt = simHintBg.rectTransform;
        shrt.anchorMin = new Vector2(0, 0.27f); shrt.anchorMax = new Vector2(1, 0.29f);
        shrt.offsetMin = shrt.offsetMax = Vector2.zero; simHintBg.raycastTarget = false;
        var simHintTxt = AddText(simHintBg.rectTransform, "SimHintText",
            "Find all 6 red flags before the shark escapes its cage!",
            19, new Color(0.90f, 0.90f, 1f), TextAlignmentOptions.Center, FontStyles.Italic);
        Stretch(simHintTxt.rectTransform); simHintTxt.raycastTarget = false;

        // Cage + diver simulation
        BuildSimulation(canvasRT, manager, spPhisherman, spShark, spCircle);

        // Effect layer (water drips)
        var effectLayerGo = new GameObject("EffectLayer", typeof(RectTransform));
        effectLayerGo.transform.SetParent(canvasRT, false);
        Stretch(effectLayerGo.GetComponent<RectTransform>());
        effectLayerGo.transform.SetAsLastSibling();
        var eImg = effectLayerGo.AddComponent<Image>();
        eImg.color = new Color(0, 0, 0, 0); eImg.raycastTarget = false;
        manager.effectLayer = effectLayerGo.GetComponent<RectTransform>();

        // Magnifying glass overlay
        BuildMagnifyingGlass(canvasRT, manager, spMagGlass, spCircle);

        // Feedback popup
        var feedback = AddText(canvasRT, "Feedback", string.Empty,
            38, Color.green, TextAlignmentOptions.Center, FontStyles.Bold);
        var fbRT = feedback.rectTransform;
        fbRT.anchorMin = new Vector2(0.5f, 1); fbRT.anchorMax = new Vector2(0.5f, 1);
        fbRT.pivot = new Vector2(0.5f, 1); fbRT.sizeDelta = new Vector2(1100, 56);
        fbRT.anchoredPosition = new Vector2(0, -118); feedback.raycastTarget = false;

        // Result panel
        var resultPanel = BuildResultPanel(canvasRT, manager);

        // Commentator
        manager.commentator = BuildCommentator(canvasRT);

        manager.feedbackText = feedback;
        manager.resultPanel = resultPanel;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings(ScenePath);
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log($"[SpotDifferenceBuilder] Built → {ScenePath}");
    }

    // =================================================================
    // HUD
    // =================================================================

    static void BuildHud(RectTransform parent, SpotDifferenceManager manager,
        Sprite spPhisherman, Sprite spMagGlass, Sprite spCircle)
    {
        var hud = AddImage(parent, "HUD", HudDark);
        AnchorTopStretch(hud.rectTransform, 100);
        hud.raycastTarget = false;

        var border = AddImage(hud.rectTransform, "HudBorder", HudBorder);
        var brt = border.rectTransform;
        brt.anchorMin = Vector2.zero; brt.anchorMax = new Vector2(1, 0);
        brt.pivot = new Vector2(0.5f, 0); brt.sizeDelta = new Vector2(0, 2);
        border.raycastTarget = false;

        float phX = 24f;
        if (spPhisherman != null)
        {
            var phH = new GameObject("PhishermanHolder", typeof(RectTransform));
            phH.transform.SetParent(hud.rectTransform, false);
            var phRT = phH.GetComponent<RectTransform>();
            phRT.anchorMin = new Vector2(0, 0); phRT.anchorMax = new Vector2(0, 1);
            phRT.pivot = new Vector2(0, 0.5f); phRT.sizeDelta = new Vector2(70, 0);
            phRT.anchoredPosition = new Vector2(14, 0);

            var noMag = NewGO("NoMagGlass", phH.transform); Stretch(noMag.GetComponent<RectTransform>());
            var wMag = NewGO("WithMagGlass", phH.transform); Stretch(wMag.GetComponent<RectTransform>());
            var nmI = noMag.AddComponent<Image>(); nmI.sprite = spPhisherman; nmI.preserveAspect = true; nmI.raycastTarget = false;
            var wmI = wMag.AddComponent<Image>(); wmI.sprite = spPhisherman; wmI.preserveAspect = true; wmI.raycastTarget = false;
            if (spMagGlass != null)
            {
                var mgi = NewGO("MagIcon", wMag.transform);
                var mgRT = mgi.GetComponent<RectTransform>();
                mgRT.anchorMin = mgRT.anchorMax = new Vector2(1, 0); mgRT.pivot = new Vector2(1, 0);
                mgRT.sizeDelta = new Vector2(28, 28); mgRT.anchoredPosition = new Vector2(-2, 2);
                var mgImg = mgi.AddComponent<Image>(); mgImg.sprite = spMagGlass; mgImg.preserveAspect = true; mgImg.raycastTarget = false;
            }
            wMag.SetActive(false);
            manager.phishermanNoMagGlass = noMag;
            manager.phishermanWithMagGlass = wMag;
            phX = 96f;
        }

        var title = AddText(hud.rectTransform, "Title",
            "Case File #4471 — Spot the Phishing Red Flags",
            26, HudGold, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        AnchorRect(title.rectTransform, new Vector2(0, 0.5f), new Vector2(0.30f, 1),
            new Vector2(phX, 0), Vector2.zero);

        var sub = AddText(hud.rectTransform, "CaseSub",
            "INVESTIGATOR: PHISHERMAN  |  AMAZON IMPERSONATION  |  WORLD 1",
            14, HudMuted, TextAlignmentOptions.MidlineLeft);
        AnchorRect(sub.rectTransform, new Vector2(0, 0), new Vector2(0.30f, 0.5f),
            new Vector2(phX, 0), Vector2.zero);

        var timerBox = AddImage(hud.rectTransform, "TimerBox", new Color(0.10f, 0.07f, 0.03f, 1f));
        AnchorRect(timerBox.rectTransform, new Vector2(0.30f, 0), new Vector2(0.46f, 1),
            new Vector2(8, 8), new Vector2(-8, -8));
        timerBox.raycastTarget = false;
        var timerTxt = AddText(timerBox.rectTransform, "Timer", "1:00",
            40, HudGold, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(timerTxt.rectTransform);

        var magBtnGo = NewGO("MagToggleBtn", hud.rectTransform);
        var magBtnRT = magBtnGo.GetComponent<RectTransform>();
        AnchorRect(magBtnRT, new Vector2(0.46f, 0), new Vector2(0.58f, 1),
            new Vector2(8, 14), new Vector2(-8, -14));
        var magBtnImg = magBtnGo.AddComponent<Image>(); magBtnImg.color = new Color(0.22f, 0.16f, 0.06f, 1f);
        var magBtn = magBtnGo.AddComponent<Button>(); magBtn.targetGraphic = magBtnImg;
        var magLbl = AddText(magBtnGo.transform, "MagLabel", "🔍 Magnifier",
            18, HudGold, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(magLbl.rectTransform);
        UnityEventTools.AddPersistentListener(magBtn.onClick, manager.ToggleMagnifier);
        manager.magToggleButton = magBtn;
        manager.magToggleLabel = magLbl;

        var heartsH = NewGO("HeartsHolder", hud.rectTransform);
        var heartsHRT = heartsH.GetComponent<RectTransform>();
        AnchorRect(heartsHRT, new Vector2(0.58f, 0), new Vector2(0.68f, 1),
            new Vector2(8, 0), Vector2.zero);
        var hlg = heartsH.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter; hlg.spacing = 6;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;
        hlg.childControlWidth = hlg.childControlHeight = false;
        hlg.padding = new RectOffset(0, 0, 18, 18);
        var heartImages = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            var hgo = NewGO("Heart_" + i, heartsH.transform);
            hgo.GetComponent<RectTransform>().sizeDelta = new Vector2(30, 30);
            var hi = hgo.AddComponent<Image>(); hi.sprite = spCircle; hi.color = HeartFull;
            hi.preserveAspect = true; hi.raycastTarget = false;
            heartImages[i] = hi;
        }
        manager.heartImages = heartImages;

        var counter = AddText(hud.rectTransform, "Counter", "Found: 0 / 5",
            26, HudGold, TextAlignmentOptions.Midline, FontStyles.Bold);
        AnchorRect(counter.rectTransform, new Vector2(0.68f, 0), new Vector2(0.84f, 1),
            Vector2.zero, Vector2.zero);

        var score = AddText(hud.rectTransform, "Score", "Score: 0",
            26, HudGold, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
        AnchorRect(score.rectTransform, new Vector2(0.84f, 0), new Vector2(1f, 1),
            Vector2.zero, new Vector2(-18, 0));

        manager.timerText = timerTxt;
        manager.counterText = counter;
        manager.scoreText = score;
    }

    // =================================================================
    // Evidence log strip
    // =================================================================

    static void BuildEvidenceStrip(RectTransform parent, SpotDifferenceManager manager)
    {
        var bg = AddImage(parent, "EvidenceBg", EvidenceBg);
        var rt = bg.rectTransform;
        rt.anchorMin = new Vector2(0, 0.895f); rt.anchorMax = new Vector2(1, 0.932f);
        rt.offsetMin = rt.offsetMax = Vector2.zero; bg.raycastTarget = false;

        var topLine = AddImage(rt, "EvidenceTop", HudBorder);
        var tlrt = topLine.rectTransform;
        tlrt.anchorMin = new Vector2(0, 1); tlrt.anchorMax = new Vector2(1, 1);
        tlrt.pivot = new Vector2(0.5f, 1); tlrt.sizeDelta = new Vector2(0, 1);
        topLine.raycastTarget = false;

        var row = NewGO("EvidenceRow", rt);
        Stretch(row.GetComponent<RectTransform>());

        var lbl = AddText(row.transform, "EvLabel", "EVIDENCE LOG:", 14,
            EvidenceGold, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        var lrt = lbl.rectTransform;
        lrt.anchorMin = new Vector2(0, 0); lrt.anchorMax = new Vector2(0, 1);
        lrt.pivot = new Vector2(0, 0.5f); lrt.sizeDelta = new Vector2(130, 0);
        lrt.anchoredPosition = new Vector2(18, 0); lbl.raycastTarget = false;

        var evLog = AddText(row.transform, "EvidenceLog",
            "<color=#4a3520>— no flags marked yet —</color>",
            14, new Color(0.85f, 0.80f, 0.65f), TextAlignmentOptions.MidlineLeft);
        var ert = evLog.rectTransform;
        ert.anchorMin = new Vector2(0, 0); ert.anchorMax = new Vector2(1, 1);
        ert.offsetMin = new Vector2(155, 0); ert.offsetMax = new Vector2(-18, 0);
        evLog.raycastTarget = false;

        manager.evidenceLogText = evLog;
    }

    // =================================================================
    // Folder wrapper + email panels
    // =================================================================

    static void BuildFolder(RectTransform parent, SpotDifferenceManager manager)
    {
        var folder = AddImage(parent, "Folder", FolderOuter);
        var frt = folder.rectTransform;
        frt.anchorMin = new Vector2(0.010f, 0.290f); frt.anchorMax = new Vector2(0.990f, 0.893f);
        frt.offsetMin = frt.offsetMax = Vector2.zero; folder.raycastTarget = false;

        var tabGo = NewGO("FolderTab", folder.rectTransform);
        var tabRT = tabGo.GetComponent<RectTransform>();
        tabRT.anchorMin = new Vector2(0.02f, 1); tabRT.anchorMax = new Vector2(0.16f, 1);
        tabRT.pivot = new Vector2(0, 0); tabRT.sizeDelta = new Vector2(0, 28);
        var tabImg = tabGo.AddComponent<Image>(); tabImg.color = Hex("#B8944A"); tabImg.raycastTarget = false;
        var tabTxt = AddText(tabGo.transform, "TabText", "EVIDENCE DOSSIER",
            12, Hex("#3D2B0E"), TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(tabTxt.rectTransform); tabTxt.raycastTarget = false;

        var inner = AddImage(folder.rectTransform, "FolderInner", FolderInner);
        var irt = inner.rectTransform;
        irt.anchorMin = new Vector2(0.005f, 0.005f); irt.anchorMax = new Vector2(0.995f, 0.995f);
        irt.offsetMin = irt.offsetMax = Vector2.zero; inner.raycastTarget = false;

        var stamp = AddText(inner.rectTransform, "ClassifiedStamp", "CLASSIFIED",
            20, ClassifiedRed, TextAlignmentOptions.Center, FontStyles.Bold);
        var srt = stamp.rectTransform;
        srt.anchorMin = new Vector2(0.78f, 0.88f); srt.anchorMax = new Vector2(0.99f, 0.99f);
        srt.offsetMin = srt.offsetMax = Vector2.zero; stamp.raycastTarget = false;
        stamp.gameObject.AddComponent<Outline>().effectColor =
            new Color(ClassifiedRed.r, ClassifiedRed.g, ClassifiedRed.b, 0.5f);

        var caseNum = AddText(inner.rectTransform, "CaseNumber",
            "CASE #4471-B  |  FILED: MAY 5 2024  |  Identify 6 red flags in the suspect document",
            14, CaseMono, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
        var cnrt = caseNum.rectTransform;
        cnrt.anchorMin = new Vector2(0, 0.92f); cnrt.anchorMax = new Vector2(0.76f, 1f);
        cnrt.offsetMin = new Vector2(14, 0); cnrt.offsetMax = Vector2.zero;
        caseNum.raycastTarget = false;

        var cdiv = AddImage(inner.rectTransform, "CaseDivider", FolderDash);
        var cdrt = cdiv.rectTransform;
        cdrt.anchorMin = new Vector2(0, 0.91f); cdrt.anchorMax = new Vector2(1, 0.915f);
        cdrt.offsetMin = new Vector2(8, 0); cdrt.offsetMax = new Vector2(-8, 0);
        cdiv.raycastTarget = false;

        BuildEmailPanel(inner.rectTransform, manager, isScam: false,
            new Vector2(0.01f, 0.01f), new Vector2(0.492f, 0.908f));
        BuildEmailPanel(inner.rectTransform, manager, isScam: true,
            new Vector2(0.508f, 0.01f), new Vector2(0.99f, 0.908f));
    }

    // =================================================================
    // Email panel
    // =================================================================

    static void BuildEmailPanel(RectTransform parent, SpotDifferenceManager manager,
        bool isScam, Vector2 anchorMin, Vector2 anchorMax)
    {
        var panel = AddImage(parent, isScam ? "ScamDoc" : "RealDoc", PanelBg);
        var rt = panel.rectTransform;
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        panel.raycastTarget = false;

        var brd = AddImage(rt, "DocBorder", PanelBorder);
        Stretch(brd.rectTransform); brd.raycastTarget = false;
        var face = AddImage(rt, "DocFace", PanelBg);
        var faceRT = face.rectTransform;
        faceRT.anchorMin = Vector2.zero; faceRT.anchorMax = Vector2.one;
        faceRT.offsetMin = new Vector2(2, 2); faceRT.offsetMax = new Vector2(-2, -2);
        face.raycastTarget = isScam;
        if (isScam) { var recv1 = face.gameObject.AddComponent<PanelClickReceiver>(); recv1.manager = manager; }

        var lbl = AddImage(face.rectTransform, "DocStamp", isScam ? LabelScamBg : LabelRealBg);
        AnchorTopStretch(lbl.rectTransform, 36); lbl.raycastTarget = false;
        var lblTxt = AddText(lbl.rectTransform, "StampText",
            isScam ? "SUSPECT DOCUMENT — find 6 red flags" : "LEGITIMATE — for reference",
            17, isScam ? LabelScamText : LabelRealText, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(lblTxt.rectTransform); lblTxt.raycastTarget = false;

        var subjArea = AddImage(face.rectTransform, "SubjectArea", PanelBg);
        var sart = subjArea.rectTransform;
        sart.anchorMin = new Vector2(0, 1); sart.anchorMax = new Vector2(1, 1);
        sart.pivot = new Vector2(0.5f, 1); sart.sizeDelta = new Vector2(0, 60);
        sart.anchoredPosition = new Vector2(0, -36); subjArea.raycastTarget = false;
        var subjTxt = AddText(subjArea.rectTransform, "Subject",
            isScam ? "URGENT: Account suspended in 24 hours!" : "Your package will arrive Friday",
            24, isScam ? Hex("#7A1515") : BodyText,
            TextAlignmentOptions.MidlineLeft, isScam ? FontStyles.Bold : FontStyles.Normal);
        var strt = subjTxt.rectTransform;
        strt.anchorMin = Vector2.zero; strt.anchorMax = Vector2.one;
        strt.offsetMin = new Vector2(18, 0); strt.offsetMax = Vector2.zero;
        subjTxt.raycastTarget = false;
        if (isScam)
            AddMarkerWithCircle(subjArea.gameObject, manager,
                "Urgency and threat language",
                "'URGENT', 'suspended', '24 hours' — designed to create panic so you don't think clearly.");

        var senderRow = AddImage(face.rectTransform, "SenderRow", PanelBg);
        var srrt = senderRow.rectTransform;
        srrt.anchorMin = new Vector2(0, 1); srrt.anchorMax = new Vector2(1, 1);
        srrt.pivot = new Vector2(0.5f, 1); srrt.sizeDelta = new Vector2(0, 58);
        srrt.anchoredPosition = new Vector2(0, -96); senderRow.raycastTarget = false;

        var avatar = AddImage(senderRow.rectTransform, "Avatar", AmazonOrange);
        avatar.sprite = TryGetCircleSprite(); avatar.preserveAspect = true;
        var avrt = avatar.rectTransform;
        avrt.anchorMin = avrt.anchorMax = new Vector2(0, 0.5f); avrt.pivot = new Vector2(0, 0.5f);
        avrt.sizeDelta = new Vector2(42, 42); avrt.anchoredPosition = new Vector2(14, 0);
        avatar.raycastTarget = false;
        var avLtr = AddText(avatar.rectTransform, "Letter", "A", 22, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(avLtr.rectTransform); avLtr.raycastTarget = false;

        var nameT = AddText(senderRow.rectTransform, "SenderName",
            isScam ? "Amaz0n Security" : "Amazon",
            19, BodyText, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        var ntrt = nameT.rectTransform;
        ntrt.anchorMin = new Vector2(0, 0.5f); ntrt.anchorMax = new Vector2(1, 1);
        ntrt.pivot = new Vector2(0, 0.5f); ntrt.sizeDelta = new Vector2(-70, 0);
        ntrt.anchoredPosition = new Vector2(68, -2); nameT.raycastTarget = false;

        var emailT = AddText(senderRow.rectTransform, "SenderEmail",
            isScam ? "<security@amaz0n-shipping.net>" : "<ship-confirm@amazon.com>",
            14, MutedText, TextAlignmentOptions.MidlineLeft);
        var etrt = emailT.rectTransform;
        etrt.anchorMin = new Vector2(0, 0); etrt.anchorMax = new Vector2(1, 0.5f);
        etrt.pivot = new Vector2(0, 0.5f); etrt.sizeDelta = new Vector2(-70, 0);
        etrt.anchoredPosition = new Vector2(68, 2); emailT.raycastTarget = false;

        var tsT = AddText(senderRow.rectTransform, "Timestamp", "May 5, 4:03 PM",
            13, MutedText, TextAlignmentOptions.MidlineRight);
        var tsrt = tsT.rectTransform;
        tsrt.anchorMin = new Vector2(1, 0); tsrt.anchorMax = new Vector2(1, 1);
        tsrt.pivot = new Vector2(1, 0.5f); tsrt.sizeDelta = new Vector2(160, 0);
        tsrt.anchoredPosition = new Vector2(-12, 0); tsT.raycastTarget = false;

        if (isScam)
            AddMarkerWithCircle(senderRow.gameObject, manager,
                "Spoofed sender domain",
                "'amaz0n-shipping.net' uses a zero instead of 'o'. Real Amazon emails come from @amazon.com.");

        var div = AddImage(face.rectTransform, "HeaderDivider", DividerColor);
        var drt = div.rectTransform;
        drt.anchorMin = new Vector2(0, 1); drt.anchorMax = new Vector2(1, 1);
        drt.pivot = new Vector2(0.5f, 1); drt.sizeDelta = new Vector2(-24, 1);
        drt.anchoredPosition = new Vector2(0, -154); div.raycastTarget = false;

        float headerH = 155f;
        var scrollGo = NewGO("BodyScroll", face.rectTransform);
        var scrollRT = scrollGo.GetComponent<RectTransform>();
        scrollRT.anchorMin = Vector2.zero; scrollRT.anchorMax = Vector2.one;
        scrollRT.offsetMin = Vector2.zero; scrollRT.offsetMax = new Vector2(0, -headerH);
        var scrollImg = scrollGo.AddComponent<Image>(); scrollImg.color = PanelBg; scrollImg.raycastTarget = false;
        var sr = scrollGo.AddComponent<ScrollRect>();
        sr.horizontal = false; sr.vertical = true; sr.scrollSensitivity = 35;
        sr.movementType = ScrollRect.MovementType.Clamped;

        var vpGo = NewGO("Viewport", scrollRT); Stretch(vpGo.GetComponent<RectTransform>());
        var vpImg = vpGo.AddComponent<Image>(); vpImg.color = PanelBg; vpImg.raycastTarget = isScam;
        if (isScam) { var recv2 = vpGo.AddComponent<PanelClickReceiver>(); recv2.manager = manager; }
        vpGo.AddComponent<Mask>().showMaskGraphic = false;

        var contentGo = NewGO("BodyContent", vpGo.transform);
        var contentRT = contentGo.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1); contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(0.5f, 1); contentRT.sizeDelta = Vector2.zero;
        contentRT.anchoredPosition = Vector2.zero;
        var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.spacing = 5; vlg.padding = new RectOffset(18, 18, 16, 24);
        contentGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sr.viewport = vpGo.GetComponent<RectTransform>(); sr.content = contentRT;

        BuildEmailBody(contentGo.transform, manager, isScam);
    }

    // =================================================================
    // Email body (6 markers on scam side)
    // =================================================================

    static void BuildEmailBody(Transform content, SpotDifferenceManager manager, bool isScam)
    {
        var greet = BodyRow(content, "Greeting", isScam ? "Dear Valued Customer," : "Hi John,", 22, BodyText, FontStyles.Normal);
        if (isScam) AddMarkerWithCircle(greet, manager, "Generic greeting",
            "'Dear Valued Customer' — scammers don't know your name. Real Amazon emails always use your actual name.");
        Spacer(content, 6);

        if (!isScam)
            BodyRow(content, "OpenPara", "Great news! Your recent order has shipped and is on its way. We wanted to give you a quick update on your delivery.", 22, BodyText, FontStyles.Normal);
        else
        {
            var b1 = BodyRow(content, "OpenPara", "We have detected suspicious activity on your account and have temporarily suspended your access. You must verify your identity immediately.", 22, BodyText, FontStyles.Normal);
            AddMarkerWithCircle(b1, manager, "Urgency — 'immediately'",
                "'You must verify your identity immediately' — scammers use panic words like 'immediately' to stop you thinking clearly.");
        }
        Spacer(content, 8);

        BodyRow(content, "OrderLbl", "Order Summary:", 18, MutedText, FontStyles.Bold);
        Spacer(content, 2);
        BodyRow(content, "OrderItem", "    Echo Dot (5th Gen) — Charcoal x 1", 20, BodyText, FontStyles.Normal);
        BodyRow(content, "OrderNum", "    Order #: 113-4567890", 20, MutedText, FontStyles.Italic);
        if (!isScam)
        {
            BodyRow(content, "OrderDate", "    Estimated Delivery: Friday, May 10, 2024", 20, MutedText, FontStyles.Italic);
            BodyRow(content, "OrderAddr", "    Shipping to: 142 Maple Street, Edmonton, AB", 20, MutedText, FontStyles.Italic);
        }
        Spacer(content, 10);

        if (!isScam)
            BodyRow(content, "Para2", "You can track your package in real time using the button below. Our delivery partner will send you a separate notification when your order is out for delivery.", 22, BodyText, FontStyles.Normal);
        else
        {
            var para2 = BodyRow(content, "Para2", "To restore your account, click the button below and complete identity verification. Failure to respond within 24 hours will result in permanent account deletion.", 22, BodyText, FontStyles.Normal);
            AddMarkerWithCircle(para2, manager, "Threat — 24-hour deadline",
                "'Failure to respond within 24 hours will result in permanent account deletion' — real companies don't threaten to delete your account via email.");
        }
        Spacer(content, 12);

        var ctaH = NewGO("CtaHolder", content);
        ctaH.AddComponent<Image>().color = new Color(0, 0, 0, 0);
        ctaH.GetComponent<Image>().raycastTarget = false;
        ctaH.AddComponent<LayoutElement>().preferredHeight = 60;
        var ctaBtn = AddImage(ctaH.GetComponent<RectTransform>(), "CtaBtn", isScam ? CtaScam : CtaReal);
        var cbrt = ctaBtn.rectTransform;
        cbrt.anchorMin = cbrt.anchorMax = new Vector2(0.5f, 0.5f);
        cbrt.pivot = new Vector2(0.5f, 0.5f); cbrt.sizeDelta = new Vector2(280, 48);
        ctaBtn.raycastTarget = false;
        var ctaLbl = AddText(ctaBtn.rectTransform, "Label", isScam ? "VERIFY ACCOUNT NOW" : "View Order Details",
            20, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(ctaLbl.rectTransform); ctaLbl.raycastTarget = false;
        if (isScam) AddMarkerWithCircle(ctaH, manager, "Vague CTA button",
            "'VERIFY ACCOUNT NOW' — a vague, pressuring button. Real Amazon buttons say something specific like 'View Order Details'.");
        Spacer(content, 12);

        if (!isScam)
        {
            BodyRow(content, "Para3", "If you have any questions, our customer support team is available 24/7 through the Amazon Help Center.", 22, BodyText, FontStyles.Normal);
            Spacer(content, 8);
            BodyRow(content, "Sig", "Thank you for shopping with Amazon!", 22, BodyText, FontStyles.Normal);
            BodyRow(content, "SigName", "— The Amazon Team", 20, MutedText, FontStyles.Italic);
        }
        else
        {
            BodyRow(content, "Para3", "For verification you will be required to provide your password and full billing information including credit card details. This step cannot be skipped.", 22, BodyText, FontStyles.Normal);
            Spacer(content, 8);
            BodyRow(content, "Sig", "— Amaz0n Security Team", 20, MutedText, FontStyles.Italic);
            BodyRow(content, "SigDept", "Account Protection Division", 18, MutedText, FontStyles.Normal);
        }

        Spacer(content, 12);
        var fdiv = AddImage(content, "FooterDiv", DividerColor);
        (fdiv.GetComponent<LayoutElement>() ?? fdiv.gameObject.AddComponent<LayoutElement>()).preferredHeight = 1;
        fdiv.raycastTarget = false;
        Spacer(content, 8);
        BodyRow(content, "Footer",
            isScam ? "Questions? Contact: support@amaz0n-security.tk"
                   : "Need help? Visit our Help Center  |  Manage your account",
            16, MutedText, FontStyles.Normal);
    }

    // =================================================================
    // SIMULATION — Cage + Diver edition
    // =================================================================

    static void BuildSimulation(RectTransform parent, SpotDifferenceManager manager,
        Sprite spDiver, Sprite spShark, Sprite spCircle)
    {
        // ── Root panel: bottom 27 % of canvas ────────────────────────
        var root = AddImage(parent, "SimulationRoot", SimDeep);
        var rrt = root.rectTransform;
        rrt.anchorMin = new Vector2(0, 0); rrt.anchorMax = new Vector2(1, 0.270f);
        rrt.offsetMin = rrt.offsetMax = Vector2.zero;
        root.raycastTarget = false;

        var mid = AddImage(rrt, "SimMid", SimMid);
        var mrt = mid.rectTransform;
        mrt.anchorMin = new Vector2(0, 0.15f); mrt.anchorMax = Vector2.one;
        mrt.offsetMin = mrt.offsetMax = Vector2.zero;
        mid.raycastTarget = false;

        // Water surface shimmer strip
        var surf = AddImage(rrt, "Surface", new Color(0.15f, 0.40f, 0.65f, 0.38f));
        var srt2 = surf.rectTransform;
        srt2.anchorMin = new Vector2(0, 0.85f); srt2.anchorMax = new Vector2(1, 0.91f);
        srt2.offsetMin = srt2.offsetMax = Vector2.zero;
        surf.raycastTarget = false;

        // ── "DANGER ZONE" label (top-left) ───────────────────────────
        var dangerLbl = AddText(rrt, "DangerLabel", "DANGER ZONE", 14,
            new Color(0.9f, 0.2f, 0.2f, 0.65f), TextAlignmentOptions.Center, FontStyles.Bold);
        var dlrt = dangerLbl.rectTransform;
        dlrt.anchorMin = new Vector2(0, 0.82f); dlrt.anchorMax = new Vector2(0.20f, 1f);
        dlrt.offsetMin = dlrt.offsetMax = Vector2.zero;
        dangerLbl.raycastTarget = false;

        // ── SHARK (centre-left, nx ≈ 0.30) ───────────────────────────
        var sharkImg = AddImage(rrt, "Shark", Color.white);
        if (spShark != null) { sharkImg.sprite = spShark; sharkImg.preserveAspect = true; }
        else if (spCircle != null) { sharkImg.sprite = spCircle; sharkImg.color = new Color(0.25f, 0.32f, 0.42f); }
        var sharkRT = sharkImg.rectTransform;
        sharkRT.anchorMin = sharkRT.anchorMax = new Vector2(0.5f, 0.5f);
        sharkRT.pivot = new Vector2(0.5f, 0.5f);
        sharkRT.sizeDelta = new Vector2(100f, 68f);
        sharkRT.anchoredPosition = Vector2.zero;           // builder sets runtime position
        sharkRT.localScale = new Vector3(-1f, 1f, 1f);       // face right
        sharkImg.raycastTarget = false;

        // ── DIVER / Phisherman (right side, nx ≈ 0.78) ───────────────
        var diverImg = AddImage(rrt, "Diver", Color.white);
        if (spDiver != null) { diverImg.sprite = spDiver; diverImg.preserveAspect = true; }
        else { diverImg.color = new Color(0.20f, 0.55f, 0.85f); }
        var diverRT = diverImg.rectTransform;
        diverRT.anchorMin = diverRT.anchorMax = new Vector2(0.5f, 0.5f);
        diverRT.pivot = new Vector2(0.5f, 0.5f);
        diverRT.sizeDelta = new Vector2(80f, 90f);
        diverRT.anchoredPosition = Vector2.zero;           // runtime
        diverImg.raycastTarget = false;

        // Bubble label above diver
        var bubbleLbl = AddText(rrt, "DiverBubble", "Find the flags!",
            13, new Color(0.80f, 0.95f, 1f, 0.90f), TextAlignmentOptions.Center, FontStyles.Italic);
        var blrt = bubbleLbl.rectTransform;
        blrt.anchorMin = new Vector2(0.65f, 0.70f); blrt.anchorMax = new Vector2(0.95f, 0.92f);
        blrt.offsetMin = blrt.offsetMax = Vector2.zero;
        bubbleLbl.raycastTarget = false;

        // ── CAGE GROUP ────────────────────────────────────────────────
        // We build the cage as a group of child RectTransforms under a pivot GO.
        // The LureSimulation script animates cageGO as a whole (drop + lift).
        var cageGO = new GameObject("CageGroup", typeof(RectTransform));
        cageGO.transform.SetParent(rrt, false);
        var cageGroupRT = cageGO.GetComponent<RectTransform>();
        // Cage size: 130 × 120 px in panel space
        cageGroupRT.anchorMin = cageGroupRT.anchorMax = new Vector2(0.5f, 0.5f);
        cageGroupRT.pivot = new Vector2(0.5f, 0f);     // pivot at bottom so "lift" raises from bottom
        cageGroupRT.sizeDelta = new Vector2(130f, 120f);
        cageGroupRT.anchoredPosition = Vector2.zero;  // runtime

        // Shadow under cage
        var shadowImg = AddImage(cageGroupRT, "CageShadow", CageShadowC);
        var shadowRT = shadowImg.rectTransform;
        shadowRT.anchorMin = new Vector2(0.05f, -0.06f); shadowRT.anchorMax = new Vector2(0.95f, 0.04f);
        shadowRT.offsetMin = shadowRT.offsetMax = Vector2.zero;
        shadowImg.sprite = spCircle; shadowImg.raycastTarget = false;

        // Cage base (floor)
        var cageBaseImg = AddImage(cageGroupRT, "CageBase", CageBaseColor);
        var baseRT = cageBaseImg.rectTransform;
        baseRT.anchorMin = new Vector2(0, 0); baseRT.anchorMax = new Vector2(1, 0.08f);
        baseRT.offsetMin = baseRT.offsetMax = Vector2.zero;
        cageBaseImg.raycastTarget = false;

        // Left vertical bar
        var leftBar = BuildCageVertBar(cageGroupRT, "BarLeft", 0.05f, spCircle);
        // Right vertical bar
        var rightBar = BuildCageVertBar(cageGroupRT, "BarRight", 0.88f, spCircle);

        // 3 horizontal cross-bars (bottom → top)
        float[] barYMin = { 0.22f, 0.52f, 0.82f };
        float[] barYMax = { 0.30f, 0.60f, 0.90f };
        var cageBars = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            var barImg = AddImage(cageGroupRT, "HBar_" + i, CageBarColor);
            var brt2 = barImg.rectTransform;
            brt2.anchorMin = new Vector2(0.00f, barYMin[i]);
            brt2.anchorMax = new Vector2(1.00f, barYMax[i]);
            brt2.offsetMin = brt2.offsetMax = Vector2.zero;
            barImg.raycastTarget = false;
            cageBars[i] = barImg;
        }

        // Cage top cap
        var topCap = AddImage(cageGroupRT, "CageTop", CageBarColor);
        var topCapRT = topCap.rectTransform;
        topCapRT.anchorMin = new Vector2(0, 0.92f); topCapRT.anchorMax = new Vector2(1, 1f);
        topCapRT.offsetMin = topCapRT.offsetMax = Vector2.zero;
        topCap.raycastTarget = false;

        // Chain / rope above cage top
        var chainGO = new GameObject("Chain", typeof(RectTransform));
        chainGO.transform.SetParent(cageGroupRT, false);
        var chainRT = chainGO.GetComponent<RectTransform>();
        chainRT.anchorMin = new Vector2(0.42f, 1.0f); chainRT.anchorMax = new Vector2(0.58f, 1.0f);
        chainRT.pivot = new Vector2(0.5f, 0f); chainRT.sizeDelta = new Vector2(8f, 60f);
        var chainImg = chainGO.AddComponent<Image>();
        chainImg.color = CageBarColor; chainImg.raycastTarget = false;

        // ── Wire LureSimulation component ────────────────────────────
        var simComp = root.gameObject.AddComponent<LureSimulation>();
        simComp.panelRT = rrt;
        simComp.sharkRT = sharkRT;
        simComp.sharkImage = sharkImg;
        simComp.cageRT = cageGroupRT;
        simComp.cageBars = cageBars;
        simComp.cageBase = cageBaseImg;
        simComp.cageShadow = shadowImg;
        simComp.diverRT = diverRT;
        simComp.diverImage = diverImg;
        simComp.totalTime = 60f;

        // Legacy fields kept for compile compatibility (unused in cage edition)
        simComp.rodLineRT = null;
        simComp.hookRT = null;
        simComp.debrisLayers = new RectTransform[0];
        simComp.debrisImages = new Image[0];

        simComp.onImpact = () => { if (manager.sfxSource && manager.sfxImpact) manager.sfxSource.PlayOneShot(manager.sfxImpact, 0.85f); };
        simComp.onRodWinding = () => { if (manager.sfxSource && manager.sfxRodWinding) manager.sfxSource.PlayOneShot(manager.sfxRodWinding, 0.85f); };

        manager.lureSimulation = simComp;
    }

    /// <summary>Builds a single vertical cage bar.</summary>
    static Image BuildCageVertBar(RectTransform parent, string name, float xMin, Sprite sp)
    {
        var img = AddImage(parent, name, CageBarColor);
        var rt = img.rectTransform;
        rt.anchorMin = new Vector2(xMin, 0.08f);
        rt.anchorMax = new Vector2(xMin + 0.07f, 1.00f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        img.raycastTarget = false;
        return img;
    }

    // =================================================================
    // Magnifying glass
    // =================================================================

    static void BuildMagnifyingGlass(RectTransform parent, SpotDifferenceManager manager,
        Sprite spMagGlass, Sprite spCircle)
    {
        var go = NewGO("MagnifyingGlass", parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(120f, 120f);
        rt.anchoredPosition = Vector2.zero;

        var ringImg = go.AddComponent<Image>();
        ringImg.sprite = spCircle; ringImg.color = new Color(0, 0, 0, 0);
        ringImg.preserveAspect = true; ringImg.raycastTarget = false;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0.85f, 0.70f, 0.30f, 1.0f);
        outline.effectDistance = new Vector2(5, -5);

        var maskGo = NewGO("MagMask", rt);
        var maskRT = maskGo.GetComponent<RectTransform>();
        maskRT.anchorMin = Vector2.zero; maskRT.anchorMax = Vector2.one;
        maskRT.offsetMin = new Vector2(5, 5); maskRT.offsetMax = new Vector2(-5, -5);
        var maskImg = maskGo.AddComponent<Image>();
        maskImg.sprite = spCircle; maskImg.color = Color.white; maskImg.raycastTarget = false;
        maskGo.AddComponent<Mask>().showMaskGraphic = false;

        var handleGo = NewGO("MagHandle", rt);
        var handleRT = handleGo.GetComponent<RectTransform>();
        handleRT.anchorMin = handleRT.anchorMax = new Vector2(1f, 0f);
        handleRT.pivot = new Vector2(0f, 1f);
        handleRT.sizeDelta = new Vector2(8f, 36f); handleRT.anchoredPosition = new Vector2(4f, -4f);
        var handleImg = handleGo.AddComponent<Image>();
        handleImg.color = Hex("#8B6914"); handleImg.raycastTarget = false;
        handleRT.localRotation = Quaternion.Euler(0, 0, 38f);

        var zoom = go.AddComponent<MagnifierZoom>();
        zoom.mainCanvasRT = manager.mainCanvasRT;
        zoom.magnifierRT = rt;
        zoom.maskGo = maskGo;
        zoom.zoomScale = 2.2f;

        go.SetActive(false);
        manager.magnifyingGlassRT = rt;
    }

    // =================================================================
    // Result panel
    // =================================================================

    static GameObject BuildResultPanel(RectTransform parent, SpotDifferenceManager manager)
    {
        var overlay = AddImage(parent, "ResultOverlay", OverlayColor);
        Stretch(overlay.rectTransform); overlay.raycastTarget = true;

        var panel = AddImage(overlay.rectTransform, "ResultPanel", FolderInner);
        var prt = panel.rectTransform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot = new Vector2(0.5f, 0.5f); prt.sizeDelta = new Vector2(1100, 800);

        var pBrd = AddImage(prt, "PanelBorder", FolderOuter); Stretch(pBrd.rectTransform); pBrd.raycastTarget = false;
        var pFace = AddImage(prt, "PanelFace", FolderInner);
        var pfRT = pFace.rectTransform;
        pfRT.anchorMin = Vector2.zero; pfRT.anchorMax = Vector2.one;
        pfRT.offsetMin = new Vector2(3, 3); pfRT.offsetMax = new Vector2(-3, -3);
        pFace.raycastTarget = false;

        var header = AddImage(pFace.rectTransform, "Header", HudDark);
        AnchorTopStretch(header.rectTransform, 90);
        var hLabel = AddText(header.rectTransform, "Title", "Case closed!",
            42, HudGold, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(hLabel.rectTransform); hLabel.raycastTarget = false;

        var breakdown = AddText(pFace.rectTransform, "Breakdown", string.Empty,
            20, BodyText, TextAlignmentOptions.TopLeft);
        breakdown.textWrappingMode = TextWrappingModes.Normal;
        var brt = breakdown.rectTransform;
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
        brt.offsetMin = new Vector2(50, 180); brt.offsetMax = new Vector2(-50, -120);

        var scoreText = AddText(pFace.rectTransform, "Score", "Final Score: 0",
            34, BodyText, TextAlignmentOptions.Center, FontStyles.Bold);
        var srt = scoreText.rectTransform;
        srt.anchorMin = new Vector2(0, 0); srt.anchorMax = new Vector2(1, 0);
        srt.pivot = new Vector2(0.5f, 0); srt.sizeDelta = new Vector2(-80, 52);
        srt.anchoredPosition = new Vector2(0, 110);

        var playAgain = AddButton(pFace.rectTransform, "PlayAgainBtn", "Play Again", 26, Hex("#2ECC71"), Color.white);
        var part = playAgain.GetComponent<RectTransform>();
        part.anchorMin = new Vector2(0.5f, 0); part.anchorMax = new Vector2(0.5f, 0);
        part.pivot = new Vector2(1f, 0); part.sizeDelta = new Vector2(240, 62);
        part.anchoredPosition = new Vector2(-16, 28);
        playAgain.GetComponent<Button>().onClick.AddListener(manager.PlayAgain);

        var menuBtn = AddButton(pFace.rectTransform, "MenuBtn", "Back to Map", 26, Hex("#7F8C8D"), Color.white);
        var mrt2 = menuBtn.GetComponent<RectTransform>();
        mrt2.anchorMin = new Vector2(0.5f, 0); mrt2.anchorMax = new Vector2(0.5f, 0);
        mrt2.pivot = new Vector2(0f, 0); mrt2.sizeDelta = new Vector2(240, 62);
        mrt2.anchoredPosition = new Vector2(16, 28);
        menuBtn.GetComponent<Button>().onClick.AddListener(manager.BackToWorldMap);

        manager.resultTitle = hLabel;
        manager.resultBreakdown = breakdown;
        manager.resultScore = scoreText;
        overlay.gameObject.SetActive(false);
        return overlay.gameObject;
    }

    // =================================================================
    // Commentator
    // =================================================================

    static Commentator BuildCommentator(RectTransform parent)
    {
        var root = NewGO("Commentator", parent);
        var rrt = root.GetComponent<RectTransform>();
        rrt.anchorMin = rrt.anchorMax = Vector2.zero; rrt.pivot = Vector2.zero;
        rrt.sizeDelta = new Vector2(560, 120); rrt.anchoredPosition = new Vector2(24, 24);

        var portrait = AddImage(rrt, "Portrait", new Color(1f, 0.72f, 0.78f));
        portrait.sprite = TryGetCircleSprite(); portrait.preserveAspect = true;
        var prt = portrait.rectTransform;
        prt.anchorMin = new Vector2(0, 0); prt.anchorMax = new Vector2(0, 1);
        prt.pivot = new Vector2(0, 0.5f); prt.sizeDelta = new Vector2(100, 0);
        var letter = AddText(portrait.rectTransform, "Letter", "G", 54,
            new Color(0.2f, 0.1f, 0.18f), TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(letter.rectTransform);

        var bubble = AddImage(rrt, "Bubble", Color.white);
        var brt = bubble.rectTransform;
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
        brt.offsetMin = new Vector2(115, 0); brt.offsetMax = Vector2.zero;

        var spkr = AddText(bubble.rectTransform, "Speaker", "Grandma", 15,
            new Color(0.78f, 0.30f, 0.50f), TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        var slrt = spkr.rectTransform;
        slrt.anchorMin = new Vector2(0, 1); slrt.anchorMax = new Vector2(1, 1);
        slrt.pivot = new Vector2(0, 1); slrt.sizeDelta = new Vector2(0, 22);
        slrt.anchoredPosition = new Vector2(16, -5);

        var btxt = AddText(bubble.rectTransform, "BubbleText", "...", 19,
            new Color(0.13f, 0.13f, 0.13f), TextAlignmentOptions.MidlineLeft);
        btxt.textWrappingMode = TextWrappingModes.Normal;
        var btrt = btxt.rectTransform;
        btrt.anchorMin = Vector2.zero; btrt.anchorMax = Vector2.one;
        btrt.offsetMin = new Vector2(16, 5); btrt.offsetMax = new Vector2(-16, -26);

        var comm = root.AddComponent<Commentator>();
        comm.root = root; comm.portrait = portrait; comm.portraitLetter = letter;
        comm.bubbleBg = bubble; comm.bubbleText = btxt; comm.speakerLabel = spkr;
        comm.portraitSprite = TryGetCircleSprite();
        comm.speakerName = "Grandma"; comm.portraitInitial = "G";
        comm.portraitColor = new Color(1f, 0.72f, 0.78f);
        return comm;
    }

    // =================================================================
    // AddMarkerWithCircle
    // =================================================================

    static void AddMarkerWithCircle(GameObject parent, SpotDifferenceManager manager,
        string flagName, string explanation)
    {
        var go = NewGO("Marker_" + Sanitize(flagName), parent.transform);
        var rt = go.GetComponent<RectTransform>();
        Stretch(rt);
        rt.offsetMin = new Vector2(-12, -12);
        rt.offsetMax = new Vector2(12, 12);

        var img = go.AddComponent<Image>(); img.color = new Color(1, 0, 0, 0); img.raycastTarget = true;
        var marker = go.AddComponent<DifferenceMarker>();
        marker.flagName = flagName;
        marker.explanation = explanation;
        marker.manager = manager;

        var circGo = NewGO("PenCircle", go.transform);
        var circRT = circGo.GetComponent<RectTransform>();
        Stretch(circRT);
        circRT.offsetMin = new Vector2(-4, -4); circRT.offsetMax = new Vector2(4, 4);
        var circImg = circGo.AddComponent<Image>();
        circImg.sprite = TryGetCircleSprite();
        circImg.fillMethod = Image.FillMethod.Radial360;
        circImg.fillAmount = 1f;
        circImg.type = Image.Type.Filled;
        circImg.color = new Color(PenRed.r, PenRed.g, PenRed.b, 0.0f);
        circImg.raycastTarget = false;
        var outline = circGo.AddComponent<Outline>();
        outline.effectColor = new Color(PenRed.r, PenRed.g, PenRed.b, 0.90f);
        outline.effectDistance = new Vector2(3, 3);
        circGo.SetActive(false);
        marker.penCircleImage = circImg;

        manager.markers.Add(marker);
    }

    static void AddMarkerWithCircle(Transform parent, SpotDifferenceManager manager,
        string flagName, string explanation)
        => AddMarkerWithCircle(parent.gameObject, manager, flagName, explanation);

    // =================================================================
    // Build settings helpers
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

    static Sprite FindSprite(string name)
    {
        foreach (var g in AssetDatabase.FindAssets(name + " t:Sprite"))
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            if (System.IO.Path.GetFileNameWithoutExtension(path).ToLower() == name.ToLower())
            { var s = AssetDatabase.LoadAssetAtPath<Sprite>(path); if (s != null) return s; }
        }
        return null;
    }

    static AudioClip FindAudioClip(string name)
    {
        foreach (var g in AssetDatabase.FindAssets(name + " t:AudioClip"))
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            if (System.IO.Path.GetFileNameWithoutExtension(path).ToLower() == name.ToLower())
            { var c = AssetDatabase.LoadAssetAtPath<AudioClip>(path); if (c != null) return c; }
        }
        return null;
    }

    static void LogFound(string n, Sprite s) =>
        Debug.Log($"[SpotDiffBuilder] {n}: " + (s != null ? "found" : "not found"));

    // =================================================================
    // Body row helpers
    // =================================================================

    static GameObject BodyRow(Transform parent, string name, string text,
        int fs, Color col, FontStyles style)
    {
        var go = NewGO(name, parent); var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = fs; t.color = col; t.fontStyle = style;
        t.alignment = TextAlignmentOptions.TopLeft;
        t.textWrappingMode = TextWrappingModes.Normal; t.raycastTarget = false;
        go.AddComponent<LayoutElement>().flexibleWidth = 1; return go;
    }

    static void Spacer(Transform parent, float h)
    {
        var go = NewGO("Spacer", parent);
        var le = go.AddComponent<LayoutElement>(); le.preferredHeight = h; le.flexibleWidth = 1;
    }

    // =================================================================
    // Low-level helpers
    // =================================================================

    static Image AddImage(Transform p, string n, Color c)
    { var go = NewGO(n, p); var img = go.AddComponent<Image>(); img.color = c; return img; }

    static TMP_Text AddText(Transform p, string n, string content, int fs, Color col,
        TextAlignmentOptions align, FontStyles style = FontStyles.Normal)
    {
        var go = NewGO(n, p); var t = go.AddComponent<TextMeshProUGUI>();
        t.text = content; t.fontSize = fs; t.color = col; t.alignment = align;
        t.fontStyle = style; t.raycastTarget = false; return t;
    }

    static GameObject AddButton(Transform p, string n, string lbl, int fs, Color bg, Color tc)
    {
        var go = NewGO(n, p); var img = go.AddComponent<Image>(); img.color = bg;
        var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        var t = AddText(go.transform, "Label", lbl, fs, tc, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(t.rectTransform); return go;
    }

    static GameObject NewGO(string n, Transform p)
    { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); return go; }

    static void Stretch(RectTransform r)
    { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }

    static void AnchorTopStretch(RectTransform r, float h, float inset = 0)
    {
        r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1);
        r.pivot = new Vector2(0.5f, 1); r.sizeDelta = new Vector2(0, h);
        r.anchoredPosition = new Vector2(0, -inset);
    }

    static void AnchorRect(RectTransform r, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
    { r.anchorMin = aMin; r.anchorMax = aMax; r.offsetMin = oMin; r.offsetMax = oMax; }

    static Color Hex(string h) => ColorUtility.TryParseHtmlString(h, out var c) ? c : Color.magenta;
    static string Sanitize(string s) => new string(s.Where(c => char.IsLetterOrDigit(c)).ToArray());
    static Sprite TryGetCircleSprite()
    { try { return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"); } catch { return null; } }
}