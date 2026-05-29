using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the bottom simulation: a shark slowly approaches steel bars
/// protecting a fish and its eggs. Over 60 seconds the bars crack
/// progressively. The shark lunges every 5-11 seconds.
///
/// All child references are RectTransforms inside the ScreenSpaceOverlay
/// canvas. Set up by SpotDifferenceBuilder, driven by SpotDifferenceManager.
/// </summary>
public class SharkSimulation : MonoBehaviour
{
    [Header("References — wired by builder")]
    public RectTransform sharkRT;
    public Image sharkImage;
    public RectTransform fishRT;
    public RectTransform[] eggRTs;
    public Image[] barImages;          // the 6 steel bar images
    public GameObject[] crackStages;        // 3 root GameObjects, each a set of cracks

    [Header("Settings")]
    public float totalTime = 60f;            // matches manager timer

    // ── Motion constants (canvas-space, pivot = panel centre) ──
    private const float SharkStartX = -660f;   // far left
    private const float SharkEndX = -310f;   // resting just before lunge zone
    private const float SharkLungeX = -170f;   // teeth at bars

    // Egg base positions (set on Start from initial placement)
    private Vector2[] eggBasePos;

    // Internal state
    private float elapsed;
    private bool active;
    private bool lunging;
    private float nextLungeAt;
    private float sharkBobTimer;
    private float fishBobTimer;

    // Steel → cracked colour
    private static readonly Color BarSteelColor = new Color(0.54f, 0.63f, 0.70f);
    private static readonly Color BarCrackedColor = new Color(0.40f, 0.25f, 0.20f);

    // =================================================================
    // Public API
    // =================================================================

    public void StartSim()
    {
        active = true;
        elapsed = 0f;
        nextLungeAt = Random.Range(4f, 8f);

        // Store egg base positions
        if (eggRTs != null)
        {
            eggBasePos = new Vector2[eggRTs.Length];
            for (int i = 0; i < eggRTs.Length; i++)
                if (eggRTs[i] != null) eggBasePos[i] = eggRTs[i].anchoredPosition;
        }

        // Hide crack stages
        if (crackStages != null)
            foreach (var cs in crackStages)
                cs?.SetActive(false);
    }

    public void StopSim() { active = false; }

    // =================================================================
    // Update
    // =================================================================

    void Update()
    {
        if (!active) return;

        elapsed += Time.deltaTime;
        sharkBobTimer += Time.deltaTime;
        fishBobTimer += Time.deltaTime;

        float progress = Mathf.Clamp01(elapsed / totalTime);
        float timeLeft = totalTime - elapsed;

        // ── Shark rest position drifts toward bars ──
        float restX = Mathf.Lerp(SharkStartX, SharkEndX, progress);

        // ── Bob ──
        float bobY = Mathf.Sin(sharkBobTimer * 1.4f) * 10f;

        if (!lunging && sharkRT != null)
            sharkRT.anchoredPosition = new Vector2(restX, bobY);

        // ── Fish gentle bob ──
        if (fishRT != null)
            fishRT.anchoredPosition = new Vector2(fishRT.anchoredPosition.x,
                Mathf.Sin(fishBobTimer * 2.1f) * 5f);

        // ── Egg gentle wobble ──
        AnimateEggs();

        // ── Cracks appear at 40s / 20s / 5s remaining ──
        UpdateCracks(timeLeft, progress);

        // ── Schedule lunges ──
        if (!lunging && elapsed >= nextLungeAt)
        {
            StartCoroutine(DoLunge(restX));
            nextLungeAt = elapsed + Random.Range(5f, 11f);
        }
    }

    // =================================================================
    // Crack stages + bar colour
    // =================================================================

    void UpdateCracks(float timeLeft, float progress)
    {
        if (crackStages != null)
        {
            if (crackStages.Length > 0 && timeLeft < 40f && crackStages[0] != null)
                crackStages[0].SetActive(true);
            if (crackStages.Length > 1 && timeLeft < 20f && crackStages[1] != null)
                crackStages[1].SetActive(true);
            if (crackStages.Length > 2 && timeLeft < 5f && crackStages[2] != null)
                crackStages[2].SetActive(true);
        }

        if (barImages != null)
        {
            float crackLerp = Mathf.Clamp01(1f - timeLeft / 40f);
            Color barCol = Color.Lerp(BarSteelColor, BarCrackedColor, crackLerp);
            foreach (var b in barImages)
                if (b != null) b.color = barCol;
        }
    }

    // =================================================================
    // Lunge coroutine
    // =================================================================

    IEnumerator DoLunge(float restX)
    {
        lunging = true;

        // ── Dash toward bars ──
        Vector2 dashStart = sharkRT != null ? sharkRT.anchoredPosition : Vector2.zero;
        Vector2 dashTarget = new Vector2(SharkLungeX, 0f);
        float t = 0f, dashDur = 0.22f;
        while (t < dashDur)
        {
            t += Time.deltaTime;
            float p = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / dashDur), 2f);
            if (sharkRT != null) sharkRT.anchoredPosition = Vector2.Lerp(dashStart, dashTarget, p);
            yield return null;
        }
        if (sharkRT != null) sharkRT.anchoredPosition = dashTarget;

        // ── Hold + shake fish and eggs ──
        yield return new WaitForSeconds(0.12f);
        if (fishRT != null) StartCoroutine(Shake(fishRT, 0.35f, 9f));
        if (eggRTs != null)
            foreach (var e in eggRTs)
                if (e != null) StartCoroutine(Shake(e, 0.35f, 6f));

        // ── Recoil back ──
        Vector2 recoilStart = sharkRT != null ? sharkRT.anchoredPosition : dashTarget;
        Vector2 recoilEnd = new Vector2(restX, 0f);
        t = 0f; float recoilDur = 0.48f;
        while (t < recoilDur)
        {
            t += Time.deltaTime;
            float p = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / recoilDur), 3f);
            if (sharkRT != null) sharkRT.anchoredPosition = Vector2.Lerp(recoilStart, recoilEnd, p);
            yield return null;
        }

        lunging = false;
    }

    // =================================================================
    // Egg wobble
    // =================================================================

    void AnimateEggs()
    {
        if (eggRTs == null || eggBasePos == null) return;
        for (int i = 0; i < eggRTs.Length; i++)
        {
            if (eggRTs[i] == null) continue;
            float wobble = Mathf.Sin(fishBobTimer * 1.7f + i * 1.2f) * 2.5f;
            eggRTs[i].anchoredPosition = eggBasePos[i] + new Vector2(0, wobble);
        }
    }

    // =================================================================
    // Shake utility
    // =================================================================

    IEnumerator Shake(RectTransform rt, float dur, float intensity)
    {
        Vector2 orig = rt.anchoredPosition;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float d = 1f - Mathf.Clamp01(t / dur);
            rt.anchoredPosition = orig + new Vector2(
                Random.Range(-intensity, intensity) * d,
                Random.Range(-intensity, intensity) * d);
            yield return null;
        }
        rt.anchoredPosition = orig;
    }
}