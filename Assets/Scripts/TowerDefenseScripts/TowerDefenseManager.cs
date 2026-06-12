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
/// Click to shoot — hits the single nearest fish to the cursor only.
/// </summary>
public class TowerDefenseManager : MonoBehaviour
{
    [Header("Game Settings")]
    public int startingHealth = 5;
    public float spearSpeed = 22f;

    // Single neutral tint passed to every fish — player must read the label
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

    [Header("Scene References")]
    public Transform spawnPoint;
    public Transform towerRoot;
    public Transform towerShakeRoot;
    public Transform crackContainer;
    public Transform towerWaterContainer;
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
    private int score, health, combo, tutStep, currentWave, enemiesRemaining;
    private bool gameActive, spawning, advancingWave;
    private List<FishUnit> liveFish = new List<FishUnit>();
    private List<Image> heartImages = new List<Image>();

    // Per-fish pool swim state (struct so we can write back cheaply)
    private struct TowerFishState
    {
        public RectTransform rt;
        public Vector2 center;
        public float phaseX, phaseY;
        public float freqX, freqY;
        public float ampX, ampY;
    }
    private List<TowerFishState> towerFishStates = new List<TowerFishState>();

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
        new Wave(10, 0.50f, 0.40f, 3.2f),
        new Wave(15, 0.55f, 0.50f, 2.8f),
        new Wave(20, 0.60f, 0.60f, 2.4f),
    };

    string[] tutTitles = { "Phish Patrol -- Tower Defense", "How to play" };
    string[] tutBodies = {
        "Fish carrying log banners are swimming toward your tower!\n\n" +
        "Aim Phisherman's speargun and CLICK to shoot.",
        "Read the password on each fish's log banner!\n\n" +
        "WEAK password fish (like '123456') -- SHOOT THEM!\n" +
        "  Warning: shooting strong passwords makes them charge fast!\n\n" +
        "STRONG password fish (symbols + numbers) -- LET THEM reach the tower!\n\n" +
        "You must read each label to decide!"
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

        // Audio Setup
        bgmSource = gameObject.AddComponent<AudioSource>();
        if (bgmClip != null)
        {
            bgmSource.clip = bgmClip;
            bgmSource.loop = true;
            bgmSource.volume = 0.5f;
            bgmSource.Play();
        }

        sfxSource = gameObject.AddComponent<AudioSource>();

        ShowTutStep(0);
    }

    void Update()
    {
        if (!gameActive) return;

        // Speargun tracks mouse
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
            if (sfxSource != null && spearClip != null) sfxSource.PlayOneShot(spearClip);

            Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mouseWorld.z = 0;
            TryHitNearestFish(mouseWorld);
        }

        AnimateTowerFish();

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
        if (tutStep >= tutTitles.Length) { tutorialPanel.SetActive(false); StartGame(); }
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
        commentator?.Say("Read the label — shoot the weak passwords, let the strong ones through!");
    }

    IEnumerator WaveDelay(float d)
    {
        yield return new WaitForSeconds(d);
        StartCoroutine(SpawnWave(waves[currentWave]));
    }

    // =================================================================
    // Interaction
    // =================================================================

    void TryHitNearestFish(Vector3 clickWorld, float maxRadius = 1.4f)
    {
        FishUnit nearest = null;
        float bestDist = maxRadius;

        for (int i = liveFish.Count - 1; i >= 0; i--)
        {
            var f = liveFish[i];
            if (f == null) continue;
            float d = Vector3.Distance(f.transform.position, clickWorld);
            if (d < bestDist) { bestDist = d; nearest = f; }
        }

        if (nearest == null) return;

        Vector3 origin = spearSpawnPoint != null
            ? spearSpawnPoint.position
            : (towerRoot != null ? towerRoot.position + new Vector3(-0.5f, 2.5f, 0) : Vector3.zero);

        StartCoroutine(BoltAnim(origin, nearest.transform.position));
        nearest.TakeSpearHit();
    }

    IEnumerator BoltAnim(Vector3 from, Vector3 to)
    {
        var boltGO = new GameObject("SpearBolt");
        boltGO.transform.position = (from + to) * 0.5f;

        Vector3 delta = to - from;
        float len = delta.magnitude;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        boltGO.transform.rotation = Quaternion.Euler(0, 0, angle);
        boltGO.transform.localScale = new Vector3(len, 0.07f, 1f);

        var sr = boltGO.AddComponent<SpriteRenderer>();
        sr.sprite = WhitePix;
        sr.color = new Color(0.95f, 0.85f, 0.30f, 1f);
        sr.sortingOrder = 10;

        float t = 0f;
        while (t < 0.12f)
        {
            t += Time.deltaTime;
            if (boltGO == null) yield break;
            sr.color = new Color(0.95f, 0.85f, 0.30f, 1f - t / 0.12f);
            yield return null;
        }
        if (boltGO != null) Destroy(boltGO);
    }

    // =================================================================
    // Fish callbacks — called by FishUnit
    // =================================================================

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
        else commentator?.SayRandom(new[] { "Got one!", "Nice shot, dear.", "That's the spirit!" });
    }

    public void OnRedFishReachedTower(Vector3 pos, bool wasShot = false)
    {
        combo = 0;

        SpawnPopup("BAD FISH!", pos + Vector3.up, new Color(1f, 0.3f, 0.3f));
        TakeDamage();
        if (health > 0)
            commentator?.SayRandom(new[] { "A fish cracked the tower!", "Keep them back!" });

        UpdateHUD();
    }

    public void OnGreenFishCollected(GameObject fishGO, Sprite fishSpr, bool isDefused = false)
    {
        SpawnTowerFish(fishSpr);
        if (!isDefused)
        {
            score += 5;
            combo++;
            commentator?.SayRandom(new[] {
                "A strong password joined the tower!",
                "Safe and sound!", "Good fish welcome, dear."
            });
        }
        else
        {
            SpawnPopup("DEFUSED!", fishGO.transform.position + Vector3.up, new Color(0.3f, 1f, 0.3f));
            commentator?.SayRandom(new[] {
                "The defused fish is swimming happily!",
                "Look at it go!"
            });
        }
        UpdateHUD();
    }

    public void OnGreenFishShotEarly(Vector3 pos)
    {
        combo = 0;
        score = Mathf.Max(0, score - 5);
        SpawnPopup("-5 safe fish!", pos + Vector3.up * 2, new Color(0.9f, 0.4f, 0.1f));
        UpdateHUD();
    }

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
                  fishNormalSprite, fishHappySprite, fishPuffedSprite, logSprite,
                  NeutralFishColor);

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
    // Tower pool fish
    // =================================================================

    void SpawnTowerFish(Sprite spr)
    {
        if (towerWaterContainer == null) return;

        var go = new GameObject("TowerFish", typeof(RectTransform));
        go.transform.SetParent(towerWaterContainer, false);

        var rt = go.GetComponent<RectTransform>();
        Vector2 center = new Vector2(Random.Range(-70f, 70f), Random.Range(-18f, 18f));
        rt.anchoredPosition = center;
        rt.sizeDelta = new Vector2(36, 36);

        var img = go.AddComponent<Image>();
        img.color = NeutralFishColor;
        img.preserveAspect = true;
        img.raycastTarget = false;
        if (spr != null) img.sprite = spr;

        float facing = Random.value > 0.5f ? 1f : -1f;
        rt.localScale = new Vector3(facing, 1f, 1f);

        towerFishStates.Add(new TowerFishState
        {
            rt = rt,
            center = center,
            phaseX = Random.Range(0f, Mathf.PI * 2f),
            phaseY = Random.Range(0f, Mathf.PI * 2f),
            freqX = Random.Range(0.5f, 1.1f),
            freqY = Random.Range(1.0f, 1.9f),
            ampX = Random.Range(48f, 82f),
            ampY = Random.Range(12f, 24f),
        });

        StartCoroutine(SplashIn(rt, facing));
    }

    IEnumerator SplashIn(RectTransform rt, float facing)
    {
        rt.localScale = new Vector3(facing * 1.5f, 1.5f, 1f);
        float t = 0f;
        while (t < 0.25f)
        {
            t += Time.deltaTime;
            if (rt == null) yield break;
            float s = Mathf.Lerp(1.5f, 1f, Mathf.Clamp01(t / 0.25f));
            rt.localScale = new Vector3(facing * s, s, 1f);
            yield return null;
        }
    }

    void AnimateTowerFish()
    {
        float time = Time.time;
        for (int i = towerFishStates.Count - 1; i >= 0; i--)
        {
            var s = towerFishStates[i];
            if (s.rt == null) { towerFishStates.RemoveAt(i); continue; }

            float x = s.center.x + Mathf.Sin(time * s.freqX + s.phaseX) * s.ampX;
            float y = s.center.y + Mathf.Sin(time * s.freqY + s.phaseY) * s.ampY;

            x = Mathf.Clamp(x, -86f, 86f);
            y = Mathf.Clamp(y, -26f, 26f);
            s.rt.anchoredPosition = new Vector2(x, y);

            float dx = Mathf.Cos(time * s.freqX + s.phaseX);
            if (Mathf.Abs(dx) > 0.05f)
                s.rt.localScale = new Vector3(dx > 0 ? 1f : -1f, 1f, 1f);

            towerFishStates[i] = s;
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
        c.renderMode = RenderMode.WorldSpace; c.sortingOrder = 20;
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