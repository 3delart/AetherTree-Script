using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// =============================================================
// SKILLSYSTEM.CS — Exécution des sorts
// Path : Assets/Scripts/Systems/SkillSystem.cs
// AetherTree GDD v30 — Section 8 / 21
//
// Flow unifié (découplé) :
//   SkillBar / AutoAttack → Execute()
//     → player.UseSkill()      : enregistre lastSkillUsed + élémentaire
//     → mob.RegisterLastSkill(): enregistre le skill pour MobKilledEvent
//     → CombatSystem           : calcul pur
//     → GameEventBus.Publish(SkillUsedEvent)
//     → GameEventBus.Publish(DamageDealtEvent)
//   Mob.Die() → GameEventBus.Publish(MobKilledEvent) — 1 seul publish
// =============================================================

public class SkillSystem : MonoBehaviour
{
    public static SkillSystem Instance { get; private set; }

    private Player player;

    private void Awake()
    {
        if (Instance != null) { Destroy(this); return; }
        Instance = this;
    }

    private void Start()
    {
        player = FindObjectOfType<Player>();
    }

    // =========================================================
    // POINT D'ENTRÉE
    // =========================================================
    public void Execute(SkillData skill, Entity target)
    {
        if (skill == null || player == null) return;

        // Enregistre le skill AVANT l'exécution :
        // lastSkillUsed, affinité élémentaire, RefreshTitle().
        // UseSkill() ne fait plus d'EmitSkillCast direct — remplacé par
        // GameEventBus.Publish(SkillUsedEvent) ci-dessous.
        player.UseSkill(skill, target);

        // Publish SkillUsedEvent — UnlockManager s'abonne pour les conditions SkillCast
        GameEventBus.Publish(new SkillUsedEvent
        {
            skill          = skill,
            target         = target,
            caster         = player,
            primaryElement = skill.PrimaryElement,
            isCombo        = skill.elements != null && skill.elements.Count >= 2,
            locationID     = "",   // TODO : ZoneSystem
            isInParty      = false, // TODO : PartySystem
        });

        // ── Dispatch selon le type d'exécution ───────────────
        if (skill.executionType == SkillExecutionType.MultiHit
            && skill.hitSteps != null && skill.hitSteps.Count > 0)
        {
            // Hit initial = skill parent (damageMultiplier du SkillData)
            // puis la coroutine enchaîne les hitSteps
            switch (skill.targetType)
            {
                case TargetType.Target:     ExecuteOnTarget(skill, target);                      break;
                case TargetType.Self:       ExecuteOnSelf(skill);                                break;
                case TargetType.AoE_Self:   ExecuteAoESelf(skill);                               break;
                case TargetType.AoE_Target: if (target != null) ExecuteAoETarget(skill, target); break;
                default: break;
            }
            // Lock tous les slots pendant toute la durée des hitSteps
            SkillBar.Instance?.LockForMultiHit(skill);
            StartCoroutine(ExecuteMultiHit(skill, target));
        }
        else
        {
            // Normal (et ComboSequence step-by-step géré par SkillBar)
            switch (skill.targetType)
            {
                case TargetType.Target:      ExecuteOnTarget(skill, target);                       break;
                case TargetType.Self:        ExecuteOnSelf(skill);                                 break;
                case TargetType.AoE_Self:    ExecuteAoESelf(skill);                                break;
                case TargetType.AoE_Target:  if (target != null) ExecuteAoETarget(skill, target);  break;
                case TargetType.Dash_Target: if (target != null) ExecuteDashTarget(skill, target); break;
                default: Debug.Log($"[SKILL] {skill.targetType} — Phase suivante !"); break;
            }
        }
        if (skill.vfxPrefab != null)
        {
            Vector3 vfxPos = target != null ? target.transform.position : player.transform.position;
            Instantiate(skill.vfxPrefab, vfxPos, Quaternion.identity);
        }
        if (skill.soundEffect != null)
            AudioSource.PlayClipAtPoint(skill.soundEffect, player.transform.position);
    }

