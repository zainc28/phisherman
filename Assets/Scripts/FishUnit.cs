using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One fish+log unit. Fish hangs below; log floats above (arms reach up).
/// Password/flag text displays ON the log.
///
/// Four behaviours:
///   Red  + reaches tower  → pufferfish explosion, damages tower
///   Red  + shot early     → enrages, charges at 2.5x speed
///   Green + reaches tower → collects in tower water
///   Green + shot early    → reeled to tower dead (no benefit)
/// </summary>
public class FishUnit : MonoBehaviour
{
    [HideInInspector] public Sprite fishSprite;
    [HideInInspector] public Sprite logSprite;

    // Child visuals
    private SpriteRenderer fishSR;
    private SpriteRenderer logSR;
    private TextMeshProUGUI labelTMP;
    private Transform logRoot;

    public enum State { Swimming, Enraged, PulledIn, Exploding, Collected, Dead }
    private State state = State.Swimming;

    private bool isScam;
    private string labelText;
    private float baseSpeed;
    private float currentSpeed;
    private TowerDefenseManager manager;
    private Vector3 towerPos;
    private bool hitRegistered;
    private float bobTimer;
    private float bobOffset;

    private static readonly Color ScamTint = new Color(1.0f, 0.42f, 0.42f);
    private static readonly Color SafeTint = new Color(0.42f, 1.0f, 0.62f);
    private static readonly Color ScamBg = new Color(0.78f, 0.08f, 0.08f, 0.92f);
    private static readonly Color SafeBg = new Color(0.06f, 0.58f, 0.18f, 0.92f);
    private static readonly Color EnragedCol = new Color(1.0f, 0.15f, 0.05f);

    // =================================================================
    // Init
    // =================================================================

    public void Init(bool scam, string label, float speed,
                     TowerDefenseManager mgr, Vector3 twrPos,
                     Sprite fSpr, Sprite lSpr)
    {
        isScam = scam;
        labelText = label;
        baseSpeed = speed;
        currentSpeed = speed;
        manager = mgr;
        towerPos = twrPos;
        fishSprite = fSpr;
        logSprite = lSpr;
        bobOffset = Random.Range(0f, Mathf.PI * 2f);

        BuildVisuals();
    }

    // =================================================================
    // Build child visuals
    // =================================================================

    void BuildVisuals()
    {
        // ── Fish body ──
        var fishGO = new GameObject("FishBody");
        fishGO.transform.SetParent(transform, false);
        fishGO.transform.localPosition = Vector3.zero;
        fishSR = fishGO.AddComponent<SpriteRenderer>();
        fishSR.sprite = fishSprite;
        fishSR.color = isScam ? ScamTint : SafeTint;
        fishSR.sortingOrder = 4;
        // Flip to face right (toward tower); most fish sprites face left
        fishGO.transform.localScale = new Vector3(-1f, 1f, 1f);

        // ── Log root (above fish — fish arms reach up) ──
        logRoot = new GameObject("LogRoot").transform;
        logRoot.SetParent(transform, false);
        logRoot.localPosition = new Vector3(0f, 0.55f, 0f);

        var logGO = new GameObject("Log");
        logGO.transform.SetParent(logRoot, false);
        logGO.transform.localPosition = Vector3.zero;
        logSR = logGO.AddComponent<SpriteRenderer>();
        logSR.sprite = logSprite;
        logSR.color = Color.white;
        logSR.sortingOrder = 3;
        logGO.transform.localScale = new Vector3(1.0f, 0.5f, 1f);

        // ── Label ON the log ──
        var canvasGO = new GameObject("LabelCanvas");
        canvasGO.transform.SetParent(logRoot, false);
        canvasGO.transform.localPosition = new Vector3(0f, 0.02f, -0.1f);
        const float k = 0.006f;
        canvasGO.transform.localScale = new Vector3(k, k, 1f);

        var c = canvasGO.AddComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace;
        c.sortingOrder = 6;
        canvasGO.GetComponent<RectTransform>().sizeDelta = new Vector2(220, 60);

        // Background tint on the log label
        var bg = new GameObject("LabelBg");
        bg.transform.SetParent(canvasGO.transform, false);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = isScam ? ScamBg : SafeBg;
        bgImg.raycastTarget = false;
        var bgRT = bg.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;

        // Text — plain ASCII to avoid font warnings
        var textGO = new GameObject("Label");
        textGO.transform.SetParent(canvasGO.transform, false);
        labelTMP = textGO.AddComponent<TextMeshProUGUI>();
        labelTMP.text = (isScam ? "[RED] " : "[OK] ") + labelText;
        labelTMP.fontSize = 18;
        labelTMP.color = Color.white;
        labelTMP.fontStyle = FontStyles.Bold;
        labelTMP.alignment = TextAlignmentOptions.Center;
        labelTMP.raycastTarget = false;
        var tRT = textGO.GetComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
        tRT.offsetMin = new Vector2(4, 0); tRT.offsetMax = new Vector2(-4, 0);
    }

