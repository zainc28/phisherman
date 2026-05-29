using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Phish Patrol — Tower Defense.
///
/// Fish+log units swim from the left toward the tower.
/// Phisherman stands on top with a speargun that aims at the mouse.
/// Click to fire a spear.
///
/// Red  + reaches tower  → pufferfish explosion, tower damage
/// Red  + shot early     → enrages, charges at 2.5x speed
/// Green + reaches tower → collects in tower water, swims
/// Green + shot early    → reeled to tower dead, no benefit
/// </summary>
public class TowerDefenseManager : MonoBehaviour
{
    [Header("Game Settings")]
    public int startingHealth = 5;
    public float spearSpeed = 22f;

    [Header("Sprites — auto-wired by builder")]
    public Sprite fishSprite;
    public Sprite logSprite;
    public Sprite towerSprite;
    public Sprite phishermanSprite;
    public Sprite speargunSprite;
    public Sprite whiteSprite;

    [Header("Scene References")]
    public Transform spawnPoint;
    public Transform towerRoot;
    public Transform towerShakeRoot;
    public Transform crackContainer;
    public Transform towerWaterContainer;   // WorldSpace canvas RectTransform
    public Transform speargunPivot;         // rotates toward mouse each frame
    public Transform spearSpawnPoint;       // tip of speargun

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
    private int score, health, combo, tutStep, currentWave, enemiesRemaining;
    private bool gameActive, spawning, advancingWave;
    private List<FishUnit> liveFish = new List<FishUnit>();
    private List<Image> heartImages = new List<Image>();
    private List<RectTransform> towerFish = new List<RectTransform>();

    private static readonly Color HeartFull = new Color(0.91f, 0.30f, 0.24f);
    private static readonly Color HeartEmpty = new Color(0.40f, 0.40f, 0.45f);

    // ── Wave / password data ──
    struct ED
    {
        public string label; public bool isScam;
        public ED(string l, bool s) { label = l; isScam = s; }
    }

    ED[] scams = {
        new ED("password123", true), new ED("123456", true),   new ED("qwerty", true),
        new ED("iloveyou", true),    new ED("abc123", true),    new ED("password1", true),
        new ED("111111", true),      new ED("letmein", true),   new ED("welcome", true),
        new ED("monkey", true),      new ED("dragon", true),    new ED("master", true),
        new ED("hello", true),       new ED("login", true),     new ED("admin", true),
        new ED("baseball", true),    new ED("shadow", true),    new ED("trustno1", true),
        new ED("12345678", true),    new ED("princess", true),
    };

    ED[] safes = {
        new ED("K#9mP!2xL", false),  new ED("Blue$Tree47!", false),
        new ED("Xq8@nW3!vY", false), new ED("Maple!Leaf99#", false),
        new ED("T7@kLz!9Rp", false), new ED("Sun$Rise2024!", false),
        new ED("Wr9#mK!6Lp", false), new ED("Cat!Rain$42X", false),
        new ED("Gr@pe!Vine88", false),new ED("Z3br@Dance#7", false),
        new ED("Moon&Star99#", false),new ED("P@rrot3!Wing", false),
        new ED("B3eHoney$44!", false),new ED("Night#Sky25!", false),
    };

    struct Wave
    {
        public int count; public float scamRatio, speed, interval;
        public Wave(int c, float r, float s, float i)
        { count = c; scamRatio = r; speed = s; interval = i; }
    }

    Wave[] waves = {
        new Wave(12, 0.50f, 1.0f, 1.8f),
        new Wave(16, 0.55f, 1.3f, 1.4f),
        new Wave(20, 0.60f, 1.7f, 1.1f),
        new Wave(24, 0.65f, 2.1f, 0.85f),
    };

