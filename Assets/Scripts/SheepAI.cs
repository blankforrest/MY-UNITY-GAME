using System.Collections.Generic;
using UnityEngine;

public class SheepAI : MonoBehaviour
{
    public enum State
    {
        Idle,
        Wandering,
        Scared,
        Dead
    }

    [Header("Stats")]
    public float maxHealth = 10f;
    public float currentHealth = 10f;
    public float walkSpeed = 1.6f;
    public float fleeSpeed = 3.6f;

    [Header("Physics")]
    public float gravity = -18f;
    public float jumpForce = 5.5f;

    private State currentState = State.Idle;
    private CharacterController cc;
    private PlayerController player;

    private float stateTimer = 0f;
    private float verticalVelocity = 0f;
    private Vector3 wanderDirection = Vector3.zero;
    private Vector3 fleeDirection = Vector3.zero;

    // Model Transforms
    private Transform modelRoot;
    private Transform head;
    private Transform frontLeftLeg;
    private Transform frontRightLeg;
    private Transform backLeftLeg;
    private Transform backRightLeg;

    private List<Renderer> modelRenderers = new List<Renderer>();
    private Dictionary<Renderer, Color> originalColors = new Dictionary<Renderer, Color>();

    private float flashTimer = 0f;
    private bool isFlashing = false;

    // Sheep Palette
    private Color woolColor = new Color(0.95f, 0.95f, 0.95f);
    private Color skinColor = new Color(0.92f, 0.82f, 0.77f);
    private Color eyeColor = new Color(0.12f, 0.12f, 0.12f);
    private Color hoofColor = new Color(0.24f, 0.22f, 0.20f);

    void Start()
    {
        // Add CharacterController if missing
        cc = GetComponent<CharacterController>();
        if (cc == null)
        {
            cc = gameObject.AddComponent<CharacterController>();
        }

        // Configure CharacterController to fit 1-block spaces nicely
        cc.height = 0.8f;
        cc.radius = 0.38f;
        cc.center = new Vector3(0, 0.4f, 0);
        cc.stepOffset = 0.4f;

        // Procedurally assemble the voxel-style sheep model
        BuildModel();

        // Cache player reference
        player = FindFirstObjectByType<PlayerController>();

        // Ignore collision with all foliage mesh colliders in the world
        foreach (var chunk in FindObjectsByType<Chunk>(FindObjectsSortMode.None))
        {
            if (chunk != null)
            {
                Transform foliage = chunk.transform.Find("Foliage");
                if (foliage != null)
                {
                    Collider foliageCollider = foliage.GetComponent<Collider>();
                    if (foliageCollider != null && cc != null)
                    {
                        Physics.IgnoreCollision(cc, foliageCollider, true);
                    }
                }
            }
        }

        // Start idling
        SwitchState(State.Idle);
    }

    void Update()
    {
        if (currentState == State.Dead)
        {
            ApplyDeadState();
            return;
        }

        HandleDamageFlashing();
        ApplyGravity();

        // State machine processing
        switch (currentState)
        {
            case State.Idle:
                ProcessIdle();
                break;
            case State.Wandering:
                ProcessWandering();
                break;
            case State.Scared:
                ProcessScared();
                break;
        }

        // Animate model details
        AnimateModelParts();
    }

