using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// LureSimulation.cs — Cage & Diver edition
///
/// FIX: Shark Update now sets both X and Y from NormToLocal every frame
///      (previously only set X, leaving Y stuck at the pre-layout zero).
/// FIX: Cage stays at rest position during breach — no fly-off or sink.
/// FIX: NormToLocal defers positioning until panelRT.rect is valid
///      (avoids first-frame zero-rect placing everything at centre).
/// </summary>
public class LureSimulation : MonoBehaviour
{
    [Header("References — wired by builder")]
    public RectTransform rodLineRT;
    public RectTransform hookRT;
    public RectTransform[] debrisLayers;
    public Image[] debrisImages;

    public RectTransform sharkRT;
    public Image sharkImage;
    public RectTransform panelRT;

    public RectTransform cageRT;
    public Image cageImage;
    public Sprite cageSprite;
    public Sprite cagedamaged1;
    public Sprite cagedamaged2;
    public Sprite cagedamaged3;

    // Legacy fields kept for compile compatibility
    public Image[] cageBars;
    public Image cageBase;
    public Image cageShadow;

    public RectTransform diverRT;
    public Image diverImage;
    public Sprite diverNormal;
    public Sprite diverPointing;
    public Sprite diverScared;

    [Header("Audio callbacks")]
    public System.Action onImpact;
    public System.Action onRodWinding;
    public System.Action onWrongClick;

    [Header("Settings")]
    public float totalTime = 60f;

    // ── Private state ─────────────────────────────────────────────────
    private int _wrongClicks;
    private bool _active;
    private bool _resolved;
    private float _idleTimer;
    private float _bangCooldown;
    private float _diverBobTimer;
    private bool _layoutReady;      // FIX: true once panelRT.rect is valid

    private const float CageNX = 0.30f;
    private const float CageNYRest = 0.10f;
    private const float CageNYTop = 0.90f;

    private const float SharkNX = 0.30f;
    private const float SharkNYRest = 0.38f;

    private const float DiverNX = 0.78f;
    private const float DiverNYMid = 0.50f;
    private const float DiverBobAmp = 0.10f;
    private const float DiverBobFreq = 0.55f;

    // =================================================================
    // Helpers
    // =================================================================

    Vector2 NormToLocal(float nx, float ny)
    {
        Rect r = panelRT != null ? panelRT.rect : new Rect(0, 0, 1920, 291);
        return new Vector2((nx - 0.5f) * r.width, (ny - 0.5f) * r.height);
    }

    /// <summary>Returns true once panelRT.rect has non-zero dimensions.</summary>
    bool CheckLayoutReady()
    {
        if (_layoutReady) return true;
        if (panelRT == null) return false;
        Rect r = panelRT.rect;
        if (r.width > 1f && r.height > 1f)
        {
            _layoutReady = true;

            // Now that layout is valid, place everything at its correct
            // starting position (StartSim may have run before layout).
            if (_active) ApplyInitialPositions();
            return true;
        }
        return false;
    }

    void ApplyInitialPositions()
    {
        if (sharkRT != null)
            sharkRT.anchoredPosition = NormToLocal(SharkNX, SharkNYRest);
        if (diverRT != null)
            diverRT.anchoredPosition = NormToLocal(DiverNX, DiverNYMid);
        // Cage is handled by the DropCage coroutine.
    }

    // =================================================================
    // Start
    // =================================================================

    void Start()
    {
        // No idle bob coroutine needed — Update handles all positioning.
    }

    // =================================================================
    // Public API
    // =================================================================

    public void StartSim(int maxLives, int totalFlags)
    {
        _wrongClicks = 0;
        _active = true;
        _resolved = false;
        _idleTimer = 0f;
        _bangCooldown = 0f;
        _diverBobTimer = 0f;
        _layoutReady = false;

        // Try to place now; if rect isn't valid yet, CheckLayoutReady
        // will do it on the first Update after layout.
        CheckLayoutReady();

        if (sharkRT != null)
            sharkRT.anchoredPosition = NormToLocal(SharkNX, SharkNYRest);
        if (diverRT != null)
            diverRT.anchoredPosition = NormToLocal(DiverNX, DiverNYMid);

        SetDiverSprite(diverPointing);
        SetCageSprite(cageSprite);

        if (cageRT != null)
        {
            cageRT.anchoredPosition = NormToLocal(CageNX, CageNYTop + 0.25f);
            cageRT.gameObject.SetActive(true);
            StartCoroutine(DropCage());
        }
    }

    public void StopSim() { _active = false; }

