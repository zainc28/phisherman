using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// ============================================================
//  CardDotAnimator  (merged from CardDotAnimator.cs)
//  Attached to the dots row at the bottom of each email card.
//  Each dot independently pulses through aquarium colours.
// ============================================================
public class CardDotAnimator : MonoBehaviour
{
    [HideInInspector] public Image[] dots;

    static readonly Color[] Palette =
    {
        new Color(1.00f,0.42f,0.20f), new Color(1.00f,0.85f,0.20f),
        new Color(0.20f,0.75f,1.00f), new Color(0.35f,0.95f,0.55f),
        new Color(0.90f,0.30f,0.60f), new Color(0.85f,0.85f,1.00f),
        new Color(0.45f,0.90f,0.90f), new Color(1.00f,0.60f,0.80f),
        new Color(0.60f,0.40f,1.00f), new Color(1.00f,0.95f,0.70f),
    };

    void Start()
    {
        if (dots == null) return;
        for (int i = 0; i < dots.Length; i++)
            StartCoroutine(AnimateDot(dots[i], i * 0.12f));
    }

    IEnumerator AnimateDot(Image dot, float initialDelay)
    {
        yield return new WaitForSeconds(initialDelay);
        Color current = Palette[UnityEngine.Random.Range(0, Palette.Length)];
        dot.color = current;
        while (true)
        {
            float holdTime = UnityEngine.Random.Range(0.25f, 0.90f);
            yield return new WaitForSeconds(holdTime);
            Color next;
            do { next = Palette[UnityEngine.Random.Range(0, Palette.Length)]; } while (next == current);
            float fadeDur = UnityEngine.Random.Range(0.10f, 0.28f);
            float t = 0f;
            while (t < fadeDur) { t += Time.deltaTime; if (dot == null) yield break; dot.color = Color.Lerp(current, next, t / fadeDur); yield return null; }
            current = next;
        }
    }
}

// ============================================================
//  SwipeButtonRelay
//  Sits on each net root Button — fires SimulateSwipe on the
//  SwipeCard with the correct direction when clicked.
// ============================================================
public class SwipeButtonRelay : MonoBehaviour
{
    public SwipeCard target;
    public int direction; // -1 = SCAM (left), +1 = SAFE (right)
    public void Fire() { if (target != null) target.SimulateSwipe(direction); }
}

// ============================================================
//  NetHoverEffect
//  Lifts and sways the net while the pointer hovers over it.
//  Animates position + rotation only (never scale) to avoid
//  fighting EmailSwiperManager.NetBounce().
// ============================================================
public class NetHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Bob")]
    public float lift = 14f;
    public float swayAngle = 2.5f;
    public float swaySpeed = 2.4f;
    public float lerpSpeed = 12f;

    [Header("Glow")]
    public float glowBoost = 0.16f;
    public Graphic[] glowGraphics;

    bool _hovered;
    Vector2 _basePos;
    float _baseRot;
    Color[] _baseColors;
    bool _basesCached;

    void CacheBases()
    {
        if (_basesCached) return;
        _basesCached = true;
        var rt = GetComponent<RectTransform>();
        _basePos = rt != null ? rt.anchoredPosition : Vector2.zero;
        _baseRot = rt != null ? rt.localEulerAngles.z : 0f;
        if (glowGraphics != null)
        {
            _baseColors = new Color[glowGraphics.Length];
            for (int i = 0; i < glowGraphics.Length; i++)
                _baseColors[i] = glowGraphics[i] != null ? glowGraphics[i].color : Color.white;
        }
    }

    void Update()
    {
        CacheBases();
        var rt = GetComponent<RectTransform>();
        if (rt == null) return;

        float targetY = _hovered ? _basePos.y + lift : _basePos.y;
        float targetRot = _hovered ? Mathf.Sin(Time.time * swaySpeed) * swayAngle : _baseRot;

        Vector2 curPos = rt.anchoredPosition;
        float curRot = rt.localEulerAngles.z;
        rt.anchoredPosition = Vector2.Lerp(curPos, new Vector2(_basePos.x, targetY), lerpSpeed * Time.deltaTime);
        float newRot = Mathf.LerpAngle(curRot > 180f ? curRot - 360f : curRot, targetRot, lerpSpeed * Time.deltaTime);
        rt.localEulerAngles = new Vector3(0, 0, newRot);

        if (glowGraphics != null && _baseColors != null)
        {
            for (int i = 0; i < glowGraphics.Length; i++)
            {
                if (glowGraphics[i] == null) continue;
                Color bc = _baseColors[i];
                Color tc = _hovered ? new Color(Mathf.Min(bc.r + glowBoost, 1f), Mathf.Min(bc.g + glowBoost, 1f), Mathf.Min(bc.b + glowBoost, 1f), bc.a) : bc;
                glowGraphics[i].color = Color.Lerp(glowGraphics[i].color, tc, lerpSpeed * Time.deltaTime);
            }
        }
    }

    public void OnPointerEnter(PointerEventData _) => _hovered = true;
    public void OnPointerExit(PointerEventData _) => _hovered = false;
}

