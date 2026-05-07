using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Phish Patrol: Password Edition — tower defense minigame.
///
/// Weak passwords (red) fly toward the player's computer. Click them to
/// destroy them. Strong passwords (green) should be left alone — clicking
/// one launches it as a rocket at the player's tower.
///
/// Each impact: shake + screen flash + a fresh crack. Five hits and the
/// computer breaks.
///
/// Wired by TowerDefenseBuilder when the scene is built. Wave/password
/// content lives in the data arrays below.
/// </summary>
public class TowerDefenseManager : MonoBehaviour
{
    [Header("Game Settings")]
    public int startingHealth = 5;

    [Header("Sprites (assigned by builder)")]
    public Sprite heartSprite;       // circle for life icons
    public Sprite whiteSprite;       // for rockets, cracks, popups

    [Header("Scene References")]
    public Transform spawnPoint;
    public Transform towerTransform;       // root position (target for enemies)
    public Transform towerShakeRoot;       // child that shakes on impact
    public SpriteRenderer towerScreenSr;   // screen renderer (color flash)
    public Transform crackContainer;       // parent for crack children

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

    // ===== Commentator =====
    [Header("Commentator")]
    public Commentator commentator;

    // ===== Runtime state =====
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
        new ED("password123", true),
        new ED("123456", true),
        new ED("qwerty", true),
        new ED("iloveyou", true),
        new ED("abc123", true),
        new ED("password1", true),
        new ED("111111", true),
        new ED("letmein", true),
        new ED("welcome", true),
        new ED("monkey", true),
        new ED("dragon", true),
        new ED("master", true),
        new ED("hello", true),
        new ED("login", true),
        new ED("admin", true),
        new ED("baseball", true),
        new ED("shadow", true),
        new ED("trustno1", true),
        new ED("12345678", true),
        new ED("princess", true),
        new ED("sunshine", true),
        new ED("superman", true),
        new ED("football", true),
        new ED("charlie", true),
        new ED("donald", true),
    };

    ED[] safes = {
        new ED("K#9mP!2xL", false),
        new ED("Blue$Tree47!", false),
        new ED("Xq8@nW3!vY", false),
        new ED("Maple!Leaf99#", false),
        new ED("T7@kLz!9Rp", false),
        new ED("Sun$Rise2024!", false),
        new ED("Wr9#mK!6Lp", false),
        new ED("Cat!Rain$42X", false),
        new ED("Gr@pe!Vine88", false),
        new ED("Z3br@Dance#7", false),
        new ED("Moon&Star99#", false),
        new ED("P@rrot3!Wing", false),
        new ED("B3eHoney$44!", false),
        new ED("Night#Sky25!", false),
        new ED("W1nt3r!Sun##", false),
        new ED("x9K!mPqR2@L", false),
        new ED("J@zz7Beat!99", false),
        new ED("Cr0wn$Eagle#5", false),
        new ED("H0r1zon&Sun2!", false),
        new ED("R@inB0w!77Frg", false),
    };

    struct Wave
    {
        public int count; public float scamRatio, speed, interval;
        public Wave(int c, float r, float s, float i) { count = c; scamRatio = r; speed = s; interval = i; }
    }

    Wave[] waves = {
        new Wave(14, 0.5f, 1.0f, 1.8f),   // Wave 1 — slow intro, half and half
        new Wave(18, 0.55f, 1.3f, 1.4f),  // Wave 2 — slightly more scams, faster
        new Wave(22, 0.6f,  1.6f, 1.1f),  // Wave 3 — scam heavy, quick
        new Wave(26, 0.65f, 2.0f, 0.85f), // Wave 4 — hardest, lots of scams fast
    };

    string[] tutTitles = { "Phish Patrol: Password Edition", "Your mission" };
    string[] tutBodies = {
        "Weak passwords are flying at your computer.\n\nClick the weak passwords to destroy them before they hit your tower.\n\nLet the strong passwords pass through safely.",
        "RED FLAG = Weak password (destroy it!)\nexamples: password123, qwerty, 123456\n\nGREEN FLAG = Strong password (let it pass!)\nexamples: K#9mP!2xL, Blue$Tree47!\n\nYour computer has 5 lives. Don't let it crack apart."
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

        // Catch enemies that destroyed themselves without calling EnemyGone —
        // most commonly safe (strong) passwords that flew past the tower and
        // called Destroy(gameObject) in EnemyEmail without a manager callback.
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
    // Tutorial flow
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
        else
        {
            ShowTutStep(tutStep);
        }
    }

    // =========================================================================
    // Game start / wave loop
    // =========================================================================

    void StartGame()
    {
        score = 0;
        health = startingHealth;
        combo = 0;
        currentWave = 0;
        gameActive = true;

        SpawnHearts();
        UpdateHUD();
        StartCoroutine(WaveDelay(1.5f));

        if (commentator != null)
        {
            commentator.Say("Click the weak red passwords — let the green strong ones pass!");
        }
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
        var sp = new List<ED>(scams);
        var sfp = new List<ED>(safes);
        Shuffle(sp);
        Shuffle(sfp);
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
            T t = list[i];
            list[i] = list[r];
            list[r] = t;
        }
    }

    void SpawnEnemy(ED data, float speed)
    {
        GameObject go = new GameObject("Enemy");
        go.transform.position = new Vector3(spawnPoint.position.x, Random.Range(-1.8f, 1.8f), 0f);
        go.transform.localScale = new Vector3(3.8f, 1.4f, 1f);

        // No SpriteRenderer — the colored rectangle was covering the password text.
        // The BoxCollider2D below still makes the area clickable.
        go.AddComponent<BoxCollider2D>().size = Vector2.one;

        // World-space label
        GameObject cGO = new GameObject("LabelCanvas");
        cGO.transform.SetParent(go.transform, false);
        Canvas c = cGO.AddComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace;
        c.sortingOrder = 5;
        cGO.transform.localScale = new Vector3(0.008f, 0.011f, 1f);
        cGO.GetComponent<RectTransform>().sizeDelta = new Vector2(220, 80);

        GameObject tGO = new GameObject("Label");
        tGO.transform.SetParent(cGO.transform, false);
        TextMeshProUGUI tmp = tGO.AddComponent<TextMeshProUGUI>();
        tmp.text = data.label;
        tmp.fontSize = 22;
        tmp.color = Color.white;   // no colour cue — player reads the password
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        RectTransform tRT = tGO.GetComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
        tRT.offsetMin = tRT.offsetMax = Vector2.zero;

        EnemyEmail enemy = go.AddComponent<EnemyEmail>();
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
        EnemyGone();
        UpdateHUD();

        if (commentator != null)
        {
            if (combo == 3) commentator.Say("Three in a row — wonderful!");
            else if (combo == 6) commentator.Say("Oh my, you're unstoppable!");
            else if (combo == 1)
                commentator.SayRandom(new[] {
                    "Got one!", "Nice shot, dear.", "That's the spirit!"
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

        if (commentator != null)
        {
            commentator.SayRandom(new[] {
                "Oh dear, that one was a good password!",
                "Easy now — green ones are safe.",
                "Don't shoot the strong passwords!"
            });
        }
    }

    public void OnScamReachedTower()
    {
        combo = 0;
        TakeDamage();
        EnemyGone();
        UpdateHUD();

        if (commentator != null && health > 0)
        {
            commentator.SayRandom(new[] {
                "One slipped through!",
                "Catch them faster, dear.",
                "Watch out — they're tricky!"
            });
        }
    }

    /// <summary>
    /// Called by EnemyEmail when a SAFE (strong) password reaches the tower.
    /// Safe passwords pass through harmlessly — no damage, just count them out.
    /// </summary>
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

    IEnumerator LaunchRocket(Vector3 from, Vector3 to)
    {
        var rocket = new GameObject("Rocket");
        rocket.transform.position = from;
        var sr = rocket.AddComponent<SpriteRenderer>();
        sr.sprite = WhitePix;
        sr.color = new Color(1f, 0.55f, 0.15f);
        sr.sortingOrder = 12;
        rocket.transform.localScale = new Vector3(0.55f, 0.22f, 1f);

        Vector3 dir = (to - from).normalized;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        rocket.transform.rotation = Quaternion.Euler(0, 0, angle);

        // Trail (a slightly faded copy that drags behind)
        var trail = new GameObject("RocketTrail");
        trail.transform.SetParent(rocket.transform, false);
        var tsr = trail.AddComponent<SpriteRenderer>();
        tsr.sprite = sr.sprite;
        tsr.color = new Color(1f, 0.85f, 0.3f, 0.5f);
        tsr.sortingOrder = 11;
        trail.transform.localPosition = new Vector3(-0.3f, 0, 0);
        trail.transform.localScale = new Vector3(1.4f, 0.7f, 1f);

        float duration = 0.42f;
        float t = 0;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            // ease-in (accelerating toward target)
            float eased = p * p;
            rocket.transform.position = Vector3.Lerp(from, to, eased);
            // pulse
            float pulse = 1f + Mathf.Sin(t * 35f) * 0.08f;
            rocket.transform.localScale = new Vector3(0.55f * pulse, 0.22f, 1f);
            yield return null;
        }

        Destroy(rocket);
    }

    void TakeDamage()
    {
        health--;
        if (health < 0) health = 0;

        UpdateHearts();
        SpawnCrack();
        StartCoroutine(ShakeTower(0.18f, 0.35f));
        StartCoroutine(FlashScreen());

        if (health <= 0)
        {
            StartCoroutine(BreakSequence());
        }
    }

    // =========================================================================
    // Tower visual feedback
    // =========================================================================

    IEnumerator ShakeTower(float intensity, float duration)
    {
        if (towerShakeRoot == null) yield break;
        Vector3 origin = towerShakeRoot.localPosition;
        float t = 0;
        while (t < duration)
        {
            t += Time.deltaTime;
            float damper = 1f - Mathf.Clamp01(t / duration);
            towerShakeRoot.localPosition = origin + new Vector3(
                Random.Range(-intensity, intensity) * damper,
                Random.Range(-intensity, intensity) * damper,
                0);
            yield return null;
        }
        towerShakeRoot.localPosition = origin;
    }

    IEnumerator FlashScreen()
    {
        if (towerScreenSr == null) yield break;
        Color original = ScreenNormal;
        towerScreenSr.color = ScreenHit;
        yield return new WaitForSeconds(0.12f);

        float t = 0, dur = 0.3f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            towerScreenSr.color = Color.Lerp(ScreenHit, original, p);
            yield return null;
        }
        towerScreenSr.color = original;
    }

    void SpawnCrack()
    {
        if (crackContainer == null) return;

        // Each "crack" is a clutch of 2 thin rotated rectangles intersecting
        var crack = new GameObject("Crack");
        crack.transform.SetParent(crackContainer, false);
        crack.transform.localPosition = new Vector3(
            Random.Range(-1.2f, 1.2f),
            Random.Range(-0.7f, 0.7f),
            -0.05f);

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

        // Pile on extra cracks
        for (int i = 0; i < 8; i++)
        {
            SpawnCrack();
            yield return new WaitForSeconds(0.05f);
        }

        // Big shake
        yield return StartCoroutine(ShakeTower(0.32f, 0.6f));

        // Screen goes dark
        if (towerScreenSr != null)
            towerScreenSr.color = new Color(0.08f, 0.04f, 0.04f);

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
            le.preferredWidth = 38;
            le.preferredHeight = 38;

            heartImages.Add(img);
        }
    }

    void UpdateHearts()
    {
        for (int i = 0; i < heartImages.Count; i++)
        {
            heartImages[i].color = (i < health) ? HeartFilled : HeartEmpty;
        }
    }

    void UpdateHUD()
    {
        if (scoreText != null) scoreText.text = "Score: " + score;
        if (waveText != null) waveText.text = "Wave " + (currentWave + 1) + " / " + waves.Length;
        if (comboText != null) comboText.text = combo >= 3 ? combo + "x combo!" : "";
    }

    // =========================================================================
    // Popup feedback
    // =========================================================================

    void SpawnPopup(string text, Vector3 pos, Color col)
    {
        GameObject go = new GameObject("Popup");
        go.transform.position = pos + Vector3.up * 0.8f;

        Canvas c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace;
        go.transform.localScale = new Vector3(0.014f, 0.014f, 1f);
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(220, 60);

        // Add CanvasGroup ONCE here, not in the animation loop (the previous
        // GetComponent<CanvasGroup>() ?? AddComponent<CanvasGroup>() pattern
        // hits Unity's "fake null" gotcha and throws at runtime).
        go.AddComponent<CanvasGroup>();

        GameObject t = new GameObject("T");
        t.transform.SetParent(go.transform, false);
        TextMeshProUGUI tmp = t.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 22;
        tmp.color = col;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        RectTransform rt = t.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
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
    // Wave/end-game
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
        gameOverMessageText.text = "Your computer cracked. Weak passwords like '123456' and 'qwerty' are easy targets — strong, unique passwords keep them out.";

        if (commentator != null)
        {
            commentator.Clear();
            commentator.Say("The tower fell! We'll get them next time, dear.");
        }
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

        if (commentator != null)
        {
            commentator.Clear();
            if (pct >= 0.9f) commentator.Say("Perfect! You saved the tower, dear!");
            else commentator.Say("We did it! Thank you, dear.");
        }
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
            if (_cachedWhite == null) _cachedWhite = MakeFallbackSprite();
            return _cachedWhite;
        }
    }

    private static Sprite MakeFallbackSprite()
    {
        Texture2D tex = new Texture2D(4, 4);
        Color[] c = new Color[16];
        for (int i = 0; i < 16; i++) c[i] = Color.white;
        tex.SetPixels(c);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
    }
}