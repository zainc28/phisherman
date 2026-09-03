using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Spawns a pool of faint glowing circles that drift upward forever,
/// wrapping back to the bottom once they scroll past the top edge.
/// Purely decorative — every bubble has raycastTarget disabled so it
/// never intercepts clicks meant for the menu underneath.
/// </summary>
public class MenuBubbleField : MonoBehaviour
{
    [Header("Setup — assign in builder")]
    public Sprite bubbleSprite;
    public Color tint = Color.white;

    [Header("Tuning")]
    public int bubbleCount = 18;
    public Vector2 sizeRange = new Vector2(8f, 28f);
    public Vector2 alphaRange = new Vector2(0.08f, 0.15f);
    public Vector2 speedRange = new Vector2(8f, 24f);

    struct Bubble
    {
        public RectTransform rt;
        public float speed;
        public float baseX;
        public float driftPhase;
        public float driftFreq;
        public float driftAmp;
    }

    RectTransform _container;
    Bubble[] _bubbles;

    void OnEnable()
    {
        _container = GetComponent<RectTransform>();
        Spawn();
    }

    void Spawn()
    {
        _bubbles = new Bubble[Mathf.Max(0, bubbleCount)];
        for (int i = 0; i < _bubbles.Length; i++)
            _bubbles[i] = MakeBubble(Random.Range(0f, 1f));
    }

    Bubble MakeBubble(float startYFrac)
    {
        var go = new GameObject("Bubble", typeof(RectTransform));
        go.transform.SetParent(_container, false);
        var rt = go.GetComponent<RectTransform>();

        float size = Random.Range(sizeRange.x, sizeRange.y);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);

        float w = ContainerWidth();
        float h = ContainerHeight();
        float x = Random.Range(0f, w);
        rt.anchoredPosition = new Vector2(x, startYFrac * h);

        var img = go.AddComponent<Image>();
        if (bubbleSprite != null) img.sprite = bubbleSprite;
        img.color = new Color(tint.r, tint.g, tint.b, Random.Range(alphaRange.x, alphaRange.y));
        img.raycastTarget = false;
        img.preserveAspect = true;

        return new Bubble
        {
            rt = rt,
            speed = Random.Range(speedRange.x, speedRange.y),
            baseX = x,
            driftPhase = Random.Range(0f, Mathf.PI * 2f),
            driftFreq = Random.Range(0.15f, 0.4f),
            driftAmp = Random.Range(4f, 14f)
        };
    }

    float ContainerWidth() => _container != null && _container.rect.width > 1f ? _container.rect.width : 1920f;
    float ContainerHeight() => _container != null && _container.rect.height > 1f ? _container.rect.height : 1080f;

    void Update()
    {
        if (_bubbles == null || _container == null) return;
        float h = ContainerHeight();
        float w = ContainerWidth();
        for (int i = 0; i < _bubbles.Length; i++)
        {
            var b = _bubbles[i];
            if (b.rt == null) continue;

            var pos = b.rt.anchoredPosition;
            pos.y += b.speed * Time.unscaledDeltaTime;
            pos.x = b.baseX + Mathf.Sin(Time.unscaledTime * b.driftFreq + b.driftPhase) * b.driftAmp;

            if (pos.y > h + 20f)
            {
                b.baseX = Random.Range(0f, w);
                pos.x = b.baseX;
                pos.y = -20f;
                _bubbles[i] = b;
            }

            b.rt.anchoredPosition = pos;
            _bubbles[i] = b;
        }
    }
}
