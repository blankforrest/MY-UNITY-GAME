using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// Handles all slot drag-drop via New Input System mouse polling.
/// Attach to any persistent GameObject in the scene.
/// </summary>
public class DragDropManager : MonoBehaviour
{
    public static DragDropManager Instance { get; private set; }

    private SlotUI      dragging;
    private Image       ghost;
    private bool        isDragging;
    private Vector2     dragStartPos;
    private const float DragThreshold = 8f;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        GameObject ghostGO = new GameObject("DDGhost", typeof(RectTransform), typeof(Image));
        ghostGO.transform.SetParent(canvas.transform, false);

        RectTransform rt = ghostGO.GetComponent<RectTransform>();
        rt.sizeDelta  = new Vector2(50f, 50f);
        rt.anchorMin  = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);

        ghost = ghostGO.GetComponent<Image>();
        ghost.raycastTarget = false;
        ghost.color         = new Color(1f, 1f, 1f, 0.85f);
        ghost.enabled       = false;

        SlotUI.Ghost = ghost;
    }

    void Update()
    {
        if (Mouse.current == null) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();

        // ── Press ──────────────────────────────────────────────────────────────
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            SlotUI hit = RaycastSlot(mousePos);
            if (hit != null)
            {
                var data = hit.GetItemData();
                if (data != null && data.item != null)
                {
                    dragging     = hit;
                    dragStartPos = mousePos;
                    isDragging   = false;
                }
            }
        }

        // ── Drag threshold ─────────────────────────────────────────────────────
        if (dragging != null && !isDragging && Mouse.current.leftButton.isPressed)
        {
            if (Vector2.Distance(mousePos, dragStartPos) > DragThreshold)
            {
                isDragging = true;
                var data = dragging.GetItemData();
                if (ghost != null && data?.item != null)
                {
                    ghost.sprite  = data.item.icon;
                    ghost.enabled = true;
                    ghost.transform.SetAsLastSibling();
                }
                dragging.SetIconVisible(false);
            }
        }

        // ── Move ghost ─────────────────────────────────────────────────────────
        if (isDragging && ghost != null)
            ghost.rectTransform.position = mousePos;

        // ── Release ────────────────────────────────────────────────────────────
        if (Mouse.current.leftButton.wasReleasedThisFrame && dragging != null)
        {
            if (ghost != null) ghost.enabled = false;

            if (isDragging)
            {
                SlotUI target = RaycastSlot(mousePos);

                if (target != null && target != dragging)
                {
                    // Prevent dropping into crafting output
                    if (target.owner == SlotUI.Owner.CraftingOutput)
                    {
                        dragging.Refresh();
                        isDragging = false;
                        dragging = null;
                        return;
                    }

                    // Handling drag FROM CraftingOutput
                    if (dragging.owner == SlotUI.Owner.CraftingOutput)
                    {
                        var src = dragging.GetItemData(); // The crafted item
                        var dst = target.GetItemData(); // The target slot contents

                        if (src != null && src.item != null)
                        {
                            if (dst == null || dst.item == null)
                            {
                                // Success! Drop into empty slot
                                target.WriteItemData(new InventorySlot(src.item, src.amount), silent: true);
                                Inventory.Instance?.ConsumeCraftingInputs();
                                dragging.Refresh();
                                target.Refresh();
                                Inventory.Instance?.onInventoryChangedCallback?.Invoke();
                            }
                            else if (dst.item.itemName == src.item.itemName)
                            {
                                // Success! Stack with existing item
                                target.WriteItemData(new InventorySlot(src.item, dst.amount + src.amount), silent: true);
                                Inventory.Instance?.ConsumeCraftingInputs();
                                dragging.Refresh();
                                target.Refresh();
                                Inventory.Instance?.onInventoryChangedCallback?.Invoke();
                            }
                            else
                            {
                                // Different item, reject
                                dragging.Refresh();
                            }
                        }
                        else
                        {
                            dragging.Refresh();
                        }
                    }
                    else
                    {
                        // Copy data FIRST before any writes
                        var src = dragging.GetItemData();
                        var dst = target.GetItemData();
                        var srcCopy = src != null ? new InventorySlot(src.item, src.amount) : null;
                        var dstCopy = dst != null ? new InventorySlot(dst.item, dst.amount) : null;

                        // Write silently (no mid-swap callbacks)
                        dragging.WriteItemData(dstCopy, silent: true);
                        target.WriteItemData(srcCopy, silent: true);

                        // One single refresh for everything
                        dragging.Refresh();
                        target.Refresh();
                        Inventory.Instance?.onInventoryChangedCallback?.Invoke();
                    }
                }
                else if (target == null && !InventoryUI.IsInventoryOpen && !IsPointerOverUI())
                {
                    // World-drop: only when inventory is closed and cursor is outside all UI
                    var data = dragging.GetItemData();
                    if (data?.item != null)
                    {
                        DroppedItem dropped = DroppedItem.Spawn(data.item, data.amount, (byte)data.item.blockTypeID);

                        // If this is the wrench tool, apply the 3D wrench mesh instead of a mini-cube
                        if (dropped != null && WrenchItem.Instance != null
                            && data.item.itemID == WrenchItem.Instance.wrenchItemID)
                        {
                            dropped.overrideMesh = WrenchItem.BuildWrenchMesh();
                            // overrideMaterial left null → DroppedItem uses gold fallback color
                        }

                        if (dragging.owner == SlotUI.Owner.CraftingOutput)
                        {
                            Inventory.Instance?.ConsumeCraftingInputs();
                        }
                        else
                        {
                            dragging.WriteItemData(null);
                        }
                    }
                    dragging.Refresh();
                }
                else
                {
                    // Released on panel background or same slot — restore icon
                    dragging.Refresh();
                }
            }
            else
            {
                // Click (no drag threshold met)
                if (dragging.owner == SlotUI.Owner.CraftingOutput)
                {
                    // Clicked crafting output -> try to auto-craft and add to hotbar/inventory!
                    var src = dragging.GetItemData();
                    if (src != null && src.item != null)
                    {
                        bool added = false;
                        if (Hotbar.Instance != null)
                            added = Hotbar.Instance.TryAddItem(src.item, src.amount);
                        if (!added && Inventory.Instance != null)
                            added = Inventory.Instance.Add(src.item, src.amount);

                        if (added)
                        {
                            Inventory.Instance?.ConsumeCraftingInputs();
                            Inventory.Instance?.onInventoryChangedCallback?.Invoke();
                        }
                    }
                }
                dragging.Refresh(); // no drag occurred — restore icon
            }

            isDragging = false;
            dragging   = null;
        }
    }

    // ── Raycast helpers ────────────────────────────────────────────────────────

    SlotUI RaycastSlot(Vector2 screenPos)
    {
        if (EventSystem.current == null) return null;

        var ped     = new PointerEventData(EventSystem.current) { position = screenPos };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(ped, results);

        foreach (var r in results)
        {
            SlotUI s = r.gameObject.GetComponentInParent<SlotUI>();
            if (s != null) return s;
        }
        return null;
    }

    /// <summary>Returns true if the mouse is currently over any UI element (used by PlayerInteraction).</summary>
    public static bool IsPointerOverUI()
    {
        if (Mouse.current == null || EventSystem.current == null) return false;

        var ped     = new PointerEventData(EventSystem.current)
                      { position = Mouse.current.position.ReadValue() };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(ped, results);
        return results.Count > 0;
    }
}
