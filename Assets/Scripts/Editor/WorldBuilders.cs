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
//  WorldMapBuilder  (World 1 — UNCHANGED)
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
        // Use disk-based save (same as Worlds 2-5) so the polygon is preserved
        // regardless of which scene happens to be open in the editor right now.
        var savedVerts = SharedWorldBuilderUtils.SaveWalkableZone(ScenePath, "WorldMapBuilder", out var savedZonePos);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        if (savedVerts != null) { var zGo = new GameObject("WalkableZone"); zGo.transform.position = savedZonePos; var pc2 = zGo.AddComponent<PolygonCollider2D>(); pc2.isTrigger = true; pc2.SetPath(0, savedVerts); Debug.Log($"[WorldMapBuilder] Restored WalkableZone: {savedVerts.Length} verts."); }
        else { var zGo = new GameObject("WalkableZone"); var pc2 = zGo.AddComponent<PolygonCollider2D>(); pc2.isTrigger = true; pc2.SetPath(0, new Vector2[] { new Vector2(-7.5f, -4.5f), new Vector2(-7.5f, 4.5f), new Vector2(7.5f, 4.5f), new Vector2(7.5f, -4.5f) }); Debug.LogWarning("[WorldMapBuilder] No WalkableZone — placeholder created."); }
        Sprite circle = GetCircle();
        Sprite phisherman = FindSprite("phisherman"); Sprite phishermanWalk = FindSprite("phisherman_walking");
        LogFound("phisherman", phisherman); LogFound("phisherman_walking", phishermanWalk);
        var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera"; var cam = camGo.AddComponent<Camera>(); camGo.AddComponent<AudioListener>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Hex("#4FA8D9"); cam.orthographic = true; cam.orthographicSize = OrthoSize; camGo.transform.position = new Vector3(0, 0, -10);
        var es = new GameObject("EventSystem"); es.AddComponent<EventSystem>(); es.AddComponent<StandaloneInputModule>();
        Sprite bgArt = FindSprite("world_stage_1_background");
        if (bgArt != null) { var bgGo = new GameObject("WorldBackground"); var bgSR = bgGo.AddComponent<SpriteRenderer>(); bgSR.sprite = bgArt; bgSR.color = Color.white; bgSR.sortingOrder = -50; float camH = OrthoSize * 2f, camW = camH * AspectW; bgGo.transform.localScale = new Vector3(camW / bgArt.bounds.size.x, camH / bgArt.bounds.size.y, 1f); }
        else Debug.LogWarning("[WorldMapBuilder] 'world_stage_1_background' not found.");
        var spawnGo = new GameObject("PlayerSpawn"); spawnGo.transform.position = new Vector3(0f, -1.5f, 0f);
        var playerGo = new GameObject("Phisherman"); playerGo.tag = "Player";
        playerGo.transform.position = new Vector3(0f, -1.5f, 0f); playerGo.transform.localScale = new Vector3(PlayerScale, PlayerScale, 1f);
        var playerSR = playerGo.AddComponent<SpriteRenderer>(); playerSR.sprite = phisherman; playerSR.color = Color.white; playerSR.sortingOrder = 10;
        var anim = playerGo.AddComponent<MapWalkAnimator>(); anim.idleSprite = phisherman; anim.walkSprite = phishermanWalk ?? phisherman; anim.fps = 2f;
        playerGo.AddComponent<MapWasdZoneClamp>();
        var nav = playerGo.AddComponent<MapNavAgent>(); nav.gridCols = 80; nav.gridRows = 45; nav.moveSpeed = SharedSpeed;
        var mover = playerGo.AddComponent<PlayerController>(); mover.moveSpeed = SharedSpeed;
        var mgrGo = new GameObject("GameManager"); var manager = mgrGo.AddComponent<WorldMapManager>(); manager.playerSpeed = SharedSpeed;
        var hud1 = mgrGo.AddComponent<LevelSystemHUD>(); AssignStickerSprites(hud1);
        Transform playerT = playerGo.transform;
        AddDoorCTA("Door_LeftHouse", playerT, -4.0f, 0.4f, "ApartmentInterior", "EmailSwiper", "Grandma Rose", Hex("#FF9F1C"));
        AddDoorCTA("Door_CentreHouse", playerT, 0.2f, 1.2f, "PizzaInterior", "EmailSwiper", "Uncle Tony", Hex("#FF6B6B"));
        AddDoorCTA("Door_RightHouse", playerT, 4.3f, 0.2f, "OfficeInterior", "TowerDefense", "Mrs. Patel", Hex("#4ECDC4"));
        var canvasGo = new GameObject("Canvas"); var canvas = canvasGo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>(); var canvasRT = canvasGo.GetComponent<RectTransform>();
        var (dialogPanel, advBtn, nameText, bodyText, hintText, choicePanel, acceptBtn, acceptTxt, declineBtn, declineTxt) = BuildDialoguePanel(canvasRT);
        BuildMapHint(canvasRT, manager);
        var mapPanel = BuildWorldMapPanel(canvasRT, manager);
        var lockedPopup = BuildLockedPopup(canvasRT);
        AddWorldCTA("WorldCTA_World2", playerT, "WorldMap2", "World 2", Hex("#2BB3A3"), lockedPopup, requiresWorldComplete: 1, isLeft: true, canvasRT: canvasRT);
        AddWorldCTA("WorldCTA_Locked", playerT, "", "World 3", Hex("#E74C3C"), lockedPopup, requiresWorldComplete: 2, isLeft: false, canvasRT: canvasRT);
        foreach (var doorName in new[] { "Door_LeftHouse", "Door_CentreHouse", "Door_RightHouse" }) { var doorCta = GameObject.Find(doorName + "_CTA")?.GetComponent<MapDoorCTA>(); if (doorCta != null) doorCta.completedLockPopup = lockedPopup; }
        manager.playerTransform = playerGo.transform; manager.playerRenderer = playerSR;
        manager.npcTransforms = new Transform[0]; manager.exclamationMarks = new GameObject[0];
        manager.dialoguePanel = dialogPanel; manager.advanceButton = advBtn;
        manager.speakerNameText = nameText; manager.dialogueText = bodyText; manager.continueHint = hintText;
        manager.choicePanel = choicePanel; manager.acceptButton = acceptBtn; manager.acceptButtonText = acceptTxt;
        manager.declineButton = declineBtn; manager.declineButtonText = declineTxt; manager.worldMapPanel = mapPanel;
        manager.boundsX = new Vector2(-7.8f, 7.8f); manager.boundsY = new Vector2(-4.0f, 4.3f);
        UnityEventTools.AddPersistentListener(advBtn.onClick, manager.AdvanceDialogue);
        UnityEventTools.AddPersistentListener(acceptBtn.onClick, manager.OnAcceptHelp);
        UnityEventTools.AddPersistentListener(declineBtn.onClick, manager.OnDeclineHelp);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuild(ScenePath); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log($"[WorldMapBuilder] Built -> {ScenePath}");
    }

    static void AddDoorCTA(string name, Transform player, float cx, float cy, string interiorScene, string minigameScene, string label, Color col, GameObject lockedPopup = null)
    {
        var doorGo = new GameObject(name); doorGo.transform.position = new Vector3(cx, cy, 0f);
        // targetScene is empty — MapDoorCTA.Update() owns proximity entry for all worlds.
        // Keeping a non-empty targetScene here would cause a double scene load.
        var dt = doorGo.AddComponent<MapDoorTrigger>(); dt.player = player; dt.targetScene = ""; dt.triggerRadius = 0.55f; dt.promptProximity = 999f; dt.prompt = null;
        var ctaRoot = new GameObject(name + "_CTA"); ctaRoot.transform.position = new Vector3(cx, cy + 0.75f, -0.5f);
        var cGo = new GameObject("C"); cGo.transform.SetParent(ctaRoot.transform, false); cGo.transform.localScale = new Vector3(0.012f, 0.012f, 1f);
        var wc = cGo.AddComponent<Canvas>(); wc.renderMode = RenderMode.WorldSpace; wc.sortingOrder = 35; cGo.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 70);
        var borderGo = new GameObject("Border", typeof(RectTransform)); borderGo.transform.SetParent(cGo.transform, false);
        var borderImg = borderGo.AddComponent<Image>(); borderImg.color = col; borderImg.raycastTarget = false;
        var brt = borderGo.GetComponent<RectTransform>(); brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = new Vector2(-4, -4); brt.offsetMax = new Vector2(4, 4);
        var fillGo = new GameObject("Fill", typeof(RectTransform)); fillGo.transform.SetParent(cGo.transform, false);
        var fillImg = fillGo.AddComponent<Image>(); fillImg.color = new Color(0.06f, 0.08f, 0.14f, 0.92f); fillImg.raycastTarget = false;
        var frt = fillGo.GetComponent<RectTransform>(); frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = frt.offsetMax = Vector2.zero;
        var tGo = new GameObject("Label", typeof(RectTransform)); tGo.transform.SetParent(cGo.transform, false);
        var tmp = tGo.AddComponent<TextMeshProUGUI>(); tmp.text = label; tmp.fontSize = 38; tmp.color = col; tmp.fontStyle = FontStyles.Bold; tmp.alignment = TextAlignmentOptions.Center; tmp.raycastTarget = false;
        var tRT = tGo.GetComponent<RectTransform>(); tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one; tRT.offsetMin = new Vector2(8, 4); tRT.offsetMax = new Vector2(-8, -4);
        var cta = ctaRoot.AddComponent<MapDoorCTA>(); cta.targetScene = interiorScene; cta.playerTransform = player; cta.doorPosition = doorGo.transform; cta.label = tmp;
        cta.completionKey = "completed_" + interiorScene; cta.activeColor = col; cta.completedColor = new Color(col.r * 0.35f, col.g * 0.35f, col.b * 0.35f, 0.40f);
        cta.bobAmount = 0.16f; cta.bobSpeed = 2.8f; cta.clickRadius = 1.4f; cta.proximityRadius = 0.55f; cta.completedLockPopup = lockedPopup;
        cta.completedLockMessage = "Already helped here! Try visiting another building.";
        var borderCTA = ctaRoot.AddComponent<CTABorderFader>(); borderCTA.borderImage = borderImg; borderCTA.completionKey = "completed_" + interiorScene; borderCTA.activeColor = col; borderCTA.completedColor = new Color(col.r * 0.35f, col.g * 0.35f, col.b * 0.35f, 0.40f);
    }

    static void BuildMapHint(RectTransform canvasRT, WorldMapManager manager)
    {
        var go = new GameObject("MapHint", typeof(RectTransform));
        go.transform.SetParent(canvasRT, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1, 1); rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(1, 1); rt.sizeDelta = new Vector2(230, 44);
        rt.anchoredPosition = new Vector2(-24, -24);
        var img = go.AddComponent<Image>(); img.color = new Color(0, 0, 0, 0.45f);
        var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        var cols = btn.colors;
        cols.highlightedColor = new Color(0.2f, 0.2f, 0.2f, 0.65f);
        cols.pressedColor = new Color(0.1f, 0.1f, 0.1f, 0.80f);
        btn.colors = cols;
        UnityEventTools.AddPersistentListener(btn.onClick, manager.ToggleMap);
        var txt = UTxt(go.transform, "T", "[ M ]  World Map", 22, new Color(0.9f, 0.9f, 1f), TextAlignmentOptions.Center);
        Stretch(txt.rectTransform);
        go.transform.SetAsLastSibling();
    }

    static void AddWorldCTA(string name, Transform player, string targetScene, string label,
        Color col, GameObject lockedPopup, int requiresWorldComplete, bool isLeft, RectTransform canvasRT)
    {
        var triggerGo = new GameObject(name + "_Trigger");
        triggerGo.transform.position = new Vector3(isLeft ? -7.0f : 7.0f, -3.8f, 0f);
        var wCTA = triggerGo.AddComponent<WorldTransitionCTA>();
        wCTA.player = player;
        wCTA.targetScene = targetScene;
        wCTA.lockedPopup = lockedPopup;
        wCTA.requiresWorldComplete = requiresWorldComplete;
        wCTA.triggerRadius = 1.2f;
        wCTA.lockedMessage = "Finish exploring this neighbourhood first!";
        wCTA.activeColor = col;
        wCTA.completedColor = new Color(col.r * 0.35f, col.g * 0.35f, col.b * 0.35f, 0.40f);

        var root = new GameObject(name, typeof(RectTransform));
        root.transform.SetParent(canvasRT, false);
        var rootRT = root.GetComponent<RectTransform>();
        rootRT.anchorMin = isLeft ? Vector2.zero : new Vector2(1, 0);
        rootRT.anchorMax = isLeft ? Vector2.zero : new Vector2(1, 0);
        rootRT.pivot = isLeft ? Vector2.zero : new Vector2(1, 0);
        rootRT.sizeDelta = new Vector2(220, 60);
        rootRT.anchoredPosition = new Vector2(isLeft ? 24 : -24, 24);

        var cGo = new GameObject("C", typeof(RectTransform));
        cGo.transform.SetParent(root.transform, false);
        var cRT = cGo.GetComponent<RectTransform>();
        cRT.anchorMin = Vector2.zero; cRT.anchorMax = Vector2.one;
        cRT.offsetMin = cRT.offsetMax = Vector2.zero;

        var borderGo = new GameObject("Border", typeof(RectTransform));
        borderGo.transform.SetParent(cGo.transform, false);
        var borderImg = borderGo.AddComponent<Image>(); borderImg.color = col; borderImg.raycastTarget = false;
        var brt = borderGo.GetComponent<RectTransform>();
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
        brt.offsetMin = new Vector2(-4, -4); brt.offsetMax = new Vector2(4, 4);

        var fillGo = new GameObject("Fill", typeof(RectTransform));
        fillGo.transform.SetParent(cGo.transform, false);
        var fillImg = fillGo.AddComponent<Image>(); fillImg.color = new Color(0.06f, 0.08f, 0.14f, 0.92f); fillImg.raycastTarget = false;
        var frt = fillGo.GetComponent<RectTransform>();
        frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = frt.offsetMax = Vector2.zero;

        var tGo = new GameObject("Label", typeof(RectTransform));
        tGo.transform.SetParent(cGo.transform, false);
        var tmp = tGo.AddComponent<TextMeshProUGUI>();
        tmp.text = isLeft ? $"< {label}" : $"{label} >";
        tmp.fontSize = 28; tmp.color = col;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center; tmp.raycastTarget = false;
        var tRT = tGo.GetComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
        tRT.offsetMin = new Vector2(8, 4); tRT.offsetMax = new Vector2(-8, -4);

        var bg = root.AddComponent<Image>(); bg.color = new Color(0, 0, 0, 0);
        var btn = root.AddComponent<Button>(); btn.targetGraphic = bg;
        btn.onClick.AddListener(() => wCTA.OnBadgeClick());

        wCTA.badgeLabel = tmp;
        wCTA.badgeBorderImage = borderImg;

        var fader = root.AddComponent<CTABorderFader>();
        fader.borderImage = borderImg;
        fader.completionKey = "";
        fader.activeColor = col;
        fader.completedColor = new Color(col.r * 0.35f, col.g * 0.35f, col.b * 0.35f, 0.40f);
    }

    static (GameObject panel, Button adv, TMP_Text name, TMP_Text body, TMP_Text hint, GameObject choicePanel, Button acceptBtn, TMP_Text acceptTxt, Button declineBtn, TMP_Text declineTxt) BuildDialoguePanel(RectTransform canvasRT)
    {
        var advGo = new GameObject("AdvanceBtn", typeof(RectTransform)); advGo.transform.SetParent(canvasRT, false); Stretch(advGo.GetComponent<RectTransform>());
        var advImg = advGo.AddComponent<Image>(); advImg.color = new Color(0, 0, 0, 0); var advBtn = advGo.AddComponent<Button>(); advBtn.targetGraphic = advImg;
        advGo.SetActive(false); // Start inactive — never block map/CTA clicks at scene open.
        var panel = new GameObject("DialoguePanel", typeof(RectTransform)); panel.transform.SetParent(canvasRT, false);
        var prt = panel.GetComponent<RectTransform>(); prt.anchorMin = new Vector2(0, 0); prt.anchorMax = new Vector2(1, 0); prt.pivot = new Vector2(0.5f, 0); prt.sizeDelta = new Vector2(0, 220); prt.anchoredPosition = Vector2.zero;
        panel.AddComponent<Image>().color = new Color(0.08f, 0.15f, 0.28f, 0.95f);
        var nameBar = UImg(prt, "NameBar", Hex("#1A3A6B")); var nbrrt = nameBar.rectTransform; nbrrt.anchorMin = new Vector2(0, 1); nbrrt.anchorMax = new Vector2(0.42f, 1); nbrrt.pivot = new Vector2(0, 1); nbrrt.sizeDelta = new Vector2(0, 44);
        var nameText = UTxt(nbrrt, "Name", "Speaker", 30, NameCol, TextAlignmentOptions.MidlineLeft, FontStyles.Bold); var ntrt = nameText.rectTransform; ntrt.anchorMin = Vector2.zero; ntrt.anchorMax = Vector2.one; ntrt.offsetMin = new Vector2(18, 0); ntrt.offsetMax = Vector2.zero;
        var bodyText = UTxt(prt, "Body", "...", 26, Color.white, TextAlignmentOptions.TopLeft); bodyText.textWrappingMode = TextWrappingModes.Normal; var brrt = bodyText.rectTransform; brrt.anchorMin = Vector2.zero; brrt.anchorMax = Vector2.one; brrt.offsetMin = new Vector2(24, 48); brrt.offsetMax = new Vector2(-24, -50);
        var hint = UTxt(prt, "Hint", "Click to continue...", 20, new Color(0.7f, 0.7f, 0.9f), TextAlignmentOptions.MidlineRight); var hrrt = hint.rectTransform; hrrt.anchorMin = new Vector2(0, 0); hrrt.anchorMax = new Vector2(1, 0); hrrt.pivot = new Vector2(0.5f, 0); hrrt.sizeDelta = new Vector2(0, 36); hrrt.anchoredPosition = new Vector2(0, 8);
        var cpGo = new GameObject("ChoicePanel", typeof(RectTransform)); cpGo.transform.SetParent(prt, false); var cprt = cpGo.GetComponent<RectTransform>(); cprt.anchorMin = new Vector2(0.5f, 0); cprt.anchorMax = new Vector2(0.5f, 0); cprt.pivot = new Vector2(0.5f, 0); cprt.sizeDelta = new Vector2(800, 72); cprt.anchoredPosition = new Vector2(0, 8);
        var acceptGo = UBtn(cprt, "AcceptBtn", "Let's go!", 26, AcceptCol, Color.white); var declineGo = UBtn(cprt, "DeclineBtn", "Maybe later", 26, DeclineCol, Color.white);
        var art = acceptGo.GetComponent<RectTransform>(); art.anchorMin = new Vector2(0, 0); art.anchorMax = new Vector2(0.48f, 1); art.offsetMin = art.offsetMax = Vector2.zero;
        var drt = declineGo.GetComponent<RectTransform>(); drt.anchorMin = new Vector2(0.52f, 0); drt.anchorMax = new Vector2(1, 1); drt.offsetMin = drt.offsetMax = Vector2.zero;
        panel.SetActive(false);
        return (panel, advBtn, nameText, bodyText, hint, cpGo, acceptGo.GetComponent<Button>(), acceptGo.transform.Find("Label").GetComponent<TMP_Text>(), declineGo.GetComponent<Button>(), declineGo.transform.Find("Label").GetComponent<TMP_Text>());
    }

    static GameObject BuildWorldMapPanel(RectTransform canvasRT, WorldMapManager manager)
    {
        var ov = UImg(canvasRT, "WorldMapOverlay", new Color(0, 0, 0, 0.88f)); Stretch(ov.rectTransform); ov.raycastTarget = true;
        var card = UImg(ov.rectTransform, "MapCard", new Color(0, 0, 0, 0)); var crt = card.rectTransform; crt.anchorMin = new Vector2(0.05f, 0.06f); crt.anchorMax = new Vector2(0.95f, 0.94f); crt.offsetMin = crt.offsetMax = Vector2.zero;
        var mi = UImg(crt, "MapImage", Color.white); mi.type = Image.Type.Simple; mi.preserveAspect = true; mi.raycastTarget = false; Stretch(mi.rectTransform);
        Sprite[] mapVersions = new Sprite[5]; for (int v = 1; v <= 5; v++) mapVersions[v - 1] = FindSprite("map_v" + v);
        bool anyMapFound = false; foreach (var s in mapVersions) if (s != null) anyMapFound = true;
        if (anyMapFound) { var switcher = mi.gameObject.AddComponent<WorldMapImageSwitcher>(); switcher.targetImage = mi; switcher.mapVersions = mapVersions; switcher.Refresh(); } else { card.color = MapBg; }
        var titleBg = UImg(ov.rectTransform, "TitleBar", new Color(0.05f, 0.08f, 0.15f, 0.92f)); var tbr = titleBg.rectTransform; tbr.anchorMin = new Vector2(0, 1); tbr.anchorMax = new Vector2(1, 1); tbr.pivot = new Vector2(0.5f, 1); tbr.sizeDelta = new Vector2(0, 64); tbr.anchoredPosition = Vector2.zero;
        Stretch(UTxt(tbr, "Title", "PHISHERMAN  WORLD MAP", 36, Color.white, TextAlignmentOptions.Center, FontStyles.Bold).rectTransform);
        var closeBtn = UBtn(ov.rectTransform, "CloseBtn", "X  Close Map  ( M )", 26, new Color(0.18f, 0.28f, 0.45f, 0.95f), Color.white); var cbrt = closeBtn.GetComponent<RectTransform>(); cbrt.anchorMin = new Vector2(0.5f, 0); cbrt.anchorMax = new Vector2(0.5f, 0); cbrt.pivot = new Vector2(0.5f, 0); cbrt.sizeDelta = new Vector2(360, 56); cbrt.anchoredPosition = new Vector2(0, 12);
        UnityEventTools.AddPersistentListener(closeBtn.GetComponent<Button>().onClick, manager.CloseMap);
        ov.gameObject.SetActive(false); return ov.gameObject;
    }

    static GameObject BuildLockedPopup(RectTransform canvasRT)
    {
        var ov = UImg(canvasRT, "LockedPopup", new Color(0, 0, 0, 0)); Stretch(ov.rectTransform); ov.raycastTarget = false;
        var card = UImg(ov.rectTransform, "Card", new Color(0.08f, 0.10f, 0.20f, 0.93f)); var crt = card.rectTransform; crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.62f); crt.pivot = new Vector2(0.5f, 0.5f); crt.sizeDelta = new Vector2(740, 150);
        var brd = UImg(crt, "Border", Hex("#E74C3C")); var brt = brd.rectTransform; brt.anchorMin = new Vector2(0, 1); brt.anchorMax = new Vector2(1, 1); brt.pivot = new Vector2(0.5f, 1); brt.sizeDelta = new Vector2(0, 6);
        var txt = UTxt(crt, "Msg", "You haven't unlocked this area yet!\nExplore the neighbourhood first.", 30, Color.white, TextAlignmentOptions.Center); txt.textWrappingMode = TextWrappingModes.Normal; Stretch(txt.rectTransform);
        var controller = ov.gameObject.AddComponent<LockedPopupController>(); controller.messageText = txt;
        ov.gameObject.SetActive(false); return ov.gameObject;
    }

    static Sprite FindSprite(string name) { foreach (var g in AssetDatabase.FindAssets(name + " t:Texture2D")) { var p = AssetDatabase.GUIDToAssetPath(g); if (System.IO.Path.GetFileNameWithoutExtension(p).ToLower() == name.ToLower()) { var s = AssetDatabase.LoadAssetAtPath<Sprite>(p); if (s != null) return s; var reps = AssetDatabase.LoadAllAssetRepresentationsAtPath(p); foreach (var r in reps) { if (r is Sprite sp) return sp; } } } return null; }
    static void LogFound(string n, Sprite s) => Debug.Log($"[WorldMapBuilder] {n}: " + (s != null ? "OK" : "MISSING"));
    static Sprite GetCircle() { try { return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"); } catch { return null; } }
    static Image UImg(Transform p, string n, Color c) { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); var img = go.AddComponent<Image>(); img.color = c; return img; }
    static TMP_Text UTxt(Transform p, string n, string text, int size, Color col, TextAlignmentOptions align, FontStyles style = FontStyles.Normal) { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); var t = go.AddComponent<TextMeshProUGUI>(); t.text = text; t.fontSize = size; t.color = col; t.alignment = align; t.fontStyle = style; t.raycastTarget = false; return t; }
    static GameObject UBtn(Transform p, string n, string label, int size, Color bg, Color tc) { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); var img = go.AddComponent<Image>(); img.color = bg; var btn = go.AddComponent<Button>(); btn.targetGraphic = img; var t = UTxt(go.transform, "Label", label, size, tc, TextAlignmentOptions.Center, FontStyles.Bold); Stretch(t.rectTransform); return go; }
    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
    static Color Hex(string h) => ColorUtility.TryParseHtmlString(h, out var c) ? c : Color.magenta;
    static void AddToBuild(string path) { var scenes = EditorBuildSettings.scenes.ToList(); if (!scenes.Any(s => s.path == path)) { scenes.Add(new EditorBuildSettingsScene(path, true)); EditorBuildSettings.scenes = scenes.ToArray(); } }
    static void AssignStickerSprites(LevelSystemHUD hud) { if (hud == null) return; var catalog = PlayerProgress.FishCatalog; var sprites = new Sprite[catalog.Length]; for (int i = 0; i < catalog.Length; i++) sprites[i] = FindSprite(catalog[i].spriteName); hud.stickerSprites = sprites; int found = 0; foreach (var s in sprites) if (s != null) found++; Debug.Log($"[WorldMapBuilder] AssignStickerSprites: {found}/{catalog.Length} fish sprites assigned."); }
}

