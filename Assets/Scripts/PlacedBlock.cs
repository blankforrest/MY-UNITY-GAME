using UnityEngine;

/// <summary>
/// Attach this component to every player-placed block GameObject.
/// Stores the block's type, grid position, and whether it belongs to a vehicle structure.
/// </summary>
public class PlacedBlock : MonoBehaviour
{
    [Tooltip("The block type identifier (e.g. 2 = wooden plank, 5 = iron block).")]
    public int blockTypeID;

    [Tooltip("The block's position snapped to the integer voxel grid.")]
    public Vector3Int gridPosition;

    [Tooltip("Set to true once this block is identified as part of a vehicle structure.")]
    public bool isPartOfVehicle = false;

    /// <summary>
    /// Convenience initialiser — call this right after instantiating a placed block.
    /// </summary>
    public void Init(int typeID, Vector3Int gridPos)
    {
        blockTypeID   = typeID;
        gridPosition  = gridPos;
        isPartOfVehicle = false;
    }
}
