using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton that tracks which voxel grid positions were placed by the player
/// (as opposed to terrain that was generated procedurally).
/// Add this component to a persistent GameObject (e.g. the World object).
/// </summary>
public class PlacedBlockRegistry : MonoBehaviour
{
    public static PlacedBlockRegistry Instance { get; private set; }

    // All positions the player has explicitly placed a block at.
    private readonly HashSet<Vector3Int> _placedPositions = new HashSet<Vector3Int>();

    // -----------------------------------------------------------------------

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }
    }

    // ── Public API ──────────────────────────────────────────────────────────

    /// <summary>Call this when the player successfully places a block.</summary>
    public void Register(Vector3Int gridPos)
    {
        _placedPositions.Add(gridPos);
    }

    /// <summary>Call this when the player breaks a block (or a placed block is removed).</summary>
    public void Unregister(Vector3Int gridPos)
    {
        _placedPositions.Remove(gridPos);
    }

    /// <summary>Returns true if the position was placed by the player (not terrain).</summary>
    public bool IsPlayerPlaced(Vector3Int gridPos)
    {
        return _placedPositions.Contains(gridPos);
    }

    /// <summary>Read-only view of all currently registered positions.</summary>
    public IReadOnlyCollection<Vector3Int> AllPositions => _placedPositions;

    /// <summary>Total number of player-placed blocks currently registered.</summary>
    public int Count => _placedPositions.Count;

    /// <summary>Clear all registered player-placed blocks.</summary>
    public void Clear()
    {
        _placedPositions.Clear();
    }
}