// ============================================================
//  SharedWorldBuilderUtils
//  Common save/restore + build helpers used by Worlds 2-5
// ============================================================
internal static class SharedWorldBuilderUtils
{
    internal static Vector2[] SaveWalkableZone(string scenePath, string builderTag, out Vector3 pos)
    {
        pos = Vector3.zero;
        if (!File.Exists(scenePath)) return null;
        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        var zone = GameObject.Find("WalkableZone"); if (zone == null) return null;
        var pc = zone.GetComponent<PolygonCollider2D>(); if (pc == null) return null;
        pos = zone.transform.position; var verts = pc.points.ToArray();
        Debug.Log($"[{builderTag}] Saved WalkableZone: {verts.Length} verts."); return verts;
    }

    internal static void RestoreWalkableZone(string builderTag, Vector2[] savedVerts, Vector3 savedPos)
    {
        if (savedVerts != null) { var zGo = new GameObject("WalkableZone"); zGo.transform.position = savedPos; var pc2 = zGo.AddComponent<PolygonCollider2D>(); pc2.isTrigger = true; pc2.SetPath(0, savedVerts); Debug.Log($"[{builderTag}] Restored WalkableZone: {savedVerts.Length} verts."); }
        else { var zGo = new GameObject("WalkableZone"); var pc2 = zGo.AddComponent<PolygonCollider2D>(); pc2.isTrigger = true; pc2.SetPath(0, new Vector2[] { new Vector2(-7.5f, -4f), new Vector2(-7.5f, 0f), new Vector2(7.5f, 0f), new Vector2(7.5f, -4f) }); Debug.LogWarning($"[{builderTag}] No WalkableZone found -- placeholder created."); }
    }

