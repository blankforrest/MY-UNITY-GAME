using UnityEngine;

/// <summary>
/// Procedurally builds a beautiful 3-blade propeller visual using Unity primitive shapes.
/// Propeller axis = local Z (matches PropellerBlock.Rotate(0,0,speed) spin).
/// </summary>
public static class PropellerMeshBuilder
{
    public static void Apply(GameObject go, float radius, float depth)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        // Rich brass color for the propeller blades, dark steel for the hub
        Material hubMat = new Material(sh) { color = new Color(0.24f, 0.24f, 0.26f) };
        Material bladeMat = new Material(sh) { color = new Color(0.82f, 0.58f, 0.16f) };

        // 1. Central Hub (Cylinder aligned with Z-axis via 90° X rotation)
        GameObject hub = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        hub.name = "PropellerHub";
        hub.transform.SetParent(go.transform, false);
        hub.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        
        float hubLength = (depth > 1.0f) ? 2.0f : 0.8f;
        float hubZPos   = (depth > 1.0f) ? 0f : -0.1f;
        
        hub.transform.localPosition = new Vector3(0f, 0f, hubZPos);
        hub.transform.localScale = new Vector3(radius * 0.35f, hubLength * 0.5f, radius * 0.35f);
        Object.DestroyImmediate(hub.GetComponent<Collider>());
        hub.GetComponent<MeshRenderer>().sharedMaterial = hubMat;

        // 2. Nose Cone (Sphere at the front tip of the hub)
        GameObject nose = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        nose.name = "NoseCone";
        nose.transform.SetParent(go.transform, false);
        
        float noseZPos = (depth > 1.0f) ? 1.0f : 0.3f;
        nose.transform.localPosition = new Vector3(0f, 0f, noseZPos);
        nose.transform.localScale = new Vector3(radius * 0.35f, radius * 0.35f, depth * 0.4f);
        Object.DestroyImmediate(nose.GetComponent<Collider>());
        nose.GetComponent<MeshRenderer>().sharedMaterial = hubMat;

        float zOffset = 0f;
        if (depth > 1.0f)
        {
            zOffset = depth * 0.425f; // Shifts the blades 0.85 units (near the top) for the 2.0-unit deep propeller
        }

        // 3. Three Angled Blades (spaced 120° apart in the local XY plane)
        for (int i = 0; i < 3; i++)
        {
            float angle = i * 120f;
            CreateBlade(go, radius, depth, angle, bladeMat, zOffset);
        }
    }

    private static void CreateBlade(GameObject parent, float radius, float depth, float angleDeg, Material mat, float zOffset)
    {
        GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blade.name = $"Blade_{angleDeg}";
        blade.transform.SetParent(parent.transform, false);

        // Rotation: rotate around Z to position the blade, and apply pitch (rotate around Y) for propulsion angle
        Quaternion radialRotation = Quaternion.Euler(0f, 0f, angleDeg);
        Quaternion bladePitch = Quaternion.Euler(0f, 28f, 0f);

        blade.transform.localRotation = radialRotation * bladePitch;

        // Calculate radial offsets
        float innerRadius = radius * 0.15f;
        float outerRadius = radius * 0.95f;
        float midRadius = (innerRadius + outerRadius) * 0.5f;
        float length = outerRadius - innerRadius;

        // Position at the midpoint of the blade radius, shifted by zOffset along local Z
        blade.transform.localPosition = radialRotation * new Vector3(0f, midRadius, 0f) + new Vector3(0f, 0f, zOffset);

        // Scale: X = width, Y = length, Z = thickness
        blade.transform.localScale = new Vector3(
            radius * 0.22f, // width of blade
            length,         // length of blade
            radius * 0.05f  // thickness of blade
        );

        Object.DestroyImmediate(blade.GetComponent<Collider>());
        blade.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }
}
