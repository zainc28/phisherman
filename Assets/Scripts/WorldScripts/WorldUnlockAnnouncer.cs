using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  WorldUnlockAnnouncer
//  Placed on the GameManager object of every world map scene.
//  On Start(), checks whether THIS world's completion has just
//  unlocked the next one and, if that hasn't been announced yet,
//  builds and shows a one-time celebration popup — fully in code.
//  Kept in its own file — see the note in WorldMapCore.cs for why
//  bundling MonoBehaviours together breaks already-built scenes.
// ============================================================
public class WorldUnlockAnnouncer : MonoBehaviour
{
    [Header("Wired by the builder")]
    public RectTransform canvasRT;
    public Sprite circleSprite;
    public int myWorldNumber = 1;

    static readonly Dictionary<int, string> WorldNames = new Dictionary<int, string>
    {
        { 2, "The Digital Streets" },
        { 3, "Social Media Seas" },
        { 4, "The SMS Shallows" },
        { 5, "The Final Depths" },
    };

    static readonly Color Gold = new Color(1.00f, 0.843f, 0f);
    static readonly Color Teal = new Color(0f, 0.898f, 1f);
    static readonly Color CardBg = new Color(0.039f, 0.086f, 0.157f);

    bool _dismissed;

    void Start()
    {
        int nextWorld = myWorldNumber + 1;
        if (!WorldNames.ContainsKey(nextWorld)) return; // World 5 has no next world
        if (!WorldProgress.IsWorldComplete(myWorldNumber)) return;

        string key = "announced_world_" + nextWorld;
        if (PlayerPrefs.GetInt(key, 0) == 1) return;
        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.Save();

        ShowPopup(nextWorld, WorldNames[nextWorld]);
    }

    void ShowPopup(int worldNumber, string worldName)
    {
        if (canvasRT == null) return;

        var overlay = MakeImg(canvasRT, "WorldUnlockOverlay", new Color(0f, 0f, 0f, 0.7f));
        Stretch(overlay.rectTransform);
        overlay.raycastTarget = true; // blocks clicks during the entrance animation only

        // Star burst — 8 thin rectangles radiating from the card's centre, behind the card.
        var burstRoot = new GameObject("StarBurst", typeof(RectTransform));
        burstRoot.transform.SetParent(overlay.rectTransform, false);
        var burstRT = burstRoot.GetComponent<RectTransform>();
        burstRT.anchorMin = burstRT.anchorMax = new Vector2(0.5f, 0.5f);
        burstRT.sizeDelta = Vector2.zero;
        for (int i = 0; i < 8; i++)
        {
            var ray = MakeImg(burstRT, "Ray" + i, new Color(Gold.r, Gold.g, Gold.b, 0.35f));
            ray.raycastTarget = false;
            var rrt = ray.rectTransform;
            rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 0.5f);
            rrt.sizeDelta = new Vector2(6f, 640f);
            rrt.localEulerAngles = new Vector3(0, 0, i * 22.5f);
        }

        // Card
        var cardGo = new GameObject("Card", typeof(RectTransform));
        cardGo.transform.SetParent(overlay.rectTransform, false);
        var crt = cardGo.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(480, 280);

        var bg = MakeImg(crt, "Bg", CardBg); Stretch(bg.rectTransform); bg.raycastTarget = false;

        var accent = MakeImg(crt, "AccentBar", Gold);
        var art = accent.rectTransform; art.anchorMin = new Vector2(0, 1); art.anchorMax = new Vector2(1, 1);
        art.pivot = new Vector2(0.5f, 1); art.sizeDelta = new Vector2(0, 5); accent.raycastTarget = false;

