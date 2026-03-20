using UnityEngine;
using System.Collections;

// =============================================================
// MOBSKILLSYSTEM.CS — Exécution des skills de mobs
// Path : Assets/Scripts/Systems/MobSkillSystem.cs
// AetherTree GDD v30 — Section 18
//
// Séparé de SkillSystem (joueur) — pas de dépendance à Player.
//
// Dégâts : mob.data.attackDamage × skill.damageMultiplier
// Source des StatusEffects : le mob lui-même
// VFX : centré sur le mob ou la cible selon le type
// =============================================================

public class MobSkillSystem : MonoBehaviour
{
    // =========================================================
    // POINT D'ENTRÉE — appelé par Mob.cs HandleAttack()
    // =========================================================

    public void Execute(SkillData skill, Mob caster, Entity target)
    {
        if (skill == null || caster == null || caster.isDead) return;

        // ── LOG : skill lancé ─────────────────────────────────
        bool isBasicAttack = skill.skillType == SkillType.BasicAttack
                          || skill.HasTag(SkillTag.BasicAttack);
        string targetName  = target != null ? target.entityName : "aucune";
        string logPrefix   = isBasicAttack ? "⚔ BASIC" : "✦ SKILL";


        switch (skill.targetType)
        {
            case TargetType.Target:
                if (target != null) ExecuteOnTarget(skill, caster, target);
                break;

            case TargetType.Self:
                ExecuteOnSelf(skill, caster);
                break;

            case TargetType.AoE_Self:
                ExecuteAoESelf(skill, caster);
                break;

            case TargetType.AoE_Target:
                if (target != null) ExecuteAoETarget(skill, caster, target);
                break;

            case TargetType.Dash_Target:
                if (target != null) StartCoroutine(DashToTarget(skill, caster, target));
                break;

            default:
                Debug.Log($"[MOB SKILL] {skill.targetType} — non supporté pour les mobs.");
                break;
        }

        // VFX & Son
        if (skill.vfxPrefab != null)
        {
            Vector3 vfxPos = target != null ? target.transform.position : caster.transform.position;
            Instantiate(skill.vfxPrefab, vfxPos, Quaternion.identity);
        }
        if (skill.soundEffect != null)
            AudioSource.PlayClipAtPoint(skill.soundEffect, caster.transform.position);
    }

    // =========================================================
    // TARGET
    // =========================================================

    private void ExecuteOnTarget(SkillData skill, Mob caster, Entity target)
    {
        if (target.isDead) return;
        ApplyEffectType(skill, caster, target);
        ApplyStatusEffects(skill, caster, target);
    }

    // =========================================================
    // SELF — buff/heal sur le mob lui-même
    // =========================================================

    private void ExecuteOnSelf(SkillData skill, Mob caster)
    {
        ApplyEffectType(skill, caster, caster);
        ApplyStatusEffects(skill, caster, caster);
    }

    // =========================================================
    // AoE SELF — zone autour du mob
    // =========================================================

    private void ExecuteAoESelf(SkillData skill, Mob caster)
    {
        Collider[] hits = Physics.OverlapSphere(
            caster.transform.position,
            skill.aoeRadius,
            ~LayerMask.GetMask("Mob")   // ne touche pas les autres mobs
        );

        int count = 0;
        foreach (Collider col in hits)
        {
            Entity entity = col.GetComponentInParent<Entity>();
            if (entity == null || entity == caster || entity.isDead) continue;

            ApplyEffectType(skill, caster, entity);
            ApplyStatusEffects(skill, caster, entity);
            count++;
        }
        Debug.Log($"[MOB SKILL] AoE Self — {count} cibles touchées");
    }

    // =========================================================
    // AoE TARGET — zone autour de la cible
    // =========================================================

    private void ExecuteAoETarget(SkillData skill, Mob caster, Entity target)
    {
        Collider[] hits = Physics.OverlapSphere(target.transform.position, skill.aoeRadius);
        foreach (Collider col in hits)
        {
            Entity entity = col.GetComponentInParent<Entity>();
            if (entity == null || entity == caster || entity.isDead) continue;

            ApplyEffectType(skill, caster, entity);
            ApplyStatusEffects(skill, caster, entity);
        }
    }

    // =========================================================
    // DASH TARGET — le mob charge vers la cible
    // =========================================================

