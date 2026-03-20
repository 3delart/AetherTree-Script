using System;
using System.Collections.Generic;
using UnityEngine;

// =============================================================
// ELEMENTDATA.CS — Data-driven, zéro switch à maintenir
// Path : Assets/Scripts/Data/Elements/ElementData.cs
// AetherTree GDD v30 — Section 7
//
// Pour ajouter un élément :
//   1. Ajouter la valeur dans ElementType
//   2. Lui mettre l'attribut [ElementInfo(label, r, g, b, counter)]
//   C'est tout. Tout le reste s'adapte automatiquement.
//
// DÉMO : Neutral, Fire, Water, Earth, Nature (5 éléments)
// FINAL : + Lightning, Darkness, Light (8 éléments total)
//         Glace supprimée v30 — Wind supprimé v21
//
// Cycle : 🌊 Eau → 🔥 Feu → 🌿 Nature → 🌍 Terre → ⚡ Foudre → 🌊 Eau
// Duo   : ☀ Lumière ↔ 🌑 Ténèbres
// Neutre : seul, ne contre rien, rien ne le contre
//
// Poison = DebuffType uniquement — JAMAIS ElementType
// =============================================================

[AttributeUsage(AttributeTargets.Field)]
public class ElementInfoAttribute : Attribute
{
    public string      Label   { get; }
    public float       R       { get; }
    public float       G       { get; }
    public float       B       { get; }
    public ElementType Counter { get; }  // Élément contre lequel on est vulnérable
    public bool        IsDemo  { get; }  // Disponible en démo

    public ElementInfoAttribute(string label, float r, float g, float b,
                                ElementType counter, bool isDemo = false)
    {
        Label   = label;
        R       = r;
        G       = g;
        B       = b;
        Counter = counter;
        IsDemo  = isDemo;
    }
}

// =============================================================
// ENUM ELEMENTTYPE
// Ajouter un élément = ajouter une ligne + son attribut
// =============================================================
public enum ElementType
{
    [ElementInfo("— Any —",  0f, 0f, 0f, ElementType.Any)]
    Any = -1,

    [ElementInfo("Neutre",   0.75f, 0.75f, 0.75f, ElementType.Neutral, isDemo: true)]
    Neutral,


    [ElementInfo("Feu",      1.0f,  0.35f, 0.0f,  ElementType.Water,    isDemo: true)]
    Fire,

    [ElementInfo("Eau",      0.1f,  0.5f,  1.0f,  ElementType.Earth,    isDemo: true)]
    Water,

    [ElementInfo("Terre",    0.6f,  0.4f,  0.1f,  ElementType.Nature,   isDemo: true)]
    Earth,

    [ElementInfo("Nature",   0.15f, 0.75f, 0.2f,  ElementType.Fire,     isDemo: true)]
    Nature,

    [ElementInfo("Foudre",   0.8f,  0.6f,  1.0f,  ElementType.Water)]
    Lightning,

    [ElementInfo("Ténèbres", 0.3f,  0.1f,  0.4f,  ElementType.Light)]
    Darkness,

    [ElementInfo("Lumière",  1.0f,  0.95f, 0.5f,  ElementType.Darkness)]
    Light,
}

// =============================================================
// EXTENSIONS — lit les attributs, cache les résultats
// =============================================================
public static class ElementDataExtensions
{
    private static readonly Dictionary<ElementType, ElementInfoAttribute> _cache
        = new Dictionary<ElementType, ElementInfoAttribute>();

    private static ElementInfoAttribute GetInfo(ElementType type)
    {
        if (_cache.TryGetValue(type, out var cached)) return cached;
        var field = typeof(ElementType).GetField(type.ToString());
        var attr  = field?.GetCustomAttributes(typeof(ElementInfoAttribute), false);
        var info  = (attr != null && attr.Length > 0) ? (ElementInfoAttribute)attr[0] : null;
        _cache[type] = info;
        return info;
    }

    public static string      GetLabel(this ElementType t)   => GetInfo(t)?.Label   ?? t.ToString();
    public static ElementType GetCounter(this ElementType t) => GetInfo(t)?.Counter ?? ElementType.Neutral;
    public static bool        IsNeutral(this ElementType t)  => t == ElementType.Neutral;
    public static bool        IsDemo(this ElementType t)     => GetInfo(t)?.IsDemo  ?? false;

    public static Color GetColor(this ElementType t)
    {
        var info = GetInfo(t);
        return info != null ? new Color(info.R, info.G, info.B) : Color.white;
    }

    /// <summary>Tous les éléments (avec ou sans Neutre).</summary>
    public static List<ElementType> GetAllElements(bool includeNeutral = false)
    {
        var result = new List<ElementType>();
        foreach (ElementType t in Enum.GetValues(typeof(ElementType)))
        {
            if (!includeNeutral && t == ElementType.Neutral) continue;
            result.Add(t);
        }
        return result;
    }

    /// <summary>Éléments disponibles en démo (includeNeutral optionnel).</summary>
    public static List<ElementType> GetDemoElements(bool includeNeutral = false)
    {
        var result = new List<ElementType>();
        foreach (ElementType t in Enum.GetValues(typeof(ElementType)))
        {
            if (!includeNeutral && t == ElementType.Neutral) continue;
            if (t.IsDemo()) result.Add(t);
        }
        return result;
    }
}

// =============================================================
// ELEMENTDATA SO — config par élément
// =============================================================
[CreateAssetMenu(fileName = "Element_", menuName = "AetherTree/Elemental/ElementData")]
public class ElementData : ScriptableObject
{
    [Header("Identification")]
    public ElementType element;

    [Header("Effets thématiques")]
    [TextArea] public string primaryEffect;
    [TextArea] public string advancedEffect;

    [Header("Contenu associé")]
    public List<MobData>   associatedMobs  = new List<MobData>();
    public SkillData       firstSkill;

    // ── API statique (proxy vers extensions) ─────────────────
    public static string      GetLabel(ElementType t)   => t.GetLabel();
    public static Color       GetColor(ElementType t)   => t.GetColor();
    public static ElementType GetCounter(ElementType t) => t.GetCounter();
    public static bool        IsNeutral(ElementType t)  => t.IsNeutral();
}

// =============================================================
// STATUS EFFECT — séparé d'ElementType
// Poison est un DebuffType, jamais un élément
// Freeze supprimé v30 — lié à Glace (élément supprimé v30)
// =============================================================
public enum StatusEffect
{
    Burn,        // DoT Feu
    Slow,        // Ralentissement Eau
    Stun,        // Étourdissement Foudre
    Paralysis,   // Paralysie Foudre
    Root,        // Enracinement Terre/Nature
    Fear,        // Peur Ténèbres
    ManaDrain,   // Drain mana Ténèbres
    Poison,      // DoT Nature/Ténèbres — DebuffType, PAS ElementType
    Blinded,     // Lumière
    Knockback,   // Eau
    // Silenced supprimé — lié à Wind (élément supprimé v21)
    // Freeze supprimé — lié à Glace (élément supprimé v30)
}


public class ElementAffinityReq
{
    public ElementType element;
    [Range(0f, 1f)] public float minAffinity = 0.20f;
}