    internal static void AddDoorCTA(string name, Transform player, float cx, float cy, string interiorScene, string label, Color col, GameObject lockedPopup = null)
    {
        var doorGo = new GameObject(name); doorGo.transform.position = new Vector3(cx, cy, 0f);
        // targetScene is intentionally empty: MapDoorCTA handles entry and completion gating.
        // A non-empty targetScene on MapDoorTrigger would fire an unconditional scene load
        // on proximity, bypassing the completion check and grayout logic entirely.
        var dt = doorGo.AddComponent<MapDoorTrigger>(); dt.player = player; dt.targetScene = ""; dt.triggerRadius = 0.55f; dt.promptProximity = 999f; dt.prompt = null;
        var ctaRoot = new GameObject(name + "_CTA"); ctaRoot.transform.position = new Vector3(cx, cy + 0.75f, -0.5f);
        var cGo = new GameObject("C"); cGo.transform.SetParent(ctaRoot.transform, false); cGo.transform.localScale = new Vector3(0.012f, 0.012f, 1f);
        var wc = cGo.AddComponent<Canvas>(); wc.renderMode = RenderMode.WorldSpace; wc.sortingOrder = 35; cGo.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 70);
        var borderGo = new GameObject("Border", typeof(RectTransform)); borderGo.transform.SetParent(cGo.transform, false); var borderImg = borderGo.AddComponent<Image>(); borderImg.color = col; borderImg.raycastTarget = false; var brt = borderGo.GetComponent<RectTransform>(); brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = new Vector2(-4, -4); brt.offsetMax = new Vector2(4, 4);
        var fillGo = new GameObject("Fill", typeof(RectTransform)); fillGo.transform.SetParent(cGo.transform, false); var fillImg = fillGo.AddComponent<Image>(); fillImg.color = new Color(0.06f, 0.08f, 0.14f, 0.92f); fillImg.raycastTarget = false; var frt = fillGo.GetComponent<RectTransform>(); frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = frt.offsetMax = Vector2.zero;
        var tGo = new GameObject("Label", typeof(RectTransform)); tGo.transform.SetParent(cGo.transform, false); var tmp = tGo.AddComponent<TextMeshProUGUI>(); tmp.text = label; tmp.fontSize = 38; tmp.color = col; tmp.fontStyle = FontStyles.Bold; tmp.alignment = TextAlignmentOptions.Center; tmp.raycastTarget = false; var tRT = tGo.GetComponent<RectTransform>(); tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one; tRT.offsetMin = new Vector2(8, 4); tRT.offsetMax = new Vector2(-8, -4);
        var cta = ctaRoot.AddComponent<MapDoorCTA>(); cta.targetScene = interiorScene; cta.playerTransform = player; cta.doorPosition = doorGo.transform; cta.label = tmp; cta.completionKey = "completed_" + interiorScene; cta.activeColor = col; cta.completedColor = new Color(col.r * 0.35f, col.g * 0.35f, col.b * 0.35f, 0.40f); cta.bobAmount = 0.16f; cta.bobSpeed = 2.8f; cta.clickRadius = 1.4f; cta.proximityRadius = 0.55f; cta.completedLockPopup = lockedPopup; cta.completedLockMessage = "Already helped here! Try visiting another building.";
        var borderCTA = ctaRoot.AddComponent<CTABorderFader>(); borderCTA.borderImage = borderImg; borderCTA.completionKey = "completed_" + interiorScene; borderCTA.activeColor = col; borderCTA.completedColor = new Color(col.r * 0.35f, col.g * 0.35f, col.b * 0.35f, 0.40f);
    }

    // ============================================================
    //  AddWorldTransitionCTA
    //  Replaces AddPathFloatingText for world-to-world transitions.
    //  Creates a screen-space badge (bottom-left or bottom-right) with
    //  a WorldTransitionCTA proximity trigger in world space.
    //  requiresWorldComplete = 0 means always unlocked (e.g. "go back").
    // ============================================================
    internal static void AddWorldTransitionCTA(
        string name, Transform player, string targetScene, string label,
        Color col, GameObject lockedPopup, int requiresWorldComplete,
        bool isLeft, RectTransform canvasRT, WorldMapManager manager,
        string lockedMessage = "Finish exploring this neighbourhood first!")
    {
        // World-space proximity trigger
        var triggerGo = new GameObject(name + "_Trigger");
        triggerGo.transform.position = new Vector3(isLeft ? -7.0f : 7.0f, -3.8f, 0f);
        var wCTA = triggerGo.AddComponent<WorldTransitionCTA>();
        wCTA.player = player;
        wCTA.targetScene = targetScene;
        wCTA.lockedPopup = lockedPopup;
        wCTA.requiresWorldComplete = requiresWorldComplete;
        wCTA.triggerRadius = 1.2f;
        wCTA.lockedMessage = lockedMessage;
        wCTA.activeColor = col;
        wCTA.completedColor = new Color(col.r * 0.35f, col.g * 0.35f, col.b * 0.35f, 0.40f);

        // Screen-space badge
        var root = new GameObject(name, typeof(RectTransform));
        root.transform.SetParent(canvasRT, false);
        var rootRT = root.GetComponent<RectTransform>();
        rootRT.anchorMin = isLeft ? Vector2.zero : new Vector2(1, 0);
        rootRT.anchorMax = isLeft ? Vector2.zero : new Vector2(1, 0);
        rootRT.pivot = isLeft ? Vector2.zero : new Vector2(1, 0);
        rootRT.sizeDelta = new Vector2(220, 60);
        rootRT.anchoredPosition = new Vector2(isLeft ? 24 : -24, 24);

        var cGo = new GameObject("C", typeof(RectTransform));
        cGo.transform.SetParent(root.transform, false);
        var cRT = cGo.GetComponent<RectTransform>();
        cRT.anchorMin = Vector2.zero; cRT.anchorMax = Vector2.one;
        cRT.offsetMin = cRT.offsetMax = Vector2.zero;

        var borderGo = new GameObject("Border", typeof(RectTransform));
        borderGo.transform.SetParent(cGo.transform, false);
        var borderImg = borderGo.AddComponent<Image>(); borderImg.color = col; borderImg.raycastTarget = false;
        var brtG = borderGo.GetComponent<RectTransform>();
        brtG.anchorMin = Vector2.zero; brtG.anchorMax = Vector2.one;
        brtG.offsetMin = new Vector2(-4, -4); brtG.offsetMax = new Vector2(4, 4);

        var fillGo = new GameObject("Fill", typeof(RectTransform));
        fillGo.transform.SetParent(cGo.transform, false);
        var fillImg = fillGo.AddComponent<Image>(); fillImg.color = new Color(0.06f, 0.08f, 0.14f, 0.92f); fillImg.raycastTarget = false;
        var frtG = fillGo.GetComponent<RectTransform>();
        frtG.anchorMin = Vector2.zero; frtG.anchorMax = Vector2.one; frtG.offsetMin = frtG.offsetMax = Vector2.zero;

        var tGo = new GameObject("Label", typeof(RectTransform));
        tGo.transform.SetParent(cGo.transform, false);
        var tmp = tGo.AddComponent<TextMeshProUGUI>();
        tmp.text = isLeft ? $"< {label}" : $"{label} >";
        tmp.fontSize = 28; tmp.color = col;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center; tmp.raycastTarget = false;
        var tRT = tGo.GetComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
        tRT.offsetMin = new Vector2(8, 4); tRT.offsetMax = new Vector2(-8, -4);

        var bg = root.AddComponent<Image>(); bg.color = new Color(0, 0, 0, 0);
        var btn = root.AddComponent<Button>(); btn.targetGraphic = bg;
        btn.onClick.AddListener(() => wCTA.OnBadgeClick());

        wCTA.badgeLabel = tmp;
        wCTA.badgeBorderImage = borderImg;
    }

    // ============================================================
    //  BuildMapHint — wires the [M] button click to manager.ToggleMap
    // ============================================================
    internal static void BuildMapHint(RectTransform canvasRT, WorldMapManager manager)
    {
        var go = new GameObject("MapHint", typeof(RectTransform));
        go.transform.SetParent(canvasRT, false);
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(1, 1); r.anchorMax = new Vector2(1, 1);
        r.pivot = new Vector2(1, 1); r.sizeDelta = new Vector2(230, 44);
        r.anchoredPosition = new Vector2(-24, -24);
        var img = go.AddComponent<Image>(); img.color = new Color(0, 0, 0, 0.45f);
        var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        var cols = btn.colors;
        cols.highlightedColor = new Color(0.2f, 0.2f, 0.2f, 0.65f);
        cols.pressedColor = new Color(0.1f, 0.1f, 0.1f, 0.80f);
        btn.colors = cols;
        UnityEventTools.AddPersistentListener(btn.onClick, manager.ToggleMap);
        Stretch(MakeTxt(go.transform, "T", "[ M ]  World Map", 22, new Color(0.9f, 0.9f, 1f), TextAlignmentOptions.Center).rectTransform);
        // Must be last sibling so no other canvas element covers it and steals its clicks.
        go.transform.SetAsLastSibling();
    }

    internal static (GameObject panel, Button adv, TMP_Text name, TMP_Text body, TMP_Text hint, GameObject cp, Button ab, TMP_Text at, Button db, TMP_Text dt) BuildDialoguePanel(RectTransform rt, Color nameCol)
    {
        var advGo = new GameObject("AdvanceBtn", typeof(RectTransform)); advGo.transform.SetParent(rt, false); Stretch(advGo.GetComponent<RectTransform>()); var advImg = advGo.AddComponent<Image>(); advImg.color = new Color(0, 0, 0, 0); var advBtn = advGo.AddComponent<Button>(); advBtn.targetGraphic = advImg;
        advGo.SetActive(false); // Start inactive — this full-screen button must not block map/CTA clicks at scene open.
        var panel = new GameObject("DialoguePanel", typeof(RectTransform)); panel.transform.SetParent(rt, false); var prt = panel.GetComponent<RectTransform>(); prt.anchorMin = new Vector2(0, 0); prt.anchorMax = new Vector2(1, 0); prt.pivot = new Vector2(0.5f, 0); prt.sizeDelta = new Vector2(0, 220); prt.anchoredPosition = Vector2.zero; panel.AddComponent<Image>().color = new Color(0.08f, 0.15f, 0.28f, 0.95f);
        var nameBar = MakeImg(prt, "NameBar", Hex("#1A3A6B")); var nbrrt = nameBar.rectTransform; nbrrt.anchorMin = new Vector2(0, 1); nbrrt.anchorMax = new Vector2(0.42f, 1); nbrrt.pivot = new Vector2(0, 1); nbrrt.sizeDelta = new Vector2(0, 44);
        var nameText = MakeTxt(nbrrt, "Name", "Speaker", 30, nameCol, TextAlignmentOptions.MidlineLeft, FontStyles.Bold); var ntrt = nameText.rectTransform; ntrt.anchorMin = Vector2.zero; ntrt.anchorMax = Vector2.one; ntrt.offsetMin = new Vector2(18, 0); ntrt.offsetMax = Vector2.zero;
        var bodyText = MakeTxt(prt, "Body", "...", 26, Color.white, TextAlignmentOptions.TopLeft); bodyText.textWrappingMode = TextWrappingModes.Normal; var brrt = bodyText.rectTransform; brrt.anchorMin = Vector2.zero; brrt.anchorMax = Vector2.one; brrt.offsetMin = new Vector2(24, 48); brrt.offsetMax = new Vector2(-24, -50);
        var hint = MakeTxt(prt, "Hint", "Click to continue...", 20, new Color(0.7f, 0.7f, 0.9f), TextAlignmentOptions.MidlineRight); var hrrt = hint.rectTransform; hrrt.anchorMin = new Vector2(0, 0); hrrt.anchorMax = new Vector2(1, 0); hrrt.pivot = new Vector2(0.5f, 0); hrrt.sizeDelta = new Vector2(0, 36); hrrt.anchoredPosition = new Vector2(0, 8);
        var cpGo = new GameObject("ChoicePanel", typeof(RectTransform)); cpGo.transform.SetParent(prt, false); var cprt = cpGo.GetComponent<RectTransform>(); cprt.anchorMin = new Vector2(0.5f, 0); cprt.anchorMax = new Vector2(0.5f, 0); cprt.pivot = new Vector2(0.5f, 0); cprt.sizeDelta = new Vector2(800, 72); cprt.anchoredPosition = new Vector2(0, 8);
        var aGo = MakeBtn(cprt, "AcceptBtn", "Let's go!", 26, Hex("#2ECC71"), Color.white); var dGo = MakeBtn(cprt, "DeclineBtn", "Maybe later", 26, Hex("#E74C3C"), Color.white);
        var art = aGo.GetComponent<RectTransform>(); art.anchorMin = new Vector2(0, 0); art.anchorMax = new Vector2(0.48f, 1); art.offsetMin = art.offsetMax = Vector2.zero;
        var drt = dGo.GetComponent<RectTransform>(); drt.anchorMin = new Vector2(0.52f, 0); drt.anchorMax = new Vector2(1, 1); drt.offsetMin = drt.offsetMax = Vector2.zero;
        panel.SetActive(false);
        return (panel, advBtn, nameText, bodyText, hint, cpGo, aGo.GetComponent<Button>(), aGo.transform.Find("Label").GetComponent<TMP_Text>(), dGo.GetComponent<Button>(), dGo.transform.Find("Label").GetComponent<TMP_Text>());
    }

    internal static GameObject BuildWorldMapPanel(RectTransform canvasRT, WorldMapManager manager, Color mapBg)
    {
        var ov = MakeImg(canvasRT, "WorldMapOverlay", new Color(0, 0, 0, 0.88f)); Stretch(ov.rectTransform); ov.raycastTarget = true;
        var card = MakeImg(ov.rectTransform, "MapCard", new Color(0, 0, 0, 0)); var crt = card.rectTransform; crt.anchorMin = new Vector2(0.05f, 0.06f); crt.anchorMax = new Vector2(0.95f, 0.94f); crt.offsetMin = crt.offsetMax = Vector2.zero;
        var mi = MakeImg(crt, "MapImage", Color.white); mi.type = Image.Type.Simple; mi.preserveAspect = true; mi.raycastTarget = false; Stretch(mi.rectTransform);
        Sprite[] mapVersions = new Sprite[5]; for (int v = 1; v <= 5; v++) mapVersions[v - 1] = FindSprite("map_v" + v);
        bool anyMapFound = false; foreach (var s in mapVersions) if (s != null) anyMapFound = true;
        if (anyMapFound) { var sw = mi.gameObject.AddComponent<WorldMapImageSwitcher>(); sw.targetImage = mi; sw.mapVersions = mapVersions; sw.Refresh(); } else { card.color = mapBg; }
        var tb = MakeImg(ov.rectTransform, "TitleBar", new Color(0.05f, 0.08f, 0.15f, 0.92f)); var tbr = tb.rectTransform; tbr.anchorMin = new Vector2(0, 1); tbr.anchorMax = new Vector2(1, 1); tbr.pivot = new Vector2(0.5f, 1); tbr.sizeDelta = new Vector2(0, 64); tbr.anchoredPosition = Vector2.zero;
        Stretch(MakeTxt(tbr, "Title", "PHISHERMAN  WORLD MAP", 36, Color.white, TextAlignmentOptions.Center, FontStyles.Bold).rectTransform);
        var cb = MakeBtn(ov.rectTransform, "CloseBtn", "X  Close Map  ( M )", 26, new Color(0.18f, 0.28f, 0.45f, 0.95f), Color.white); var cbrt = cb.GetComponent<RectTransform>(); cbrt.anchorMin = new Vector2(0.5f, 0); cbrt.anchorMax = new Vector2(0.5f, 0); cbrt.pivot = new Vector2(0.5f, 0); cbrt.sizeDelta = new Vector2(360, 56); cbrt.anchoredPosition = new Vector2(0, 12);
        UnityEventTools.AddPersistentListener(cb.GetComponent<Button>().onClick, manager.CloseMap);
        ov.gameObject.SetActive(false); return ov.gameObject;
    }

    internal static GameObject BuildLockedPopup(RectTransform canvasRT, string msg, Color borderCol)
    {
        var ov = MakeImg(canvasRT, "LockedPopup", new Color(0, 0, 0, 0)); Stretch(ov.rectTransform); ov.raycastTarget = false;
        var card = MakeImg(ov.rectTransform, "Card", new Color(0.08f, 0.10f, 0.20f, 0.93f)); var crt = card.rectTransform; crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.62f); crt.pivot = new Vector2(0.5f, 0.5f); crt.sizeDelta = new Vector2(740, 150);
        var brd = MakeImg(crt, "Border", borderCol); var brt = brd.rectTransform; brt.anchorMin = new Vector2(0, 1); brt.anchorMax = new Vector2(1, 1); brt.pivot = new Vector2(0.5f, 1); brt.sizeDelta = new Vector2(0, 6);
        var txt = MakeTxt(crt, "Msg", msg, 30, Color.white, TextAlignmentOptions.Center); txt.textWrappingMode = TextWrappingModes.Normal; Stretch(txt.rectTransform);
        var controller = ov.gameObject.AddComponent<LockedPopupController>(); controller.messageText = txt;
        ov.gameObject.SetActive(false); return ov.gameObject;
    }

    internal static void WireManager(WorldMapManager manager, GameObject playerGo, SpriteRenderer playerSR, GameObject dialogPanel, Button advBtn, TMP_Text nameText, TMP_Text bodyText, TMP_Text hintText, GameObject choicePanel, Button acceptBtn, TMP_Text acceptTxt, Button declineBtn, TMP_Text declineTxt, GameObject mapPanel)
    {
        manager.playerTransform = playerGo.transform; manager.playerRenderer = playerSR;
        manager.npcTransforms = new Transform[0]; manager.exclamationMarks = new GameObject[0];
        manager.dialoguePanel = dialogPanel; manager.advanceButton = advBtn; manager.speakerNameText = nameText; manager.dialogueText = bodyText; manager.continueHint = hintText;
        manager.choicePanel = choicePanel; manager.acceptButton = acceptBtn; manager.acceptButtonText = acceptTxt; manager.declineButton = declineBtn; manager.declineButtonText = declineTxt;
        manager.worldMapPanel = mapPanel; manager.boundsX = new Vector2(-7.8f, 7.8f); manager.boundsY = new Vector2(-4.0f, 4.3f);
        UnityEventTools.AddPersistentListener(advBtn.onClick, manager.AdvanceDialogue);
        UnityEventTools.AddPersistentListener(acceptBtn.onClick, manager.OnAcceptHelp);
        UnityEventTools.AddPersistentListener(declineBtn.onClick, manager.OnDeclineHelp);
    }

    internal static void AddNavHint(RectTransform canvasRT, string msg, Vector2 anchor, Color col, FontStyles style = FontStyles.Normal)
    {
        var go = new GameObject("NavHint", typeof(RectTransform)); go.transform.SetParent(canvasRT, false);
        var r = go.GetComponent<RectTransform>(); r.anchorMin = anchor; r.anchorMax = anchor; r.pivot = new Vector2(anchor.x <= 0.5f ? 0 : 1, 1); r.sizeDelta = new Vector2(480, 44); r.anchoredPosition = new Vector2(anchor.x <= 0.5f ? 24 : -24, -24);
        go.AddComponent<Image>().color = new Color(0, 0, 0, 0.45f);
        var t = MakeTxt(go.transform, "T", msg, 20, col, TextAlignmentOptions.Center, style); Stretch(t.rectTransform);
    }

    internal static Sprite FindSprite(string name) { foreach (var g in AssetDatabase.FindAssets(name + " t:Texture2D")) { var p = AssetDatabase.GUIDToAssetPath(g); if (System.IO.Path.GetFileNameWithoutExtension(p).ToLower() == name.ToLower()) { var s = AssetDatabase.LoadAssetAtPath<Sprite>(p); if (s != null) return s; var reps = AssetDatabase.LoadAllAssetRepresentationsAtPath(p); foreach (var r in reps) { if (r is Sprite sp) return sp; } } } return null; }
    internal static Image MakeImg(Transform p, string n, Color c) { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); var img = go.AddComponent<Image>(); img.color = c; return img; }
    internal static TMP_Text MakeTxt(Transform p, string n, string text, int size, Color col, TextAlignmentOptions align, FontStyles style = FontStyles.Normal) { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); var t = go.AddComponent<TextMeshProUGUI>(); t.text = text; t.fontSize = size; t.color = col; t.alignment = align; t.fontStyle = style; t.raycastTarget = false; return t; }
    internal static GameObject MakeBtn(Transform p, string n, string label, int size, Color bg, Color tc) { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); var img = go.AddComponent<Image>(); img.color = bg; var btn = go.AddComponent<Button>(); btn.targetGraphic = img; var t = MakeTxt(go.transform, "Label", label, size, tc, TextAlignmentOptions.Center, FontStyles.Bold); Stretch(t.rectTransform); return go; }
    internal static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
    internal static Color Hex(string h) => ColorUtility.TryParseHtmlString(h, out var c) ? c : Color.magenta;
    internal static void AddToBuild(string path) { var scenes = EditorBuildSettings.scenes.ToList(); if (!scenes.Any(s => s.path == path)) { scenes.Add(new EditorBuildSettingsScene(path, true)); EditorBuildSettings.scenes = scenes.ToArray(); } }

    // ============================================================
    //  BuildPlayerAndManager
    //  CHANGED: Now also adds PlayerController for WASD movement,
    //  matching World 1 exactly.
    // ============================================================
    internal static (GameObject playerGo, SpriteRenderer playerSR, Transform playerT, WorldMapManager manager) BuildPlayerAndManager(float playerScale, float sharedSpeed)
    {
        var phisherman = FindSprite("phisherman"); var phishermanWalk = FindSprite("phisherman_walking");
        var playerGo = new GameObject("Phisherman"); playerGo.tag = "Player";
        playerGo.transform.position = new Vector3(0f, -1.5f, 0f); playerGo.transform.localScale = new Vector3(playerScale, playerScale, 1f);
        var playerSR = playerGo.AddComponent<SpriteRenderer>(); playerSR.sprite = phisherman; playerSR.color = Color.white; playerSR.sortingOrder = 10;
        var anim = playerGo.AddComponent<MapWalkAnimator>(); anim.idleSprite = phisherman; anim.walkSprite = phishermanWalk ?? phisherman; anim.fps = 2f;
        playerGo.AddComponent<MapWasdZoneClamp>();
        var nav = playerGo.AddComponent<MapNavAgent>(); nav.gridCols = 80; nav.gridRows = 45; nav.moveSpeed = sharedSpeed;
        // ADDED: PlayerController for WASD movement, same as World 1
        var mover = playerGo.AddComponent<PlayerController>(); mover.moveSpeed = sharedSpeed;
        var mgrGo = new GameObject("GameManager"); var manager = mgrGo.AddComponent<WorldMapManager>(); manager.playerSpeed = sharedSpeed;
        var hud = mgrGo.AddComponent<LevelSystemHUD>(); AssignStickerSprites(hud);
        return (playerGo, playerSR, playerGo.transform, manager);
    }
    internal static void AssignStickerSprites(LevelSystemHUD hud) { if (hud == null) return; var catalog = PlayerProgress.FishCatalog; var sprites = new Sprite[catalog.Length]; for (int i = 0; i < catalog.Length; i++) sprites[i] = FindSprite(catalog[i].spriteName); hud.stickerSprites = sprites; int found = 0; foreach (var s in sprites) if (s != null) found++; Debug.Log($"[SharedWorldBuilderUtils] AssignStickerSprites: {found}/{catalog.Length} fish sprites assigned."); }
}

