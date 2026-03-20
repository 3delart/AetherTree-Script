using UnityEngine;
using System.Collections.Generic;

// =============================================================
// GlovesData — ScriptableObject template de gants
// Path : Assets/Scripts/Data/Inventory/Equipment/GlovesData.cs
// AetherTree GDD v30 — Section 5.1
//
// Règles GDD :
//   - Pas de grade, pas d'upgrade, pas de rune
//   - Fusion N'importe quels gants → nouveaux gants (via PNJ)
//   - Stats fixes sur le SO : défenses physique, distance, magique
//   - Résistances élémentaires : sur l'instance (additionnées à la fusion)
//   - Palier de fusion S1→S6 stocké sur l'instance
//
// 4 slots de configuration (uniformes sur tous les équipements) :
//   bonuses           → StatBonus (crit, précision, BonusHP...)
//   statusEffects     → StatusEffectEntry (debuffs/buffs à l'attaque)
//   debuffResistances → DebuffResistanceEntry (résistance aux debuffs)
//   onHitEffects      → OnHitEffectEntry (effets quand on reçoit un coup)
//
// Assets > Create > AetherTree > Equipment > GlovesData
// =============================================================

[CreateAssetMenu(fileName = "NewGloves", menuName = "AetherTree/Equipment/GlovesData")]
public class GlovesData : ScriptableObject
{
    [Header("Identité")]
    public string     glovesName = "Gloves";
    public Sprite     icon;
    public GameObject glovesPrefab;

    [Header("Niveau")]
    [Tooltip("Niveau minimum requis pour équiper cette arme.")]
    [Min(1)] public int requiredLevel = 1;

    [Header("Défenses (fixes — identiques sur toutes les instances)")]
    [Tooltip("Défense contre les attaques de mêlée.")]
    public float meleeDefense  = 0f;

    [Tooltip("Défense contre les attaques à distance.")]
    public float rangedDefense = 0f;

    [Tooltip("Défense contre les attaques magiques.")]
    public float magicDefense  = 0f;

    [Header("Résistances élémentaires de base (ratio 0.01 = 1%)")]
    [Tooltip("Résistances de départ de ces gants. Ex: 0.05 = 5%. Additionnées à la fusion.")]
    public float baseResistFire      = 0f;
    public float baseResistWater     = 0f;
    public float baseResistLightning = 0f;
    public float baseResistEarth     = 0f;
    public float baseResistNature    = 0f;
    public float baseResistDarkness  = 0f;
    public float baseResistLight     = 0f;

    // ── 4 slots de configuration ──────────────────────────────

    [Header("① Bonus secondaires (stats passives)")]
    [Tooltip("Bonus supplémentaires de ces gants.\n" +
             "Ex: Precision 20 | CritChance 0.05 | BonusHP 100\n" +
             "Ces bonus sont fixes — non affectés par la fusion.")]
    public List<StatBonus> bonuses = new List<StatBonus>();

    [Header("② Effets de statut (appliqués à chaque attaque)")]
    [Tooltip("Effets appliqués lors d'une attaque selon leur probabilité.\n" +
             "Glisse un DebuffData ou BuffData + règle la chance.")]
    public List<StatusEffectEntry> statusEffects = new List<StatusEffectEntry>();

    [Header("③ Résistances aux debuffs")]
    [Tooltip("Chances de résister à un debuff spécifique.\n" +
             "Ex: Blind 0.10 = 10% de chance de résister à l'aveuglement.")]
    public List<DebuffResistanceEntry> debuffResistances = new List<DebuffResistanceEntry>();

    [Header("④ Effets On-Hit (déclenchés quand on reçoit un coup)")]
    [Tooltip("Effets déclenchés quand le porteur reçoit un coup.\n" +
             "Ex: Thorns 5 dmg | CounterPoison 8%\n" +
             "Glisse un OnHitEffectData + ajuste la chance si besoin.")]
    public List<OnHitEffectEntry> onHitEffects = new List<OnHitEffectEntry>();

