using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Phish Patrol — Tower Defense.
/// WORLD 1: email phishing (bad sender domains, urgency phrases, lookalike URLs)
/// WORLD 2: bad URLs vs safe URLs
/// WORLD 3: smishing phrases vs safe messages
/// WORLD 4: social media scam tactics vs real platforms
/// WORLD 5: combined mix of all themes
/// Commentator removed. Sounds wired. Educational TD taglines.
///
/// CHANGE vs original: World 1 (default case) arrays and tutorial text
/// updated from password-themed to email-phishing-themed.
/// Everything else is UNCHANGED.
/// </summary>
public class TowerDefenseManager : MonoBehaviour
{
    [Header("Game Settings")]
    public int startingHealth = 3;
    public float spearSpeed = 22f;

    [Header("Win Condition")]
    public float surviveDuration = 60f;

    [Header("Sprites — UI")]
    public Sprite heartSprite;
    public Sprite hourglassSprite;

    private static readonly Color NeutralFishColor = new Color(0.55f, 0.82f, 0.95f);

    [Header("Audio")]
    public AudioClip bgmClip;
    public AudioClip spearClip;
    public AudioClip sfxGlassCrack;
    public AudioClip sfxSplash;
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
    public Sprite hangingFishSprite;
    public Sprite[] fishPoolSprites;

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

    // ── Internal ──
    private int score, health, combo, tutStep;
    private bool gameActive, spawning;
    private List<FishUnit> liveFish = new List<FishUnit>();
    private MinigameLivesHUD livesHUD;
    private MinigameTimerHUD timerHUD;
    private float surviveTimeRemaining;
    private bool gameEnded;

    private const int LaneCount = 7;
    private const float LaneSpread = 1.6f;
    private bool[] laneOccupied = new bool[LaneCount];

    private struct NetFishState
    {
        public SpriteRenderer sr;
        public Vector3 center;
        public float phaseX, phaseY, freqX, freqY, ampX, ampY;
    }
    private List<NetFishState> netFishStates = new List<NetFishState>();

    // ── World theme ──
    private int _worldTheme = 1;

    // ── Entry data (label + isScam flag) ──
    struct ED { public string label; public bool isScam; public ED(string l, bool s) { label = l; isScam = s; } }

    // ── WORLD 1: email phishing — teach bad sender domains, urgency, lookalike URLs ──
    // CHANGED from password theme to email theme
    ED[] w1Scams = {
        new ED("paypa1.com",         true),  // '1' replacing 'l' in domain
        new ED("amaz0n-secure.net",  true),  // '0' replacing 'o', wrong TLD
        new ED("no-reply@g00gle.com",true),  // double-zero typo in sender
        new ED("URGENT: Act now!",   true),  // artificial urgency phrase
        new ED("Verify account",     true),  // unsolicited verify request
        new ED("Click link NOW",     true),  // vague pressured CTA
        new ED("Dear Customer,",     true),  // generic greeting — no name
        new ED("apple-id.xyz",       true),  // wrong TLD for Apple
        new ED("You've won $1000",   true),  // prize bait
        new ED("irs-refund.tk",      true),  // fake government + .tk domain
        new ED("Acct on hold!",      true),  // fear tactic
        new ED("Expires 24hrs!",     true),  // fake deadline
        new ED("micros0ft-fix.net",  true),  // typo impersonation
        new ED("Unusual sign-in",    true),  // unsolicited alarm
        new ED("Your pkg delayed",   true),  // parcel bait from unknown sender
        new ED("Free gift — claim",  true),  // prize scam
        new ED("SIN required!",      true),  // government impersonation
        new ED("netflix-bill.net",   true),  // wrong domain for Netflix
        new ED("Confirm password",   true),  // real orgs never ask via email
        new ED("cra-etransfer.tk",   true),  // fake CRA, throwaway domain
    };

