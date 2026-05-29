using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Phish Patrol: Password Edition — tower defense minigame.
///
/// Weak passwords (scam) fly toward the player's computer as fish.
/// Click a fish to cast a fishing rod and reel it in.
/// Strong passwords (safe) should pass — clicking one launches a rocket
/// at your tower.
///
/// Wired by TowerDefenseBuilder. Wave/password content lives in the
/// data arrays below.
/// </summary>
public class TowerDefenseManager : MonoBehaviour
{
    [Header("Game Settings")]
    public int startingHealth = 5;

    [Header("Sprites (assigned by builder)")]
    public Sprite heartSprite;    // circle for life icons
    public Sprite whiteSprite;    // fallback white pixel
    public Sprite fishSprite;     // displayed on enemy GameObjects
    public Sprite rocketSprite;   // used when a safe password is wrongly clicked

    [Header("Scene References")]
    public Transform spawnPoint;
    public Transform towerTransform;
    public Transform towerShakeRoot;
    public SpriteRenderer towerScreenSr;
    public Transform crackContainer;

    [Header("Phisherman")]
    public Transform fishermanTransform;  // placeholder on top of tower; rod casts from here

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

    [Header("Game Over UI")]
    public TMP_Text gameOverScoreText;
    public TMP_Text gameOverMessageText;

    [Header("Win UI")]
    public TMP_Text winScoreText;
    public TMP_Text winStarsText;

    [Header("Commentator")]
    public Commentator commentator;

    // =========================================================================
    // Runtime state
    // =========================================================================

    private int score, health, combo, tutStep, currentWave, enemiesRemaining;
    private bool gameActive, spawning;
    private List<EnemyEmail> liveEnemies = new List<EnemyEmail>();
    private List<Image> heartImages = new List<Image>();

    private static readonly Color HeartFilled = new Color(0.91f, 0.30f, 0.24f);
    private static readonly Color HeartEmpty = new Color(0.40f, 0.40f, 0.45f);
    private static readonly Color ScreenNormal = new Color(0.04f, 0.18f, 0.32f);
    private static readonly Color ScreenHit = new Color(0.85f, 0.20f, 0.20f);

    static Color WEAK_BG = new Color(0.95f, 0.22f, 0.22f);
    static Color STRONG_BG = new Color(0.15f, 0.78f, 0.35f);

    struct ED { public string label; public bool isScam; public ED(string l, bool s) { label = l; isScam = s; } }

    ED[] scams = {
        new ED("password123", true), new ED("123456", true),   new ED("qwerty", true),
        new ED("iloveyou", true),    new ED("abc123", true),    new ED("password1", true),
        new ED("111111", true),      new ED("letmein", true),   new ED("welcome", true),
        new ED("monkey", true),      new ED("dragon", true),    new ED("master", true),
        new ED("hello", true),       new ED("login", true),     new ED("admin", true),
        new ED("baseball", true),    new ED("shadow", true),    new ED("trustno1", true),
        new ED("12345678", true),    new ED("princess", true),  new ED("sunshine", true),
        new ED("superman", true),    new ED("football", true),  new ED("charlie", true),
        new ED("donald", true),
    };

    ED[] safes = {
        new ED("K#9mP!2xL", false),    new ED("Blue$Tree47!", false),  new ED("Xq8@nW3!vY", false),
        new ED("Maple!Leaf99#", false), new ED("T7@kLz!9Rp", false),   new ED("Sun$Rise2024!", false),
        new ED("Wr9#mK!6Lp", false),   new ED("Cat!Rain$42X", false),  new ED("Gr@pe!Vine88", false),
        new ED("Z3br@Dance#7", false),  new ED("Moon&Star99#", false),  new ED("P@rrot3!Wing", false),
        new ED("B3eHoney$44!", false),  new ED("Night#Sky25!", false),  new ED("W1nt3r!Sun##", false),
        new ED("x9K!mPqR2@L", false),  new ED("J@zz7Beat!99", false),  new ED("Cr0wn$Eagle#5", false),
        new ED("H0r1zon&Sun2!", false), new ED("R@inB0w!77Frg", false),
    };

