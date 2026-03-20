using System;
using System.Collections.Generic;
using UnityEngine;

// =============================================================
// ELEMENTALSYSTEM.CS — Jauge d'affinité élémentaire
// Path : Assets/Scripts/Data/Elements/ElementalSystem.cs
// AetherTree GDD v30 — Section 7
//
// Logique : dilution ÉGALE
//   Quand on ajoute du poids à un élément A, l'excédent est retiré
//   ÉQUITABLEMENT sur tous les autres éléments présents (> 0).
// =============================================================

public enum TitleMode { Neutral, Mono, Dual, Equilibriste }

public class ElementalSystem : MonoBehaviour
{
    [Header("Fenêtre glissante")]
    [Tooltip("Taille de la fenêtre en cast-équivalents (basic = 0.25, sort = 1.0)")]
    [SerializeField] private float _windowSize = 200f;

    public const float BASIC_ATTACK_WEIGHT = 0.25f;
    public const float SKILL_WEIGHT        = 1.0f;

    private Dictionary<ElementType, float> _weightCounts;
    private int _totalCasts = 0;

    private void Awake()
    {
        _weightCounts = new Dictionary<ElementType, float>();
        foreach (ElementType t in Enum.GetValues(typeof(ElementType)))
            _weightCounts[t] = 0f;
    }

    private void Start()
    {
        InitNeutralWindow();
    }

    public void InitNeutralWindow()
    {
        foreach (ElementType t in Enum.GetValues(typeof(ElementType)))
            _weightCounts[t] = 0f;

        _weightCounts[ElementType.Neutral] = _windowSize;
        _totalCasts = 0;
    }

    // =========================================================
    // CHARGEMENT SAUVEGARDE
    // =========================================================

    /// <summary>
    /// Restaure les affinités depuis la sauvegarde.
    /// Appelé par SaveSystem après un chargement.
    /// </summary>
    public void LoadAffinities(List<SavedElementAffinity> affinities)
    {
        if (affinities == null || affinities.Count == 0)
        {
            InitNeutralWindow();
            return;
        }

        // Remet tout à zéro
        foreach (ElementType t in Enum.GetValues(typeof(ElementType)))
            _weightCounts[t] = 0f;

        // Applique les poids sauvegardés
        foreach (var entry in affinities)
        {
            if (Enum.TryParse(entry.element, out ElementType type)
                && type != ElementType.Any)   // ignore la valeur sentinelle Any = -1
                _weightCounts[type] = Mathf.Max(0f, entry.weight);
        }

        // Renormalise
        Renormalize();
    }

    // =========================================================
    // ENREGISTREMENT D'UN CAST — dilution égale
    // =========================================================

    public void RegisterCast(ElementType element, bool isBasicAttack = false)
    {
        float weight = isBasicAttack ? BASIC_ATTACK_WEIGHT : SKILL_WEIGHT;
        _totalCasts++;

        // 1. Ajoute le poids à l'élément entrant
        _weightCounts[element] += weight;

        // 2. Retire le même poids équitablement sur les autres éléments présents
        float toDistribute = weight;

        while (toDistribute > 0.0001f)
        {
            var others = new List<ElementType>();
            foreach (var kvp in _weightCounts)
                if (kvp.Key != element && kvp.Value > 0f)
                    others.Add(kvp.Key);

            if (others.Count == 0) break;

            float shareEach = toDistribute / others.Count;
            float leftover  = 0f;

            foreach (ElementType key in others)
            {
                float available = _weightCounts[key];
                if (available >= shareEach)
                {
                    _weightCounts[key] -= shareEach;
                }
                else
                {
                    leftover += shareEach - available;
                    _weightCounts[key] = 0f;
                }
            }

            toDistribute = leftover;
        }

        // 3. Renormalise
        Renormalize();
    }

    // =========================================================
    // RENORMALISATION
    // =========================================================

    private void Renormalize()
    {
        float total = 0f;
        foreach (var kvp in _weightCounts)
            total += kvp.Value;

        if (total <= 0f)
        {
            InitNeutralWindow();
            return;
        }

        float factor = _windowSize / total;
        var keys = new List<ElementType>(_weightCounts.Keys);
        foreach (ElementType key in keys)
            _weightCounts[key] *= factor;
    }

    // =========================================================
    // AFFINITÉS
    // =========================================================

    public float GetAffinity(ElementType element)
    {
        float w = 0f;
        _weightCounts.TryGetValue(element, out w);
        return Mathf.Clamp01(w / _windowSize);
    }

    public List<ElementAffinityPair> GetActiveAffinities()
    {
        var result = new List<ElementAffinityPair>();
        foreach (ElementType t in Enum.GetValues(typeof(ElementType)))
        {
            float aff = GetAffinity(t);
            if (aff > 0.001f)
                result.Add(new ElementAffinityPair(t, aff));
        }
        result.Sort((a, b) => b.affinity.CompareTo(a.affinity));
        return result;
    }

