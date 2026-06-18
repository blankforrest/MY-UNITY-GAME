using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    public float reach = 5f;
    private Camera playerCam;
    private CharacterController cc;

    void Start()
    {
        playerCam = GetComponentInChildren<Camera>();
        if (playerCam == null)
        {
            Debug.LogError("PlayerInteraction needs a Camera as a child object to raycast from.");
        }
        cc = GetComponent<CharacterController>();
    }

    void Update()
    {
        if (playerCam == null) return;

        // Don't interact with the world if the pointer is over any UI element
        // IMPORTANT: Only block raycasts if the cursor is unlocked! If the cursor is locked, 
        // we are in FPS mode and the mouse cannot be clicking UI (it would just hit the crosshair).
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            if (InventoryUI.IsInventoryOpen) return;
            if (DragDropManager.IsPointerOverUI()) return;
        }

        // WRENCH PRIORITY: If the player is holding the wrench, WrenchItem.cs handles
        // left click entirely (flood fill scan). Skip normal block-break to avoid
        // accidentally destroying a block while scanning a structure.
        if (!WrenchItem.IsHoldingWrench &&
            Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) // Left click - Break block
        {
            RaycastHit hit;
            // Use Collide so trigger MeshColliders on flowers (foliage child) register hits
            if (Physics.Raycast(playerCam.transform.position, playerCam.transform.forward,
                                out hit, reach, ~0, QueryTriggerInteraction.Collide))
            {
                // Tiny nudge to land just inside the hit block, then floor to voxel coords,
                // then pass the CENTER of that voxel so FloorToInt is never ambiguous.
                Vector3 p = hit.point - hit.normal * 0.001f;
                Vector3Int gridPos = new Vector3Int(
                    Mathf.FloorToInt(p.x),
                    Mathf.FloorToInt(p.y),
                    Mathf.FloorToInt(p.z));
                Vector3 voxelCenter = new Vector3(gridPos.x + 0.5f, gridPos.y + 0.5f, gridPos.z + 0.5f);

                if (VoxelWorld.Instance != null)
                {
                    byte existing = VoxelWorld.Instance.GetBlock(voxelCenter);
                    if (existing == 21 || existing == 23) // Large Wheel or helper
                    {
                        // Find all adjacent connected large wheel parts and break them as a group
                        List<Vector3Int> parts = FindConnectedWheelBlocks(gridPos);
                        foreach (var partPos in parts)
                        {
                            Vector3 partCenter = new Vector3(partPos.x + 0.5f, partPos.y + 0.5f, partPos.z + 0.5f);
                            // Only spawn one drop (from the main block we broke, or first block in parts)
                            bool suppressDrop = (partPos != gridPos && VoxelWorld.Instance.GetBlock(partCenter) == 23);
                            VoxelWorld.Instance.ModifyBlock(partCenter, 0, suppressDrop);
                            PlacedBlockRegistry.Instance?.Unregister(partPos);
                        }
                    }
                    else
                    {
                        VoxelWorld.Instance.ModifyBlock(voxelCenter, 0);
                        PlacedBlockRegistry.Instance?.Unregister(gridPos);
                    }
                }
            }
        }

        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) // Right click - Place block
        {
            // Read block type from the currently selected hotbar item.
            // blockTypeID == 0 means the item is not a placeable block (e.g. wrench) — skip.
            InventorySlot selectedSlot = Hotbar.Instance?.GetSelectedSlot();
            if (selectedSlot?.item == null || selectedSlot.item.blockTypeID == 0)
                return;

            byte blockType = (byte)selectedSlot.item.blockTypeID;

            RaycastHit[] hits = Physics.RaycastAll(playerCam.transform.position, playerCam.transform.forward, reach, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            RaycastHit hit = default;
            bool foundSolid = false;

            foreach (var h in hits)
            {
                if (h.collider.name != "Foliage")
                {
                    hit = h;
                    foundSolid = true;
                    break;
                }
            }

            if (foundSolid)
            {
                // Tiny nudge outward to land in the empty space adjacent to the hit face.
                Vector3 p = hit.point + hit.normal * 0.001f;
                Vector3Int gridPos = new Vector3Int(
                    Mathf.FloorToInt(p.x),
                    Mathf.FloorToInt(p.y),
                    Mathf.FloorToInt(p.z));
                Vector3 voxelCenter = new Vector3(gridPos.x + 0.5f, gridPos.y + 0.5f, gridPos.z + 0.5f);

                // Check if the block being LOOKED AT (inward nudge) is a Crafting Table.
                Vector3 inwardP = hit.point - hit.normal * 0.001f;
                Vector3Int hitVoxel = new Vector3Int(
                    Mathf.FloorToInt(inwardP.x),
                    Mathf.FloorToInt(inwardP.y),
                    Mathf.FloorToInt(inwardP.z));
                byte hitBlock = VoxelWorld.Instance?.GetBlock(new Vector3(hitVoxel.x + 0.5f, hitVoxel.y + 0.5f, hitVoxel.z + 0.5f)) ?? 0;
                if (hitBlock == 36)
                {
                    // Open the 3x3 Crafting Table UI instead of placing a block
                    InventoryUI.Instance?.Open3x3Crafting();
                    return;
                }

                // Prepare layout of blocks to place. Large Wheel is 2x2.
                List<Vector3Int> positionsToPlace = new List<Vector3Int>();
                positionsToPlace.Add(gridPos);

                if (blockType == 21) // Large Wheel
                {
                    Vector3Int sideDir = Vector3Int.right;
                    // Determine horizontal orientation based on player view
                    if (Mathf.Abs(playerCam.transform.forward.x) > Mathf.Abs(playerCam.transform.forward.z))
                    {
                        sideDir = new Vector3Int(0, 0, 1);
                    }
                    positionsToPlace.Add(gridPos + Vector3Int.up);
                    positionsToPlace.Add(gridPos + sideDir);
                    positionsToPlace.Add(gridPos + sideDir + Vector3Int.up);
                }

                // Verify placement is not blocked for any of the blocks
                bool placementBlocked = false;
                if (VoxelWorld.Instance != null)
                {
                    foreach (var pos in positionsToPlace)
                    {
                        Vector3 center = new Vector3(pos.x + 0.5f, pos.y + 0.5f, pos.z + 0.5f);
                        byte existing = VoxelWorld.Instance.GetBlock(center);
                        if (existing != 0 && existing != 7) // occupied by a solid block
                        {
                            placementBlocked = true;
                            break;
                        }

                        // Prevent placing block inside the player
                        Bounds blockBounds = new Bounds(center, Vector3.one);
                        blockBounds.Expand(-0.1f);

                        Collider[] hitColliders = Physics.OverlapBox(blockBounds.center, blockBounds.extents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                        foreach (Collider col in hitColliders)
                        {
                            if (col.CompareTag("Player") || col.GetComponentInParent<CharacterController>() != null)
                            {
                                placementBlocked = true;
                                break;
                            }
                        }
                        if (placementBlocked) break;
                    }
                }

                if (placementBlocked)
                {
                    return;
                }

                if (VoxelWorld.Instance != null)
                {
                    if (blockType == 9 || blockType == 10 || blockType == 11) // Flower varieties (Rose, Dandelion, Iris)
                    {
                        Vector3 belowPos = voxelCenter + Vector3.down;
                        byte blockBelow = VoxelWorld.Instance.GetBlock(belowPos);
                        // Flower can only be placed on Grass (4), Dirt (5), or Sand (8)
                        if (blockBelow != 4 && blockBelow != 5 && blockBelow != 8)
                        {
                            return;
                        }
                    }

                    foreach (var pos in positionsToPlace)
                    {
                        Vector3 center = new Vector3(pos.x + 0.5f, pos.y + 0.5f, pos.z + 0.5f);
                        // For large wheel: anchor = ID 21, helpers = ID 23.
                        // For every other block: all positions use the actual selected blockType.
                        byte idToPlace;
                        if (blockType == 21)
                            idToPlace = (pos == gridPos) ? (byte)21 : (byte)23;
                        else
                            idToPlace = blockType;
                        VoxelWorld.Instance.ModifyBlock(center, idToPlace);
                        PlacedBlockRegistry.Instance?.Register(pos);
                    }
                }
            }
        }
    }

    private List<Vector3Int> FindConnectedWheelBlocks(Vector3Int startPos)
    {
        List<Vector3Int> result = new List<Vector3Int>();
        if (VoxelWorld.Instance == null) return result;

        Queue<Vector3Int> queue = new Queue<Vector3Int>();
        HashSet<Vector3Int> visited = new HashSet<Vector3Int>();

        queue.Enqueue(startPos);
        visited.Add(startPos);

        while (queue.Count > 0)
        {
            Vector3Int curr = queue.Dequeue();
            result.Add(curr);

            // Scan in a 3x3x3 grid around the current part
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        Vector3Int neighbor = curr + new Vector3Int(dx, dy, dz);
                        if (visited.Contains(neighbor)) continue;

                        Vector3 center = new Vector3(neighbor.x + 0.5f, neighbor.y + 0.5f, neighbor.z + 0.5f);
                        byte type = VoxelWorld.Instance.GetBlock(center);
                        if (type == 21 || type == 23)
                        {
                            visited.Add(neighbor);
                            queue.Enqueue(neighbor);
                        }
                    }
                }
            }
        }
        return result;
    }
}
