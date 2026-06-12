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
/// Builds the WorldMap scene from scratch.
/// Run via:  Phisherman ▸ Build World Map Scene
///
/// Club Penguin town-square layout:
///   - Large open cobblestone plaza fills bottom 70% (walkable)
///   - Buildings arc across the TOP, pushed to the back wall
///   - Raised stone ledge separates plaza from buildings
///   - Sky + distant hills fill behind the buildings
///   - Click-to-move preserved; bounds keep player on the plaza
/// </summary>
public static class WorldMapBuilder
{
    private const string ScenesDir = "Assets/Scenes";
    private const string ScenePath = "Assets/Scenes/WorldMap.unity";

    private const float OrthoSize = 4.5f;
    private const float AspectW = 16f / 9f;
    private const float HalfW = OrthoSize * AspectW;

    // Y positions
    private const float LedgeTopY = 1.40f;
    private const float LedgeBotY = 0.85f;
    private const float LedgeMidY = 1.125f;
    private const float NpcY = 0.55f;
    private const float PlayerStartY = -1.8f;

    // Building arc
    private static readonly float[] BuildingX = { -5.8f, -2.9f, 0f, 2.9f, 5.8f };
    private static readonly float[] BuildingY = { 1.85f, 2.05f, 2.20f, 2.05f, 1.85f };
    private static readonly float[] BuildingScale = { 0.88f, 0.95f, 1.0f, 0.95f, 0.88f };

    // Palette
    private static readonly Color SkyTop = Hex("#4FA8D9");
    private static readonly Color SkyBot = Hex("#87CEEB");
    private static readonly Color HillFar = Hex("#5A9E38");
    private static readonly Color HillMid = Hex("#4D8B2F");
    private static readonly Color LedgeTop = Hex("#9E9E8A");
    private static readonly Color LedgeFront = Hex("#7A7A68");
    private static readonly Color LedgeShadow = Hex("#5C5C4E");
    private static readonly Color PlazaMain = Hex("#C4B89A");
    private static readonly Color PlazaDark = Hex("#B0A488");
    private static readonly Color PlazaEdge = Hex("#8C7E60");
    private static readonly Color FgStrip = Hex("#3A7D1E");
    private static readonly Color AcceptCol = Hex("#2ECC71");
    private static readonly Color DeclineCol = Hex("#E74C3C");
    private static readonly Color NameCol = Hex("#FFD93D");
    private static readonly Color MapBg = Hex("#1A6E9E");
    private static readonly Color IslandGreen = Hex("#4A9B2E");
    private static readonly Color IslandSand = Hex("#D4A853");

    private static readonly (Color wall, Color roof, Color sign, string label)[] BuildingDefs =
    {
        (Hex("#F4A7B9"), Hex("#C0392B"), Hex("#E91E8C"), "Grandma's"),
        (Hex("#FDEAA7"), Hex("#E67E22"), Hex("#F39C12"), "Pizza Shop"),
        (Hex("#B8D4E8"), Hex("#2980B9"), Hex("#1ABC9C"), "Grandpa's"),
        (Hex("#C8E6C9"), Hex("#27AE60"), Hex("#2ECC71"), "Mrs. Patel's"),
        (Hex("#D7BDE2"), Hex("#8E44AD"), Hex("#9B59B6"), "Uncle Rajan's"),
    };