    string[] tutTitles = { "Phish Patrol -- Tower Defense", "How to play" };
    string[] tutBodies = {
        "Fish carrying log banners are swimming toward your tower!\n\n" +
        "Aim Phisherman's speargun and CLICK to shoot.",
        "RED flag fish = weak password -- SHOOT THEM!\n" +
        "  Warning: shooting them early makes them charge faster!\n\n" +
        "GREEN flag fish = strong password -- LET THEM REACH THE TOWER!\n" +
        "  Shooting green fish reels them in dead.\n\n" +
        "Let green fish swim into your tower. Block the red ones!"
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
        ShowTutStep(0);
    }

    void Update()
    {
        if (!gameActive) return;

        // ── Speargun tracks mouse ──
        if (speargunPivot != null)
        {
            Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mouseWorld.z = 0;
            Vector3 dir = mouseWorld - speargunPivot.position;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            speargunPivot.rotation = Quaternion.Euler(0, 0, angle);
        }

        // ── Fire on click ──
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mouseWorld.z = 0;
            FireSpear(mouseWorld);
        }

        // ── Animate swimming fish in tower ──
        AnimateTowerFish();

        // ── Clean null refs (destroyed fish) ──
        liveFish.RemoveAll(f => f == null);
        if (!spawning && enemiesRemaining <= 0 && liveFish.Count == 0)
            TryAdvanceWave();
    }

    // =================================================================
    // Tutorial
    // =================================================================

    void ShowTutStep(int step)
    {
        tutorialTitleText.text = tutTitles[step];
        tutorialBodyText.text = tutBodies[step];
        tutorialStepText.text = (step + 1) + " of " + tutTitles.Length;
        tutorialNextButtonText.text = (step == tutTitles.Length - 1) ? "Start!" : "Next";
    }

    public void OnTutorialNext()
    {
        tutStep++;
        if (tutStep >= tutTitles.Length)
        {
            tutorialPanel.SetActive(false);
            StartGame();
        }
        else ShowTutStep(tutStep);
    }

    // =================================================================
    // Game start
    // =================================================================

    void StartGame()
    {
        score = 0; health = startingHealth; combo = 0;
        currentWave = 0; gameActive = true;
        hudPanel.SetActive(true);
        SpawnHearts();
        UpdateHUD();
        StartCoroutine(WaveDelay(1.5f));
        commentator?.Say("Let the green fish reach the tower. Spear the red ones!");
    }

    IEnumerator WaveDelay(float d)
    {
        yield return new WaitForSeconds(d);
        StartCoroutine(SpawnWave(waves[currentWave]));
    }

    // =================================================================
    // Spear firing
    // =================================================================

    void FireSpear(Vector3 targetWorld)
    {
        Vector3 origin = spearSpawnPoint != null
            ? spearSpawnPoint.position
            : (towerRoot != null ? towerRoot.position + new Vector3(-0.5f, 2.5f, 0) : Vector3.zero);

        Vector3 dir = (targetWorld - origin).normalized;

        var spearGO = new GameObject("Spear");
        spearGO.tag = "Spear";
        spearGO.transform.position = origin;

        var col = spearGO.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.18f;

        var rb = spearGO.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0;

        var sr = spearGO.AddComponent<SpriteRenderer>();
        sr.sprite = WhitePix;
        sr.color = new Color(0.95f, 0.85f, 0.30f);
        sr.sortingOrder = 10;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        spearGO.transform.rotation = Quaternion.Euler(0, 0, angle);
        spearGO.transform.localScale = new Vector3(0.55f, 0.07f, 1f);

        StartCoroutine(MoveSpear(spearGO, dir));
    }

    IEnumerator MoveSpear(GameObject spear, Vector3 dir)
    {
        float maxDist = 20f, traveled = 0f;
        while (spear != null && traveled < maxDist)
        {
            float step = spearSpeed * Time.deltaTime;
            spear.transform.position += dir * step;
            traveled += step;
            yield return null;
        }
        if (spear != null) Destroy(spear);
    }

    // =================================================================
    // Fish callbacks — called by FishUnit
    // =================================================================

    /// <summary>Called when a red fish is HIT by the spear (before it enrages).</summary>
    public void OnRedFishSpearHit(Vector3 pos)
    {
        combo++;
        int pts = combo >= 3 ? 15 : 10;
        score += pts;
        SpawnPopup("+" + pts + (combo >= 3 ? " COMBO!" : ""), pos + Vector3.up,
            combo >= 3 ? new Color(1f, 0.85f, 0.1f) : new Color(0.3f, 1f, 0.5f));
        UpdateHUD();

        if (combo == 3) commentator?.Say("Three in a row -- wonderful!");
        else if (combo == 6) commentator?.Say("Oh my, you are unstoppable!");
        else commentator?.SayRandom(new[] {
            "Got one!", "Nice shot, dear.", "That's the spirit!"
        });
    }

    /// <summary>Called when a red fish (enraged or not) reaches the tower and explodes.</summary>
    public void OnRedFishReachedTower(Vector3 pos)
    {
        combo = 0;
        SpawnPopup("BAD FISH!", pos + Vector3.up, new Color(1f, 0.3f, 0.3f));
        TakeDamage();
        UpdateHUD();
        if (health > 0)
            commentator?.SayRandom(new[] {
                "One slipped through!", "Keep them back!", "Block the red ones!"
            });
    }

    /// <summary>Called when a green fish safely reaches the tower.</summary>
    public void OnGreenFishCollected(GameObject fishGO, Sprite fishSpr)
    {
        SpawnTowerFish(fishSpr);
        score += 5;
        combo++;
        UpdateHUD();
        commentator?.SayRandom(new[] {
            "A strong password joined the tower!",
            "Safe and sound!", "Good fish welcome, dear."
        });
    }

    /// <summary>Called when a green fish is shot and reeled in dead.</summary>
    public void OnGreenFishShotDead()
    {
        combo = 0;
        score = Mathf.Max(0, score - 5);
        SpawnPopup("-5 safe fish!", Vector3.zero + Vector3.up * 2,
            new Color(0.9f, 0.4f, 0.1f));
        UpdateHUD();
    }

    /// <summary>
    /// Called by FishUnit whenever it destroys itself (any outcome).
    /// Tracks remaining fish so the wave can advance.
    /// </summary>
    public void OnFishRemoved()
    {
        enemiesRemaining = Mathf.Max(0, enemiesRemaining - 1);
    }

    // =================================================================
    // Spawning
    // =================================================================

    IEnumerator SpawnWave(Wave w)
    {
        spawning = true;
        waveText.text = "Wave " + (currentWave + 1) + " / " + waves.Length;
        var pool = BuildPool(w.count, w.scamRatio);
        enemiesRemaining = pool.Count;

        foreach (var data in pool)
        {
            if (!gameActive) yield break;
            SpawnFish(data, w.speed);
            yield return new WaitForSeconds(w.interval);
        }
        spawning = false;
    }

    void SpawnFish(ED data, float speed)
    {
        var go = new GameObject("FishUnit");
        go.transform.position = new Vector3(
            spawnPoint.position.x,
            towerRoot != null
                ? towerRoot.position.y + Random.Range(-1.4f, 1.4f)
                : Random.Range(-1.8f, 1.8f),
            0f);

        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.35f;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0;

        var fish = go.AddComponent<FishUnit>();
        fish.Init(data.isScam, data.label, speed,
                  this,
                  towerRoot != null ? towerRoot.position : new Vector3(5f, 0, 0),
                  fishSprite, logSprite);

        liveFish.Add(fish);
    }

    List<ED> BuildPool(int count, float scamRatio)
    {
        var pool = new List<ED>();
        int sc = Mathf.RoundToInt(count * scamRatio);
        int sf = count - sc;
        var sp = new List<ED>(scams);
        var sfp = new List<ED>(safes);
        Shuffle(sp); Shuffle(sfp);
        for (int i = 0; i < sc && i < sp.Count; i++) pool.Add(sp[i]);
        for (int i = 0; i < sf && i < sfp.Count; i++) pool.Add(sfp[i]);
        Shuffle(pool);
        return pool;
    }

    void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        { int r = Random.Range(0, i + 1); T t = list[i]; list[i] = list[r]; list[r] = t; }
    }

    // =================================================================
    // Tower water fish
    // =================================================================

    void SpawnTowerFish(Sprite spr)
    {
        if (towerWaterContainer == null) return;
        var go = new GameObject("TowerFish", typeof(RectTransform));
        go.transform.SetParent(towerWaterContainer, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(Random.Range(-80f, 80f), Random.Range(-25f, 25f));
        rt.sizeDelta = new Vector2(36, 36);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.42f, 1f, 0.65f);
        img.preserveAspect = true;
        img.raycastTarget = false;
        if (Random.value > 0.5f) rt.localScale = new Vector3(-1, 1, 1);

        towerFish.Add(rt);
        StartCoroutine(SplashIn(rt));
    }

    IEnumerator SplashIn(RectTransform rt)
    {
        float sign = rt.localScale.x;
        rt.localScale = new Vector3(sign * 1.5f, 1.5f, 1);
        float t = 0f;
        while (t < 0.25f)
        {
            t += Time.deltaTime;
            float s = Mathf.Lerp(1.5f, 1f, Mathf.Clamp01(t / 0.25f));
            rt.localScale = new Vector3(sign * s, s, 1);
            yield return null;
        }
    }

    void AnimateTowerFish()
    {
        float time = Time.time;
        for (int i = towerFish.Count - 1; i >= 0; i--)
        {
            if (towerFish[i] == null) { towerFish.RemoveAt(i); continue; }
            var rt = towerFish[i];
            var pos = rt.anchoredPosition;
            pos.x += Mathf.Sin(time * 0.8f + i * 1.7f) * 0.4f;
            pos.y += Mathf.Cos(time * 1.2f + i * 2.3f) * 0.12f;
            rt.anchoredPosition = pos;
        }
    }

    // =================================================================
    // Tower damage
    // =================================================================

    void TakeDamage()
    {
        health = Mathf.Max(0, health - 1);
        UpdateHearts();
        SpawnCrack();
        StartCoroutine(ShakeTower(0.20f, 0.35f));
        if (health <= 0) StartCoroutine(BreakSequence());
    }

    IEnumerator ShakeTower(float intensity, float dur)
    {
        if (towerShakeRoot == null) yield break;
        Vector3 orig = towerShakeRoot.localPosition;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float d = 1f - Mathf.Clamp01(t / dur);
            towerShakeRoot.localPosition = orig + new Vector3(
                Random.Range(-intensity, intensity) * d,
                Random.Range(-intensity * 0.5f, intensity * 0.5f) * d, 0);
            yield return null;
        }
        towerShakeRoot.localPosition = orig;
    }

    void SpawnCrack()
    {
        if (crackContainer == null) return;
        var crack = new GameObject("Crack");
        crack.transform.SetParent(crackContainer, false);
        crack.transform.localPosition = new Vector3(
            Random.Range(-0.8f, 0.8f), Random.Range(-1.0f, 1.0f), -0.05f);
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
        sr.sprite = WhitePix; sr.color = new Color(1f, 1f, 1f, 0.80f);
        sr.sortingOrder = 8;
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
    // Wave management
    // =================================================================

    void TryAdvanceWave()
    {
        if (advancingWave || !gameActive) return;
        advancingWave = true;
        currentWave++;
        if (currentWave >= waves.Length) WinGame();
        else StartCoroutine(NextWaveRoutine());
    }

    IEnumerator NextWaveRoutine()
    {
        yield return new WaitForSeconds(2f);
        advancingWave = false;
        StartCoroutine(SpawnWave(waves[currentWave]));
    }

    // =================================================================
    // HUD
    // =================================================================

    void SpawnHearts()
    {
        foreach (Transform t in heartsContainer) Destroy(t.gameObject);
        heartImages.Clear();
        for (int i = 0; i < startingHealth; i++)
        {
            var go = new GameObject("Life" + i, typeof(RectTransform));
            go.transform.SetParent(heartsContainer, false);
            var img = go.AddComponent<Image>();
            img.color = HeartFull; img.raycastTarget = false; img.preserveAspect = true;
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = le.preferredHeight = 38;
            heartImages.Add(img);
        }
    }

    void UpdateHearts()
    {
        for (int i = 0; i < heartImages.Count; i++)
            heartImages[i].color = i < health ? HeartFull : HeartEmpty;
    }

    void UpdateHUD()
    {
        if (scoreText != null) scoreText.text = "Score: " + score;
        if (waveText != null) waveText.text = "Wave " + (currentWave + 1) + " / " + waves.Length;
        if (comboText != null) comboText.text = combo >= 3 ? combo + "x combo!" : "";
    }

    // =================================================================
    // Popups
    // =================================================================

    void SpawnPopup(string text, Vector3 pos, Color col)
    {
        var go = new GameObject("Popup");
        go.transform.position = pos;
        var c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace;
        c.sortingOrder = 20;
        go.transform.localScale = new Vector3(0.013f, 0.013f, 1f);
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(260, 60);
        go.AddComponent<CanvasGroup>();

        var tgo = new GameObject("T"); tgo.transform.SetParent(go.transform, false);
        var tmp = tgo.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = 22; tmp.color = col;
        tmp.fontStyle = FontStyles.Bold; tmp.alignment = TextAlignmentOptions.Center;
        var rt = tgo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        StartCoroutine(PopupAnim(go));
    }

    IEnumerator PopupAnim(GameObject go)
    {
        Vector3 start = go.transform.position;
        var cg = go.GetComponent<CanvasGroup>();
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 1.4f;
            if (go == null) yield break;
            go.transform.position = start + Vector3.up * t * 1.0f;
            if (cg != null) cg.alpha = 1f - Mathf.Clamp01((t - 0.5f) * 2f);
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    // =================================================================
    // End game
    // =================================================================

    void GameOver()
    {
        gameActive = false;
        StopAllCoroutines();
        foreach (var f in liveFish) if (f != null) Destroy(f.gameObject);
        liveFish.Clear();
        gameOverPanel.SetActive(true);
        gameOverScoreText.text = "Score: " + score;
        gameOverMessageText.text =
            "Your tower cracked! Weak passwords like '123456' let the bad fish through.\n" +
            "Strong passwords (symbols + numbers) keep them out.";
        commentator?.Say("The tower fell! We'll be stronger next time, dear.");
    }

    void WinGame()
    {
        gameActive = false;
        winPanel.SetActive(true);
        int max = 0;
        foreach (var w in waves) max += Mathf.RoundToInt(w.count * w.scamRatio) * 10;
        winScoreText.text = score + " / " + max;
        float pct = max > 0 ? (float)score / max : 0;
        winStarsText.text = pct >= 0.9f ? "* * *" : pct >= 0.6f ? "* *" : "*";
        commentator?.Say(pct >= 0.9f
            ? "Perfect defence! The tower is safe, dear."
            : "We did it! Strong passwords saved the day.");
    }

    public void OnRetry() { SceneManager.LoadScene("TowerDefense"); }
    public void OnReturnToMap() { SceneManager.LoadScene("WorldMap"); }

    // =================================================================
    // Helpers
    // =================================================================

    private Sprite _white;
    private Sprite WhitePix
    {
        get
        {
            if (whiteSprite != null) return whiteSprite;
            if (_white != null) return _white;
            var tex = new Texture2D(4, 4);
            var px = new Color[16]; for (int i = 0; i < 16; i++) px[i] = Color.white;
            tex.SetPixels(px); tex.Apply();
            _white = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            return _white;
        }
    }
}