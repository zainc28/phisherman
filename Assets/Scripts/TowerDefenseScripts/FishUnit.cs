using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One fish+log unit. Fish hangs below; log floats above (arms reach up).
/// Password text displays ON the log.
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
    private TextMeshProUGUI labelTMP;
    private Transform logRoot;

    public enum State { Swimming, Enraged, Defused, Exploding, Collected, Dead }
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

    public void TakeSpearHit()
    {
        if (isDead || state == State.Enraged || state == State.Exploding ||
            state == State.Collected || state == State.Dead || state == State.Defused)
            return;

        if (isScam)
        {
            // Red flag hit: Becomes Defused (Happy), moves FASTER to get off screen
            manager.OnRedFishSpearHit(transform.position);
            state = State.Defused;
            currentSpeed = baseSpeed * 3.5f; // Zip away to the tower!
            if (fishHappySprite != null) fishSR.sprite = fishHappySprite;

            manager.commentator?.SayRandom(new[] {
                "Defused!",
                "It's safe now!",
                "Good eye, dear!"
            });
        }
        else
        {
            // Green flag hit: Becomes Enraged (Puffed up), charges fast
            manager.OnGreenFishShotEarly(transform.position);
            state = State.Enraged;
            currentSpeed = baseSpeed * 2.5f;
            if (fishPuffedSprite != null) fishSR.sprite = fishPuffedSprite;

            // Instantly play a puff-up scale animation
            StartCoroutine(PuffUpAnim());

            manager.commentator?.SayRandom(new[] {
                "Oh no, you made a safe one angry!",
                "Don't shoot the strong passwords!",
                "Watch out — it's charging!"
            });
        }
    }

    IEnumerator PuffUpAnim()
    {
        Vector3 start = transform.localScale;
        Vector3 end = start * 1.5f; // Visibly pop up by 50%
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
    // Reached tower
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
}