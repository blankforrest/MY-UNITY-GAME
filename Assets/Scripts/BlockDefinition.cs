using UnityEngine;

[System.Serializable]
public class BlockDefinition
{
    [Header("Identity")]
    public string blockName = "Custom Block";
    public byte blockID = 0;

    [Header("Visuals")]
    [Tooltip("Inventory icon sprite.")]
    public Sprite inventoryIcon;
    [Tooltip("Custom texture for the top face. If null, falls back to default design.")]
    public Texture2D textureTop;
    [Tooltip("Custom texture for the side faces. If null, falls back to default design.")]
    public Texture2D textureSide;
    [Tooltip("Custom texture for the bottom face. If null, falls back to default design.")]
    public Texture2D textureBottom;

    [Header("Drops")]
    public Item dropItem;
    public int dropAmount = 1;

    [Header("Properties")]
    public float hardness = 1.0f;
    public ToolType preferredTool = ToolType.None;
    public bool isSolid = true;
    public bool isTransparent = false;

    [Header("Lighting")]
    public bool emitsLight = false;
    [Range(0, 15)] public int lightLevel = 8;

    [Header("Audio")]
    public AudioClip stepSound;
    public AudioClip placeSound;
    public AudioClip breakSound;

    [Header("Vehicle Behavior")]
    public bool isVehicleBlock = false;
    [Tooltip("Type of vehicle component (e.g., None, Wheel, Propeller, ControlBlock).")]
    public string vehiclePartType = "None";
}