    // =========================================================
    // MULTIHIT — Méthode 1
    // Enchaîne les HitStep en séquence avec délai entre chaque hit.
    // Chaque step a ses propres stats (mult, élément, effets).
    // Le VFX/son du step remplace celui du skill parent si défini.
    // =========================================================
    private IEnumerator ExecuteMultiHit(SkillData skill, Entity target)
    {
        foreach (HitStep step in skill.hitSteps)
        {
            yield return new WaitForSeconds(step.delay);

            // Cible peut mourir entre deux hits — on arrête proprement
            if (target == null || target.isDead) yield break;

            // Calcul dégâts avec les stats du step
            float dmg = CalculateDamageForStep(step, target);

            if (target is Mob mobTarget)
                mobTarget.RegisterLastSkill(player, skill);

            target.TakeDamage(dmg, step.element, player);

            GameEventBus.Publish(new DamageDealtEvent
            {
                amount   = dmg,
                element  = step.element,
                source   = player,
                target   = target,
                isCrit   = false,
                isOneHit = target.isDead && dmg >= target.MaxHP,
            });

            FloatingText.Spawn(Mathf.RoundToInt(dmg).ToString(),
                target.transform.position, Color.cyan);

            // Status effects spécifiques au step
            if (step.statusEffects != null)
                foreach (var entry in step.statusEffects)
                    ApplyStatusEffectEntry(entry, target);

            // VFX / Son du step (prioritaire sur le skill parent)
            GameObject vfx = step.vfxPrefab ?? skill.vfxPrefab;
            if (vfx != null)
                Instantiate(vfx, target.transform.position, Quaternion.identity);

            AudioClip sound = step.soundEffect ?? skill.soundEffect;
            if (sound != null)
                AudioSource.PlayClipAtPoint(sound, player.transform.position);

            CheckKill(target);
        }
    }

    /// <summary>
    /// Calcule les dégâts d'un HitStep en utilisant les stats du step
    /// mais l'arme et les bonus du joueur.
    /// </summary>
    private float CalculateDamageForStep(HitStep step, Entity target)
    {
        if (player.equippedWeaponInstance == null) return 1f;

        // Crée un SkillData proxy temporaire pour réutiliser CombatSystem
        // sans dupliquer la logique de calcul.
        // On surcharge uniquement les champs propres au step.
        var proxy = ScriptableObject.CreateInstance<SkillData>();
        proxy.damageMultiplier  = step.damageMultiplier;
        proxy.damageMeleeRatio  = step.damageMeleeRatio;
        proxy.damageRangedRatio = step.damageRangedRatio;
        proxy.damageMagicRatio  = step.damageMagicRatio;
        proxy.elementalRatio    = step.elementalRatio;
        // elements doit contenir l'élément du step pour le calcul élémentaire
        proxy.elements = new System.Collections.Generic.List<ElementType> { step.element };

        float dmg = CombatSystem.Instance.CalculateDamage(
            player.equippedWeaponInstance,
            proxy,
            player.GetComponent<ElementalSystem>(),
            player,
            target);

        Destroy(proxy);
        return dmg;
    }

    // =========================================================
    // TARGET
    // =========================================================
    private void ExecuteOnTarget(SkillData skill, Entity target)
    {
        if (target == null || target.isDead) return;
        ApplyEffectType(skill, target);
        ApplyStatusEffects(skill, target);
        if (skill.effectType == SkillEffectType.Damage) ApplyWeaponStatusEffects(target);
        CheckKill(target);
    }

    // =========================================================
    // SELF
    // =========================================================
    private void ExecuteOnSelf(SkillData skill)
    {
        ApplyEffectType(skill, player);
        ApplyStatusEffects(skill, player);
    }

