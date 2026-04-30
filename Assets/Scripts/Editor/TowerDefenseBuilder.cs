#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

public class TowerDefenseBuilder : EditorWindow
{
    static readonly Color BG = Hex("#2D1B55");
    static readonly Color HEADER_YEL = Hex("#FFD700");
    static readonly Color HEADER_CYN = Hex("#00C8E0");
    static readonly Color CARD_WHITE = Hex("#FFFFFF");
    static readonly Color TEXT_DARK = Hex("#2A1040");
    static readonly Color TEXT_MID = Hex("#5A3A7A");
    static readonly Color BTN_BLUE = Hex("#1E7BF0");
    static readonly Color BTN_GREEN = Hex("#1DB954");
    static readonly Color BTN_GREY = Hex("#7A7A9A");
    static readonly Color BTN_RED = Hex("#E8233A");
    static readonly Color PATH_COL = Hex("#3D2870");
    static readonly Color TOWER_COL = Hex("#4A90F0");

    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out Color c); return c; }

    [MenuItem("Phisherman/Build Tower Defense Scene")]
    public static void BuildScene()
    {
        // ============================
        // CLEAN SLATE
        // ============================
        string[] toDelete = { "TutorialPanel","HUDPanel","GameOverPanel","WinPanel",
                               "Tower","SpawnPoint","PathBackground","GameManager" };

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas != null)
            foreach (string n in toDelete)
            {
                Transform t = canvas.transform.Find(n);
                if (t != null) DestroyImmediate(t.gameObject);
            }
        // Add EventSystem if missing
        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        foreach (string n in toDelete)
        {
            GameObject g = GameObject.Find(n);
            if (g != null) DestroyImmediate(g);
        }

        // ============================
        // CAMERA
        // ============================
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.backgroundColor = BG;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.orthographic = true;
            cam.orthographicSize = 5f;
        }

        // ============================
        // CANVAS
        // ============================
        if (canvas == null)
        {
            GameObject cGO = new GameObject("Canvas");
            canvas = cGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var cs = cGO.AddComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920, 1080);
            cs.matchWidthOrHeight = 0.5f;
            cGO.AddComponent<GraphicRaycaster>();
        }
        else
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler cs = canvas.GetComponent<CanvasScaler>();
            if (cs != null) { cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; cs.referenceResolution = new Vector2(1920, 1080); }
        }

        // ============================
        // 2D WORLD — PATH
        // ============================
        GameObject path = GameObject.CreatePrimitive(PrimitiveType.Quad);
        path.name = "PathBackground";
        path.transform.position = new Vector3(-1f, 0f, 0.5f);
        path.transform.localScale = new Vector3(24f, 4.2f, 1f);
        path.GetComponent<Renderer>().material = new Material(Shader.Find("Sprites/Default")) { color = PATH_COL };
        DestroyImmediate(path.GetComponent<Collider>());

        // ============================
        // 2D WORLD — SPAWN POINT
        // ============================
        GameObject spawn = new GameObject("SpawnPoint");
        spawn.transform.position = new Vector3(-13f, 0f, 0f);

        // ============================
        // 2D WORLD — TOWER
        // ============================
        GameObject tower = new GameObject("Tower");
        tower.transform.position = new Vector3(8.5f, 0f, 0f);

        GameObject tSprite = new GameObject("TowerSprite");
        tSprite.transform.SetParent(tower.transform, false);
        tSprite.transform.localScale = new Vector3(1.6f, 3.2f, 1f);
        SpriteRenderer tsr = tSprite.AddComponent<SpriteRenderer>();
        tsr.sprite = MakeSprite(); tsr.color = TOWER_COL; tsr.sortingOrder = 2;

        GameObject tTop = new GameObject("TowerTop");
        tTop.transform.SetParent(tower.transform, false);
        tTop.transform.localPosition = new Vector3(0, 1.8f, 0);
        tTop.transform.localScale = new Vector3(2f, 0.5f, 1f);
        SpriteRenderer ttsr = tTop.AddComponent<SpriteRenderer>();
        ttsr.sprite = MakeSprite(); ttsr.color = Hex("#2563C4"); ttsr.sortingOrder = 3;

        // ============================
        // TUTORIAL PANEL
        // ============================
        GameObject tutPanel = MakePanel(canvas.transform, "TutorialPanel", BG);

        MakePanel(tutPanel.transform, "TopBar", HEADER_YEL).GetComponent<RectTransform>().SetTopStrip(0.82f);

        var tutTitle = MakeText(tutPanel.transform, "TutorialTitle", "Phish Patrol", 50, FontStyles.Bold, TEXT_DARK);
        SetRect(tutTitle, 0, 215, 860, 80);

        var tutCard = MakePanel(tutPanel.transform, "TutCard", CARD_WHITE);
        SetRect(tutCard, 0, -20, 860, 360);

        var tutBody = MakeText(tutCard.transform, "TutorialBody", "", 24, FontStyles.Normal, TEXT_DARK);
        FillParent(tutBody, 28, 28, 22, 22);
        TMP(tutBody).alignment = TextAlignmentOptions.TopLeft;
        TMP(tutBody).textWrappingMode = TextWrappingModes.Normal;

        var stepText = MakeText(tutPanel.transform, "TutorialStepText", "1 of 2", 20, FontStyles.Normal, new Color(1, 1, 1, 0.7f));
        SetRect(stepText, 0, -225, 200, 36);

        var tutBtn = MakeButton(tutPanel.transform, "TutorialNextButton", "Next", BTN_BLUE, 36);
        SetRect(tutBtn, 0, -278, 300, 76);
        BoldBtn(tutBtn);

        // ============================
        // HUD PANEL
        // ============================
        var hudPanel = MakePanel(canvas.transform, "HUDPanel", new Color(0, 0, 0, 0));
        hudPanel.SetActive(false);

        var hudBar = MakePanel(hudPanel.transform, "HUDBar", new Color(0, 0, 0, 0.55f));
        hudBar.GetComponent<RectTransform>().SetTopStrip(0.9f);

        var healthText = MakeText(hudBar.transform, "HealthText", "♥ ♥ ♥ ♥ ♥", 30, FontStyles.Bold, new Color(1f, 0.3f, 0.4f));
        SetRect(healthText, -300, 0, 400, 60);
        TMP(healthText).alignment = TextAlignmentOptions.Left;

        var scoreText = MakeText(hudBar.transform, "ScoreText", "Score: 0", 28, FontStyles.Bold, HEADER_YEL);
        SetRect(scoreText, 50, 0, 280, 60);

        var waveText = MakeText(hudBar.transform, "WaveText", "Wave 1 / 3", 26, FontStyles.Bold, Color.white);
        SetRect(waveText, 350, 0, 280, 60);
        TMP(waveText).alignment = TextAlignmentOptions.Right;

        var comboText = MakeText(hudPanel.transform, "ComboText", "", 34, FontStyles.Bold, HEADER_YEL);
        SetRect(comboText, 0, -380, 400, 60);

        // ============================
        // GAME OVER PANEL
        // ============================
        var gameOverPanel = MakePanel(canvas.transform, "GameOverPanel", new Color(0.15f, 0.05f, 0.3f, 0.95f));
        gameOverPanel.SetActive(false);

        MakeText(gameOverPanel.transform, "GOTitle", "Tower Destroyed", 54, FontStyles.Bold, BTN_RED);
        SetRect(gameOverPanel.transform.Find("GOTitle").gameObject, 0, 150, 800, 90);

        var goScore = MakeText(gameOverPanel.transform, "GameOverScoreText", "Score: 0", 38, FontStyles.Bold, Color.white);
        SetRect(goScore, 0, 50, 500, 70);

        var goMsg = MakeText(gameOverPanel.transform, "GameOverMessageText", "", 24, FontStyles.Normal, new Color(0.8f, 0.8f, 0.9f));
        SetRect(goMsg, 0, -40, 700, 90);
        TMP(goMsg).textWrappingMode = TextWrappingModes.Normal;

        var retryBtn = MakeButton(gameOverPanel.transform, "RetryButton", "Try Again", BTN_BLUE, 32); SetRect(retryBtn, 0, -165, 280, 74); BoldBtn(retryBtn);
        var goBackBtn = MakeButton(gameOverPanel.transform, "ReturnButton", "Return to Map", BTN_GREY, 26); SetRect(goBackBtn, 0, -260, 280, 66);

        // ============================
        // WIN PANEL
        // ============================
        var winPanel = MakePanel(canvas.transform, "WinPanel", new Color(0.05f, 0.3f, 0.1f, 0.95f));
        winPanel.SetActive(false);

        MakeText(winPanel.transform, "WinTitle", "Tower Defended!", 54, FontStyles.Bold, BTN_GREEN);
        SetRect(winPanel.transform.Find("WinTitle").gameObject, 0, 160, 800, 90);

        var winStars = MakeText(winPanel.transform, "WinStarsText", "* * *", 50, FontStyles.Bold, HEADER_YEL);
        SetRect(winStars, 0, 60, 500, 80);

        var winScore = MakeText(winPanel.transform, "WinScoreText", "0 / 0", 38, FontStyles.Bold, Color.white);
        SetRect(winScore, 0, -30, 500, 70);

        var winRetryBtn = MakeButton(winPanel.transform, "RetryButton", "Play Again", BTN_GREEN, 32); SetRect(winRetryBtn, 0, -155, 280, 74); BoldBtn(winRetryBtn);
        var winBackBtn = MakeButton(winPanel.transform, "ReturnButton", "Return to Map", BTN_GREY, 26); SetRect(winBackBtn, 0, -250, 280, 66);

        // ============================
        // GAME MANAGER + WIRING
        // ============================
        var gmGO = new GameObject("GameManager");
        var mgr = gmGO.AddComponent<TowerDefenseManager>();

        mgr.spawnPoint = spawn.transform;
        mgr.towerTransform = tower.transform;
        mgr.towerSprite = tsr;

        mgr.tutorialPanel = tutPanel;
        mgr.hudPanel = hudPanel;
        mgr.gameOverPanel = gameOverPanel;
        mgr.winPanel = winPanel;

        mgr.tutorialTitleText = TMP(tutTitle);
        mgr.tutorialBodyText = TMP(tutBody);
        mgr.tutorialStepText = TMP(stepText);
        mgr.tutorialNextButton = tutBtn.GetComponent<Button>();
        mgr.tutorialNextButtonText = TMP(tutBtn.transform.Find("Text (TMP)").gameObject);

        mgr.scoreText = TMP(scoreText);
        mgr.waveText = TMP(waveText);
        mgr.healthText = TMP(healthText);
        mgr.comboText = TMP(comboText);

        mgr.gameOverScoreText = TMP(goScore);
        mgr.gameOverMessageText = TMP(goMsg);

        mgr.winScoreText = TMP(winScore);
        mgr.winStarsText = TMP(winStars);

        Wire(tutBtn, mgr, "OnTutorialNext");
        Wire(retryBtn, mgr, "OnRetry");
        Wire(goBackBtn, mgr, "OnReturnToMap");
        Wire(winRetryBtn, mgr, "OnRetry");
        Wire(winBackBtn, mgr, "OnReturnToMap");

        EditorUtility.SetDirty(gmGO);
        AssetDatabase.SaveAssets();
        Debug.Log("Tower Defense scene built! Hit Play to test.");
    }

    // ============================
    // HELPERS — same as EmailSwiperBuilder
    // ============================
    static void Wire(GameObject btn, TowerDefenseManager target, string method)
    {
        Button b = btn.GetComponent<Button>();
        var m = typeof(TowerDefenseManager).GetMethod(method);
        if (b == null || m == null) { Debug.LogError("Wire failed: " + method); return; }
        var del = System.Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction), target, m) as UnityEngine.Events.UnityAction;
        UnityEditor.Events.UnityEventTools.AddPersistentListener(b.onClick, del);
    }

    static Sprite MakeSprite()
    {
        Texture2D tex = new Texture2D(4, 4);
        Color[] c = new Color[16]; for (int i = 0; i < 16; i++) c[i] = Color.white;
        tex.SetPixels(c); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
    }

    static GameObject MakePanel(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<Image>().color = color;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return go;
    }

    static GameObject MakeText(Transform parent, string name, string text, int size, FontStyles style, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.fontStyle = style; t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.Normal;
        return go;
    }

    static GameObject MakeButton(Transform parent, string name, string label, Color color, int fontSize)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>(); img.color = color;
        Button btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        ColorBlock cb = btn.colors;
        cb.highlightedColor = new Color(Mathf.Min(color.r + 0.12f, 1), Mathf.Min(color.g + 0.12f, 1), Mathf.Min(color.b + 0.12f, 1));
        cb.pressedColor = new Color(Mathf.Max(color.r - 0.12f, 0), Mathf.Max(color.g - 0.12f, 0), Mathf.Max(color.b - 0.12f, 0));
        btn.colors = cb;

        GameObject tgo = new GameObject("Text (TMP)");
        tgo.transform.SetParent(go.transform, false);
        TextMeshProUGUI tmp = tgo.AddComponent<TextMeshProUGUI>();
        tmp.text = label; tmp.fontSize = fontSize; tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        RectTransform tRT = tgo.GetComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
        tRT.offsetMin = tRT.offsetMax = Vector2.zero;
        return go;
    }

    static void SetRect(GameObject go, float x, float y, float w, float h)
    {
        RectTransform rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    static void FillParent(GameObject go, float l, float r, float t, float b)
    {
        RectTransform rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
    }

    static void BoldBtn(GameObject btn)
    {
        var t = btn.transform.Find("Text (TMP)");
        if (t != null) t.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
    }

    static TextMeshProUGUI TMP(GameObject go) => go.GetComponent<TextMeshProUGUI>();
}

// Reuse the same extension method (already defined in TowerDefenseManager.cs if needed)
public static class TDRectExtensions
{
    public static void SetTopStrip(this RectTransform rt, float yMin)
    {
        rt.anchorMin = new Vector2(0, yMin); rt.anchorMax = new Vector2(1, 1);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
#endif