// ============================================================
//  World2Builder
// ============================================================
public static class World2Builder
{
    private const string ScenesDir = "Assets/Scenes"; private const string ScenePath = "Assets/Scenes/WorldMap2.unity";
    private const float OrthoSize = 4.5f; private const float AspectW = 16f / 9f; private const float SharedSpeed = 3.5f; private const float PlayerScale = 0.44f;
    static Color Hex(string h) => SharedWorldBuilderUtils.Hex(h);

    [MenuItem("Phisherman/Build World 2 Scene")]
    public static void Build()
    {
        if (!Directory.Exists(ScenesDir)) Directory.CreateDirectory(ScenesDir);
        var savedVerts = SharedWorldBuilderUtils.SaveWalkableZone(ScenePath, "World2Builder", out var savedPos);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SharedWorldBuilderUtils.RestoreWalkableZone("World2Builder", savedVerts, savedPos);

        var bgArt = SharedWorldBuilderUtils.FindSprite("world_stage_2_background");
        if (bgArt != null) { var bgGo = new GameObject("WorldBackground"); var bgSR = bgGo.AddComponent<SpriteRenderer>(); bgSR.sprite = bgArt; bgSR.color = Color.white; bgSR.sortingOrder = -50; float camH = OrthoSize * 2f, camW = camH * AspectW; bgGo.transform.localScale = new Vector3(camW / bgArt.bounds.size.x, camH / bgArt.bounds.size.y, 1f); } else Debug.LogWarning("[World2Builder] 'world_stage_2_background' not found.");

        var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera"; var cam = camGo.AddComponent<Camera>(); camGo.AddComponent<AudioListener>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Hex("#6BB8D4"); cam.orthographic = true; cam.orthographicSize = OrthoSize; camGo.transform.position = new Vector3(0, 0, -10);
        var es = new GameObject("EventSystem"); es.AddComponent<EventSystem>(); es.AddComponent<StandaloneInputModule>();

        var (playerGo, playerSR, playerT, manager) = SharedWorldBuilderUtils.BuildPlayerAndManager(PlayerScale, SharedSpeed);

        // Door CTAs — identical pattern to World 1
        var canvasGo = new GameObject("Canvas"); var canvas = canvasGo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; var scaler = canvasGo.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f; canvasGo.AddComponent<GraphicRaycaster>(); var canvasRT = canvasGo.GetComponent<RectTransform>();

        var (dialogPanel, advBtn, nameText, bodyText, hintText, choicePanel, acceptBtn, acceptTxt, declineBtn, declineTxt) = SharedWorldBuilderUtils.BuildDialoguePanel(canvasRT, Hex("#FFD93D"));

        // CHANGED: BuildMapHint now wires the click listener
        SharedWorldBuilderUtils.BuildMapHint(canvasRT, manager);

        var mapPanel = SharedWorldBuilderUtils.BuildWorldMapPanel(canvasRT, manager, Hex("#1A6E9E"));

        // CHANGED: single locked popup used by both doors and world transitions
        var lockedPopup = SharedWorldBuilderUtils.BuildLockedPopup(canvasRT, "Explore the neighbourhood first before moving on!", Hex("#2BB3A3"));

        // Door CTAs with completion tracking
        SharedWorldBuilderUtils.AddDoorCTA("Door_W2_LeftHouse", playerT, -4.0f, 0.4f, "FlatInterior", "Mr. Kowalski", Hex("#FF9F1C"), lockedPopup);
        SharedWorldBuilderUtils.AddDoorCTA("Door_W2_CentreHouse", playerT, 0.2f, 1.2f, "PizzaW2Interior", "Nonna Bea", Hex("#FF6B6B"), lockedPopup);
        SharedWorldBuilderUtils.AddDoorCTA("Door_W2_RightHouse", playerT, 4.3f, 0.2f, "OfficeW2Interior", "Mr. Frost", Hex("#4ECDC4"), lockedPopup);

        // CHANGED: World transition CTAs replacing AddPathFloatingText
        // Left = go back to World 1 (always unlocked — requiresWorldComplete:0)
        SharedWorldBuilderUtils.AddWorldTransitionCTA("WorldCTA_W1", playerT, "WorldMap", "World 1",
            Hex("#2BB3A3"), lockedPopup, requiresWorldComplete: 0, isLeft: true, canvasRT: canvasRT, manager: manager);
        // Right = advance to World 3 (requires World 2 complete)
        SharedWorldBuilderUtils.AddWorldTransitionCTA("WorldCTA_W3", playerT, "WorldMap3", "World 3",
            Hex("#E74C3C"), lockedPopup, requiresWorldComplete: 2, isLeft: false, canvasRT: canvasRT, manager: manager,
            lockedMessage: "Complete all 3 houses in this neighbourhood first!");

        // Nav hint
        // Nav hint removed — world transition CTAs in the corners already convey direction.

        SharedWorldBuilderUtils.WireManager(manager, playerGo, playerSR, dialogPanel, advBtn, nameText, bodyText, hintText, choicePanel, acceptBtn, acceptTxt, declineBtn, declineTxt, mapPanel);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene, ScenePath); SharedWorldBuilderUtils.AddToBuild(ScenePath); AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); Debug.Log($"[World2Builder] Built -> {ScenePath}");
    }
}