    ED[] w1Safes = {
        new ED("orders@amazon.com",   false),  // real Amazon sender domain
        new ED("noreply@spotify.com", false),  // real Spotify sender domain
        new ED("no-reply@uber.com",   false),  // real Uber sender domain
        new ED("Hi [Your Name],",     false),  // personalised greeting
        new ED("canada.ca ✓",        false),  // real government domain
        new ED("paypal.com ✓",       false),  // real PayPal domain
        new ED("Track your order",    false),  // benign delivery update
        new ED("Your receipt is in",  false),  // normal transactional email
        new ED("google.com ✓",       false),  // real Google domain
        new ED("Padlock + HTTPS ✓",   false),  // teach: check for HTTPS
        new ED("Your name used ✓",    false),  // teach: real emails use name
        new ED("No link click ✓",     false),  // teach: log in directly
        new ED("Call # on card ✓",    false),  // teach: verify by phone
        new ED("Reply STOP to opt",   false),  // legit opt-out phrasing
    };

    // ── WORLD 2: URLs — teach how to spot fake vs real domains ──
    ED[] w2BadUrls = {
        new ED("amaz0n.tk",       true),
        new ED("bit.ly/fr33gift", true),
        new ED("login-paypa1.com",true),
        new ED("netflix-verify.net",true),
        new ED("bank-secure.xyz", true),
        new ED("free-iphone.win", true),
        new ED("ebay-support.ru", true),
        new ED("claimprize.tk",   true),
        new ED("micros0ft-fix.com",true),
        new ED("gov-refund.tk",   true),
        new ED("fb-login-help.net",true),
        new ED("apple-id.xyz",    true),
        new ED("post-pkg.ru",     true),
        new ED("winner2024.ml",   true),
        new ED("bank0famerica.com",true),
        new ED("irs-refund.net",  true),
        new ED(".tk = free/fake", true),
        new ED("Lookalike URL!",  true),
        new ED("Extra hyphens!",  true),
        new ED("Typo in name!",   true),
    };

    ED[] w2GoodUrls = {
        new ED("amazon.com",    false),
        new ED("paypal.com",    false),
        new ED("netflix.com",   false),
        new ED("github.com",    false),
        new ED("google.com",    false),
        new ED("reddit.com",    false),
        new ED("rbc.com",       false),
        new ED("canada.ca",     false),
        new ED("apple.com",     false),
        new ED("microsoft.com", false),
        new ED("ebay.com",      false),
        new ED("spotify.com",   false),
        new ED("https:// ✓",   false),
        new ED("Padlock ✓",    false),
    };

    // ── WORLD 3: smishing ──
    ED[] w3BadPhrases = {
        new ED("Unknown sender",  true),
        new ED("Link in text!",   true),
        new ED("Act NOW!!",       true),
        new ED("Free gift claim", true),
        new ED("Verify account",  true),
        new ED("Typos in msg",    true),
        new ED("Call this #",     true),
        new ED("Gift card fee",   true),
        new ED("Wire $ now",      true),
        new ED("Share your OTP",  true),
        new ED("Delivery fee $",  true),
        new ED("Expires soon!",   true),
        new ED("Bank link txt",   true),
        new ED("Win $ reply now", true),
        new ED("Unusual login?",  true),
        new ED("Loose access!",   true),
        new ED("Your $ on hold",  true),
        new ED("Click b4 delete", true),
        new ED("Refund waiting",  true),
        new ED("Shared ur info",  true),
    };

    ED[] w3SafePhrases = {
        new ED("Call me",       false),
        new ED("On my way",     false),
        new ED("Sounds good",   false),
        new ED("See you soon",  false),
        new ED("Thanks!",       false),
        new ED("Running late",  false),
        new ED("Love you 💙",   false),
        new ED("Be there @ 7",  false),
        new ED("lol ok",        false),
        new ED("Confirmed ✓",   false),
        new ED("Reply STOP opt",false),
        new ED("Short code ✓",  false),
        new ED("No links = ✓",  false),
        new ED("Call back # ✓", false),
    };

