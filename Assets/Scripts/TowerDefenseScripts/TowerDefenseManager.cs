using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Phish Patrol — Tower Defense.
/// WORLD 1: weak vs strong passwords (original)
/// WORLD 2: bad URLs vs safe URLs on the log banners
/// Only the data arrays and tutorial text change between worlds.
/// All mechanics are identical.
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

    // ── WORLD 1: passwords ──
    ED[] w1Scams = {
        new ED("password123",true), new ED("123456",  true), new ED("qwerty",   true),
        new ED("iloveyou",  true),  new ED("abc123",  true), new ED("password1",true),
        new ED("111111",    true),  new ED("letmein", true), new ED("welcome",  true),
        new ED("monkey",    true),  new ED("dragon",  true), new ED("master",   true),
        new ED("hello",     true),  new ED("login",   true), new ED("admin",    true),
        new ED("baseball",  true),  new ED("shadow",  true), new ED("trustno1", true),
        new ED("12345678",  true),  new ED("princess",true),
    };

    ED[] w1Safes = {
        new ED("K#9mP!2xL",    false), new ED("Blue$Tree47!",  false),
        new ED("Xq8@nW3!vY",   false), new ED("Maple!Leaf99#", false),
        new ED("T7@kLz!9Rp",   false), new ED("Sun$Rise2024!", false),
        new ED("Wr9#mK!6Lp",   false), new ED("Cat!Rain$42X",  false),
        new ED("Gr@pe!Vine88", false), new ED("Z3br@Dance#7",  false),
        new ED("Moon&Star99#", false), new ED("P@rrot3!Wing",  false),
        new ED("B3eHoney$44!", false), new ED("Night#Sky25!",  false),
    };

    // ── WORLD 2: URLs ──
    ED[] w2BadUrls = {
        new ED("amaz0n-deals.tk",      true),
        new ED("bit.ly/fr33gift",      true),
        new ED("login-paypa1.com",     true),
        new ED("netflix-verify.net",   true),
        new ED("secure-banklogin.xyz", true),
        new ED("free-iphone15.win",    true),
        new ED("ebay-support.ru",      true),
        new ED("tinyurl.com/claimpr1z",true),
        new ED("micros0ft-fix.com",    true),
        new ED("gov-refund-ca.tk",     true),
        new ED("fb-login-help.net",    true),
        new ED("apple-id-verify.xyz",  true),
        new ED("canadapost-pkg.ru",    true),
        new ED("prize-winner2024.ml",  true),
        new ED("bank0famerica.com",    true),
        new ED("irs-refund-now.net",   true),
        new ED("steam-freegame.win",   true),
        new ED("amaZon-gift.tk",       true),
        new ED("paypal-secure-id.ml",  true),
        new ED("cra-refund-now.xyz",   true),
    };

    ED[] w2GoodUrls = {
        new ED("amazon.com",       false),
        new ED("paypal.com",       false),
        new ED("netflix.com",      false),
        new ED("github.com",       false),
        new ED("google.com",       false),
        new ED("reddit.com",       false),
        new ED("rbc.com",          false),
        new ED("canada.ca",        false),
        new ED("apple.com",        false),
        new ED("microsoft.com",    false),
        new ED("ebay.com",         false),
        new ED("spotify.com",      false),
        new ED("youtube.com",      false),
        new ED("wikipedia.org",    false),
    };

    // ── WORLD 3: smishing phrases ──
    ED[] w3BadPhrases = {
        new ED("URGENT!",       true),
        new ED("Click here",    true),
        new ED("You won!",      true),
        new ED("Act NOW",       true),
        new ED("Verify acct",   true),
        new ED("Free gift",     true),
        new ED("Claim prize",   true),
        new ED("Acc suspended", true),
        new ED("Expires soon",  true),
        new ED("Wire money",    true),
        new ED("Gift card pls", true),
        new ED("Confirm NOW",   true),
        new ED("Limited time!", true),
        new ED("WINNER!!",      true),
        new ED("Send $$$",      true),
        new ED("Click link",    true),
        new ED("Refund ready",  true),
        new ED("Reset passwrd", true),
        new ED("Free iPhone",   true),
        new ED("OTP is 9284",   true),
    };

    ED[] w3SafePhrases = {
        new ED("Call me",       false),
        new ED("On my way",     false),
        new ED("Sounds good",   false),
        new ED("See you soon",  false),
        new ED("Thanks!",       false),
        new ED("Running late",  false),
        new ED("Omw now",       false),
        new ED("Love you 💙",   false),
        new ED("Be there @ 7",  false),
        new ED("lol ok",        false),
        new ED("Miss you",      false),
        new ED("Confirmed ✓",   false),
        new ED("No worries",    false),
        new ED("👍 Got it",     false),
    };

    // ── WORLD 4: social media red flags vs safe posts ──
    ED[] w4BadPhrases = {
        new ED("@Meta.Support2", true),
        new ED("#FreeIphone15",  true),
        new ED("@PayPal.Help",   true),
        new ED("DM to claim",    true),
        new ED("Verify now ⚠️",  true),
        new ED("@Insta_Appeal",  true),
        new ED("Send 1 BTC",     true),
        new ED("Get badge $49",  true),
        new ED("@ElonMusk2024",  true),
        new ED("#CryptoDouble",  true),
        new ED("Free followers", true),
        new ED("Acct deleted?",  true),
        new ED("@FB_Rewards24",  true),
        new ED("Buy likes $5",   true),
        new ED("Report appeal",  true),
        new ED("@TikTok.Verify", true),
        new ED("Win $750 DM us", true),
        new ED("Acct hacked!",   true),
        new ED("#GiveawayLive",  true),
        new ED("@Support_Meta",  true),
    };

    ED[] w4SafePhrases = {
        new ED("@YouTube",       false),
        new ED("@NASA",          false),
        new ED("@Wikipedia",     false),
        new ED("Like & share",   false),
        new ED("New post 📸",    false),
        new ED("@BBCNews",       false),
        new ED("Following you",  false),
        new ED("@NatGeo",        false),
        new ED("Story reaction", false),
        new ED("@TED",           false),
        new ED("Comment reply",  false),
        new ED("@Spotify",       false),
        new ED("Tagged you",     false),
        new ED("@GitHub",        false),
    };

    // ── WORLD 5: combined mix of all themes ──
    ED[] w5Bad = {
        new ED("password123",   true), new ED("123456",        true), new ED("letmein",       true),
        new ED("amaz0n.tk",     true), new ED("bit.ly/fr33",   true), new ED("paypa1.net",    true),
        new ED("URGENT!",       true), new ED("Verify acct",   true), new ED("Claim prize",   true),
        new ED("@Meta.Support2",true), new ED("Send 1 BTC",    true), new ED("DM to claim",   true),
        new ED("Free iPhone",   true), new ED("Win $750 DM us",true), new ED("Gift card pls", true),
        new ED("netfl1x.net",   true), new ED("Click link",    true), new ED("#CryptoDouble", true),
        new ED("abc123",        true), new ED("Get badge $49", true),
    };

    ED[] w5Safe = {
        new ED("K#9mP!2xL",    false), new ED("Blue$Tree47!", false),
        new ED("amazon.com",   false), new ED("paypal.com",   false), new ED("github.com",   false),
        new ED("Sounds good",  false), new ED("On my way",    false), new ED("Thanks!",      false),
        new ED("@YouTube",     false), new ED("@NASA",        false), new ED("@Wikipedia",   false),
        new ED("canada.ca",    false), new ED("@Spotify",     false), new ED("Call me",      false),
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
                    "Fish are swimming toward your tower carrying URL banners!\n\nSome URLs are dangerous scam sites. Others are real, trusted websites.",
                    "Read the URL on each fish's banner carefully!\n\nBAD URL (fake domain, weird ending) — SHOOT IT!\n" +
                    "GOOD URL (real, trusted site) — LET IT reach the tower!\n\n" +
                    "Survive 60 seconds to win. Fish get faster over time!\n\nTip: watch for typos like 'amaz0n' or dodgy endings like '.tk' or '.xyz'."
                };
                break;
            case 3:
                scams = w3BadPhrases; safes = w3SafePhrases;
                tutTitles = new[] { "Phish Patrol — Smishing Edition", "How to play" };
                tutBodies = new[]
                {
                    "Fish are swimming toward your tower carrying text message banners!\n\nSome phrases are scam SMS tactics. Others are normal, harmless messages.",
                    "Read the phrase on each fish's banner!\n\nSCAM PHRASE ('URGENT!', 'Click here', 'You won!') — SHOOT IT!\n" +
                    "SAFE PHRASE ('Call me', 'On my way', 'Thanks!') — LET IT reach the tower!\n\n" +
                    "Survive 60 seconds to win. Fish get faster over time!\n\nTip: pressure words, prize claims, and requests to click are all red flags."
                };
                break;
            case 4:
                scams = w4BadPhrases; safes = w4SafePhrases;
                tutTitles = new[] { "Phish Patrol — Social Media Edition", "How to play" };
                tutBodies = new[]
                {
                    "Fish are swimming toward your tower carrying social media banners!\n\nSome are fake accounts and scam tactics. Others are real platforms and safe interactions.",
                    "Read the banner on each fish carefully!\n\nSCAM ACCOUNT/PHRASE ('@Meta.Support2', 'Send 1 BTC', 'DM to claim') — SHOOT IT!\n" +
                    "SAFE PLATFORM/PHRASE ('@YouTube', '@NASA', 'Like & share') — LET IT reach the tower!\n\n" +
                    "Survive 60 seconds to win. Fish get faster over time!\n\nTip: unofficial-looking handles and prize offers are always suspicious."
                };
                break;
            case 5:
                scams = w5Bad; safes = w5Safe;
                tutTitles = new[] { "Phish Patrol — Final Gauntlet", "How to play" };
                tutBodies = new[]
                {
                    "The final wave! Fish carry banners from every kind of scam you've seen — passwords, URLs, smishing phrases, and fake social accounts.",
                    "You know what to do by now!\n\nSHOOT weak passwords, bad URLs, scam phrases, and fake social handles.\n" +
                    "LET THROUGH strong passwords, real URLs, safe messages, and real platforms.\n\n" +
                    "Survive 60 seconds to win. This is the hardest wave yet — stay sharp!"
                };
                break;
            default:
                scams = w1Scams; safes = w1Safes;
                tutTitles = new[] { "Phish Patrol — Tower Defense", "How to play" };
                tutBodies = new[]
                {
                    "Fish carrying log banners are swimming toward your tower!\n\nClick near a fish to shoot it with the tower's speargun.",
                    "Read the password on each fish's log banner!\n\nWEAK password fish (like '123456') — SHOOT THEM!\n" +
                    "STRONG password fish (symbols + numbers) — LET THEM reach the tower!\n\n" +
                    "Survive 60 seconds to win. Fish get faster over time!"
                };
                break;
        }

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

        commentator?.Say(_worldTheme switch
        {
            2 => "Read each URL carefully — shoot the fakes, let the real sites through!",
            3 => "Watch the text message phrases — shoot the scam words, let normal messages through!",
            4 => "Social media is full of fakes — shoot the scam accounts, let the real ones through!",
            5 => "This is the final wave — everything you've learned, all at once. Stay sharp, dear!",
            _ => "Read the label — shoot the weak passwords, let the strong ones reach the tower!"
        });
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
    //  Fish callbacks
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
        SpawnPopup(_worldTheme == 2 ? "BAD URL!" : "BAD FISH!", pos + Vector3.up, new Color(1f, 0.3f, 0.3f));
        TakeDamage();
        if (health > 0) commentator?.SayRandom(new[] { "A fish cracked the tower!", "Keep them back!" });
        UpdateHUD();
    }

    public void OnGreenFishCollected(GameObject fishGO, Sprite fishSpr, bool isDefused = false)
    {
        SpawnNetFish(fishSpr);
        PlayerProgress.RegisterFish(PlayerProgress.GetRandomNetFishId());
        if (!isDefused) { score += 5; combo++; commentator?.SayRandom(new[] { _worldTheme == 2 ? "Safe site reached the tower!" : "A strong password joined the tower!", "Safe and sound!" }); }
        else { SpawnPopup("DEFUSED!", fishGO.transform.position + Vector3.up, new Color(0.3f, 1f, 0.3f)); commentator?.SayRandom(new[] { "The defused fish is swimming happily!", "Look at it go!" }); }
        UpdateHUD();
    }

    public void OnGreenFishShotEarly(Vector3 pos)
    {
        combo = 0; score = Mathf.Max(0, score - 5);
        SpawnPopup(_worldTheme == 2 ? "-5 real site!" : "-5 safe fish!", pos + Vector3.up * 2, new Color(0.9f, 0.4f, 0.1f));
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
            hfGo.transform.localScale = new Vector3(0.40f, 0.40f, 1f);
            var hfSR = hfGo.AddComponent<SpriteRenderer>();
            hfSR.sprite = hangingFishSprite; hfSR.color = Color.white; hfSR.sortingOrder = 5;
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
    //  End game — CHANGED: writes interior_result
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

        // Write loss result so InteriorDialogueManager enters PostLose phase
        PlayerPrefs.SetString("interior_result", "lose");
        PlayerPrefs.Save();

        gameOverPanel.SetActive(true);
        if (gameOverScoreText != null) gameOverScoreText.text = "Score: " + score;
        if (gameOverMessageText != null)
            gameOverMessageText.text = _worldTheme switch
            {
                2 => "A bad URL got through! Watch for typos like 'amaz0n' or shady endings like '.tk' or '.xyz'.\nReal sites use simple, recognisable domains.",
                3 => "A scam phrase got through! Pressure words like 'URGENT', 'Click here', and 'You won' are almost always smishing.\nSafe texts don't demand immediate action.",
                4 => "A scam social post got through! Fake account handles, crypto giveaways, and 'DM to claim' are all red flags.\nReal platforms don't pressure you in DMs.",
                5 => "The final wave broke through! Review your weak spots — passwords, URLs, smishing, and social scams each have their own tells.",
                _ => "Your tower cracked! Weak passwords like '123456' let the bad fish through.\nStrong passwords (symbols + numbers) keep them out."
            };
        commentator?.Say("The tower fell! We'll be stronger next time, dear.");
        PlayerProgress.QueueFromPerformance(ComputeAccuracy());
    }

    void WinGame()
    {
        if (gameEnded) return;
        gameEnded = true;
        gameActive = false;

        // Write win result so InteriorDialogueManager enters PostWin phase
        PlayerPrefs.SetString("interior_result", "win");
        PlayerPrefs.Save();

        winPanel.SetActive(true);
        int max = 210;
        if (winScoreText != null) winScoreText.text = score + " / " + max;
        float pct = max > 0 ? (float)score / max : 0;
        if (winStarsText != null)
            winStarsText.text = pct >= 0.9f ? "★ ★ ★" : pct >= 0.6f ? "★ ★" : "★";
        commentator?.Say(pct >= 0.9f
            ? "Perfect defence! The tower is safe, dear."
            : "We did it! Strong passwords saved the day.");
        PlayerProgress.QueueFromPerformance(ComputeAccuracy());
    }

    // CHANGED: reads interior_source instead of hardcoding "WorldMap"
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