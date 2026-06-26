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
/// Builds the WorldMap scene.
///
/// - world_stage_1_background fills the camera.
/// - MapBlocker rects match the actual neighbourhood image:
///     three houses, fences, pond/river, flower beds, screen edges.
/// - MapNavAgent on the player does A* around blockers so clicking
///   an obstructed area routes around the obstacle automatically.
/// - MapCornerZone (200px screen-space) at bottom-left loads WorldMap2;
///   bottom-right shows the locked popup.
/// - MapPathTrigger proximity badges still exist at the path exits.
/// - M-key world map overlay, dialogue panel, door triggers all unchanged.
/// </summary>
public static class WorldMapBuilder
{
    private const string ScenesDir = "Assets/Scenes";
    private const string ScenePath = "Assets/Scenes/WorldMap.unity";

    private const float OrthoSize = 4.5f;
    private const float AspectW = 16f / 9f;

    private const float PlayerStartX = 0.0f;
    private const float PlayerStartY = -1.5f;

    // Walkable bounds — must match MapNavAgent.boundsX/Y
    private static readonly Vector2 BoundsX = new Vector2(-7.8f, 7.8f);
    private static readonly Vector2 BoundsY = new Vector2(-4.0f, 4.3f);

    // UI colours
    private static readonly Color AcceptCol = Hex("#2ECC71");
    private static readonly Color DeclineCol = Hex("#E74C3C");
    private static readonly Color NameCol = Hex("#FFD93D");
    private static readonly Color MapBg = Hex("#1A6E9E");
    private static readonly Color BadgeAccent = Hex("#2BB3A3");

