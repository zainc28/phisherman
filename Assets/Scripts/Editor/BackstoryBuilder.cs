using System.Collections;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// ============================================================
//  BackstoryBuilder  (Editor)
// ============================================================
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

        Sprite back = FindSprite("phisherman_backstory_backfacing");
        Sprite front = FindSprite("phisherman_backstory_frontfacing");
        Sprite three = FindSprite("phisherman_backstory_threequarter");
        Sprite bgSpr = FindSprite("swiper_background");
        Sprite dadSpr = FindSprite("uncle_4");

        LogFound("phisherman_backstory_backfacing", back);
        LogFound("phisherman_backstory_frontfacing", front);
        LogFound("phisherman_backstory_threequarter", three);
        LogFound("uncle_4", dadSpr);

        if (back == null) back = FindSprite("phisherman");
        if (front == null) front = FindSprite("phisherman");
        if (three == null) three = FindSprite("phisherman");

        // Camera
        var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>(); camGo.AddComponent<AudioListener>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Hex("#060C1A");
        cam.orthographic = true; cam.orthographicSize = 4.5f;
        camGo.transform.position = new Vector3(0, 0, -10);

        // EventSystem
        var es = new GameObject("EventSystem"); es.AddComponent<EventSystem>(); es.AddComponent<StandaloneInputModule>();

        // Canvas
        var canvasGo = new GameObject("Canvas"); var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        var canvasRT = canvasGo.GetComponent<RectTransform>();
        var rootCG = canvasGo.AddComponent<CanvasGroup>();

        // Background
        var bgImg = Img(canvasRT, "Background", bgSpr != null ? Color.white : Hex("#060C1A"));
        if (bgSpr != null) { bgImg.sprite = bgSpr; bgImg.preserveAspect = false; }
        Stretch(bgImg.rectTransform); bgImg.raycastTarget = false;

        var ov = Img(canvasRT, "Overlay", new Color(0f, 0f, 0.05f, 0.65f));
        Stretch(ov.rectTransform); ov.raycastTarget = false;

        // Phisherman portrait — left side
        var phImg = Img(canvasRT, "PhishermanPortrait", Color.white);
        phImg.sprite = back; phImg.preserveAspect = true; phImg.raycastTarget = false;
        var phRT = phImg.rectTransform;
        phRT.anchorMin = new Vector2(0.02f, 0.16f); phRT.anchorMax = new Vector2(0.47f, 0.98f);
        phRT.offsetMin = phRT.offsetMax = Vector2.zero;

        // Dad portrait (uncle_4) — right side, Act 2 only, flipped to face left
        var dadImg = Img(canvasRT, "DadPortrait", Color.white);
        dadImg.sprite = dadSpr; dadImg.preserveAspect = true; dadImg.raycastTarget = false;
        var dadRT = dadImg.rectTransform;
        dadRT.anchorMin = new Vector2(0.53f, 0.16f); dadRT.anchorMax = new Vector2(0.98f, 0.98f);
        dadRT.offsetMin = dadRT.offsetMax = Vector2.zero;
        dadRT.localScale = new Vector3(-1f, 1f, 1f);   // flip horizontally so dad faces Phisherman
        dadImg.gameObject.SetActive(false);

        // Dialogue panel — bottom 20%
        var dp = Img(canvasRT, "DialoguePanel", PanelBg); var dpRT = dp.rectTransform;
        dpRT.anchorMin = new Vector2(0, 0); dpRT.anchorMax = new Vector2(1, 0.20f); dpRT.offsetMin = dpRT.offsetMax = Vector2.zero;

        var bord = Img(dpRT, "Border", AccentCol); var brRT = bord.rectTransform;
        brRT.anchorMin = new Vector2(0, 1); brRT.anchorMax = new Vector2(1, 1);
        brRT.pivot = new Vector2(0.5f, 1); brRT.sizeDelta = new Vector2(0, 4); bord.raycastTarget = false;

        var nameBar = Img(dpRT, "NameBar", new Color(AccentCol.r, AccentCol.g, AccentCol.b, 0.22f)); var nbRT = nameBar.rectTransform;
        nbRT.anchorMin = new Vector2(0, 1); nbRT.anchorMax = new Vector2(0.22f, 1); nbRT.pivot = new Vector2(0, 1); nbRT.sizeDelta = new Vector2(0, 42);

        var speakerTxt = Txt(nbRT, "Speaker", "", 26, SpeakerCol, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        var strt = speakerTxt.rectTransform; strt.anchorMin = Vector2.zero; strt.anchorMax = Vector2.one; strt.offsetMin = new Vector2(16, 0); strt.offsetMax = Vector2.zero;

        var bodyTxt = Txt(dpRT, "BodyText", "", 26, NarratorCol, TextAlignmentOptions.TopLeft);
        bodyTxt.textWrappingMode = TextWrappingModes.Normal;
        var btRT = bodyTxt.rectTransform; btRT.anchorMin = Vector2.zero; btRT.anchorMax = Vector2.one; btRT.offsetMin = new Vector2(28, 38); btRT.offsetMax = new Vector2(-28, -44);

        var hintTxt = Txt(dpRT, "Hint", "Tap to continue...", 18, HintCol, TextAlignmentOptions.MidlineRight);
        var htRT = hintTxt.rectTransform; htRT.anchorMin = new Vector2(0, 0); htRT.anchorMax = new Vector2(1, 0); htRT.pivot = new Vector2(0.5f, 0); htRT.sizeDelta = new Vector2(0, 30); htRT.anchoredPosition = new Vector2(-20, 8);

        // Full-screen advance button
        var advGo = new GameObject("AdvanceBtn", typeof(RectTransform)); advGo.transform.SetParent(canvasRT, false); Stretch(advGo.GetComponent<RectTransform>());
        var advImg = advGo.AddComponent<Image>(); advImg.color = new Color(0, 0, 0, 0); advImg.raycastTarget = true;
        var advBtn = advGo.AddComponent<Button>(); advBtn.targetGraphic = advImg;

        // Skip button
        var skipGo = new GameObject("SkipBtn", typeof(RectTransform)); skipGo.transform.SetParent(canvasRT, false);
        var skipRT = skipGo.GetComponent<RectTransform>();
        skipRT.anchorMin = new Vector2(1, 1); skipRT.anchorMax = new Vector2(1, 1); skipRT.pivot = new Vector2(1, 1); skipRT.sizeDelta = new Vector2(160, 44); skipRT.anchoredPosition = new Vector2(-24, -24);
        var skipBg = skipGo.AddComponent<Image>(); skipBg.color = new Color(0, 0, 0, 0.40f);
        var skipBtn = skipGo.AddComponent<Button>(); skipBtn.targetGraphic = skipBg;
        var skipLbl = Txt(skipRT, "Label", "Skip  ›", 22, new Color(1, 1, 1, 0.60f), TextAlignmentOptions.Center); Stretch(skipLbl.rectTransform);

        // Manager
        var mgrGo = new GameObject("BackstoryManager");
        var mgr = mgrGo.AddComponent<BackstoryManager>();
        mgr.phishermanImage = phImg;
        mgr.dadImage = dadImg;
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

        Color deep = Hex("#060C1A"), dusk = Hex("#0D1B2E"), evening = Hex("#0A1428");

        mgr.lines = new BackstoryManager.CutsceneLine[]
        {
            // Act 1 — no dad
            new BackstoryManager.CutsceneLine { text="A quiet fishing town. A family that worked hard for everything they had.",                                                               speaker="",            phishermanSprite=back,  showDad=false, bgTint=deep    },
            new BackstoryManager.CutsceneLine { text="One evening, Phisherman's father received an email.\n\"Congratulations! You've won a prize. Click here to claim.\"",                   speaker="",            phishermanSprite=back,  showDad=false, bgTint=deep    },
            new BackstoryManager.CutsceneLine { text="He clicked the link. He filled in his details. He lost everything.",                                                                   speaker="",            phishermanSprite=three, showDad=false, bgTint=dusk    },
            // Act 2 — dad visible
            new BackstoryManager.CutsceneLine { text="Dad, it was a scam. They tricked you into giving them your bank details.",                                                             speaker="Phisherman",  phishermanSprite=three, showDad=true,  bgTint=dusk    },
            new BackstoryManager.CutsceneLine { text="I know, son. I should have known better. But it looked so real...",                                                                   speaker="Father",      phishermanSprite=three, showDad=true,  bgTint=dusk    },
            new BackstoryManager.CutsceneLine { text="That's what they count on. They study us. They know exactly what will make us trust them.",                                            speaker="Phisherman",  phishermanSprite=three, showDad=true,  bgTint=evening },
            // Act 3 — dad gone, phisherman alone
            new BackstoryManager.CutsceneLine { text="He stood at the water's edge and made a promise to himself.",                                                                          speaker="",            phishermanSprite=front, showDad=false, bgTint=evening },
            new BackstoryManager.CutsceneLine { text="No one else in this town would fall for this. Not on my watch.",                                                                      speaker="Phisherman",  phishermanSprite=front, showDad=false, bgTint=evening },
            new BackstoryManager.CutsceneLine { text="I'm going to learn every trick they use. And I'm going to teach everyone I know.",                                                     speaker="Phisherman",  phishermanSprite=front, showDad=false, bgTint=evening },
            new BackstoryManager.CutsceneLine { text="The Phisherman had found his purpose.\n\nAnd the town would never be the same.",                                                      speaker="",            phishermanSprite=front, showDad=false, bgTint=evening },
        };

        UnityEventTools.AddPersistentListener(advBtn.onClick, mgr.AdvanceLine);
        UnityEventTools.AddPersistentListener(skipBtn.onClick, mgr.SkipAll);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuild(ScenePath);
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log($"[BackstoryBuilder] Built → {ScenePath}");
    }

    static Sprite FindSprite(string name) { foreach (var g in AssetDatabase.FindAssets(name + " t:Sprite")) { var p = AssetDatabase.GUIDToAssetPath(g); if (System.IO.Path.GetFileNameWithoutExtension(p).ToLower() == name.ToLower()) { var s = AssetDatabase.LoadAssetAtPath<Sprite>(p); if (s != null) return s; } } return null; }
    static void LogFound(string n, Sprite s) => Debug.Log($"[BackstoryBuilder] {n}: " + (s != null ? "✓" : "✗"));
    static Image Img(Transform p, string n, Color c) { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); var img = go.AddComponent<Image>(); img.color = c; return img; }
    static TMP_Text Txt(Transform p, string n, string text, int size, Color col, TextAlignmentOptions align, FontStyles style = FontStyles.Normal) { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); var t = go.AddComponent<TextMeshProUGUI>(); t.text = text; t.fontSize = size; t.color = col; t.alignment = align; t.fontStyle = style; t.raycastTarget = false; return t; }
    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
    static Color Hex(string h) => ColorUtility.TryParseHtmlString(h, out var c) ? c : Color.magenta;
    static void AddToBuild(string path) { var scenes = EditorBuildSettings.scenes.ToList(); if (!scenes.Any(s => s.path == path)) { scenes.Add(new EditorBuildSettingsScene(path, true)); EditorBuildSettings.scenes = scenes.ToArray(); } }
}

