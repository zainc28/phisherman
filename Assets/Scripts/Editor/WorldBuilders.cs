using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// ============================================================
//  WorldMapBuilder  (Stage 1 — neighbourhood)
//  Phisherman > Build World Map Scene
// ============================================================
public static class WorldMapBuilder
{
    private const string ScenesDir = "Assets/Scenes";
    private const string ScenePath = "Assets/Scenes/WorldMap.unity";

    private const float OrthoSize = 4.5f;
    private const float AspectW = 16f / 9f;
    private const float SharedSpeed = 3.5f;
    private const float PlayerScale = 0.44f;

    static readonly Color AcceptCol = Hex("#2ECC71");
    static readonly Color DeclineCol = Hex("#E74C3C");
    static readonly Color NameCol = Hex("#FFD93D");
    static readonly Color MapBg = Hex("#1A6E9E");

    [MenuItem("Phisherman/Build World Map Scene")]
    public static void Build()
    {
        if (!Directory.Exists(ScenesDir)) Directory.CreateDirectory(ScenesDir);

        // Preserve WalkableZone across rebuilds
        Vector2[] savedVerts = null;
        Vector3 savedZonePos = Vector3.zero;
        var existing = GameObject.Find("WalkableZone");
        if (existing != null)
        {
            var pc = existing.GetComponent<PolygonCollider2D>();
            if (pc != null) { savedVerts = pc.points.ToArray(); savedZonePos = existing.transform.position; Debug.Log($"[WorldMapBuilder] Saved WalkableZone: {savedVerts.Length} verts."); }
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Restore WalkableZone
        if (savedVerts != null)
        {
            var zGo = new GameObject("WalkableZone"); zGo.transform.position = savedZonePos;
            var pc2 = zGo.AddComponent<PolygonCollider2D>(); pc2.isTrigger = true; pc2.SetPath(0, savedVerts);
            Debug.Log($"[WorldMapBuilder] Restored WalkableZone: {savedVerts.Length} verts.");
        }
        else
        {
            var zGo = new GameObject("WalkableZone");
            var pc2 = zGo.AddComponent<PolygonCollider2D>(); pc2.isTrigger = true;
            pc2.SetPath(0, new Vector2[] { new Vector2(-7.5f, -4f), new Vector2(-7.5f, 0f), new Vector2(7.5f, 0f), new Vector2(7.5f, -4f) });
            Debug.LogWarning("[WorldMapBuilder] No WalkableZone — placeholder created. Draw your walkable paths then save the scene.");
        }

        Sprite circle = GetCircle();
        Sprite phisherman = FindSprite("phisherman");
        Sprite phishermanWalk = FindSprite("phisherman_walking");
        LogFound("phisherman", phisherman);
        LogFound("phisherman_walking", phishermanWalk);

        // Camera
        var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>(); camGo.AddComponent<AudioListener>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Hex("#4FA8D9");
        cam.orthographic = true; cam.orthographicSize = OrthoSize;
        camGo.transform.position = new Vector3(0, 0, -10);

        // EventSystem
        var es = new GameObject("EventSystem"); es.AddComponent<EventSystem>(); es.AddComponent<StandaloneInputModule>();

        // Background
        Sprite bgArt = FindSprite("world_stage_1_background");
        if (bgArt != null)
        {
            var bgGo = new GameObject("WorldBackground"); var bgSR = bgGo.AddComponent<SpriteRenderer>();
            bgSR.sprite = bgArt; bgSR.color = Color.white; bgSR.sortingOrder = -50;
            float camH = OrthoSize * 2f, camW = camH * AspectW;
            bgGo.transform.localScale = new Vector3(camW / bgArt.bounds.size.x, camH / bgArt.bounds.size.y, 1f);
        }
        else Debug.LogWarning("[WorldMapBuilder] 'world_stage_1_background' not found.");

        // Player spawn
        var spawnGo = new GameObject("PlayerSpawn"); spawnGo.transform.position = new Vector3(0f, -1.5f, 0f);

        // Player
        var playerGo = new GameObject("Phisherman"); playerGo.tag = "Player";
        playerGo.transform.position = new Vector3(0f, -1.5f, 0f);
        playerGo.transform.localScale = new Vector3(PlayerScale, PlayerScale, 1f);
        var playerSR = playerGo.AddComponent<SpriteRenderer>(); playerSR.sprite = phisherman; playerSR.color = Color.white; playerSR.sortingOrder = 10;

        var anim = playerGo.AddComponent<MapWalkAnimator>(); anim.idleSprite = phisherman; anim.walkSprite = phishermanWalk ?? phisherman; anim.fps = 2f;
        playerGo.AddComponent<MapWasdZoneClamp>();
        var nav = playerGo.AddComponent<MapNavAgent>(); nav.gridCols = 80; nav.gridRows = 45; nav.moveSpeed = SharedSpeed;

        // GameManager
        var mgrGo = new GameObject("GameManager"); var manager = mgrGo.AddComponent<WorldMapManager>(); manager.playerSpeed = SharedSpeed;
        mgrGo.AddComponent<LevelSystemHUD>();

        Transform playerT = playerGo.transform;

        // Door CTAs — pass interior scene AND minigame scene separately so
        // completion key matches what InteriorDialogueManager writes
        AddDoorCTA("Door_LeftHouse", playerT, -4.0f, 0.4f, "ApartmentInterior", "EmailSwiper", "Grandma Rose", Hex("#FF9F1C"));
        AddDoorCTA("Door_CentreHouse", playerT, 0.2f, 1.2f, "PizzaInterior", "EmailSwiper", "Uncle Tony", Hex("#FF6B6B"));
        AddDoorCTA("Door_RightHouse", playerT, 4.3f, 0.2f, "OfficeInterior", "TowerDefense", "Mrs. Patel", Hex("#4ECDC4"));

        // Canvas
        var canvasGo = new GameObject("Canvas"); var canvas = canvasGo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>(); var canvasRT = canvasGo.GetComponent<RectTransform>();

        // World 2 path floating text (smaller)
        AddPathFloatingText("Path_World2", playerT, -6.5f, -3.8f, MapPathTrigger.TriggerType.LoadScene, "WorldMap2", "World 2  ›", Hex("#2BB3A3"), 0.18f);
        AddPathFloatingText("Path_Locked", playerT, 6.5f, -3.8f, MapPathTrigger.TriggerType.LockedPopup, "", "Locked  ✕", Hex("#E74C3C"), 0.18f);

        var (dialogPanel, advBtn, nameText, bodyText, hintText, choicePanel, acceptBtn, acceptTxt, declineBtn, declineTxt) = BuildDialoguePanel(canvasRT);
        BuildMapHint(canvasRT);
        var mapPanel = BuildWorldMapPanel(canvasRT, manager);
        var lockedPopup = BuildLockedPopup(canvasRT);

        // Wire locked popup to right path trigger
        var rpt = GameObject.Find("Path_Locked")?.GetComponent<MapPathTrigger>();
        if (rpt != null) rpt.lockedPopup = lockedPopup;

        // World 2 only opens once World 1's minigames are complete —
        // reuse the same locked popup so it reads consistently either way.
        var p2t = GameObject.Find("Path_World2")?.GetComponent<MapPathTrigger>();
        if (p2t != null) { p2t.lockedPopup = lockedPopup; p2t.requiresWorldComplete = 1; }

        // Corner zones
        AddCornerZone("CornerZone_World2", playerT, MapCornerZone.Corner.BottomLeft, MapCornerZone.ZoneAction.LoadScene, "WorldMap2", lockedPopup, 200);
        AddCornerZone("CornerZone_Locked", playerT, MapCornerZone.Corner.BottomRight, MapCornerZone.ZoneAction.LockedPopup, "", lockedPopup, 200);

        var cz2 = GameObject.Find("CornerZone_World2")?.GetComponent<MapCornerZone>();
        if (cz2 != null) cz2.requiresWorldComplete = 1;

        // Wire each door's "already done" lock popup now that it exists
        foreach (var doorName in new[] { "Door_LeftHouse", "Door_CentreHouse", "Door_RightHouse" })
        {
            var doorCta = GameObject.Find(doorName + "_CTA")?.GetComponent<MapDoorCTA>();
            if (doorCta != null) doorCta.completedLockPopup = lockedPopup;
        }

        // Wire manager
        manager.playerTransform = playerGo.transform; manager.playerRenderer = playerSR;
        manager.npcTransforms = new Transform[0]; manager.exclamationMarks = new GameObject[0];
        manager.dialoguePanel = dialogPanel; manager.advanceButton = advBtn;
        manager.speakerNameText = nameText; manager.dialogueText = bodyText;
        manager.continueHint = hintText; manager.choicePanel = choicePanel;
        manager.acceptButton = acceptBtn; manager.acceptButtonText = acceptTxt;
        manager.declineButton = declineBtn; manager.declineButtonText = declineTxt;
        manager.worldMapPanel = mapPanel;
        manager.boundsX = new Vector2(-7.8f, 7.8f); manager.boundsY = new Vector2(-4.0f, 4.3f);

        UnityEventTools.AddPersistentListener(advBtn.onClick, manager.AdvanceDialogue);
        UnityEventTools.AddPersistentListener(acceptBtn.onClick, manager.OnAcceptHelp);
        UnityEventTools.AddPersistentListener(declineBtn.onClick, manager.OnDeclineHelp);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuild(ScenePath);
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log($"[WorldMapBuilder] Built → {ScenePath}");
    }

    // ── Plain floating text CTA with border ───────────────────
    static void AddDoorCTA(string name, Transform player, float cx, float cy,
        string interiorScene, string minigameScene, string label, Color col, GameObject lockedPopup = null)
    {
        // Invisible door trigger (walk-on still works)
        var doorGo = new GameObject(name); doorGo.transform.position = new Vector3(cx, cy, 0f);
        var dt = doorGo.AddComponent<MapDoorTrigger>(); dt.player = player;
        dt.targetScene = interiorScene; dt.triggerRadius = 0.55f; dt.promptProximity = 999f; dt.prompt = null;

        // Floating label root
        var ctaRoot = new GameObject(name + "_CTA"); ctaRoot.transform.position = new Vector3(cx, cy + 0.75f, -0.5f);

        // World-space canvas
        var cGo = new GameObject("C"); cGo.transform.SetParent(ctaRoot.transform, false);
        cGo.transform.localScale = new Vector3(0.012f, 0.012f, 1f);
        var wc = cGo.AddComponent<Canvas>(); wc.renderMode = RenderMode.WorldSpace; wc.sortingOrder = 35;
        cGo.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 70);

        // Border (slightly larger, sits behind)
        var borderGo = new GameObject("Border", typeof(RectTransform)); borderGo.transform.SetParent(cGo.transform, false);
        var borderImg = borderGo.AddComponent<Image>(); borderImg.color = col; borderImg.raycastTarget = false;
        var brt = borderGo.GetComponent<RectTransform>();
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = new Vector2(-4, -4); brt.offsetMax = new Vector2(4, 4);

        // Dark fill behind text
        var fillGo = new GameObject("Fill", typeof(RectTransform)); fillGo.transform.SetParent(cGo.transform, false);
        var fillImg = fillGo.AddComponent<Image>(); fillImg.color = new Color(0.06f, 0.08f, 0.14f, 0.92f); fillImg.raycastTarget = false;
        var frt = fillGo.GetComponent<RectTransform>(); frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = frt.offsetMax = Vector2.zero;

        // Label text
        var tGo = new GameObject("Label", typeof(RectTransform)); tGo.transform.SetParent(cGo.transform, false);
        var tmp = tGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label; tmp.fontSize = 38; tmp.color = col; tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center; tmp.raycastTarget = false;
        var tRT = tGo.GetComponent<RectTransform>(); tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one; tRT.offsetMin = new Vector2(8, 4); tRT.offsetMax = new Vector2(-8, -4);

        // MapDoorCTA — completion key uses the INTERIOR scene name, not the
        // minigame scene name. Multiple doors can share the same minigame
        // (EmailSwiper, TowerDefense, SpotDifference) — keying on the
        // minigame would gray out every door using it once ANY of them
        // was beaten. Must match InteriorDialogueManager.MarkComplete().
        var cta = ctaRoot.AddComponent<MapDoorCTA>();
        cta.targetScene = interiorScene;
        cta.playerTransform = player;
        cta.doorPosition = doorGo.transform;
        cta.label = tmp;
        cta.completionKey = "completed_" + interiorScene;
        cta.activeColor = col;
        cta.completedColor = new Color(col.r * 0.35f, col.g * 0.35f, col.b * 0.35f, 0.40f);
        cta.bobAmount = 0.16f;
        cta.bobSpeed = 2.8f;
        cta.clickRadius = 1.4f;
        cta.completedLockPopup = lockedPopup;
        cta.completedLockMessage = "This door is locked — please explore another building!";

        // Also gray the border when complete
        var borderCTA = ctaRoot.AddComponent<CTABorderFader>();
        borderCTA.borderImage = borderImg;
        borderCTA.completionKey = "completed_" + interiorScene;
        borderCTA.activeColor = col;
        borderCTA.completedColor = new Color(col.r * 0.35f, col.g * 0.35f, col.b * 0.35f, 0.40f);
    }

    // ── Small floating text for path exits ───────────────────────────
    static void AddPathFloatingText(string name, Transform player, float cx, float cy,
        MapPathTrigger.TriggerType type, string targetScene, string label, Color col, float scale)
    {
        var go = new GameObject(name); go.transform.position = new Vector3(cx, cy, 0f);
        var pt = go.AddComponent<MapPathTrigger>(); pt.player = player; pt.type = type;
        pt.targetScene = targetScene; pt.promptProximity = 2.2f; pt.triggerRadius = 1.0f;

        var promptGo = new GameObject("Prompt"); promptGo.transform.SetParent(go.transform, false);
        promptGo.transform.localPosition = new Vector3(0f, 0.6f, -0.5f);

        var cGo = new GameObject("C"); cGo.transform.SetParent(promptGo.transform, false);
        cGo.transform.localScale = new Vector3(0.012f, 0.012f, 1f);
        var wc = cGo.AddComponent<Canvas>(); wc.renderMode = RenderMode.WorldSpace; wc.sortingOrder = 30;
        cGo.GetComponent<RectTransform>().sizeDelta = new Vector2(220, 50);

        var tGo = new GameObject("Label", typeof(RectTransform)); tGo.transform.SetParent(cGo.transform, false);
        var tmp = tGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label; tmp.fontSize = 28; tmp.color = col;
        tmp.fontStyle = FontStyles.Bold; tmp.alignment = TextAlignmentOptions.Center; tmp.raycastTarget = false;
        var tRT = tGo.GetComponent<RectTransform>(); tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one; tRT.offsetMin = tRT.offsetMax = Vector2.zero;

        pt.prompt = promptGo; promptGo.SetActive(false);
    }

