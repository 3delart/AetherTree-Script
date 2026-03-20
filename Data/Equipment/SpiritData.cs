using UnityEngine;
using System.Collections.Generic;

// =============================================================
// SpiritData — ScriptableObject template d'esprit élémentaire
// Path : Assets/Scripts/Data/Inventory/Equipment/SpiritData.cs
// AetherTree GDD v30 — Section 6.9
//
// Règles :
//   - 8 esprits au total — 7 élémentaires (Feu/Eau/Terre/Nature/Foudre/Ténèbres/Lumière)
//     + 1 Esprit Neutre (ElementType.Neutral)
//   - Esprit Neutre : bonus phys/crit/HP au lieu de points élémentaires (paliers distincts)
//   - 1 seul esprit actif à la fois — changement libre depuis l'inventaire
//   - Niveau max 50 — bonus aux paliers 10, 20, 30, 40, 50
//   - XP = mobs tués dans la plage ±15 niveaux du joueur
//   - Source principale de points élémentaires du joueur (§5.2)
//   - Esprit Neutre : même slot, même mécanique XP/niveau, bonus différents (§6.9)
//
// Assets > Create > AetherTree > Equipment > SpiritData
// =============================================================

[CreateAssetMenu(fileName = "NewSpirit", menuName = "AetherTree/Equipment/SpiritData")]
public class SpiritData : ScriptableObject
{
    // ── Identité ──────────────────────────────────────────────
    [Header("Identité")]
    public string      spiritName = "Spirit";
    public Sprite      icon;
    public GameObject  spiritPrefab;

    [Header("Niveau")]
    [Tooltip("Niveau minimum requis pour équiper cette arme.")]
    [Min(1)] public int requiredLevel = 1;

    [Header("Élément")]
    [Tooltip("Élément fixe de cet esprit — 1 esprit par élément")]
    public ElementType element = ElementType.Fire;

    // ── Progression ───────────────────────────────────────────
    [Header("Progression")]
    [Tooltip("Niveau maximum de l'esprit (défaut 50)")]
    public int maxLevel = 50;

    [Tooltip("Points élémentaires donnés au niveau 1")]
    public int pointsAtLevel1 = 1;

    [Tooltip("Points élémentaires donnés au niveau max")]
    public int pointsAtMaxLevel = 20;

    [Tooltip("Exposant de la courbe de progression (1 = linéaire, 2 = exponentielle).\n" +
             "Recommandé : 1.5 pour une progression progressive")]
    public float pointsCurveExponent = 1.5f;

    // ── Paliers ───────────────────────────────────────────────
    [Header("Bonus aux paliers")]
    [Tooltip("Bonus débloqués à certains niveaux.\n" +
             "Recommandé : PointsFire (flat) ou Element Bonus Fire (ratio 0.05 = +5% pts feu).\n" +
             "Défaut : paliers 10, 20, 30, 40, 50")]

    [Header("Description")]
    [TextArea]
    public string       description  = "";
    public List<SpiritMilestone> milestones = new List<SpiritMilestone>();


    // ── Utilitaires ───────────────────────────────────────────

    /// <summary>
    /// Points élémentaires donnés au niveau N.
    /// Interpolation progressive entre pointsAtLevel1 et pointsAtMaxLevel.
    /// </summary>
    public int GetPointsAtLevel(int level)
    {
        if (maxLevel <= 0) return pointsAtLevel1;
        float t     = Mathf.Clamp01((float)(level - 1) / (maxLevel - 1));
        float tPow  = Mathf.Pow(t, pointsCurveExponent);
        return Mathf.RoundToInt(Mathf.Lerp(pointsAtLevel1, pointsAtMaxLevel, tPow));
    }

    /// <summary>
    /// XP requis pour passer du niveau N au niveau N+1.
    /// Formule automatique : croissance progressive.
    /// </summary>
    public int GetXPRequired(int level)
    {
        return Mathf.RoundToInt(100 * Mathf.Pow(level, 1.3f));
    }

    /// <summary>Points élémentaires cumulés de niveau 1 à N.</summary>
    public int GetTotalPointsAtLevel(int level)
    {
        int total = 0;
        for (int i = 1; i <= Mathf.Min(level, maxLevel); i++)
            total += GetPointsAtLevel(i);
        return total;
    }

    /// <summary>Retourne les bonus du palier atteint à ce niveau (null si aucun).</summary>
    public SpiritMilestone GetMilestone(int level)
    {
        if (milestones == null) return null;
        foreach (SpiritMilestone m in milestones)
            if (m.level == level) return m;
        return null;
    }
}

// =============================================================
// SpiritMilestone — Bonus débloqué à un palier de niveau
// =============================================================
[System.Serializable]
public class SpiritMilestone
{
    [Tooltip("Niveau auquel ce bonus est débloqué (ex: 10, 20, 30, 40, 50)")]
    public int level;

    [Tooltip("Bonus débloqué à ce palier — recommandé : points ou % dégâts élémentaires")]
    public List<StatBonus> bonuses = new List<StatBonus>();
}

// =============================================================
// SpiritInstance — données runtime d'un esprit équipé
// =============================================================
[System.Serializable]
public class SpiritInstance
{
    public SpiritData data;

    // ── Progression runtime ───────────────────────────────────
    public int level = 1;
    public int currentXP = 0;

    public SpiritInstance(SpiritData source)
    {
        data  = source;
        level = 1;
        currentXP = 0;
    }

    // ── Accesseurs ────────────────────────────────────────────
    public string      SpiritName  => data?.spiritName ?? "Spirit";
    public Sprite      Icon        => data?.icon;
    public ElementType Element     => data != null ? data.element : ElementType.Neutral;
    public int         MaxLevel    => data != null ? data.maxLevel : 50;
    public bool        IsMaxLevel  => level >= MaxLevel;

    /// <summary>Points élémentaires cumulés apportés par cet esprit à son niveau actuel.</summary>
    public int TotalElementalPoints => data != null ? data.GetTotalPointsAtLevel(level) : 0;

    /// <summary>XP requis pour le prochain niveau.</summary>
    public int XPRequired => data != null ? data.GetXPRequired(level) : 100;

    // ── XP & Level Up ─────────────────────────────────────────

    /// <summary>
    /// Ajoute de l'XP à l'esprit.
    /// Retourne true si un level up s'est produit.
    /// Le mob tué doit être dans la plage ±15 niveaux du joueur — vérifié par SpiritSystem.
    /// </summary>
    public bool AddXP(int amount)
    {
        if (IsMaxLevel) return false;

        currentXP += amount;
        bool leveledUp = false;

        while (currentXP >= XPRequired && !IsMaxLevel)
        {
            currentXP -= XPRequired;
            level++;
            leveledUp = true;
            Debug.Log($"[Spirit] {SpiritName} → Niveau {level} !");
        }

        if (IsMaxLevel) currentXP = 0;
        return leveledUp;
    }
}