using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One fish+log unit. Fish hangs below; log floats above (arms reach up).
/// Password text displays ON the log.
///
/// REEL-IN MECHANIC:
///   When a spear hits, a line renders from the speargun pivot to the fish,
///   and the fish is physically dragged toward the tower over ~0.6s.
///   Once it arrives at the tower edge, THEN the original outcome fires:
///     - Red flag  → puffs up and explodes (damages tower)
///     - Green flag → happy sprite, swims into the pool
/// </summary>
public class FishUnit : MonoBehaviour
{
    private Sprite fishNormalSprite;
    private Sprite fishHappySprite;
    private Sprite fishPuffedSprite;
    [HideInInspector] public Sprite logSprite;

    // Child visuals
    private SpriteRenderer fishSR;
    private SpriteRenderer logSR;
    private SpriteRenderer reelLineSR;   // the spear-line drawn during reel-in
    private TextMeshProUGUI labelTMP;
    private Transform logRoot;

    public enum State { Swimming, Enraged, Defused, Exploding, Collected, Dead, ReelingIn }
    private State state = State.Swimming;

    private bool isScam;
    private string labelText;
    private float baseSpeed;
    private float currentSpeed;
    private TowerDefenseManager manager;
    private Vector3 towerPos;
    private bool hitRegistered;
    private bool isDead;
    private float bobTimer;
    private float bobOffset;
    private Color fishColor;

    // Reel-in state
    private Transform speargunPivot;     // set by manager before TakeSpearHit
    private GameObject reelLineGO;

    // =================================================================
    // Init
    // =================================================================

    public void Init(bool scam, string label, float speed,
                     TowerDefenseManager mgr, Vector3 twrPos,
                     Sprite fNormal, Sprite fHappy, Sprite fPuffed, Sprite lSpr,
                     Color fishColor)
    {
        isScam = scam;
        labelText = label;
        baseSpeed = speed;
        currentSpeed = speed;
        manager = mgr;
        towerPos = twrPos;

        fishNormalSprite = fNormal;
        fishHappySprite = fHappy;
        fishPuffedSprite = fPuffed;
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
        fishSR.sprite = fishNormalSprite;
        fishSR.color = fishColor;
        fishSR.sortingOrder = 4;
        fishGO.transform.localScale = new Vector3(-1.8f, 1.8f, 1f);

        // ── Log root (above fish) ──
        logRoot = new GameObject("LogRoot").transform;
        logRoot.SetParent(transform, false);
        logRoot.localPosition = new Vector3(0f, 0.85f, 0f);

        var logGO = new GameObject("Log");
        logGO.transform.SetParent(logRoot, false);
        logGO.transform.localPosition = Vector3.zero;
        logSR = logGO.AddComponent<SpriteRenderer>();
        logSR.sprite = logSprite;
        logSR.color = Color.white;
        logSR.sortingOrder = 3;
        logGO.transform.localScale = new Vector3(1.6f, 0.85f, 1f);

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

        var bg = new GameObject("LabelBg");
        bg.transform.SetParent(canvasGO.transform, false);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.10f, 0.10f, 0.18f, 0.88f);
        bgImg.raycastTarget = false;
        var bgRT = bg.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;

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
            case State.Swimming:
            case State.Defused:
                TickSwimming();
                break;
            case State.Enraged:
                TickEnraged();
                break;
            case State.ReelingIn:
                UpdateReelLine();
                break;
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
    // Spear hit — begin reel-in instead of instant outcome
    // =================================================================

    /// <summary>
    /// Called by TowerDefenseManager after a spear hit.
    /// pivot: the speargun pivot Transform (for drawing the line).
    /// </summary>
    public void TakeSpearHit(Transform pivot)
    {
        if (isDead || state == State.Enraged || state == State.Exploding ||
            state == State.Collected || state == State.Dead || state == State.Defused ||
            state == State.ReelingIn)
            return;

        speargunPivot = pivot;
        state = State.ReelingIn;
        hitRegistered = true;           // stop natural tower-hit logic while reeling

        // Flash the fish to signal the hit
        StartCoroutine(HitFlash());

        // Build a persistent line GO that we update every frame
        SpawnReelLine();

        // Begin the reel-in coroutine
        StartCoroutine(ReelToTower());
    }

    // Kept for API compatibility (builder wires without pivot)
    public void TakeSpearHit()
    {
        TakeSpearHit(null);
    }

    // =================================================================
    // Reel-in coroutine
    // =================================================================

    IEnumerator ReelToTower()
    {
        // Target: just to the left of the tower so it's visually "at" the wall
        Vector3 target = new Vector3(towerPos.x - 1.1f, transform.position.y, 0f);
        Vector3 startPos = transform.position;

        // Freeze autonomous movement during reel
        float reelDur = 0.55f;
        float elapsed = 0f;

        // Shrink the log & spin it as it's dragged
        while (elapsed < reelDur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / reelDur);
            // Ease-in-out
            float p = t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

            transform.position = Vector3.Lerp(startPos, target, p);

            // Spin log faster as it approaches
            if (logRoot != null)
                logRoot.localRotation = Quaternion.Euler(0, 0, p * 720f);

            // Fish flips to face right (toward tower) while being reeled
            var fishBody = transform.Find("FishBody");
            if (fishBody != null)
                fishBody.localScale = new Vector3(1.8f, 1.8f, 1f);   // face right

            yield return null;
        }