    static void AddCornerZone(string name, Transform player,
        MapCornerZone.Corner corner, MapCornerZone.ZoneAction action,
        string targetScene, GameObject lockedPopup, int px)
    {
        var go = new GameObject(name); var c = go.AddComponent<MapCornerZone>();
        c.player = player; c.corner = corner; c.action = action;
        c.targetScene = targetScene; c.lockedPopup = lockedPopup; c.zonePixels = px; c.popupDuration = 2.8f;
    }

    // ── Shared UI builders ────────────────────────────────────────────
    static (GameObject panel, Button adv, TMP_Text name, TMP_Text body, TMP_Text hint,
            GameObject choicePanel, Button acceptBtn, TMP_Text acceptTxt, Button declineBtn, TMP_Text declineTxt)
        BuildDialoguePanel(RectTransform canvasRT)
    {
        var advGo = new GameObject("AdvanceBtn", typeof(RectTransform)); advGo.transform.SetParent(canvasRT, false); Stretch(advGo.GetComponent<RectTransform>());
        var advImg = advGo.AddComponent<Image>(); advImg.color = new Color(0, 0, 0, 0);
        var advBtn = advGo.AddComponent<Button>(); advBtn.targetGraphic = advImg;

        var panel = new GameObject("DialoguePanel", typeof(RectTransform)); panel.transform.SetParent(canvasRT, false);
        var prt = panel.GetComponent<RectTransform>(); prt.anchorMin = new Vector2(0, 0); prt.anchorMax = new Vector2(1, 0); prt.pivot = new Vector2(0.5f, 0); prt.sizeDelta = new Vector2(0, 220); prt.anchoredPosition = Vector2.zero;
        panel.AddComponent<Image>().color = new Color(0.08f, 0.15f, 0.28f, 0.95f);

        var nameBar = UImg(prt, "NameBar", Hex("#1A3A6B")); var nbrrt = nameBar.rectTransform;
        nbrrt.anchorMin = new Vector2(0, 1); nbrrt.anchorMax = new Vector2(0.42f, 1); nbrrt.pivot = new Vector2(0, 1); nbrrt.sizeDelta = new Vector2(0, 44);
        var nameText = UTxt(nbrrt, "Name", "Speaker", 30, NameCol, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        var ntrt = nameText.rectTransform; ntrt.anchorMin = Vector2.zero; ntrt.anchorMax = Vector2.one; ntrt.offsetMin = new Vector2(18, 0); ntrt.offsetMax = Vector2.zero;

        var bodyText = UTxt(prt, "Body", "...", 26, Color.white, TextAlignmentOptions.TopLeft); bodyText.textWrappingMode = TextWrappingModes.Normal;
        var brrt = bodyText.rectTransform; brrt.anchorMin = Vector2.zero; brrt.anchorMax = Vector2.one; brrt.offsetMin = new Vector2(24, 48); brrt.offsetMax = new Vector2(-24, -50);

        var hint = UTxt(prt, "Hint", "Click to continue…", 20, new Color(0.7f, 0.7f, 0.9f), TextAlignmentOptions.MidlineRight);
        var hrrt = hint.rectTransform; hrrt.anchorMin = new Vector2(0, 0); hrrt.anchorMax = new Vector2(1, 0); hrrt.pivot = new Vector2(0.5f, 0); hrrt.sizeDelta = new Vector2(0, 36); hrrt.anchoredPosition = new Vector2(0, 8);

        var cpGo = new GameObject("ChoicePanel", typeof(RectTransform)); cpGo.transform.SetParent(prt, false);
        var cprt = cpGo.GetComponent<RectTransform>(); cprt.anchorMin = new Vector2(0.5f, 0); cprt.anchorMax = new Vector2(0.5f, 0); cprt.pivot = new Vector2(0.5f, 0); cprt.sizeDelta = new Vector2(800, 72); cprt.anchoredPosition = new Vector2(0, 8);

        var acceptGo = UBtn(cprt, "AcceptBtn", "Let's go!", 26, AcceptCol, Color.white); var declineGo = UBtn(cprt, "DeclineBtn", "Maybe later", 26, DeclineCol, Color.white);
        var art = acceptGo.GetComponent<RectTransform>(); art.anchorMin = new Vector2(0, 0); art.anchorMax = new Vector2(0.48f, 1); art.offsetMin = art.offsetMax = Vector2.zero;
        var drt = declineGo.GetComponent<RectTransform>(); drt.anchorMin = new Vector2(0.52f, 0); drt.anchorMax = new Vector2(1, 1); drt.offsetMin = drt.offsetMax = Vector2.zero;

        panel.SetActive(false);
        return (panel, advBtn, nameText, bodyText, hint, cpGo, acceptGo.GetComponent<Button>(), acceptGo.transform.Find("Label").GetComponent<TMP_Text>(), declineGo.GetComponent<Button>(), declineGo.transform.Find("Label").GetComponent<TMP_Text>());
    }

    static void BuildMapHint(RectTransform canvasRT)
    {
        var go = new GameObject("MapHint", typeof(RectTransform)); go.transform.SetParent(canvasRT, false);
        var rt = go.GetComponent<RectTransform>(); rt.anchorMin = new Vector2(1, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(1, 1); rt.sizeDelta = new Vector2(230, 44); rt.anchoredPosition = new Vector2(-24, -24);
        go.AddComponent<Image>().color = new Color(0, 0, 0, 0.45f);
        Stretch(UTxt(rt, "T", "[ M ]  World Map", 22, new Color(0.9f, 0.9f, 1f), TextAlignmentOptions.Center).rectTransform);
    }

    static GameObject BuildWorldMapPanel(RectTransform canvasRT, WorldMapManager manager)
    {
        var ov = UImg(canvasRT, "WorldMapOverlay", new Color(0, 0, 0, 0.88f)); Stretch(ov.rectTransform); ov.raycastTarget = true;
        var card = UImg(ov.rectTransform, "MapCard", new Color(0, 0, 0, 0)); var crt = card.rectTransform;
        crt.anchorMin = new Vector2(0.05f, 0.06f); crt.anchorMax = new Vector2(0.95f, 0.94f); crt.offsetMin = crt.offsetMax = Vector2.zero;
        var mi = UImg(crt, "MapImage", Color.white); mi.type = Image.Type.Simple; mi.preserveAspect = true; mi.raycastTarget = false; Stretch(mi.rectTransform);
        Sprite[] mapVersions = new Sprite[5];
        for (int v = 1; v <= 5; v++) mapVersions[v - 1] = FindSprite("map_v" + v);
        bool anyMapFound = false; foreach (var s in mapVersions) if (s != null) anyMapFound = true;
        if (anyMapFound)
        {
            var switcher = mi.gameObject.AddComponent<WorldMapImageSwitcher>();
            switcher.targetImage = mi; switcher.mapVersions = mapVersions; switcher.Refresh();
        }
        else { card.color = MapBg; Debug.LogWarning("[WorldMapBuilder] 'map_v1'..'map_v5' sprites not found."); }
        var titleBg = UImg(ov.rectTransform, "TitleBar", new Color(0.05f, 0.08f, 0.15f, 0.92f)); var tbr = titleBg.rectTransform;
        tbr.anchorMin = new Vector2(0, 1); tbr.anchorMax = new Vector2(1, 1); tbr.pivot = new Vector2(0.5f, 1); tbr.sizeDelta = new Vector2(0, 64); tbr.anchoredPosition = Vector2.zero;
        Stretch(UTxt(tbr, "Title", "PHISHERMAN  WORLD MAP", 36, Color.white, TextAlignmentOptions.Center, FontStyles.Bold).rectTransform);
        var closeBtn = UBtn(ov.rectTransform, "CloseBtn", "X  Close Map  ( M )", 26, new Color(0.18f, 0.28f, 0.45f, 0.95f), Color.white);
        var cbrt = closeBtn.GetComponent<RectTransform>(); cbrt.anchorMin = new Vector2(0.5f, 0); cbrt.anchorMax = new Vector2(0.5f, 0); cbrt.pivot = new Vector2(0.5f, 0); cbrt.sizeDelta = new Vector2(360, 56); cbrt.anchoredPosition = new Vector2(0, 12);
        UnityEventTools.AddPersistentListener(closeBtn.GetComponent<Button>().onClick, manager.CloseMap);
        ov.gameObject.SetActive(false); return ov.gameObject;
    }

    static GameObject BuildLockedPopup(RectTransform canvasRT)
    {
        var ov = UImg(canvasRT, "LockedPopup", new Color(0, 0, 0, 0)); Stretch(ov.rectTransform); ov.raycastTarget = false;
        var card = UImg(ov.rectTransform, "Card", new Color(0.08f, 0.10f, 0.20f, 0.93f)); var crt = card.rectTransform;
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.62f); crt.pivot = new Vector2(0.5f, 0.5f); crt.sizeDelta = new Vector2(740, 150);
        var brd = UImg(crt, "Border", Hex("#E74C3C")); var brt = brd.rectTransform;
        brt.anchorMin = new Vector2(0, 1); brt.anchorMax = new Vector2(1, 1); brt.pivot = new Vector2(0.5f, 1); brt.sizeDelta = new Vector2(0, 6);
        var txt = UTxt(crt, "Msg", "You haven't unlocked this area yet!\nExplore the neighbourhood first.", 30, Color.white, TextAlignmentOptions.Center);
        txt.textWrappingMode = TextWrappingModes.Normal; Stretch(txt.rectTransform);
        var controller = ov.gameObject.AddComponent<LockedPopupController>();
        controller.messageText = txt;
        ov.gameObject.SetActive(false); return ov.gameObject;
    }

    // ── Utilities ─────────────────────────────────────────────────────
    static Sprite FindSprite(string name) { foreach (var g in AssetDatabase.FindAssets(name + " t:Texture2D")) { var p = AssetDatabase.GUIDToAssetPath(g); if (System.IO.Path.GetFileNameWithoutExtension(p).ToLower() == name.ToLower()) { var s = AssetDatabase.LoadAssetAtPath<Sprite>(p); if (s != null) return s; var reps = AssetDatabase.LoadAllAssetRepresentationsAtPath(p); foreach (var r in reps) { if (r is Sprite sp) return sp; } } } return null; }
    static void LogFound(string n, Sprite s) => Debug.Log($"[WorldMapBuilder] {n}: " + (s != null ? "✓" : "✗"));
    static Sprite GetCircle() { try { return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"); } catch { return null; } }
    static Image UImg(Transform p, string n, Color c) { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); var img = go.AddComponent<Image>(); img.color = c; return img; }
    static TMP_Text UTxt(Transform p, string n, string text, int size, Color col, TextAlignmentOptions align, FontStyles style = FontStyles.Normal) { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); var t = go.AddComponent<TextMeshProUGUI>(); t.text = text; t.fontSize = size; t.color = col; t.alignment = align; t.fontStyle = style; t.raycastTarget = false; return t; }
    static GameObject UBtn(Transform p, string n, string label, int size, Color bg, Color tc) { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); var img = go.AddComponent<Image>(); img.color = bg; var btn = go.AddComponent<Button>(); btn.targetGraphic = img; var t = UTxt(go.transform, "Label", label, size, tc, TextAlignmentOptions.Center, FontStyles.Bold); Stretch(t.rectTransform); return go; }
    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
    static Color Hex(string h) => ColorUtility.TryParseHtmlString(h, out var c) ? c : Color.magenta;
    static void AddToBuild(string path) { var scenes = EditorBuildSettings.scenes.ToList(); if (!scenes.Any(s => s.path == path)) { scenes.Add(new EditorBuildSettingsScene(path, true)); EditorBuildSettings.scenes = scenes.ToArray(); } }
}

// ============================================================
//  World2Builder  (Stage 2)
//  Phisherman > Build World 2 Scene
// ============================================================
public static class World2Builder
{
    private const string ScenesDir = "Assets/Scenes";
    private const string ScenePath = "Assets/Scenes/WorldMap2.unity";
    private const float OrthoSize = 4.5f;
    private const float AspectW = 16f / 9f;
    private const float SharedSpeed = 3.5f;
    private const float PlayerScale = 0.44f;

    static readonly Color AcceptCol = Hex("#2ECC71");
    static readonly Color DeclineCol = Hex("#E74C3C");
    static readonly Color NameCol = Hex("#FFD93D");
    static readonly Color MapBg = Hex("#1A6E9E");

