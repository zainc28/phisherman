using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Swaps the world-map overlay image to match how many worlds the player
/// has unlocked (map_v1 .. map_v5). Refreshes on OnEnable, which fires
/// every time the map panel is opened (M key), so progress made mid-session
/// shows up the next time the map is opened — no extra wiring needed.
/// </summary>
public class WorldMapImageSwitcher : MonoBehaviour
{
    public Image targetImage;
    public Sprite[] mapVersions; // index 0 = v1, index 1 = v2, ...

    void OnEnable() => Refresh();

    public void Refresh()
    {
        if (targetImage == null || mapVersions == null || mapVersions.Length == 0) return;
        int version = Mathf.Clamp(WorldProgress.GetMapVersion(), 1, mapVersions.Length);
        var spr = mapVersions[version - 1];
        if (spr != null) targetImage.sprite = spr;
    }
}