using UnityEngine;

// =============================================================
// RESOURCEDATA.CS — ScriptableObject template de ressource
// Path : Assets/Scripts/Data/Inventory/ResourceData.cs
// AetherTree GDD v3.1
//
// ResourceType détermine à la fois le type ET la source :
//   CraftMaterial  — bois, minerai, tissu, cuir... (loot ou node)
//   CookIngredient — ingrédients de cuisine (loot ou node)
//   MobDrop        — drop exclusif de mob (os, écailles...)
//   Collectible    — ramassable dans le monde via node 3D
//   Other          — ressource générique
//
// Une ressource Collectible utilise les champs "Node World"
// pour être spawnée par SpawnManager et collectée via clic.
// Une ressource MobDrop n'a pas de nodePrefab — elle ne spawn
// pas dans le monde, uniquement via LootTable.
//
// Note : CraftMaterial et CookIngredient peuvent être à la fois
// droppés par des mobs (LootTable) ET collectés dans le monde
// (SpawnManager) — il suffit de les ajouter aux deux.
// =============================================================

public enum ResourceType
{
    CraftMaterial,  // Matériaux de craft (bois, minerai, tissu...)
    CookIngredient, // Ingrédients de cuisine
    MobDrop,        // Drop exclusif de mob (os, écailles, fourrure...)
    Collectible,    // Ramassable dans le monde via node 3D
    Other,          // Ressource générique
}

[CreateAssetMenu(fileName = "Resource_", menuName = "AetherTree/Inventory/ResourceData")]
public class ResourceData : ScriptableObject
{
    // ── Identité ──────────────────────────────────────────────
    [Header("Identité")]
    public string       resourceName = "Resource";
    public ResourceType resourceType = ResourceType.CraftMaterial;
    public Sprite       icon;
    public GameObject   prefab;       // prefab objet au sol (WorldLootItem)
    [TextArea]
    public string       description  = "";

    // ── Stack ─────────────────────────────────────────────────
    [Header("Stack")]
    [Tooltip("Quantité max par stack dans l'inventaire.")]
    public int maxStack = 99;

    // ── Valeur ────────────────────────────────────────────────
    [Header("Valeur")]
    [Tooltip("Prix de vente de base en Aeris.")]
    public int sellPrice = 1;

    // ── Node World (Collectible uniquement) ───────────────────
    [Header("Node World (resourceType = Collectible)")]
    [Tooltip("Prefab 3D placé dans le monde (plante, rocher, arbre...).")]
    public GameObject nodePrefab;

    [Tooltip("Quantité minimum ramassée par collecte.")]
    [Min(1)] public int minQuantity = 1;

    [Tooltip("Quantité maximum ramassée par collecte.")]
    [Min(1)] public int maxQuantity = 1;

    [Tooltip("Durée de la barre de progression en secondes.")]
    [Min(0.1f)] public float collectTime = 5f;

    [Tooltip("Délai de réapparition du node après collecte.")]
    [Min(1f)] public float respawnDelay = 300f;

    [Tooltip("Distance max à laquelle le joueur peut collecter ce node.")]
    public float interactionRadius = 2.5f;

    // ── Utilitaires ───────────────────────────────────────────
    public bool IsCollectible => resourceType == ResourceType.Collectible;

    public ResourceInstance CreateInstance(int quantity = 1)
        => new ResourceInstance(this, Mathf.Clamp(quantity, 1, maxStack));
}

// =============================================================
// RESOURCEINSTANCE — données runtime d'une ressource
// =============================================================
[System.Serializable]
public class ResourceInstance
{
    public ResourceData data;
    public int          quantity = 1;

    public ResourceInstance(ResourceData source, int qty = 1)
    {
        data     = source;
        quantity = qty;
    }

    public string       Name       => data?.resourceName ?? "Resource";
    public Sprite       Icon       => data?.icon;
    public ResourceType Type       => data?.resourceType ?? ResourceType.Other;
    public int          MaxStack   => data?.maxStack     ?? 999;
    public int          SellPrice  => data?.sellPrice    ?? 1;
    public bool         IsEmpty    => quantity <= 0;

    public int Add(int amount)
    {
        int total = quantity + amount;
        quantity  = Mathf.Min(total, MaxStack);
        return Mathf.Max(0, total - MaxStack);
    }

    public bool Remove(int amount = 1)
    {
        if (quantity < amount) return false;
        quantity -= amount;
        return true;
    }
}
