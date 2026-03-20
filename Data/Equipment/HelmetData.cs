using UnityEngine;
using System.Collections.Generic;

// =============================================================
// HelmetData — ScriptableObject template de casque
// Path : Assets/Scripts/Data/Inventory/Equipment/HelmetData.cs
// AetherTree GDD v30 — Section 5.1
//
// Règles GDD :
//   - Pas de grade, pas d'upgrade, pas de rune (section 5.1)
//   - Pas de restriction ArmorType — tout joueur peut équiper
//   - Stats toutes fixes (pas de roll)
//   - Toutes les stats passent par List<StatBonus>
//
// 4 slots de configuration (uniformes sur tous les équipements) :
//   bonuses           → StatBonus (défense, crit, résistances, HP...)
//   statusEffects     → StatusEffectEntry (debuffs/buffs à l'attaque)
//   debuffResistances → DebuffResistanceEntry (résistance aux debuffs)
//   onHitEffects      → OnHitEffectEntry (effets quand on reçoit un coup)
//
// Assets > Create > AetherTree > Equipment > HelmetData
// =============================================================

[CreateAssetMenu(fileName = "NewHelmet", menuName = "AetherTree/Equipment/HelmetData")]
public class HelmetData : ScriptableObject
{
    [Header("Identité")]
    public string     helmetName = "Helmet";
    public Sprite     icon;
    public GameObject helmetPrefab;

    [Header("Niveau")]
    [Tooltip("Niveau minimum requis pour équiper cette arme.")]
    [Min(1)] public int requiredLevel = 1;

    // ── 4 slots de configuration ──────────────────────────────

    [Header("① Bonus fixes (stats passives)")]
    [Tooltip("Bonus supplémentaires de ce casque.\n" +
             "Ex: MeleeDefense 30 | CritChance 0.05 | BonusHP 150\n" +
             "Ces bonus sont fixes — identiques sur toutes les instances.")]
    public List<StatBonus> bonuses = new List<StatBonus>();

    [Header("② Effets de statut (appliqués à chaque attaque)")]
    [Tooltip("Effets appliqués lors d'une attaque selon leur probabilité.\n" +
             "Glisse un DebuffData ou BuffData + règle la chance.")]
    public List<StatusEffectEntry> statusEffects = new List<StatusEffectEntry>();

    [Header("③ Résistances aux debuffs")]
    [Tooltip("Chances de résister à un debuff spécifique.\n" +
             "Ex: Stun 0.10 = 10% de chance de résister à l'étourdissement.")]
    public List<DebuffResistanceEntry> debuffResistances = new List<DebuffResistanceEntry>();

    [Header("④ Effets On-Hit (déclenchés quand on reçoit un coup)")]
    [Tooltip("Effets déclenchés quand le porteur reçoit un coup.\n" +
             "Ex: CounterFreeze 10% | HealOnHit 1% MaxHP\n" +
             "Glisse un OnHitEffectData + ajuste la chance si besoin.")]
    public List<OnHitEffectEntry> onHitEffects = new List<OnHitEffectEntry>();

    [Header("Description")]
    [TextArea]
    public string       description  = "";


    public HelmetInstance CreateInstance() => new HelmetInstance(this);
}

// =============================================================
// HelmetInstance — wrapper runtime d'un casque équipé
// =============================================================
[System.Serializable]
public class HelmetInstance
{
    public HelmetData data;

    public HelmetInstance(HelmetData source) { data = source; }

    // ── Raccourcis SO (4 slots) ───────────────────────────────
    public string                       HelmetName        => data?.helmetName ?? "Helmet";
    public Sprite                       Icon              => data?.icon;
    public List<StatBonus>              Bonuses           => data?.bonuses;
    public List<StatusEffectEntry>      StatusEffects     => data?.statusEffects;
    public List<DebuffResistanceEntry>  DebuffResistances => data?.debuffResistances;
    public List<OnHitEffectEntry>       OnHitEffects      => data?.onHitEffects;
}