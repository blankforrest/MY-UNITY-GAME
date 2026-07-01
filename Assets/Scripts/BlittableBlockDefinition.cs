using System.Runtime.InteropServices;
using UnityEngine;

[StructLayout(LayoutKind.Sequential)]
public struct BlittableBlockDefinition
{
    public byte blockID;
    public bool isSolid;
    public bool isTransparent;
    public bool emitsLight;
    public int lightLevel;
    public bool isVehicleBlock;

    // Face tiles: 0=back (South), 1=front (North), 2=top, 3=bottom, 4=left (West), 5=right (East)
    public int tileBack;
    public int tileFront;
    public int tileFrontLit;
    public int tileTop;
    public int tileBottom;
    public int tileLeft;
    public int tileRight;

    // Custom mesh reference details
    public bool hasCustomMesh;
    public int customMeshVertexStart;
    public int customMeshVertexCount;
    public int customMeshIndexStart;
    public int customMeshIndexCount;
}
