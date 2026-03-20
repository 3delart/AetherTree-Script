using UnityEngine;
using System.Collections.Generic;

// =============================================================
// JewelryData — ScriptableObject template de bijou
// Path : Assets/Scripts/Data/Inventory/Equipment/JewelryData.cs
// AetherTree GDD v30 — Section 5.1
//
// Règles GDD :
//   - Pas de grade, pas d'upgrade, pas de fusion
//   - 1 à 4 slots gemme fixés sur le SO
//   - Gemmes : première insertion irréversible
//
// 4 slots de configuration (uniformes sur tous les équipements) :
//   bonuses           → StatBonus (CritChance, BonusHP, Precision...)
//   statusEffects     → StatusEffectEntry (debuffs/buffs à l'attaque)
//   debuffResistances → DebuffResistanceEntry (résistance aux debuffs)
//   onHitEffects      → OnHitEffectEntry (effets quand on reçoit un coup)
//
// Assets > Create > AetherTree > Equipment > JewelryData
// =============================================================

[CreateAssetMenu(fileName = "NewJewelry", menuName = "AetherTree/Equipment/JewelryData")]
public class JewelryData : ScriptableObject
{
    [Header("Identité")]
    public string      jewelryName = "Jewelry";
    public JewelrySlot jewelrySlot = JewelrySlot.Ring;
    public Sprite      icon;

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

    [Header("Niveau")]
    [Tooltip("Niveau du bijou — fixe, identique sur toutes les instances de ce SO.")]
    public int jewelryLevel = 1;

    [Header("Slots gemme")]
    [Tooltip("Nombre de slots gemme sur ce bijou (1 à 4).")]
    [Range(1, 4)]
    public int gemSlots = 1;

    [Tooltip("Niveau maximum de gemme pouvant être insérée.")]
    public int maxGemLevel = 1;

    // ── 4 slots de configuration ──────────────────────────────

    [Header("① Bonus secondaires (stats passives)")]
    [Tooltip("Bonus supplémentaires de ce bijou.\n" +
             "Ex: CritChance 0.05 | BonusHP 100 | Precision 20\n" +
             "Ces bonus sont fixes — non affectés par les gemmes.")]
    public List<StatBonus> bonuses = new List<StatBonus>();

    [Header("② Effets de statut (appliqués à chaque attaque)")]
    [Tooltip("Effets appliqués lors d'une attaque selon leur probabilité.\n" +
             "Glisse un DebuffData ou BuffData + règle la chance.")]
    public List<StatusEffectEntry> statusEffects = new List<StatusEffectEntry>();

    [Header("③ Résistances aux debuffs")]
    [Tooltip("Chances de résister à un debuff spécifique.\n" +
             "Ex: Curse 0.15 = 15% de chance de résister à la malédiction.")]
    public List<DebuffResistanceEntry> debuffResistances = new List<DebuffResistanceEntry>();

    [Header("④ Effets On-Hit (déclenchés quand on reçoit un coup)")]
    [Tooltip("Effets déclenchés quand le porteur reçoit un coup.\n" +
             "Ex: HealOnHit 3% MaxHP | CounterBuff Haste 20%\n" +
             "Glisse un OnHitEffectData + ajuste la chance si besoin.")]
    public List<OnHitEffectEntry> onHitEffects = new List<OnHitEffectEntry>();

    [Header("Description")]
    [TextArea]
    public string       description  = "";


    public JewelryInstance CreateInstance() => new JewelryInstance(this);
}

// ── Type de bijou ─────────────────────────────────────────────
public enum JewelrySlot { Ring, Necklace, Bracelet }

// =============================================================
// GemSlotInstance — état d'un slot gemme sur un bijou
// =============================================================
[System.Serializable]
public class GemSlotInstance
{
    public GemInstance gem = null;

    public bool IsEmpty  => gem == null;
    public bool IsFilled => gem != null;

    /// <summary>
    /// Tente d'insérer une gemme dans ce slot.
    /// Première insertion irréversible — GDD v21 section 5.1.
    /// </summary>
    public bool TryInsert(GemInstance newGem)
    {
        if (newGem == null)
        {
            Debug.LogWarning("[GemSlotInstance] TryInsert : gemme null.");
            return false;
        }

        if (IsFilled)
        {
            Debug.LogWarning($"[GemSlotInstance] Slot déjà occupé par {gem.Label} — insertion irréversible.");
            return false;
        }

        gem = newGem;
        gem.Reveal();
        Debug.Log($"[GemSlotInstance] Gemme insérée définitivement : {newGem.Label}");
        return true;
    }
}

// =============================================================
// JewelryInstance — données runtime d'un bijou équipé
// =============================================================
[System.Serializable]
public class JewelryInstance
{
    public JewelryData data;

    public GemSlotInstance[] gemSlots;

    public JewelryInstance(JewelryData source)
    {
        data     = source;
        gemSlots = new GemSlotInstance[source.gemSlots];
        for (int i = 0; i < gemSlots.Length; i++)
            gemSlots[i] = new GemSlotInstance();
    }

    // ── Défenses (lues sur le SO) ─────────────────────────────
    public float MeleeDefense  => data?.meleeDefense  ?? 0f;
    public float RangedDefense => data?.rangedDefense ?? 0f;
    public float MagicDefense  => data?.magicDefense  ?? 0f;

    // ── Raccourcis SO (4 slots) ───────────────────────────────
    public string                       JewelryName       => data?.jewelryName ?? "Jewelry";
    public Sprite                       Icon              => data?.icon;
    public JewelrySlot                  Slot              => data?.jewelrySlot ?? JewelrySlot.Ring;
    public int                          JewelryLevel      => data?.jewelryLevel ?? 1;
    public int                          MaxGemLevel       => data?.maxGemLevel  ?? 1;
    public List<StatBonus>              Bonuses           => data?.bonuses;
    public List<StatusEffectEntry>      StatusEffects     => data?.statusEffects;
    public List<DebuffResistanceEntry>  DebuffResistances => data?.debuffResistances;
    public List<OnHitEffectEntry>       OnHitEffects      => data?.onHitEffects;

    // ── Insertion gemme ───────────────────────────────────────
    public bool TryInsertGem(int slotIndex, GemInstance gem)
    {
        if (slotIndex < 0 || slotIndex >= gemSlots.Length)
        {
            Debug.LogWarning($"[JewelryInstance] Slot {slotIndex} invalide (max {gemSlots.Length - 1}).");
            return false;
        }

        if (gem.GemLevel > (data?.maxGemLevel ?? 1))
        {
            Debug.LogWarning($"[JewelryInstance] Gemme {gem.Label} (Lv{gem.GemLevel}) trop haute " +
                             $"pour ce bijou (max Lv{data?.maxGemLevel}).");
            return false;
        }

        return gemSlots[slotIndex].TryInsert(gem);
    }

    public IEnumerable<GemInstance> GetEquippedGems()
    {
        foreach (var slot in gemSlots)
            if (!slot.IsEmpty) yield return slot.gem;
    }
}