using System.Collections.Generic;
using UnityEngine;

// =============================================================
// PlayerStats — Stats agrégées du joueur
// Path : Assets/Scripts/Core/PlayerStats.cs
// AetherTree GDD v30 — §5.2 (Stats de combat), §5.3 (Équipement), §6 (Équipement détail)
//
// Source unique de vérité pour toutes les stats de combat du joueur.
// Recalculé intégralement à chaque changement d'équipement via
// RecalculateStats(). Les buffs/debuffs temporaires sont appliqués
// à la volée dans CombatSystem sans modifier ces valeurs.
//
// Flow :
//   EquipItem() → player.stats.RecalculateStats(player) → CombatSystem lit player.stats
//   CombatSystem → crée un StatSnapshot(player.stats) + applique buffs → calcule dégâts
//
// Défense pondérée (GDD v30 §6.1) :
//   CombatSystem étape ⑤ appelle GetWeightedDefense(skill)
//   → meleeDefense×skill.meleeRatio + rangedDefense×skill.rangedRatio + magicDefense×skill.magicRatio
//   Pour les BasicAttack (pas de SkillData) → GetDefenseForCategory(WeaponCategory)
//
// Slots contributeurs (GDD v30 section 5.1) :
//   Arme    → baseAttackMin/Max, attackGrade, precision, critChance, critDamage
//   Armure  → meleeDefense, rangedDefense, magicDefense, defenseGrade, dodge
//   Casque  → toutes stats via List<StatBonus>
//   Gants   → défenses fixes SO + résistances instance + List<StatBonus>
//   Bottes  → défenses fixes SO + résistances instance + List<StatBonus>
//   Bijoux  → défenses fixes SO + List<StatBonus>
//   Runes   → slots sur arme + armure via List<StatBonus>
//   Esprits → points élémentaires cumulés + paliers via List<StatBonus>
// =============================================================

public class PlayerStats
{
    // =========================================================
    // ATTAQUE
    // =========================================================

    /// <summary>
    /// Dégâts min après rareté + upgrade, avant skill multiplier.
    /// GDD v30 section 8.1 & 5.2–5.4.
    /// </summary>
    public float baseAttackMin { get; private set; }

    /// <summary>Dégâts max — même sources que baseAttackMin.</summary>
    public float baseAttackMax { get; private set; }

    /// <summary>
    /// Grade d'attaque (1–10) — fixe, défini dans WeaponData.
    /// CombatSystem étape ③ : bonus = (attackGrade − defenseGrade) × 5%.
    /// GDD v30 section 5.2.
    /// </summary>
    public int attackGrade { get; private set; }

    /// <summary>
    /// Précision agrégée : arme + casque/gants/bijoux/runes.
    /// Miss% = dodge^6 / (dodge^6 + precision^6) × 100.
    /// Mage : fixée à 999 dans CombatSystem — GDD v30 section 6.2.
    /// </summary>
    public float precision { get; private set; }

    /// <summary>Chance de critique [0..1] — fixe sur l'arme + bonus équipement.</summary>
    public float critChance { get; private set; }

    /// <summary>Multiplicateur critique — fixe sur l'arme + bonus équipement.</summary>
    public float critDamage { get; private set; }

    // =========================================================
    // DÉFENSE — 3 types distincts
    // =========================================================

    /// <summary>
    /// Défense contre les attaques de mêlée.
    /// Armure corps (rollé + rareté/upgrade) + bonus tous équipements.
    /// GDD v30 section 6.1 & 8.2.
    /// </summary>
    public float meleeDefense { get; private set; }

    /// <summary>Défense contre les attaques à distance.</summary>
    public float rangedDefense { get; private set; }

    /// <summary>Défense contre les attaques magiques.</summary>
    public float magicDefense { get; private set; }

    /// <summary>
    /// Grade de défense (1–10) — fixe, défini dans ArmorData.
    /// CombatSystem étape ③ côté défenseur. GDD v30 section 5.2.
    /// </summary>
    public int defenseGrade { get; private set; }

