using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Handles the bottom-corner world transition CTA badge.
/// Checks WorldProgress before loading the next world.
/// Also acts as a proximity trigger: if the player walks into the
/// corner zone, it fires the same logic as clicking the badge.
/// </summary>
public class WorldTransitionCTA : MonoBehaviour
{
    [Header("Navigation")]
    public Transform player;
    public string targetScene = "";
    public int requiresWorldComplete = 1;
    public float triggerRadius = 1.2f;

    [Header("Locked popup")]
    public GameObject lockedPopup;
    public string lockedMessage = "Finish this neighbourhood first before moving on!";

    [Header("Badge visuals (wired by WorldMapBuilder)")]
    public TextMeshProUGUI badgeLabel;
    public Image badgeBorderImage;
    public Color activeColor = Color.white;
    public Color completedColor = new Color(0.5f, 0.5f, 0.5f, 0.4f);

    bool _fired;

    void Start()
    {
        RefreshVisuals();
    }

    void Update()
    {
        RefreshVisuals();

        if (_fired || player == null) return;
        float dist = Vector2.Distance(transform.position, player.position);
        if (dist < triggerRadius)
            TryTravel();
    }

    void RefreshVisuals()
    {
        bool unlocked = IsUnlocked();
        if (badgeLabel != null)
            badgeLabel.color = unlocked ? activeColor : completedColor;
        if (badgeBorderImage != null)
            badgeBorderImage.color = unlocked ? activeColor : completedColor;
    }

    bool IsUnlocked()
    {
        if (requiresWorldComplete <= 0) return true;
        return WorldProgress.IsWorldComplete(requiresWorldComplete);
    }

    public void OnBadgeClick()
    {
        TryTravel();
    }

    void TryTravel()
    {
        if (!IsUnlocked())
        {
            ShowLockedPopup();
            return;
        }
        if (string.IsNullOrEmpty(targetScene)) return;
        _fired = true;
        SceneManager.LoadScene(targetScene);
    }

    void ShowLockedPopup()
    {
        if (lockedPopup == null) return;
        var ctrl = lockedPopup.GetComponent<LockedPopupController>();
        if (ctrl != null)
            ctrl.Show(lockedMessage);
        else
            lockedPopup.SetActive(true);
    }
}