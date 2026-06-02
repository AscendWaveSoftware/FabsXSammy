using UnityEngine;

[CreateAssetMenu(fileName = "MinionStats", menuName = "Scriptable Objects/MinionStats")]
public class MinionStats : ScriptableObject
{
    public float m_health = 100f;
    public float m_moveSpeed = 5f;

    public float m_attackDamage = 10.0f;
    public float m_attackCooldown = 1.0f;

    public Team m_team;

    public float m_stopDistance = 5.0f;
    public float m_detectRange = 10.0f;
    public float m_targetSwitchInterval = 0.5f;
}
