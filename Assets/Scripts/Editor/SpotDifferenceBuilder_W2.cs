using System.IO;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// World 2 Spot the Difference — Bank Login Page theme.
/// FIXED: 
///   - Layout rebuilt before markers are added so footer markers get correct rects
///   - Retry/Back buttons wired with UnityEventTools.AddPersistentListener (survives builds)
///   - shark_music_v1 assigned as BGM
///   - Commentator removed
/// </summary>
public static class SpotDifferenceBuilder_W2
{
    private const string ScenesDir = "Assets/Scenes";
    private const string ScenePath = "Assets/Scenes/SpotDifference_W2.unity";

    private static readonly Color HudDark = Hex("#1A140E");
    private static readonly Color HudBorder = Hex("#8B6914");
    private static readonly Color FolderOuter = Hex("#C8A96E");
    private static readonly Color FolderInner = Hex("#E8D5A3");
    private static readonly Color HudGold = Hex("#D4A843");
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
    private static readonly Color CtaReal = Hex("#1565C0");
    private static readonly Color CtaScam = Hex("#B71C1C");
    private static readonly Color SimDeep = Hex("#071824");
    private static readonly Color SimMid = Hex("#0A2840");
    private static readonly Color SimHintBg = new Color(0.18f, 0.28f, 0.40f, 0.85f);
    private static readonly Color PenRed = Hex("#C0392B");
    private static readonly Color OverlayColor = new Color(0f, 0f, 0f, 0.72f);
    private static readonly Color HeartRed = new Color(0.75f, 0.18f, 0.18f);
    private static readonly Color BankBlue = Hex("#003087");
    private static readonly Color BankGold = Hex("#FFB800");

