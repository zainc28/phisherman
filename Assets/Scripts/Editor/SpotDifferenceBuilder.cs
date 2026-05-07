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
/// Builds the Spot the Difference scene from scratch.
///
/// Run via:    Phisherman > Build Spot the Difference Scene
/// Output:     Assets/Scenes/SpotDifference.unity
///
/// Each email panel now looks like a real Gmail open-email view — subject
/// header, avatar + sender row, scrollable body — mirroring the Email
/// Swiper detail format. Both emails are long enough that the player must
/// scroll to read the full message.
/// </summary>
public static class SpotDifferenceBuilder
{
    private const string ScenesDir = "Assets/Scenes";
    private const string ScenePath = "Assets/Scenes/SpotDifference.unity";

    // ===== Palette =====
    private static readonly Color BgColor = Hex("#F6F8FC");
    private static readonly Color HudColor = Hex("#2E3A59");
    private static readonly Color HudText = Color.white;
    private static readonly Color OverlayColor = new Color(0f, 0f, 0f, 0.65f);
    private static readonly Color HintText = Hex("#553311");

    // Gmail panel
    private static readonly Color PanelBg = Color.white;
    private static readonly Color LabelRealBg = Hex("#E8F5E9");
    private static readonly Color LabelScamBg = Hex("#FCE8E6");
    private static readonly Color LabelRealText = Hex("#0D652D");
    private static readonly Color LabelScamText = Hex("#C5221F");
    private static readonly Color DividerColor = Hex("#E0E0E0");
    private static readonly Color BodyText = Hex("#202124");
    private static readonly Color MutedText = Hex("#5F6368");
    private static readonly Color ScamRed = Hex("#C5221F");
    private static readonly Color AmazonOrange = Hex("#E37400");
    private static readonly Color CtaReal = Hex("#0061D5");
    private static readonly Color CtaScam = Hex("#C5221F");

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
        AnchorTopStretch(hud.rectTransform, height: 110);
        hud.raycastTarget = false;

