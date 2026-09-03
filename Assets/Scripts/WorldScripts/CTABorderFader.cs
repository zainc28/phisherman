using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  CTABorderFader
// ============================================================
public class CTABorderFader : MonoBehaviour
{
    public Image borderImage; public string completionKey = "";
    public Color activeColor = Color.white, completedColor = new Color(0.3f, 0.3f, 0.3f, 0.4f);
    bool _last;
    void Start() => Refresh(); void Update() => Refresh();
    void Refresh() { if (!borderImage || string.IsNullOrEmpty(completionKey)) return; bool done = PlayerPrefs.GetInt(completionKey, 0) == 1; if (done == _last) return; _last = done; borderImage.color = done ? completedColor : activeColor; }
}
