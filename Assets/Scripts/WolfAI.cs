using System.Collections.Generic;
using UnityEngine;

public class WolfAI : MonoBehaviour
{
    public enum State
    {
        Idle,
        Wandering,
        Chasing,
        Attacking,
        Dead
    }

    [Header("Stats")]
    public float maxHealth = 20f;
    public float currentHealth = 20f;
    public float walkSpeed = 2.0f;
    public float chaseSpeed = 4.5f;
    public float detectionRange = 14f;
    public float attackRange = 1.6f;
    public float attackDamage = 2.0f;
    public float attackCooldown = 1.2f;

    [Header("Physics")]
    public float gravity = -18f;
    public float jumpForce = 5.5f;

    private State currentState = State.Idle;
    private CharacterController cc;
    private PlayerController player;

    private float stateTimer = 0f;
    private float lastAttackTime = 0f;
    private float verticalVelocity = 0f;
    private Vector3 wanderDirection = Vector3.zero;

    // Model Transforms
    private Transform modelRoot;
    private Transform head;
    private Transform frontLeftLeg;
    private Transform frontRightLeg;
    private Transform backLeftLeg;
    private Transform backRightLeg;
    private Transform tail;

    private List<Renderer> modelRenderers = new List<Renderer>();
    private Dictionary<Renderer, Color> originalColors = new Dictionary<Renderer, Color>();

    private float flashTimer = 0f;
    private bool isFlashing = false;

    // Wolf Palette
    private Color bodyColor = new Color(0.32f, 0.32f, 0.35f);      // charcoal gray
    private Color underbellyColor = new Color(0.48f, 0.48f, 0.50f); // lighter belly gray
    private Color noseColor = new Color(0.12f, 0.12f, 0.12f);       // dark black snout
    private Color eyeColor = new Color(1.0f, 0.15f, 0.15f);        // glowing red eyes
    private Color earInnerColor = new Color(0.22f, 0.22f, 0.25f);   // inner ear

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

        // Procedurally assemble the voxel-style wolf model
        BuildModel();

        // Cache player reference
        player = FindFirstObjectByType<PlayerController>();

        // Start wandering
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

        // Target check
        if (player != null && !player.isCreativeMode && !player.IsDead)
        {
            float distToPlayer = Vector3.Distance(transform.position, player.transform.position);

            if (currentState != State.Chasing && currentState != State.Attacking && distToPlayer <= detectionRange)
            {
                SwitchState(State.Chasing);
            }
        }
        else if (currentState == State.Chasing || currentState == State.Attacking)
        {
            // Reset if player dies or enters creative mode
            SwitchState(State.Idle);
        }

