using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// LureSimulation.cs  —  Cage & Diver edition
///
/// Layout (all positions are normalised fractions of the sim panel rect):
///
///   RIGHT SIDE  (nx ≈ 0.78)  Phisherman diver swims up-and-down idly.
///   CENTRE-LEFT (nx ≈ 0.30)  Shark lurks inside a cage.
///
/// Game flow
///   • Start   : cage drops from top and lands over the shark.
///   • Wrong ×1: cage lifts one third of the way up  (shark visible below bars).
///   • Wrong ×2: cage lifts two thirds.
///   • Wrong ×3: cage fully lifted → shark swims free → breach animation.
///   • Periodically the shark bangs the cage bars (idle taunt).
///   • Success  : diver gives a thumbs-up bob; shark stays caged.
/// </summary>
public class LureSimulation : MonoBehaviour
{
    // ── References wired by the builder ──────────────────────────────────────
    [Header("References — wired by builder")]
    public RectTransform rodLineRT;      // kept for compatibility (hidden / unused)
    public RectTransform hookRT;         // kept for compatibility (hidden / unused)
    public RectTransform[] debrisLayers; // not used in cage edition — kept so builder compiles
    public Image[] debrisImages; // not used in cage edition — kept so builder compiles

    public RectTransform sharkRT;
    public Image sharkImage;
    public RectTransform panelRT;        // sim root RectTransform (for size look-up)

    // Cage visuals — four bars + a base, all children of the panel
    public RectTransform cageRT;         // the whole cage group
    public Image[] cageBars;       // 3 horizontal bars; index 0 = bottom bar
    public Image cageBase;       // solid floor of the cage
    public Image cageShadow;     // subtle shadow beneath cage

    // Diver (phisherman sprite)
    public RectTransform diverRT;
    public Image diverImage;

    // Audio callbacks
    [Header("Audio callbacks")]
    public System.Action onImpact;
    public System.Action onRodWinding;

    [Header("Settings")]
    public float totalTime = 60f;

    // ── Private state ─────────────────────────────────────────────────────────
    private int _wrongClicks;
    private bool _active;
    private bool _resolved;
    private float _idleTimer;
    private float _bangCooldown;
    private float _diverBobTimer;

    // Cage lift stages (normalised Y offsets added to cage rest position)
    // 0 = fully down (shark trapped), 1.0 = fully lifted
    private static readonly float[] CageLiftNY = { 0f, 0.28f, 0.56f, 1.0f };

    // Cage rest position (normalised, in panel space)
    private const float CageNX = 0.30f;
    private const float CageNYRest = 0.10f;   // cage bottom sits here when fully down
    private const float CageNYTop = 0.90f;   // cage top anchor when fully raised

    // Shark rests at the cage centre
    private const float SharkNX = 0.30f;
    private const float SharkNYRest = 0.38f;

    // Diver swims on the right
    private const float DiverNX = 0.78f;
    private const float DiverNYMid = 0.50f;
    private const float DiverBobAmp = 0.10f;   // normalised units
    private const float DiverBobFreq = 0.55f;

    // Bar colours
    private static readonly Color BarSafe = new Color(0.72f, 0.58f, 0.22f, 0.92f);  // gold bars
    private static readonly Color BarStressed = new Color(0.85f, 0.40f, 0.10f, 0.95f);  // orange-red when shark bangs
    private static readonly Color BarShattered = new Color(0.92f, 0.20f, 0.10f, 1.00f);  // red when fully lifted

    // ==========================================================================
    // Helpers — normalised panel coords → anchoredPosition
    // ==========================================================================

    Vector2 NormToLocal(float nx, float ny)
    {
        Rect r = panelRT != null ? panelRT.rect : new Rect(0, 0, 1920, 291);
        return new Vector2(
            (nx - 0.5f) * r.width,
            (ny - 0.5f) * r.height);
    }

    // ==========================================================================
    // Public API
    // ==========================================================================

    public void StartSim(int maxLives, int totalFlags)
    {
        _wrongClicks = 0;
        _active = true;
        _resolved = false;
        _idleTimer = 0f;
        _bangCooldown = 0f;
        _diverBobTimer = 0f;

        // Place shark at rest
        if (sharkRT != null)
            sharkRT.anchoredPosition = NormToLocal(SharkNX, SharkNYRest);

        // Place diver
        if (diverRT != null)
            diverRT.anchoredPosition = NormToLocal(DiverNX, DiverNYMid);

        // Reset cage bars
        if (cageBars != null)
            foreach (var bar in cageBars)
                if (bar != null) bar.color = BarSafe;

        // Drop cage from above
        if (cageRT != null)
        {
            cageRT.anchoredPosition = NormToLocal(CageNX, CageNYTop + 0.25f); // start above panel
            cageRT.gameObject.SetActive(true);
            StartCoroutine(DropCage());
        }
    }