    [MenuItem("Phisherman/Build World 2 Scene")]
    public static void Build()
    {
        if (!Directory.Exists(ScenesDir)) Directory.CreateDirectory(ScenesDir);

        if (File.Exists(ScenePath))
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (GameObject.Find("Canvas") != null)
            {
                Debug.Log("[World2Builder] WorldMap2.unity already exists and looks built — skipping to preserve your manual edits (door positions, extra paths, etc). Delete the scene file first if you want a full rebuild.");
                return;
            }
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Sprite phisherman = FindSprite("phisherman");
        Sprite phishermanWalk = FindSprite("phisherman_walking");

        // Camera
        var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>(); camGo.AddComponent<AudioListener>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Hex("#6BB8D4");
        cam.orthographic = true; cam.orthographicSize = OrthoSize;
        camGo.transform.position = new Vector3(0, 0, -10);

        var es = new GameObject("EventSystem"); es.AddComponent<EventSystem>(); es.AddComponent<StandaloneInputModule>();

        // Background
        Sprite bgArt = FindSprite("world_stage_2_background");
        if (bgArt != null)
        {
            var bgGo = new GameObject("WorldBackground"); var bgSR = bgGo.AddComponent<SpriteRenderer>();
            bgSR.sprite = bgArt; bgSR.color = Color.white; bgSR.sortingOrder = -50;
            float camH = OrthoSize * 2f, camW = camH * AspectW;
            bgGo.transform.localScale = new Vector3(camW / bgArt.bounds.size.x, camH / bgArt.bounds.size.y, 1f);
        }
        else Debug.LogWarning("[World2Builder] 'world_stage_2_background' not found.");

        // Player
        var playerGo = new GameObject("Phisherman"); playerGo.tag = "Player";
        playerGo.transform.position = new Vector3(0f, -1.5f, 0f);
        playerGo.transform.localScale = new Vector3(PlayerScale, PlayerScale, 1f);
        var playerSR = playerGo.AddComponent<SpriteRenderer>(); playerSR.sprite = phisherman; playerSR.color = Color.white; playerSR.sortingOrder = 10;

        var anim = playerGo.AddComponent<MapWalkAnimator>(); anim.idleSprite = phisherman; anim.walkSprite = phishermanWalk ?? phisherman; anim.fps = 2f;
        playerGo.AddComponent<MapWasdZoneClamp>();
        var nav = playerGo.AddComponent<MapNavAgent>(); nav.gridCols = 80; nav.gridRows = 45; nav.moveSpeed = SharedSpeed;

        var mgrGo = new GameObject("GameManager"); var manager = mgrGo.AddComponent<WorldMapManager>(); manager.playerSpeed = SharedSpeed;
        mgrGo.AddComponent<LevelSystemHUD>();

        Transform playerT = playerGo.transform;

        // Canvas
        var canvasGo = new GameObject("Canvas"); var canvas = canvasGo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>(); var canvasRT = canvasGo.GetComponent<RectTransform>();

        // Path floating texts
        AddPathFloatingText("Path_World1", playerT, -5.0f, -3.6f, MapPathTrigger.TriggerType.LoadScene, "WorldMap", "‹ World 1", Hex("#2BB3A3"));
        AddPathFloatingText("Path_Locked", playerT, 5.0f, -3.6f, MapPathTrigger.TriggerType.LockedPopup, "", "Locked  ✕", Hex("#E74C3C"));

        var (dialogPanel, advBtn, nameText, bodyText, hintText, choicePanel, acceptBtn, acceptTxt, declineBtn, declineTxt) = BuildDialoguePanel(canvasRT);
        BuildMapHint(canvasRT);
        var mapPanel = BuildWorldMapPanel(canvasRT, manager);
        var lockedPopup = BuildLockedPopup(canvasRT);

        var rpt = GameObject.Find("Path_Locked")?.GetComponent<MapPathTrigger>();
        if (rpt != null) rpt.lockedPopup = lockedPopup;

        // Back-to-World1 hint
        var hintGo = new GameObject("BackHint", typeof(RectTransform)); hintGo.transform.SetParent(canvasRT, false);
        var hrt = hintGo.GetComponent<RectTransform>(); hrt.anchorMin = new Vector2(0, 1); hrt.anchorMax = new Vector2(0, 1); hrt.pivot = new Vector2(0, 1); hrt.sizeDelta = new Vector2(300, 44); hrt.anchoredPosition = new Vector2(24, -24);
        hintGo.AddComponent<Image>().color = new Color(0, 0, 0, 0.45f);
        Stretch(UTxt(hintGo.transform, "T", "‹ Walk left for World 1", 20, new Color(0.9f, 0.9f, 1f), TextAlignmentOptions.Center).rectTransform);

        manager.playerTransform = playerGo.transform; manager.playerRenderer = playerSR;
        manager.npcTransforms = new Transform[0]; manager.exclamationMarks = new GameObject[0];
        manager.dialoguePanel = dialogPanel; manager.advanceButton = advBtn;
        manager.speakerNameText = nameText; manager.dialogueText = bodyText; manager.continueHint = hintText;
        manager.choicePanel = choicePanel; manager.acceptButton = acceptBtn; manager.acceptButtonText = acceptTxt;
        manager.declineButton = declineBtn; manager.declineButtonText = declineTxt;
        manager.worldMapPanel = mapPanel; manager.boundsX = new Vector2(-7.8f, 7.8f); manager.boundsY = new Vector2(-4.0f, 4.3f);

        UnityEventTools.AddPersistentListener(advBtn.onClick, manager.AdvanceDialogue);
        UnityEventTools.AddPersistentListener(acceptBtn.onClick, manager.OnAcceptHelp);
        UnityEventTools.AddPersistentListener(declineBtn.onClick, manager.OnDeclineHelp);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuild(ScenePath);
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log($"[World2Builder] Built → {ScenePath}");
    }

    static void AddPathFloatingText(string name, Transform player, float cx, float cy,
        MapPathTrigger.TriggerType type, string targetScene, string label, Color col)
    {
        var go = new GameObject(name); go.transform.position = new Vector3(cx, cy, 0f);
        var pt = go.AddComponent<MapPathTrigger>(); pt.player = player; pt.type = type;
        pt.targetScene = targetScene; pt.promptProximity = 2.2f; pt.triggerRadius = 1.0f;

        var promptGo = new GameObject("Prompt"); promptGo.transform.SetParent(go.transform, false);
        promptGo.transform.localPosition = new Vector3(0f, 0.6f, -0.5f);

        var cGo = new GameObject("C"); cGo.transform.SetParent(promptGo.transform, false);
        cGo.transform.localScale = new Vector3(0.012f, 0.012f, 1f);
        var wc = cGo.AddComponent<Canvas>(); wc.renderMode = RenderMode.WorldSpace; wc.sortingOrder = 30;
        cGo.GetComponent<RectTransform>().sizeDelta = new Vector2(220, 50);

        var tGo = new GameObject("Label", typeof(RectTransform)); tGo.transform.SetParent(cGo.transform, false);
        var tmp = tGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label; tmp.fontSize = 28; tmp.color = col; tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center; tmp.raycastTarget = false;
        var tRT = tGo.GetComponent<RectTransform>(); tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one; tRT.offsetMin = tRT.offsetMax = Vector2.zero;

        pt.prompt = promptGo; promptGo.SetActive(false);
    }

    static (GameObject panel, Button adv, TMP_Text name, TMP_Text body, TMP_Text hint,
            GameObject cp, Button ab, TMP_Text at, Button db, TMP_Text dt)
        BuildDialoguePanel(RectTransform rt)
    {
        var advGo = new GameObject("AdvanceBtn", typeof(RectTransform)); advGo.transform.SetParent(rt, false); Stretch(advGo.GetComponent<RectTransform>());
        var advImg = advGo.AddComponent<Image>(); advImg.color = new Color(0, 0, 0, 0);
        var advBtn = advGo.AddComponent<Button>(); advBtn.targetGraphic = advImg;
        var panel = new GameObject("DialoguePanel", typeof(RectTransform)); panel.transform.SetParent(rt, false);
        var prt = panel.GetComponent<RectTransform>(); prt.anchorMin = new Vector2(0, 0); prt.anchorMax = new Vector2(1, 0); prt.pivot = new Vector2(0.5f, 0); prt.sizeDelta = new Vector2(0, 220); prt.anchoredPosition = Vector2.zero;
        panel.AddComponent<Image>().color = new Color(0.08f, 0.15f, 0.28f, 0.95f);
        var nameBar = UImg(prt, "NameBar", Hex("#1A3A6B")); var nbrrt = nameBar.rectTransform;
        nbrrt.anchorMin = new Vector2(0, 1); nbrrt.anchorMax = new Vector2(0.42f, 1); nbrrt.pivot = new Vector2(0, 1); nbrrt.sizeDelta = new Vector2(0, 44);
        var nameText = UTxt(nbrrt, "Name", "Speaker", 30, NameCol, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        var ntrt = nameText.rectTransform; ntrt.anchorMin = Vector2.zero; ntrt.anchorMax = Vector2.one; ntrt.offsetMin = new Vector2(18, 0); ntrt.offsetMax = Vector2.zero;
        var bodyText = UTxt(prt, "Body", "...", 26, Color.white, TextAlignmentOptions.TopLeft); bodyText.textWrappingMode = TextWrappingModes.Normal;
        var brrt = bodyText.rectTransform; brrt.anchorMin = Vector2.zero; brrt.anchorMax = Vector2.one; brrt.offsetMin = new Vector2(24, 48); brrt.offsetMax = new Vector2(-24, -50);
        var hint = UTxt(prt, "Hint", "Click to continue…", 20, new Color(0.7f, 0.7f, 0.9f), TextAlignmentOptions.MidlineRight);
        var hrrt = hint.rectTransform; hrrt.anchorMin = new Vector2(0, 0); hrrt.anchorMax = new Vector2(1, 0); hrrt.pivot = new Vector2(0.5f, 0); hrrt.sizeDelta = new Vector2(0, 36); hrrt.anchoredPosition = new Vector2(0, 8);
        var cpGo = new GameObject("ChoicePanel", typeof(RectTransform)); cpGo.transform.SetParent(prt, false);
        var cprt = cpGo.GetComponent<RectTransform>(); cprt.anchorMin = new Vector2(0.5f, 0); cprt.anchorMax = new Vector2(0.5f, 0); cprt.pivot = new Vector2(0.5f, 0); cprt.sizeDelta = new Vector2(800, 72); cprt.anchoredPosition = new Vector2(0, 8);
        var acceptGo = UBtn(cprt, "AcceptBtn", "Let's go!", 26, AcceptCol, Color.white); var declineGo = UBtn(cprt, "DeclineBtn", "Maybe later", 26, DeclineCol, Color.white);
        var a = acceptGo.GetComponent<RectTransform>(); a.anchorMin = new Vector2(0, 0); a.anchorMax = new Vector2(0.48f, 1); a.offsetMin = a.offsetMax = Vector2.zero;
        var d = declineGo.GetComponent<RectTransform>(); d.anchorMin = new Vector2(0.52f, 0); d.anchorMax = new Vector2(1, 1); d.offsetMin = d.offsetMax = Vector2.zero;
        panel.SetActive(false);
        return (panel, advBtn, nameText, bodyText, hint, cpGo, acceptGo.GetComponent<Button>(), acceptGo.transform.Find("Label").GetComponent<TMP_Text>(), declineGo.GetComponent<Button>(), declineGo.transform.Find("Label").GetComponent<TMP_Text>());
    }

    static void BuildMapHint(RectTransform canvasRT)
    {
        var go = new GameObject("MapHint", typeof(RectTransform)); go.transform.SetParent(canvasRT, false);
        var r = go.GetComponent<RectTransform>(); r.anchorMin = new Vector2(1, 1); r.anchorMax = new Vector2(1, 1); r.pivot = new Vector2(1, 1); r.sizeDelta = new Vector2(230, 44); r.anchoredPosition = new Vector2(-24, -24);
        go.AddComponent<Image>().color = new Color(0, 0, 0, 0.45f);
        Stretch(UTxt(go.transform, "T", "[ M ]  World Map", 22, new Color(0.9f, 0.9f, 1f), TextAlignmentOptions.Center).rectTransform);
    }

    static GameObject BuildWorldMapPanel(RectTransform canvasRT, WorldMapManager manager)
    {
        var ov = UImg(canvasRT, "WorldMapOverlay", new Color(0, 0, 0, 0.88f)); Stretch(ov.rectTransform); ov.raycastTarget = true;
        var card = UImg(ov.rectTransform, "MapCard", new Color(0, 0, 0, 0)); var crt = card.rectTransform;
        crt.anchorMin = new Vector2(0.05f, 0.06f); crt.anchorMax = new Vector2(0.95f, 0.94f); crt.offsetMin = crt.offsetMax = Vector2.zero;
        var mi = UImg(crt, "MapImage", Color.white); mi.type = Image.Type.Simple; mi.preserveAspect = true; mi.raycastTarget = false; Stretch(mi.rectTransform);
        Sprite[] mapVersions = new Sprite[5]; for (int v = 1; v <= 5; v++) mapVersions[v - 1] = FindSprite("map_v" + v);
        bool anyMapFound = false; foreach (var s in mapVersions) if (s != null) anyMapFound = true;
        if (anyMapFound) { var switcher = mi.gameObject.AddComponent<WorldMapImageSwitcher>(); switcher.targetImage = mi; switcher.mapVersions = mapVersions; switcher.Refresh(); } else { card.color = MapBg; }
        var tb = UImg(ov.rectTransform, "TitleBar", new Color(0.05f, 0.08f, 0.15f, 0.92f)); var tbr = tb.rectTransform;
        tbr.anchorMin = new Vector2(0, 1); tbr.anchorMax = new Vector2(1, 1); tbr.pivot = new Vector2(0.5f, 1); tbr.sizeDelta = new Vector2(0, 64); tbr.anchoredPosition = Vector2.zero;
        Stretch(UTxt(tbr, "Title", "PHISHERMAN  WORLD MAP", 36, Color.white, TextAlignmentOptions.Center, FontStyles.Bold).rectTransform);
        var cb = UBtn(ov.rectTransform, "CloseBtn", "X  Close Map  ( M )", 26, new Color(0.18f, 0.28f, 0.45f, 0.95f), Color.white);
        var cbrt = cb.GetComponent<RectTransform>(); cbrt.anchorMin = new Vector2(0.5f, 0); cbrt.anchorMax = new Vector2(0.5f, 0); cbrt.pivot = new Vector2(0.5f, 0); cbrt.sizeDelta = new Vector2(360, 56); cbrt.anchoredPosition = new Vector2(0, 12);
        UnityEventTools.AddPersistentListener(cb.GetComponent<Button>().onClick, manager.CloseMap);
        ov.gameObject.SetActive(false); return ov.gameObject;
    }

    static GameObject BuildLockedPopup(RectTransform canvasRT)
    {
        var ov = UImg(canvasRT, "LockedPopup", new Color(0, 0, 0, 0)); Stretch(ov.rectTransform); ov.raycastTarget = false;
        var card = UImg(ov.rectTransform, "Card", new Color(0.08f, 0.10f, 0.20f, 0.93f)); var crt = card.rectTransform;
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.62f); crt.pivot = new Vector2(0.5f, 0.5f); crt.sizeDelta = new Vector2(740, 150);
        var brd = UImg(crt, "Border", Hex("#2BB3A3")); var brt = brd.rectTransform;
        brt.anchorMin = new Vector2(0, 1); brt.anchorMax = new Vector2(1, 1); brt.pivot = new Vector2(0.5f, 1); brt.sizeDelta = new Vector2(0, 6);
        var txt = UTxt(crt, "Msg", "You haven't unlocked this area yet!\nExplore the town first.", 30, Color.white, TextAlignmentOptions.Center);
        txt.textWrappingMode = TextWrappingModes.Normal; Stretch(txt.rectTransform);
        var controller = ov.gameObject.AddComponent<LockedPopupController>();
        controller.messageText = txt;
        ov.gameObject.SetActive(false); return ov.gameObject;
    }