    /// <summary>
    /// Esquive totale : armure rollée (fixe) + bonus casque/bottes/bijoux.
    /// Miss% = dodge^6 / (dodge^6 + precision^6) × 100.
    /// GDD v30 section 7.
    /// </summary>
    public float dodge { get; private set; }

    // =========================================================
    // RÉSISTANCES ÉLÉMENTAIRES
    // =========================================================

    /// <summary>
    /// Résistances élémentaires [0..1] par élément.
    /// Agrège : gants/bottes (instance), casque/bijoux/runes (StatBonus).
    /// CombatSystem étape ⑥. GDD v30 section 6.1.
    /// </summary>
    public Dictionary<ElementType, float> elementalResistances { get; private set; }
        = new Dictionary<ElementType, float>();

    // =========================================================
    // POINTS ÉLÉMENTAIRES
    // =========================================================

    /// <summary>
    /// Points élémentaires d'équipement par élément.
    /// Agrège : esprits (cumulés au niveau) + paliers + casque/bijoux/runes (StatBonus).
    /// S'ajoute aux points d'affinité de ElementalSystem.
    /// CombatSystem étape ⑥. GDD v30 section 6.1.
    /// </summary>
    public Dictionary<ElementType, float> elementalPoints { get; private set; }
        = new Dictionary<ElementType, float>();

    // =========================================================
    // BONUS ENTITÉ — HP, Mana, Regen, MoveSpeed
    // Agrégés ici, appliqués sur Entity dans RecalculateStats()
    // =========================================================

    public float bonusHP        { get; private set; }
    public float bonusMana      { get; private set; }
    public float bonusRegenHP   { get; private set; }
    public float bonusRegenMana { get; private set; }
    public float bonusMoveSpeed { get; private set; }

    /// <summary>
    /// Multiplicateurs sur les points élémentaires [0..1+].
    /// Appliqués après tous les points flat.
    /// Source : paliers d'esprits, runes, bijoux spéciaux.
    /// </summary>
    public Dictionary<ElementType, float> elementalPointsMultipliers { get; private set; }
        = new Dictionary<ElementType, float>();

    // =========================================================
    // CONSTRUCTEUR
    // =========================================================

    public PlayerStats()
    {
        foreach (ElementType e in System.Enum.GetValues(typeof(ElementType)))
        {
            elementalResistances[e]       = 0f;
            elementalPoints[e]            = 0f;
            elementalPointsMultipliers[e] = 0f;
        }
    }

    // =========================================================
    // RECALCUL COMPLET
    // Appelé à chaque changement d'équipement — repart de zéro.
    // =========================================================

    public void RecalculateStats(Player player)
    {
        Reset();
        if (player == null) return;

        ApplyWeapon(player.equippedWeaponInstance, player);
        ApplyArmor(player.equippedArmorInstance);
        ApplyHelmet(player.equippedHelmetInstance);
        ApplyGloves(player.equippedGlovesInstance);
        ApplyBoots(player.equippedBootsInstance);
        ApplyJewelry(player.equippedJewelryInstances);
        ApplyRunes(player.equippedWeaponInstance, player.equippedArmorInstance);
        ApplySpirits(player.equippedSpiritInstances);

        // Applique les multiplicateurs sur les points élémentaires
        foreach (ElementType e in System.Enum.GetValues(typeof(ElementType)))
            if (elementalPointsMultipliers[e] > 0f)
                elementalPoints[e] *= (1f + elementalPointsMultipliers[e]);

        // Applique HP/Mana/Regen/MoveSpeed sur Entity depuis CharacterData + niveau
        float baseHP        = player.characterData != null ? player.characterData.baseMaxHP     : 100f;
        float baseMana      = player.characterData != null ? player.characterData.baseMaxMana   : 50f;
        float baseRegenHP   = player.characterData != null ? player.characterData.baseRegenHP   : 1f;
        float baseRegenMana = player.characterData != null ? player.characterData.baseRegenMana : 2f;
        float baseMoveSpeed = player.characterData != null ? player.characterData.baseMoveSpeed : 5f;

        int lvl = player.level - 1;
        if (player.characterData != null && lvl > 0)
        {
            baseHP        += player.characterData.hpPerLevel        * lvl;
            baseMana      += player.characterData.manaPerLevel      * lvl;
            baseRegenHP   += player.characterData.regenHPPerLevel   * lvl;
            baseRegenMana += player.characterData.regenManaPerLevel * lvl;
        }

        player.SetMaxHP    (baseHP        + bonusHP);
        player.SetMaxMana  (baseMana      + bonusMana);
        player.SetRegenHP  (baseRegenHP   + bonusRegenHP);
        player.SetRegenMana(baseRegenMana + bonusRegenMana);
        player.SetMoveSpeed(baseMoveSpeed + bonusMoveSpeed);

        ApplyDebuffResistances(player);
    }

