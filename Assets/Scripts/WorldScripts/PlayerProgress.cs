using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  PlayerProgress
//  Static persistence layer backed by PlayerPrefs.
//  Tracks: total XP, level, discovered fish (sticker book).
//
//  POINT NORMALIZATION:
//    Every minigame awards XP based on accuracy percentage.
//    Perfect game ≈ 500 XP regardless of which minigame.
//    This keeps leveling fair across all three games.
//
//  LEVEL CURVE:
//    Total XP for level N = 100 * (N-1) * (N+1)
//    Lv1=0  Lv2=300  Lv3=800  Lv4=1500  Lv5=2400 ...
//    Each subsequent level needs 200 more XP than the last.
// ============================================================
public static class PlayerProgress
{
    // ── PlayerPrefs keys ──
    const string XP_KEY = "pp_total_xp";
    const string PENDING_KEY = "pp_pending_xp";
    const string FISH_KEY = "pp_discovered_fish";

    /// <summary>Max XP a single perfect minigame run can award.</summary>
    public const int MaxXPPerGame = 500;

    // =================================================================
    // XP / Level
    // =================================================================

    public static int GetTotalXP() => PlayerPrefs.GetInt(XP_KEY, 0);

    /// <summary>Cumulative XP needed to REACH level N.</summary>
    public static int XPForLevel(int level)
    {
        if (level <= 1) return 0;
        // Lv2=300, Lv3=800, Lv4=1500, Lv5=2400 …
        return 100 * (level - 1) * (level + 1);
    }

    public static int GetLevel()
    {
        int xp = GetTotalXP();
        int level = 1;
        while (XPForLevel(level + 1) <= xp) level++;
        return level;
    }

    /// <summary>XP earned inside the current level (for bar fill).</summary>
    public static int GetXPInCurrentLevel()
        => GetTotalXP() - XPForLevel(GetLevel());

    /// <summary>XP span of the current level bracket.</summary>
    public static int GetXPNeededForNextLevel()
    {
        int lv = GetLevel();
        return XPForLevel(lv + 1) - XPForLevel(lv);
    }

    /// <summary>0-1 fill fraction for the XP bar.</summary>
    public static float GetXPProgress01()
    {
        int needed = GetXPNeededForNextLevel();
        return needed <= 0 ? 1f : Mathf.Clamp01((float)GetXPInCurrentLevel() / needed);
    }

    /// <summary>
    /// Directly add XP (used by the HUD after animation).
    /// Returns the level BEFORE the addition so callers can detect a level-up.
    /// </summary>
    public static int AddXP(int amount)
    {
        int oldLevel = GetLevel();
        PlayerPrefs.SetInt(XP_KEY, GetTotalXP() + amount);
        PlayerPrefs.Save();
        return oldLevel;
    }

    // ── Pending XP (minigame → world map hand-off) ──

    /// <summary>
    /// Called by minigame managers at game-end.
    /// Stores the XP to award WITHOUT adding it yet — the world-map
    /// HUD will read this, animate the bar, then commit via AddXP().
    /// </summary>
    public static void QueueXP(int amount)
    {
        int existing = PlayerPrefs.GetInt(PENDING_KEY, 0);
        PlayerPrefs.SetInt(PENDING_KEY, existing + amount);
        PlayerPrefs.Save();
    }

    public static int GetPendingXP() => PlayerPrefs.GetInt(PENDING_KEY, 0);

