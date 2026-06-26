using System;
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
/// Builds the three stub INTERIOR scenes for the WorldMap buildings.
/// Run via:  Phisherman ▸ Build Interior Stub Scenes
///
///   ApartmentInterior · PizzaInterior · OfficeInterior
///
/// Each is a simple themed room: back wall + floor, a few signature props, a
/// title, a movable Phisherman (uses your existing click-to-move PlayerController
/// as a stub), a walk-on EXIT door that returns to "WorldMap", and a reliable
/// corner "Back to Town" button. Interiors are intentionally rough — next pass
/// is dropping the minigame-giver NPCs in here.
/// </summary>
public static class InteriorBuilder
{
    [MenuItem("Phisherman/Build Interior Stub Scenes")]
    public static void BuildAll()
    {
        BuildInterior("ApartmentInterior", "APARTMENT", "Interior coming soon",
            Hex("#2A2438"), Hex("#F0E2C8"), Hex("#C9A06A"), Hex("#2BB3A3"), DecorApartment);

        BuildInterior("PizzaInterior", "PIZZA PLACE", "Interior coming soon",
            Hex("#2A1A14"), Hex("#E8C9A0"), Hex("#9A5A3A"), Hex("#D6453C"), DecorPizza);

        BuildInterior("OfficeInterior", "OFFICE", "Interior coming soon",
            Hex("#1C2530"), Hex("#D6DBE0"), Hex("#8A8F98"), Hex("#2BB3A3"), DecorOffice);

        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log("[InteriorBuilder] Built 3 interior stub scenes.");
    }

