using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Speech-bubble commentary widget. Drop it into any minigame scene and
/// have the manager call <c>Say(line)</c> at key moments — start, score
/// gains, life lost, streaks, win/lose, etc.
///
/// Lines are queued so they don't overlap. Each line shows for
/// <c>defaultDuration</c> seconds, then fades out and the next line
/// (if any) appears.
///
/// Wiring is done by the scene's builder. The minigame manager keeps a
/// reference to the Commentator and calls Say() — that's it.
/// </summary>
public class Commentator : MonoBehaviour
{
    [Header("Speaker")]
    public string speakerName = "Grandma";
    public Color portraitColor = new Color(1f, 0.72f, 0.78f);
    public string portraitInitial = "G";
    public Sprite portraitSprite;     // optional circle (assigned by builder)

    [Header("UI Refs (wired by builder)")]
    public GameObject root;           // entire bubble panel
    public Image portrait;
    public TMP_Text portraitLetter;
    public Image bubbleBg;
    public TMP_Text bubbleText;
    public TMP_Text speakerLabel;

    [Header("Timing")]
    public float defaultDuration = 2.6f;
    public float gapBetweenLines = 0.18f;

    private readonly Queue<(string line, float duration)> queue = new Queue<(string, float)>();
    private Coroutine activeRoutine;
    private CanvasGroup rootCg;

    void Awake()
    {
        if (root != null)
        {
            rootCg = root.GetComponent<CanvasGroup>();
            if (rootCg == null) rootCg = root.AddComponent<CanvasGroup>();
            // Hide via alpha rather than SetActive — Commentator lives on
            // the root GameObject, so deactivating it would also stop the
            // coroutines we need to fire when Say() is called later.
            rootCg.alpha = 0f;
            rootCg.interactable = false;
            rootCg.blocksRaycasts = false;
        }

        if (portrait != null && portraitSprite != null) portrait.sprite = portraitSprite;
        if (portrait != null) portrait.color = portraitColor;
        if (portraitLetter != null) portraitLetter.text = portraitInitial;
        if (speakerLabel != null) speakerLabel.text = speakerName;
    }

    /// <summary>
    /// Queue a line of commentary. Call this from your minigame manager
    /// at any meaningful moment.
    /// </summary>
    public void Say(string line, float duration = -1f)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        if (duration <= 0f) duration = defaultDuration;
        queue.Enqueue((line, duration));
        if (activeRoutine == null)
        {
            activeRoutine = StartCoroutine(SpeakLoop());
        }
    }

    /// <summary>
    /// Pick a random line from a list. Useful for variety on the same
    /// trigger (e.g. multiple "wrong answer" lines).
    /// </summary>
    public void SayRandom(string[] lines, float duration = -1f)
    {
        if (lines == null || lines.Length == 0) return;
        Say(lines[Random.Range(0, lines.Length)], duration);
    }

    /// <summary>
    /// Drop any queued lines and hide immediately. Call on game-over /
    /// scene-end so old lines don't pop in mid-transition.
    /// </summary>
    public void Clear()
    {
        queue.Clear();
        if (activeRoutine != null) StopCoroutine(activeRoutine);
        activeRoutine = null;
        if (rootCg != null) rootCg.alpha = 0f;
    }

    IEnumerator SpeakLoop()
    {
        while (queue.Count > 0)
        {
            var (line, dur) = queue.Dequeue();
            yield return ShowLine(line, dur);
            if (queue.Count > 0)
                yield return new WaitForSeconds(gapBetweenLines);
        }
        activeRoutine = null;
    }

    IEnumerator ShowLine(string line, float duration)
    {
        if (bubbleText != null) bubbleText.text = line;
        if (rootCg != null) rootCg.alpha = 1f;

        // Pop in
        if (root != null)
        {
            root.transform.localScale = Vector3.one * 0.85f;
            float t = 0f;
            while (t < 0.18f)
            {
                t += Time.deltaTime;
                float p = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.18f), 3f);
                root.transform.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, p);
                yield return null;
            }
            root.transform.localScale = Vector3.one;
        }

        yield return new WaitForSeconds(duration);

        // Fade out
        if (rootCg != null)
        {
            float t = 0f;
            while (t < 0.25f)
            {
                t += Time.deltaTime;
                rootCg.alpha = 1f - Mathf.Clamp01(t / 0.25f);
                yield return null;
            }
            rootCg.alpha = 0f;
        }
    }
}