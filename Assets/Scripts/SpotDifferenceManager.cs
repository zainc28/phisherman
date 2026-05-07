using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Runtime manager for the Spot the Difference minigame.
/// Tracks score, found markers, and shows the result screen when all
/// red flags are clicked.
///
/// Wiring is done by SpotDifferenceBuilder (Editor) when the scene is
/// generated. The marker list is populated at build time and re-validated
/// at runtime in Start() as a fallback.
/// </summary>
public class SpotDifferenceManager : MonoBehaviour
{
    [Header("Scoring")]
    public int correctPoints = 100;
    public int wrongPenalty = 25;
    public int streakBonus = 25;

    [Header("HUD")]
    public TMP_Text scoreText;
    public TMP_Text counterText;
    public TMP_Text feedbackText;

    [Header("Result Screen")]
    public GameObject resultPanel;
    public TMP_Text resultTitle;
    public TMP_Text resultBreakdown;
    public TMP_Text resultScore;

    [Header("Markers (auto-populated by builder)")]
    public List<DifferenceMarker> markers = new List<DifferenceMarker>();

    [Header("Commentator")]
    public Commentator commentator;

    private int score;
    private int found;
    private int total;
    private int streak;
    private bool gameOver;

    void Start()
    {
        // Fallback: if the builder didn't pre-populate the list, find them now.
        if (markers == null || markers.Count == 0)
        {
            markers = new List<DifferenceMarker>(
                FindObjectsByType<DifferenceMarker>(FindObjectsSortMode.None));
        }

        // Make sure every marker has a back-reference to us
        foreach (var m in markers)
        {
            if (m != null) m.manager = this;
        }

        total = markers.Count;
        UpdateHud();
        HideFeedback();

        if (resultPanel != null) resultPanel.SetActive(false);

        if (commentator != null)
        {
            commentator.Say($"Find all {total} red flags, dear. Look closely!");
        }
    }

    /// <summary>Called by a DifferenceMarker when the player clicks it for the first time.</summary>
    public void OnDifferenceFound(DifferenceMarker marker)
    {
        if (gameOver) return;

        found++;
        streak++;
        int gain = correctPoints + Mathf.Max(0, streak - 1) * streakBonus;
        score += gain;

        ShowFeedback($"+{gain}   {marker.flagName}", new Color(0.18f, 0.62f, 0.20f));
        UpdateHud();

        if (commentator != null)
        {
            int remaining = total - found;
            if (remaining == 0)
                commentator.Say("That's all of them — wonderful!");
            else if (remaining == 1)
                commentator.Say("Just one more, dear!");
            else if (streak >= 3)
                commentator.Say("You're catching them so quickly!");
            else
                commentator.SayRandom(new[] {
                    "Yes! That's a red flag.",
                    "Good eye, dear.",
                    "I see it now too!"
                });
        }

        if (found >= total)
        {
            gameOver = true;
            Invoke(nameof(ShowResult), 0.7f);
        }
    }

    public void OnWrongClick()
    {
        if (gameOver) return;

        score = Mathf.Max(0, score - wrongPenalty);
        streak = 0;

        ShowFeedback($"?{wrongPenalty}   Not a red flag", new Color(0.78f, 0.20f, 0.20f));
        UpdateHud();

        if (commentator != null)
        {
            commentator.SayRandom(new[] {
                "Hmm, look more carefully there.",
                "Not quite — keep searching.",
                "That part looks normal to me."
            });
        }
    }

    private void UpdateHud()
    {
        if (scoreText != null) scoreText.text = $"Score: {score}";
        if (counterText != null) counterText.text = $"Found: {found} / {total}";
    }

    private void ShowFeedback(string text, Color color)
    {
        if (feedbackText == null) return;
        feedbackText.text = text;
        feedbackText.color = color;
        CancelInvoke(nameof(HideFeedback));
        Invoke(nameof(HideFeedback), 1.1f);
    }

    private void HideFeedback()
    {
        if (feedbackText != null) feedbackText.text = string.Empty;
    }

    private void ShowResult()
    {
        if (resultPanel == null) return;
        resultPanel.SetActive(true);

        if (resultTitle != null)
            resultTitle.text = $"You spotted all {total} red flags!";

        if (resultScore != null)
            resultScore.text = $"Final Score: {score}";

        if (resultBreakdown != null)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < markers.Count; i++)
            {
                var m = markers[i];
                if (m == null) continue;
                sb.Append("<b>").Append(i + 1).Append(". ").Append(m.flagName).AppendLine("</b>");
                sb.Append("<size=80%>").Append(m.explanation).AppendLine("</size>");
                sb.AppendLine();
            }
            resultBreakdown.text = sb.ToString();
        }
    }

    // === Buttons on the result panel ===

    public void PlayAgain()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void BackToWorldMap()
    {
        // If WorldMap isn't in build settings yet, this will throw a clear error.
        SceneManager.LoadScene("WorldMap");
    }
}