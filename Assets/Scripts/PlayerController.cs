using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// CS464 Assignment 02: A Player That Feels Right.
/// Implements responsive movement, camera-relative steering, coyote time, jump buffering,
/// asymmetric fall gravity, visible CheckSphere ground check, and squashing juice.
/// Input System only (activeInputHandler = 1).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float speed = 5f;

    [Header("Jump Tuning")]
    [SerializeField] private float jumpHeight = 1.8f; // metres
    [SerializeField] private float fallMultiplier = 2.2f;
    [SerializeField] private float coyoteTime = 0.1f; // seconds
    [SerializeField] private float jumpBuffer = 0.12f; // seconds

    [Header("Ground Check")]
    [SerializeField] private Transform feet;
    [SerializeField] private float checkRadius = 0.2f;
    [SerializeField] private LayerMask groundMask;

    [Header("Juice")]
    [SerializeField] private Transform body;
    [SerializeField] private float squashDuration = 0.25f;

    private Rigidbody rb;
    private CapsuleCollider col;
    private Transform cam;
    private Vector3 move; // built in Update, applied in FixedUpdate
    private bool isGrounded;
    private bool wasGrounded;
    private float airTime;
    private bool jumpQueued;
    private float lastGroundedTime = -1f;
    private float lastPressedTime = -1f;
    private Coroutine squashCoroutine;

    private InputAction moveAction;
    private InputAction jumpAction;

    public float Speed => speed;
    public float JumpHeight => jumpHeight;
    public float FallMultiplier => fallMultiplier;
    public float CoyoteTime => coyoteTime;
    public float JumpBuffer => jumpBuffer;
    public bool Grounded => isGrounded;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<CapsuleCollider>();

        // Physics stability: freeze rotation and interpolate
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // Frictionless physics material so player never sticks to walls mid-air
        if (col != null && col.sharedMaterial == null)
        {
            PhysicsMaterial frictionless = new PhysicsMaterial("PlayerFrictionless")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
            col.sharedMaterial = frictionless;
        }

        // Cache main camera transform
        if (Camera.main != null)
        {
            cam = Camera.main.transform;
        }

        // Auto-locate feet child if not explicitly assigned
        if (feet == null)
        {
            feet = transform.Find("Feet");
        }

        // Auto-locate body child if not explicitly assigned
        if (body == null)
        {
            body = transform.Find("Body");
        }

        // Auto-assign Ground layer mask if left unset (layer named "Ground")
        if (groundMask.value == 0)
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer != -1)
            {
                groundMask = 1 << groundLayer;
            }
            else
            {
                groundMask = ~0; // fallback to everything
            }
        }

        // Bind Input System actions
        try
        {
            if (InputSystem.actions != null)
            {
                moveAction = InputSystem.actions.FindAction("Move");
                jumpAction = InputSystem.actions.FindAction("Jump");
            }
        }
        catch
        {
            // Actions not available; movement will be inert
        }
    }

    private void Update()
    {
        // Keep camera reference fresh in case of scene switches
        if (cam == null && Camera.main != null)
        {
            cam = Camera.main.transform;
        }

        // --- 1. Read input (Input System) ---
        float h = 0f;
        float v = 0f;
        bool jumpPressed = false;

        if (moveAction != null && moveAction.enabled)
        {
            Vector2 val = moveAction.ReadValue<Vector2>();
            h = val.x;
            v = val.y;
        }

        if (jumpAction != null && jumpAction.enabled)
        {
            jumpPressed = jumpAction.WasPressedThisFrame();
        }

        // --- 2. Camera-relative movement vector ---
        Vector3 forward = cam != null ? cam.forward : Vector3.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 right = cam != null ? cam.right : Vector3.right;
        right.y = 0f;
        right.Normalize();

        Vector3 rawMove = forward * v + right * h;
        move = Vector3.ClampMagnitude(rawMove, 1f);

        // --- 3. Ground check ---
        Vector3 feetPos = feet != null ? feet.position : (transform.position + Vector3.down * 0.9f);
        isGrounded = Physics.CheckSphere(feetPos, checkRadius, groundMask, QueryTriggerInteraction.Ignore);

        // --- 4. Juice on real landing (airTime > 0.1s) ---
        if (!isGrounded)
        {
            airTime += Time.deltaTime;
        }
        else
        {
            if (!wasGrounded && airTime > 0.1f)
            {
                if (squashCoroutine != null)
                {
                    StopCoroutine(squashCoroutine);
                }
                squashCoroutine = StartCoroutine(Squash());
            }
            airTime = 0f;
        }
        wasGrounded = isGrounded;

        // --- 5. Jump windows: Coyote time & Jump buffer ---
        // Stamp ground only when no jump is queued and player isn't rising
        if (isGrounded && !jumpQueued && rb.linearVelocity.y <= 0f)
        {
            lastGroundedTime = Time.time;
        }
        if (jumpPressed)
        {
            lastPressedTime = Time.time;
        }

        bool canJump = (Time.time - lastGroundedTime) <= coyoteTime;
        bool wantJump = (Time.time - lastPressedTime) <= jumpBuffer;

        if (canJump && wantJump)
        {
            jumpQueued = true;
            // Both timestamps used up immediately
            lastGroundedTime = -1f;
            lastPressedTime = -1f;
        }
    }

    private void FixedUpdate()
    {
        // Apply horizontal movement, preserving vertical velocity
        rb.linearVelocity = new Vector3(move.x * speed, rb.linearVelocity.y, move.z * speed);

        // Apply queued jump impulse
        if (jumpQueued)
        {
            jumpQueued = false;

            // Zero vertical velocity first so jumps taken while falling reach full height
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

            float g = -Physics.gravity.y; // 9.81
            float v0 = Mathf.Sqrt(2f * g * jumpHeight);
            rb.AddForce(Vector3.up * rb.mass * v0, ForceMode.Impulse);
        }

        // Apply extra fall gravity for asymmetric arc (fall faster than rise)
        if (rb.linearVelocity.y < 0f)
        {
            rb.AddForce(Physics.gravity * (fallMultiplier - 1f), ForceMode.Acceleration);
        }
    }

    private IEnumerator Squash()
    {
        if (body == null) yield break;

        // Volume-preserving squash: at y = 0.7, x and z are 1 / sqrt(0.7) ≈ 1.195
        Vector3 squashed = new Vector3(1.195f, 0.7f, 1.195f);
        Vector3 rest = Vector3.one;

        for (float elapsed = 0f; elapsed < squashDuration; elapsed += Time.deltaTime)
        {
            float t = Mathf.Clamp01(elapsed / squashDuration);
            float p = Ease.OutBack(t);
            body.localScale = Vector3.LerpUnclamped(squashed, rest, p);
            yield return null;
        }

        body.localScale = rest;
        squashCoroutine = null;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 feetPos = feet != null ? feet.position : (transform.position + Vector3.down * 0.9f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(feetPos, checkRadius);
    }
}