    public void OnWrongClick()
    {
        if (_resolved) return;
        _wrongClicks = Mathf.Min(_wrongClicks + 1, 3);

        switch (_wrongClicks)
        {
            case 1: SetCageSprite(cagedamaged1); break;
            case 2: SetCageSprite(cagedamaged2); break;
            case 3: SetCageSprite(cagedamaged3); break;
        }

        StartCoroutine(ShakeRT(cageRT, 0.30f, 14f));
        onImpact?.Invoke();
        onWrongClick?.Invoke();
        StartCoroutine(DiverScaredBriefly());
    }

    public void OnCorrectFind(int findIndex)
    {
        if (_resolved) return;
        StartCoroutine(DiverCelebrate());
    }

    public void TriggerSuccess()
    {
        if (_resolved) return;
        _resolved = true;
        _active = false;
        StartCoroutine(SuccessSequence());
    }

    public void TriggerBreach()
    {
        if (_resolved) return;
        _resolved = true;
        _active = false;
        StartCoroutine(BreachSequence());
    }

    // =================================================================
    // Update — FIX: sets both X and Y for shark every frame, and
    // defers positioning until layout is ready.
    // =================================================================

    void Update()
    {
        if (!_active) return;

        // Wait for layout so NormToLocal returns real coordinates.
        if (!CheckLayoutReady()) return;

        _idleTimer += Time.deltaTime;
        _bangCooldown -= Time.deltaTime;
        _diverBobTimer += Time.deltaTime;

        // ── Diver bob ────────────────────────────────────────────────
        if (diverRT != null)
        {
            float bobY = Mathf.Sin(_diverBobTimer * DiverBobFreq * Mathf.PI * 2f) * DiverBobAmp;
            diverRT.anchoredPosition = NormToLocal(DiverNX, DiverNYMid + bobY);
        }

        // ── Shark idle sway — FIX: sets BOTH x and y ─────────────────
        if (sharkRT != null && !_resolved)
        {
            float sway = Mathf.Sin(_idleTimer * 1.2f) * 5f;
            Vector2 rest = NormToLocal(SharkNX, SharkNYRest);
            sharkRT.anchoredPosition = new Vector2(rest.x + sway, rest.y);
        }

        // ── Shark bang timer ─────────────────────────────────────────
        float bangInterval = Mathf.Lerp(5.5f, 1.8f, _wrongClicks / 3f);
        if (_bangCooldown <= 0f && !_resolved)
        {
            _bangCooldown = bangInterval;
            StartCoroutine(SharkBangCage());
        }
    }

    // =================================================================
    // Sprite helpers
    // =================================================================

    void SetDiverSprite(Sprite spr)
    {
        if (diverImage == null || spr == null) return;
        diverImage.sprite = spr;
    }

    void SetCageSprite(Sprite spr)
    {
        if (cageImage == null || spr == null) return;
        cageImage.sprite = spr;
    }

    // =================================================================
    // Diver scared briefly (wrong click)
    // =================================================================

    IEnumerator DiverScaredBriefly()
    {
        SetDiverSprite(diverScared);
        yield return new WaitForSeconds(2.0f);
        if (!_resolved) SetDiverSprite(diverNormal);
    }

    // =================================================================
    // Cage drop
    // =================================================================

