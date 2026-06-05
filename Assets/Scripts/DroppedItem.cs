using UnityEngine;

/// <summary>
/// A floating, rotating world item that can be picked up by the player.
/// Spawned when an item is dragged out of the inventory.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class DroppedItem : MonoBehaviour
{
    public Item item;
    public int  amount    = 1;
    public byte blockType = 1; // 1=Grass, 2=Dirt — controls dropped cube appearance

    [Tooltip("If set, this mesh is used instead of the default mini-cube (e.g. for tool items).")]
    public Mesh     overrideMesh;
    [Tooltip("Material used with overrideMesh. Falls back to chunk material if null.")]
    public Material overrideMaterial;

    [Header("Animation")]
    public float bobSpeed  = 2f;
    public float bobHeight = 0.18f;
    public float spinSpeed = 80f;

    private float startY;
    private SphereCollider trigger;
    private Rigidbody      rb;
    private bool           landed = false;
    private float          settleTimer = 0f;
    private float          aliveTime   = 0f;  // prevents premature settling on ledges

    void Start()
    {
        startY = transform.position.y;

        trigger = GetComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius    = 0.6f;
        trigger.enabled   = false;
        Invoke(nameof(EnablePickup), 0.6f);

        // Non-trigger collider so Rigidbody lands on the terrain mesh
        BoxCollider groundCol = gameObject.AddComponent<BoxCollider>();
        groundCol.size      = new Vector3(0.32f, 0.32f, 0.32f);
        groundCol.isTrigger = false;

        // Add gravity
        rb                       = gameObject.AddComponent<Rigidbody>();
        rb.mass                  = 0.5f;
        rb.linearDamping         = 0.05f;  // low drag so items fall naturally
        rb.useGravity            = true;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous; // no tunneling
        rb.constraints           = RigidbodyConstraints.FreezeRotationX
                                 | RigidbodyConstraints.FreezeRotationZ;

        // Small downward nudge so items don't hover if spawned exactly on a surface
        rb.AddForce(Vector3.down * 2f, ForceMode.Impulse);

        BuildMiniCube();

    }

    void EnablePickup() => trigger.enabled = true;

    // ── 3D mini cube ──────────────────────────────────────────────────────────

    void BuildMiniCube()
    {
        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale    = Vector3.one;

        MeshFilter   mf = visual.AddComponent<MeshFilter>();
        MeshRenderer mr = visual.AddComponent<MeshRenderer>();

        if (overrideMesh != null)
        {
            // Use custom mesh (e.g. wrench tool shape)
            mf.mesh = overrideMesh;
            if (overrideMaterial != null)
            {
                mr.material = overrideMaterial;
            }
            else
            {
                Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                Material mat = new Material(s);
                mat.color = new Color(1f, 0.78f, 0.12f); // gold fallback
                mr.material = mat;
            }
        }
        else
        {
            if (blockType == 9 || blockType == 10 || blockType == 11) // Flower varieties (Rose, Dandelion, Iris)
            {
                mf.mesh = BuildCrossedQuadsMesh(0.18f);
                if (VoxelWorld.Instance != null && VoxelWorld.Instance.foliageMaterial != null)
                {
                    mr.sharedMaterial = VoxelWorld.Instance.foliageMaterial;
                }
                else
                {
                    Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    Material mat = new Material(s);
                    mat.mainTexture = GrassTextureGenerator.Create();
                    mr.sharedMaterial = mat;
                }
            }
            else
            {
                // Default: mini voxel cube
                const float SIZE = 0.175f;
                mf.mesh = BuildCubeMesh(SIZE);

                if (VoxelWorld.Instance != null && VoxelWorld.Instance.chunkMaterial != null)
                {
                    mr.sharedMaterial = VoxelWorld.Instance.chunkMaterial;
                }
                else
                {
                    Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    Material mat = new Material(s);
                    mat.mainTexture = GrassTextureGenerator.Create();
                    mat.color = Color.white;
                    mr.sharedMaterial = mat;
                }
            }
        }
    }

    Mesh BuildCubeMesh(float s)
    {
        // 6 faces, each with 4 vertices
        var verts = new System.Collections.Generic.List<Vector3>();
        var uvs2  = new System.Collections.Generic.List<Vector2>();
        var tris  = new System.Collections.Generic.List<int>();

        // face order matches VoxelData.faceChecks: back,front,top,bottom,left,right
        Vector3[][] faceVerts = {
            // Back  (z-)
            new[]{new Vector3(-s,-s,-s), new Vector3(-s, s,-s), new Vector3( s,-s,-s), new Vector3( s, s,-s)},
            // Front (z+)
            new[]{new Vector3( s,-s, s), new Vector3( s, s, s), new Vector3(-s,-s, s), new Vector3(-s, s, s)},
            // Top   (y+)
            new[]{new Vector3(-s, s,-s), new Vector3(-s, s, s), new Vector3( s, s,-s), new Vector3( s, s, s)},
            // Bottom(y-)
            new[]{new Vector3( s,-s,-s), new Vector3( s,-s, s), new Vector3(-s,-s,-s), new Vector3(-s,-s, s)},
            // Left  (x-)
            new[]{new Vector3(-s,-s, s), new Vector3(-s, s, s), new Vector3(-s,-s,-s), new Vector3(-s, s,-s)},
            // Right (x+)
            new[]{new Vector3( s,-s,-s), new Vector3( s, s,-s), new Vector3( s,-s, s), new Vector3( s, s, s)},
        };

        for (int face = 0; face < 6; face++)
        {
            int vi = verts.Count;
            foreach (var v in faceVerts[face]) verts.Add(v);

            Vector2[] fuv = GrassTextureGenerator.GetBlockUVs(face, blockType);
            uvs2.Add(fuv[0]); uvs2.Add(fuv[1]); uvs2.Add(fuv[2]); uvs2.Add(fuv[3]);

            tris.Add(vi); tris.Add(vi+1); tris.Add(vi+2);
            tris.Add(vi+2); tris.Add(vi+1); tris.Add(vi+3);
        }

        var mesh = new Mesh();
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs2);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        return mesh;
    }

    Mesh BuildCrossedQuadsMesh(float s)
    {
        var verts = new System.Collections.Generic.List<Vector3>();
        var uvs2  = new System.Collections.Generic.List<Vector2>();
        var tris  = new System.Collections.Generic.List<int>();

        // Two vertical crossed quads, centered on X/Z (y from -s to s)
        Vector3[] q1 = new Vector3[]
        {
            new Vector3(-s, -s, -s), // bottom-left
            new Vector3(-s,  s, -s), // top-left
            new Vector3( s, -s,  s), // bottom-right
            new Vector3( s,  s,  s)  // top-right
        };
        Vector3[] q2 = new Vector3[]
        {
            new Vector3( s, -s, -s),
            new Vector3( s,  s, -s),
            new Vector3(-s, -s,  s),
            new Vector3(-s,  s,  s)
        };

        Vector2[] uvFlow = GrassTextureGenerator.GetBlockUVs(0, blockType); // flower tile

        // Quad 1 Front
        int v0 = verts.Count;
        verts.AddRange(q1); uvs2.AddRange(uvFlow);
        tris.Add(v0); tris.Add(v0+1); tris.Add(v0+2);
        tris.Add(v0+2); tris.Add(v0+1); tris.Add(v0+3);

        // Quad 1 Back
        int v1 = verts.Count;
        verts.AddRange(q1); uvs2.AddRange(uvFlow);
        tris.Add(v1+2); tris.Add(v1+1); tris.Add(v1);
        tris.Add(v1+3); tris.Add(v1+1); tris.Add(v1+2);

        // Quad 2 Front
        int v2 = verts.Count;
        verts.AddRange(q2); uvs2.AddRange(uvFlow);
        tris.Add(v2); tris.Add(v2+1); tris.Add(v2+2);
        tris.Add(v2+2); tris.Add(v2+1); tris.Add(v2+3);

        // Quad 2 Back
        int v3 = verts.Count;
        verts.AddRange(q2); uvs2.AddRange(uvFlow);
        tris.Add(v3+2); tris.Add(v3+1); tris.Add(v3);
        tris.Add(v3+3); tris.Add(v3+1); tris.Add(v3+2);

        var mesh = new Mesh();
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs2);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        return mesh;
    }

    void Update()
    {
        if (!landed)
        {
            transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
            aliveTime += Time.deltaTime;

            // Safety fallback: if item hasn't landed after 8s, force-settle it
            if (aliveTime > 8f)
                Settle();

            return;
        }

        // Landed: check ground still exists, then float bob + spin
        bool groundStillThere = Physics.Raycast(
            transform.position, Vector3.down, 0.6f, ~0, QueryTriggerInteraction.Ignore);

        if (!groundStillThere)
        {
            // Terrain below was removed — start falling again
            landed      = false;
            settleTimer = 0f;
            aliveTime   = 0f;
            rb.isKinematic = false;
            rb.AddForce(Vector3.down * 3f, ForceMode.Impulse);
            return;
        }

        float y = startY + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = new Vector3(transform.position.x, y, transform.position.z);
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
    }

    void OnCollisionStay(Collision collision)
    {
        // ── Settle logic ──────────────────────────────────────────────────────
        if (!landed)
        {
            if (aliveTime >= 0.5f && rb != null && rb.linearVelocity.magnitude < 0.3f)
            {
                settleTimer += Time.fixedDeltaTime;
                if (settleTimer > 0.15f) Settle();
            }
            else settleTimer = 0f;
        }

        // ── Stack same-type items ─────────────────────────────────────────────
        DroppedItem other = collision.gameObject.GetComponent<DroppedItem>();
        if (other == null) other = collision.gameObject.GetComponentInParent<DroppedItem>();
        if (other != null && other != this && other.item != null && item != null && other.item.itemName == item.itemName && other.blockType == blockType)
        {
            // Older item absorbs newer one
            if (aliveTime >= other.aliveTime)
            {
                amount += other.amount;
                Destroy(other.gameObject);
            }
        }
    }

    void OnCollisionExit(Collision collision)
    {
        // Reset settle timer when item leaves a surface (still falling)
        if (!landed) settleTimer = 0f;
    }

    void Settle()
    {
        if (landed) return;
        landed = true;
        startY = transform.position.y + 0.25f;
        rb.isKinematic = true;
        transform.position = new Vector3(transform.position.x, startY, transform.position.z);
    }


    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        bool picked = false;

        // Try hotbar first (it's always visible)
        if (Hotbar.Instance != null)
            picked = Hotbar.Instance.TryAddItem(item, amount);

        // Fall back to inventory if hotbar is full
        if (!picked && Inventory.Instance != null)
            Inventory.Instance.Add(item, amount);

        Destroy(gameObject);
    }

    public static DroppedItem Spawn(Item item, int amount, Vector3 worldPosition, byte blockType = 1)
    {
        if (item == null) return null;
        worldPosition.y += 0.3f;
        GameObject go = new GameObject($"DroppedItem_{item.itemName}");
        go.transform.position = worldPosition;
        DroppedItem dropped  = go.AddComponent<DroppedItem>();
        dropped.item      = item;
        dropped.amount    = amount;
        dropped.blockType = blockType;
        return dropped;
    }

    public static DroppedItem Spawn(Item item, int amount, byte blockType = 1)
    {
        if (item == null) return null;
        Camera cam     = Camera.main;
        Vector3 fwd    = cam != null ? cam.transform.forward : Vector3.forward;
        fwd.y = 0f; fwd.Normalize();
        Vector3 pos    = (cam != null ? cam.transform.position : Vector3.zero) + fwd * 1.5f;
        pos.y -= 0.5f;
        return Spawn(item, amount, pos, blockType);
    }
}