    [MenuItem("Phisherman/Build Spot Difference W2 Scene")]
    public static void Build()
    {
        if (!Directory.Exists(ScenesDir)) Directory.CreateDirectory(ScenesDir);
        var permanentColliders = PermanentColliderGuard.Capture(ScenePath);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        PermanentColliderGuard.Restore(permanentColliders);

        Sprite spCircle = TryGetCircle();
        Sprite spDiverNormal = FindSprite("phisherman_underwater") ?? FindSprite("phisherman");
        Sprite spDiverPointing = FindSprite("phisherman_underwater_pointing") ?? spDiverNormal;
        Sprite spDiverScared = FindSprite("phisherman_underwater_slightly_scared") ?? spDiverNormal;
        Sprite spCage = FindSprite("cage");
        Sprite spCaged1 = FindSprite("cage_damaged_1");
        Sprite spCaged2 = FindSprite("cage_damaged_2");
        Sprite spCaged3 = FindSprite("cage_damaged_3");
        Sprite spShark = FindSprite("shark");
        Sprite spHeart = FindSprite("heart");
        AudioClip sharkMusic = FindAudio("shark_music_v1");

        var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>(); camGo.AddComponent<AudioListener>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = HudDark; cam.orthographic = true;

        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>(); es.AddComponent<StandaloneInputModule>();

        var canvasGo = new GameObject("Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam; canvas.planeDistance = 100;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        var canvasRT = canvasGo.GetComponent<RectTransform>();

        var mgrGo = new GameObject("GameManager");
        var manager = mgrGo.AddComponent<SpotDifferenceManager>();

        // Two audio sources: sfx + bgm
        var sfxSrc = mgrGo.AddComponent<AudioSource>(); sfxSrc.playOnAwake = false;
        var musicSrc = mgrGo.AddComponent<AudioSource>(); musicSrc.playOnAwake = false; musicSrc.loop = true;

        manager.mainCanvas = canvas;
        manager.mainCanvasRT = canvasRT;
        manager.circleSprite = spCircle;
        manager.heartSprite = spHeart;
        manager.sfxSource = sfxSrc;
        manager.bgMusicSource = musicSrc;
        manager.sfxSplash = FindAudio("water_splash");
        manager.sfxWrong = FindAudio("glass_crack");
        manager.sfxImpact = FindAudio("impact");
        manager.sfxRodWinding = FindAudio("fishing_rod_winding");
        // Assign shark music so BGM plays automatically
        manager.bgMusicClip = sharkMusic;
        if (sharkMusic != null) musicSrc.clip = sharkMusic;

        var bgImg = AddImg(canvasRT, "Background", HudDark); Stretch(bgImg.rectTransform); bgImg.raycastTarget = false;

        BuildHud(canvasRT, manager, spCircle, spHeart);

        var simHintBg = AddImg(canvasRT, "SimHintBg", SimHintBg);
        var shrt = simHintBg.rectTransform;
        shrt.anchorMin = new Vector2(0, 0.27f); shrt.anchorMax = new Vector2(1, 0.29f);
        shrt.offsetMin = shrt.offsetMax = Vector2.zero; simHintBg.raycastTarget = false;
        var simHintTxt = AddTxt(simHintBg.rectTransform, "SimHintText",
            "Find all 6 red flags on the fake login page before the shark escapes!",
            19, new Color(0.90f, 0.90f, 1f), TextAlignmentOptions.Center, FontStyles.Italic);
        Stretch(simHintTxt.rectTransform); simHintTxt.raycastTarget = false;

        BuildFolder(canvasRT, manager);
        BuildSimulation(canvasRT, manager, spDiverNormal, spDiverPointing, spDiverScared,
            spShark, spCage, spCaged1, spCaged2, spCaged3, spCircle);

        var efGo = new GameObject("EffectLayer", typeof(RectTransform));
        efGo.transform.SetParent(canvasRT, false);
        Stretch(efGo.GetComponent<RectTransform>());
        efGo.transform.SetAsLastSibling();
        var eImg = efGo.AddComponent<Image>(); eImg.color = new Color(0, 0, 0, 0); eImg.raycastTarget = false;
        manager.effectLayer = efGo.GetComponent<RectTransform>();

        var feedback = AddTxt(canvasRT, "Feedback", string.Empty, 38, Color.green, TextAlignmentOptions.Center, FontStyles.Bold);
        var fbRT = feedback.rectTransform;
        fbRT.anchorMin = new Vector2(0.5f, 1); fbRT.anchorMax = new Vector2(0.5f, 1);
        fbRT.pivot = new Vector2(0.5f, 1); fbRT.sizeDelta = new Vector2(1100, 56);
        fbRT.anchoredPosition = new Vector2(0, -118); feedback.raycastTarget = false;

        manager.feedbackText = feedback;
        manager.resultPanel = BuildResultPanel(canvasRT, manager);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuild(ScenePath);
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log($"[SpotDifferenceBuilder_W2] Built → {ScenePath}");
    }

    // =================================================================
    //  HUD
    // =================================================================

    static void BuildHud(RectTransform parent, SpotDifferenceManager manager, Sprite spCircle, Sprite spHeart)
    {
        var hud = AddImg(parent, "HUD", HudDark); AnchorTopStretch(hud.rectTransform, 100); hud.raycastTarget = false;
        var border = AddImg(hud.rectTransform, "HudBorder", HudBorder);
        var brt = border.rectTransform;
        brt.anchorMin = Vector2.zero; brt.anchorMax = new Vector2(1, 0);
        brt.pivot = new Vector2(0.5f, 0); brt.sizeDelta = new Vector2(0, 2);
        border.raycastTarget = false;

        var heartsH = NewGO("HeartsHolder", hud.rectTransform);
        var heartsHRT = heartsH.GetComponent<RectTransform>();
        AnchorRect(heartsHRT, new Vector2(0f, 0), new Vector2(0.14f, 1), new Vector2(18, 0), Vector2.zero);
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

        var timerBox = AddImg(hud.rectTransform, "TimerBox", new Color(0.10f, 0.07f, 0.03f, 1f));
        AnchorRect(timerBox.rectTransform, new Vector2(0.14f, 0), new Vector2(0.35f, 1), new Vector2(8, 8), new Vector2(-8, -8));
        timerBox.raycastTarget = false;
        var timerTxt = AddTxt(timerBox.rectTransform, "Timer", "1:00", 40, HudGold, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(timerTxt.rectTransform);

        var counter = AddTxt(hud.rectTransform, "Counter", "Found: 0 / 6", 30, HudGold, TextAlignmentOptions.Center, FontStyles.Bold);
        AnchorRect(counter.rectTransform, new Vector2(0.35f, 0), new Vector2(0.65f, 1), Vector2.zero, Vector2.zero);
        var score = AddTxt(hud.rectTransform, "Score", "Score: 0", 30, HudGold, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
        AnchorRect(score.rectTransform, new Vector2(0.65f, 0), new Vector2(1f, 1), Vector2.zero, new Vector2(-22, 0));

        manager.timerText = timerTxt;
        manager.counterText = counter;
        manager.scoreText = score;
    }

    // =================================================================
    //  Folder
    // =================================================================

    static void BuildFolder(RectTransform parent, SpotDifferenceManager manager)
    {
        var folder = AddImg(parent, "Folder", FolderOuter);
        var frt = folder.rectTransform;
        frt.anchorMin = new Vector2(0.010f, 0.290f); frt.anchorMax = new Vector2(0.990f, 0.893f);
        frt.offsetMin = frt.offsetMax = Vector2.zero; folder.raycastTarget = false;

        var inner = AddImg(folder.rectTransform, "FolderInner", FolderInner);
        var irt = inner.rectTransform;
        irt.anchorMin = new Vector2(0.005f, 0.005f); irt.anchorMax = new Vector2(0.995f, 0.995f);
        irt.offsetMin = irt.offsetMax = Vector2.zero; inner.raycastTarget = false;

        var stamp = AddTxt(inner.rectTransform, "ClassifiedStamp", "CLASSIFIED", 20, ClassifiedRed, TextAlignmentOptions.Center, FontStyles.Bold);
        var srt = stamp.rectTransform;
        srt.anchorMin = new Vector2(0.78f, 0.88f); srt.anchorMax = new Vector2(0.99f, 0.99f);
        srt.offsetMin = srt.offsetMax = Vector2.zero; stamp.raycastTarget = false;

        BuildLoginPanel(inner.rectTransform, manager, isScam: false, new Vector2(0.01f, 0.01f), new Vector2(0.492f, 0.99f));
        BuildLoginPanel(inner.rectTransform, manager, isScam: true, new Vector2(0.508f, 0.01f), new Vector2(0.99f, 0.99f));
    }

    // =================================================================
    //  Login panel — FIX: build layout fully, THEN add markers
    // =================================================================

    static void BuildLoginPanel(RectTransform parent, SpotDifferenceManager manager,
        bool isScam, Vector2 anchorMin, Vector2 anchorMax)
    {
        var panel = AddImg(parent, isScam ? "ScamPage" : "RealPage", PanelBg);
        var rt = panel.rectTransform;
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero; panel.raycastTarget = false;

        var brd = AddImg(rt, "DocBorder", PanelBorder);
        Stretch(brd.rectTransform); brd.raycastTarget = false;

        var face = AddImg(rt, "DocFace", PanelBg);
        var faceRT = face.rectTransform;
        faceRT.anchorMin = Vector2.zero; faceRT.anchorMax = Vector2.one;
        faceRT.offsetMin = new Vector2(2, 2); faceRT.offsetMax = new Vector2(-2, -2);
        face.raycastTarget = isScam;
        if (isScam) { var recv = face.gameObject.AddComponent<PanelClickReceiver>(); recv.manager = manager; }

        var lbl = AddImg(face.rectTransform, "DocStamp", isScam ? LabelScamBg : LabelRealBg);
        AnchorTopStretch(lbl.rectTransform, 36); lbl.raycastTarget = false;
        var lblTxt = AddTxt(lbl.rectTransform, "StampText",
            isScam ? "SUSPECT PAGE — find 6 red flags" : "LEGITIMATE — for reference",
            17, isScam ? LabelScamText : LabelRealText, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(lblTxt.rectTransform); lblTxt.raycastTarget = false;

        var scrollGo = NewGO("BodyScroll", face.rectTransform);
        var scrollRT = scrollGo.GetComponent<RectTransform>();
        scrollRT.anchorMin = Vector2.zero; scrollRT.anchorMax = Vector2.one;
        scrollRT.offsetMin = Vector2.zero; scrollRT.offsetMax = new Vector2(0, -36);
        var scrollImg = scrollGo.AddComponent<Image>(); scrollImg.color = PanelBg; scrollImg.raycastTarget = false;
        var sr = scrollGo.AddComponent<ScrollRect>();
        sr.horizontal = false; sr.vertical = true; sr.scrollSensitivity = 35;
        sr.movementType = ScrollRect.MovementType.Clamped;

        var vpGo = NewGO("Viewport", scrollRT); Stretch(vpGo.GetComponent<RectTransform>());
        var vpImg = vpGo.AddComponent<Image>(); vpImg.color = PanelBg; vpImg.raycastTarget = true;
        if (isScam) { var recv2 = vpGo.AddComponent<PanelClickReceiver>(); recv2.manager = manager; }
        vpGo.AddComponent<Mask>().showMaskGraphic = false;

        var contentGo = NewGO("Content", vpGo.transform);
        var contentRT = contentGo.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1); contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(0.5f, 1); contentRT.sizeDelta = Vector2.zero;
        contentRT.anchoredPosition = Vector2.zero;
        var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.spacing = 6; vlg.padding = new RectOffset(18, 18, 14, 20);
        contentGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sr.viewport = vpGo.GetComponent<RectTransform>(); sr.content = contentRT;

        // Build body content first (no markers yet)
        BuildLoginBody(contentGo.transform, manager, isScam);

        // CRITICAL FIX: force layout rebuild so all rects are valid BEFORE placing markers
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRT);
        sr.verticalNormalizedPosition = 1f;
    }

    // =================================================================
    //  Login body — markers added AFTER body is built
    // =================================================================

    static void BuildLoginBody(Transform content, SpotDifferenceManager manager, bool isScam)
    {
        // ── Browser chrome bar (Task 4: Website/Forum theme) — decorative
        // only, not a counted red flag, so it's never wrapped in AddMarker. ──
        AddBrowserChrome(content, isScam);

        // ── Bank header bar ──
        var headerGo = NewGO("BankHeader", content);
        headerGo.AddComponent<LayoutElement>().preferredHeight = 56;
        var headerImg = headerGo.AddComponent<Image>();
        headerImg.color = isScam ? Hex("#004080") : BankBlue; headerImg.raycastTarget = false;
        var headerTxt = AddTxt(headerGo.transform, "BankName",
            isScam ? "TD Bank — Secure Login Portal" : "TD Bank — Online Banking",
            22, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        var htrt = headerTxt.rectTransform;
        htrt.anchorMin = Vector2.zero; htrt.anchorMax = Vector2.one;
        htrt.offsetMin = new Vector2(18, 0); htrt.offsetMax = Vector2.zero;
        headerTxt.raycastTarget = false;

        // URL bar (FLAG 1 for scam)
        var urlRow = NewGO("UrlBar", content);
        urlRow.AddComponent<LayoutElement>().preferredHeight = 36;
        var urlImg = urlRow.AddComponent<Image>(); urlImg.color = new Color(0.94f, 0.94f, 0.94f); urlImg.raycastTarget = false;
        var urlTxt = AddTxt(urlRow.transform, "Url",
            isScam ? "🔓  http://td-bank-secure-login.net/signin"
                   : "🔒  https://easyweb.td.com/signin",
            15, isScam ? Hex("#8B0000") : Hex("#2E7D32"), TextAlignmentOptions.MidlineLeft);
        var urt = urlTxt.rectTransform;
        urt.anchorMin = Vector2.zero; urt.anchorMax = Vector2.one;
        urt.offsetMin = new Vector2(12, 0); urt.offsetMax = Vector2.zero;
        urlTxt.raycastTarget = false;

        Spacer(content, 8);

        // Urgency banner (FLAG 2 for scam only)
        GameObject urgGo = null;
        if (isScam)
        {
            urgGo = NewGO("UrgencyBanner", content);
            urgGo.AddComponent<LayoutElement>().preferredHeight = 42;
            var urgImg = urgGo.AddComponent<Image>(); urgImg.color = new Color(1f, 0.85f, 0.10f, 0.95f); urgImg.raycastTarget = false;
            var urgTxt = AddTxt(urgGo.transform, "UrgencyText",
                "⚠️  Your account has been flagged — verify now or access will be suspended in 2 hours.",
                16, Hex("#7A1515"), TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            var utt = urgTxt.rectTransform;
            utt.anchorMin = Vector2.zero; utt.anchorMax = Vector2.one;
            utt.offsetMin = new Vector2(12, 0); utt.offsetMax = Vector2.zero;
            urgTxt.raycastTarget = false;
            Spacer(content, 4);
        }

        BodyRow(content, "UsernameLabel", "Username / Access Card Number", 18, BodyText, FontStyles.Bold);
        InputField(content, "username or access card number");
        Spacer(content, 6);

        BodyRow(content, "PasswordLabel", "Password", 18, BodyText, FontStyles.Bold);
        InputField(content, "••••••••");
        Spacer(content, 6);

        // ATM PIN field (FLAG 3 for scam only)
        GameObject pinField = null;
        if (isScam)
        {
            BodyRow(content, "PinLabel", "ATM PIN (required for verification)", 18, BodyText, FontStyles.Bold);
            pinField = InputField(content, "4-digit PIN");
            Spacer(content, 6);
        }

        // Login button
        var ctaGo = NewGO("CtaHolder", content);
        ctaGo.AddComponent<Image>().color = new Color(0, 0, 0, 0);
        ctaGo.GetComponent<Image>().raycastTarget = false;
        ctaGo.AddComponent<LayoutElement>().preferredHeight = 52;
        var ctaBtn = AddImg(ctaGo.GetComponent<RectTransform>(), "LoginBtn", isScam ? CtaScam : CtaReal);
        var cbrt = ctaBtn.rectTransform;
        cbrt.anchorMin = cbrt.anchorMax = new Vector2(0.5f, 0.5f);
        cbrt.pivot = new Vector2(0.5f, 0.5f); cbrt.sizeDelta = new Vector2(240, 44);
        ctaBtn.raycastTarget = false;
        var ctaLbl = AddTxt(ctaBtn.rectTransform, "Label", isScam ? "VERIFY & LOGIN" : "Log In",
            20, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(ctaLbl.rectTransform); ctaLbl.raycastTarget = false;

        Spacer(content, 10);

        var div = AddImg(content, "Divider", DividerColor);
        div.GetComponent<Image>().raycastTarget = false;
        (div.GetComponent<LayoutElement>() ?? div.AddComponent<LayoutElement>()).preferredHeight = 1;
        Spacer(content, 8);

        // Footer rows (FLAGS 4, 5, 6 for scam)
        var footerCopyright = BodyRow(content, "Footer1",
            isScam ? "© 2019 TD Bank Group. All rights reserved."
                   : "© 2024 TD Bank Group. All rights reserved.",
            14, MutedText, FontStyles.Normal);

        var footerLinks = BodyRow(content, "Footer2",
            isScam ? "Help: td-bank-secure-login.net/help | Privacy Policy | Legal"
                   : "Help: td.com/help | Privacy Policy | Security | Legal",
            14, MutedText, FontStyles.Normal);

        GameObject phoneLine = null;
        if (isScam)
        {
            phoneLine = BodyRow(content, "ScamPhone",
                "Security concern? Call our 24/7 line: 1-888-555-0147 (not affiliated with TD Bank N.A.)",
                13, MutedText, FontStyles.Italic);
        }

        // ── IMPORTANT: all layout objects created — now add markers ──
        // The parent content needs a rebuild before markers are placed,
        // but that happens in BuildLoginPanel after this method returns.
        // We store references and add markers via a deferred approach by
        // just adding them now — BuildLoginPanel forces rebuild after calling this.

        if (isScam)
        {
            AddMarker(urlRow, manager, "Fake URL / no HTTPS",
                "The real TD Bank login is at easyweb.td.com. 'td-bank-secure-login.net' is a lookalike domain owned by scammers. The 🔓 padlock means the connection is NOT secure.");
            if (urgGo != null)
                AddMarker(urgGo, manager, "Urgency threat banner",
                    "Real banks never show countdown threats on their login page. This banner is designed to panic you into entering your details without thinking.");
            if (pinField != null)
                AddMarker(pinField, manager, "Asking for your ATM PIN",
                    "A legitimate bank login page NEVER asks for your ATM PIN. This is one of the clearest signs of a phishing site — they want to drain your accounts.");
            AddMarker(footerCopyright, manager, "Outdated copyright year",
                "The footer shows © 2019 — scammers often forget to update copied page templates. The real TD site shows the current year. Stale dates are a quick tell.");
            AddMarker(footerLinks, manager, "Footer links use the fake domain",
                "All footer links point back to 'td-bank-secure-login.net' — the scam domain. On a real page every link would go to td.com. This confirms the entire page is fake.");
            if (phoneLine != null)
                AddMarker(phoneLine, manager, "Fake support number in fine print",
                    "'Not affiliated with TD Bank N.A.' — hidden in tiny italic text. The number connects to scammers who will ask for even more information to 'verify' your account.");
        }
    }

    static void AddBrowserChrome(Transform content, bool isScam)
    {
        var chromeGo = NewGO("BrowserChrome", content);
        chromeGo.AddComponent<LayoutElement>().preferredHeight = 30;
        var chromeImg = chromeGo.AddComponent<Image>(); chromeImg.color = Hex("#E8E8E8"); chromeImg.raycastTarget = false;
        var chromeRT = chromeGo.GetComponent<RectTransform>();

        float[] dotX = { 14f, 30f, 46f };
        Color[] dotCol = { Hex("#FF5F57"), Hex("#FFBD2E"), Hex("#28C840") };
        for (int i = 0; i < 3; i++)
        {
            var dotGo = NewGO("Dot" + i, chromeRT);
            var dotRT = dotGo.GetComponent<RectTransform>();
            dotRT.anchorMin = dotRT.anchorMax = new Vector2(0f, 0.5f); dotRT.pivot = new Vector2(0.5f, 0.5f);
            dotRT.sizeDelta = new Vector2(10, 10); dotRT.anchoredPosition = new Vector2(dotX[i], 0);
            var dotImg = dotGo.AddComponent<Image>(); dotImg.sprite = TryGetCircle(); dotImg.color = dotCol[i]; dotImg.raycastTarget = false;
        }

        // Real panel gets a professional blue accent; scam panel gets a subtle red warning stripe.
        var accent = AddImg(chromeRT, "Accent", isScam ? ClassifiedRed : Hex("#1A73E8"));
        var arT = accent.rectTransform; arT.anchorMin = new Vector2(0, 0); arT.anchorMax = new Vector2(1, 0);
        arT.pivot = new Vector2(0.5f, 0); arT.sizeDelta = new Vector2(0, 3); arT.anchoredPosition = Vector2.zero;
        accent.raycastTarget = false;
    }

    static GameObject InputField(Transform parent, string placeholder)
    {
        var go = NewGO("InputField", parent);
        var le = go.AddComponent<LayoutElement>(); le.preferredHeight = 38;
        var img = go.AddComponent<Image>(); img.color = Color.white; img.raycastTarget = false;
        var brd = AddImg(go.GetComponent<RectTransform>(), "Border", DividerColor);
        Stretch(brd.rectTransform); brd.raycastTarget = false;
        var txt = AddTxt(go.transform, "Placeholder", placeholder, 18, MutedText, TextAlignmentOptions.MidlineLeft);
        var trt = txt.rectTransform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(10, 0); trt.offsetMax = Vector2.zero;
        txt.raycastTarget = false;
        return go;
    }

    // =================================================================
    //  Simulation
    // =================================================================

    static void BuildSimulation(RectTransform parent, SpotDifferenceManager manager,
        Sprite spDiverNormal, Sprite spDiverPointing, Sprite spDiverScared,
        Sprite spShark, Sprite spCage, Sprite spCaged1, Sprite spCaged2, Sprite spCaged3,
        Sprite spCircle)
    {
        var root = AddImg(parent, "SimulationRoot", SimDeep);
        var rrt = root.rectTransform;
        rrt.anchorMin = new Vector2(0, 0); rrt.anchorMax = new Vector2(1, 0.270f);
        rrt.offsetMin = rrt.offsetMax = Vector2.zero; root.raycastTarget = false;

        var mid = AddImg(rrt, "SimMid", SimMid);
        var mrt = mid.rectTransform;
        mrt.anchorMin = new Vector2(0, 0.15f); mrt.anchorMax = Vector2.one;
        mrt.offsetMin = mrt.offsetMax = Vector2.zero; mid.raycastTarget = false;

        var sharkImg = AddImg(rrt, "Shark", Color.white);
        if (spShark != null) { sharkImg.sprite = spShark; sharkImg.preserveAspect = true; }
        else if (spCircle != null) { sharkImg.sprite = spCircle; sharkImg.color = new Color(0.25f, 0.32f, 0.42f); }
        var sharkRT = sharkImg.rectTransform;
        sharkRT.anchorMin = sharkRT.anchorMax = new Vector2(0.5f, 0.5f);
        sharkRT.pivot = new Vector2(0.5f, 0.5f); sharkRT.sizeDelta = new Vector2(120f, 80f);
        sharkRT.anchoredPosition = Vector2.zero; sharkRT.localScale = new Vector3(-1f, 1f, 1f);
        sharkImg.raycastTarget = false;

        var diverImg = AddImg(rrt, "Diver", Color.white);
        if (spDiverPointing != null) { diverImg.sprite = spDiverPointing; diverImg.preserveAspect = true; }
        else if (spDiverNormal != null) { diverImg.sprite = spDiverNormal; diverImg.preserveAspect = true; }
        var diverRT = diverImg.rectTransform;
        diverRT.anchorMin = diverRT.anchorMax = new Vector2(0.5f, 0.5f);
        diverRT.pivot = new Vector2(0.5f, 0.5f); diverRT.sizeDelta = new Vector2(80f, 90f);
        diverRT.anchoredPosition = Vector2.zero; diverImg.raycastTarget = false;
        diverRT.localScale = new Vector3(-1f, 1f, 1f);

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

        var simComp = root.gameObject.AddComponent<LureSimulation>();
        simComp.panelRT = rrt;
        simComp.sharkRT = sharkRT; simComp.sharkImage = sharkImg;
        simComp.cageRT = cageGroupRT; simComp.cageImage = cageImg;
        simComp.cageSprite = spCage;
        simComp.cagedamaged1 = spCaged1; simComp.cagedamaged2 = spCaged2; simComp.cagedamaged3 = spCaged3;
        simComp.diverRT = diverRT; simComp.diverImage = diverImg;
        simComp.diverNormal = spDiverNormal; simComp.diverPointing = spDiverPointing; simComp.diverScared = spDiverScared;
        simComp.totalTime = 60f;
        simComp.rodLineRT = null; simComp.hookRT = null;
        simComp.debrisLayers = new RectTransform[0]; simComp.debrisImages = new Image[0];
        simComp.cageBars = new Image[0]; simComp.cageBase = null; simComp.cageShadow = null;
        simComp.onImpact = () => { if (manager.sfxSource && manager.sfxImpact) manager.sfxSource.PlayOneShot(manager.sfxImpact, 0.85f); };
        simComp.onRodWinding = () => { if (manager.sfxSource && manager.sfxRodWinding) manager.sfxSource.PlayOneShot(manager.sfxRodWinding, 0.85f); };
        var heartImagesRef = manager.heartImages;
        int[] wcc = { 0 };
        simComp.onWrongClick = () =>
        {
            if (heartImagesRef == null) return;
            int lost = wcc[0];
            if (lost < heartImagesRef.Length && heartImagesRef[lost] != null)
                heartImagesRef[lost].color = new Color(0.30f, 0.30f, 0.30f, 0.45f);
            wcc[0]++;
        };
        manager.lureSimulation = simComp;
    }

    // =================================================================
    //  Result panel — FIX: use UnityEventTools for persistent listeners
    // =================================================================

    static GameObject BuildResultPanel(RectTransform parent, SpotDifferenceManager manager)
    {
        var overlay = AddImg(parent, "ResultOverlay", OverlayColor);
        Stretch(overlay.rectTransform); overlay.raycastTarget = true;

        var panel = AddImg(overlay.rectTransform, "ResultPanel", FolderInner);
        var prt = panel.rectTransform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot = new Vector2(0.5f, 0.5f); prt.sizeDelta = new Vector2(1100, 800);

        var pBrd = AddImg(prt, "PanelBorder", FolderOuter); Stretch(pBrd.rectTransform); pBrd.raycastTarget = false;
        var pFace = AddImg(prt, "PanelFace", FolderInner);
        var pfRT = pFace.rectTransform;
        pfRT.anchorMin = Vector2.zero; pfRT.anchorMax = Vector2.one;
        pfRT.offsetMin = new Vector2(3, 3); pfRT.offsetMax = new Vector2(-3, -3);
        pFace.raycastTarget = false;

        var header = AddImg(pFace.rectTransform, "Header", HudDark);
        AnchorTopStretch(header.rectTransform, 90);
        var hLabel = AddTxt(header.rectTransform, "Title", "Login page cracked!", 42, HudGold, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(hLabel.rectTransform); hLabel.raycastTarget = false;

        var breakdown = AddTxt(pFace.rectTransform, "Breakdown", string.Empty, 20, BodyText, TextAlignmentOptions.TopLeft);
        breakdown.textWrappingMode = TextWrappingModes.Normal;
        var brt = breakdown.rectTransform;
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
        brt.offsetMin = new Vector2(50, 180); brt.offsetMax = new Vector2(-50, -120);

        var scoreText = AddTxt(pFace.rectTransform, "Score", "Final Score: 0", 34, BodyText, TextAlignmentOptions.Center, FontStyles.Bold);
        var srt = scoreText.rectTransform;
        srt.anchorMin = new Vector2(0, 0); srt.anchorMax = new Vector2(1, 0);
        srt.pivot = new Vector2(0.5f, 0); srt.sizeDelta = new Vector2(-80, 52);
        srt.anchoredPosition = new Vector2(0, 110);

        // Play Again button — wired with UnityEventTools so it survives a build
        var playAgain = AddBtn(pFace.rectTransform, "PlayAgainBtn", "Play Again", 26, Hex("#2ECC71"), Color.white);
        var part = playAgain.GetComponent<RectTransform>();
        part.anchorMin = new Vector2(0.5f, 0); part.anchorMax = new Vector2(0.5f, 0);
        part.pivot = new Vector2(1f, 0); part.sizeDelta = new Vector2(240, 62);
        part.anchoredPosition = new Vector2(-16, 28);
        UnityEventTools.AddPersistentListener(playAgain.GetComponent<Button>().onClick, manager.PlayAgain);

        // Back to Map button — wired with UnityEventTools so it survives a build
        var menuBtn = AddBtn(pFace.rectTransform, "MenuBtn", "Back to Map", 26, Hex("#7F8C8D"), Color.white);
        var mrt2 = menuBtn.GetComponent<RectTransform>();
        mrt2.anchorMin = new Vector2(0.5f, 0); mrt2.anchorMax = new Vector2(0.5f, 0);
        mrt2.pivot = new Vector2(0f, 0); mrt2.sizeDelta = new Vector2(240, 62);
        mrt2.anchoredPosition = new Vector2(16, 28);
        UnityEventTools.AddPersistentListener(menuBtn.GetComponent<Button>().onClick, manager.BackToWorldMap);

        manager.resultTitle = hLabel;
        manager.resultBreakdown = breakdown;
        manager.resultScore = scoreText;
        overlay.gameObject.SetActive(false);
        return overlay.gameObject;
    }

    // =================================================================
    //  Marker helper
    // =================================================================

    static void AddMarker(GameObject parent, SpotDifferenceManager manager, string flagName, string explanation)
    {
        var go = NewGO("Marker_" + new string(flagName.Where(char.IsLetterOrDigit).ToArray()), parent.transform);
        var rt = go.GetComponent<RectTransform>(); Stretch(rt);
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
        circImg.sprite = TryGetCircle();
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

    // =================================================================
    //  Layout helpers
    // =================================================================

    static void Spacer(Transform parent, float h) { var go = NewGO("Spacer", parent); var le = go.AddComponent<LayoutElement>(); le.preferredHeight = h; le.flexibleWidth = 1; }
    static GameObject BodyRow(Transform parent, string name, string text, int fs, Color col, FontStyles style)
    {
        var go = NewGO(name, parent); var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = fs; t.color = col; t.fontStyle = style;
        t.alignment = TextAlignmentOptions.TopLeft;
        t.textWrappingMode = TextWrappingModes.Normal; t.raycastTarget = false;
        go.AddComponent<LayoutElement>().flexibleWidth = 1; return go;
    }
    static Image AddImg(Transform p, string n, Color c) { var go = NewGO(n, p); var img = go.AddComponent<Image>(); img.color = c; return img; }
    static TMP_Text AddTxt(Transform p, string n, string content, int fs, Color col, TextAlignmentOptions align, FontStyles style = FontStyles.Normal)
    { var go = NewGO(n, p); var t = go.AddComponent<TextMeshProUGUI>(); t.text = content; t.fontSize = fs; t.color = col; t.alignment = align; t.fontStyle = style; t.raycastTarget = false; return t; }
    static GameObject AddBtn(Transform p, string n, string lbl, int fs, Color bg, Color tc)
    { var go = NewGO(n, p); var img = go.AddComponent<Image>(); img.color = bg; var btn = go.AddComponent<Button>(); btn.targetGraphic = img; var t = AddTxt(go.transform, "Label", lbl, fs, tc, TextAlignmentOptions.Center, FontStyles.Bold); Stretch(t.rectTransform); return go; }
    static GameObject NewGO(string n, Transform p) { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); return go; }
    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
    static void AnchorTopStretch(RectTransform r, float h) { r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1); r.pivot = new Vector2(0.5f, 1); r.sizeDelta = new Vector2(0, h); r.anchoredPosition = Vector2.zero; }
    static void AnchorRect(RectTransform r, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax) { r.anchorMin = aMin; r.anchorMax = aMax; r.offsetMin = oMin; r.offsetMax = oMax; }
    static Color Hex(string h) => UnityEngine.ColorUtility.TryParseHtmlString(h, out var c) ? c : Color.magenta;
    static Sprite TryGetCircle() { try { return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"); } catch { return null; } }
    static Sprite FindSprite(string name) { foreach (var g in AssetDatabase.FindAssets(name + " t:Sprite")) { var p = AssetDatabase.GUIDToAssetPath(g); if (Path.GetFileNameWithoutExtension(p).ToLower() == name.ToLower()) { var s = AssetDatabase.LoadAssetAtPath<Sprite>(p); if (s != null) return s; } } return null; }
    static AudioClip FindAudio(string name) { foreach (var g in AssetDatabase.FindAssets(name + " t:AudioClip")) { var p = AssetDatabase.GUIDToAssetPath(g); if (Path.GetFileNameWithoutExtension(p).ToLower() == name.ToLower()) return AssetDatabase.LoadAssetAtPath<AudioClip>(p); } return null; }
    static void AddToBuild(string path) { var scenes = EditorBuildSettings.scenes.ToList(); if (!scenes.Any(s => s.path == path)) { scenes.Add(new EditorBuildSettingsScene(path, true)); EditorBuildSettings.scenes = scenes.ToArray(); } }
}