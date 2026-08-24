using System.Collections;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// ============================================================
//  MainMenuBuilder  (Editor-only)
//  Run via: Phisherman > Build Main Menu Scene
//
//  All button wiring happens at runtime in MainMenuManager.Start().
//  Buttons are found via Transform.Find() on their parent panel
//  with includeInactive:true so inactive panels are still searchable.
// ============================================================
public static class MainMenuBuilder
{
    const string ScenesDir = "Assets/Scenes";
    const string ScenePath = "Assets/Scenes/MainMenu.unity";

    static readonly Color BgDark = new Color(0.03f, 0.06f, 0.12f, 1f);
    static readonly Color PanelBg = new Color(0.04f, 0.06f, 0.13f, 0.98f);
    static readonly Color CardBg = new Color(0.05f, 0.08f, 0.17f, 0.97f);
    static readonly Color StoryC = Hex("#00E5FF");
    static readonly Color ArcadeC = Hex("#FFD93D");
    static readonly Color SettingC = Hex("#A29BFE");
    static readonly Color ExitC = Hex("#636E72");
    static readonly Color ResetC = Hex("#E74C3C");
    static readonly Color EmailC = Hex("#FF6B6B");
    static readonly Color SpotC = Hex("#FFD93D");
    static readonly Color TowerC = Hex("#2ECC71");

