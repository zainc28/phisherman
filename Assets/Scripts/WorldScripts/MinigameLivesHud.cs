using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared "3 lives" HUD used by every minigame, for a consistent look:
/// always red hearts, always top-left, always the same shake + grey-out
/// feedback when a life is lost.
///
/// USAGE (from any minigame manager's Start()):
///   livesHUD = gameObject.AddComponent&lt;MinigameLivesHUD&gt;();
///   livesHUD.Initialize(3);
///   ...
///   livesHUD.LoseLife();   // call once per hit taken
///
/// The heart icon is drawn procedurally at runtime (classic heart curve
/// baked into a texture) so it works in builds, not just the editor —
/// no sprite asset needed.
/// </summary>
public class MinigameLivesHUD : MonoBehaviour
{
    static readonly Color HeartFull = new Color(0.91f, 0.20f, 0.20f);
    static readonly Color HeartEmpty = new Color(0.35f, 0.35f, 0.40f, 0.55f);

    static Sprite _heartSprite;
    static Sprite _proceduralHeartSprite;

    List<Image> _hearts = new List<Image>();
    int _maxLives;
    int _currentLives;

    /// <summary>
    /// heartSpriteOverride: pass your project's existing heart sprite here
    /// (e.g. Assets/Sprites/UI/heart) so the HUD uses real art instead of
    /// the procedural fallback. Pass null to keep the drawn placeholder.
    /// </summary>
    public void Initialize(int lives, Sprite heartSpriteOverride = null)
    {
        _maxLives = Mathf.Max(1, lives);
        _currentLives = _maxLives;
        _heartSprite = heartSpriteOverride != null ? heartSpriteOverride : GetProceduralHeartSprite();
        BuildUI();
    }

    void BuildUI()
    {
        Canvas canvas = FindOrCreateCanvas();
        var canvasRT = canvas.GetComponent<RectTransform>();

        var root = MakeRT(canvasRT, "LivesHUD");
        root.anchorMin = new Vector2(0, 1);
        root.anchorMax = new Vector2(0, 1);
        root.pivot = new Vector2(0, 1);
        root.sizeDelta = new Vector2(46 * _maxLives, 46);
        root.anchoredPosition = new Vector2(24, -24);

        _hearts.Clear();
        for (int i = 0; i < _maxLives; i++)
        {
            var h = MakeRT(root, "Heart" + i);
            h.anchorMin = new Vector2(0, 1);
            h.anchorMax = new Vector2(0, 1);
            h.pivot = new Vector2(0, 1);
            h.sizeDelta = new Vector2(40, 40);
            h.anchoredPosition = new Vector2(i * 46, 0);

            var img = h.gameObject.AddComponent<Image>();
            img.sprite = _heartSprite;
            img.color = HeartFull;
            img.raycastTarget = false;
            img.preserveAspect = true;

            _hearts.Add(img);
        }
    }

    Canvas FindOrCreateCanvas()
    {
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c.renderMode == RenderMode.ScreenSpaceOverlay) return c;

        var go = new GameObject("LivesCanvas");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    /// <summary>Call once per hit taken. Greys out + shakes the next heart in line.</summary>
    public void LoseLife()
    {
        if (_currentLives <= 0) return;
        int idx = _currentLives - 1;
        _currentLives--;
        if (idx >= 0 && idx < _hearts.Count)
            StartCoroutine(ShakeAndGrey(_hearts[idx]));
    }

    /// <summary>Resets all hearts to full — e.g. on a scene retry without a full reload.</summary>
    public void ResetLives(int lives)
    {
        _maxLives = Mathf.Max(1, lives);
        _currentLives = _maxLives;
        for (int i = 0; i < _hearts.Count; i++)
            _hearts[i].color = i < _currentLives ? HeartFull : HeartEmpty;
    }

    IEnumerator ShakeAndGrey(Image img)
    {
        var rt = img.rectTransform;
        Vector2 orig = rt.anchoredPosition;
        float t = 0f, dur = 0.35f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float damp = 1f - t / dur;
            float x = Mathf.Sin(t * 45f) * 7f * damp;
            rt.anchoredPosition = orig + new Vector2(x, 0f);
            yield return null;
        }
        rt.anchoredPosition = orig;
        img.color = HeartEmpty;
    }

    // ── Procedural heart sprite fallback (used only if no real sprite is assigned) ──
    static Sprite GetProceduralHeartSprite()
    {
        if (_proceduralHeartSprite != null) return _proceduralHeartSprite;

        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x / (float)(size - 1)) * 2f - 1f;
                float v = (y / (float)(size - 1)) * 2f - 1f;
                // Classic heart curve: (x^2+y^2-1)^3 - x^2*y^3 <= 0
                float vv = -v * 1.2f + 0.35f;
                float val = Mathf.Pow(u * u + vv * vv - 1f, 3f) - u * u * vv * vv * vv;
                tex.SetPixel(x, y, val <= 0f ? Color.white : new Color(1, 1, 1, 0));
            }
        }
        tex.Apply();

        _proceduralHeartSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return _proceduralHeartSprite;
    }

    static RectTransform MakeRT(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }
}