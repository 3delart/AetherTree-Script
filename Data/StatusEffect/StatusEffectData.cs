using UnityEngine;
using System.Collections.Generic;

// =============================================================
// StatusEffectData — SO de base pour tous les buffs et debuffs
// Path : Assets/Scripts/Data/StatusEffect/StatusEffectData.cs
// AetherTree GDD v30 — Section 21bis
//
// Historique :
//   v21 — Silence réattribué à Ténèbres (Wind supprimé v21)
//   v30 — Glace supprimée → Freeze reste, réattribué à Eau
//          Shock renommé ArmorBreak (réduction défense physique)
//          Shocked ajouté séparément (interrupt + mini-stun Foudre)
//          ManaBreak renommé ManaDrain (cohérence GDD §21bis.1)
//          HealOnTime renommé Regeneration (cohérence GDD §21bis.2)
//          AttackSpeedUp / MoveSpeedUp fusionnés dans Haste (§21bis.2)
//          RangeDefense corrigé en RangedDefense (cohérence §4.3)
//          Doublons BuffStatType / DebuffStatType supprimés
// =============================================================

// ── Enums debuff ──────────────────────────────────────────────
public enum DebuffType
{
    // DoT
    Burn,       // Brûlure    — Feu        — dégâts sur la durée, tick/s (§21bis.1)
    Poison,     // Poison     — Nature     — DoT + réduction soins reçus % (§21bis.1)
    Bleed,      // Saignement — Neutre     — dégâts sur la durée

    // Ralentissement & Immobilisation
    Freeze,     // Gel        — Eau        — ralentit / immobilise (réattribué Eau v30)
    Slow,       // Ralenti    — Eau        — réduit la vitesse de déplacement % (§21bis.1)
    Root,       // Enraciné   — Nature     — bloque le mouvement, peut toujours attaquer (§21bis.1)

    // Contrôle de foule dur
    Stun,       // Étourdi    — Terre      — bloque toutes les actions (CC dur §21bis.1)
    Fear,       // Peur       — Ténèbres   — fuite incontrôlée (CC dur §21bis.1)
    Sleep,      // Sommeil    — réveil au premier dégât reçu (§21bis.3)

    // Déplacement & Interrupt
    Knockback,  // Recul      — Eau        — déplace la cible à l'impact (§21bis.1)
    Shocked,    // Choc       — Foudre     — interruption du cast + mini-stun 0.5s (§21bis.1)

    // Précision & Ressource
    Blind,      // Aveugle    — Lumière    — réduction précision drastique (§21bis.1)
    ManaDrain,  // Drain mana — Ténèbres   — drain progressif sur la durée (§21bis.1)

    // Défense
    ArmorBreak, // Armure brisée — Terre  — réduction défense physique % temporaire (§21bis.1)

    // Utilitaire
    Silence,    // Silence    — Ténèbres   — bloque les skills (réattribué Ténèbres v30)

    // Stats & Spéciaux
    Stats,      // Réduction de stat spécifique (utilise DebuffStatType)
    Curse,      // Malédiction spéciale
    Mark,       // Marque pour bonus dégâts
    Other,      // Effet spécial custom
}

// ── Enums buff ────────────────────────────────────────────────
public enum BuffType
{
    // Soins
    Heal,           // Soin instantané (§21bis.2)
    Regeneration,   // Soin sur la durée — HoT — Lumière / Skills soin (§21bis.2)
    RegenHP,        // Régénération HP passive (tick interne Entity)

    // Défense
    Shield,         // Bouclier — absorbe les dégâts en priorité avant les HP (§21bis.2)
    Barrier,        // Bouclier HP + résistance élémentaire — Nature (§21bis.2)
    DefenseUp,      // Augmentation de défense — Fortify — Terre (§21bis.2)
    DodgeUp,        // Augmentation d'esquive

    // Offensif
    AttackUp,       // Augmentation d'attaque
    Haste,          // Augmentation vitesse déplacement + attackSpeed — Foudre (§21bis.2)
    CritChanceUp,   // Augmentation de chance de critique
    CritDamageUp,   // Augmentation de dégâts critiques

    // Spéciaux
    Purified,       // Suppression de tous les debuffs actifs — Lumière (§21bis.2)
    Invincible,     // Invincibilité temporaire — post-respawn 3s (§21bis.3)
    Stealth,        // Furtivité — interrompue par attaque/dégât reçu (§21bis.3)
    Taunt,          // Force les mobs proches à cibler le lanceur — PvE only (§21bis.3)
    Dispel,         // Supprime un buff spécifique sur la cible ennemie (§21bis.3)
    Stats,          // Augmentation de stat spécifique (utilise BuffStatType)
    Other,          // Effet spécial custom
}

// ── Stat ciblée par un buff Stats ─────────────────────────────
public enum BuffStatType
{
    MaxHP, MaxMana, RegenHP, RegenMana,
    AttackDamage, AttackSpeed, MoveSpeed,
    MeleeDefense, RangedDefense, MagicDefense,
    CritChance, CritDamage,
    ElementalPoint, Dodge,
    FireResistance, WaterResistance, EarthResistance,
    NatureResistance, LightningResistance,
    DarknessResistance, LightResistance,
    AllResistances,
}

// ── Stat ciblée par un debuff Stats ───────────────────────────
public enum DebuffStatType
{
    MaxHP, MaxMana, RegenHP, RegenMana,
    AttackDamage, AttackSpeed, MoveSpeed,
    MeleeDefense, RangedDefense, MagicDefense,
    CritChance, CritDamage,
    ElementalPoint, Dodge,
    FireResistance, WaterResistance, EarthResistance,
    NatureResistance, LightningResistance,
    DarknessResistance, LightResistance,
    AllResistances,
}

// =============================================================
// StatusEffectData — base abstraite
// =============================================================
public abstract class StatusEffectData : ScriptableObject
{
    [Header("Identité")]
    public string effectName = "Effect";
    public Sprite icon;

    [Header("Durée")]
    [Tooltip("Durée de base de l'effet en secondes.\n§21bis.4 : durée et chance d'application définitives sur SkillData.")]
    public float duration = 3f;

    /// <summary>Crée une instance runtime de cet effet.</summary>
    public abstract StatusEffectInstance CreateInstance(Entity source);
}

// =============================================================
// StatusEffectEntry — une ligne sur arme / rune / skill
// =============================================================
[System.Serializable]
public class StatusEffectEntry
{
    [Tooltip("SO de l'effet à appliquer (DebuffData ou BuffData).")]
    public StatusEffectData effect;

    [Tooltip("Probabilité d'application par attaque/utilisation [0..1].\nEx: 0.03 = 3% de chance.")]
    [Range(0f, 1f)]
    public float chance = 0.05f;

    /// <summary>Roll si l'effet se déclenche. True = appliquer l'effet.</summary>
    public bool Roll() => effect != null && Random.value < chance;
}