    static void BuildInterior(string sceneName, string title, string subtitle,
        Color bgCol, Color wallCol, Color floorCol, Color accent,
        Action<Transform, Sprite, Sprite> decor)
    {
        string scenePath = "Assets/Scenes/" + sceneName + ".unity";
        if (!Directory.Exists("Assets/Scenes")) Directory.CreateDirectory("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Sprite white = EnsureWhitePixel();
        Sprite circle = GetCircle();
        Sprite blob = circle != null ? circle : white;
        Sprite phisherman = FindSprite("phisherman");

        // Camera
        var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>(); camGo.AddComponent<AudioListener>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = bgCol;
        cam.orthographic = true; cam.orthographicSize = 4.5f;
        camGo.transform.position = new Vector3(0, 0, -10);

        // EventSystem
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>(); es.AddComponent<StandaloneInputModule>();

        // Room shell
        var room = new GameObject("Room").transform;
        SR("Wall", white, wallCol, new Vector3(0, 2.0f, 0), new Vector3(22, 7.2f, 1), -20, room);
        SR("Floor", white, floorCol, new Vector3(0, -3.0f, 0), new Vector3(22, 3.6f, 1), -19, room);
        SR("Baseboard", white, new Color(wallCol.r * 0.7f, wallCol.g * 0.7f, wallCol.b * 0.7f),
            new Vector3(0, -1.05f, 0), new Vector3(22, 0.22f, 1), -18, room);

        // Theme props
        decor?.Invoke(room, white, blob);

        // Title
        AddWorldText(title, new Vector3(0, 3.4f, -1), 0.6f, Color.white, 6);
        AddWorldText(subtitle, new Vector3(0, 2.75f, -1), 0.22f, new Color(1, 1, 1, 0.7f), 6);

        // Player (stub controller: click-to-move)
        var player = new GameObject("Phisherman");
        player.transform.position = new Vector3(0f, -2.4f, -1f);
        var psr = player.AddComponent<SpriteRenderer>();
        psr.sprite = phisherman != null ? phisherman : white;
        psr.sortingOrder = 10;
        player.transform.localScale = new Vector3(0.55f, 0.55f, 1f);
        player.AddComponent<PlayerController>();

        // Exit door (walk-on -> WorldMap)
        var doorVis = new GameObject("ExitDoorVisual").transform;
        SR("Frame", white, accent, new Vector3(0, -3.5f, 0), new Vector3(1.2f, 1.5f, 1), 7, doorVis);
        SR("Door", white, new Color(0.20f, 0.14f, 0.10f), new Vector3(0, -3.55f, 0), new Vector3(0.95f, 1.3f, 1), 8, doorVis);
        SR("Arch", blob, accent, new Vector3(0, -2.85f, 0), new Vector3(1.2f, 0.6f, 1), 7, doorVis);
        SR("Knob", blob, Hex("#F4D03F"), new Vector3(0.3f, -3.55f, -0.01f), new Vector3(0.1f, 0.1f, 1), 9, doorVis);

        var exit = new GameObject("ExitTrigger");
        exit.transform.position = new Vector3(0, -3.2f, 0);
        var dt = exit.AddComponent<DoorTrigger>();
        dt.player = player.transform;
        dt.targetScene = "WorldMap";
        dt.triggerRadius = 0.75f;
        dt.promptProximity = 2.6f;
        var prompt = new GameObject("Prompt");
        prompt.transform.SetParent(exit.transform, false);
        prompt.transform.localPosition = new Vector3(0, 1.15f, -0.2f);
        SR("BadgeEdge", blob, accent, new Vector3(0, -0.03f, 0.05f), new Vector3(2.2f, 0.74f, 1), 29, prompt.transform);
        SR("Badge", blob, new Color(0.10f, 0.12f, 0.20f, 0.95f), Vector3.zero, new Vector3(2.0f, 0.6f, 1), 30, prompt.transform);
        WorldTextGO(prompt.transform, "EXIT TO TOWN", new Vector3(0, 0, -0.1f), 0.24f, Color.white, 31);
        dt.prompt = prompt;
        prompt.SetActive(false);

        // UI: back button + hint
        var canvasGo = new GameObject("Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        var crt = canvasGo.GetComponent<RectTransform>();

        var loaderGo = new GameObject("SceneLoader");
        var loader = loaderGo.AddComponent<SceneLoader>();
        loader.sceneName = "WorldMap";

        var backBtn = UBtn(crt, "BackBtn", "Back to Town", 26, new Color(0.18f, 0.28f, 0.45f), Color.white);
        var bbr = backBtn.GetComponent<RectTransform>();
        bbr.anchorMin = new Vector2(0, 1); bbr.anchorMax = new Vector2(0, 1); bbr.pivot = new Vector2(0, 1);
        bbr.sizeDelta = new Vector2(280, 64); bbr.anchoredPosition = new Vector2(24, -24);
        UnityEventTools.AddPersistentListener(backBtn.GetComponent<Button>().onClick, loader.Load);

        var hintGo = new GameObject("Hint", typeof(RectTransform));
        hintGo.transform.SetParent(crt, false);
        var hr = hintGo.GetComponent<RectTransform>();
        hr.anchorMin = new Vector2(0.5f, 0); hr.anchorMax = new Vector2(0.5f, 0); hr.pivot = new Vector2(0.5f, 0);
        hr.sizeDelta = new Vector2(900, 44); hr.anchoredPosition = new Vector2(0, 24);
        var ht = UTxt(hr, "T", "Click to move  -  walk to the door to leave", 22,
            new Color(1, 1, 1, 0.85f), TextAlignmentOptions.Center);
        Stretch(ht.rectTransform);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, scenePath);
        AddToBuild(scenePath);
    }

    // =========================================================================
    // Theme decorations
    // =========================================================================

    static void DecorApartment(Transform room, Sprite white, Sprite blob)
    {
        // window with sky view
        SR("WinFrame", white, Hex("#5A3A22"), new Vector3(-4.2f, 1.3f, 0), new Vector3(2.5f, 2.3f, 1), -11, room);
        SR("WinGlass", white, Hex("#BFE6F0"), new Vector3(-4.2f, 1.3f, 0), new Vector3(2.2f, 2.0f, 1), -10, room);
        SR("WinV", white, Hex("#5A3A22"), new Vector3(-4.2f, 1.3f, -0.01f), new Vector3(0.08f, 2.0f, 1), -9, room);
        SR("WinH", white, Hex("#5A3A22"), new Vector3(-4.2f, 1.3f, -0.01f), new Vector3(2.2f, 0.08f, 1), -9, room);
        // picture
        SR("Frame", white, Hex("#C9A06A"), new Vector3(2.8f, 1.8f, 0), new Vector3(1.3f, 1.0f, 1), -10, room);
        SR("Pic", white, Hex("#87CEEB"), new Vector3(2.8f, 1.8f, 0), new Vector3(1.1f, 0.8f, 1), -9, room);
        // rug
        SR("Rug", blob, Hex("#C0392B"), new Vector3(0, -2.7f, 0), new Vector3(5.2f, 1.5f, 1), -16, room);
        // sofa
        SR("Sofa", white, Hex("#2BB3A3"), new Vector3(3.4f, -1.7f, 0), new Vector3(3.0f, 0.9f, 1), 1, room);
        SR("SofaBack", white, Hex("#249E8D"), new Vector3(3.4f, -1.2f, 0), new Vector3(3.0f, 0.6f, 1), 1, room);
        SR("ArmL", white, Hex("#249E8D"), new Vector3(2.05f, -1.55f, 0), new Vector3(0.5f, 1.1f, 1), 2, room);
        SR("ArmR", white, Hex("#249E8D"), new Vector3(4.75f, -1.55f, 0), new Vector3(0.5f, 1.1f, 1), 2, room);
        // plant
        SR("Pot", white, Hex("#8E5A3A"), new Vector3(-6.2f, -1.7f, 0), new Vector3(0.6f, 0.7f, 1), 2, room);
        SR("Leaf", blob, Hex("#3E8E41"), new Vector3(-6.2f, -0.85f, 0), new Vector3(1.1f, 1.3f, 1), 2, room);
    }

    static void DecorPizza(Transform room, Sprite white, Sprite blob)
    {
        // checker floor tiles
        Color t1 = Hex("#C0392B"), t2 = Hex("#F2E6D0");
        int cols = 11, rows = 3; float tw = 2.0f;
        float startX = -10f, startY = -3.9f;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                SR("Tile", white, ((r + c) % 2 == 0) ? t1 : t2,
                    new Vector3(startX + c * tw, startY + r * 1.1f, 0), new Vector3(tw, 1.1f, 1), -16, room);
        // brick oven + glow
        SR("Oven", white, Hex("#8E3B27"), new Vector3(-4.0f, 0.4f, 0), new Vector3(2.8f, 2.6f, 1), -10, room);
        SR("OvenMouth", blob, Hex("#3A1E14"), new Vector3(-4.0f, 0.1f, 0), new Vector3(1.7f, 1.4f, 1), -9, room);
        SR("OvenGlow", blob, Hex("#FF8C2A"), new Vector3(-4.0f, -0.05f, 0), new Vector3(1.05f, 0.85f, 1), -8, room);
        // counter
        SR("Counter", white, Hex("#6B4423"), new Vector3(3.6f, -1.7f, 0), new Vector3(4.2f, 1.2f, 1), 1, room);
        SR("CounterTop", white, Hex("#C0C0C0"), new Vector3(3.6f, -1.05f, 0), new Vector3(4.4f, 0.2f, 1), 2, room);
        // menu board
        SR("Menu", white, Hex("#222222"), new Vector3(2.6f, 1.9f, 0), new Vector3(2.6f, 1.5f, 1), -10, room);
        AddWorldText("MENU", new Vector3(2.6f, 2.4f, -1), 0.22f, Hex("#FFD24A"), -7);
    }

    static void DecorOffice(Transform room, Sprite white, Sprite blob)
    {
        // carpet band
        SR("Carpet", white, Hex("#7E848C"), new Vector3(0, -3.0f, 0), new Vector3(22, 3.4f, 1), -16, room);
        // window
        SR("WinO", white, Hex("#37597F"), new Vector3(4.6f, 1.6f, 0), new Vector3(3.2f, 2.6f, 1), -11, room);
        SR("Win", white, Hex("#A9D7E8"), new Vector3(4.6f, 1.6f, 0), new Vector3(2.9f, 2.3f, 1), -10, room);
        // wall clock
        SR("ClockRim", blob, Hex("#2C3E50"), new Vector3(-3.5f, 2.1f, 0), new Vector3(1.05f, 1.05f, 1), -11, room);
        SR("ClockFace", blob, Color.white, new Vector3(-3.5f, 2.1f, 0), new Vector3(0.88f, 0.88f, 1), -10, room);
        // two desks
        foreach (float dx in new[] { -3.0f, 3.0f })
        {
            SR("Desk", white, Hex("#8E6A4F"), new Vector3(dx, -1.9f, 0), new Vector3(2.6f, 0.9f, 1), 1, room);
            SR("Monitor", white, Hex("#202830"), new Vector3(dx, -1.05f, 0), new Vector3(1.0f, 0.7f, 1), 2, room);
            SR("Screen", white, Hex("#4AA3DF"), new Vector3(dx, -1.05f, -0.01f), new Vector3(0.85f, 0.55f, 1), 3, room);
        }
        // plant
        SR("Pot", white, Hex("#3A3A3A"), new Vector3(-6.3f, -1.7f, 0), new Vector3(0.6f, 0.7f, 1), 2, room);
        SR("Leaf", blob, Hex("#3E8E41"), new Vector3(-6.3f, -0.8f, 0), new Vector3(1.1f, 1.3f, 1), 2, room);
    }

    // =========================================================================
    // Helpers (self-contained copies)
    // =========================================================================

    static GameObject WorldTextGO(Transform parent, string text, Vector3 localPos,
        float worldSize, Color col, int order)
    {
        string safe = (text.Length > 10 ? text.Substring(0, 10) : text).Replace(" ", "");
        var go = new GameObject("WT_" + safe);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        const float k = 0.012f;
        go.transform.localScale = Vector3.one * k;
        var c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace; c.sortingOrder = order;
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 80);
        var tgo = new GameObject("T"); tgo.transform.SetParent(go.transform, false);
        var tmp = tgo.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = worldSize / k; tmp.color = col;
        tmp.fontStyle = FontStyles.Bold; tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        var rt = tgo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return go;
    }

