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


        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) // Left click - Break block
        {
            RaycastHit hit;
            if (Physics.Raycast(playerCam.transform.position, playerCam.transform.forward,
                                out hit, reach, ~0, QueryTriggerInteraction.Ignore))
            {
                // Tiny nudge to land just inside the hit block, then floor to voxel coords,
                // then pass the CENTER of that voxel so FloorToInt is never ambiguous.
                Vector3 p = hit.point - hit.normal * 0.001f;
                Vector3 voxelCenter = new Vector3(
                    Mathf.FloorToInt(p.x) + 0.5f,
                    Mathf.FloorToInt(p.y) + 0.5f,
                    Mathf.FloorToInt(p.z) + 0.5f);
                if (VoxelWorld.Instance != null)
                    VoxelWorld.Instance.ModifyBlock(voxelCenter, 0);
            }
        }

        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) // Right click - Place block
        {
            RaycastHit hit;
            if (Physics.Raycast(playerCam.transform.position, playerCam.transform.forward,
                                out hit, reach, ~0, QueryTriggerInteraction.Ignore))
            {
                // Tiny nudge outward to land in the empty space adjacent to the hit face.
                Vector3 p = hit.point + hit.normal * 0.001f;
                Vector3 voxelCenter = new Vector3(
                    Mathf.FloorToInt(p.x) + 0.5f,
                    Mathf.FloorToInt(p.y) + 0.5f,
                    Mathf.FloorToInt(p.z) + 0.5f);
                if (VoxelWorld.Instance != null)
                    VoxelWorld.Instance.ModifyBlock(voxelCenter, 1);
            }
        }
    }
}
