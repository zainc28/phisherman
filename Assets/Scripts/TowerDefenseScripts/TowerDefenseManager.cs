using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Phish Patrol — Tower Defense.
///
/// CHANGES vs original:
///  - fishPoolSprites[] exposed so builder can wire 20 random fish
///  - Fish spawn on fixed Y lanes (no overlap, labels always readable)
///  - Hanging fish flipped to face LEFT (toward tower)
///  - Net container used instead of blue pool
///  - Waves progressively accelerate every 15 seconds
///  - 60-second survival timer (surviveDuration = 60)
///  - heart + hourglass sprite fields for MinigameLivesHUD / TimerHUD
/// </summary>
public class TowerDefenseManager : MonoBehaviour
{
    [Header("Game Settings")]
    public int startingHealth = 3;
    public float spearSpeed = 22f;

    [Header("Win Condition")]
    [Tooltip("Survive this many seconds with the tower still standing.")]
    public float surviveDuration = 60f;

    [Header("Sprites — UI")]
    [Tooltip("heart sprite for the lives HUD")]
    public Sprite heartSprite;
    [Tooltip("hourglass sprite for the timer HUD")]
    public Sprite hourglassSprite;

    // Single neutral tint — player must read the label
    private static readonly Color NeutralFishColor = new Color(0.55f, 0.82f, 0.95f);

    [Header("Audio")]
    public AudioClip bgmClip;
    public AudioClip spearClip;
    private AudioSource bgmSource;
    private AudioSource sfxSource;

    [Header("Sprites — auto-wired by builder")]
    public Sprite fishNormalSprite;
    public Sprite fishHappySprite;
    public Sprite fishPuffedSprite;
    public Sprite logSprite;
    public Sprite towerSprite;
    public Sprite phishermanSprite;
    public Sprite speargunSprite;
    public Sprite whiteSprite;
    [Tooltip("td_hanging_fish — hangs from each log banner.")]
    public Sprite hangingFishSprite;

    // CHANGED: pool of 20 fish sprites for net fish visuals
    [Tooltip("fish_1 through fish_20 sprites in order.")]
    public Sprite[] fishPoolSprites;

    [Header("Scene References")]
    public Transform spawnPoint;
    public Transform towerRoot;
    public Transform towerShakeRoot;
    public Transform crackContainer;
    public Transform towerWaterContainer;   // now the net fish container
    public Transform speargunPivot;
    public Transform spearSpawnPoint;

    [Header("HUD")]
    public Transform heartsContainer;
    public TMP_Text scoreText;
    public TMP_Text waveText;
    public TMP_Text comboText;

    [Header("Panels")]
    public GameObject tutorialPanel;
    public GameObject hudPanel;
    public GameObject gameOverPanel;
    public GameObject winPanel;

    [Header("Tutorial UI")]
    public TMP_Text tutorialTitleText;
    public TMP_Text tutorialBodyText;
    public TMP_Text tutorialStepText;
    public Button tutorialNextButton;
    public TMP_Text tutorialNextButtonText;

    [Header("Result UI")]
    public TMP_Text gameOverScoreText;
    public TMP_Text gameOverMessageText;
    public TMP_Text winScoreText;
    public TMP_Text winStarsText;

    [Header("Commentator")]
    public Commentator commentator;

    // ── Internal ──
    private int score, health, combo, tutStep;
    private bool gameActive, spawning;
    private List<FishUnit> liveFish = new List<FishUnit>();
    private MinigameLivesHUD livesHUD;
    private MinigameTimerHUD timerHUD;
    private float surviveTimeRemaining;
    private bool gameEnded;

    // Fixed Y lanes — prevents fish label overlap
    private const int LaneCount = 7;
    private const float LaneSpread = 1.6f;
    private bool[] laneOccupied = new bool[LaneCount];

    // Net fish that swim in world space around the tower's built-in net
    private struct NetFishState
    {
        public SpriteRenderer sr;
        public Vector3 center;
        public float phaseX, phaseY, freqX, freqY, ampX, ampY;
    }
    private List<NetFishState> netFishStates = new List<NetFishState>();

    // ── Wave / password data ──
    struct ED { public string label; public bool isScam; public ED(string l, bool s) { label = l; isScam = s; } }

