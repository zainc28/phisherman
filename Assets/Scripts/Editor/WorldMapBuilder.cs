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
/// Builds the WorldMap scene (Stage 1 — neighbourhood).
///
/// KEY CHANGES:
///  - Player has PersistentPlayer component → survives all scene transitions
///    via DontDestroyOnLoad. Animation, speed, size all carry over automatically.
///  - Player tagged "Player" so WorldMapManager and PersistentPlayer can find it.
///  - PlayerSpawn GO marks where the player should stand in this scene.
///  - WalkableZone is NOT rebuilt — it's preserved from what you drew in the editor.
///    Run this builder after drawing WalkableZone and it will be kept.
///  - MapNavAgent uses pure ray-casting math (no Physics2D/Rigidbody2D needed).
/// </summary>
public static class WorldMapBuilder
{
    private const string ScenesDir = "Assets/Scenes";
    private const string ScenePath = "Assets/Scenes/WorldMap.unity";

    private const float OrthoSize = 4.5f;
    private const float AspectW = 16f / 9f;
    private const float SharedSpeed = 3.5f;
    private const float PlayerScale = 0.44f;

    private static readonly Color AcceptCol = Hex("#2ECC71");
    private static readonly Color DeclineCol = Hex("#E74C3C");
    private static readonly Color NameCol = Hex("#FFD93D");
    private static readonly Color MapBg = Hex("#1A6E9E");
    private static readonly Color BadgeAccent = Hex("#2BB3A3");

