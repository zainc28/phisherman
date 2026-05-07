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
/// Builds (and rebuilds) the Spot the Difference scene from scratch.
///
/// Run via:    Phisherman > Build Spot the Difference Scene
///
/// Output:     Assets/Scenes/SpotDifference.unity
///
/// Pattern matches EmailSwiperBuilder / TowerDefenseBuilder. Re-running
/// fully overwrites the scene, so the source of truth is this file —
/// edit the email content / red flags / explanations here, then re-run.
/// </summary>
public static class SpotDifferenceBuilder
{
    private const string ScenesDir = "Assets/Scenes";
    private const string ScenePath = "Assets/Scenes/SpotDifference.unity";

    // === Visual constants ===
    private static readonly Color BgColor = Hex("#FFE9C4"); // warm cream backdrop
    private static readonly Color HudColor = Hex("#5DA831"); // green HUD bar
    private static readonly Color HudText = Color.white;
    private static readonly Color EmailBg = Color.white;
    private static readonly Color HeaderScam = Hex("#C7503A"); // red strip on scam panel
    private static readonly Color HeaderReal = Hex("#3A8DC7"); // blue strip on real panel
    private static readonly Color BodyText = Hex("#222222");
    private static readonly Color MutedText = Hex("#555555");
    private static readonly Color CtaScam = Hex("#C7503A");
    private static readonly Color CtaReal = Hex("#FF9900"); // amazon-ish orange
    private static readonly Color OverlayColor = new Color(0f, 0f, 0f, 0.65f);
    private static readonly Color HintText = Hex("#553311");

