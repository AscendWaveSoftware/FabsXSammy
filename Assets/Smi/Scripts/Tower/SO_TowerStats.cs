using UnityEngine;

[CreateAssetMenu(fileName = "TowerCombat", menuName = "Scriptable Objects/TowerCombat")]
public class TowerStats : ScriptableObject
{
    public Team m_team;
    public float m_damage;
    public float m_projectileSpeed;
    public float m_attackRange;
    public float m_attackCooldown;
}