    public ElementType GetDominantElement()
    {
        ElementType dominant = ElementType.Neutral;
        float       maxAff   = GetAffinity(ElementType.Neutral);

        foreach (ElementType t in Enum.GetValues(typeof(ElementType)))
        {
            if (t == ElementType.Neutral) continue;
            float aff = GetAffinity(t);
            if (aff > maxAff) { maxAff = aff; dominant = t; }
        }

        return dominant;
    }

    // =========================================================
    // RANG D'AFFINITÉ (0–5)
    // =========================================================

    public int GetElementRank(ElementType element)
    {
        float aff = GetAffinity(element);
        if (aff <= 0f)   return 0;
        if (aff < 0.25f) return 1;
        if (aff < 0.50f) return 2;
        if (aff < 0.75f) return 3;
        if (aff < 0.90f) return 4;
        return 5;
    }

    // =========================================================
    // TITRE MODE
    // =========================================================

    public TitleMode GetTitleMode(bool hasDualSkillEquipped = false)
    {
        ElementType dominant = GetDominantElement();
        float domAff = GetAffinity(dominant);

        if (dominant == ElementType.Neutral || domAff < 0.01f)
            return TitleMode.Neutral;

        List<ElementType> dualCandidates = GetDualCandidates();

        if (dualCandidates.Count >= 2 && hasDualSkillEquipped)
        {
            float aff1 = GetAffinity(dualCandidates[0]);
            float aff2 = GetAffinity(dualCandidates[1]);
            if (Mathf.Abs(aff1 - aff2) < 0.10f)
                return TitleMode.Dual;
        }

        if (domAff >= 0.25f)
            return TitleMode.Mono;

        return TitleMode.Equilibriste;
    }

    public List<ElementType> GetDualCandidates()
    {
        var result = new List<ElementType>();
        foreach (ElementType t in Enum.GetValues(typeof(ElementType)))
        {
            if (t == ElementType.Neutral) continue;
            if (GetAffinity(t) >= 0.20f) result.Add(t);
        }
        result.Sort((a, b) => GetAffinity(b).CompareTo(GetAffinity(a)));
        return result;
    }

    // =========================================================
    // BONUS DÉGÂTS
    // =========================================================

    public float GetElementalDamageBonus(ElementType element)
    {
        int rank = GetElementRank(element);
        TitleMode mode = GetTitleMode();

        float bonus;
        switch (rank)
        {
            case 1:  bonus = 0.05f; break;
            case 2:  bonus = 0.10f; break;
            case 3:  bonus = 0.15f; break;
            case 4:  bonus = 0.20f; break;
            case 5:  bonus = 0.25f; break;
            default: bonus = 0.00f; break;
        }

        if (mode == TitleMode.Dual)
            bonus *= 0.75f;

        if (mode == TitleMode.Equilibriste && rank > 0)
            bonus += 0.05f;

        return 1f + bonus;
    }

    public float GetNeutralBonus()
        => GetAffinity(ElementType.Neutral) >= 0.99f ? 1.20f : 1.00f;

    // =========================================================
    // VULNÉRABILITÉ
    // =========================================================

    public float GetVulnerability(ElementType incomingElement)
    {
        TitleMode mode = GetTitleMode();

        if (mode == TitleMode.Mono)
        {
            ElementType dominant = GetDominantElement();
            if (incomingElement != dominant.GetCounter()) return 1f;
            return GetMonoVulnerability(GetAffinity(dominant));
        }

        if (mode == TitleMode.Dual)
        {
            List<ElementType> candidates = GetDualCandidates();
            float maxBonus = 0f;
            foreach (ElementType elem in candidates)
            {
                if (incomingElement == elem.GetCounter())
                {
                    float vuln = GetDualVulnerability(GetAffinity(elem));
                    if (vuln - 1f > maxBonus) maxBonus = vuln - 1f;
                }
            }
            return 1f + maxBonus;
        }

        return 1f;
    }

    private float GetMonoVulnerability(float aff)
    {
        if (aff >= 1.00f) return 1.25f;
        if (aff >= 0.95f) return 1.23f;
        if (aff >= 0.90f) return 1.20f;
        if (aff >= 0.80f) return 1.15f;
        if (aff >= 0.65f) return 1.10f;
        if (aff >= 0.50f) return 1.05f;
        return 1f;
    }

    private float GetDualVulnerability(float aff)
    {
        if (aff >= 0.40f) return 1.12f;
        if (aff >= 0.30f) return 1.08f;
        if (aff >= 0.20f) return 1.05f;
        return 1f;
    }

    // =========================================================
    // UTILITAIRES
    // =========================================================

    public int   GetTotalCasts()   => _totalCasts;
    public int   GetHistoryCount() => 0;
    public float GetTotalWeight()  => _windowSize;
    public float GetWindowSize()   => _windowSize;
}

public struct ElementAffinityPair
{
    public ElementType element;
    public float       affinity;
    public ElementAffinityPair(ElementType e, float a) { element = e; affinity = a; }
}