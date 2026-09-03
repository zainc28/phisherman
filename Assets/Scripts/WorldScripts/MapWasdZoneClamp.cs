using UnityEngine;

// ============================================================
//  MapWasdZoneClamp
// ============================================================
public class MapWasdZoneClamp : MonoBehaviour
{
    MapNavAgent _nav; Vector2[] _worldVerts; bool _ready;
    void Start()
    {
        _nav = GetComponent<MapNavAgent>();
        var zoneGo = GameObject.Find("WalkableZone"); if (!zoneGo) return;
        var pc = zoneGo.GetComponent<PolygonCollider2D>(); if (!pc) return;
        var pts = pc.points; Vector2 off = zoneGo.transform.position;
        _worldVerts = new Vector2[pts.Length]; for (int i = 0; i < pts.Length; i++) _worldVerts[i] = pts[i] + off;
        _ready = true;
    }
    void LateUpdate()
    {
        if (!_ready || _worldVerts == null || _worldVerts.Length < 3) return;
        Vector2 pos = transform.position;
        if (!PolygonUtils.PointInPolygon(pos, _worldVerts)) { Vector2 cl = PolygonUtils.NearestEdgePoint(pos, _worldVerts); transform.position = new Vector3(cl.x, cl.y, transform.position.z); if (_nav != null && _nav.IsNavigating) _nav.CancelPath(); }
    }
}
