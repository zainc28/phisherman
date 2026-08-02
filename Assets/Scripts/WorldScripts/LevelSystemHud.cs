using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  LevelSystemHUD
//
//  Drop this on ANY GameObject in a world-map scene.
//  It finds (or creates) a ScreenSpace-Overlay Canvas and builds:
//    • Level badge + XP bar   (top centre)
//    • Sticker-book button    (right of bar)
//    • Sticker-book overlay   (full-screen, hidden until clicked)
//
//  On Start it checks for pending XP from a minigame and
//  plays a smooth fill + level-up animation.
// ============================================================
public class LevelSystemHUD : MonoBehaviour
{
    // ── Palette ──
    static readonly Color BarBg = new Color(0.12f, 0.14f, 0.22f, 0.88f);
    static readonly Color BarFill = new Color(0.30f, 0.78f, 0.95f, 1f);
    static readonly Color BarFlash = new Color(1f, 0.85f, 0.25f, 1f);
    static readonly Color LevelCircle = new Color(0.18f, 0.55f, 0.82f, 1f);
    static readonly Color BookBtn = new Color(0.72f, 0.53f, 0.30f, 1f);
    static readonly Color BookBtnDark = new Color(0.10f, 0.12f, 0.18f, 0.92f);
    static readonly Color PanelBg = new Color(0.05f, 0.07f, 0.14f, 0.96f);
    static readonly Color AccentTeal = new Color(0.31f, 0.80f, 0.77f);
    static readonly Color GreyedOut = new Color(0.25f, 0.25f, 0.30f, 0.70f);

    [Header("Sticker Book Sprites")]
    [Tooltip("23 sprites in catalog order: fish_1 clownfish_normal through fish_20 black, then td_hanging_fish, shark, pufferfish_1_default.")]
    public Sprite[] stickerSprites;

    // Runtime lookup built from stickerSprites
    System.Collections.Generic.Dictionary<string, Sprite> _spriteMap
        = new System.Collections.Generic.Dictionary<string, Sprite>();

    // ── Built references ──
    Canvas _canvas;
    RectTransform _canvasRT;
    RectTransform _hudRoot;
    Image _barFillImg;
    TMP_Text _levelText;
    TMP_Text _xpText;
    RectTransform _levelCircleRT;
    GameObject _levelUpBanner;
    TMP_Text _levelUpText;

    // Sticker book
    GameObject _stickerPanel;
    Transform _gridParent;
    TMP_Text _detailName, _detailDesc;
    Image _detailImage;
    GameObject _detailPanel;
    List<StickerSlot> _slots = new List<StickerSlot>();
    struct StickerSlot
    {
        public string fishId;
        public Image icon;
        public TMP_Text label;
        public Button button;
    }

    // =================================================================
    // Lifecycle
    // =================================================================

    void Start()
    {
        // Build fish-ID → sprite lookup from the inspector-assigned sticker array
        _spriteMap.Clear();
        var catalog = PlayerProgress.FishCatalog;
        if (stickerSprites != null)
        {
            for (int i = 0; i < Mathf.Min(stickerSprites.Length, catalog.Length); i++)
                if (stickerSprites[i] != null) _spriteMap[catalog[i].id] = stickerSprites[i];
        }

        FindOrCreateCanvas();
        BuildHUD();
        BuildStickerBookPanel();
        RefreshHUD(false);

        // Check for pending XP from a minigame
        int pending = PlayerProgress.GetPendingXP();
        if (pending > 0)
        {
            PlayerProgress.ClearPendingXP();
            StartCoroutine(AnimateXPGain(pending));
        }
    }

    // =================================================================
    // Canvas
    // =================================================================