// ============================================================
//  World3Builder
// ============================================================
public static class World3Builder
{
    private const string ScenesDir = "Assets/Scenes"; private const string ScenePath = "Assets/Scenes/WorldMap3.unity";
    private const float OrthoSize = 4.5f; private const float AspectW = 16f / 9f; private const float SharedSpeed = 3.5f; private const float PlayerScale = 0.44f;
    static Color Hex(string h) => SharedWorldBuilderUtils.Hex(h);

    [MenuItem("Phisherman/Build World 3 Scene")]
    public static void Build()
    {
        if (!Directory.Exists(ScenesDir)) Directory.CreateDirectory(ScenesDir);
        var savedVerts = SharedWorldBuilderUtils.SaveWalkableZone(ScenePath, "World3Builder", out var savedPos);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SharedWorldBuilderUtils.RestoreWalkableZone("World3Builder", savedVerts, savedPos);

        var bgArt = SharedWorldBuilderUtils.FindSprite("world_stage_3_background");
        if (bgArt != null) { var bgGo = new GameObject("WorldBackground"); var bgSR = bgGo.AddComponent<SpriteRenderer>(); bgSR.sprite = bgArt; bgSR.color = Color.white; bgSR.sortingOrder = -50; float camH = OrthoSize * 2f, camW = camH * AspectW; bgGo.transform.localScale = new Vector3(camW / bgArt.bounds.size.x, camH / bgArt.bounds.size.y, 1f); } else Debug.LogWarning("[World3Builder] 'world_stage_3_background' not found.");

        var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera"; var cam = camGo.AddComponent<Camera>(); camGo.AddComponent<AudioListener>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Hex("#1A1A2E"); cam.orthographic = true; cam.orthographicSize = OrthoSize; camGo.transform.position = new Vector3(0, 0, -10);
        var es = new GameObject("EventSystem"); es.AddComponent<EventSystem>(); es.AddComponent<StandaloneInputModule>();

        var (playerGo, playerSR, playerT, manager) = SharedWorldBuilderUtils.BuildPlayerAndManager(PlayerScale, SharedSpeed);

        var canvasGo = new GameObject("Canvas"); var canvas = canvasGo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; var scaler = canvasGo.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f; canvasGo.AddComponent<GraphicRaycaster>(); var canvasRT = canvasGo.GetComponent<RectTransform>();

        var (dialogPanel, advBtn, nameText, bodyText, hintText, choicePanel, acceptBtn, acceptTxt, declineBtn, declineTxt) = SharedWorldBuilderUtils.BuildDialoguePanel(canvasRT, Hex("#FFD93D"));

        SharedWorldBuilderUtils.BuildMapHint(canvasRT, manager);

        var mapPanel = SharedWorldBuilderUtils.BuildWorldMapPanel(canvasRT, manager, Hex("#1A6E9E"));

        var lockedPopup = SharedWorldBuilderUtils.BuildLockedPopup(canvasRT, "Complete all 3 houses in this neighbourhood first!", Hex("#E74C3C"));

        SharedWorldBuilderUtils.AddDoorCTA("Door_W3_House1", playerT, -4.0f, 0.4f, "AuntCarolInterior", "Aunt Carol", Hex("#2BB3A3"), lockedPopup);
        SharedWorldBuilderUtils.AddDoorCTA("Door_W3_House2", playerT, 0.2f, 1.2f, "UncleMarcusInterior", "Uncle Marcus", Hex("#FF9F1C"), lockedPopup);
        SharedWorldBuilderUtils.AddDoorCTA("Door_W3_House3", playerT, 4.3f, 0.2f, "GrandpaLouInterior", "Grandpa Lou", Hex("#A29BFE"), lockedPopup);

        // Left = go back to World 2 (always unlocked)
        SharedWorldBuilderUtils.AddWorldTransitionCTA("WorldCTA_W2", playerT, "WorldMap2", "World 2",
            Hex("#2BB3A3"), lockedPopup, requiresWorldComplete: 0, isLeft: true, canvasRT: canvasRT, manager: manager);
        // Right = advance to World 4 (requires World 3 complete)
        SharedWorldBuilderUtils.AddWorldTransitionCTA("WorldCTA_W4", playerT, "WorldMap4", "World 4",
            Hex("#FF9F1C"), lockedPopup, requiresWorldComplete: 3, isLeft: false, canvasRT: canvasRT, manager: manager,
            lockedMessage: "Complete all 3 houses in this neighbourhood first!");

        // Nav hint removed.

        SharedWorldBuilderUtils.WireManager(manager, playerGo, playerSR, dialogPanel, advBtn, nameText, bodyText, hintText, choicePanel, acceptBtn, acceptTxt, declineBtn, declineTxt, mapPanel);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene, ScenePath); SharedWorldBuilderUtils.AddToBuild(ScenePath); AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); Debug.Log($"[World3Builder] Built -> {ScenePath}");
    }
}

