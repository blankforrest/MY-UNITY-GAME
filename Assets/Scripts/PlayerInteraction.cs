using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    public float reach = 5f;
    private Camera playerCam;
    private CharacterController cc;

    // Progressive Block Breaking State
    private Vector3Int breakingBlockPos;
    private byte breakingBlockID;
    private float breakProgress = 0f;
    private bool isBreakingBlock = false;

    private GameObject crackOverlay;
    private MeshRenderer crackRenderer;
    private Material crackMaterial;
    private Texture2D[] crackTextures;

    void Start()
    {
        playerCam = GetComponentInChildren<Camera>();
        if (playerCam == null)
        {
            Debug.LogError("PlayerInteraction needs a Camera as a child object to raycast from.");
        }
        cc = GetComponent<CharacterController>();
        InitializeCrackTextures();
    }

    void Update()
    {
        if (playerCam == null) return;

        // Don't interact with the world if the pointer is over any UI element
        // IMPORTANT: Only block raycasts if the cursor is unlocked! If the cursor is locked, 
        // we are in FPS mode and the mouse cannot be clicking UI (it would just hit the crosshair).
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            if (InventoryUI.IsInventoryOpen)
            {
                ResetBreakingState();
                return;
            }
            if (DragDropManager.IsPointerOverUI())
            {
                ResetBreakingState();
                return;
            }
        }

        // Get held item from hotbar
        InventorySlot selectedSlot = Hotbar.Instance != null ? Hotbar.Instance.GetSelectedSlot() : null;
        Item heldItem = selectedSlot?.item;

        // Check if we are currently holding down left click
        bool isLeftClickHeld = Mouse.current != null && Mouse.current.leftButton.isPressed;

        // Block break logic
        if (!WrenchItem.IsHoldingWrench && isLeftClickHeld)
        {
            RaycastHit hit;
            // Use Collide so trigger MeshColliders on flowers (foliage child) register hits
            if (Physics.Raycast(playerCam.transform.position, playerCam.transform.forward,
                                out hit, reach, ~(1 << 2), QueryTriggerInteraction.Collide))
            {
                // Tiny nudge to land just inside the hit block, then floor to voxel coords,
                // then pass the CENTER of that voxel so FloorToInt is never ambiguous.
                Vector3 p = hit.point - hit.normal * 0.001f;
                Vector3Int targetGridPos = new Vector3Int(
                    Mathf.FloorToInt(p.x),
                    Mathf.FloorToInt(p.y),
                    Mathf.FloorToInt(p.z));
                Vector3 voxelCenter = new Vector3(targetGridPos.x + 0.5f, targetGridPos.y + 0.5f, targetGridPos.z + 0.5f);

                if (VoxelWorld.Instance != null)
                {
                    byte blockID = VoxelWorld.Instance.GetBlock(voxelCenter);
                    
                    // Don't mine water (7) or empty blocks (0)
                    if (blockID != 0 && blockID != 7)
                    {
                        var pc = GetComponent<PlayerController>();
                        bool isCreative = (pc != null && pc.isCreativeMode);
                        float hardness = GetBlockHardness(blockID);

                        if (isCreative || hardness <= 0f)
                        {
                            // Instant break in creative mode or for flowers/grass
                            BreakBlockInstantly(targetGridPos, voxelCenter, blockID);
                            ResetBreakingState();
                        }
                        else
                        {
                            // Survival mode: gradual breaking!
                            if (isBreakingBlock && targetGridPos == breakingBlockPos && blockID == breakingBlockID)
                            {
                                float speedMultiplier = GetBreakingSpeedMultiplier(blockID, heldItem);
                                float breakSpeed = speedMultiplier / hardness;
                                
                                breakProgress += breakSpeed * Time.deltaTime;
                                UpdateCrackOverlay(targetGridPos, breakProgress);

                                if (breakProgress >= 1.0f)
                                {
                                    BreakBlockInstantly(targetGridPos, voxelCenter, blockID);
                                    ResetBreakingState();
                                }
                            }
                            else
                            {
                                // Started breaking a new block
                                isBreakingBlock = true;
                                breakingBlockPos = targetGridPos;
                                breakingBlockID = blockID;
                                breakProgress = 0f;
                                UpdateCrackOverlay(targetGridPos, breakProgress);
                            }
                        }
                    }
                    else
                    {
                        ResetBreakingState();
                    }
                }
                else
                {
                    ResetBreakingState();
                }
            }
            else
            {
                ResetBreakingState();
            }
        }
        else
        {
            ResetBreakingState();
        }

        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) // Right click - Place block
        {
            // Read block type from the currently selected hotbar item.
            // blockTypeID == 0 means the item is not a placeable block (e.g. wrench) — skip.
            InventorySlot placeSlot = Hotbar.Instance?.GetSelectedSlot();
            if (placeSlot?.item == null || placeSlot.item.blockTypeID == 0)
                return;

            byte blockType = (byte)placeSlot.item.blockTypeID;

            RaycastHit[] hits = Physics.RaycastAll(playerCam.transform.position, playerCam.transform.forward, reach, ~(1 << 2), QueryTriggerInteraction.Ignore);
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
                // Find the voxel we are looking at (inward nudge)
                Vector3 inwardP = hit.point - hit.normal * 0.001f;
                Vector3Int hitVoxel = new Vector3Int(
                    Mathf.FloorToInt(inwardP.x),
                    Mathf.FloorToInt(inwardP.y),
                    Mathf.FloorToInt(inwardP.z));

                // Target placement space is adjacent to the hit face of the voxel
                Vector3Int gridPos = hitVoxel + new Vector3Int(
                    Mathf.RoundToInt(hit.normal.x),
                    Mathf.RoundToInt(hit.normal.y),
                    Mathf.RoundToInt(hit.normal.z));

                Vector3 voxelCenter = new Vector3(gridPos.x + 0.5f, gridPos.y + 0.5f, gridPos.z + 0.5f);
                byte hitBlock = VoxelWorld.Instance?.GetBlock(new Vector3(hitVoxel.x + 0.5f, hitVoxel.y + 0.5f, hitVoxel.z + 0.5f)) ?? 0;
                if (hitBlock == 36)
                {
                    // Open the 3x3 Crafting Table UI instead of placing a block
                    InventoryUI.Instance?.Open3x3Crafting();
                    return;
                }
                if (hitBlock == 37)
                {
                    // Open the Furnace UI instead of placing a block
                    var furnaceState = FurnaceManager.Instance?.GetOrCreateFurnace(hitVoxel);
                    if (furnaceState != null)
                    {
                        InventoryUI.Instance?.OpenFurnaceUI(furnaceState);
                    }
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
                        else if (blockType == 38 || blockType == 39)
                        {
                            Vector3 forward = playerCam.transform.forward;
                            forward.y = 0f;
                            forward.Normalize();

                            if (Mathf.Abs(forward.z) > Mathf.Abs(forward.x))
                            {
                                if (forward.z > 0f) // Looking North -> rising towards North (+Z) -> South stair
                                    idToPlace = (blockType == 38) ? (byte)38 : (byte)39;
                                else // Looking South -> rising towards South (-Z) -> North stair
                                    idToPlace = (blockType == 38) ? (byte)40 : (byte)43;
                            }
                            else
                            {
                                if (forward.x > 0f) // Looking East -> rising towards East (+X) -> West stair
                                    idToPlace = (blockType == 38) ? (byte)41 : (byte)44;
                                else // Looking West -> rising towards West (-X) -> East stair
                                    idToPlace = (blockType == 38) ? (byte)42 : (byte)45;
                            }
                        }
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

    private void BreakBlockInstantly(Vector3Int gridPos, Vector3 voxelCenter, byte existing)
    {
        if (VoxelWorld.Instance == null) return;

        if (existing == 21 || existing == 23)
        {
            List<Vector3Int> parts = FindConnectedWheelBlocks(gridPos);
            foreach (var partPos in parts)
            {
                Vector3 partCenter = new Vector3(partPos.x + 0.5f, partPos.y + 0.5f, partPos.z + 0.5f);
                bool suppressDrop = (partPos != gridPos && VoxelWorld.Instance.GetBlock(partCenter) == 23);
                VoxelWorld.Instance.ModifyBlock(partCenter, 0, suppressDrop);
                PlacedBlockRegistry.Instance?.Unregister(partPos);
            }
        }
        else
        {
            if (existing == 37)
            {
                FurnaceManager.Instance?.RemoveFurnace(gridPos);
            }
            VoxelWorld.Instance.ModifyBlock(voxelCenter, 0);
            PlacedBlockRegistry.Instance?.Unregister(gridPos);
        }
    }

    private float GetBlockHardness(byte blockType)
    {
        switch (blockType)
        {
            case 7:
            case 9:
            case 10:
            case 11:
                return 0.0f;

            case 12:
                return 0.2f;

            case 35:
                return 0.3f;

            case 4:
            case 5:
            case 8:
            case 34:
                return 0.8f;

            case 1:
            case 2:
            case 36:
            case 46:
            case 38: case 40: case 41: case 42:
                return 1.5f;

            case 3:
            case 30:
            case 31:
            case 32:
            case 33:
            case 37:
            case 47:
            case 39: case 43: case 44: case 45:
                return 3.0f;

            default:
                return 1.0f;
        }
    }

    private float GetBreakingSpeedMultiplier(byte blockType, Item heldItem)
    {
        ToolType preferredTool = ToolType.None;
        
        switch (blockType)
        {
            case 4:
            case 5:
            case 8:
            case 34:
                preferredTool = ToolType.Shovel;
                break;

            case 1:
            case 2:
            case 36:
            case 46:
            case 38: case 40: case 41: case 42:
                preferredTool = ToolType.Axe;
                break;

            case 3:
            case 30:
            case 31:
            case 32:
            case 33:
            case 37:
            case 47:
            case 39: case 43: case 44: case 45:
                preferredTool = ToolType.Pickaxe;
                break;
                
            case 12:
                preferredTool = ToolType.Sword;
                break;
        }

        ToolType toolType = heldItem != null ? heldItem.toolType : ToolType.None;
        ToolTier toolTier = heldItem != null ? heldItem.toolTier : ToolTier.None;

        bool isStoneBlock = (preferredTool == ToolType.Pickaxe);
        float penalty = 1.0f;
        if (isStoneBlock && toolType != ToolType.Pickaxe)
        {
            penalty = 0.33f;
        }

        if (toolType == preferredTool && preferredTool != ToolType.None)
        {
            switch (toolTier)
            {
                case ToolTier.Wood: return 2.5f * penalty;
                case ToolTier.Stone: return 5.0f * penalty;
                case ToolTier.Iron: return 8.0f * penalty;
                case ToolTier.Diamond: return 12.0f * penalty;
                default: return 1.0f * penalty;
            }
        }
        
        if (toolType == ToolType.Rake && (blockType == 4 || blockType == 5 || blockType == 12))
        {
            switch (toolTier)
            {
                case ToolTier.Wood: return 3.0f;
                case ToolTier.Stone: return 6.0f;
                case ToolTier.Iron: return 9.0f;
                case ToolTier.Diamond: return 12.0f;
                default: return 3.0f;
            }
        }

        if (toolType == ToolType.Sword && blockType == 12)
        {
            return 10.0f;
        }

        return 1.0f * penalty;
    }

    private void UpdateCrackOverlay(Vector3Int pos, float progress)
    {
        if (progress <= 0.0f || progress >= 1.0f)
        {
            if (crackOverlay != null) crackOverlay.SetActive(false);
            return;
        }

        if (crackOverlay == null)
        {
            crackOverlay = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(crackOverlay.GetComponent<Collider>());
            
            crackRenderer = crackOverlay.GetComponent<MeshRenderer>();
            
            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            crackMaterial = new Material(s);
            if (s.name.Contains("Standard"))
            {
                crackMaterial.SetFloat("_Mode", 3f);
                crackMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                crackMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                crackMaterial.SetInt("_ZWrite", 0);
                crackMaterial.EnableKeyword("_ALPHABLEND_ON");
                crackMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            else
            {
                crackMaterial.SetFloat("_Surface", 1f);
                crackMaterial.SetFloat("_Blend", 0f);
                crackMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                crackMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                crackMaterial.SetInt("_ZWrite", 0);
                crackMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            
            crackMaterial.color = Color.white;
            if (crackMaterial.HasProperty("_BaseColor"))
                crackMaterial.SetColor("_BaseColor", Color.white);
            
            crackRenderer.sharedMaterial = crackMaterial;
        }

        crackOverlay.SetActive(true);
        crackOverlay.transform.position = new Vector3(pos.x + 0.5f, pos.y + 0.5f, pos.z + 0.5f);
        crackOverlay.transform.localScale = new Vector3(1.002f, 1.002f, 1.002f);

        int crackIndex = Mathf.Clamp(Mathf.FloorToInt(progress * 10), 0, 9);
        if (crackTextures != null && crackTextures.Length > crackIndex)
        {
            crackMaterial.mainTexture = crackTextures[crackIndex];
            if (crackMaterial.HasProperty("_BaseMap"))
                crackMaterial.SetTexture("_BaseMap", crackTextures[crackIndex]);
        }
    }

    private void ResetBreakingState()
    {
        isBreakingBlock = false;
        breakProgress = 0f;
        if (crackOverlay != null)
        {
            crackOverlay.SetActive(false);
        }
    }

    private void InitializeCrackTextures()
    {
        crackTextures = new Texture2D[10];
        for (int i = 0; i < 10; i++)
        {
            float progress = (i + 1) / 10f;
            crackTextures[i] = GenerateCrackTexture(progress);
        }
    }

    private Texture2D GenerateCrackTexture(float progress)
    {
        Texture2D tex = new Texture2D(32, 32);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] cols = new Color[32 * 32];
        for (int i = 0; i < cols.Length; i++) cols[i] = Color.clear;
        tex.SetPixels(cols);

        Random.State oldState = Random.state;
        Random.InitState(1337);

        int numLines = Mathf.FloorToInt(progress * 15);
        for (int l = 0; l < numLines; l++)
        {
            int x = 16;
            int y = 16;
            int length = Random.Range(3, 8 + Mathf.FloorToInt(progress * 12));
            for (int step = 0; step < length; step++)
            {
                if (x >= 0 && x < 32 && y >= 0 && y < 32)
                {
                    tex.SetPixel(x, y, new Color(0.12f, 0.12f, 0.12f, 0.85f));
                }
                
                int dx = 0;
                int dy = 0;
                if (Random.value < 0.5f) dx = (Random.value < 0.5f) ? -1 : 1;
                if (Random.value < 0.5f) dy = (Random.value < 0.5f) ? -1 : 1;
                x += dx;
                y += dy;
            }
        }
        tex.Apply();
        Random.state = oldState;
        return tex;
    }
}
