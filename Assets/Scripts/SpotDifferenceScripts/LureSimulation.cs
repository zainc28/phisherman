using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// LureSimulation.cs
///
/// ALL positions are stored as normalised fractions (0..1) of the sim panel's
/// rect so nothing flies outside the panel regardless of screen size.
///
/// Layout:
///   Rod hangs vertically from top, slightly right of centre.
///   4 horizontal debris planks at staggered heights surround the rod.
///   Hook starts below plank 3 (trapped). Rises past planks 2, 1, 0 as flags found.
///   Shark starts bottom-left, moves diagonally up-right.
///   Wrong click 1-3 → shark lunges into plank 3, 2, 1 respectively.
///   All lives gone → shark bites the rod.
/// </summary>
public class LureSimulation : MonoBehaviour
{
    [Header("References — wired by builder")]
    public RectTransform rodLineRT;
    public RectTransform hookRT;
    public RectTransform[] debrisLayers;   // 4 planks
    public Image[] debrisImages;
    public RectTransform sharkRT;
    public Image sharkImage;
    public RectTransform panelRT;        // the sim root RectTransform (for size lookup)

    [Header("Audio callbacks")]
    public System.Action onImpact;
    public System.Action onRodWinding;

    [Header("Settings")]
    public float totalTime = 60f;

    // ── State ──
    private int _wrongClicks;
    private int _correctFinds;
    private bool _active;
    private bool _resolved;
    private float _bobTimer;
    private bool _lunging;

    // Colours
    private static readonly Color DebrisHealthy = new Color(0.36f, 0.24f, 0.12f);
    private static readonly Color DebrisDamaged = new Color(0.58f, 0.32f, 0.14f);
    private static readonly Color DebrisShattered = new Color(0.75f, 0.44f, 0.18f);

    // =================================================================
    // Helpers — convert normalised panel coords → anchoredPosition
    // The sim panel is anchored 0-27% of the canvas; objects inside it
    // use anchoredPosition relative to the panel's own pivot (centre).
    // We express all positions as fractions of panel width/height so
    // nothing ever escapes the panel.
    // =================================================================

    Vector2 NormToLocal(float nx, float ny)
    {
        // nx,ny in [0,1] → local anchoredPosition inside sim panel
        Rect r = panelRT != null ? panelRT.rect : new Rect(0, 0, 1920, 291);
        return new Vector2(
            (nx - 0.5f) * r.width,
            (ny - 0.5f) * r.height);
    }

    // Rod is at nx=0.55 (slightly right of centre), top of panel
    // Hook starts at nx=0.55, ny=0.28 (near bottom, trapped behind plank 3)
    Vector2 RodTopLocal() => NormToLocal(0.55f, 1.0f);
    Vector2 HookStartLocal() => NormToLocal(0.55f, 0.28f);
    Vector2 HookSuccessLocal() => NormToLocal(0.55f, 0.92f);

    // Hook ny per correct find (rises through plank 3→2→1→0→free)
    float[] HookRiseNY = { 0.42f, 0.58f, 0.74f, 0.88f, 0.92f };

    // Shark stages: normalised positions inside sim panel
    Vector2 SharkStageNorm(int stage)
    {
        switch (stage)
        {
            case 0: return NormToLocal(0.05f, 0.18f);  // far bottom-left
            case 1: return NormToLocal(0.18f, 0.28f);  // approaching plank 3
            case 2: return NormToLocal(0.30f, 0.40f);  // near plank 2
            case 3: return NormToLocal(0.42f, 0.50f);  // near plank 1
            default: return NormToLocal(0.54f, 0.55f);  // at rod (bite)
        }
    }

    // =================================================================
    // Public API
    // =================================================================

    public void StartSim(int maxLives, int totalFlags)
    {
        _wrongClicks = 0;
        _correctFinds = 0;
        _active = true;
        _resolved = false;
        _bobTimer = 0f;
        _lunging = false;

        if (sharkRT != null) sharkRT.anchoredPosition = SharkStageNorm(0);
        if (hookRT != null) hookRT.anchoredPosition = HookStartLocal();
        UpdateRodLine();

        if (debrisImages != null)
            foreach (var d in debrisImages)
                if (d != null) { d.color = DebrisHealthy; d.gameObject.SetActive(true); }
    }

    public void StopSim() { _active = false; }

    public void OnWrongClick()
    {
        if (_resolved) return;
        _wrongClicks++;
        StartCoroutine(SharkLunge());
    }