// ============================================================
//  World4Builder
// ============================================================
public static class World4Builder
{
    private const string ScenesDir = "Assets/Scenes"; private const string ScenePath = "Assets/Scenes/WorldMap4.unity";
    private const float OrthoSize = 4.5f; private const float AspectW = 16f / 9f; private const float SharedSpeed = 3.5f; private const float PlayerScale = 0.44f;
    static Color Hex(string h) => SharedWorldBuilderUtils.Hex(h);

    [MenuItem("Phisherman/Build World 4 Scene")]
    public static void Build()
    {
        if (!Directory.Exists(ScenesDir)) Directory.CreateDirectory(ScenesDir);
        var savedVerts = SharedWorldBuilderUtils.SaveWalkableZone(ScenePath, "World4Builder", out var savedPos);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SharedWorldBuilderUtils.RestoreWalkableZone("World4Builder", savedVerts, savedPos);

        var bgArt = SharedWorldBuilderUtils.FindSprite("world_stage_4_background");
        if (bgArt != null) { var bgGo = new GameObject("WorldBackground"); var bgSR = bgGo.AddComponent<SpriteRenderer>(); bgSR.sprite = bgArt; bgSR.color = Color.white; bgSR.sortingOrder = -50; float camH = OrthoSize * 2f, camW = camH * AspectW; bgGo.transform.localScale = new Vector3(camW / bgArt.bounds.size.x, camH / bgArt.bounds.size.y, 1f); } else Debug.LogWarning("[World4Builder] 'world_stage_4_background' not found.");

        var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera"; var cam = camGo.AddComponent<Camera>(); camGo.AddComponent<AudioListener>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Hex("#1A0A2E"); cam.orthographic = true; cam.orthographicSize = OrthoSize; camGo.transform.position = new Vector3(0, 0, -10);
        var es = new GameObject("EventSystem"); es.AddComponent<EventSystem>(); es.AddComponent<StandaloneInputModule>();

        var (playerGo, playerSR, playerT, manager) = SharedWorldBuilderUtils.BuildPlayerAndManager(PlayerScale, SharedSpeed);

        var canvasGo = new GameObject("Canvas"); var canvas = canvasGo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; var scaler = canvasGo.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f; canvasGo.AddComponent<GraphicRaycaster>(); var canvasRT = canvasGo.GetComponent<RectTransform>();

        var (dialogPanel, advBtn, nameText, bodyText, hintText, choicePanel, acceptBtn, acceptTxt, declineBtn, declineTxt) = SharedWorldBuilderUtils.BuildDialoguePanel(canvasRT, Hex("#FFD93D"));

        SharedWorldBuilderUtils.BuildMapHint(canvasRT, manager);

        var mapPanel = SharedWorldBuilderUtils.BuildWorldMapPanel(canvasRT, manager, Hex("#1A6E9E"));

        var lockedPopup = SharedWorldBuilderUtils.BuildLockedPopup(canvasRT, "Complete all 3 houses in this neighbourhood first!", Hex("#A29BFE"));

        SharedWorldBuilderUtils.AddDoorCTA("Door_W4_House1", playerT, -4.0f, 0.4f, "GrandmaIrisInterior", "Grandma Iris", Hex("#2ECC71"), lockedPopup);
        SharedWorldBuilderUtils.AddDoorCTA("Door_W4_House2", playerT, 0.2f, 1.2f, "UncleFelixInterior", "Uncle Felix", Hex("#A29BFE"), lockedPopup);
        SharedWorldBuilderUtils.AddDoorCTA("Door_W4_House3", playerT, 4.3f, 0.2f, "AuntDanaInterior", "Aunt Dana", Hex("#FF6B6B"), lockedPopup);

        // Left = go back to World 3 (always unlocked)
        SharedWorldBuilderUtils.AddWorldTransitionCTA("WorldCTA_W3", playerT, "WorldMap3", "World 3",
            Hex("#2BB3A3"), lockedPopup, requiresWorldComplete: 0, isLeft: true, canvasRT: canvasRT, manager: manager);
        // Right = advance to World 5 (requires World 4 complete)
        SharedWorldBuilderUtils.AddWorldTransitionCTA("WorldCTA_W5", playerT, "WorldMap5", "World 5",
            Hex("#A29BFE"), lockedPopup, requiresWorldComplete: 4, isLeft: false, canvasRT: canvasRT, manager: manager,
            lockedMessage: "Complete all 3 houses in this neighbourhood first!");

        // Nav hint removed.

        SharedWorldBuilderUtils.WireManager(manager, playerGo, playerSR, dialogPanel, advBtn, nameText, bodyText, hintText, choicePanel, acceptBtn, acceptTxt, declineBtn, declineTxt, mapPanel);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene, ScenePath); SharedWorldBuilderUtils.AddToBuild(ScenePath); AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); Debug.Log($"[World4Builder] Built -> {ScenePath}");
    }
}