    [MenuItem("Phisherman/Build World Map Scene")]
    public static void Build()
    {
        if (!Directory.Exists(ScenesDir)) Directory.CreateDirectory(ScenesDir);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Sprite white = EnsureWhitePixel();
        Sprite phisherman = FindSprite("phisherman");
        Sprite circle = GetCircle();
        LogFound("phisherman", phisherman);

        // Camera
        var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>(); camGo.AddComponent<AudioListener>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = SkyTop;
        cam.orthographic = true; cam.orthographicSize = OrthoSize;
        camGo.transform.position = new Vector3(0, 0, -10);

        // EventSystem
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>(); es.AddComponent<StandaloneInputModule>();

        BuildBackground(white);

        var mgrGo = new GameObject("GameManager");
        var manager = mgrGo.AddComponent<WorldMapManager>();

        var npcTransforms = new Transform[5];
        var exclamationMarks = new GameObject[5];
        for (int i = 0; i < 5; i++)
        {
            var (npcTr, exclam) = BuildBuilding(i, white, phisherman, circle);
            npcTransforms[i] = npcTr;
            exclamationMarks[i] = exclam;
        }

        // Player
        var playerGo = new GameObject("Phisherman");
        playerGo.transform.position = new Vector3(0f, PlayerStartY, 0f);
        var playerSR = playerGo.AddComponent<SpriteRenderer>();
        playerSR.sprite = phisherman != null ? phisherman : white;
        playerSR.color = Color.white; playerSR.sortingOrder = 10;
        playerGo.transform.localScale = new Vector3(0.55f, 0.55f, 1f);

        // Canvas
        var canvasGo = new GameObject("Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        var canvasRT = canvasGo.GetComponent<RectTransform>();

        var (dialogPanel, advBtn, nameText, bodyText, hintText,
             choicePanel, acceptBtn, acceptTxt, declineBtn, declineTxt)
            = BuildDialoguePanel(canvasRT);

        BuildMapHint(canvasRT);
        var mapPanel = BuildWorldMapPanel(canvasRT, manager);

        manager.playerTransform = playerGo.transform;
        manager.playerRenderer = playerSR;
        manager.npcTransforms = npcTransforms;
        manager.exclamationMarks = exclamationMarks;
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

        // UPDATED: Tighter bounds mapped precisely to the cobblestone plaza floor
        manager.boundsX = new Vector2(-7.5f, 7.5f);
        manager.boundsY = new Vector2(-4.0f, 0.5f);

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
    // Background — Club Penguin bowl
    // =========================================================================

    static void BuildBackground(Sprite white)
    {
        float fullW = HalfW * 2f + 2f;

        // Sky
        SR("Sky_Bot", white, SkyBot, new Vector3(0, 4f, 0), new Vector3(fullW, 12f, 1), -30);
        SR("Sky_Top", white, SkyTop, new Vector3(0, 6f, 0), new Vector3(fullW, 6f, 1), -29);

        // Distant hills behind buildings (3 bumps)
        float[] hx = { -4.5f, 0f, 4.5f };
        float[] hw = { 5.5f, 6.2f, 5.5f };
        for (int h = 0; h < 3; h++)
        {
            SR("Hill_" + h, white, HillFar, new Vector3(hx[h], 1.9f, 0), new Vector3(hw[h], 2.4f, 1), -22);
            SR("HillFg_" + h, white, HillMid, new Vector3(hx[h], 1.3f, 0), new Vector3(hw[h] * 0.8f, 1.0f, 1), -21);
        }

        // Raised ledge — top surface (lighter, seen from slight above)
        SR("LedgeTop", white, LedgeTop, new Vector3(0, LedgeMidY + 0.14f, 0), new Vector3(fullW, 0.55f, 1), -15);
        // Ledge front face (dark vertical face)
        SR("LedgeFront", white, LedgeFront, new Vector3(0, LedgeBotY - 0.04f, 0), new Vector3(fullW, 0.33f, 1), -14);
        // Ledge shadow at base
        SR("LedgeShadow", white, LedgeShadow, new Vector3(0, LedgeBotY - 0.22f, 0), new Vector3(fullW, 0.12f, 1), -13);

        // Large open plaza — main walkable floor
        SR("Plaza", white, PlazaMain, new Vector3(0, -1.5f, 0), new Vector3(fullW, 7.0f, 1), -12);
        // Subtle centre path darker strip (like CP's curved dirt path)
        SR("PlazaMid", white, PlazaDark, new Vector3(0, -1.8f, 0), new Vector3(fullW * 0.60f, 2.2f, 1), -11);

        // Scattered paving stones
        float[] stoneX = { -5.5f, -2.8f, 0.6f, 3.5f, -4.0f, 1.8f, -1.2f };
        float[] stoneY = { -0.4f, -1.3f, -0.8f, -2.0f, -2.6f, -2.2f, -1.6f };
        for (int s = 0; s < stoneX.Length; s++)
            SR("Stone_" + s, white, PlazaDark,
                new Vector3(stoneX[s], stoneY[s], 0), new Vector3(0.95f, 0.46f, 1), -10);

        // Plaza front edge strip
        SR("PlazaEdge", white, PlazaEdge,
            new Vector3(0, -OrthoSize + 0.6f, 0), new Vector3(fullW, 0.75f, 1), -9);

        // Dark green foreground strip (player's feet area, very bottom)
        SR("FgStrip", white, FgStrip,
            new Vector3(0, -OrthoSize - 0.3f, 0), new Vector3(fullW, 1.8f, 1), -8);
    }

    // =========================================================================
    // Building + NPC
    // =========================================================================

    static (Transform npcTr, GameObject exclam) BuildBuilding(
        int i, Sprite white, Sprite phisherman, Sprite circle)
    {
        var def = BuildingDefs[i];
        float bx = BuildingX[i];
        float by = BuildingY[i];
        float scl = BuildingScale[i];
        float bw = 1.6f * scl;
        float bh = 1.9f * scl;

        var bRoot = new GameObject("Building_" + i);
        bRoot.transform.position = new Vector3(bx, by, 0f);

        // Wall
        SR("Wall", white, def.wall, Vector3.zero, new Vector3(bw, bh, 1), 1, bRoot.transform);

        // Roof base + peak
        SR("RoofBase", white, def.roof,
            new Vector3(0, bh * 0.5f + 0.10f, 0), new Vector3(bw + 0.32f * scl, 0.32f * scl, 1), 2, bRoot.transform);
        SR("RoofPeak", white, def.roof,
            new Vector3(0, bh * 0.5f + 0.34f, 0), new Vector3(bw * 0.55f, 0.32f * scl, 1), 2, bRoot.transform);
        SR("RoofShadow", white, new Color(def.roof.r * 0.6f, def.roof.g * 0.6f, def.roof.b * 0.6f),
            new Vector3(0, bh * 0.5f + 0.04f, 0), new Vector3(bw + 0.36f * scl, 0.10f * scl, 1), 2, bRoot.transform);

        // Sign
        SR("Sign", white, def.sign,
            new Vector3(0, bh * 0.18f, 0), new Vector3(bw * 0.72f, 0.28f * scl, 1), 3, bRoot.transform);
        AddWorldText(def.label, new Vector3(bx, by + bh * 0.18f, -0.15f), 0.14f * scl, Color.white, 4);

        // Windows
        Color winCol = new Color(0.88f, 0.96f, 1.0f);
        Color frameCol = new Color(0.42f, 0.32f, 0.22f);
        float wx = bw * 0.26f, wy = bh * 0.08f, ws = 0.34f * scl;
        for (int w = -1; w <= 1; w += 2)
        {
            SR("Win_" + w, white, winCol, new Vector3(w * wx, wy, 0), new Vector3(ws, ws, 1), 3, bRoot.transform);
            SR("WinH_" + w, white, frameCol, new Vector3(w * wx, wy, 0), new Vector3(ws, 0.04f * scl, 1), 4, bRoot.transform);
            SR("WinV_" + w, white, frameCol, new Vector3(w * wx, wy, 0), new Vector3(0.04f * scl, ws, 1), 4, bRoot.transform);
        }

        // Door
        Color doorCol = new Color(0.32f, 0.20f, 0.10f);
        float dh = 0.56f * scl, dw = 0.34f * scl, dy = -bh * 0.33f;
        SR("Door", white, doorCol,
            new Vector3(0, dy, 0), new Vector3(dw, dh, 1), 3, bRoot.transform);
        SR("DoorArc", white, def.roof,
            new Vector3(0, dy + dh * 0.47f, 0), new Vector3(dw, dh * 0.28f, 1), 4, bRoot.transform);
        SR("Knob", white, new Color(0.88f, 0.78f, 0.12f),
            new Vector3(dw * 0.32f, dy, 0), new Vector3(0.07f * scl, 0.07f * scl, 1), 4, bRoot.transform);

        // Chimney on even buildings
        if (i % 2 == 0)
            SR("Chimney", white,
                new Color(def.roof.r * 0.75f, def.roof.g * 0.75f, def.roof.b * 0.75f),
                new Vector3(bw * 0.30f, bh * 0.54f, 0), new Vector3(0.20f * scl, 0.42f * scl, 1),
                2, bRoot.transform);

        // NPC sprite on the ledge in front of building
        var npcGo = new GameObject("NPC_" + i);
        npcGo.transform.position = new Vector3(bx, NpcY, -0.3f);
        var npcSR = npcGo.AddComponent<SpriteRenderer>();
        Color[] tints = {
            new Color(1.0f, 0.82f, 0.86f),
            new Color(1.0f, 0.88f, 0.60f),
            new Color(0.70f, 0.88f, 1.0f),
            new Color(0.72f, 1.0f, 0.76f),
            new Color(0.86f, 0.72f, 1.0f),
        };
        npcSR.sprite = phisherman != null ? phisherman : white;
        npcSR.color = tints[i]; npcSR.sortingOrder = 5;
        npcGo.transform.localScale = new Vector3(0.45f * scl, 0.45f * scl, 1f);
        npcGo.AddComponent<CircleCollider2D>().radius = 0.50f;
        npcGo.AddComponent<NPCMarker>().npcIndex = i;

        // Exclamation mark
        var exclamGo = new GameObject("Exclam_" + i);
        exclamGo.transform.position = new Vector3(bx, NpcY + 1.1f, -0.5f);
        var bgGo = new GameObject("BG"); bgGo.transform.SetParent(exclamGo.transform, false);
        var bgSR = bgGo.AddComponent<SpriteRenderer>();
        bgSR.sprite = circle != null ? circle : white;
        bgSR.color = new Color(1f, 0.88f, 0.10f); bgSR.sortingOrder = 15;
        bgGo.transform.localScale = Vector3.one * 0.36f;
        AddWorldText("!", exclamGo.transform.position + new Vector3(0, 0, -0.1f),
            0.34f, new Color(0.12f, 0.08f, 0.02f), 16);
        exclamGo.SetActive(false);

        return (npcGo.transform, exclamGo);
    }

    // =========================================================================
    // World-space text
    // =========================================================================

    static void AddWorldText(string text, Vector3 pos, float worldSize, Color col, int order)
    {
        string safeName = text.Length > 8 ? text.Substring(0, 8).Replace(" ", "") : text.Replace(" ", "");
        var go = new GameObject("WT_" + safeName);
        go.transform.position = pos;
        const float k = 0.012f;
        go.transform.localScale = Vector3.one * k;
        var c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace; c.sortingOrder = order;
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(220, 70);
        var tgo = new GameObject("T"); tgo.transform.SetParent(go.transform, false);
        var tmp = tgo.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = worldSize / k; tmp.color = col;
        tmp.fontStyle = FontStyles.Bold; tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        var rt = tgo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
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
    // M-key hint
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
        var txt = UTxt(rt, "T", "[ M ]  World Map", 22,
            new Color(0.9f, 0.9f, 1f), TextAlignmentOptions.Center);
        Stretch(txt.rectTransform);
    }

    // =========================================================================
    // World map overlay
    // =========================================================================

    static GameObject BuildWorldMapPanel(RectTransform canvasRT, WorldMapManager manager)
    {
        var ov = UImg(canvasRT, "WorldMapOverlay", new Color(0, 0, 0, 0.82f));
        Stretch(ov.rectTransform); ov.raycastTarget = true;

        var card = UImg(ov.rectTransform, "MapCard", MapBg);
        var crt = card.rectTransform;
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f); crt.sizeDelta = new Vector2(1280, 720);

        var title = UTxt(crt, "Title", "PHISHERMAN", 56, Color.white,
            TextAlignmentOptions.Center, FontStyles.Bold);
        var trt = title.rectTransform;
        trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1);
        trt.pivot = new Vector2(0.5f, 1);
        trt.sizeDelta = new Vector2(0, 80); trt.anchoredPosition = new Vector2(0, -10);

        var sea = UTxt(crt, "Sea", "THE PHISH BOWL SEA", 24,
            new Color(0.85f, 0.95f, 1f), TextAlignmentOptions.Center);
        sea.rectTransform.anchoredPosition = Vector2.zero;

        BuildIsland(crt, "Start", new Vector2(430, -200), 200, 75, IslandSand, IslandGreen, "START", false);
        BuildIsland(crt, "Messaging", new Vector2(-360, 130), 185, 70, IslandGreen, IslandGreen, "THE MESSAGING ISLE\n(Email / SMS)", true);
        BuildIsland(crt, "GlitchGrove", new Vector2(320, 150), 165, 68, IslandGreen, IslandGreen, "THE GLITCH GROVE\n(Social / Spam)", true);
        BuildIsland(crt, "Vortex", new Vector2(-370, -170), 135, 55,
            new Color(0.15f, 0.20f, 0.35f), IslandGreen, "VORTEX POINT\n(Voice)", true);
        BuildIslandMarker(crt, new Vector2(-360, 195), "WORLD 1\nActive", new Color(1f, 0.9f, 0.2f));

        // UPDATED: Fixed Unicode character here
        var closeBtn = UBtn(crt, "CloseBtn", "X  Close Map  (M)", 26,
            new Color(0.18f, 0.28f, 0.45f), Color.white);
        var cbrt = closeBtn.GetComponent<RectTransform>();
        cbrt.anchorMin = new Vector2(0.5f, 0); cbrt.anchorMax = new Vector2(0.5f, 0);
        cbrt.pivot = new Vector2(0.5f, 0);
        cbrt.sizeDelta = new Vector2(340, 62); cbrt.anchoredPosition = new Vector2(0, 24);
        UnityEventTools.AddPersistentListener(closeBtn.GetComponent<Button>().onClick, manager.CloseMap);

        ov.gameObject.SetActive(false);
        return ov.gameObject;
    }

