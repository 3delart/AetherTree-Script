using UnityEngine;
using System.Collections.Generic;

// =============================================================
// StatusEffectSystem — composant gérant tous les effets actifs
// Path : Assets/Scripts/Core/StatusEffectSystem.cs
// AetherTree GDD v30 — §4.5 (Entity), §21bis (Status Effects, Buffs & Debuffs)
//
// À attacher sur : Entity (Player, Mob, PNJ, Pet) via [RequireComponent] sur Entity.
//
// Debuffs offensifs (§21bis.1) :
//   Burn, Slow, Knockback*, Rooted, Poisoned, Stunned, ArmorBreak,
//   Shocked, Chain*, Feared, ManaDrain, Blinded
//   (*) Knockback et Chain sont des effets ponctuels — pas de flag runtime.
//
// Buffs défensifs (§21bis.2) :
//   Shield, Regeneration, Haste, Fortify, Barrier, Purified*
//   (*) Purified est un effet instantané (cleanse) — pas de flag runtime.
//
// Effets spéciaux (§21bis.3) :
//   Invincibility, Stealth, Taunt, Interrupt*, Dispel*
//   (*) Interrupt et Dispel sont des effets ponctuels — pas de flag runtime.
//
// Règles clés (§21bis.4) :
//   — Un debuff ne se cumule pas avec lui-même (refresh uniquement)
//   — CC dur (Stunned/Feared) : pas d'immunité automatique post-CC —
//     l'immunité peut être accordée via buff ou équipement
//   — PvP : durées CC réduites de 50% — TODO déléguer à CombatSystem/SkillSystem
// =============================================================

[RequireComponent(typeof(Entity))]
public class StatusEffectSystem : MonoBehaviour
{
    // ── Effets actifs ─────────────────────────────────────────
    private Dictionary<DebuffType, DebuffInstance> _activeDebuffs
        = new Dictionary<DebuffType, DebuffInstance>();

    private Dictionary<BuffType, BuffInstance> _activeBuffs
        = new Dictionary<BuffType, BuffInstance>();

    // ── Debug Inspector ───────────────────────────────────────
    [Header("Debug — Effets actifs (lecture seule)")]
    [SerializeField] private List<string> _debugDebuffs = new List<string>();
    [SerializeField] private List<string> _debugBuffs   = new List<string>();

    // ── Résistances aux debuffs [0..1] ────────────────────────
    private Dictionary<DebuffType, float> _debuffResistances
        = new Dictionary<DebuffType, float>();

    // ── Flags runtime ─────────────────────────────────────────
    // ── Flags debuff ──────────────────────────────────────────
    // GDD v30 §21bis.1
    public bool  isStunned             { get; private set; } = false;   // Stun — bloque toutes les actions
    public bool  isFreezed              { get; private set; } = false;  // Freeze — immobilise
    public bool  isSilenced            { get; private set; } = false;   // Silence — bloque les skills
    public bool  isRooted              { get; private set; } = false;   //Root — bloque le mouvement
    public bool  isBlinded             { get; private set; } = false;  // Blind — Assombrissement + malus précision
    public bool  isFeared              { get; private set; } = false;  // Fear — fuite incontrôlée
    public bool  isSleeping            { get; private set; } = false;  // Sleep — immobilise jusqu'au premier dégât
    public bool  isPoisoned            { get; private set; } = false;  // Poison — DoT + réduction soins
    public bool  isArmorBroken         { get; private set; } = false;  // ArmorBreak — réduction défense physique %
    public bool  isTaunted             { get; private set; } = false;  // Force les mobs à cibler le lanceur (PvE)
    public float slowMultiplier        { get; private set; } = 1f;
    public float shockDefenseReduction { get; private set; } = 0f;     // Shocked — réduction défense + mini-stun 0.5s
    public float armorBreakReduction   { get; private set; } = 0f;     // ArmorBreak — réduction défense physique %
    public float poisonHealReduction   { get; private set; } = 0f;     // Poisoned — réduction soins reçus %
    public float blindPrecisionMalus   { get; private set; } = 0f;     // % de précision perdue
    public float manaBreakAmount       { get; private set; } = 0f;     // mana drainée/s (ManaDrain)
    public bool  isMarked              { get; private set; } = false;
    public float markDamageBonus       { get; private set; } = 0f;     // bonus dégâts sur cible marquée

