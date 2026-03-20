using System;
using System.Collections.Generic;
using UnityEngine;

// =============================================================
// ELEMENTALSYSTEM.CS — Jauge d'affinité élémentaire
// Path : Assets/Scripts/Data/Elements/ElementalSystem.cs
// AetherTree GDD v30 — Section 7
//
// Règles :
//   - Jauge glissante (fenêtre de N "cast-équivalents")
//   - Attaque basique  = 0.25 cast-équivalent
//   - Sort élémentaire = 1.0 cast-équivalent
//   - Fenêtre par défaut : 200 cast-équivalents (paramétrable)
//   - Jauge commence à 100% Neutre
//   - Mono si 1 élément ≥ 25% ET aucun autre ≥ 20%
//   - Dual si 2 éléments ≥ 20%, écart < 10pts, ET sort combo équipé
//   - Équilibriste si aucun élément ≥ 25%
//
// Cycle : 🌊 Eau → 🔥 Feu → 🌿 Nature → 🌍 Terre → ⚡ Foudre → 🌊 Eau
// Duo   : ☀ Lumière ↔ 🌑 Ténèbres
// Neutre : seul — pas de counter, pas de vulnérabilité
// Glace supprimée v30
// =============================================================

public enum TitleMode { Neutral, Mono, Dual, Equilibriste }

public class ElementalSystem : MonoBehaviour
{
    // ── Paramètre Inspector ───────────────────────────────────
    [Header("Fenêtre glissante")]
    [Tooltip("Taille de la fenêtre en cast-équivalents (basic = 0.25, sort = 1.0)")]
    [SerializeField] private float _windowSize = 200f;

    // ── Constantes de poids ───────────────────────────────────
    public const float BASIC_ATTACK_WEIGHT = 0.25f;
    public const float SKILL_WEIGHT        = 1.0f;

    // ── Structure interne (évite les tuples nommés) ───────────
    private struct CastEntry
    {
        public ElementType element;
        public float       weight;
        public CastEntry(ElementType e, float w) { element = e; weight = w; }
    }

    // ── Données internes ──────────────────────────────────────
    private readonly List<CastEntry>            _history      = new List<CastEntry>();
    private          Dictionary<ElementType, float> _weightCounts;
    private          float                      _totalWeight  = 0f;
    private          int                        _totalCasts   = 0;

    // ─────────────────────────────────────────────────────────
    private void Awake()
    {
        _weightCounts = new Dictionary<ElementType, float>();
        foreach (ElementType t in Enum.GetValues(typeof(ElementType)))
            _weightCounts[t] = 0f;
    }

    private void Start()
    {
        // GDD §7.3 : jauge commence à 100% Neutre.
        // Dans Start() et non Awake() : _windowSize est injecté par Unity
        // depuis l'Inspector AVANT Start(), mais PAS garanti avant Awake().
        InitNeutralWindow();
    }

    /// <summary>
    /// Remplit la fenêtre avec _windowSize cast-équivalents Neutre.
    /// Appelé au Start(). Peut être rappelé après un reset de scène.
    /// </summary>
    public void InitNeutralWindow()
    {
        _history.Clear();
        foreach (ElementType t in Enum.GetValues(typeof(ElementType)))
            _weightCounts[t] = 0f;
        _totalWeight = 0f;
        _totalCasts  = 0;

        _history.Add(new CastEntry(ElementType.Neutral, _windowSize));
        _weightCounts[ElementType.Neutral] = _windowSize;
        _totalWeight = _windowSize;
    }

    // =========================================================
    // ENREGISTREMENT D'UN CAST
    // =========================================================

