using UnityEngine;

// ============================================================
//  MinigameTheme
//  Static context written by an interior scene just before
//  loading a minigame scene.  The minigame managers read it
//  at Start() to decide which content set to load.
//
//  Usage (from InteriorDialogueManager.OnPlay or equivalent):
//      MinigameTheme.Set(2);               // world 2
//      SceneManager.LoadScene("EmailSwiper");
//
//  Persisted via PlayerPrefs so it survives the scene load.
//  Cleared automatically once read by a minigame manager.
// ============================================================
public static class MinigameTheme
{
    private const string KEY = "mg_theme_world";

    /// <summary>
    /// Call this BEFORE loading the minigame scene.
    /// worldNumber = 1 ? email theme (default)
    /// worldNumber = 2 ? website / forum theme
    /// </summary>
    public static void Set(int worldNumber)
    {
        PlayerPrefs.SetInt(KEY, worldNumber);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Read by the minigame manager at Start().
    /// Returns the stored world number (default 1 if never set).
    /// Does NOT clear the value — call Clear() after reading if desired.
    /// </summary>
    public static int Get()
    {
        return PlayerPrefs.GetInt(KEY, 1);
    }

    /// <summary>
    /// Optional: call after the minigame has loaded its content
    /// so a direct scene reload doesn't re-read stale data.
    /// </summary>
    public static void Clear()
    {
        PlayerPrefs.DeleteKey(KEY);
        PlayerPrefs.Save();
    }
}