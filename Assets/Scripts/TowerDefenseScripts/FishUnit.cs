using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One fish+log unit. Fish hangs below; log floats above (arms reach up).
/// Password text displays ON the log.
///
/// Four behaviours:
///   Red  + reaches tower  → pufferfish explosion, damages tower
///   Red  + TakeSpearHit() → enrages, charges at 2.5x speed
///   Green + reaches tower → collects in tower water
///   Green + TakeSpearHit()→ reeled to tower dead (no benefit)
///
/// CHANGES (June 2026):
///   - Init() accepts fishColor — all fish render the same neutral tint (no red/green giveaway)
///   - Label shows password only — no [RED]/[OK] prefix
///   - Log background is neutral dark — no red/green tint
///   - OnHitBySpear() renamed to TakeSpearHit() with isDead guard
///   - OnTriggerEnter2D removed — manager targets via TryHitNearestFish
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
    private bool isDead;          // guard: TakeSpearHit can only fire once per fish
    private float bobTimer;
    private float bobOffset;
    private Color fishColor;

    private static readonly Color EnragedCol = new Color(1.0f, 0.15f, 0.05f);

    // =================================================================
    // Init
    // =================================================================

    public void Init(bool scam, string label, float speed,
                     TowerDefenseManager mgr, Vector3 twrPos,
                     Sprite fSpr, Sprite lSpr,
                     Color fishColor)
    {
        isScam = scam;
        labelText = label;
        baseSpeed = speed;
        currentSpeed = speed;
        manager = mgr;
        towerPos = twrPos;
        fishSprite = fSpr;
        logSprite = lSpr;
        this.fishColor = fishColor;
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
        fishSR.color = fishColor;   // FIX: neutral colour, same for all fish
        fishSR.sortingOrder = 4;
        fishGO.transform.localScale = new Vector3(-1.8f, 1.8f, 1f); // face right toward tower; bigger sprite

        // ── Log root (above fish) ──
        logRoot = new GameObject("LogRoot").transform;
        logRoot.SetParent(transform, false);
        logRoot.localPosition = new Vector3(0f, 0.85f, 0f); // sits on top of the (now larger) fish

        var logGO = new GameObject("Log");
        logGO.transform.SetParent(logRoot, false);
        logGO.transform.localPosition = Vector3.zero;
        logSR = logGO.AddComponent<SpriteRenderer>();
        logSR.sprite = logSprite;
        logSR.color = Color.white;   // FIX: no red/green tint on log
        logSR.sortingOrder = 3;
        logGO.transform.localScale = new Vector3(1.6f, 0.85f, 1f); // wider and taller log banner

        // ── Label ON the log ──
        var canvasGO = new GameObject("LabelCanvas");
        canvasGO.transform.SetParent(logRoot, false);
        canvasGO.transform.localPosition = new Vector3(0f, 0.02f, -0.1f);
        const float k = 0.006f;
        canvasGO.transform.localScale = new Vector3(k, k, 1f);

        var c = canvasGO.AddComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace;
        c.sortingOrder = 6;
        canvasGO.GetComponent<RectTransform>().sizeDelta = new Vector2(260, 70);

        // FIX: neutral dark background — same for scam and safe
        var bg = new GameObject("LabelBg");
        bg.transform.SetParent(canvasGO.transform, false);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.10f, 0.10f, 0.18f, 0.88f);
        bgImg.raycastTarget = false;
        var bgRT = bg.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;

        // FIX: password text only — no [RED]/[OK] prefix
        var textGO = new GameObject("Label");
        textGO.transform.SetParent(canvasGO.transform, false);
        labelTMP = textGO.AddComponent<TextMeshProUGUI>();
        labelTMP.text = labelText;
        labelTMP.fontSize = 26;
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
        transform.position += new Vector3(0,
            Mathf.Sin(bobTimer * 1.8f + bobOffset) * 0.004f, 0);
        if (logRoot != null)
            logRoot.localRotation = Quaternion.Euler(0, 0,
                Mathf.Sin(bobTimer * 2.2f + bobOffset) * 3f);
        CheckReachedTower();
    }

    void TickEnraged()
    {
        bobTimer += Time.deltaTime;
        transform.position += Vector3.right * currentSpeed * Time.deltaTime;
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
    // FIX: TakeSpearHit — called by manager, one fish per click
    // =================================================================

    public void TakeSpearHit()
    {
        // isDead guard prevents any double-fire edge cases
        if (isDead || state == State.Enraged || state == State.Exploding ||
                      state == State.Collected || state == State.Dead || state == State.PulledIn)
            return;

        if (isScam)
        {
            // Award points first, then enrage — fish is still alive so don't set isDead
            manager.OnRedFishSpearHit(transform.position);
            state = State.Enraged;
            currentSpeed = baseSpeed * 2.5f;
            fishSR.color = EnragedCol;
            if (logSR != null) logSR.color = new Color(1f, 0.5f, 0.3f);
            StartCoroutine(EnrageFlash());
            manager.commentator?.SayRandom(new[] {
                "Oh no, you made it angry!",
                "It's charging the tower!",
                "Watch out — it's enraged!"
            });
        }
        else
        {
            isDead = true;   // green fish is done — lock it out
            state = State.PulledIn;
            manager.commentator?.SayRandom(new[] {
                "Oh dear, that was a safe one!",
                "Don't shoot the strong passwords!",
                "Let the good ones reach the tower!"
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
        manager.OnFishRemoved();
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
            manager.OnFishRemoved();
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

        fishSR.color = Color.white;
        transform.localScale = baseScale * 2.5f;
        yield return new WaitForSeconds(0.05f);

        manager.OnRedFishReachedTower(transform.position);
        manager.OnFishRemoved();

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

    // NOTE: OnTriggerEnter2D intentionally removed.
    // Manager uses TryHitNearestFish → TakeSpearHit() — no spear GameObjects spawned.
}