    static Sprite FindSprite(string name) { foreach (var g in AssetDatabase.FindAssets(name + " t:Texture2D")) { var p = AssetDatabase.GUIDToAssetPath(g); if (System.IO.Path.GetFileNameWithoutExtension(p).ToLower() == name.ToLower()) { var s = AssetDatabase.LoadAssetAtPath<Sprite>(p); if (s != null) return s; var reps = AssetDatabase.LoadAllAssetRepresentationsAtPath(p); foreach (var r in reps) { if (r is Sprite sp) return sp; } } } return null; }
    static Image UImg(Transform p, string n, Color c) { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); var img = go.AddComponent<Image>(); img.color = c; return img; }
    static TMP_Text UTxt(Transform p, string n, string text, int size, Color col, TextAlignmentOptions align, FontStyles style = FontStyles.Normal) { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); var t = go.AddComponent<TextMeshProUGUI>(); t.text = text; t.fontSize = size; t.color = col; t.alignment = align; t.fontStyle = style; t.raycastTarget = false; return t; }
    static GameObject UBtn(Transform p, string n, string label, int size, Color bg, Color tc) { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); var img = go.AddComponent<Image>(); img.color = bg; var btn = go.AddComponent<Button>(); btn.targetGraphic = img; var t = UTxt(go.transform, "Label", label, size, tc, TextAlignmentOptions.Center, FontStyles.Bold); Stretch(t.rectTransform); return go; }
    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
    static Color Hex(string h) => ColorUtility.TryParseHtmlString(h, out var c) ? c : Color.magenta;
    static void AddToBuild(string path) { var scenes = EditorBuildSettings.scenes.ToList(); if (!scenes.Any(s => s.path == path)) { scenes.Add(new EditorBuildSettingsScene(path, true)); EditorBuildSettings.scenes = scenes.ToArray(); } }
}

// ============================================================
//  World3Builder  (Stage 3 — Urban Mobile Quarter)
//  Phisherman > Build World 3 Scene
// ============================================================
public static class World3Builder
{
    private const string ScenesDir = "Assets/Scenes";
    private const string ScenePath = "Assets/Scenes/WorldMap3.unity";
    private const float OrthoSize = 4.5f;
    private const float AspectW = 16f / 9f;
    private const float SharedSpeed = 3.5f;
    private const float PlayerScale = 0.44f;
    static readonly Color AcceptCol = Hex("#2ECC71");
    static readonly Color DeclineCol = Hex("#E74C3C");
    static readonly Color NameCol = Hex("#FFD93D");
    static readonly Color MapBg = Hex("#1A6E9E");

    [MenuItem("Phisherman/Build World 3 Scene")]
    public static void Build()
    {
        if (!Directory.Exists(ScenesDir)) Directory.CreateDirectory(ScenesDir);

        if (File.Exists(ScenePath))
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (GameObject.Find("Canvas") != null)
            {
                Debug.Log("[World3Builder] WorldMap3.unity already exists and looks built — skipping to preserve your manual edits (door positions, extra paths, etc). Delete the scene file first if you want a full rebuild.");
                return;
            }
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Sprite phisherman = FindSprite("phisherman");
        Sprite phishermanWalk = FindSprite("phisherman_walking");

        var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>(); camGo.AddComponent<AudioListener>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Hex("#1A1A2E");
        cam.orthographic = true; cam.orthographicSize = OrthoSize;
        camGo.transform.position = new Vector3(0, 0, -10);

        var es = new GameObject("EventSystem"); es.AddComponent<EventSystem>(); es.AddComponent<StandaloneInputModule>();

        Sprite bgArt = FindSprite("world_stage_3_background");
        if (bgArt != null)
        {
            var bgGo = new GameObject("WorldBackground"); var bgSR = bgGo.AddComponent<SpriteRenderer>();
            bgSR.sprite = bgArt; bgSR.color = Color.white; bgSR.sortingOrder = -50;
            float camH = OrthoSize * 2f, camW = camH * AspectW;
            bgGo.transform.localScale = new Vector3(camW / bgArt.bounds.size.x, camH / bgArt.bounds.size.y, 1f);
        }
        else Debug.LogWarning("[World3Builder] 'world_stage_3_background' not found.");

        var playerGo = new GameObject("Phisherman"); playerGo.tag = "Player";
        playerGo.transform.position = new Vector3(0f, -1.5f, 0f);
        playerGo.transform.localScale = new Vector3(PlayerScale, PlayerScale, 1f);
        var playerSR = playerGo.AddComponent<SpriteRenderer>(); playerSR.sprite = phisherman; playerSR.color = Color.white; playerSR.sortingOrder = 10;
        var anim = playerGo.AddComponent<MapWalkAnimator>(); anim.idleSprite = phisherman; anim.walkSprite = phishermanWalk ?? phisherman; anim.fps = 2f;
        playerGo.AddComponent<MapWasdZoneClamp>();
        var nav = playerGo.AddComponent<MapNavAgent>(); nav.gridCols = 80; nav.gridRows = 45; nav.moveSpeed = SharedSpeed;

        var mgrGo = new GameObject("GameManager"); var manager = mgrGo.AddComponent<WorldMapManager>(); manager.playerSpeed = SharedSpeed;
        mgrGo.AddComponent<LevelSystemHUD>();
        Transform playerT = playerGo.transform;

        // Doors — starter positions, move them however you like in the editor.
        // Each rebuild is now a no-op once Canvas exists, so your edits stick.
        AddDoorCTA("Door_W3_House1", playerT, -4.0f, 0.4f, "AuntCarolInterior", "EmailSwiper", "Aunt Carol", Hex("#2BB3A3"));
        AddDoorCTA("Door_W3_House2", playerT, 0.2f, 1.2f, "UncleMarcusInterior", "TowerDefense", "Uncle Marcus", Hex("#FF9F1C"));
        AddDoorCTA("Door_W3_House3", playerT, 4.3f, 0.2f, "GrandpaLouInterior", "SpotDifference", "Grandpa Lou", Hex("#FFD93D"));

        var canvasGo = new GameObject("Canvas"); var canvas = canvasGo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>(); var canvasRT = canvasGo.GetComponent<RectTransform>();

        // Left → World 2 | Right → World 4
        AddPathFloatingText("Path_World2", playerT, -5.0f, -3.6f, MapPathTrigger.TriggerType.LoadScene, "WorldMap2", "‹ World 2", Hex("#2BB3A3"));
        AddPathFloatingText("Path_World4", playerT, 5.0f, -3.6f, MapPathTrigger.TriggerType.LoadScene, "WorldMap4", "World 4 ›", Hex("#FF9F1C"));

        var (dialogPanel, advBtn, nameText, bodyText, hintText, choicePanel, acceptBtn, acceptTxt, declineBtn, declineTxt) = BuildDialoguePanel(canvasRT);
        BuildMapHint(canvasRT);
        var mapPanel = BuildWorldMapPanel(canvasRT, manager);
        var lockedPopup3 = BuildLockedPopup(canvasRT);

        foreach (var doorName in new[] { "Door_W3_House1", "Door_W3_House2", "Door_W3_House3" })
        {
            var doorCta = GameObject.Find(doorName + "_CTA")?.GetComponent<MapDoorCTA>();
            if (doorCta != null) doorCta.completedLockPopup = lockedPopup3;
        }

        AddNavHint(canvasRT, "‹ World 2   |   World 4 ›");

        manager.playerTransform = playerGo.transform; manager.playerRenderer = playerSR;
        manager.npcTransforms = new Transform[0]; manager.exclamationMarks = new GameObject[0];
        manager.dialoguePanel = dialogPanel; manager.advanceButton = advBtn;
        manager.speakerNameText = nameText; manager.dialogueText = bodyText; manager.continueHint = hintText;
        manager.choicePanel = choicePanel; manager.acceptButton = acceptBtn; manager.acceptButtonText = acceptTxt;
        manager.declineButton = declineBtn; manager.declineButtonText = declineTxt;
        manager.worldMapPanel = mapPanel; manager.boundsX = new Vector2(-7.8f, 7.8f); manager.boundsY = new Vector2(-4.0f, 4.3f);
        UnityEventTools.AddPersistentListener(advBtn.onClick, manager.AdvanceDialogue);
        UnityEventTools.AddPersistentListener(acceptBtn.onClick, manager.OnAcceptHelp);
        UnityEventTools.AddPersistentListener(declineBtn.onClick, manager.OnDeclineHelp);

        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuild(ScenePath); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log($"[World3Builder] Built → {ScenePath}");
    }

    static void AddPathFloatingText(string name, Transform player, float cx, float cy, MapPathTrigger.TriggerType type, string targetScene, string label, Color col)
    {
        var go = new GameObject(name); go.transform.position = new Vector3(cx, cy, 0f);
        var pt = go.AddComponent<MapPathTrigger>(); pt.player = player; pt.type = type; pt.targetScene = targetScene; pt.promptProximity = 2.2f; pt.triggerRadius = 1.0f;
        var promptGo = new GameObject("Prompt"); promptGo.transform.SetParent(go.transform, false); promptGo.transform.localPosition = new Vector3(0f, 0.6f, -0.5f);
        var cGo = new GameObject("C"); cGo.transform.SetParent(promptGo.transform, false); cGo.transform.localScale = new Vector3(0.012f, 0.012f, 1f);
        var wc = cGo.AddComponent<Canvas>(); wc.renderMode = RenderMode.WorldSpace; wc.sortingOrder = 30; cGo.GetComponent<RectTransform>().sizeDelta = new Vector2(220, 50);
        var tGo = new GameObject("Label", typeof(RectTransform)); tGo.transform.SetParent(cGo.transform, false);
        var tmp = tGo.AddComponent<TextMeshProUGUI>(); tmp.text = label; tmp.fontSize = 28; tmp.color = col; tmp.fontStyle = FontStyles.Bold; tmp.alignment = TextAlignmentOptions.Center; tmp.raycastTarget = false;
        var tRT = tGo.GetComponent<RectTransform>(); tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one; tRT.offsetMin = tRT.offsetMax = Vector2.zero;
        pt.prompt = promptGo; promptGo.SetActive(false);
    }