    IEnumerator DropCage()
    {
        if (cageRT == null) yield break;

        // Wait for layout so start/target positions are correct.
        while (!_layoutReady) yield return null;

        Vector2 start = NormToLocal(CageNX, CageNYTop + 0.25f);
        cageRT.anchoredPosition = start;
        Vector2 target = CageRestPos();
        float dur = 0.55f, t = 0f;

        while (t < dur)
        {
            t += Time.deltaTime;
            float p = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / dur), 3f);
            cageRT.anchoredPosition = Vector2.Lerp(start, target, p);
            yield return null;
        }
        cageRT.anchoredPosition = target;

        onImpact?.Invoke();
        StartCoroutine(ShakeRT(cageRT, 0.28f, 10f));

        yield return new WaitForSeconds(0.4f);
        SetDiverSprite(diverNormal);
    }

    Vector2 CageRestPos() => NormToLocal(CageNX, CageNYRest);

    // =================================================================
    // Shark bangs cage
    // =================================================================

    IEnumerator SharkBangCage()
    {
        if (sharkRT == null || _resolved) yield break;

        Vector2 restPos = NormToLocal(SharkNX, SharkNYRest);
        Rect r = panelRT != null ? panelRT.rect : new Rect(0, 0, 1920, 291);
        Vector2 lungeTarget = restPos + new Vector2(r.width * 0.08f, 0f);

        float t = 0f;
        while (t < 0.10f)
        {
            t += Time.deltaTime;
            if (sharkRT != null) sharkRT.anchoredPosition = Vector2.Lerp(restPos, lungeTarget, t / 0.10f);
            yield return null;
        }

        onImpact?.Invoke();
        if (cageRT != null) StartCoroutine(ShakeRT(cageRT, 0.20f, 12f));

        t = 0f;
        while (t < 0.25f)
        {
            t += Time.deltaTime;
            float p = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.25f), 2f);
            if (sharkRT != null) sharkRT.anchoredPosition = Vector2.Lerp(lungeTarget, restPos, p);
            yield return null;
        }
        if (sharkRT != null) sharkRT.anchoredPosition = restPos;
    }

    // =================================================================
    // Diver celebrate
    // =================================================================

    IEnumerator DiverCelebrate()
    {
        if (diverRT == null) yield break;
        Vector2 origin = diverRT.anchoredPosition;
        float t = 0f;
        while (t < 0.45f)
        {
            t += Time.deltaTime;
            float bump = Mathf.Sin(t / 0.45f * Mathf.PI) * 18f;
            diverRT.anchoredPosition = origin + new Vector2(0f, bump);
            yield return null;
        }
        diverRT.anchoredPosition = origin;
    }

    // =================================================================
    // Success sequence
    // =================================================================

    IEnumerator SuccessSequence()
    {
        if (diverRT != null)
        {
            Vector2 orig = diverRT.anchoredPosition;
            float t = 0f;
            while (t < 0.7f)
            {
                t += Time.deltaTime;
                float bump = Mathf.Sin(t / 0.7f * Mathf.PI * 2f) * 22f;
                if (diverRT != null) diverRT.anchoredPosition = orig + new Vector2(0f, bump);
                yield return null;
            }
            if (diverRT != null) diverRT.anchoredPosition = orig;
        }

        if (cageRT != null) StartCoroutine(ShakeRT(cageRT, 0.4f, 6f));
        SetCageSprite(cageSprite);
    }

    // =================================================================
    // Breach sequence — FIX: cage stays at rest, only shark + diver move
    // =================================================================

    IEnumerator BreachSequence()
    {
        SetCageSprite(cagedamaged3);
        SetDiverSprite(diverScared);

        // Shake cage violently but keep it in place
        if (cageRT != null) StartCoroutine(ShakeRT(cageRT, 0.30f, 20f));
        yield return new WaitForSeconds(0.15f);

        // Shark moves through cage position
        if (sharkRT != null)
        {
            Vector2 startShark = sharkRT.anchoredPosition;
            Vector2 throughCage = NormToLocal(CageNX + 0.18f, SharkNYRest);
            float t = 0f;
            while (t < 0.25f)
            {
                t += Time.deltaTime;
                if (sharkRT != null)
                    sharkRT.anchoredPosition = Vector2.Lerp(startShark, throughCage, t / 0.25f);
                yield return null;
            }
        }

        onImpact?.Invoke();
        yield return new WaitForSeconds(0.10f);

        // Shark charges toward diver
        if (sharkRT != null)
        {
            Vector2 start = sharkRT.anchoredPosition;
            Vector2 target = NormToLocal(DiverNX + 0.05f, DiverNYMid);
            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / 0.35f);
                if (sharkRT != null) sharkRT.anchoredPosition = Vector2.Lerp(start, target, p * p);
                yield return null;
            }
        }

        onImpact?.Invoke();
        if (diverRT != null) StartCoroutine(ShakeRT(diverRT, 0.5f, 14f));

        // Sink shark and diver off screen — cage stays in place
        yield return new WaitForSeconds(0.25f);
        float sink = 0f;
        Rect pr = panelRT != null ? panelRT.rect : new Rect(0, 0, 1920, 291);
        float speed = pr.height * 0.9f;

        while (sink < 1.0f)
        {
            sink += Time.deltaTime;
            Vector2 d = new Vector2(speed * 0.1f, -speed) * Time.deltaTime;
            if (sharkRT != null) sharkRT.anchoredPosition += d;
            if (diverRT != null) diverRT.anchoredPosition += d;
            yield return null;
        }
    }

    // =================================================================
    // Shake utility
    // =================================================================

    IEnumerator ShakeRT(RectTransform rt, float dur, float intensity)
    {
        if (rt == null) yield break;
        Vector2 orig = rt.anchoredPosition;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float d = 1f - Mathf.Clamp01(t / dur);
            rt.anchoredPosition = orig + new Vector2(
                Random.Range(-intensity, intensity) * d,
                Random.Range(-intensity * 0.3f, intensity * 0.3f) * d);
            yield return null;
        }
        rt.anchoredPosition = orig;
    }
}