    struct Wave
    {
        public int count; public float scamRatio, speed, interval;
        public Wave(int c, float r, float s, float i) { count = c; scamRatio = r; speed = s; interval = i; }
    }

    Wave[] waves = {
        new Wave(14, 0.5f,  1.0f, 1.8f),
        new Wave(18, 0.55f, 1.3f, 1.4f),
        new Wave(22, 0.6f,  1.6f, 1.1f),
        new Wave(26, 0.65f, 2.0f, 0.85f),
    };

    string[] tutTitles = { "Phish Patrol: Password Edition", "Your mission" };
    string[] tutBodies = {
        "Weak passwords are swimming at your computer as fish!\n\nCast your fishing rod at weak passwords to catch them.\n\nLet strong passwords pass through safely.",
        "🎣 RED fish = Weak password — click to catch!\nexamples: password123, qwerty, 123456\n\n✓ GREEN fish = Strong password — let them swim past!\nexamples: K#9mP!2xL, Blue$Tree47!\n\nYour computer has 5 lives. Don't let it crack apart."
    };

    // =========================================================================
    // Lifecycle
    // =========================================================================

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

        int before = liveEnemies.Count;
        liveEnemies.RemoveAll(e => e == null);
        int vanished = before - liveEnemies.Count;
        if (vanished > 0)
        {
            enemiesRemaining = Mathf.Max(0, enemiesRemaining - vanished);
            if (!spawning && enemiesRemaining <= 0) TryAdvanceWave();
        }