    // =========================================================
    // RÉSISTANCES AUX DEBUFFS
    // =========================================================

    private void ApplyDebuffResistances(Player player)
    {
        if (player?.statusEffects == null) return;

        player.statusEffects.ResetDebuffResistances();

        var allResistances = new System.Collections.Generic.List<System.Collections.Generic.List<DebuffResistanceEntry>>();

        if (player.equippedWeaponInstance?.data != null && player.equippedWeaponInstance.DebuffResistances != null)
            allResistances.Add(player.equippedWeaponInstance.DebuffResistances);
        if (player.equippedArmorInstance?.data != null && player.equippedArmorInstance.DebuffResistances != null)
            allResistances.Add(player.equippedArmorInstance.DebuffResistances);
        if (player.equippedHelmetInstance?.data != null && player.equippedHelmetInstance.DebuffResistances != null)
            allResistances.Add(player.equippedHelmetInstance.DebuffResistances);
        if (player.equippedGlovesInstance?.data != null && player.equippedGlovesInstance.DebuffResistances != null)
            allResistances.Add(player.equippedGlovesInstance.DebuffResistances);
        if (player.equippedBootsInstance?.data != null && player.equippedBootsInstance.DebuffResistances != null)
            allResistances.Add(player.equippedBootsInstance.DebuffResistances);
        if (player.equippedJewelryInstances != null)
            foreach (var jewelry in player.equippedJewelryInstances)
                if (jewelry?.DebuffResistances != null)
                    allResistances.Add(jewelry.DebuffResistances);

        var accumulated = new System.Collections.Generic.Dictionary<DebuffType, float>();
        foreach (var list in allResistances)
        {
            if (list == null) continue;
            foreach (var entry in list)
            {
                if (!accumulated.ContainsKey(entry.debuffType))
                    accumulated[entry.debuffType] = 0f;
                accumulated[entry.debuffType] += entry.resistChance;
            }
        }

        foreach (var kvp in accumulated)
        {
            player.statusEffects.SetDebuffResistance(kvp.Key, Mathf.Clamp01(kvp.Value));
            Debug.Log($"[PlayerStats] Résistance {kvp.Key} : {Mathf.Clamp01(kvp.Value):P0}");
        }
    }

    // =========================================================
    // RESET
    // =========================================================

    private void Reset()
    {
        baseAttackMin  = 0f;
        baseAttackMax  = 0f;
        attackGrade    = 1;
        precision      = 0f;
        critChance     = 0f;
        critDamage     = 1f;
        meleeDefense   = 0f;
        rangedDefense  = 0f;
        magicDefense   = 0f;
        defenseGrade   = 1;
        dodge          = 0f;
        bonusHP        = 0f;
        bonusMana      = 0f;
        bonusRegenHP   = 0f;
        bonusRegenMana = 0f;
        bonusMoveSpeed = 0f;

        foreach (ElementType e in System.Enum.GetValues(typeof(ElementType)))
        {
            elementalResistances[e]       = 0f;
            elementalPoints[e]            = 0f;
            elementalPointsMultipliers[e] = 0f;
        }
    }

