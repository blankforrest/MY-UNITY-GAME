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
                    VoxelWorld.Instance.ModifyBlock(voxelCenter, 0);

                // Unregister from structure detection registry
                PlacedBlockRegistry.Instance?.Unregister(gridPos);
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

                // Prevent placing block inside the player
                Bounds blockBounds = new Bounds(voxelCenter, Vector3.one);
                
                // Shrink bounds slightly so you can place blocks while standing flush against the grid
                blockBounds.Expand(-0.1f);

                // Use the physics engine as the source of truth for overlaps
                Collider[] hitColliders = Physics.OverlapBox(blockBounds.center, blockBounds.extents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                bool playerInWay = false;
                
                foreach (Collider col in hitColliders)
                {
                    // If the collider belongs to the player (either tagged Player or has the CharacterController)
                    if (col.CompareTag("Player") || col.GetComponentInParent<CharacterController>() != null)
                    {
                        playerInWay = true;
                        break;
                    }
                }

                if (playerInWay)
                {
                    // Option 2 chosen: Do not give the player the ability to place a block where they are standing.
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
                    VoxelWorld.Instance.ModifyBlock(voxelCenter, blockType);
                }

                // Register as player-placed for structure detection
                PlacedBlockRegistry.Instance?.Register(gridPos);
            }
        }
    }
}