    [Header("Description")]
    [TextArea]
    public string       description  = "";


    // ── Utilitaires ───────────────────────────────────────────

    public GlovesInstance CreateInstance() => new GlovesInstance(this)
    {
        resistFire      = baseResistFire,
        resistWater     = baseResistWater,
        resistLightning = baseResistLightning,
        resistEarth     = baseResistEarth,
        resistNature    = baseResistNature,
        resistDarkness  = baseResistDarkness,
        resistLight     = baseResistLight,
    };
}

// =============================================================
// GlovesInstance — wrapper runtime d'une paire de gants équipée
// =============================================================
[System.Serializable]
public class GlovesInstance
{
    public GlovesData data;

    [Tooltip("Palier de fusion : 0 = S0 (base) … 6 = S6 (max)")]
    public int fusionLevel = 0;

    [Tooltip("Résistances élémentaires accumulées via la fusion.")]
    public float resistFire      = 0f;
    public float resistWater     = 0f;
    public float resistLightning = 0f;
    public float resistEarth     = 0f;
    public float resistNature    = 0f;
    public float resistDarkness  = 0f;
    public float resistLight     = 0f;

    public GlovesInstance(GlovesData source) { data = source; }

    // ── Défenses (lues sur le SO) ─────────────────────────────
    public float MeleeDefense  => data?.meleeDefense  ?? 0f;
    public float RangedDefense => data?.rangedDefense ?? 0f;
    public float MagicDefense  => data?.magicDefense  ?? 0f;

    // ── Raccourcis SO (4 slots) ───────────────────────────────
    public string                       GlovesName        => data?.glovesName ?? "Gloves";
    public Sprite                       Icon              => data?.icon;
    public List<StatBonus>              Bonuses           => data?.bonuses;
    public List<StatusEffectEntry>      StatusEffects     => data?.statusEffects;
    public List<DebuffResistanceEntry>  DebuffResistances => data?.debuffResistances;
    public List<OnHitEffectEntry>       OnHitEffects      => data?.onHitEffects;
    public string                       FusionLabel       => $"S{fusionLevel}";

    // ── Résistance par ElementType ────────────────────────────
    public float GetResistance(ElementType element)
    {
        switch (element)
        {
            case ElementType.Fire:      return resistFire;
            case ElementType.Water:     return resistWater;
            case ElementType.Lightning: return resistLightning;
            case ElementType.Earth:     return resistEarth;
            case ElementType.Nature:    return resistNature;
            case ElementType.Darkness:  return resistDarkness;
            case ElementType.Light:     return resistLight;
            default:                    return 0f;
        }
    }

    // ── Fusion ────────────────────────────────────────────────
    public static GlovesInstance Fuse(GlovesInstance a, GlovesInstance b)
    {
        if (a == null || b == null)
        {
            UnityEngine.Debug.LogWarning("[GlovesInstance] Fuse : une des deux instances est null.");
            return a ?? b;
        }

        GlovesInstance result = new GlovesInstance(a.data)
        {
            fusionLevel     = Mathf.Min(6, Mathf.Max(a.fusionLevel, b.fusionLevel) + 1),
            resistFire      = a.resistFire      + b.resistFire,
            resistWater     = a.resistWater     + b.resistWater,
            resistLightning = a.resistLightning + b.resistLightning,
            resistEarth     = a.resistEarth     + b.resistEarth,
            resistNature    = a.resistNature    + b.resistNature,
            resistDarkness  = a.resistDarkness  + b.resistDarkness,
            resistLight     = a.resistLight     + b.resistLight,
        };

        UnityEngine.Debug.Log($"[GlovesInstance] Fusion → S{result.fusionLevel} | " +
                              $"Fire {result.resistFire:P0} | " +
                              $"Water {result.resistWater:P0} | Lightning {result.resistLightning:P0}");
        return result;
    }
}