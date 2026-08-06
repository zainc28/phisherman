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
/// FIX: Shark/diver/cage anchors at (0.5,0.5) so NormToLocal works.
/// FIX: Viewport raycastTarget always true so both pages can scroll.
/// FIX: Marker hit zones reduced from ±12 to ±4 so adjacent found
///      markers don't swallow clicks meant for unfound neighbours.
/// FIX: Cage size increased 50%.
/// FIX: force an immediate layout rebuild after building each email's
///      scroll content (see BuildEmailPanel) — without this, the
///      ContentSizeFitter can still hold a stale/undersized content
///      height when the ScrollRect first measures it, making that
///      panel appear non-scrollable even though it has overflow.
/// </summary>
public static class SpotDifferenceBuilder
{
    private const string ScenesDir = "Assets/Scenes";
    private const string ScenePath = "Assets/Scenes/SpotDifference.unity";

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
    private static readonly Color SimDeep = Hex("#071824");
    private static readonly Color SimMid = Hex("#0A2840");
    private static readonly Color SimHintBg = new Color(0.18f, 0.28f, 0.40f, 0.85f);
    private static readonly Color PenRed = Hex("#C0392B");
    private static readonly Color OverlayColor = new Color(0f, 0f, 0f, 0.72f);
    private static readonly Color HeartRed = new Color(0.75f, 0.18f, 0.18f);

