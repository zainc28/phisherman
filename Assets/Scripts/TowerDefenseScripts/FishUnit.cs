using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One fish+log unit.
/// CHANGES:
///  - Log scale reduced to 60% of original (was 1.6 x 0.85, now 0.96 x 0.51)
///  - laneIdx tracked so manager can free lane on death
///  - Commentator removed
/// </summary>
public class FishUnit : MonoBehaviour
{
    private Sprite fishNormalSprite;
    private Sprite fishHappySprite;
    private Sprite fishPuffedSprite;
    [HideInInspector] public Sprite logSprite;

    private SpriteRenderer fishSR;
    private SpriteRenderer logSR;
    private SpriteRenderer reelLineSR;
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
    private int laneIndex = -1;

    private Transform speargunPivot;
    private GameObject reelLineGO;

    // =================================================================
    //  Init
    // =================================================================

    public void Init(bool scam, string label, float speed, int laneIdx,
                     TowerDefenseManager mgr, Vector3 twrPos,
                     Sprite fNormal, Sprite fHappy, Sprite fPuffed, Sprite lSpr,
                     Color fishCol)
    {
        isScam = scam;
        labelText = label;
        baseSpeed = speed;
        currentSpeed = speed;
        manager = mgr;
        towerPos = twrPos;
        laneIndex = laneIdx;

        fishNormalSprite = fNormal;
        fishHappySprite = fHappy;
        fishPuffedSprite = fPuffed;
        logSprite = lSpr;
        fishColor = fishCol;
        bobOffset = Random.Range(0f, Mathf.PI * 2f);

        BuildVisuals();
    }

    // =================================================================
    //  Build child visuals
    // =================================================================

    void BuildVisuals()
    {
        // Invisible fish body renderer — state-change code references it without null-ref
        var fishGO = new GameObject("FishBody");
        fishGO.transform.SetParent(transform, false);
        fishGO.transform.localPosition = Vector3.zero;
        fishSR = fishGO.AddComponent<SpriteRenderer>();
        fishSR.sprite = null;
        fishSR.sortingOrder = -99;
        fishGO.transform.localScale = Vector3.one * 0.01f;

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
        // Log: 0.96 x 0.51 +20% (Task 4C) = 1.152 x 0.612
        logGO.transform.localScale = new Vector3(1.152f, 0.612f, 1f);

        var canvasGO = new GameObject("LabelCanvas");
        canvasGO.transform.SetParent(logRoot, false);
        canvasGO.transform.localPosition = new Vector3(0f, 0.02f, -0.1f);
        const float k = 0.006f;
        canvasGO.transform.localScale = new Vector3(k, k, 1f);
        var c = canvasGO.AddComponent<Canvas>(); c.renderMode = RenderMode.WorldSpace; c.sortingOrder = 6;
        canvasGO.GetComponent<RectTransform>().sizeDelta = new Vector2(260, 70);

        var bg = new GameObject("LabelBg"); bg.transform.SetParent(canvasGO.transform, false);
        var bgImg = bg.AddComponent<Image>(); bgImg.color = new Color(0.10f, 0.10f, 0.18f, 0.88f); bgImg.raycastTarget = false;
        var bgRT = bg.GetComponent<RectTransform>(); bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one; bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;

        var textGO = new GameObject("Label"); textGO.transform.SetParent(canvasGO.transform, false);
        labelTMP = textGO.AddComponent<TextMeshProUGUI>();
        labelTMP.text = labelText;
        labelTMP.fontSize = 18;
        labelTMP.color = Color.white;
        labelTMP.fontStyle = FontStyles.Bold;
        labelTMP.alignment = TextAlignmentOptions.Center;
        labelTMP.raycastTarget = false;
        var tRT = textGO.GetComponent<RectTransform>(); tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one; tRT.offsetMin = new Vector2(4, 0); tRT.offsetMax = new Vector2(-4, 0);
    }

    // =================================================================
    //  Update
    // =================================================================

    void Update()
    {
        switch (state)
        {
            case State.Swimming:
            case State.Defused:
                TickSwimming(); break;
            case State.Enraged:
                TickEnraged(); break;
            case State.ReelingIn:
                UpdateReelLine(); break;
        }
    }

