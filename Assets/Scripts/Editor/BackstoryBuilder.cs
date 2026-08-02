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
/// Builds the one-time Backstory cutscene scene.
/// Run via: Phisherman > Build Backstory Scene
///
/// Visual style:
///   - Dark underwater / night bg (reuses swiper_background or falls back
///     to a deep navy colour)
///   - Large phisherman backstory portrait, centre-left
///   - Dialogue box at the bottom (same style as interior scenes)
///   - Narrator caption lines (no speaker name) + phisherman spoken lines
///   - Skip button top-right
///   - Crossfades between the three backstory poses as the story progresses
/// </summary>
public static class BackstoryBuilder
{
    private const string ScenesDir = "Assets/Scenes";
    private const string ScenePath = "Assets/Scenes/Backstory.unity";

    static readonly Color PanelBg = new Color(0.06f, 0.08f, 0.16f, 0.96f);
    static readonly Color AccentCol = Hex("#4ECDC4");
    static readonly Color NarratorCol = new Color(0.85f, 0.85f, 1.00f, 0.90f);
    static readonly Color SpeakerCol = Hex("#FFD93D");
    static readonly Color HintCol = new Color(1f, 1f, 1f, 0.40f);

    [MenuItem("Phisherman/Build Backstory Scene")]
    public static void Build()
    {
        if (!Directory.Exists(ScenesDir)) Directory.CreateDirectory(ScenesDir);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── Sprites ───────────────────────────────────────────────────
        Sprite back = FindSprite("phisherman_backstory_backfacing");
        Sprite front = FindSprite("phisherman_backstory_frontfacing");
        Sprite three = FindSprite("phisherman_backstory_threequarter");
        Sprite bgSpr = FindSprite("swiper_background");   // reuse ocean bg
        LogFound("phisherman_backstory_backfacing", back);
        LogFound("phisherman_backstory_frontfacing", front);
        LogFound("phisherman_backstory_threequarter", three);

        // Fallback poses if some sprites are missing
        if (back == null) back = FindSprite("phisherman");
        if (front == null) front = FindSprite("phisherman");
        if (three == null) three = FindSprite("phisherman");

        // ── Camera ───────────────────────────────────────────────────
        var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>(); camGo.AddComponent<AudioListener>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Hex("#060C1A");
        cam.orthographic = true; cam.orthographicSize = 4.5f;
        camGo.transform.position = new Vector3(0, 0, -10);

        // ── EventSystem ───────────────────────────────────────────────
        var es = new GameObject("EventSystem"); es.AddComponent<EventSystem>(); es.AddComponent<StandaloneInputModule>();

        // ── Canvas ────────────────────────────────────────────────────
        var canvasGo = new GameObject("Canvas"); var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        var canvasRT = canvasGo.GetComponent<RectTransform>();
        var rootCG = canvasGo.AddComponent<CanvasGroup>();

        // ── Background ────────────────────────────────────────────────
        var bgImg = Img(canvasRT, "Background", bgSpr != null ? Color.white : Hex("#060C1A"));
        if (bgSpr != null) { bgImg.sprite = bgSpr; bgImg.preserveAspect = false; }
        Stretch(bgImg.rectTransform); bgImg.raycastTarget = false;

        // Dark overlay to keep bg subdued / cinematic
        var ov = Img(canvasRT, "Overlay", new Color(0f, 0f, 0.05f, 0.65f));
        Stretch(ov.rectTransform); ov.raycastTarget = false;

        // ── Phisherman portrait — centre-left, tall ───────────────────
        var phImg = Img(canvasRT, "PhishermanPortrait", Color.white);
        phImg.sprite = back; phImg.preserveAspect = true; phImg.raycastTarget = false;
        var phRT = phImg.rectTransform;
        phRT.anchorMin = new Vector2(0.05f, 0.16f);
        phRT.anchorMax = new Vector2(0.52f, 0.98f);
        phRT.offsetMin = phRT.offsetMax = Vector2.zero;

        // ── Dialogue panel (bottom ~22%) ──────────────────────────────
        var dp = Img(canvasRT, "DialoguePanel", PanelBg);
        var dpRT = dp.rectTransform;
        dpRT.anchorMin = new Vector2(0, 0); dpRT.anchorMax = new Vector2(1, 0.20f);
        dpRT.offsetMin = dpRT.offsetMax = Vector2.zero;

        // Accent top border
        var bord = Img(dpRT, "Border", AccentCol); var brRT = bord.rectTransform;
        brRT.anchorMin = new Vector2(0, 1); brRT.anchorMax = new Vector2(1, 1);
        brRT.pivot = new Vector2(0.5f, 1); brRT.sizeDelta = new Vector2(0, 4); bord.raycastTarget = false;

        // Speaker name bar (shown for Phisherman lines, hidden for narrator)
        var nameBar = Img(dpRT, "NameBar", new Color(AccentCol.r, AccentCol.g, AccentCol.b, 0.22f));
        var nbRT = nameBar.rectTransform;
        nbRT.anchorMin = new Vector2(0, 1); nbRT.anchorMax = new Vector2(0.22f, 1);
        nbRT.pivot = new Vector2(0, 1); nbRT.sizeDelta = new Vector2(0, 42);

        var speakerTxt = Txt(nbRT, "Speaker", "", 26, SpeakerCol, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        var strt = speakerTxt.rectTransform; strt.anchorMin = Vector2.zero; strt.anchorMax = Vector2.one;
        strt.offsetMin = new Vector2(16, 0); strt.offsetMax = Vector2.zero;

        // Body text — narrator lines in soft blue-white, spoken in white
        var bodyTxt = Txt(dpRT, "BodyText", "", 26, NarratorCol, TextAlignmentOptions.TopLeft);
        bodyTxt.textWrappingMode = TextWrappingModes.Normal;
        var btRT = bodyTxt.rectTransform;
        btRT.anchorMin = Vector2.zero; btRT.anchorMax = Vector2.one;
        btRT.offsetMin = new Vector2(28, 38); btRT.offsetMax = new Vector2(-28, -44);

        // Continue hint
        var hintTxt = Txt(dpRT, "Hint", "Tap to continue...", 18, HintCol, TextAlignmentOptions.MidlineRight);
        var htRT = hintTxt.rectTransform;
        htRT.anchorMin = new Vector2(0, 0); htRT.anchorMax = new Vector2(1, 0);
        htRT.pivot = new Vector2(0.5f, 0); htRT.sizeDelta = new Vector2(0, 30); htRT.anchoredPosition = new Vector2(-20, 8);

        // ── Full-screen advance button ─────────────────────────────────
        var advGo = new GameObject("AdvanceBtn", typeof(RectTransform));
        advGo.transform.SetParent(canvasRT, false); Stretch(advGo.GetComponent<RectTransform>());
        var advImg = advGo.AddComponent<Image>(); advImg.color = new Color(0, 0, 0, 0); advImg.raycastTarget = true;
        var advBtn = advGo.AddComponent<Button>(); advBtn.targetGraphic = advImg;

        // ── Skip button (top-right) ────────────────────────────────────
        var skipGo = new GameObject("SkipBtn", typeof(RectTransform));
        skipGo.transform.SetParent(canvasRT, false);
        var skipRT = skipGo.GetComponent<RectTransform>();
        skipRT.anchorMin = new Vector2(1, 1); skipRT.anchorMax = new Vector2(1, 1);
        skipRT.pivot = new Vector2(1, 1); skipRT.sizeDelta = new Vector2(160, 44);
        skipRT.anchoredPosition = new Vector2(-24, -24);

        var skipBg = skipGo.AddComponent<Image>(); skipBg.color = new Color(0, 0, 0, 0.40f);
        var skipBtn = skipGo.AddComponent<Button>(); skipBtn.targetGraphic = skipBg;
        var skipLbl = Txt(skipRT, "Label", "Skip  ›", 22, new Color(1, 1, 1, 0.60f), TextAlignmentOptions.Center);
        Stretch(skipLbl.rectTransform);

        // ── Manager ───────────────────────────────────────────────────
        var mgrGo = new GameObject("BackstoryManager");
        var mgr = mgrGo.AddComponent<BackstoryManager>();
        mgr.phishermanImage = phImg;
        mgr.backgroundImage = bgImg;
        mgr.speakerText = speakerTxt;
        mgr.bodyText = bodyTxt;
        mgr.continueHint = hintTxt;
        mgr.canvasGroup = rootCG;
        mgr.advanceButton = advBtn;
        mgr.skipButton = skipBtn;
        mgr.nextScene = "WorldMap";
        mgr.fadeDuration = 0.5f;
        mgr.portraitFade = 0.28f;

        // ── Cutscene lines ────────────────────────────────────────────
        Color deep = Hex("#060C1A");
        Color dusk = Hex("#0D1B2E");
        Color evening = Hex("#0A1428");

        mgr.lines = new BackstoryManager.CutsceneLine[]
        {
            // ── Act 1: The Discovery ──────────────────────────────────
            new BackstoryManager.CutsceneLine
            {
                text             = "A quiet fishing town. A family that worked hard for everything they had.",
                speaker          = "",
                phishermanSprite = back,
                bgTint           = deep,
            },
            new BackstoryManager.CutsceneLine
            {
                text             = "One evening, Phisherman's father received an email.\n\"Congratulations! You've won a prize. Click here to claim.\"",
                speaker          = "",
                phishermanSprite = back,
                bgTint           = deep,
            },
            new BackstoryManager.CutsceneLine
            {
                text             = "He clicked the link. He filled in his details. He lost everything.",
                speaker          = "",
                phishermanSprite = three,
                bgTint           = dusk,
            },

            // ── Act 2: The Confrontation ──────────────────────────────
            new BackstoryManager.CutsceneLine
            {
                text             = "Dad, it was a scam. They tricked you into giving them your bank details.",
                speaker          = "Phisherman",
                phishermanSprite = three,
                bgTint           = dusk,
            },
            new BackstoryManager.CutsceneLine
            {
                text             = "I know, son. I should have known better. But it looked so real...",
                speaker          = "Father",
                phishermanSprite = three,
                bgTint           = dusk,
            },
            new BackstoryManager.CutsceneLine
            {
                text             = "That's what they count on. They study us. They know exactly what will make us trust them.",
                speaker          = "Phisherman",
                phishermanSprite = three,
                bgTint           = evening,
            },

            // ── Act 3: The Vow ────────────────────────────────────────
            new BackstoryManager.CutsceneLine
            {
                text             = "He stood at the water's edge and made a promise to himself.",
                speaker          = "",
                phishermanSprite = front,
                bgTint           = evening,
            },
            new BackstoryManager.CutsceneLine
            {
                text             = "No one else in this town would fall for this. Not on my watch.",
                speaker          = "Phisherman",
                phishermanSprite = front,
                bgTint           = evening,
            },
            new BackstoryManager.CutsceneLine
            {
                text             = "I'm going to learn every trick they use. And I'm going to teach everyone I know.",
                speaker          = "Phisherman",
                phishermanSprite = front,
                bgTint           = evening,
            },
            new BackstoryManager.CutsceneLine
            {
                text             = "The Phisherman had found his purpose.\n\nAnd the town would never be the same.",
                speaker          = "",
                phishermanSprite = front,
                bgTint           = evening,
            },
        };

        // ── Wire buttons ──────────────────────────────────────────────
        UnityEventTools.AddPersistentListener(advBtn.onClick, mgr.AdvanceLine);
        UnityEventTools.AddPersistentListener(skipBtn.onClick, mgr.SkipAll);

        // ── Save ──────────────────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuild(ScenePath);
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log($"[BackstoryBuilder] Built → {ScenePath}");
    }

    // ── Helpers ───────────────────────────────────────────────────────

    static Sprite FindSprite(string name)
    {
        foreach (var g in AssetDatabase.FindAssets(name + " t:Sprite"))
        { var p = AssetDatabase.GUIDToAssetPath(g); if (Path.GetFileNameWithoutExtension(p).ToLower() == name.ToLower()) { var s = AssetDatabase.LoadAssetAtPath<Sprite>(p); if (s != null) return s; } }
        return null;
    }
    static void LogFound(string n, Sprite s) => Debug.Log($"[BackstoryBuilder] {n}: " + (s != null ? "✓" : "✗"));
    static Image Img(Transform p, string n, Color c) { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); var img = go.AddComponent<Image>(); img.color = c; return img; }
    static TMP_Text Txt(Transform p, string n, string text, int size, Color col, TextAlignmentOptions align, FontStyles style = FontStyles.Normal)
    { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); var t = go.AddComponent<TextMeshProUGUI>(); t.text = text; t.fontSize = size; t.color = col; t.alignment = align; t.fontStyle = style; t.raycastTarget = false; return t; }
    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
    static Color Hex(string h) => ColorUtility.TryParseHtmlString(h, out var c) ? c : Color.magenta;
    static void AddToBuild(string path) { var scenes = EditorBuildSettings.scenes.ToList(); if (!scenes.Any(s => s.path == path)) { scenes.Add(new EditorBuildSettingsScene(path, true)); EditorBuildSettings.scenes = scenes.ToArray(); } }
}