    public void StopSim() { _active = false; }

    public void OnWrongClick()
    {
        if (_resolved) return;
        _wrongClicks = Mathf.Min(_wrongClicks + 1, 3);
        StartCoroutine(LiftCage(_wrongClicks));

        // Colour bars progressively
        if (cageBars != null)
        {
            for (int i = 0; i < cageBars.Length; i++)
            {
                if (cageBars[i] == null) continue;
                cageBars[i].color = (_wrongClicks >= 3) ? BarShattered
                                  : (_wrongClicks >= 2) ? BarStressed
                                  : BarSafe;
            }
        }
    }

    public void OnCorrectFind(int findIndex)
    {
        // No hook mechanic in cage edition — kept for API compatibility
        if (_resolved) return;
        // Optional: diver does a small celebratory bob
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

    // ==========================================================================
    // Update — idle animations
    // ==========================================================================

    void Update()
    {
        if (!_active) return;

        _idleTimer += Time.deltaTime;
        _bangCooldown -= Time.deltaTime;
        _diverBobTimer += Time.deltaTime;

        // Diver bobs up and down
        if (diverRT != null)
        {
            float bobY = Mathf.Sin(_diverBobTimer * DiverBobFreq * Mathf.PI * 2f) * DiverBobAmp;
            diverRT.anchoredPosition = NormToLocal(DiverNX, DiverNYMid + bobY);
        }

        // Shark idle sway (small horizontal wiggle inside cage)
        if (sharkRT != null && !_resolved)
        {
            float sway = Mathf.Sin(_idleTimer * 1.2f) * 5f;
            Vector2 rest = NormToLocal(SharkNX, SharkNYRest);
            sharkRT.anchoredPosition = new Vector2(rest.x + sway, sharkRT.anchoredPosition.y);
        }

        // Periodic shark bang — more frequent with each wrong click
        float bangInterval = Mathf.Lerp(5.5f, 1.8f, _wrongClicks / 3f);
        if (_bangCooldown <= 0f && !_resolved)
        {
            _bangCooldown = bangInterval;
            StartCoroutine(SharkBangCage());
        }
    }

    // ==========================================================================
    // Cage drop (start of game)
    // ==========================================================================

    IEnumerator DropCage()
    {
        if (cageRT == null) yield break;
        Vector2 start = cageRT.anchoredPosition;
        Vector2 target = CageRestPos();
        float dur = 0.55f;
        float t = 0f;

        while (t < dur)
        {
            t += Time.deltaTime;
            float p = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / dur), 3f);
            cageRT.anchoredPosition = Vector2.Lerp(start, target, p);
            yield return null;
        }
        cageRT.anchoredPosition = target;

