using UnityEngine;

[CreateAssetMenu(fileName = "TowerCombat", menuName = "Scriptable Objects/TowerCombat")]
public class TowerStats : ScriptableObject
{
    public float m_damage;
    public float m_projectileSpeed;
    public float m_attackRange;
    public float m_attackCooldown;

    public float m_health;

    public string m_enemyMinionTag = "EnemyMinion";
}
