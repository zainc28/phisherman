using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attached to the dots row at the top of each email card.
/// Each dot independently cycles through random colours at slightly
/// different rates so they pulse like little aquarium LEDs.
/// </summary>
public class CardDotAnimator : MonoBehaviour
{
    [HideInInspector] public Image[] dots;

    // A curated palette of fish-tank-friendly colours
    private static readonly Color[] Palette =
    {
        new Color(1.00f, 0.42f, 0.20f),   // clownfish orange
        new Color(1.00f, 0.85f, 0.20f),   // yellow tang
        new Color(0.20f, 0.75f, 1.00f),   // blue tang
        new Color(0.35f, 0.95f, 0.55f),   // sea-green
        new Color(0.90f, 0.30f, 0.60f),   // pink coral
        new Color(0.85f, 0.85f, 1.00f),   // pearl white
        new Color(0.45f, 0.90f, 0.90f),   // teal
        new Color(1.00f, 0.60f, 0.80f),   // salmon
        new Color(0.60f, 0.40f, 1.00f),   // purple urchin
        new Color(1.00f, 0.95f, 0.70f),   // warm white
    };

    void Start()
    {
        if (dots == null) return;
        for (int i = 0; i < dots.Length; i++)
            StartCoroutine(AnimateDot(dots[i], i * 0.12f));
    }

    IEnumerator AnimateDot(Image dot, float initialDelay)
    {
        yield return new WaitForSeconds(initialDelay);

        Color current = Palette[Random.Range(0, Palette.Length)];
        dot.color = current;

        while (true)
        {
            // Hold current colour for a random duration
            float holdTime = Random.Range(0.25f, 0.90f);
            yield return new WaitForSeconds(holdTime);

            // Pick a new colour (different from current)
            Color next;
            do { next = Palette[Random.Range(0, Palette.Length)]; }
            while (next == current);

            // Quick cross-fade
            float fadeDur = Random.Range(0.10f, 0.28f);
            float t = 0f;
            while (t < fadeDur)
            {
                t += Time.deltaTime;
                if (dot == null) yield break;
                dot.color = Color.Lerp(current, next, t / fadeDur);
                yield return null;
            }
            current = next;
        }
    }
}