    [MenuItem("Phisherman/Build Spot the Difference Scene")]
    public static void Build()
    {
        if (!Directory.Exists(ScenesDir)) Directory.CreateDirectory(ScenesDir);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Sprite spCircle = TryGetCircleSprite();

        Sprite spDiverNormal = FindSprite("phisherman_underwater");
        Sprite spDiverPointing = FindSprite("phisherman_underwater_pointing");
        Sprite spDiverScared = FindSprite("phisherman_underwater_slightly_scared");
        if (spDiverNormal == null) spDiverNormal = FindSprite("phisherman");
        if (spDiverPointing == null) spDiverPointing = spDiverNormal;
        if (spDiverScared == null) spDiverScared = spDiverNormal;

        Sprite spCage = FindSprite("cage");
        Sprite spCaged1 = FindSprite("cage_damaged_1");
        Sprite spCaged2 = FindSprite("cage_damaged_2");
        Sprite spCaged3 = FindSprite("cage_damaged_3");

        Sprite spMagGlass = FindSprite("magnifying_glass");
        Sprite spShark = FindSprite("shark");
        Sprite spHeart = FindSprite("heart");

        LogFound("phisherman_underwater", spDiverNormal);
        LogFound("phisherman_underwater_pointing", spDiverPointing);
        LogFound("phisherman_underwater_slightly_scared", spDiverScared);
        LogFound("cage", spCage);
        LogFound("cage_damaged_1", spCaged1);
        LogFound("cage_damaged_2", spCaged2);
        LogFound("cage_damaged_3", spCaged3);
        LogFound("shark", spShark);
        LogFound("heart", spHeart);

        var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>(); camGo.AddComponent<AudioListener>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Hex("#1A140E");
        cam.orthographic = true;

        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>(); es.AddComponent<StandaloneInputModule>();

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
        manager.heartSprite = spHeart;
        manager.sfxSource = sfxSrc;
        manager.sfxSplash = FindAudioClip("water_splash");
        manager.sfxWrong = FindAudioClip("glass_crack");
        manager.sfxImpact = FindAudioClip("impact");
        manager.sfxRodWinding = FindAudioClip("fishing_rod_winding");

        var bgImg = AddImage(canvasRT, "Background", HudDark);
        Stretch(bgImg.rectTransform); bgImg.raycastTarget = false;

        BuildHud(canvasRT, manager, spDiverNormal, spMagGlass, spCircle, spHeart);
        BuildFolder(canvasRT, manager);

        var simHintBg = AddImage(canvasRT, "SimHintBg", SimHintBg);
        var shrt = simHintBg.rectTransform;
        shrt.anchorMin = new Vector2(0, 0.27f); shrt.anchorMax = new Vector2(1, 0.29f);
        shrt.offsetMin = shrt.offsetMax = Vector2.zero; simHintBg.raycastTarget = false;
        var simHintTxt = AddText(simHintBg.rectTransform, "SimHintText",
            "Find all 6 red flags before the shark escapes its cage!",
            19, new Color(0.90f, 0.90f, 1f), TextAlignmentOptions.Center, FontStyles.Italic);
        Stretch(simHintTxt.rectTransform); simHintTxt.raycastTarget = false;

        BuildSimulation(canvasRT, manager,
            spDiverNormal, spDiverPointing, spDiverScared,
            spShark,
            spCage, spCaged1, spCaged2, spCaged3,
            spCircle);

        var effectLayerGo = new GameObject("EffectLayer", typeof(RectTransform));
        effectLayerGo.transform.SetParent(canvasRT, false);
        Stretch(effectLayerGo.GetComponent<RectTransform>());
        effectLayerGo.transform.SetAsLastSibling();
        var eImg = effectLayerGo.AddComponent<Image>();
        eImg.color = new Color(0, 0, 0, 0); eImg.raycastTarget = false;
        manager.effectLayer = effectLayerGo.GetComponent<RectTransform>();

        var feedback = AddText(canvasRT, "Feedback", string.Empty,
            38, Color.green, TextAlignmentOptions.Center, FontStyles.Bold);
        var fbRT = feedback.rectTransform;
        fbRT.anchorMin = new Vector2(0.5f, 1); fbRT.anchorMax = new Vector2(0.5f, 1);
        fbRT.pivot = new Vector2(0.5f, 1); fbRT.sizeDelta = new Vector2(1100, 56);
        fbRT.anchoredPosition = new Vector2(0, -118); feedback.raycastTarget = false;

        var resultPanel = BuildResultPanel(canvasRT, manager);

        manager.commentator = BuildHiddenCommentator(canvasRT);
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
        Sprite spPhisherman, Sprite spMagGlass, Sprite spCircle, Sprite spHeart)
    {
        Sprite spHourglass = FindSprite("hourglass");

        var hud = AddImage(parent, "HUD", HudDark);
        AnchorTopStretch(hud.rectTransform, 100);
        hud.raycastTarget = false;

        var border = AddImage(hud.rectTransform, "HudBorder", HudBorder);
        var brt = border.rectTransform;
        brt.anchorMin = Vector2.zero; brt.anchorMax = new Vector2(1, 0);
        brt.pivot = new Vector2(0.5f, 0); brt.sizeDelta = new Vector2(0, 2);
        border.raycastTarget = false;

        var heartsH = NewGO("HeartsHolder", hud.rectTransform);
        var heartsHRT = heartsH.GetComponent<RectTransform>();
        AnchorRect(heartsHRT, new Vector2(0f, 0), new Vector2(0.14f, 1),
            new Vector2(18, 0), Vector2.zero);
        var hlg = heartsH.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleLeft; hlg.spacing = 8;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;
        hlg.childControlWidth = hlg.childControlHeight = false;
        hlg.padding = new RectOffset(0, 0, 18, 18);
        var heartImages = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            var hgo = NewGO("Heart_" + i, heartsH.transform);
            hgo.GetComponent<RectTransform>().sizeDelta = new Vector2(44, 44);
            var hi = hgo.AddComponent<Image>();
            if (spHeart != null) { hi.sprite = spHeart; hi.color = Color.white; }
            else { hi.sprite = spCircle; hi.color = HeartRed; }
            hi.preserveAspect = true; hi.raycastTarget = false;
            heartImages[i] = hi;
        }
        manager.heartImages = heartImages;

        var timerBox = AddImage(hud.rectTransform, "TimerBox", new Color(0.10f, 0.07f, 0.03f, 1f));
        AnchorRect(timerBox.rectTransform, new Vector2(0.14f, 0), new Vector2(0.35f, 1),
            new Vector2(8, 8), new Vector2(-8, -8));
        timerBox.raycastTarget = false;

        if (spHourglass != null)
        {
            var hgGo = NewGO("HourglassIcon", timerBox.rectTransform);
            var hgRT = hgGo.GetComponent<RectTransform>();
            hgRT.anchorMin = new Vector2(0, 0.5f); hgRT.anchorMax = new Vector2(0, 0.5f);
            hgRT.pivot = new Vector2(0, 0.5f);
            hgRT.sizeDelta = new Vector2(38, 38); hgRT.anchoredPosition = new Vector2(10, 0);
            var hgImg = hgGo.AddComponent<Image>();
            hgImg.sprite = spHourglass; hgImg.color = HudGold;
            hgImg.preserveAspect = true; hgImg.raycastTarget = false;
        }

