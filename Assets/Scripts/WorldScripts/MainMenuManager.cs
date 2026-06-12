using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    public void OnStoryModeClicked()
    {
        SceneManager.LoadScene("WorldMap");
    }

    public void OnArcadeModeClicked()
    {
        // Arcade mode locked for now - will unlock after story complete
        Debug.Log("Arcade Mode coming soon");
    }
}