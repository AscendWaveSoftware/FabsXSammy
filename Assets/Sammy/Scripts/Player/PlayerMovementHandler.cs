using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerMovementHandler : MonoBehaviour
{
    [Header("Player Movement Settings")]
    [SerializeField] private float m_acceleration = 2.5f;
    [SerializeField] private float m_moveSpeed = 10f;
    [SerializeField] private bool m_characterSpriteFlip = false;
    [SerializeField] private SpriteRenderer m_sp;

    private Rigidbody m_rb;
    private Vector2 m_Velocity;
    private bool m_facingLocked;
    private bool m_movementLocked;

    public float CurrentMaxPlanarSpeed => Mathf.Max(0f, m_moveSpeed / 4f);

    /// <summary>
    /// World direction the sprite is currently facing. The body never rotates,
    /// so the facing lives entirely in the sprite flip.
    /// </summary>
    public Vector3 FacingDirection => m_characterSpriteFlip ? -transform.right : transform.right;

    private void Awake()
    {
        m_rb = GetComponent<Rigidbody>();
        m_rb.useGravity = false;
        m_rb.constraints = RigidbodyConstraints.FreezeRotationX |
                           RigidbodyConstraints.FreezeRotationY |
                           RigidbodyConstraints.FreezeRotationZ |
                           RigidbodyConstraints.FreezePositionY;
    }

    private void OnDisable()
    {
        m_Velocity = Vector2.zero;
        m_facingLocked = false;
        m_movementLocked = false;
        StopPlanarMovement();
    }

    private void Update()
    {
        if (!m_facingLocked)
        {
            if (m_Velocity.x > 0.01f)
                m_characterSpriteFlip = false;
            else if (m_Velocity.x < -0.01f)
                m_characterSpriteFlip = true;
        }

        if (m_sp != null)
            m_sp.flipX = m_characterSpriteFlip;
    }

    private void FixedUpdate() => Movement();

    public void OnMove(InputValue _value)
    {
        if (!isActiveAndEnabled)
        {
            m_Velocity = Vector2.zero;
            return;
        }

        m_Velocity = _value.Get<Vector2>();

        if (m_Velocity.sqrMagnitude <= 0.0001f)
            StopPlanarMovement();
    }

    public void SetFacingLocked(bool _locked) => m_facingLocked = _locked;

    public void SetMovementLocked(bool _locked)
    {
        m_movementLocked = _locked;

        if (_locked)
            StopPlanarMovement();
    }

    public void SetMoveSpeed(float _newSpeed)
    {
        m_moveSpeed = Mathf.Max(0f, _newSpeed);
    }

    public void AddMoveSpeedBonus(float _bonus)
    {
        m_moveSpeed = Mathf.Max(0f, m_moveSpeed + _bonus);
    }

    public void AddMoveSpeedPercentage(float _percentage)
    {
        if (_percentage <= 0f)
            return;

        m_moveSpeed *= 1f + _percentage;
    }

    private void Movement()
    {
        if (m_rb == null)
            return;

        if (m_movementLocked || m_Velocity.sqrMagnitude <= 0.0001f)
        {
            StopPlanarMovement();
            return;
        }

        // Preserve analogue input magnitude so slow movement genuinely maps to Walk.
        // Normalizing here made every non-zero stick input reach full running speed.
        Vector3 localInput = Vector3.ClampMagnitude(new Vector3(m_Velocity.x, 0f, m_Velocity.y), 1f);
        Vector3 targetSpeed = transform.TransformDirection(localInput) * CurrentMaxPlanarSpeed;
        Vector3 velocityXZ = new Vector3(m_rb.linearVelocity.x, 0f, m_rb.linearVelocity.z);
        Vector3 speedDif = targetSpeed - velocityXZ;

        float accelRate = (targetSpeed.magnitude > 0.01f) ? 5 : m_acceleration;

        Vector3 movement = new Vector3(
            Mathf.Pow(Mathf.Abs(speedDif.x) * accelRate, 1) * Mathf.Sign(speedDif.x),
            0,
            Mathf.Pow(Mathf.Abs(speedDif.z) * accelRate, 1) * Mathf.Sign(speedDif.z)
        );
        m_rb.AddForce(movement, ForceMode.Force);
    }

    private void StopPlanarMovement()
    {
        if (m_rb == null)
            return;

        m_rb.linearVelocity = new Vector3(0f, m_rb.linearVelocity.y, 0f);
    }

    private void OnValidate()
    {
        m_acceleration = Mathf.Max(0f, m_acceleration);
        m_moveSpeed = Mathf.Max(0f, m_moveSpeed);
    }
}