// ============================================================
//  BackstoryManager  (Runtime)
// ============================================================
public class BackstoryManager : MonoBehaviour
{
    [System.Serializable]
    public struct CutsceneLine
    {
        [UnityEngine.TextArea(2, 5)] public string text;
        public string speaker;
        public Sprite phishermanSprite;
        public bool showDad;
        public Color bgTint;
    }

    [Header("Portraits")]
    public Image phishermanImage;
    public Image dadImage;

    [Header("Background")]
    public Image backgroundImage;

    [Header("Dialogue UI")]
    public TMP_Text speakerText;
    public TMP_Text bodyText;
    public TMP_Text continueHint;

    [Header("Navigation")]
    public Button advanceButton;
    public Button skipButton;

    [Header("Timing")]
    public CanvasGroup canvasGroup;
    public float fadeDuration = 0.5f;
    public float portraitFade = 0.28f;

    [Header("Scene flow")]
    public string nextScene = "WorldMap";

    [Header("Lines")]
    public CutsceneLine[] lines;

    int _lineIndex = 0;
    bool _busy = false;

    static readonly Color NarratorCol = new Color(0.85f, 0.85f, 1.00f, 0.90f);
    static readonly Color SpeakerCol = new Color(1.00f, 0.85f, 0.25f, 1.00f);

