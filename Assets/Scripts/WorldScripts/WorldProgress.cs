using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks which world (map stage) the player has unlocked based on
/// completed minigames, and which map sprite version (map_v1 .. map_v5)
/// should be displayed.
///
/// WORLD → INTERIOR REQUIREMENTS
///   Keyed on the INTERIOR scene name (e.g. "ApartmentInterior"), not the
///   minigame scene name — multiple houses can share the same minigame
///   (EmailSwiper, TowerDefense, SpotDifference), so a world is "complete"
///   only once every one of its own houses has been beaten.
///
///   World 2 doesn't have its interiors reflected here yet — add its
///   entry the same way once its interior scene names are finalized.
/// </summary>
public static class WorldProgress
{
    const string CompletedKeyPrefix = "completed_";

    static readonly Dictionary<int, string[]> WorldRequirements = new Dictionary<int, string[]>
    {
        { 1, new [] { "ApartmentInterior", "PizzaInterior", "OfficeInterior" } },
        // { 2, new [] { "..." } },   // add once World 2's interior scene names are finalized
        { 3, new [] { "AuntCarolInterior", "UncleMarcusInterior", "GrandpaLouInterior" } },
        { 4, new [] { "GrandmaIrisInterior", "UncleFelixInterior", "AuntDanaInterior" } },
        // World 5 is the final island — no "next world" to unlock.
    };

    public static bool IsMinigameComplete(string sceneName)
        => PlayerPrefs.GetInt(CompletedKeyPrefix + sceneName, 0) == 1;

    /// <summary>True if every interior required for this world is complete.</summary>
    public static bool IsWorldComplete(int worldNumber)
    {
        if (!WorldRequirements.TryGetValue(worldNumber, out var scenes))
            return false; // no requirements defined for this world yet
        foreach (var scene in scenes)
            if (!IsMinigameComplete(scene)) return false;
        return true;
    }

    /// <summary>
    /// Highest world number currently accessible (1-indexed). World 1 is
    /// always open; world N+1 opens once world N's requirements are met.
    /// </summary>
    public static int GetHighestUnlockedWorld()
    {
        int unlocked = 1;
        while (WorldRequirements.ContainsKey(unlocked) && IsWorldComplete(unlocked))
        {
            unlocked++;
            if (unlocked > 5) break;
        }
        return Mathf.Clamp(unlocked, 1, 5);
    }

    /// <summary>Which map_v# sprite to show right now.</summary>
    public static int GetMapVersion() => GetHighestUnlockedWorld();
}