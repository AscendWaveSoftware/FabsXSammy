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
    //private bool m_IsMoving;

    private void Start()
    {
        m_rb = GetComponent<Rigidbody>();
        m_rb.useGravity = false;
        m_rb.constraints = RigidbodyConstraints.FreezeRotationX |
                           RigidbodyConstraints.FreezeRotationY |
                           RigidbodyConstraints.FreezeRotationZ |
                           RigidbodyConstraints.FreezePositionY;
    }

    private void Update()
    {
        m_sp.flipX = m_characterSpriteFlip;
    }

    private void FixedUpdate() => Movement();

    public void OnMove(InputValue _value)
    {
        m_Velocity = _value.Get<Vector2>();

        if (m_Velocity.x > 0)
            m_characterSpriteFlip = false;
        else if (m_Velocity.x < 0)
            m_characterSpriteFlip = true;
    }

    public void SetMoveSpeed(float _newSpeed)
    {
        m_moveSpeed = _newSpeed;
    }

    public void AddMoveSpeedBonus(float _bonus)
    {
        m_moveSpeed += _bonus;
    }

    private void Movement()
    {
        Vector3 localInput = new Vector3(m_Velocity.x, 0f, m_Velocity.y).normalized;
        Vector3 targetSpeed = transform.TransformDirection(localInput) * (m_moveSpeed / 4);
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
}