        var timerTxt = AddText(timerBox.rectTransform, "Timer", "1:00",
            40, HudGold, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(timerTxt.rectTransform);

        var counter = AddText(hud.rectTransform, "Counter", "Found: 0 / 6",
            30, HudGold, TextAlignmentOptions.Center, FontStyles.Bold);
        AnchorRect(counter.rectTransform, new Vector2(0.35f, 0), new Vector2(0.65f, 1),
            Vector2.zero, Vector2.zero);

        var score = AddText(hud.rectTransform, "Score", "Score: 0",
            30, HudGold, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
        AnchorRect(score.rectTransform, new Vector2(0.65f, 0), new Vector2(1f, 1),
            Vector2.zero, new Vector2(-22, 0));

        manager.timerText = timerTxt;
        manager.counterText = counter;
        manager.scoreText = score;
        manager.magToggleButton = null;
        manager.magToggleLabel = null;
        manager.phishermanNoMagGlass = null;
        manager.phishermanWithMagGlass = null;
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

        var row = NewGO("EvidenceRow", rt); Stretch(row.GetComponent<RectTransform>());

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

        manager.evidenceLogText = null;
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

        BuildEmailPanel(inner.rectTransform, manager, isScam: false,
            new Vector2(0.01f, 0.01f), new Vector2(0.492f, 0.99f));
        BuildEmailPanel(inner.rectTransform, manager, isScam: true,
            new Vector2(0.508f, 0.01f), new Vector2(0.99f, 0.99f));
    }

    // =================================================================
    // Email panel — FIX: viewport raycastTarget always true for scroll
    // =================================================================

    static void BuildEmailPanel(RectTransform parent, SpotDifferenceManager manager,
        bool isScam, Vector2 anchorMin, Vector2 anchorMax)
    {
        var panel = AddImage(parent, isScam ? "ScamDoc" : "RealDoc", PanelBg);
        var rt = panel.rectTransform;
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        panel.raycastTarget = false;

        var brd = AddImage(rt, "DocBorder", PanelBorder); Stretch(brd.rectTransform); brd.raycastTarget = false;
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
        srrt.anchoredPosition = new Vector2(0, -96);
        senderRow.raycastTarget = false;  // FIX: was default true, blocking wrong-click on face

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
        var vpImg = vpGo.AddComponent<Image>(); vpImg.color = PanelBg;
        // FIX: always true — needed for ScrollRect drag events on BOTH pages
        vpImg.raycastTarget = true;
        // Only the scam page gets the wrong-click receiver
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

        // FIX: force the VerticalLayoutGroup/ContentSizeFitter to resolve the
        // content's real height right now, before the ScrollRect gets a
        // chance to measure it on its own. Without this, one of the two
        // panels could end up with a stale/zero content size cached by the
        // ScrollRect, which makes it appear un-scrollable even though the
        // text clearly overflows the viewport. Also make sure both panels
        // start scrolled to the top.
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRT);
        sr.verticalNormalizedPosition = 1f;
    }

    // =================================================================
    // Email body
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
    // Simulation — anchors at (0.5,0.5), cage 50% larger
    // =================================================================