    public void OnCorrectFind(int findIndex)
    {
        if (_resolved) return;
        _correctFinds++;
        StartCoroutine(HookRise(findIndex));
    }

    public void TriggerSuccess()
    {
        if (_resolved) return;
        _resolved = true; _active = false;
        StartCoroutine(ReelSuccess());
    }

    public void TriggerBreach()
    {
        if (_resolved) return;
        _resolved = true; _active = false;
        StartCoroutine(SharkBreach());
    }

    // =================================================================
    // Update — gentle bob
    // =================================================================

    void Update()
    {
        if (!_active) return;
        _bobTimer += Time.deltaTime;

        if (sharkRT != null && !_resolved && !_lunging)
        {
            Vector2 target = SharkStageNorm(_wrongClicks);
            Vector2 current = sharkRT.anchoredPosition;
            // Lazy drift toward target
            sharkRT.anchoredPosition = Vector2.Lerp(current, target, Time.deltaTime * 1.2f);

            // Vertical intimidation bob — increases with wrong clicks
            float tension = Mathf.Clamp01(_wrongClicks / 3f);
            float rockAmp = Mathf.Lerp(4f, 12f, tension);
            float rockFreq = Mathf.Lerp(1.0f, 2.5f, tension);
            var p = sharkRT.anchoredPosition;
            sharkRT.anchoredPosition = new Vector2(p.x, p.y + Mathf.Sin(_bobTimer * rockFreq) * rockAmp * Time.deltaTime * 60f * 0.016f);
        }

        UpdateRodLine();
    }

    void UpdateRodLine()
    {
        if (rodLineRT == null || hookRT == null || panelRT == null) return;
        Vector2 topPos = new Vector2(hookRT.anchoredPosition.x, panelRT.rect.height * 0.5f);
        float lineLen = topPos.y - hookRT.anchoredPosition.y;
        rodLineRT.anchoredPosition = new Vector2(hookRT.anchoredPosition.x,
            hookRT.anchoredPosition.y + lineLen * 0.5f);
        rodLineRT.sizeDelta = new Vector2(3f, Mathf.Max(lineLen, 4f));
    }

    // =================================================================
    // Shark lunge
    // =================================================================