    // =========================================================
    // ARME — GDD v30 section 5.1
    // =========================================================

    private void ApplyWeapon(WeaponInstance weapon, Player player = null)
    {
        if (weapon == null)
        {
            // Sans arme : Coup de poing — scale avec le niveau du joueur
            // Formule : dmgMin = 2 + level * 0.5 | dmgMax = 4 + level * 1.0
            // Ex: Lv1 → 2-5 | Lv10 → 7-14 | Lv50 → 27-54 | Lv100 → 52-104
            // attackGrade plafonné à 6 — mains nues restent moins efficaces qu'une vraie arme
            float lvl     = player != null ? player.level : 1f;
            baseAttackMin = 2f + lvl * 0.5f;
            baseAttackMax = 4f + lvl * 1.0f;
            precision     = 85f;
            critChance    = 0.03f;
            critDamage    = 1.5f;
            attackGrade   = Mathf.Clamp(1 + (int)(lvl / 20f), 1, 6);
            return;
        }

        baseAttackMin = weapon.FinalDamageMin;
        baseAttackMax = weapon.FinalDamageMax;
        precision     = weapon.FinalPrecision;
        critChance    = weapon.CritChance;
        critDamage    = weapon.CritDamage;
        attackGrade   = weapon.AttackGrade;

        ApplyStatBonusList(weapon.Bonuses);
    }

    // =========================================================
    // ARMURE CORPS — GDD v30 section 5.1
    // =========================================================

    private void ApplyArmor(ArmorInstance armor)
    {
        if (armor == null || armor.data == null) return;

        meleeDefense  += armor.FinalMeleeDefense;
        rangedDefense += armor.FinalRangedDefense;
        magicDefense  += armor.FinalMagicDefense;
        dodge         += armor.FinalDodge;
        defenseGrade   = armor.DefenseGrade;

        ApplyStatBonusList(armor.Bonuses);
    }

    // =========================================================
    // CASQUE — GDD v30 section 5.1
    // Toutes les stats via StatBonus — pas de grade/upgrade/rune
    // =========================================================

    private void ApplyHelmet(HelmetInstance helmet)
    {
        if (helmet == null) return;
        ApplyStatBonusList(helmet.Bonuses);
    }

    // =========================================================
    // GANTS — GDD v30 section 5.1
    // Défenses fixes SO + résistances instance + StatBonus
    // =========================================================

    private void ApplyGloves(GlovesInstance gloves)
    {
        if (gloves == null) return;

        meleeDefense  += gloves.MeleeDefense;
        rangedDefense += gloves.RangedDefense;
        magicDefense  += gloves.MagicDefense;

        foreach (ElementType e in System.Enum.GetValues(typeof(ElementType)))
        {
            float resist = gloves.GetResistance(e);
            if (resist > 0f)
                elementalResistances[e] = Mathf.Clamp01(elementalResistances[e] + resist);
        }

        ApplyStatBonusList(gloves.Bonuses);
    }

    // =========================================================
    // BOTTES — GDD v30 section 5.1
    // Défenses fixes SO + résistances instance + StatBonus
    // =========================================================

    private void ApplyBoots(BootsInstance boots)
    {
        if (boots == null) return;

        meleeDefense  += boots.MeleeDefense;
        rangedDefense += boots.RangedDefense;
        magicDefense  += boots.MagicDefense;

        foreach (ElementType e in System.Enum.GetValues(typeof(ElementType)))
        {
            float resist = boots.GetResistance(e);
            if (resist > 0f)
                elementalResistances[e] = Mathf.Clamp01(elementalResistances[e] + resist);
        }

        ApplyStatBonusList(boots.Bonuses);
    }

    // =========================================================
    // BIJOUX — GDD v30 section 5.1
    // Défenses fixes SO + StatBonus
    // =========================================================

