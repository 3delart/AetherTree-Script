using UnityEngine;

// =============================================================
// BuffData — ScriptableObject template de buff
// Path : Assets/Scripts/Data/StatusEffect/BuffData.cs
// AetherTree GDD v30 — Section 21bis
//
// Assets > Create > AetherTree > StatusEffects > BuffData
// =============================================================

[CreateAssetMenu(fileName = "NewBuff", menuName = "AetherTree/StatusEffects/BuffData")]
public class BuffData : StatusEffectData
{
    [Header("Type de buff")]
    public BuffType buffType;

    // ── Soin instantané (Heal) ────────────────────────────────
    [Header("Soin instantané (Heal)")]
    [Tooltip("Flat : valeur fixe (ex: 1000 HP)\nPercent : % du MaxHP de la cible (ex: 0.10 = 10%)")]
    public ModifierType healModifier = ModifierType.Flat;
    [Tooltip("Montant de soin instantané.")]
    public float healAmount = 0f;

    // ── Soin sur la durée (HoT, RegenHP) ─────────────────────
    [Header("Soin sur la durée (HoT, RegenHP)")]
    [Tooltip("Flat : valeur fixe par seconde\nPercent : % du MaxHP de la cible par seconde")]
    public ModifierType hotModifier = ModifierType.Flat;
    [Tooltip("Soin par seconde.")]
    public float healPerSecond = 0f;

    // ── Bouclier (Shield) ─────────────────────────────────────
    [Header("Bouclier (Shield)")]
    [Tooltip("Flat : montant fixe absorbé\nPercent : % du MaxHP de la cible")]
    public ModifierType shieldModifier = ModifierType.Flat;
    [Tooltip("Montant de dégâts absorbés.")]
    public float shieldAmount = 0f;

    // ── Résistance élémentaire (Barrier — §21bis.2) ───────────
    [Header("Résistance élémentaire (Barrier)")]
    [Tooltip("Barrier (§21bis.2) : résistance élémentaire temporaire.\n0 = aucun effet | 0.20 = -20% dégâts élémentaires reçus.")]
    [Range(0f, 1f)]
    public float elementResistBonus = 0f;

    // ── Défense (DefenseUp) ───────────────────────────────────
    [Header("Défense (DefenseUp)")]
    [Tooltip("Flat : valeur fixe | Percent : % de la défense actuelle")]
    public ModifierType defenseModifier = ModifierType.Flat;
    [Tooltip("Bonus de défense appliqué aux 3 types.")]
    public float defenseBonus = 0f;

    // ── Esquive (DodgeUp) ─────────────────────────────────────
    [Header("Esquive (DodgeUp)")]
    [Tooltip("Flat : valeur fixe | Percent : % de l'esquive actuelle")]
    public ModifierType dodgeModifier = ModifierType.Flat;
    [Tooltip("Bonus d'esquive.")]
    public float dodgeBonus = 0f;

    // ── Vitesse (Haste) ───────────────────────────────────────
    [Header("Vitesse (Haste)")]
    [Tooltip("Multiplicateur de vitesse.\nEx: 1.3 = +30% vitesse.")]
    public float speedMultiplier = 1f;

    // ── Attaque (AttackUp) ────────────────────────────────────
    [Header("Attaque (AttackUp)")]
    [Tooltip("Flat : valeur fixe | Percent : % de l'attaque actuelle")]
    public ModifierType attackModifier = ModifierType.Flat;
    [Tooltip("Bonus d'attaque ajouté à baseAttackMin et baseAttackMax.")]
    public float attackBonus = 0f;

    // ── Augmentation de stat (Stats) ──────────────────────────
    [Header("Augmentation de stat (Stats)")]
    [Tooltip("Stat à augmenter — utilisé uniquement si buffType = Stats.")]
    public BuffStatType buffStatType = BuffStatType.AttackDamage;
    [Tooltip("Flat : valeur directe | Percent : ratio (0.10 = +10%)")]
    public ModifierType buffModifier = ModifierType.Flat;
    [Tooltip("Valeur du bonus.")]
    public float buffStatValue = 0f;

    // ── Helpers ───────────────────────────────────────────────
    /// <summary>Calcule le soin instantané selon le MaxHP de la cible.</summary>
    public float GetHealAmount(float targetMaxHP)
        => healModifier == ModifierType.Percent ? targetMaxHP * healAmount : healAmount;

    /// <summary>Calcule le soin/s selon le MaxHP de la cible.</summary>
    public float GetHealPerSecond(float targetMaxHP)
        => hotModifier == ModifierType.Percent ? targetMaxHP * healPerSecond : healPerSecond;

    /// <summary>Calcule le montant du bouclier selon le MaxHP de la cible.</summary>
    public float GetShieldAmount(float targetMaxHP)
        => shieldModifier == ModifierType.Percent ? targetMaxHP * shieldAmount : shieldAmount;

    public override StatusEffectInstance CreateInstance(Entity source)
        => new BuffInstance(this, source);
}