    // ── Flags buff ────────────────────────────────────────────
    // GDD v30 §21bis.2 & §21bis.3
    public bool  isInvincible          { get; private set; } = false;
    public bool  isStealthed           { get; private set; } = false;
    public bool  isTaunting            { get; private set; } = false;  // Taunt actif sur ce joueur (force aggro mobs)
    public float buffDefenseBonus      { get; private set; } = 0f;     // Fortify — défense physique % temporaire
    public float buffDodgeBonus        { get; private set; } = 0f;
    public float buffSpeedMultiplier   { get; private set; } = 1f;     // Haste
    public float buffAttackBonus       { get; private set; } = 0f;
    public float buffRegenBonus        { get; private set; } = 0f;     // Regeneration — HP/s supplémentaire sur la durée
    public float barrierElementResist  { get; private set; } = 0f;     // Barrier — résistance élémentaire temporaire %

    private Entity _entity;

    private void Awake() => _entity = GetComponent<Entity>();

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        if (_entity.isDead) return;

        // Tick debuffs
        var expiredDebuffs = new List<DebuffType>();
        foreach (var kvp in _activeDebuffs)
        {
            kvp.Value.Tick(_entity, Time.deltaTime);
            if (kvp.Value.IsExpired) expiredDebuffs.Add(kvp.Key);
        }
        foreach (var t in expiredDebuffs) ExpireDebuff(t);

        // ManaBreak — draine le mana chaque seconde
        if (manaBreakAmount > 0f)
            _entity.SpendMana(manaBreakAmount * Time.deltaTime);

        // Regeneration buff — HP/s supplémentaire sur la durée (GDD v30 §21bis.2)
        if (buffRegenBonus > 0f)
            _entity.Heal(buffRegenBonus * Time.deltaTime);

        // Tick buffs
        var expiredBuffs = new List<BuffType>();
        foreach (var kvp in _activeBuffs)
        {
            kvp.Value.Tick(_entity, Time.deltaTime);
            if (kvp.Value.IsExpired) expiredBuffs.Add(kvp.Key);
        }
        foreach (var t in expiredBuffs) ExpireBuff(t);

        // Refresh debug lists
        _debugDebuffs.Clear();
        foreach (var kvp in _activeDebuffs)
            _debugDebuffs.Add($"{kvp.Key} — {kvp.Value.remainingTime:F1}s");

