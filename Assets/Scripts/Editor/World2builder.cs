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
/// Builds the WorldMap2 scene — the market/harbour world (World 2).
/// Run via: Phisherman > Build World 2 Scene
///
/// For now this is a stub: background image + player walking.
/// Buildings, NPCs and door triggers come in a later pass once
/// Firefly art for World 2 interiors is ready.
///
/// Walking left at the edge of WorldMap2 returns to WorldMap (World 1).
/// </summary>
public static class World2Builder
{
    private const string ScenesDir = "Assets/Scenes";
    private const string ScenePath = "Assets/Scenes/WorldMap2.unity";

    private const float OrthoSize = 4.5f;
    private const float AspectW = 16f / 9f;

    private static readonly Color AcceptCol = Hex("#2ECC71");
    private static readonly Color DeclineCol = Hex("#E74C3C");
    private static readonly Color NameCol = Hex("#FFD93D");
    private static readonly Color MapBg = Hex("#1A6E9E");

    [MenuItem("Phisherman/Build World 2 Scene")]
    public static void Build()
    {
        if (!Directory.Exists(ScenesDir)) Directory.CreateDirectory(ScenesDir);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Sprite phisherman = FindSprite("phisherman");
        LogFound("phisherman", phisherman);

        // ── Camera ───────────────────────────────────────────────────
        var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>(); camGo.AddComponent<AudioListener>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Hex("#6BB8D4");
        cam.orthographic = true;
        cam.orthographicSize = OrthoSize;
        camGo.transform.position = new Vector3(0, 0, -10);

        // ── EventSystem ───────────────────────────────────────────────
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>(); es.AddComponent<StandaloneInputModule>();

        // ── Background ────────────────────────────────────────────────
        Sprite bgArt = FindSprite("world_stage_2_background");
        if (bgArt != null)
        {
            var bgGo = new GameObject("WorldBackground");
            var bgSR = bgGo.AddComponent<SpriteRenderer>();
            bgSR.sprite = bgArt;
            bgSR.color = Color.white;
            bgSR.sortingOrder = -50;
            float camH = OrthoSize * 2f;
            float camW = camH * AspectW;
            var bnds = bgArt.bounds;
            bgGo.transform.localScale = new Vector3(camW / bnds.size.x, camH / bnds.size.y, 1f);
            bgGo.transform.position = Vector3.zero;
        }
        else
        {
            Debug.LogWarning("[World2Builder] 'world_stage_2_background' not found — import it with Texture Type = Sprite (2D and UI).");
        }

        // ── Player ────────────────────────────────────────────────────
        var playerGo = new GameObject("Phisherman");
        playerGo.transform.position = new Vector3(0f, -1.5f, 0f);
        var playerSR = playerGo.AddComponent<SpriteRenderer>();
        playerSR.sprite = phisherman;
        playerSR.color = Color.white;
        playerSR.sortingOrder = 10;
        playerGo.transform.localScale = new Vector3(0.55f, 0.55f, 1f);

        // ── GameManager ───────────────────────────────────────────────
        var mgrGo = new GameObject("GameManager");
        var manager = mgrGo.AddComponent<WorldMapManager>();

        Transform playerT = playerGo.transform;

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

        // ── Left path → back to World 1 ──────────────────────────────
        AddPathTrigger("Path_World1", playerT, canvasRT, -5.0f, -3.6f,
            MapPathTrigger.TriggerType.LoadScene, "WorldMap", "WORLD 1", Hex("#2BB3A3"));

        // ── Right path → locked popup ─────────────────────────────────
        AddPathTrigger("Path_Locked", playerT, canvasRT, 5.0f, -3.6f,
            MapPathTrigger.TriggerType.LockedPopup, "", "LOCKED", Hex("#E74C3C"));

        var (dialogPanel, advBtn, nameText, bodyText, hintText,
             choicePanel, acceptBtn, acceptTxt, declineBtn, declineTxt)
            = BuildDialoguePanel(canvasRT);

        BuildMapHint(canvasRT);
        var mapPanel = BuildWorldMapPanel(canvasRT, manager);

        // ── "Back to World 1" hint in top-left ────────────────────────
        var hintGo = new GameObject("BackHint", typeof(RectTransform));
        hintGo.transform.SetParent(canvasRT, false);
        var hrt = hintGo.GetComponent<RectTransform>();
        hrt.anchorMin = new Vector2(0, 1); hrt.anchorMax = new Vector2(0, 1);
        hrt.pivot = new Vector2(0, 1);
        hrt.sizeDelta = new Vector2(300, 44); hrt.anchoredPosition = new Vector2(24, -24);
        hintGo.AddComponent<Image>().color = new Color(0, 0, 0, 0.45f);
        var ht = UTxt(hrt, "T", "Walk left to return to World 1", 20,
            new Color(0.9f, 0.9f, 1f), TextAlignmentOptions.Center);
        Stretch(ht.rectTransform);

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
        Debug.Log($"[World2Builder] Built → {ScenePath}");
    }