// ============================================================
//  World5Builder
// ============================================================
public static class World5Builder
{
    private const string ScenesDir = "Assets/Scenes"; private const string ScenePath = "Assets/Scenes/WorldMap5.unity";
    private const float OrthoSize = 4.5f; private const float AspectW = 16f / 9f; private const float SharedSpeed = 3.5f; private const float PlayerScale = 0.44f;
    static Color Hex(string h) => SharedWorldBuilderUtils.Hex(h);

    [MenuItem("Phisherman/Build World 5 Scene")]
    public static void Build()
    {
        if (!Directory.Exists(ScenesDir)) Directory.CreateDirectory(ScenesDir);
        var savedVerts = SharedWorldBuilderUtils.SaveWalkableZone(ScenePath, "World5Builder", out var savedPos);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SharedWorldBuilderUtils.RestoreWalkableZone("World5Builder", savedVerts, savedPos);

        var bgArt = SharedWorldBuilderUtils.FindSprite("world_stage_5_background");
        if (bgArt != null) { var bgGo = new GameObject("WorldBackground"); var bgSR = bgGo.AddComponent<SpriteRenderer>(); bgSR.sprite = bgArt; bgSR.color = Color.white; bgSR.sortingOrder = -50; float camH = OrthoSize * 2f, camW = camH * AspectW; bgGo.transform.localScale = new Vector3(camW / bgArt.bounds.size.x, camH / bgArt.bounds.size.y, 1f); } else Debug.LogWarning("[World5Builder] 'world_stage_5_background' not found.");

        var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera"; var cam = camGo.AddComponent<Camera>(); camGo.AddComponent<AudioListener>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Hex("#050D1A"); cam.orthographic = true; cam.orthographicSize = OrthoSize; camGo.transform.position = new Vector3(0, 0, -10);
        var es = new GameObject("EventSystem"); es.AddComponent<EventSystem>(); es.AddComponent<StandaloneInputModule>();

        var (playerGo, playerSR, playerT, manager) = SharedWorldBuilderUtils.BuildPlayerAndManager(PlayerScale, SharedSpeed);

        var canvasGo = new GameObject("Canvas"); var canvas = canvasGo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; var scaler = canvasGo.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f; canvasGo.AddComponent<GraphicRaycaster>(); var canvasRT = canvasGo.GetComponent<RectTransform>();

        var (dialogPanel, advBtn, nameText, bodyText, hintText, choicePanel, acceptBtn, acceptTxt, declineBtn, declineTxt) = SharedWorldBuilderUtils.BuildDialoguePanel(canvasRT, Hex("#FFD93D"));

        SharedWorldBuilderUtils.BuildMapHint(canvasRT, manager);

        var mapPanel = SharedWorldBuilderUtils.BuildWorldMapPanel(canvasRT, manager, Hex("#1A6E9E"));

        // World 5 is the final island — no locked popup needed for the right exit,
        // but we still need one for the door "already completed" messages.
        var lockedPopup = SharedWorldBuilderUtils.BuildLockedPopup(canvasRT, "You've already helped here! Explore the other houses.", Hex("#A29BFE"));

        SharedWorldBuilderUtils.AddDoorCTA("Door_W5_House1", playerT, -4.0f, 0.4f, "GrandpaErnestInterior", "Grandpa Ernest", Hex("#3498DB"), lockedPopup);
        SharedWorldBuilderUtils.AddDoorCTA("Door_W5_House2", playerT, 0.2f, 1.2f, "AuntPriyaInterior", "Aunt Priya", Hex("#1ABC9C"), lockedPopup);
        SharedWorldBuilderUtils.AddDoorCTA("Door_W5_House3", playerT, 4.3f, 0.2f, "UncleDiegoInterior", "Uncle Diego", Hex("#E74C3C"), lockedPopup);

        // Both exits go back to World 4 (final island — no world 6)
        // Left exit: always unlocked (go back)
        SharedWorldBuilderUtils.AddWorldTransitionCTA("WorldCTA_W4_L", playerT, "WorldMap4", "Exit Island",
            Hex("#A29BFE"), lockedPopup, requiresWorldComplete: 0, isLeft: true, canvasRT: canvasRT, manager: manager);
        // Right exit: also goes back to World 4 (always unlocked — final world)
        SharedWorldBuilderUtils.AddWorldTransitionCTA("WorldCTA_W4_R", playerT, "WorldMap4", "Exit Island",
            Hex("#A29BFE"), lockedPopup, requiresWorldComplete: 0, isLeft: false, canvasRT: canvasRT, manager: manager);

        SharedWorldBuilderUtils.AddNavHint(canvasRT, "The Inner Island -- Final World", new Vector2(0.5f, 1), new Color(0.85f, 0.80f, 1f), FontStyles.Italic);

        SharedWorldBuilderUtils.WireManager(manager, playerGo, playerSR, dialogPanel, advBtn, nameText, bodyText, hintText, choicePanel, acceptBtn, acceptTxt, declineBtn, declineTxt, mapPanel);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene, ScenePath); SharedWorldBuilderUtils.AddToBuild(ScenePath); AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); Debug.Log($"[World5Builder] Built -> {ScenePath}");
    }
}