    /// <summary>
    /// Enregistre un cast dans la fenêtre glissante.
    /// isBasicAttack = true → poids 0.25 | false → poids 1.0
    /// </summary>
    public void RegisterCast(ElementType element, bool isBasicAttack = false)
    {
        float weight = isBasicAttack ? BASIC_ATTACK_WEIGHT : SKILL_WEIGHT;

        _history.Add(new CastEntry(element, weight));
        _totalCasts++;

        if (_weightCounts.ContainsKey(element))
            _weightCounts[element] += weight;
        else
            _weightCounts[element] = weight;

        _totalWeight += weight;

        // Dilution proportionnelle : l excédent est retiré équitablement sur tous les
        // éléments présents SAUF l élément entrant. Un cast ne s annule jamais lui-même.
        // Si la fenêtre est 100% du même élément, l excédent est retiré sur lui-même.
        float excess = _totalWeight - _windowSize;
        if (excess > 0f)
        {
            // Calcule le poids total des autres éléments
            float othersTotal = 0f;
            foreach (var kvp in _weightCounts)
                if (kvp.Key != element) othersTotal += kvp.Value;

            if (othersTotal > 0f)
            {
                // Retire proportionnellement sur chaque autre élément
                // ET purge les entrées history correspondantes
                var keys = new System.Collections.Generic.List<ElementType>(_weightCounts.Keys);
                foreach (ElementType key in keys)
                {
                    if (key == element) continue;
                    float ratio      = _weightCounts[key] / othersTotal;
                    float toRemove   = excess * ratio;
                    _weightCounts[key] = Mathf.Max(0f, _weightCounts[key] - toRemove);
                }

                // Purge history : retire les entrées des autres éléments proportionnellement
                float remainingExcess = excess;
                for (int i = 0; i < _history.Count - 1 && remainingExcess > 0f; i++)
                {
                    if (_history[i].element == element) continue;
                    float remove = Mathf.Min(_history[i].weight, remainingExcess);
                    remainingExcess -= remove;
                    if (remove >= _history[i].weight)
                    {
                        _history.RemoveAt(i);
                        i--;
                    }
                    else
                    {
                        _history[i] = new CastEntry(_history[i].element, _history[i].weight - remove);
                    }
                }
            }
            else
            {
                // Fenêtre 100% même élément — retire l excédent sur lui-même (FIFO)
                while (_totalWeight > _windowSize && _history.Count > 0)
                {
                    CastEntry old = _history[0];
                    _history.RemoveAt(0);
                    _weightCounts[old.element] = Mathf.Max(0f, _weightCounts[old.element] - old.weight);
                    _totalWeight = Mathf.Max(0f, _totalWeight - old.weight);
                }
            }

            _totalWeight = _windowSize;
        }
    }

    // =========================================================
    // AFFINITÉS
    // =========================================================

    /// <summary>
    /// Affinité [0..1] d'un élément.
    /// Fenêtre vide → 1.0 pour Neutral, 0 pour le reste.
    /// </summary>
    public float GetAffinity(ElementType element)
    {
        if (_totalWeight <= 0f)
            return (element == ElementType.Neutral) ? 1f : 0f;

        float w = 0f;
        _weightCounts.TryGetValue(element, out w);
        return w / _totalWeight;
    }

    /// <summary>
    /// Éléments actifs (> 0%), triés par affinité décroissante.
    /// Retourne une List de ElementAffinityPair pour l'UI.
    /// </summary>
    public List<ElementAffinityPair> GetActiveAffinities()
    {
        var result = new List<ElementAffinityPair>();

        foreach (ElementType t in Enum.GetValues(typeof(ElementType)))
        {
            float aff = GetAffinity(t);
            if (aff > 0f)
                result.Add(new ElementAffinityPair(t, aff));
        }

        result.Sort((a, b) => b.affinity.CompareTo(a.affinity));
        return result;
    }

    /// <summary>Élément le plus présent dans la fenêtre.</summary>
    public ElementType GetDominantElement()
    {
        if (_totalWeight <= 0f) return ElementType.Neutral;

        ElementType dominant = ElementType.Neutral;
        float       maxAff   = GetAffinity(ElementType.Neutral); // commence avec Neutral

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
        // §7.4 : Rang 1 > 0% | Rang 2 ≥ 25% | Rang 3 ≥ 50% | Rang 4 ≥ 75% | Rang 5 ≥ 90%
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

    /// <summary>
    /// Mode de titre actif.
    /// Dual nécessite hasDualSkillEquipped = true ET 2 éléments ≥ 20%, écart < 10pts.
    /// </summary>
    public TitleMode GetTitleMode(bool hasDualSkillEquipped = false)
    {
        if (_totalWeight <= 0f) return TitleMode.Neutral;

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

    /// <summary>Éléments éligibles dual (≥ 20%), triés par affinité décroissante.</summary>
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

        // §7.4 : Rang 1=+5% | Rang 2=+10% | Rang 3=+15% | Rang 4=+20% | Rang 5=+25%
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

        // §7.5 : Dual = 75% du bonus mono sur chaque élément
        if (mode == TitleMode.Dual)
            bonus *= 0.75f;

        // §7.5 : Équilibriste = +5% dégâts sur tous les éléments actifs
        if (mode == TitleMode.Equilibriste && rank > 0)
            bonus += 0.05f;

        return 1f + bonus;
    }

    /// <summary>Bonus neutre : +20% si fenêtre 100% neutre.</summary>
    public float GetNeutralBonus()
        => (_totalWeight <= 0f) ? 1.20f : 1.00f;

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
    public int   GetHistoryCount() => _history.Count;
    public float GetTotalWeight()  => _totalWeight;
    public float GetWindowSize()   => _windowSize;
}

// ── Struct de retour pour l'UI (plus safe que les tuples nommés) ─
public struct ElementAffinityPair
{
    public ElementType element;
    public float       affinity;
    public ElementAffinityPair(ElementType e, float a) { element = e; affinity = a; }
}