    [MenuItem("Phisherman/Build World Map Scene")]
    public static void Build()
    {
        if (!Directory.Exists(ScenesDir)) Directory.CreateDirectory(ScenesDir);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Sprite circle = GetCircle();
        Sprite phisherman = FindSprite("phisherman");
        LogFound("phisherman", phisherman);

        // ── Camera ───────────────────────────────────────────────────
        var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>(); camGo.AddComponent<AudioListener>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Hex("#4FA8D9");
        cam.orthographic = true;
        cam.orthographicSize = OrthoSize;
        camGo.transform.position = new Vector3(0, 0, -10);

        // ── EventSystem ───────────────────────────────────────────────
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>(); es.AddComponent<StandaloneInputModule>();

        // ── Background ────────────────────────────────────────────────
        Sprite bgArt = FindSprite("world_stage_1_background");
        if (bgArt != null)
        {
            var bgGo = new GameObject("WorldBackground");
            var bgSR = bgGo.AddComponent<SpriteRenderer>();
            bgSR.sprite = bgArt; bgSR.color = Color.white; bgSR.sortingOrder = -50;
            float camH = OrthoSize * 2f, camW = camH * AspectW;
            var bnds = bgArt.bounds;
            bgGo.transform.localScale = new Vector3(camW / bnds.size.x, camH / bnds.size.y, 1f);
            bgGo.transform.position = Vector3.zero;
        }
        else Debug.LogWarning("[WorldMapBuilder] 'world_stage_1_background' not found.");

        // ── Player ────────────────────────────────────────────────────
        var playerGo = new GameObject("Phisherman");
        playerGo.transform.position = new Vector3(PlayerStartX, PlayerStartY, 0f);
        var playerSR = playerGo.AddComponent<SpriteRenderer>();
        playerSR.sprite = phisherman; playerSR.color = Color.white; playerSR.sortingOrder = 10;
        playerGo.transform.localScale = new Vector3(0.55f, 0.55f, 1f);

        // ── GameManager ───────────────────────────────────────────────
        var mgrGo = new GameObject("GameManager");
        var manager = mgrGo.AddComponent<WorldMapManager>();

        Transform playerT = playerGo.transform;

        // ── MapNavAgent (A* pathfinding) ──────────────────────────────
        var nav = playerGo.AddComponent<MapNavAgent>();
        nav.boundsX = BoundsX;
        nav.boundsY = BoundsY;
        nav.gridCols = 64;
        nav.gridRows = 36;
        nav.moveSpeed = 5f;

        // ── Blockers re-mapped to the neighbourhood image ─────────────
        //
        // Camera view: x ∈ [-8, +8],  y ∈ [-4.5, +4.5]
        // The image shows: three houses along the top half,
        // cobblestone paths in the lower half, flower beds bottom-centre,
        // pond/river top-right, fences between houses.
        //
        // Coordinate reference (eyeballed from game view screenshot):
        //   Left house  (red brick)  : x ≈ -5.5 to -2.5,  y ≈ 0.5 to 4.5
        //   Centre house(yellow)     : x ≈ -1.5 to  1.8,  y ≈  1.2 to 4.5
        //   Right house (teal)       : x ≈  2.8 to  6.0,  y ≈  0.2 to 4.0
        //   Pond / river (top-right) : x ≈  3.5 to  8.0,  y ≈  2.8 to 4.5
        //   Left fence  row          : x ≈ -2.8 to -1.2,  y ≈  0.0 to  1.5
        //   Right fence row          : x ≈  1.8 to  3.2,  y ≈  0.0 to  1.5
        //   Flower bed left          : x ≈ -2.2 to  0.0,  y ≈ -4.2 to -2.8
        //   Flower bed right         : x ≈  0.5 to  2.8,  y ≈ -4.2 to -2.8
        //   Top-left trees / hedge   : x ≈ -8.0 to -5.5,  y ≈  1.5 to  4.5
        //   Screen edges             : all four sides
        //
        // AddBlocker(name, player, cx, cy, halfW, halfH)

        // Screen edges
        AddBlocker("Edge_Top", playerT, 0.0f, 5.2f, 9.0f, 0.9f);
        AddBlocker("Edge_Bottom", playerT, 0.0f, -5.2f, 9.0f, 0.9f);
        AddBlocker("Edge_Left", playerT, -9.0f, 0.0f, 1.0f, 6.0f);
        AddBlocker("Edge_Right", playerT, 9.0f, 0.0f, 1.0f, 6.0f);

        // Left house (red brick) — body + roof
        AddBlocker("House_Left", playerT, -4.0f, 2.5f, 1.8f, 2.2f);
        AddBlocker("House_Left_Roof", playerT, -4.0f, 4.2f, 2.2f, 0.5f);

        // Centre house (yellow two-storey) — body + porch steps are walkable
        AddBlocker("House_Centre", playerT, 0.2f, 3.0f, 1.4f, 1.8f);
        AddBlocker("House_Centre_Roof", playerT, 0.2f, 4.4f, 1.8f, 0.4f);

        // Right house (teal bungalow)
        AddBlocker("House_Right", playerT, 4.3f, 2.0f, 1.8f, 2.0f);
        AddBlocker("House_Right_Roof", playerT, 4.3f, 3.8f, 2.0f, 0.5f);

        // Pond / river (top-right)
        AddBlocker("Pond", playerT, 5.8f, 3.8f, 2.5f, 0.8f);
        AddBlocker("Pond_Shore", playerT, 6.8f, 2.8f, 1.4f, 1.2f);

        // Left fence between left house and centre path
        AddBlocker("Fence_Left", playerT, -2.1f, 0.6f, 0.9f, 0.8f);

        // Right fence between centre house and right path
        AddBlocker("Fence_Right", playerT, 2.5f, 0.5f, 0.8f, 0.8f);

        // Trees / dense hedge top-left
        AddBlocker("Hedge_TopLeft", playerT, -6.8f, 3.0f, 1.4f, 2.0f);

        // Weeping willow tree (centre-left, y ≈ 1.5 to 3)
        AddBlocker("Tree_Willow", playerT, -2.8f, 2.2f, 0.9f, 1.2f);

        // Flower beds bottom-centre (player can walk around them)
        AddBlocker("FlowerBed_L", playerT, -1.2f, -3.4f, 1.2f, 0.8f);
        AddBlocker("FlowerBed_R", playerT, 1.6f, -3.4f, 1.3f, 0.8f);

        // Dense shrubs along right edge
        AddBlocker("Shrubs_Right", playerT, 7.0f, 0.5f, 1.2f, 2.0f);

        // ── Door triggers (house doors) ───────────────────────────────
        // Positions at the doorsteps of each house
        AddDoor("Door_LeftHouse", playerT, circle, -4.0f, 0.4f, "ApartmentInterior", "ENTER");
        AddDoor("Door_CentreHouse", playerT, circle, 0.2f, 1.2f, "PizzaInterior", "ENTER");
        AddDoor("Door_RightHouse", playerT, circle, 4.3f, 0.2f, "OfficeInterior", "ENTER");

        // ── Canvas + dialogue / map UI ────────────────────────────────
        var canvasGo = new GameObject("Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        var canvasRT = canvasGo.GetComponent<RectTransform>();

        // Path-exit badges (proximity world-space badges at path ends)
        AddPathTrigger("Path_World2", playerT, canvasRT, -6.5f, -3.8f,
            MapPathTrigger.TriggerType.LoadScene, "WorldMap2", "WORLD 2", Hex("#2BB3A3"));
        AddPathTrigger("Path_Locked", playerT, canvasRT, 6.5f, -3.8f,
            MapPathTrigger.TriggerType.LockedPopup, "", "LOCKED", Hex("#E74C3C"));

        var (dialogPanel, advBtn, nameText, bodyText, hintText,
             choicePanel, acceptBtn, acceptTxt, declineBtn, declineTxt)
            = BuildDialoguePanel(canvasRT);

        BuildMapHint(canvasRT);
        var mapPanel = BuildWorldMapPanel(canvasRT, manager);

        // ── Locked popup (used by right corner zone AND right path trigger)
        var lockedPopup = BuildLockedPopup(canvasRT);

        // Wire locked popup into the right path trigger
        var rightPathTrigger = GameObject.Find("Path_Locked");
        if (rightPathTrigger != null)
        {
            var pt = rightPathTrigger.GetComponent<MapPathTrigger>();
            if (pt != null) pt.lockedPopup = lockedPopup;
        }

        // ── Corner zones (200px screen-space) ─────────────────────────
        // Bottom-left → World 2
        var czLeft = new GameObject("CornerZone_World2");
        var czlComp = czLeft.AddComponent<MapCornerZone>();
        czlComp.player = playerT;
        czlComp.corner = MapCornerZone.Corner.BottomLeft;
        czlComp.action = MapCornerZone.ZoneAction.LoadScene;
        czlComp.targetScene = "WorldMap2";
        czlComp.zonePixels = 200;

        // Bottom-right → locked popup
        var czRight = new GameObject("CornerZone_Locked");
        var czrComp = czRight.AddComponent<MapCornerZone>();
        czrComp.player = playerT;
        czrComp.corner = MapCornerZone.Corner.BottomRight;
        czrComp.action = MapCornerZone.ZoneAction.LockedPopup;
        czrComp.lockedPopup = lockedPopup;
        czrComp.zonePixels = 200;
        czrComp.popupDuration = 2.8f;

        // ── Wire manager ──────────────────────────────────────────────
        manager.playerTransform = playerGo.transform;
        manager.playerRenderer = playerSR;
        manager.npcTransforms = new Transform[0];
        manager.exclamationMarks = new GameObject[0];
        manager.dialoguePanel = dialogPanel;
        manager.advanceButton = advBtn;
        manager.speakerNameText = nameText;
        manager.dialogueText = bodyText;
        manager.continueHint = hintText;
        manager.choicePanel = choicePanel;
        manager.acceptButton = acceptBtn;
        manager.acceptButtonText = acceptTxt;
        manager.declineButton = declineBtn;
        manager.declineButtonText = declineTxt;
        manager.worldMapPanel = mapPanel;
        manager.boundsX = BoundsX;
        manager.boundsY = BoundsY;

        UnityEventTools.AddPersistentListener(advBtn.onClick, manager.AdvanceDialogue);
        UnityEventTools.AddPersistentListener(acceptBtn.onClick, manager.OnAcceptHelp);
        UnityEventTools.AddPersistentListener(declineBtn.onClick, manager.OnDeclineHelp);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuild(ScenePath);
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log($"[WorldMapBuilder] Built → {ScenePath}");
    }

    // =========================================================================
    // Blocker helper
    // =========================================================================
    static void AddBlocker(string name, Transform player, float cx, float cy, float hw, float hh)
    {
        var go = new GameObject(name);
        go.transform.position = new Vector3(cx, cy, 0f);
        var b = go.AddComponent<MapBlocker>();
        b.player = player; b.halfW = hw; b.halfH = hh;
    }

    // =========================================================================
    // Door trigger helper
    // =========================================================================
    static void AddDoor(string name, Transform player, Sprite circle,
        float cx, float cy, string scene, string label)
    {
        var go = new GameObject(name);
        go.transform.position = new Vector3(cx, cy, 0f);
        var dt = go.AddComponent<MapDoorTrigger>();
        dt.player = player; dt.targetScene = scene;
        dt.triggerRadius = 0.55f; dt.promptProximity = 1.8f;

        // Badge prompt
        var promptGo = new GameObject("Prompt");
        promptGo.transform.SetParent(go.transform, false);
        promptGo.transform.localPosition = new Vector3(0f, 0.9f, -0.5f);

        var bgGo = new GameObject("Badge");
        bgGo.transform.SetParent(promptGo.transform, false);
        bgGo.transform.localPosition = Vector3.zero;
        bgGo.transform.localScale = new Vector3(0.012f, 0.012f, 1f);
        var bgCanvas = bgGo.AddComponent<Canvas>();
        bgCanvas.renderMode = RenderMode.WorldSpace; bgCanvas.sortingOrder = 30;
        bgGo.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 70);

        var borderGo = new GameObject("Border", typeof(RectTransform));
        borderGo.transform.SetParent(bgGo.transform, false);
        var bImg = borderGo.AddComponent<Image>(); bImg.color = BadgeAccent; bImg.raycastTarget = false;
        var brt = borderGo.GetComponent<RectTransform>();
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
        brt.offsetMin = new Vector2(-5, -5); brt.offsetMax = new Vector2(5, 5);

        var fillGo = new GameObject("Fill", typeof(RectTransform));
        fillGo.transform.SetParent(bgGo.transform, false);
        var fImg = fillGo.AddComponent<Image>(); fImg.color = new Color(0.08f, 0.10f, 0.18f, 0.96f); fImg.raycastTarget = false;
        var frt = fillGo.GetComponent<RectTransform>();
        frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = frt.offsetMax = Vector2.zero;

        var txtGo = new GameObject("Label", typeof(RectTransform));
        txtGo.transform.SetParent(bgGo.transform, false);
        var tmp = txtGo.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text = label; tmp.fontSize = 32; tmp.color = Color.white;
        tmp.fontStyle = TMPro.FontStyles.Bold; tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        var trt = txtGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(6, 0); trt.offsetMax = new Vector2(-6, 0);

        dt.prompt = promptGo;
        promptGo.SetActive(false);
    }