    public static void ClearPendingXP()
    {
        PlayerPrefs.DeleteKey(PENDING_KEY);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Convenience: convert a 0-1 accuracy percentage to XP and queue it.
    /// </summary>
    public static void QueueFromPerformance(float accuracy01)
    {
        int xp = Mathf.RoundToInt(Mathf.Clamp01(accuracy01) * MaxXPPerGame);
        QueueXP(xp);
    }

    // =================================================================
    // Fish Sticker Book
    // =================================================================

    public static void RegisterFish(string fishId)
    {
        var set = GetDiscoveredFishSet();
        if (set.Contains(fishId)) return;
        set.Add(fishId);
        SaveFishSet(set);
    }

    public static bool IsFishDiscovered(string fishId)
        => GetDiscoveredFishSet().Contains(fishId);

    public static HashSet<string> GetDiscoveredFishSet()
    {
        string raw = PlayerPrefs.GetString(FISH_KEY, "");
        var set = new HashSet<string>();
        if (string.IsNullOrEmpty(raw)) return set;
        foreach (var id in raw.Split(','))
            if (!string.IsNullOrEmpty(id)) set.Add(id);
        return set;
    }

    static void SaveFishSet(HashSet<string> set)
    {
        PlayerPrefs.SetString(FISH_KEY, string.Join(",", set));
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Returns the catalog ID of a random fish from the pool of 20 (fish_1–fish_20).
    /// Used by EmailSwiper and Tower Defense when a fish lands in a net so each
    /// catch adds a different sticker.
    /// </summary>
    public static string GetRandomNetFishId()
    {
        // Pool is the first 20 entries in the catalog (fish_1 through fish_20)
        int max = Mathf.Min(20, FishCatalog.Length);
        int idx = UnityEngine.Random.Range(0, max);
        return FishCatalog[idx].id;
    }

    /// <summary>
    /// Looks up the runtime Sprite for a given fish ID from a pre-built
    /// dictionary (keyed by catalog ID). Returns null if not found.
    /// </summary>
    public static UnityEngine.Sprite GetFishSprite(
        string fishId,
        System.Collections.Generic.Dictionary<string, UnityEngine.Sprite> spriteMap)
    {
        if (spriteMap == null) return null;
        spriteMap.TryGetValue(fishId, out var spr);
        return spr;
    }

    // ── Fish catalog ──

    public struct FishEntry
    {
        public string id;           // maps to PlayerPrefs key
        public string spriteName;   // exact filename in Assets/Sprites/Minigames/
        public string name;
        public string description;
        public Color tintWhenGreyed; // color used as fallback if sprite missing
        public FishEntry(string id, string spriteName, string name, string desc, Color tint)
        { this.id = id; this.spriteName = spriteName; this.name = name; this.description = desc; this.tintWhenGreyed = tint; }
    }

    public static readonly FishEntry[] FishCatalog =
    {
        // ── EmailSwiper pool (fish_1 – fish_20) ──────────────────────────
        new FishEntry("fish_1",  "fish_1_clownfish_normal",  "Clownfish",         "A cheerful clownfish who swims straight and true.",                          new Color(0.95f, 0.55f, 0.20f)),
        new FishEntry("fish_2",  "fish_2_clownfish_happy",   "Happy Clownfish",   "Smiling wide — another scam stopped in its tracks!",                        new Color(0.95f, 0.75f, 0.25f)),
        new FishEntry("fish_3",  "fish_3_clownfish_mad",     "Angry Clownfish",   "Furious at weak passwords. Watch the fins!",                                 new Color(0.85f, 0.25f, 0.20f)),
        new FishEntry("fish_4",  "fish_4_blueflowy",         "Blue Flowy",        "Graceful fins trail behind like a silk scarf in water.",                     new Color(0.35f, 0.60f, 0.95f)),
        new FishEntry("fish_5",  "fish_5_spotted",           "Spotted Fish",      "Covered in dots — each one a scam it spotted and dodged.",                   new Color(0.70f, 0.90f, 0.50f)),
        new FishEntry("fish_6",  "fish_6_green",             "Green Dart",        "Fast and slippery — like the phishing links it outruns.",                    new Color(0.25f, 0.80f, 0.40f)),
        new FishEntry("fish_7",  "fish_7_tuna",              "Tuna",              "A sturdy tuna. Built tough enough to ignore spam.",                           new Color(0.40f, 0.55f, 0.80f)),
        new FishEntry("fish_8",  "fish_8_sandy",             "Sandy Fish",        "Camouflages itself — just like a convincing scam email.",                    new Color(0.85f, 0.78f, 0.55f)),
        new FishEntry("fish_9",  "fish_9_blueyellowstripe",  "Striped Fish",      "Bold blue-and-yellow stripes. Hard to miss. Harder to fool.",                new Color(0.30f, 0.70f, 0.95f)),
        new FishEntry("fish_10", "fish_10_spiky",            "Spiky Fish",        "Handle with care — almost as sharp as its eye for red flags.",               new Color(0.55f, 0.80f, 0.75f)),
        new FishEntry("fish_11", "fish_11_wierddory",        "Weird Dory",        "Looks familiar… in a slightly off way. Just like a spoofed email.",          new Color(0.30f, 0.65f, 0.90f)),
        new FishEntry("fish_12", "fish_12_dory",             "Dory",              "Clear-eyed and confident — knows a fake sender domain when it sees one.",    new Color(0.25f, 0.55f, 0.85f)),
        new FishEntry("fish_13", "fish_13_findingnemounclefish", "Uncle Fish",    "Wise old uncle of the reef. Has seen every scam twice.",                     new Color(0.90f, 0.65f, 0.30f)),
        new FishEntry("fish_14", "fish_14_longsalmonthing",  "Long Salmon",       "Stretches the length of your inbox. Misses nothing.",                        new Color(0.85f, 0.40f, 0.40f)),
        new FishEntry("fish_15", "fish_15_spottedsharkthing","Spotted Shark-ish", "Not quite a shark — but it bites back at phishing attempts.",                new Color(0.65f, 0.80f, 0.70f)),
        new FishEntry("fish_16", "fish_16_chameleon_thingy", "Chameleon Fish",    "Changes colour with every email batch. Always adapting.",                    new Color(0.70f, 0.50f, 0.85f)),
        new FishEntry("fish_17", "fish_17_whiteandredcirclefish", "Circle Fish",  "Round and bold — like a warning stamp on a scam.",                           new Color(0.90f, 0.30f, 0.35f)),
        new FishEntry("fish_18", "fish_18_piranha",          "Piranha",           "Snaps at weak passwords instantly. Don't use '123456' near this one.",       new Color(0.75f, 0.20f, 0.20f)),
        new FishEntry("fish_19", "fish_19_rainbow",          "Rainbow Fish",      "Every colour of the spectrum — just like the range of scams it knows.",      new Color(0.90f, 0.75f, 0.40f)),
        new FishEntry("fish_20", "fish_20_black",            "Shadow Fish",       "Lurks in the deep — silent guardian against credential theft.",              new Color(0.35f, 0.35f, 0.45f)),
        // ── Tower Defense special fish ────────────────────────────────────
        new FishEntry("fish_hanging", "td_hanging_fish",     "Hanging Fish",      "Dangles bravely from the log banner. Still carrying the warning.",           new Color(0.55f, 0.75f, 0.90f)),
        new FishEntry("fish_shark",   "shark",               "Shark",             "The boss of the reef. Circles anything with a weak password.",               new Color(0.45f, 0.55f, 0.70f)),
        new FishEntry("fish_puffer",  "pufferfish_1_default","Pufferfish",        "Puffs up with anger every time a wrong answer rocks the boat.",              new Color(0.95f, 0.45f, 0.35f)),
    };
}