        // Thud shake
        onImpact?.Invoke();
        StartCoroutine(ShakeRT(cageRT, 0.28f, 10f));
    }

    Vector2 CageRestPos() => NormToLocal(CageNX, CageNYRest);

    Vector2 CageLiftedPos(int wrongCount)
    {
        float liftFraction = wrongCount < CageLiftNY.Length
            ? CageLiftNY[wrongCount] : 1f;
        float ny = Mathf.Lerp(CageNYRest, CageNYTop, liftFraction);
        return NormToLocal(CageNX, ny);
    }

    // ==========================================================================
    // Cage lift (per wrong click)
    // ==========================================================================

    IEnumerator LiftCage(int wrongCount)
    {
        if (cageRT == null) yield break;
        onRodWinding?.Invoke();

        Vector2 start = cageRT.anchoredPosition;
        Vector2 target = CageLiftedPos(wrongCount);
        float dur = 0.50f;
        float t = 0f;

        while (t < dur)
        {
            t += Time.deltaTime;
            float p = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / dur), 3f);
            cageRT.anchoredPosition = Vector2.Lerp(start, target, p);
            yield return null;
        }
        cageRT.anchoredPosition = target;

        // Brief cage shake after lift
        StartCoroutine(ShakeRT(cageRT, 0.22f, 8f));
        onImpact?.Invoke();
    }

    // ==========================================================================
    // Shark bangs the cage
    // ==========================================================================

    IEnumerator SharkBangCage()
    {
        if (sharkRT == null || _resolved) yield break;

        Vector2 restPos = NormToLocal(SharkNX, SharkNYRest);

        // Lunge toward cage bar on the right side
        Rect r = panelRT != null ? panelRT.rect : new Rect(0, 0, 1920, 291);
        Vector2 lungeTarget = restPos + new Vector2(r.width * 0.08f, 0f);

        // Quick lunge right
        float t = 0f;
        while (t < 0.10f)
        {
            t += Time.deltaTime;
            if (sharkRT != null)
                sharkRT.anchoredPosition = Vector2.Lerp(restPos, lungeTarget, t / 0.10f);
            yield return null;
        }

        // Flash bars orange
        onImpact?.Invoke();
        if (cageBars != null && _wrongClicks < 3)
        {
            foreach (var bar in cageBars) if (bar != null) bar.color = BarStressed;
        }
        if (cageRT != null) StartCoroutine(ShakeRT(cageRT, 0.20f, 12f));

        // Recoil back
        t = 0f;
        while (t < 0.25f)
        {
            t += Time.deltaTime;
            float p = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.25f), 2f);
            if (sharkRT != null)
                sharkRT.anchoredPosition = Vector2.Lerp(lungeTarget, restPos, p);
            yield return null;
        }
        if (sharkRT != null) sharkRT.anchoredPosition = restPos;

        // Restore bar colour after a beat
        yield return new WaitForSeconds(0.35f);
        if (cageBars != null && _wrongClicks < 3)
            foreach (var bar in cageBars) if (bar != null) bar.color = BarSafe;
    }

    // ==========================================================================
    // Diver celebratory bob
    // ==========================================================================

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

    // ==========================================================================
    // Success sequence — cage stays down, diver cheers
    // ==========================================================================

    IEnumerator SuccessSequence()
    {
        // Diver big bob + shake cage triumphantly
        if (diverRT != null)
        {
            Vector2 orig = diverRT.anchoredPosition;
            float t = 0f;
            while (t < 0.7f)
            {
                t += Time.deltaTime;
                float bump = Mathf.Sin(t / 0.7f * Mathf.PI * 2f) * 22f;
                if (diverRT != null)
                    diverRT.anchoredPosition = orig + new Vector2(0f, bump);
                yield return null;
            }
            if (diverRT != null) diverRT.anchoredPosition = orig;
        }

        if (cageRT != null) StartCoroutine(ShakeRT(cageRT, 0.4f, 6f));
        if (cageBars != null)
            foreach (var bar in cageBars) if (bar != null) bar.color = BarSafe;
    }

    // ==========================================================================
    // Breach sequence — cage fully lifted, shark swims free toward diver
    // ==========================================================================

    IEnumerator BreachSequence()
    {
        // 1. Flash all bars red
        if (cageBars != null)
            foreach (var bar in cageBars) if (bar != null) bar.color = BarShattered;

        // 2. Lift cage all the way off screen
        if (cageRT != null)
        {
            Vector2 start = cageRT.anchoredPosition;
            Vector2 target = NormToLocal(CageNX, CageNYTop + 0.60f);
            float t = 0f;
            while (t < 0.38f)
            {
                t += Time.deltaTime;
                float p = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.38f), 3f);
                if (cageRT != null) cageRT.anchoredPosition = Vector2.Lerp(start, target, p);
                yield return null;
            }
        }

        onImpact?.Invoke();
        yield return new WaitForSeconds(0.15f);

        // 3. Shark charges toward diver (right side)
        if (sharkRT != null)
        {
            Vector2 start = sharkRT.anchoredPosition;
            Vector2 target = NormToLocal(DiverNX + 0.05f, DiverNYMid);
            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / 0.35f);
                if (sharkRT != null)
                    sharkRT.anchoredPosition = Vector2.Lerp(start, target, p * p);
                yield return null;
            }
        }

        onImpact?.Invoke();
        // Diver shakes in panic
        if (diverRT != null) StartCoroutine(ShakeRT(diverRT, 0.5f, 14f));

        // 4. Sink everything off the bottom of the panel
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
            if (cageRT != null) cageRT.anchoredPosition += d;
            yield return null;
        }
    }

    // ==========================================================================
    // Shake utility
    // ==========================================================================

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