    // =========================================================================
    // Path trigger helper
    // =========================================================================

    static void AddPathTrigger(string name, Transform player, RectTransform canvasRT,
        float cx, float cy, MapPathTrigger.TriggerType type,
        string targetScene, string badgeLabel, Color accentCol)
    {
        var go = new GameObject(name);
        go.transform.position = new Vector3(cx, cy, 0f);

        var pt = go.AddComponent<MapPathTrigger>();
        pt.player = player;
        pt.type = type;
        pt.targetScene = targetScene;
        pt.promptProximity = 2.2f;
        pt.triggerRadius = 1.0f;

        // Floating badge
        var promptGo = new GameObject("Prompt");
        promptGo.transform.SetParent(go.transform, false);
        promptGo.transform.localPosition = new Vector3(0f, 1.1f, -0.5f);

        var bgGo = new GameObject("Badge");
        bgGo.transform.SetParent(promptGo.transform, false);
        bgGo.transform.localPosition = Vector3.zero;
        bgGo.transform.localScale = new Vector3(0.012f, 0.012f, 1f);
        var bgCanvas = bgGo.AddComponent<Canvas>();
        bgCanvas.renderMode = RenderMode.WorldSpace;
        bgCanvas.sortingOrder = 30;
        bgGo.GetComponent<RectTransform>().sizeDelta = new Vector2(260, 80);

        var borderGo = new GameObject("Border", typeof(RectTransform));
        borderGo.transform.SetParent(bgGo.transform, false);
        var borderImg = borderGo.AddComponent<Image>();
        borderImg.color = accentCol; borderImg.raycastTarget = false;
        var brt = borderGo.GetComponent<RectTransform>();
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
        brt.offsetMin = new Vector2(-6, -6); brt.offsetMax = new Vector2(6, 6);

        var fillGo = new GameObject("Fill", typeof(RectTransform));
        fillGo.transform.SetParent(bgGo.transform, false);
        var fillImg = fillGo.AddComponent<Image>();
        fillImg.color = new Color(0.08f, 0.10f, 0.18f, 0.96f); fillImg.raycastTarget = false;
        var frt = fillGo.GetComponent<RectTransform>();
        frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
        frt.offsetMin = frt.offsetMax = Vector2.zero;

        var txtGo = new GameObject("Label", typeof(RectTransform));
        txtGo.transform.SetParent(bgGo.transform, false);
        var tmp = txtGo.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text = badgeLabel; tmp.fontSize = 36; tmp.color = Color.white;
        tmp.fontStyle = TMPro.FontStyles.Bold;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        var trt = txtGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(8, 0); trt.offsetMax = new Vector2(-8, 0);

        pt.prompt = promptGo;
        promptGo.SetActive(false);

        if (type == MapPathTrigger.TriggerType.LockedPopup)
            pt.lockedPopup = BuildLockedPopup(canvasRT);
    }

    // =========================================================================
    // Locked popup
    // =========================================================================

    static GameObject BuildLockedPopup(RectTransform canvasRT)
    {
        var ov = UImg(canvasRT, "LockedPopup", new Color(0f, 0f, 0f, 0f));
        Stretch(ov.rectTransform); ov.raycastTarget = false;

        var card = UImg(ov.rectTransform, "Card", new Color(0.08f, 0.10f, 0.20f, 0.93f));
        var crt = card.rectTransform;
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(700, 140);

        var border = UImg(crt, "Border", Hex("#2BB3A3"));
        var brt = border.rectTransform;
        brt.anchorMin = new Vector2(0, 1); brt.anchorMax = new Vector2(1, 1);
        brt.pivot = new Vector2(0.5f, 1); brt.sizeDelta = new Vector2(0, 5);

        var txt = UTxt(crt, "Msg",
            "You haven't unlocked this area yet!\nExplore the town first.",
            32, Color.white, TextAlignmentOptions.Center);
        txt.textWrappingMode = TextWrappingModes.Normal;
        Stretch(txt.rectTransform);

        ov.gameObject.SetActive(false);
        return ov.gameObject;
    }

    // =========================================================================
    // Dialogue panel (copy from WorldMapBuilder — self-contained)
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

