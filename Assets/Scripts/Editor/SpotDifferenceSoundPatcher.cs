using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Patches the existing SpotDifference W3, W4, W5 scenes in-place:
///   1. Assigns shark_music_v1 as BGM clip on SpotDifferenceManager
///   2. Ensures both AudioSources on GameManager are configured
///   3. Assigns water_splash, glass_crack, impact, fishing_rod_winding sfx clips
///   4. Re-wires PlayAgain and BackToWorldMap buttons with persistent listeners
///      so they survive builds (previously used AddListener which is runtime-only)
///
/// Run via:  Phisherman > Patch Spot Difference Sound (W3/W4/W5)
///
/// Also patches W1 (SpotDifference) for completeness.
/// W2 is handled by its own builder (SpotDifferenceBuilder_W2).
/// </summary>
public static class SpotDifferenceSoundPatcher
{
    static readonly string[] ScenePaths = new[]
    {
        "Assets/Scenes/SpotDifference.unity",
        "Assets/Scenes/SpotDifference_W3.unity",
        "Assets/Scenes/SpotDifference_W4.unity",
        "Assets/Scenes/SpotDifference_W5.unity",
    };

    [MenuItem("Phisherman/Patch Spot Difference Sound (W1/W3/W4/W5)")]
    public static void PatchAll()
    {
        AudioClip sharkMusic = FindAudio("shark_music_v1");
        AudioClip splash = FindAudio("water_splash");
        AudioClip glassCrack = FindAudio("glass_crack");
        AudioClip impact = FindAudio("impact");
        AudioClip rodWinding = FindAudio("fishing_rod_winding");

        Debug.Log($"[SDSoundPatcher] shark_music_v1: {(sharkMusic != null ? "✓" : "NOT FOUND")}");
        Debug.Log($"[SDSoundPatcher] water_splash:   {(splash != null ? "✓" : "NOT FOUND")}");
        Debug.Log($"[SDSoundPatcher] glass_crack:    {(glassCrack != null ? "✓" : "NOT FOUND")}");
        Debug.Log($"[SDSoundPatcher] impact:         {(impact != null ? "✓" : "NOT FOUND")}");
        Debug.Log($"[SDSoundPatcher] rod_winding:    {(rodWinding != null ? "✓" : "NOT FOUND")}");

        foreach (var scenePath in ScenePaths)
        {
            if (!File.Exists(scenePath))
            {
                Debug.LogWarning($"[SDSoundPatcher] Scene not found, skipping: {scenePath}");
                continue;
            }
            PatchScene(scenePath, sharkMusic, splash, glassCrack, impact, rodWinding);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[SDSoundPatcher] Done patching all Spot Difference scenes.");
    }

    static void PatchScene(string scenePath, AudioClip sharkMusic, AudioClip splash,
        AudioClip glassCrack, AudioClip impact, AudioClip rodWinding)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        // ── Find GameManager ──────────────────────────────────────────
        var mgrGo = GameObject.Find("GameManager");
        if (mgrGo == null)
        {
            Debug.LogWarning($"[SDSoundPatcher] No GameManager in {scenePath}");
            EditorSceneManager.CloseScene(scene, false);
            return;
        }

        var manager = mgrGo.GetComponent<SpotDifferenceManager>();
        if (manager == null)
        {
            Debug.LogWarning($"[SDSoundPatcher] No SpotDifferenceManager in {scenePath}");
            EditorSceneManager.CloseScene(scene, false);
            return;
        }

        // ── Ensure two AudioSources ──────────────────────────────────
        var sources = mgrGo.GetComponents<AudioSource>();
        AudioSource sfxSrc, bgmSrc;
        if (sources.Length == 0)
        {
            sfxSrc = mgrGo.AddComponent<AudioSource>();
            bgmSrc = mgrGo.AddComponent<AudioSource>();
        }
        else if (sources.Length == 1)
        {
            sfxSrc = sources[0];
            bgmSrc = mgrGo.AddComponent<AudioSource>();
        }
        else
        {
            sfxSrc = sources[0];
            bgmSrc = sources[1];
        }

        sfxSrc.playOnAwake = false; sfxSrc.loop = false; sfxSrc.volume = 0.85f;
        bgmSrc.playOnAwake = false; bgmSrc.loop = true; bgmSrc.volume = 0.35f;
        if (sharkMusic != null) bgmSrc.clip = sharkMusic;

        // Assign clips onto the manager serialized fields
        manager.sfxSource = sfxSrc;
        manager.bgMusicSource = bgmSrc;
        manager.bgMusicClip = sharkMusic;
        manager.sfxSplash = splash;
        manager.sfxWrong = glassCrack;
        manager.sfxImpact = impact;
        manager.sfxRodWinding = rodWinding;

        // ── Fix result panel buttons — replace runtime listeners with persistent ──
        var resultPanel = manager.resultPanel;
        if (resultPanel != null)
        {
            // PlayAgain
            var playAgainBtn = resultPanel.transform.FindDeepChild("PlayAgainBtn")?.GetComponent<Button>();
            if (playAgainBtn != null)
            {
                playAgainBtn.onClick.RemoveAllListeners();
                UnityEventTools.AddPersistentListener(playAgainBtn.onClick, manager.PlayAgain);
                Debug.Log($"[SDSoundPatcher] Wired PlayAgain in {scenePath}");
            }

            // MenuBtn / BackToWorldMap
            var menuBtn = resultPanel.transform.FindDeepChild("MenuBtn")?.GetComponent<Button>();
            if (menuBtn != null)
            {
                menuBtn.onClick.RemoveAllListeners();
                UnityEventTools.AddPersistentListener(menuBtn.onClick, manager.BackToWorldMap);
                Debug.Log($"[SDSoundPatcher] Wired BackToWorldMap in {scenePath}");
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log($"[SDSoundPatcher] Patched: {scenePath}");
    }

    static AudioClip FindAudio(string name)
    {
        foreach (var g in AssetDatabase.FindAssets(name + " t:AudioClip"))
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            if (Path.GetFileNameWithoutExtension(p).ToLower() == name.ToLower())
                return AssetDatabase.LoadAssetAtPath<AudioClip>(p);
        }
        return null;
    }
}

// ── Extension: deep child search ──────────────────────────────────────────────
public static class TransformExtensions
{
    public static Transform FindDeepChild(this Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var result = child.FindDeepChild(name);
            if (result != null) return result;
        }
        return null;
    }
}