        if (Input.GetMouseButtonDown(0))
        {
            Vector2 wp = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Collider2D hit = Physics2D.OverlapPoint(wp);
            if (hit != null)
            {
                EnemyEmail e = hit.GetComponent<EnemyEmail>();
                if (e != null) e.GetClicked();
            }
        }
    }

    // =========================================================================
    // Tutorial
    // =========================================================================

    void ShowTutStep(int step)
    {
        tutorialTitleText.text = tutTitles[step];
        tutorialBodyText.text = tutBodies[step];
        tutorialStepText.text = (step + 1) + " of " + tutTitles.Length;
        tutorialNextButtonText.text = step == tutTitles.Length - 1 ? "Start" : "Next";
    }

    public void OnTutorialNext()
    {
        tutStep++;
        if (tutStep >= tutTitles.Length)
        {
            tutorialPanel.SetActive(false);
            hudPanel.SetActive(true);
            StartGame();
        }
        else ShowTutStep(tutStep);
    }

    // =========================================================================
    // Game start / wave loop
    // =========================================================================

    void StartGame()
    {
        score = 0; health = startingHealth; combo = 0;
        currentWave = 0; gameActive = true;
        SpawnHearts();
        UpdateHUD();
        StartCoroutine(WaveDelay(1.5f));
        commentator?.Say("Cast your rod at the red fish — let the green ones swim past!");
    }

    IEnumerator WaveDelay(float d)
    {
        yield return new WaitForSeconds(d);
        StartCoroutine(SpawnWave(waves[currentWave]));
    }

    IEnumerator SpawnWave(Wave w)
    {
        spawning = true;
        waveText.text = "Wave " + (currentWave + 1) + " / " + waves.Length;
        var pool = BuildPool(w.count, w.scamRatio);
        enemiesRemaining = pool.Count;
        foreach (var e in pool)
        {
            if (!gameActive) yield break;
            SpawnEnemy(e, w.speed);
            yield return new WaitForSeconds(w.interval);
        }
        spawning = false;
    }

    List<ED> BuildPool(int count, float scamRatio)
    {
        var pool = new List<ED>();
        int sc = Mathf.RoundToInt(count * scamRatio);
        int sf = count - sc;
        var sp = new List<ED>(scams); var sfp = new List<ED>(safes);
        Shuffle(sp); Shuffle(sfp);
        for (int i = 0; i < sc && i < sp.Count; i++) pool.Add(sp[i]);
        for (int i = 0; i < sf && i < sfp.Count; i++) pool.Add(sfp[i]);
        Shuffle(pool);
        return pool;
    }

    void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int r = Random.Range(0, i + 1);
            T t = list[i]; list[i] = list[r]; list[r] = t;
        }
    }

    // =========================================================================
    // Spawn enemy — attaches fish sprite as a child
    // =========================================================================

    void SpawnEnemy(ED data, float speed)
    {
        var go = new GameObject("Enemy");
        go.transform.position = new Vector3(spawnPoint.position.x, Random.Range(-1.8f, 1.8f), 0f);
        go.transform.localScale = new Vector3(3.8f, 1.4f, 1f);
        go.AddComponent<BoxCollider2D>().size = Vector2.one;

        // ── Fish sprite ──────────────────────────────────────────────────────
        // Placed as a child so it moves with the enemy.
        // Parent scale is (3.8, 1.4) — local scale compensates so the fish
        // renders at roughly 1.5 × 1.0 world units.
        var fishChild = new GameObject("FishSprite");
        fishChild.transform.SetParent(go.transform, false);
        // Negative X flips sprite to face RIGHT (toward tower); most fish icons face left.
        fishChild.transform.localScale = new Vector3(-0.40f, 0.72f, 1f);
        fishChild.transform.localPosition = new Vector3(0f, 0f, 0.1f);
        var fsr = fishChild.AddComponent<SpriteRenderer>();
        fsr.sprite = fishSprite != null ? fishSprite : WhitePix;
        fsr.color = data.isScam ? new Color(1f, 0.45f, 0.45f)   // red tint  = weak
                                       : new Color(0.45f, 1f, 0.60f);  // green tint = strong
        fsr.sortingOrder = 3;

        // ── Password label (world-space canvas, renders above fish) ──────────
        var cGO = new GameObject("LabelCanvas");
        cGO.transform.SetParent(go.transform, false);
        var c = cGO.AddComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace;
        c.sortingOrder = 5;
        cGO.transform.localScale = new Vector3(0.008f, 0.011f, 1f);
        cGO.GetComponent<RectTransform>().sizeDelta = new Vector2(220, 80);

        var tGO = new GameObject("Label");
        tGO.transform.SetParent(cGO.transform, false);
        var tmp = tGO.AddComponent<TextMeshProUGUI>();
        tmp.text = data.label;
        tmp.fontSize = 22;
        tmp.color = Color.white;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        var tRT = tGO.GetComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
        tRT.offsetMin = tRT.offsetMax = Vector2.zero;

        // ── EnemyEmail controller ────────────────────────────────────────────
        var enemy = go.AddComponent<EnemyEmail>();
        enemy.Init(data.isScam, data.label, speed, this, towerTransform.position);
        liveEnemies.Add(enemy);
    }

    // =========================================================================
    // Damage flow
    // =========================================================================

    public void OnScamDestroyed(Vector3 pos)
    {
        combo++;
        int pts = combo >= 3 ? 15 : 10;
        score += pts;
        SpawnPopup("+" + pts, pos, new Color(0.1f, 0.9f, 0.3f));

        // Fishing-rod catch animation
        StartCoroutine(CastFishingRod(pos));

        EnemyGone();
        UpdateHUD();

        if (commentator != null)
        {
            if (combo == 3) commentator.Say("Three in a row — wonderful!");
            else if (combo == 6) commentator.Say("Oh my, you're unstoppable!");
            else if (combo == 1) commentator.SayRandom(new[] {
                "Got one!", "Nice catch, dear.", "That's the spirit!"
            });
        }
    }

    public void OnSafeDestroyed(Vector3 pos)
    {
        combo = 0;
        SpawnPopup("That was strong!", pos, new Color(0.95f, 0.3f, 0.3f));
        StartCoroutine(RocketThenDamage(pos));
        EnemyGone();
        UpdateHUD();

        commentator?.SayRandom(new[] {
            "Oh dear, that one was a good password!",
            "Easy now — green fish are safe.",
            "Don't catch the strong passwords!"
        });
    }

    public void OnScamReachedTower()
    {
        combo = 0;
        TakeDamage();
        EnemyGone();
        UpdateHUD();

        if (commentator != null && health > 0)
            commentator.SayRandom(new[] {
                "One slipped through!", "Cast faster, dear.", "Watch out — they're tricky!"
            });
    }

    public void OnSafeReachedTower()
    {
        EnemyGone();
        UpdateHUD();
    }

    IEnumerator RocketThenDamage(Vector3 from)
    {
        yield return StartCoroutine(LaunchRocket(from, towerTransform.position));
        TakeDamage();
        UpdateHUD();
    }

    // =========================================================================
    // Fishing-rod cast animation
    //   Casts a line from the phisherman's rod tip OUT to the fish position,
    //   holds a beat (hooked!), then reels the line back.
    // =========================================================================

    IEnumerator CastFishingRod(Vector3 fishWorldPos)
    {
        if (fishermanTransform == null) yield break;

        var rodGo = new GameObject("FishingRodLine");
        var lr = rodGo.AddComponent<LineRenderer>();
        lr.positionCount = 10;
        lr.startWidth = 0.07f;
        lr.endWidth = 0.015f;
        lr.useWorldSpace = true;
        lr.sortingOrder = 20;
        // Build a simple unlit material from the built-in Sprites shader
        var mat = new Material(Shader.Find("Sprites/Default"));
        lr.material = mat;
        lr.startColor = new Color(0.55f, 0.33f, 0.10f);
        lr.endColor = new Color(0.55f, 0.33f, 0.10f, 0.25f);

        // Rod tip: slightly left + upward from the phisherman centre
        Vector3 origin = fishermanTransform.position + new Vector3(-0.6f, 0.4f, 0f);

        // Helper: draw a drooping catenary-ish line from origin to tip
        void DrawLine(Vector3 tip)
        {
            for (int i = 0; i < 10; i++)
            {
                float f = i / 9f;
                Vector3 pt = Vector3.Lerp(origin, tip, f);
                // Parabolic sag in the middle
                float sag = Mathf.Sin(f * Mathf.PI) * 0.45f;
                pt.y -= sag;
                lr.SetPosition(i, pt);
            }
        }

        // ── Cast out ─────────────────────────────────────────────────────────
        float t = 0f, castDur = 0.26f;
        while (t < castDur)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / castDur));
            DrawLine(Vector3.Lerp(origin, fishWorldPos, p));
            yield return null;
        }
        DrawLine(fishWorldPos);

        // ── Brief "hooked!" hold ─────────────────────────────────────────────
        yield return new WaitForSeconds(0.10f);

        // ── Reel back ────────────────────────────────────────────────────────
        t = 0f;
        float reelDur = 0.22f;
        while (t < reelDur)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / reelDur));
            DrawLine(Vector3.Lerp(fishWorldPos, origin, p));
            yield return null;
        }

        Destroy(rodGo);
        Destroy(mat);
    }

    // =========================================================================
    // Rocket animation  (penalty for clicking a safe/green fish)
    //   Uses rocketSprite if assigned, falls back to tinted white pixel.
    // =========================================================================

    IEnumerator LaunchRocket(Vector3 from, Vector3 to)
    {
        var rocket = new GameObject("Rocket");
        rocket.transform.position = from;

        var sr = rocket.AddComponent<SpriteRenderer>();
        bool hasSprite = rocketSprite != null;
        sr.sprite = hasSprite ? rocketSprite : WhitePix;
        sr.color = hasSprite ? Color.white : new Color(1f, 0.55f, 0.15f);
        sr.sortingOrder = 12;
        rocket.transform.localScale = hasSprite
            ? new Vector3(0.65f, 0.65f, 1f)   // square-ish for real sprite
            : new Vector3(0.55f, 0.22f, 1f);   // elongated for pixel fallback

        // Point toward tower
        Vector3 dir = (to - from).normalized;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        rocket.transform.rotation = Quaternion.Euler(0, 0, angle);

        // Exhaust trail
        var trail = new GameObject("RocketTrail");
        trail.transform.SetParent(rocket.transform, false);
        var tsr = trail.AddComponent<SpriteRenderer>();
        tsr.sprite = WhitePix;
        tsr.color = new Color(1f, 0.85f, 0.3f, 0.45f);
        tsr.sortingOrder = 11;
        trail.transform.localPosition = new Vector3(-0.5f, 0, 0);
        trail.transform.localScale = new Vector3(1.4f, 0.55f, 1f);

        float duration = 0.42f, t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            float eased = p * p;
            rocket.transform.position = Vector3.Lerp(from, to, eased);
            float pulse = 1f + Mathf.Sin(t * 35f) * 0.08f;
            rocket.transform.localScale = hasSprite
                ? new Vector3(0.65f * pulse, 0.65f, 1f)
                : new Vector3(0.55f * pulse, 0.22f, 1f);
            yield return null;
        }

        Destroy(rocket);
    }

    // =========================================================================
    // Damage / tower effects
    // =========================================================================

    void TakeDamage()
    {
        health--;
        if (health < 0) health = 0;
        UpdateHearts();
        SpawnCrack();
        StartCoroutine(ShakeTower(0.18f, 0.35f));
        StartCoroutine(FlashScreen());
        if (health <= 0) StartCoroutine(BreakSequence());
    }

    IEnumerator ShakeTower(float intensity, float duration)
    {
        if (towerShakeRoot == null) yield break;
        Vector3 origin = towerShakeRoot.localPosition;
        float t = 0;
        while (t < duration)
        {
            t += Time.deltaTime;
            float d = 1f - Mathf.Clamp01(t / duration);
            towerShakeRoot.localPosition = origin + new Vector3(
                Random.Range(-intensity, intensity) * d,
                Random.Range(-intensity, intensity) * d, 0);
            yield return null;
        }
        towerShakeRoot.localPosition = origin;
    }

    IEnumerator FlashScreen()
    {
        if (towerScreenSr == null) yield break;
        towerScreenSr.color = ScreenHit;
        yield return new WaitForSeconds(0.12f);
        float t = 0, dur = 0.3f;
        while (t < dur)
        {
            t += Time.deltaTime;
            towerScreenSr.color = Color.Lerp(ScreenHit, ScreenNormal,
                Mathf.Clamp01(t / dur));
            yield return null;
        }
        towerScreenSr.color = ScreenNormal;
    }

    void SpawnCrack()
    {
        if (crackContainer == null) return;
        var crack = new GameObject("Crack");
        crack.transform.SetParent(crackContainer, false);
        crack.transform.localPosition = new Vector3(
            Random.Range(-1.2f, 1.2f), Random.Range(-0.7f, 0.7f), -0.05f);
        AddCrackLine(crack.transform, Random.Range(0.5f, 1.0f), 0.04f, Random.Range(-30f, 30f));
        AddCrackLine(crack.transform, Random.Range(0.3f, 0.7f), 0.035f, Random.Range(60f, 120f));
    }

    void AddCrackLine(Transform parent, float length, float width, float angleDeg)
    {
        var line = new GameObject("CrackLine");
        line.transform.SetParent(parent, false);
        line.transform.localRotation = Quaternion.Euler(0, 0, angleDeg);
        line.transform.localScale = new Vector3(length, width, 1);
        var sr = line.AddComponent<SpriteRenderer>();
        sr.sprite = WhitePix;
        sr.color = new Color(1f, 1f, 1f, 0.85f);
        sr.sortingOrder = 6;
    }

    IEnumerator BreakSequence()
    {
        gameActive = false;
        for (int i = 0; i < 8; i++) { SpawnCrack(); yield return new WaitForSeconds(0.05f); }
        yield return StartCoroutine(ShakeTower(0.32f, 0.6f));
        if (towerScreenSr != null) towerScreenSr.color = new Color(0.08f, 0.04f, 0.04f);
        yield return new WaitForSeconds(0.3f);
        GameOver();
    }

    // =========================================================================
    // HUD
    // =========================================================================

    void SpawnHearts()
    {
        if (heartsContainer == null) return;
        foreach (Transform t in heartsContainer) Destroy(t.gameObject);
        heartImages.Clear();
        for (int i = 0; i < startingHealth; i++)
        {
            var go = new GameObject("Life" + i, typeof(RectTransform));
            go.transform.SetParent(heartsContainer, false);
            var img = go.AddComponent<Image>();
            if (heartSprite != null) img.sprite = heartSprite;
            img.color = HeartFilled;
            img.raycastTarget = false;
            img.preserveAspect = true;
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = le.preferredHeight = 38;
            heartImages.Add(img);
        }
    }

    void UpdateHearts()
    {
        for (int i = 0; i < heartImages.Count; i++)
            heartImages[i].color = (i < health) ? HeartFilled : HeartEmpty;
    }

    void UpdateHUD()
    {
        if (scoreText != null) scoreText.text = "Score: " + score;
        if (waveText != null) waveText.text = "Wave " + (currentWave + 1) + " / " + waves.Length;
        if (comboText != null) comboText.text = combo >= 3 ? combo + "x combo!" : "";
    }

    // =========================================================================
    // Popups
    // =========================================================================

    void SpawnPopup(string text, Vector3 pos, Color col)
    {
        var go = new GameObject("Popup");
        go.transform.position = pos + Vector3.up * 0.8f;
        var c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace;
        go.transform.localScale = new Vector3(0.014f, 0.014f, 1f);
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(220, 60);
        go.AddComponent<CanvasGroup>();

        var t = new GameObject("T"); t.transform.SetParent(go.transform, false);
        var tmp = t.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 22;
        tmp.color = col;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        var rt = t.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        StartCoroutine(PopupAnim(go));
    }

    IEnumerator PopupAnim(GameObject go)
    {
        Vector3 start = go.transform.position;
        var cg = go.GetComponent<CanvasGroup>();
        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime * 1.5f;
            if (go == null) yield break;
            go.transform.position = start + Vector3.up * t * 1.2f;
            if (cg != null) cg.alpha = 1f - Mathf.Clamp01((t - 0.5f) * 2f);
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    // =========================================================================
    // Wave / end-game
    // =========================================================================

    void EnemyGone()
    {
        enemiesRemaining = Mathf.Max(0, enemiesRemaining - 1);
        liveEnemies.RemoveAll(e => e == null);
        if (!spawning && enemiesRemaining <= 0) TryAdvanceWave();
    }

    private bool advancingWave;
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

    void GameOver()
    {
        gameActive = false;
        StopAllCoroutines();
        foreach (var e in liveEnemies) if (e != null) Destroy(e.gameObject);
        liveEnemies.Clear();
        gameOverPanel.SetActive(true);
        gameOverScoreText.text = "Score: " + score;
        gameOverMessageText.text = "Your computer cracked. Weak passwords like '123456' are easy targets — strong, unique passwords keep the bad fish out.";
        commentator?.Clear();
        commentator?.Say("The tower fell! We'll get them next time, dear.");
    }

    void WinGame()
    {
        gameActive = false;
        winPanel.SetActive(true);
        int max = 0;
        foreach (var w in waves) max += Mathf.RoundToInt(w.count * w.scamRatio) * 10;
        winScoreText.text = score + " / " + max;
        float pct = (float)score / max;
        winStarsText.text = pct >= 0.9f ? "* * *" : pct >= 0.6f ? "* *" : "*";
        commentator?.Clear();
        commentator?.Say(pct >= 0.9f ? "Perfect! You saved the tower, dear!" : "We did it! Thank you, dear.");
    }

    public void OnRetry() { SceneManager.LoadScene("TowerDefense"); }
    public void OnReturnToMap() { SceneManager.LoadScene("WorldMap"); }

    // =========================================================================
    // Helpers
    // =========================================================================

    private Sprite _cachedWhite;
    private Sprite WhitePix
    {
        get
        {
            if (whiteSprite != null) return whiteSprite;
            if (_cachedWhite != null) return _cachedWhite;
            _cachedWhite = MakeFallbackSprite();
            return _cachedWhite;
        }
    }

    private static Sprite MakeFallbackSprite()
    {
        var tex = new Texture2D(4, 4);
        var px = new Color[16];
        for (int i = 0; i < 16; i++) px[i] = Color.white;
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
    }
}