    [MenuItem("Phisherman/Build Main Menu Scene")]
    public static void Build()
    {
        if (File.Exists(ScenePath)) { AssetDatabase.DeleteAsset(ScenePath); AssetDatabase.Refresh(); }
        if (!Directory.Exists(ScenesDir)) Directory.CreateDirectory(ScenesDir);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Camera
        var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>(); camGo.AddComponent<AudioListener>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = BgDark;
        cam.orthographic = true; camGo.transform.position = new Vector3(0, 0, -10);

        // EventSystem
        var esGo = new GameObject("EventSystem");
        esGo.AddComponent<EventSystem>(); esGo.AddComponent<StandaloneInputModule>();

        // Canvas
        var cGo = new GameObject("Canvas");
        var cv = cGo.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 200;
        var scaler = cGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
        cGo.AddComponent<GraphicRaycaster>();
        var cRT = cGo.GetComponent<RectTransform>();
        var rootCG = cGo.AddComponent<CanvasGroup>();

        // Background + vignette
        Sprite bgSpr = FindSprite("main_menu_bg");
        var bgImg = MkImg(cRT, "Background", bgSpr != null ? Color.white : BgDark);
        if (bgSpr != null) { bgImg.sprite = bgSpr; bgImg.preserveAspect = false; }
        Stretch(bgImg.rectTransform); bgImg.raycastTarget = false;
        var vign = MkImg(cRT, "Vignette", new Color(0, 0, 0, 0.40f));
        Stretch(vign.rectTransform); vign.raycastTarget = false;

        // Manager
        var mgr = new GameObject("MainMenuManager").AddComponent<MainMenuManager>();
        mgr.canvasGroup = rootCG;

        // ── Main panel ────────────────────────────────────────
        var mainPanel = new GameObject("MainPanel", typeof(RectTransform));
        mainPanel.transform.SetParent(cRT, false);
        Stretch(mainPanel.GetComponent<RectTransform>());

        var titleTxt = MkTxt(mainPanel.transform, "TitleText", "PHISHERMAN", 84, StoryC, TextAlignmentOptions.Center, FontStyles.Bold);
        titleTxt.textWrappingMode = TextWrappingModes.NoWrap;
        SetAnch(titleTxt.rectTransform, 0.20f, 0.83f, 0.80f, 0.97f);

        var tagTxt = MkTxt(mainPanel.transform, "Tagline", "An Educational Anti-Phishing Adventure", 21, new Color(1, 1, 1, 0.40f), TextAlignmentOptions.Center);
        SetAnch(tagTxt.rectTransform, 0.15f, 0.78f, 0.85f, 0.83f);

        MkBtn(mainPanel.transform, "StoryBtn", "STORY MODE", StoryC, 0.30f, 0.585f, 0.70f, 0.668f);
        MkBtn(mainPanel.transform, "ArcadeBtn", "ARCADE MODE", ArcadeC, 0.30f, 0.466f, 0.70f, 0.549f);
        MkBtn(mainPanel.transform, "SettingsBtn", "SETTINGS", SettingC, 0.30f, 0.347f, 0.70f, 0.430f);
        MkBtn(mainPanel.transform, "ExitBtn", "EXIT GAME", ExitC, 0.76f, 0.038f, 0.95f, 0.108f, 24);

        var verTxt = MkTxt(mainPanel.transform, "Ver", "v0.1 - Research Prototype", 15, new Color(1, 1, 1, 0.25f), TextAlignmentOptions.Left);
        SetAnch(verTxt.rectTransform, 0.01f, 0.008f, 0.35f, 0.045f, ox: 12);

        // ── Arcade panel ──────────────────────────────────────
        var arcadePanel = MkPanel(cRT, "ArcadePanel", 0.07f, 0.06f, 0.93f, 0.94f);
        {
            var pRT = arcadePanel.GetComponent<RectTransform>();
            TopBar(pRT, ArcadeC);
            MkTxt(arcadePanel.transform, "Title", "ARCADE MODE", 48, ArcadeC, TextAlignmentOptions.Center, FontStyles.Bold)
                .rectTransform.With(r => SetAnch(r, 0f, 0.88f, 1f, 1f));
            MkTxt(arcadePanel.transform, "Sub", "Choose a minigame — no story progress required.", 21, new Color(1, 1, 1, 0.46f), TextAlignmentOptions.Center)
                .rectTransform.With(r => SetAnch(r, 0.05f, 0.82f, 0.95f, 0.89f));
            MkCard(pRT, "EmailCard", "Email Swiper", "Sort real vs phishing emails.\nSwipe SCAM or SAFE.", EmailC, "fish", 0.02f, 0.13f, 0.33f, 0.80f);
            MkCard(pRT, "SpotCard", "Spot the Difference", "Find every red flag the\nfake email hides.", SpotC, "magnifying_glass", 0.36f, 0.13f, 0.64f, 0.80f);
            MkCard(pRT, "TowerCard", "Tower Defense", "Block waves of phishing\nattacks before they land.", TowerC, "tower", 0.67f, 0.13f, 0.98f, 0.80f);
            MkBtn(arcadePanel.transform, "ArcadeBackBtn", "Back", ExitC, 0.36f, 0.01f, 0.64f, 0.10f, 27);
        }
        arcadePanel.SetActive(false);

        // ── Settings panel ────────────────────────────────────
        var settingsPanel = MkPanel(cRT, "SettingsPanel", 0.15f, 0.06f, 0.85f, 0.94f);
        {
            var pRT = settingsPanel.GetComponent<RectTransform>();
            TopBar(pRT, SettingC);
            MkTxt(settingsPanel.transform, "Title", "SETTINGS", 48, SettingC, TextAlignmentOptions.Center, FontStyles.Bold)
                .rectTransform.With(r => SetAnch(r, 0f, 0.88f, 1f, 1f));
            MkVolRow(pRT, "MasterVolumeRow", "Master Volume", SettingC, 0.73f, 0.82f);
            MkVolRow(pRT, "MusicVolumeRow", "Music Volume", SettingC, 0.60f, 0.69f);
            MkVolRow(pRT, "SFXVolumeRow", "SFX Volume", SettingC, 0.47f, 0.56f);

            var div = MkImg(pRT, "Div", new Color(1, 1, 1, 0.07f));
            div.rectTransform.anchorMin = new Vector2(0.04f, 0.44f); div.rectTransform.anchorMax = new Vector2(0.96f, 0.44f);
            div.rectTransform.sizeDelta = new Vector2(0, 1); div.raycastTarget = false;

            MkTxt(settingsPanel.transform, "DLbl", "DANGER ZONE", 18, new Color(ResetC.r, ResetC.g, ResetC.b, 0.50f), TextAlignmentOptions.Left, FontStyles.Bold)
                .rectTransform.With(r => SetAnch(r, 0.06f, 0.37f, 0.50f, 0.44f));

            MkBtn(settingsPanel.transform, "ResetBtn", "Reset All Progress", ResetC, 0.08f, 0.24f, 0.92f, 0.36f, 29);

            MkTxt(settingsPanel.transform, "ResetWarn",
                "Permanently erases XP, levels, fish stickers, and all world / house progress.",
                15, new Color(1f, 0.45f, 0.45f, 0.60f), TextAlignmentOptions.Center)
                .With(t => { t.textWrappingMode = TextWrappingModes.Normal; SetAnch(t.rectTransform, 0.05f, 0.15f, 0.95f, 0.24f); });

            MkBtn(settingsPanel.transform, "SettingsBackBtn", "Back", ExitC, 0.36f, 0.01f, 0.64f, 0.10f, 27);
            settingsPanel.AddComponent<SettingsManager>();
        }
        settingsPanel.SetActive(false);

        // Give manager the panel references
        mgr.mainPanel = mainPanel;
        mgr.arcadePanel = arcadePanel;
        mgr.settingsPanel = settingsPanel;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuild(ScenePath);
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log("[MainMenuBuilder] Done -> " + ScenePath);
    }