    private IEnumerator DashToTarget(SkillData skill, Mob caster, Entity target)
    {
        float elapsed      = 0f;
        float dashDuration = 0.25f;
        Vector3 startPos   = caster.transform.position;
        Vector3 endPos     = target.transform.position
                        - (target.transform.position - startPos).normalized * 1.2f;

        var agent = caster.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.enabled = false;
        caster.IsDashing = true;

        while (elapsed < dashDuration)
        {
            if (caster == null || caster.isDead) yield break; // ✅ déjà là
            caster.transform.position = Vector3.Lerp(startPos, endPos, elapsed / dashDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (caster != null)
        {
            caster.transform.position = endPos;
            caster.IsDashing = false;
            if (agent != null) agent.enabled = true;

            // ✅ MANQUE caster.isDead ICI — c'est le bug
            if (!caster.isDead && target != null && !target.isDead)
            {
                ApplyEffectType(skill, caster, target);
                ApplyStatusEffects(skill, caster, target);
            }
        }
    }

    // =========================================================
    // EFFET PRINCIPAL
    // =========================================================

    private void ApplyEffectType(SkillData skill, Mob caster, Entity target)
    {
        switch (skill.effectType)
        {
            case SkillEffectType.Damage:
                float dmg = CalculateDamage(skill, caster, target);

                target.TakeDamage(dmg, skill.PrimaryElement, caster);

                FloatingText.Spawn(Mathf.RoundToInt(dmg).ToString(),
                    target.transform.position, Color.red, heightOffset: 1.8f);
                break;

            case SkillEffectType.Buff:
                // Géré via StatusEffects SO ci-dessous
                break;

            case SkillEffectType.Debuff:
                // Géré via StatusEffects SO ci-dessous
                break;

            case SkillEffectType.Other:
                ApplySpecialEffect(skill, caster, target);
                break;
        }
    }

    // =========================================================
    // STATUS EFFECTS
    // =========================================================

    private void ApplyStatusEffects(SkillData skill, Mob caster, Entity target)
    {
        if (skill.statusEffects == null || skill.statusEffects.Count == 0) return;
        if (target == null || target.isDead) return;
        if (target.statusEffects == null)
            {
                Debug.LogWarning($"[MOB SKILL] {target.entityName} n'a pas de StatusEffectSystem !");
                return;
            }

        foreach (var entry in skill.statusEffects)
        {
            if (entry == null || entry.effect == null) continue;
            if (!entry.Roll()) continue;

            if (entry.effect is DebuffData debuff)
            {
                bool applied = target.statusEffects.TryApplyDebuff(debuff, caster);

            }
            else if (entry.effect is BuffData buff)
            {
                target.statusEffects.ApplyBuff(buff, caster);
 
            }
        }
    }

    // =========================================================
    // EFFETS SPECIAUX — mobs
    // =========================================================

    private void ApplySpecialEffect(SkillData skill, Mob caster, Entity target)
    {
        if (skill.specialEffect == SkillSpecialEffect.None)
        {
            Debug.LogWarning($"[MOB SKILL] {skill.skillName} : effectType=Other mais specialEffect=None.");
            return;
        }

        Vector3 casterPos = caster.transform.position;

        switch (skill.specialEffect)
        {
            // ── Pull — attire la cible vers le mob ───────────
            case SkillSpecialEffect.Pull:
                if (target == null) return;
                DisplacementUtils.WarpEntity(target, casterPos, skill.pullPushForce, towards: true);
                FloatingText.Spawn("PULL", target.transform.position, Color.yellow, 1.8f);
                break;

            // ── Push — repousse la cible ──────────────────────
            case SkillSpecialEffect.Push:
                if (target == null) return;
                DisplacementUtils.WarpEntity(target, casterPos, skill.pullPushForce, towards: false);
                FloatingText.Spawn("PUSH", target.transform.position, Color.yellow, 1.8f);
                break;

            // ── SwapPosition ──────────────────────────────────
            case SkillSpecialEffect.SwapPosition:
                if (target == null) return;
                Vector3 mobPos    = casterPos;
                Vector3 targetPos = target.transform.position;
                DisplacementUtils.WarpToNavMesh(caster, targetPos);
                DisplacementUtils.WarpToNavMesh(target, mobPos);
                FloatingText.Spawn("SWAP", targetPos, Color.yellow, 1.8f);
                break;

            // ── PullAoE — attire toute la zone vers le mob ───
            case SkillSpecialEffect.PullAoE:
            {
                int count = DisplacementUtils.ApplyDisplacementAoE(
                    casterPos, skill.aoeRadius, skill.pullPushForce,
                    towardsCenter: true, caster: caster,
                    layerMask: ~LayerMask.GetMask("Mob"));
                FloatingText.Spawn($"PULL ×{count}", casterPos, Color.yellow, 1.8f);
                break;
            }

            // ── PushAoE — repousse toute la zone ─────────────
            case SkillSpecialEffect.PushAoE:
            {
                int count = DisplacementUtils.ApplyDisplacementAoE(
                    casterPos, skill.aoeRadius, skill.pullPushForce,
                    towardsCenter: false, caster: caster,
                    layerMask: ~LayerMask.GetMask("Mob"));
                FloatingText.Spawn($"PUSH ×{count}", casterPos, Color.yellow, 1.8f);
                break;
            }

            // ── GatherAoE ─────────────────────────────────────
            case SkillSpecialEffect.GatherAoE:
            {
                int count = DisplacementUtils.GatherAoE(
                    casterPos, skill.aoeRadius, caster,
                    layerMask: ~LayerMask.GetMask("Mob"));
                FloatingText.Spawn($"GATHER ×{count}", casterPos, Color.magenta, 1.8f);
                break;
            }

            // ── Vortex ────────────────────────────────────────
            case SkillSpecialEffect.Vortex:
            {
                int count = DisplacementUtils.ApplyDisplacementAoE(
                    casterPos, skill.aoeRadius, skill.pullPushForce,
                    towardsCenter: true, caster: caster,
                    layerMask: ~LayerMask.GetMask("Mob"));
                FloatingText.Spawn($"VORTEX ×{count}", casterPos, Color.cyan, 1.8f);
                break;
            }

            // ── TeleportSelf — le mob se téléporte sur la cible ─
            case SkillSpecialEffect.TeleportSelf:
                if (target == null) return;
                DisplacementUtils.WarpToNavMesh(caster, target.transform.position
                    - (target.transform.position - casterPos).normalized * 1.5f);
                break;

            // ── TeleportTarget — attire la cible sur le mob ──
            case SkillSpecialEffect.TeleportTarget:
                if (target == null) return;
                DisplacementUtils.WarpToNavMesh(target, casterPos);
                FloatingText.Spawn("TELEPORT", target.transform.position, Color.cyan, 1.8f);
                break;

            // ── DrainHP ───────────────────────────────────────
            case SkillSpecialEffect.DrainHP:
            {
                if (target == null || target.isDead) return;
                float dmg    = CalculateDamage(skill, caster, target);
                target.TakeDamage(dmg, skill.PrimaryElement, caster);
                float healed = dmg * skill.drainHealRatio;
                caster.Heal(healed);
                FloatingText.Spawn($"-{Mathf.RoundToInt(dmg)}",    target.transform.position, Color.red,   1.8f);
                FloatingText.Spawn($"+{Mathf.RoundToInt(healed)}", casterPos,                 Color.green, 1.8f);
                break;
            }

            // ── DrainMana ─────────────────────────────────────
            case SkillSpecialEffect.DrainMana:
            {
                if (target == null || target.isDead) return;
                float stolen = target.CurrentMana * skill.drainHealRatio;
                target.SpendMana(stolen);
                caster.RecoverMana(stolen);
                FloatingText.Spawn($"MANA -{Mathf.RoundToInt(stolen)}", target.transform.position, Color.blue, 1.8f);
                break;
            }

            // ── Summon / Interrupt — TODO ─────────────────────
            case SkillSpecialEffect.Summon:
            case SkillSpecialEffect.Interrupt:
                Debug.Log($"[MOB SKILL] {skill.specialEffect} — TODO.");
                break;
        }
    }

    // =========================================================
    // CALCUL DES DÉGÂTS — délégué à CombatSystem
    // =========================================================

    private float CalculateDamage(SkillData skill, Mob caster, Entity target)
    {
        if (CombatSystem.Instance != null)
            return CombatSystem.Instance.CalculateMobDamage(skill, caster, target);

        // Fallback si CombatSystem absent
        float baseDmg = caster.data != null ? caster.data.attackDamage : 10f;
        return baseDmg * skill.damageMultiplier * Random.Range(0.9f, 1.1f);
    }
}