    void TickSwimming()
    {
        bobTimer += Time.deltaTime;
        transform.position += Vector3.right * currentSpeed * Time.deltaTime;
        transform.position += new Vector3(0, Mathf.Sin(bobTimer * 1.8f + bobOffset) * 0.004f, 0);
        if (logRoot != null) logRoot.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(bobTimer * 2.2f + bobOffset) * 3f);
        CheckReachedTower();
    }

    void TickEnraged()
    {
        bobTimer += Time.deltaTime;
        transform.position += Vector3.right * currentSpeed * Time.deltaTime;
        transform.position += new Vector3(0, Mathf.Sin(bobTimer * 14f) * 0.012f, 0);
        if (logRoot != null) logRoot.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(bobTimer * 14f) * 8f);
        CheckReachedTower();
    }

    void CheckReachedTower()
    {
        if (hitRegistered) return;
        if (transform.position.x >= towerPos.x - 0.8f) { hitRegistered = true; OnReachedTower(); }
    }

    // =================================================================
    //  Spear hit
    // =================================================================

    public void TakeSpearHit(Transform pivot)
    {
        if (isDead || state == State.Enraged || state == State.Exploding ||
            state == State.Collected || state == State.Dead || state == State.Defused ||
            state == State.ReelingIn)
            return;

        speargunPivot = pivot;
        state = State.ReelingIn;
        hitRegistered = true;
        StartCoroutine(HitFlash());
        SpawnReelLine();
        StartCoroutine(ReelToTower());
    }

    public void TakeSpearHit() { TakeSpearHit(null); }

    // =================================================================
    //  Reel-in
    // =================================================================

    IEnumerator ReelToTower()
    {
        Vector3 target = new Vector3(towerPos.x - 1.1f, transform.position.y, 0f);
        Vector3 startPos = transform.position;
        float reelDur = 0.55f, elapsed = 0f;

        while (elapsed < reelDur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / reelDur);
            float p = t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
            transform.position = Vector3.Lerp(startPos, target, p);
            if (logRoot != null) logRoot.localRotation = Quaternion.Euler(0, 0, p * 720f);
            yield return null;
        }

        transform.position = target;
        DestroyReelLine();
        ApplySpearOutcome();
    }

    void ApplySpearOutcome()
    {
        if (isScam)
        {
            state = State.Defused; currentSpeed = baseSpeed * 3.0f; hitRegistered = false;
            if (fishHappySprite != null) fishSR.sprite = fishHappySprite;
            manager.OnRedFishSpearHit(transform.position);
        }
        else
        {
            state = State.Enraged; currentSpeed = baseSpeed * 2.2f; hitRegistered = false;
            if (fishPuffedSprite != null) fishSR.sprite = fishPuffedSprite;
            StartCoroutine(PuffUpAnim());
            manager.OnGreenFishShotEarly(transform.position);
        }
    }

    // =================================================================
    //  Reel line helpers
    // =================================================================

    void SpawnReelLine()
    {
        reelLineGO = new GameObject("ReelLine");
        reelLineGO.transform.SetParent(transform, false);
        reelLineSR = reelLineGO.AddComponent<SpriteRenderer>();
        reelLineSR.color = new Color(0.95f, 0.85f, 0.30f, 0.88f);
        reelLineSR.sortingOrder = 10;
        reelLineSR.sprite = MakePixelSprite();
    }

    void UpdateReelLine()
    {
        if (reelLineGO == null || reelLineSR == null) return;
        if (speargunPivot == null) { DestroyReelLine(); return; }
        Vector3 from = speargunPivot.position, to = transform.position;
        Vector3 mid = (from + to) * 0.5f; Vector3 delta = to - from;
        float length = delta.magnitude, angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
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
    //  Visual helpers
    // =================================================================

    IEnumerator HitFlash()
    {
        Color orig = fishSR != null ? fishSR.color : Color.white;
        for (int i = 0; i < 3; i++)
        { if (fishSR != null) fishSR.color = Color.white; yield return new WaitForSeconds(0.045f); if (fishSR != null) fishSR.color = orig; yield return new WaitForSeconds(0.045f); }
    }

    IEnumerator PuffUpAnim()
    {
        Vector3 start = transform.localScale, end = start * 1.5f; float t = 0f, dur = 0.15f;
        while (t < dur) { t += Time.deltaTime; transform.localScale = Vector3.Lerp(start, end, t / dur); yield return null; }
        transform.localScale = end;
    }

    // =================================================================
    //  Natural arrival at tower
    // =================================================================

    void OnReachedTower()
    {
        if (state == State.Defused || (!isScam && state == State.Swimming))
        {
            state = State.Collected;
            if (logRoot != null) logRoot.gameObject.SetActive(false);
            manager.OnGreenFishCollected(gameObject, fishSR.sprite, state == State.Defused);
            manager.OnFishRemoved(laneIndex);
            Destroy(gameObject);
        }
        else if (state == State.Enraged || (isScam && state == State.Swimming))
        {
            state = State.Exploding;
            StartCoroutine(PufferfishExplode());
        }
    }

    IEnumerator PufferfishExplode()
    {
        if (fishPuffedSprite != null) fishSR.sprite = fishPuffedSprite;
        fishSR.color = new Color(1f, 0.45f, 0.1f);
        if (logRoot != null) logRoot.gameObject.SetActive(false);

        float dur = 0.38f, t = 0f; Vector3 baseScale = transform.localScale;
        while (t < dur)
        { t += Time.deltaTime; float p = Mathf.Clamp01(t / dur); transform.localScale = baseScale * (1f + p * 1.2f); fishSR.color = Color.Lerp(new Color(1f, 0.45f, 0.1f), Color.red, p); yield return null; }
        fishSR.color = Color.white; transform.localScale = baseScale * 2.5f;
        yield return new WaitForSeconds(0.05f);

        manager.OnRedFishReachedTower(transform.position, false);
        manager.OnFishRemoved(laneIndex);

        t = 0f; dur = 0.22f;
        while (t < dur) { t += Time.deltaTime; float p = Mathf.Clamp01(t / dur); transform.localScale = baseScale * (2.5f * (1f - p)); yield return null; }
        Destroy(gameObject);
    }

    // =================================================================
    //  Pixel sprite
    // =================================================================

    static Sprite _pixSprite;
    static Sprite MakePixelSprite()
    {
        if (_pixSprite != null) return _pixSprite;
        var tex = new Texture2D(4, 4); var px = new Color[16];
        for (int i = 0; i < 16; i++) px[i] = Color.white;
        tex.SetPixels(px); tex.Apply();
        _pixSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        return _pixSprite;
    }
}