    // =========================================================================
    // Path-exit trigger helper (proximity badge + scene load or locked popup)
    // =========================================================================
    static void AddPathTrigger(string name, Transform player, RectTransform canvasRT,
        float cx, float cy, MapPathTrigger.TriggerType type,
        string targetScene, string badgeLabel, Color accentCol)
    {
        var go = new GameObject(name);
        go.transform.position = new Vector3(cx, cy, 0f);
        var pt = go.AddComponent<MapPathTrigger>();
        pt.player = player; pt.type = type; pt.targetScene = targetScene;
        pt.promptProximity = 2.0f; pt.triggerRadius = 1.0f;

        var promptGo = new GameObject("Prompt");
        promptGo.transform.SetParent(go.transform, false);
        promptGo.transform.localPosition = new Vector3(0f, 1.0f, -0.5f);

        var bgGo = new GameObject("Badge");
        bgGo.transform.SetParent(promptGo.transform, false);
        bgGo.transform.localPosition = Vector3.zero;
        bgGo.transform.localScale = new Vector3(0.012f, 0.012f, 1f);
        var bgCanvas = bgGo.AddComponent<Canvas>();
        bgCanvas.renderMode = RenderMode.WorldSpace; bgCanvas.sortingOrder = 30;
        bgGo.GetComponent<RectTransform>().sizeDelta = new Vector2(240, 75);

        void MakeImg(Transform p, Color c, Vector2 oMin, Vector2 oMax)
        {
            var g = new GameObject("I", typeof(RectTransform)); g.transform.SetParent(p, false);
            var img = g.AddComponent<Image>(); img.color = c; img.raycastTarget = false;
            var r = g.GetComponent<RectTransform>();
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = oMin; r.offsetMax = oMax;
        }
        MakeImg(bgGo.transform, accentCol, new Vector2(-6, -6), new Vector2(6, 6));
        MakeImg(bgGo.transform, new Color(0.08f, 0.10f, 0.18f, 0.96f), Vector2.zero, Vector2.zero);

        var tGo = new GameObject("Label", typeof(RectTransform)); tGo.transform.SetParent(bgGo.transform, false);
        var tmp2 = tGo.AddComponent<TMPro.TextMeshProUGUI>();
        tmp2.text = badgeLabel; tmp2.fontSize = 34; tmp2.color = Color.white;
        tmp2.fontStyle = TMPro.FontStyles.Bold; tmp2.alignment = TMPro.TextAlignmentOptions.Center;
        tmp2.raycastTarget = false;
        var tr2 = tGo.GetComponent<RectTransform>();
        tr2.anchorMin = Vector2.zero; tr2.anchorMax = Vector2.one;
        tr2.offsetMin = new Vector2(8, 0); tr2.offsetMax = new Vector2(-8, 0);

        pt.prompt = promptGo;
        promptGo.SetActive(false);
        // lockedPopup wired after canvas is built (see Build())
    }