    // ── Card ──────────────────────────────────────────────────
    static void MkCard(RectTransform parent, string name, string title, string desc,
        Color col, string iconName, float xMin, float yMin, float xMax, float yMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(xMin, yMin); rt.anchorMax = new Vector2(xMax, yMax);
        rt.offsetMin = new Vector2(6, 0); rt.offsetMax = new Vector2(-6, 0);

        var bg = go.AddComponent<Image>(); bg.color = CardBg;

        var tb = MkImg(rt, "TB", col);
        tb.rectTransform.anchorMin = new Vector2(0, 1); tb.rectTransform.anchorMax = new Vector2(1, 1);
        tb.rectTransform.pivot = new Vector2(0.5f, 1); tb.rectTransform.sizeDelta = new Vector2(0, 4);
        tb.raycastTarget = false;

        Sprite spr = FindSprite(iconName);
        var icon = MkImg(rt, "Icon", spr != null ? Color.white : new Color(col.r, col.g, col.b, 0.12f));
        if (spr != null) { icon.sprite = spr; icon.preserveAspect = true; }
        icon.rectTransform.anchorMin = new Vector2(0.12f, 0.46f); icon.rectTransform.anchorMax = new Vector2(0.88f, 0.88f);
        icon.rectTransform.offsetMin = icon.rectTransform.offsetMax = Vector2.zero; icon.raycastTarget = false;

        MkTxt(rt, "CardTitle", title, 26, col, TextAlignmentOptions.Center, FontStyles.Bold)
            .rectTransform.With(r => SetAnch(r, 0f, 0.28f, 1f, 0.46f, ox: 8, ox2: -8));
        MkTxt(rt, "CardDesc", desc, 17, new Color(1, 1, 1, 0.58f), TextAlignmentOptions.Center)
            .With(t => { t.textWrappingMode = TextWrappingModes.Normal; SetAnch(t.rectTransform, 0f, 0.04f, 1f, 0.28f, ox: 10, ox2: -10); });

        var btn = go.AddComponent<Button>(); btn.targetGraphic = bg;
        var cb = btn.colors;
        cb.normalColor = CardBg;
        cb.highlightedColor = new Color(col.r * 0.22f, col.g * 0.22f, col.b * 0.22f, 0.97f);
        cb.pressedColor = new Color(col.r * 0.36f, col.g * 0.36f, col.b * 0.36f, 1f);
        btn.colors = cb;
        MkHover(go, 1.030f, 0.975f);
    }

    // ── Button ────────────────────────────────────────────────
    static void MkBtn(Transform parent, string name, string label,
        Color col, float xMin, float yMin, float xMax, float yMax, int fontSize = 33)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(xMin, yMin); rt.anchorMax = new Vector2(xMax, yMax);
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var bg = go.AddComponent<Image>(); bg.color = new Color(0.04f, 0.06f, 0.13f, 0.95f);

