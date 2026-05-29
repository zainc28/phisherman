using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Reigns-style card swiping. Lives on a transparent OVERLAY that sits
/// on top of the entire card so nothing (scroll rects, buttons, text)
/// can steal drag events. Moves cardRoot (the visual card) underneath.
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
    public RectTransform cardRoot;        // visual card that moves with drag
    public CanvasGroup scamIndicator;   // "SCAM" label fades in on left drag
    public CanvasGroup safeIndicator;   // "SAFE" label fades in on right drag
    public Image cardBorder;      // glow tints red/green during drag

    private static readonly Color ScamTint = new Color(1f, 0.42f, 0.42f);
    private static readonly Color SafeTint = new Color(0.31f, 0.80f, 0.77f);
    private static readonly Color Neutral = new Color(1f, 1f, 1f, 0f);

    private RectTransform rt;
    private Vector2 origin;
    private bool dragging;
    private bool locked;
    private float dragX;

    /// <summary>Fires with -1 (left/SCAM) or +1 (right/SAFE).</summary>
    public System.Action<int> onSwipeCommit;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        origin = rt.anchoredPosition;
    }

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
        dragX = 0f;
        locked = false;
        SetIndicators(0f);
    }

    public void Lock() { locked = true; }
    public void Unlock() { locked = false; }

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
        dragging = true;
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
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (locked) { dragging = false; return; }
        dragging = false;

        if (Mathf.Abs(dragX / swipeThreshold) >= 1f)
        {
            locked = true;
            onSwipeCommit?.Invoke(dragX < 0 ? -1 : 1);
        }
        else
        {
            StartCoroutine(SpringBack());
        }
    }

    // ================================================================
    // Animations
    // ================================================================

    private System.Collections.IEnumerator SpringBack()
    {
        float startX = dragX;
        float startRot = 0f;
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
                cardRoot.localScale = Vector3.Lerp(
                    Vector3.one * scaleOnPickup, Vector3.one, ease);
            }
            SetIndicators(Mathf.Lerp(startX / swipeThreshold, 0f, ease));
            yield return null;
        }

        dragX = 0f;
        rt.anchoredPosition = origin;
        if (cardRoot != null)
        {
            cardRoot.anchoredPosition = origin;
            cardRoot.localRotation = Quaternion.identity;
            cardRoot.localScale = Vector3.one;
        }
        SetIndicators(0f);
    }

    /// <summary>
    /// Card flies off + shrinks (crumple). Manager spawns the fish after.
    /// </summary>
    public System.Collections.IEnumerator FlyOffAndCrumple(int dir, float dur = 0.32f)
    {
        if (cardRoot == null) yield break;

        Vector2 start = cardRoot.anchoredPosition;
        Vector2 end = start + new Vector2(dir * 500f, -80f);
        float sRot = cardRoot.localRotation.eulerAngles.z;
        if (sRot > 180f) sRot -= 360f;
        float eRot = dir * -45f;

        CanvasGroup cg = cardRoot.GetComponent<CanvasGroup>();

        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            float ease = p * p;

            cardRoot.anchoredPosition = Vector2.Lerp(start, end, ease);
            cardRoot.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(sRot, eRot, ease));
            cardRoot.localScale = Vector3.one * Mathf.Lerp(1f, 0.12f, ease);
            if (cg != null) cg.alpha = 1f - Mathf.Clamp01((p - 0.4f) / 0.6f);

            yield return null;
        }
        if (cg != null) cg.alpha = 0f;
    }

    public Vector2 GetCardPosition()
    {
        return cardRoot != null ? cardRoot.anchoredPosition : rt.anchoredPosition;
    }

    // ================================================================
    // Indicators
    // ================================================================

    void SetIndicators(float norm)
    {
        float scamA = Mathf.Clamp01(-norm);
        float safeA = Mathf.Clamp01(norm);

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