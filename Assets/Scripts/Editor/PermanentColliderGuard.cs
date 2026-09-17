using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Every scene builder wipes its target scene with NewScene(EmptyScene) and
// regenerates everything from code. That is safe for everything EXCEPT
// hand-shaped PolygonCollider2D data (e.g. WalkableZone), which artists edit
// directly in the Scene view and can never be regenerated from a script.
//
// Tag a GameObject "PermanentCollider" and every builder below will skip it:
// its PolygonCollider2D data is snapshotted before the scene is wiped and
// recreated untouched (same points, same transform) after rebuild — it is
// never destroyed, reset, regenerated, or modified by builder code.
internal static class PermanentColliderGuard
{
    internal const string Tag = "PermanentCollider";

    internal class Snapshot
    {
        public string name;
        public int layer;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 localScale;
        public bool isTrigger;
        public Vector2 offset;
        public Vector2[][] paths;
    }

    // Call this BEFORE EditorSceneManager.NewScene(...) wipes the scene.
    // excludeNames lets a builder skip objects it already round-trips itself
    // by name (e.g. WorldBuilders' WalkableZone save/restore) so the tag path
    // doesn't create a duplicate GameObject alongside it.
    internal static List<Snapshot> Capture(string scenePath, HashSet<string> excludeNames = null)
    {
        var result = new List<Snapshot>();
        if (!TagExists()) return result;

        var activeScene = EditorSceneManager.GetActiveScene();
        bool alreadyOpen = activeScene.IsValid() && activeScene.path == scenePath;
        if (!alreadyOpen)
        {
            if (string.IsNullOrEmpty(scenePath) || !File.Exists(scenePath)) return result;
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }

        GameObject[] tagged;
        try { tagged = GameObject.FindGameObjectsWithTag(Tag); }
        catch (UnityException) { return result; }

        foreach (var go in tagged)
        {
            if (excludeNames != null && excludeNames.Contains(go.name)) continue;
            var pc = go.GetComponent<PolygonCollider2D>();
            if (pc == null) continue;

            var paths = new Vector2[pc.pathCount][];
            for (int i = 0; i < pc.pathCount; i++) paths[i] = pc.GetPath(i);

            result.Add(new Snapshot
            {
                name = go.name,
                layer = go.layer,
                position = go.transform.position,
                rotation = go.transform.rotation,
                localScale = go.transform.localScale,
                isTrigger = pc.isTrigger,
                offset = pc.offset,
                paths = paths,
            });
        }
        return result;
    }

    // Call this AFTER the scene has been rebuilt.
    internal static void Restore(List<Snapshot> snapshots)
    {
        if (snapshots == null) return;
        foreach (var s in snapshots)
        {
            var go = new GameObject(s.name);
            go.tag = Tag;
            go.layer = s.layer;
            go.transform.position = s.position;
            go.transform.rotation = s.rotation;
            go.transform.localScale = s.localScale;

            var pc = go.AddComponent<PolygonCollider2D>();
            pc.isTrigger = s.isTrigger;
            pc.offset = s.offset;
            pc.pathCount = s.paths.Length;
            for (int i = 0; i < s.paths.Length; i++) pc.SetPath(i, s.paths[i]);

            Debug.Log($"[PermanentColliderGuard] Restored '{s.name}' untouched ({s.paths.Length} path(s), {pc.points.Length} total points).");
        }
    }

    static bool TagExists()
    {
        foreach (var t in UnityEditorInternal.InternalEditorUtility.tags)
            if (t == Tag) return true;
        return false;
    }
}