    static void BuildIsland(RectTransform parent, string name, Vector2 centre,
        float w, float h, Color mainCol, Color accentCol, string label, bool locked)
    {
        var go = new GameObject("Island_" + name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h); rt.anchoredPosition = centre;
        go.AddComponent<Image>().color = mainCol;
        var ac = UImg(rt, "Top", accentCol);
        ac.rectTransform.anchorMin = new Vector2(0, 0.6f); ac.rectTransform.anchorMax = Vector2.one;
        ac.rectTransform.offsetMin = ac.rectTransform.offsetMax = Vector2.zero; ac.raycastTarget = false;
        var lbl = UTxt(rt, "L", label, 16,
            locked ? new Color(0.9f, 0.9f, 0.6f) : Color.white,
            TextAlignmentOptions.Center, locked ? FontStyles.Normal : FontStyles.Bold);
        lbl.textWrappingMode = TextWrappingModes.Normal; Stretch(lbl.rectTransform);
    }

    static void BuildIslandMarker(RectTransform parent, Vector2 pos, string label, Color col)
    {
        var go = new GameObject("Marker", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(130, 52); rt.anchoredPosition = pos;
        go.AddComponent<Image>().color = new Color(0, 0, 0, 0.55f);
        var txt = UTxt(rt, "T", label, 17, col, TextAlignmentOptions.Center, FontStyles.Bold);
        txt.textWrappingMode = TextWrappingModes.Normal; Stretch(txt.rectTransform);
    }

    // =========================================================================
    // Sprite helpers
    // =========================================================================

    static Sprite FindSprite(string name)
    {
        foreach (var g in AssetDatabase.FindAssets(name + " t:Sprite"))
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            if (System.IO.Path.GetFileNameWithoutExtension(path).ToLower() == name.ToLower())
            {
                var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (s != null) return s;
            }
        }
        foreach (var g in AssetDatabase.FindAssets(name + " t:Texture2D"))
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            if (System.IO.Path.GetFileNameWithoutExtension(path).ToLower().Contains(name.ToLower()))
            {
                var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (s != null) return s;
            }
        }
        return null;
    }

    static void LogFound(string n, Sprite s) =>
        Debug.Log($"[WorldMapBuilder] {n}: " + (s != null ? "✓" : "✗ not found (placeholder)"));

    static Sprite GetCircle()
    {
        try { return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"); }
        catch { return null; }
    }

    // UPDATED: Generates a 1x1 sprite and sets PPU to 1f so scales match Unity units perfectly
    static Sprite EnsureWhitePixel()
    {
        const string dir = "Assets/Sprites";
        const string path = "Assets/Sprites/world_white_pixel.png";
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        if (!File.Exists(path))
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null)
        {
            bool ch = false;
            if (imp.textureType != TextureImporterType.Sprite) { imp.textureType = TextureImporterType.Sprite; ch = true; }
            if (imp.filterMode != FilterMode.Point) { imp.filterMode = FilterMode.Point; ch = true; }
            if (imp.spritePixelsPerUnit != 1f) { imp.spritePixelsPerUnit = 1f; ch = true; }
            if (ch) imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // =========================================================================
    // Low-level helpers
    // =========================================================================

    static SpriteRenderer SR(string name, Sprite spr, Color col,
        Vector3 localPos, Vector3 scale, int order, Transform parent = null)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = spr; sr.color = col; sr.sortingOrder = order;
        return sr;
    }

    static Image UImg(Transform p, string n, Color c)
    {
        var go = new GameObject(n, typeof(RectTransform));
        go.transform.SetParent(p, false);
        var img = go.AddComponent<Image>(); img.color = c; return img;
    }

    static TMP_Text UTxt(Transform p, string n, string text, int size, Color col,
        TextAlignmentOptions align, FontStyles style = FontStyles.Normal)
    {
        var go = new GameObject(n, typeof(RectTransform));
        go.transform.SetParent(p, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.color = col;
        t.alignment = align; t.fontStyle = style; t.raycastTarget = false;
        return t;
    }

    static GameObject UBtn(Transform p, string n, string label, int size, Color bg, Color tc)
    {
        var go = new GameObject(n, typeof(RectTransform));
        go.transform.SetParent(p, false);
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
        {
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}