    static void AddWorldText(string text, Vector3 pos, float worldSize, Color col, int order)
    {
        string safe = text.Length > 8 ? text.Substring(0, 8).Replace(" ", "") : text.Replace(" ", "");
        var go = new GameObject("WT_" + safe);
        go.transform.position = pos;
        const float k = 0.012f;
        go.transform.localScale = Vector3.one * k;
        var c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace; c.sortingOrder = order;
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 80);
        var tgo = new GameObject("T"); tgo.transform.SetParent(go.transform, false);
        var tmp = tgo.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = worldSize / k; tmp.color = col;
        tmp.fontStyle = FontStyles.Bold; tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        var rt = tgo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

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
        var t = UTxt(go.transform, "Label", label, size, tc, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(t.rectTransform); return go;
    }

    static void Stretch(RectTransform r)
    { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }

    static Color Hex(string h) =>
        ColorUtility.TryParseHtmlString(h, out var c) ? c : Color.magenta;

    static Sprite FindSprite(string name)
    {
        foreach (var g in AssetDatabase.FindAssets(name + " t:Sprite"))
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            if (Path.GetFileNameWithoutExtension(path).ToLower() == name.ToLower())
            {
                var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (s != null) return s;
            }
        }
        return null;
    }

    static Sprite GetCircle()
    {
        try { return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"); }
        catch { return null; }
    }

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