    static void AddNavHint(RectTransform canvasRT, string msg)
    {
        var go = new GameObject("NavHint", typeof(RectTransform)); go.transform.SetParent(canvasRT, false);
        var r = go.GetComponent<RectTransform>(); r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1); r.sizeDelta = new Vector2(380, 44); r.anchoredPosition = new Vector2(24, -24);
        go.AddComponent<Image>().color = new Color(0, 0, 0, 0.45f);
        var t = UTxt(go.transform, "T", msg, 20, new Color(0.9f, 0.9f, 1f), TextAlignmentOptions.Center); Stretch(t.rectTransform);
    }

    // ── Door CTA (floating "come here!" label above a building) ───────
    static void AddDoorCTA(string name, Transform player, float cx, float cy,
        string interiorScene, string minigameScene, string label, Color col, GameObject lockedPopup = null)
    {
        var doorGo = new GameObject(name); doorGo.transform.position = new Vector3(cx, cy, 0f);
        var dt = doorGo.AddComponent<MapDoorTrigger>(); dt.player = player;
        dt.targetScene = interiorScene; dt.triggerRadius = 0.55f; dt.promptProximity = 999f; dt.prompt = null;

        var ctaRoot = new GameObject(name + "_CTA"); ctaRoot.transform.position = new Vector3(cx, cy + 0.75f, -0.5f);

        var cGo = new GameObject("C"); cGo.transform.SetParent(ctaRoot.transform, false);
        cGo.transform.localScale = new Vector3(0.012f, 0.012f, 1f);
        var wc = cGo.AddComponent<Canvas>(); wc.renderMode = RenderMode.WorldSpace; wc.sortingOrder = 35;
        cGo.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 70);

        var borderGo = new GameObject("Border", typeof(RectTransform)); borderGo.transform.SetParent(cGo.transform, false);
        var borderImg = borderGo.AddComponent<Image>(); borderImg.color = col; borderImg.raycastTarget = false;
        var brt = borderGo.GetComponent<RectTransform>();
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = new Vector2(-4, -4); brt.offsetMax = new Vector2(4, 4);

        var fillGo = new GameObject("Fill", typeof(RectTransform)); fillGo.transform.SetParent(cGo.transform, false);
        var fillImg = fillGo.AddComponent<Image>(); fillImg.color = new Color(0.06f, 0.08f, 0.14f, 0.92f); fillImg.raycastTarget = false;
        var frt = fillGo.GetComponent<RectTransform>(); frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = frt.offsetMax = Vector2.zero;

        var tGo = new GameObject("Label", typeof(RectTransform)); tGo.transform.SetParent(cGo.transform, false);
        var tmp = tGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label; tmp.fontSize = 38; tmp.color = col; tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center; tmp.raycastTarget = false;
        var tRT = tGo.GetComponent<RectTransform>(); tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one; tRT.offsetMin = new Vector2(8, 4); tRT.offsetMax = new Vector2(-8, -4);

        // Completion key uses the INTERIOR scene name so each house tracks
        // its own progress independently, even if it shares a minigame type
        // with another house — see InteriorDialogueManager.MarkComplete().
        var cta = ctaRoot.AddComponent<MapDoorCTA>();
        cta.targetScene = interiorScene;
        cta.playerTransform = player;
        cta.doorPosition = doorGo.transform;
        cta.label = tmp;
        cta.completionKey = "completed_" + interiorScene;
        cta.activeColor = col;
        cta.completedColor = new Color(col.r * 0.35f, col.g * 0.35f, col.b * 0.35f, 0.40f);
        cta.bobAmount = 0.16f;
        cta.bobSpeed = 2.8f;
        cta.clickRadius = 1.4f;
        cta.completedLockPopup = lockedPopup;
        cta.completedLockMessage = "This door is locked — please explore another building!";

        var borderCTA = ctaRoot.AddComponent<CTABorderFader>();
        borderCTA.borderImage = borderImg;
        borderCTA.completionKey = "completed_" + interiorScene;
        borderCTA.activeColor = col;
        borderCTA.completedColor = new Color(col.r * 0.35f, col.g * 0.35f, col.b * 0.35f, 0.40f);
    }

    static GameObject BuildLockedPopup(RectTransform canvasRT)
    {
        var ov = UImg(canvasRT, "LockedPopup", new Color(0, 0, 0, 0)); Stretch(ov.rectTransform); ov.raycastTarget = false;
        var card = UImg(ov.rectTransform, "Card", new Color(0.08f, 0.10f, 0.20f, 0.93f)); var crt = card.rectTransform;
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.62f); crt.pivot = new Vector2(0.5f, 0.5f); crt.sizeDelta = new Vector2(740, 150);
        var brd = UImg(crt, "Border", Hex("#E74C3C")); var brt = brd.rectTransform;
        brt.anchorMin = new Vector2(0, 1); brt.anchorMax = new Vector2(1, 1); brt.pivot = new Vector2(0.5f, 1); brt.sizeDelta = new Vector2(0, 6);
        var txt = UTxt(crt, "Msg", "You haven't unlocked this area yet!\nExplore the neighbourhood first.", 30, Color.white, TextAlignmentOptions.Center);
        txt.textWrappingMode = TextWrappingModes.Normal; Stretch(txt.rectTransform);
        var controller = ov.gameObject.AddComponent<LockedPopupController>();
        controller.messageText = txt;
        ov.gameObject.SetActive(false); return ov.gameObject;
    }

    static (GameObject panel, Button adv, TMP_Text name, TMP_Text body, TMP_Text hint, GameObject cp, Button ab, TMP_Text at, Button db, TMP_Text dt) BuildDialoguePanel(RectTransform rt)
    {
        var advGo = new GameObject("AdvanceBtn", typeof(RectTransform)); advGo.transform.SetParent(rt, false); Stretch(advGo.GetComponent<RectTransform>()); var advImg = advGo.AddComponent<Image>(); advImg.color = new Color(0, 0, 0, 0); var advBtn = advGo.AddComponent<Button>(); advBtn.targetGraphic = advImg;
        var panel = new GameObject("DialoguePanel", typeof(RectTransform)); panel.transform.SetParent(rt, false); var prt = panel.GetComponent<RectTransform>(); prt.anchorMin = new Vector2(0, 0); prt.anchorMax = new Vector2(1, 0); prt.pivot = new Vector2(0.5f, 0); prt.sizeDelta = new Vector2(0, 220); prt.anchoredPosition = Vector2.zero; panel.AddComponent<Image>().color = new Color(0.08f, 0.15f, 0.28f, 0.95f);
        var nameBar = UImg(prt, "NameBar", Hex("#1A3A6B")); var nbrrt = nameBar.rectTransform; nbrrt.anchorMin = new Vector2(0, 1); nbrrt.anchorMax = new Vector2(0.42f, 1); nbrrt.pivot = new Vector2(0, 1); nbrrt.sizeDelta = new Vector2(0, 44);
        var nameText = UTxt(nbrrt, "Name", "Speaker", 30, NameCol, TextAlignmentOptions.MidlineLeft, FontStyles.Bold); var ntrt = nameText.rectTransform; ntrt.anchorMin = Vector2.zero; ntrt.anchorMax = Vector2.one; ntrt.offsetMin = new Vector2(18, 0); ntrt.offsetMax = Vector2.zero;
        var bodyText = UTxt(prt, "Body", "...", 26, Color.white, TextAlignmentOptions.TopLeft); bodyText.textWrappingMode = TextWrappingModes.Normal; var brrt = bodyText.rectTransform; brrt.anchorMin = Vector2.zero; brrt.anchorMax = Vector2.one; brrt.offsetMin = new Vector2(24, 48); brrt.offsetMax = new Vector2(-24, -50);
        var hint = UTxt(prt, "Hint", "Click to continue…", 20, new Color(0.7f, 0.7f, 0.9f), TextAlignmentOptions.MidlineRight); var hrrt = hint.rectTransform; hrrt.anchorMin = new Vector2(0, 0); hrrt.anchorMax = new Vector2(1, 0); hrrt.pivot = new Vector2(0.5f, 0); hrrt.sizeDelta = new Vector2(0, 36); hrrt.anchoredPosition = new Vector2(0, 8);
        var cpGo = new GameObject("ChoicePanel", typeof(RectTransform)); cpGo.transform.SetParent(prt, false); var cprt = cpGo.GetComponent<RectTransform>(); cprt.anchorMin = new Vector2(0.5f, 0); cprt.anchorMax = new Vector2(0.5f, 0); cprt.pivot = new Vector2(0.5f, 0); cprt.sizeDelta = new Vector2(800, 72); cprt.anchoredPosition = new Vector2(0, 8);
        var acceptGo = UBtn(cprt, "AcceptBtn", "Let's go!", 26, AcceptCol, Color.white); var declineGo = UBtn(cprt, "DeclineBtn", "Maybe later", 26, DeclineCol, Color.white);
        var a = acceptGo.GetComponent<RectTransform>(); a.anchorMin = new Vector2(0, 0); a.anchorMax = new Vector2(0.48f, 1); a.offsetMin = a.offsetMax = Vector2.zero;
        var d = declineGo.GetComponent<RectTransform>(); d.anchorMin = new Vector2(0.52f, 0); d.anchorMax = new Vector2(1, 1); d.offsetMin = d.offsetMax = Vector2.zero;
        panel.SetActive(false);
        return (panel, advBtn, nameText, bodyText, hint, cpGo, acceptGo.GetComponent<Button>(), acceptGo.transform.Find("Label").GetComponent<TMP_Text>(), declineGo.GetComponent<Button>(), declineGo.transform.Find("Label").GetComponent<TMP_Text>());
    }

    static void BuildMapHint(RectTransform canvasRT) { var go = new GameObject("MapHint", typeof(RectTransform)); go.transform.SetParent(canvasRT, false); var r = go.GetComponent<RectTransform>(); r.anchorMin = new Vector2(1, 1); r.anchorMax = new Vector2(1, 1); r.pivot = new Vector2(1, 1); r.sizeDelta = new Vector2(230, 44); r.anchoredPosition = new Vector2(-24, -24); go.AddComponent<Image>().color = new Color(0, 0, 0, 0.45f); Stretch(UTxt(go.transform, "T", "[ M ]  World Map", 22, new Color(0.9f, 0.9f, 1f), TextAlignmentOptions.Center).rectTransform); }

    static GameObject BuildWorldMapPanel(RectTransform canvasRT, WorldMapManager manager)
    {
        var ov = UImg(canvasRT, "WorldMapOverlay", new Color(0, 0, 0, 0.88f)); Stretch(ov.rectTransform); ov.raycastTarget = true;
        var card = UImg(ov.rectTransform, "MapCard", new Color(0, 0, 0, 0)); var crt = card.rectTransform; crt.anchorMin = new Vector2(0.05f, 0.06f); crt.anchorMax = new Vector2(0.95f, 0.94f); crt.offsetMin = crt.offsetMax = Vector2.zero;
        var mi = UImg(crt, "MapImage", Color.white); mi.type = Image.Type.Simple; mi.preserveAspect = true; mi.raycastTarget = false; Stretch(mi.rectTransform);
        Sprite[] mapVersions = new Sprite[5]; for (int v = 1; v <= 5; v++) mapVersions[v - 1] = FindSprite("map_v" + v);
        bool anyMapFound = false; foreach (var s in mapVersions) if (s != null) anyMapFound = true;
        if (anyMapFound) { var switcher = mi.gameObject.AddComponent<WorldMapImageSwitcher>(); switcher.targetImage = mi; switcher.mapVersions = mapVersions; switcher.Refresh(); } else { card.color = MapBg; }
        var tb = UImg(ov.rectTransform, "TitleBar", new Color(0.05f, 0.08f, 0.15f, 0.92f)); var tbr = tb.rectTransform; tbr.anchorMin = new Vector2(0, 1); tbr.anchorMax = new Vector2(1, 1); tbr.pivot = new Vector2(0.5f, 1); tbr.sizeDelta = new Vector2(0, 64); tbr.anchoredPosition = Vector2.zero;
        Stretch(UTxt(tbr, "Title", "PHISHERMAN  WORLD MAP", 36, Color.white, TextAlignmentOptions.Center, FontStyles.Bold).rectTransform);
        var cb = UBtn(ov.rectTransform, "CloseBtn", "X  Close Map  ( M )", 26, new Color(0.18f, 0.28f, 0.45f, 0.95f), Color.white); var cbrt = cb.GetComponent<RectTransform>(); cbrt.anchorMin = new Vector2(0.5f, 0); cbrt.anchorMax = new Vector2(0.5f, 0); cbrt.pivot = new Vector2(0.5f, 0); cbrt.sizeDelta = new Vector2(360, 56); cbrt.anchoredPosition = new Vector2(0, 12);
        UnityEventTools.AddPersistentListener(cb.GetComponent<Button>().onClick, manager.CloseMap);
        ov.gameObject.SetActive(false); return ov.gameObject;
    }

    static Sprite FindSprite(string name) { foreach (var g in AssetDatabase.FindAssets(name + " t:Texture2D")) { var p = AssetDatabase.GUIDToAssetPath(g); if (System.IO.Path.GetFileNameWithoutExtension(p).ToLower() == name.ToLower()) { var s = AssetDatabase.LoadAssetAtPath<Sprite>(p); if (s != null) return s; var reps = AssetDatabase.LoadAllAssetRepresentationsAtPath(p); foreach (var r in reps) { if (r is Sprite sp) return sp; } } } return null; }
    static Image UImg(Transform p, string n, Color c) { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); var img = go.AddComponent<Image>(); img.color = c; return img; }
    static TMP_Text UTxt(Transform p, string n, string text, int size, Color col, TextAlignmentOptions align, FontStyles style = FontStyles.Normal) { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); var t = go.AddComponent<TextMeshProUGUI>(); t.text = text; t.fontSize = size; t.color = col; t.alignment = align; t.fontStyle = style; t.raycastTarget = false; return t; }
    static GameObject UBtn(Transform p, string n, string label, int size, Color bg, Color tc) { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); var img = go.AddComponent<Image>(); img.color = bg; var btn = go.AddComponent<Button>(); btn.targetGraphic = img; var t = UTxt(go.transform, "Label", label, size, tc, TextAlignmentOptions.Center, FontStyles.Bold); Stretch(t.rectTransform); return go; }
    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
    static Color Hex(string h) => ColorUtility.TryParseHtmlString(h, out var c) ? c : Color.magenta;
    static void AddToBuild(string path) { var scenes = EditorBuildSettings.scenes.ToList(); if (!scenes.Any(s => s.path == path)) { scenes.Add(new EditorBuildSettingsScene(path, true)); EditorBuildSettings.scenes = scenes.ToArray(); } }
}

// ============================================================
//  World4Builder  (Stage 4 — Social Plaza)
//  Phisherman > Build World 4 Scene
// ============================================================
public static class World4Builder
{
    private const string ScenesDir = "Assets/Scenes";
    private const string ScenePath = "Assets/Scenes/WorldMap4.unity";
    private const float OrthoSize = 4.5f;
    private const float AspectW = 16f / 9f;
    private const float SharedSpeed = 3.5f;
    private const float PlayerScale = 0.44f;
    static readonly Color AcceptCol = Hex("#2ECC71");
    static readonly Color DeclineCol = Hex("#E74C3C");
    static readonly Color NameCol = Hex("#FFD93D");
    static readonly Color MapBg = Hex("#1A6E9E");