    [MenuItem("Phisherman/Build World Map Scene")]
    public static void Build()
    {
        if (!Directory.Exists(ScenesDir)) Directory.CreateDirectory(ScenesDir);

        // ── Preserve WalkableZone polygon across rebuilds ──────────
        // Save the polygon points before clearing the scene so we can
        // restore it after the new scene is created.
        Vector2[] savedVerts = null;
        Vector3 savedZonePos = Vector3.zero;
        var existingZone = GameObject.Find("WalkableZone");
        if (existingZone != null)
        {
            var pc = existingZone.GetComponent<PolygonCollider2D>();
            if (pc != null)
            {
                savedVerts = pc.points.ToArray();
                savedZonePos = existingZone.transform.position;
                Debug.Log($"[WorldMapBuilder] Saved WalkableZone with {savedVerts.Length} verts.");
            }
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Restore WalkableZone
        if (savedVerts != null)
        {
            var zoneGo = new GameObject("WalkableZone");
            zoneGo.transform.position = savedZonePos;
            var pc2 = zoneGo.AddComponent<PolygonCollider2D>();
            pc2.isTrigger = true;
            pc2.SetPath(0, savedVerts);
            Debug.Log($"[WorldMapBuilder] Restored WalkableZone with {savedVerts.Length} verts.");
        }
        else
        {
            // No zone drawn yet — create a placeholder that covers the whole scene.
            // Draw your own shape in the editor after the first build.
            var zoneGo = new GameObject("WalkableZone");
            var pc2 = zoneGo.AddComponent<PolygonCollider2D>();
            pc2.isTrigger = true;
            pc2.SetPath(0, new Vector2[]
            {
                new Vector2(-7.5f,-4f), new Vector2(-7.5f,0f),
                new Vector2( 7.5f, 0f), new Vector2( 7.5f,-4f)
            });
            Debug.LogWarning("[WorldMapBuilder] No WalkableZone found — created a placeholder. " +
                             "Select WalkableZone in Hierarchy, click Edit Collider, and trace the walkable paths.");
        }

        Sprite circle = GetCircle();
        Sprite phisherman = FindSprite("phisherman");
        Sprite phishermanWalk = FindSprite("phisherman_walking");
        LogFound("phisherman", phisherman);
        LogFound("phisherman_walking", phishermanWalk);

        // ── Camera ───────────────────────────────────────────────────
        var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>(); camGo.AddComponent<AudioListener>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Hex("#4FA8D9");
        cam.orthographic = true; cam.orthographicSize = OrthoSize;
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
            bgGo.transform.localScale = new Vector3(camW / bgArt.bounds.size.x, camH / bgArt.bounds.size.y, 1f);
        }
        else Debug.LogWarning("[WorldMapBuilder] 'world_stage_1_background' not found.");

        // ── Player spawn point ─────────────────────────────────────────
        // PersistentPlayer will teleport the player here on scene load.
        var spawnGo = new GameObject("PlayerSpawn");
        spawnGo.transform.position = new Vector3(0f, -1.5f, 0f);

        // ── Player GO (only created on very first build — PersistentPlayer
        //    survives subsequent scene loads, so we tag it correctly) ──
        // We always rebuild the player here; PersistentPlayer's singleton
        // logic destroys the duplicate if one already exists from a previous
        // scene load. The fresh one in WorldMap is always the authoritative one
        // on a cold start.
        var playerGo = new GameObject("Phisherman");
        playerGo.tag = "Player";
        playerGo.transform.position = new Vector3(0f, -1.5f, 0f);
        playerGo.transform.localScale = new Vector3(PlayerScale, PlayerScale, 1f);

        var playerSR = playerGo.AddComponent<SpriteRenderer>();
        playerSR.sprite = phisherman; playerSR.color = Color.white; playerSR.sortingOrder = 10;

        var anim = playerGo.AddComponent<MapWalkAnimator>();
        anim.idleSprite = phisherman;
        anim.walkSprite = phishermanWalk != null ? phishermanWalk : phisherman;
        anim.fps = 2f;

        playerGo.AddComponent<MapWasdZoneClamp>();

        var nav = playerGo.AddComponent<MapNavAgent>();
        nav.gridCols = 80;
        nav.gridRows = 45;
        nav.moveSpeed = SharedSpeed;

        // No PersistentPlayer — each scene creates its own player with
        // identical components (walk anim, nav, zone clamp) for consistency.

        // ── GameManager ───────────────────────────────────────────────
        var mgrGo = new GameObject("GameManager");
        var manager = mgrGo.AddComponent<WorldMapManager>();
        manager.playerSpeed = SharedSpeed;

        Transform playerT = playerGo.transform;

        // ── Door triggers ─────────────────────────────────────────────
        AddDoor("Door_LeftHouse", playerT, circle, -4.0f, 0.4f, "ApartmentInterior", "ENTER");
        AddDoor("Door_CentreHouse", playerT, circle, 0.2f, 1.2f, "PizzaInterior", "ENTER");
        AddDoor("Door_RightHouse", playerT, circle, 4.3f, 0.2f, "OfficeInterior", "ENTER");

        // ── Canvas ────────────────────────────────────────────────────
        var canvasGo = new GameObject("Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        var canvasRT = canvasGo.GetComponent<RectTransform>();

        AddPathTrigger("Path_World2", playerT, canvasRT, -6.5f, -3.8f,
            MapPathTrigger.TriggerType.LoadScene, "WorldMap2", "WORLD 2", Hex("#2BB3A3"));
        AddPathTrigger("Path_Locked", playerT, canvasRT, 6.5f, -3.8f,
            MapPathTrigger.TriggerType.LockedPopup, "", "LOCKED", Hex("#E74C3C"));

        var (dialogPanel, advBtn, nameText, bodyText, hintText,
             choicePanel, acceptBtn, acceptTxt, declineBtn, declineTxt)
            = BuildDialoguePanel(canvasRT);

        BuildMapHint(canvasRT);
        var mapPanel = BuildWorldMapPanel(canvasRT, manager);
        var lockedPopup = BuildLockedPopup(canvasRT);

        var rpt = GameObject.Find("Path_Locked")?.GetComponent<MapPathTrigger>();
        if (rpt != null) rpt.lockedPopup = lockedPopup;

        var czLeft = new GameObject("CornerZone_World2"); czLeft.AddComponent<MapCornerZone>();
        var czlC = czLeft.GetComponent<MapCornerZone>();
        czlC.player = playerT; czlC.corner = MapCornerZone.Corner.BottomLeft;
        czlC.action = MapCornerZone.ZoneAction.LoadScene; czlC.targetScene = "WorldMap2"; czlC.zonePixels = 200;

        var czRight = new GameObject("CornerZone_Locked"); czRight.AddComponent<MapCornerZone>();
        var czrC = czRight.GetComponent<MapCornerZone>();
        czrC.player = playerT; czrC.corner = MapCornerZone.Corner.BottomRight;
        czrC.action = MapCornerZone.ZoneAction.LockedPopup;
        czrC.lockedPopup = lockedPopup; czrC.zonePixels = 200; czrC.popupDuration = 2.8f;

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
        manager.boundsX = new Vector2(-7.8f, 7.8f);
        manager.boundsY = new Vector2(-4.0f, 4.3f);

        UnityEventTools.AddPersistentListener(advBtn.onClick, manager.AdvanceDialogue);
        UnityEventTools.AddPersistentListener(acceptBtn.onClick, manager.OnAcceptHelp);
        UnityEventTools.AddPersistentListener(declineBtn.onClick, manager.OnDeclineHelp);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuild(ScenePath);
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log($"[WorldMapBuilder] Built → {ScenePath}");
    }

    // ── Door helper ───────────────────────────────────────────────────
    static void AddDoor(string name, Transform player, Sprite circle,
        float cx, float cy, string scene, string label)
    {
        var go = new GameObject(name); go.transform.position = new Vector3(cx, cy, 0f);
        var dt = go.AddComponent<MapDoorTrigger>();
        dt.player = player; dt.targetScene = scene; dt.triggerRadius = 0.55f; dt.promptProximity = 1.8f;

        var prompt = new GameObject("Prompt"); prompt.transform.SetParent(go.transform, false);
        prompt.transform.localPosition = new Vector3(0f, 0.9f, -0.5f);

        var badge = new GameObject("Badge"); badge.transform.SetParent(prompt.transform, false);
        badge.transform.localScale = new Vector3(0.012f, 0.012f, 1f);
        var bc = badge.AddComponent<Canvas>(); bc.renderMode = RenderMode.WorldSpace; bc.sortingOrder = 30;
        badge.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 70);

        MakeImg(badge.transform, BadgeAccent, new Vector2(-5, -5), new Vector2(5, 5));
        MakeImg(badge.transform, new Color(0.08f, 0.10f, 0.18f, 0.96f), Vector2.zero, Vector2.zero);
        MakeTMP(badge.transform, label, 32);

        dt.prompt = prompt; prompt.SetActive(false);
    }