    // =================================================================
    // Update
    // =================================================================

    void Update()
    {
        switch (state)
        {
            case State.Swimming: TickSwimming(); break;
            case State.Enraged: TickEnraged(); break;
        }
    }

    void TickSwimming()
    {
        bobTimer += Time.deltaTime;
        transform.position += Vector3.right * currentSpeed * Time.deltaTime;
        // Gentle bob
        transform.position += new Vector3(0,
            Mathf.Sin(bobTimer * 1.8f + bobOffset) * 0.004f, 0);
        // Log tilts
        if (logRoot != null)
            logRoot.localRotation = Quaternion.Euler(0, 0,
                Mathf.Sin(bobTimer * 2.2f + bobOffset) * 3f);
        CheckReachedTower();
    }

    void TickEnraged()
    {
        bobTimer += Time.deltaTime;
        transform.position += Vector3.right * currentSpeed * Time.deltaTime;
        // Angry wobble
        transform.position += new Vector3(0,
            Mathf.Sin(bobTimer * 14f) * 0.012f, 0);
        if (logRoot != null)
            logRoot.localRotation = Quaternion.Euler(0, 0,
                Mathf.Sin(bobTimer * 14f) * 8f);
        CheckReachedTower();
    }

    void CheckReachedTower()
    {
        if (hitRegistered) return;
        if (transform.position.x >= towerPos.x - 0.8f)
        {
            hitRegistered = true;
            OnReachedTower();
        }
    }

    // =================================================================
    // Hit by spear
    // =================================================================

    public void OnHitBySpear()
    {
        if (state != State.Swimming) return;

        if (isScam)
        {
            // Award points immediately on spear hit
            manager.OnRedFishSpearHit(transform.position);
            // Then enrage
            state = State.Enraged;
            currentSpeed = baseSpeed * 2.5f;
            fishSR.color = EnragedCol;
            if (logSR != null) logSR.color = new Color(1f, 0.5f, 0.3f);
            StartCoroutine(EnrageFlash());
            manager.commentator?.SayRandom(new[] {
                "Oh no, you made it angry!",
                "It's charging the tower!",
                "Shoot red fish from farther away!"
            });
        }
        else
        {
            state = State.PulledIn;
            manager.commentator?.SayRandom(new[] {
                "Oh dear, that was a safe one!",
                "Don't shoot the green fish!",
                "Let the good ones swim past!"
            });
            StartCoroutine(PullInToTower());
        }
    }

    IEnumerator EnrageFlash()
    {
        for (int i = 0; i < 4; i++)
        {
            fishSR.color = Color.white;
            yield return new WaitForSeconds(0.055f);
            fishSR.color = EnragedCol;
            yield return new WaitForSeconds(0.055f);
        }
    }

    IEnumerator PullInToTower()
    {
        Vector3 start = transform.position;
        Vector3 end = towerPos + new Vector3(-0.5f, 0, 0);
        float dur = 0.42f, t = 0f;

        while (t < dur)
        {
            t += Time.deltaTime;
            float p = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / dur), 3f);
            transform.position = Vector3.Lerp(start, end, p);
            transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.22f, p);
            yield return null;
        }
        manager.OnGreenFishShotDead();
        manager.OnFishRemoved();   // decrement wave counter
        Destroy(gameObject);
    }

    // =================================================================
    // Reached tower
    // =================================================================

    void OnReachedTower()
    {
        if (isScam)
        {
            state = State.Exploding;
            StartCoroutine(PufferfishExplode());
        }
        else
        {
            state = State.Collected;
            if (logRoot != null) logRoot.gameObject.SetActive(false);
            manager.OnGreenFishCollected(gameObject, fishSR.sprite);
            manager.OnFishRemoved();  // decrement wave counter
            Destroy(gameObject);
        }
    }

    IEnumerator PufferfishExplode()
    {
        fishSR.color = new Color(1f, 0.45f, 0.1f);
        if (logRoot != null) logRoot.gameObject.SetActive(false);

        float dur = 0.38f, t = 0f;
        Vector3 baseScale = transform.localScale;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            transform.localScale = baseScale * (1f + p * 1.2f);
            fishSR.color = Color.Lerp(new Color(1f, 0.45f, 0.1f), Color.red, p);
            yield return null;
        }

        // Flash burst
        fishSR.color = Color.white;
        transform.localScale = baseScale * 2.5f;
        yield return new WaitForSeconds(0.05f);

        // Notify manager — damage tower
        manager.OnRedFishReachedTower(transform.position);
        manager.OnFishRemoved();  // decrement wave counter

        // Shrink away
        t = 0f; dur = 0.22f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            transform.localScale = baseScale * (2.5f * (1f - p));
            yield return null;
        }
        Destroy(gameObject);
    }

    // =================================================================
    // Trigger (spear tag)
    // =================================================================

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Spear"))
            OnHitBySpear();
    }
}