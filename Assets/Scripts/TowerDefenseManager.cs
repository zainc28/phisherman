using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class TowerDefenseManager : MonoBehaviour
{
    [Header("Scene References")]
    public Transform spawnPoint;
    public Transform towerTransform;
    public SpriteRenderer towerSprite;

    [Header("Panels")]
    public GameObject tutorialPanel;
    public GameObject hudPanel;
    public GameObject gameOverPanel;
    public GameObject winPanel;

    [Header("Tutorial UI")]
    public TextMeshProUGUI tutorialTitleText;
    public TextMeshProUGUI tutorialBodyText;
    public TextMeshProUGUI tutorialStepText;
    public Button tutorialNextButton;
    public TextMeshProUGUI tutorialNextButtonText;

    [Header("HUD UI")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI waveText;
    public TextMeshProUGUI healthText;
    public TextMeshProUGUI comboText;

    [Header("Game Over UI")]
    public TextMeshProUGUI gameOverScoreText;
    public TextMeshProUGUI gameOverMessageText;

    [Header("Win UI")]
    public TextMeshProUGUI winScoreText;
    public TextMeshProUGUI winStarsText;

    // State
    private int score, health, combo, tutStep, currentWave, enemiesRemaining;
    private bool gameActive, spawning;
    private List<EnemyEmail> liveEnemies = new List<EnemyEmail>();

    struct ED { public string label; public bool isScam; public ED(string l, bool s) { label = l; isScam = s; } }

    ED[] scams = {
        new ED("paypa1.com", true),
        new ED("cra-refund.net", true),
        new ED("micros0ft-help.com", true),
        new ED("secure-bankofcanada.net", true),
        new ED("amazon-verify.info", true),
        new ED("netflix-billing-alert.com", true),
        new ED("irs-gov-refund.com", true),
        new ED("support@paypa1.com", true),
    };

    ED[] safes = {
        new ED("spotify.com", false),
        new ED("amazon.com", false),
        new ED("uber.com", false),
        new ED("gmail.com", false),
        new ED("linkedin.com", false),
        new ED("apple.com", false),
        new ED("orders@amazon.com", false),
        new ED("no-reply@uber.com", false),
    };

    struct Wave
    {
        public int count; public float scamRatio, speed, interval;
        public Wave(int c, float r, float s, float i) { count = c; scamRatio = r; speed = s; interval = i; }
    }

    Wave[] waves = {
        new Wave(6,  0.5f, 1.8f, 1.4f),
        new Wave(8,  0.5f, 2.4f, 1.1f),
        new Wave(10, 0.6f, 3.0f, 0.9f),
    };

    string[] tutTitles = { "Phish Patrol: Tower Defense", "Your mission" };
    string[] tutBodies = {
        "Scam emails are heading for your tower!\n\nClick them to destroy them before they arrive.\n\nBe careful — safe emails are mixed in. Clicking a safe one by mistake costs you health.",
        "Read the sender domain quickly and decide:\n\nFAKE: paypa1.com, cra-refund.net, micros0ft-help.com\nSAFE: spotify.com, amazon.com, uber.com\n\nYour tower has 5 health. Do not let it reach zero."
    };

    void Start()
    {
        tutorialPanel.SetActive(true);
        hudPanel.SetActive(false);
        gameOverPanel.SetActive(false);
        winPanel.SetActive(false);

        // Wire buttons at runtime — avoids duplicate listener issues from builder
        tutorialNextButton.onClick.RemoveAllListeners();
        tutorialNextButton.onClick.AddListener(OnTutorialNext);

        ShowTutStep(0);
    }

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

    void StartGame()
    {
        score = 0; health = 5; combo = 0; currentWave = 0; gameActive = true;
        UpdateHUD();
        StartCoroutine(WaveDelay(1.5f));
    }

    IEnumerator WaveDelay(float d) { yield return new WaitForSeconds(d); StartCoroutine(SpawnWave(waves[currentWave])); }

    IEnumerator SpawnWave(Wave w)
    {
        spawning = true;
        waveTMP().text = "Wave " + (currentWave + 1) + " / " + waves.Length;
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

    TextMeshProUGUI waveTMP() => waveText;

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

    void SpawnEnemy(ED data, float speed)
    {
        GameObject go = new GameObject("Enemy");
        go.transform.position = new Vector3(spawnPoint.position.x, Random.Range(-1.8f, 1.8f), 0f);
        go.transform.localScale = new Vector3(2.8f, 1.0f, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = MakeSprite();
        sr.color = new Color(0.95f, 0.92f, 1f);
        sr.sortingOrder = 1;

        go.AddComponent<BoxCollider2D>().size = Vector2.one;

        // World space label
        GameObject cGO = new GameObject("LabelCanvas");
        cGO.transform.SetParent(go.transform, false);
        Canvas c = cGO.AddComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace;
        cGO.transform.localScale = new Vector3(0.011f, 0.011f, 1f);
        cGO.GetComponent<RectTransform>().sizeDelta = new Vector2(220, 80);

        GameObject tGO = new GameObject("Label");
        tGO.transform.SetParent(cGO.transform, false);
        TextMeshProUGUI tmp = tGO.AddComponent<TextMeshProUGUI>();
        tmp.text = data.label;
        tmp.fontSize = 18;
        tmp.color = new Color(0.1f, 0.05f, 0.25f);
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        RectTransform tRT = tGO.GetComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
        tRT.offsetMin = tRT.offsetMax = Vector2.zero;

        EnemyEmail enemy = go.AddComponent<EnemyEmail>();
        enemy.Init(data.isScam, data.label, speed, this, towerTransform.position);
        liveEnemies.Add(enemy);
    }

    Sprite MakeSprite()
    {
        Texture2D t = new Texture2D(4, 4);
        Color[] c = new Color[16]; for (int i = 0; i < 16; i++) c[i] = Color.white;
        t.SetPixels(c); t.Apply();
        return Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
    }

    void Update()
    {
        if (!gameActive) return;
        if (Input.GetMouseButtonDown(0))
        {
            Vector2 wp = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Collider2D hit = Physics2D.OverlapPoint(wp);
            if (hit != null) { EnemyEmail e = hit.GetComponent<EnemyEmail>(); if (e != null) e.GetClicked(); }
        }
    }

    public void OnScamDestroyed(Vector3 pos)
    {
        combo++; int pts = combo >= 3 ? 15 : 10; score += pts;
        SpawnPopup("+" + pts, pos, new Color(0.1f, 0.9f, 0.3f));
        EnemyGone(); UpdateHUD();
    }

    public void OnSafeDestroyed(Vector3 pos)
    {
        combo = 0; TakeDamage();
        SpawnPopup("Safe email!", pos, new Color(0.9f, 0.2f, 0.2f));
        EnemyGone(); UpdateHUD();
    }

    public void OnScamReachedTower() { combo = 0; TakeDamage(); EnemyGone(); UpdateHUD(); }

    void TakeDamage()
    {
        health--;
        if (towerSprite != null) StartCoroutine(FlashTower());
        if (health <= 0) { health = 0; GameOver(); }
    }

    IEnumerator FlashTower()
    {
        towerSprite.color = new Color(1f, 0.2f, 0.2f);
        yield return new WaitForSeconds(0.25f);
        ColorUtility.TryParseHtmlString("#4A90F0", out Color c);
        towerSprite.color = c;
    }

    void EnemyGone()
    {
        enemiesRemaining--;
        liveEnemies.RemoveAll(e => e == null);
        if (!spawning && enemiesRemaining <= 0)
        {
            currentWave++;
            if (currentWave >= waves.Length) WinGame();
            else StartCoroutine(WaveDelay(2f));
        }
    }

    void UpdateHUD()
    {
        scoreText.text = "Score: " + score;
        waveText.text = "Wave " + (currentWave + 1) + " / " + waves.Length;
        string h = ""; for (int i = 0; i < 5; i++) h += i < health ? "♥ " : "♡ ";
        healthText.text = h.Trim();
        comboText.text = combo >= 3 ? combo + "x combo!" : "";
    }

    void SpawnPopup(string text, Vector3 pos, Color col)
    {
        GameObject go = new GameObject("Popup");
        go.transform.position = pos + Vector3.up * 0.5f;
        Canvas c = go.AddComponent<Canvas>(); c.renderMode = RenderMode.WorldSpace;
        go.transform.localScale = new Vector3(0.014f, 0.014f, 1f);
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 60);
        GameObject t = new GameObject("T"); t.transform.SetParent(go.transform, false);
        TextMeshProUGUI tmp = t.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = 22; tmp.color = col;
        tmp.fontStyle = FontStyles.Bold; tmp.alignment = TextAlignmentOptions.Center;
        RectTransform rt = t.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        StartCoroutine(PopupAnim(go));
    }

    IEnumerator PopupAnim(GameObject go)
    {
        float t = 0; Vector3 s = go.transform.position;
        while (t < 1f)
        {
            t += Time.deltaTime * 1.5f; if (go == null) yield break;
            go.transform.position = s + Vector3.up * t * 1.2f;
            CanvasGroup cg = go.GetComponent<CanvasGroup>() ?? go.AddComponent<CanvasGroup>();
            cg.alpha = 1f - Mathf.Clamp01((t - 0.5f) * 2f);
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    void GameOver()
    {
        gameActive = false; StopAllCoroutines();
        foreach (var e in liveEnemies) if (e != null) Destroy(e.gameObject);
        liveEnemies.Clear();
        gameOverPanel.SetActive(true);
        gameOverScoreText.text = "Score: " + score;
        gameOverMessageText.text = "The tower fell. Study the sender domains and try again.";
    }

    void WinGame()
    {
        gameActive = false; winPanel.SetActive(true);
        int max = 0; foreach (var w in waves) max += Mathf.RoundToInt(w.count * w.scamRatio) * 10;
        winScoreText.text = score + " / " + max;
        float pct = (float)score / max;
        winStarsText.text = pct >= 0.9f ? "* * *" : pct >= 0.6f ? "* *" : "*";
    }

    public void OnRetry() { SceneManager.LoadScene("TowerDefense"); }
    public void OnReturnToMap() { SceneManager.LoadScene("WorldMap"); }
}