        // State machine processing
        switch (currentState)
        {
            case State.Idle:
                ProcessIdle();
                break;
            case State.Wandering:
                ProcessWandering();
                break;
            case State.Chasing:
                ProcessChasing();
                break;
            case State.Attacking:
                ProcessAttacking();
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

        // Main Body Box
        CreateBox("Body", new Vector3(0.5f, 0.55f, 1.1f), new Vector3(0f, 0.6f, -0.05f), bodyColor, standardMat);

        // Underbelly Box
        CreateBox("Underbelly", new Vector3(0.48f, 0.1f, 0.7f), new Vector3(0f, 0.3f, -0.05f), underbellyColor, standardMat);

        // Neck/Mane (Survivalcraft wolves have bulkier front necks)
        CreateBox("NeckMane", new Vector3(0.52f, 0.6f, 0.4f), new Vector3(0f, 0.75f, 0.35f), bodyColor * 0.9f, standardMat);

        // Head Pivot
        GameObject headPivot = new GameObject("HeadPivot");
        headPivot.transform.SetParent(modelRoot, false);
        headPivot.transform.localPosition = new Vector3(0f, 0.95f, 0.45f);
        head = headPivot.transform;

        // Head Main Cube
        CreateBox("HeadCube", new Vector3(0.4f, 0.4f, 0.4f), new Vector3(0f, 0.1f, 0.1f), bodyColor, standardMat, head);

        // Snout
        CreateBox("Snout", new Vector3(0.2f, 0.15f, 0.25f), new Vector3(0f, 0.0f, 0.38f), bodyColor * 0.85f, standardMat, head);
        CreateBox("NosePuff", new Vector3(0.12f, 0.08f, 0.06f), new Vector3(0f, 0.05f, 0.52f), noseColor, standardMat, head);

        // Ears
        CreateBox("LeftEar", new Vector3(0.08f, 0.18f, 0.08f), new Vector3(-0.14f, 0.32f, 0.02f), bodyColor, standardMat, head);
        CreateBox("LeftEarInner", new Vector3(0.04f, 0.12f, 0.02f), new Vector3(-0.14f, 0.3f, 0.06f), earInnerColor, standardMat, head);

        CreateBox("RightEar", new Vector3(0.08f, 0.18f, 0.08f), new Vector3(0.14f, 0.32f, 0.02f), bodyColor, standardMat, head);
        CreateBox("RightEarInner", new Vector3(0.04f, 0.12f, 0.02f), new Vector3(0.14f, 0.3f, 0.06f), earInnerColor, standardMat, head);

        // Eyes
        CreateBox("LeftEye", new Vector3(0.05f, 0.05f, 0.03f), new Vector3(-0.12f, 0.12f, 0.31f), eyeColor, standardMat, head);
        CreateBox("RightEye", new Vector3(0.05f, 0.05f, 0.03f), new Vector3(0.12f, 0.12f, 0.31f), eyeColor, standardMat, head);

        // Legs
        frontLeftLeg = CreateLeg("FrontLeftLeg", new Vector3(-0.16f, 0.35f, 0.35f), standardMat);
        frontRightLeg = CreateLeg("FrontRightLeg", new Vector3(0.16f, 0.35f, 0.35f), standardMat);
        backLeftLeg = CreateLeg("BackLeftLeg", new Vector3(-0.16f, 0.35f, -0.38f), standardMat);
        backRightLeg = CreateLeg("BackRightLeg", new Vector3(0.16f, 0.35f, -0.38f), standardMat);

        // Tail (Pivot at body base edge)
        GameObject tailRoot = new GameObject("TailRoot");
        tailRoot.transform.SetParent(modelRoot, false);
        tailRoot.transform.localPosition = new Vector3(0f, 0.75f, -0.6f);
        tail = tailRoot.transform;

        CreateBox("TailCube", new Vector3(0.12f, 0.12f, 0.5f), new Vector3(0f, -0.1f, -0.22f), bodyColor, standardMat, tail);
        CreateBox("TailTip", new Vector3(0.1f, 0.1f, 0.15f), new Vector3(0f, -0.1f, -0.5f), underbellyColor, standardMat, tail);
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

        // Leg bone
        CreateBox(legName + "_Bone", new Vector3(0.12f, 0.45f, 0.12f), new Vector3(0f, -0.2f, 0f), bodyColor * 0.95f, baseMat, legRoot.transform);
        // Paw
        CreateBox(legName + "_Foot", new Vector3(0.14f, 0.08f, 0.16f), new Vector3(0f, -0.42f, 0.02f), noseColor, baseMat, legRoot.transform);

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
        stateTimer = Random.Range(2f, 4f);

        if (newState == State.Idle)
        {
            wanderDirection = Vector3.zero;
        }
        else if (newState == State.Wandering)
        {
            float randAngle = Random.Range(0f, 360f);
            wanderDirection = new Vector3(Mathf.Sin(randAngle), 0, Mathf.Cos(randAngle)).normalized;
        }
    }