        var title = MakeTxt(crt, "Title", "NEW WORLD UNLOCKED!", 38, Gold, TextAlignmentOptions.Center, FontStyles.Bold);
        var trt = title.rectTransform; trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1);
        trt.pivot = new Vector2(0.5f, 1); trt.sizeDelta = new Vector2(-24, 56); trt.anchoredPosition = new Vector2(0, -24);

        var subtitle = MakeTxt(crt, "Subtitle", $"World {worldNumber} — {worldName}", 22, Color.white, TextAlignmentOptions.Center);
        var strt = subtitle.rectTransform; strt.anchorMin = new Vector2(0, 1); strt.anchorMax = new Vector2(1, 1);
        strt.pivot = new Vector2(0.5f, 1); strt.sizeDelta = new Vector2(-24, 34); strt.anchoredPosition = new Vector2(0, -84);

        BuildFishIcon(crt, new Vector2(0, -6));

        var btnGo = MakeBtnGo(crt, "LetsGoBtn", "Let's Go!", Teal, Color.white);
        var brt2 = btnGo.GetComponent<RectTransform>();
        brt2.anchorMin = new Vector2(0.5f, 0); brt2.anchorMax = new Vector2(0.5f, 0);
        brt2.pivot = new Vector2(0.5f, 0); brt2.sizeDelta = new Vector2(220, 54); brt2.anchoredPosition = new Vector2(0, 20);
        btnGo.GetComponent<Button>().onClick.AddListener(() => Dismiss(overlay.gameObject));

        StartCoroutine(EntranceThenAutoDismiss(crt, overlay));
    }

    // Fish icon — same construction as the top-left fish menu button
    // (tail drawn first so the body overlaps and covers half of it).
    void BuildFishIcon(RectTransform parent, Vector2 anchoredPos)
    {
        var root = new GameObject("FishIcon", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        var rrt = root.GetComponent<RectTransform>();
        rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 0.5f);
        rrt.sizeDelta = new Vector2(52, 36); rrt.anchoredPosition = anchoredPos;

        var tail = MakeImg(rrt, "FishTail", Teal); tail.raycastTarget = false;
        var tailRT = tail.rectTransform; tailRT.anchorMin = tailRT.anchorMax = new Vector2(0.5f, 0.5f);
        tailRT.sizeDelta = new Vector2(14, 14); tailRT.anchoredPosition = new Vector2(-15, 0); tailRT.localEulerAngles = new Vector3(0, 0, 45f);

        var body = MakeImg(rrt, "FishBody", Teal); body.sprite = circleSprite; body.raycastTarget = false;
        var bodyRT = body.rectTransform; bodyRT.anchorMin = bodyRT.anchorMax = new Vector2(0.5f, 0.5f);
        bodyRT.sizeDelta = new Vector2(36, 22); bodyRT.anchoredPosition = new Vector2(6, 0);
    }

    IEnumerator EntranceThenAutoDismiss(RectTransform card, Image overlay)
    {
        card.localScale = Vector3.one * 0.5f;
        float t = 0f;
        while (t < 0.4f)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / 0.4f);
            card.localScale = Vector3.one * Mathf.Lerp(0.5f, 1f, p);
            yield return null;
        }
        card.localScale = Vector3.one;

        if (overlay != null) overlay.raycastTarget = false; // non-blocking once the entrance finishes

        yield return new WaitForSecondsRealtime(5f);
        Dismiss(overlay != null ? overlay.gameObject : null);
    }

    void Dismiss(GameObject overlayGo)
    {
        if (_dismissed) return;
        _dismissed = true;
        if (overlayGo != null) Destroy(overlayGo);
    }

    // ── Small local UI helpers (this is a runtime script, not an Editor
    //    builder, so it can't reuse SharedWorldBuilderUtils) ──
    static Image MakeImg(Transform p, string n, Color c) { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); var img = go.AddComponent<Image>(); img.color = c; return img; }
    static TMP_Text MakeTxt(Transform p, string n, string text, int size, Color col, TextAlignmentOptions align, FontStyles style = FontStyles.Normal) { var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false); var t = go.AddComponent<TextMeshProUGUI>(); t.text = text; t.fontSize = size; t.color = col; t.alignment = align; t.fontStyle = style; t.raycastTarget = false; return t; }
    static GameObject MakeBtnGo(Transform p, string n, string label, Color bg, Color tc)
    {
        var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false);
        var img = go.AddComponent<Image>(); img.color = bg;
        var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        var t = MakeTxt(go.transform, "Label", label, 20, tc, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(t.rectTransform);
        return go;
    }
    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
}
