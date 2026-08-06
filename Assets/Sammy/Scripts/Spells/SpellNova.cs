using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Instant shockwave around the caster. Unlike the other deliveries it owns no
/// object of its own: it resolves in the frame it is cast and hands the visual
/// over to <see cref="SpellImpact"/>.
/// </summary>
public static class SpellNova
{
    private static readonly HashSet<EnemyStats> DetonatedEnemies = new();

    public static void Detonate(
        SpellDefinition _definition,
        Vector3 _center,
        LayerMask _enemyLayer,
        PlayerResources _playerResources)
    {
        if (_definition == null)
            return;

        SpellImpact.Show(_definition, _center);

        // Allocating on purpose: a nova is a rare event and must never silently
        // drop enemies because a fixed buffer ran out.
        Collider[] caughtColliders = Physics.OverlapSphere(_center, _definition.ImpactRadius, _enemyLayer);

        DetonatedEnemies.Clear();

        foreach (Collider caughtCollider in caughtColliders)
        {
            EnemyStats enemyStats = caughtCollider != null
                ? caughtCollider.GetComponentInParent<EnemyStats>()
                : null;

            // One enemy can own several colliders, and taking damage may destroy
            // it, so every enemy is only ever resolved once.
            if (enemyStats == null || enemyStats.IsDead || !DetonatedEnemies.Add(enemyStats))
                continue;

            // Stunned before the damage lands. Damage can destroy the enemy, and
            // a survivor should lose its windup even if the hit does not kill it.
            StunEnemy(enemyStats, _definition);
            enemyStats.TakeDamage(_definition.Damage, _playerResources);
        }

        DetonatedEnemies.Clear();
    }

    private static void StunEnemy(EnemyStats _enemyStats, SpellDefinition _definition)
    {
        EnemyAttack enemyAttack = _enemyStats.GetComponent<EnemyAttack>();

        if (enemyAttack != null)
            enemyAttack.Stun(_definition.NovaStunDuration);

        if (_definition.NovaStunDuration <= 0f)
            return;

        // A short spark on every caught enemy, so a wave that lands off screen
        // still reads as having hit something.
        Collider enemyCollider = _enemyStats.GetComponent<Collider>();
        Vector3 sparkPosition = enemyCollider != null
            ? enemyCollider.bounds.center
            : _enemyStats.transform.position;

        CombatGlowPuff.Show(sparkPosition, _definition.GlowColor, 0.35f, 1.1f, 0.4f);
    }
}