    // ── WORLD 4: social media ──
    ED[] w4BadPhrases = {
        new ED("Unverified acct", true),
        new ED("DM to claim $",   true),
        new ED("Send crypto now", true),
        new ED("Pay $49 badge",   true),
        new ED("Fake celeb acct", true),
        new ED("Free followers",  true),
        new ED("Acct deleted!",   true),
        new ED("Process fee $",   true),
        new ED("Buy likes $5",    true),
        new ED("Gift card prize", true),
        new ED("Win! DM us",      true),
        new ED("Acct hacked!",    true),
        new ED("Click or lose",   true),
        new ED("#CryptoDouble",   true),
        new ED("ID via DM",       true),
        new ED("New # = safe?",   true),
        new ED("Repost to win",   true),
        new ED("Limited time!",   true),
        new ED("Giveaway = risk", true),
        new ED("Copycat logo",    true),
    };

    ED[] w4SafePhrases = {
        new ED("@YouTube",       false),
        new ED("@NASA",          false),
        new ED("@Wikipedia",     false),
        new ED("New post 📸",    false),
        new ED("@BBCNews",       false),
        new ED("@NatGeo",        false),
        new ED("Story reaction", false),
        new ED("@TED",           false),
        new ED("Comment reply",  false),
        new ED("@Spotify",       false),
        new ED("Tagged you",     false),
        new ED("@GitHub",        false),
        new ED("Blue check ✓",   false),
        new ED("Official page ✓",false),
    };

    // ── WORLD 5: combined mix ──
    ED[] w5Bad = {
        new ED("paypa1.com",     true),  new ED("amaz0n-secure.net",true),  new ED("Dear Customer,", true),
        new ED("amaz0n.tk",      true),  new ED("bit.ly/fr33",      true),  new ED("Wrong TLD .xyz", true),
        new ED("Unknown sender", true),  new ED("Link in text!",    true),  new ED("Gift card fee",  true),
        new ED("Fake celeb DM",  true),  new ED("Send crypto",      true),  new ED("DM to claim $",  true),
        new ED("Act NOW!!",      true),  new ED("Typos = fake",     true),  new ED("Pay $49 badge",  true),
        new ED("Lookalike URL",  true),  new ED("Acct deleted!",    true),  new ED("#CryptoDouble",  true),
        new ED("Verify by text", true),  new ED("Prize expires!",   true),
    };

    ED[] w5Safe = {
        new ED("orders@amazon.com",false), new ED("Your name used ✓",false),
        new ED("amazon.com",   false), new ED("paypal.com",    false), new ED("canada.ca",     false),
        new ED("Call back # ✓",false), new ED("Short code ✓",  false), new ED("No links = ✓",  false),
        new ED("@YouTube",     false), new ED("@NASA",         false), new ED("Blue check ✓",  false),
        new ED("https:// ✓",  false), new ED("Padlock ✓",    false), new ED("Confirmed ✓",   false),
    };

    // Active arrays (set at Start based on theme)
    ED[] scams;
    ED[] safes;

    // Tutorial content per world
    string[] tutTitles;
    string[] tutBodies;

    private const float BaseSpeed = 0.38f;
    private const float BaseInterval = 3.0f;
    private const float ScamRatio = 0.55f;

    // =================================================================
    //  Lifecycle
    // =================================================================