    // ── Path trigger helper ───────────────────────────────────────────
    static void AddPathTrigger(string name, Transform player, RectTransform canvasRT,
        float cx, float cy, MapPathTrigger.TriggerType type,
        string targetScene, string badgeLabel, Color accentCol)
    {
        var go = new GameObject(name); go.transform.position = new Vector3(cx, cy, 0f);
        var pt = go.AddComponent<MapPathTrigger>();
        pt.player = player; pt.type = type; pt.targetScene = targetScene;
        pt.promptProximity = 2.0f; pt.triggerRadius = 1.0f;

        var prompt = new GameObject("Prompt"); prompt.transform.SetParent(go.transform, false);
        prompt.transform.localPosition = new Vector3(0f, 1.0f, -0.5f);

        var badge = new GameObject("Badge"); badge.transform.SetParent(prompt.transform, false);
        badge.transform.localScale = new Vector3(0.012f, 0.012f, 1f);
        var bc = badge.AddComponent<Canvas>(); bc.renderMode = RenderMode.WorldSpace; bc.sortingOrder = 30;
        badge.GetComponent<RectTransform>().sizeDelta = new Vector2(240, 75);

        MakeImg(badge.transform, accentCol, new Vector2(-6, -6), new Vector2(6, 6));
        MakeImg(badge.transform, new Color(0.08f, 0.10f, 0.18f, 0.96f), Vector2.zero, Vector2.zero);
        MakeTMP(badge.transform, badgeLabel, 34);

        pt.prompt = prompt; prompt.SetActive(false);
    }

    // ── UI helpers ────────────────────────────────────────────────────
    static void MakeImg(Transform p, Color c, Vector2 oMin, Vector2 oMax)
    {
        var g = new GameObject("I", typeof(RectTransform)); g.transform.SetParent(p, false);
        var img = g.AddComponent<Image>(); img.color = c; img.raycastTarget = false;
        var r = g.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = oMin; r.offsetMax = oMax;
    }

    static void MakeTMP(Transform p, string text, int size)
    {
        var g = new GameObject("Label", typeof(RectTransform)); g.transform.SetParent(p, false);
        var t = g.AddComponent<TMPro.TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.color = Color.white;
        t.fontStyle = TMPro.FontStyles.Bold; t.alignment = TMPro.TextAlignmentOptions.Center;
        t.raycastTarget = false;
        var r = g.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(8, 0); r.offsetMax = new Vector2(-8, 0);
    }

