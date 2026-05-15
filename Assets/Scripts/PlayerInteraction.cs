using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    public float reach = 5f;
    private Camera playerCam;

    void Start()
    {
        playerCam = GetComponentInChildren<Camera>();
        if (playerCam == null)
        {
            Debug.LogError("PlayerInteraction needs a Camera as a child object to raycast from.");
        }
    }

    void Update()
    {
        if (playerCam == null) return;

        // Don't interact with the world if the pointer is over any UI element
        if (InventoryUI.IsInventoryOpen) return;
        if (DragDropManager.IsPointerOverUI()) return;


        // WRENCH PRIORITY: If the player is holding the wrench, WrenchItem.cs handles
        // left click entirely (flood fill scan). Skip normal block-break to avoid
        // accidentally destroying a block while scanning a structure.
        if (!WrenchItem.IsHoldingWrench &&
            Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) // Left click - Break block
        {
            RaycastHit hit;
            if (Physics.Raycast(playerCam.transform.position, playerCam.transform.forward,
                                out hit, reach, ~0, QueryTriggerInteraction.Ignore))
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

            RaycastHit hit;
            if (Physics.Raycast(playerCam.transform.position, playerCam.transform.forward,
                                out hit, reach, ~0, QueryTriggerInteraction.Ignore))
            {
                // Tiny nudge outward to land in the empty space adjacent to the hit face.
                Vector3 p = hit.point + hit.normal * 0.001f;
                Vector3Int gridPos = new Vector3Int(
                    Mathf.FloorToInt(p.x),
                    Mathf.FloorToInt(p.y),
                    Mathf.FloorToInt(p.z));
                Vector3 voxelCenter = new Vector3(gridPos.x + 0.5f, gridPos.y + 0.5f, gridPos.z + 0.5f);

                if (VoxelWorld.Instance != null)
                    VoxelWorld.Instance.ModifyBlock(voxelCenter, blockType);

                // Register as player-placed for structure detection
                PlacedBlockRegistry.Instance?.Register(gridPos);
            }
        }
    }
}
