using UnityEngine;
using System.Collections.Generic;

// =============================================================
// CHARACTERDATA — ScriptableObject TEMPLATE uniquement
// Définit les stats de base, l'arme de départ et les skills
// initiaux d'un personnage. Ne stocke PAS de données runtime.
//
// Les données runtime (XP, affinités, compteurs, titre actif)
// vivent dans Player.cs.
//
// AetherTree GDD v16 — Section 2
// =============================================================

[CreateAssetMenu(fileName = "NewCharacter", menuName = "AetherTree/Characters/CharacterData")]
public class CharacterData : ScriptableObject
{
    // ── Identité ──────────────────────────────────────────────
    [Header("Identité")]
    public string characterName = "Aventurier";
    public Sprite portrait;

    // ── Arme de départ — choix définitif ─────────────────────
    [Header("Arme de départ (choix irrévocable)")]
    [Tooltip("Arme sélectionnée à la création — définit WeaponCategory et ArmorType pour toute la partie")]
    public WeaponData startingWeapon;

    // ── Stats de base (avant équipement) ─────────────────────
    [Header("Stats de base")]
    public float baseMaxHP    = 100f;
    public float baseMaxMana  = 50f;
    public float baseRegenHP  = 0.2f;
    public float baseRegenMana = 0.1f;
    public float baseMoveSpeed = 5f;

    // ── Progression par niveau ────────────────────────────────
    [Header("Progression par niveau")]
    [Tooltip("HP gagnés par niveau selon la catégorie d'arme")]
    public float hpPerLevel    = 20f;
    [Tooltip("Mana gagnée par niveau")]
    public float manaPerLevel  = 10f;
    public float regenHPPerLevel   = 0.2f;
    public float regenManaPerLevel = 0.1f;

    // ── XP requis par niveau ──────────────────────────────────
    [Header("Courbe XP")]
    [Tooltip("XP combat requis pour passer chaque niveau (index 0 = niveau 1→2)")]
    public List<int> xpThresholds = new List<int> { 100, 250, 500, 900, 1500 };

    // ── Skills de départ ──────────────────────────────────────
    [Header("Skills de départ")]
    [Tooltip("Skills disponibles dès le début — placés automatiquement dans la SkillBar")]
    public List<SkillData> startingSkills = new List<SkillData>();

    // ── Utilitaires ───────────────────────────────────────────
    /// <summary>Catégorie d'arme déduite de l'arme de départ.</summary>
    public WeaponCategory WeaponCategory =>
        startingWeapon != null ? startingWeapon.Category : WeaponCategory.Melee;

    /// <summary>ArmorType équipable par ce personnage.</summary>
    public ArmorType ArmorType =>
        startingWeapon != null ? startingWeapon.LinkedArmorType : ArmorType.Melee;

    /// <summary>XP nécessaire pour atteindre le niveau suivant.</summary>
    public int GetXPThreshold(int currentLevel)
    {
        int idx = currentLevel - 1;
        if (idx < 0) return xpThresholds.Count > 0 ? xpThresholds[0] : 100;
        if (idx < xpThresholds.Count) return xpThresholds[idx];
        // Formule exponentielle si la liste est dépassée
        return Mathf.RoundToInt(xpThresholds[xpThresholds.Count - 1] * Mathf.Pow(1.5f, idx - xpThresholds.Count + 1));
    }
}
