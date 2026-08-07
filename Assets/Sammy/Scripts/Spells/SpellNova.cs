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
    private static readonly HashSet<EnemyStats> ChainedEnemies = new();
    private static readonly List<Vector3> ChainAnchors = new();
    private static readonly Collider[] ChainBuffer = new Collider[32];

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

        ResolveChain(_definition, _center, _enemyLayer, _playerResources);
    }

    /// <summary>
    /// Arcs on from the blast, hopping to the nearest enemy it has not touched
    /// yet and losing power with every jump. Damage is resolved here in one go;
    /// the bolt that draws it afterwards is purely visual.
    /// </summary>
    private static void ResolveChain(
        SpellDefinition _definition,
        Vector3 _center,
        LayerMask _enemyLayer,
        PlayerResources _playerResources)
    {
        if (_definition.ChainJumps <= 0)
            return;

        ChainedEnemies.Clear();
        ChainAnchors.Clear();
        ChainAnchors.Add(_center);

        Vector3 currentPosition = _center;
        float damageScale = 1f;

        for (int jump = 0; jump < _definition.ChainJumps; jump++)
        {
            EnemyStats nextTarget = FindNextChainTarget(currentPosition, _definition.ChainJumpRange, _enemyLayer);

            if (nextTarget == null)
                break;

            ChainedEnemies.Add(nextTarget);

            // Captured before the hit lands, because the damage can destroy the
            // enemy and the bolt still has to be drawn to where it stood.
            Vector3 targetPosition = GetBodyCenter(nextTarget);
            ChainAnchors.Add(targetPosition);
            currentPosition = targetPosition;

            damageScale *= _definition.ChainDamageFalloff;
            int jumpDamage = Mathf.Max(1, Mathf.RoundToInt(_definition.Damage * damageScale));

            CombatGlowPuff.Show(
                targetPosition,
                _definition.ChainColor,
                0.3f,
                1f,
                0.3f,
                199,
                _definition.EmissiveMaterial,
                _definition.EmissionIntensity
            );
            nextTarget.TakeDamage(jumpDamage, _playerResources);
        }

        if (ChainAnchors.Count >= 2)
            SpellChainLightning.Show(ChainAnchors, _definition.ChainColor, _definition);

        ChainedEnemies.Clear();
        ChainAnchors.Clear();
    }

    private static EnemyStats FindNextChainTarget(Vector3 _from, float _jumpRange, LayerMask _enemyLayer)
    {
        int candidateCount = Physics.OverlapSphereNonAlloc(_from, _jumpRange, ChainBuffer, _enemyLayer);

        EnemyStats nearestEnemy = null;
        float nearestSquaredDistance = float.MaxValue;

        for (int i = 0; i < candidateCount; i++)
        {
            EnemyStats enemyStats = ChainBuffer[i] != null
                ? ChainBuffer[i].GetComponentInParent<EnemyStats>()
                : null;

            // Already struck enemies stay in the set even once destroyed, so the
            // bolt can never double back onto the same target.
            if (enemyStats == null || enemyStats.IsDead || ChainedEnemies.Contains(enemyStats))
                continue;

            float squaredDistance = (GetBodyCenter(enemyStats) - _from).sqrMagnitude;

            if (squaredDistance >= nearestSquaredDistance)
                continue;

            nearestSquaredDistance = squaredDistance;
            nearestEnemy = enemyStats;
        }

        return nearestEnemy;
    }

    private static Vector3 GetBodyCenter(EnemyStats _enemyStats)
    {
        Collider enemyCollider = _enemyStats.GetComponent<Collider>();
        return enemyCollider != null ? enemyCollider.bounds.center : _enemyStats.transform.position;
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

        CombatGlowPuff.Show(
            sparkPosition,
            _definition.GlowColor,
            0.35f,
            1.1f,
            0.4f,
            199,
            _definition.EmissiveMaterial,
            _definition.EmissionIntensity
        );
    }
}