    void Start()
    {
        _worldTheme = MinigameTheme.Get();

        switch (_worldTheme)
        {
            case 2:
                scams = w2BadUrls; safes = w2GoodUrls;
                tutTitles = new[] { "Phish Patrol — URL Edition", "How to play" };
                tutBodies = new[]
                {
                    "Fish carrying URL banners are swimming toward your tower!\n\nSome URLs are dangerous fake sites. Others are real, trusted websites.",
                    "Read the URL on each banner:\n\nBAD URL — typos, wrong TLD (.tk/.xyz), shortened links — SHOOT IT!\nGOOD URL — real, recognisable domain — LET IT through!\n\nTip: 'amaz0n' ≠ 'amazon'. '.tk' = throwaway domain. 'bit.ly' hides the real destination."
                };
                break;
            case 3:
                scams = w3BadPhrases; safes = w3SafePhrases;
                tutTitles = new[] { "Phish Patrol — Smishing Edition", "How to play" };
                tutBodies = new[]
                {
                    "Fish carrying text message banners are swimming toward your tower!\n\nSome banners show smishing red flags. Others show safe text patterns.",
                    "Read the banner on each fish:\n\nSMISHING RED FLAG — SHOOT IT!\nSAFE TEXT PATTERN — LET IT through!\n\nTip: Real orgs use short codes, never ask you to click links, and never request gift cards or OTPs. Unknown senders + urgency = scam."
                };
                break;
            case 4:
                scams = w4BadPhrases; safes = w4SafePhrases;
                tutTitles = new[] { "Phish Patrol — Social Media Edition", "How to play" };
                tutBodies = new[]
                {
                    "Fish carrying social media banners are swimming toward your tower!\n\nSome show fake account tactics and scams. Others show real, safe platforms.",
                    "Read the banner:\n\nSCAM TACTIC — unverified accounts, crypto giveaways, DM prizes, gift card fees — SHOOT IT!\nSAFE PLATFORM — verified accounts, normal interactions — LET IT through!\n\nTip: No legitimate platform will DM you asking for payment, gift cards, or government ID."
                };
                break;
            case 5:
                scams = w5Bad; safes = w5Safe;
                tutTitles = new[] { "Phish Patrol — Final Gauntlet", "How to play" };
                tutBodies = new[]
                {
                    "The final wave! Fish carry banners covering every scam type — emails, URLs, smishing, and social media tactics.",
                    "Apply everything you've learned:\n\nSHOOT phishing emails, fake URLs, smishing tactics, and social scam patterns.\nLET THROUGH real sender domains, safe URLs, safe texts, and verified platforms.\n\nRemember: urgency + pressure + unknown sender = almost always a scam."
                };
                break;
            default:
                // WORLD 1 — email phishing theme (CHANGED from password theme)
                scams = w1Scams; safes = w1Safes;
                tutTitles = new[] { "Phish Patrol — Email Edition", "How to play" };
                tutBodies = new[]
                {
                    "Fish carrying email banners are swimming toward your tower!\n\nSome banners show phishing emails. Others show real, safe emails from legitimate senders.",
                    "Read the banner on each fish:\n\nPHISHING EMAIL — typos in the domain (amaz0n, paypa1), urgency phrases, generic 'Dear Customer' greetings, suspicious links — SHOOT IT!\nSAFE EMAIL — real sender domain, calm tone, uses your name, no suspicious links — LET IT through!\n\nTip: Real companies never email asking you to click a link to verify your account. Always log in directly to the website."
                };
                break;
        }

        tutorialPanel.SetActive(true);
        hudPanel.SetActive(false);
        gameOverPanel.SetActive(false);
        winPanel.SetActive(false);

        // Audio setup
        var audioSources = GetComponents<AudioSource>();
        if (audioSources.Length == 0)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            sfxSource = gameObject.AddComponent<AudioSource>();
        }
        else if (audioSources.Length == 1)
        {
            bgmSource = audioSources[0];
            sfxSource = gameObject.AddComponent<AudioSource>();
        }
        else
        {
            bgmSource = audioSources[0];
            sfxSource = audioSources[1];
        }

        bgmSource.playOnAwake = false;
        bgmSource.loop = true;
        bgmSource.volume = 0.4f;
        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.volume = 0.85f;

        if (bgmClip != null)
        {
            bgmSource.clip = bgmClip;
            bgmSource.Play();
        }

