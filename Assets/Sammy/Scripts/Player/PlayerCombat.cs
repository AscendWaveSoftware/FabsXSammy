using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCombat : MonoBehaviour
{
    [Header("Combat Settings")]
    [SerializeField] private int attackDamage = 25;
    [SerializeField] private float attackRange = 1.5f;
    //[SerializeField] private float attackCooldown = 0.4f;

    [Header("Hit Detection")]
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private Transform attackPoint;

    [Header("References")]
    [SerializeField] private PlayerResources playerResources;

    private float m_nextAttackTime;

    public void OnAttack(InputValue _value)
    {
        if (!_value.isPressed)
            return;

        TryAttack();
    }

    private void TryAttack()
    {
        if(attackPoint == null)
        {
            Debug.LogWarning("No attack point assigned to PlayerCombat.");
            return;
        }

        Collider[] hitEnemies = Physics.OverlapSphere(
                attackPoint.position,
                attackRange,
                enemyLayer
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

            enemyStats.TakeDamage(attackDamage, playerResources);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null)
            return;

        Gizmos.DrawWireSphere(attackPoint.position, attackRange);
    }
}