    [MenuItem("Phisherman/Build World 4 Scene")]
    public static void Build()
    {
        if (!Directory.Exists(ScenesDir)) Directory.CreateDirectory(ScenesDir);

        if (File.Exists(ScenePath))
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (GameObject.Find("Canvas") != null)
            {
                Debug.Log("[World4Builder] WorldMap4.unity already exists and looks built — skipping to preserve your manual edits (door positions, extra paths, etc). Delete the scene file first if you want a full rebuild.");
                return;
            }
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Sprite phisherman = FindSprite("phisherman");
        Sprite phishermanWalk = FindSprite("phisherman_walking");

        var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>(); camGo.AddComponent<AudioListener>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Hex("#1A0A2E");
        cam.orthographic = true; cam.orthographicSize = OrthoSize;
        camGo.transform.position = new Vector3(0, 0, -10);

        var es = new GameObject("EventSystem"); es.AddComponent<EventSystem>(); es.AddComponent<StandaloneInputModule>();

        Sprite bgArt = FindSprite("world_stage_4_background");
        if (bgArt != null)
        {
            var bgGo = new GameObject("WorldBackground"); var bgSR = bgGo.AddComponent<SpriteRenderer>();
            bgSR.sprite = bgArt; bgSR.color = Color.white; bgSR.sortingOrder = -50;
            float camH = OrthoSize * 2f, camW = camH * AspectW;
            bgGo.transform.localScale = new Vector3(camW / bgArt.bounds.size.x, camH / bgArt.bounds.size.y, 1f);
        }
        else Debug.LogWarning("[World4Builder] 'world_stage_4_background' not found.");

        var playerGo = new GameObject("Phisherman"); playerGo.tag = "Player";
        playerGo.transform.position = new Vector3(0f, -1.5f, 0f);
        playerGo.transform.localScale = new Vector3(PlayerScale, PlayerScale, 1f);
        var playerSR = playerGo.AddComponent<SpriteRenderer>(); playerSR.sprite = phisherman; playerSR.color = Color.white; playerSR.sortingOrder = 10;
        var anim = playerGo.AddComponent<MapWalkAnimator>(); anim.idleSprite = phisherman; anim.walkSprite = phishermanWalk ?? phisherman; anim.fps = 2f;
        playerGo.AddComponent<MapWasdZoneClamp>();
        var nav = playerGo.AddComponent<MapNavAgent>(); nav.gridCols = 80; nav.gridRows = 45; nav.moveSpeed = SharedSpeed;

        var mgrGo = new GameObject("GameManager"); var manager = mgrGo.AddComponent<WorldMapManager>(); manager.playerSpeed = SharedSpeed;
        mgrGo.AddComponent<LevelSystemHUD>();
        Transform playerT = playerGo.transform;

        // Doors — starter positions, move them however you like in the editor.
        // Each rebuild is now a no-op once Canvas exists, so your edits stick.
        AddDoorCTA("Door_W4_House1", playerT, -4.0f, 0.4f, "GrandmaIrisInterior", "TowerDefense", "Grandma Iris", Hex("#2ECC71"));
        AddDoorCTA("Door_W4_House2", playerT, 0.2f, 1.2f, "UncleFelixInterior", "SpotDifference", "Uncle Felix", Hex("#A29BFE"));
        AddDoorCTA("Door_W4_House3", playerT, 4.3f, 0.2f, "AuntDanaInterior", "EmailSwiper", "Aunt Dana", Hex("#FF6B6B"));

        var canvasGo = new GameObject("Canvas"); var canvas = canvasGo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>(); var canvasRT = canvasGo.GetComponent<RectTransform>();

        // Left → World 3 | Right → World 5 (locked for now)
        AddPathFloatingText("Path_World3", playerT, -5.0f, -3.6f, MapPathTrigger.TriggerType.LoadScene, "WorldMap3", "‹ World 3", Hex("#2BB3A3"));
        AddPathFloatingText("Path_World5", playerT, 5.0f, -3.6f, MapPathTrigger.TriggerType.LoadScene, "WorldMap5", "World 5 ›", Hex("#A29BFE"));

        var (dialogPanel, advBtn, nameText, bodyText, hintText, choicePanel, acceptBtn, acceptTxt, declineBtn, declineTxt) = BuildDialoguePanel(canvasRT);
        BuildMapHint(canvasRT);
        var mapPanel = BuildWorldMapPanel(canvasRT, manager);
        var lockedPopup4 = BuildLockedPopup(canvasRT);

        foreach (var doorName in new[] { "Door_W4_House1", "Door_W4_House2", "Door_W4_House3" })
        {
            var doorCta = GameObject.Find(doorName + "_CTA")?.GetComponent<MapDoorCTA>();
            if (doorCta != null) doorCta.completedLockPopup = lockedPopup4;
        }

        AddNavHint(canvasRT, "‹ World 3   |   World 5 ›");

        manager.playerTransform = playerGo.transform; manager.playerRenderer = playerSR;
        manager.npcTransforms = new Transform[0]; manager.exclamationMarks = new GameObject[0];
        manager.dialoguePanel = dialogPanel; manager.advanceButton = advBtn;
        manager.speakerNameText = nameText; manager.dialogueText = bodyText; manager.continueHint = hintText;
        manager.choicePanel = choicePanel; manager.acceptButton = acceptBtn; manager.acceptButtonText = acceptTxt;
        manager.declineButton = declineBtn; manager.declineButtonText = declineTxt;
        manager.worldMapPanel = mapPanel; manager.boundsX = new Vector2(-7.8f, 7.8f); manager.boundsY = new Vector2(-4.0f, 4.3f);
        UnityEventTools.AddPersistentListener(advBtn.onClick, manager.AdvanceDialogue);
        UnityEventTools.AddPersistentListener(acceptBtn.onClick, manager.OnAcceptHelp);
        UnityEventTools.AddPersistentListener(declineBtn.onClick, manager.OnDeclineHelp);

        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuild(ScenePath); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log($"[World4Builder] Built → {ScenePath}");
    }

    static void AddPathFloatingText(string name, Transform player, float cx, float cy, MapPathTrigger.TriggerType type, string targetScene, string label, Color col)
    {
        var go = new GameObject(name); go.transform.position = new Vector3(cx, cy, 0f);
        var pt = go.AddComponent<MapPathTrigger>(); pt.player = player; pt.type = type; pt.targetScene = targetScene; pt.promptProximity = 2.2f; pt.triggerRadius = 1.0f;
        var promptGo = new GameObject("Prompt"); promptGo.transform.SetParent(go.transform, false); promptGo.transform.localPosition = new Vector3(0f, 0.6f, -0.5f);
        var cGo = new GameObject("C"); cGo.transform.SetParent(promptGo.transform, false); cGo.transform.localScale = new Vector3(0.012f, 0.012f, 1f);
        var wc = cGo.AddComponent<Canvas>(); wc.renderMode = RenderMode.WorldSpace; wc.sortingOrder = 30; cGo.GetComponent<RectTransform>().sizeDelta = new Vector2(220, 50);
        var tGo = new GameObject("Label", typeof(RectTransform)); tGo.transform.SetParent(cGo.transform, false);
        var tmp = tGo.AddComponent<TextMeshProUGUI>(); tmp.text = label; tmp.fontSize = 28; tmp.color = col; tmp.fontStyle = FontStyles.Bold; tmp.alignment = TextAlignmentOptions.Center; tmp.raycastTarget = false;
        var tRT = tGo.GetComponent<RectTransform>(); tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one; tRT.offsetMin = tRT.offsetMax = Vector2.zero;
        pt.prompt = promptGo; promptGo.SetActive(false);
    }

    static void AddNavHint(RectTransform canvasRT, string msg) { var go = new GameObject("NavHint", typeof(RectTransform)); go.transform.SetParent(canvasRT, false); var r = go.GetComponent<RectTransform>(); r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1); r.sizeDelta = new Vector2(380, 44); r.anchoredPosition = new Vector2(24, -24); go.AddComponent<Image>().color = new Color(0, 0, 0, 0.45f); var t = UTxt(go.transform, "T", msg, 20, new Color(0.9f, 0.9f, 1f), TextAlignmentOptions.Center); Stretch(t.rectTransform); }

    static (GameObject panel, Button adv, TMP_Text name, TMP_Text body, TMP_Text hint, GameObject cp, Button ab, TMP_Text at, Button db, TMP_Text dt) BuildDialoguePanel(RectTransform rt)
    {
        var advGo = new GameObject("AdvanceBtn", typeof(RectTransform)); advGo.transform.SetParent(rt, false); Stretch(advGo.GetComponent<RectTransform>()); var advImg = advGo.AddComponent<Image>(); advImg.color = new Color(0, 0, 0, 0); var advBtn = advGo.AddComponent<Button>(); advBtn.targetGraphic = advImg;
        var panel = new GameObject("DialoguePanel", typeof(RectTransform)); panel.transform.SetParent(rt, false); var prt = panel.GetComponent<RectTransform>(); prt.anchorMin = new Vector2(0, 0); prt.anchorMax = new Vector2(1, 0); prt.pivot = new Vector2(0.5f, 0); prt.sizeDelta = new Vector2(0, 220); prt.anchoredPosition = Vector2.zero; panel.AddComponent<Image>().color = new Color(0.08f, 0.15f, 0.28f, 0.95f);
        var nameBar = UImg(prt, "NameBar", Hex("#1A3A6B")); var nbrrt = nameBar.rectTransform; nbrrt.anchorMin = new Vector2(0, 1); nbrrt.anchorMax = new Vector2(0.42f, 1); nbrrt.pivot = new Vector2(0, 1); nbrrt.sizeDelta = new Vector2(0, 44);
        var nameText = UTxt(nbrrt, "Name", "Speaker", 30, NameCol, TextAlignmentOptions.MidlineLeft, FontStyles.Bold); var ntrt = nameText.rectTransform; ntrt.anchorMin = Vector2.zero; ntrt.anchorMax = Vector2.one; ntrt.offsetMin = new Vector2(18, 0); ntrt.offsetMax = Vector2.zero;
        var bodyText = UTxt(prt, "Body", "...", 26, Color.white, TextAlignmentOptions.TopLeft); bodyText.textWrappingMode = TextWrappingModes.Normal; var brrt = bodyText.rectTransform; brrt.anchorMin = Vector2.zero; brrt.anchorMax = Vector2.one; brrt.offsetMin = new Vector2(24, 48); brrt.offsetMax = new Vector2(-24, -50);
        var hint = UTxt(prt, "Hint", "Click to continue…", 20, new Color(0.7f, 0.7f, 0.9f), TextAlignmentOptions.MidlineRight); var hrrt = hint.rectTransform; hrrt.anchorMin = new Vector2(0, 0); hrrt.anchorMax = new Vector2(1, 0); hrrt.pivot = new Vector2(0.5f, 0); hrrt.sizeDelta = new Vector2(0, 36); hrrt.anchoredPosition = new Vector2(0, 8);
        var cpGo = new GameObject("ChoicePanel", typeof(RectTransform)); cpGo.transform.SetParent(prt, false); var cprt = cpGo.GetComponent<RectTransform>(); cprt.anchorMin = new Vector2(0.5f, 0); cprt.anchorMax = new Vector2(0.5f, 0); cprt.pivot = new Vector2(0.5f, 0); cprt.sizeDelta = new Vector2(800, 72); cprt.anchoredPosition = new Vector2(0, 8);
        var acceptGo = UBtn(cprt, "AcceptBtn", "Let's go!", 26, AcceptCol, Color.white); var declineGo = UBtn(cprt, "DeclineBtn", "Maybe later", 26, DeclineCol, Color.white);
        var a = acceptGo.GetComponent<RectTransform>(); a.anchorMin = new Vector2(0, 0); a.anchorMax = new Vector2(0.48f, 1); a.offsetMin = a.offsetMax = Vector2.zero;
        var d = declineGo.GetComponent<RectTransform>(); d.anchorMin = new Vector2(0.52f, 0); d.anchorMax = new Vector2(1, 1); d.offsetMin = d.offsetMax = Vector2.zero;
        panel.SetActive(false);
        return (panel, advBtn, nameText, bodyText, hint, cpGo, acceptGo.GetComponent<Button>(), acceptGo.transform.Find("Label").GetComponent<TMP_Text>(), declineGo.GetComponent<Button>(), declineGo.transform.Find("Label").GetComponent<TMP_Text>());
    }

    static void BuildMapHint(RectTransform canvasRT) { var go = new GameObject("MapHint", typeof(RectTransform)); go.transform.SetParent(canvasRT, false); var r = go.GetComponent<RectTransform>(); r.anchorMin = new Vector2(1, 1); r.anchorMax = new Vector2(1, 1); r.pivot = new Vector2(1, 1); r.sizeDelta = new Vector2(230, 44); r.anchoredPosition = new Vector2(-24, -24); go.AddComponent<Image>().color = new Color(0, 0, 0, 0.45f); Stretch(UTxt(go.transform, "T", "[ M ]  World Map", 22, new Color(0.9f, 0.9f, 1f), TextAlignmentOptions.Center).rectTransform); }

    static GameObject BuildWorldMapPanel(RectTransform canvasRT, WorldMapManager manager)
    {
        var ov = UImg(canvasRT, "WorldMapOverlay", new Color(0, 0, 0, 0.88f)); Stretch(ov.rectTransform); ov.raycastTarget = true;
        var card = UImg(ov.rectTransform, "MapCard", new Color(0, 0, 0, 0)); var crt = card.rectTransform; crt.anchorMin = new Vector2(0.05f, 0.06f); crt.anchorMax = new Vector2(0.95f, 0.94f); crt.offsetMin = crt.offsetMax = Vector2.zero;
        var mi = UImg(crt, "MapImage", Color.white); mi.type = Image.Type.Simple; mi.preserveAspect = true; mi.raycastTarget = false; Stretch(mi.rectTransform);
        Sprite[] mapVersions = new Sprite[5]; for (int v = 1; v <= 5; v++) mapVersions[v - 1] = FindSprite("map_v" + v);
        bool anyMapFound = false; foreach (var s in mapVersions) if (s != null) anyMapFound = true;
        if (anyMapFound) { var switcher = mi.gameObject.AddComponent<WorldMapImageSwitcher>(); switcher.targetImage = mi; switcher.mapVersions = mapVersions; switcher.Refresh(); } else { card.color = MapBg; }
        var tb = UImg(ov.rectTransform, "TitleBar", new Color(0.05f, 0.08f, 0.15f, 0.92f)); var tbr = tb.rectTransform; tbr.anchorMin = new Vector2(0, 1); tbr.anchorMax = new Vector2(1, 1); tbr.pivot = new Vector2(0.5f, 1); tbr.sizeDelta = new Vector2(0, 64); tbr.anchoredPosition = Vector2.zero;
        Stretch(UTxt(tbr, "Title", "PHISHERMAN  WORLD MAP", 36, Color.white, TextAlignmentOptions.Center, FontStyles.Bold).rectTransform);
        var cb = UBtn(ov.rectTransform, "CloseBtn", "X  Close Map  ( M )", 26, new Color(0.18f, 0.28f, 0.45f, 0.95f), Color.white); var cbrt = cb.GetComponent<RectTransform>(); cbrt.anchorMin = new Vector2(0.5f, 0); cbrt.anchorMax = new Vector2(0.5f, 0); cbrt.pivot = new Vector2(0.5f, 0); cbrt.sizeDelta = new Vector2(360, 56); cbrt.anchoredPosition = new Vector2(0, 12);
        UnityEventTools.AddPersistentListener(cb.GetComponent<Button>().onClick, manager.CloseMap);
        ov.gameObject.SetActive(false); return ov.gameObject;
    }

    // ── Door CTA (floating "come here!" label above a building) ───────
    static void AddDoorCTA(string name, Transform player, float cx, float cy,
        string interiorScene, string minigameScene, string label, Color col, GameObject lockedPopup = null)
    {
        var doorGo = new GameObject(name); doorGo.transform.position = new Vector3(cx, cy, 0f);
        var dt = doorGo.AddComponent<MapDoorTrigger>(); dt.player = player;
        dt.targetScene = interiorScene; dt.triggerRadius = 0.55f; dt.promptProximity = 999f; dt.prompt = null;

        var ctaRoot = new GameObject(name + "_CTA"); ctaRoot.transform.position = new Vector3(cx, cy + 0.75f, -0.5f);

        var cGo = new GameObject("C"); cGo.transform.SetParent(ctaRoot.transform, false);
        cGo.transform.localScale = new Vector3(0.012f, 0.012f, 1f);
        var wc = cGo.AddComponent<Canvas>(); wc.renderMode = RenderMode.WorldSpace; wc.sortingOrder = 35;
        cGo.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 70);

        var borderGo = new GameObject("Border", typeof(RectTransform)); borderGo.transform.SetParent(cGo.transform, false);
        var borderImg = borderGo.AddComponent<Image>(); borderImg.color = col; borderImg.raycastTarget = false;
        var brt = borderGo.GetComponent<RectTransform>();
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = new Vector2(-4, -4); brt.offsetMax = new Vector2(4, 4);

        var fillGo = new GameObject("Fill", typeof(RectTransform)); fillGo.transform.SetParent(cGo.transform, false);
        var fillImg = fillGo.AddComponent<Image>(); fillImg.color = new Color(0.06f, 0.08f, 0.14f, 0.92f); fillImg.raycastTarget = false;
        var frt = fillGo.GetComponent<RectTransform>(); frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = frt.offsetMax = Vector2.zero;

        var tGo = new GameObject("Label", typeof(RectTransform)); tGo.transform.SetParent(cGo.transform, false);
        var tmp = tGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label; tmp.fontSize = 38; tmp.color = col; tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center; tmp.raycastTarget = false;
        var tRT = tGo.GetComponent<RectTransform>(); tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one; tRT.offsetMin = new Vector2(8, 4); tRT.offsetMax = new Vector2(-8, -4);

        // Completion key uses the INTERIOR scene name so each house tracks
        // its own progress independently, even if it shares a minigame type
        // with another house — see InteriorDialogueManager.MarkComplete().
        var cta = ctaRoot.AddComponent<MapDoorCTA>();
        cta.targetScene = interiorScene;
        cta.playerTransform = player;
        cta.doorPosition = doorGo.transform;
        cta.label = tmp;
        cta.completionKey = "completed_" + interiorScene;
        cta.activeColor = col;
        cta.completedColor = new Color(col.r * 0.35f, col.g * 0.35f, col.b * 0.35f, 0.40f);
        cta.bobAmount = 0.16f;
        cta.bobSpeed = 2.8f;
        cta.clickRadius = 1.4f;
        cta.completedLockPopup = lockedPopup;
        cta.completedLockMessage = "This door is locked — please explore another building!";

        var borderCTA = ctaRoot.AddComponent<CTABorderFader>();
        borderCTA.borderImage = borderImg;
        borderCTA.completionKey = "completed_" + interiorScene;
        borderCTA.activeColor = col;
        borderCTA.completedColor = new Color(col.r * 0.35f, col.g * 0.35f, col.b * 0.35f, 0.40f);
    }

    static GameObject BuildLockedPopup(RectTransform canvasRT)
    {
        var ov = UImg(canvasRT, "LockedPopup", new Color(0, 0, 0, 0)); Stretch(ov.rectTransform); ov.raycastTarget = false;
        var card = UImg(ov.rectTransform, "Card", new Color(0.08f, 0.10f, 0.20f, 0.93f)); var crt = card.rectTransform;
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.62f); crt.pivot = new Vector2(0.5f, 0.5f); crt.sizeDelta = new Vector2(740, 150);
        var brd = UImg(crt, "Border", Hex("#A29BFE")); var brt = brd.rectTransform;
        brt.anchorMin = new Vector2(0, 1); brt.anchorMax = new Vector2(1, 1); brt.pivot = new Vector2(0.5f, 1); brt.sizeDelta = new Vector2(0, 6);
        var txt = UTxt(crt, "Msg", "You haven't unlocked this area yet!\nExplore the plaza first.", 30, Color.white, TextAlignmentOptions.Center);
        txt.textWrappingMode = TextWrappingModes.Normal; Stretch(txt.rectTransform);
        var controller = ov.gameObject.AddComponent<LockedPopupController>();
        controller.messageText = txt;
        ov.gameObject.SetActive(false); return ov.gameObject;
    }

    static Sprite FindSprite(string name) { foreach (var g in AssetDatabase.FindAssets(name + " t:Texture2D")) { var p = AssetDatabase.GUIDToAssetPath(g); if (System.IO.Path.GetFileNameWithoutExtension(p).ToLower() == name.ToLower()) { var s = AssetDatabase.LoadAssetAtPath<Sprite>(p); if (s != null) return s; var reps = AssetDatabase.LoadAllAssetRepresentationsAtPath(p); foreach (var r in reps) { if (r is Sprite sp) return sp; } } } return null; }
    static Image UImg(Transform p, string n, Color c) { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); var img = go.AddComponent<Image>(); img.color = c; return img; }
    static TMP_Text UTxt(Transform p, string n, string text, int size, Color col, TextAlignmentOptions align, FontStyles style = FontStyles.Normal) { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); var t = go.AddComponent<TextMeshProUGUI>(); t.text = text; t.fontSize = size; t.color = col; t.alignment = align; t.fontStyle = style; t.raycastTarget = false; return t; }
    static GameObject UBtn(Transform p, string n, string label, int size, Color bg, Color tc) { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); var img = go.AddComponent<Image>(); img.color = bg; var btn = go.AddComponent<Button>(); btn.targetGraphic = img; var t = UTxt(go.transform, "Label", label, size, tc, TextAlignmentOptions.Center, FontStyles.Bold); Stretch(t.rectTransform); return go; }
    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
    static Color Hex(string h) => ColorUtility.TryParseHtmlString(h, out var c) ? c : Color.magenta;
    static void AddToBuild(string path) { var scenes = EditorBuildSettings.scenes.ToList(); if (!scenes.Any(s => s.path == path)) { scenes.Add(new EditorBuildSettingsScene(path, true)); EditorBuildSettings.scenes = scenes.ToArray(); } }
}

