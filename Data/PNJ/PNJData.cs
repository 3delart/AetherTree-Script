using UnityEngine;
using System.Collections.Generic;

// =============================================================
// PNJDATA — ScriptableObject de configuration d'un PNJ
// Path : Assets/Scripts/Data/PNJData.cs
// AetherTree GDD v30 — §13 (PNJ Données Techniques), §19 (PNJ & Dialogues)
//
// Contient toutes les données statiques d'un PNJ :
// type, stats, dialogues, shop, quêtes, respawn.
//
// Créer via : Assets > Create > AetherTree > PNJ > PNJData
// =============================================================

[CreateAssetMenu(fileName = "PNJ_New", menuName = "AetherTree/PNJ/PNJData")]
public class PNJData : ScriptableObject
{
    // ── Identité ──────────────────────────────────────────────
    [Header("Identité")]
    public string      pnjName    = "PNJ";
    public PNJType     pnjType    = PNJType.Decorative;
    public Sprite      portrait;                        // Affiché dans la DialogueUI

    // ── Dialogue ──────────────────────────────────────────────
    [Header("Dialogue")]
    public DialogueData defaultDialogue;                // Dialogue de base (tous types)
    public DialogueData knownPlayerDialogue;            // Si le joueur est déjà connu
    public DialogueData highReputationDialogue;         // Si worldReputation ≥ seuil
    [Tooltip("Rang de Réputation Monde minimum pour le dialogue premium (0 = désactivé)")]
    public int          reputationDialogueThreshold = 0;

    // ── Marchand ──────────────────────────────────────────────
    [Header("Marchand (PNJType.Merchant)")]
    public List<ShopEntry> shopItems  = new List<ShopEntry>();
    public List<ShopEntry> shopSkills = new List<ShopEntry>();

    // ── Forgeron ──────────────────────────────────────────────
    // GDD v30 §19.3 — upgrade +0→+3 à Braven, +0→+10 à Erenthal
    [Header("Forgeron (PNJType.Blacksmith)")]
    [Tooltip("Niveau d'upgrade maximum autorisé par ce forgeron (3 = Braven, 10 = Erenthal)")]
    public int maxUpgradeLevel = 3;

    // ── Antiquaire ────────────────────────────────────────────
    // GDD v30 §19.3 — identification + insertion de runes
    [Header("Antiquaire (PNJType.Antiquarian)")]
    [Tooltip("Ce PNJ peut identifier les runes non identifiées")]
    public bool canIdentifyRunes = false;
    [Tooltip("Ce PNJ peut insérer les runes dans les équipements")]
    public bool canInsertRunes   = false;

    // ── Quête ─────────────────────────────────────────────────
    [Header("Quête (PNJType.Quest)")]
    public List<QuestData> availableQuests = new List<QuestData>();

    // ── Garde ─────────────────────────────────────────────────
    [Header("Garde (PNJType.Guard)")]
    public float guardAggroRadius    = 200f;   // GDD v30 §19.2 — rayon 200
    public float guardAttackRange    = 2f;
    public float guardAttackDamage   = 20f;
    public float guardAttackCooldown = 1.5f;
    public float guardMoveSpeed      = 4f;

    // ── Maire ─────────────────────────────────────────────────
    // GDD v30 §19.5 — dialogue conditionnel en 4 stages
    [Header("Maire (PNJType.Mayor)")]
    public DialogueData guildUnlockDialogue;
    public DialogueData guildNotReadyDialogue;
    [Tooltip("Coût en Aeris pour créer une guilde — GDD v30 §19.5 (⚠ à définir)")]
    public int guildCreationCost = 0;

    // ── PNJ Faction ───────────────────────────────────────────
    // GDD v30 §19.6 — Solthars / Umbrans — zone faction uniquement
    [Header("Faction (PNJType.FactionNPC)")]
    [Tooltip("Faction à laquelle appartient ce PNJ")]
    public FactionType faction = FactionType.None;
    [Tooltip("Dialogue affiché si le joueur appartient à la faction adverse")]
    public DialogueData hostileDialogue;

    // ── Capitaine de Port ─────────────────────────────────────
    // GDD v30 §19.3 — navigation bateau, départ toutes les 5-15 min
    [Header("Capitaine de Port (PNJType.HarborMaster)")]
    public List<string> availableDestinations = new List<string>(); // IDs de zones
    [Tooltip("Intervalle de départ en secondes (défaut : 5-15 min)")]
    public float departureIntervalMin = 300f;
    public float departureIntervalMax = 900f;

    // ── Respawn ───────────────────────────────────────────────
    [Header("Respawn")]
    [Tooltip("Délai de respawn en secondes après mort (Garde uniquement). 0 = jamais.")]
    public float respawnDelay = 60f;

    // ── Stats de base (Garde) ─────────────────────────────────
    [Header("Stats (Garde uniquement)")]
    public float baseMaxHP   = 500f;
    public float baseMaxMana = 100f;
    [Tooltip("Regen HP/s du Garde — GDD §4.2 exception : le Garde régénère entre les combats")]
    public float baseRegenHP = 5f;

    // ── Stats de combat — même pipeline que Mob/Joueur (§21.2) ──
    // Tous les PNJ ont des stats — Guards protègent le village,
    // mais Marchands/Maires/etc. peuvent aussi mourir (village envahi)
    [Header("Combat")]
    [Tooltip("Défense contre les attaques de mêlée")]
    public float meleeDefense  = 10f;
    [Tooltip("Défense contre les attaques à distance")]
    public float rangedDefense = 8f;
    [Tooltip("Défense contre les attaques magiques")]
    public float magicDefense  = 5f;
    [Tooltip("Précision — entre dans le calcul Miss/Dodge (§6.10)")]
    public float precision     = 10f;
    [Tooltip("Esquive — entre dans le calcul Miss/Dodge (§6.10)")]
    public float dodge         = 5f;
}

// ── Types de PNJ — GDD v30 §19.2 ─────────────────────────────
public enum PNJType
{
    Merchant,       // Achat/vente items consommables et ressources
    Blacksmith,     // Amélioration arme/armure (+0→+3 Braven, +0→+10 Erenthal)
    Antiquarian,    // Identification et insertion de runes
    FusionNPC,      // Fusion de Gants & Bottes (S0→S6)
    CraftMaster,    // Déblocage activités (Bûcheron, Pêcheur...)
    Quest,          // Donneur de quêtes — conditions + récompenses
    Mayor,          // Création de guilde — dialogue conditionnel
    FactionNPC,     // Services et quêtes Solthars / Umbrans — zone faction uniquement
    HarborMaster,   // Navigation bateau — choix de destination
    Guard,          // Dialogue neutre + IA combat mobs proches
    Decorative,     // Ambiance, lore, rumeurs — pas de service
}

// ── Entrée de shop ────────────────────────────────────────────
[System.Serializable]
public class ShopEntry
{
    [Tooltip("Glisse le SO ici (WeaponData, ArmorData, ConsumableData, ResourceData, SkillData...)")]
    public ScriptableObject item;
    public int     aerisCost;
    [Tooltip("Rang de Réputation Monde minimum (0 = toujours visible)")]
    public int     requiredWorldReputationRank = 0;
    public bool    isUnlimitedStock = true;
    public int     stockCount = 1;
}

// ── Faction — GDD v30 §19.6 ───────────────────────────────────
public enum FactionType
{
    None,       // PNJ neutre — accessible à tous
    Solthars,   // PNJ Solthars — hostile aux Umbrans
    Umbrans,    // PNJ Umbrans — hostile aux Solthars
}