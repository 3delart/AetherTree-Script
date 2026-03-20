using UnityEngine;
using System.Collections.Generic;

// =============================================================
// WeaponData — ScriptableObject template d'arme
// Path : Assets/Scripts/Data/Inventory/Equipment/WeaponData.cs
// AetherTree GDD v30 — Section 5.1 / 5.2 / 5.3 / 5.4
//
// Stats affectées par rareté + upgrade :
//   damageMin, damageMax, precision
//
// Stats fixes (jamais modifiées par rareté/upgrade) :
//   critChance, critDamage, attackSpeed, attackGrade
//
// 4 slots de configuration (uniformes sur tous les équipements) :
//   bonuses           → StatBonus (points élémentaires, crit, BonusAttack...)
//   statusEffects     → StatusEffectEntry (debuffs/buffs appliqués à l'attaque)
//   debuffResistances → DebuffResistanceEntry (résistance aux debuffs du porteur)
//   onHitEffects      → OnHitEffectEntry (effets quand le porteur reçoit un coup)
// =============================================================

[CreateAssetMenu(fileName = "NewWeapon", menuName = "AetherTree/Equipment/WeaponData")]
public class WeaponData : ScriptableObject
{
    // ── Identité ──────────────────────────────────────────────
    [Header("Identité")]
    public string     weaponName   = "Weapon";
    public WeaponType weaponType   = WeaponType.ShortSword;
    public Sprite     icon;
    public GameObject weaponPrefab;

    [Header("Niveau")]
    [Tooltip("Niveau minimum requis pour équiper cette arme.")]
    [Min(1)] public int requiredLevel = 1;

    // ── Stats variables — rollées au drop, affectées par rareté/upgrade ───
    [Header("Stats variables (rollées au drop — affectées par rareté + upgrade)")]

    [Tooltip("Borne basse du roll pour damageMin")]
    public float baseDamageMinLow  = 8f;
    [Tooltip("Borne haute du roll pour damageMin")]
    public float baseDamageMinHigh = 12f;

    [Tooltip("Borne basse du roll pour damageMax")]
    public float baseDamageMaxLow  = 13f;
    [Tooltip("Borne haute du roll pour damageMax")]
    public float baseDamageMaxHigh = 18f;

    [Tooltip("Borne basse du roll pour la précision")]
    public float basePrecisionMin = 85f;
    [Tooltip("Borne haute du roll pour la précision")]
    public float basePrecisionMax = 95f;

    // ── Stats fixes — jamais modifiées par rareté/upgrade ─────
    [Header("Stats fixes (inchangées par rareté/upgrade)")]

    [Tooltip("Attaques par seconde")]
    public float attackSpeed = 1f;

    [Tooltip("Chance de critique en % — fixe, indépendante de la rareté")]
    [Range(0f, 1f)]
    public float critChance  = 0.05f;

    [Tooltip("Multiplicateur de dégâts critiques.\nEx: 1.50 = +50% dégâts | 1.20 = +20% dégâts")]
    public float critDamage  = 1.50f;

    [Tooltip("Grade d'attaque (1–10) — utilisé dans CombatSystem étape ③ : bonus = (attackGrade − defenseGrade) × 5%")]
    [Range(1, 10)]
    public int attackGrade = 5;

    // ── 4 slots de configuration ──────────────────────────────

    [Header("① Bonus fixes (stats passives)")]
    [Tooltip("Bonus supplémentaires de cette arme.\n" +
             "Ex: PointsFire 10 | CritChance 0.05 | BonusAttack 15\n" +
             "Ces bonus sont fixes — ils ne sont pas affectés par rareté/upgrade.")]
    public List<StatBonus> bonuses = new List<StatBonus>();

    [Header("② Effets de statut (appliqués à chaque attaque)")]
    [Tooltip("Effets appliqués à chaque attaque selon leur probabilité.\n" +
             "Glisse un DebuffData ou BuffData + règle la chance.\n" +
             "Ex: Gel 2s à 3% | Brûlure 4s à 5%")]
    public List<StatusEffectEntry> statusEffects = new List<StatusEffectEntry>();

    [Header("③ Résistances aux debuffs (porteur)")]
    [Tooltip("Chances de résister à un debuff spécifique quand on est attaqué.\n" +
             "Ex: Freeze 0.05 = 5% de chance de résister au gel.\n" +
             "Chargées dans StatusEffectSystem via PlayerStats.RecalculateStats().")]
    public List<DebuffResistanceEntry> debuffResistances = new List<DebuffResistanceEntry>();

    [Header("④ Effets On-Hit (déclenchés quand le porteur reçoit un coup)")]
    [Tooltip("Effets déclenchés quand le porteur reçoit un coup.\n" +
             "Ex: Thorns 5 dmg à 100% | CounterPoison à 8% | HealOnHit 2% MaxHP\n" +
             "Glisse un OnHitEffectData + ajuste la chance si besoin.")]
    public List<OnHitEffectEntry> onHitEffects = new List<OnHitEffectEntry>();