    private void BuildModel()
    {
        modelRoot = new GameObject("ModelRoot").transform;
        modelRoot.SetParent(transform, false);
        modelRoot.localPosition = Vector3.zero;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material standardMat = new Material(shader);
        if (standardMat.HasProperty("_Glossiness")) standardMat.SetFloat("_Glossiness", 0.0f);
        if (standardMat.HasProperty("_Smoothness")) standardMat.SetFloat("_Smoothness", 0.0f);

        // Bulky woolly body
        CreateBox("Body Wool", new Vector3(0.68f, 0.65f, 0.95f), new Vector3(0f, 0.65f, -0.05f), woolColor, standardMat);

        // Head Pivot
        GameObject headPivot = new GameObject("HeadPivot");
        headPivot.transform.SetParent(modelRoot, false);
        headPivot.transform.localPosition = new Vector3(0f, 0.9f, 0.45f);
        head = headPivot.transform;

        // Head Wool cap
        CreateBox("HeadWool", new Vector3(0.40f, 0.25f, 0.32f), new Vector3(0f, 0.15f, -0.02f), woolColor, standardMat, head);

        // Head Face
        CreateBox("HeadFace", new Vector3(0.34f, 0.32f, 0.36f), new Vector3(0f, -0.05f, 0.05f), skinColor, standardMat, head);

        // Snout
        CreateBox("Snout", new Vector3(0.22f, 0.14f, 0.18f), new Vector3(0f, -0.1f, 0.24f), skinColor * 0.92f, standardMat, head);

        // Eyes (outset by 0.005f on front/sides to prevent Z-fighting and jittering)
        CreateBox("LeftEye", new Vector3(0.04f, 0.04f, 0.04f), new Vector3(-0.155f, 0.02f, 0.215f), eyeColor, standardMat, head);
        CreateBox("RightEye", new Vector3(0.04f, 0.04f, 0.04f), new Vector3(0.155f, 0.02f, 0.215f), eyeColor, standardMat, head);

        // Drooping side ears
        CreateBox("LeftEar", new Vector3(0.18f, 0.08f, 0.08f), new Vector3(-0.22f, 0.05f, -0.02f), skinColor, standardMat, head);
        CreateBox("RightEar", new Vector3(0.18f, 0.08f, 0.08f), new Vector3(0.22f, 0.05f, -0.02f), skinColor, standardMat, head);

        // Thin skin legs
        frontLeftLeg = CreateLeg("FrontLeftLeg", new Vector3(-0.18f, 0.35f, 0.3f), standardMat);
        frontRightLeg = CreateLeg("FrontRightLeg", new Vector3(0.18f, 0.35f, 0.3f), standardMat);
        backLeftLeg = CreateLeg("BackLeftLeg", new Vector3(-0.18f, 0.35f, -0.32f), standardMat);
        backRightLeg = CreateLeg("BackRightLeg", new Vector3(0.18f, 0.35f, -0.32f), standardMat);
    }

    private void CreateBox(string boxName, Vector3 size, Vector3 localPos, Color color, Material baseMat, Transform parent = null)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = boxName;
        Destroy(cube.GetComponent<BoxCollider>()); // keep collision clean and only on controller

        cube.transform.SetParent(parent == null ? modelRoot : parent, false);
        cube.transform.localPosition = localPos;
        cube.transform.localScale = size;

