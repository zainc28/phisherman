using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Reigns-style card swiping with fishing rod visual.
///
/// Rod uses a 4-segment cubic Bezier approximation so the line curves
/// naturally like a real fishing string under tension — no sharp vertex.
/// </summary>
public class SwipeCard : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerDownHandler, IPointerUpHandler
{
    [Header("Swipe Feel")]
    public float swipeThreshold = 220f;
    public float maxRotation = 15f;
    public float snapSpeed = 12f;
    public float scaleOnPickup = 1.03f;

    [Header("References")]
    public RectTransform cardRoot;
    public CanvasGroup scamIndicator;
    public CanvasGroup safeIndicator;
    public Image cardBorder;

    [Header("Fishing Rod — 4-segment Bezier")]
    public RectTransform rodTipRT;      // fixed anchor at top-centre (just below HUD)
    public RectTransform rodLineRT;     // segment 0-1
    public RectTransform rodLine2RT;    // segment 1-2
    public RectTransform rodLine3RT;    // segment 2-3
    public RectTransform rodLine4RT;    // segment 3-bob
    public RectTransform rodBobRT;      // orb at hook end
    public Canvas rootCanvas;

    // ── Colours ──
    private static readonly Color ScamTint = new Color(1f, 0.42f, 0.42f);
    private static readonly Color SafeTint = new Color(0.31f, 0.80f, 0.77f);
    private static readonly Color Neutral = new Color(1f, 1f, 1f, 0f);

    private RectTransform rt;
    private Vector2 origin;
    private bool dragging, locked;
    private float dragX, bobTime;
    private Vector2 _bobOffset;

