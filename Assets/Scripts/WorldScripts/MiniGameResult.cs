using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Call MinigameResult.Win() or MinigameResult.Lose() from your
/// minigame manager's result handler INSTEAD of (or just before)
/// loading a scene. This writes the result into PlayerPrefs so
/// InteriorDialogueManager knows which dialogue branch to show.
///
/// The interior scene name is read from "interior_source" which
/// InteriorDialogueManager writes before launching the minigame.
/// Falls back to "WorldMap" if the key is missing.
///
/// USAGE in EmailSwiperManager:
///   // In OnReturnToMap or result panel "Back" button:
///   MinigameResult.Win();     // if player completed successfully
///   MinigameResult.Lose();    // if boat sank / time ran out
/// </summary>
public static class MinigameResult
{
    const string ResultKey = "interior_result";
    const string SourceKey = "interior_source";

    /// Call when the player beats the minigame.
    public static void Win()
    {
        PlayerPrefs.SetString(ResultKey, "win");
        PlayerPrefs.Save();
        ReturnToInterior();
    }

    /// Call when the player fails the minigame.
    public static void Lose()
    {
        PlayerPrefs.SetString(ResultKey, "lose");
        PlayerPrefs.Save();
        ReturnToInterior();
    }

    static void ReturnToInterior()
    {
        string source = PlayerPrefs.GetString(SourceKey, "WorldMap");
        // Don't delete the source key yet — InteriorDialogueManager
        // may need to re-launch the minigame from the retry panel.
        SceneManager.LoadScene(source);
    }
}