// ============================================================
//  World5Builder  (Stage 5 — The Inner Island, final world)
//  Phisherman > Build World 5 Scene
// ============================================================
public static class World5Builder
{
    private const string ScenesDir = "Assets/Scenes";
    private const string ScenePath = "Assets/Scenes/WorldMap5.unity";
    private const float OrthoSize = 4.5f;
    private const float AspectW = 16f / 9f;
    private const float SharedSpeed = 3.5f;
    private const float PlayerScale = 0.44f;
    static readonly Color AcceptCol = Hex("#2ECC71");
    static readonly Color DeclineCol = Hex("#E74C3C");
    static readonly Color NameCol = Hex("#FFD93D");
    static readonly Color MapBg = Hex("#1A6E9E");

    [MenuItem("Phisherman/Build World 5 Scene")]
    public static void Build()
    {
        if (!Directory.Exists(ScenesDir)) Directory.CreateDirectory(ScenesDir);

        if (File.Exists(ScenePath))
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (GameObject.Find("Canvas") != null)
            {
                Debug.Log("[World5Builder] WorldMap5.unity already exists and looks built — skipping to preserve your manual edits (door positions, extra paths, etc). Delete the scene file first if you want a full rebuild.");
                return;
            }
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Sprite phisherman = FindSprite("phisherman");
        Sprite phishermanWalk = FindSprite("phisherman_walking");

        var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>(); camGo.AddComponent<AudioListener>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Hex("#050D1A");
        cam.orthographic = true; cam.orthographicSize = OrthoSize;
        camGo.transform.position = new Vector3(0, 0, -10);

        var es = new GameObject("EventSystem"); es.AddComponent<EventSystem>(); es.AddComponent<StandaloneInputModule>();

        Sprite bgArt = FindSprite("world_stage_5_background");
        if (bgArt != null)
        {
            var bgGo = new GameObject("WorldBackground"); var bgSR = bgGo.AddComponent<SpriteRenderer>();
            bgSR.sprite = bgArt; bgSR.color = Color.white; bgSR.sortingOrder = -50;
            float camH = OrthoSize * 2f, camW = camH * AspectW;
            bgGo.transform.localScale = new Vector3(camW / bgArt.bounds.size.x, camH / bgArt.bounds.size.y, 1f);
        }
        else Debug.LogWarning("[World5Builder] 'world_stage_5_background' not found.");

        var playerGo = new GameObject("Phisherman"); playerGo.tag = "Player";
        playerGo.transform.position = new Vector3(0f, -1.5f, 0f);
        playerGo.transform.localScale = new Vector3(PlayerScale, PlayerScale, 1f);
        var playerSR = playerGo.AddComponent<SpriteRenderer>(); playerSR.sprite = phisherman; playerSR.color = Color.white; playerSR.sortingOrder = 10;
        var anim = playerGo.AddComponent<MapWalkAnimator>(); anim.idleSprite = phisherman; anim.walkSprite = phishermanWalk ?? phisherman; anim.fps = 2f;
        playerGo.AddComponent<MapWasdZoneClamp>();
        var nav = playerGo.AddComponent<MapNavAgent>(); nav.gridCols = 80; nav.gridRows = 45; nav.moveSpeed = SharedSpeed;

        var mgrGo = new GameObject("GameManager"); var manager = mgrGo.AddComponent<WorldMapManager>(); manager.playerSpeed = SharedSpeed;
        mgrGo.AddComponent<LevelSystemHUD>();
        Transform playerT = playerGo.transform;

        // Doors — starter positions, move them however you like in the editor.
        // Each rebuild is now a no-op once Canvas exists, so your edits stick.
        AddDoorCTA("Door_W5_House1", playerT, -4.0f, 0.4f, "GrandpaErnestInterior", "EmailSwiper", "Grandpa Ernest", Hex("#3498DB"));
        AddDoorCTA("Door_W5_House2", playerT, 0.2f, 1.2f, "AuntPriyaInterior", "SpotDifference", "Aunt Priya", Hex("#1ABC9C"));
        AddDoorCTA("Door_W5_House3", playerT, 4.3f, 0.2f, "UncleDiegoInterior", "TowerDefense", "Uncle Diego", Hex("#E74C3C"));

        var canvasGo = new GameObject("Canvas"); var canvas = canvasGo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>(); var canvasRT = canvasGo.GetComponent<RectTransform>();

        // World 5 is the final island — both exits return to World 4
        AddPathFloatingText("Path_World4_L", playerT, -5.0f, -3.6f, MapPathTrigger.TriggerType.LoadScene, "WorldMap4", "‹ Exit Island", Hex("#A29BFE"));
        AddPathFloatingText("Path_World4_R", playerT, 5.0f, -3.6f, MapPathTrigger.TriggerType.LoadScene, "WorldMap4", "Exit Island ›", Hex("#A29BFE"));

        var (dialogPanel, advBtn, nameText, bodyText, hintText, choicePanel, acceptBtn, acceptTxt, declineBtn, declineTxt) = BuildDialoguePanel(canvasRT);
        BuildMapHint(canvasRT);
        var mapPanel = BuildWorldMapPanel(canvasRT, manager);
        var lockedPopup5 = BuildLockedPopup(canvasRT);

        foreach (var doorName in new[] { "Door_W5_House1", "Door_W5_House2", "Door_W5_House3" })
        {
            var doorCta = GameObject.Find(doorName + "_CTA")?.GetComponent<MapDoorCTA>();
            if (doorCta != null) doorCta.completedLockPopup = lockedPopup5;
        }

        AddNavHint(canvasRT, "The Inner Island — Final World");

        manager.playerTransform = playerGo.transform; manager.playerRenderer = playerSR;
        manager.npcTransforms = new Transform[0]; manager.exclamationMarks = new GameObject[0];
        manager.dialoguePanel = dialogPanel; manager.advanceButton = advBtn;
        manager.speakerNameText = nameText; manager.dialogueText = bodyText; manager.continueHint = hintText;
        manager.choicePanel = choicePanel; manager.acceptButton = acceptBtn; manager.acceptButtonText = acceptTxt;
        manager.declineButton = declineBtn; manager.declineButtonText = declineTxt;
        manager.worldMapPanel = mapPanel; manager.boundsX = new Vector2(-7.8f, 7.8f); manager.boundsY = new Vector2(-4.0f, 4.3f);
        UnityEventTools.AddPersistentListener(advBtn.onClick, manager.AdvanceDialogue);
        UnityEventTools.AddPersistentListener(acceptBtn.onClick, manager.OnAcceptHelp);
        UnityEventTools.AddPersistentListener(declineBtn.onClick, manager.OnDeclineHelp);

        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuild(ScenePath); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log($"[World5Builder] Built → {ScenePath}");
    }

    static void AddPathFloatingText(string name, Transform player, float cx, float cy, MapPathTrigger.TriggerType type, string targetScene, string label, Color col)
    {
        var go = new GameObject(name); go.transform.position = new Vector3(cx, cy, 0f);
        var pt = go.AddComponent<MapPathTrigger>(); pt.player = player; pt.type = type; pt.targetScene = targetScene; pt.promptProximity = 2.2f; pt.triggerRadius = 1.0f;
        var promptGo = new GameObject("Prompt"); promptGo.transform.SetParent(go.transform, false); promptGo.transform.localPosition = new Vector3(0f, 0.6f, -0.5f);
        var cGo = new GameObject("C"); cGo.transform.SetParent(promptGo.transform, false); cGo.transform.localScale = new Vector3(0.012f, 0.012f, 1f);
        var wc = cGo.AddComponent<Canvas>(); wc.renderMode = RenderMode.WorldSpace; wc.sortingOrder = 30; cGo.GetComponent<RectTransform>().sizeDelta = new Vector2(220, 50);
        var tGo = new GameObject("Label", typeof(RectTransform)); tGo.transform.SetParent(cGo.transform, false);
        var tmp = tGo.AddComponent<TextMeshProUGUI>(); tmp.text = label; tmp.fontSize = 28; tmp.color = col; tmp.fontStyle = FontStyles.Bold; tmp.alignment = TextAlignmentOptions.Center; tmp.raycastTarget = false;
        var tRT = tGo.GetComponent<RectTransform>(); tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one; tRT.offsetMin = tRT.offsetMax = Vector2.zero;
        pt.prompt = promptGo; promptGo.SetActive(false);
    }

    static void AddNavHint(RectTransform canvasRT, string msg) { var go = new GameObject("NavHint", typeof(RectTransform)); go.transform.SetParent(canvasRT, false); var r = go.GetComponent<RectTransform>(); r.anchorMin = new Vector2(0.5f, 1); r.anchorMax = new Vector2(0.5f, 1); r.pivot = new Vector2(0.5f, 1); r.sizeDelta = new Vector2(480, 44); r.anchoredPosition = new Vector2(0, -24); go.AddComponent<Image>().color = new Color(0, 0, 0, 0.45f); var t = UTxt(go.transform, "T", msg, 20, new Color(0.85f, 0.80f, 1f), TextAlignmentOptions.Center, FontStyles.Italic); Stretch(t.rectTransform); }

