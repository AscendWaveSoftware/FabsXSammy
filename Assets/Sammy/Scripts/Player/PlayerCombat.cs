using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCombat : MonoBehaviour
{
    [Header("Combat Settings")]
    [SerializeField] private int m_attackDamage = 25;
    [SerializeField] private float m_attackRange = 1.5f;
    //[SerializeField] private float attackCooldown = 0.4f;

    [Header("Hit Detection")]
    [SerializeField] private LayerMask m_enemyLayer;
    [SerializeField] private Transform m_attackPoint;

    [Header("References")]
    [SerializeField] private PlayerResources m_playerResources;

    private float m_nextAttackTime;

    public void OnAttack(InputValue _value)
    {
        if (!_value.isPressed)
            return;

        TryAttack();
    }

    public void AddDamage(int _amount)
    {
        if (_amount <= 0) return;

        m_attackDamage += _amount;
    }

    private void TryAttack()
    {
        if(m_attackPoint == null)
        {
            Debug.LogWarning("No attack point assigned to PlayerCombat.");
            return;
        }

        Collider[] hitEnemies = Physics.OverlapSphere(
                m_attackPoint.position,
                m_attackRange,
                m_enemyLayer
        );

        if(hitEnemies.Length == 0){
            Debug.Log("Attack missed.");
            return;
        }

        foreach (Collider enemyCollider in hitEnemies)
        {
            EnemyStats enemyStats = enemyCollider.GetComponentInParent<EnemyStats>();

            if (enemyStats == null)
                continue;

            enemyStats.TakeDamage(m_attackDamage, m_playerResources);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (m_attackPoint == null)
            return;

        Gizmos.DrawWireSphere(m_attackPoint.position, m_attackRange);
    }
}
