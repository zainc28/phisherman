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
/// Builds the World Map (hub) scene from scratch.
///
/// Run via:    Phisherman > Build World Map Scene
/// Output:     Assets/Scenes/WorldMap.unity
///
/// Creates a top-down 2D scene: sky + grass background, three Club Penguin
/// style buildings across the top, a path running through the middle,
/// the player character (WASD-controlled), grandma NPC with a bobbing
/// exclamation mark, and a Screen Space Overlay canvas with the dialogue UI.
///
/// Re-running fully overwrites the scene.
/// </summary>
public static class WorldMapBuilder
{
    private const string ScenesDir = "Assets/Scenes";
    private const string ScenePath = "Assets/Scenes/WorldMap.unity";

    // Layout
    private const float CameraOrthoSize = 5f;

    // Palette
    private static readonly Color SkyColor = Hex("#7FC9E8");
    private static readonly Color GrassColor = Hex("#7BC76A");
    private static readonly Color GrassDark = Hex("#5BA34F");
    private static readonly Color PathColor = Hex("#D4B886");
    private static readonly Color PathDark = Hex("#B89868");

    private static readonly Color PlayerColor = Hex("#3D7DD9");
    private static readonly Color GrandmaPink = Hex("#F5A8B8");
    private static readonly Color GrandmaSkirt = Hex("#B86987");
    private static readonly Color ExclamColor = Hex("#FFD93D");

    // Dialogue UI
    private static readonly Color DialogueBg = Color.white;
    private static readonly Color DialogueDim = new Color(0, 0, 0, 0.55f);
    private static readonly Color DarkText = Hex("#202124");
    private static readonly Color MutedText = Hex("#5F6368");
    private static readonly Color AcceptGreen = Hex("#2ECC71");
    private static readonly Color DeclineGrey = Hex("#7F8C8D");
    private static readonly Color HeaderPink = Hex("#E91E63");