        var accGo = new GameObject("Acc", typeof(RectTransform)); accGo.transform.SetParent(go.transform, false);
        var accImg = accGo.AddComponent<Image>(); accImg.color = col; accImg.raycastTarget = false;
        accGo.GetComponent<RectTransform>().With(r => { r.anchorMin = Vector2.zero; r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 0.5f); r.sizeDelta = new Vector2(5, 0); });

        var brdGo = new GameObject("Brd", typeof(RectTransform)); brdGo.transform.SetParent(go.transform, false);
        var brdImg = brdGo.AddComponent<Image>(); brdImg.color = new Color(col.r, col.g, col.b, 0.28f); brdImg.raycastTarget = false;
        brdGo.GetComponent<RectTransform>().With(r => { r.anchorMin = Vector2.zero; r.anchorMax = new Vector2(1, 0); r.pivot = new Vector2(0.5f, 0); r.sizeDelta = new Vector2(0, 2); });

        MkTxt(go.transform, "Label", label, fontSize, col, TextAlignmentOptions.MidlineLeft, FontStyles.Bold)
            .rectTransform.With(r => { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(20, 0); r.offsetMax = new Vector2(-12, 0); });

        var btn = go.AddComponent<Button>(); btn.targetGraphic = bg;
        var cb = btn.colors;
        cb.normalColor = new Color(0.04f, 0.06f, 0.13f, 0.95f);
        cb.highlightedColor = new Color(col.r * 0.18f, col.g * 0.18f, col.b * 0.18f, 0.97f);
        cb.pressedColor = new Color(col.r * 0.30f, col.g * 0.30f, col.b * 0.30f, 1.00f);
        cb.colorMultiplier = 1f; btn.colors = cb;
        MkHover(go, 1.025f, 0.975f);
    }

    // ── Volume row ────────────────────────────────────────────
    static void MkVolRow(RectTransform parent, string rowName, string label, Color col, float yMin, float yMax)
    {
        var row = new GameObject(rowName, typeof(RectTransform)); row.transform.SetParent(parent, false);
        row.GetComponent<RectTransform>().With(r => { r.anchorMin = new Vector2(0.04f, yMin); r.anchorMax = new Vector2(0.96f, yMax); r.offsetMin = new Vector2(0, 3); r.offsetMax = new Vector2(0, -3); });
        MkTxt(row.transform, "Lbl", label, 25, Color.white, TextAlignmentOptions.MidlineLeft)
            .rectTransform.With(r => { r.anchorMin = Vector2.zero; r.anchorMax = new Vector2(0.32f, 1); r.offsetMin = new Vector2(8, 0); r.offsetMax = Vector2.zero; });
        var track = MkImg(row.transform, "Track", new Color(1, 1, 1, 0.09f));
        track.rectTransform.With(r => { r.anchorMin = new Vector2(0.34f, 0.20f); r.anchorMax = new Vector2(0.93f, 0.80f); r.offsetMin = r.offsetMax = Vector2.zero; });
        MkImg(track.rectTransform, "Fill", col).With(i => { i.raycastTarget = false; i.rectTransform.anchorMin = Vector2.zero; i.rectTransform.anchorMax = new Vector2(0.80f, 1); i.rectTransform.offsetMin = new Vector2(2, 2); i.rectTransform.offsetMax = new Vector2(-2, -2); });
    }

    // ── Helpers ───────────────────────────────────────────────
    static GameObject MkPanel(RectTransform cRT, string name, float xMin, float yMin, float xMax, float yMax)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(cRT, false);
        go.GetComponent<RectTransform>().With(r => { r.anchorMin = new Vector2(xMin, yMin); r.anchorMax = new Vector2(xMax, yMax); r.offsetMin = r.offsetMax = Vector2.zero; });
        go.AddComponent<Image>().color = PanelBg; return go;
    }

    static void TopBar(RectTransform p, Color col)
    {
        MkImg(p, "TopBar", col).With(i => { i.raycastTarget = false; i.rectTransform.anchorMin = new Vector2(0, 1); i.rectTransform.anchorMax = new Vector2(1, 1); i.rectTransform.pivot = new Vector2(0.5f, 1); i.rectTransform.sizeDelta = new Vector2(0, 5); });
    }

    static void MkHover(GameObject go, float h, float p, float spd = 10f)
    { var hv = go.AddComponent<MenuHoverButton>(); hv.normalScale = Vector3.one; hv.hoverScale = new Vector3(h, h, 1f); hv.pressScale = new Vector3(p, p, 1f); hv.animSpeed = spd; }

    static void SetAnch(RectTransform rt, float xMin, float yMin, float xMax, float yMax, float ox = 0, float oy = 0, float ox2 = 0, float oy2 = 0)
    { rt.anchorMin = new Vector2(xMin, yMin); rt.anchorMax = new Vector2(xMax, yMax); rt.offsetMin = new Vector2(ox, oy); rt.offsetMax = new Vector2(ox2, oy2); }

    static Sprite FindSprite(string n) { foreach (var g in AssetDatabase.FindAssets(n + " t:Sprite")) { var p = AssetDatabase.GUIDToAssetPath(g); if (Path.GetFileNameWithoutExtension(p).ToLower() == n.ToLower()) { var s = AssetDatabase.LoadAssetAtPath<Sprite>(p); if (s != null) return s; } } return null; }
    static Image MkImg(Transform p, string n, Color c) { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); var i = go.AddComponent<Image>(); i.color = c; return i; }
    static Image MkImg(RectTransform p, string n, Color c) => MkImg((Transform)p, n, c);
    static TMP_Text MkTxt(Transform p, string n, string txt, int sz, Color col, TextAlignmentOptions al, FontStyles fs = FontStyles.Normal)
    { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); var t = go.AddComponent<TextMeshProUGUI>(); t.text = txt; t.fontSize = sz; t.color = col; t.alignment = al; t.fontStyle = fs; t.raycastTarget = false; return t; }
    static TMP_Text MkTxt(RectTransform p, string n, string txt, int sz, Color col, TextAlignmentOptions al, FontStyles fs = FontStyles.Normal) => MkTxt((Transform)p, n, txt, sz, col, al, fs);
    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
    static Color Hex(string h) => ColorUtility.TryParseHtmlString(h, out var c) ? c : Color.magenta;
    static void AddToBuild(string path) { var s = EditorBuildSettings.scenes.ToList(); if (!s.Any(x => x.path == path)) { s.Add(new EditorBuildSettingsScene(path, true)); EditorBuildSettings.scenes = s.ToArray(); } }
}

