//Code by Fabian Schmiedel
using System.Collections;
using System.Collections.Generic;
using UnityEngine; 

public class Turret : MonoBehaviour
{
    [SerializeField] private SO_TowerStats m_towerStats;
    [SerializeField] private float m_rotationSpeed = 300f;

    [Header("Idle Rotation Settings")]
    [SerializeField] private float m_idleRotationSpeed = 50f;      // Geschwindigkeit beim Suchen
    [SerializeField] private float m_idleChangeInterval = 2f;    // Wie oft (in Sek.) ein neuer Winkel gesucht wird
    [SerializeField] private float m_maxIdleAngle = 45f;         // Maximaler Winkel von der Startrotation aus

    public GameObject m_projectilePrefab;
    public Transform m_projectileSpawnPoint;

    private float m_nextAttackTime;
    private GameObject m_currentTarget;
    private AudioSource m_audioSource;
    private Team m_team;

    // Variablen für die Idle-Drehung
    private Quaternion m_initialRotation;
    private Quaternion m_targetIdleRotation;
    private float m_idleTimer;

    private void Awake()
    {
        m_team = m_towerStats.m_team;
        m_audioSource = GetComponent<AudioSource>();
        // Startrotation merken, damit der Turm sich nicht komplett im Kreis dreht
        m_initialRotation = transform.rotation;
        m_targetIdleRotation = transform.rotation;
    }

    private void Update()
    {
        if (m_currentTarget == null || !IsTargetInRange(m_currentTarget))
        {
            FindNewTarget();
        }

        // ÄNDERUNG HIER: Wenn kein Ziel da ist, führe IdleRotation aus und springe per return raus
        if (m_currentTarget == null)
        {
            IdleRotation();
            return;
        }

        RotateTowardsTarget();

        if (Time.time >= m_nextAttackTime)
        {
            AttackCurrentTarget();
            m_nextAttackTime = Time.time + m_towerStats.m_attackCooldown;
        }
    }

    private void IdleRotation()
    {
        m_idleTimer += Time.deltaTime;

        // Wenn die Zeit abgelaufen ist, wähle einen neuen zufälligen Winkel basierend auf der Startrichtung
        if (m_idleTimer >= m_idleChangeInterval)
        {
            float randomAngle = Random.Range(-m_maxIdleAngle, m_maxIdleAngle);
            // Nutzt die Startrotation als Basis und dreht sich auf der Y-Achse hin/her
            m_targetIdleRotation = m_initialRotation * Quaternion.Euler(0f, randomAngle, 0f);
            m_idleTimer = 0f;
        }

        // Sanft zum zufälligen Idle-Winkel rotieren
        transform.rotation = Quaternion.RotateTowards(transform.rotation, m_targetIdleRotation, m_idleRotationSpeed * Time.deltaTime);
    }

    private bool IsTargetInRange(GameObject target)
    {
        float distSqr = (target.transform.position - transform.position).sqrMagnitude;
        float rangeSqr = m_towerStats.m_attackRange * m_towerStats.m_attackRange;
        return distSqr <= rangeSqr;
    }

    private void FindNewTarget()
    {
        List<AI_Minion> enemies = m_team == Team.Blue ? EntityManager.RedMinions : EntityManager.BlueMinions;
        float closestDistSqr = float.MaxValue;
        GameObject closest = null;
        Vector3 pos = transform.position;
        float rangeSqr = m_towerStats.m_attackRange * m_towerStats.m_attackRange;

        foreach (AI_Minion minion in enemies)
        {
            if (minion == null) continue;
            float distSqr = (minion.transform.position - pos).sqrMagnitude;
            if (distSqr <= rangeSqr && distSqr < closestDistSqr)
            {
                closestDistSqr = distSqr;
                closest = minion.gameObject;
            }
        }
        m_currentTarget = closest;
    }

    private void RotateTowardsTarget()
    {
        if (m_currentTarget == null) return;
        Vector3 direction = m_currentTarget.transform.position - transform.position;
        direction.y = 0f;
        if (direction != Vector3.zero)
        {
            Quaternion baseLookRotation = Quaternion.LookRotation(direction);
            Quaternion targetRotation = baseLookRotation * Quaternion.Euler(0f, -90f, 0f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, m_rotationSpeed * Time.deltaTime);
        }
    }

    private void AttackCurrentTarget()
    {
        if (m_currentTarget == null) return;
        MOBA_Health health = m_currentTarget.GetComponent<MOBA_Health>();
        if (health != null && health.m_destroyed)
        {
            m_currentTarget = null;
            return;
        }
        else if (!health.m_destroyed)
        {
            PlayAttackSound();
            StartCoroutine(SpawnProjectile());
        }
    }

    private IEnumerator DestroyProjectile(Projectile _projectile)
    {
        yield return new WaitForSeconds(3);
        if (_projectile != null) Destroy(_projectile.gameObject);
    }

    private IEnumerator SpawnProjectile()
    {
        yield return new WaitForSeconds(0.5f);
        GameObject projectileObj = Instantiate(m_projectilePrefab, m_projectileSpawnPoint.position, Quaternion.identity);
        Projectile projectile = projectileObj.GetComponent<Projectile>();
        projectile.m_damage = m_towerStats.m_damage;
        projectile.m_speed = m_towerStats.m_projectileSpeed;
        if (m_currentTarget) projectile.SeekTarget(m_currentTarget.transform);
        StartCoroutine(DestroyProjectile(projectile));
        m_projectileSpawnPoint.GetComponentInChildren<ParticleSystem>().Play();
    }

    private void PlayAttackSound()
    {
        GameObject audioObj = new GameObject("AttackSound");
        audioObj.transform.parent = this.transform;
        audioObj.transform.localPosition = Vector3.zero;
        AudioSource source = audioObj.AddComponent<AudioSource>();
        source.clip = m_audioSource.clip;
        source.volume = m_audioSource.volume;
        source.pitch = Random.Range(0.8f, 1.1f);
        source.spatialBlend = m_audioSource.spatialBlend;
        source.minDistance = m_audioSource.minDistance;
        source.maxDistance = m_audioSource.maxDistance;
        source.rolloffMode = m_audioSource.rolloffMode;
        source.outputAudioMixerGroup = m_audioSource.outputAudioMixerGroup;
        source.Play();
        float duration = source.clip.length / source.pitch;
        StartCoroutine(DisableAfterTime(audioObj, duration));
    }

    private IEnumerator DisableAfterTime(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (obj != null)
        {
            obj.SetActive(false);
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }
}
