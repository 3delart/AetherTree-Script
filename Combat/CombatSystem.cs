using UnityEngine;

// =============================================================
// COMBATSYSTEM.CS — Calcul des dégâts
// Path : Assets/Scripts/Systems/CombatSystem.cs
// AetherTree GDD v30 — Section 21
//
// Pipeline deux branches indépendantes (§21.1) :
//   Branche Physique   : base → rareté → upgrade → ×ratio skill → réduction def (brut²/(brut+def×1.5))
//   Branche Élémentaire: pts fixes → +bonus rang fixes → ×mult rang → ×ratio skill → -résistance → ×vulnérabilité
//   Total = physique + élémentaire
//
// Réduction physique (§21.2) : brut² / (brut + def × 1.5)
//   → def = brut/2 : ~40% réduit | def = brut : ~50% | def = brut×2 : ~67%
//
// Miss% (§6.10) : Esquive^6 / (Esquive^6 + Précision^6) × 100
//
// Grade PvP uniquement (§6.3) : bonus = (attackGrade − defenseGrade) × 5%
// =============================================================

public class CombatSystem : MonoBehaviour
{
    public static CombatSystem Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null) { Destroy(this); return; }
        Instance = this;
    }

    /// <summary>
    /// Calcule les dégâts d'une attaque.
    /// </summary>
    public float CalculateDamage(
        WeaponInstance weapon,
        SkillData skill,
        ElementalSystem elemental,
        Player attacker,
        Entity target)
    {
        // 1. Dégâts bruts de l'arme — roll entre FinalDamageMin et FinalDamageMax
        // (valeurs déjà intégrant rareté + upgrade de l'instance droppée)
        float baseDamage = Random.Range(weapon.FinalDamageMin, weapon.FinalDamageMax);
        baseDamage *= skill.damageMultiplier;

        // 2. Partie physique — réduction §21.2 : brut² / (brut + def × 1.5)
        float physDamage = 0f;
        if (target != null)
        {
            float mDef = target.GetMeleeDefense();
            float rDef = target.GetRangedDefense();
            float gDef = target.GetMagicDefense();

            // ArmorBreak — réduit toutes les défenses de la cible (§21bis.1)
            StatusEffectSystem targetStatus = target.GetComponent<StatusEffectSystem>();
            if (targetStatus != null && targetStatus.armorBreakReduction > 0f)
            {
                mDef = Mathf.Max(0f, mDef - targetStatus.armorBreakReduction);
                rDef = Mathf.Max(0f, rDef - targetStatus.armorBreakReduction);
                gDef = Mathf.Max(0f, gDef - targetStatus.armorBreakReduction);
            }

            // Formule GDD §21.2 : brut² / (brut + def × 1.5)
            float mRed = (baseDamage * baseDamage) / (baseDamage + mDef * 1.5f);
            float rRed = (baseDamage * baseDamage) / (baseDamage + rDef * 1.5f);
            float gRed = (baseDamage * baseDamage) / (baseDamage + gDef * 1.5f);

            physDamage = mRed * skill.damageMeleeRatio
                       + rRed * skill.damageRangedRatio
                       + gRed * skill.damageMagicRatio;
        }
        else
        {
            physDamage = baseDamage;
        }

        // TODO : Pénétration d'armure — sera ajouté comme effet de debuff (Phase combat)

        // 3. Partie élémentaire — s'ajoute aux dégâts physiques (ratio indépendant)
        float elemDamage = baseDamage * skill.elementalRatio;

        if (skill.PrimaryElement != ElementType.Neutral && elemental != null && attacker != null)
        {
            // Points élémentaires en flat (avant les %)
            float elemPoints = attacker.GetElementPoints(skill.PrimaryElement);
            float flatBonus  = 1f + (elemPoints * 0.001f); // 1000 pts = +100%

            // Bonus affinité
            float affinityBonus = elemental.GetElementalDamageBonus(skill.PrimaryElement);

            elemDamage *= flatBonus * affinityBonus * skill.elementalMultiplier;
        }
        else
        {
            // Sort neutre : bonus neutre si 100% neutre
            if (elemental != null)
                elemDamage *= elemental.GetNeutralBonus();
        }

        // 4. Vulnérabilité de la cible
        if (target != null)
        {
            Player targetPlayer = target.GetComponent<Player>();
            if (targetPlayer != null)
            {
                ElementalSystem targetElemental = targetPlayer.GetComponent<ElementalSystem>();
                if (targetElemental != null)
                    elemDamage *= targetElemental.GetVulnerability(skill.PrimaryElement);
            }
        }

        // 5. Résistance élémentaire de la cible (Mob)
        if (target != null)
        {
            Mob mob = target.GetComponent<Mob>();
            if (mob?.data != null)
            {
                float resistance = mob.data.GetElementalResistance(skill.PrimaryElement);
                elemDamage *= (1f - resistance);
            }
        }

        float totalDamage = physDamage + elemDamage;

        // 6. Critique — CritChance [0..1] | CritDamage [0..1+] (ex: 1.5 = ×1.5)
        if (Random.value < weapon.CritChance)
            totalDamage *= weapon.CritDamage;

        // 7. Mark — bonus dégâts sur cible marquée
        if (target?.statusEffects != null && target.statusEffects.isMarked)
            totalDamage *= (1f + target.statusEffects.GetMarkDamageBonus());

        // 8. AttackUp buff de l'attaquant
        if (attacker?.statusEffects != null)
            totalDamage += attacker.statusEffects.GetBuffAttackBonus();

        return Mathf.Max(1f, totalDamage);
    }

    /// <summary>
    /// Calcule les dégâts d'un skill de mob.
    /// Basé sur attackDamage du mob × damageMultiplier.
    /// </summary>
    public float CalculateMobDamage(SkillData skill, Mob caster, Entity target)
    {
        float baseDamage = caster.data != null ? caster.data.attackDamage : 10f;
        baseDamage *= skill.damageMultiplier * Random.Range(0.9f, 1.1f);

        // Défense pondérée — formule §21.2 : brut² / (brut + def × 1.5)
        float reduction = 1f;
        if (target != null)
        {
            float mDef = target.GetMeleeDefense();
            float rDef = target.GetRangedDefense();
            float gDef = target.GetMagicDefense();

            // ArmorBreak — réduit toutes les défenses de la cible (§21bis.1)
            StatusEffectSystem targetStatus = target.GetComponent<StatusEffectSystem>();
            if (targetStatus != null && targetStatus.armorBreakReduction > 0f)
            {
                mDef = Mathf.Max(0f, mDef - targetStatus.armorBreakReduction);
                rDef = Mathf.Max(0f, rDef - targetStatus.armorBreakReduction);
                gDef = Mathf.Max(0f, gDef - targetStatus.armorBreakReduction);
            }

            float mRed = (baseDamage * baseDamage) / (baseDamage + mDef * 1.5f);
            float rRed = (baseDamage * baseDamage) / (baseDamage + rDef * 1.5f);
            float gRed = (baseDamage * baseDamage) / (baseDamage + gDef * 1.5f);

            reduction = mRed * skill.damageMeleeRatio
                      + rRed * skill.damageRangedRatio
                      + gRed * skill.damageMagicRatio;

            // Normalise la réduction en ratio [0..1] par rapport aux dégâts bruts
            if (baseDamage > 0f)
                reduction /= baseDamage;
        }

        // Résistance élémentaire
        float elemResist = 0f;
        if (target != null)
        {
            Mob targetMob = target.GetComponent<Mob>();
            if (targetMob?.data != null)
                elemResist = targetMob.data.GetElementalResistance(skill.PrimaryElement);
        }

        float total = baseDamage * reduction * (1f - elemResist);

        // Mark — bonus dégâts sur cible marquée
        if (target?.statusEffects != null && target.statusEffects.isMarked)
            total *= (1f + target.statusEffects.GetMarkDamageBonus());

        return Mathf.Max(1f, total);
    }

    /// <summary>
    /// Calcule les dégâts d'une attaque de PNJ (Guard, ou tout PNJ combattant).
    /// Même pipeline que CalculateMobDamage — source = guardAttackDamage du PNJData.
    /// Miss/Dodge vérifié en amont dans HandleGuardAI.
    /// </summary>
    public float CalculatePNJDamage(PNJ guard, Entity target)
    {
        if (guard?.data == null) return 1f;

        float baseDamage = guard.data.guardAttackDamage * Random.Range(0.9f, 1.1f);

        if (target == null) return Mathf.Max(1f, baseDamage);

        // Défense pondérée — formule §21.2 : brut² / (brut + def × 1.5)
        float mDef = target.GetMeleeDefense();
        float rDef = target.GetRangedDefense();
        float gDef = target.GetMagicDefense();

        // ArmorBreak
        StatusEffectSystem targetStatus = target.GetComponent<StatusEffectSystem>();
        if (targetStatus != null && targetStatus.armorBreakReduction > 0f)
        {
            mDef = Mathf.Max(0f, mDef - targetStatus.armorBreakReduction);
            rDef = Mathf.Max(0f, rDef - targetStatus.armorBreakReduction);
            gDef = Mathf.Max(0f, gDef - targetStatus.armorBreakReduction);
        }

        // Garde = attaque physique mêlée pure
        float def    = mDef;
        float dmg    = (baseDamage * baseDamage) / (baseDamage + def * 1.5f);

        return Mathf.Max(1f, dmg);
    }

    /// <summary>
    /// Roll de dodge selon la formule GDD §6.10 :
    /// Miss% = Esquive^6 / (Esquive^6 + Précision^6) × 100
    /// </summary>
    public bool RollDodge(float dodge, float precision)
    {
        if (dodge <= 0f) return false;
        float d6 = Mathf.Pow(dodge,     6f);
        float p6 = Mathf.Pow(precision, 6f);
        float missChance = d6 / (d6 + p6);
        return Random.value < missChance;
    }

    /// <summary>Roll de toucher — tient compte de Blind sur l'attaquant.</summary>
    public static bool RollHit(float precision, Entity attacker)
    {
        float effectivePrecision = precision;

        // Blind — réduit la précision de l'attaquant
        if (attacker?.statusEffects != null && attacker.statusEffects.isBlinded)
            effectivePrecision *= (1f - attacker.statusEffects.GetBlindMalus());

        return Random.value <= effectivePrecision / 100f;
    }
}