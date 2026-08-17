using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// XP bar + sticker book HUD.
///
/// SPRITES: Assigned at build time by WorldMapBuilder via AssignStickerSprites().
/// They are serialized into the scene so no runtime asset search is needed.
/// The stickerSprites array is in catalog order (same order as PlayerProgress.FishCatalog).
/// </summary>
public class LevelSystemHUD : MonoBehaviour
{
    static readonly Color BarBg = new Color(0.12f, 0.14f, 0.22f, 0.88f);
    static readonly Color BarFill = new Color(0.30f, 0.78f, 0.95f, 1f);
    static readonly Color BarFlash = new Color(1f, 0.85f, 0.25f, 1f);
    static readonly Color LevelCircle = new Color(0.18f, 0.55f, 0.82f, 1f);
    static readonly Color BookBtn = new Color(0.72f, 0.53f, 0.30f, 1f);
    static readonly Color BookBtnDark = new Color(0.10f, 0.12f, 0.18f, 0.92f);
    static readonly Color PanelBg = new Color(0.05f, 0.07f, 0.14f, 0.96f);
    static readonly Color AccentTeal = new Color(0.31f, 0.80f, 0.77f);
    static readonly Color GreyedOut = new Color(0.25f, 0.25f, 0.30f, 0.70f);

    [Header("Sticker Sprites — assigned automatically by the scene builder")]
    [Tooltip("23 sprites in catalog order. Assigned by WorldMapBuilder at build time.")]
    public Sprite[] stickerSprites;

    Dictionary<string, Sprite> _spriteMap = new Dictionary<string, Sprite>();

    Canvas _canvas;
    RectTransform _canvasRT;
    RectTransform _hudRoot;
    Image _barFillImg;
    TMP_Text _levelText, _xpText;
    RectTransform _levelCircleRT;
    GameObject _levelUpBanner;
    TMP_Text _levelUpText;

    GameObject _stickerPanel;
    TMP_Text _detailName, _detailDesc;

    struct StickerSlot { public string fishId; public Image icon; public TMP_Text label; public Button button; }
    List<StickerSlot> _slots = new List<StickerSlot>();

    void Start()
    {
        BuildSpriteMap();
        FindOrCreateCanvas();
        BuildHUD();
        BuildStickerBookPanel();
        RefreshHUD(false);
        int pending = PlayerProgress.GetPendingXP();
        if (pending > 0) { PlayerProgress.ClearPendingXP(); StartCoroutine(AnimateXPGain(pending)); }
    }

    void BuildSpriteMap()
    {
        _spriteMap.Clear();
        var catalog = PlayerProgress.FishCatalog;
        if (stickerSprites == null) return;
        for (int i = 0; i < Mathf.Min(stickerSprites.Length, catalog.Length); i++)
            if (stickerSprites[i] != null) _spriteMap[catalog[i].id] = stickerSprites[i];
    }

