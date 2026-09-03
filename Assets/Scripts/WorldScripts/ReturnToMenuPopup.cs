using UnityEngine;
using UnityEngine.SceneManagement;

// ============================================================
//  ReturnToMenuPopup
//  Shown by the fish button on every world map scene. Pauses the
//  game while open (Time.timeScale = 0) and resumes it on close.
//  Kept in its own file — see the note in WorldMapCore.cs for why
//  bundling MonoBehaviours together breaks already-built scenes.
// ============================================================
public class ReturnToMenuPopup : MonoBehaviour
{
    public GameObject overlayRoot;
    public const string MainMenuSceneName = "MainMenu";

    // No coroutines here — Open/Close are instant toggles, so there is
    // nothing that needs WaitForSecondsRealtime. If an entrance/exit fade
    // is ever added, it must use WaitForSecondsRealtime / unscaled time,
    // since Time.timeScale is 0 for the entire time this popup is open.
    public void Open()
    {
        if (overlayRoot != null) overlayRoot.SetActive(true);
        Time.timeScale = 0f;
    }

    public void Close()
    {
        if (overlayRoot != null) overlayRoot.SetActive(false);
        Time.timeScale = 1f;
    }

    public void OnYes()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(MainMenuSceneName);
    }

    public void OnNo() => Close();
}