    public System.Action<int> onSwipeCommit;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        origin = rt.anchoredPosition;
        HideRod();
    }

    void Update()
    {
        if (rodBobRT != null && rodBobRT.gameObject.activeSelf)
        {
            bobTime += Time.deltaTime;
            _bobOffset = new Vector2(
                Mathf.Sin(bobTime * 2.2f) * 3f,
                Mathf.Sin(bobTime * 3.5f) * 4f - 2f);
        }
    }

    // ================================================================
    // Public API
    // ================================================================

    public void ResetPosition()
    {
        rt.anchoredPosition = origin;
        if (cardRoot != null)
        {
            cardRoot.anchoredPosition = origin;
            cardRoot.localRotation = Quaternion.identity;
            cardRoot.localScale = Vector3.one;
            var cg = cardRoot.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 1f;
        }
        dragX = 0f; locked = false;
        SetIndicators(0f);
        HideRod();
    }

    public void Lock() { locked = true; HideRod(); }
    public void Unlock() { locked = false; }

    public void SimulateSwipe(int dir)
    {
        if (locked) return;
        locked = true; HideRod();
        onSwipeCommit?.Invoke(dir);
    }

    // ================================================================
    // Pointer / Drag
    // ================================================================

    public void OnPointerDown(PointerEventData e)
    {
        if (locked) return;
        if (cardRoot != null) cardRoot.localScale = Vector3.one * scaleOnPickup;
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (!dragging && cardRoot != null) cardRoot.localScale = Vector3.one;
    }

    public void OnBeginDrag(PointerEventData e)
    {
        if (locked) return;
        dragging = true; ShowRod();
    }

    public void OnDrag(PointerEventData e)
    {
        if (locked || !dragging) return;
        dragX += e.delta.x;

        rt.anchoredPosition = origin + new Vector2(dragX, 0f);
        if (cardRoot != null)
        {
            cardRoot.anchoredPosition = rt.anchoredPosition;
            float norm = Mathf.Clamp(dragX / swipeThreshold, -1f, 1f);
            cardRoot.localRotation = Quaternion.Euler(0, 0, -norm * maxRotation);
        }
        SetIndicators(Mathf.Clamp(dragX / swipeThreshold, -1f, 1f));
        UpdateRod();
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (locked) { dragging = false; return; }
        dragging = false; HideRod();

        if (Mathf.Abs(dragX / swipeThreshold) >= 1f)
        {
            locked = true;
            onSwipeCommit?.Invoke(dragX < 0 ? -1 : 1);
        }
        else StartCoroutine(SpringBack());
    }

    // ================================================================
    // Rod — cubic Bezier, 4 segments
    // ================================================================

    void ShowRod()
    {
        SetRodActive(true);
        bobTime = 0f;
    }

    void HideRod()
    {
        SetRodActive(false);
    }

    void SetRodActive(bool on)
    {
        if (rodLineRT != null) rodLineRT.gameObject.SetActive(on);
        if (rodLine2RT != null) rodLine2RT.gameObject.SetActive(on);
        if (rodLine3RT != null) rodLine3RT.gameObject.SetActive(on);
        if (rodLine4RT != null) rodLine4RT.gameObject.SetActive(on);
        if (rodBobRT != null) rodBobRT.gameObject.SetActive(on);
    }

    Vector2 ToCanvasSpace(RectTransform target)
    {
        if (rootCanvas == null || target == null) return Vector2.zero;
        Camera cam = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null : Camera.main;
        Vector2 screen;
        screen = RectTransformUtility.WorldToScreenPoint(cam, target.position);
        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rootCanvas.GetComponent<RectTransform>(), screen, cam, out local);
        return local;
    }

    /// <summary>
    /// Cubic Bezier: P(t) = (1-t)³P0 + 3(1-t)²tP1 + 3(1-t)t²P2 + t³P3
    ///
    /// Control points are chosen so the rod:
    ///   - Leaves the tip going slightly downward (like a bent rod tip)
    ///   - Droops in the middle under "gravity"
    ///   - Arrives at the bob from above
    ///
    /// We approximate the smooth curve with 4 line segments (t = 0, 0.25, 0.5, 0.75, 1.0)
    /// which looks smooth at normal view distances.
    /// </summary>
    void UpdateRod()
    {
        if (rodLineRT == null || rodTipRT == null || cardRoot == null) return;

        Vector2 p0 = ToCanvasSpace(rodTipRT);                    // rod tip (top)
        Vector2 p3 = ToCanvasSpace(cardRoot) + _bobOffset;       // bob end (card)

        // Horizontal drag offset biases control points so the line leans with the card
        float horizLean = (p3.x - p0.x) * 0.35f;

        // Control point 1: just below the tip, leaning in drag direction
        // (gives the "bent rod" look at the top)
        Vector2 p1 = p0 + new Vector2(horizLean * 0.5f, -80f);

        // Control point 2: drooped low in the middle
        // droop deepens with total line length so short lines are tighter
        float lineLen = (p3 - p0).magnitude;
        float droop = 100f + lineLen * 0.22f;
        Vector2 p2 = (p0 + p3) * 0.5f + new Vector2(horizLean * 0.3f, -droop);

        // Sample 5 points along the cubic Bezier (t = 0, 0.25, 0.5, 0.75, 1)
        Vector2 b0 = CubicBezier(p0, p1, p2, p3, 0.00f);
        Vector2 b1 = CubicBezier(p0, p1, p2, p3, 0.25f);
        Vector2 b2 = CubicBezier(p0, p1, p2, p3, 0.50f);
        Vector2 b3 = CubicBezier(p0, p1, p2, p3, 0.75f);
        Vector2 b4 = CubicBezier(p0, p1, p2, p3, 1.00f);

        DrawSeg(rodLineRT, b0, b1);
        DrawSeg(rodLine2RT, b1, b2);
        DrawSeg(rodLine3RT, b2, b3);
        DrawSeg(rodLine4RT, b3, b4);

        // Bob orb sits at the end of the line
        if (rodBobRT != null) rodBobRT.anchoredPosition = b4;
    }

    static Vector2 CubicBezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float u = 1f - t;
        float u2 = u * u;
        float u3 = u2 * u;
        float t2 = t * t;
        float t3 = t2 * t;
        return u3 * p0 + 3f * u2 * t * p1 + 3f * u * t2 * p2 + t3 * p3;
    }

    static void DrawSeg(RectTransform seg, Vector2 from, Vector2 to)
    {
        if (seg == null) return;
        Vector2 delta = to - from;
        seg.anchoredPosition = (from + to) * 0.5f;
        seg.sizeDelta = new Vector2(delta.magnitude, seg.sizeDelta.y);
        seg.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
    }

    // ================================================================
    // Spring back
    // ================================================================

    IEnumerator SpringBack()
    {
        float startX = dragX, startRot = 0f;
        if (cardRoot != null)
        {
            startRot = cardRoot.localRotation.eulerAngles.z;
            if (startRot > 180f) startRot -= 360f;
        }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * snapSpeed;
            float ease = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
            dragX = Mathf.Lerp(startX, 0f, ease);
            rt.anchoredPosition = origin + new Vector2(dragX, 0f);
            if (cardRoot != null)
            {
                cardRoot.anchoredPosition = rt.anchoredPosition;
                cardRoot.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(startRot, 0f, ease));
                cardRoot.localScale = Vector3.Lerp(Vector3.one * scaleOnPickup, Vector3.one, ease);
            }
            SetIndicators(Mathf.Lerp(startX / swipeThreshold, 0f, ease));
            UpdateRod();
            yield return null;
        }

        dragX = 0f; rt.anchoredPosition = origin;
        if (cardRoot != null)
        {
            cardRoot.anchoredPosition = origin;
            cardRoot.localRotation = Quaternion.identity;
            cardRoot.localScale = Vector3.one;
        }
        SetIndicators(0f); HideRod();
    }

    public IEnumerator FlyOffAndCrumple(int dir, float dur = 0.32f)
    {
        if (cardRoot == null) yield break;
        HideRod();
        Vector2 start = cardRoot.anchoredPosition;
        Vector2 end = start + new Vector2(dir * 500f, -80f);
        float sRot = cardRoot.localRotation.eulerAngles.z;
        if (sRot > 180f) sRot -= 360f;
        CanvasGroup cg = cardRoot.GetComponent<CanvasGroup>();
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur), ease = p * p;
            cardRoot.anchoredPosition = Vector2.Lerp(start, end, ease);
            cardRoot.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(sRot, dir * -45f, ease));
            cardRoot.localScale = Vector3.one * Mathf.Lerp(1f, 0.12f, ease);
            if (cg != null) cg.alpha = 1f - Mathf.Clamp01((p - 0.4f) / 0.6f);
            yield return null;
        }
        if (cg != null) cg.alpha = 0f;
    }

    public Vector2 GetCardPosition() =>
        cardRoot != null ? cardRoot.anchoredPosition : rt.anchoredPosition;

    // ================================================================
    // Indicators
    // ================================================================

    void SetIndicators(float norm)
    {
        float scamA = Mathf.Clamp01(-norm), safeA = Mathf.Clamp01(norm);
        if (scamIndicator != null) scamIndicator.alpha = scamA * 0.95f;
        if (safeIndicator != null) safeIndicator.alpha = safeA * 0.95f;
        if (cardBorder != null)
        {
            if (Mathf.Abs(norm) < 0.05f) cardBorder.color = Neutral;
            else if (norm < 0) cardBorder.color = Color.Lerp(Neutral, ScamTint, scamA);
            else cardBorder.color = Color.Lerp(Neutral, SafeTint, safeA);
        }
    }
}

/// <summary>Relay so swipe-zone buttons can trigger SimulateSwipe.</summary>
public class SwipeButtonRelay : MonoBehaviour
{
    public SwipeCard target;
    public int direction;
    public void Fire() => target?.SimulateSwipe(direction);
}