    IEnumerator SharkLunge()
    {
        _lunging = true;
        if (sharkRT == null) { _lunging = false; yield break; }

        bool isFinal = _wrongClicks >= 3;
        int debrisIdx = Mathf.Clamp(_wrongClicks - 1, 0, (debrisLayers?.Length ?? 1) - 1);

        Vector2 restPos = SharkStageNorm(_wrongClicks);

        // Windup — rock back
        Vector2 start = sharkRT.anchoredPosition;
        Vector2 windupPos = start + NormToLocal(0f, 0f) + new Vector2(-30f, -15f);
        float t = 0f;
        while (t < 0.18f)
        {
            t += Time.deltaTime;
            if (sharkRT != null)
                sharkRT.anchoredPosition = Vector2.Lerp(start, windupPos, t / 0.18f);
            yield return null;
        }

        // Lunge toward debris/rod
        Vector2 lungeTarget = isFinal
            ? NormToLocal(0.56f, 0.55f)
            : restPos + new Vector2(20f, 8f);

        t = 0f;
        while (t < 0.16f)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / 0.16f);
            if (sharkRT != null)
                sharkRT.anchoredPosition = Vector2.Lerp(windupPos, lungeTarget, p * p);
            yield return null;
        }

        // Impact
        onImpact?.Invoke();
        if (debrisLayers != null && debrisIdx < debrisLayers.Length && debrisLayers[debrisIdx] != null)
            StartCoroutine(ShakeRT(debrisLayers[debrisIdx], 0.32f, 16f));

        UpdateDebrisCracks(debrisIdx, isFinal);

        if (!isFinal)
        {
            // Recoil to rest
            Vector2 recoilStart = sharkRT != null ? sharkRT.anchoredPosition : lungeTarget;
            t = 0f;
            while (t < 0.42f)
            {
                t += Time.deltaTime;
                float p = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.42f), 3f);
                if (sharkRT != null)
                    sharkRT.anchoredPosition = Vector2.Lerp(recoilStart, restPos, p);
                yield return null;
            }
            if (sharkRT != null) sharkRT.anchoredPosition = restPos;
        }

        _lunging = false;
    }

    // =================================================================
    // Hook rise
    // =================================================================

    IEnumerator HookRise(int findIndex)
    {
        if (hookRT == null) yield break;
        onRodWinding?.Invoke();

        Vector2 start = hookRT.anchoredPosition;
        int nyIdx = Mathf.Clamp(findIndex, 0, HookRiseNY.Length - 1);
        Vector2 target = NormToLocal(0.55f, HookRiseNY[nyIdx]);

        float t = 0f;
        while (t < 0.45f)
        {
            t += Time.deltaTime;
            float p = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.45f), 3f);
            if (hookRT != null) hookRT.anchoredPosition = Vector2.Lerp(start, target, p);
            yield return null;
        }
        if (hookRT != null) hookRT.anchoredPosition = target;
    }

    // =================================================================
    // Success — reel above surface
    // =================================================================

    IEnumerator ReelSuccess()
    {
        if (hookRT == null) yield break;
        onRodWinding?.Invoke();
        Vector2 start = hookRT.anchoredPosition, target = HookSuccessLocal();
        float t = 0f;
        while (t < 0.7f)
        {
            t += Time.deltaTime;
            float p = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.7f), 3f);
            if (hookRT != null) hookRT.anchoredPosition = Vector2.Lerp(start, target, p);
            yield return null;
        }
        if (hookRT != null) StartCoroutine(ShakeRT(hookRT, 0.4f, 5f));
    }

    // =================================================================
    // Breach — shark bites rod
    // =================================================================

    IEnumerator SharkBreach()
    {
        if (sharkRT == null) yield break;

        UpdateDebrisCracks(3, true);
        if (debrisLayers != null)
            foreach (var dl in debrisLayers)
                if (dl != null) StartCoroutine(ShakeRT(dl, 0.5f, 20f));

        Vector2 start = sharkRT.anchoredPosition;
        Vector2 rodPos = hookRT != null ? hookRT.anchoredPosition : NormToLocal(0.55f, 0.5f);
        float t = 0f;
        while (t < 0.20f)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / 0.20f);
            if (sharkRT != null) sharkRT.anchoredPosition = Vector2.Lerp(start, rodPos, p * p);
            yield return null;
        }
        onImpact?.Invoke();
        if (hookRT != null) StartCoroutine(ShakeRT(hookRT, 0.5f, 10f));

        // Bend hook sideways
        if (hookRT != null)
        {
            Vector2 hs = hookRT.anchoredPosition;
            t = 0f;
            while (t < 0.28f)
            {
                t += Time.deltaTime;
                if (hookRT != null)
                    hookRT.anchoredPosition = Vector2.Lerp(hs, hs + new Vector2(40f, -20f), t / 0.28f);
                yield return null;
            }
        }

        yield return new WaitForSeconds(0.25f);

        // Sink everything
        float sink = 0f;
        Rect r = panelRT != null ? panelRT.rect : new Rect(0, 0, 1920, 291);
        float sinkSpeed = r.height * 0.8f;   // move 80% of panel height per second
        while (sink < 1.0f)
        {
            sink += Time.deltaTime;
            Vector2 d = new Vector2(sinkSpeed * 0.15f, -sinkSpeed) * Time.deltaTime;
            if (sharkRT != null) sharkRT.anchoredPosition += d;
            if (hookRT != null) hookRT.anchoredPosition += d;
            if (rodLineRT != null) rodLineRT.anchoredPosition += d;
            yield return null;
        }
    }

    // =================================================================
    // Debris colours
    // =================================================================

    void UpdateDebrisCracks(int upToIndex, bool shatter)
    {
        if (debrisImages == null) return;
        for (int i = 0; i <= upToIndex && i < debrisImages.Length; i++)
        {
            if (debrisImages[i] == null) continue;
            debrisImages[i].color = (shatter && i == upToIndex)
                ? DebrisShattered : DebrisDamaged;
        }
    }

    // =================================================================
    // Shake utility
    // =================================================================

    IEnumerator ShakeRT(RectTransform rt, float dur, float intensity)
    {
        if (rt == null) yield break;
        Vector2 orig = rt.anchoredPosition; float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime; float d = 1f - Mathf.Clamp01(t / dur);
            rt.anchoredPosition = orig + new Vector2(
                Random.Range(-intensity, intensity) * d,
                Random.Range(-intensity * 0.3f, intensity * 0.3f) * d);
            yield return null;
        }
        rt.anchoredPosition = orig;
    }
}