using UnityEngine;

// =============================================================
// DebuffData — ScriptableObject template de debuff
// Path : Assets/Scripts/Data/StatusEffect/DebuffData.cs
// AetherTree GDD v30 — Section 21bis
//
// Assets > Create > AetherTree > StatusEffects > DebuffData
// =============================================================

[CreateAssetMenu(fileName = "NewDebuff", menuName = "AetherTree/StatusEffects/DebuffData")]
public class DebuffData : StatusEffectData
{
    [Header("Type de debuff")]
    public DebuffType debuffType;

    // ── Dégâts sur la durée (Burn, Poison, Bleed) ─────────────
    [Header("Dégâts sur la durée (Burn, Poison, Bleed)")]
    [Tooltip("Dégâts par seconde.")]
    public float damagePerSecond = 0f;

    [Tooltip("Élément des dégâts.\nBurn → Fire | Poison → Nature | Bleed → Neutral")]
    public ElementType damageElement = ElementType.Neutral;

    // ── Ralentissement (Freeze, Slow) ─────────────────────────
    [Header("Ralentissement (Freeze, Slow)")]
    [Tooltip("Multiplicateur de vitesse [0..1].\n0 = immobilisé | 0.5 = 50% vitesse | 1 = aucun effet\nFreeze réattribué à Eau en v30 (Glace supprimée).")]
    [Range(0f, 1f)]
    public float slowMultiplier = 0f;

    // ── Réduction de soins (Poison) ───────────────────────────
    [Header("Réduction de soins reçus (Poison)")]
    [Tooltip("Poison (§21bis.1) : réduit les soins reçus par la cible.\n0 = aucune réduction | 0.3 = -30% soins reçus")]
    [Range(0f, 1f)]
    public float healReduction = 0f;

    

    // ── Réduction de défense (Shock) ──────────────────────────
    [Header("Réduction de défense (Shock)")]
    [Tooltip("Réduction de défense flat appliquée aux 3 types.")]
    public float defenseReduction = 0f;


    // ── Réduction de stat (Stats) ─────────────────────────────
    [Header("Réduction de stat (Stats)")]
    [Tooltip("Stat à réduire — utilisé uniquement si debuffType = Stats.")]
    public DebuffStatType debuffStatType = DebuffStatType.MoveSpeed;

    [Tooltip("Type de modificateur — Flat ou Percent.")]
    public ModifierType debuffModifier = ModifierType.Percent;

    [Tooltip("Valeur de réduction.\nFlat : valeur directe | Percent : ratio (0.10 = -10%)")]
    public float debuffValue = 0f;

    public override StatusEffectInstance CreateInstance(Entity source)
        => new DebuffInstance(this, source);
}
