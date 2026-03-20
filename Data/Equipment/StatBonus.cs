using UnityEngine;

// =============================================================
// StatBonus — Système de bonus de stats générique
// Path : Assets/Scripts/Data/Inventory/Equipment/StatBonus.cs
// AetherTree GDD v30 — Section 6.2
//
// Utilisé par tout équipement apportant des bonus fixes :
//   Casque, Gants, Bottes, Cape, Ceinture...
//
// Pour ajouter une nouvelle stat :
//   1. Ajouter la valeur dans StatType
//   2. Ajouter le case dans PlayerStats.ApplyStatBonus()
//   C'est tout — aucun autre fichier à modifier.
//
// Unités à respecter dans l'Inspector :
//   Défense / Attaque / Points : valeur flat (ex: 10, 25)
//   CritChance / Résistances   : ratio [0..1]  (ex: 0.05 = 5%)
//   CritDamage                 : ratio [0..1+] (ex: 0.15 = +15%)
//   MoveSpeed                  : valeur flat (ex: 0.5)
//   HP / Mana / Regen          : valeur flat (ex: 50, 5)
// =============================================================

public enum StatType
{
    // ── Défense ───────────────────────────────────────────────
    [InspectorName("Melee Defense (flat)")]    MeleeDefense,
    [InspectorName("Ranged Defense (flat)")]   RangedDefense,
    [InspectorName("Magic Defense (flat)")]    MagicDefense,

    // ── Attaque ───────────────────────────────────────────────
    [InspectorName("Bonus Attack (flat)")]     BonusAttack,

    // ── Mobilité ──────────────────────────────────────────────
    [InspectorName("Dodge (flat)")]            Dodge,
    [InspectorName("Precision (flat)")]        Precision,
    [InspectorName("Move Speed (flat)")]       MoveSpeed,

    // ── Critique ──────────────────────────────────────────────
    [InspectorName("Crit Chance (ratio 0.05 = 5%)")]   CritChance,
    [InspectorName("Crit Damage (ratio 0.15 = +15%)")] CritDamage,

    // ── Résistances élémentaires ──────────────────────────────
    [InspectorName("Resist Fire (ratio 0.10 = 10%)")]      ResistFire,
    [InspectorName("Resist Water (ratio 0.10 = 10%)")]     ResistWater,
    [InspectorName("Resist Lightning (ratio 0.10 = 10%)")] ResistLightning,
    [InspectorName("Resist Earth (ratio 0.10 = 10%)")]     ResistEarth,
    [InspectorName("Resist Nature (ratio 0.10 = 10%)")]    ResistNature,
    [InspectorName("Resist Darkness (ratio 0.10 = 10%)")] ResistDarkness,
    [InspectorName("Resist Light (ratio 0.10 = 10%)")]     ResistLight,
    [InspectorName("Resist ALL (ratio 0.10 = 10%)")]       ResistAll,

    // ── Points élémentaires ───────────────────────────────────
    [InspectorName("Points Fire (flat)")]      PointsFire,
    [InspectorName("Points Water (flat)")]     PointsWater,
    [InspectorName("Points Lightning (flat)")] PointsLightning,
    [InspectorName("Points Earth (flat)")]     PointsEarth,
    [InspectorName("Points Nature (flat)")]    PointsNature,
    [InspectorName("Points Darkness (flat)")] PointsDarkness,
    [InspectorName("Points Light (flat)")]     PointsLight,
    [InspectorName("Points ALL (flat)")]       PointsAll,

    // ── Bonus % sur points élémentaires ──────────────────────
    // Multiplie les points élémentaires existants du joueur.
    // Ex: ElementBonusFire 0.05 + 100 pts feu → 105 pts feu effectifs.
    [InspectorName("Element Bonus Fire (ratio 0.05 = +5% pts feu)")]      ElementBonusFire,
    [InspectorName("Element Bonus Water (ratio 0.05 = +5% pts eau)")]     ElementBonusWater,
    [InspectorName("Element Bonus Lightning (ratio 0.05 = +5% pts foudre)")] ElementBonusLightning,
    [InspectorName("Element Bonus Earth (ratio 0.05 = +5% pts terre)")]   ElementBonusEarth,
    [InspectorName("Element Bonus Nature (ratio 0.05 = +5% pts nature)")] ElementBonusNature,
    [InspectorName("Element Bonus Darkness (ratio 0.05 = +5% pts ténèbres)")] ElementBonusDarkness,
    [InspectorName("Element Bonus Light (ratio 0.05 = +5% pts lumière)")] ElementBonusLight,
    [InspectorName("Element Bonus ALL (ratio 0.05 = +5% tous pts élém)")] ElementBonusAll,

    // ── Vie & Mana ────────────────────────────────────────────
    [InspectorName("Bonus HP (flat)")]         BonusHP,
    [InspectorName("Bonus Mana (flat)")]       BonusMana,
    [InspectorName("Bonus Regen HP (flat)")]   BonusRegenHP,
    [InspectorName("Bonus Regen Mana (flat)")] BonusRegenMana,
}

// =============================================================
// =============================================================
// StatBonus — une ligne de bonus dans l'Inspector
//
// ┌─────────────────────────┬──────────┬────────────────────────┐
// │ StatType                │ Unité    │ Exemple                │
// ├─────────────────────────┼──────────┼────────────────────────┤
// │ MeleeDefense            │ flat     │ 30   → +30 déf mêlée   │
// │ RangedDefense           │ flat     │ 20   → +20 déf distance│
// │ MagicDefense            │ flat     │ 15   → +15 déf magique │
// │ BonusAttack             │ flat     │ 15   → +15 atk min/max │
// │ Dodge                   │ flat     │ 10   → +10 esquive     │
// │ Precision               │ flat     │ 20   → +20 précision   │
// │ MoveSpeed               │ flat     │ 0.5  → +0.5 vitesse    │
// │ CritChance              │ RATIO    │ 0.05 → +5% crit        │
// │ CritDamage              │ RATIO    │ 0.15 → +15% mult crit  │
// │ ResistFire/All/...      │ RATIO    │ 0.10 → +10% résistance │
// │ PointsFire/All/...      │ flat     │ 10   → +10 pts élém    │
// │ BonusHP                 │ flat     │ 200  → +200 HP max     │
// │ BonusMana               │ flat     │ 50   → +50 Mana max    │
// │ BonusRegenHP            │ flat     │ 2    → +2 HP/s         │
// │ BonusRegenMana          │ flat     │ 1    → +1 Mana/s       │
// └─────────────────────────┴──────────┴────────────────────────┘
// =============================================================
[System.Serializable]
public class StatBonus
{
    [Tooltip("Type de stat à modifier — voir tableau des unités dans StatBonus.cs")]
    public StatType statType;

    [Tooltip(
        "FLAT  : Défenses, BonusAttack, Dodge, Précision, MoveSpeed,\n" +
        "        PointsFire/All/..., BonusHP, BonusMana, BonusRegen\n" +
        "        → entrer la valeur directe  ex: 200, 10, 0.5\n" +
        "\n" +
        "RATIO : CritChance, CritDamage, ResistFire/All/...\n" +
        "        → entrer en décimal  ex: 0.05 = 5% | 0.10 = 10%"
    )]
    public float value;
}