        ShowTutStep(0);
    }

    // ── Audio helpers ──────────────────────────────────────────────
    void PlaySFX(AudioClip clip)
    {
        if (sfxSource == null || clip == null) return;
        sfxSource.PlayOneShot(clip, sfxSource.volume);
    }

    // =================================================================
    //  Update
    // =================================================================

    void Update()
    {
        if (!gameActive) return;
        surviveTimeRemaining -= Time.deltaTime;
        timerHUD?.SetTime(surviveTimeRemaining);
        if (surviveTimeRemaining <= 0f && !gameEnded) WinGame();

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
    //  Tutorial
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
    //  Game start
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
    }

    // =================================================================
    //  Spawn loop
    // =================================================================

    IEnumerator ContinuousSpawn()
    {
        yield return new WaitForSeconds(1.5f);
        while (gameActive && !gameEnded)
        {
            float elapsed = surviveDuration - surviveTimeRemaining;
            float ramp = 1f + (elapsed / surviveDuration) * 1.5f;
            float speed = BaseSpeed * ramp;
            float interval = Mathf.Max(BaseInterval / ramp, 0.8f);

            int lane = GetFreeLane();
            laneOccupied[lane] = true;
            const float TowerY = -0.3f;
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
    //  Interaction
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
        PlaySFX(spearClip);
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
    //  Fish callbacks
    // =================================================================

    public void OnRedFishSpearHit(Vector3 pos)
    {
        combo++; int pts = combo >= 3 ? 15 : 10; score += pts;
        SpawnPopup("+" + pts + (combo >= 3 ? " COMBO!" : ""), pos + Vector3.up,
            combo >= 3 ? new Color(1f, 0.85f, 0.1f) : new Color(0.3f, 1f, 0.5f));
        UpdateHUD();
    }

    public void OnRedFishReachedTower(Vector3 pos, bool wasShot = false)
    {
        combo = 0;
        SpawnPopup(_worldTheme == 2 ? "BAD URL!" : "SCAM!", pos + Vector3.up, new Color(1f, 0.3f, 0.3f));
        TakeDamage();
        UpdateHUD();
    }

    public void OnGreenFishCollected(GameObject fishGO, Sprite fishSpr, bool isDefused = false)
    {
        PlaySFX(sfxSplash);
        SpawnNetFish(fishSpr);
        PlayerProgress.RegisterFish(PlayerProgress.GetRandomNetFishId());
        if (!isDefused) { score += 5; combo++; }
        else { SpawnPopup("DEFUSED!", fishGO.transform.position + Vector3.up, new Color(0.3f, 1f, 0.3f)); }
        UpdateHUD();
    }

    public void OnGreenFishShotEarly(Vector3 pos)
    {
        combo = 0; score = Mathf.Max(0, score - 5);
        string label = _worldTheme == 2 ? "-5 real site!" : "-5 safe!";
        SpawnPopup(label, pos + Vector3.up * 2, new Color(0.9f, 0.4f, 0.1f));
        UpdateHUD();
    }

    public void OnFishRemoved(int laneIdx)
    {
        if (laneIdx >= 0 && laneIdx < LaneCount) laneOccupied[laneIdx] = false;
    }

    // =================================================================
    //  Spawning
    // =================================================================

    private const float TowerY = -0.3f;

    int GetFreeLane()
    {
        for (int i = 0; i < LaneCount; i++) if (!laneOccupied[i]) return i;
        return Random.Range(0, LaneCount);
    }

    void SpawnFish(ED data, float speed, float spawnY, int laneIdx)
    {
        var go = new GameObject("FishUnit");
        go.transform.position = new Vector3(spawnPoint.position.x, spawnY, 0f);
        var col = go.AddComponent<CircleCollider2D>(); col.isTrigger = true; col.radius = 0.45f;
        var rb = go.AddComponent<Rigidbody2D>(); rb.gravityScale = 0;
        var fish = go.AddComponent<FishUnit>();
        fish.Init(data.isScam, data.label, speed, laneIdx, this,
                  towerRoot != null ? towerRoot.position : new Vector3(5f, 0, 0),
                  fishNormalSprite, fishHappySprite, fishPuffedSprite, logSprite,
                  NeutralFishColor);

        if (hangingFishSprite != null)
        {
            var hfGo = new GameObject("HangingFish");
            hfGo.transform.SetParent(go.transform, false);
            hfGo.transform.localPosition = new Vector3(0f, 0.30f, 0.01f);
            hfGo.transform.localScale = new Vector3(0.80f, 0.80f, 1f);
            var hfSR = hfGo.AddComponent<SpriteRenderer>();
            hfSR.sprite = hangingFishSprite;
            hfSR.color = Color.white;
            hfSR.sortingOrder = 5;
        }
        liveFish.Add(fish);
        PlayerProgress.RegisterFish("fish_hanging");
    }

    // =================================================================
    //  Net fish
    // =================================================================

    void SpawnNetFish(Sprite spr)
    {
        var go = new GameObject("NetFish");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = spr != null ? spr : fishNormalSprite;
        sr.color = NeutralFishColor; sr.sortingOrder = 5;
        go.transform.localScale = Vector3.one * 0.35f;
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
    //  Tower damage
    // =================================================================

    void TakeDamage()
    {
        health = Mathf.Max(0, health - 1);
        livesHUD?.LoseLife();
        PlaySFX(sfxGlassCrack);
        SpawnCrack();
        StartCoroutine(ShakeTower(0.20f, 0.35f));
        if (health <= 0) StartCoroutine(BreakSequence());
    }

    IEnumerator ShakeTower(float intensity, float dur)
    {
        if (towerShakeRoot == null) yield break;
        Vector3 orig = towerShakeRoot.localPosition; float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime; float d = 1f - Mathf.Clamp01(t / dur);
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
    //  HUD
    // =================================================================

    void UpdateHUD()
    {
        if (scoreText != null) scoreText.text = "Score: " + score;
        if (waveText != null) waveText.text = "";
        if (comboText != null) comboText.text = combo >= 3 ? combo + "x combo!" : "";
    }

    // =================================================================
    //  Popups
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
        while (t < 1f)
        {
            t += Time.deltaTime * 1.4f; if (go == null) yield break;
            go.transform.position = start + Vector3.up * t * 1.0f;
            if (cg != null) cg.alpha = 1f - Mathf.Clamp01((t - 0.5f) * 2f);
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    // =================================================================
    //  End game
    // =================================================================

    float ComputeAccuracy()
    {
        float maxPossible = 210f;
        return Mathf.Clamp01(score / maxPossible);
    }

    void GameOver()
    {
        if (gameEnded) return;
        gameEnded = true;
        gameActive = false;
        StopAllCoroutines();
        foreach (var f in liveFish) if (f != null) Destroy(f.gameObject);
        liveFish.Clear();

        if (bgmSource != null && bgmSource.isPlaying) bgmSource.Stop();

        PlayerPrefs.SetString("interior_result", "lose");
        PlayerPrefs.Save();

        gameOverPanel.SetActive(true);
        if (gameOverScoreText != null) gameOverScoreText.text = "Score: " + score;
        if (gameOverMessageText != null)
            gameOverMessageText.text = _worldTheme switch
            {
                2 => "A bad URL got through! Remember: typos in domain names, .tk/.xyz endings, and shortened links are red flags. Always check the full domain before clicking.",
                3 => "A smishing tactic got through! Remember: real banks and services never send links by text. Unknown senders + urgency + links = scam.",
                4 => "A social scam got through! Remember: no legitimate platform sends DMs asking for payment, gift cards, or government ID.",
                5 => "The final wave broke through! Review your weak spots — check for typos in emails and URLs, avoid clicking text links, verify accounts have blue checks.",
                _ => "A phishing email got through! Remember: check the sender's domain carefully — 'paypa1.com' is not 'paypal.com'. Real companies never email you to click a link and verify your account."
            };
        PlayerProgress.QueueFromPerformance(ComputeAccuracy());
    }

    void WinGame()
    {
        if (gameEnded) return;
        gameEnded = true;
        gameActive = false;

        if (bgmSource != null && bgmSource.isPlaying) bgmSource.Stop();

        PlayerPrefs.SetString("interior_result", "win");
        PlayerPrefs.Save();

        winPanel.SetActive(true);
        int max = 210;
        if (winScoreText != null) winScoreText.text = score + " / " + max;
        float pct = max > 0 ? (float)score / max : 0;
        if (winStarsText != null)
            winStarsText.text = pct >= 0.9f ? "★ ★ ★" : pct >= 0.6f ? "★ ★" : "★";
        PlayerProgress.QueueFromPerformance(ComputeAccuracy());
    }

    public void OnRetry() { UnityEngine.SceneManagement.SceneManager.LoadScene("TowerDefense"); }
    public void OnReturnToMap()
    {
        string src = PlayerPrefs.GetString("interior_source", "WorldMap");
        if (string.IsNullOrEmpty(src)) src = "WorldMap";
        UnityEngine.SceneManagement.SceneManager.LoadScene(src);
    }

    // =================================================================
    //  White pixel helper
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