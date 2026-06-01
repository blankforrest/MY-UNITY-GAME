using UnityEngine;

/// <summary>
/// Builds a realistic wheel visual using Unity primitive cylinders.
/// Wheel axis = local X (matches WheelBlock.Rotate(speed,0,0) spin).
///
/// Unity Cylinder defaults: height=2 along localY (from -1 to +1), radius=0.5 in localXZ.
/// After Euler(0,0,90): localY points along world X, localXZ maps to world YZ.
///
/// Correct scale:
///   scaleX = radius * 2   → controls visual radius in world Y  (0.5 * scaleX = radius)
///   scaleY = width  * 0.5 → controls visual width  in world X  (2   * scaleY = width)
///   scaleZ = radius * 2   → controls visual radius in world Z
/// </summary>
public static class WheelMeshBuilder
{
    public static void Apply(GameObject go, float radius, float visualWidth)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        Material rubber = new Material(sh) { color = new Color(0.07f, 0.07f, 0.07f) };
        Material rim    = new Material(sh) { color = new Color(0.60f, 0.62f, 0.65f) };
        Material hub    = new Material(sh) { color = new Color(0.18f, 0.18f, 0.22f) };

        CreateCylinder(go, "Tire", radius,          visualWidth,         rubber);
        CreateCylinder(go, "Rim",  radius * 0.65f,  visualWidth * 0.88f, rim);
        CreateCylinder(go, "Hub",  radius * 0.13f,  visualWidth * 0.90f, hub);

        for (int i = 0; i < 6; i++)
            CreateSpoke(go, radius, visualWidth, i * 60f, hub);
    }

    static void CreateCylinder(GameObject parent, string label,
                                float radius, float visualWidth, Material mat)
    {
        GameObject cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cyl.name = label;
        cyl.transform.SetParent(parent.transform, false);
        cyl.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

        // After 90° Z rotation:  localY → world X,  localXZ → world YZ
        // scaleX = radius*2  →  world-Y radius = 0.5 * (radius*2) = radius  ✓
        // scaleY = width*0.5 →  world-X width  = 2   * (width*0.5) = width  ✓
        // scaleZ = radius*2  →  world-Z radius = 0.5 * (radius*2) = radius  ✓
        cyl.transform.localScale = new Vector3(
            radius * 2f,
            visualWidth * 0.5f,
            radius * 2f);

        Object.DestroyImmediate(cyl.GetComponent<Collider>());
        cyl.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    static void CreateSpoke(GameObject parent, float outerRadius, float visualWidth,
                             float angleDeg, Material mat)
    {
        GameObject spoke = GameObject.CreatePrimitive(PrimitiveType.Cube);
        spoke.name = "Spoke";
        spoke.transform.SetParent(parent.transform, false);

        // Rotate around X so spokes fan out in the wheel's YZ plane
        Quaternion rot = Quaternion.Euler(angleDeg, 0f, 0f);
        spoke.transform.localRotation = rot;

        // Span from hub edge to rim inner edge
        float spokeStart  = outerRadius * 0.15f;
        float spokeEnd    = outerRadius * 0.62f;
        float spokeMid    = (spokeStart + spokeEnd) * 0.5f;
        float spokeLength = spokeEnd - spokeStart;

        // Position at midpoint along spoke direction
        spoke.transform.localPosition = rot * new Vector3(0f, spokeMid, 0f);

        // scaleX = along wheel axis, scaleY = spoke length, scaleZ = spoke thickness
        spoke.transform.localScale = new Vector3(
            visualWidth * 0.5f,
            spokeLength,
            outerRadius * 0.08f);

        Object.DestroyImmediate(spoke.GetComponent<Collider>());
        spoke.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }
}
