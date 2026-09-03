using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Self-building top-right countdown timer HUD, used by any minigame that
/// needs a "survive N seconds" clock. Pass your project's existing timer
/// icon (e.g. Assets/Sprites/UI/hourglass) so it uses real art.
///
/// USAGE:
///   timerHUD = gameObject.AddComponent&lt;MinigameTimerHUD&gt;();
///   timerHUD.Initialize(hourglassSprite);
///   ...
///   timerHUD.SetTime(secondsRemaining);   // call every frame while counting down
/// </summary>
public class MinigameTimerHUD : MonoBehaviour
{
    TMP_Text _timerText;
    Image _icon;

    public void Initialize(Sprite iconSprite)
    {
        BuildUI(iconSprite);
    }

    public void SetTime(float secondsRemaining)
    {
        if (_timerText == null) return;
        int s = Mathf.CeilToInt(Mathf.Max(0f, secondsRemaining));
        _timerText.text = $"{s / 60}:{s % 60:D2}";
        _timerText.color = s <= 10 ? new Color(0.95f, 0.30f, 0.25f) : Color.white;
    }

    void BuildUI(Sprite iconSprite)
    {
        Canvas canvas = FindOrCreateCanvas();
        var canvasRT = canvas.GetComponent<RectTransform>();

        var root = MakeRT(canvasRT, "TimerHUD");
        root.anchorMin = new Vector2(1, 1);
        root.anchorMax = new Vector2(1, 1);
        root.pivot = new Vector2(1, 1);
        root.sizeDelta = new Vector2(160, 46);
        root.anchoredPosition = new Vector2(-24, -24);

        var bg = root.gameObject.AddComponent<Image>();
        bg.color = new Color(0.10f, 0.12f, 0.18f, 0.85f);
        bg.raycastTarget = false;

        if (iconSprite != null)
        {
            var iconRT = MakeRT(root, "Icon");
            iconRT.anchorMin = new Vector2(0, 0.5f);
            iconRT.anchorMax = new Vector2(0, 0.5f);
            iconRT.pivot = new Vector2(0, 0.5f);
            iconRT.sizeDelta = new Vector2(34, 34);
            iconRT.anchoredPosition = new Vector2(8, 0);
            _icon = iconRT.gameObject.AddComponent<Image>();
            _icon.sprite = iconSprite;
            _icon.preserveAspect = true;
            _icon.raycastTarget = false;
        }

        var textRT = MakeRT(root, "Text");
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(iconSprite != null ? 46 : 10, 4);
        textRT.offsetMax = new Vector2(-10, -4);
        _timerText = textRT.gameObject.AddComponent<TextMeshProUGUI>();
        _timerText.fontSize = 22;
        _timerText.fontStyle = FontStyles.Bold;
        _timerText.color = Color.white;
        _timerText.alignment = TextAlignmentOptions.MidlineRight;
        _timerText.raycastTarget = false;
        _timerText.text = "0:00";
    }

    Canvas FindOrCreateCanvas()
    {
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c.renderMode == RenderMode.ScreenSpaceOverlay) return c;

        var go = new GameObject("TimerCanvas");
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

    static RectTransform MakeRT(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }
}