    [MenuItem("Phisherman/Build Spot the Difference Scene")]
    public static void Build()
    {
        if (!Directory.Exists(ScenesDir)) Directory.CreateDirectory(ScenesDir);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // === Camera ===
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        camGo.AddComponent<AudioListener>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = BgColor;
        cam.orthographic = true;

        // === EventSystem ===
        // NOTE: uses legacy StandaloneInputModule. If your project uses the
        // new Input System exclusively, swap this for InputSystemUIInputModule.
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();

        // === Canvas ===
        var canvasGo = new GameObject("Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        var canvasRT = canvasGo.GetComponent<RectTransform>();

        // === Manager ===
        var managerGo = new GameObject("GameManager");
        var manager = managerGo.AddComponent<SpotDifferenceManager>();

        // === Background ===
        var bg = AddImage(canvasRT, "Background", BgColor);
        Stretch(bg.rectTransform);
        bg.raycastTarget = false;

        // === HUD strip across the top ===
        var hud = AddImage(canvasRT, "HUD", HudColor);
        AnchorTopStretch(hud.rectTransform, height: 120);
        hud.raycastTarget = false;

        var title = AddText(hud.rectTransform, "Title",
            "Spot the Phishing Red Flags",
            48, HudText, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        AnchorRect(title.rectTransform,
            new Vector2(0, 0), new Vector2(0.55f, 1),
            new Vector2(40, 0), Vector2.zero);

        var counter = AddText(hud.rectTransform, "Counter",
            "Found: 0 / 5",
            40, HudText, TextAlignmentOptions.Midline, FontStyles.Bold);
        AnchorRect(counter.rectTransform,
            new Vector2(0.55f, 0), new Vector2(0.78f, 1),
            Vector2.zero, Vector2.zero);

        var score = AddText(hud.rectTransform, "Score",
            "Score: 0",
            40, HudText, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
        AnchorRect(score.rectTransform,
            new Vector2(0.78f, 0), new Vector2(1, 1),
            Vector2.zero, new Vector2(-40, 0));

        // === Floating feedback popup just below the HUD ===
        var feedback = AddText(canvasRT, "Feedback",
            string.Empty,
            52, Color.green, TextAlignmentOptions.Center, FontStyles.Bold);
        var fbRT = feedback.rectTransform;
        fbRT.anchorMin = new Vector2(0.5f, 1f);
        fbRT.anchorMax = new Vector2(0.5f, 1f);
        fbRT.pivot = new Vector2(0.5f, 1f);
        fbRT.sizeDelta = new Vector2(1100, 70);
        fbRT.anchoredPosition = new Vector2(0, -135);
        feedback.raycastTarget = false;

        // === Email panels: real on the left (reference), scam on the right (interactive) ===
        BuildEmailPanel(canvasRT, manager, isScam: false,
            anchorMin: new Vector2(0.04f, 0.10f),
            anchorMax: new Vector2(0.49f, 0.85f));

        BuildEmailPanel(canvasRT, manager, isScam: true,
            anchorMin: new Vector2(0.51f, 0.10f),
            anchorMax: new Vector2(0.96f, 0.85f));

        // === Hint at the bottom ===
        var hint = AddText(canvasRT, "Hint",
            "Click the 5 phishing red flags in the email on the right.",
            32, HintText, TextAlignmentOptions.Center, FontStyles.Italic);
        var hintRT = hint.rectTransform;
        hintRT.anchorMin = new Vector2(0, 0);
        hintRT.anchorMax = new Vector2(1, 0);
        hintRT.pivot = new Vector2(0.5f, 0);
        hintRT.sizeDelta = new Vector2(0, 50);
        hintRT.anchoredPosition = new Vector2(0, 30);
        hint.raycastTarget = false;

        // === Result panel (initially hidden) ===
        var resultPanel = BuildResultPanel(canvasRT, manager);

        // === Wire up manager refs ===
        manager.scoreText = score;
        manager.counterText = counter;
        manager.feedbackText = feedback;
        manager.resultPanel = resultPanel;

        // Commentator (grandma's reactive speech bubble in bottom-left)
        manager.commentator = BuildCommentator(canvasRT);

        // === Save ===
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);

        AddSceneToBuildSettings(ScenePath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[SpotDifferenceBuilder] Built scene at {ScenePath}");
    }

    // ======================================================================
    // Email panel
    // ======================================================================

    /// <summary>
    /// Builds one of the two email panels. The real panel is non-interactive
    /// reference. The scam panel has invisible DifferenceMarker click zones
    /// over each red flag, plus a PanelClickReceiver that catches misses.
    /// </summary>
    private static void BuildEmailPanel(
        RectTransform parent, SpotDifferenceManager manager, bool isScam,
        Vector2 anchorMin, Vector2 anchorMax)
    {
        var panel = AddImage(parent, isScam ? "ScamEmail" : "RealEmail", EmailBg);
        var rt = panel.rectTransform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        panel.raycastTarget = false; // header & padding don't trigger wrong-click

        // --- Colored header strip ---
        var header = AddImage(rt, "Header", isScam ? HeaderScam : HeaderReal);
        AnchorTopStretch(header.rectTransform, height: 60);
        header.raycastTarget = false;

        var headerLabel = AddText(header.rectTransform, "HeaderLabel",
            isScam ? "PHISHING EXAMPLE — find 5 red flags" : "LEGITIMATE EMAIL — for comparison",
            26, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(headerLabel.rectTransform);
        headerLabel.raycastTarget = false;

        // --- Body content area (the "wrong click" zone for scam panel) ---
        var content = AddImage(rt, "Content", new Color(1f, 1f, 1f, 0f));
        var crt = content.rectTransform;
        crt.anchorMin = new Vector2(0, 0);
        crt.anchorMax = new Vector2(1, 1);
        crt.offsetMin = new Vector2(30, 30);
        crt.offsetMax = new Vector2(-30, -90); // 60 header + 30 padding
        content.raycastTarget = isScam;

        if (isScam)
        {
            var receiver = content.gameObject.AddComponent<PanelClickReceiver>();
            receiver.manager = manager;
        }

        // Stack rows top-to-bottom. y is distance from top of `content`.
        float y = 0;
        const float rowGap = 12;

        // ============ Row 1: Sender ============
        string sender = isScam
            ? "From: Amaz0n Security <security@amaz0n-shipping.net>"
            : "From: Amazon Shipping <ship-confirm@amazon.com>";
        var senderRow = AddRow(crt, "Sender", sender, 28, MutedText, FontStyles.Normal, ref y, 50);
        if (isScam)
            AddMarker(senderRow, manager,
                "Spoofed sender domain",
                "Real Amazon emails come from amazon.com. Lookalike domains like 'amaz0n-shipping.net' (note the zero, and the unfamiliar suffix) are a classic phishing tactic. Always check the full domain after the @ symbol.");
        y += rowGap;

        // ============ Row 2: Subject ============
        string subject = isScam
            ? "Subject: URGENT: Account suspended in 24 hours!"
            : "Subject: Your package will arrive Friday";
        var subjectRow = AddRow(crt, "Subject", subject, 30, BodyText, FontStyles.Bold, ref y, 55);
        if (isScam)
            AddMarker(subjectRow, manager,
                "Urgency and threat language",
                "Words like 'URGENT', 'suspended', and time pressure ('24 hours') are designed to short-circuit your judgment. Real companies describe what the message is about ('Your package shipped'); they don't threaten you in subject lines.");
        y += rowGap * 1.5f;

        // ============ Divider ============
        var divider = AddImage(crt, "Divider", new Color(0, 0, 0, 0.15f));
        AnchorTopStretch(divider.rectTransform, height: 2, topInset: y);
        divider.raycastTarget = false;
        y += 2 + rowGap;

        // ============ Row 3: Greeting ============
        string greeting = isScam ? "Dear Valued Customer," : "Hi John,";
        var greetingRow = AddRow(crt, "Greeting", greeting, 28, BodyText, FontStyles.Normal, ref y, 45);
        if (isScam)
            AddMarker(greetingRow, manager,
                "Generic greeting",
                "'Dear Valued Customer' suggests the sender doesn't actually know who you are. Companies you have accounts with normally use your real name. A generic greeting on an 'urgent' email is a strong red flag.");
        y += rowGap;

        // ============ Row 4: Body line 1 (no flag) ============
        AddRow(crt, "BodyLine1", "Your order has shipped! Track your", 26, BodyText, FontStyles.Normal, ref y, 38);

        // ============ Row 5: Body line 2 (spelling error on scam) ============
        string body2 = isScam ? "pakage at the link below." : "package at the link below.";
        var body2Row = AddRow(crt, "BodyLine2", body2, 26, BodyText, FontStyles.Normal, ref y, 38);
        if (isScam)
            AddMarker(body2Row, manager,
                "Spelling / grammar errors",
                "'Pakage' is misspelled. Real companies proofread their communications. Misspellings, awkward grammar, or odd capitalization are common in scams — attackers often work in a hurry or in a second language.");
        y += rowGap;

        // ============ Row 6: Order line (no flag, identical) ============
        AddRow(crt, "OrderLine", "Order #: 113-4567890", 24, MutedText, FontStyles.Italic, ref y, 38);
        y += rowGap * 2;

        // ============ Row 7: CTA button ============
        var ctaContainer = AddImage(crt, "CtaContainer", new Color(1, 1, 1, 0));
        var ccRT = ctaContainer.rectTransform;
        ccRT.anchorMin = new Vector2(0.5f, 1);
        ccRT.anchorMax = new Vector2(0.5f, 1);
        ccRT.pivot = new Vector2(0.5f, 1);
        ccRT.sizeDelta = new Vector2(420, 75);
        ccRT.anchoredPosition = new Vector2(0, -y);
        ctaContainer.raycastTarget = false;

        var cta = AddImage(ccRT, "CtaButton", isScam ? CtaScam : CtaReal);
        Stretch(cta.rectTransform);
        cta.raycastTarget = false;
        var ctaLabel = AddText(cta.rectTransform, "CtaLabel",
            isScam ? "VERIFY ACCOUNT NOW" : "View Order Details",
            26, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(ctaLabel.rectTransform);
        ctaLabel.raycastTarget = false;

        if (isScam)
            AddMarker(ctaContainer.gameObject, manager,
                "Suspicious call to action",
                "'VERIFY ACCOUNT NOW' demands urgent, vague action — a hallmark of phishing. Legitimate buttons describe a specific task ('View Order Details', 'Track Package'). If a button is pressuring you, hover over the link before clicking and check where it actually goes.");

        y += 75 + rowGap;

        // ============ Row 8: Footer (identical, no flag) ============
        AddRow(crt, "Footer", "Need help? Visit our Help Center.", 22, MutedText, FontStyles.Italic, ref y, 36);
    }

    /// <summary>
    /// Adds a horizontally-stretched row anchored to the top of `parent`,
    /// with a TMP text inside. Advances `y` by the row height so the next
    /// row lands directly below.
    /// </summary>
    private static GameObject AddRow(
        RectTransform parent, string name, string text,
        int fontSize, Color color, FontStyles style,
        ref float y, float height)
    {
        var row = AddImage(parent, name, new Color(1, 1, 1, 0));
        var rrt = row.rectTransform;
        rrt.anchorMin = new Vector2(0, 1);
        rrt.anchorMax = new Vector2(1, 1);
        rrt.pivot = new Vector2(0.5f, 1);
        rrt.sizeDelta = new Vector2(0, height);
        rrt.anchoredPosition = new Vector2(0, -y);
        row.raycastTarget = false;

        var t = AddText(rrt, "Text", text, fontSize, color, TextAlignmentOptions.MidlineLeft, style);
        Stretch(t.rectTransform);
        t.raycastTarget = false;

        y += height;
        return row.gameObject;
    }

    /// <summary>
    /// Drops an invisible click-zone child onto a row that contains a red
    /// flag. The zone is sized to fully cover its parent row and registered
    /// with the manager.
    /// </summary>
    private static void AddMarker(
        GameObject parent, SpotDifferenceManager manager,
        string flagName, string explanation)
    {
        var go = new GameObject("DiffMarker_" + Sanitize(flagName), typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);

        var img = go.AddComponent<Image>();
        img.color = new Color(1, 0, 0, 0); // invisible until found
        img.raycastTarget = true;
        Stretch(img.rectTransform);

        var marker = go.AddComponent<DifferenceMarker>();
        marker.flagName = flagName;
        marker.explanation = explanation;
        marker.manager = manager;

        manager.markers.Add(marker);
    }

    // ======================================================================
    // Result panel (full-screen overlay shown when all flags are found)
    // ======================================================================

    private static GameObject BuildResultPanel(RectTransform parent, SpotDifferenceManager manager)
    {
        var overlay = AddImage(parent, "ResultOverlay", OverlayColor);
        Stretch(overlay.rectTransform);
        overlay.raycastTarget = true; // blocks clicks behind

        var panel = AddImage(overlay.rectTransform, "ResultPanel", Color.white);
        var prt = panel.rectTransform;
        prt.anchorMin = new Vector2(0.5f, 0.5f);
        prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(1100, 800);

        var title = AddText(prt, "Title",
            "You spotted all the red flags!",
            54, Hex("#222222"), TextAlignmentOptions.Center, FontStyles.Bold);
        var trt = title.rectTransform;
        trt.anchorMin = new Vector2(0, 1);
        trt.anchorMax = new Vector2(1, 1);
        trt.pivot = new Vector2(0.5f, 1);
        trt.sizeDelta = new Vector2(-80, 80);
        trt.anchoredPosition = new Vector2(0, -40);

        var breakdown = AddText(prt, "Breakdown",
            string.Empty,
            22, Hex("#333333"), TextAlignmentOptions.TopLeft, FontStyles.Normal);
        var brt = breakdown.rectTransform;
        brt.anchorMin = new Vector2(0, 0);
        brt.anchorMax = new Vector2(1, 1);
        brt.pivot = new Vector2(0.5f, 0.5f);
        brt.offsetMin = new Vector2(60, 180);
        brt.offsetMax = new Vector2(-60, -140);

        var scoreText = AddText(prt, "Score",
            "Final Score: 0",
            38, Hex("#222222"), TextAlignmentOptions.Center, FontStyles.Bold);
        var srt = scoreText.rectTransform;
        srt.anchorMin = new Vector2(0, 0);
        srt.anchorMax = new Vector2(1, 0);
        srt.pivot = new Vector2(0.5f, 0);
        srt.sizeDelta = new Vector2(-80, 60);
        srt.anchoredPosition = new Vector2(0, 110);

        var playAgain = AddButton(prt, "PlayAgainBtn", "Play Again", 28, Hex("#5DA831"), Color.white);
        var part = playAgain.GetComponent<RectTransform>();
        part.anchorMin = new Vector2(0.5f, 0);
        part.anchorMax = new Vector2(0.5f, 0);
        part.pivot = new Vector2(1f, 0);
        part.sizeDelta = new Vector2(260, 70);
        part.anchoredPosition = new Vector2(-20, 30);
        playAgain.GetComponent<Button>().onClick.AddListener(manager.PlayAgain);

        var menu = AddButton(prt, "MenuBtn", "Back to Map", 28, Hex("#777777"), Color.white);
        var mrt = menu.GetComponent<RectTransform>();
        mrt.anchorMin = new Vector2(0.5f, 0);
        mrt.anchorMax = new Vector2(0.5f, 0);
        mrt.pivot = new Vector2(0f, 0);
        mrt.sizeDelta = new Vector2(260, 70);
        mrt.anchoredPosition = new Vector2(20, 30);
        menu.GetComponent<Button>().onClick.AddListener(manager.BackToWorldMap);

        manager.resultTitle = title;
        manager.resultBreakdown = breakdown;
        manager.resultScore = scoreText;

        overlay.gameObject.SetActive(false);
        return overlay.gameObject;
    }

    // ======================================================================
    // Commentator (reactive speech bubble — bottom-left)
    // ======================================================================

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

    private static Sprite TryGetCircleSprite()
    {
        try { return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"); }
        catch { return null; }
    }

    // ======================================================================
    // Build settings
    // ======================================================================

    private static void AddSceneToBuildSettings(string path)
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(s => s.path == path))
        {
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[SpotDifferenceBuilder] Added {path} to Build Settings.");
        }
    }

    // ======================================================================
    // Tiny UI helpers
    // ======================================================================

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
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = align;
        tmp.fontStyle = style;
        tmp.raycastTarget = false;
        return tmp;
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

    private static void AnchorTopStretch(RectTransform rt, float height, float topInset = 0)
    {
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.sizeDelta = new Vector2(0, height);
        rt.anchoredPosition = new Vector2(0, -topInset);
    }

    private static void AnchorRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }

    private static Color Hex(string hex)
    {
        return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
    }

    private static string Sanitize(string s)
    {
        return new string(s.Where(c => char.IsLetterOrDigit(c)).ToArray());
    }
}