        var title = AddText(hud.rectTransform, "Title",
            "Spot the Phishing Red Flags",
            44, HudText, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        AnchorRect(title.rectTransform,
            new Vector2(0, 0), new Vector2(0.5f, 1),
            new Vector2(36, 0), Vector2.zero);

        var counter = AddText(hud.rectTransform, "Counter",
            "Found: 0 / 5",
            36, HudText, TextAlignmentOptions.Midline, FontStyles.Bold);
        AnchorRect(counter.rectTransform,
            new Vector2(0.5f, 0), new Vector2(0.76f, 1),
            Vector2.zero, Vector2.zero);

        var score = AddText(hud.rectTransform, "Score",
            "Score: 0",
            36, HudText, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
        AnchorRect(score.rectTransform,
            new Vector2(0.76f, 0), new Vector2(1, 1),
            Vector2.zero, new Vector2(-36, 0));

        // === Feedback popup below HUD ===
        var feedback = AddText(canvasRT, "Feedback",
            string.Empty,
            46, Color.green, TextAlignmentOptions.Center, FontStyles.Bold);
        var fbRT = feedback.rectTransform;
        fbRT.anchorMin = new Vector2(0.5f, 1f);
        fbRT.anchorMax = new Vector2(0.5f, 1f);
        fbRT.pivot = new Vector2(0.5f, 1f);
        fbRT.sizeDelta = new Vector2(1100, 64);
        fbRT.anchoredPosition = new Vector2(0, -124);
        feedback.raycastTarget = false;

        // === Email panels — real (left) and phishing (right) ===
        BuildEmailPanel(canvasRT, manager, isScam: false,
            new Vector2(0.025f, 0.07f), new Vector2(0.487f, 0.895f));

        BuildEmailPanel(canvasRT, manager, isScam: true,
            new Vector2(0.513f, 0.07f), new Vector2(0.975f, 0.895f));

        // === Hint at the bottom ===
        var hint = AddText(canvasRT, "Hint",
            "Find all 5 phishing red flags in the email on the right.  Scroll to read the full message.",
            26, HintText, TextAlignmentOptions.Center, FontStyles.Italic);
        var hintRT = hint.rectTransform;
        hintRT.anchorMin = new Vector2(0, 0);
        hintRT.anchorMax = new Vector2(1, 0);
        hintRT.pivot = new Vector2(0.5f, 0);
        hintRT.sizeDelta = new Vector2(0, 44);
        hintRT.anchoredPosition = new Vector2(0, 12);
        hint.raycastTarget = false;

        // === Result panel (initially hidden) ===
        var resultPanel = BuildResultPanel(canvasRT, manager);

        // === Wire manager refs ===
        manager.scoreText = score;
        manager.counterText = counter;
        manager.feedbackText = feedback;
        manager.resultPanel = resultPanel;

        // Grandma commentator (bottom-left)
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
    // Gmail-styled email panel
    // ======================================================================

    private static void BuildEmailPanel(
        RectTransform parent, SpotDifferenceManager manager, bool isScam,
        Vector2 anchorMin, Vector2 anchorMax)
    {
        var panel = AddImage(parent, isScam ? "ScamEmail" : "RealEmail", PanelBg);
        var rt = panel.rectTransform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        panel.raycastTarget = false;

        // --- Label strip (real vs phishing indicator) ---
        var labelBar = AddImage(rt, "LabelBar", isScam ? LabelScamBg : LabelRealBg);
        AnchorTopStretch(labelBar.rectTransform, height: 42);
        labelBar.raycastTarget = false;
        var labelText = AddText(labelBar.rectTransform, "LabelText",
            isScam ? "PHISHING EXAMPLE — find 5 red flags"
                   : "LEGITIMATE EMAIL — for comparison",
            20, isScam ? LabelScamText : LabelRealText,
            TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(labelText.rectTransform);
        labelText.raycastTarget = false;

        // --- Subject area (big, outside scroll) ---
        var subjectArea = AddImage(rt, "SubjectArea", PanelBg);
        var sart = subjectArea.rectTransform;
        sart.anchorMin = new Vector2(0, 1);
        sart.anchorMax = new Vector2(1, 1);
        sart.pivot = new Vector2(0.5f, 1);
        sart.sizeDelta = new Vector2(0, 70);
        sart.anchoredPosition = new Vector2(0, -42);
        subjectArea.raycastTarget = false;

        string subjectStr = isScam
            ? "URGENT: Account suspended in 24 hours!"
            : "Your package will arrive Friday";
        var subjectTmp = AddText(subjectArea.rectTransform, "SubjectText",
            subjectStr, 30, BodyText,
            TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        var strt = subjectTmp.rectTransform;
        strt.anchorMin = Vector2.zero; strt.anchorMax = Vector2.one;
        strt.offsetMin = new Vector2(24, 0);
        strt.offsetMax = Vector2.zero;
        subjectTmp.raycastTarget = false;

        if (isScam)
        {
            AddMarker(subjectArea.gameObject, manager,
                "Urgency and threat language",
                "'URGENT', 'suspended', and '24 hours' are designed to create panic. Real companies describe what the email is about calmly — they never threaten you in the subject line.");
        }

        // --- Sender row (avatar + name + email + timestamp, outside scroll) ---
        var senderRow = AddImage(rt, "SenderRow", PanelBg);
        var srrt = senderRow.rectTransform;
        srrt.anchorMin = new Vector2(0, 1);
        srrt.anchorMax = new Vector2(1, 1);
        srrt.pivot = new Vector2(0.5f, 1);
        srrt.sizeDelta = new Vector2(0, 68);
        srrt.anchoredPosition = new Vector2(0, -112);
        senderRow.raycastTarget = false;

        // Avatar circle
        var avatar = AddImage(senderRow.rectTransform, "Avatar", AmazonOrange);
        avatar.sprite = TryGetCircleSprite();
        avatar.preserveAspect = true;
        var avrt = avatar.rectTransform;
        avrt.anchorMin = new Vector2(0, 0.5f);
        avrt.anchorMax = new Vector2(0, 0.5f);
        avrt.pivot = new Vector2(0, 0.5f);
        avrt.sizeDelta = new Vector2(50, 50);
        avrt.anchoredPosition = new Vector2(22, 0);
        avatar.raycastTarget = false;
        var avLetter = AddText(avatar.rectTransform, "Letter", "A",
            28, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(avLetter.rectTransform);
        avLetter.raycastTarget = false;

        // Sender name
        string senderName = isScam ? "Amaz0n Security" : "Amazon";
        var nameT = AddText(senderRow.rectTransform, "SenderName", senderName,
            22, BodyText, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        var ntrt = nameT.rectTransform;
        ntrt.anchorMin = new Vector2(0, 0.5f); ntrt.anchorMax = new Vector2(0, 1);
        ntrt.pivot = new Vector2(0, 0.5f);
        ntrt.sizeDelta = new Vector2(360, 0);
        ntrt.anchoredPosition = new Vector2(86, -4);
        nameT.raycastTarget = false;

        // Sender email
        string senderEmail = isScam
            ? "<security@amaz0n-shipping.net>"
            : "<ship-confirm@amazon.com>";
        var emailT = AddText(senderRow.rectTransform, "SenderEmail", senderEmail,
            17, MutedText, TextAlignmentOptions.MidlineLeft);
        var etrt = emailT.rectTransform;
        etrt.anchorMin = new Vector2(0, 0); etrt.anchorMax = new Vector2(0, 0.5f);
        etrt.pivot = new Vector2(0, 0.5f);
        etrt.sizeDelta = new Vector2(420, 0);
        etrt.anchoredPosition = new Vector2(86, 4);
        emailT.raycastTarget = false;

        // Timestamp
        var tsT = AddText(senderRow.rectTransform, "Timestamp", "May 5, 4:03 PM",
            17, MutedText, TextAlignmentOptions.MidlineRight);
        var tsrt = tsT.rectTransform;
        tsrt.anchorMin = new Vector2(1, 0); tsrt.anchorMax = new Vector2(1, 1);
        tsrt.pivot = new Vector2(1, 0.5f);
        tsrt.sizeDelta = new Vector2(200, 0);
        tsrt.anchoredPosition = new Vector2(-22, 0);
        tsT.raycastTarget = false;

        if (isScam)
        {
            AddMarker(senderRow.gameObject, manager,
                "Spoofed sender domain",
                "The address ends in 'amaz0n-shipping.net' — note the zero instead of 'o', and an unfamiliar domain suffix. Real Amazon emails come from @amazon.com. Always check the full address after the @ symbol.");
        }

        // --- Thin divider ---
        var div = AddImage(rt, "HeaderDivider", DividerColor);
        var drt = div.rectTransform;
        drt.anchorMin = new Vector2(0, 1); drt.anchorMax = new Vector2(1, 1);
        drt.pivot = new Vector2(0.5f, 1);
        drt.sizeDelta = new Vector2(0, 1);
        drt.anchoredPosition = new Vector2(0, -180);
        div.raycastTarget = false;

        // --- Scrollable body ---
        float headerHeight = 181f;  // 42 + 70 + 68 + 1
        var scrollGo = new GameObject("BodyScroll", typeof(RectTransform));
        scrollGo.transform.SetParent(rt, false);
        var scrollRT = scrollGo.GetComponent<RectTransform>();
        scrollRT.anchorMin = new Vector2(0, 0);
        scrollRT.anchorMax = new Vector2(1, 1);
        scrollRT.offsetMin = Vector2.zero;
        scrollRT.offsetMax = new Vector2(0, -headerHeight);

        var scrollImg = scrollGo.AddComponent<Image>();
        scrollImg.color = PanelBg;
        scrollImg.raycastTarget = false;

        var scrollRect = scrollGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 35;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        // Viewport (with mask + wrong-click receiver on scam side)
        var vpGo = new GameObject("Viewport", typeof(RectTransform));
        vpGo.transform.SetParent(scrollRT, false);
        Stretch(vpGo.GetComponent<RectTransform>());

        var vpImg = vpGo.AddComponent<Image>();
        vpImg.color = PanelBg;
        vpImg.raycastTarget = isScam;
        if (isScam)
        {
            var recv = vpGo.AddComponent<PanelClickReceiver>();
            recv.manager = manager;
        }
        var mask = vpGo.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        // Content (VerticalLayoutGroup + ContentSizeFitter)
        var contentGo = new GameObject("BodyContent", typeof(RectTransform));
        contentGo.transform.SetParent(vpGo.transform, false);
        var contentRT = contentGo.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1);
        contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(0.5f, 1);
        contentRT.sizeDelta = new Vector2(0, 0);
        contentRT.anchoredPosition = Vector2.zero;

        var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.spacing = 6;
        vlg.padding = new RectOffset(28, 28, 22, 32);

        var csf = contentGo.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = vpGo.GetComponent<RectTransform>();
        scrollRect.content = contentRT;

        BuildEmailBody(contentGo.transform, manager, isScam);
    }

    // ======================================================================
    // Email body content
    // ======================================================================

    private static void BuildEmailBody(
        Transform content, SpotDifferenceManager manager, bool isScam)
    {
        // ---- Greeting ----
        string greeting = isScam ? "Dear Valued Customer," : "Hi John,";
        var greetingRow = BodyRow(content, "Greeting", greeting,
            26, BodyText, FontStyles.Normal);
        if (isScam)
            AddMarker(greetingRow, manager,
                "Generic greeting",
                "'Dear Valued Customer' shows the sender doesn't actually know who you are. Real companies with your account on file use your real name. A generic greeting on an 'urgent' security email is a strong red flag.");
        Spacer(content, 8);

        // ---- Opening paragraph ----
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
                "We have detected suspecious activity on your account and have " +
                "temporarily suspended your access as a precautionary measure. " +
                "You must verify your identity immediately to restore full account access.",
                24, BodyText, FontStyles.Normal);
            AddMarker(body1, manager,
                "Spelling error",
                "'Suspecious' is a misspelling of 'suspicious'. Real corporate communications are professionally written and proofread. Typos and awkward phrasing are common in phishing messages, often written quickly or in a second language.");
        }
        Spacer(content, 10);

        // ---- Order summary section ----
        BodyRow(content, "OrderLabel", "Order Summary:", 21, MutedText, FontStyles.Bold);
        Spacer(content, 2);
        BodyRow(content, "OrderItem", "    Echo Dot (5th Gen) — Charcoal × 1",
            22, BodyText, FontStyles.Normal);
        BodyRow(content, "OrderNum", "    Order #: 113-4567890",
            22, MutedText, FontStyles.Italic);
        if (!isScam)
        {
            BodyRow(content, "OrderDate", "    Estimated Delivery: Friday, May 10, 2024",
                22, MutedText, FontStyles.Italic);
            BodyRow(content, "OrderAddr", "    Shipping to: 142 Maple Street, Edmonton, AB",
                22, MutedText, FontStyles.Italic);
        }
        Spacer(content, 12);

        // ---- Second paragraph ----
        if (!isScam)
        {
            BodyRow(content, "Para2",
                "You can track your package in real time using the button below. " +
                "Our delivery partner will send you a separate notification when your " +
                "order is out for delivery.",
                24, BodyText, FontStyles.Normal);
        }
        else
        {
            BodyRow(content, "Para2",
                "To restore your account, click the button below and complete identity " +
                "verification. Failure to respond within 24 hours will result in " +
                "permanent account deletion and cancellation of all pending orders.",
                24, BodyText, FontStyles.Normal);
        }
        Spacer(content, 14);

        // ---- CTA button ----
        var ctaHolder = new GameObject("CtaHolder", typeof(RectTransform));
        ctaHolder.transform.SetParent(content, false);
        var ctaHolderImg = ctaHolder.AddComponent<Image>();
        ctaHolderImg.color = new Color(0, 0, 0, 0);
        ctaHolderImg.raycastTarget = false;
        var ctaLE = ctaHolder.AddComponent<LayoutElement>();
        ctaLE.preferredHeight = 72;

        var ctaBtn = AddImage(ctaHolder.GetComponent<RectTransform>(), "CtaBtn",
            isScam ? CtaScam : CtaReal);
        var cbrt = ctaBtn.rectTransform;
        cbrt.anchorMin = new Vector2(0.5f, 0.5f);
        cbrt.anchorMax = new Vector2(0.5f, 0.5f);
        cbrt.pivot = new Vector2(0.5f, 0.5f);
        cbrt.sizeDelta = new Vector2(340, 56);
        cbrt.anchoredPosition = Vector2.zero;
        ctaBtn.raycastTarget = false;

        var ctaLabel = AddText(ctaBtn.rectTransform, "Label",
            isScam ? "VERIFY ACCOUNT NOW" : "View Order Details",
            23, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(ctaLabel.rectTransform);
        ctaLabel.raycastTarget = false;

        if (isScam)
        {
            AddMarker(ctaHolder, manager,
                "Suspicious call to action",
                "'VERIFY ACCOUNT NOW' uses vague urgent language to pressure you into clicking. Legitimate email buttons describe specific tasks — 'View Order Details', 'Track Package'. Urgent, non-specific action buttons are a major phishing sign.");
        }
        Spacer(content, 14);

        // ---- Additional paragraphs (making email longer / scrollable) ----
        if (!isScam)
        {
            BodyRow(content, "Para3",
                "If you have any questions about your order or delivery, our customer " +
                "support team is available 24 hours a day, 7 days a week through the Amazon Help Center.",
                24, BodyText, FontStyles.Normal);
            Spacer(content, 10);
            BodyRow(content, "Para4",
                "As an Amazon Prime member, your package is eligible for our A-to-Z " +
                "Guarantee. If anything is wrong with your order upon arrival, we will " +
                "make it right — no questions asked.",
                24, BodyText, FontStyles.Normal);
            Spacer(content, 10);
            BodyRow(content, "Para5",
                "You have 30 days from the delivery date to initiate a return if needed. " +
                "Visit our Returns Center to get started.",
                24, BodyText, FontStyles.Normal);
            Spacer(content, 16);
            BodyRow(content, "Sig", "Thank you for shopping with Amazon!", 24, BodyText, FontStyles.Normal);
            BodyRow(content, "SigName", "— The Amazon Team", 22, MutedText, FontStyles.Italic);
        }
        else
        {
            BodyRow(content, "Para3",
                "For the verification process you will be required to confirm your Amazon " +
                "account password and provide your full billing information including credit " +
                "card details. This is a mandatory step that cannot be skipped.",
                24, BodyText, FontStyles.Normal);
            Spacer(content, 10);
            BodyRow(content, "Para4",
                "Please note: this is the only notice you will receive. We are unable to " +
                "process account recovery requests or refunds for accounts that have not " +
                "been verified within the required time window.",
                24, BodyText, FontStyles.Normal);
            Spacer(content, 10);
            BodyRow(content, "Para5",
                "Do not share this email or the verification link with anyone. This " +
                "link is unique to your account and expires in exactly 24 hours from " +
                "the time of this notice.",
                24, BodyText, FontStyles.Normal);
            Spacer(content, 16);
            BodyRow(content, "Sig", "— Amaz0n Security Team", 22, MutedText, FontStyles.Italic);
            BodyRow(content, "SigDept", "Account Protection Division", 20, MutedText, FontStyles.Normal);
        }

        // ---- Footer divider and footer text ----
        Spacer(content, 16);
        var footerDiv = AddImage(content, "FooterDiv", DividerColor);
        var fdLE = footerDiv.GetComponent<LayoutElement>() ?? footerDiv.gameObject.AddComponent<LayoutElement>();
        fdLE.preferredHeight = 1;
        footerDiv.raycastTarget = false;
        Spacer(content, 10);

        string footerStr = isScam
            ? "Questions? Contact us at: support@amaz0n-security.tk"
            : "Need help? Visit our Help Center   |   Manage your account";
        BodyRow(content, "Footer", footerStr, 18, MutedText, FontStyles.Normal);
    }

    // ---- Body row helpers ----

    private static GameObject BodyRow(Transform parent, string name, string text,
        int fontSize, Color color, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = fontSize;
        t.color = color;
        t.fontStyle = style;
        t.alignment = TextAlignmentOptions.TopLeft;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.raycastTarget = false;
        var le = go.AddComponent<LayoutElement>();
        le.flexibleWidth = 1;
        return go;
    }

    private static void Spacer(Transform parent, float height)
    {
        var go = new GameObject("Spacer", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.flexibleWidth = 1;
    }

    private static Image AddImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    private static void AddMarker(GameObject parent, SpotDifferenceManager manager,
        string flagName, string explanation)
    {
        var go = new GameObject("DiffMarker_" + Sanitize(flagName), typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        Stretch(go.GetComponent<RectTransform>());

        var img = go.AddComponent<Image>();
        img.color = new Color(1, 0, 0, 0);
        img.raycastTarget = true;

        var marker = go.AddComponent<DifferenceMarker>();
        marker.flagName = flagName;
        marker.explanation = explanation;
        marker.manager = manager;

        manager.markers.Add(marker);
    }

    // ======================================================================
    // Result panel
    // ======================================================================

    private static GameObject BuildResultPanel(RectTransform parent, SpotDifferenceManager manager)
    {
        var overlay = AddImage(parent, "ResultOverlay", OverlayColor);
        Stretch(overlay.rectTransform);
        overlay.raycastTarget = true;

        var panel = AddImage(overlay.rectTransform, "ResultPanel", Color.white);
        var prt = panel.rectTransform;
        prt.anchorMin = new Vector2(0.5f, 0.5f);
        prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(1100, 800);

        var header = AddImage(prt, "Header", HudColor);
        AnchorTopStretch(header.rectTransform, height: 90);
        var hLabel = AddText(header.rectTransform, "Title",
            "You spotted all the red flags!",
            44, HudText, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(hLabel.rectTransform);
        hLabel.raycastTarget = false;

        var breakdown = AddText(prt, "Breakdown", string.Empty,
            22, Hex("#333333"), TextAlignmentOptions.TopLeft);
        var brt = breakdown.rectTransform;
        brt.anchorMin = new Vector2(0, 0);
        brt.anchorMax = new Vector2(1, 1);
        brt.pivot = new Vector2(0.5f, 0.5f);
        brt.offsetMin = new Vector2(60, 190);
        brt.offsetMax = new Vector2(-60, -130);

        var scoreText = AddText(prt, "Score", "Final Score: 0",
            36, Hex("#222222"), TextAlignmentOptions.Center, FontStyles.Bold);
        var srt = scoreText.rectTransform;
        srt.anchorMin = new Vector2(0, 0);
        srt.anchorMax = new Vector2(1, 0);
        srt.pivot = new Vector2(0.5f, 0);
        srt.sizeDelta = new Vector2(-80, 56);
        srt.anchoredPosition = new Vector2(0, 120);

        var playAgain = AddButton(prt, "PlayAgainBtn", "Play Again", 28, Hex("#2ECC71"), Color.white);
        var part = playAgain.GetComponent<RectTransform>();
        part.anchorMin = new Vector2(0.5f, 0); part.anchorMax = new Vector2(0.5f, 0);
        part.pivot = new Vector2(1f, 0);
        part.sizeDelta = new Vector2(260, 70);
        part.anchoredPosition = new Vector2(-20, 32);
        playAgain.GetComponent<Button>().onClick.AddListener(manager.PlayAgain);

        var menu = AddButton(prt, "MenuBtn", "Back to Map", 28, Hex("#7F8C8D"), Color.white);
        var mrt = menu.GetComponent<RectTransform>();
        mrt.anchorMin = new Vector2(0.5f, 0); mrt.anchorMax = new Vector2(0.5f, 0);
        mrt.pivot = new Vector2(0f, 0);
        mrt.sizeDelta = new Vector2(260, 70);
        mrt.anchoredPosition = new Vector2(20, 32);
        menu.GetComponent<Button>().onClick.AddListener(manager.BackToWorldMap);

        manager.resultTitle = hLabel;
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
        prt.anchorMin = new Vector2(0, 0); prt.anchorMax = new Vector2(0, 1);
        prt.pivot = new Vector2(0, 0.5f);
        prt.sizeDelta = new Vector2(120, 0);
        prt.anchoredPosition = Vector2.zero;

        var letter = AddText(portrait.rectTransform, "Letter", "G",
            64, new Color(0.20f, 0.10f, 0.18f), TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(letter.rectTransform);

        var bubble = AddImage(rrt, "Bubble", Color.white);
        var brt = bubble.rectTransform;
        brt.anchorMin = new Vector2(0, 0); brt.anchorMax = new Vector2(1, 1);
        brt.pivot = new Vector2(0, 0.5f);
        brt.offsetMin = new Vector2(140, 0);
        brt.offsetMax = Vector2.zero;

        var speakerLabel = AddText(bubble.rectTransform, "SpeakerLabel", "Grandma", 18,
            new Color(0.78f, 0.30f, 0.50f), TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        var slrt = speakerLabel.rectTransform;
        slrt.anchorMin = new Vector2(0, 1); slrt.anchorMax = new Vector2(1, 1);
        slrt.pivot = new Vector2(0, 1);
        slrt.sizeDelta = new Vector2(0, 26);
        slrt.anchoredPosition = new Vector2(20, -8);

        var bubbleText = AddText(bubble.rectTransform, "BubbleText", "...", 22,
            new Color(0.13f, 0.13f, 0.13f), TextAlignmentOptions.MidlineLeft);
        bubbleText.textWrappingMode = TextWrappingModes.Normal;
        var btrt = bubbleText.rectTransform;
        btrt.anchorMin = new Vector2(0, 0); btrt.anchorMax = new Vector2(1, 1);
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
        rt.offsetMin = rt.offsetMax = Vector2.zero;
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