        transform.position = target;

        // Clean up the line
        DestroyReelLine();

        // Now trigger the actual outcome
        ApplySpearOutcome();
    }

    // =================================================================
    // Apply outcome once fish is next to tower
    // =================================================================

    void ApplySpearOutcome()
    {
        if (isScam)
        {
            // Red flag → defuse: happy sprite, swim into pool
            state = State.Defused;
            currentSpeed = baseSpeed * 3.0f;
            hitRegistered = false;     // let CheckReachedTower fire naturally
            if (fishHappySprite != null) fishSR.sprite = fishHappySprite;

            manager.OnRedFishSpearHit(transform.position);

            manager.commentator?.SayRandom(new[] {
                "Defused!", "It's safe now!", "Good eye, dear!"
            });
        }
        else
        {
            // Green flag → enraged: puff up and damage tower
            state = State.Enraged;
            currentSpeed = 0f;         // already at tower — explode immediately
            if (fishPuffedSprite != null) fishSR.sprite = fishPuffedSprite;

            StartCoroutine(PuffUpAnim());
            manager.OnGreenFishShotEarly(transform.position);

            manager.commentator?.SayRandom(new[] {
                "Oh no, you made a safe one angry!",
                "Don't shoot the strong passwords!",
                "Watch out — it's charging!"
            });

            // Trigger explosion right here since it's already at the tower
            StartCoroutine(PufferfishExplode());
        }
    }

    // =================================================================
    // Reel line helpers
    // =================================================================

    void SpawnReelLine()
    {
        reelLineGO = new GameObject("ReelLine");
        reelLineGO.transform.SetParent(transform, false);   // child of fish so it moves with it
        reelLineSR = reelLineGO.AddComponent<SpriteRenderer>();
        reelLineSR.color = new Color(0.95f, 0.85f, 0.30f, 0.88f);
        reelLineSR.sortingOrder = 10;

        // Use a white pixel sprite as the line texture
        reelLineSR.sprite = MakePixelSprite();
    }

    void UpdateReelLine()
    {
        if (reelLineGO == null || reelLineSR == null) return;
        if (speargunPivot == null) { DestroyReelLine(); return; }

        Vector3 from = speargunPivot.position;
        Vector3 to = transform.position;
        Vector3 mid = (from + to) * 0.5f;
        Vector3 delta = to - from;
        float length = delta.magnitude;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

        // Position line at midpoint in world space (we parented to fish, so un-parent first)
        reelLineGO.transform.SetParent(null, true);
        reelLineGO.transform.position = mid;
        reelLineGO.transform.rotation = Quaternion.Euler(0, 0, angle);
        reelLineGO.transform.localScale = new Vector3(length, 0.06f, 1f);
    }

    void DestroyReelLine()
    {
        if (reelLineGO != null) Destroy(reelLineGO);
        reelLineGO = null; reelLineSR = null;
    }

    // =================================================================
    // Visual helpers
    // =================================================================

    IEnumerator HitFlash()
    {
        Color orig = fishSR != null ? fishSR.color : Color.white;
        for (int i = 0; i < 3; i++)
        {
            if (fishSR != null) fishSR.color = Color.white;
            yield return new WaitForSeconds(0.045f);
            if (fishSR != null) fishSR.color = orig;
            yield return new WaitForSeconds(0.045f);
        }
    }

    IEnumerator PuffUpAnim()
    {
        Vector3 start = transform.localScale;
        Vector3 end = start * 1.5f;
        float t = 0f, dur = 0.15f;
        while (t < dur)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(start, end, t / dur);
            yield return null;
        }
        transform.localScale = end;
    }

    // =================================================================
    // Natural arrival at tower (not shot)
    // =================================================================

    void OnReachedTower()
    {
        if (state == State.Defused || (!isScam && state == State.Swimming))
        {
            // Naturally safe, or defused red flag -> collect in pool
            state = State.Collected;
            if (logRoot != null) logRoot.gameObject.SetActive(false);

            manager.OnGreenFishCollected(gameObject, fishSR.sprite, state == State.Defused);
            manager.OnFishRemoved();
            Destroy(gameObject);
        }
        else if (state == State.Enraged || (isScam && state == State.Swimming))
        {
            // Unshot red flag, or enraged safe flag -> explodes & damages tower
            state = State.Exploding;
            StartCoroutine(PufferfishExplode());
        }
    }

    IEnumerator PufferfishExplode()
    {
        if (fishPuffedSprite != null) fishSR.sprite = fishPuffedSprite;
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

        manager.OnRedFishReachedTower(transform.position, false);
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

    // =================================================================
    // Tiny white pixel sprite (for the reel line)
    // =================================================================

    static Sprite _pixSprite;
    static Sprite MakePixelSprite()
    {
        if (_pixSprite != null) return _pixSprite;
        var tex = new Texture2D(4, 4);
        var px = new Color[16];
        for (int i = 0; i < 16; i++) px[i] = Color.white;
        tex.SetPixels(px); tex.Apply();
        _pixSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        return _pixSprite;
    }
}