    [MenuItem("Phisherman/Build World Map Scene")]
    public static void Build()
    {
        if (!Directory.Exists(ScenesDir)) Directory.CreateDirectory(ScenesDir);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var whiteSprite = EnsureWhitePixelSprite();
        var circleSprite = TryGetCircleSprite();

        // === Camera ===
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        camGo.AddComponent<AudioListener>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = SkyColor;
        cam.orthographic = true;
        cam.orthographicSize = CameraOrthoSize;
        camGo.transform.position = new Vector3(0, 0, -10);

        // === EventSystem ===
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();

        // === World ===
        BuildSky(whiteSprite);
        BuildGrass(whiteSprite);
        BuildPath(whiteSprite);
        BuildBuildings(whiteSprite);

        var player = BuildPlayer(circleSprite, whiteSprite);
        var (grandma, grandmaCollider) = BuildGrandma(circleSprite, whiteSprite);
        var exclamation = BuildExclamationMark();

        // === Manager ===
        var managerGo = new GameObject("GameManager");
        var manager = managerGo.AddComponent<WorldMapManager>();
        manager.playerTransform = player.transform;
        manager.npcTransforms = new Transform[] { grandma.transform };
        manager.exclamationMark = exclamation.transform;

        // Tag grandma collider with NPCMarker
        var marker = grandmaCollider.AddComponent<NPCMarker>();
        marker.npcIndex = 0;

        // === Canvas (overlay) ===
        var canvasGo = new GameObject("Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        var canvasRT = canvasGo.GetComponent<RectTransform>();

        BuildHelperHUD(canvasRT);
        BuildDialogueUI(canvasRT, manager);

        // === Save ===
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings(ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[WorldMapBuilder] Built scene at {ScenePath}");
    }

    // =====================================================================
    // World layers
    // =====================================================================

    private static void BuildSky(Sprite whiteSprite)
    {
        var sky = CreateSpriteRect("Sky", whiteSprite, SkyColor,
            new Vector3(0, 3.5f, 0), new Vector3(20f, 4f, 1f), sortingOrder: -10);
    }

    private static void BuildGrass(Sprite whiteSprite)
    {
        // Lower grass band
        CreateSpriteRect("Grass", whiteSprite, GrassColor,
            new Vector3(0, -2f, 0), new Vector3(20f, 7f, 1f), sortingOrder: -9);

        // Darker grass at very bottom for depth
        CreateSpriteRect("GrassFront", whiteSprite, GrassDark,
            new Vector3(0, -4.3f, 0), new Vector3(20f, 1.5f, 1f), sortingOrder: -8);
    }

    private static void BuildPath(Sprite whiteSprite)
    {
        // Horizontal path strip
        CreateSpriteRect("Path", whiteSprite, PathColor,
            new Vector3(0, -1f, 0), new Vector3(20f, 1.6f, 1f), sortingOrder: -5);

        // Path edge shading
        CreateSpriteRect("PathEdgeTop", whiteSprite, PathDark,
            new Vector3(0, -0.18f, 0), new Vector3(20f, 0.06f, 1f), sortingOrder: -4);
        CreateSpriteRect("PathEdgeBot", whiteSprite, PathDark,
            new Vector3(0, -1.82f, 0), new Vector3(20f, 0.06f, 1f), sortingOrder: -4);
    }

    // =====================================================================
    // Buildings
    // =====================================================================

    private static void BuildBuildings(Sprite whiteSprite)
    {
        // Three buildings across the top of the map
        BuildBuilding(whiteSprite, new Vector3(-5.5f, 2.0f, 0),
            width: 2.4f, height: 2.0f,
            body: Hex("#F5DEB3"), roof: Hex("#A0522D"),
            door: Hex("#5C3317"), window: Hex("#FFE680"),
            label: null);

        BuildBuilding(whiteSprite, new Vector3(-1.5f, 2.1f, 0),
            width: 2.6f, height: 2.4f,
            body: Hex("#FFD9DD"), roof: Hex("#B85A82"),
            door: Hex("#6E3B53"), window: Hex("#FFF5C8"),
            label: "Grandma's");

        BuildBuilding(whiteSprite, new Vector3(2.8f, 2.2f, 0),
            width: 3.0f, height: 2.6f,
            body: Hex("#FFE680"), roof: Hex("#D9531E"),
            door: Hex("#5C3317"), window: Hex("#9FE2FF"),
            label: "Shop");

        BuildBuilding(whiteSprite, new Vector3(6.5f, 1.9f, 0),
            width: 2.2f, height: 1.8f,
            body: Hex("#A4D9A4"), roof: Hex("#5C3317"),
            door: Hex("#3D2515"), window: Hex("#FFE680"),
            label: null);
    }

    private static void BuildBuilding(Sprite whiteSprite, Vector3 pos,
        float width, float height,
        Color body, Color roof, Color door, Color window,
        string label)
    {
        var go = new GameObject("Building");
        go.transform.position = pos;

        float halfH = height / 2f;
        float halfW = width / 2f;

        // Body
        CreateSpriteRect("Body", whiteSprite, body,
            Vector3.zero, new Vector3(width, height, 1f),
            parent: go.transform, sortingOrder: 0);

        // Roof (slightly wider, on top, darker color)
        CreateSpriteRect("Roof", whiteSprite, roof,
            new Vector3(0, halfH + 0.18f, 0),
            new Vector3(width + 0.4f, 0.42f, 1f),
            parent: go.transform, sortingOrder: 1);

        // Roof "peak" — small extra band giving a stepped feel
        CreateSpriteRect("RoofTop", whiteSprite,
            DarkenColor(roof, 0.15f),
            new Vector3(0, halfH + 0.45f, 0),
            new Vector3(width * 0.6f, 0.18f, 1f),
            parent: go.transform, sortingOrder: 1);

        // Door
        CreateSpriteRect("Door", whiteSprite, door,
            new Vector3(0, -halfH + 0.45f, 0),
            new Vector3(0.55f, 0.85f, 1f),
            parent: go.transform, sortingOrder: 1);

        // Door knob
        CreateSpriteRect("DoorKnob", whiteSprite, Hex("#FFD93D"),
            new Vector3(0.16f, -halfH + 0.45f, 0),
            new Vector3(0.06f, 0.06f, 1f),
            parent: go.transform, sortingOrder: 2);

        // Two windows
        CreateSpriteRect("Window1", whiteSprite, window,
            new Vector3(-halfW * 0.6f, 0.2f, 0),
            new Vector3(0.45f, 0.45f, 1f),
            parent: go.transform, sortingOrder: 1);
        CreateSpriteRect("Window2", whiteSprite, window,
            new Vector3(halfW * 0.6f, 0.2f, 0),
            new Vector3(0.45f, 0.45f, 1f),
            parent: go.transform, sortingOrder: 1);

        // Window cross
        CreateSpriteRect("Win1Cross", whiteSprite, body,
            new Vector3(-halfW * 0.6f, 0.2f, 0),
            new Vector3(0.45f, 0.04f, 1f),
            parent: go.transform, sortingOrder: 2);
        CreateSpriteRect("Win2Cross", whiteSprite, body,
            new Vector3(halfW * 0.6f, 0.2f, 0),
            new Vector3(0.45f, 0.04f, 1f),
            parent: go.transform, sortingOrder: 2);

        if (!string.IsNullOrEmpty(label))
        {
            CreateWorldLabel(go.transform, "Sign", label,
                Color.white, DarkenColor(roof, 0.2f),
                new Vector3(0, halfH + 0.85f, -0.1f),
                width: 1.8f, height: 0.45f);
        }
    }

    // =====================================================================
    // Player + NPC
    // =====================================================================

    private static GameObject BuildPlayer(Sprite circleSprite, Sprite whiteSprite)
    {
        var player = new GameObject("Player");
        player.transform.position = new Vector3(-3f, -1f, 0);

        // Head (circle)
        var head = CreateSpriteRect("Head", circleSprite, PlayerColor,
            new Vector3(0, 0.15f, 0), new Vector3(0.7f, 0.7f, 1f),
            parent: player.transform, sortingOrder: 5);
        if (head.sprite == null) head.sprite = whiteSprite;

        // Body
        CreateSpriteRect("Body", whiteSprite, PlayerColor,
            new Vector3(0, -0.45f, 0), new Vector3(0.6f, 0.7f, 1f),
            parent: player.transform, sortingOrder: 4);

        // Letter "P" on head
        CreateWorldLabel(head.transform, "Letter", "P", Color.white,
            new Color(0, 0, 0, 0),
            new Vector3(0, 0, -0.1f), 0.5f, 0.5f, fontSize: 56);

        return player;
    }

    private static (GameObject root, GameObject collider) BuildGrandma(
        Sprite circleSprite, Sprite whiteSprite)
    {
        var grandma = new GameObject("Grandma");
        grandma.transform.position = new Vector3(0f, -0.9f, 0);

        // Head
        var head = CreateSpriteRect("Head", circleSprite, GrandmaPink,
            new Vector3(0, 0.25f, 0), new Vector3(0.85f, 0.85f, 1f),
            parent: grandma.transform, sortingOrder: 5);
        if (head.sprite == null) head.sprite = whiteSprite;

        // Hair (white, behind head)
        var hair = CreateSpriteRect("Hair", circleSprite,
            new Color(0.95f, 0.95f, 0.95f),
            new Vector3(0, 0.42f, 0), new Vector3(0.95f, 0.7f, 1f),
            parent: grandma.transform, sortingOrder: 4);
        if (hair.sprite == null) hair.sprite = whiteSprite;

        // Body / dress
        CreateSpriteRect("Dress", whiteSprite, GrandmaSkirt,
            new Vector3(0, -0.55f, 0), new Vector3(0.95f, 0.9f, 1f),
            parent: grandma.transform, sortingOrder: 4);

        // Letter "G" on head
        CreateWorldLabel(head.transform, "Letter", "G", DarkText,
            new Color(0, 0, 0, 0),
            new Vector3(0, 0, -0.1f), 0.5f, 0.5f, fontSize: 50);

        // Click target — invisible collider covering grandma + a little above
        // (so clicks on the exclamation mark also count)
        var clickTarget = new GameObject("ClickTarget");
        clickTarget.transform.SetParent(grandma.transform, false);
        clickTarget.transform.localPosition = new Vector3(0, 0.4f, 0);
        var col = clickTarget.AddComponent<BoxCollider2D>();
        col.size = new Vector2(1.4f, 2.6f);
        col.isTrigger = true;

        return (grandma, clickTarget);
    }

    private static GameObject BuildExclamationMark()
    {
        var go = new GameObject("ExclamationMark");
        // Position is set by manager dynamically (UpdateExclamationBob)
        go.transform.position = new Vector3(0f, 0.5f, 0);

        // World-space canvas for the "!"
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 6;

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(80, 80);
        go.transform.localScale = new Vector3(0.012f, 0.012f, 1f);

        // Background bubble (circle)
        var bg = AddImage(go.transform, "Bubble", ExclamColor);
        bg.sprite = TryGetCircleSprite();
        var brt = bg.rectTransform;
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
        brt.offsetMin = brt.offsetMax = Vector2.zero;
        bg.raycastTarget = false;

        // The "!"
        var bang = AddText(go.transform, "Bang", "!", 70,
            DarkText, TextAlignmentOptions.Center, FontStyles.Bold);
        var rt2 = bang.rectTransform;
        rt2.anchorMin = Vector2.zero; rt2.anchorMax = Vector2.one;
        rt2.offsetMin = rt2.offsetMax = Vector2.zero;
        bang.raycastTarget = false;

        return go;
    }

    // =====================================================================
    // Helper HUD (movement instructions in corner)
    // =====================================================================

    private static void BuildHelperHUD(RectTransform parent)
    {
        var hint = AddText(parent, "ControlsHint",
            "",
            22, Color.white, TextAlignmentOptions.MidlineLeft);
        hint.fontStyle = FontStyles.Bold;
        var rt = hint.rectTransform;
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.sizeDelta = new Vector2(900, 40);
        rt.anchoredPosition = new Vector2(30, -25);

        // Subtle text shadow for legibility on grass background — drop a
        // dark copy slightly offset behind it
        var shadow = AddText(parent, "ControlsHintShadow", hint.text,
            22, new Color(0, 0, 0, 0.6f), TextAlignmentOptions.MidlineLeft);
        shadow.fontStyle = FontStyles.Bold;
        var srt = shadow.rectTransform;
        srt.anchorMin = new Vector2(0, 1);
        srt.anchorMax = new Vector2(0, 1);
        srt.pivot = new Vector2(0, 1);
        srt.sizeDelta = new Vector2(900, 40);
        srt.anchoredPosition = new Vector2(32, -27);
        // Move shadow behind by setting it as earlier sibling
        shadow.transform.SetSiblingIndex(hint.transform.GetSiblingIndex());
    }

    // =====================================================================
    // Dialogue UI
    // =====================================================================

    private static void BuildDialogueUI(RectTransform parent, WorldMapManager manager)
    {
        // Root
        var panel = new GameObject("DialoguePanel", typeof(RectTransform));
        panel.transform.SetParent(parent, false);
        var prt = panel.GetComponent<RectTransform>();
        Stretch(prt);

        // Dim background
        var dim = AddImage(prt, "DimBg", DialogueDim);
        Stretch(dim.rectTransform);
        dim.raycastTarget = false;  // doesn't block — advance button below handles clicks

        // Full-screen advance button (transparent)
        var advance = new GameObject("AdvanceButton", typeof(RectTransform));
        advance.transform.SetParent(prt, false);
        Stretch(advance.GetComponent<RectTransform>());
        var advImg = advance.AddComponent<Image>();
        advImg.color = new Color(0, 0, 0, 0);
        advImg.raycastTarget = true;
        var advBtn = advance.AddComponent<Button>();
        advBtn.targetGraphic = advImg;
        UnityEventTools.AddPersistentListener(advBtn.onClick, manager.AdvanceDialogue);

        // Dialogue box at the bottom
        var box = AddImage(prt, "DialogueBox", DialogueBg);
        var brt = box.rectTransform;
        brt.anchorMin = new Vector2(0, 0);
        brt.anchorMax = new Vector2(1, 0);
        brt.pivot = new Vector2(0.5f, 0);
        brt.sizeDelta = new Vector2(-120, 280);
        brt.anchoredPosition = new Vector2(0, 40);
        box.raycastTarget = false;

        // Border
        var boxBorder = AddImage(box.rectTransform, "Border", HeaderPink);
        var bbrt = boxBorder.rectTransform;
        bbrt.anchorMin = new Vector2(0, 1);
        bbrt.anchorMax = new Vector2(1, 1);
        bbrt.pivot = new Vector2(0.5f, 1);
        bbrt.sizeDelta = new Vector2(0, 8);
        boxBorder.raycastTarget = false;

        // Speaker name badge
        var nameBadge = AddImage(box.rectTransform, "SpeakerBadge", HeaderPink);
        var nbrt = nameBadge.rectTransform;
        nbrt.anchorMin = new Vector2(0, 1);
        nbrt.anchorMax = new Vector2(0, 1);
        nbrt.pivot = new Vector2(0, 1);
        nbrt.sizeDelta = new Vector2(220, 50);
        nbrt.anchoredPosition = new Vector2(40, 28);
        nameBadge.raycastTarget = false;

        var speakerName = AddText(nameBadge.rectTransform, "Name", "Grandma",
            26, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(speakerName.rectTransform);

        // Dialogue text
        var text = AddText(box.rectTransform, "DialogueText",
            "Dialogue line goes here.",
            28, DarkText, TextAlignmentOptions.Center);
        var trt = text.rectTransform;
        trt.anchorMin = new Vector2(0, 0);
        trt.anchorMax = new Vector2(1, 1);
        trt.offsetMin = new Vector2(60, 70);
        trt.offsetMax = new Vector2(-60, -30);

        // Continue hint
        var hint = AddText(box.rectTransform, "ContinueHint", "Click to continue...",
            18, MutedText, TextAlignmentOptions.MidlineRight);
        var hrt = hint.rectTransform;
        hrt.anchorMin = new Vector2(1, 0);
        hrt.anchorMax = new Vector2(1, 0);
        hrt.pivot = new Vector2(1, 0);
        hrt.sizeDelta = new Vector2(280, 30);
        hrt.anchoredPosition = new Vector2(-30, 20);

        // Choice panel — siblings AFTER advance button so its buttons take
        // priority for clicks within their bounds
        var choicePanel = new GameObject("ChoicePanel", typeof(RectTransform));
        choicePanel.transform.SetParent(prt, false);
        var crt = choicePanel.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0.5f, 0);
        crt.anchorMax = new Vector2(0.5f, 0);
        crt.pivot = new Vector2(0.5f, 0);
        crt.sizeDelta = new Vector2(820, 80);
        crt.anchoredPosition = new Vector2(0, 70);

        var accept = AddButton(choicePanel.transform, "AcceptBtn", "Help her",
            26, AcceptGreen, Color.white);
        var art = accept.GetComponent<RectTransform>();
        art.anchorMin = new Vector2(0, 0); art.anchorMax = new Vector2(0, 1);
        art.pivot = new Vector2(0, 0.5f);
        art.sizeDelta = new Vector2(380, 0);
        art.anchoredPosition = new Vector2(20, 0);
        var acceptBtn = accept.GetComponent<Button>();
        UnityEventTools.AddPersistentListener(acceptBtn.onClick, manager.OnAcceptHelp);

        var decline = AddButton(choicePanel.transform, "DeclineBtn", "Maybe later",
            26, DeclineGrey, Color.white);
        var drt = decline.GetComponent<RectTransform>();
        drt.anchorMin = new Vector2(1, 0); drt.anchorMax = new Vector2(1, 1);
        drt.pivot = new Vector2(1, 0.5f);
        drt.sizeDelta = new Vector2(380, 0);
        drt.anchoredPosition = new Vector2(-20, 0);
        var declineBtn = decline.GetComponent<Button>();
        UnityEventTools.AddPersistentListener(declineBtn.onClick, manager.OnDeclineHelp);

        // Wire manager refs
        manager.dialoguePanel = panel;
        manager.advanceButton = advBtn;
        manager.speakerNameText = speakerName;
        manager.dialogueText = text;
        manager.continueHint = hint;
        manager.choicePanel = choicePanel;
        manager.acceptButton = acceptBtn;
        manager.acceptButtonText = accept.transform.Find("Label").GetComponent<TMP_Text>();
        manager.declineButton = declineBtn;
        manager.declineButtonText = decline.transform.Find("Label").GetComponent<TMP_Text>();

        panel.SetActive(false);
    }

    // =====================================================================
    // Helpers
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

    private static void CreateWorldLabel(Transform parent, string name, string text,
        Color textColor, Color bgColor,
        Vector3 localPos, float width, float height,
        int fontSize = 28)
    {
        var canvasGo = new GameObject(name);
        canvasGo.transform.SetParent(parent, false);
        canvasGo.transform.localPosition = localPos;
        canvasGo.transform.localScale = new Vector3(0.01f, 0.01f, 1f);

        var c = canvasGo.AddComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace;
        c.sortingOrder = 4;

        var rt = canvasGo.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(width / 0.01f, height / 0.01f);

        if (bgColor.a > 0.05f)
        {
            var bg = AddImage(canvasGo.transform, "Bg", bgColor);
            Stretch(bg.rectTransform);
            bg.raycastTarget = false;
        }

        var label = AddText(canvasGo.transform, "Label", text,
            fontSize, textColor, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(label.rectTransform);
        label.raycastTarget = false;
    }

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

    private static Color Hex(string hex)
    {
        return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
    }

    private static Color DarkenColor(Color c, float amount)
    {
        return new Color(
            Mathf.Max(0, c.r - amount),
            Mathf.Max(0, c.g - amount),
            Mathf.Max(0, c.b - amount),
            c.a);
    }

    private static Sprite TryGetCircleSprite()
    {
        try { return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"); }
        catch { return null; }
    }

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

        // Always enforce importer settings — the file might have been imported
        // previously with Unity's defaults (PPU=100, type=Default), which
        // makes sprites render at ~8% of their intended size.
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