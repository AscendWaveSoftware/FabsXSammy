//Code by Fabian Schmiedel

using UnityEngine;

[CreateAssetMenu(fileName = "TowerCombat", menuName = "Scriptable Objects/TowerCombat")]
public class SO_TowerStats : HealthData
{
    public Team m_team;
    public float m_damage;
    public float m_projectileSpeed;
    public float m_attackRange;
    public float m_attackCooldown;
}