    // =========================================================
    // AoE SELF
    // =========================================================
    private void ExecuteAoESelf(SkillData skill)
    {
        Collider[] hits = Physics.OverlapSphere(
            player.transform.position,
            skill.aoeRadius,
            ~LayerMask.GetMask("Player")
        );

        int count = 0;
        foreach (Collider col in hits)
        {
            Entity entity = col.GetComponentInParent<Entity>();
            if (entity == null || entity == player || entity.isDead) continue;

            ApplyEffectType(skill, entity);
            ApplyStatusEffects(skill, entity);
            if (skill.effectType == SkillEffectType.Damage) ApplyWeaponStatusEffects(entity);
            CheckKill(entity);
            count++;
        }
        Debug.Log($"[SKILL] AoE Self — {count} cibles touchées");
    }

    // =========================================================
    // AoE TARGET
    // =========================================================
    private void ExecuteAoETarget(SkillData skill, Entity target)
    {
        Collider[] hits = Physics.OverlapSphere(target.transform.position, skill.aoeRadius);
        foreach (Collider col in hits)
        {
            Entity entity = col.GetComponentInParent<Entity>();
            if (entity == null || entity == player || entity.isDead) continue;

            ApplyEffectType(skill, entity);
            ApplyStatusEffects(skill, entity);
            if (skill.effectType == SkillEffectType.Damage) ApplyWeaponStatusEffects(entity);
            CheckKill(entity);
        }
    }

    // =========================================================
    // DASH TARGET
    // =========================================================
    private void ExecuteDashTarget(SkillData skill, Entity target)
    {
        StartCoroutine(DashToTarget(skill, target));
    }