// ============================================================
//  With() extension — lets us configure a component inline
//  without a separate variable. Only used inside the builder.
// ============================================================
public static class MMBExtensions
{
    public static T With<T>(this T obj, System.Action<T> fn) { fn(obj); return obj; }
}

// ============================================================
//  MainMenuManager  (Runtime)
//
//  WireButtons() uses GetComponentsInChildren(includeInactive:true)
//  on each panel so inactive panels are still searched correctly.
//  This was the bug: GameObject.Find() skips inactive objects.
// ============================================================
public class MainMenuManager : MonoBehaviour
{
    [Header("Panels")]
    public GameObject mainPanel;
    public GameObject arcadePanel;
    public GameObject settingsPanel;

    [Header("Fade")]
    public CanvasGroup canvasGroup;
    public float fadeDuration = 0.35f;

    bool _resetPending;
    float _resetTimer;
    const float ResetWindow = 4f;
    const string ResetLabel = "Reset All Progress";
    TMP_Text _resetLbl;

    void Awake()
    {
        // Kill any DDOL canvas that would eat our click events
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c != null && c.gameObject.scene.name == "DontDestroyOnLoad")
                Destroy(c.gameObject);
    }

    void Start()
    {
        // Show main panel, hide sub-panels
        SetActive(mainPanel, true);
        SetActive(arcadePanel, false);
        SetActive(settingsPanel, false);

        // Wire BEFORE hiding — or use the panel-aware search below
        WireButtons();

        if (canvasGroup) StartCoroutine(FadeIn());
        CacheResetLabel();
    }

    void Update()
    {
        if (_resetPending) { _resetTimer -= Time.unscaledDeltaTime; if (_resetTimer <= 0f) CancelReset(); }
    }

    // ── Button wiring ─────────────────────────────────────────
    // Searches each panel with includeInactive:true so buttons inside
    // disabled panels are still found and wired correctly.
    void WireButtons()
    {
        // Main panel buttons (panel is active so normal search works too,
        // but we use the panel-scoped search for consistency)
        BindIn(mainPanel, "StoryBtn", OnStoryMode);
        BindIn(mainPanel, "ArcadeBtn", OnArcadeMode);
        BindIn(mainPanel, "SettingsBtn", OnSettings);
        BindIn(mainPanel, "ExitBtn", OnExit);

        // Arcade panel buttons — panel is INACTIVE, must use includeInactive
        BindIn(arcadePanel, "EmailCard", OnPlayEmailSwiper);
        BindIn(arcadePanel, "SpotCard", OnPlaySpotDiff);
        BindIn(arcadePanel, "TowerCard", OnPlayTowerDefense);
        BindIn(arcadePanel, "ArcadeBackBtn", OnArcadeBack);

        // Settings panel buttons — panel is INACTIVE, must use includeInactive
        BindIn(settingsPanel, "ResetBtn", OnResetProgress);
        BindIn(settingsPanel, "SettingsBackBtn", OnSettingsBack);
    }

    // Finds a Button by name inside a parent (including inactive children)
    // and adds the listener. Logs a clear error if anything is missing.
    void BindIn(GameObject parent, string childName, UnityEngine.Events.UnityAction action)
    {
        if (parent == null) { Debug.LogError("[MMM] Parent is null when looking for: " + childName); return; }

        // Search all Buttons in the hierarchy (including inactive)
        var buttons = parent.GetComponentsInChildren<Button>(includeInactive: true);
        foreach (var btn in buttons)
        {
            if (btn.gameObject.name == childName)
            {
                btn.onClick.AddListener(action);
                return;
            }
        }
        Debug.LogError("[MMM] Button not found in " + parent.name + ": " + childName);
    }

    static void SetActive(GameObject g, bool v) { if (g) g.SetActive(v); }

    IEnumerator FadeIn()
    {
        canvasGroup.alpha = 0f; float t = 0f;
        while (t < fadeDuration) { t += Time.unscaledDeltaTime; canvasGroup.alpha = Mathf.Clamp01(t / fadeDuration); yield return null; }
        canvasGroup.alpha = 1f;
    }

    IEnumerator FadeLoad(string scene)
    {
        if (canvasGroup) { float t = 0f; while (t < fadeDuration) { t += Time.unscaledDeltaTime; canvasGroup.alpha = 1f - Mathf.Clamp01(t / fadeDuration); yield return null; } }
        SceneManager.LoadScene(scene);
    }

    void CacheResetLabel()
    {
        if (!settingsPanel) return;
        var buttons = settingsPanel.GetComponentsInChildren<Button>(includeInactive: true);
        foreach (var btn in buttons)
        {
            if (btn.gameObject.name == "ResetBtn")
            {
                var lbl = btn.transform.Find("Label");
                if (lbl) _resetLbl = lbl.GetComponent<TMP_Text>();
                break;
            }
        }
    }

    // ── Main panel ────────────────────────────────────────────
    public void OnStoryMode()
    {
        string dest = PlayerPrefs.GetInt("backstory_seen", 0) == 0 ? "Backstory" : "WorldMap";
        StartCoroutine(FadeLoad(dest));
    }
    public void OnArcadeMode() { SetActive(mainPanel, false); SetActive(arcadePanel, true); }
    public void OnSettings() { SetActive(mainPanel, false); SetActive(settingsPanel, true); }
    public void OnExit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ── Arcade panel ──────────────────────────────────────────
    public void OnPlayEmailSwiper() => StartCoroutine(FadeLoad("EmailSwiper"));
    public void OnPlaySpotDiff() => StartCoroutine(FadeLoad("SpotDifference"));
    public void OnPlayTowerDefense() => StartCoroutine(FadeLoad("TowerDefense"));
    public void OnArcadeBack() { SetActive(arcadePanel, false); SetActive(mainPanel, true); }

    // ── Settings panel ────────────────────────────────────────
    public void OnSettingsBack() { CancelReset(); SetActive(settingsPanel, false); SetActive(mainPanel, true); }

    public void OnResetProgress()
    {
        if (!_resetPending)
        {
            _resetPending = true; _resetTimer = ResetWindow;
            if (_resetLbl) _resetLbl.text = "Tap again to confirm!";
            return;
        }
        DoReset();
    }

    void DoReset()
    {
        string[] keys =
        {
            "pp_total_xp","pp_pending_xp","pp_discovered_fish","backstory_seen",
            "completed_ApartmentInterior",   "completed_PizzaInterior",        "completed_OfficeInterior",
            "completed_FlatInterior",        "completed_PizzaW2Interior",      "completed_OfficeW2Interior",
            "completed_AuntCarolInterior",   "completed_UncleMarcusInterior",  "completed_GrandpaLouInterior",
            "completed_GrandmaIrisInterior", "completed_UncleFelixInterior",   "completed_AuntDanaInterior",
            "completed_GrandpaErnestInterior","completed_AuntPriyaInterior",   "completed_UncleDiegoInterior",
            "vol_master","vol_music","vol_sfx","acc_largetext","acc_highcontrast",
            "mg_theme_world","interior_result","interior_source"
        };
        foreach (var k in keys) PlayerPrefs.DeleteKey(k);
        PlayerPrefs.Save();

        _resetPending = false;
        if (_resetLbl) _resetLbl.text = "All Progress Reset!";
        StartCoroutine(RestoreLabel(2.2f));
    }

    void CancelReset() { _resetPending = false; if (_resetLbl) _resetLbl.text = ResetLabel; }
    IEnumerator RestoreLabel(float d) { yield return new WaitForSecondsRealtime(d); if (_resetLbl) _resetLbl.text = ResetLabel; }
}