    static void BuildSimulation(RectTransform parent, SpotDifferenceManager manager,
        Sprite spDiverNormal, Sprite spDiverPointing, Sprite spDiverScared,
        Sprite spShark,
        Sprite spCage, Sprite spCaged1, Sprite spCaged2, Sprite spCaged3,
        Sprite spCircle)
    {
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

        var surf = AddImage(rrt, "Surface", new Color(0.15f, 0.40f, 0.65f, 0.38f));
        var srt2 = surf.rectTransform;
        srt2.anchorMin = new Vector2(0, 0.85f); srt2.anchorMax = new Vector2(1, 0.91f);
        srt2.offsetMin = srt2.offsetMax = Vector2.zero;
        surf.raycastTarget = false;

        var dangerLbl = AddText(rrt, "DangerLabel", "DANGER ZONE", 14,
            new Color(0.9f, 0.2f, 0.2f, 0.65f), TextAlignmentOptions.Center, FontStyles.Bold);
        var dlrt = dangerLbl.rectTransform;
        dlrt.anchorMin = new Vector2(0, 0.82f); dlrt.anchorMax = new Vector2(0.20f, 1f);
        dlrt.offsetMin = dlrt.offsetMax = Vector2.zero;
        dangerLbl.raycastTarget = false;

        // ── SHARK — anchor at centre ─────────────────────────────────
        var sharkImg = AddImage(rrt, "Shark", Color.white);
        if (spShark != null) { sharkImg.sprite = spShark; sharkImg.preserveAspect = true; }
        else if (spCircle != null) { sharkImg.sprite = spCircle; sharkImg.color = new Color(0.25f, 0.32f, 0.42f); }
        var sharkRT = sharkImg.rectTransform;
        sharkRT.anchorMin = sharkRT.anchorMax = new Vector2(0.5f, 0.5f);
        sharkRT.pivot = new Vector2(0.5f, 0.5f);
        sharkRT.sizeDelta = new Vector2(120f, 80f);
        sharkRT.anchoredPosition = Vector2.zero;
        sharkRT.localScale = new Vector3(-1f, 1f, 1f);
        sharkImg.raycastTarget = false;

        // ── DIVER — anchor at centre ─────────────────────────────────
        var diverImg = AddImage(rrt, "Diver", Color.white);
        if (spDiverPointing != null) { diverImg.sprite = spDiverPointing; diverImg.preserveAspect = true; }
        else if (spDiverNormal != null) { diverImg.sprite = spDiverNormal; diverImg.preserveAspect = true; }
        else { diverImg.color = new Color(0.20f, 0.55f, 0.85f); }
        var diverRT = diverImg.rectTransform;
        diverRT.anchorMin = diverRT.anchorMax = new Vector2(0.5f, 0.5f);
        diverRT.pivot = new Vector2(0.5f, 0.5f);
        diverRT.sizeDelta = new Vector2(80f, 90f);
        diverRT.anchoredPosition = Vector2.zero;
        diverImg.raycastTarget = false;
        diverRT.localScale = new Vector3(-1f, 1f, 1f);

        var bubbleLbl = AddText(rrt, "DiverBubble", "Find the flags!",
            13, new Color(0.80f, 0.95f, 1f, 0.90f), TextAlignmentOptions.Center, FontStyles.Italic);
        var blrt = bubbleLbl.rectTransform;
        blrt.anchorMin = new Vector2(0.65f, 0.70f); blrt.anchorMax = new Vector2(0.95f, 0.92f);
        blrt.offsetMin = blrt.offsetMax = Vector2.zero;
        bubbleLbl.raycastTarget = false;

        // ── CAGE — anchor at centre, 50% larger ─────────────────────
        var cageGO = new GameObject("CageGroup", typeof(RectTransform));
        cageGO.transform.SetParent(rrt, false);
        var cageGroupRT = cageGO.GetComponent<RectTransform>();
        cageGroupRT.anchorMin = cageGroupRT.anchorMax = new Vector2(0.5f, 0.5f);
        cageGroupRT.pivot = new Vector2(0.5f, 0f);
        cageGroupRT.sizeDelta = new Vector2(315f, 293f);
        cageGroupRT.anchoredPosition = Vector2.zero;

        var cageImg = cageGO.AddComponent<Image>();
        if (spCage != null) { cageImg.sprite = spCage; cageImg.preserveAspect = true; }
        else { cageImg.color = new Color(0.78f, 0.60f, 0.15f, 0.90f); }
        cageImg.raycastTarget = false;

        var chainGO = new GameObject("Chain", typeof(RectTransform));
        chainGO.transform.SetParent(cageGroupRT, false);
        var chainRT = chainGO.GetComponent<RectTransform>();
        chainRT.anchorMin = new Vector2(0.42f, 1.0f); chainRT.anchorMax = new Vector2(0.58f, 1.0f);
        chainRT.pivot = new Vector2(0.5f, 0f); chainRT.sizeDelta = new Vector2(8f, 60f);
        var chainImg = chainGO.AddComponent<Image>();
        chainImg.color = new Color(0.78f, 0.60f, 0.15f, 0.90f); chainImg.raycastTarget = false;

        // ── Wire LureSimulation ──────────────────────────────────────
        var simComp = root.gameObject.AddComponent<LureSimulation>();
        simComp.panelRT = rrt;
        simComp.sharkRT = sharkRT;
        simComp.sharkImage = sharkImg;
        simComp.cageRT = cageGroupRT;
        simComp.cageImage = cageImg;
        simComp.cageSprite = spCage;
        simComp.cagedamaged1 = spCaged1;
        simComp.cagedamaged2 = spCaged2;
        simComp.cagedamaged3 = spCaged3;
        simComp.diverRT = diverRT;
        simComp.diverImage = diverImg;
        simComp.diverNormal = spDiverNormal;
        simComp.diverPointing = spDiverPointing;
        simComp.diverScared = spDiverScared;
        simComp.totalTime = 60f;
        simComp.rodLineRT = null;
        simComp.hookRT = null;
        simComp.debrisLayers = new RectTransform[0];
        simComp.debrisImages = new Image[0];
        simComp.cageBars = new Image[0];
        simComp.cageBase = null;
        simComp.cageShadow = null;

        simComp.onImpact = () => { if (manager.sfxSource && manager.sfxImpact) manager.sfxSource.PlayOneShot(manager.sfxImpact, 0.85f); };
        simComp.onRodWinding = () => { if (manager.sfxSource && manager.sfxRodWinding) manager.sfxSource.PlayOneShot(manager.sfxRodWinding, 0.85f); };

        int[] wrongClickCount = { 0 };
        var heartImagesRef = manager.heartImages;
        simComp.onWrongClick = () =>
        {
            if (heartImagesRef == null) return;
            int lost = wrongClickCount[0];
            if (lost < heartImagesRef.Length && heartImagesRef[lost] != null)
                heartImagesRef[lost].color = new Color(0.30f, 0.30f, 0.30f, 0.45f);
            wrongClickCount[0]++;
        };

        manager.lureSimulation = simComp;
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
    // Hidden commentator
    // =================================================================

    static Commentator BuildHiddenCommentator(RectTransform parent)
    {
        var root = NewGO("HiddenCommentator", parent);
        var rrt = root.GetComponent<RectTransform>();
        rrt.anchorMin = rrt.anchorMax = new Vector2(2f, -1f);
        rrt.sizeDelta = new Vector2(1, 1);
        var cg = root.AddComponent<CanvasGroup>();
        cg.alpha = 0f; cg.blocksRaycasts = false; cg.interactable = false;

        var portrait = root.AddComponent<Image>(); portrait.color = new Color(0, 0, 0, 0);
        var comm = root.AddComponent<Commentator>();
        comm.root = root;
        comm.portrait = portrait;
        return comm;
    }

    // =================================================================
    // Commentator (full — kept for reference)
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
    // AddMarkerWithCircle — FIX: reduced offset from ±12 to ±4 so
    // adjacent markers don't overlap and swallow each other's clicks.
    // =================================================================

    static void AddMarkerWithCircle(GameObject parent, SpotDifferenceManager manager,
        string flagName, string explanation)
    {
        var go = NewGO("Marker_" + Sanitize(flagName), parent.transform);
        var rt = go.GetComponent<RectTransform>(); Stretch(rt);
        // FIX: reduced from ±12 to ±4 to prevent found markers from
        // swallowing clicks meant for adjacent unfound markers.
        rt.offsetMin = new Vector2(-4, -4); rt.offsetMax = new Vector2(4, 4);

        var img = go.AddComponent<Image>(); img.color = new Color(1, 0, 0, 0); img.raycastTarget = true;
        var marker = go.AddComponent<DifferenceMarker>();
        marker.flagName = flagName;
        marker.explanation = explanation;
        marker.manager = manager;

        var circGo = NewGO("PenCircle", go.transform);
        var circRT = circGo.GetComponent<RectTransform>(); Stretch(circRT);
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
    // Build settings + helpers
    // =================================================================

    static void AddSceneToBuildSettings(string path)
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(s => s.path == path))
        { scenes.Add(new EditorBuildSettingsScene(path, true)); EditorBuildSettings.scenes = scenes.ToArray(); }
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
        Debug.Log($"[SpotDiffBuilder] {n}: " + (s != null ? "✓ found" : "✗ not found"));

    static GameObject BodyRow(Transform parent, string name, string text, int fs, Color col, FontStyles style)
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