    // ── Door CTA (floating "come here!" label above a building) ───────
    static void AddDoorCTA(string name, Transform player, float cx, float cy,
        string interiorScene, string minigameScene, string label, Color col, GameObject lockedPopup = null)
    {
        var doorGo = new GameObject(name); doorGo.transform.position = new Vector3(cx, cy, 0f);
        var dt = doorGo.AddComponent<MapDoorTrigger>(); dt.player = player;
        dt.targetScene = interiorScene; dt.triggerRadius = 0.55f; dt.promptProximity = 999f; dt.prompt = null;

        var ctaRoot = new GameObject(name + "_CTA"); ctaRoot.transform.position = new Vector3(cx, cy + 0.75f, -0.5f);

        var cGo = new GameObject("C"); cGo.transform.SetParent(ctaRoot.transform, false);
        cGo.transform.localScale = new Vector3(0.012f, 0.012f, 1f);
        var wc = cGo.AddComponent<Canvas>(); wc.renderMode = RenderMode.WorldSpace; wc.sortingOrder = 35;
        cGo.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 70);

        var borderGo = new GameObject("Border", typeof(RectTransform)); borderGo.transform.SetParent(cGo.transform, false);
        var borderImg = borderGo.AddComponent<Image>(); borderImg.color = col; borderImg.raycastTarget = false;
        var brt = borderGo.GetComponent<RectTransform>();
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = new Vector2(-4, -4); brt.offsetMax = new Vector2(4, 4);

        var fillGo = new GameObject("Fill", typeof(RectTransform)); fillGo.transform.SetParent(cGo.transform, false);
        var fillImg = fillGo.AddComponent<Image>(); fillImg.color = new Color(0.06f, 0.08f, 0.14f, 0.92f); fillImg.raycastTarget = false;
        var frt = fillGo.GetComponent<RectTransform>(); frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = frt.offsetMax = Vector2.zero;

        var tGo = new GameObject("Label", typeof(RectTransform)); tGo.transform.SetParent(cGo.transform, false);
        var tmp = tGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label; tmp.fontSize = 38; tmp.color = col; tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center; tmp.raycastTarget = false;
        var tRT = tGo.GetComponent<RectTransform>(); tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one; tRT.offsetMin = new Vector2(8, 4); tRT.offsetMax = new Vector2(-8, -4);

        // Completion key uses the INTERIOR scene name so each house tracks
        // its own progress independently, even if it shares a minigame type
        // with another house — see InteriorDialogueManager.MarkComplete().
        var cta = ctaRoot.AddComponent<MapDoorCTA>();
        cta.targetScene = interiorScene;
        cta.playerTransform = player;
        cta.doorPosition = doorGo.transform;
        cta.label = tmp;
        cta.completionKey = "completed_" + interiorScene;
        cta.activeColor = col;
        cta.completedColor = new Color(col.r * 0.35f, col.g * 0.35f, col.b * 0.35f, 0.40f);
        cta.bobAmount = 0.16f;
        cta.bobSpeed = 2.8f;
        cta.clickRadius = 1.4f;
        cta.completedLockPopup = lockedPopup;
        cta.completedLockMessage = "This door is locked — please explore another building!";

        var borderCTA = ctaRoot.AddComponent<CTABorderFader>();
        borderCTA.borderImage = borderImg;
        borderCTA.completionKey = "completed_" + interiorScene;
        borderCTA.activeColor = col;
        borderCTA.completedColor = new Color(col.r * 0.35f, col.g * 0.35f, col.b * 0.35f, 0.40f);
    }

    static GameObject BuildLockedPopup(RectTransform canvasRT)
    {
        var ov = UImg(canvasRT, "LockedPopup", new Color(0, 0, 0, 0)); Stretch(ov.rectTransform); ov.raycastTarget = false;
        var card = UImg(ov.rectTransform, "Card", new Color(0.08f, 0.10f, 0.20f, 0.93f)); var crt = card.rectTransform;
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.62f); crt.pivot = new Vector2(0.5f, 0.5f); crt.sizeDelta = new Vector2(740, 150);
        var brd = UImg(crt, "Border", Hex("#A29BFE")); var brt = brd.rectTransform;
        brt.anchorMin = new Vector2(0, 1); brt.anchorMax = new Vector2(1, 1); brt.pivot = new Vector2(0.5f, 1); brt.sizeDelta = new Vector2(0, 6);
        var txt = UTxt(crt, "Msg", "You haven't unlocked this area yet!\nThis is the final island.", 30, Color.white, TextAlignmentOptions.Center);
        txt.textWrappingMode = TextWrappingModes.Normal; Stretch(txt.rectTransform);
        var controller = ov.gameObject.AddComponent<LockedPopupController>();
        controller.messageText = txt;
        ov.gameObject.SetActive(false); return ov.gameObject;
    }

    static (GameObject panel, Button adv, TMP_Text name, TMP_Text body, TMP_Text hint, GameObject cp, Button ab, TMP_Text at, Button db, TMP_Text dt) BuildDialoguePanel(RectTransform rt)
    {
        var advGo = new GameObject("AdvanceBtn", typeof(RectTransform)); advGo.transform.SetParent(rt, false); Stretch(advGo.GetComponent<RectTransform>()); var advImg = advGo.AddComponent<Image>(); advImg.color = new Color(0, 0, 0, 0); var advBtn = advGo.AddComponent<Button>(); advBtn.targetGraphic = advImg;
        var panel = new GameObject("DialoguePanel", typeof(RectTransform)); panel.transform.SetParent(rt, false); var prt = panel.GetComponent<RectTransform>(); prt.anchorMin = new Vector2(0, 0); prt.anchorMax = new Vector2(1, 0); prt.pivot = new Vector2(0.5f, 0); prt.sizeDelta = new Vector2(0, 220); prt.anchoredPosition = Vector2.zero; panel.AddComponent<Image>().color = new Color(0.08f, 0.15f, 0.28f, 0.95f);
        var nameBar = UImg(prt, "NameBar", Hex("#1A3A6B")); var nbrrt = nameBar.rectTransform; nbrrt.anchorMin = new Vector2(0, 1); nbrrt.anchorMax = new Vector2(0.42f, 1); nbrrt.pivot = new Vector2(0, 1); nbrrt.sizeDelta = new Vector2(0, 44);
        var nameText = UTxt(nbrrt, "Name", "Speaker", 30, NameCol, TextAlignmentOptions.MidlineLeft, FontStyles.Bold); var ntrt = nameText.rectTransform; ntrt.anchorMin = Vector2.zero; ntrt.anchorMax = Vector2.one; ntrt.offsetMin = new Vector2(18, 0); ntrt.offsetMax = Vector2.zero;
        var bodyText = UTxt(prt, "Body", "...", 26, Color.white, TextAlignmentOptions.TopLeft); bodyText.textWrappingMode = TextWrappingModes.Normal; var brrt = bodyText.rectTransform; brrt.anchorMin = Vector2.zero; brrt.anchorMax = Vector2.one; brrt.offsetMin = new Vector2(24, 48); brrt.offsetMax = new Vector2(-24, -50);
        var hint = UTxt(prt, "Hint", "Click to continue…", 20, new Color(0.7f, 0.7f, 0.9f), TextAlignmentOptions.MidlineRight); var hrrt = hint.rectTransform; hrrt.anchorMin = new Vector2(0, 0); hrrt.anchorMax = new Vector2(1, 0); hrrt.pivot = new Vector2(0.5f, 0); hrrt.sizeDelta = new Vector2(0, 36); hrrt.anchoredPosition = new Vector2(0, 8);
        var cpGo = new GameObject("ChoicePanel", typeof(RectTransform)); cpGo.transform.SetParent(prt, false); var cprt = cpGo.GetComponent<RectTransform>(); cprt.anchorMin = new Vector2(0.5f, 0); cprt.anchorMax = new Vector2(0.5f, 0); cprt.pivot = new Vector2(0.5f, 0); cprt.sizeDelta = new Vector2(800, 72); cprt.anchoredPosition = new Vector2(0, 8);
        var acceptGo = UBtn(cprt, "AcceptBtn", "Let's go!", 26, AcceptCol, Color.white); var declineGo = UBtn(cprt, "DeclineBtn", "Maybe later", 26, DeclineCol, Color.white);
        var a = acceptGo.GetComponent<RectTransform>(); a.anchorMin = new Vector2(0, 0); a.anchorMax = new Vector2(0.48f, 1); a.offsetMin = a.offsetMax = Vector2.zero;
        var d = declineGo.GetComponent<RectTransform>(); d.anchorMin = new Vector2(0.52f, 0); d.anchorMax = new Vector2(1, 1); d.offsetMin = d.offsetMax = Vector2.zero;
        panel.SetActive(false);
        return (panel, advBtn, nameText, bodyText, hint, cpGo, acceptGo.GetComponent<Button>(), acceptGo.transform.Find("Label").GetComponent<TMP_Text>(), declineGo.GetComponent<Button>(), declineGo.transform.Find("Label").GetComponent<TMP_Text>());
    }

    static void BuildMapHint(RectTransform canvasRT) { var go = new GameObject("MapHint", typeof(RectTransform)); go.transform.SetParent(canvasRT, false); var r = go.GetComponent<RectTransform>(); r.anchorMin = new Vector2(1, 1); r.anchorMax = new Vector2(1, 1); r.pivot = new Vector2(1, 1); r.sizeDelta = new Vector2(230, 44); r.anchoredPosition = new Vector2(-24, -24); go.AddComponent<Image>().color = new Color(0, 0, 0, 0.45f); Stretch(UTxt(go.transform, "T", "[ M ]  World Map", 22, new Color(0.9f, 0.9f, 1f), TextAlignmentOptions.Center).rectTransform); }

    static GameObject BuildWorldMapPanel(RectTransform canvasRT, WorldMapManager manager)
    {
        var ov = UImg(canvasRT, "WorldMapOverlay", new Color(0, 0, 0, 0.88f)); Stretch(ov.rectTransform); ov.raycastTarget = true;
        var card = UImg(ov.rectTransform, "MapCard", new Color(0, 0, 0, 0)); var crt = card.rectTransform; crt.anchorMin = new Vector2(0.05f, 0.06f); crt.anchorMax = new Vector2(0.95f, 0.94f); crt.offsetMin = crt.offsetMax = Vector2.zero;
        var mi = UImg(crt, "MapImage", Color.white); mi.type = Image.Type.Simple; mi.preserveAspect = true; mi.raycastTarget = false; Stretch(mi.rectTransform);
        Sprite[] mapVersions = new Sprite[5]; for (int v = 1; v <= 5; v++) mapVersions[v - 1] = FindSprite("map_v" + v);
        bool anyMapFound = false; foreach (var s in mapVersions) if (s != null) anyMapFound = true;
        if (anyMapFound) { var switcher = mi.gameObject.AddComponent<WorldMapImageSwitcher>(); switcher.targetImage = mi; switcher.mapVersions = mapVersions; switcher.Refresh(); } else { card.color = MapBg; }
        var tb = UImg(ov.rectTransform, "TitleBar", new Color(0.05f, 0.08f, 0.15f, 0.92f)); var tbr = tb.rectTransform; tbr.anchorMin = new Vector2(0, 1); tbr.anchorMax = new Vector2(1, 1); tbr.pivot = new Vector2(0.5f, 1); tbr.sizeDelta = new Vector2(0, 64); tbr.anchoredPosition = Vector2.zero;
        Stretch(UTxt(tbr, "Title", "PHISHERMAN  WORLD MAP", 36, Color.white, TextAlignmentOptions.Center, FontStyles.Bold).rectTransform);
        var cb = UBtn(ov.rectTransform, "CloseBtn", "X  Close Map  ( M )", 26, new Color(0.18f, 0.28f, 0.45f, 0.95f), Color.white); var cbrt = cb.GetComponent<RectTransform>(); cbrt.anchorMin = new Vector2(0.5f, 0); cbrt.anchorMax = new Vector2(0.5f, 0); cbrt.pivot = new Vector2(0.5f, 0); cbrt.sizeDelta = new Vector2(360, 56); cbrt.anchoredPosition = new Vector2(0, 12);
        UnityEventTools.AddPersistentListener(cb.GetComponent<Button>().onClick, manager.CloseMap);
        ov.gameObject.SetActive(false); return ov.gameObject;
    }

    static Sprite FindSprite(string name) { foreach (var g in AssetDatabase.FindAssets(name + " t:Texture2D")) { var p = AssetDatabase.GUIDToAssetPath(g); if (System.IO.Path.GetFileNameWithoutExtension(p).ToLower() == name.ToLower()) { var s = AssetDatabase.LoadAssetAtPath<Sprite>(p); if (s != null) return s; var reps = AssetDatabase.LoadAllAssetRepresentationsAtPath(p); foreach (var r in reps) { if (r is Sprite sp) return sp; } } } return null; }
    static Image UImg(Transform p, string n, Color c) { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); var img = go.AddComponent<Image>(); img.color = c; return img; }
    static TMP_Text UTxt(Transform p, string n, string text, int size, Color col, TextAlignmentOptions align, FontStyles style = FontStyles.Normal) { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); var t = go.AddComponent<TextMeshProUGUI>(); t.text = text; t.fontSize = size; t.color = col; t.alignment = align; t.fontStyle = style; t.raycastTarget = false; return t; }
    static GameObject UBtn(Transform p, string n, string label, int size, Color bg, Color tc) { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); var img = go.AddComponent<Image>(); img.color = bg; var btn = go.AddComponent<Button>(); btn.targetGraphic = img; var t = UTxt(go.transform, "Label", label, size, tc, TextAlignmentOptions.Center, FontStyles.Bold); Stretch(t.rectTransform); return go; }
    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
    static Color Hex(string h) => ColorUtility.TryParseHtmlString(h, out var c) ? c : Color.magenta;
    static void AddToBuild(string path) { var scenes = EditorBuildSettings.scenes.ToList(); if (!scenes.Any(s => s.path == path)) { scenes.Add(new EditorBuildSettingsScene(path, true)); EditorBuildSettings.scenes = scenes.ToArray(); } }
}