    static GameObject BuildLockedPopup(RectTransform canvasRT)
    {
        var ov = UImg(canvasRT, "LockedPopup", new Color(0, 0, 0, 0)); Stretch(ov.rectTransform); ov.raycastTarget = false;
        var card = UImg(ov.rectTransform, "Card", new Color(0.08f, 0.10f, 0.20f, 0.93f));
        var crt = card.rectTransform; crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.62f);
        crt.pivot = new Vector2(0.5f, 0.5f); crt.sizeDelta = new Vector2(740, 150);
        var brd = UImg(crt, "Border", Hex("#E74C3C")); var brt = brd.rectTransform;
        brt.anchorMin = new Vector2(0, 1); brt.anchorMax = new Vector2(1, 1); brt.pivot = new Vector2(0.5f, 1); brt.sizeDelta = new Vector2(0, 6);
        var txt = UTxt(crt, "Msg", "You haven't unlocked this area yet!\nExplore the neighbourhood first.", 30, Color.white, TextAlignmentOptions.Center);
        txt.textWrappingMode = TextWrappingModes.Normal; Stretch(txt.rectTransform);
        ov.gameObject.SetActive(false); return ov.gameObject;
    }

    static (GameObject panel, Button adv, TMP_Text name, TMP_Text body, TMP_Text hint,
            GameObject choicePanel, Button acceptBtn, TMP_Text acceptTxt,
            Button declineBtn, TMP_Text declineTxt)
        BuildDialoguePanel(RectTransform canvasRT)
    {
        var advGo = new GameObject("AdvanceBtn", typeof(RectTransform)); advGo.transform.SetParent(canvasRT, false);
        Stretch(advGo.GetComponent<RectTransform>());
        var advImg = advGo.AddComponent<Image>(); advImg.color = new Color(0, 0, 0, 0);
        var advBtn = advGo.AddComponent<Button>(); advBtn.targetGraphic = advImg;

        var panel = new GameObject("DialoguePanel", typeof(RectTransform)); panel.transform.SetParent(canvasRT, false);
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = new Vector2(0, 0); prt.anchorMax = new Vector2(1, 0); prt.pivot = new Vector2(0.5f, 0);
        prt.sizeDelta = new Vector2(0, 220); prt.anchoredPosition = Vector2.zero;
        panel.AddComponent<Image>().color = new Color(0.08f, 0.15f, 0.28f, 0.95f);

        var nameBar = UImg(prt, "NameBar", Hex("#1A3A6B")); var nbrrt = nameBar.rectTransform;
        nbrrt.anchorMin = new Vector2(0, 1); nbrrt.anchorMax = new Vector2(0.42f, 1); nbrrt.pivot = new Vector2(0, 1); nbrrt.sizeDelta = new Vector2(0, 44);
        var nameText = UTxt(nbrrt, "Name", "Speaker", 30, NameCol, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        var ntrt = nameText.rectTransform; ntrt.anchorMin = Vector2.zero; ntrt.anchorMax = Vector2.one; ntrt.offsetMin = new Vector2(18, 0); ntrt.offsetMax = Vector2.zero;

        var bodyText = UTxt(prt, "Body", "...", 26, Color.white, TextAlignmentOptions.TopLeft);
        bodyText.textWrappingMode = TextWrappingModes.Normal;
        var brrt = bodyText.rectTransform; brrt.anchorMin = Vector2.zero; brrt.anchorMax = Vector2.one; brrt.offsetMin = new Vector2(24, 48); brrt.offsetMax = new Vector2(-24, -50);

        var hint = UTxt(prt, "Hint", "Click to continue…", 20, new Color(0.7f, 0.7f, 0.9f), TextAlignmentOptions.MidlineRight);
        var hrrt = hint.rectTransform; hrrt.anchorMin = new Vector2(0, 0); hrrt.anchorMax = new Vector2(1, 0);
        hrrt.pivot = new Vector2(0.5f, 0); hrrt.sizeDelta = new Vector2(0, 36); hrrt.anchoredPosition = new Vector2(0, 8);

        var cpGo = new GameObject("ChoicePanel", typeof(RectTransform)); cpGo.transform.SetParent(prt, false);
        var cprt = cpGo.GetComponent<RectTransform>();
        cprt.anchorMin = new Vector2(0.5f, 0); cprt.anchorMax = new Vector2(0.5f, 0); cprt.pivot = new Vector2(0.5f, 0);
        cprt.sizeDelta = new Vector2(800, 72); cprt.anchoredPosition = new Vector2(0, 8);

        var acceptGo = UBtn(cprt, "AcceptBtn", "Let's go!", 26, AcceptCol, Color.white);
        var declineGo = UBtn(cprt, "DeclineBtn", "Maybe later", 26, DeclineCol, Color.white);
        var art = acceptGo.GetComponent<RectTransform>(); art.anchorMin = new Vector2(0, 0); art.anchorMax = new Vector2(0.48f, 1); art.offsetMin = art.offsetMax = Vector2.zero;
        var drt = declineGo.GetComponent<RectTransform>(); drt.anchorMin = new Vector2(0.52f, 0); drt.anchorMax = new Vector2(1, 1); drt.offsetMin = drt.offsetMax = Vector2.zero;

        panel.SetActive(false);
        return (panel, advBtn, nameText, bodyText, hint, cpGo,
                acceptGo.GetComponent<Button>(), acceptGo.transform.Find("Label").GetComponent<TMP_Text>(),
                declineGo.GetComponent<Button>(), declineGo.transform.Find("Label").GetComponent<TMP_Text>());
    }

    static void BuildMapHint(RectTransform canvasRT)
    {
        var go = new GameObject("MapHint", typeof(RectTransform)); go.transform.SetParent(canvasRT, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(1, 1);
        rt.sizeDelta = new Vector2(230, 44); rt.anchoredPosition = new Vector2(-24, -24);
        go.AddComponent<Image>().color = new Color(0, 0, 0, 0.45f);
        Stretch(UTxt(rt, "T", "[ M ]  World Map", 22, new Color(0.9f, 0.9f, 1f), TextAlignmentOptions.Center).rectTransform);
    }

    static GameObject BuildWorldMapPanel(RectTransform canvasRT, WorldMapManager manager)
    {
        var ov = UImg(canvasRT, "WorldMapOverlay", new Color(0, 0, 0, 0.88f)); Stretch(ov.rectTransform); ov.raycastTarget = true;
        var card = UImg(ov.rectTransform, "MapCard", new Color(0, 0, 0, 0));
        var crt = card.rectTransform; crt.anchorMin = new Vector2(0.05f, 0.06f); crt.anchorMax = new Vector2(0.95f, 0.94f); crt.offsetMin = crt.offsetMax = Vector2.zero;
        Sprite mapSpr = FindSprite("map");
        if (mapSpr != null) { var mi = UImg(crt, "MapImage", Color.white); mi.sprite = mapSpr; mi.type = Image.Type.Simple; mi.preserveAspect = true; mi.raycastTarget = false; Stretch(mi.rectTransform); }
        else { card.color = MapBg; Debug.LogWarning("[WorldMapBuilder] 'map' sprite not found."); }
        var titleBg = UImg(ov.rectTransform, "TitleBar", new Color(0.05f, 0.08f, 0.15f, 0.92f));
        var tbr = titleBg.rectTransform; tbr.anchorMin = new Vector2(0, 1); tbr.anchorMax = new Vector2(1, 1); tbr.pivot = new Vector2(0.5f, 1); tbr.sizeDelta = new Vector2(0, 64); tbr.anchoredPosition = Vector2.zero;
        Stretch(UTxt(tbr, "Title", "PHISHERMAN  WORLD MAP", 36, Color.white, TextAlignmentOptions.Center, FontStyles.Bold).rectTransform);
        var closeBtn = UBtn(ov.rectTransform, "CloseBtn", "X  Close Map  ( M )", 26, new Color(0.18f, 0.28f, 0.45f, 0.95f), Color.white);
        var cbrt = closeBtn.GetComponent<RectTransform>(); cbrt.anchorMin = new Vector2(0.5f, 0); cbrt.anchorMax = new Vector2(0.5f, 0);
        cbrt.pivot = new Vector2(0.5f, 0); cbrt.sizeDelta = new Vector2(360, 56); cbrt.anchoredPosition = new Vector2(0, 12);
        UnityEventTools.AddPersistentListener(closeBtn.GetComponent<Button>().onClick, manager.CloseMap);
        ov.gameObject.SetActive(false); return ov.gameObject;
    }

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