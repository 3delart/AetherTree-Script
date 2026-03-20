using UnityEngine;
using System.Collections.Generic;

// =============================================================
// ArmorData — ScriptableObject template d'armure corps
// Path : Assets/Scripts/Data/Inventory/Equipment/ArmorData.cs
// AetherTree GDD v30 — Section 5.1 / 5.2 / 5.3 / 5.4
//
// Stats rollées au drop :
//   meleeDefense, rangedDefense, magicDefense → affectées par rareté + upgrade
//   dodge                                     → rollé, NON affecté par rareté/upgrade
//
// Stats fixes (définies dans ArmorData) :
//   defenseGrade (1–10) — utilisé dans CombatSystem étape ③
//
// 4 slots de configuration (uniformes sur tous les équipements) :
//   bonuses           → StatBonus (HP, mana, défense, résistances...)
//   statusEffects     → StatusEffectEntry (debuffs/buffs à l'attaque)
//   debuffResistances → DebuffResistanceEntry (résistance aux debuffs)
//   onHitEffects      → OnHitEffectEntry (effets quand on reçoit un coup)
// =============================================================

// =============================================================
// DebuffResistanceEntry — une ligne de résistance à un debuff
// Ex: Freeze 0.05 = 5% de chance de résister au gel
// =============================================================
[System.Serializable]
public class DebuffResistanceEntry
{
    [Tooltip("Type de debuff résisté.")]
    public DebuffType debuffType;

    [Tooltip("Chance de résister [0..1].\nEx: 0.05 = 5% | 0.30 = 30%")]
    [Range(0f, 1f)]
    public float resistChance = 0.05f;
}

[CreateAssetMenu(fileName = "NewArmor", menuName = "AetherTree/Equipment/ArmorData")]
public class ArmorData : ScriptableObject
{
    // ── Identité ──────────────────────────────────────────────
    [Header("Identité")]
    public string armorName = "Armor";

    [Tooltip("Type d'armure — détermine quel joueur peut l'équiper.\n" +
             "Melee = Lourde | Ranged = Légère | Magic = Robe\n" +
             "GDD v21 section 2.1 — vérifié dans Player.EquipArmor()")]
    public ArmorType  armorType = ArmorType.Melee;

    public Sprite     icon;
    public GameObject armorPrefab;

    [Header("Niveau")]
    [Tooltip("Niveau minimum requis pour équiper cette arme.")]
    [Min(1)] public int requiredLevel = 1;

    // ── Stats variables — rollées + rareté + upgrade ──────────
    [Header("Stats variables (rollées au drop — affectées par rareté + upgrade)")]

    [Tooltip("Borne basse du roll pour la défense mêlée")]
    public float baseMeleeDefenseMin  = 10f;
    [Tooltip("Borne haute du roll pour la défense mêlée")]
    public float baseMeleeDefenseMax  = 15f;

    [Tooltip("Borne basse du roll pour la défense distance")]
    public float baseRangedDefenseMin = 10f;
    [Tooltip("Borne haute du roll pour la défense distance")]
    public float baseRangedDefenseMax = 15f;

    [Tooltip("Borne basse du roll pour la défense magique")]
    public float baseMagicDefenseMin  = 8f;
    [Tooltip("Borne haute du roll pour la défense magique")]
    public float baseMagicDefenseMax  = 12f;

    // ── Stats rollées — NON affectées par rareté/upgrade ──────
    [Header("Stats rollées (NON affectées par rareté/upgrade)")]

    [Tooltip("Borne basse du roll pour l'esquive")]
    public float baseDodgeMin = 5f;
    [Tooltip("Borne haute du roll pour l'esquive")]
    public float baseDodgeMax = 10f;

    // ── Stats fixes ───────────────────────────────────────────
    [Header("Stats fixes (inchangées par rareté/upgrade)")]

    [Tooltip("Grade de défense (1–10) — PvP uniquement (§6.3).\nFormule PvP : bonus = (attackGrade − defenseGrade) × 5%\nIgnoré en PvE — invisible pour le joueur.")]
    [Range(1, 10)]
    public int defenseGrade = 5;

    // ── 4 slots de configuration ──────────────────────────────

    [Header("① Bonus fixes (stats passives)")]
    [Tooltip("Bonus supplémentaires de cette armure.\n" +
             "Ex: BonusHP 200 | ResistFire 0.10 | BonusMana 50\n" +
             "Ces bonus sont fixes — ils ne sont pas affectés par rareté/upgrade.")]
    public List<StatBonus> bonuses = new List<StatBonus>();

    [Header("② Effets de statut (appliqués à chaque attaque)")]
    [Tooltip("Effets appliqués lors d'une attaque selon leur probabilité.\n" +
             "Glisse un DebuffData ou BuffData + règle la chance.")]
    public List<StatusEffectEntry> statusEffects = new List<StatusEffectEntry>();

    [Header("③ Résistances aux debuffs")]
    [Tooltip("Chances de résister à un debuff spécifique.\n" +
             "Ex: Freeze 0.05 = 5% de chance de résister au gel.")]
    public List<DebuffResistanceEntry> debuffResistances = new List<DebuffResistanceEntry>();