// ============================================================
//  SwipeCard
//  Handles drag + swipe physics, rod line update, and fling.
// ============================================================
public class SwipeCard : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    // ── Refs wired by builder ─────────────────────────────────
    [Header("Card")]
    public RectTransform cardRoot;
    public Image cardBorder;
    public CanvasGroup cardCanvasGroup;
    public Canvas rootCanvas;

    [Header("Swipe indicators")]
    public CanvasGroup scamIndicator;
    public CanvasGroup safeIndicator;

    [Header("Rod line segments")]
    public RectTransform rodTipRT;
    public RectTransform rodLineRT;
    public RectTransform rodLine2RT;
    public RectTransform rodLine3RT;
    public RectTransform rodLine4RT;
    public RectTransform rodBobRT;

    // ── Colours ───────────────────────────────────────────────
    static readonly Color ScamBorderCol = new Color(0.85f, 0.20f, 0.20f, 0.88f);
    static readonly Color SafeBorderCol = new Color(0.18f, 0.70f, 0.65f, 0.88f);
    static readonly Color NeutralCol = new Color(0f, 0f, 0f, 0f);

    // ── Tuning ────────────────────────────────────────────────
    const float SwipeThreshold = 80f;
    const float FlingSpeed = 2200f;
    const float RotationScale = 0.08f;
    const float IndicatorFade = 5f;
    const float ReturnSpeed = 12f;

    // ── State ─────────────────────────────────────────────────
    bool _dragging;
    bool locked;
    Vector2 _pointerStart;
    Vector2 _cardStart;

    public Action<int> onSwipeCommit;  // -1 or +1

    // ── Public API ────────────────────────────────────────────
    public bool IsLocked => locked;
    public void Lock() => locked = true;
    public void Unlock() => locked = false;

    public void ResetPosition()
    {
        locked = false;
        if (cardRoot == null) return;
        cardRoot.anchoredPosition = Vector2.zero;
        cardRoot.localEulerAngles = Vector3.zero;
        if (cardBorder != null) cardBorder.color = NeutralCol;
        if (scamIndicator != null) scamIndicator.alpha = 0f;
        if (safeIndicator != null) safeIndicator.alpha = 0f;
        UpdateRodLine(Vector2.zero);
        ShowBob(false);
    }

    public void ShowBob(bool show)
    {
        if (rodBobRT != null) rodBobRT.gameObject.SetActive(show);
        if (rodLineRT != null) rodLineRT.gameObject.SetActive(show);
        if (rodLine2RT != null) rodLine2RT.gameObject.SetActive(show);
        if (rodLine3RT != null) rodLine3RT.gameObject.SetActive(show);
        if (rodLine4RT != null) rodLine4RT.gameObject.SetActive(show);
    }

    public void SimulateSwipe(int dir)
    {
        if (locked) return;
        locked = true;
        float targetX = dir < 0 ? -1800f : 1800f;
        StartCoroutine(FlingRoutine(new Vector2(targetX, 200f * dir)));
    }

    // ── Pointer events ────────────────────────────────────────
    public void OnPointerDown(PointerEventData e)
    {
        if (locked) return;
        _dragging = true;
        _pointerStart = e.position;
        _cardStart = cardRoot != null ? cardRoot.anchoredPosition : Vector2.zero;
        ShowBob(true);
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (!_dragging) return;
        _dragging = false;

        if (locked || cardRoot == null) return;

        Vector2 delta = (Vector2)cardRoot.anchoredPosition - _cardStart;
        if (Mathf.Abs(delta.x) >= SwipeThreshold)
        {
            locked = true;
            StartCoroutine(FlingRoutine(delta));
        }
        else
        {
            StartCoroutine(ReturnRoutine());
        }
    }

    public void OnDrag(PointerEventData e)
    {
        if (!_dragging || locked || cardRoot == null) return;

        Vector2 localDelta;
        float scaleFactor = rootCanvas != null ? rootCanvas.scaleFactor : 1f;
        localDelta = (e.position - _pointerStart) / scaleFactor;

        cardRoot.anchoredPosition = _cardStart + localDelta;

        float rot = -localDelta.x * RotationScale;
        cardRoot.localEulerAngles = new Vector3(0, 0, rot);

        float t = Mathf.Clamp01(Mathf.Abs(localDelta.x) / 300f);
        if (cardBorder != null)
            cardBorder.color = Color.Lerp(NeutralCol, localDelta.x < 0 ? ScamBorderCol : SafeBorderCol, t);

        if (scamIndicator != null) scamIndicator.alpha = localDelta.x < 0 ? Mathf.Lerp(0, 1, t * IndicatorFade) : 0;
        if (safeIndicator != null) safeIndicator.alpha = localDelta.x > 0 ? Mathf.Lerp(0, 1, t * IndicatorFade) : 0;

        UpdateRodLine(localDelta);
    }

    // ── Coroutines ────────────────────────────────────────────
    IEnumerator FlingRoutine(Vector2 direction)
    {
        if (cardRoot == null) yield break;
        Vector2 start = cardRoot.anchoredPosition;
        Vector2 target = start + direction.normalized * 1600f;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * (FlingSpeed / 1600f);
            if (cardRoot == null) yield break;
            cardRoot.anchoredPosition = Vector2.Lerp(start, target, Mathf.SmoothStep(0, 1, t));
            float rot = -direction.x * RotationScale * Mathf.Lerp(1, 3, t);
            cardRoot.localEulerAngles = new Vector3(0, 0, rot);
            UpdateRodLine(cardRoot.anchoredPosition - _cardStart);
            yield return null;
        }

        int dir = direction.x < 0 ? -1 : 1;
        onSwipeCommit?.Invoke(dir);
        ShowBob(false);
    }

    IEnumerator ReturnRoutine()
    {
        if (cardRoot == null) yield break;
        while (cardRoot.anchoredPosition.sqrMagnitude > 1f)
        {
            cardRoot.anchoredPosition = Vector2.Lerp(cardRoot.anchoredPosition, Vector2.zero, ReturnSpeed * Time.deltaTime);
            cardRoot.localEulerAngles = Vector3.Lerp(cardRoot.localEulerAngles, Vector3.zero, ReturnSpeed * Time.deltaTime);
            if (cardBorder != null) cardBorder.color = Color.Lerp(cardBorder.color, NeutralCol, ReturnSpeed * Time.deltaTime);
            if (scamIndicator != null) scamIndicator.alpha = Mathf.Lerp(scamIndicator.alpha, 0f, ReturnSpeed * Time.deltaTime);
            if (safeIndicator != null) safeIndicator.alpha = Mathf.Lerp(safeIndicator.alpha, 0f, ReturnSpeed * Time.deltaTime);
            UpdateRodLine(cardRoot.anchoredPosition);
            yield return null;
        }
        ResetPosition();
    }

    // ── Rod line ──────────────────────────────────────────────
    void UpdateRodLine(Vector2 cardOffset)
    {
        if (rodTipRT == null || rodLineRT == null) return;

        // Tip world position
        Vector2 tipWorld = GetWorldPos(rodTipRT);
        // Bob tracks top-centre of the card
        Vector2 bobCanvas = _cardStart + cardOffset + new Vector2(0, 330f);
        Vector2 bobWorld = CanvasToWorld(bobCanvas);

        if (rodBobRT != null)
        {
            rodBobRT.position = new Vector3(bobWorld.x, bobWorld.y, rodBobRT.position.z);
        }

        // Distribute 4 segments as a catenary-ish curve from tip to bob
        PlaceSegment(rodLineRT, tipWorld, Vector2.Lerp(tipWorld, bobWorld, 0.33f));
        PlaceSegment(rodLine2RT, Vector2.Lerp(tipWorld, bobWorld, 0.33f), Vector2.Lerp(tipWorld, bobWorld, 0.66f));
        PlaceSegment(rodLine3RT, Vector2.Lerp(tipWorld, bobWorld, 0.66f), bobWorld);
        if (rodLine4RT != null)
            PlaceSegment(rodLine4RT, tipWorld, bobWorld);  // overlay thin full line
    }

    void PlaceSegment(RectTransform seg, Vector2 worldA, Vector2 worldB)
    {
        if (seg == null) return;
        Vector2 mid = (worldA + worldB) * 0.5f;
        seg.position = new Vector3(mid.x, mid.y, seg.position.z);
        float len = Vector2.Distance(worldA, worldB);
        Vector2 cur = seg.sizeDelta; seg.sizeDelta = new Vector2(len, cur.y);
        float angle = Mathf.Atan2(worldB.y - worldA.y, worldB.x - worldA.x) * Mathf.Rad2Deg;
        seg.localEulerAngles = new Vector3(0, 0, angle);
        seg.gameObject.SetActive(true);
    }

    Vector2 GetWorldPos(RectTransform rt)
    {
        if (rt == null) return Vector2.zero;
        return rt.position;
    }

    Vector2 CanvasToWorld(Vector2 canvasPos)
    {
        if (rootCanvas == null) return canvasPos;
        // Screen-space overlay: canvas pos maps 1:1 to screen at scale 1
        float sf = rootCanvas.scaleFactor > 0 ? rootCanvas.scaleFactor : 1f;
        // canvasPos is in reference resolution space, convert to screen then to world
        // For screen-space overlay canvases the canvas rect IS screen coords * scaleFactor
        Vector2 screenPos = canvasPos * sf + new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        if (Camera.main == null) return screenPos;
        return Camera.main.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 10f));
    }

    // Canvas-space position relative to canvas centre
    Vector2 ToCanvasSpace(RectTransform rt)
    {
        if (rt == null || rootCanvas == null) return Vector2.zero;
        Vector2 screenPos = rt.position;
        float sf = rootCanvas.scaleFactor > 0 ? rootCanvas.scaleFactor : 1f;
        return (screenPos - new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)) / sf;
    }
}