        Renderer r = cube.GetComponent<Renderer>();
        r.material = new Material(baseMat);
        r.material.color = color;
        if (r.material.HasProperty("_BaseColor"))
        {
            r.material.SetColor("_BaseColor", color);
        }
        modelRenderers.Add(r);
        originalColors[r] = color;
    }

    private Transform CreateLeg(string legName, Vector3 pivotPos, Material baseMat)
    {
        GameObject legRoot = new GameObject(legName);
        legRoot.transform.SetParent(modelRoot, false);
        legRoot.transform.localPosition = pivotPos;

        // Leg bone (skin colored)
        CreateBox(legName + "_Bone", new Vector3(0.12f, 0.45f, 0.12f), new Vector3(0f, -0.2f, 0f), skinColor, baseMat, legRoot.transform);
        // Hoof
        CreateBox(legName + "_Foot", new Vector3(0.14f, 0.08f, 0.15f), new Vector3(0f, -0.42f, 0.01f), hoofColor, baseMat, legRoot.transform);

        return legRoot.transform;
    }

    private void ApplyGravity()
    {
        if (cc.isGrounded)
        {
            verticalVelocity = -0.5f;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }
    }

    private void SwitchState(State newState)
    {
        currentState = newState;

        if (newState == State.Idle)
        {
            stateTimer = Random.Range(2f, 5f);
            wanderDirection = Vector3.zero;
        }
        else if (newState == State.Wandering)
        {
            stateTimer = Random.Range(3f, 6f);
            float randAngle = Random.Range(0f, 360f);
            wanderDirection = new Vector3(Mathf.Sin(randAngle), 0, Mathf.Cos(randAngle)).normalized;
        }
        else if (newState == State.Scared)
        {
            stateTimer = 4.0f; // Flee for 4 seconds
            if (player != null)
            {
                Vector3 away = (transform.position - player.transform.position);
                away.y = 0f;
                fleeDirection = away.normalized;
                if (fleeDirection == Vector3.zero)
                {
                    float randAngle = Random.Range(0f, 360f);
                    fleeDirection = new Vector3(Mathf.Sin(randAngle), 0, Mathf.Cos(randAngle)).normalized;
                }
            }
            else
            {
                float randAngle = Random.Range(0f, 360f);
                fleeDirection = new Vector3(Mathf.Sin(randAngle), 0, Mathf.Cos(randAngle)).normalized;
            }
        }
    }

    private void ProcessIdle()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            SwitchState(State.Wandering);
        }

        cc.Move(new Vector3(0, verticalVelocity, 0) * Time.deltaTime);
    }

    private void ProcessWandering()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            SwitchState(State.Idle);
            return;
        }

        CheckAndJumpIfBlocked();
        CheckCliffsAndWater();

        Vector3 hMove = wanderDirection * walkSpeed;
        Vector3 finalMove = new Vector3(hMove.x, verticalVelocity, hMove.z);
        cc.Move(finalMove * Time.deltaTime);

        if (wanderDirection != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(wanderDirection, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 5f);
        }
    }

    private void ProcessScared()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            SwitchState(State.Idle);
            return;
        }

        // Periodically update flee direction to push away from player
        if (player != null)
        {
            Vector3 away = (transform.position - player.transform.position);
            away.y = 0f;
            if (away.sqrMagnitude > 0.01f)
            {
                fleeDirection = Vector3.Slerp(fleeDirection, away.normalized, Time.deltaTime * 3f);
            }
        }

        CheckAndJumpIfBlocked();
        CheckCliffsAndWaterFleeing();

        Vector3 hMove = fleeDirection * fleeSpeed;
        Vector3 finalMove = new Vector3(hMove.x, verticalVelocity, hMove.z);
        cc.Move(finalMove * Time.deltaTime);

        if (fleeDirection != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(fleeDirection, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 10f);
        }
    }

    private void CheckAndJumpIfBlocked()
    {
        if (cc.isGrounded)
        {
            Vector3 rayStart = transform.position + Vector3.up * 0.15f;
            RaycastHit hit;
            Vector3 fwd = currentState == State.Scared ? fleeDirection : wanderDirection;
            if (Physics.Raycast(rayStart, fwd, out hit, 0.65f))
            {
                if (!hit.collider.isTrigger)
                {
                    verticalVelocity = jumpForce;
                }
            }
        }
    }

    private void CheckCliffsAndWater()
    {
        Vector3 checkPos = transform.position + transform.forward * 0.6f + Vector3.up * 0.1f;
        RaycastHit hit;
        if (!Physics.Raycast(checkPos, Vector3.down, out hit, 2.0f))
        {
            wanderDirection = -wanderDirection;
        }
        else if (hit.collider != null && hit.collider.name.Contains("Water"))
        {
            wanderDirection = -wanderDirection;
        }
    }

    private void CheckCliffsAndWaterFleeing()
    {
        Vector3 checkPos = transform.position + transform.forward * 0.6f + Vector3.up * 0.1f;
        RaycastHit hit;
        if (!Physics.Raycast(checkPos, Vector3.down, out hit, 2.0f))
        {
            // Pick a random side direction
            float turn = Random.value < 0.5f ? 90f : -90f;
            fleeDirection = Quaternion.Euler(0, turn, 0) * fleeDirection;
        }
        else if (hit.collider != null && hit.collider.name.Contains("Water"))
        {
            float turn = Random.value < 0.5f ? 90f : -90f;
            fleeDirection = Quaternion.Euler(0, turn, 0) * fleeDirection;
        }
    }

    private void AnimateModelParts()
    {
        float speed = new Vector3(cc.velocity.x, 0, cc.velocity.z).magnitude;

        if (speed > 0.1f)
        {
            float swingMult = currentState == State.Scared ? 14f : 8f;
            float swingAngle = Mathf.Sin(Time.time * swingMult) * 30f;

            frontLeftLeg.localRotation = Quaternion.Euler(swingAngle, 0f, 0f);
            frontRightLeg.localRotation = Quaternion.Euler(-swingAngle, 0f, 0f);
            backLeftLeg.localRotation = Quaternion.Euler(-swingAngle, 0f, 0f);
            backRightLeg.localRotation = Quaternion.Euler(swingAngle, 0f, 0f);

            head.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * swingMult) * 3f, 0f, 0f);
        }
        else
        {
            frontLeftLeg.localRotation = Quaternion.identity;
            frontRightLeg.localRotation = Quaternion.identity;
            backLeftLeg.localRotation = Quaternion.identity;
            backRightLeg.localRotation = Quaternion.identity;

            head.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 1.5f) * 2f, 0f, 0f);
        }
    }

    public void TakeDamage(float amount)
    {
        if (currentState == State.Dead) return;

        currentHealth -= amount;
        TriggerDamageFlash();

        if (player != null)
        {
            Vector3 pushDir = (transform.position - player.transform.position);
            pushDir.y = 0f;
            pushDir.Normalize();
            cc.Move((pushDir * 4.0f + Vector3.up * 2f) * Time.deltaTime);
        }

        if (currentHealth <= 0f)
        {
            Die();
        }
        else
        {
            SwitchState(State.Scared);
        }
    }

    private void TriggerDamageFlash()
    {
        isFlashing = true;
        flashTimer = 0.18f;

        foreach (var r in modelRenderers)
        {
            r.material.color = Color.red;
            if (r.material.HasProperty("_BaseColor"))
            {
                r.material.SetColor("_BaseColor", Color.red);
            }
        }
    }

    private void HandleDamageFlashing()
    {
        if (isFlashing)
        {
            flashTimer -= Time.deltaTime;
            if (flashTimer <= 0f)
            {
                isFlashing = false;
                foreach (var r in modelRenderers)
                {
                    if (originalColors.TryGetValue(r, out Color c))
                    {
                        r.material.color = c;
                        if (r.material.HasProperty("_BaseColor"))
                        {
                            r.material.SetColor("_BaseColor", c);
                        }
                    }
                }
            }
        }
    }

    private void Die()
    {
        currentState = State.Dead;
        cc.enabled = false;
        stateTimer = 2.0f;

        modelRoot.localRotation = Quaternion.Euler(0f, 0f, -80f);

        foreach (var r in modelRenderers)
        {
            Color deathColor = new Color(0.8f, 0.1f, 0.1f);
            r.material.color = deathColor;
            if (r.material.HasProperty("_BaseColor"))
            {
                r.material.SetColor("_BaseColor", deathColor);
            }
        }

        // Spawn drops (Apple as food/mutton out of the box!)
        Item appleDrop = StarterItems.CreateItemInstance("Apple", 0, Color.red);
        if (appleDrop != null)
        {
            int amount = Random.Range(1, 3);
            DroppedItem.Spawn(appleDrop, amount, transform.position + Vector3.up * 0.4f);
        }
    }

    private void ApplyDeadState()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer > 0f)
        {
            float scale = Mathf.Clamp01(stateTimer / 2.0f);
            modelRoot.localScale = new Vector3(scale, scale, scale);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public static float GetTopSolidBlockY(float x, float z)
    {
        int ix = Mathf.FloorToInt(x);
        int iz = Mathf.FloorToInt(z);
        if (VoxelWorld.Instance == null) return 0f;

        for (int y = VoxelData.ChunkHeight - 1; y >= 0; y--)
        {
            byte block = VoxelWorld.Instance.GetBlock(new Vector3(ix, y, iz));
            if (block != 0 && block != 7)
            {
                return y + 1f;
            }
        }
        return 0f;
    }
}