// ============================================================
//  SettingsManager  (placeholder — assign Sliders in Inspector)
// ============================================================
public class SettingsManager : MonoBehaviour
{
    public Slider masterVolume, musicVolume, sfxVolume;
    public Toggle largeTextToggle, highContrastToggle;
    void Start()
    {
        Bind(masterVolume, "vol_master", 1.0f, v => { AudioListener.volume = v; PlayerPrefs.SetFloat("vol_master", v); });
        Bind(musicVolume, "vol_music", 0.8f, v => PlayerPrefs.SetFloat("vol_music", v));
        Bind(sfxVolume, "vol_sfx", 1.0f, v => PlayerPrefs.SetFloat("vol_sfx", v));
        BindT(largeTextToggle, "acc_largetext", false);
        BindT(highContrastToggle, "acc_highcontrast", false);
        AudioListener.volume = PlayerPrefs.GetFloat("vol_master", 1f);
    }
    static void Bind(Slider s, string k, float d, UnityEngine.Events.UnityAction<float> cb) { if (!s) return; s.value = PlayerPrefs.GetFloat(k, d); s.onValueChanged.AddListener(cb); }
    static void BindT(Toggle t, string k, bool d) { if (!t) return; t.isOn = PlayerPrefs.GetInt(k, d ? 1 : 0) == 1; t.onValueChanged.AddListener(v => PlayerPrefs.SetInt(k, v ? 1 : 0)); }
}