    void Start()
    {
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        if (dadImage != null) dadImage.gameObject.SetActive(false);
        StartCoroutine(BeginSequence());
    }

    IEnumerator BeginSequence()
    {
        if (canvasGroup != null)
        { float t = 0f; while (t < fadeDuration) { t += Time.deltaTime; canvasGroup.alpha = Mathf.Clamp01(t / fadeDuration); yield return null; } canvasGroup.alpha = 1f; }
        ShowLine(_lineIndex);
    }

    public void AdvanceLine()
    {
        if (_busy) return;
        _lineIndex++;
        if (_lineIndex >= lines.Length) { StartCoroutine(FadeAndLoad(nextScene)); return; }
        StartCoroutine(CrossfadeToLine(_lineIndex));
    }

    public void SkipAll()
    { if (_busy) return; StartCoroutine(FadeAndLoad(nextScene)); }

    void ShowLine(int idx)
    {
        if (lines == null || idx < 0 || idx >= lines.Length) return;
        var line = lines[idx];
        if (bodyText != null) bodyText.text = line.text;
        if (continueHint != null) continueHint.text = idx < lines.Length - 1 ? "Tap to continue..." : "Tap to finish";
        bool hasSpeaker = !string.IsNullOrEmpty(line.speaker);
        if (speakerText != null) { speakerText.text = line.speaker; speakerText.color = hasSpeaker ? SpeakerCol : NarratorCol; }
        if (bodyText != null) bodyText.color = hasSpeaker ? Color.white : NarratorCol;
        if (phishermanImage != null && line.phishermanSprite != null) phishermanImage.sprite = line.phishermanSprite;
        if (dadImage != null) dadImage.gameObject.SetActive(line.showDad);
        if (Camera.main != null && line.bgTint != default) Camera.main.backgroundColor = line.bgTint;
    }