    ED[] scams = {
        new ED("password123",true), new ED("123456",  true), new ED("qwerty",   true),
        new ED("iloveyou",  true),  new ED("abc123",  true), new ED("password1",true),
        new ED("111111",    true),  new ED("letmein", true), new ED("welcome",  true),
        new ED("monkey",    true),  new ED("dragon",  true), new ED("master",   true),
        new ED("hello",     true),  new ED("login",   true), new ED("admin",    true),
        new ED("baseball",  true),  new ED("shadow",  true), new ED("trustno1", true),
        new ED("12345678",  true),  new ED("princess",true),
    };

    ED[] safes = {
        new ED("K#9mP!2xL",    false), new ED("Blue$Tree47!",  false),
        new ED("Xq8@nW3!vY",   false), new ED("Maple!Leaf99#", false),
        new ED("T7@kLz!9Rp",   false), new ED("Sun$Rise2024!", false),
        new ED("Wr9#mK!6Lp",   false), new ED("Cat!Rain$42X",  false),
        new ED("Gr@pe!Vine88", false), new ED("Z3br@Dance#7",  false),
        new ED("Moon&Star99#", false), new ED("P@rrot3!Wing",  false),
        new ED("B3eHoney$44!", false), new ED("Night#Sky25!",  false),
    };

    // Continuous spawn — no discrete wave phases. Speed/interval scale smoothly with time.
    private const float BaseSpeed = 0.38f;
    private const float BaseInterval = 3.0f;
    private const float ScamRatio = 0.55f;

    string[] tutTitles = { "Phish Patrol -- Tower Defense", "How to play" };
    string[] tutBodies = {
        "Fish carrying log banners are swimming toward your tower!\n\nClick near a fish to shoot it with the tower's speargun.",
        "Read the password on each fish's log banner!\n\nWEAK password fish (like '123456') — SHOOT THEM!\n" +
        "STRONG password fish (symbols + numbers) — LET THEM reach the tower!\n\n" +
        "Survive 60 seconds to win. Fish get faster over time!"
    };

    // =================================================================
    // Lifecycle
    // =================================================================

    void Start()
    {
        tutorialPanel.SetActive(true);
        hudPanel.SetActive(false);
        gameOverPanel.SetActive(false);
        winPanel.SetActive(false);

        bgmSource = gameObject.AddComponent<AudioSource>();
        if (bgmClip != null) { bgmSource.clip = bgmClip; bgmSource.loop = true; bgmSource.volume = 0.5f; bgmSource.Play(); }
        sfxSource = gameObject.AddComponent<AudioSource>();
        ShowTutStep(0);
    }

    void Update()
    {
        if (!gameActive) return;

        // Countdown
        surviveTimeRemaining -= Time.deltaTime;
        timerHUD?.SetTime(surviveTimeRemaining);

        if (surviveTimeRemaining <= 0f && !gameEnded) WinGame();

        // Speargun pivot tracks mouse (pivot is on the tower now)
        if (speargunPivot != null)
        {
            Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mouseWorld.z = 0;
            Vector3 dir = mouseWorld - speargunPivot.position;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            speargunPivot.rotation = Quaternion.Euler(0, 0, angle);
        }

        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mw = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mw.z = 0;
            TryHitNearestFish(mw);
        }

