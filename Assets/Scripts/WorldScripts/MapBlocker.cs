using UnityEngine;

// ============================================================
//  MapBlocker
// ============================================================
public class MapBlocker : MonoBehaviour
{
    public Transform player; public float halfW = 1f, halfH = 1f;
    void LateUpdate()
    {
        if (!player) return;
        Vector2 c = transform.position, pos = player.position;
        float l = c.x - halfW, r = c.x + halfW, b = c.y - halfH, t = c.y + halfH;
        if (pos.x > l && pos.x < r && pos.y > b && pos.y < t)
        {
            float dL = pos.x - l, dR = r - pos.x, dB = pos.y - b, dT = t - pos.y, m = Mathf.Min(dL, dR, dB, dT);
            Vector3 pp = player.position;
            if (m == dL) pp.x = l - 0.01f;
            else if (m == dR) pp.x = r + 0.01f;
            else if (m == dB) pp.y = b - 0.01f; else pp.y = t + 0.01f;
            player.position = pp;
        }
    }
}