    [Header("④ Effets On-Hit (déclenchés quand on reçoit un coup)")]
    [Tooltip("Effets déclenchés quand le porteur reçoit un coup.\n" +
             "Ex: Thorns 10 dmg à 100% | Reflect 20% à 15% | CounterFreeze à 10%\n" +
             "Glisse un OnHitEffectData + ajuste la chance si besoin.")]
    public List<OnHitEffectEntry> onHitEffects = new List<OnHitEffectEntry>();

    [Header("Description")]
    [TextArea]
    public string       description  = "";


    // ── Utilitaires ───────────────────────────────────────────

    public ArmorInstance CreateDropInstance(int rarityRank = 0, int upgradeLevel = 0)
    {
        float rolledMelee  = Random.Range(baseMeleeDefenseMin,  baseMeleeDefenseMax);
        float rolledRanged = Random.Range(baseRangedDefenseMin, baseRangedDefenseMax);
        float rolledMagic  = Random.Range(baseMagicDefenseMin,  baseMagicDefenseMax);
        float rolledDodge  = Random.Range(baseDodgeMin,         baseDodgeMax);

        return new ArmorInstance(this, rolledMelee, rolledRanged, rolledMagic, rolledDodge, rarityRank, upgradeLevel);
    }

    public static int RollRarity()
    {
        float roll = Random.value * 100f;
        if (roll < 8f)     return -2;
        if (roll < 20f)    return -1;
        if (roll < 40.85f) return  0;
        if (roll < 58.85f) return  1;
        if (roll < 74.55f) return  2;
        if (roll < 86.05f) return  3;
        if (roll < 94.55f) return  4;
        if (roll < 98.65f) return  5;
        if (roll < 99.65f) return  6;
        return 7;
    }
}

// =============================================================
// ArmorInstance — données runtime d'une armure droppée
// =============================================================
[System.Serializable]
public class ArmorInstance
{
    public ArmorData data;

    public float rolledMeleeDefense;
    public float rolledRangedDefense;
    public float rolledMagicDefense;
    public float rolledDodge;

    public int rarityRank   = 0;
    public int upgradeLevel = 0;

    public RuneInstance equippedRune = null;

    public ArmorInstance(ArmorData source,
                         float meleeDefense, float rangedDefense, float magicDefense,
                         float dodge, int rarity = 0, int upgrade = 0)
    {
        data                 = source;
        rolledMeleeDefense   = meleeDefense;
        rolledRangedDefense  = rangedDefense;
        rolledMagicDefense   = magicDefense;
        rolledDodge          = dodge;
        rarityRank           = rarity;
        upgradeLevel         = upgrade;
    }

    private float RarityBonus => rarityRank * 0.10f;

    private float UpgradeBonus
    {
        get
        {
            int n = Mathf.Clamp(upgradeLevel, 0, 10);
            return (n * (n + 1) / 2f) * 0.01f;
        }
    }

    public float FinalMeleeDefense  => rolledMeleeDefense  * (1f + RarityBonus) * (1f + UpgradeBonus);
    public float FinalRangedDefense => rolledRangedDefense * (1f + RarityBonus) * (1f + UpgradeBonus);
    public float FinalMagicDefense  => rolledMagicDefense  * (1f + RarityBonus) * (1f + UpgradeBonus);
    public float FinalDodge         => rolledDodge;
    public int   DefenseGrade       => data.defenseGrade;

    // ── Raccourcis SO (4 slots) ───────────────────────────────
    public ArmorType                    ArmorType         => data.armorType;
    public string                       ArmorName         => data.armorName;
    public Sprite                       Icon              => data.icon;
    public List<StatBonus>              Bonuses           => data.bonuses;
    public List<StatusEffectEntry>      StatusEffects     => data.statusEffects;
    public List<DebuffResistanceEntry>  DebuffResistances => data.debuffResistances;
    public List<OnHitEffectEntry>       OnHitEffects      => data.onHitEffects;
    public string                       RarityLabel       => rarityRank >= 0 ? $"r+{rarityRank}" : $"r{rarityRank}";

    // ── Rune ──────────────────────────────────────────────────
    public bool TryInsertRune(RuneInstance rune)
    {
        if (rune == null)
        {
            Debug.LogWarning("[ArmorInstance] TryInsertRune : rune null.");
            return false;
        }
        if (rune.Category != RuneCategory.Armor)
        {
            Debug.LogWarning($"[ArmorInstance] {rune.RuneName} est une rune Weapon — incompatible avec une armure.");
            return false;
        }
        if (!rune.CanInsertInto(rarityRank))
        {
            Debug.LogWarning($"[ArmorInstance] Rune {rune.RarityLabel} trop haute pour cette armure ({RarityLabel}).");
            return false;
        }
        if (equippedRune != null)
            Debug.Log($"[ArmorInstance] Rune {equippedRune.RuneName} écrasée par {rune.RuneName}.");

        equippedRune = rune;
        Debug.Log($"[ArmorInstance] Rune insérée : {rune.Label} dans {ArmorName}.");
        return true;
    }

    public RuneInstance RemoveRune()
    {
        RuneInstance removed = equippedRune;
        equippedRune = null;
        return removed;
    }
}