    // =========================================================================
    // Locked popup
    // =========================================================================
    static GameObject BuildLockedPopup(RectTransform canvasRT)
    {
        var ov = UImg(canvasRT, "LockedPopup", new Color(0, 0, 0, 0));
        Stretch(ov.rectTransform); ov.raycastTarget = false;

        var card = UImg(ov.rectTransform, "Card", new Color(0.08f, 0.10f, 0.20f, 0.93f));
        var crt = card.rectTransform;
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.62f);
        crt.pivot = new Vector2(0.5f, 0.5f); crt.sizeDelta = new Vector2(740, 150);

        var border = UImg(crt, "Border", Hex("#E74C3C"));
        var brt = border.rectTransform;
        brt.anchorMin = new Vector2(0, 1); brt.anchorMax = new Vector2(1, 1);
        brt.pivot = new Vector2(0.5f, 1); brt.sizeDelta = new Vector2(0, 6);

        var txt = UTxt(crt, "Msg",
            "You haven't unlocked this area yet!\nExplore the neighbourhood first.",
            30, Color.white, TextAlignmentOptions.Center);
        txt.textWrappingMode = TextWrappingModes.Normal;
        Stretch(txt.rectTransform);

        ov.gameObject.SetActive(false);
        return ov.gameObject;
    }

    // =========================================================================
    // Dialogue panel
    // =========================================================================
    static (GameObject panel, Button adv, TMP_Text name, TMP_Text body, TMP_Text hint,
            GameObject choicePanel, Button acceptBtn, TMP_Text acceptTxt,
            Button declineBtn, TMP_Text declineTxt)
        BuildDialoguePanel(RectTransform canvasRT)
    {
        var advGo = new GameObject("AdvanceBtn", typeof(RectTransform));
        advGo.transform.SetParent(canvasRT, false);
        Stretch(advGo.GetComponent<RectTransform>());
        var advImg = advGo.AddComponent<Image>(); advImg.color = new Color(0, 0, 0, 0);
        var advBtn = advGo.AddComponent<Button>(); advBtn.targetGraphic = advImg;

        var panel = new GameObject("DialoguePanel", typeof(RectTransform));
        panel.transform.SetParent(canvasRT, false);
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = new Vector2(0, 0); prt.anchorMax = new Vector2(1, 0);
        prt.pivot = new Vector2(0.5f, 0);
        prt.sizeDelta = new Vector2(0, 220); prt.anchoredPosition = Vector2.zero;
        panel.AddComponent<Image>().color = new Color(0.08f, 0.15f, 0.28f, 0.95f);

        var nameBar = UImg(prt, "NameBar", Hex("#1A3A6B"));
        var nbrrt = nameBar.rectTransform;
        nbrrt.anchorMin = new Vector2(0, 1); nbrrt.anchorMax = new Vector2(0.42f, 1);
        nbrrt.pivot = new Vector2(0, 1); nbrrt.sizeDelta = new Vector2(0, 44);
        var nameText = UTxt(nbrrt, "Name", "Speaker", 30, NameCol,
            TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        var ntrt = nameText.rectTransform;
        ntrt.anchorMin = Vector2.zero; ntrt.anchorMax = Vector2.one;
        ntrt.offsetMin = new Vector2(18, 0); ntrt.offsetMax = Vector2.zero;

        var bodyText = UTxt(prt, "Body", "...", 26, Color.white, TextAlignmentOptions.TopLeft);
        bodyText.textWrappingMode = TextWrappingModes.Normal;
        var brrt = bodyText.rectTransform;
        brrt.anchorMin = Vector2.zero; brrt.anchorMax = Vector2.one;
        brrt.offsetMin = new Vector2(24, 48); brrt.offsetMax = new Vector2(-24, -50);

        var hint = UTxt(prt, "Hint", "Click to continue…", 20,
            new Color(0.7f, 0.7f, 0.9f), TextAlignmentOptions.MidlineRight);
        var hrrt = hint.rectTransform;
        hrrt.anchorMin = new Vector2(0, 0); hrrt.anchorMax = new Vector2(1, 0);
        hrrt.pivot = new Vector2(0.5f, 0);
        hrrt.sizeDelta = new Vector2(0, 36); hrrt.anchoredPosition = new Vector2(0, 8);

        var cpGo = new GameObject("ChoicePanel", typeof(RectTransform));
        cpGo.transform.SetParent(prt, false);
        var cprt = cpGo.GetComponent<RectTransform>();
        cprt.anchorMin = new Vector2(0.5f, 0); cprt.anchorMax = new Vector2(0.5f, 0);
        cprt.pivot = new Vector2(0.5f, 0);
        cprt.sizeDelta = new Vector2(800, 72); cprt.anchoredPosition = new Vector2(0, 8);

        var acceptGo = UBtn(cprt, "AcceptBtn", "Let's go!", 26, AcceptCol, Color.white);
        var declineGo = UBtn(cprt, "DeclineBtn", "Maybe later", 26, DeclineCol, Color.white);
        var art = acceptGo.GetComponent<RectTransform>();
        art.anchorMin = new Vector2(0, 0); art.anchorMax = new Vector2(0.48f, 1);
        art.offsetMin = art.offsetMax = Vector2.zero;
        var drt = declineGo.GetComponent<RectTransform>();
        drt.anchorMin = new Vector2(0.52f, 0); drt.anchorMax = new Vector2(1, 1);
        drt.offsetMin = drt.offsetMax = Vector2.zero;

        panel.SetActive(false);
        return (panel, advBtn, nameText, bodyText, hint, cpGo,
                acceptGo.GetComponent<Button>(),
                acceptGo.transform.Find("Label").GetComponent<TMP_Text>(),
                declineGo.GetComponent<Button>(),
                declineGo.transform.Find("Label").GetComponent<TMP_Text>());
    }

    // =========================================================================
    // Map hint + world map panel
    // =========================================================================
    static void BuildMapHint(RectTransform canvasRT)
    {
        var go = new GameObject("MapHint", typeof(RectTransform));
        go.transform.SetParent(canvasRT, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1, 1); rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(1, 1);
        rt.sizeDelta = new Vector2(230, 44); rt.anchoredPosition = new Vector2(-24, -24);
        go.AddComponent<Image>().color = new Color(0, 0, 0, 0.45f);
        var txt = UTxt(rt, "T", "[ M ]  World Map", 22, new Color(0.9f, 0.9f, 1f), TextAlignmentOptions.Center);
        Stretch(txt.rectTransform);
    }

    static GameObject BuildWorldMapPanel(RectTransform canvasRT, WorldMapManager manager)
    {
        var ov = UImg(canvasRT, "WorldMapOverlay", new Color(0, 0, 0, 0.88f));
        Stretch(ov.rectTransform); ov.raycastTarget = true;

        var card = UImg(ov.rectTransform, "MapCard", new Color(0, 0, 0, 0));
        var crt = card.rectTransform;
        crt.anchorMin = new Vector2(0.05f, 0.06f); crt.anchorMax = new Vector2(0.95f, 0.94f);
        crt.offsetMin = crt.offsetMax = Vector2.zero;

        Sprite mapSpr = FindSprite("map");
        if (mapSpr != null)
        {
            var mapImg = UImg(crt, "MapImage", Color.white);
            mapImg.sprite = mapSpr; mapImg.type = Image.Type.Simple;
            mapImg.preserveAspect = true; mapImg.raycastTarget = false;
            Stretch(mapImg.rectTransform);
        }
        else
        {
            card.color = MapBg;
            Debug.LogWarning("[WorldMapBuilder] 'map' sprite not found.");
        }

        var titleBg = UImg(ov.rectTransform, "TitleBar", new Color(0.05f, 0.08f, 0.15f, 0.92f));
        var tbr = titleBg.rectTransform;
        tbr.anchorMin = new Vector2(0, 1); tbr.anchorMax = new Vector2(1, 1);
        tbr.pivot = new Vector2(0.5f, 1); tbr.sizeDelta = new Vector2(0, 64); tbr.anchoredPosition = Vector2.zero;
        var titleTxt = UTxt(tbr, "Title", "PHISHERMAN  WORLD MAP", 36, Color.white,
            TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(titleTxt.rectTransform);

        var closeBtn = UBtn(ov.rectTransform, "CloseBtn", "X  Close Map  ( M )", 26,
            new Color(0.18f, 0.28f, 0.45f, 0.95f), Color.white);
        var cbrt = closeBtn.GetComponent<RectTransform>();
        cbrt.anchorMin = new Vector2(0.5f, 0); cbrt.anchorMax = new Vector2(0.5f, 0);
        cbrt.pivot = new Vector2(0.5f, 0); cbrt.sizeDelta = new Vector2(360, 56);
        cbrt.anchoredPosition = new Vector2(0, 12);
        UnityEventTools.AddPersistentListener(closeBtn.GetComponent<Button>().onClick, manager.CloseMap);

        ov.gameObject.SetActive(false);
        return ov.gameObject;
    }

    // =========================================================================
    // Utilities
    // =========================================================================
    static Sprite FindSprite(string name)
    {
        foreach (var g in AssetDatabase.FindAssets(name + " t:Sprite"))
        { var p = AssetDatabase.GUIDToAssetPath(g); if (Path.GetFileNameWithoutExtension(p).ToLower() == name.ToLower()) { var s = AssetDatabase.LoadAssetAtPath<Sprite>(p); if (s != null) return s; } }
        return null;
    }
    static void LogFound(string n, Sprite s) => Debug.Log($"[WorldMapBuilder] {n}: " + (s != null ? "✓" : "✗ not found"));
    static Sprite GetCircle() { try { return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"); } catch { return null; } }
    static Image UImg(Transform p, string n, Color c) { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); var img = go.AddComponent<Image>(); img.color = c; return img; }
    static TMP_Text UTxt(Transform p, string n, string text, int size, Color col, TextAlignmentOptions align, FontStyles style = FontStyles.Normal)
    { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); var t = go.AddComponent<TextMeshProUGUI>(); t.text = text; t.fontSize = size; t.color = col; t.alignment = align; t.fontStyle = style; t.raycastTarget = false; return t; }
    static GameObject UBtn(Transform p, string n, string label, int size, Color bg, Color tc)
    { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); var img = go.AddComponent<Image>(); img.color = bg; var btn = go.AddComponent<Button>(); btn.targetGraphic = img; var t = UTxt(go.transform, "Label", label, size, tc, TextAlignmentOptions.Center, FontStyles.Bold); Stretch(t.rectTransform); return go; }
    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
    static Color Hex(string h) => ColorUtility.TryParseHtmlString(h, out var c) ? c : Color.magenta;
    static void AddToBuild(string path) { var scenes = EditorBuildSettings.scenes.ToList(); if (!scenes.Any(s => s.path == path)) { scenes.Add(new EditorBuildSettingsScene(path, true)); EditorBuildSettings.scenes = scenes.ToArray(); } }
}