    private void ApplyJewelry(List<JewelryInstance> jewelryList)
    {
        if (jewelryList == null) return;

        foreach (JewelryInstance jewelry in jewelryList)
        {
            if (jewelry == null) continue;

            meleeDefense  += jewelry.MeleeDefense;
            rangedDefense += jewelry.RangedDefense;
            magicDefense  += jewelry.MagicDefense;

            ApplyStatBonusList(jewelry.Bonuses);
        }
    }

    // =========================================================
    // RUNES — GDD v30 section 5.1
    // Slot sur arme + slot sur armure corps
    // =========================================================

    private void ApplyRunes(WeaponInstance weapon, ArmorInstance armor)
    {
        if (weapon?.equippedRune != null) ApplyStatBonusList(weapon.equippedRune.bonuses);
        if (armor?.equippedRune  != null) ApplyStatBonusList(armor.equippedRune.bonuses);
    }

    // =========================================================
    // ESPRITS — GDD v30 section 5.1
    // Points élémentaires cumulés + paliers débloqués
    // =========================================================

    private void ApplySpirits(List<SpiritInstance> spirits)
    {
        if (spirits == null) return;

        foreach (SpiritInstance spirit in spirits)
        {
            if (spirit?.data == null) continue;

            elementalPoints[spirit.Element] += spirit.TotalElementalPoints;

            for (int lvl = 1; lvl <= spirit.level; lvl++)
            {
                SpiritMilestone milestone = spirit.data.GetMilestone(lvl);
                if (milestone != null)
                    ApplyStatBonusList(milestone.bonuses);
            }
        }
    }

    // =========================================================
    // DISPATCH StatBonus → stats
    // =========================================================

    private void ApplyStatBonusList(List<StatBonus> bonuses)
    {
        if (bonuses == null) return;
        foreach (StatBonus b in bonuses)
            ApplyStatBonus(b);
    }