    [Header("Description")]
    [TextArea]
    public string       description  = "";


    // ── Utilitaires ───────────────────────────────────────────

    /// <summary>Catégorie déduite du WeaponType (Melee/Ranged/Magic).</summary>
    public WeaponCategory Category => weaponType.GetCategory();

    /// <summary>ArmorType lié à cette catégorie d'arme.</summary>
    public ArmorType LinkedArmorType => weaponType.GetArmorType();

    /// <summary>
    /// Crée une instance droppée avec stats rollées.
    /// rarityRank : -2 à +7 | upgradeLevel : 0 à 10.
    /// </summary>
    public WeaponInstance CreateDropInstance(int rarityRank = 0, int upgradeLevel = 0)
    {
        float rolledDmgMin  = Random.Range(baseDamageMinLow,  baseDamageMinHigh);
        float rolledDmgMax  = Random.Range(baseDamageMaxLow,  baseDamageMaxHigh);
        float rolledPrec    = Random.Range(basePrecisionMin,  basePrecisionMax);

        if (rolledDmgMin > rolledDmgMax)
            (rolledDmgMin, rolledDmgMax) = (rolledDmgMax, rolledDmgMin);

        return new WeaponInstance(this, rolledDmgMin, rolledDmgMax, rolledPrec, rarityRank, upgradeLevel);
    }

    /// <summary>
    /// Roll la rareté au drop selon la table GDD v21 section 5.3.
    /// r-2(8%) r-1(12%) r0(20.85%) r+1(18%) r+2(15.7%) r+3(11.5%)
    /// r+4(8.5%) r+5(4.1%) r+6(1%) r+7(0.35%)
    /// </summary>
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
// WeaponInstance — données runtime d'une arme droppée
// Path : Assets/Scripts/Data/Inventory/Equipment/WeaponData.cs
// AetherTree GDD v30 — Section 5.1 / 5.3 / 5.4
// =============================================================
[System.Serializable]
public class WeaponInstance
{
    public WeaponData data;

    public float rolledDamageMin;
    public float rolledDamageMax;
    public float rolledPrecision;

    public int rarityRank   = 0;
    public int upgradeLevel = 0;

    public RuneInstance equippedRune = null;

    public WeaponInstance(WeaponData source, float dmgMin, float dmgMax,
                          float precision, int rarity = 0, int upgrade = 0)
    {
        data             = source;
        rolledDamageMin  = dmgMin;
        rolledDamageMax  = dmgMax;
        rolledPrecision  = precision;
        rarityRank       = rarity;
        upgradeLevel     = upgrade;
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

    public float FinalDamageMin => rolledDamageMin * (1f + RarityBonus) * (1f + UpgradeBonus);
    public float FinalDamageMax => rolledDamageMax * (1f + RarityBonus) * (1f + UpgradeBonus);
    public float FinalPrecision => rolledPrecision * (1f + RarityBonus) * (1f + UpgradeBonus);

    public float CritChance  => data.critChance;
    public float CritDamage  => data.critDamage;
    public float AttackSpeed => data.attackSpeed;
    public int   AttackGrade => data.attackGrade;

    // ── Raccourcis SO (4 slots) ───────────────────────────────
    public WeaponType                   WeaponType        => data.weaponType;
    public WeaponCategory               Category          => data.Category;
    public string                       WeaponName        => data?.weaponName ?? "Weapon";
    public Sprite                       Icon              => data?.icon;
    public List<StatBonus>              Bonuses           => data?.bonuses;
    public List<StatusEffectEntry>      StatusEffects     => data?.statusEffects;
    public List<DebuffResistanceEntry>  DebuffResistances => data?.debuffResistances;
    public List<OnHitEffectEntry>       OnHitEffects      => data?.onHitEffects;
    public string                       RarityLabel       => rarityRank >= 0 ? $"r+{rarityRank}" : $"r{rarityRank}";

    // ── Rune ──────────────────────────────────────────────────
    public bool TryInsertRune(RuneInstance rune)
    {
        if (rune == null)
        {
            Debug.LogWarning("[WeaponInstance] TryInsertRune : rune null.");
            return false;
        }
        if (rune.Category != RuneCategory.Weapon)
        {
            Debug.LogWarning($"[WeaponInstance] {rune.RuneName} est une rune Armor — incompatible avec une arme.");
            return false;
        }
        if (!rune.CanInsertInto(rarityRank))
        {
            Debug.LogWarning($"[WeaponInstance] Rune {rune.RarityLabel} trop haute pour cette arme ({RarityLabel}).");
            return false;
        }
        if (equippedRune != null)
            Debug.Log($"[WeaponInstance] Rune {equippedRune.RuneName} écrasée par {rune.RuneName}.");

        equippedRune = rune;
        Debug.Log($"[WeaponInstance] Rune insérée : {rune.Label} dans {WeaponName}.");
        return true;
    }

    public RuneInstance RemoveRune()
    {
        RuneInstance removed = equippedRune;
        equippedRune = null;
        return removed;
    }
}