        AnimateNetFish();
        liveFish.RemoveAll(f => f == null);
    }

    // =================================================================
    // Tutorial
    // =================================================================

    void ShowTutStep(int step)
    {
        if (tutorialTitleText != null) tutorialTitleText.text = tutTitles[step];
        if (tutorialBodyText != null) tutorialBodyText.text = tutBodies[step];
        if (tutorialStepText != null) tutorialStepText.text = (step + 1) + " of " + tutTitles.Length;
        if (tutorialNextButtonText != null)
            tutorialNextButtonText.text = (step == tutTitles.Length - 1) ? "Start!" : "Next";
    }

    public void OnTutorialNext()
    {
        tutStep++;
        if (tutStep >= tutTitles.Length) { tutorialPanel.SetActive(false); StartGame(); }
        else ShowTutStep(tutStep);
    }

    // =================================================================
    // Game start
    // =================================================================

    void StartGame()
    {
        score = 0; health = startingHealth; combo = 0; gameActive = true; gameEnded = false;
        surviveTimeRemaining = surviveDuration;
        hudPanel.SetActive(true);

        if (towerRoot != null) towerRoot.localScale = towerRoot.localScale * 0.5f;

        if (heartsContainer != null) heartsContainer.gameObject.SetActive(false);
        livesHUD = gameObject.AddComponent<MinigameLivesHUD>();
        livesHUD.Initialize(startingHealth, heartSprite);

        timerHUD = gameObject.AddComponent<MinigameTimerHUD>();
        timerHUD.Initialize(hourglassSprite);
        timerHUD.SetTime(surviveTimeRemaining);

        UpdateHUD();
        StartCoroutine(ContinuousSpawn());
        commentator?.Say("Read the label — shoot the weak passwords, let the strong ones reach the tower!");
    }

    // Continuous spawn — one fish at a time, interval and speed both shrink over time
    IEnumerator ContinuousSpawn()
    {
        yield return new WaitForSeconds(1.5f);
        while (gameActive && !gameEnded)
        {
            float elapsed = surviveDuration - surviveTimeRemaining;
            // Smoothly ramp: at t=0 → BaseSpeed/BaseInterval; at t=60 → 2.5× faster
            float ramp = 1f + (elapsed / surviveDuration) * 1.5f;
            float speed = BaseSpeed * ramp;
            float interval = BaseInterval / ramp;
            interval = Mathf.Max(interval, 0.8f);  // floor so it doesn't go insane

            int lane = GetFreeLane();
            laneOccupied[lane] = true;
            float laneY = TowerY + Mathf.Lerp(-LaneSpread * 0.5f, LaneSpread * 0.5f,
                          (float)lane / (LaneCount - 1));

            bool isScam = Random.value < ScamRatio;
            ED data = isScam
                ? scams[Random.Range(0, scams.Length)]
                : safes[Random.Range(0, safes.Length)];

            SpawnFish(data, speed, laneY, lane);
            yield return new WaitForSeconds(interval);
        }
    }

    // =================================================================
    // Interaction
    // =================================================================

    void TryHitNearestFish(Vector3 clickWorld, float maxRadius = 1.4f)
    {
        FishUnit nearest = null; float bestDist = maxRadius;
        for (int i = liveFish.Count - 1; i >= 0; i--)
        {
            var f = liveFish[i]; if (f == null) continue;
            float d = Vector3.Distance(f.transform.position, clickWorld);
            if (d < bestDist) { bestDist = d; nearest = f; }
        }
        if (nearest == null) return;

        if (sfxSource != null && spearClip != null) sfxSource.PlayOneShot(spearClip);
        if (spearSpawnPoint != null) StartCoroutine(BoltFlash(spearSpawnPoint.position, nearest.transform.position));
        nearest.TakeSpearHit(speargunPivot);
    }

    IEnumerator BoltFlash(Vector3 from, Vector3 to)
    {
        var go = new GameObject("SpearBolt");
        go.transform.position = (from + to) * 0.5f;
        Vector3 delta = to - from; float len = delta.magnitude;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        go.transform.rotation = Quaternion.Euler(0, 0, angle);
        go.transform.localScale = new Vector3(len, 0.08f, 1f);
        var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = WhitePix;
        sr.color = new Color(0.95f, 0.85f, 0.30f, 1f); sr.sortingOrder = 12;
        float t = 0f;
        while (t < 0.10f) { t += Time.deltaTime; if (go == null) yield break; sr.color = new Color(0.95f, 0.85f, 0.30f, 1f - t / 0.10f); yield return null; }
        if (go != null) Destroy(go);
    }

    // =================================================================
    // Fish callbacks
    // =================================================================

    public void OnRedFishSpearHit(Vector3 pos)
    {
        combo++; int pts = combo >= 3 ? 15 : 10; score += pts;
        SpawnPopup("+" + pts + (combo >= 3 ? " COMBO!" : ""), pos + Vector3.up,
            combo >= 3 ? new Color(1f, 0.85f, 0.1f) : new Color(0.3f, 1f, 0.5f));
        UpdateHUD();
        PlayerProgress.RegisterFish("fish_hanging");
        if (combo == 3) commentator?.Say("Three in a row — wonderful!");
        else if (combo == 6) commentator?.Say("Oh my, you are unstoppable!");
        else commentator?.SayRandom(new[] { "Got one!", "Nice shot, dear.", "That's the spirit!" });
    }

    public void OnRedFishReachedTower(Vector3 pos, bool wasShot = false)
    {
        combo = 0;
        SpawnPopup("BAD FISH!", pos + Vector3.up, new Color(1f, 0.3f, 0.3f));
        TakeDamage();
        if (health > 0) commentator?.SayRandom(new[] { "A fish cracked the tower!", "Keep them back!" });
        UpdateHUD();
    }

    public void OnGreenFishCollected(GameObject fishGO, Sprite fishSpr, bool isDefused = false)
    {
        // CHANGED: spawn a net fish so the caught fish actually swims around in the tower's net
        SpawnNetFish(fishSpr);
        PlayerProgress.RegisterFish(PlayerProgress.GetRandomNetFishId());
        if (!isDefused) { score += 5; combo++; commentator?.SayRandom(new[] { "A strong password joined the tower!", "Safe and sound!" }); }
        else { SpawnPopup("DEFUSED!", fishGO.transform.position + Vector3.up, new Color(0.3f, 1f, 0.3f)); commentator?.SayRandom(new[] { "The defused fish is swimming happily!", "Look at it go!" }); }
        UpdateHUD();
    }

    public void OnGreenFishShotEarly(Vector3 pos)
    {
        combo = 0; score = Mathf.Max(0, score - 5);
        SpawnPopup("-5 safe fish!", pos + Vector3.up * 2, new Color(0.9f, 0.4f, 0.1f));
        UpdateHUD();
    }

    public void OnFishRemoved(int laneIdx)
    {
        if (laneIdx >= 0 && laneIdx < LaneCount) laneOccupied[laneIdx] = false;
    }

    // =================================================================
    // Spawning — CHANGED: fixed Y lanes for no overlap, speed multiplied
    // =================================================================

    // TowerY constant for lane calculation
    private const float TowerY = -0.3f;

    int GetFreeLane()
    {
        for (int i = 0; i < LaneCount; i++)
            if (!laneOccupied[i]) return i;
        return Random.Range(0, LaneCount);
    }

    void SpawnFish(ED data, float speed, float spawnY, int laneIdx)
    {
        var go = new GameObject("FishUnit");
        go.transform.position = new Vector3(spawnPoint.position.x, spawnY, 0f);

        var col = go.AddComponent<CircleCollider2D>(); col.isTrigger = true; col.radius = 0.45f;
        var rb = go.AddComponent<Rigidbody2D>(); rb.gravityScale = 0;

        var fish = go.AddComponent<FishUnit>();
        fish.Init(data.isScam, data.label, speed, laneIdx,
                  this,
                  towerRoot != null ? towerRoot.position : new Vector3(5f, 0, 0),
                  fishNormalSprite, fishHappySprite, fishPuffedSprite, logSprite,
                  NeutralFishColor);

        // td_hanging_fish hangs from the log, facing right — CHANGED from facing left
        if (hangingFishSprite != null)
        {
            var hfGo = new GameObject("HangingFish");
            hfGo.transform.SetParent(go.transform, false);
            hfGo.transform.localPosition = new Vector3(0f, 0.30f, 0.01f);
            hfGo.transform.localScale = new Vector3(0.40f, 0.40f, 1f);
            var hfSR = hfGo.AddComponent<SpriteRenderer>();
            hfSR.sprite = hangingFishSprite; hfSR.color = Color.white; hfSR.sortingOrder = 5;
        }

        liveFish.Add(fish);
        PlayerProgress.RegisterFish("fish_hanging");
    }

    // =================================================================
    // Net fish — swim in world space around the tower's built-in net
    // towerWaterContainer is set to the tower root so we offset relative to it
    // =================================================================

    void SpawnNetFish(Sprite spr)
    {
        // Spawn a small SpriteRenderer world-space fish near the tower net area
        // Net area is roughly at towerRoot.position + (0, -2.0, 0)
        var go = new GameObject("NetFish");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = spr != null ? spr : fishNormalSprite;
        sr.color = NeutralFishColor;
        sr.sortingOrder = 5;
        go.transform.localScale = Vector3.one * 0.35f;

        // Place near the net (bottom of tower)
        Vector3 netCenter = towerRoot != null
            ? towerRoot.position + new Vector3(0f, -2.2f, -0.3f)
            : new Vector3(6.2f, TowerY - 2.2f, -0.3f);
        go.transform.position = netCenter + new Vector3(
            Random.Range(-0.6f, 0.6f), Random.Range(-0.3f, 0.3f), 0f);

        float facing = Random.value > 0.5f ? 1f : -1f;
        go.transform.localScale = new Vector3(facing * 0.35f, 0.35f, 1f);

        netFishStates.Add(new NetFishState
        {
            sr = sr,
            center = go.transform.position,
            phaseX = Random.Range(0f, Mathf.PI * 2f),
            phaseY = Random.Range(0f, Mathf.PI * 2f),
            freqX = Random.Range(0.35f, 0.75f),
            freqY = Random.Range(0.60f, 1.20f),
            ampX = Random.Range(0.30f, 0.65f),
            ampY = Random.Range(0.08f, 0.20f),
        });
    }

    void AnimateNetFish()
    {
        float time = Time.time;
        for (int i = netFishStates.Count - 1; i >= 0; i--)
        {
            var s = netFishStates[i];
            if (s.sr == null) { netFishStates.RemoveAt(i); continue; }
            float x = s.center.x + Mathf.Sin(time * s.freqX + s.phaseX) * s.ampX;
            float y = s.center.y + Mathf.Sin(time * s.freqY + s.phaseY) * s.ampY;
            s.sr.transform.position = new Vector3(x, y, s.center.z);
            float dx = Mathf.Cos(time * s.freqX + s.phaseX);
            if (Mathf.Abs(dx) > 0.05f)
            {
                var sc = s.sr.transform.localScale;
                s.sr.transform.localScale = new Vector3(dx > 0 ? Mathf.Abs(sc.x) : -Mathf.Abs(sc.x), sc.y, sc.z);
            }
            netFishStates[i] = s;
        }
    }

    // =================================================================
    // Tower damage
    // =================================================================

    void TakeDamage()
    {
        health = Mathf.Max(0, health - 1);
        livesHUD?.LoseLife();
        SpawnCrack();
        StartCoroutine(ShakeTower(0.20f, 0.35f));
        if (health <= 0) StartCoroutine(BreakSequence());
    }

    IEnumerator ShakeTower(float intensity, float dur)
    {
        if (towerShakeRoot == null) yield break;
        Vector3 orig = towerShakeRoot.localPosition; float t = 0f;
        while (t < dur) { t += Time.deltaTime; float d = 1f - Mathf.Clamp01(t / dur); towerShakeRoot.localPosition = orig + new Vector3(Random.Range(-intensity, intensity) * d, Random.Range(-intensity * 0.5f, intensity * 0.5f) * d, 0); yield return null; }
        towerShakeRoot.localPosition = orig;
    }

    void SpawnCrack()
    {
        if (crackContainer == null) return;
        var crack = new GameObject("Crack");
        crack.transform.SetParent(crackContainer, false);
        crack.transform.localPosition = new Vector3(Random.Range(-0.8f, 0.8f), Random.Range(-1.0f, 1.0f), -0.05f);
        CrackLine(crack.transform, Random.Range(0.4f, 0.9f), 0.04f, Random.Range(-35f, 35f));
        CrackLine(crack.transform, Random.Range(0.25f, 0.55f), 0.03f, Random.Range(55f, 125f));
    }

    void CrackLine(Transform p, float length, float width, float angle)
    {
        var line = new GameObject("CrackLine");
        line.transform.SetParent(p, false);
        line.transform.localRotation = Quaternion.Euler(0, 0, angle);
        line.transform.localScale = new Vector3(length, width, 1);
        var sr = line.AddComponent<SpriteRenderer>();
        sr.sprite = WhitePix; sr.color = new Color(1f, 1f, 1f, 0.80f); sr.sortingOrder = 8;
    }

    IEnumerator BreakSequence()
    {
        gameActive = false;
        for (int i = 0; i < 10; i++) { SpawnCrack(); yield return new WaitForSeconds(0.04f); }
        yield return StartCoroutine(ShakeTower(0.40f, 0.7f));
        yield return new WaitForSeconds(0.3f);
        GameOver();
    }

    // =================================================================
    // =================================================================
    // HUD
    // =================================================================

    void UpdateHUD()
    {
        if (scoreText != null) scoreText.text = "Score: " + score;
        if (waveText != null) waveText.text = "";   // no wave phases — just the timer
        if (comboText != null) comboText.text = combo >= 3 ? combo + "x combo!" : "";
    }

    // =================================================================
    // Popups
    // =================================================================

    void SpawnPopup(string text, Vector3 pos, Color col)
    {
        var go = new GameObject("Popup");
        go.transform.position = pos;
        var c = go.AddComponent<Canvas>(); c.renderMode = RenderMode.WorldSpace; c.sortingOrder = 20;
        go.transform.localScale = new Vector3(0.013f, 0.013f, 1f);
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(260, 60);
        go.AddComponent<CanvasGroup>();
        var tgo = new GameObject("T"); tgo.transform.SetParent(go.transform, false);
        var tmp = tgo.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = 22; tmp.color = col;
        tmp.fontStyle = FontStyles.Bold; tmp.alignment = TextAlignmentOptions.Center;
        var rt = tgo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        StartCoroutine(PopupAnim(go));
    }

    IEnumerator PopupAnim(GameObject go)
    {
        Vector3 start = go.transform.position; var cg = go.GetComponent<CanvasGroup>(); float t = 0f;
        while (t < 1f) { t += Time.deltaTime * 1.4f; if (go == null) yield break; go.transform.position = start + Vector3.up * t * 1.0f; if (cg != null) cg.alpha = 1f - Mathf.Clamp01((t - 0.5f) * 2f); yield return null; }
        if (go != null) Destroy(go);
    }

    // =================================================================
    // End game
    // =================================================================

    float ComputeAccuracy()
    {
        // Max possible: ~20 fish/minute at 55% scam → ~11 scam × 15pts + 9 safe × 5pts = 210
        float maxPossible = 210f;
        return Mathf.Clamp01(score / maxPossible);
    }

    void GameOver()
    {
        if (gameEnded) return; gameEnded = true; gameActive = false;
        StopAllCoroutines();
        foreach (var f in liveFish) if (f != null) Destroy(f.gameObject);
        liveFish.Clear();
        gameOverPanel.SetActive(true);
        if (gameOverScoreText != null) gameOverScoreText.text = "Score: " + score;
        if (gameOverMessageText != null) gameOverMessageText.text = "Your tower cracked! Weak passwords like '123456' let the bad fish through.\nStrong passwords (symbols + numbers) keep them out.";
        commentator?.Say("The tower fell! We'll be stronger next time, dear.");
        PlayerProgress.QueueFromPerformance(ComputeAccuracy());
    }

    void WinGame()
    {
        if (gameEnded) return; gameEnded = true; gameActive = false;
        winPanel.SetActive(true);
        int max = 210; // estimated max score for a full 60s run (continuous spawn)
        if (winScoreText != null) winScoreText.text = score + " / " + max;
        float pct = max > 0 ? (float)score / max : 0;
        if (winStarsText != null) winStarsText.text = pct >= 0.9f ? "★ ★ ★" : pct >= 0.6f ? "★ ★" : "★";
        commentator?.Say(pct >= 0.9f ? "Perfect defence! The tower is safe, dear." : "We did it! Strong passwords saved the day.");
        PlayerProgress.QueueFromPerformance(ComputeAccuracy());
    }

    public void OnRetry() { UnityEngine.SceneManagement.SceneManager.LoadScene("TowerDefense"); }
    public void OnReturnToMap() { UnityEngine.SceneManagement.SceneManager.LoadScene("WorldMap"); }

    // =================================================================
    // White pixel
    // =================================================================

    private Sprite _white;
    private Sprite WhitePix
    {
        get
        {
            if (whiteSprite != null) return whiteSprite;
            if (_white != null) return _white;
            var tex = new Texture2D(4, 4); var px = new Color[16];
            for (int i = 0; i < 16; i++) px[i] = Color.white;
            tex.SetPixels(px); tex.Apply();
            _white = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            return _white;
        }
    }
}