        _debugBuffs.Clear();
        foreach (var kvp in _activeBuffs)
            _debugBuffs.Add($"{kvp.Key} — {kvp.Value.remainingTime:F1}s");
    }

    // =========================================================
    // APPLICATION DEBUFF
    // =========================================================

    public bool TryApplyDebuff(DebuffData debuff, Entity source)
    {
        if (debuff == null || _entity.isDead) return false;

        // Vérification résistance
        float resistance = GetDebuffResistance(debuff.debuffType);
        if (resistance > 0f && Random.value < resistance)
        {
            return false;
        }

        // Refresh si déjà actif
        if (_activeDebuffs.TryGetValue(debuff.debuffType, out var existing))
        {
            existing.Refresh();
            return true;
        }

        // Nouvelle application
        DebuffInstance instance = (DebuffInstance)debuff.CreateInstance(source);
        _activeDebuffs[debuff.debuffType] = instance;
        OnApplyDebuff(instance);
    
        return true;
    }

    // =========================================================
    // APPLICATION BUFF
    // =========================================================

    public void ApplyBuff(BuffData buff, Entity source)
    {
        if (buff == null || _entity.isDead) return;

        // Refresh si déjà actif
        if (_activeBuffs.TryGetValue(buff.buffType, out var existing))
        {
            existing.Refresh();
            return;
        }

        BuffInstance instance = (BuffInstance)buff.CreateInstance(source);
        _activeBuffs[buff.buffType] = instance;
        OnApplyBuff(instance);
    }

    // =========================================================
    // ON APPLY
    // =========================================================

    private void OnApplyDebuff(DebuffInstance instance)
    {
        switch (instance.DebuffType)
        {
            case DebuffType.Stun:
                isStunned = true;
                break;
            case DebuffType.Silence:
                isSilenced = true;
                break;
            case DebuffType.Root:
                isRooted = true;
                break;
            case DebuffType.Blind:
                isBlinded = true;
                blindPrecisionMalus += instance.DebuffData.debuffValue;
                break;
            case DebuffType.Fear:
                isFeared = true;
                break;
            case DebuffType.Sleep:
                isSleeping = true;
                break;
            case DebuffType.Mark:
                isMarked = true;
                markDamageBonus += instance.DebuffData.debuffValue;
                break;
            case DebuffType.ManaDrain:
                manaBreakAmount += instance.DebuffData.damagePerSecond;
                break;
            case DebuffType.Freeze:
            case DebuffType.Slow:
                slowMultiplier = Mathf.Min(slowMultiplier, instance.DebuffData.slowMultiplier);
                break;
            case DebuffType.Shocked:
                // GDD v30 §21bis.1 — Foudre : interruption cast + mini-stun 0.5s
                // Le mini-stun est appliqué via un Stun temporaire séparé dans CombatSystem/SkillSystem
                shockDefenseReduction += instance.DebuffData.defenseReduction;
                break;
            case DebuffType.Poison:
                // GDD v30 §21bis.1 — Nature : DoT + réduction soins %
                isPoisoned = true;
                poisonHealReduction += instance.DebuffData.healReduction;
                break;
            case DebuffType.ArmorBreak:
                // GDD v30 §21bis.1 — Terre : réduction défense physique % temporaire
                isArmorBroken = true;
                armorBreakReduction += instance.DebuffData.defenseReduction;
                break;
            // Taunt : géré dans OnApplyBuff (BuffType.Taunt) — §21bis.3
            // DebuffType.Taunt n'existe pas — Taunt est un buff sur le lanceur
        }
    }

    private void OnApplyBuff(BuffInstance instance)
    {
        switch (instance.BuffType)
        {
            case BuffType.Heal:
                float healAmt = instance.BuffData.GetHealAmount(_entity.MaxHP);
                if (healAmt > 0f) _entity.Heal(healAmt);
                break;
            case BuffType.Shield:
                instance.remainingShield = instance.BuffData.GetShieldAmount(_entity.MaxHP);
                break;
            case BuffType.DefenseUp:
                buffDefenseBonus += instance.BuffData.defenseBonus;
                break;
            case BuffType.DodgeUp:
                buffDodgeBonus += instance.BuffData.dodgeBonus;
                break;
            case BuffType.Haste:
                buffSpeedMultiplier = Mathf.Max(buffSpeedMultiplier, instance.BuffData.speedMultiplier);
                break;
            case BuffType.AttackUp:
                buffAttackBonus += instance.BuffData.attackBonus;
                break;
            case BuffType.Invincible:
                isInvincible = true;
                break;
            case BuffType.Stealth:
                isStealthed = true;
                break;
            case BuffType.Regeneration:
                // GDD v30 §21bis.2 — Régénération HP progressive sur la durée
                // Le tick est géré dans Update() via buffRegenBonus (appliqué comme regen supplémentaire)
                buffRegenBonus += instance.BuffData.healPerSecond;
                break;
            case BuffType.Barrier:
                // GDD v30 §21bis.2 — Bouclier HP absorbant + résistance élémentaire
                instance.remainingShield  = instance.BuffData.GetShieldAmount(_entity.MaxHP);
                barrierElementResist     += instance.BuffData.elementResistBonus;
                break;
            case BuffType.Purified:
                // GDD v30 §21bis.2 — Cleanse instantané : supprime tous les debuffs actifs
                CleanseAllDebuffs();
                break;
            case BuffType.Taunt:
                // GDD v30 §21bis.3 — Force les mobs proches à cibler ce joueur (PvE uniquement)
                isTaunting = true;
                break;
        }
    }

    /// <summary>
    /// Supprime tous les debuffs actifs (Purified — GDD v30 §21bis.2).
    /// </summary>
    private void CleanseAllDebuffs()
    {
        var types = new List<DebuffType>(_activeDebuffs.Keys);
        foreach (DebuffType t in types)
            ExpireDebuff(t);
    }

    // =========================================================
    // EXPIRATION
    // =========================================================

    private void ExpireDebuff(DebuffType type)
    {
        if (!_activeDebuffs.TryGetValue(type, out var instance)) return;

        switch (type)
        {
            case DebuffType.Stun:    isStunned  = false; break;
            case DebuffType.Silence: isSilenced = false; break;
            case DebuffType.Root:    isRooted   = false; break;
            case DebuffType.Fear:    isFeared   = false; break;
            case DebuffType.Sleep:   isSleeping = false; break;
            // Taunt expire : géré dans OnExpireBuff (BuffType.Taunt) — §21bis.3
            case DebuffType.Mark:
                isMarked        = false;
                markDamageBonus = Mathf.Max(0f, markDamageBonus - instance.DebuffData.debuffValue);
                break;
            case DebuffType.Blind:
                blindPrecisionMalus = Mathf.Max(0f, blindPrecisionMalus - instance.DebuffData.debuffValue);
                isBlinded = blindPrecisionMalus > 0f;
                break;
            case DebuffType.ManaDrain:
                manaBreakAmount = Mathf.Max(0f, manaBreakAmount - instance.DebuffData.damagePerSecond);
                break;
            case DebuffType.Shocked:
                shockDefenseReduction = Mathf.Max(0f, shockDefenseReduction - instance.DebuffData.defenseReduction);
                break;
            case DebuffType.Poison:
                // GDD v30 §21bis.1 — retire la réduction de soins
                poisonHealReduction = Mathf.Max(0f, poisonHealReduction - instance.DebuffData.healReduction);
                isPoisoned = poisonHealReduction > 0f;
                break;
            case DebuffType.ArmorBreak:
                // GDD v30 §21bis.1 — retire la réduction défense physique
                armorBreakReduction = Mathf.Max(0f, armorBreakReduction - instance.DebuffData.defenseReduction);
                isArmorBroken = armorBreakReduction > 0f;
                break;
        }

        // ⚠ Remove AVANT RefreshSlowMultiplier — sinon le debuff expiré
        // est encore dans le dict et fausse le recalcul (vitesse ne revient pas à 1).
        _activeDebuffs.Remove(type);

        if (type == DebuffType.Freeze || type == DebuffType.Slow)
            RefreshSlowMultiplier();
    }

    private void ExpireBuff(BuffType type)
    {
        if (!_activeBuffs.TryGetValue(type, out var instance)) return;

        switch (type)
        {
            case BuffType.DefenseUp:
                buffDefenseBonus = Mathf.Max(0f, buffDefenseBonus - instance.BuffData.defenseBonus);
                break;
            case BuffType.DodgeUp:
                buffDodgeBonus = Mathf.Max(0f, buffDodgeBonus - instance.BuffData.dodgeBonus);
                break;
            case BuffType.AttackUp:
                buffAttackBonus = Mathf.Max(0f, buffAttackBonus - instance.BuffData.attackBonus);
                break;
            case BuffType.Invincible:
                isInvincible = false;
                break;
            case BuffType.Stealth:
                isStealthed = false;
                break;
            case BuffType.Regeneration:
                // GDD v30 §21bis.2 — retire le bonus de regen
                buffRegenBonus = Mathf.Max(0f, buffRegenBonus - instance.BuffData.healPerSecond);
                break;
            case BuffType.Barrier:
                // GDD v30 §21bis.2 — retire la résistance élémentaire temporaire
                barrierElementResist = Mathf.Max(0f, barrierElementResist - instance.BuffData.elementResistBonus);
                break;
            case BuffType.Taunt:
                isTaunting = false;
                break;
            // Purified n'a pas d'expiration (effet instantané)
        }

        // ⚠ Remove AVANT RefreshSpeedMultiplier — même raison que pour Slow.
        _activeBuffs.Remove(type);

        if (type == BuffType.Haste)
            RefreshSpeedMultiplier();
    }

    // =========================================================
    // BOUCLIER
    // =========================================================

    /// <summary>Absorbe les dégâts avec le bouclier actif. Retourne les dégâts résiduels.</summary>
    public float AbsorbWithShield(float incomingDamage)
    {
        if (!_activeBuffs.TryGetValue(BuffType.Shield, out var buff)) return incomingDamage;
        float remaining = buff.AbsorbDamage(incomingDamage);
        if (buff.remainingShield <= 0f) ExpireBuff(BuffType.Shield);
        return remaining;
    }

    // =========================================================
    // ACCESSEURS — lus par CombatSystem
    // =========================================================

    public bool HasDebuff(DebuffType type) => _activeDebuffs.ContainsKey(type);
    public bool HasBuff(BuffType type)     => _activeBuffs.ContainsKey(type);

    public float GetBuffDefenseBonus()      => buffDefenseBonus;
    public float GetBuffDodgeBonus()        => buffDodgeBonus;
    public float GetBuffSpeedMultiplier()   => buffSpeedMultiplier;
    public float GetBuffAttackBonus()       => buffAttackBonus;
    public float GetMarkDamageBonus()       => isMarked ? markDamageBonus : 0f;
    public float GetBlindMalus()            => blindPrecisionMalus;
    public float GetShockDefenseReduction() => shockDefenseReduction;
    public float GetArmorBreakReduction()   => isArmorBroken ? armorBreakReduction : 0f;
    public float GetPoisonHealReduction()   => isPoisoned ? poisonHealReduction : 0f;
    public float GetBarrierElementResist()  => barrierElementResist;

    // =========================================================
    // UI — données pour StatusEffectUI
    // =========================================================

    /// <summary>
    /// Retourne la liste de tous les effets actifs pour l'affichage UI.
    /// Appelé par StatusEffectUI chaque frame.
    /// </summary>
    public List<StatusEffectUIEntry> GetActiveEffectsForUI()
    {
        var list = new List<StatusEffectUIEntry>();

        foreach (var kvp in _activeDebuffs)
            list.Add(new StatusEffectUIEntry
            {
                key           = kvp.Key.ToString(),
                icon          = kvp.Value.data.icon,
                remainingTime = kvp.Value.remainingTime,
                totalDuration = kvp.Value.data.duration,
                isDebuff      = true
            });

        foreach (var kvp in _activeBuffs)
            list.Add(new StatusEffectUIEntry
            {
                key           = kvp.Key.ToString(),
                icon          = kvp.Value.data.icon,
                remainingTime = kvp.Value.remainingTime,
                totalDuration = kvp.Value.data.duration,
                isDebuff      = false
            });

        return list;
    }

    // =========================================================
    // SLEEP — réveil au premier dégât
    // =========================================================

    /// <summary>
    /// Appelé par Entity.TakeDamage — réveille l'entité si elle dort.
    /// Retourne true si les dégâts doivent être annulés (Invincible).
    /// </summary>
    public bool OnTakeDamage()
    {
        // Invincible — annule les dégâts
        if (isInvincible) return true;

        // Sleep — réveil au premier dégât
        if (isSleeping) ExpireDebuff(DebuffType.Sleep);

        return false;
    }

    // =========================================================
    // RÉSISTANCES
    // =========================================================

    public void SetDebuffResistance(DebuffType type, float value)
        => _debuffResistances[type] = Mathf.Clamp01(value);

    public float GetDebuffResistance(DebuffType type)
        => _debuffResistances.TryGetValue(type, out float v) ? v : 0f;

    public void ResetDebuffResistances() => _debuffResistances.Clear();

    // =========================================================
    // HELPERS PRIVÉS
    // =========================================================

    private void RefreshSlowMultiplier()
    {
        slowMultiplier = 1f;
        foreach (var kvp in _activeDebuffs)
            if (kvp.Key == DebuffType.Freeze || kvp.Key == DebuffType.Slow)
                slowMultiplier = Mathf.Min(slowMultiplier, kvp.Value.DebuffData.slowMultiplier);
    }

    private void RefreshSpeedMultiplier()
    {
        buffSpeedMultiplier = 1f;
        foreach (var kvp in _activeBuffs)
            if (kvp.Key == BuffType.Haste)
                buffSpeedMultiplier = Mathf.Max(buffSpeedMultiplier, kvp.Value.BuffData.speedMultiplier);
    }
}