    IEnumerator CrossfadeToLine(int idx)
    {
        _busy = true;
        yield return StartCoroutine(FadePortraits(1f, 0f));
        ShowLine(idx);
        yield return StartCoroutine(FadePortraits(0f, 1f));
        _busy = false;
    }

    IEnumerator FadePortraits(float from, float to)
    {
        float t = 0f;
        while (t < portraitFade)
        { t += Time.deltaTime; float a = Mathf.Lerp(from, to, Mathf.Clamp01(t / portraitFade)); SetAlpha(phishermanImage, a); if (dadImage != null && dadImage.gameObject.activeSelf) SetAlpha(dadImage, a); yield return null; }
        SetAlpha(phishermanImage, to);
        if (dadImage != null && dadImage.gameObject.activeSelf) SetAlpha(dadImage, to);
    }

    static void SetAlpha(Image img, float a) { if (img == null) return; var c = img.color; c.a = a; img.color = c; }

    IEnumerator FadeAndLoad(string sceneName)
    {
        _busy = true;
        PlayerPrefs.SetInt("backstory_seen", 1); PlayerPrefs.Save();
        if (canvasGroup != null)
        { float t = 0f; while (t < fadeDuration) { t += Time.deltaTime; canvasGroup.alpha = 1f - Mathf.Clamp01(t / fadeDuration); yield return null; } canvasGroup.alpha = 0f; }
        SceneManager.LoadScene(sceneName);
    }
}