    void FindOrCreateCanvas()
    {
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c.renderMode == RenderMode.ScreenSpaceOverlay) { _canvas = c; break; }
        if (_canvas == null)
        {
            var go = new GameObject("LevelCanvas");
            _canvas = go.AddComponent<Canvas>(); _canvas.renderMode = RenderMode.ScreenSpaceOverlay; _canvas.sortingOrder = 50;
            var sc = go.AddComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
        }
        _canvasRT = _canvas.GetComponent<RectTransform>();
    }

    void BuildHUD()
    {
        _hudRoot = MakeRT(_canvasRT, "LevelHUD");
        _hudRoot.anchorMin = new Vector2(0.5f, 1f); _hudRoot.anchorMax = new Vector2(0.5f, 1f); _hudRoot.pivot = new Vector2(0.5f, 1f); _hudRoot.sizeDelta = new Vector2(460, 56); _hudRoot.anchoredPosition = new Vector2(0, -12);
        _hudRoot.gameObject.AddComponent<Image>().color = BarBg;

        _levelCircleRT = MakeRT(_hudRoot, "LevelCircle");
        _levelCircleRT.anchorMin = new Vector2(0, 0.5f); _levelCircleRT.anchorMax = new Vector2(0, 0.5f); _levelCircleRT.pivot = new Vector2(0.5f, 0.5f); _levelCircleRT.sizeDelta = new Vector2(52, 52); _levelCircleRT.anchoredPosition = new Vector2(32, 0);
        _levelCircleRT.gameObject.AddComponent<Image>().color = LevelCircle;
        var lvGo = new GameObject("LvText", typeof(RectTransform)); lvGo.transform.SetParent(_levelCircleRT, false);
        _levelText = lvGo.AddComponent<TextMeshProUGUI>(); _levelText.text = "1"; _levelText.fontSize = 24; _levelText.fontStyle = FontStyles.Bold; _levelText.color = Color.white; _levelText.alignment = TextAlignmentOptions.Center; _levelText.raycastTarget = false;
        Stretch(lvGo.GetComponent<RectTransform>());

        var trackRT = MakeRT(_hudRoot, "BarTrack");
        trackRT.anchorMin = new Vector2(0, 0.5f); trackRT.anchorMax = new Vector2(0, 0.5f); trackRT.pivot = new Vector2(0, 0.5f); trackRT.sizeDelta = new Vector2(280, 22); trackRT.anchoredPosition = new Vector2(66, 0);
        trackRT.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.10f, 0.16f, 0.90f);
        var fillRT = MakeRT(trackRT, "BarFill");
        fillRT.anchorMin = Vector2.zero; fillRT.anchorMax = new Vector2(0, 1); fillRT.pivot = new Vector2(0, 0.5f); fillRT.offsetMin = new Vector2(2, 2); fillRT.offsetMax = new Vector2(-2, -2);
        _barFillImg = fillRT.gameObject.AddComponent<Image>(); _barFillImg.color = BarFill; _barFillImg.raycastTarget = false;
        var xpGo = new GameObject("XPText", typeof(RectTransform)); xpGo.transform.SetParent(trackRT, false);
        _xpText = xpGo.AddComponent<TextMeshProUGUI>(); _xpText.fontSize = 14; _xpText.color = new Color(1, 1, 1, 0.85f); _xpText.alignment = TextAlignmentOptions.Center; _xpText.fontStyle = FontStyles.Bold; _xpText.raycastTarget = false;
        Stretch(xpGo.GetComponent<RectTransform>());

        var btnRT = MakeRT(_hudRoot, "StickerBtn");
        btnRT.anchorMin = new Vector2(1, 0); btnRT.anchorMax = new Vector2(1, 1); btnRT.pivot = new Vector2(1, 0.5f); btnRT.sizeDelta = new Vector2(100, 0); btnRT.anchoredPosition = new Vector2(-4, 0);
        btnRT.offsetMin = new Vector2(btnRT.offsetMin.x, 4); btnRT.offsetMax = new Vector2(btnRT.offsetMax.x, -4);
        var btnBg = btnRT.gameObject.AddComponent<Image>(); btnBg.color = BookBtnDark;
        BuildBookIcon(btnRT);
        var lbl = new GameObject("Lbl", typeof(RectTransform)); lbl.transform.SetParent(btnRT, false);
        var lblT = lbl.AddComponent<TextMeshProUGUI>(); lblT.text = "Book"; lblT.fontSize = 15; lblT.fontStyle = FontStyles.Bold; lblT.color = BookBtn; lblT.alignment = TextAlignmentOptions.Bottom; lblT.raycastTarget = false;
        var lblRT = lbl.GetComponent<RectTransform>(); lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = Vector2.one; lblRT.offsetMin = new Vector2(4, 2); lblRT.offsetMax = new Vector2(-4, -2);
        var btn = btnRT.gameObject.AddComponent<Button>(); btn.targetGraphic = btnBg;
        var cols = btn.colors; cols.normalColor = BookBtnDark; cols.highlightedColor = new Color(BookBtn.r * 0.3f, BookBtn.g * 0.3f, BookBtn.b * 0.3f, 0.95f); cols.pressedColor = new Color(BookBtn.r * 0.5f, BookBtn.g * 0.5f, BookBtn.b * 0.5f, 1f); btn.colors = cols;
        btn.onClick.AddListener(ToggleStickerBook);

        var bannerGo = new GameObject("LevelUpBanner", typeof(RectTransform)); bannerGo.transform.SetParent(_canvasRT, false);
        var brt = bannerGo.GetComponent<RectTransform>(); brt.anchorMin = new Vector2(0.5f, 0.5f); brt.anchorMax = new Vector2(0.5f, 0.5f); brt.pivot = new Vector2(0.5f, 0.5f); brt.sizeDelta = new Vector2(420, 90);
        bannerGo.AddComponent<Image>().color = new Color(0.10f, 0.12f, 0.20f, 0.95f);
        var gbar = MakeRT(brt, "Gold"); gbar.anchorMin = new Vector2(0, 1); gbar.anchorMax = new Vector2(1, 1); gbar.pivot = new Vector2(0.5f, 1); gbar.sizeDelta = new Vector2(0, 5); gbar.gameObject.AddComponent<Image>().color = BarFlash;
        var luGo = new GameObject("Text", typeof(RectTransform)); luGo.transform.SetParent(brt, false);
        _levelUpText = luGo.AddComponent<TextMeshProUGUI>(); _levelUpText.text = "LEVEL UP!"; _levelUpText.fontSize = 38; _levelUpText.fontStyle = FontStyles.Bold; _levelUpText.color = BarFlash; _levelUpText.alignment = TextAlignmentOptions.Center; _levelUpText.raycastTarget = false;
        Stretch(luGo.GetComponent<RectTransform>());
        _levelUpBanner = bannerGo; _levelUpBanner.SetActive(false);
    }

    void BuildBookIcon(RectTransform parent)
    {
        var cover = MakeRT(parent, "BookCover"); cover.anchorMin = new Vector2(0.5f, 0.5f); cover.anchorMax = new Vector2(0.5f, 0.5f); cover.pivot = new Vector2(0.5f, 0.5f); cover.sizeDelta = new Vector2(28, 30); cover.anchoredPosition = new Vector2(0, 5);
        cover.gameObject.AddComponent<Image>().color = BookBtn;
        var spine = MakeRT(cover, "Spine"); spine.anchorMin = new Vector2(0, 0); spine.anchorMax = new Vector2(0, 1); spine.pivot = new Vector2(0, 0.5f); spine.sizeDelta = new Vector2(5, 0);
        spine.gameObject.AddComponent<Image>().color = new Color(BookBtn.r * 0.6f, BookBtn.g * 0.6f, BookBtn.b * 0.6f);
        var pages = MakeRT(cover, "Pages"); pages.anchorMin = Vector2.zero; pages.anchorMax = Vector2.one; pages.offsetMin = new Vector2(7, 3); pages.offsetMax = new Vector2(-3, -3);
        pages.gameObject.AddComponent<Image>().color = new Color(0.95f, 0.92f, 0.85f);
        for (int i = 0; i < 3; i++) { var line = MakeRT(pages, "Line" + i); line.anchorMin = new Vector2(0.1f, 0); line.anchorMax = new Vector2(0.9f, 0); line.pivot = new Vector2(0.5f, 0); line.sizeDelta = new Vector2(0, 2); line.anchoredPosition = new Vector2(0, 4 + i * 7); line.gameObject.AddComponent<Image>().color = new Color(0.6f, 0.55f, 0.45f, 0.5f); }
    }

    void RefreshHUD(bool animated = false)
    {
        _levelText.text = PlayerProgress.GetLevel().ToString();
        if (!animated) SetBarFill(PlayerProgress.GetXPProgress01());
        _xpText.text = $"{PlayerProgress.GetXPInCurrentLevel()} / {PlayerProgress.GetXPNeededForNextLevel()}";
    }

    void SetBarFill(float t) { _barFillImg.GetComponent<RectTransform>().anchorMax = new Vector2(Mathf.Clamp01(t), 1); }

    IEnumerator AnimateXPGain(int xpToAdd)
    {
        int rem = xpToAdd; float speed = 0.8f;
        while (rem > 0)
        {
            int need = PlayerProgress.GetXPNeededForNextLevel(), cur = PlayerProgress.GetXPInCurrentLevel(), chunk = Mathf.Min(rem, need - cur);
            float fromFill = PlayerProgress.GetXPProgress01(); int oldLv = PlayerProgress.AddXP(chunk); rem -= chunk; int newLv = PlayerProgress.GetLevel();
            float dist = (newLv > oldLv) ? 1f - fromFill : PlayerProgress.GetXPProgress01() - fromFill;
            float dur = Mathf.Max(0.3f, Mathf.Abs(dist) / speed), t = 0f;
            while (t < dur) { t += Time.deltaTime; float p = Mathf.Clamp01(t / dur), e = 1f - Mathf.Pow(1f - p, 2f); SetBarFill(Mathf.Lerp(fromFill, fromFill + dist, e)); _xpText.text = $"{Mathf.RoundToInt(Mathf.Lerp(fromFill, fromFill + dist, e) * PlayerProgress.GetXPNeededForNextLevel())} / {PlayerProgress.GetXPNeededForNextLevel()}"; yield return null; }
            if (newLv > oldLv) { yield return StartCoroutine(LevelUpAnimation(newLv)); SetBarFill(0f); }
            RefreshHUD(false);
        }
        StartCoroutine(BarFlashAnim());
    }

    IEnumerator LevelUpAnimation(int newLevel)
    {
        _levelText.text = newLevel.ToString(); _barFillImg.color = BarFlash; StartCoroutine(PunchScale(_levelCircleRT, 0.4f, 1.5f));
        _levelUpBanner.SetActive(true); _levelUpText.text = $"LEVEL UP!  Lv {newLevel}";
        var bRT = _levelUpBanner.GetComponent<RectTransform>(); bRT.localScale = Vector3.one * 0.5f;
        var cg = _levelUpBanner.GetComponent<CanvasGroup>() ?? _levelUpBanner.AddComponent<CanvasGroup>(); cg.alpha = 0f;
        float t = 0f; while (t < 0.3f) { t += Time.deltaTime; float p = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.3f), 3f); bRT.localScale = Vector3.one * Mathf.Lerp(0.5f, 1.05f, p); cg.alpha = p; yield return null; }
        bRT.localScale = Vector3.one; yield return new WaitForSeconds(1.5f);
        t = 0f; while (t < 0.4f) { t += Time.deltaTime; float p = Mathf.Clamp01(t / 0.4f); cg.alpha = 1f - p; bRT.anchoredPosition = new Vector2(0, p * 60f); yield return null; }
        _levelUpBanner.SetActive(false); bRT.anchoredPosition = Vector2.zero; _barFillImg.color = BarFill;
    }

    IEnumerator BarFlashAnim() { Color orig = BarFill; float t = 0f; while (t < 0.5f) { t += Time.deltaTime; _barFillImg.color = Color.Lerp(orig, BarFlash, Mathf.Sin(Mathf.Clamp01(t / 0.5f) * Mathf.PI) * 0.5f); yield return null; } _barFillImg.color = orig; }
    IEnumerator PunchScale(RectTransform rt, float dur, float peak) { Vector3 orig = rt.localScale; float t = 0f; while (t < dur) { t += Time.deltaTime; rt.localScale = Vector3.one * (1f + (peak - 1f) * Mathf.Sin(Mathf.Clamp01(t / dur) * Mathf.PI)); yield return null; } rt.localScale = orig; }

    void ToggleStickerBook() { bool show = !_stickerPanel.activeSelf; _stickerPanel.SetActive(show); if (show) RefreshStickerBook(); }

    void BuildStickerBookPanel()
    {
        var overlay = new GameObject("StickerBookPanel", typeof(RectTransform)); overlay.transform.SetParent(_canvasRT, false); Stretch(overlay.GetComponent<RectTransform>());
        var oi = overlay.AddComponent<Image>(); oi.color = new Color(0, 0, 0, 0.85f); oi.raycastTarget = true;
        var card = MakeRT(overlay.transform, "Card"); card.anchorMin = new Vector2(0.08f, 0.06f); card.anchorMax = new Vector2(0.92f, 0.94f); card.offsetMin = card.offsetMax = Vector2.zero;
        card.gameObject.AddComponent<Image>().color = PanelBg;
        var acb = MakeRT(card, "Accent"); acb.anchorMin = new Vector2(0, 1); acb.anchorMax = new Vector2(1, 1); acb.pivot = new Vector2(0.5f, 1); acb.sizeDelta = new Vector2(0, 5); acb.gameObject.AddComponent<Image>().color = BookBtn;

        SetAnchors(MakeTMP(card, "Title", "STICKER BOOK", 36, BookBtn, TextAlignmentOptions.Center, FontStyles.Bold), new Vector2(0, 0.90f), new Vector2(1, 1f), new Vector2(20, 0), new Vector2(-20, -10));
        SetAnchors(MakeTMP(card, "Sub", "Fish you've encountered on your adventures", 20, new Color(1, 1, 1, 0.55f), TextAlignmentOptions.Center), new Vector2(0, 0.85f), new Vector2(1, 0.90f), Vector2.zero, Vector2.zero);
        SetAnchors(MakeTMP(card, "Counter", $"0 / {PlayerProgress.FishCatalog.Length} discovered", 18, AccentTeal, TextAlignmentOptions.Center, FontStyles.Bold), new Vector2(0, 0.80f), new Vector2(1, 0.85f), Vector2.zero, Vector2.zero);

        var gridArea = MakeRT(card, "GridArea"); gridArea.anchorMin = new Vector2(0.03f, 0.14f); gridArea.anchorMax = new Vector2(0.97f, 0.79f); gridArea.offsetMin = gridArea.offsetMax = Vector2.zero;
        var catalog = PlayerProgress.FishCatalog;
        int cols = 5; float cw = 1f / cols; int rows = Mathf.CeilToInt((float)catalog.Length / cols); float ch = 1f / Mathf.Max(rows, 1);

        for (int i = 0; i < catalog.Length; i++)
        {
            int c = i % cols, r = i / cols;
            var slot = MakeRT(gridArea, "Slot_" + catalog[i].id); slot.anchorMin = new Vector2(c * cw, 1f - (r + 1) * ch); slot.anchorMax = new Vector2((c + 1) * cw, 1f - r * ch); slot.offsetMin = new Vector2(6, 6); slot.offsetMax = new Vector2(-6, -6);
            var slotBg = slot.gameObject.AddComponent<Image>(); slotBg.color = new Color(0.10f, 0.12f, 0.18f, 0.90f);
            var bord = MakeRT(slot, "Border"); bord.anchorMin = Vector2.zero; bord.anchorMax = Vector2.one; bord.offsetMin = new Vector2(-2, -2); bord.offsetMax = new Vector2(2, 2); bord.gameObject.AddComponent<Image>().color = GreyedOut;
            var fill = MakeRT(slot, "Fill"); fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one; fill.offsetMin = fill.offsetMax = Vector2.zero; fill.gameObject.AddComponent<Image>().color = new Color(0.10f, 0.12f, 0.18f, 0.90f);

            // Icon — fills most of the cell, leaving room for name at bottom
            var iconRT = MakeRT(slot, "Icon"); iconRT.anchorMin = new Vector2(0.1f, 0.28f); iconRT.anchorMax = new Vector2(0.9f, 0.92f); iconRT.offsetMin = iconRT.offsetMax = Vector2.zero;
            var iconImg = iconRT.gameObject.AddComponent<Image>(); iconImg.preserveAspect = true; iconImg.raycastTarget = false; iconImg.color = GreyedOut;

            var nameT = MakeTMP(slot, "Name", "???", 14, GreyedOut, TextAlignmentOptions.Center, FontStyles.Bold);
            SetAnchors(nameT, new Vector2(0, 0), new Vector2(1, 0.27f), new Vector2(2, 2), new Vector2(-2, -2));

            var slotBtn = slot.gameObject.AddComponent<Button>(); slotBtn.targetGraphic = slotBg;
            int idx = i; slotBtn.onClick.AddListener(() => ShowFishDetail(idx));
            _slots.Add(new StickerSlot { fishId = catalog[i].id, icon = iconImg, label = nameT, button = slotBtn });
        }

        var det = MakeRT(card, "DetailPanel"); det.anchorMin = new Vector2(0.03f, 0.02f); det.anchorMax = new Vector2(0.97f, 0.13f); det.offsetMin = det.offsetMax = Vector2.zero; det.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.10f, 0.16f, 0.90f);
        _detailName = MakeTMP(det, "DName", "", 20, AccentTeal, TextAlignmentOptions.MidlineLeft, FontStyles.Bold); SetAnchors(_detailName, new Vector2(0, 0), new Vector2(0.35f, 1), new Vector2(16, 4), new Vector2(0, -4));
        _detailDesc = MakeTMP(det, "DDesc", "Tap a fish to see its description.", 16, new Color(1, 1, 1, 0.65f), TextAlignmentOptions.MidlineLeft); SetAnchors(_detailDesc, new Vector2(0.36f, 0), new Vector2(1, 1), new Vector2(8, 4), new Vector2(-12, -4)); _detailDesc.textWrappingMode = TextWrappingModes.Normal;

        var closeRT = MakeRT(overlay.GetComponent<RectTransform>(), "CloseBtn"); closeRT.anchorMin = new Vector2(0.38f, 0.02f); closeRT.anchorMax = new Vector2(0.62f, 0.065f); closeRT.offsetMin = closeRT.offsetMax = Vector2.zero;
        var closeBg = closeRT.gameObject.AddComponent<Image>(); closeBg.color = new Color(0.18f, 0.28f, 0.45f, 0.95f);
        var closeBtn = closeRT.gameObject.AddComponent<Button>(); closeBtn.targetGraphic = closeBg; closeBtn.onClick.AddListener(() => _stickerPanel.SetActive(false));
        var ct = MakeTMP(closeRT, "Lbl", "X  Close", 22, Color.white, TextAlignmentOptions.Center, FontStyles.Bold); Stretch(ct.rectTransform);

        _stickerPanel = overlay; _stickerPanel.SetActive(false);
    }

    void RefreshStickerBook()
    {
        var discovered = PlayerProgress.GetDiscoveredFishSet(); var catalog = PlayerProgress.FishCatalog; int found = 0;
        for (int i = 0; i < _slots.Count && i < catalog.Length; i++)
        {
            bool disc = discovered.Contains(catalog[i].id); if (disc) found++;
            if (_spriteMap.TryGetValue(catalog[i].id, out var spr) && spr != null) { _slots[i].icon.sprite = spr; _slots[i].icon.preserveAspect = true; }
            _slots[i].icon.color = disc ? Color.white : GreyedOut;
            _slots[i].label.text = disc ? catalog[i].name : "???";
            _slots[i].label.color = disc ? Color.white : GreyedOut;
        }
        foreach (var t in _stickerPanel.GetComponentsInChildren<TMP_Text>()) if (t.name == "Counter") t.text = $"{found} / {catalog.Length} discovered";
    }

    void ShowFishDetail(int index)
    {
        var catalog = PlayerProgress.FishCatalog; if (index < 0 || index >= catalog.Length) return;
        bool disc = PlayerProgress.IsFishDiscovered(catalog[index].id);
        _detailName.text = disc ? catalog[index].name : "???"; _detailDesc.text = disc ? catalog[index].description : "You haven't encountered this fish yet.\nKeep playing to discover it!"; _detailName.color = disc ? catalog[index].tintWhenGreyed : GreyedOut;
    }

    static RectTransform MakeRT(Transform p, string n) { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); return go.GetComponent<RectTransform>(); }
    static RectTransform MakeRT(RectTransform p, string n) => MakeRT((Transform)p, n);
    static TMP_Text MakeTMP(Transform p, string n, string text, int size, Color col, TextAlignmentOptions align, FontStyles style = FontStyles.Normal) { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); var t = go.AddComponent<TextMeshProUGUI>(); t.text = text; t.fontSize = size; t.color = col; t.alignment = align; t.fontStyle = style; t.raycastTarget = false; return t; }
    static TMP_Text MakeTMP(RectTransform p, string n, string text, int size, Color col, TextAlignmentOptions align, FontStyles style = FontStyles.Normal) => MakeTMP((Transform)p, n, text, size, col, align, style);
    static void SetAnchors(TMP_Text t, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax) { var rt = t.rectTransform; rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = oMin; rt.offsetMax = oMax; }
    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
}