    void FindOrCreateCanvas()
    {
        // Prefer existing overlay canvas
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (c.renderMode == RenderMode.ScreenSpaceOverlay)
            { _canvas = c; break; }
        }
        if (_canvas == null)
        {
            var go = new GameObject("LevelCanvas");
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 50;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
        }
        _canvasRT = _canvas.GetComponent<RectTransform>();
    }

    // =================================================================
    // Build HUD  (top centre bar)
    // =================================================================

    void BuildHUD()
    {
        // Root container — top centre
        _hudRoot = MakeRT(_canvasRT, "LevelHUD");
        _hudRoot.anchorMin = new Vector2(0.5f, 1f);
        _hudRoot.anchorMax = new Vector2(0.5f, 1f);
        _hudRoot.pivot = new Vector2(0.5f, 1f);
        _hudRoot.sizeDelta = new Vector2(460, 56);
        _hudRoot.anchoredPosition = new Vector2(0, -12);

        // Dark backdrop
        var bg = _hudRoot.gameObject.AddComponent<Image>();
        bg.color = BarBg;
        bg.raycastTarget = false;

        // ── Level circle (left) ──
        _levelCircleRT = MakeRT(_hudRoot, "LevelCircle");
        _levelCircleRT.anchorMin = new Vector2(0, 0.5f);
        _levelCircleRT.anchorMax = new Vector2(0, 0.5f);
        _levelCircleRT.pivot = new Vector2(0.5f, 0.5f);
        _levelCircleRT.sizeDelta = new Vector2(52, 52);
        _levelCircleRT.anchoredPosition = new Vector2(32, 0);
        var circleImg = _levelCircleRT.gameObject.AddComponent<Image>();
        circleImg.color = LevelCircle;
        circleImg.raycastTarget = false;

        // Level number text
        var lvGo = new GameObject("LvText", typeof(RectTransform));
        lvGo.transform.SetParent(_levelCircleRT, false);
        _levelText = lvGo.AddComponent<TextMeshProUGUI>();
        _levelText.text = "1";
        _levelText.fontSize = 24;
        _levelText.fontStyle = FontStyles.Bold;
        _levelText.color = Color.white;
        _levelText.alignment = TextAlignmentOptions.Center;
        _levelText.raycastTarget = false;
        Stretch(lvGo.GetComponent<RectTransform>());

        // ── XP bar track (centre) ──
        var trackRT = MakeRT(_hudRoot, "BarTrack");
        trackRT.anchorMin = new Vector2(0, 0.5f);
        trackRT.anchorMax = new Vector2(0, 0.5f);
        trackRT.pivot = new Vector2(0, 0.5f);
        trackRT.sizeDelta = new Vector2(280, 22);
        trackRT.anchoredPosition = new Vector2(66, 0);
        var trackImg = trackRT.gameObject.AddComponent<Image>();
        trackImg.color = new Color(0.08f, 0.10f, 0.16f, 0.90f);
        trackImg.raycastTarget = false;

        // Fill
        var fillRT = MakeRT(trackRT, "BarFill");
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = new Vector2(0, 1);
        fillRT.pivot = new Vector2(0, 0.5f);
        fillRT.offsetMin = new Vector2(2, 2);
        fillRT.offsetMax = new Vector2(-2, -2);
        _barFillImg = fillRT.gameObject.AddComponent<Image>();
        _barFillImg.color = BarFill;
        _barFillImg.raycastTarget = false;

        // XP text overlay
        var xpGo = new GameObject("XPText", typeof(RectTransform));
        xpGo.transform.SetParent(trackRT, false);
        _xpText = xpGo.AddComponent<TextMeshProUGUI>();
        _xpText.fontSize = 14;
        _xpText.color = new Color(1, 1, 1, 0.85f);
        _xpText.alignment = TextAlignmentOptions.Center;
        _xpText.fontStyle = FontStyles.Bold;
        _xpText.raycastTarget = false;
        Stretch(xpGo.GetComponent<RectTransform>());

        // ── Sticker-book button (right) ──
        var btnRT = MakeRT(_hudRoot, "StickerBtn");
        btnRT.anchorMin = new Vector2(1, 0);
        btnRT.anchorMax = new Vector2(1, 1);
        btnRT.pivot = new Vector2(1, 0.5f);
        btnRT.sizeDelta = new Vector2(100, 0);
        btnRT.anchoredPosition = new Vector2(-4, 0);
        btnRT.offsetMin = new Vector2(btnRT.offsetMin.x, 4);
        btnRT.offsetMax = new Vector2(btnRT.offsetMax.x, -4);

        // Book button visuals
        var btnBg = btnRT.gameObject.AddComponent<Image>();
        btnBg.color = BookBtnDark;

        // Book icon (procedural: spine + cover)
        BuildBookIcon(btnRT);

        // Button label
        var btnLbl = new GameObject("Lbl", typeof(RectTransform));
        btnLbl.transform.SetParent(btnRT, false);
        var btnTmp = btnLbl.AddComponent<TextMeshProUGUI>();
        btnTmp.text = "Book";
        btnTmp.fontSize = 15;
        btnTmp.fontStyle = FontStyles.Bold;
        btnTmp.color = BookBtn;
        btnTmp.alignment = TextAlignmentOptions.Bottom;
        btnTmp.raycastTarget = false;
        var blrt = btnLbl.GetComponent<RectTransform>();
        blrt.anchorMin = Vector2.zero; blrt.anchorMax = Vector2.one;
        blrt.offsetMin = new Vector2(4, 2); blrt.offsetMax = new Vector2(-4, -2);

        var btn = btnRT.gameObject.AddComponent<Button>();
        btn.targetGraphic = btnBg;
        var cols = btn.colors;
        cols.normalColor = BookBtnDark;
        cols.highlightedColor = new Color(BookBtn.r * 0.3f, BookBtn.g * 0.3f, BookBtn.b * 0.3f, 0.95f);
        cols.pressedColor = new Color(BookBtn.r * 0.5f, BookBtn.g * 0.5f, BookBtn.b * 0.5f, 1f);
        btn.colors = cols;
        btn.onClick.AddListener(ToggleStickerBook);

        // ── Level-up banner (hidden) ──
        var bannerGo = new GameObject("LevelUpBanner", typeof(RectTransform));
        bannerGo.transform.SetParent(_canvasRT, false);
        var brt = bannerGo.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0.5f, 0.5f);
        brt.anchorMax = new Vector2(0.5f, 0.5f);
        brt.pivot = new Vector2(0.5f, 0.5f);
        brt.sizeDelta = new Vector2(420, 90);
        var bannerBg = bannerGo.AddComponent<Image>();
        bannerBg.color = new Color(0.10f, 0.12f, 0.20f, 0.95f);
        bannerBg.raycastTarget = false;

        // Gold top border
        var goldBar = MakeRT(brt, "Gold");
        goldBar.anchorMin = new Vector2(0, 1); goldBar.anchorMax = new Vector2(1, 1);
        goldBar.pivot = new Vector2(0.5f, 1); goldBar.sizeDelta = new Vector2(0, 5);
        var gImg = goldBar.gameObject.AddComponent<Image>();
        gImg.color = BarFlash; gImg.raycastTarget = false;

        var luGo = new GameObject("Text", typeof(RectTransform));
        luGo.transform.SetParent(brt, false);
        _levelUpText = luGo.AddComponent<TextMeshProUGUI>();
        _levelUpText.text = "LEVEL UP!";
        _levelUpText.fontSize = 38;
        _levelUpText.fontStyle = FontStyles.Bold;
        _levelUpText.color = BarFlash;
        _levelUpText.alignment = TextAlignmentOptions.Center;
        _levelUpText.raycastTarget = false;
        Stretch(luGo.GetComponent<RectTransform>());

        _levelUpBanner = bannerGo;
        _levelUpBanner.SetActive(false);
    }

    /// <summary>Draws a tiny procedural book icon (spine + pages).</summary>
    void BuildBookIcon(RectTransform parent)
    {
        // Book cover
        var cover = MakeRT(parent, "BookCover");
        cover.anchorMin = new Vector2(0.5f, 0.5f);
        cover.anchorMax = new Vector2(0.5f, 0.5f);
        cover.pivot = new Vector2(0.5f, 0.5f);
        cover.sizeDelta = new Vector2(28, 30);
        cover.anchoredPosition = new Vector2(0, 5);
        var coverImg = cover.gameObject.AddComponent<Image>();
        coverImg.color = BookBtn; coverImg.raycastTarget = false;

        // Spine
        var spine = MakeRT(cover, "Spine");
        spine.anchorMin = new Vector2(0, 0);
        spine.anchorMax = new Vector2(0, 1);
        spine.pivot = new Vector2(0, 0.5f);
        spine.sizeDelta = new Vector2(5, 0);
        spine.anchoredPosition = Vector2.zero;
        var spineImg = spine.gameObject.AddComponent<Image>();
        spineImg.color = new Color(BookBtn.r * 0.6f, BookBtn.g * 0.6f, BookBtn.b * 0.6f);
        spineImg.raycastTarget = false;

        // Pages (lighter inner rect)
        var pages = MakeRT(cover, "Pages");
        pages.anchorMin = Vector2.zero; pages.anchorMax = Vector2.one;
        pages.offsetMin = new Vector2(7, 3); pages.offsetMax = new Vector2(-3, -3);
        var pagesImg = pages.gameObject.AddComponent<Image>();
        pagesImg.color = new Color(0.95f, 0.92f, 0.85f);
        pagesImg.raycastTarget = false;

        // Lines on pages
        for (int i = 0; i < 3; i++)
        {
            var line = MakeRT(pages, "Line" + i);
            line.anchorMin = new Vector2(0.1f, 0);
            line.anchorMax = new Vector2(0.9f, 0);
            line.pivot = new Vector2(0.5f, 0);
            line.sizeDelta = new Vector2(0, 2);
            line.anchoredPosition = new Vector2(0, 4 + i * 7);
            var lineImg = line.gameObject.AddComponent<Image>();
            lineImg.color = new Color(0.6f, 0.55f, 0.45f, 0.5f);
            lineImg.raycastTarget = false;
        }
    }

    // =================================================================
    // Refresh
    // =================================================================

    void RefreshHUD(bool animated = false)
    {
        int level = PlayerProgress.GetLevel();
        _levelText.text = level.ToString();

        float fill = PlayerProgress.GetXPProgress01();
        if (!animated)
            SetBarFill(fill);

        int cur = PlayerProgress.GetXPInCurrentLevel();
        int need = PlayerProgress.GetXPNeededForNextLevel();
        _xpText.text = $"{cur} / {need}";
    }

    void SetBarFill(float t)
    {
        // Fill is done via anchorMax.x on the fill rect
        var rt = _barFillImg.GetComponent<RectTransform>();
        rt.anchorMax = new Vector2(Mathf.Clamp01(t), 1);
    }

    // =================================================================
    // XP gain animation
    // =================================================================

    IEnumerator AnimateXPGain(int xpToAdd)
    {
        // Snapshot state BEFORE adding
        float startFill = PlayerProgress.GetXPProgress01();
        int startLevel = PlayerProgress.GetLevel();

        // We'll add XP in incremental ticks for smooth animation
        int remaining = xpToAdd;
        float fillSpeed = 0.8f; // fill-fraction per second

        while (remaining > 0)
        {
            int currentLevel = PlayerProgress.GetLevel();
            int neededForNext = PlayerProgress.GetXPNeededForNextLevel();
            int currentInLevel = PlayerProgress.GetXPInCurrentLevel();
            int toFillLevel = neededForNext - currentInLevel;

            int chunk = Mathf.Min(remaining, toFillLevel);
            float fromFill = PlayerProgress.GetXPProgress01();

            // Add this chunk
            int oldLevel = PlayerProgress.AddXP(chunk);
            remaining -= chunk;

            float toFill = PlayerProgress.GetXPProgress01();
            int newLevel = PlayerProgress.GetLevel();

            // Animate bar fill
            float dist = toFill - fromFill;
            if (newLevel > oldLevel) dist = 1f - fromFill; // fill to 1.0 then reset

            float dur = Mathf.Max(0.3f, Mathf.Abs(dist) / fillSpeed);
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / dur);
                float eased = 1f - Mathf.Pow(1f - p, 2f);
                SetBarFill(Mathf.Lerp(fromFill, fromFill + dist, eased));

                // Update XP text during animation
                int displayXP = Mathf.RoundToInt(Mathf.Lerp(
                    fromFill * PlayerProgress.GetXPNeededForNextLevel(),
                    (fromFill + dist) * PlayerProgress.GetXPNeededForNextLevel(), eased));
                _xpText.text = $"{displayXP} / {PlayerProgress.GetXPNeededForNextLevel()}";

                yield return null;
            }

            // Level up!
            if (newLevel > oldLevel)
            {
                yield return StartCoroutine(LevelUpAnimation(newLevel));
                // Reset bar to new fill
                SetBarFill(0f);
            }

            RefreshHUD(false);
        }

        // Flash bar briefly
        StartCoroutine(BarFlashAnim());
    }

    IEnumerator LevelUpAnimation(int newLevel)
    {
        _levelText.text = newLevel.ToString();

        // Bar flash gold
        _barFillImg.color = BarFlash;

        // Level circle pop
        StartCoroutine(PunchScale(_levelCircleRT, 0.4f, 1.5f));

        // Show banner
        _levelUpBanner.SetActive(true);
        _levelUpText.text = $"LEVEL UP!  Lv {newLevel}";
        var bannerRT = _levelUpBanner.GetComponent<RectTransform>();
        bannerRT.localScale = Vector3.one * 0.5f;
        var bannerCG = _levelUpBanner.GetComponent<CanvasGroup>();
        if (bannerCG == null) bannerCG = _levelUpBanner.AddComponent<CanvasGroup>();
        bannerCG.alpha = 0f;

        // Banner pop in
        float t = 0f;
        while (t < 0.3f)
        {
            t += Time.deltaTime;
            float p = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.3f), 3f);
            bannerRT.localScale = Vector3.one * Mathf.Lerp(0.5f, 1.05f, p);
            bannerCG.alpha = p;
            yield return null;
        }
        bannerRT.localScale = Vector3.one;

        yield return new WaitForSeconds(1.5f);

        // Banner fade out
        t = 0f;
        while (t < 0.4f)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / 0.4f);
            bannerCG.alpha = 1f - p;
            bannerRT.anchoredPosition = new Vector2(0, p * 60f);
            yield return null;
        }
        _levelUpBanner.SetActive(false);
        bannerRT.anchoredPosition = Vector2.zero;

        // Restore bar colour
        _barFillImg.color = BarFill;
    }

    IEnumerator BarFlashAnim()
    {
        Color orig = BarFill;
        float t = 0f;
        while (t < 0.5f)
        {
            t += Time.deltaTime;
            float p = Mathf.Sin(Mathf.Clamp01(t / 0.5f) * Mathf.PI);
            _barFillImg.color = Color.Lerp(orig, BarFlash, p * 0.5f);
            yield return null;
        }
        _barFillImg.color = orig;
    }

    IEnumerator PunchScale(RectTransform rt, float dur, float peak)
    {
        Vector3 orig = rt.localScale;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            float s = 1f + (peak - 1f) * Mathf.Sin(p * Mathf.PI);
            rt.localScale = Vector3.one * s;
            yield return null;
        }
        rt.localScale = orig;
    }

    // =================================================================
    // Sticker Book
    // =================================================================

    void ToggleStickerBook()
    {
        bool show = !_stickerPanel.activeSelf;
        _stickerPanel.SetActive(show);
        if (show) RefreshStickerBook();
    }

    void BuildStickerBookPanel()
    {
        // Full-screen overlay
        var overlay = new GameObject("StickerBookPanel", typeof(RectTransform));
        overlay.transform.SetParent(_canvasRT, false);
        Stretch(overlay.GetComponent<RectTransform>());
        var overlayImg = overlay.AddComponent<Image>();
        overlayImg.color = new Color(0, 0, 0, 0.85f);
        overlayImg.raycastTarget = true;

        // Card
        var card = MakeRT(overlay.transform, "Card");
        card.anchorMin = new Vector2(0.08f, 0.06f);
        card.anchorMax = new Vector2(0.92f, 0.94f);
        card.offsetMin = card.offsetMax = Vector2.zero;
        var cardImg = card.gameObject.AddComponent<Image>();
        cardImg.color = PanelBg;
        cardImg.raycastTarget = false;

        // Top accent bar
        var accent = MakeRT(card, "Accent");
        accent.anchorMin = new Vector2(0, 1); accent.anchorMax = new Vector2(1, 1);
        accent.pivot = new Vector2(0.5f, 1); accent.sizeDelta = new Vector2(0, 5);
        var acImg = accent.gameObject.AddComponent<Image>();
        acImg.color = BookBtn; acImg.raycastTarget = false;

        // Title
        var title = MakeTMP(card, "Title", "STICKER BOOK", 36,
            BookBtn, TextAlignmentOptions.Center, FontStyles.Bold);
        var trt = title.rectTransform;
        trt.anchorMin = new Vector2(0, 0.90f); trt.anchorMax = new Vector2(1, 1f);
        trt.offsetMin = new Vector2(20, 0); trt.offsetMax = new Vector2(-20, -10);

        // Subtitle
        var sub = MakeTMP(card, "Sub", "Fish you've encountered on your adventures", 20,
            new Color(1, 1, 1, 0.55f), TextAlignmentOptions.Center);
        var srt = sub.rectTransform;
        srt.anchorMin = new Vector2(0, 0.85f); srt.anchorMax = new Vector2(1, 0.90f);
        srt.offsetMin = srt.offsetMax = Vector2.zero;

        // Discovery counter
        var counter = MakeTMP(card, "Counter", $"0 / {PlayerProgress.FishCatalog.Length} discovered", 18,
            AccentTeal, TextAlignmentOptions.Center, FontStyles.Bold);
        var crt2 = counter.rectTransform;
        crt2.anchorMin = new Vector2(0, 0.80f); crt2.anchorMax = new Vector2(1, 0.85f);
        crt2.offsetMin = crt2.offsetMax = Vector2.zero;

        // Grid area
        var gridArea = MakeRT(card, "GridArea");
        gridArea.anchorMin = new Vector2(0.03f, 0.14f);
        gridArea.anchorMax = new Vector2(0.97f, 0.79f);
        gridArea.offsetMin = gridArea.offsetMax = Vector2.zero;
        _gridParent = gridArea;

        // Build fish slots (5 columns × 5 rows = 25 slots for 23 fish)
        var catalog = PlayerProgress.FishCatalog;
        int cols = 5;
        float cellW = 1f / cols;
        int rows = Mathf.CeilToInt((float)catalog.Length / cols);
        float cellH = 1f / Mathf.Max(rows, 1);

        for (int i = 0; i < catalog.Length; i++)
        {
            int col = i % cols;
            int row = i / cols;

            var slot = MakeRT(gridArea, "Slot_" + catalog[i].id);
            slot.anchorMin = new Vector2(col * cellW, 1f - (row + 1) * cellH);
            slot.anchorMax = new Vector2((col + 1) * cellW, 1f - row * cellH);
            slot.offsetMin = new Vector2(6, 6);
            slot.offsetMax = new Vector2(-6, -6);

            // Card background
            var slotBg = slot.gameObject.AddComponent<Image>();
            slotBg.color = new Color(0.10f, 0.12f, 0.18f, 0.90f);

            // Border
            var border = MakeRT(slot, "Border");
            border.anchorMin = Vector2.zero; border.anchorMax = Vector2.one;
            border.offsetMin = new Vector2(-2, -2); border.offsetMax = new Vector2(2, 2);
            var borderImg = border.gameObject.AddComponent<Image>();
            borderImg.color = GreyedOut; borderImg.raycastTarget = false;

            // Fill over border
            var fill2 = MakeRT(slot, "Fill");
            fill2.anchorMin = Vector2.zero; fill2.anchorMax = Vector2.one;
            fill2.offsetMin = fill2.offsetMax = Vector2.zero;
            var fill2Img = fill2.gameObject.AddComponent<Image>();
            fill2Img.color = new Color(0.10f, 0.12f, 0.18f, 0.90f);
            fill2Img.raycastTarget = false;

            // Fish icon (circle with color)
            var iconRT = MakeRT(slot, "Icon");
            iconRT.anchorMin = new Vector2(0.5f, 0.55f);
            iconRT.anchorMax = new Vector2(0.5f, 0.55f);
            iconRT.pivot = new Vector2(0.5f, 0.5f);
            iconRT.sizeDelta = new Vector2(56, 56);
            var iconImg = iconRT.gameObject.AddComponent<Image>();
            iconImg.color = GreyedOut;
            iconImg.raycastTarget = false;

            // Name
            var nameText = MakeTMP(slot, "Name", "???", 16,
                GreyedOut, TextAlignmentOptions.Center, FontStyles.Bold);
            var nrt = nameText.rectTransform;
            nrt.anchorMin = new Vector2(0, 0);
            nrt.anchorMax = new Vector2(1, 0.28f);
            nrt.offsetMin = new Vector2(4, 4);
            nrt.offsetMax = new Vector2(-4, 0);

            // Button for detail
            var slotBtn = slot.gameObject.AddComponent<Button>();
            slotBtn.targetGraphic = slotBg;
            int idx = i; // capture
            slotBtn.onClick.AddListener(() => ShowFishDetail(idx));

            _slots.Add(new StickerSlot
            {
                fishId = catalog[i].id,
                icon = iconImg,
                label = nameText,
                button = slotBtn
            });
        }

        // ── Detail panel (bottom of card) ──
        var detailGo = MakeRT(card, "DetailPanel");
        detailGo.anchorMin = new Vector2(0.03f, 0.02f);
        detailGo.anchorMax = new Vector2(0.97f, 0.13f);
        detailGo.offsetMin = detailGo.offsetMax = Vector2.zero;
        var detBg = detailGo.gameObject.AddComponent<Image>();
        detBg.color = new Color(0.08f, 0.10f, 0.16f, 0.90f);
        detBg.raycastTarget = false;

        _detailName = MakeTMP(detailGo, "DName", "", 20,
            AccentTeal, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        var dnrt = _detailName.rectTransform;
        dnrt.anchorMin = new Vector2(0, 0); dnrt.anchorMax = new Vector2(0.35f, 1);
        dnrt.offsetMin = new Vector2(16, 4); dnrt.offsetMax = new Vector2(0, -4);

        _detailDesc = MakeTMP(detailGo, "DDesc", "Tap a fish to see its description.", 16,
            new Color(1, 1, 1, 0.65f), TextAlignmentOptions.MidlineLeft);
        _detailDesc.textWrappingMode = TextWrappingModes.Normal;
        var ddrt = _detailDesc.rectTransform;
        ddrt.anchorMin = new Vector2(0.36f, 0); ddrt.anchorMax = new Vector2(1, 1);
        ddrt.offsetMin = new Vector2(8, 4); ddrt.offsetMax = new Vector2(-12, -4);

        _detailPanel = detailGo.gameObject;

        // ── Close button ──
        var closeRT = MakeRT(card, "CloseBtn");
        closeRT.anchorMin = new Vector2(0.35f, 0.02f);
        closeRT.anchorMax = new Vector2(0.65f, 0.08f);
        // Shift close button to overlay level, outside detail panel
        closeRT.SetParent(overlay.GetComponent<RectTransform>(), true);
        closeRT.anchorMin = new Vector2(0.38f, 0.02f);
        closeRT.anchorMax = new Vector2(0.62f, 0.065f);
        closeRT.offsetMin = closeRT.offsetMax = Vector2.zero;
        var closeBg = closeRT.gameObject.AddComponent<Image>();
        closeBg.color = new Color(0.18f, 0.28f, 0.45f, 0.95f);
        var closeBtn = closeRT.gameObject.AddComponent<Button>();
        closeBtn.targetGraphic = closeBg;
        closeBtn.onClick.AddListener(() => _stickerPanel.SetActive(false));
        var closeTxt = MakeTMP(closeRT, "Lbl", "✕  Close", 22,
            Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(closeTxt.rectTransform);

        _stickerPanel = overlay;
        _stickerPanel.SetActive(false);
    }

    void RefreshStickerBook()
    {
        var discovered = PlayerProgress.GetDiscoveredFishSet();
        var catalog = PlayerProgress.FishCatalog;
        int found = 0;

        for (int i = 0; i < _slots.Count && i < catalog.Length; i++)
        {
            bool disc = discovered.Contains(catalog[i].id);
            if (disc) found++;

            // Always set the real sprite if we have one — when not discovered
            // we tint it with GreyedOut so it's a recognisable silhouette rather
            // than a blank coloured circle.
            if (_spriteMap.TryGetValue(catalog[i].id, out var spr) && spr != null)
            {
                _slots[i].icon.sprite = spr;
                _slots[i].icon.preserveAspect = true;
            }
            _slots[i].icon.color = disc ? Color.white : GreyedOut;
            _slots[i].label.text = disc ? catalog[i].name : "???";
            _slots[i].label.color = disc ? Color.white : GreyedOut;
        }

        // Update counter
        var counterText = _stickerPanel.GetComponentsInChildren<TMP_Text>();
        foreach (var t in counterText)
        {
            if (t.name == "Counter")
                t.text = $"{found} / {catalog.Length} discovered";
        }
    }

    void ShowFishDetail(int index)
    {
        var catalog = PlayerProgress.FishCatalog;
        if (index < 0 || index >= catalog.Length) return;

        bool disc = PlayerProgress.IsFishDiscovered(catalog[index].id);
        _detailName.text = disc ? catalog[index].name : "???";
        _detailDesc.text = disc
            ? catalog[index].description
            : "You haven't encountered this fish yet.\nKeep playing to discover it!";
        _detailName.color = disc ? catalog[index].tintWhenGreyed : GreyedOut;
    }

    // =================================================================
    // Utility
    // =================================================================

    static RectTransform MakeRT(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    static RectTransform MakeRT(RectTransform parent, string name)
        => MakeRT((Transform)parent, name);

    static TMP_Text MakeTMP(Transform parent, string name, string text,
        int size, Color col, TextAlignmentOptions align,
        FontStyles style = FontStyles.Normal)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.color = col;
        t.alignment = align; t.fontStyle = style;
        t.raycastTarget = false;
        return t;
    }

    static TMP_Text MakeTMP(RectTransform parent, string name, string text,
        int size, Color col, TextAlignmentOptions align,
        FontStyles style = FontStyles.Normal)
        => MakeTMP((Transform)parent, name, text, size, col, align, style);

    static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }
}