    private void ApplyStatBonus(StatBonus b)
    {
        switch (b.statType)
        {
            case StatType.BonusAttack:
                baseAttackMin += b.value;
                baseAttackMax += b.value;
                break;
            case StatType.CritChance:    critChance    += b.value; break;
            case StatType.CritDamage:    critDamage    += b.value; break;
            case StatType.Precision:     precision     += b.value; break;

            case StatType.MeleeDefense:  meleeDefense  += b.value; break;
            case StatType.RangedDefense: rangedDefense += b.value; break;
            case StatType.MagicDefense:  magicDefense  += b.value; break;
            case StatType.Dodge:         dodge         += b.value; break;

            case StatType.BonusHP:        bonusHP        += b.value; break;
            case StatType.BonusMana:      bonusMana      += b.value; break;
            case StatType.BonusRegenHP:   bonusRegenHP   += b.value; break;
            case StatType.BonusRegenMana: bonusRegenMana += b.value; break;

            case StatType.MoveSpeed: bonusMoveSpeed += b.value; break;

            case StatType.ResistFire:
                elementalResistances[ElementType.Fire]      = Mathf.Clamp01(elementalResistances[ElementType.Fire]      + b.value); break;
            case StatType.ResistWater:
                elementalResistances[ElementType.Water]     = Mathf.Clamp01(elementalResistances[ElementType.Water]     + b.value); break;
            case StatType.ResistEarth:
                elementalResistances[ElementType.Earth]     = Mathf.Clamp01(elementalResistances[ElementType.Earth]     + b.value); break;
            case StatType.ResistNature:
                elementalResistances[ElementType.Nature]    = Mathf.Clamp01(elementalResistances[ElementType.Nature]    + b.value); break;
            case StatType.ResistLightning:
                elementalResistances[ElementType.Lightning] = Mathf.Clamp01(elementalResistances[ElementType.Lightning] + b.value); break;
            case StatType.ResistDarkness:
                elementalResistances[ElementType.Darkness]  = Mathf.Clamp01(elementalResistances[ElementType.Darkness]  + b.value); break;
            case StatType.ResistLight:
                elementalResistances[ElementType.Light]     = Mathf.Clamp01(elementalResistances[ElementType.Light]     + b.value); break;
            case StatType.ResistAll:
                foreach (ElementType e in System.Enum.GetValues(typeof(ElementType)))
                    elementalResistances[e] = Mathf.Clamp01(elementalResistances[e] + b.value);
                break;

            case StatType.PointsFire:       elementalPoints[ElementType.Fire]      += b.value; break;
            case StatType.PointsWater:      elementalPoints[ElementType.Water]     += b.value; break;
            case StatType.PointsEarth:      elementalPoints[ElementType.Earth]     += b.value; break;
            case StatType.PointsNature:     elementalPoints[ElementType.Nature]    += b.value; break;
            case StatType.PointsLightning:  elementalPoints[ElementType.Lightning] += b.value; break;
            case StatType.PointsDarkness:   elementalPoints[ElementType.Darkness]  += b.value; break;
            case StatType.PointsLight:      elementalPoints[ElementType.Light]     += b.value; break;
            case StatType.PointsAll:
                foreach (ElementType e in System.Enum.GetValues(typeof(ElementType)))
                    elementalPoints[e] += b.value;
                break;

            case StatType.ElementBonusFire:       elementalPointsMultipliers[ElementType.Fire]      += b.value; break;
            case StatType.ElementBonusWater:      elementalPointsMultipliers[ElementType.Water]     += b.value; break;
            case StatType.ElementBonusEarth:      elementalPointsMultipliers[ElementType.Earth]     += b.value; break;
            case StatType.ElementBonusNature:     elementalPointsMultipliers[ElementType.Nature]    += b.value; break;
            case StatType.ElementBonusLightning:  elementalPointsMultipliers[ElementType.Lightning] += b.value; break;
            case StatType.ElementBonusDarkness:   elementalPointsMultipliers[ElementType.Darkness]  += b.value; break;
            case StatType.ElementBonusLight:      elementalPointsMultipliers[ElementType.Light]     += b.value; break;
            case StatType.ElementBonusAll:
                foreach (ElementType e in System.Enum.GetValues(typeof(ElementType)))
                    elementalPointsMultipliers[e] += b.value;
                break;
        }
    }

    // =========================================================
    // ACCESSEURS UTILITAIRES
    // =========================================================

    /// <summary>Dégâts de base rollés aléatoirement entre min et max.</summary>
    public float RollBaseAttack() => Random.Range(baseAttackMin, baseAttackMax);

    /// <summary>True si le coup est un critique selon critChance.</summary>
    public bool RollCrit() => Random.value < critChance;

    /// <summary>Résistance élémentaire pour un élément donné [0..1].</summary>
    public float GetResistance(ElementType element)
        => elementalResistances.TryGetValue(element, out float v) ? v : 0f;

    /// <summary>Points élémentaires d'équipement pour un élément donné.</summary>
    public float GetElementalPoints(ElementType element)
        => elementalPoints.TryGetValue(element, out float v) ? v : 0f;

    /// <summary>
    /// Défense pondérée selon les ratios DamageType du skill.
    /// GDD v30 §6.1 — CombatSystem étape ⑤.
    /// Ex: skill (melee=0.7, magic=0.3) → meleeDefense×0.7 + magicDefense×0.3
    /// </summary>
    public float GetWeightedDefense(SkillData skill)
    {
        if (skill == null) return meleeDefense;
        return skill.GetWeightedDefense(meleeDefense, rangedDefense, magicDefense);
    }

    /// <summary>
    /// Défense pure par catégorie d'arme — utilisé pour les BasicAttack (pas de SkillData).
    /// GDD v30 §6.1.
    /// </summary>
    public float GetDefenseForCategory(WeaponCategory attackerCategory)
    {
        switch (attackerCategory)
        {
            case WeaponCategory.Ranged: return rangedDefense;
            case WeaponCategory.Magic:  return magicDefense;
            default:                    return meleeDefense; // Melee + Unarmed
        }
    }
}