    private IEnumerator DashToTarget(SkillData skill, Entity target)
    {
        float elapsed      = 0f;
        float dashDuration = 0.3f;
        Vector3 startPos   = player.transform.position;
        Vector3 endPos     = target.transform.position
                           - (target.transform.position - startPos).normalized * 1.5f;

        var agent = player.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.enabled = false;

        while (elapsed < dashDuration)
        {
            player.transform.position = Vector3.Lerp(startPos, endPos, elapsed / dashDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        player.transform.position = endPos;
        if (agent != null) agent.enabled = true;

        ApplyEffectType(skill, target);
        ApplyStatusEffects(skill, target);
        if (skill.effectType == SkillEffectType.Damage) ApplyWeaponStatusEffects(target);
        CheckKill(target);
    }

    // =========================================================
    // EFFET PRINCIPAL
    // =========================================================
    private void ApplyEffectType(SkillData skill, Entity target)
    {
        switch (skill.effectType)
        {
            case SkillEffectType.Damage:
                float dmg = CalculateDamage(skill, target);

                // Enregistre le skill sur le mob AVANT TakeDamage
                // → Die() pourra remplir killerSkill dans MobKilledEvent
                if (target is Mob mobTarget)
                    mobTarget.RegisterLastSkill(player, skill);

                target.TakeDamage(dmg, skill.PrimaryElement, player);

                // Publish découplé — UnlockManager s'abonne pour les conditions
                GameEventBus.Publish(new DamageDealtEvent
                {
                    amount   = dmg,
                    element  = skill.PrimaryElement,
                    source   = player,
                    target   = target,
                    isCrit   = false,   // TODO : remonter isCrit depuis CombatSystem
                    isOneHit = target.isDead && dmg >= target.MaxHP,
                });

                FloatingText.Spawn(Mathf.RoundToInt(dmg).ToString(),
                    target.transform.position, Color.cyan);
                break;

            case SkillEffectType.Buff:
                // Entièrement géré via StatusEffects SO
                break;

            case SkillEffectType.Debuff:
                // Entièrement géré via StatusEffects SO
                break;

            case SkillEffectType.Other:
                ApplySpecialEffect(skill, target);
                break;
        }
    }

    // =========================================================
    // EFFETS SECONDAIRES — StatusEffects SO du skill
    // =========================================================
    private void ApplyStatusEffects(SkillData skill, Entity target)
    {
        if (skill.statusEffects == null || skill.statusEffects.Count == 0) return;
        if (target == null || target.isDead) return;

        var statusSystem = target.GetComponent<StatusEffectSystem>();
        if (statusSystem == null) return;

        foreach (var entry in skill.statusEffects)
        {
            if (entry == null || entry.effect == null) continue;
            if (!entry.Roll()) continue;

            switch (entry.effect)
            {
                case BuffData buff:
                    statusSystem.ApplyBuff(buff, player);
                    Debug.Log($"[SKILL] {skill.skillName} → {buff.effectName} sur {target.entityName}.");
                    break;

                case DebuffData debuff:
                    bool applied = statusSystem.TryApplyDebuff(debuff, player);
                    if (applied)
                        Debug.Log($"[SKILL] {skill.skillName} → {debuff.effectName} sur {target.entityName}.");
                    break;
            }
        }
    }

    /// <summary>
    /// Applique une seule StatusEffectEntry sur une cible.
    /// Utilisé par ExecuteMultiHit pour les effets par HitStep.
    /// </summary>
    private void ApplyStatusEffectEntry(StatusEffectEntry entry, Entity target)
    {
        if (entry == null || entry.effect == null) return;
        if (target == null || target.isDead) return;
        if (!entry.Roll()) return;

        var statusSystem = target.GetComponent<StatusEffectSystem>();
        if (statusSystem == null) return;

        switch (entry.effect)
        {
            case BuffData buff:
                statusSystem.ApplyBuff(buff, player);
                break;
            case DebuffData debuff:
                statusSystem.TryApplyDebuff(debuff, player);
                break;
        }
    }

    // =========================================================
    // EFFETS SECONDAIRES — StatusEffects de tous les équipements
    // Appliqués sur chaque attaque de type Damage (section 5.1)
    // Arme + Armure + Casque + Gants + Bottes + Bijoux
    // =========================================================
    private void ApplyWeaponStatusEffects(Entity target)
    {
        if (target == null || target.isDead) return;

        var statusSystem = target.GetComponent<StatusEffectSystem>();
        if (statusSystem == null)
        {
            Debug.LogWarning($"[SKILL] {target.entityName} n'a pas de StatusEffectSystem !");
            return;
        }

        var allEffects = new List<(List<StatusEffectEntry> effects, string source)>();

        if (player.equippedWeaponInstance?.data != null)
            allEffects.Add((player.equippedWeaponInstance.StatusEffects, player.equippedWeaponInstance.WeaponName));
        if (player.equippedArmorInstance?.data != null)
            allEffects.Add((player.equippedArmorInstance.StatusEffects, player.equippedArmorInstance.ArmorName));
        if (player.equippedHelmetInstance?.data != null)
            allEffects.Add((player.equippedHelmetInstance.StatusEffects, player.equippedHelmetInstance.HelmetName));
        if (player.equippedGlovesInstance?.data != null)
            allEffects.Add((player.equippedGlovesInstance.StatusEffects, player.equippedGlovesInstance.GlovesName));
        if (player.equippedBootsInstance?.data != null)
            allEffects.Add((player.equippedBootsInstance.StatusEffects, player.equippedBootsInstance.BootsName));
        if (player.equippedJewelryInstances != null)
            foreach (var jewelry in player.equippedJewelryInstances)
                if (jewelry != null) allEffects.Add((jewelry.StatusEffects, jewelry.JewelryName));

        foreach (var (effects, sourceName) in allEffects)
        {
            if (effects == null || effects.Count == 0) continue;
            foreach (var entry in effects)
            {
                if (entry == null || entry.effect == null) continue;
                if (!entry.Roll()) continue;

                switch (entry.effect)
                {
                    case BuffData buff:
                        statusSystem.ApplyBuff(buff, player);
                        break;
                    case DebuffData debuff:
                        bool applied = statusSystem.TryApplyDebuff(debuff, player);
                        if (applied)
                            Debug.Log($"[EQUIP] {sourceName} → {debuff.effectName} sur {target.entityName}.");
                        break;
                }
            }
        }
    }

    // =========================================================
    // UTILITAIRES
    // =========================================================
    private float CalculateDamage(SkillData skill, Entity target = null)
    {
        if (player.equippedWeaponInstance == null) return 10f * skill.damageMultiplier;
        return CombatSystem.Instance.CalculateDamage(
            player.equippedWeaponInstance,
            skill,
            player.GetElementalSystem(),
            player,
            target);
    }

    // =========================================================
    // EFFETS SPECIAUX
    // =========================================================

    private void ApplySpecialEffect(SkillData skill, Entity target)
    {
        if (skill.specialEffect == SkillSpecialEffect.None)
        {
            Debug.LogWarning($"[SKILL] {skill.skillName} : effectType=Other mais specialEffect=None.");
            return;
        }

        switch (skill.specialEffect)
        {
            // ── Pull — attire la cible vers le caster ────────
            case SkillSpecialEffect.Pull:
                if (target == null) return;
                WarpEntity(target, player.transform.position, skill.pullPushForce, towards: true);
                break;

            // ── Push — repousse la cible ──────────────────────
            case SkillSpecialEffect.Push:
                if (target == null) return;
                WarpEntity(target, player.transform.position, skill.pullPushForce, towards: false);
                break;

            // ── SwapPosition — échange positions ─────────────
            case SkillSpecialEffect.SwapPosition:
                if (target == null) return;
                Vector3 playerPos = player.transform.position;
                Vector3 targetPos = target.transform.position;
                WarpToNavMesh(player, targetPos);
                WarpToNavMesh(target, playerPos);
                FloatingText.Spawn("SWAP", targetPos, Color.yellow, 1.8f);
                break;

            // ── PullAoE — attire toute la zone vers soi ──────
            case SkillSpecialEffect.PullAoE:
            {
                Vector3 center = skill.targetType == TargetType.GroundTarget && _groundTargetPoint.HasValue
                    ? _groundTargetPoint.Value
                    : player.transform.position;
                ApplyDisplacementAoE(skill, center, towardsCenter: true);
                break;
            }

            // ── PushAoE — repousse toute la zone ─────────────
            case SkillSpecialEffect.PushAoE:
            {
                Vector3 center = skill.targetType == TargetType.GroundTarget && _groundTargetPoint.HasValue
                    ? _groundTargetPoint.Value
                    : player.transform.position;
                ApplyDisplacementAoE(skill, center, towardsCenter: false);
                break;
            }

            // ── GatherAoE — regroupe sur un point central ────
            case SkillSpecialEffect.GatherAoE:
            {
                Vector3 center = skill.targetType == TargetType.GroundTarget && _groundTargetPoint.HasValue
                    ? _groundTargetPoint.Value
                    : player.transform.position;
                Collider[] hits = Physics.OverlapSphere(center, skill.aoeRadius, ~LayerMask.GetMask("Player"));
                foreach (Collider col in hits)
                {
                    Entity entity = col.GetComponentInParent<Entity>();
                    if (entity == null || entity == player || entity.isDead) continue;
                    WarpToNavMesh(entity, center);
                }
                FloatingText.Spawn("REGROUPEMENT", center, Color.magenta, 1.8f);
                break;
            }

            // ── Vortex — attire en spirale (= GatherAoE + Slow) ─
            case SkillSpecialEffect.Vortex:
            {
                Vector3 center = skill.targetType == TargetType.GroundTarget && _groundTargetPoint.HasValue
                    ? _groundTargetPoint.Value
                    : player.transform.position;
                Collider[] hits = Physics.OverlapSphere(center, skill.aoeRadius, ~LayerMask.GetMask("Player"));
                foreach (Collider col in hits)
                {
                    Entity entity = col.GetComponentInParent<Entity>();
                    if (entity == null || entity == player || entity.isDead) continue;
                    WarpEntity(entity, center, skill.pullPushForce, towards: true);
                }
                FloatingText.Spawn("VORTEX", center, Color.cyan, 1.8f);
                break;
            }

            // ── TeleportSelf — warp joueur vers cible / sol ──
            case SkillSpecialEffect.TeleportSelf:
            {
                Vector3 dest = _groundTargetPoint ?? (target != null ? target.transform.position : player.transform.position);
                WarpToNavMesh(player, dest);
                FloatingText.Spawn("TELEPORT", dest, Color.cyan, 1.8f);
                break;
            }

            // ── TeleportTarget — warp cible vers joueur ──────
            case SkillSpecialEffect.TeleportTarget:
                if (target == null) return;
                WarpToNavMesh(target, player.transform.position);
                FloatingText.Spawn("TELEPORT", target.transform.position, Color.cyan, 1.8f);
                break;

            // ── DrainHP — vol de HP ───────────────────────────
            case SkillSpecialEffect.DrainHP:
            {
                if (target == null || target.isDead) return;
                float dmg = CalculateDamage(skill, target);
                if (target is Mob mobDrain) mobDrain.RegisterLastSkill(player, skill);
                target.TakeDamage(dmg, skill.PrimaryElement, player);
                float healed = dmg * skill.drainHealRatio;
                player.Heal(healed);
                GameEventBus.Publish(new DamageDealtEvent
                {
                    amount = dmg, element = skill.PrimaryElement,
                    source = player, target = target,
                    isCrit = false, isOneHit = target.isDead && dmg >= target.MaxHP,
                });
                FloatingText.Spawn($"-{Mathf.RoundToInt(dmg)}", target.transform.position, Color.red,   1.8f);
                FloatingText.Spawn($"+{Mathf.RoundToInt(healed)}", player.transform.position, Color.green, 1.8f);
                CheckKill(target);
                break;
            }

            // ── DrainMana — vol de Mana ───────────────────────
            case SkillSpecialEffect.DrainMana:
            {
                if (target == null || target.isDead) return;
                float stolen = target.CurrentMana * skill.drainHealRatio;
                target.SpendMana(stolen);
                player.RecoverMana(stolen);
                FloatingText.Spawn($"MANA -{Mathf.RoundToInt(stolen)}", target.transform.position, Color.blue, 1.8f);
                break;
            }

            // ── Summon — TODO ─────────────────────────────────
            case SkillSpecialEffect.Summon:
                Debug.Log($"[SKILL] Summon — TODO (summonMobData={skill.summonMobData?.mobName ?? "null"}).");
                break;

            // ── Interrupt — TODO ──────────────────────────────
            case SkillSpecialEffect.Interrupt:
                Debug.Log($"[SKILL] Interrupt — TODO.");
                break;
        }

        // Reset du point au sol après utilisation
        _groundTargetPoint = null;
    }

    // ── Helpers — délégués à DisplacementUtils ────────────────
    private void WarpEntity(Entity entity, Vector3 origin, float force, bool towards)
        => DisplacementUtils.WarpEntity(entity, origin, force, towards);

    private void WarpToNavMesh(Entity entity, Vector3 destination)
        => DisplacementUtils.WarpToNavMesh(entity, destination);

    private void ApplyDisplacementAoE(SkillData skill, Vector3 center, bool towardsCenter)
    {
        int count = DisplacementUtils.ApplyDisplacementAoE(
            center, skill.aoeRadius, skill.pullPushForce, towardsCenter,
            caster: player, layerMask: ~LayerMask.GetMask("Player"));
        string label = towardsCenter ? "PULL" : "PUSH";
        FloatingText.Spawn($"{label} ×{count}", center, Color.yellow, 1.8f);
    }

    // ── Stocke le point de clic au sol (GroundTarget) ────────
    // Alimenté par TargetingSystem avant d'appeler Execute().
    private Vector3? _groundTargetPoint = null;

    /// <summary>
    /// À appeler par TargetingSystem juste avant Execute()
    /// quand targetType == GroundTarget.
    /// </summary>
    public void SetGroundTargetPoint(Vector3 point)
    {
        _groundTargetPoint = point;
    }

    private void CheckKill(Entity target)
    {
        if (target == null || !target.isDead) return;
        // RegisterKill et XP sont déjà gérés dans Mob.Die() pour tous les kills
        // (directs ET DoT). On ne fait ici que désélectionner la cible morte.
        TargetingSystem.Instance?.Deselect();
    }
}