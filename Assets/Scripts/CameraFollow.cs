using UnityEngine;

/// <summary>
/// CS464 Assignment 02: A Camera That Follows.
/// Follows the player in LateUpdate using exact frame-rate independent exponential smoothing (1 - e^(-k dt))
/// and look-ahead aiming based on flat player velocity.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 5f, -8f);
    [SerializeField] private float sharpness = 5f;
    [SerializeField] private float lookAhead = 0.35f; // seconds

    public float Sharpness => sharpness;
    public float LookAhead => lookAhead;
    public Vector3 Offset { get => offset; set => offset = value; }

    private void Awake()
    {
        if (target == null)
        {
            var player = FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                target = player.transform;
            }
        }
    }

    private void Start()
    {
        // Snap immediately to initial follow pose so camera does not pan from scene origin
        if (target != null)
        {
            Vector3 aimPoint = target.position;
            transform.position = aimPoint + offset;
            transform.LookAt(aimPoint);
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            var player = FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                target = player.transform;
            }
            else
            {
                return;
            }
        }

        // 1. Look-ahead aims where the player will be: add flat velocity * lookAhead
        Vector3 aimPoint = target.position;
        Rigidbody targetRb = target.GetComponent<Rigidbody>();
        if (targetRb != null)
        {
            Vector3 flatVel = targetRb.linearVelocity;
            flatVel.y = 0f;
            aimPoint += flatVel * lookAhead;
        }

        Vector3 wanted = aimPoint + offset;

        // 2. Exact exponential smoothing: blend = 1 - e^(-sharpness * dt)
        float blend = 1f - Mathf.Exp(-sharpness * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, wanted, blend);

        // 3. Look at the aim point, not raw player, so look-ahead is preserved
        transform.LookAt(aimPoint);
    }
}