    static void BuildMapHint(RectTransform canvasRT)
    {
        var go = new GameObject("MapHint", typeof(RectTransform));
        go.transform.SetParent(canvasRT, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1, 1); rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(1, 1);
        rt.sizeDelta = new Vector2(230, 44); rt.anchoredPosition = new Vector2(-24, -24);
        go.AddComponent<Image>().color = new Color(0, 0, 0, 0.45f);
        var txt = UTxt(rt, "T", "[ M ]  World Map", 22,
            new Color(0.9f, 0.9f, 1f), TextAlignmentOptions.Center);
        Stretch(txt.rectTransform);
    }

    static GameObject BuildWorldMapPanel(RectTransform canvasRT, WorldMapManager manager)
    {
        var ov = UImg(canvasRT, "WorldMapOverlay", new Color(0, 0, 0, 0.88f));
        Stretch(ov.rectTransform); ov.raycastTarget = true;

        var card = UImg(ov.rectTransform, "MapCard", new Color(0, 0, 0, 0));
        var crt = card.rectTransform;
        crt.anchorMin = new Vector2(0.05f, 0.06f);
        crt.anchorMax = new Vector2(0.95f, 0.94f);
        crt.offsetMin = crt.offsetMax = Vector2.zero;

        Sprite mapSpr = FindSprite("map");
        if (mapSpr != null)
        {
            var mapImg = UImg(crt, "MapImage", Color.white);
            mapImg.sprite = mapSpr;
            mapImg.type = Image.Type.Simple;
            mapImg.preserveAspect = true;
            mapImg.raycastTarget = false;
            Stretch(mapImg.rectTransform);
        }
        else
        {
            card.color = MapBg;
            Debug.LogWarning("[World2Builder] 'map' sprite not found.");
        }

        var titleBg = UImg(ov.rectTransform, "TitleBar", new Color(0.05f, 0.08f, 0.15f, 0.92f));
        var tbr = titleBg.rectTransform;
        tbr.anchorMin = new Vector2(0, 1); tbr.anchorMax = new Vector2(1, 1);
        tbr.pivot = new Vector2(0.5f, 1);
        tbr.sizeDelta = new Vector2(0, 64); tbr.anchoredPosition = Vector2.zero;
        var titleTxt = UTxt(tbr, "Title", "PHISHERMAN — WORLD MAP", 36, Color.white,
            TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(titleTxt.rectTransform);

        var closeBtn = UBtn(ov.rectTransform, "CloseBtn", "X  Close Map  ( M )", 26,
            new Color(0.18f, 0.28f, 0.45f, 0.95f), Color.white);
        var cbrt = closeBtn.GetComponent<RectTransform>();
        cbrt.anchorMin = new Vector2(0.5f, 0); cbrt.anchorMax = new Vector2(0.5f, 0);
        cbrt.pivot = new Vector2(0.5f, 0);
        cbrt.sizeDelta = new Vector2(360, 56); cbrt.anchoredPosition = new Vector2(0, 12);
        UnityEventTools.AddPersistentListener(closeBtn.GetComponent<Button>().onClick, manager.CloseMap);

        ov.gameObject.SetActive(false);
        return ov.gameObject;
    }

    // =========================================================================
    // Utilities (self-contained copy)
    // =========================================================================

    static Sprite FindSprite(string name)
    {
        foreach (var g in AssetDatabase.FindAssets(name + " t:Sprite"))
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            if (System.IO.Path.GetFileNameWithoutExtension(p).ToLower() == name.ToLower())
            { var s = AssetDatabase.LoadAssetAtPath<Sprite>(p); if (s != null) return s; }
        }
        return null;
    }

    static void LogFound(string n, Sprite s) =>
        Debug.Log($"[World2Builder] {n}: " + (s != null ? "✓" : "✗ not found"));

    static Image UImg(Transform p, string n, Color c)
    {
        var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false);
        var img = go.AddComponent<Image>(); img.color = c; return img;
    }

    static TMP_Text UTxt(Transform p, string n, string text, int size, Color col,
        TextAlignmentOptions align, FontStyles style = FontStyles.Normal)
    {
        var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.color = col;
        t.alignment = align; t.fontStyle = style; t.raycastTarget = false; return t;
    }

    static GameObject UBtn(Transform p, string n, string label, int size, Color bg, Color tc)
    {
        var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false);
        var img = go.AddComponent<Image>(); img.color = bg;
        var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        var t = UTxt(go.transform, "Label", label, size, tc,
            TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(t.rectTransform); return go;
    }

    static void Stretch(RectTransform r)
    { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }

    static Color Hex(string h) =>
        ColorUtility.TryParseHtmlString(h, out var c) ? c : Color.magenta;

    static void AddToBuild(string path)
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(s => s.path == path))
        { scenes.Add(new EditorBuildSettingsScene(path, true)); EditorBuildSettings.scenes = scenes.ToArray(); }
    }
}