    private void ProcessIdle()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            SwitchState(State.Wandering);
        }

        // Face velocity or drift
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

        // Stepping obstacles / jump trigger
        CheckAndJumpIfBlocked();

        // Turn around at steep cliffs/water
        CheckCliffsAndWater();

        // Move
        Vector3 hMove = wanderDirection * walkSpeed;
        Vector3 finalMove = new Vector3(hMove.x, verticalVelocity, hMove.z);
        cc.Move(finalMove * Time.deltaTime);

        // Rotation
        if (wanderDirection != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(wanderDirection, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 6f);
        }
    }

    private void ProcessChasing()
    {
        if (player == null || player.isCreativeMode || player.IsDead)
        {
            SwitchState(State.Idle);
            return;
        }

        float dist = Vector3.Distance(transform.position, player.transform.position);

        if (dist <= attackRange)
        {
            SwitchState(State.Attacking);
            return;
        }

        if (dist > detectionRange * 1.5f)
        {
            SwitchState(State.Idle);
            return;
        }

        // Direct chase direction (xz plane)
        Vector3 targetDir = (player.transform.position - transform.position);
        targetDir.y = 0f;
        targetDir.Normalize();

        CheckAndJumpIfBlocked();

        Vector3 hMove = targetDir * chaseSpeed;
        Vector3 finalMove = new Vector3(hMove.x, verticalVelocity, hMove.z);
        cc.Move(finalMove * Time.deltaTime);

        if (targetDir != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(targetDir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 10f);
        }
    }

    private void ProcessAttacking()
    {
        if (player == null || player.isCreativeMode || player.IsDead)
        {
            SwitchState(State.Idle);
            return;
        }

        float dist = Vector3.Distance(transform.position, player.transform.position);

        // If player moves away, resume chase
        if (dist > attackRange * 1.5f)
        {
            SwitchState(State.Chasing);
            return;
        }

        // Look at player
        Vector3 targetDir = (player.transform.position - transform.position);
        targetDir.y = 0f;
        targetDir.Normalize();
        if (targetDir != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(targetDir, Vector3.up);
        }

        // Cooldown and attack
        if (Time.time - lastAttackTime >= attackCooldown)
        {
            lastAttackTime = Time.time;

            // Simple attack leap visual
            verticalVelocity = 3.5f; 
            cc.Move((targetDir * chaseSpeed + new Vector3(0, verticalVelocity, 0)) * Time.deltaTime);

            player.TakeDamage(attackDamage);
        }
        else
        {
            // Just apply gravity and wait
            cc.Move(new Vector3(0, verticalVelocity, 0) * Time.deltaTime);
        }
    }

    private void CheckAndJumpIfBlocked()
    {
        if (cc.isGrounded)
        {
            Vector3 rayStart = transform.position + Vector3.up * 0.15f;
            RaycastHit hit;
            if (Physics.Raycast(rayStart, transform.forward, out hit, 0.65f))
            {
                // Solid obstacle -> jump
                if (!hit.collider.isTrigger)
                {
                    verticalVelocity = jumpForce;
                }
            }
        }
    }

    private void CheckCliffsAndWater()
    {
        // Check 1 block in front, and 1.5 blocks down
        Vector3 checkPos = transform.position + transform.forward * 0.6f + Vector3.up * 0.1f;
        RaycastHit hit;
        if (!Physics.Raycast(checkPos, Vector3.down, out hit, 2.0f))
        {
            // Cliff -> pick new direction
            wanderDirection = -wanderDirection;
        }
        else if (hit.collider != null && hit.collider.name.Contains("Water"))
        {
            // Water -> turn back
            wanderDirection = -wanderDirection;
        }
    }

    private void AnimateModelParts()
    {
        float speed = new Vector3(cc.velocity.x, 0, cc.velocity.z).magnitude;

        if (speed > 0.1f)
        {
            // Walk legs swinging
            float swingMult = currentState == State.Chasing ? 12f : 8f;
            float swingAngle = Mathf.Sin(Time.time * swingMult) * 35f;

            frontLeftLeg.localRotation = Quaternion.Euler(swingAngle, 0f, 0f);
            frontRightLeg.localRotation = Quaternion.Euler(-swingAngle, 0f, 0f);
            backLeftLeg.localRotation = Quaternion.Euler(-swingAngle, 0f, 0f);
            backRightLeg.localRotation = Quaternion.Euler(swingAngle, 0f, 0f);

            // Tail wags up/down when running/walking
            tail.localRotation = Quaternion.Euler(-20f + Mathf.Sin(Time.time * swingMult) * 10f, Mathf.Cos(Time.time * swingMult) * 8f, 0f);
            
            // Head headbob
            head.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * swingMult) * 4f, 0f, 0f);
        }
        else
        {
            // Reset local rotations when idle
            frontLeftLeg.localRotation = Quaternion.identity;
            frontRightLeg.localRotation = Quaternion.identity;
            backLeftLeg.localRotation = Quaternion.identity;
            backRightLeg.localRotation = Quaternion.identity;

            // Tail tail-wag
            tail.localRotation = Quaternion.Euler(-15f, Mathf.Sin(Time.time * 2f) * 5f, 0f);
            
            // Calm breathing headbob
            head.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 1.5f) * 2f, 0f, 0f);
        }
    }

    public void TakeDamage(float amount)
    {
        if (currentState == State.Dead) return;

        currentHealth -= amount;
        
        // Damage flash
        TriggerDamageFlash();

        // Knockback (pushes wolf back)
        if (player != null)
        {
            Vector3 pushDir = (transform.position - player.transform.position);
            pushDir.y = 0f;
            pushDir.Normalize();
            cc.Move((pushDir * 3.5f + Vector3.up * 2f) * Time.deltaTime);
        }

        if (currentHealth <= 0f)
        {
            Die();
        }
        else
        {
            // Enter Chase state immediately if hit
            if (currentState != State.Chasing && currentState != State.Attacking)
            {
                SwitchState(State.Chasing);
            }
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
                // Restore original colors
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
        cc.enabled = false; // Disable collisions
        stateTimer = 2.0f;  // Death display time

        // Tip over model Z-axis (Minecraft style)
        modelRoot.localRotation = Quaternion.Euler(0f, 0f, -80f);

        // Turn completely red
        foreach (var r in modelRenderers)
        {
            Color deathColor = new Color(0.8f, 0.1f, 0.1f);
            r.material.color = deathColor;
            if (r.material.HasProperty("_BaseColor"))
            {
                r.material.SetColor("_BaseColor", deathColor);
            }
        }

        // Spawn drops (Apples work as food out of the box!)
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

        // Shrink away
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
            if (block != 0 && block != 7) // solid, not air, not water
            {
                return y + 1f;
            }
        }
        return 0f;
    }
}