// ============================================================
//  DoorTrigger  (unchanged)
// ============================================================
public class DoorTrigger : MonoBehaviour
{
    public Transform player; public string targetScene; public float triggerRadius = 0.6f;
    public GameObject prompt; public float promptProximity = 2.3f, bobAmount = 0.10f, bobSpeed = 4f;
    bool _fired; Vector3 _base; float _bt;
    void Start() { if (prompt) { _base = prompt.transform.localPosition; prompt.SetActive(false); } }
    void Update()
    {
        if (!player || _fired) return; float d = Vector2.Distance(player.position, transform.position);
        if (prompt) { bool n = d <= promptProximity; if (prompt.activeSelf != n) prompt.SetActive(n); if (n) { _bt += Time.deltaTime; prompt.transform.localPosition = _base + new Vector3(0, Mathf.Sin(_bt * bobSpeed) * bobAmount, 0); } }
        if (d <= triggerRadius && !string.IsNullOrEmpty(targetScene)) { _fired = true; SceneManager.LoadScene(targetScene); }
    }
}

// ============================================================
//  SceneLoader  (unchanged)
// ============================================================
public class SceneLoader : MonoBehaviour
{
    public string sceneName;
    public void Load() { if (!string.IsNullOrEmpty(sceneName)) SceneManager.LoadScene(sceneName); }
}