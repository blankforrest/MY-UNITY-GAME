using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SerializableBlockEntry
{
    public int x;
    public int y;
    public int z;
    public int typeID;
}

[System.Serializable]
public class SerializableBlueprint
{
    public List<SerializableBlockEntry> blocks = new List<SerializableBlockEntry>();
    public int dimX;
    public int dimY;
    public int dimZ;
    public float totalMass;
    public float totalDurability;
}

public static class BlueprintSerializer
{
    public static void SaveBlueprint(StructureBlueprint blueprint, string path)
    {
        if (blueprint == null) return;

        SerializableBlueprint sb = new SerializableBlueprint();
        sb.dimX = blueprint.dimensions.x;
        sb.dimY = blueprint.dimensions.y;
        sb.dimZ = blueprint.dimensions.z;
        sb.totalMass = blueprint.totalMass;
        sb.totalDurability = blueprint.totalDurability;

        foreach (var entry in blueprint.blocks)
        {
            sb.blocks.Add(new SerializableBlockEntry
            {
                x = entry.localPosition.x,
                y = entry.localPosition.y,
                z = entry.localPosition.z,
                typeID = entry.blockTypeID
            });
        }

        string json = JsonUtility.ToJson(sb, true);
        System.IO.File.WriteAllText(path, json);
    }

    public static StructureBlueprint LoadBlueprint(string path)
    {
        if (!System.IO.File.Exists(path)) return null;

        try
        {
            string json = System.IO.File.ReadAllText(path);
            SerializableBlueprint sb = JsonUtility.FromJson<SerializableBlueprint>(json);

            StructureBlueprint bp = new StructureBlueprint();
            bp.dimensions = new Vector3Int(sb.dimX, sb.dimY, sb.dimZ);
            bp.totalMass = sb.totalMass;
            bp.totalDurability = sb.totalDurability;

            foreach (var entry in sb.blocks)
            {
                bp.blocks.Add(new BlockEntry(
                    new Vector3Int(entry.x, entry.y, entry.z),
                    entry.typeID
                ));
            }
            return bp;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[BlueprintSerializer] Failed to load blueprint from {path}: {ex.Message}");
            return null;
        }
    }
}
